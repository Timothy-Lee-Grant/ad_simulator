# ROADMAP

This roadmap is based on the current implementation in `ad_simulator` (BidEngine + FrontEnd + Docker stack), plus the repository audits.

_Last verified 2026-07-16 (see `Agent/CURRENT_ISSUES.md` and `Agent/SESSION_LOG.md`): the stack builds, boots, and passes its tests and smoke test end-to-end after fixing a startup-crashing EF Core migration gap. Status of the items below updated accordingly — item 2 is done; item 1 is still open (no `.env.example` exists yet, and `appsettings.json`/`.env` still carry AWS connection config, though the values present look like placeholder/example data rather than live credentials — worth confirming either way before treating this as low-priority)._

## Recommended Next Moves (as of 2026-07-16)

Given Timothy's stated priorities (backend/distributed systems mastery; explicitly weak on event-driven/pub-sub systems and async/concurrency intuition — see `persona.md`), these three existing roadmap items are the highest-leverage next picks, in order:

1. **Wire the existing Kafka overlay to the event pipeline.** Closes the gap noted in "Best Resume-Worthy New Features" item 3 below — `docker-compose.kafka.yml` and `docs/KAFKA.md` already describe the intended topology, but nothing produces or consumes from it yet. Real producer (impression/click events) + consumer (materializing `AdEventAggregate` asynchronously instead of synchronously in the request path) targets the Kafka/pub-sub weakness directly, and would make the "event-driven architecture" resume claim fully true instead of half-true. Also doubles as the "Background work isolation and async processing" item under Scalability Upgrades below.
2. **Concurrency-safe budget deduction** (Reliability Upgrades item 1 below). Currently a read-modify-write with no locking — a textbook race condition under concurrent bid traffic, and exactly the kind of bug Timothy wants more intuition for. Small, self-contained, good interview story ("here's a race condition I found and fixed").
3. **Configuration/secrets refactor** (Highest ROI Refactors item 1 below). Cheap and mechanical; closes out the one roadmap item that's been open since April. Add `.env.example`, strongly-typed options binding, and confirm whether the AWS connection values are real or placeholder before deciding priority.

---

## 3 Highest ROI Refactors

1. **Configuration and secrets refactor (single source of truth) — still open**
   - Unify connection-string handling (`DefaultConnection` vs `AwsConnection`) and remove plaintext secrets from tracked config.
   - Introduce strongly typed options classes and clear environment precedence.
   - **ROI:** Immediately reduces production risk and eliminates recurring config drift/debug time.
   - **Refactor plan:**
     - Add a `.env.example` and document required environment variables; keep all real secrets out of the repository.
   - Make local development the default runtime path: `ConnectionStrings__DefaultConnection` and `Redis__ConnectionString` should be primary, with safe local placeholders such as `Server=postgres;Port=5432;Database=ads_db;User Id=postgres;Password=postgres`.
   - Remove AWS-specific runtime wiring from the default Docker Compose flow. If AWS support remains, keep it optional and documented with placeholder values like `Place Your Info Here` rather than real credentials.
   - Refactor `src/BidEngine/Program.cs` to bind strongly typed options for database and Redis using environment variables first, then non-secret `appsettings.json` defaults.
   - Strip secrets from checked-in `appsettings.json` and `.env`; replace any real AWS or database credentials with placeholders.
   - Ensure the code fails fast with clear errors when required config is missing, so local Docker Compose can run cleanly without any AWS account.
   - Update `docker-compose.yml` to use `DefaultConnection` and `Redis` env values only, and remove or deprecate `ConnectionStrings__AwsConnection` from the main composition.
   - Add unit tests for configuration binding and env precedence, plus a smoke test that validates the local compose stack uses the local default connection string.

2. **Service boundary cleanup in BidEngine** ✅ COMPLETED (2026-04-30)
   - Split `CampaignCache` into focused services:
     - `CampaignReadCacheService` (campaign read/cache),
     - `VideoEmbeddingService` (embedding/vector service),
     - `SemanticQueryService` (semantic query service).
   - Controllers are thin; `BidSelector` now composes these services plus the strategy pattern below.
   - **ROI:** Faster onboarding, easier testing, and safer future feature changes.

3. **Bidding strategy refactor to pluggable policies** ✅ COMPLETED
   - Replace hardcoded algorithm branching with strategy pattern (e.g., highest CPM, semantic-only, hybrid weighted).
   - Add a selection policy config or experiment flag.
   - **ROI:** Enables controlled experimentation and feature velocity without destabilizing core bid path.
   - **Implementation completed:**
     - Created `IBiddingStrategy` interface and three concrete implementations: `HighestCpmStrategy`, `SemanticOnlyStrategy`, `HybridWeightedStrategy`.
     - Refactored `BidSelector` to use Strategy pattern with dependency injection.
     - Added configuration-driven strategy selection via `BiddingStrategyOptions` and factory pattern.
     - Updated DI registration in `Program.cs` and configuration in `appsettings.json`.
     - Validated compilation and runtime functionality.

---

