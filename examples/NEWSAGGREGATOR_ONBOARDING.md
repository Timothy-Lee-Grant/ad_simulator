# NewsAggregator — Onboarding Guide (Entry-Level Engineer)

**Repository:** https://github.com/Lev4and/NewsAggregator
**Saved:** `examples/NEWSAGGREGATOR_ONBOARDING.md`
**Purpose:** This document orients a new engineer to the NewsAggregator project: architecture, directory layout, core technologies, how to run it locally, and areas for contribution.

---

**Note on assumptions:** I inspected the repository's public GitHub pages (folder listing, topics, releases and package names). Where I inferred behavior (e.g., scrapers using Selenium/AngleSharp), that is explicitly marked as an assumption — verify by reading the service code in `src/` before making changes.

---

**Table of Contents**

1. **Overview**
2. **High-Level Architecture**
3. **Project Organization**
4. **Service-by-Service Summary**
5. **Core Technologies**
6. **Local Development & Run Instructions**
7. **Deployment & Docker Compose**
8. **Observability & Monitoring**
9. **Testing & QA**
10. **Troubleshooting**
11. **Recommended Learning Path & Next Steps**

---

**1. Overview**

- **What it is:** NewsAggregator collects articles from many sources, processes and serves them via microservices. It uses a Clean Architecture / microservices approach and modern .NET tooling.
- **Primary goals:** Demonstrate microservices on .NET Core (8.0), scraping/parsing pipelines (Selenium/AngleSharp/HtmlAgilityPack), event-driven components (Redis, RabbitMQ), real-time updates (SignalR), and standard infra (Docker, Prometheus, Nginx).
- **Where to start as an engineer:** Read `src/` (Microservices), then `docker-compose.yml` to see how services connect. Focus first on the `News` service to understand the scraping and parsing pipeline.

---

**2. High-Level Architecture**

- Single Git repository organized into microservices under `src/Microservices`.
- Components include:
  - API Gateway(s): `Gateways/NewsAggregator.Gateways.Api` — routes, exposes external endpoints and possibly aggregates service APIs.
  - Microservices: Identity, News, Notification (see `src/Microservices/Services`).
  - BuildingBlocks: shared code used across services (`src/Microservices/BuildingBlocks`).
  - Tools: scraping and parsing helpers, CI scripts, or utility tooling (`tools/`).
  - Infrastructure: `docker-compose.yml` (development), `docker-compose.production.yml`, `nginx.conf`, `prometheus.yml`.

Communication patterns (inferred):
- Synchronous HTTP APIs for public endpoints and inter-service calls via REST.
- Asynchronous messaging using RabbitMQ for events between services.
- Redis likely used for caching and/or transient storage (topics include `redis`).
- PostgreSQL as primary relational database (topic includes `postgresql`).
- SignalR used for pushing real-time updates to clients.

Diagram (logical):

```
[Web UI / Client] ↔ [API Gateway (YARP / Kestrel / Nginx)]
        ↕                   ↕
       Auth              Microservices
                          ├─ News (scraper + API)
                          ├─ Notification (SignalR, push)
                          └─ Identity (Auth)

Message Bus: RabbitMQ (events)
Cache: Redis
DB: PostgreSQL
Monitoring: Prometheus
Reverse Proxy: Nginx
```

---

**3. Project Organization (Files & Folders)**

This repository uses a `src/` root for application code and a `tools/` folder.

Key top-level files and folders (from the GitHub listing):

- `NewsAggregator.sln` — Visual Studio solution file.
- `docker-compose.yml` — Local orchestration for development.
- `docker-compose.override.yml` — Local overrides.
- `docker-compose.production.yml` / `docker-compose-production.override.yml` — Production compose files.
- `nginx.conf` / `nginx.production.conf` — Nginx configuration used as reverse proxy.
- `prometheus.yml` — Prometheus scrape configuration.
- `launchSettings.json` — .NET launch profiles for local debugging.

`src/Microservices/` contains:
- `BuildingBlocks/` — shared helpers, messaging contracts, cross-cutting concerns.
- `Gateways/NewsAggregator.Gateways.Api/` — Gateway API project (routes, auth, aggregation).
- `Services/` containing service subfolders:
  - `Identity/NewsAggregator.Identity` — authentication and user management.
  - `News/` — primary service for scraping, parsing, storage and news API.
  - `Notification/` — real-time notifications and SignalR hub.

