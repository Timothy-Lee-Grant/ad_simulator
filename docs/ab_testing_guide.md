# A/B Testing Guide for ad_simulator (Entry-level friendly)

This guide explains how to set up, run, and analyze A/B tests (experiments) for different bid selection algorithms in this project. It includes conceptual explanation, step-by-step instructions, the code I added, and recommended next steps and best practices.

---

## 🎯 What is A/B testing (in plain terms)
- A/B testing is a method to compare two (or more) variations of a system to determine which performs better for a chosen metric (e.g., CTR, revenue, conversion).
- You split traffic between variants (Control = A, Treatment = B), measure key metrics, and use statistical tests to decide whether B is better than A.
- Key ideas: deterministic assignment per user (stickiness), metric instrumentation, sufficient sample size, and pre-defined stopping criteria.

---

## ✅ Summary of the changes I made for A/B testing
I implemented a basic A/B framework and added tests so you can get started immediately.

Files added/changed:
- src/BidEngine/Services/IExperimentService.cs — interface for experiment assignment
- src/BidEngine/Services/HashExperimentService.cs — deterministic, hash-based assignment (simple 50/50 by default)
- src/BidEngine/Services/BidSelector.cs — updated to consult the experiment service and increment a basic Prometheus counter when assigning users to variants
- src/BidEngine/Program.cs — registered the `HashExperimentService` in DI
- tests/BidEngine.Tests/Services/HashExperimentServiceTests.cs — tests for assignment
- tests/BidEngine.Tests/Services/BidSelectorTests.cs — tests verifying routing to variant A/B

Notes: the `BidSelector` already had two algorithms (Algorithm1 and Algorithm2). I wired the experiment service so it deterministically selects A or B for each request (based on `UserId` or `PlacementId`). I also added a simple Prometheus counter label for `experiment` and `variant` to make it easy to count assignments.

---

## Step-by-step guide (how this works and how to use it)
This section walks you (step-by-step) through the implementation and how to run and measure an experiment.

### 1) Decide your experiment goal and metric
- Pick a single primary metric (e.g., click-through rate (CTR), fill rate, revenue per mille (RPM), success rate of budget deduction).
- Define a hypothesis, e.g.: "Algorithm B will increase CTR by at least 5% vs Algorithm A on homepage placement."

Why: experiments without a clear primary metric and hypothesis are hard to interpret.

### 2) Ensure deterministic, sticky assignment
- We added `IExperimentService` and a `HashExperimentService` that deterministically assigns a user to variant A or B based on a hash of `experimentName + identity`.
- This ensures the same user will always be assigned to the same variant (stickiness), which avoids cross-contamination.

Code (already added):

```csharp
public interface IExperimentService
{
    string GetVariant(string experimentName, string identity);
}

// HashExperimentService: deterministic SHA256-based bucket 0-99
var svc = new HashExperimentService();
var variant = svc.GetVariant("bid-selector", "user123"); // "A" or "B"
```

### 3) Route requests to different algorithms
- Modify the code that chooses the bidding algorithm to consult `IExperimentService` and then call Algorithm A or B accordingly. I updated `BidSelector.SelectWinningBidAsync` to use the experiment service and increment an `ab_experiment_assignments` Prometheus counter with labels `experiment` and `variant`.

Key points:
- Use `UserId` as the primary identity for assignment; fallback to `PlacementId` or `anonymous` if needed.
- Keep the routing logic small and deterministic.

### 4) Instrument outcome metrics
- You need to measure user-visible and business metrics. Examples:
  - `impression` counter (labels: placement, variant)
  - `click` counter (placement, variant)
  - `win_rate` or `budget_deduction_success` counters
  - `revenue` (histogram or summary)

Where to record:
- Use Prometheus counters (we already use Prometheus in the project). Increment counters at the points where the events occur (e.g., when an ad is actually returned, when a click is recorded, when budget deduction succeeds).
- Ensure each metric includes the `variant` label so you can compare A vs B directly.

Example (pseudo):
```csharp
Metrics.CreateCounter("impressions", "Impression count", new CounterConfiguration { LabelNames = new[]{"variant","placement"}})
    .WithLabels(variant, placement).Inc();
```

### 5) Run the experiment and collect data
- For example start with 50/50 split.
- Run until you reach predetermined sample size or time window.
- Check Prometheus (Grafana) dashboards for metrics by variant: A and B.

### 6) Analyze results (basic statistical sanity)
- For success/failure metrics (binary outcomes), use a two-proportion z-test to compare rates.
- For continuous metrics (revenue), use t-tests or non-parametric tests depending on distribution.
- Consider implementing a small analysis notebook (Python) that pulls metrics and runs the tests.

