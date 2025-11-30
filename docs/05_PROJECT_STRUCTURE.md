# Mini Ad Server and Bidding Engine
## Project Structure & Quick Reference Guide

**Version:** 1.0  
**Format:** Quick lookup guide  
**Updated:** November 29, 2025

---

## 📁 Complete Project File Structure

```
ad_simulator/
│
├── docs/
│   ├── 01_ARCHITECTURE_AND_DESIGN.md      ← Start here for overview
│   ├── 02_STEP_BY_STEP_IMPLEMENTATION_GUIDE.md  ← Detailed walkthrough
│   ├── 03_API_DOCUMENTATION.md            ← API reference
│   ├── 04_DEPLOYMENT_GUIDE.md             ← Deployment & operations
│   ├── 05_PROJECT_STRUCTURE.md            ← This file
│   └── README.md                          ← Quick start
│
├── src/
│   ├── AdServer/
│   │   ├── AdServer.csproj               ← Project file
│   │   ├── Program.cs                    ← App startup
│   │   ├── appsettings.json              ← Config (dev)
│   │   ├── appsettings.Production.json   ← Config (prod)
│   │   ├── Dockerfile                    ← Container build
│   │   │
│   │   ├── Controllers/
│   │   │   ├── AdController.cs           ← GET /serve
│   │   │   ├── ClickController.cs        ← POST /click
│   │   │   └── HealthController.cs       ← GET /health
│   │   │
│   │   ├── Services/
│   │   │   ├── BidEngineClient.cs        ← HTTP client to Bid Engine
│   │   │   ├── EventPublisher.cs         ← Kafka producer
│   │   │   ├── AdResponseBuilder.cs      ← Build response objects
│   │   │   └── RequestValidator.cs       ← Validate inputs
│   │   │
│   │   ├── Models/
│   │   │   ├── AdResponse.cs             ← API response model
│   │   │   ├── ServeRequest.cs           ← API request model
│   │   │   └── ClickEvent.cs             ← Click event model
│   │   │
│   │   ├── Middleware/
│   │   │   ├── RequestLoggingMiddleware.cs  ← Log all requests
│   │   │   └── ExceptionHandlingMiddleware.cs ← Centralized errors
│   │   │
│   │   └── Metrics/
│   │       └── PrometheusMetrics.cs      ← Custom metrics
│   │
│   ├── BidEngine/
│   │   ├── BidEngine.csproj
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── appsettings.Production.json
│   │   ├── Dockerfile
│   │   │
│   │   ├── Controllers/
│   │   │   ├── BidController.cs          ← POST /api/bid
│   │   │   └── HealthController.cs
│   │   │
│   │   ├── Services/
│   │   │   ├── BidSelector.cs            ← Core algorithm
│   │   │   ├── CampaignCache.cs          ← Redis caching
│   │   │   ├── BudgetService.cs          ← Budget tracking
│   │   │   └── TargetingEngine.cs        ← Rule matching
│   │   │
│   │   ├── Models/
│   │   │   ├── Campaign.cs               ← Campaign entity
│   │   │   ├── Ad.cs                     ← Ad entity
│   │   │   ├── TargetingRule.cs          ← Targeting entity
│   │   │   ├── BidRequest.cs             ← API request
│   │   │   └── BidResponse.cs            ← API response
│   │   │
│   │   └── Data/
│   │       ├── AppDbContext.cs           ← EF Core context
│   │       └── Migrations/               ← Database migrations
│   │           ├── InitialSchema.sql
│   │           └── ...
│   │
│   ├── EventConsumer/
│   │   ├── EventConsumer.csproj
│   │   ├── Program.cs                    ← Main entry point
│   │   ├── appsettings.json
│   │   │
│   │   ├── Services/
│   │   │   ├── KafkaConsumerService.cs   ← Kafka polling
│   │   │   ├── MetricsAggregator.cs      ← In-memory aggregation
│   │   │   └── MetricsPersistence.cs     ← Batch writes to DB
│   │   │
│   │   ├── Models/
│   │   │   ├── Event.cs                  ← Event entity
│   │   │   └── MetricsBatch.cs           ← Batch writes
│   │   │
│   │   └── Data/
│   │       └── AppDbContext.cs
│   │
│   ├── AnalyticsService/
│   │   ├── AnalyticsService.csproj
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── Dockerfile
│   │   │
│   │   ├── Controllers/
│   │   │   └── AnalyticsController.cs    ← GET /analytics
│   │   │
│   │   ├── Services/
│   │   │   ├── AnalyticsQueryService.cs  ← Query builder
│   │   │   └── CacheService.cs           ← Cache management
│   │   │
│   │   ├── Models/
│   │   │   ├── AnalyticsResponse.cs
│   │   │   └── MetricsSummary.cs
│   │   │
│   │   └── Data/
│   │       └── AppDbContext.cs
│   │
│   └── Shared/
│       ├── Shared.csproj
│       ├── Models/
│       │   ├── Event.cs                  ← Shared event model
│       │   └── Constants.cs              ← Magic strings, config
│       ├── Extensions/
│       │   ├── ServiceCollectionExtensions.cs
│       │   └── LoggingExtensions.cs
│       └── Utilities/
│           ├── DateTimeHelper.cs
│           └── JsonHelper.cs
│
├── tests/
│   ├── AdServer.Tests/
│   │   ├── AdServer.Tests.csproj
│   │   ├── Controllers/
│   │   │   ├── AdControllerTests.cs      ← Test /serve endpoint
│   │   │   └── ClickControllerTests.cs   ← Test /click endpoint
│   │   ├── Services/
│   │   │   └── BidEngineClientTests.cs   ← Mock Bid Engine
│   │   └── Fixtures/
│   │       └── TestDataGenerator.cs      ← Generate test data
│   │
│   ├── BidEngine.Tests/
│   │   ├── BidEngine.Tests.csproj
│   │   ├── Services/
│   │   │   ├── BidSelectorTests.cs       ← Test algorithm
│   │   │   ├── BudgetServiceTests.cs     ← Test budget logic
│   │   │   └── CampaignCacheTests.cs     ← Test caching
│   │   └── Fixtures/
│   │       └── CampaignFactory.cs        ← Create test campaigns
│   │
│   └── Integration.Tests/
│       ├── Integration.Tests.csproj
│       ├── AdServingFlowTests.cs         ← End-to-end tests
│       ├── EventPipelineTests.cs         ← Kafka tests
│       └── Fixtures/
│           └── TestDatabaseFixture.cs    ← In-memory DB for tests
│
├── infrastructure/
│   ├── docker/
│   │   └── docker-compose.yml            ← All services
│   │
│   ├── database/
│   │   ├── migrations/
│   │   │   ├── 001_initial_schema.sql
│   │   │   ├── 002_create_indexes.sql
│   │   │   └── 003_seed_data.sql
│   │   └── scripts/
│   │       ├── backup.sh                 ← Database backup
│   │       ├── restore.sh                ← Database restore
│   │       └── reset.sh                  ← Reset database
│   │
│   ├── monitoring/
│   │   ├── prometheus.yml                ← Prometheus config
│   │   ├── rules.yml                     ← Alert rules
│   │   ├── grafana-dashboard.json        ← Dashboard export
│   │   ├── dashboards/
│   │   │   ├── service-health.json
│   │   │   ├── ad-serving.json
│   │   │   ├── bid-engine.json
│   │   │   ├── event-pipeline.json
│   │   │   └── business-metrics.json
│   │   └── datasources/
│   │       └── prometheus.json
│   │
│   ├── kubernetes/
│   │   ├── namespace.yaml
│   │   ├── configmap.yaml               ← Config management
│   │   ├── secrets.yaml                 ← Sensitive data
│   │   ├── ad-server-deployment.yaml
│   │   ├── bid-engine-deployment.yaml
│   │   ├── event-consumer-statefulset.yaml
│   │   ├── analytics-deployment.yaml
│   │   ├── postgres-statefulset.yaml
│   │   ├── redis-statefulset.yaml
│   │   ├── kafka-statefulset.yaml
│   │   ├── services.yaml
│   │   ├── ingress.yaml
│   │   └── hpa.yaml                     ← Auto-scaling rules
│   │
│   └── scripts/
│       ├── setup.sh                      ← Initial setup
│       ├── start.sh                      ← Start services
│       ├── stop.sh                       ← Stop services
│       ├── reset.sh                      ← Reset everything
│       └── test.sh                       ← Run tests
│
├── .gitignore
├── .dockerignore
├── docker-compose.yml                   ← Root compose file
├── docker-compose.prod.yml               ← Production compose
├── global.json                           ← .NET SDK version
├── SOLUTION.sln                          ← Visual Studio solution
└── README.md                             ← Project overview

```

