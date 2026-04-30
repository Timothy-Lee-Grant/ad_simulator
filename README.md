# ad_simulator
# ad_simulator

A professional advertising platform simulation that combines a real-time bidding engine, semantic campaign matching, caching, analytics, and observability.

This repository contains a .NET-based mini ad-server and bidding stack built for experimentation, training, and prototype deployments.

## Project Summary

`ad_simulator` is a modular backend system that simulates a modern advertising infrastructure with the following capabilities:

- Real-time bid evaluation and winning-campaign selection
- PostgreSQL-backed campaign and video storage with `pgvector` support for embeddings
- Redis caching for fast campaign lookup and budget management
- Metrics and observability through Prometheus and Grafana
- Optional event-driven pipeline support with Kafka
- A simple Node/Express frontend for service demonstration

## Architecture Overview

The core system is built around a microservices pattern with the following components:

- `src/BidEngine` - The primary bid evaluation service and API layer
- `src/Shared` - Shared domain models used by services across the repository
- `tests/BidEngine.Tests` - Unit tests for the bid engine behavior
- `FrontEnd` - Lightweight Express.js frontend that consumes the bid engine API
- `infrastructure/database/migrations` - SQL migration scripts for PostgreSQL and `pgvector`
- `docs/` - Supporting documentation for architecture, API, deployment, and operations

### Primary Service Responsibilities

- `BidEngine` handles requests for bidding, active campaign selection, budget deduction, and vectorized semantic matching.
- `Shared` contains cross-service models such as `BidRequest`, `Campaign`, and `Video`.
- `FrontEnd` provides a simple demonstration UI and can be extended for interactive testing.

## Technologies Used

- .NET 9 / ASP.NET Core
- Entity Framework Core 9
- PostgreSQL with `pgvector` extension
- Redis for caching and transient budget state
- Prometheus for metrics collection
- Grafana for dashboarding
- Docker Compose for environment orchestration
- Node.js + Express + EJS for the frontend demo
- Optional Kafka for event streaming

## Requirements

- Docker and Docker Compose
- .NET 9 SDK (for local development without containers)
- Node.js 18+ (for frontend development outside Docker)
- Git

## Quick Start

### 1. Clone the repository

```bash
git clone https://github.com/<your-org>/ad_simulator.git
cd ad_simulator
```

### 2. Start the required infrastructure

This repository includes a base `docker-compose.yml` that launches PostgreSQL, Redis, Prometheus, Grafana, the bid engine service, and the frontend.

```bash
docker compose up -d
```

### 3. Optional: enable Kafka

Kafka is not started by default to keep the development environment light. Enable it when you need event streaming support.

```bash
docker compose -f docker-compose.yml -f docker-compose.kafka.yml up -d
```

### 4. Verify services

- Bid Engine: http://localhost:8081
- Frontend UI: http://localhost:3001
- Prometheus: http://localhost:9090
- Grafana: http://localhost:3000

### 5. Stop services

```bash
docker compose down
```

## Running the Bid Engine Locally

If you prefer to run the bid engine without Docker:

```bash
cd src/BidEngine
dotnet restore
dotnet run --urls=http://localhost:8081
```

The service will use the `ConnectionStrings:DefaultConnection` value from `src/BidEngine/appsettings.json` by default.

## Environment Configuration

The Bid Engine is configured to use the following defaults when run inside Docker:

- `ConnectionStrings__DefaultConnection=Server=postgres;Port=5432;Database=ads_db;User ID=postgres;Password=postgres`
- `Redis__ConnectionString=redis:6379`
- `ASPNETCORE_URLS=http://+:8081`

For AWS or production-style environments, use the `AWS_DB_CONN` environment variable or configure connection strings in `appsettings.json`.

## API Summary

### Bid API

- `POST /api/bid`
  - Evaluates active campaigns and returns a winning bid response
  - Request payload: `BidRequest`
  - Response: `BidResponse` or `204 No Content`

- `GET /api/bid/test`
  - Health check endpoint for the bid engine service

- `GET /api/bid/User_Click_Event` 
  - Records a simulated click event and increments Prometheus click metrics
  - Query parameters: `campaignId`, `adId`, `userId`

### Admin API

- `POST /api/admin/seed-vectors`
  - Generates embeddings for videos and stores them in the database

- `POST /api/admin/seed-vectors-with-debugging`
  - Same as `seed-vectors` with additional log detail

- `POST /api/admin/seed-vector-ads`
  - Generates vector embeddings for ad assets

### Video API

- `GET /api/videos?limit=3`
  - Returns a paginated preview of available videos

- `GET /api/videos/{id}`
  - Returns metadata for a specific video

## Observability

The project includes built-in Prometheus metrics exposed by the bid engine. Metrics may include:

- `bid_requests_total`
- `bid_latency_seconds`
- `ad_clicks_total`

Use Grafana to build dashboards and monitor service health.

## Testing

Run the unit test suite for the bid engine:

```bash
cd tests/BidEngine.Tests
dotnet test
```

## Project Structure

- `src/BidEngine/` - Main bid engine API, services, data access, and migrations
- `src/Shared/` - Shared domain classes and models
- `tests/BidEngine.Tests/` - Unit tests for core bid engine logic
- `FrontEnd/` - Demo frontend service and static assets
- `infrastructure/database/migrations/` - PostgreSQL initialization scripts and schema migrations
- `docs/` - Architecture, design, API, deployment, and operational documentation

## Notes

- `src/BidEngine/Program.cs` configures `pgvector` support for PostgreSQL and uses `StackExchange.Redis` for caching.
- `docker-compose.yml` defines the core local development stack.
- Kafka is optional and available through `docker-compose.kafka.yml`.
- Swagger UI is available in development mode for API exploration.

## Documentation

For deeper architecture details, deployment guidance, and API reference, consult the `docs/` directory. Key documents include:

- `docs/01_ARCHITECTURE_AND_DESIGN.md`
- `docs/02_STEP_BY_STEP_IMPLEMENTATION_GUIDE.md`
- `docs/03_API_DOCUMENTATION.md`
- `docs/04_DEPLOYMENT_GUIDE.md`
- `docs/05_PROJECT_STRUCTURE.md`

## Contributing

Contributions are welcome. Use the existing documentation and code comments as guidance, and open issues for bug reports or feature requests.

---

For an enterprise-ready advertising simulation, `ad_simulator` is designed to demonstrate how real-time bidding, vectorized campaign matching, caching, and observability work together in a modern .NET stack.
