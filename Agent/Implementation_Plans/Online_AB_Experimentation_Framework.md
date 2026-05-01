# Online A/B Experimentation Framework for Bidding

## Overview

This implementation plan defines a complete online A/B experimentation framework for the BidEngine bidding path. The goal is to enable deterministic user bucketing, experiment configuration, exposure logging, experiment outcome tracking, and Prometheus/Grafana metrics for campaign performance comparison.

The resulting system should be production-ready, easy to extend for new experiments, and safe to run without destabilizing the existing bid path.

## Objectives

- Add deterministic experiment assignment at request time.
- Keep experiments stable for each user across repeated requests.
- Log exposures and outcomes clearly for later analysis.
- Integrate experiment metadata into bidding and metrics.
- Support admin configuration for experiments and variations.
- Add observability in Prometheus for exposures, outcomes, and experiment performance.
- Maintain a minimal impact on latency for the bid path.

## Success Criteria

- Configured experiments can be enabled/disabled without code changes.
- A user is consistently assigned to the same variation for a given experiment.
- Bid selection can be influenced by experiment variation (e.g., highest CPM vs. hybrid strategy).
- Exposure and outcome events are logged with experiment identifiers.
- Metrics are exported to Prometheus with experiment labels.
- End-to-end tests verify assignment, logging, and strategy selection.

## Architecture

### Key components

- `ExperimentDefinition` / `ExperimentVariation` domain model
- `IExperimentAssignmentService` for deterministic bucketing
- `IExperimentService` for runtime experiment configuration and lookup
- `IExperimentLogger` or experiment event dispatcher for exposure/outcome events
- `ExperimentAwareBidSelector` or extension to `BidSelector`
- Admin experiment management API endpoints
- Prometheus metrics instrumentation for experiment events

### Data flow

1. Bid request enters the bidding API.
2. The system evaluates the current experiment configuration.
3. A deterministic assignment is made based on user identity, session, or request attributes.
4. The assigned variation is stored in request context for the current request.
5. Bid selection logic reads the experiment variation and applies the corresponding bidding policy.
6. Exposure and outcome events are logged and emitted to metrics.

## Design Details

### Experiment definition

Create a strongly typed experiment configuration model used by both runtime code and admin APIs.

- `ExperimentDefinition`
  - `Id` (string)
  - `Name` (string)
  - `Description` (string)
  - `Enabled` (bool)
  - `TargetingRules` (optional) - rules used to qualify requests
  - `Variations` (list of `ExperimentVariation`)
  - `DefaultVariation` (string)
  - `TrafficAllocation` (0-100)
  - `Seed` (optional string)
  - `StartTime` / `EndTime`

- `ExperimentVariation`
  - `Id` (string)
  - `Name` (string)
  - `Description` (string)
  - `Weight` (int)
  - `Strategy` (optional string)
  - `Metadata` (optional dictionary)

- `ExperimentAssignment`
  - `ExperimentId` (string)
  - `VariationId` (string)
  - `AssignedAt` (DateTime)
  - `BucketKey` / `UserId`

### Deterministic bucketing

Implement a stable bucketing algorithm so the same user gets the same variation across calls.

- Use a hash function such as SHA256 or MurmurHash over:
  - experiment ID
  - user ID
  - optional request fingerprint (device/session)
  - experiment seed
- Map the hash value to a uniform bucket value [0, 100).
- Use the variation weights to select a variation.
- If the user is outside traffic allocation, assign `control` or `not-in-experiment`.
- Ensure bucket assignment is independent of ordering and stable across config reloads.

### Experiment configuration storage

Support two levels of configuration:

- `appsettings.json` / environment variables for local defaults
- optional persisted experiment store (database table or JSON file)

For Phase 2 implementation, start with configuration in `appsettings.json` and `IConfiguration`, then add a future-proof `IExperimentConfigurationProvider` if required.

### Experiment service contract

Create a service interface for experiment evaluation and assignment.