---

## 🔑 Key Files & Their Purposes

### Configuration Files

| File | Purpose | Environment |
|------|---------|-------------|
| `src/*/appsettings.json` | Default configuration | All |
| `src/*/appsettings.Development.json` | Development overrides | Dev |
| `src/*/appsettings.Production.json` | Production overrides | Prod |
| `docker-compose.yml` | Local development stack | Dev |
| `docker-compose.prod.yml` | Production stack | Prod |
| `infrastructure/monitoring/prometheus.yml` | Metrics collection | All |
| `infrastructure/kubernetes/*.yaml` | K8s deployments | Prod |

### Database Schema

| File | Purpose |
|------|---------|
| `infrastructure/database/migrations/001_initial_schema.sql` | Tables, relationships |
| `infrastructure/database/migrations/002_create_indexes.sql` | Performance indexes |
| `infrastructure/database/migrations/003_seed_data.sql` | Sample data |

### Testing

| File | Purpose |
|------|---------|
| `tests/AdServer.Tests/*` | Unit tests for Ad Server |
| `tests/BidEngine.Tests/*` | Unit tests for Bid Engine |
| `tests/Integration.Tests/*` | End-to-end tests |

---

## 🔗 Dependencies Between Services

### Ad Server depends on:
```
AdServer
├── PostgreSQL (campaigns, events log)
├── Redis (cache, rate limiting)
├── Kafka (publish events)
├── BidEngine (determine winning campaign)
└── Metrics (Prometheus)
```