`tools/` likely contains scraping jobs, dev scripts, or utilities referenced by CI or CI images.

---

**4. Service-by-Service Summary**

Below are concise descriptions inferred from repo topics and file names. Before editing code, open each service's `Program.cs` and `appsettings*.json` to confirm exact details.

- `NewsAggregator.Gateways.Api` (Gateways)
  - Purpose: public API surface, route requests to services, handle CORS, rate-limiting, and authentication.
  - Likely responsibilities: aggregate endpoints (search across multiple services), host Swagger, and forward websockets/SignalR connections.

- `News` service
  - Purpose: scrape news sources, parse HTML, normalize articles, store articles in DB, expose APIs for querying articles.
  - Scraping: uses Selenium, AngleSharp and HtmlAgilityPack (topics confirm). Selenium likely used for JS-heavy sites; AngleSharp/HtmlAgilityPack for static parsing.
  - Storage: PostgreSQL + EF Core (topic). Also publishing events to RabbitMQ when new articles arrive.
  - Background Workers: a scheduler or background service likely exists to run scrapers at intervals.

- `Notification` service
  - Purpose: provide real-time updates to clients via SignalR, manage push notifications and subscriber lists.
  - Receives events (e.g., NewArticlePublished) from News service via RabbitMQ and pushes to connected clients.

- `Identity` service
  - Purpose: authentication, authorization, user management. Likely uses ASP.NET Core Identity and issues JWT tokens.
  - Responsibilities: register/login, role management, secure gateway APIs.

- `BuildingBlocks` (shared)
  - Contains common utilities: mediation (MediatR patterns), logging, exception handling, event contract classes (integration events), and message bus adapters.

- `tools/` and packages
  - Docker images for `newsaggregator-gateways-api`, `newsaggregator-notification-api`, and `newsaggregator-news-parser` are published — these are the container image names (seen in GitHub Packages). `tools/` may contain scraping helpers, CSVs, or job configs.

---

**5. Core Technologies**

- **Language & Framework:** C# on .NET 8 / ASP.NET Core 8.0
- **Architecture Patterns:** Clean Architecture, Microservices, MediatR (CQRS-like patterns), Background Workers
- **Data Stores:** PostgreSQL (relational), Redis (cache), possibly SQLite for lightweight stores
- **Messaging:** RabbitMQ (event bus)
- **Scraping & Parsing:** Selenium (browser automation), AngleSharp, HtmlAgilityPack
- **Realtime:** SignalR for push notifications / web sockets
- **Logging & Telemetry:** Serilog (and possibly Seq for storage)
- **Containerization:** Docker + Docker Compose
- **Reverse Proxy & Static:** Nginx used in front of gateway for SSL, static assets
- **Monitoring:** Prometheus (via `prometheus.yml`), Grafana likely used externally

---

**6. Local Development & Run Instructions**

Before you begin, ensure the following are installed locally:

- Docker Desktop (Windows/macOS) or Docker Engine (Linux)
- `docker-compose` (if not bundled with Docker Desktop)
- .NET 8 SDK (for inspecting/running individual services)
- An editor (VS Code / Visual Studio)

Quick start (recommended): run full stack with Docker Compose.

1. Clone the repo

```bash
git clone https://github.com/Lev4and/NewsAggregator.git
cd NewsAggregator
```

2. Start services via Docker Compose (development)

```bash
# Start core services in background
docker-compose -f docker-compose.yml -f docker-compose.override.yml up -d --build

# Follow logs
docker-compose logs -f
```

3. Open the app

- Gateway API (check `docker-compose.yml` for exact port mapping)
- RabbitMQ management UI (commonly `http://localhost:15672` if exposed)
- Prometheus (if exposed) — check `prometheus.yml` and compose ports

4. Stop and clean up

```bash
# Stop containers
docker-compose down

# Stop & remove volumes (reset DBs)
docker-compose down -v
```

Run an individual service locally (for debugging):

```bash
cd src/Microservices/Services/News
# Run the API locally using dotnet
dotnet run --project NewsAggregator.News.Api
```

Note: configuration (connection strings, RabbitMQ host, Redis host) is defined in each service's `appsettings.json` and environment vars used in `docker-compose.yml`. Verify and update values before running locally (for example, point RabbitMQ connection to the container name used in compose).

---

**7. Deployment & Docker Compose**

