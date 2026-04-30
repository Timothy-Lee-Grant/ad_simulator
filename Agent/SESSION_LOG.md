# Session Log

## Date
2026-04-29

## Goal
Audit old .NET ad server project and modernize using AI workflow.

## Actions
- Started recovery audit.

## Findings
- Docker compose startup succeeded, but `docker-compose.yml` used the obsolete `version` key and Prometheus failed because `infrastructure/monitoring/prometheus.yml` was an empty directory instead of a config file.

## Next Steps
- Remove the broken directory and add a proper `infrastructure/monitoring/prometheus.yml` config file.
- Update `docker-compose.yml` to mount Prometheus config directly and keep the service healthy.

---

## Date
2026-04-30

## Goal
Validate the updated Docker Compose stack and ensure frontend/bid-engine functionality.

## Actions
- Fixed `docker-compose.yml` and created `infrastructure/monitoring/prometheus.yml` and `infrastructure/monitoring/rules.yml`.
- Restarted the Docker Compose stack and verified Prometheus started successfully.
- Confirmed frontend homepage and bid-engine health endpoint respond correctly.

## Findings
- `ads_prometheus` is now starting with a valid Prometheus config.
- `ads_frontend` is running and the homepage returns HTML content.
- `GET /api/bid/test` returns "BidEngine is running!" as expected.
- The SIGTERM entry in frontend logs is historical; the frontend container has `RestartCount=0` and is currently running normally.

## Next Steps
- Continue monitoring the stack for any further runtime errors.
- Optionally add a dedicated smoke test script for local compose verification if additional automation is desired.

---

## Date
2026-04-30

## Goal
Run the repository smoke test suite against the Docker Compose stack.

## Actions
- Executed `scripts/e2e_smoke.sh` after verifying the frontend and bid-engine endpoints.
- Confirmed Prometheus, Redis, PostgreSQL, BidEngine, Frontend, and Grafana are all running.

## Findings
- The existing smoke test passed successfully.
- The frontend homepage is responsive and click metrics are correctly propagated to the bid engine metrics endpoint.

## Next Steps
- Keep the compose stack under observation and use `scripts/e2e_smoke.sh` for future validation.

---

## Date
2026-04-30

## Goal
Document the configuration and secrets refactor plan in the project roadmap.

## Actions
- Updated `Agent/ROADMAP.md` to add a concrete configuration and secrets refactor plan.
- Made local development the default runtime path and explicitly called out removing AWS-specific default wiring.

## Findings
- The roadmap now clearly supports running the project locally without any AWS account.
- It specifies strong-typed config binding, environment precedence, placeholder-only secrets, and Docker Compose cleanup.

## Next Steps
- Implement the config/secrets refactor in `src/BidEngine/Program.cs`, `appsettings.json`, and `docker-compose.yml`.
- Add `.env.example` and validation tests for env-based configuration binding.

---

## Date
2026-04-30

## Goal
Execute the service boundary cleanup refactor in BidEngine to split the monolithic CampaignCache into focused services.

## Actions
- Created `src/BidEngine/Services/CampaignReadCacheService.cs` to handle campaign read and caching operations (GetCampaignAsync, GetActiveCampaignsAsync, InvalidateCampaignAsync).
- Created `src/BidEngine/Services/VideoEmbeddingService.cs` to manage video and ad vector embeddings (FindVectorFromVideoId, CreateVectorFromVideoId, GenerateEmbeddingsForAllVideos, GenerateEmbeddingsForAllVideosWithDebugging, GenerateEmbeddingsForAllAds).
- Created `src/BidEngine/Services/SemanticQueryService.cs` to perform semantic searches (PerformSemanticSearchForTop3Ads).
- Refactored `src/BidEngine/Services/CampaignCashe.cs` (CampaignCache) to remove embedding and semantic query methods, keeping only campaign read/cache responsibilities.
- Updated `src/BidEngine/Services/BidSelector.cs` to inject and use the new focused services: CampaignReadCacheService for active campaigns, VideoEmbeddingService for video vectors, and SemanticQueryService for semantic ad searches.
- Updated `src/BidEngine/Controllers/BidControllers.cs` AdminController to use VideoEmbeddingService for seed endpoints.
- Modified `src/BidEngine/Program.cs` to register the new services in DI and update the --seed-vectors startup logic to use VideoEmbeddingService.
- Fixed variable naming inconsistencies (e.g., _cashe to _cache) in BidSelector.
- Built and validated the refactored BidEngine project to ensure compilation succeeds.

## Findings
- The refactor successfully split the monolithic CampaignCache into three focused services, improving separation of concerns: read/cache, embedding/vector operations, and semantic queries.
- BidSelector now depends on specific services for their responsibilities, making the code more modular and testable.
- Compilation passes with only one ASP.NET analyzer warning about UseEndpoints, which is unrelated to the refactor.
- The new service boundaries align with the roadmap's goal of isolating business rules from infrastructure details and enabling easier onboarding and testing.

## Next Steps
- Remove the transitional CampaignCache class entirely once all references are migrated.
- Add unit tests for the new focused services to validate their individual behaviors.
- Consider adding interfaces for the new services to enable dependency injection flexibility and mocking in tests.
- Proceed to the next roadmap item or continue refining the current refactor.
