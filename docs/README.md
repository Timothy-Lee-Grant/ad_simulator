# Mini Ad Server and Real-Time Bidding Engine
## Complete Project Documentation

**Status:** Documentation Complete - Ready for Implementation  
**Version:** 1.0  
**Last Updated:** November 29, 2025  
**Tech Stack:** C# / .NET 8, PostgreSQL, Redis, Apache Kafka

---

## 📋 Quick Navigation

This project includes comprehensive documentation for understanding and implementing a production-grade advertising platform:

### Core Documentation Files

| File | Purpose | Audience |
|------|---------|----------|
| **01_ARCHITECTURE_AND_DESIGN.md** | Complete system design with diagrams | Architects, Tech Leads |
| **02_STEP_BY_STEP_IMPLEMENTATION_GUIDE.md** | Detailed implementation walkthrough | Entry-Level Engineers |
| **03_API_DOCUMENTATION.md** | Complete API reference | All Developers, API Consumers |
| **README.md** (this file) | Project overview and quick start | Everyone |

---

## 🎯 Project Overview

### What is This Project?

A **Mini Ad Server and Real-Time Bidding (RTB) Engine** simulates how enterprise advertising platforms (Google Ads, AppNexus, The Trade Desk) serve advertisements and process bid requests at scale.

### Key Features

✅ **High-Performance Ad Serving**
- Sub-100ms p95 latency at 10k QPS
- Horizontal scalability via stateless services
- Campaign-based bidding with budget constraints

✅ **Real-Time Bidding Engine**
- Instant campaign selection based on highest CPM bid
- Targeting rule matching (country, device type, etc.)
- Budget tracking and enforcement

✅ **Event-Driven Analytics**
- Kafka-based event streaming for impressions and clicks
- Real-time aggregation of metrics
- Analytics API for historical reporting

✅ **Production-Ready Observability**
- Prometheus metrics collection
- Grafana dashboards
- Structured logging with correlation IDs
- Distributed tracing support

✅ **Comprehensive Testing**
- Unit tests for core logic
- Integration tests for API flows
- Load testing framework (k6)
- Seed data for easy testing

---

## 🏗️ System Architecture

### High-Level Overview

```
┌─────────────────────────────────────────────────┐
│              CLIENTS                             │
│     (Websites, Apps, Ad Networks)                │
└──────────────────┬──────────────────────────────┘
                   │ GET /serve
                   ▼
        ┌──────────────────────┐
        │   AD SERVER          │
        │  (Spring Boot)       │
        │  - Orchestrate       │
        │  - Serve ads         │
        │  - Log events        │
        └────────┬─────────────┘
                 │
         ┌───────┴────────┐
         │                │
         ▼                ▼
    ┌─────────────┐  ┌──────────────┐
    │ BID ENGINE  │  │ KAFKA EVENTS │
    │             │  │              │
    │ • Select    │  │ • Impressions│
    │   winning   │  │ • Clicks     │
    │ • Track     │  │              │
    │   budget    │  └──────┬───────┘
    │ • Cache     │         │
    │   in Redis  │         ▼
    └─────────────┘  ┌─────────────────┐
         △           │ EVENT CONSUMER  │
         │           │ • Aggregate     │
         │           │ • Store metrics │
         │           └────────┬────────┘
         │                    │
         │                    ▼
         │           ┌─────────────────┐
         └───────────┤ POSTGRESQL      │
                     │ • Campaigns     │
                     │ • Metrics       │
                     │ • Events log    │
                     └─────────────────┘

┌─────────────────────────────────────────────────┐
│          ANALYTICS API                          │
│     (Query metrics and reporting)                │
└─────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────┐
│    MONITORING (Prometheus + Grafana)            │
│     (Metrics, dashboards, alerts)                │
└─────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Technology |
|-----------|-----------------|-----------|
| **Ad Server** | Entry point, orchestration, event publishing | ASP.NET Core 8 |
| **Bid Engine** | Campaign selection, budget tracking | ASP.NET Core 8, Redis |
| **Event Consumer** | Kafka consumption, metric aggregation | Background Service |
| **Analytics Service** | Reporting API, performance queries | ASP.NET Core 8 |
| **PostgreSQL** | Persistent data storage | Database |
| **Redis** | High-speed caching | Cache |
| **Kafka** | Event streaming backbone | Message Broker |

---

## 📊 Data Flow Examples

### Serving an Ad (Request Path)

```
Timeline: 0-100ms (p95 target)

