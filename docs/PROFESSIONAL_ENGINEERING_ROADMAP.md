# Professional Engineering Roadmap — AdSimulator

Purpose: This is an exhaustive, actionable guide to transform AdSimulator from a working prototype into a production-ready system. It lists prioritized phases, concrete steps, example commands and templates, checklists, and acceptance criteria so you can implement and learn while shipping high-quality, safe, scalable infrastructure and code.

---

## Executive summary

This project already contains a strong prototype of semantic search and a simple ad-serving stack. To make it production-ready, focus on three pillars first: reliability, security, and observability. The roadmap below splits work into phases (MVP production → Harden → Scale & Optimize) with explicit tasks and acceptance criteria. Wherever possible we include small code/config examples and test ideas so you can both learn and get things done.

---

## Goals & Acceptance Criteria

- **Availability**: 99.95% for core services (bid-engine, DB) measured by uptime and synthetic checks. Recovery time objective (RTO) < 15 minutes for most incidents.
- **Correctness**: Ads are served according to targeting and budgets. Semantic search returns stable results; embedding generation is logged and versioned.
- **Safety & Security**: No plaintext secrets, TLS everywhere, scanning in CI, RBAC and least privilege enforced.
- **Observability**: Metrics, logs, traces, and alerts tied to SLOs and runbooks.
- **Testability**: Unit, integration, and end-to-end tests run in CI with reproducible environments and deterministic seeds for embeddings.
- **Repeatable Deployments**: Infrastructure as Code (IaC) drives environments (Terraform + Helm), with GitOps or secure pipelines for deployment.

---

## Project state quick assessment (what to keep / refactor)

- Keep: clean separation of BidEngine, CampaignCache, and embedding generation; provider-aware EF mappings; express frontend is simple and easy to test.
- Refactor soon: embedder lifecycle (make a single shared service, avoid re-instantiation), encapsulate model download/verification into a model manager, convert inline scripts to small frontend bundle or an accessible static file for reusability, and extract configuration constants into typed configuration objects where missing.

---

## Phases & Timeline (high level)

- Phase 0 — Preparation & Triage (1 week)
  - Lock dependency versions, set up branch protection and basic CI, create a staging environment in cloud.
- Phase 1 — Production MVP (2–4 weeks)
  - Managed Postgres with pgvector, managed Redis, containerize and deploy to Kubernetes (or managed platform). Add TLS and ingress, secrets, basic monitoring and CI/CD.
- Phase 2 — Hardening (3–6 weeks)
  - Add SAST/DAST, dependency scanning, RBAC, backups, disaster recovery plan, image scanning, vulnerability patching policy, and runbooks.
- Phase 3 — Scale & Optimize (4–8+ weeks)
  - Add HNSW/ANN indexing, integration tests for vector search, run load tests and optimize latency, add autoscaling, caching strategies, and potential migration to managed vector DB if needed.
- Phase 4 — Operational Excellence (ongoing)
  - SLO/alert refinement, chaos testing, performance budgets, capacity planning, cost optimization.

Adjust the times to your team size & priorities — treat this as a rolling plan.

---

## Phase 0 — Preparation & Triage (Checklist & How-to)

1. Pin your dependencies (must):
   - Add a dependency lock / manifest (for .NET, ensure `Directory.Packages.props` or `nuget.config` pins versions; for node, use `package-lock.json`).
   - Add Dependabot / Renovate with configuration to automate minor security updates only.

2. Branch protection & PR checks (must):
   - Require PR reviews, run CI tests, require status checks, and optionally require linear history.

3. Baseline CI (must):
   - Add a GitHub Actions workflow (or your CI) that builds, runs unit tests, runs linter & static analysis. Example job matrix: dotnet build/test, node lint + unit tests.

4. Create a staging cloud account / namespace (must):
   - Use a separate cloud project or AWS account for staging.
   - Provision minimal infra: managed Postgres (with pgvector extension enabled or ensure you can run extension), managed Redis, Kubernetes namespace.

5. Document the environment & runbook basics (must):
   - Add README.md under `infra/` with steps to deploy to staging and to inspect logs.

Acceptance Criteria for Phase 0:
- CI runs and passes on PRs.
- Branch protection is configured and enforced.
- Staging namespace is available with a running Postgres and Redis.

