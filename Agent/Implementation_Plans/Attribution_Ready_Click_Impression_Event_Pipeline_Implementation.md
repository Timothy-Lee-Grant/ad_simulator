# Attribution-ready Click/Impression Event Pipeline Implementation

## Summary
Implemented a fully working attribution-ready event pipeline in `src/BidEngine` that captures structured impression and click events, persists raw event logs, aggregates campaign/ad metrics, and exposes admin analytics.

## What changed
- Added shared event models in `src/Shared/AdEventDtos.cs`
  - `AdEventBase`
  - `AdImpressionEvent`
  - `AdClickEvent`
  - `AdEventLog`
  - `AdEventAggregate`
  - `CampaignMetricsDto`
  - `CampaignExperimentMetricsDto`

- Added event pipeline abstractions and services
  - `src/BidEngine/Services/Interfaces/IEventPublisher.cs`
  - `src/BidEngine/Services/Interfaces/IAdAnalyticsRepository.cs`
  - `src/BidEngine/Services/Interfaces/IAdEventService.cs`
  - `src/BidEngine/Services/DbEventPublisher.cs`
  - `src/BidEngine/Services/AdAnalyticsRepository.cs`
  - `src/BidEngine/Services/AdEventService.cs`

- Extended the EF Core `AppDbContext` in `src/BidEngine/Data/AppDbContext.cs`
  - `DbSet<AdEventLog>`
  - `DbSet<AdEventAggregate>`
  - mapped `ad_event_logs` and `ad_event_aggregates` tables

- Wired the event pipeline into the bid path
  - updated `src/BidEngine/Controllers/BidControllers.cs`
  - impression events are created after budget deduction
  - click events are captured in the existing `User_Click_Event` endpoint

- Added campaign analytics API
  - `src/BidEngine/Controllers/AdminMetricsController.cs`
  - `GET /api/admin/metrics/campaigns/{campaignId}`

- Registered services in DI
  - `src/BidEngine/Program.cs`
  - `IAdAnalyticsRepository`, `IEventPublisher`, `IAdEventService`

## Validation
- Verified implementation by running the existing unit tests for `tests/BidEngine.Tests/BidEngine.Tests.csproj`
- Result: `39 passed`, `3 skipped`
- The application compiles successfully with only unrelated existing warnings

## Notes
- The pipeline currently uses a durable database-backed publisher (`DbEventPublisher`) for raw event persistence.
- Aggregation is performed in `AdAnalyticsRepository` using daily event buckets and experiment/variation labels.
- The admin analytics endpoint returns campaign-level impressions, clicks, spend, CTR, and experiment breakdown.

## Next steps
- Add a dedicated event stream implementation for Kafka or Redis later.
- Add API-level integration tests for admin metrics and click/impression flow.
- Add a production migration or SQL script to create the new `ad_event_logs` and `ad_event_aggregates` tables.