### Bid Engine depends on:
```
BidEngine
├── PostgreSQL (campaign master data)
├── Redis (campaign cache)
└── Metrics (Prometheus)
```

### Event Consumer depends on:
```
EventConsumer
├── Kafka (consume events)
├── PostgreSQL (write aggregates)
└── Metrics (Prometheus)
```

### Analytics Service depends on:
```
AnalyticsService
├── PostgreSQL (read metrics)
├── Redis (cache results)
└── Metrics (Prometheus)
```

---

## 📋 Configuration Reference

### Environment Variables

| Variable | Service | Purpose | Example |
|----------|---------|---------|---------|
| `ASPNETCORE_ENVIRONMENT` | All | Development/Production mode | Production |
| `ConnectionStrings__DefaultConnection` | All | PostgreSQL connection | Server=localhost;... |
| `Redis__ConnectionString` | Server, Engine, Analytics | Redis connection | redis:6379 |
| `Kafka__BootstrapServers` | Server, Consumer | Kafka brokers | kafka:9092 |
| `ASPNETCORE_URLS` | All | HTTP binding | http://+:8080 |
| `Logging__LogLevel__Default` | All | Log level | Information |

### Default Ports

| Service | Port | Purpose |
|---------|------|---------|
| Ad Server | 8080 | Main API |
| Bid Engine | 8081 | Internal API |
| Analytics Service | 8082 | Reporting API |
| PostgreSQL | 5432 | Database |
| Redis | 6379 | Cache |
| Kafka | 9092 | Message broker |
| Zookeeper | 2181 | Kafka coordination |
| Prometheus | 9090 | Metrics scraping |
| Grafana | 3000 | Dashboards |

---

## 🔄 Data Flow: Which Service Calls What

### Serving an Ad (Request)
```
Client
  ↓ (HTTP GET /serve)
Ad Server
  ├─ (HTTP POST /api/bid) → Bid Engine
  │   ├─ (Redis GET campaign:*) → Redis
  │   ├─ (SELECT * FROM campaigns) → PostgreSQL
  │   └─ (returns BidResponse) →
  ├─ (Kafka publish impression event) → Kafka
  └─ (returns AdResponse) →
Client
```

### Processing Events (Background)
```
Kafka Topic: impressions
  ↓
Event Consumer
  ├─ (reads events continuously)
  ├─ (aggregates in-memory)
  ├─ (every 10s: INSERT/UPDATE daily_metrics) → PostgreSQL
  └─ (Kafka consumer offset) → Kafka
```