---

## Phase 1 — Production MVP (Safe deployable baseline)

Goal: Deploy a production-like stack with secure defaults and monitoring. Get one canonical way to deploy (IaC + pipeline). Keep scope small and verify behavior with smoke tests.

Key tasks:

1. Infrastructure as Code (IaC) (must)
   - Technology options: Terraform + Helm (recommended) or Pulumi.
   - Define: network (VPC), subnets, Kubernetes (managed cluster), managed Postgres (RDS/Cloud SQL/Azure DB) with pgvector enabled (or use a provider that supports extensions), managed Redis (ElastiCache/Azure Cache), and an object store (S3/GCS/Azure Blob) for artifacts and model files.
   - Example: a `terraform/` folder with modules for each service; add a `README` with variables and a plan/apply example.

2. Containerization & Registry (must)
   - Build reproducible images for `bid-engine` and `frontend` using a multi-stage Dockerfile. Pin base image versions and add image provenance (labels).
   - Publish images to a secure registry (GHCR/Amazon ECR/Azure ACR) via CI after passing tests.
   - Add `docker scan`, `trivy`, or `snyk` scanning in CI.

3. Kubernetes Deployment (must)
   - Deploy using Helm charts for each service. Keep resource requests/limits realistic.
   - Use readiness & liveness probes (for bid-engine, ensure ready only after migrations and DB connectivity).
   - Use secrets via your cloud provider secrets (Kubernetes Secrets with KMS-backed encryption) or external secrets operator tied to Vault/Key Vault.
   - Use an Ingress (NGINX / Contour / Traefik) with TLS (ACME or pre-provision certs). Terminate TLS at ingress; enforce HTTPS.

4. Database & Migrations (must)
   - Switch to managed Postgres in production and replicate the same schema. Run EF migrations in a single controlled job (migration job in Kubernetes) with backups enabled.
   - Enable automated backups and point-in-time recovery (PITR).
   - Add migration runbook: backup DB → run migration in staging → smoke tests → run in production during outage window.

5. Secrets & Config (must)
   - Move sensitive settings (DB connection strings, Redis creds, MODEL_URL credentials) into a secrets manager (Vault or cloud provider secrets).
   - Use templated config maps for non-sensitive config and use feature flags for experimental behavior (AllowDeterministicFallback as a feature flag).

6. Observability (must)
   - Metrics: Expose and scrape Prometheus metrics (bid_latency_seconds, bid_requests_total, embedding_generation_count, embedding_generation_error, db_query_duration_seconds, redis_connection_failures).
   - Logs: Structured JSON logs with correlation IDs. Centralize logs in a logging backend (Loki / Elastic / Cloud provider).
   - Tracing (recommended): Add OpenTelemetry to trace requests across frontend→bid-engine→db. Collect traces to Jaeger/Tempo.
   - Dashboards: Create Grafana dashboards for request rate, error rate, latency, and embedding throughput.

7. CI/CD (must)
   - Add pipelines to build, test, scan, and deploy images: PR -> build & test -> (optionally) publish to registry on merge -> deploy to staging -> run integration smoke tests -> deploy to production via manual approval and progressive rollout (canary or blue/green).
   - Example: GitHub Actions or GitLab CI with separate workflows for `push` and `release`.

8. Health checks & Synthetic monitoring (must)
   - Add a synthetic check that posts to `/api/bid` and verifies 200 or 204; alert on elevated error rate or high latency.

9. Runtime configuration for model files (must)
   - Use an artifact store (S3) to host model zips and verify checksum before unzipping into model path.
   - Implement a model manager service in `BidEngine` that downloads, verifies signature/checksum, extracts safely to `/var/models/<version>`, and exposes which model version is active through metrics and health endpoint.

Acceptance Criteria for Phase 1:
- Services deploy to staging and are reachable via HTTPS.
- CI runs and publishes images and Helm charts to registries/artifacts.
- Prometheus scrapes metrics; at least one dashboard and alert exist.
- DB backups and migration job are configured and tested on staging.

---

## Phase 2 — Hardening & Security