- `IExperimentService`
  - `Task<ExperimentAssignment?> AssignVariationAsync(BidRequest request)`
  - `Task<ExperimentDefinition?> GetExperimentAsync(string experimentId)`
  - `Task<IEnumerable<ExperimentDefinition>> GetActiveExperimentsAsync()`
  - `Task<ExperimentResult> GetAssignmentForUserAsync(Guid userId, string experimentId)`

- `IExperimentAssignmentService`
  - `ExperimentAssignment AssignVariation(ExperimentDefinition experiment, string bucketKey)`

### Bidding strategy integration

Extend the existing bidding pipeline so experiments can influence policy selection.

- Add a runtime context object: `ExperimentContext`
  - `ExperimentId`
  - `VariationId`
  - `VariationName`
  - `IsExperiment` / `IsControl`
  - `ExperimentTags`

- Integrate the experiment context into the bid request or request-scoped services.
- Update `BidSelector` or create `ExperimentAwareBidSelector` to:
  - determine active variation for the current request
  - choose the appropriate bidding strategy based on variation
  - optionally attach experiment labels to returned bid metadata

- Example variations:
  - `control` -> existing highest CPM behavior
  - `hybrid` -> hybrid weighted strategy
  - `semantic` -> semantic-only strategy

- Keep current `IBiddingStrategy` implementations unchanged where possible.
- Add an `ExperimentStrategyMapper` service that maps experiment variation IDs to strategy names.

### Exposure and outcome logging

Log both exposure and outcome events with experiment metadata.

- `ExperimentExposureEvent`
  - `ExperimentId`
  - `VariationId`
  - `UserId` or `AnonymousId`
  - `CampaignId`
  - `BidId`
  - `Timestamp`
  - `RequestContext`

- `ExperimentOutcomeEvent`
  - `ExperimentId`
  - `VariationId`
  - `UserId`
  - `CampaignId`
  - `ExperimentMetricType` (`win`, `click`, `impression`, `conversion`, `revenue`)
  - `Value`
  - `Timestamp`

- Persist these event records to a dedicated table or emit them as structured logs.
- Optionally route events into Kafka or existing logging pipelines.

### Metrics and dashboarding

Expose Prometheus metrics for experiment performance.

- `experiment_exposures_total` with labels:
  - `experiment_id`
  - `variation_id`
  - `campaign_id`
  - `result` (`assigned`, `excluded`, `control`)

- `experiment_outcomes_total` with labels:
  - `experiment_id`
  - `variation_id`
  - `metric` (`bid_won`, `click`, `impression`, `conversion`)

- `experiment_revenue_total` with labels:
  - `experiment_id`
  - `variation_id`
  - `campaign_id`

- Add a Grafana dashboard section for experiment comparison.
- Instrument the bid path to increment counters when exposures and outcome events occur.

### Admin API

Add admin endpoints to manage and inspect experiments.

- `GET /api/admin/experiments`
- `GET /api/admin/experiments/{experimentId}`
- `POST /api/admin/experiments`
- `PUT /api/admin/experiments/{experimentId}`
- `DELETE /api/admin/experiments/{experimentId}`
- `GET /api/admin/experiments/{experimentId}/assignments?userId={userId}`
- `GET /api/admin/experiments/{experimentId}/metrics`

Include DTOs and validation for experiment creation and updates.

### Testing strategy

- Unit tests for bucket assignment stability and weight distribution.
- Unit tests for experiment service behavior with enabled/disabled, traffic allocation, and exclusion.
- Unit tests for bid selector variation mapping to strategy.
- Integration tests for the bid request flow with experiment context and metrics.
- Smoke test verifying an experiment config in `appsettings.json` produces consistent assignments and metrics.

## Implementation Steps

### Step 1: Define experiment models and configuration

1. Create `src/Shared/ExperimentDtos.cs` with:
   - `ExperimentDefinitionDto`
   - `ExperimentVariationDto`
   - `ExperimentAssignmentDto`
   - `ExperimentConfigDto`
   - `ExperimentMetricsDto`

2. Create `src/BidEngine/Models/ExperimentDefinition.cs` and `ExperimentVariation.cs` in `src/Shared` or `src/BidEngine/Models`.