### Querying Analytics
```
Client
  ↓ (HTTP GET /analytics/campaign/...)
Analytics Service
  ├─ (Redis GET analytics:campaign:date) → Redis
  │   If cache miss:
  ├─ (SELECT * FROM daily_metrics) → PostgreSQL
  └─ (Redis SET analytics:campaign:date, TTL 1h) → Redis
  └─ (returns AnalyticsResponse) →
Client
```

---

## 💾 Database Relationships

### Entity Relationship Diagram (ERD)

```
┌──────────────────┐
│    CAMPAIGNS     │
├──────────────────┤
│ id (PK, UUID)    │◄─────────────────┐
│ name             │                  │
│ advertiser_id    │                  │
│ status           │                  │
│ cpm_bid          │                  │ One-to-Many
│ daily_budget     │                  │
│ spent_today      │                  │
│ ...              │                  │
└──────────────────┘                  │
        │ One-to-Many                 │
        │                             │
        ▼                             │
┌──────────────────┐                 │
│       ADS        │                 │
├──────────────────┤                 │
│ id (PK, UUID)    │                 │
│ campaign_id (FK) │─────────────────┘
│ title            │
│ image_url        │
│ redirect_url     │
└──────────────────┘

┌──────────────────────────┐
│ CAMPAIGN_TARGETING_RULES │
├──────────────────────────┤
│ id (PK, UUID)            │
│ campaign_id (FK)         │◄────────── One-to-Many
│ rule_type                │            from Campaigns
│ rule_value               │
└──────────────────────────┘

┌──────────────────────────┐
│   DAILY_METRICS          │
├──────────────────────────┤
│ id (PK, BIGSERIAL)       │
│ campaign_id (FK)         │◄────────── Many-to-One
│ date (DATE)              │            to Campaigns
│ impressions              │
│ clicks                   │
│ spend                    │
│ UNIQUE (campaign_id, date)
└──────────────────────────┘

┌──────────────────────────┐
│    EVENTS_LOG            │
├──────────────────────────┤
│ id (PK, BIGSERIAL)       │
│ event_id (UNIQUE, UUID)  │
│ event_type               │
│ campaign_id (FK)         │◄────────── Many-to-One
│ ad_id (FK)               │            to Campaigns & Ads
│ user_id                  │
│ timestamp                │
└──────────────────────────┘
```

---

## 🧪 Test Organization

### Test Types

**Unit Tests** (test single class in isolation)
```
BidSelectorTests
├── TestSelectHighestBidWins()
├── TestFilterByCountryTargeting()
├── TestFilterByDeviceTypeTargeting()
└── TestNoBidsAvailableReturnsNull()

BudgetServiceTests
├── TestDeductBudgetSuccess()
├── TestDeductBudgetExceedsDailyBudget()
└── TestDeductBudgetExceedsLifetimeBudget()
```

**Integration Tests** (test multiple components)
```
AdServingFlowTests
├── TestFullAdServingFlow()
├── TestClickEventIsPublished()
└── TestMetricsAreAggregated()

EventPipelineTests
├── TestKafkaEventConsumption()
├── TestMetricsAreWrittenToDatabase()
└── TestConsumerRecoveryAfterFailure()
```

---

## 🚀 Quick Commands Reference

### Docker Operations

```bash
# Start everything
docker-compose up -d

# Stop everything
docker-compose down

# View logs
docker-compose logs -f ad-server
docker-compose logs -f bid-engine

# Restart a service
docker-compose restart ad-server

# Run one-off command
docker-compose exec ad-server dotnet user-secrets list

# Remove data (reset state)
docker-compose down -v
```

### Database Operations

```bash
# Connect to database
psql -h localhost -U postgres -d ads_db

# Run SQL script
psql -h localhost -U postgres -d ads_db < script.sql

# Backup
pg_dump -h localhost -U postgres -d ads_db > backup.sql

# Check connection count
psql -h localhost -U postgres -c "SELECT count(*) FROM pg_stat_activity;"
```

### Kafka Operations

```bash
# List topics
docker exec ads_kafka kafka-topics --bootstrap-server localhost:9092 --list

# Check consumer lag
docker exec ads_kafka kafka-consumer-groups --bootstrap-server localhost:9092 \
  --group ads-event-consumer --describe

# Publish test message
docker exec ads_kafka kafka-console-producer --broker-list localhost:9092 \
  --topic impressions

# Consume messages
docker exec ads_kafka kafka-console-consumer --bootstrap-server localhost:9092 \
  --topic impressions --from-beginning
```

