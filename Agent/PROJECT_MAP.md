# PROJECT_MAP

This document is a deep, read-only repository map based on current implementation state.

_Last verified: 2026-07-16 — runtime confirmed working end-to-end (see `Agent/CURRENT_ISSUES.md`). Sections 2 and 3 below predate the auth/campaign-management/experimentation/attribution work merged 2026-04-30/05-01 and have been amended in place; the rest of this document was written after that work and is still accurate._

## 1. What this project appears to do

`ad_simulator` is a practical ad-tech simulation centered on a .NET bid engine and a lightweight Node frontend.

At runtime, it appears to:
- receive ad requests from the frontend,
- select a campaign/ad using targeting + budget checks,
- optionally use vector similarity (video-to-ad embedding matching),
- deduct campaign budget after serving,
- track click events and Prometheus metrics,
- run locally via Docker with PostgreSQL, Redis, Prometheus, and Grafana.

The codebase is partly "production-style prototype" and partly "learning sandbox": there is real infrastructure wiring plus exploratory comments and in-progress algorithm branches.

## 2. Main architecture

High-level runtime architecture:

1. **Frontend (`FrontEnd`)**
   - Express + EJS app serving pages.
   - Calls BidEngine API (`/api/bid`, `/api/videos`).
   - Proxies click tracking to BidEngine and then redirects user.

2. **Bid Engine API (`src/BidEngine`)**
   - ASP.NET Core service with controllers:
     - `BidController` for bid + click endpoints.
     - `AdminController` for embedding/seed operations.
     - `VideosController` for video listing/detail.
     - `AuthController` for login/token issuance (JWT + ASP.NET Identity).
     - `AdminCampaignsController` / `AdminAdsController` / `AdminTargetingController` for authenticated CRUD over campaigns, ads, and targeting rules (policy: `AdminOnly`).
     - `AdminMetricsController` for campaign analytics queries (CTR/spend rollups from the event pipeline).
   - Core services (post service-boundary-cleanup refactor):
     - `BidSelector`: delegates to an `IBiddingStrategy` (`HighestCpmStrategy`, `SemanticOnlyStrategy`, `HybridWeightedStrategy`, selected via `BiddingStrategyOptions` in config) plus the online A/B experiment layer (deterministic bucketing, exposure logging, Prometheus metrics).
     - `CampaignReadCacheService`: campaign read/cache (Redis + DB), split out of the old monolithic `CampaignCache`.
     - `VideoEmbeddingService` / `SemanticQueryService`: vector embedding generation and semantic ad search, also split out of `CampaignCache`.
     - `BudgetService`: spend deduction and cache invalidation.
     - `CampaignManagementService`: EF Core CRUD + audit logging for the admin campaign API.
     - `JwtService` / `AuthService` / `AuditService` / `DatabaseInitializer`: auth infrastructure (Identity, JWT issuance, audit trail, seed admin user on boot).
     - Attribution pipeline: an event publisher/repository pair persists `AdEventLog` (raw events) and `AdEventAggregate` (daily campaign/ad rollups, including experiment/variation dimensions) for both impressions (bid path) and clicks (click path).

3. **Data layer**
   - PostgreSQL (`pgvector/pg15` image) for relational entities and embeddings.
   - EF Core (`AppDbContext`) maps entities + vector columns.
   - Redis caches active campaigns and campaign records.

4. **Observability**
   - Prometheus scrapes `/metrics`.
   - Grafana available for dashboards.
   - GitHub Actions includes unit test CI and dockerized E2E smoke flow.

5. **Optional event stack**
   - Kafka + Zookeeper available through `docker-compose.kafka.yml`.
   - Current application code has little/no active Kafka producer/consumer integration.

## 3. Folder-by-folder explanation

- `src/BidEngine/`
  - Primary backend service (DI, controllers, services, migrations, config, Dockerfile, embedding model files).
  - Most business behavior lives here.

- `src/Shared/`
  - Shared domain objects (`Campaign`, `Ad`, `TargetingRule`, `BidRequest`, `BidResponse`, `Video`).
  - Referenced by BidEngine and tests.

- `tests/BidEngine.Tests/`
  - xUnit tests for selector/cache/budget/controller behavior.
  - Contains both useful unit tests and placeholder/skipped integration tests.

- `FrontEnd/`
  - Node 18 Express app, EJS views (`home`, `video`) and static CSS.
  - Demonstrates ad rendering and click-tracking redirect flow.