3. Add configuration classes in `src/BidEngine/Configuration/ExperimentOptions.cs`.
   - Bind `ExperimentOptions` from configuration section `Experimentation`.
   - Add `IOptions<ExperimentOptions>` support in `Program.cs`.

4. Add default experiment config to `src/BidEngine/appsettings.json`:
   ```json
   "Experimentation": {
     "Experiments": [
       {
         "Id": "bidding_strategy_experiment",
         "Name": "Bidding Strategy Experiment",
         "Enabled": true,
         "TrafficAllocation": 100,
         "Seed": "bid_strategy_v1",
         "Variations": [
           { "Id": "control", "Name": "Control", "Weight": 50, "Strategy": "HighestCpm" },
           { "Id": "hybrid", "Name": "Hybrid Weighted", "Weight": 50, "Strategy": "HybridWeighted" }
         ]
       }
     ]
   }
   ```

### Step 2: Build assignment and configuration services

1. Create `src/BidEngine/Services/Interfaces/IExperimentConfigurationProvider.cs`.
2. Create `src/BidEngine/Services/ExperimentConfigurationProvider.cs`.
   - Reads from `ExperimentOptions`.
   - Validates required fields and ensures weights sum to 100.
   - Returns active experiment definitions.

3. Create `src/BidEngine/Services/Interfaces/IExperimentAssignmentService.cs`.
4. Create `src/BidEngine/Services/ExperimentAssignmentService.cs`.
   - Implement deterministic hashing.
   - Implement traffic allocation and weight-based variation selection.
   - Return `ExperimentAssignment` with `VariationId` and `VariationName`.

5. Create `src/BidEngine/Services/Interfaces/IExperimentService.cs`.
6. Create `src/BidEngine/Services/ExperimentService.cs`.
   - Expose methods for assignment lookup.
   - Expose methods for experiment metadata and active list.
   - Support `GetAssignmentForUserAsync(Guid userId, string experimentId)`.

7. Register services in `Program.cs`:
   - `builder.Services.Configure<ExperimentOptions>(builder.Configuration.GetSection("Experimentation"));`
   - `builder.Services.AddSingleton<IExperimentConfigurationProvider, ExperimentConfigurationProvider>();`
   - `builder.Services.AddScoped<IExperimentAssignmentService, ExperimentAssignmentService>();`
   - `builder.Services.AddScoped<IExperimentService, ExperimentService>();`

### Step 3: Add experiment context to bid requests

1. Create `src/BidEngine/Services/ExperimentContext.cs`.
2. Add `IExperimentContextAccessor` or use `HttpContext.Items` to store runtime assignment.
3. In the bid controller or middleware, resolve the experiment assignment for each request:
   - Evaluate the current experiment definitions.
   - Build a stable bucket key from request fields: authenticated user ID, IP address, session cookie, or device fingerprint.
   - Assign variation with `IExperimentAssignmentService`.

4. Store assigned variation in `ExperimentContext` for downstream services.

### Step 4: Integrate with bidding strategy selection

1. Update `BidSelector` or create a wrapper inside `BidSelector` to use experiment context.
2. Add service `IExperimentStrategyMapper` when needed.
3. Modify the bid selection flow:
   - If an active experiment assignment exists,
     - look up the requested strategy from the variation metadata,
     - select the corresponding `IBiddingStrategy` implementation.
   - Otherwise, fallback to existing default strategy.

4. Ensure `IBiddingStrategy` still receives the same `BidRequest` and returns the same bid contract.

### Step 5: Log exposures and outcomes

1. Create `src/Shared/ExperimentEvents.cs` or event DTOs.
2. Create `src/BidEngine/Services/Interfaces/IExperimentEventLogger.cs`.
3. Implement `ExperimentEventLogger` that:
   - writes structured data to the audit store or dedicated table
   - emits Prometheus counters
   - optionally writes events to Kafka via existing event producer