### Testing

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter ClassName=BidSelectorTests

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverageFileName=coverage.xml

# Load test with k6
k6 run load-test.js
```

### Monitoring

```bash
# View Prometheus targets
curl http://localhost:9090/api/v1/targets

# Query metrics
curl 'http://localhost:9090/api/v1/query?query=bid_requests_total'

# Get Grafana dashboard
curl -u admin:admin http://localhost:3000/api/dashboards/db/ad-serving
```

---

## 📊 Metrics Exposed

### Ad Server Metrics
```
http_requests_total{endpoint="/serve",status="200"}
http_request_duration_seconds{endpoint="/serve",le="0.1"}
ad_served_total{campaign_id="..."}
bid_engine_errors_total
```

### Bid Engine Metrics
```
bid_requests_total{status="success"}
bid_requests_total{status="no_bid"}
bid_latency_seconds{quantile="0.95"}
campaign_budget_remaining{campaign_id="..."}
cache_hit_ratio
```

### Event Consumer Metrics
```
kafka_consumer_lag{topic="impressions"}
events_processed_total{event_type="impression"}
events_processed_total{event_type="click"}
metrics_batch_write_duration_seconds
```

---

## 🎯 Implementation Roadmap

### Phase 1: Database & Infrastructure ✓
- [ ] Create PostgreSQL schema
- [ ] Set up Redis
- [ ] Configure Kafka
- [ ] Create docker-compose.yml

### Phase 2: Bid Engine ✓
- [ ] Implement Campaign model
- [ ] Create CampaignCache service
- [ ] Implement BidSelector algorithm
- [ ] Create BudgetService
- [ ] Write BidController endpoint
- [ ] Add unit tests

### Phase 3: Ad Server ✓
- [ ] Implement AdController (/serve endpoint)
- [ ] Implement ClickController (/click endpoint)
- [ ] Create BidEngineClient
- [ ] Create EventPublisher (Kafka)
- [ ] Add Prometheus metrics
- [ ] Add request/error handling

### Phase 4: Event Pipeline ✓
- [ ] Set up Kafka topics
- [ ] Create EventConsumer service
- [ ] Implement MetricsAggregator
- [ ] Implement MetricsPersistence
- [ ] Handle failures and retries

### Phase 5: Analytics Service ✓
- [ ] Implement AnalyticsController
- [ ] Create AnalyticsQueryService
- [ ] Add caching layer
- [ ] Write aggregation queries

### Phase 6: Testing ✓
- [ ] Write unit tests (>80% coverage)
- [ ] Write integration tests
- [ ] Create load test scenario
- [ ] Performance validation

### Phase 7: Monitoring ✓
- [ ] Configure Prometheus
- [ ] Create Grafana dashboards
- [ ] Set up alert rules
- [ ] Configure logging

### Phase 8: Deployment ✓
- [ ] Create Kubernetes manifests
- [ ] Set up CI/CD pipeline
- [ ] Document deployment process
- [ ] Create runbooks

---

## 📞 Support Matrix

| Issue | Documentation | File |
|-------|---------------|------|
| How does the system work? | Architecture & Design | 01_ARCHITECTURE_AND_DESIGN.md |
| How do I build it? | Step-by-Step Guide | 02_STEP_BY_STEP_IMPLEMENTATION_GUIDE.md |
| API endpoint details | API Documentation | 03_API_DOCUMENTATION.md |
| How do I deploy it? | Deployment Guide | 04_DEPLOYMENT_GUIDE.md |
| File/folder layout | This document | 05_PROJECT_STRUCTURE.md |
| Quick start | README | README.md |

---

## ✅ Pre-Implementation Checklist

Before starting implementation, verify:

- [ ] .NET 8 SDK installed
- [ ] Docker Desktop installed
- [ ] Git configured
- [ ] PostgreSQL client tools available
- [ ] Text editor/IDE ready (VS Code, Visual Studio)
- [ ] 30+ GB disk space available
- [ ] Port 5432, 6379, 9092, 8080-8082, 9090, 3000 are available
- [ ] All documentation reviewed

---

**Project Structure Complete!**

You now have a comprehensive understanding of how the files are organized and how they relate to each other. Use this document as a reference while implementing each component.