T+0ms:   GET /serve?userId=user-123&placementId=banner
T+5ms:   Ad Server validates request
T+10ms:  Ad Server → Bid Engine (POST /bid)
T+30ms:  Bid Engine processes:
         • Load campaigns from Redis (2ms)
         • Filter by targeting rules (5ms)
         • Select highest CPM (3ms)
T+32ms:  Bid Engine → Response
T+35ms:  Ad Server builds response
T+40ms:  Publish IMPRESSION event to Kafka (async)
T+42ms:  HTTP 200 + Ad JSON to client
T+100ms: Kafka confirms delivery

Total: ~42ms average, <100ms p95
```

### Processing Events for Analytics

```
Timeline: Continuous background process

1. Kafka event received:
   {
     "eventId": "uuid",
     "timestamp": "2025-11-29T14:30:45Z",
     "eventType": "impression",
     "campaignId": "campaign-123",
     "userId": "user-456"
   }

2. Event Consumer (every 100ms):
   • Poll Kafka for new events (batch: 500)
   • Update in-memory counters
   • counters["campaign-123_2025-11-29_impressions"]++

3. Every 10 seconds or 10k events:
   • Build INSERT/UPDATE statement
   • Execute batch to PostgreSQL
   • Commit Kafka offsets

4. Result in Database:
   daily_metrics table updated:
   ┌─────────────┬──────┬────────────┬────────┐
   │ campaign_id │ date │ impressions│ clicks │
   ├─────────────┼──────┼────────────┼────────┤
   │campaign-123 │2025-11-29│ 85000   │  2125  │
   └─────────────┴──────┴────────────┴────────┘
```

---

## 🚀 Technology Stack Explained

### Why C# and .NET 8?

- **Performance:** Native compilation, excellent async/await support
- **Ecosystem:** Rich library ecosystem (Entity Framework, Serilog, etc.)
- **Productivity:** Strong typing, LINQ for data queries
- **Scalability:** Built-in dependency injection and middleware
- **Cross-platform:** Runs on Linux, macOS, Windows

### Database Choice: PostgreSQL

- **ACID Compliance:** Guarantees data consistency for financial transactions
- **JSON Support:** Flexible schema for user attributes and ad metadata
- **Indexing:** Multiple index types for query optimization
- **Monitoring:** Excellent observability tools

### Cache Choice: Redis

- **Speed:** Sub-millisecond access (vs. 10-100ms for database)
- **Use Cases:**
  - Campaign data caching (5-minute TTL)
  - Analytics result caching (1-hour TTL)
  - Rate limiting counters
  - Session data

### Event Streaming: Apache Kafka

- **Throughput:** Handles 1M+ events/second per cluster
- **Durability:** Persists messages to disk for replay
- **Parallelism:** Partitions allow scaling and ordered processing
- **Decoupling:** Services don't need to know about each other

### Monitoring Stack: Prometheus + Grafana

- **Prometheus:** Time-series metrics database
- **Grafana:** Visualization and alerting
- **Benefits:** Industry-standard, well-documented, free/open-source

---

## 💾 Database Schema Overview

### Campaigns Table
Represents advertiser marketing campaigns with budget and bidding info.

```sql
campaigns
├── id (UUID)
├── name (text)
├── advertiser_id (UUID)
├── status (active|paused|ended)
├── cpm_bid (decimal) -- Cost per 1000 impressions
├── daily_budget (decimal)
├── spent_today (decimal)
├── lifetime_budget (decimal)
└── lifetime_spent (decimal)

