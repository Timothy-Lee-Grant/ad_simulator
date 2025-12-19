#!/usr/bin/env bash
set -euo pipefail

BASE_URL_FRONTEND="http://localhost:3001"
BASE_URL_BIDENGINE="http://localhost:8081"

echo "Starting E2E smoke checks against frontend=$BASE_URL_FRONTEND and bid-engine=$BASE_URL_BIDENGINE"

wait_for() {
  local url=$1
  local retries=30
  local wait=2
  echo "Waiting for $url to be available..."
  for i in $(seq 1 $retries); do
    if curl -sSf "$url" >/dev/null 2>&1; then
      echo "$url is up"
      return 0
    fi
    sleep $wait
  done
  echo "Timed out waiting for $url" >&2
  return 1
}

# 1. Ensure services are responsive
wait_for "$BASE_URL_BIDENGINE/metrics"
wait_for "$BASE_URL_FRONTEND/"

# 2. Check homepage contents
echo "Checking homepage contents..."
html=$(curl -sSf "$BASE_URL_FRONTEND/")
if ! echo "$html" | grep -q "Featured Ads"; then
  echo "Homepage did not contain expected 'Featured Ads' text" >&2
  exit 2
fi

# 3. Parse ad click href
echo "Parsing first ad click href..."
# Refined regex: excludes double quotes, single quotes (\x27), and brackets
href=$(echo "$html" | grep -oE '/click\?[^"\x27> ]+' | head -n1 || true)

if [ -z "$href" ]; then
  echo "No ad click href found on homepage" >&2
  exit 3
fi

# 4. Trigger the click
echo "Triggering click endpoint: $BASE_URL_FRONTEND${href}"
status=$(curl -s -o /dev/null -w "%{http_code}" "$BASE_URL_FRONTEND${href}")
if [ "$status" != "302" ]; then
  echo "Click endpoint did not return 302 (got $status)" >&2
  exit 4
fi

echo "Waiting briefly for metrics to update..."
sleep 1

# 5. Verify Metrics
echo "Checking ad_clicks_total metric on bid engine..."
metric_output=$(curl -sSf "$BASE_URL_BIDENGINE/metrics")

# Extract numeric values for ad_clicks_total
max=$(echo "$metric_output" | awk '/^ad_clicks_total\{/ {print $2}' | awk 'BEGIN{m=0} {if($1+0>m) m=$1} END{print m+0}')

# FIX: Use -v to pass shell variable to awk to prevent parenthesis syntax errors
if [ -n "$max" ] && awk -v m="$max" 'BEGIN { exit !(m > 0) }'; then
  echo "ad_clicks_total increased to $max — OK"
else
  echo "ad_clicks_total not incremented (value: $max)" >&2
  exit 5
fi

echo "E2E smoke tests passed ✅"