- The repository supplies `docker-compose.yml` and `docker-compose.production.yml`.
- `docker-compose.override.yml` typically contains local development overrides (volume mounts, port mappings, debug flags).
- Use `docker-compose -f docker-compose.yml -f docker-compose.override.yml up -d` for local dev.
- Use the `production` compose files for staging/production deployment (verify secrets and environment variables before use).
- `nginx.conf` / `nginx.production.conf` is used as reverse proxy for TLS, load balancing, and static file hosting.

Ports & services to check in compose (verify exact values in file):
- PostgreSQL: `5432`
- Redis: `6379`
- RabbitMQ: `5672` (AMQP) and `15672` (management)
- Gateway/API: common web ports `80/443` or high-numbered host ports

---

**8. Observability & Monitoring**

- `prometheus.yml` is provided for scraping metrics from services (services should expose `/metrics` endpoints).
- Serilog is used for structured logging and likely ships logs to Seq (repository topics include `Seq`).
- Check the `docker-compose` and `appsettings` for logging sinks and metric instrumentation.

---

**9. Testing & QA**

- Unit tests: check for any `*.Tests` projects under `src/`.
- Integration tests: may be present in `tools/` or in separate test projects.
- For scraping-related code (Selenium), set up a headless browser (Chrome/Chromium) container when running scraping jobs locally.
- CI: check `.github/workflows` for action definitions if present.

---

**10. Troubleshooting (Quick wins)**

- If `docker-compose up` fails with port errors:
  - `lsof -i :<port>` (macOS/Linux) to find process using port.
  - Change the host port in `docker-compose.override.yml` if needed.

- If RabbitMQ queues are empty but producers claim to publish:
  - Open `http://localhost:15672` and inspect Exchanges/Queues (guest/guest credentials typical in dev compose).

- PostgreSQL connection failures:
  - Wait for the DB container to finish initialization.
  - Check connection string in service `appsettings.*.json`.

- Selenium scraping fails locally:
  - Confirm headless browser binary is available in the container (Chromium/Chrome).
  - Prefer to run scraper containers rather than executing Selenium on host if possible.

---

**11. Recommended Learning Path & Next Steps**

For entry-level engineers, follow this path:

- Step 1: Read the code structure
  - Open `src/Microservices/Services/News` and find the scraping pipeline and background worker.
  - Open `src/Microservices/BuildingBlocks` to understand shared patterns (MediatR, event contracts).

- Step 2: Run the stack locally
  - Run `docker-compose` and verify services start, inspect RabbitMQ and PostgreSQL.

- Step 3: Explore one feature end-to-end
  - Add a small change to the News service (e.g., add an extra field to stored Article, run migrations).
  - Trigger a scrape job and verify data arrives and the Notification service pushes an event.

- Step 4: Add tests
  - Implement unit tests for parsing logic (AngleSharp/HtmlAgilityPack parsers) — these are high value and low risk.

- Step 5: Observability & Resilience
  - Add Prometheus metric counters to critical paths (articles scraped, parse errors).
  - Add retry/circuit-breaker (Polly) for HTTP calls to external sites.

---

**Appendix: Useful Commands**

- Start local stack

```bash
docker-compose -f docker-compose.yml -f docker-compose.override.yml up -d --build
```

- Tail logs

```bash
docker-compose logs -f --tail=200
```

- Stop and remove

```bash
docker-compose down -v
```

- Run a single service locally

```bash
cd src/Microservices/Services/News
dotnet run --project NewsAggregator.News.Api
```

---

**Where to look next in the repo**

- `src/Microservices/Services/News` — scraping jobs, parser implementations, article model and DB access.
- `src/Microservices/Services/Notification` — SignalR hub, event consumer code.
- `src/Microservices/Services/Identity` — authentication flows, JWT issuance and user management.
- `src/Microservices/Gateways/NewsAggregator.Gateways.Api` — API surface and aggregation rules.
- `src/Microservices/BuildingBlocks` — messaging contracts, helper classes shared across services.
- `docker-compose*.yml`, `nginx.conf`, and `prometheus.yml` — infra orchestration and monitoring config.

---

If you'd like, I can now:
- Open and summarize specific files (e.g., `News` service `Program.cs`) to produce exact run-time ports and environment variables.
- Create example local env files or a `README.local.md` with exactly reproducible port mappings.
- Add basic unit tests for the parsing code (if you want me to scan and generate tests).

Which of these next steps would you like me to perform now? (I can read specific files and update the onboarding doc with exact, verified values.)