Indexes:
├── PRIMARY KEY (id)
├── advertiser_id
└── status
```

### Campaign Targeting Rules
Restrictions on which users/placements can see each campaign.

```sql
campaign_targeting_rules
├── id (UUID)
├── campaign_id (FK) → campaigns
├── rule_type (country|device_type|interest|age_range)
└── rule_value (US|mobile|tech|18-35)
```

### Ads Table
Creative content for each campaign.

```sql
ads
├── id (UUID)
├── campaign_id (FK) → campaigns
├── title (text)
├── image_url (text)
└── redirect_url (text)
```

### Daily Metrics Table
Aggregated performance metrics per campaign per day.

```sql
daily_metrics
├── id (BIGSERIAL, primary key)
├── campaign_id (FK) → campaigns
├── date (DATE)
├── impressions (BIGINT)
├── clicks (BIGINT)
├── spend (DECIMAL)
└── UNIQUE (campaign_id, date) -- One row per campaign per day
```

### Events Log Table
Full audit trail (optional but recommended).

```sql
events_log
├── id (BIGSERIAL)
├── event_id (UUID, UNIQUE) -- Deduplication
├── event_type (impression|click)
├── campaign_id (FK)
├── ad_id (FK)
├── user_id (text)
├── placement_id (text)
├── bid_price (decimal)
└── timestamp (TIMESTAMP)
```

---

## 📈 Key Metrics & Definitions

### Impressions and Clicks
- **Impression:** When an ad is served to and viewed by a user
- **Click:** When user clicks on the ad or follows the tracking link
- **Tracked:** Via server-side POST /click endpoint or beacon pixel

### Click-Through Rate (CTR)
```
CTR = (Clicks / Impressions) × 100
Example: 2,125 clicks / 85,000 impressions = 2.5% CTR
```

### Cost Per Mille (CPM)
```
CPM = Cost per 1,000 impressions
Example: $2.50 CPM means $2.50 for every 1,000 users who see the ad
Cost per impression = CPM / 1000 = $0.0025
```

### Cost Per Click (CPC)
```
CPC = Total Spend / Total Clicks
Example: $212.50 / 2,125 clicks = $0.10 per click
```

### Daily Spend Calculation
```
Daily Spend = (Impressions Served / 1000) × CPM Bid
Example: 85,000 impressions × ($2.50 / 1000) = $212.50
```

---

## 🔄 Event Schema

### Impression Event
Fired when an ad is served to a user.

```json
{
  "eventId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "timestamp": "2025-11-29T14:30:45.123Z",
  "eventType": "impression",
  "userId": "user-abc123",
  "campaignId": "6ba7b810-9dad-11d1-80b4-00c04fd430c8",
  "adId": "550e8400-e29b-41d4-a716-446655440000",
  "placementId": "homepage_banner_728x90",
  "bidPrice": 2.50,
  "countryCode": "US",
  "deviceType": "desktop"
}
```

### Click Event
Fired when user clicks on ad.

```json
{
  "eventId": "a7b8c9d0-e1f2-3a4b-5c6d-7e8f9a0b1c2d",
  "timestamp": "2025-11-29T14:30:47.456Z",
  "eventType": "click",
  "userId": "user-abc123",
  "campaignId": "6ba7b810-9dad-11d1-80b4-00c04fd430c8",
  "adId": "550e8400-e29b-41d4-a716-446655440000",
  "placementId": "homepage_banner_728x90"
}
```

---

## 🧪 Non-Functional Requirements

### Performance
- **Ad Server:** p95 latency < 100ms at 10k QPS
- **Bid Engine:** p95 latency < 50ms
- **Event Publishing:** < 10ms (non-blocking)
- **Analytics Query:** < 500ms

### Availability
- **Target Uptime:** 99.9% (43 minutes downtime/month)
- **Health Checks:** Kubernetes readiness probes every 10 seconds
- **Graceful Degradation:** Return cached data if database unavailable

### Scalability
- **Stateless Services:** Can add/remove instances without impact
- **Kafka Partitions:** 10+ for throughput scaling
- **Database Connections:** 50-100 concurrent
- **Redis Cluster:** High availability with replication

### Fault Tolerance
- **Retries:** Exponential backoff (100ms, 200ms, 400ms, 800ms)
- **Circuit Breaker:** Fail fast after 5 consecutive errors
- **Idempotency:** All operations use event IDs to prevent duplicates
- **Message Durability:** Kafka replication factor = 3

### Observability
- **Logging:** Structured JSON with correlation IDs
- **Metrics:** Prometheus format (counter, gauge, histogram)
- **Tracing:** W3C Trace Context headers
- **Alerting:** Grafana alerts for anomalies

---

## 📚 Documentation Structure

### For Implementation
Start here if you're building the system:
1. Read **01_ARCHITECTURE_AND_DESIGN.md** for overview
2. Follow **02_STEP_BY_STEP_IMPLEMENTATION_GUIDE.md** phase by phase
3. Refer to **03_API_DOCUMENTATION.md** for API details

### For API Consumers
If using these APIs:
1. Read **03_API_DOCUMENTATION.md** for endpoint details
2. See code examples for your language
3. Review error handling and rate limiting sections

### For Operations
If running in production:
1. Reference **monitoring/** folder for Prometheus/Grafana setup
2. Check health endpoints regularly
3. Monitor key metrics: latency, error rate, Kafka lag
4. Use runbooks for common issues

---

## 🛠️ Getting Started (Quick Reference)

### Prerequisites
```bash
# Required installations
dotnet --version          # Should be 8.0+
docker --version          # Docker Desktop
docker-compose --version  # Included with Desktop