Important: Predefine the stopping rule and do not peek too often to avoid false positives (type I error inflation). A simple approach is to run for a fixed time (e.g., one week) or until a minimum sample size is reached.

### 7) Rollout & experiment lifecycle
- If B is significantly better, gradually increase percentage traffic to B (e.g., 25% -> 50% -> 100%) or switch default.
- If B is worse, roll back to A and analyze logs to find the cause.

---

## Code-level: How to extend and improve the experiment framework
Here are recommended improvements and best practices for production-grade experiments.

1) Config-driven experiments
- Allow experiment allocations (A=40, B=60) to be configured via `appsettings.json` or a feature management system.
- Example setting:
```json
"Experiments": {
  "bid-selector": { "A": 50, "B": 50 }
}
```
Then read into `HashExperimentService` in the constructor.

2) Persist assignments (optional)
- Hash-based assignment is stateless and stable, but if you change allocation percentages you may want to persist prior assignments (in Redis or DB) to keep previously assigned users in their original variants.
- Implement a `RedisBackedExperimentService` that checks Redis for an existing assignment first, else computes and stores it.

3) More sophisticated traffic routing
- Use gradual rollouts (ramp-up) and rollout tags.

4) Robust metrics & tagging
- Tag metrics with `variant`, `placement`, `campaignId`, and other helpful labels.
- Keep label dimension count small to avoid high cardinality in the metric store.

5) Experiment dashboards & analysis
- Create Grafana dashboard panels for `variant` comparisons for the primary metric and secondary metrics.
- Export raw counts periodically for offline analysis.

6) Safety: Keep fail-safe defaults
- If experiment service fails or misconfigures, fall back to the control algorithm.

---

## Tests & CI
- I added unit tests to ensure deterministic assignment and routing from `BidSelector` to the correct algorithm.
- Recommended additional tests:
  - Integration test with Redis-backed assignment (if you implement it).
  - End-to-end test that validates metrics are incremented with correct `variant` labels.

CI notes:
- The existing GitHub Actions workflow runs unit tests on push/PR and has an optional integration job for Redis/Postgres.
- When adding integration tests, ensure they are stable and idempotent before enabling them for automatic PR runs.

---

## Minimal code examples (already added to the repo)
- `IExperimentService` (interface)
- `HashExperimentService` (sha256-based deterministic bucket)

Example of variant assignment usage in `BidSelector`:
```csharp
var identity = request.UserId ?? request.PlacementId ?? "anonymous";
var variant = _experiments.GetVariant("bid-selector", identity);
// route to algorithm
if (variant == "B") return await SelectWinningBidAsyncAlgorithm2(request);
return await SelectWinningBidAsyncAlgorithm1(request);
```

---

## Step-by-step tasks for you (concrete)
1. Review the code I added:
   - `src/BidEngine/Services/{IExperimentService,HashExperimentService}.cs`
   - `src/BidEngine/Services/BidSelector.cs` (changed to consult experiment service)
2. Run tests locally:
   - `dotnet test` (should pass; we added tests for experiment assignment)
3. Instrument additional metrics you care about (e.g., impressions, clicks). Add `variant` label on those metrics.
4. Create a Grafana dashboard comparing A vs B for primary metric.
5. Run a small canary experiment: start with 10% traffic to B, monitor, then increase.
6. When ready, switch to 50/50 split by configuring `HashExperimentService` (or config-driven version) and run the experiment for sufficient time to collect data.
7. Analyze results with a statistical test (you can use a notebook or a small script). If significant, roll out the winner.

---

## Recommended repository layout changes (optional)
- Consider extracting bidding strategies behind an `IBidSelectionStrategy` interface and register multiple implementations. This makes experimenting easier and isolates implementations.
- Add `tests/Helpers` with builders (CampaignBuilder, AdBuilder) and a test experiment service so unit tests can simulate assignments deterministically.
- Add a `metrics/` folder or `Observability` area for instrumentation helpers.

---

## Helpful resources to learn more
- Online A/B testing tutorials (Google's A/B testing guide, Evan Miller's A/B testing statistics)
- Prometheus + Grafana docs
- TestContainers for reliable integration testing of Redis/Postgres in CI

---

## Wrap-up
This initial A/B framework is intentionally simple and designed for learning and iteration. It provides deterministic assignments, variant routing, and basic assignment metrics. When you're comfortable with this setup, we can:
- Add configuration-driven allocations
- Persist assignments in Redis for stickiness across allocation changes
- Add dashboards and statistical analysis notebooks
- Add safety/rollout automation to CI

If you'd like, I can implement the next incremental improvements (config-driven allocations and a Redis-backed assignment option) and add integration tests that run in the CI integration job.

---

*Document created by GitHub Copilot (Raptor mini (Preview)) on 2025-12-18.*