## 3 Best Resume-Worthy New Features

All three shipped (2026-04-30 through 2026-05-01). What's built vs. what the original bullet aspired to, so the resume story stays honest:

1. **Authenticated Admin API + campaign management** ✅ COMPLETED
   - JWT auth (`AuthController`/`JwtService`) + ASP.NET Identity roles, `AdminOnly` policy on all admin routes.
   - Full CRUD for campaigns/ads/targeting rules (`AdminCampaignsController`/`AdminAdsController`/`AdminTargetingController`) via `CampaignManagementService`, with validation (FluentValidation) and audit logging (`AuditService`).
   - **Resume value:** Shows real backend productization (authz, data modeling, operational APIs).

2. **Online A/B experimentation framework for bidding** ✅ COMPLETED
   - Deterministic user bucketing, exposure logging, and Prometheus exposure metrics are live in the bid path.
   - **Gap vs. original bullet:** no Grafana dashboard has been built yet for experiment exposure/outcome metrics, and experiment *outcomes* (not just exposures) aren't persisted — see `Agent/Implementation_Plans/Online_AB_Experimentation_Framework_Results.md` next-steps.
   - **Resume value:** Demonstrates experimentation systems and decision-science-aware backend design — strongest once outcome persistence + a dashboard exist to point to.

3. **Attribution-ready click/impression event pipeline** ✅ COMPLETED (DB-backed, not Kafka)
   - Impression/click events are persisted to Postgres (`AdEventLog` raw events + `AdEventAggregate` daily rollups) via `AdminMetricsController` for CTR/spend queries.
   - **Gap vs. original bullet:** events are **not** emitted to Kafka — the Kafka/Zookeeper compose overlay (`docker-compose.kafka.yml`) exists but has no producer/consumer wired to it. The "event-driven architecture" resume claim is currently accurate for "structured event persistence + analytics," not for "streaming/Kafka."
   - **Resume value:** End-to-end analytics engineering today; wiring the existing Kafka overlay to the event publisher would make the full "event-driven architecture" claim true, and is the most natural next step given `docs/KAFKA.md` already documents the intended topology.

---

## 3 Scalability Upgrades

1. **Two-layer cache strategy with invalidation discipline**
   - Add in-process cache + Redis cache with versioned keys and controlled TTL.
   - Move from broad cache wipes to targeted invalidation.
   - **Scale impact:** Lower DB load and better p95 under high QPS.

2. **Database query and index optimization for hot paths**
   - Add indexes for active campaign filters and targeting predicates.
   - Optimize semantic search path for vector + metadata constraints.
   - **Scale impact:** Better read throughput and predictable latency growth.

3. **Background work isolation and async processing**
   - Move expensive operations (embedding generation, seeds, bulk updates) out of request path into workers/jobs.
   - **Scale impact:** Prevents request starvation and improves API responsiveness at load.

---

## 3 Reliability Upgrades

1. **Concurrency-safe budget deduction**
   - Add transactional consistency (row locking or optimistic concurrency tokens + retry).
   - Ensure no overspend under concurrent bid traffic.
   - **Reliability impact:** Correct financial behavior under load.

2. **Health/readiness and startup hardening**
   - Add `/health` and `/ready` probes with dependency checks.
   - Make DB migrations explicit startup task or one-off job (not automatic on every boot in prod).
   - **Reliability impact:** Cleaner deploys and fewer startup race failures.

3. **Resilience policies for external dependencies**
   - Add retries with jitter, timeouts, and circuit breakers around DB/Redis calls.
   - Introduce graceful fallback behavior for partial dependency outages.
   - **Reliability impact:** Better survivability during transient infra issues.

---

## 3 Security Upgrades

1. **Secrets hygiene and rotation program**
   - Remove all plaintext credentials from repo/config.
   - Rotate compromised credentials and move secrets to managed store.
   - **Security impact:** Eliminates current critical exposure class.

2. **Authentication/authorization enforcement**
   - Require auth for API usage and strict role checks for admin routes.
   - Add service-to-service auth if components split further.
   - **Security impact:** Prevents unauthorized control-plane actions.

3. **Input validation + redirect hardening**
   - Enforce DTO validation (length, format, ranges, requireds).
   - Replace open redirect with allowlisted destination domains.
   - **Security impact:** Reduces abuse/phishing and malformed-input attack surface.

---

## 3 Backend Interview Talking Points

1. **Built a realistic ad-bidding backend with semantic ranking**
   - Combined traditional highest-bid auction logic with vector-based relevance using `pgvector`.
   - Balanced product realism (campaign budgets, targeting rules) with ML-adjacent retrieval.

2. **Designed for observability and operational debugging**
   - Instrumented request and latency metrics, added smoke tests/CI hooks, and mapped service-level bottlenecks.
   - Demonstrated practical incident-readiness habits in a full-stack backend project.

3. **Applied production-minded engineering from audits**
   - Identified and prioritized security, reliability, and scalability gaps (auth, secrets, concurrency, config drift).
   - Created staged remediation roadmap showing engineering judgment and execution planning.