# Optional but recommended
git --version             # For version control
```

### Project Setup
```bash
cd ~/Desktop/Practice/ad_simulator

# Copy docker-compose.yml to root directory
# Create infrastructure/database/ directory
# Place SQL scripts for initialization

# Start all services
docker-compose up -d

# Verify services running
docker-compose ps
```

### Verify Services
```bash
# Ad Server (port 8080)
curl http://localhost:8080/health

# Bid Engine (port 8081)
curl http://localhost:8081/health

# Analytics (port 8082)
curl http://localhost:8082/health

# Prometheus (port 9090)
open http://localhost:9090

# Grafana (port 3000, username: admin, password: admin)
open http://localhost:3000
```

---

## 📊 Example Workflows

### Workflow 1: Serving an Ad

```bash
# 1. Client requests an ad
curl "http://localhost:8080/serve?userId=user-123&placementId=banner&countryCode=US&deviceType=desktop"

# Response:
{
  "adId": "550e8400-e29b-41d4-a716-446655440000",
  "campaignId": "6ba7b810-9dad-11d1-80b4-00c04fd430c8",
  "title": "TechGear Pro",
  "imageUrl": "https://cdn.example.com/ads/image.jpg",
  "redirectUrl": "https://techgear.example.com/pro",
  "clickTrackingUrl": "https://localhost:8080/click?adId=...",
  "bidPrice": 2.50
}

# 2. User clicks ad, browser calls tracking endpoint
curl -X POST "http://localhost:8080/click" \
  -H "Content-Type: application/json" \
  -d '{
    "adId": "550e8400-e29b-41d4-a716-446655440000",
    "campaignId": "6ba7b810-9dad-11d1-80b4-00c04fd430c8",
    "userId": "user-123"
  }'

# 3. Query analytics
curl "http://localhost:8082/analytics/campaign/6ba7b810-9dad-11d1-80b4-00c04fd430c8?startDate=2025-11-29&endDate=2025-11-29"
```

### Workflow 2: Adding a New Campaign

```bash
# 1. Insert campaign into PostgreSQL
psql -h localhost -U postgres -d ads_db << EOF
INSERT INTO campaigns (name, advertiser_id, status, cpm_bid, daily_budget)
VALUES (
  'NewCampaign',
  gen_random_uuid(),
  'active',
  3.00,
  1000.00
);
EOF

# 2. Add targeting rules
psql -h localhost -U postgres -d ads_db << EOF
INSERT INTO campaign_targeting_rules (campaign_id, rule_type, rule_value)
VALUES (
  (SELECT id FROM campaigns WHERE name = 'NewCampaign'),
  'country',
  'US'
);
EOF

# 3. Campaign is immediately available for bidding (via cache refresh)
```

---

## 📈 Performance Tuning Tips

### Database Optimization
1. **Use EXPLAIN ANALYZE** to check query plans
2. **Create indexes** on frequently filtered columns
3. **Monitor connection pool:** Look for exhausted connections
4. **Batch writes:** Update multiple rows at once

### Redis Optimization
1. **Monitor memory usage:** `redis-cli INFO memory`
2. **Check hit rate:** `(hits / (hits + misses)) × 100`
3. **Adjust TTL:** Longer for stable data, shorter for frequently changing

### Kafka Optimization
1. **Monitor consumer lag:** `kafka-consumer-groups --describe`
2. **Increase partitions** for higher throughput
3. **Tune batch sizes:** Balance latency vs. throughput
4. **Monitor broker health:** Check disk and CPU usage

---

## 🐛 Troubleshooting Common Issues

### High Latency in Ad Server

**Symptoms:** p95 latency > 200ms

**Diagnosis:**
```bash
# 1. Check Bid Engine latency
curl -X POST http://localhost:8081/api/bid \
  -H "Content-Type: application/json" \
  -d '{"userId":"test","placementId":"test"}'

# 2. Check database connections
psql -h localhost -U postgres -d ads_db \
  -c "SELECT count(*) FROM pg_stat_activity;"

# 3. Check Redis connectivity
redis-cli ping
```

**Solutions:**
- Increase Redis cache TTL
- Add database connection pool
- Add Bid Engine replicas
- Optimize targeting rule matching

### Kafka Messages Not Processed

**Symptoms:** Events not appearing in daily_metrics table

**Diagnosis:**
```bash
# 1. Check Kafka broker health
docker-compose logs kafka | tail -50

