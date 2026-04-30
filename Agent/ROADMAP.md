# ROADMAP

This roadmap is based on the current implementation in `ad_simulator` (BidEngine + FrontEnd + Docker stack), plus the repository audits.

## 3 Highest ROI Refactors

1. **Configuration and secrets refactor (single source of truth)**
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

2. **Service boundary cleanup in BidEngine**
   - Split `CampaignCache` into focused services:
     - campaign read/cache service,
     - embedding/vector service,
     - semantic query service.
   - Keep controllers thin and isolate business rules from infrastructure details.
   - **ROI:** Faster onboarding, easier testing, and safer future feature changes.

3. **Bidding strategy refactor to pluggable policies**
   - Replace hardcoded algorithm branching with strategy pattern (e.g., highest CPM, semantic-only, hybrid weighted).
   - Add a selection policy config or experiment flag.
   - **ROI:** Enables controlled experimentation and feature velocity without destabilizing core bid path.

---

## 3 Best Resume-Worthy New Features

1. **Authenticated Admin API + campaign management**
   - Add JWT auth and role-based policies.
   - Build CRUD endpoints for campaigns/ads/targeting rules with validation and audit events.
   - **Resume value:** Shows real backend productization (authz, data modeling, operational APIs).

2. **Online A/B experimentation framework for bidding**
   - Deterministic user bucketing, experiment definitions, exposure logging, and outcome metrics.
   - Add experiment dashboard metrics in Prometheus/Grafana.
   - **Resume value:** Demonstrates experimentation systems and decision-science-aware backend design.

3. **Attribution-ready click/impression event pipeline**
   - Emit structured impression/click events to Kafka and persist aggregates.
   - Add campaign analytics endpoints (CTR, spend, conversion proxy metrics).
   - **Resume value:** End-to-end event-driven architecture + analytics engineering.

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