1. Secrets & Identity (must)
   - Use a secrets manager (HashiCorp Vault, AWS Secrets Manager, Azure Key Vault). Rotate credentials periodically. Use short-lived tokens where possible.
   - Enable identity for workloads (IAM roles for service accounts in EKS/GKE) and avoid mounted static credentials.

2. SAST & DAST (must)
   - Add SAST (e.g., Semgrep, SonarQube) to CI to find injection/backdoor issues.
   - Add DAST (e.g., OWASP ZAP) for the frontend in staging to identify runtime vulnerabilities.

3. Container & Image Security (must)
   - Add container scanning (Trivy) in CI and fail builds on critical vulnerabilities.
   - Use minimal base images (distroless or slim) and enable automatic base-image updates via Dependabot for Dockerfiles.

4. Network security (must)
   - Enforce network policies (Kubernetes NetworkPolicies) to limit service-to-service communication.
   - Use mTLS (service mesh or SPIFFE) between services for intra-cluster encryption and identity verification.

5. Data Protection (must)
   - Encrypt DB at rest (managed provider handles this) and enable TLS for DB connections.
   - Audit logs for DB access if available.

6. Access Control & Logging (must)
   - Implement RBAC for the Kubernetes cluster and cloud resources with least privilege.
   - Centralize audit logs for admin activity.

7. Security Runbooks & Incident Response (must)
   - Document steps for compromised credentials, data leaks, service compromise, and post-incident forensic steps.

Acceptance Criteria for Phase 2:
- No high-critical vulnerabilities in image scanners.
- Secrets are not stored in plaintext in repos or images.
- Access is enforced via RBAC and network policies; incident runbook exists.

---

## Phase 3 — Performance, Scalability & Vector Search Productionization

1. Move from naive scans to ANN/HNSW (must for scale)
   - pgvector supports indexes (e.g., `CREATE INDEX ON videos USING ivfflat (embedding vector_l2_ops) WITH (lists = 100);` or `hnsw`) — for larger datasets, use HNSW or a dedicated vector DB (Pinecone, Milvus, RedisVect, or Cloud managed).
   - Evaluate recall/latency trade-offs on representative datasets. Keep ground truth sets for accuracy tests.

2. Embedding generation throughput (must)
   - Make embedding generation a background task / worker (e.g., use a queue like SQS/Kafka) and rate-limit / batch requests to embedder.
   - Consider using GPU-accelerated inference if throughput requires; or use a hosted inference endpoint.
   - Make embedder a singleton service / long-lived process to reduce model load time.

3. Caching & Reuse (must)
   - Cache recent similarity queries and campaign selections (Redis) with TTL to reduce repeated compute for the same video or user segments.

4. Autoscaling & Resource management (must)
   - Configure HPA/VPA for BidEngine (based on CPU and custom metrics like request latency or queue length).

5. Load testing & benchmarks (must)
   - Scripts: Use `k6` or `Vegeta` to simulate realistic traffic and measure p50/p95/p99 latencies for bid requests and embedding generation.
   - Ramp up: start small (100 rps), grow to target load; measure DB CPU, memory, query times and pgvector search latencies.

6. Query optimization (must)
   - Limit vector search scope with candidate filters (user geography, campaign targeting) before running vector similarity to reduce search costs.

Acceptance Criteria for Phase 3:
- Bid requests at target load meet p95 latency SLA (e.g., < 200ms for decision without embedding generation; embedding generation asynchronous or < 500ms if synchronous).
- ANN index is tested with recall benchmarks and documented trade-offs.

---

## Phase 4 — Reliability & Operational Excellence

1. SLOs, SLIs & Error Budgets (must)
   - Define SLIs (latency, availability, error rate), SLOs, and set error budgets. Document consequences and remediation thresholds.

2. Alerting & Runbooks (must)
   - Add Prometheus alerts tied to SLO breaches (e.g., high p95 latency, high error rate, queue backlog > threshold).
   - Maintain runbooks for the most common alerts and ensure on-call rotation.

3. Chaos & Resilience Testing (should)
   - Simulate node failures, network partitions, and DB failovers using Chaos Engineering tools (e.g., Chaos Mesh/Litmus) in staging.

4. DR & RTO testing (must)
   - Test restore from latest backups to a clean cluster and check data and index integrity.