# 2. Check consumer lag
kafka-consumer-groups --bootstrap-server kafka:9092 \
  --group ads-event-consumer --describe

# 3. Check Event Consumer logs
docker-compose logs event-consumer | tail -50
```

**Solutions:**
- Verify database connection string
- Check Event Consumer service is running
- Restart consumer: `docker-compose restart event-consumer`
- Check disk space on PostgreSQL

### Budget Not Being Deducted

**Symptoms:** Campaigns exceed daily budget

**Diagnosis:**
```bash
# Check campaign spend
psql -h localhost -U postgres -d ads_db << EOF
SELECT name, spent_today, daily_budget, spent_today > daily_budget as over_budget
FROM campaigns;
EOF
```

**Solutions:**
- Verify BudgetService.DeductBudgetAsync is being called
- Check database transaction isolation level
- Ensure campaign updates are being committed
- Monitor for concurrent request issues

---

## 📞 Support & Resources

### Debugging with Logs

**Enable verbose logging:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information"
    }
  }
}
```

**View service logs:**
```bash
docker-compose logs -f ad-server
docker-compose logs -f bid-engine
docker-compose logs -f event-consumer
```

### Monitoring Dashboards

1. **System Health:** http://localhost:3000/dashboard/system
2. **Ad Serving Metrics:** http://localhost:3000/dashboard/ad-serving
3. **Bid Engine Performance:** http://localhost:3000/dashboard/bid-engine
4. **Analytics Pipeline:** http://localhost:3000/dashboard/analytics

### Query Examples

```bash
# Top performing campaigns
psql -h localhost -U postgres -d ads_db << EOF
SELECT c.name, SUM(m.impressions), SUM(m.clicks),
       (SUM(m.clicks)::float / SUM(m.impressions) * 100) as ctr
FROM campaigns c
JOIN daily_metrics m ON c.id = m.campaign_id
GROUP BY c.id, c.name
ORDER BY SUM(m.spend) DESC
LIMIT 10;
EOF

# Today's spending by campaign
psql -h localhost -U postgres -d ads_db << EOF
SELECT name, spent_today, daily_budget,
       (spent_today::float / daily_budget * 100) as budget_pct
FROM campaigns
WHERE status = 'active'
ORDER BY spent_today DESC;
EOF
```

---

## 📝 Contributing & Maintenance

### Code Standards
- Follow C# style guide (Microsoft's)
- Use meaningful variable names
- Add XML comments for public methods
- Keep methods < 50 lines

### Testing Requirements
- Unit test coverage > 80%
- All public APIs have integration tests
- Load tests validate SLOs

### Deployment Checklist
- [ ] All tests passing
- [ ] No active alerts
- [ ] Logs reviewed for errors
- [ ] Database migrations tested
- [ ] Rollback plan documented

---

## 📄 License & Attribution

This project is educational material demonstrating real-world advertising platform architecture.

Key References:
- [AdX Platform (Google)](https://admob.google.com/home/)
- [AppNexus RTB](https://www.appnexus.com/)
- [The Trade Desk](https://www.thetradedesk.com/)
- [OpenRTB Specification](https://www.iab.com/wp-content/uploads/2016/03/OpenRTB-API-Specification-Version-2-5-1-FINAL.pdf)

---

## 🎓 Learning Outcomes

After implementing this project, you'll understand:

✅ **System Design**
- Microservices architecture
- Scalability and performance patterns
- Fault tolerance and resilience

✅ **Real-Time Processing**
- Event-driven architectures
- Message streaming (Kafka)
- Real-time aggregation

✅ **Database Design**
- SQL schema optimization
- Indexing strategies
- Query performance tuning

✅ **Distributed Systems**
- Consistency and idempotency
- Eventual consistency patterns
- Handling concurrent requests

✅ **Production Operations**
- Monitoring and observability
- Alerting and incident response
- Performance debugging

---

## 📞 Getting Help

### Questions on Architecture
→ See **01_ARCHITECTURE_AND_DESIGN.md**

### Implementation Issues
→ See **02_STEP_BY_STEP_IMPLEMENTATION_GUIDE.md**

### API Details
→ See **03_API_DOCUMENTATION.md**

### Deployment/Operations
→ See **04_DEPLOYMENT_GUIDE.md** (coming soon)

---

**Created:** November 29, 2025  
**Format:** Markdown + ASCII Diagrams  
**Status:** Documentation Complete - Ready for Development  

