# Attribution-ready Click/Impression Event Pipeline

## Overview

This implementation guide defines how to build an attribution-ready click/impression event pipeline for `BidEngine`. The goal is to capture structured ad delivery and engagement events, emit them into a reliable event stream, persist aggregates for analytics, and expose campaign-level campaign performance metrics.

This pipeline will enable campaign attribution, click-through-rate (CTR) analysis, spend monitoring, and conversion-proxy metrics while keeping the core bid path lean and resilient.

## Objectives

- Capture structured impression and click events for every eligible ad delivery and user engagement.
- Emit events into a streamable channel such as Kafka or a durable queue.
- Persist aggregate metrics for campaigns and ads in the database.
- Expose campaign analytics endpoints for CTR, spend, and conversion proxy metrics.
- Keep event capture low-latency and safe for production bid traffic.
- Support future attribution workflows and offline analytics.

## Success Criteria

- Impressions and clicks are recorded as structured events with campaign, ad, request, and experiment metadata.
- The system emits events to a stream or log sink with retry/backoff resilience.
- Aggregated metrics are persisted to the database and can be queried efficiently.
- `GET /api/admin/campaigns/{campaignId}/metrics` returns CTR, spend, impressions, clicks, and click attribution data.
- The event pipeline does not degrade bid request performance under normal load.
- Future conversion events can be layered on top of the same event model.

## Architecture

### Core components

- `AdEvent` / `AdImpressionEvent` / `AdClickEvent` domain models
- `IEventPublisher` abstraction for stream publishing
- `KafkaEventPublisher` or `InMemoryEventPublisher` implementation
- `EventIngestionService` for aggregating and persisting event data
- `EventStore` or `AdAnalyticsRepository` for campaign metrics storage
- `AdEventController` or enhanced `BidController` / `ClickController` endpoints
- `CampaignMetricsDto` and admin analytics APIs

### Data flow

1. The bid request path evaluates campaigns and returns an ad.
2. When an ad is selected or displayed, an impression event is generated and published.
3. When the user clicks the ad, a click event is captured and published.
4. A consumer service or background worker ingests published events, writes them to a durable store, and updates aggregates.
5. Admin analytics endpoints query persisted aggregates for campaign-level metrics.
6. Experiment metadata and attribution context are attached to events for later analysis.

## Design details

### Event model

Define a shared event model in `src/Shared/AdEventDtos.cs`.

- `AdEventBase`
  - `EventId` (Guid)
  - `EventType` (string)
  - `TimestampUtc` (DateTime)
  - `CampaignId` (Guid)
  - `AdId` (Guid)
  - `UserId` (string)
  - `PlacementId` (string)
  - `RequestId` (string)
  - `ExperimentId` (string?)
  - `VariationId` (string?)
  - `Metadata` (dictionary)

- `AdImpressionEvent`
  - `ImpressionValue` (decimal)
  - `BidPrice` (decimal)
  - `AdContentType` (string)

- `AdClickEvent`
  - `ClickValue` (decimal)
  - `ClickLocation` (string?)
  - `SessionId` (string?)

### Event publishing

Build a pluggable publisher abstraction.

- `IEventPublisher`
  - `Task PublishAsync<T>(T @event) where T : AdEventBase`

- Implementations:
  - `KafkaEventPublisher` (preferred for production)
  - `RedisStreamEventPublisher` or `AzureEventHubPublisher` (optional)
  - `NullEventPublisher` for local/dev fallback

The publisher should support:
- asynchronous fire-and-forget with safe retries
- configurable batch or direct publish
- logging/tracing for failures

### Event ingestion and aggregation

Add a persisted aggregate store to capture event counts.

- `AdEventAggregate`
  - `CampaignId`
  - `AdId`
  - `Date` or `Hour` bucket
  - `ImpressionCount`
  - `ClickCount`
  - `SpendTotal`
  - `ClickThroughRate` derived at query time
  - `ExperimentId`
  - `VariationId`

- `AdAnalyticsRepository`
  - `Task AddImpressionAsync(AdImpressionEvent event)`
  - `Task AddClickAsync(AdClickEvent event)`
  - `Task<CampaignMetricsDto> GetCampaignMetricsAsync(Guid campaignId, DateTime from, DateTime to)`
  - `Task<IEnumerable<CampaignAnalyticsDto>> GetAllCampaignMetricsAsync(...)`

This store can be updated by:
- direct ingestion in the app for low-volume deployments
- a background worker consuming Kafka and updating aggregates for higher volume

### API layer

Add admin analytics endpoints under `AdminMetricsController` or extend `AdminCampaignsController`.

- `GET /api/admin/campaigns/{campaignId}/metrics`
- `GET /api/admin/campaigns/{campaignId}/metrics/ctr`
- `GET /api/admin/campaigns/{campaignId}/metrics/spend`

Return:
- impressions
- clicks
- CTR
- spend
- revenue proxy
- experiment-specific breakdowns

### Attribution context

Capture attribution fields with each event so future conversion tracking can join upstream exposures.

- `AttributionId` or `RequestId`
- `Source` (e.g. `bid`, `click`, `conversion`)
- `CampaignId`, `AdId`
- `UserId`, `PlacementId`
- `ExperimentId`, `VariationId`
- `Referrer` / `LandingPage` (optional)

## Implementation steps

### Step 1: Define event DTOs

1. Add `src/Shared/AdEventDtos.cs`.
2. Add `AdImpressionEvent`, `AdClickEvent`, and shared `AdEventBase`.
3. Add `CampaignMetricsDto` and `CampaignAnalyticsDto`.

### Step 2: Add publishing infrastructure

1. Create `src/BidEngine/Services/Interfaces/IEventPublisher.cs`.
2. Add `KafkaEventPublisher` or other transport implementation.
3. Add DI registration in `Program.cs` with fallback to `NullEventPublisher`.
4. Add an `EventPublisherOptions` configuration section for broker endpoints and topic names.

### Step 3: Capture events in the bid path

1. Update `BidController` or a dedicated click/impression controller.
2. Publish `AdImpressionEvent` when an ad is selected for bid response.
3. Publish `AdClickEvent` in the click endpoint.
4. Keep event publishing asynchronous and resilient.

### Step 4: Persist aggregates

1. Add `AdEventAggregate` table to EF Core model.
2. Add repository/service `AdAnalyticsRepository`.
3. Update the background event consumer or direct service to increment metrics.
4. Add DB migrations for the metrics table.

### Step 5: Add analytics endpoints

1. Create `AdminMetricsController`.
2. Expose campaign-level and experiment-aware metrics.
3. Protect these endpoints with the existing admin policy.

### Step 6: Add tests

- Unit tests for `IEventPublisher` fallback behavior.
- Unit tests for event DTO validation and mapping.
- Integration tests for `BidController` event publishing and analytics aggregation.
- API tests for campaign metrics endpoints.
- Smoke test verifying event pipeline health and aggregated metrics calculation.

## Verification

- Run the existing unit test suite and new analytics tests.
- Use a local Kafka/Redis stub if production broker is not available.
- Validate that `AdImpressionEvent` and `AdClickEvent` are emitted on bid and click requests.
- Confirm admin campaign metrics endpoints return consistent values after events are processed.

## Next steps

- Add conversion event tracking on top of clicks and impressions.
- Add attribution joins for multi-touch or last-click credit.
- Consider a dedicated event store/table for raw event history if audit retention is required.