5. Observability maturity (must)
   - Add tracing for critical paths, add log correlation IDs across services (add to request pipeline and express middleware), and ensure dashboards answer: “Is the system healthy for serving bids?”

Acceptance Criteria for Phase 4:
- SLOs are defined and alerts map to runbooks. DR tests restore a working environment within RTO.

---

## Model Management & Embeddings Lifecycle (Detailed)

Problems to solve: model code and artifacts are binary and can change behavior; embeddings are immutable depending on model version; we must be able to roll back and compare model outputs.

Steps:

1. Model Artifact Storage & Verification (must)
   - Store model artifacts in an object store with semantic versioning (s3://ad-sim-models/all-mini-lm/v1.0.0.zip) and a corresponding checksum and signed manifest.
   - In CI, upload artifacts and record metadata (version, checksum, uploader).

2. Model Manager (must)
   - Implement a small manager in `BidEngine` that downloads a model on start (or via a management API), verifies checksum/signature, extracts to `/var/models/<version>` and sets active model by symlink (`/var/models/current`). Report `model_version` in metrics and health endpoints.
   - Expose an endpoint `/admin/models` to list versions and /admin/models/switch to change active model (guarded by auth & protected network only).

3. Embedding Versioning & Backfills (must)
   - If you change model versions, embeddings will shift. Have a plan: maintain embedding version in DB (e.g., `embedding_model_version` column), allow concurrent versions and a migration pipeline to re-embed content.
   - Backfill strategy: queue video IDs for re-embedding and run workers progressively.

4. Model Testing (must)
   - Create an evaluation set of sample video texts and expected nearest neighbors. After a new model is introduced, run evaluation to ensure recall & precision meet thresholds.

5. Rollback & Canary (must)
   - Deploy new model to a subset of replicas (canary) and monitor similarity drift and business metrics before promoting.

---

## Testing Strategy (Detailed)

1. Unit Tests (must)
   - Keep fast unit tests for pure logic. Use the provider-aware patterns already in code to keep EF in-memory testing stable.

2. Integration Tests (must)
   - Use Testcontainers or a disposable docker-compose in CI to run Postgres (with pgvector extension enabled) and Redis. Run end-to-end tests for seeding, embedding generation, and `/api/bid` flows.

3. Contract Tests (should)
   - Publish OpenAPI contract for the BidController and verify UI against it. Add contract verification in CI between frontend and bid-engine.

4. End-to-End Tests (must)
   - Use a headless browser (Playwright) to run UI flows: home loads ads, video page loads player and ad pane. Tests should include dark/light theme toggles.

5. Performance & Load Tests (must)
   - Scripted load tests using `k6`, include scenarios with cached results, cold embedding generation, and warm path queries.

6. Chaos & Resilience Tests (should)
   - Fail the database master, restart a subset of pods, or blackhole network to verify graceful degradation.

7. Test Data & Determinism (must)
   - Provide deterministic seed datasets with known properties; ensure fallback deterministic embeddings are reproducible in tests.

---

## Observability & Alerting (Detailed)

1. Metrics (add & expose):
   - bid_requests_total{status}
   - bid_latency_seconds{quantile}
   - embedding_generation_count
   - embedding_generation_error
   - db_query_duration_seconds
   - db_vector_search_latency_seconds
   - redis_connection_errors_total
   - model_version_gauge

2. Alert Rules (examples):
   - High latency: ALERT BidLatencyHigh IF histogram_quantile(0.95, sum(rate(bid_latency_seconds_bucket[5m])) by (le)) > 0.4
   - Error rate spike: ALERT BidErrorsHigh IF increase(bid_requests_total{status="error"}[5m]) > 50
   - Embedder failure: ALERT EmbeddingFailures IF increase(embedding_generation_error[5m]) > 5

3. Tracing:
   - Add request traces for key calls (frontend -> /api/bid -> DB queries -> embedder). Correlate trace IDs with logs.

4. Logs:
   - Structured JSON logs (level, ts, trace_id, span_id, request_id, message, component, extra) with sampling for high-volume traces.

5. Dashboards:
   - Operational dashboard for p95 latency, traffic, errors
   - Embeddings dashboard for generation rate, failures, model_version
   - Infrastructure health dashboard (DB replication lag, Redis memory usage)

---

## Security & Compliance (Detailed)

1. Threat model & data classification (must)
   - Define data classes (PII, non-PII). If user identifiers are PII, ensure minimal retention and clear deletion/retention policies.

2. Authentication & Authorization (must)
   - Protect admin endpoints with strong auth (OIDC, OAuth2, or mTLS). Do not expose admin endpoints to public networks.

3. Secrets & keys (must)
   - Store secrets in a vault and grant access via identity. Rotate credentials and audit access.

4. Penetration testing (should)
   - Periodically run pen tests on the deployment; schedule remediation for critical issues.

5. Data Protection (must)
   - Ensure data at rest is encrypted, redact sensitive logs, and ensure backups are also encrypted.

---

## Reliability, Backups & Recovery

1. Postgres backups and PITR (must)
   - Ensure automatic daily backups and WAL-based PITR for minimal data loss.

2. Redis persistence (must)
   - For critical caching that can't be rebuilt quickly, enable AOF/RDB persistence and replicate Redis to avoid data loss.

3. Disaster Recovery plan (must)
   - Define RTO and RPO per component and practice DR drills quarterly.

4. Horizontal redundancy (must)
   - Deploy multiple replicas of `bid-engine` across AZs, use multi-zone DB read replicas for read-heavy workloads.

---

## Deployment Strategies & Progressive Rollouts

1. Canary Releases (must)
   - Deploy new images to a small subset of users and monitor key metrics before increasing traffic.

2. Blue/Green (optional)
   - For large DB or risky migrations, consider blue/green with cutover post migration.

3. Feature Flags (must)
   - Use feature flags (LaunchDarkly/Unleash) to gate features like deterministic fallback, new ranking algorithms, or model versions.

---

## Cost & Capacity Planning

1. Cost drivers: CPU for vector search, storage for embeddings, model inference (CPU vs GPU), DB size and IOPS, and egress.

2. Plan for scale:
   - Start with reasonable instance types and autoscaling policies; collect real metrics and refine.
   - Use spot/preemptible resources for non-critical batch embedding jobs.

3. Cost optimization:
   - Use right-sizing recommendations, reserve stable capacity, and optimize index settings to reduce search cost.

---

## Team & Process Recommendations

1. Define a release cadence and ownership.
2. Create runbooks and assign on-call responsibilities.
3. Practice DR and incident postmortems — publish corrective action items.

---

## Example CI snippet (GitHub Actions)

```yaml
name: CI
on: [push, pull_request]
jobs:
  build-and-test:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:15
        env: POSTGRES_PASSWORD: postgres
        ports: [5432]
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with: dotnet-version: '9.0.x'
      - name: Restore & Build
        run: dotnet build --no-restore
      - name: Run tests
        run: dotnet test --no-build --verbosity normal
      - name: Lint frontend
        run: cd FrontEnd && npm ci && npm test
```

---

## Runbook Templates (Example)

- High latency for /api/bid:
  1. Check `bid_latency_seconds` in Grafana for recent spikes.
  2. Check recent deploys and model switches.
  3. Check DB CPU, query times for vector search, and redis errors.
  4. If caused by embedding generation, re-route new embedding work to background; consider scaling embedder pods.

- Database restore:
  1. Identify latest good backup and timeframe to restore.
  2. Restore to staging and run smoke tests.
  3. Coordinate cutover during low-traffic window.

---

## Appendix: Helpful commands and patterns

- Check embedder is running and model version:
  - `curl http://<bid-engine>/health` or `/admin/models`
- Manual embedding run:
  - `dotnet run --project src/BidEngine -- --seed-vectors`
- Inspect Postgres vectors:
  - `psql -U postgres -d ads_db -c "SELECT id, title, (embedding IS NOT NULL) FROM videos LIMIT 10;"`

---

## Final notes

This roadmap is dense and intentionally prescriptive so you can step through concrete tasks and incrementally increase the system's readiness for production. If you'd like, I can:

- Draft Terraform modules and Helm charts for the stack (example repo + skeleton).
- Provide a sample GitHub Actions pipeline for full CI/CD including container scanning and deployment.
- Help implement the Model Manager and embedder singleton service with sample code.

Tell me which area you'd like to start with and I'll provide a prioritized checklist and templated files for immediate implementation.
