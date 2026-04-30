# Docker and Compose Audit

## Scope and Validation Performed

- Inspected:
  - `src/BidEngine/Dockerfile`
  - `FrontEnd/Dockerfile`
  - `docker-compose.yml`
  - `docker-compose.kafka.yml`
  - `.dockerignore`
  - `.env`
- Executed:
  - `docker --version` -> Docker `28.3.2`
  - `docker compose version` -> Compose `v2.39.1-desktop.1`
  - `docker compose config` -> parses successfully (with warnings)
  - `docker compose -f docker-compose.yml -f docker-compose.kafka.yml config` -> parses successfully (with warnings)
  - `docker compose build --no-cache bid-engine frontend` -> not executable in current session because Docker daemon is not reachable (`Cannot connect to ... docker.sock`)

---

## 1. What Works

- **Compose syntax validates**
  - Base and Kafka-override compose files both resolve correctly via `docker compose config`.
- **Service graph is coherent**
  - `bid-engine` correctly depends on healthy `postgres` and `redis`.
  - `frontend` depends on `bid-engine`.
- **Container port mapping is clear**
  - `bid-engine:8081`, `frontend:3001->3000`, `prometheus:9090`, `grafana:3000`, `redis:6379`, `postgres:5434->5432`.
- **Dockerfiles are structurally valid**
  - .NET Dockerfile uses multi-stage build.
  - Frontend Dockerfile uses a slim `node:18-alpine` base.
- **Build context filtering exists**
  - `.dockerignore` excludes common heavy folders (`.git`, `bin`, `obj`, `node_modules`).

---

## 2. What Is Outdated

- **Obsolete Compose `version` field**
  - Both compose files use `version: '3.8'`.
  - Compose v2 warns this is obsolete and ignored.
- **Unpinned floating tags**
  - `grafana/grafana:latest` and `prom/prometheus:latest` are mutable and can break reproducibility.
- **Node base image age**
  - `node:18-alpine` is functional but older LTS line; evaluate upgrade path to current active LTS.
- **Comment drift in compose**
  - Some commented blocks suggest alternate topology states; risk of docs/config drift over time.

---

## 3. Security Issues

- **Critical: plaintext secret in `.env`**
  - `AWS_DB_CONN` includes full credentials and password.
- **Critical: secret exposed via compose rendering**
  - `docker compose config` output materializes `ConnectionStrings__AwsConnection` in plain text.
- **Weak default credentials**
  - `POSTGRES_PASSWORD=postgres`
  - `GF_SECURITY_ADMIN_PASSWORD=admin`
- **Broad host exposure**
  - Postgres, Redis, Grafana, Prometheus, Kafka, and Zookeeper are exposed on host ports; unsafe for shared/public hosts.
- **No explicit container hardening**
  - No `read_only`, `cap_drop`, `security_opt`, or non-root `user` enforcement in compose services.

---

## 4. Inefficiencies

- **BidEngine Dockerfile does redundant work**
  - Performs `dotnet build` then `dotnet publish`; publish already compiles.
- **Duplicate model copy in BidEngine Dockerfile**
  - `COPY src/BidEngine/model/ ...` appears in builder and runtime stages; one copy path could be enough depending on publish behavior.
- **Frontend image build not fully deterministic**
  - Uses `npm install` instead of `npm ci` lockfile-first strategy.
- **No healthchecks for app containers**
  - `bid-engine` and `frontend` lack healthchecks; orchestration cannot distinguish "started" vs "ready".
- **No resource controls**
  - Missing CPU/memory limits/reservations can cause noisy-neighbor instability.

---

## 5. Missing Environment Management

- **No committed env template**
  - Missing `.env.example` or documented required variable contract.
- **Mixed connection-string strategy**
  - Compose injects `ConnectionStrings__AwsConnection`, but service code primarily consumes `DefaultConnection` (config mismatch risk).
- **Secrets not externalized**
  - No Docker secrets, vault integration, or CI secret-injection pattern documented in compose path.
- **No env profiles**
  - Dev/staging/prod compose overlays are not separated cleanly for credentials, ports, and hardening options.

---

## 6. Production Improvements

- **Secrets and auth**
  - Move all secrets to a secret manager or runtime secret injection; rotate exposed credentials immediately.
  - Replace static admin passwords and DB defaults.
- **Deterministic images**
  - Pin image versions (or digests) for Grafana/Prometheus/postgres/redis/kafka.
  - Move frontend to `npm ci` with lockfile enforcement.
- **Container hardening**
  - Run non-root where possible; add `read_only`, `tmpfs`, `cap_drop: [ALL]`, and explicit security opts.
- **Network posture**
  - Remove unnecessary host port publishes for internal-only services (Redis/Postgres/Kafka/Zookeeper).
  - Keep only externally required entrypoints exposed.
- **Reliability**
  - Add healthchecks for `bid-engine` and `frontend`.
  - Add restart policies and resource limits.
  - Consider readiness-focused dependency strategy beyond `depends_on`.
- **Build optimization**
  - Simplify BidEngine Dockerfile to restore -> publish only.
  - Ensure Docker layer cache optimization by copying project files before source trees.
- **Environment clarity**
  - Add `.env.example`, environment matrix, and explicit precedence rules for connection strings.
  - Separate `docker-compose.dev.yml` and `docker-compose.prod.yml` patterns.
- **Supply-chain hygiene**
  - Add image scanning (e.g., Trivy/Grype) in CI.
  - Add SBOM generation and dependency update automation.

---

## Practical Next Step

When Docker daemon is running, re-run:
- `docker compose build --no-cache bid-engine frontend`
- `docker compose up -d`
- `docker compose ps`
- smoke test script `scripts/e2e_smoke.sh`

This will confirm runtime behavior beyond static/parse-level validation.