4. During assignment, log exposure once per request:
   - `ExperimentId`
   - `VariationId`
   - `UserId`
   - `CampaignId`
   - `RequestId`

5. During outcomes (bid won, impression tracked, click, conversion), log an outcome event.

6. If campaign analytics endpoints already exist, add experiment labels there.

### Step 6: Add Prometheus metrics

1. Add metric registration in `Program.cs` or instrumentation service.
2. Expose counters/histograms in the bid route:
   - `experiment_exposure_total`
   - `experiment_outcome_total`
   - `experiment_revenue_total`

3. Label metrics with experiment ID, variation ID, metric type, and campaign.
4. Add example Grafana panel definitions in documentation.

### Step 7: Admin API and management UI hooks

1. Add DTOs for experiment creation and updates.
2. Add `AdminExperimentsController` with route `/api/admin/experiments`.
3. Secure endpoints using `AdminOnly` policy.
4. Support experiment lifecycle operations:
   - create and update experiments
   - fetch current assignments
   - inspect variant weights and traffic allocation

5. Optionally, add a readonly endpoint for active variation assignment.

### Step 8: Testing and validation

1. Add `tests/BidEngine.Tests/Services/ExperimentAssignmentServiceTests.cs`:
   - verify stable assignment with fixed user IDs
   - verify weight allocation and distribution
   - verify assignment respects `Enabled=false`
   - verify bucket fallback when traffic allocation is partial

2. Add `tests/BidEngine.Tests/Services/ExperimentServiceTests.cs`:
   - verify config provider loads experiments
   - verify active list and experiment lookup
   - verify assignment persisted in context

3. Add `tests/BidEngine.Tests/Services/BidSelectorExperimentTests.cs`:
   - verify correct strategy is selected based on variation
   - verify default behavior when experiment inactive

4. Add integration/contract test for full bid request flow and metrics.
5. Add config binding tests for `ExperimentOptions` and env override behavior.

### Step 9: Deployment and configuration

1. Update `src/BidEngine/appsettings.json` with safe placeholders under `Experimentation` and no secrets.
2. Add `.env.example` entries for any experiment-related environment variables, e.g. `Experimentation__Enabled=true`.
3. Document experiment config format in `docs/02_STEP_BY_STEP_IMPLEMENTATION_GUIDE.md` and `Agent/ROADMAP.md`.
4. Ensure the default local Docker Compose flow remains functional without AWS.

## File output and implementation checklist

- `Agent/Implementation_Plans/Online_AB_Experimentation_Framework.md` (this file)
- `src/Shared/ExperimentDtos.cs`
- `src/BidEngine/Models/ExperimentDefinition.cs`
- `src/BidEngine/Models/ExperimentVariation.cs`
- `src/BidEngine/Configuration/ExperimentOptions.cs`
- `src/BidEngine/Services/Interfaces/IExperimentConfigurationProvider.cs`
- `src/BidEngine/Services/Interfaces/IExperimentAssignmentService.cs`
- `src/BidEngine/Services/Interfaces/IExperimentService.cs`
- `src/BidEngine/Services/Interfaces/IExperimentEventLogger.cs`
- `src/BidEngine/Services/ExperimentConfigurationProvider.cs`
- `src/BidEngine/Services/ExperimentAssignmentService.cs`
- `src/BidEngine/Services/ExperimentService.cs`
- `src/BidEngine/Services/ExperimentEventLogger.cs`
- `src/BidEngine/Services/ExperimentContext.cs`
- `src/BidEngine/Controllers/AdminExperimentsController.cs`
- `tests/BidEngine.Tests/Services/ExperimentAssignmentServiceTests.cs`
- `tests/BidEngine.Tests/Services/ExperimentServiceTests.cs`
- `tests/BidEngine.Tests/Services/BidSelectorExperimentTests.cs`

## Notes

- Start with a configuration-driven implementation rather than a database-backed experiment store.
- Implement the experiment framework as a thin extension around the existing bid path to minimize risk.
- Focus on deterministic bucketing, stable assignments, and clear observability.
- Keep admin APIs admin-only and audit all configuration changes.