- `infrastructure/database/migrations/`
  - SQL bootstrap/seed artifacts and backups (`.bak` files).
  - Coexists with EF Core migrations in `src/BidEngine/Migrations`.

- `docs/`
  - Extensive architecture/API/deployment docs.
  - Significant portions describe a broader Java/Spring multi-service design not fully present in this repository’s current code.

- `.github/workflows/`
  - `ci.yml`: build + test pipeline (integration tests optional/manual).
  - `e2e-smoke.yml`: docker-compose smoke path validating homepage/click/metrics.

- `scripts/`
  - `e2e_smoke.sh` script used by CI to verify end-to-end basics.

- `Agent/`
  - Agent notes and planning artifacts (`AUDIT.md`, `ROADMAP.md`, `SESSION_LOG.md`, this `PROJECT_MAP.md`).

- `examples/`
  - Onboarding notes and example references for related projects.

## 4. Entry points

Primary entry points:

- **Backend process entry**
  - `src/BidEngine/Program.cs`
  - Wires DI, DbContext, Redis, controllers, metrics endpoint, optional `--seed-vectors` mode, and startup migration.

- **Backend HTTP entrypoints**
  - `POST /api/bid` -> bid evaluation (auction strategy + A/B experiment assignment + impression event).
  - `GET /api/bid/test` -> simple test endpoint.
  - `GET /api/bid/User_Click_Event` -> click metric recording + click event.
  - `POST /api/admin/seed-vectors*` -> embedding generation (unauthenticated — see security audit).
  - `GET /api/videos`, `GET /api/videos/{id}` -> video browsing.
  - `POST /api/auth/login` -> JWT issuance.
  - `GET/POST/PUT/DELETE /api/admin/campaigns`, `/api/admin/ads`, `/api/admin/targeting` -> authenticated campaign/ad/targeting CRUD (`AdminOnly` policy).
  - `GET /api/admin/metrics/*` -> campaign analytics (CTR/spend) from the attribution pipeline.

- **Frontend process entry**
  - `FrontEnd/index.js`
  - Serves:
    - `/` home page (fetches bids + videos),
    - `/videos/:id` video detail page with semantic bid request,
    - `/click` click-proxy redirect endpoint.

- **Container/runtime entrypoints**
  - `docker-compose.yml` (core local stack),
  - `docker-compose.kafka.yml` (optional Kafka overlay),
  - service Dockerfiles for BidEngine and FrontEnd.

- **CI entrypoints**
  - `.github/workflows/ci.yml`
  - `.github/workflows/e2e-smoke.yml`

## 5. Databases used

1. **PostgreSQL (primary persistent store)**
   - Campaigns, ads, targeting rules, videos, embeddings.
   - EF Core migrations are under `src/BidEngine/Migrations`.
   - SQL migration/init scripts also exist under `infrastructure/database/migrations`.

2. **pgvector extension (inside PostgreSQL)**
   - Vector columns (`vector(384)`) for ad/video embeddings.
   - Semantic ad search uses SQL vector distance ordering (`embedding <=> targetVector`).

3. **Redis (cache)**
   - Active campaigns and campaign-specific cache keys.
   - Used by `CampaignCache`.

4. **Prometheus TSDB (operational metrics storage)**
   - Not application domain data, but a persisted metrics store in the observability stack.

## 6. AWS integrations

Current AWS integration appears to be **database connection only**, not full AWS SDK integration:

- `.env` defines `AWS_DB_CONN` connection string.
- `docker-compose.yml` injects `ConnectionStrings__AwsConnection=${AWS_DB_CONN}` into `bid-engine`.
- `src/BidEngine/appsettings.json` includes an `aws_connection_string` key.

Important caveat:
- `Program.cs` currently reads `ConnectionStrings:DefaultConnection`, not `AwsConnection`.
- This suggests AWS DB wiring is present in config but may not be active in current runtime path without further config alignment.

No direct code integrations were found for:
- S3, SQS, SNS, Kinesis, DynamoDB, IAM SDK calls, or CloudWatch SDK clients.

## 7. Docker usage

Docker usage is central for local/dev:

- **Core compose (`docker-compose.yml`)**
  - Postgres (pgvector), Redis, Prometheus, Grafana, BidEngine, FrontEnd.
  - BidEngine exposed on `8081`; FrontEnd on `3001`.
  - Postgres host port mapped to `5434`.

- **Optional compose overlay (`docker-compose.kafka.yml`)**
  - Adds Zookeeper + Kafka.
  - Activated by composing both files.

