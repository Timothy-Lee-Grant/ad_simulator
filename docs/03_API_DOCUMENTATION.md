# Mini Ad Server and Bidding Engine
## Complete API Documentation

**Version:** 1.0  
**Last Updated:** November 29, 2025  

---

## Table of Contents
1. [Overview](#overview)
2. [Authentication & Authorization](#authentication--authorization)
3. [Request/Response Format](#requestresponse-format)
4. [Ad Server Service](#ad-server-service)
5. [Bid Engine Service](#bid-engine-service)
6. [Analytics Service](#analytics-service)
7. [Error Handling](#error-handling)
8. [Rate Limiting](#rate-limiting)
9. [Monitoring Endpoints](#monitoring-endpoints)

---

## Overview

This document describes all REST API endpoints for the Mini Ad Server system. The system consists of three main services, each with specific responsibilities.

### Service Endpoints Summary

| Service | Base URL | Port | Purpose |
|---------|----------|------|---------|
| **Ad Server** | http://localhost:8080 | 8080 | Serves ads to clients |
| **Bid Engine** | http://localhost:8081 | 8081 | Determines winning campaigns |
| **Analytics** | http://localhost:8082 | 8082 | Reports performance metrics |

---

## Authentication & Authorization

**Current Implementation:** No authentication (suitable for internal services)

**Future Implementation (Production):**
```
Authorization: Bearer <JWT_TOKEN>

Claims:
- iss (issuer): ads-platform
- sub (subject): service-name
- iat (issued at): timestamp
- exp (expiration): timestamp
```

---

## Request/Response Format

### Headers
```http
Content-Type: application/json
Accept: application/json
X-Request-ID: <unique-request-id>  # Optional but recommended
X-Correlation-ID: <trace-id>        # For distributed tracing
```

### Timestamps
All timestamps are in ISO 8601 format (UTC):
```
2025-11-29T14:30:45.123Z
```

### HTTP Status Codes
```
200 OK                - Request successful
204 No Content        - Success but no content to return
400 Bad Request       - Invalid request parameters
404 Not Found         - Resource not found
409 Conflict          - Request conflicts with current state
500 Internal Error    - Server error
503 Service Unavailable - Temporary service issue
```

---

## Ad Server Service

The Ad Server is the primary entry point for ad requests from clients (websites, apps).

### GET /serve

**Purpose:** Serve an advertisement to a user

**Description:**
Returns an ad object containing creative content and tracking information. This is the main endpoint clients use when they need an ad to display.

**Request Parameters:**

| Parameter | Type | Required | Description | Example |
|-----------|------|----------|-------------|---------|
| userId | string | Yes | Unique user identifier | user-abc123 |
| placementId | string | Yes | Ad placement identifier | homepage_banner_728x90 |
| countryCode | string | No | ISO 3166-1 alpha-2 country code | US, GB, DE |
| deviceType | string | No | Device type | mobile, tablet, desktop |

**Request Example:**
```http
GET /serve?userId=user-abc123&placementId=homepage_banner&countryCode=US&deviceType=desktop HTTP/1.1
Host: ads.example.com
Accept: application/json
X-Request-ID: req-uuid-12345
```

**Response (200 OK):**
```json
{
  "adId": "550e8400-e29b-41d4-a716-446655440000",
  "campaignId": "6ba7b810-9dad-11d1-80b4-00c04fd430c8",
  "title": "TechGear Pro - Advanced Features, Affordable Price",
  "imageUrl": "https://cdn.example.com/ads/campaign-456/techgear-hero.jpg",
  "redirectUrl": "https://techgear.example.com/pro?utm_source=ads&utm_campaign=campaign-456",
  "clickTrackingUrl": "https://ads.example.com/click?adId=550e8400-e29b-41d4-a716-446655440000&campaignId=6ba7b810-9dad-11d1-80b4-00c04fd430c8&userId=user-abc123",
  "bidPrice": 2.50,
  "impressionId": "imp-uuid-987654",
  "timestamp": "2025-11-29T14:30:45.123Z"
}
```

**Response (204 No Content):**
```http
HTTP/1.1 204 No Content
```
Returned when no eligible campaigns are available for bidding.

**Response (400 Bad Request):**
```json
{
  "error": "MISSING_REQUIRED_PARAMETER",
  "message": "userId and placementId are required",
  "requestId": "req-uuid-12345"
}
```

**Performance Characteristics:**
- **Target p95 Latency:** < 100ms
- **Target p99 Latency:** < 200ms
- **Typical breakdown:**
  - Cache/network: 5-10ms
  - Bid Engine call: 30-40ms
  - Database operations: 20-30ms
  - Serialization/response: 5-10ms
  - Event publishing (async): < 10ms

**Code Examples:**

**JavaScript:**
```javascript
fetch('http://ads.example.com/serve?userId=user-123&placementId=banner&countryCode=US&deviceType=mobile', {
  method: 'GET',
  headers: {
    'Accept': 'application/json',
    'X-Request-ID': 'req-' + Math.random()
  }
})
.then(res => {
  if (res.status === 204) {
    console.log('No ad available');
    return;
  }
  return res.json();
})
.then(ad => {
  if (ad) {
    // Display ad
    document.getElementById('ad-container').innerHTML = `
      <a href="${ad.clickTrackingUrl}">
        <img src="${ad.imageUrl}" alt="${ad.title}">
      </a>
    `;
  }
})
.catch(err => console.error('Ad request failed:', err));
```

**Python:**
```python
import requests
import json

response = requests.get(
    'http://ads.example.com/serve',
    params={
        'userId': 'user-123',
        'placementId': 'banner',
        'countryCode': 'US',
        'deviceType': 'mobile'
    },
    headers={
        'Accept': 'application/json',
        'X-Request-ID': 'req-12345'
    }
)

if response.status_code == 200:
    ad = response.json()
    print(f"Ad: {ad['title']}")
    print(f"Image: {ad['imageUrl']}")
elif response.status_code == 204:
    print("No ad available")
else:
    print(f"Error: {response.status_code}")
```

**cURL:**
```bash
curl -X GET 'http://localhost:8080/serve?userId=user-123&placementId=banner&countryCode=US&deviceType=mobile' \
  -H 'Accept: application/json' \
  -H 'X-Request-ID: req-12345' \
  -v
```

---

### POST /click

**Purpose:** Record a click event when user interacts with an ad

**Description:**
Logs a click event for analytics. Called when user clicks on the ad or via server-side redirect. Should be called before redirecting user to destination URL.

**Request Body:**
```json
{
  "adId": "550e8400-e29b-41d4-a716-446655440000",
  "campaignId": "6ba7b810-9dad-11d1-80b4-00c04fd430c8",
  "userId": "user-abc123",
  "placementId": "homepage_banner",
  "timestamp": "2025-11-29T14:30:47.456Z"
}
```

**Request Example:**
```http
POST /click HTTP/1.1
Host: ads.example.com
Content-Type: application/json
X-Request-ID: req-uuid-click-001

{
  "adId": "550e8400-e29b-41d4-a716-446655440000",
  "campaignId": "6ba7b810-9dad-11d1-80b4-00c04fd430c8",
  "userId": "user-abc123",
  "placementId": "homepage_banner",
  "timestamp": "2025-11-29T14:30:47.456Z"
}
```

**Response (204 No Content):**
```http
HTTP/1.1 204 No Content
X-Request-ID: req-uuid-click-001
```

**Response (400 Bad Request):**
```json
{
  "error": "INVALID_CLICK_DATA",
  "message": "adId, campaignId, and userId are required",
  "requestId": "req-uuid-click-001"
}
```

**Performance Characteristics:**
- **Target Latency:** < 50ms
- **Operations:**
  - Validate request: 2ms
  - Publish to Kafka: 20-30ms (async)
  - Send response: 5ms

**Code Examples:**

**JavaScript:**
```javascript
function trackClick(ad) {
  const clickData = {
    adId: ad.adId,
    campaignId: ad.campaignId,
    userId: 'user-123',
    placementId: 'homepage_banner',
    timestamp: new Date().toISOString()
  };
  
  // Fire tracking request (don't wait for response)
  fetch('http://ads.example.com/click', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(clickData),
    keepalive: true  // Ensure request completes even if tab closes
  }).catch(err => console.warn('Click tracking failed:', err));
  
  // Redirect user after publishing click event
  window.location.href = ad.redirectUrl;
}
```

**Python:**
```python
import requests
import json

click_event = {
    'adId': '550e8400-e29b-41d4-a716-446655440000',
    'campaignId': '6ba7b810-9dad-11d1-80b4-00c04fd430c8',
    'userId': 'user-abc123',
    'placementId': 'homepage_banner',
    'timestamp': '2025-11-29T14:30:47.456Z'
}

response = requests.post(
    'http://ads.example.com/click',
    json=click_event,
    headers={'X-Request-ID': 'req-click-001'},
    timeout=5
)

if response.status_code == 204:
    print("Click recorded successfully")
```

---

### GET /health

**Purpose:** Health check endpoint for load balancers

**Request:**
```http
GET /health HTTP/1.1
Host: ads.example.com
```

**Response (200 OK):**
```json
{
  "status": "healthy",
  "timestamp": "2025-11-29T14:30:45.123Z",
  "services": {
    "database": "connected",
    "redis": "connected",
    "kafka": "connected"
  }
}
```

---

## Bid Engine Service

Internal service called by Ad Server to determine winning campaigns.

### POST /api/bid

**Purpose:** Evaluate bid requests and return winning campaign

**Description:**
Called by Ad Server for each ad request. Returns the winning campaign/ad based on highest CPM bid and targeting rules.

**Request Body:**
```json
{
  "userId": "user-abc123",
  "placementId": "homepage_banner_728x90",
  "countryCode": "US",
  "deviceType": "desktop",
  "userAttributes": {
    "age": 28,
    "interests": ["technology", "gadgets"],
    "purchaseHistory": ["electronics"],
    "daysSincePurchase": 45
  }
}
```

**Request Example:**
```http
POST /api/bid HTTP/1.1
Host: bid-engine.example.com
Content-Type: application/json
Accept: application/json

{
  "userId": "user-abc123",
  "placementId": "homepage_banner_728x90",
  "countryCode": "US",
  "deviceType": "desktop",
  "userAttributes": {
    "interests": ["technology"]
  }
}
```

**Response (200 OK):**
```json
{
  "campaignId": "6ba7b810-9dad-11d1-80b4-00c04fd430c8",
  "adId": "550e8400-e29b-41d4-a716-446655440000",
  "bidPrice": 2.50,
  "adContent": {
    "title": "TechGear Pro - Advanced Features",
    "imageUrl": "https://cdn.example.com/ads/campaign-456/image.jpg",
    "redirectUrl": "https://techgear.example.com/pro?utm_source=ads"
  },
  "confidence": 0.95
}
```

**Response (204 No Content):**
```http
HTTP/1.1 204 No Content
```
No eligible campaign found.

**Response (400 Bad Request):**
```json
{
  "error": "INVALID_REQUEST",
  "message": "userId and placementId are required",
  "requestId": "req-uuid-12345"
}
```

**Performance Characteristics:**
- **Target p95 Latency:** < 50ms
- **Breakdown:**
  - Cache lookup: 2-3ms
  - Targeting filter: 5-10ms
  - Bid selection: 2-3ms
  - Response serialization: 2-3ms

**Algorithm Details:**

```
1. Load all active campaigns from cache (2-3ms)
   - Cache key: "campaigns::active::all"
   - TTL: 5 minutes
   - Fallback: Query database if cache miss

2. Filter campaigns by targeting rules (5-10ms)
   - Check country rules
   - Check device type rules
   - Check interest rules (if provided)
   - Check budget available (SpentToday < DailyBudget)

3. Sort by CPM bid in descending order (1-2ms)
   - Highest CPM wins

4. Select random ad from winning campaign (1ms)

5. Return winning bid with ad content
```

---

## Analytics Service

External API for retrieving aggregated campaign performance metrics.

### GET /analytics/campaign/{campaignId}

**Purpose:** Get performance metrics for a specific campaign

**Description:**
Returns daily and period-level aggregated metrics for a campaign. Data comes from the daily_metrics table updated by Event Consumer.

**Path Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| campaignId | UUID | Yes | Campaign identifier |

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| startDate | Date | 30 days ago | Start date (YYYY-MM-DD) |
| endDate | Date | Today | End date (YYYY-MM-DD) |
| groupBy | string | day | Aggregation level: day, week, month |

**Request Example:**
```http
GET /analytics/campaign/6ba7b810-9dad-11d1-80b4-00c04fd430c8?startDate=2025-11-01&endDate=2025-11-29&groupBy=day HTTP/1.1
Host: analytics.example.com
Accept: application/json
```

**Response (200 OK):**
```json
{
  "campaignId": "6ba7b810-9dad-11d1-80b4-00c04fd430c8",
  "campaignName": "TechGear Pro Campaign",
  "dateRange": {
    "start": "2025-11-01",
    "end": "2025-11-29",
    "days": 29
  },
  "summary": {
    "totalImpressions": 2500000,
    "totalClicks": 62500,
    "ctr": 2.5,
    "totalSpend": 6250.00,
    "cpc": 0.10,
    "cpm": 2.50,
    "averagePosition": 1.0
  },
  "dailyMetrics": [
    {
      "date": "2025-11-01",
      "impressions": 85000,
      "clicks": 2125,
      "ctr": 2.5,
      "spend": 212.50,
      "cpc": 0.10
    },
    {
      "date": "2025-11-02",
      "impressions": 90000,
      "clicks": 2250,
      "ctr": 2.5,
      "spend": 225.00,
      "cpc": 0.10
    }
    // ... more days
  ],
  "topPlacements": [
    {
      "placementId": "homepage_banner",
      "impressions": 1000000,
      "ctr": 2.8,
      "spend": 2500.00
    }
  ]
}
```

**Metric Definitions:**

| Metric | Formula | Interpretation |
|--------|---------|-----------------|
| **Impressions** | Count of served ads | Total times ad was shown |
| **Clicks** | Count of click events | Total times users clicked ad |
| **CTR** | (Clicks / Impressions) × 100 | Engagement rate (%) |
| **Spend** | (Impressions / 1000) × CPM | Total cost in dollars |
| **CPC** | Spend / Clicks | Cost per user click |
| **CPM** | Bid price | Cost per 1000 impressions |

**Response (404 Not Found):**
```json
{
  "error": "CAMPAIGN_NOT_FOUND",
  "message": "Campaign with ID 6ba7b810-9dad-11d1-80b4-00c04fd430c8 not found",
  "requestId": "req-uuid-12345"
}
```

**Performance Characteristics:**
- **Target Latency:** < 500ms
- **Breakdown:**
  - Cache check (Redis): 1-2ms
  - Database query (PostgreSQL): 50-100ms
  - Aggregation/calculation: 10-50ms
  - Response serialization: 5-10ms

**Caching Strategy:**
- **Key:** `analytics::{campaignId}::{date}`
- **TTL:** 1 hour
- **Cache Invalidation:** Automatic on TTL or manual after Consumer writes

**Code Examples:**

**JavaScript:**
```javascript
async function getAnalytics(campaignId) {
  const response = await fetch(
    `http://analytics.example.com/analytics/campaign/${campaignId}?startDate=2025-11-01&endDate=2025-11-29`,
    {
      headers: {
        'Accept': 'application/json'
      }
    }
  );
  
  if (!response.ok) {
    throw new Error(`Analytics API error: ${response.status}`);
  }
  
  return await response.json();
}

getAnalytics('6ba7b810-9dad-11d1-80b4-00c04fd430c8')
  .then(data => {
    console.log(`CTR: ${data.summary.ctr}%`);
    console.log(`Spend: $${data.summary.totalSpend}`);
    console.log(`Daily metrics:`, data.dailyMetrics);
  })
  .catch(err => console.error('Failed to fetch analytics:', err));
```

**Python:**
```python
import requests
from datetime import datetime, timedelta

def get_campaign_analytics(campaign_id):
    end_date = datetime.now().date()
    start_date = end_date - timedelta(days=29)
    
    response = requests.get(
        f'http://analytics.example.com/analytics/campaign/{campaign_id}',
        params={
            'startDate': start_date.isoformat(),
            'endDate': end_date.isoformat(),
            'groupBy': 'day'
        }
    )
    
    if response.status_code == 200:
        data = response.json()
        print(f"Campaign: {data['campaignName']}")
        print(f"Total Impressions: {data['summary']['totalImpressions']:,}")
        print(f"Total Clicks: {data['summary']['totalClicks']:,}")
        print(f"CTR: {data['summary']['ctr']:.2f}%")
        print(f"Total Spend: ${data['summary']['totalSpend']:.2f}")
        return data
    else:
        print(f"Error: {response.status_code}")
        return None

analytics = get_campaign_analytics('6ba7b810-9dad-11d1-80b4-00c04fd430c8')
```

---

### GET /analytics/campaigns

**Purpose:** List all campaigns with summary metrics

**Description:**
Returns a list of all campaigns with current performance metrics. Useful for dashboards and reporting.

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| status | string | active | Filter by status: active, paused, ended |
| sortBy | string | spend | Sort field: spend, ctr, impressions |
| order | string | desc | Sort order: asc, desc |
| limit | integer | 100 | Max results to return |

**Request Example:**
```http
GET /analytics/campaigns?status=active&sortBy=spend&order=desc&limit=50 HTTP/1.1
Host: analytics.example.com
Accept: application/json
```

**Response (200 OK):**
```json
{
  "campaigns": [
    {
      "campaignId": "6ba7b810-9dad-11d1-80b4-00c04fd430c8",
      "campaignName": "TechGear Pro Campaign",
      "status": "active",
      "dailyMetrics": {
        "impressions": 85000,
        "clicks": 2125,
        "ctr": 2.5,
        "spend": 212.50
      },
      "budgetInfo": {
        "dailyBudget": 1000.00,
        "spentToday": 212.50,
        "budgetRemaining": 787.50
      }
    }
    // ... more campaigns
  ],
  "total": 15,
  "page": 1,
  "pageSize": 100
}
```

---

## Error Handling

All errors follow a consistent format:

```json
{
  "error": "ERROR_CODE",
  "message": "Human-readable error message",
  "requestId": "req-uuid-12345",
  "timestamp": "2025-11-29T14:30:45.123Z",
  "details": {
    "field": "parameter_name",
    "issue": "Specific issue description"
  }
}
```

**Common Error Codes:**

| Code | HTTP Status | Meaning |
|------|------------|---------|
| `INVALID_REQUEST` | 400 | Missing or invalid parameters |
| `MISSING_REQUIRED_PARAMETER` | 400 | Required parameter not provided |
| `CAMPAIGN_NOT_FOUND` | 404 | Campaign does not exist |
| `SERVICE_UNAVAILABLE` | 503 | Database or external service down |
| `RATE_LIMIT_EXCEEDED` | 429 | Too many requests |
| `INTERNAL_ERROR` | 500 | Unexpected server error |

---

## Rate Limiting

**Ad Server:**
- Soft limit: 100k requests/second per IP
- Hard limit: 200k requests/second per IP
- Response header: `X-RateLimit-Remaining: 95432`

**Bid Engine:**
- No rate limit (internal service only)

**Analytics:**
- Limit: 1000 requests/minute per API key
- Response header: `X-RateLimit-Limit: 1000, X-RateLimit-Remaining: 950`

---

## Monitoring Endpoints

### GET /metrics (Prometheus format)

**Available on all services at `:9090/metrics`**

**Example Metrics:**

```
# Ad Server Metrics
http_requests_total{service="ad_server",endpoint="/serve",status="200"} 1250000
http_request_duration_seconds{service="ad_server",endpoint="/serve",le="0.1"} 1200000
http_request_duration_seconds{service="ad_server",endpoint="/serve",le="0.2"} 1245000

# Bid Engine Metrics
bid_requests_total{status="success"} 1250000
bid_requests_total{status="no_bid"} 50000
bid_latency_seconds{quantile="0.95"} 0.045
bid_latency_seconds{quantile="0.99"} 0.085

# Kafka Metrics
kafka_consumer_lag{topic="impressions",partition="0"} 1250
kafka_producer_send_total{topic="impressions"} 2500000
```

### GET /health

Health check returning service status and dependencies.

---

## Pagination

For list endpoints, use cursor-based pagination:

```http
GET /analytics/campaigns?limit=50&cursor=abc123def456 HTTP/1.1
```

Response includes next cursor:
```json
{
  "campaigns": [...],
  "pagination": {
    "limit": 50,
    "cursor": "abc123def456",
    "nextCursor": "def456ghi789",
    "hasMore": true
  }
}
```

---

## Best Practices

### For Clients

1. **Use X-Request-ID header** for tracing
2. **Handle 204 responses** (no ad available)
3. **Set reasonable timeouts** (100-500ms)
4. **Cache responses** where appropriate
5. **Implement exponential backoff** for retries
6. **Monitor error rates** and alert on anomalies

### For Operators

1. **Monitor endpoint latencies** with Prometheus
2. **Set up alerts** for error rate increases
3. **Check Kafka lag** regularly
4. **Verify database connectivity** in health checks
5. **Review query plans** for slow endpoints
6. **Implement circuit breakers** for failing services

---

## Glossary

**CPM:** Cost Per Mille - price paid per 1,000 ad impressions
**CPC:** Cost Per Click - price paid per user click
**CTR:** Click-Through Rate - percentage of impressions that result in clicks
**Impression:** Event when ad is served to and viewed by a user
**Click:** Event when user interacts with an ad
**Placement:** Ad slot on a website or app (e.g., "homepage_banner_728x90")
**Campaign:** Advertiser's marketing effort with budget, bid, and targeting
**Bid:** CPM amount campaign will pay for impressions