- **Service Dockerfiles**
  - `src/BidEngine/Dockerfile`: multi-stage .NET 9 build/publish.
  - `FrontEnd/Dockerfile`: Node 18 alpine app install/run.

- **CI usage**
  - E2E workflow starts compose stack and runs smoke checks.

## 8. Ad serving request flow

Implemented flow (current code):

1. Browser requests `FrontEnd /` or `/videos/:id`.
2. Frontend calls `POST /api/bid` on BidEngine.
   - For video pages, includes `videoId` so semantic path can be used.
3. `BidController` validates request and invokes `BidSelector`.
4. `BidSelector`:
   - If `VideoId` exists: vector lookup + semantic top-3 ad query.
   - Else: auction path (algorithm 1 currently always selected because `adSelection` is hardcoded to `0.7`).
5. Winning bid returned to frontend and rendered in EJS views.
6. On click, frontend hits `/click` route:
   - Notifies BidEngine via `GET /api/bid/User_Click_Event`.
   - Redirects user to ad destination URL.
7. `BudgetService` deducts spend after successful bid response and invalidates cache key(s).

## 9. Legacy or suspicious areas

Areas that look legacy, inconsistent, or risky:

- **Documentation drift**
  - Several docs describe Java/Spring components (`ad-server`, `analytics-service`, `event-consumer`) that are not implemented as active services in this repo.

- **Config drift**
  - Compose injects `AwsConnection`, while runtime reads `DefaultConnection`.

- **EF Core migration drift (fixed 2026-07-16, but the pattern can recur)**
  - New `DbSet<T>` properties/entities were added to `AppDbContext` (the attribution event tables) in the 2026-05-01 session without a corresponding `dotnet ef migrations add`. This is invisible to `dotnet build`/`dotnet test` and only surfaces when the container actually boots against Postgres — `db.Database.Migrate()` throws `PendingModelChangesWarning` as an unhandled exception and the container crash-loops. This had been silently breaking the entire stack since 2026-05-01 until caught in this session's audit. See `Agent/CURRENT_ISSUES.md` for the fix and a recommended CI guard against recurrence.

- **Secret hygiene risk**
  - Sensitive DB credentials present in config surfaces (`appsettings.json` and local `.env` values).

- **Security concerns**
  - Frontend `/click` redirects user-provided `redirect` URL without allowlist validation (open redirect risk).
  - Admin seed endpoints are unauthenticated.

- **Metrics reliability concern**
  - Click counter is created in request path and exceptions are swallowed, risking inaccurate click metrics.

- **Concurrency risk**
  - Budget deduction is read-modify-write without clear optimistic/pessimistic concurrency protection.

- **Naming / quality debt**
  - Misspellings (`CampaignCashe.cs`, `PerformSematicSearch...`, assorted comments/identifiers).
  - Mixed exploratory comments and production-like code.

- **Testing gaps**
  - Key tests skipped or placeholders.
  - Integration coverage not automatically enforced in standard PR CI path.

- **Dependency ambiguity**
  - Both `prometheus-net` and `Prometheus.Client*` packages are referenced, which may indicate redundant instrumentation stack choices.

## 10. Missing documentation

Based on current code, these docs are missing or need major refresh:

1. **Accurate current-system architecture doc**
   - Should reflect actual .NET + Node implementation, not planned Java multi-service topology.

2. **Runtime configuration truth table**
   - Clarify which connection string key wins in each environment (local docker, local dotnet run, AWS DB scenarios).

3. **Security model doc**
   - Endpoint auth expectations, admin endpoint protections, redirect validation policy, secret management/rotation guidance.

4. **Data and migration strategy doc**
   - Source of truth between EF migrations vs SQL scripts, migration order, and rollback strategy.

5. **Semantic search operations guide**
   - How embeddings are generated, when to run seed endpoints, expected model files, vector dimension assumptions, and failure handling.

6. **Testing strategy doc (current-state)**
   - Which tests are trusted today, what is skipped, and CI gating policy.

7. **Observability playbook**
   - Metric definitions actually emitted by code, known caveats, dashboards/alerts tied to real endpoints.

8. **Production hardening checklist (code-aligned)**
   - Replace generic roadmap language with concrete actions for this codebase (auth, secrets, concurrency, redirect safety, startup migration policy).

---

If needed, this file can be followed by a companion `RISK_REGISTER.md` that scores each suspicious area by severity, likelihood, and remediation effort.
