# Mini Ad Server and Real-Time Bidding Engine
## Architecture and Design Document

**Version:** 1.0  
**Last Updated:** November 29, 2025  
**Project Type:** High-Performance Advertising Platform Backend

---

## Table of Contents
1. [Executive Summary](#executive-summary)
2. [System Architecture](#system-architecture)
3. [Component Details](#component-details)
4. [Data Flow Diagrams](#data-flow-diagrams)
5. [Technology Stack](#technology-stack)
6. [API Specifications](#api-specifications)
7. [Database Schema](#database-schema)
8. [Non-Functional Requirements](#non-functional-requirements)
9. [Deployment Architecture](#deployment-architecture)

---

## Executive Summary

This project implements a **Mini Ad Server and Real-Time Bidding (RTB) Engine** that simulates how enterprise advertising platforms (Google Ads, AppNexus, The Trade Desk) serve advertisements and process bid requests at scale.

### Key Objectives
- **High Availability:** Stateless, horizontally scalable services
- **Low Latency:** Sub-100ms p95 response times at 10k QPS
- **Event-Driven:** Asynchronous event streaming for analytics
- **Observability:** Comprehensive monitoring with Prometheus & Grafana
- **Testability:** Full unit, integration, and load testing coverage

---

## System Architecture

### High-Level Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                          CLIENT TIER                                │
│  (Websites, Mobile Apps, Ad Networks)                               │
└────────────────┬─────────────────────────────────────────────────────┘
                 │ HTTP Request: GET /serve?userId=X&placementId=Y
                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     API GATEWAY (Optional)                           │
│  - Request routing and load balancing                               │
│  - Rate limiting and authentication                                 │
└────────────────┬─────────────────────────────────────────────────────┘
                 │
                 ▼
        ┌────────────────────┐
        │   AD SERVER        │
        │ (Spring Boot)      │
        │                    │
        │ Responsibilities:  │
        │ • Handle /serve    │
        │ • Call Bid Engine  │
        │ • Log events       │
        │ • Return ad data   │
        │ • Prometheus       │
        │   metrics          │
        └────────┬───────────┘
                 │
         ┌───────┴────────┐
         │                │
         ▼                ▼
    ┌─────────────┐  ┌──────────────────┐
    │ BID ENGINE  │  │ KAFKA PRODUCER   │
    │             │  │ (Events Output)  │
    │ • Campaign  │  │                  │
    │   selection │  │ Topics:          │
    │ • Budget    │  │ • impressions    │
    │   tracking  │  │ • clicks         │
    │ • Caching   │  └──────────┬───────┘
    │   (Redis)   │             │
    └─────────────┘             │
         △                       │
         │                       │
         └───────────────┬───────┘
                         │
         ┌───────────────▼──────────────────┐
         │     KAFKA BROKER CLUSTER         │
         │                                  │
         │ • Partition 1 (impressions)      │
         │ • Partition 2 (clicks)           │
         │ • High throughput, durable       │
         └────────────┬─────────────────────┘
                      │
                      ▼
         ┌────────────────────────────┐
         │   EVENT CONSUMER           │
         │                            │
         │ • Kafka consumer group     │
         │ • Real-time aggregation    │
         │ • State management         │
         │ • Batching writes          │
         └────────────┬───────────────┘
                      │
         ┌────────────┴────────────┐
         │                         │
         ▼                         ▼
    ┌──────────────┐        ┌──────────────┐
    │ PostgreSQL   │        │ Redis Cache  │
    │              │        │              │
    │ • Campaigns  │        │ • Campaign   │
    │ • Metrics    │        │   data       │
    │   (daily agg)│        │ • Analytics  │
    │ • Events log │        │   cache      │
    └──────────────┘        └──────────────┘
         △                         △
         │                         │
         └─────────────┬───────────┘
                       │
                       ▼
        ┌───────────────────────────────┐
        │   ANALYTICS SERVICE           │
        │   (Spring Boot)               │
        │                               │
        │ Endpoint: GET /analytics      │
        │ • Queries aggregated metrics  │
        │ • Returns impressions, clicks,│
        │   CTR, spend per campaign     │
        └───────────────┬───────────────┘
                        │
                        ▼
        ┌───────────────────────────────┐
        │   CLIENT DASHBOARD            │
        │   (Web/Mobile)                │
        │                               │
        │ Displays:                     │
        │ • Campaign performance        │
        │ • Real-time metrics           │
        │ • Spend trends                │
        └───────────────────────────────┘


┌────────────────────────────────────────────────────────────────────┐
│                    MONITORING & OBSERVABILITY                       │
│                                                                    │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐             │
│  │ Prometheus   │  │ Grafana      │  │ Structured   │             │
│  │              │  │              │  │ Logging      │             │
│  │ • Metrics    │  │ • Dashboards │  │ (JSON logs)  │             │
│  │   scraping   │  │ • Alerts     │  │              │             │
│  │ • Time-series│  │              │  │              │             │
│  │   database   │  │              │  │              │             │
│  └──────────────┘  └──────────────┘  └──────────────┘             │
└────────────────────────────────────────────────────────────────────┘
```

---

## Component Details

### 1. Ad Server Service
**Purpose:** Main entry point for serving ads to clients. Orchestrates the bidding process and event logging.

**Key Responsibilities:**
- Accept `/serve` requests with user context and placement information
- Invoke the Bid Engine to determine the winning ad
- Log impression events asynchronously to Kafka
- Handle click events (potentially via client-side tracking redirect)
- Expose Prometheus metrics (request latency, throughput, errors)
- Return serialized ad object with metadata

**Technology:** Spring Boot, Java 17+

**Dependencies:**
- Kafka Producer (for event streaming)
- Bid Engine Service (HTTP or gRPC)
- Prometheus Client Library

---

### 2. Bid Engine Service
**Purpose:** Core decision-making engine that selects winning ads based on bid price and targeting rules.

**Key Responsibilities:**
- Load and cache campaign data (in-memory + Redis)
- Accept bid requests from Ad Server
- Apply targeting rules (user attributes, placement type, etc.)
- Select winning campaign based on highest CPM bid
- Deduct spend from campaign budget
- Enforce budget constraints
- Respond in <100ms under load

**Technology:** Spring Boot, Java 17+, Redis

**Internal Models:**
- `Campaign`: id, cpiBid, budget, dailyBudget, targeting rules, status
- `BidRequest`: userId, placementId, userAttributes, contextData
- `BidResponse`: campaignId, adId, bidPrice, adContent

**Caching Strategy:**
- Redis stores campaign data with TTL of 5 minutes
- In-memory cache with local refresh logic
- Fallback to database on cache miss

---

### 3. Event Pipeline (Kafka)
**Purpose:** Distributed event streaming backbone for decoupling ad serving from analytics processing.

**Topics:**
- `impressions`: Fired when an ad is served to user
- `clicks`: Fired when user clicks on ad (user-initiated or tracked)

**Event Schema:**
```json
{
  "eventId": "uuid",
  "timestamp": "2025-11-29T14:30:45.123Z",
  "eventType": "impression|click",
  "userId": "user-123",
  "campaignId": "campaign-456",
  "adId": "ad-789",
  "placementId": "placement-001",
  "bidPrice": 2.50,
  "country": "US",
  "deviceType": "mobile|desktop"
}
```

**Key Characteristics:**
- High throughput (10k+ events/sec)
- At-least-once delivery semantics
- Partitioned by `campaignId` for ordered processing
- Configurable retention (7-30 days)

---

### 4. Event Consumer Service
**Purpose:** Reads events from Kafka and aggregates metrics for analytics.

**Responsibilities:**
- Consume events from Kafka topics
- Aggregate metrics per campaign per day (sliding window)
- Write aggregates to PostgreSQL
- Maintain processing state (checkpoints)
- Handle failure recovery and idempotency
- Monitor Kafka lag

**Processing Logic:**
1. Read event from topic
2. Extract: campaignId, date, eventType
3. Update in-memory aggregates (HashMap by key: `{campaignId}_{date}`)
4. Every 10 seconds or 10k events: flush aggregates to PostgreSQL
5. Commit Kafka offset after successful persistence

**Output Metrics:**
```sql
INSERT INTO daily_metrics (campaign_id, date, impressions, clicks)
VALUES (?, ?, ?, ?)
ON CONFLICT (campaign_id, date) DO UPDATE
SET impressions = impressions + ?, clicks = clicks + ?
```

---

### 5. Analytics Service
**Purpose:** Provides real-time and historical analytics on ad performance.

**Endpoints:**
- `GET /analytics/campaign/{campaignId}?startDate=&endDate=`
- Returns: impressions, clicks, CTR, total spend, cost per click

**Query Pattern:**
1. Check Redis cache first (key: `analytics:{campaignId}:{date}`)
2. If miss, query PostgreSQL `daily_metrics` table
3. Calculate derived metrics (CTR, CPC)
4. Cache result in Redis (TTL: 1 hour)
5. Return JSON response

**Response Example:**
```json
{
  "campaignId": "campaign-456",
  "metrics": [
    {
      "date": "2025-11-29",
      "impressions": 50000,
      "clicks": 1250,
      "ctr": 2.5,
      "spend": 125.00,
      "cpc": 0.10
    }
  ]
}
```

---

## Data Flow Diagrams

### Ad Serving Flow (Request Path)

```
Timeline: 0-100ms (target p95)

T+0ms:   Client sends: GET /serve?userId=user-123&placementId=placement-001
         │
T+5ms:   ├─→ Ad Server receives request
         │   └─→ Validates request parameters
         │
T+10ms:  ├─→ Ad Server sends BID REQUEST to Bid Engine
         │   Request: { userId, placementId, attributes }
         │
T+30ms:  ├─→ Bid Engine processes (22ms):
         │   1. Check Redis cache for campaigns (2ms)
         │   2. Load from DB if cache miss (8ms)
         │   3. Apply targeting filters (5ms)
         │   4. Select highest CPM bid (2ms)
         │   5. Deduct budget (5ms)
         │
T+32ms:  ├─→ Bid Engine returns BID RESPONSE
         │   Response: { campaignId, adId, bidPrice, content }
         │
T+35ms:  ├─→ Ad Server constructs ad response
         │   └─→ Adds tracking parameters
         │
T+40ms:  ├─→ Ad Server publishes IMPRESSION EVENT
         │   └─→ Async Kafka producer (non-blocking)
         │       Topic: "impressions"
         │       Message: { eventId, timestamp, campaignId, ... }
         │
T+42ms:  ├─→ Ad Server returns HTTP 200 + ad JSON to client
         │
T+50ms:  └─→ Kafka Producer confirms message delivery
         
Network + Processing: ~42ms average, <100ms p95
```

### Click Tracking Flow

```
Timeline: Client clicks ad → Server records click

T+0s:    User clicks ad image
         └─→ Browser follows redirect URL with tracking parameters
             Example: https://ads.example.com/click?adId=ad-789&campaignId=campaign-456&userId=user-123

T+50ms:  ├─→ Ad Server receives click event at POST /click
         │   Extracts: adId, campaignId, userId
         │
T+55ms:  ├─→ Ad Server publishes CLICK EVENT to Kafka
         │   Topic: "clicks"
         │   Message: { eventId, timestamp, campaignId, adId, userId }
         │
T+60ms:  ├─→ Kafka confirms message delivery
         │
T+65ms:  └─→ Ad Server redirects user to destination URL
             HTTP 302 with Location header

Total latency: ~65ms
```

### Analytics Aggregation Flow

```
Timeline: Real-time aggregation in Event Consumer

CONTINUOUS PROCESS:

1. Consumer subscribes to "impressions" and "clicks" topics
   │
2. Every 100ms:
   ├─→ Poll Kafka for new events (batch size: 500)
   │
3. For each event received:
   ├─→ Extract: campaignId, date, eventType
   ├─→ Update in-memory counter: counters[{campaignId}_{date}_{eventType}]++
   │
4. Every 10 seconds OR 10k events batched:
   ├─→ Build INSERT/UPDATE SQL statement
   │   ```sql
   │   INSERT INTO daily_metrics (campaign_id, date, impressions, clicks)
   │   VALUES (?, ?, ?, ?)
   │   ON CONFLICT UPDATE SET impressions = impressions + ?, ...
   │   ```
   ├─→ Execute batch update to PostgreSQL
   ├─→ Clear in-memory counters
   ├─→ Commit Kafka offsets
   │
5. On failure:
   ├─→ Rollback in-memory state
   ├─→ Retry with exponential backoff
   ├─→ Alert monitoring system

RESULT: Sub-second latency from event to database
```

---

## Technology Stack

| Component | Technology | Rationale |
|-----------|-----------|-----------|
| **Language** | C# / .NET 8 | Modern, high-performance, excellent async/await patterns |
| **Framework** | ASP.NET Core 8 | Industry-standard for microservices, built-in DI, middleware |
| **Database** | PostgreSQL 15 | ACID compliance, excellent for analytics, JSON support |
| **Cache** | Redis 7 | Sub-millisecond access, built-in data structures |
| **Event Stream** | Apache Kafka 3.5+ | High-throughput, partition-based ordering, consumer groups |
| **Serialization** | Protobuf / JSON | Protobuf for Kafka (bandwidth), JSON for APIs |
| **Monitoring** | Prometheus + Grafana | Industry-standard observability stack |
| **Logging** | Serilog | Structured, async logging for C# |
| **Testing** | xUnit, Moq | Modern .NET testing frameworks |
| **Containerization** | Docker | Consistent environments, easy deployment |
| **Orchestration** | Docker Compose | Local development and testing |

---

## API Specifications

### 1. Ad Server APIs

#### GET /serve
**Purpose:** Serve an ad to a user

**Request:**
```http
GET /serve?userId=user-123&placementId=placement-001&countryCode=US&deviceType=mobile HTTP/1.1
Host: ads.example.com
Accept: application/json
```

**Response (200 OK):**
```json
{
  "adId": "ad-789",
  "campaignId": "campaign-456",
  "title": "Amazing Product",
  "imageUrl": "https://cdn.example.com/ads/campaign-456/image.jpg",
  "redirectUrl": "https://example.com/offer?utm_source=ads&utm_campaign=campaign-456",
  "clickTrackingUrl": "https://ads.example.com/click?adId=ad-789&campaignId=campaign-456",
  "bidPrice": 2.50,
  "impressionId": "imp-uuid-123"
}
```

**Error Response (400):**
```json
{
  "error": "INVALID_REQUEST",
  "message": "userId and placementId are required",
  "requestId": "req-uuid-123"
}
```

**SLO:** p95 latency < 100ms, p99 latency < 200ms

---

#### POST /click
**Purpose:** Record a click event

**Request:**
```http
POST /click HTTP/1.1
Host: ads.example.com
Content-Type: application/json

{
  "adId": "ad-789",
  "campaignId": "campaign-456",
  "userId": "user-123",
  "timestamp": "2025-11-29T14:30:45.123Z"
}
```

**Response (204 No Content):**
```http
HTTP/1.1 204 No Content
```

---

### 2. Bid Engine APIs

#### POST /bid
**Purpose:** Evaluate bid for given context

**Request:**
```http
POST /bid HTTP/1.1
Host: bid-engine.example.com
Content-Type: application/json

{
  "userId": "user-123",
  "placementId": "placement-001",
  "countryCode": "US",
  "deviceType": "mobile",
  "userAttributes": {
    "age": 25,
    "interests": ["tech", "gaming"],
    "purchaseHistory": ["electronics"]
  }
}
```

**Response (200 OK):**
```json
{
  "campaignId": "campaign-456",
  "adId": "ad-789",
  "bidPrice": 2.50,
  "adContent": {
    "title": "Amazing Product",
    "imageUrl": "https://cdn.example.com/ads/campaign-456/image.jpg",
    "redirectUrl": "https://example.com/offer"
  },
  "confidence": 0.95
}
```

**SLO:** p95 latency < 50ms

---

### 3. Analytics APIs

#### GET /analytics/campaign/{campaignId}
**Purpose:** Get performance metrics for a campaign

**Request:**
```http
GET /analytics/campaign/campaign-456?startDate=2025-11-01&endDate=2025-11-29 HTTP/1.1
Host: analytics.example.com
Accept: application/json
```

**Response (200 OK):**
```json
{
  "campaignId": "campaign-456",
  "dateRange": {
    "start": "2025-11-01",
    "end": "2025-11-29"
  },
  "summary": {
    "totalImpressions": 1250000,
    "totalClicks": 31250,
    "ctr": 2.5,
    "totalSpend": 3125.00,
    "cpc": 0.10,
    "cpm": 2.50
  },
  "dailyMetrics": [
    {
      "date": "2025-11-29",
      "impressions": 50000,
      "clicks": 1250,
      "ctr": 2.5,
      "spend": 125.00
    }
  ]
}
```

---

## Database Schema

### PostgreSQL Tables

#### campaigns
```sql
CREATE TABLE campaigns (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name VARCHAR(255) NOT NULL,
  advertiser_id UUID NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'active', -- active, paused, ended
  cpm_bid DECIMAL(10, 4) NOT NULL,
  daily_budget DECIMAL(12, 2) NOT NULL,
  lifetime_budget DECIMAL(12, 2),
  spent_today DECIMAL(12, 2) DEFAULT 0,
  lifetime_spent DECIMAL(12, 2) DEFAULT 0,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT cpm_positive CHECK (cpm_bid > 0)
);

CREATE INDEX idx_campaigns_advertiser ON campaigns(advertiser_id);
CREATE INDEX idx_campaigns_status ON campaigns(status);
```

#### campaign_targeting_rules
```sql
CREATE TABLE campaign_targeting_rules (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  campaign_id UUID NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
  rule_type VARCHAR(50) NOT NULL, -- country, device_type, interest, age_range
  rule_value VARCHAR(255) NOT NULL,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_targeting_campaign ON campaign_targeting_rules(campaign_id);
CREATE INDEX idx_targeting_type ON campaign_targeting_rules(rule_type);
```

#### ads
```sql
CREATE TABLE ads (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  campaign_id UUID NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
  title VARCHAR(255) NOT NULL,
  image_url VARCHAR(2048) NOT NULL,
  redirect_url VARCHAR(2048) NOT NULL,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_ads_campaign ON ads(campaign_id);
```

#### daily_metrics
```sql
CREATE TABLE daily_metrics (
  id BIGSERIAL PRIMARY KEY,
  campaign_id UUID NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
  date DATE NOT NULL,
  impressions BIGINT DEFAULT 0,
  clicks BIGINT DEFAULT 0,
  spend DECIMAL(12, 2) DEFAULT 0,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  UNIQUE (campaign_id, date)
);

CREATE INDEX idx_metrics_campaign_date ON daily_metrics(campaign_id, date);
CREATE INDEX idx_metrics_date ON daily_metrics(date);
```

#### events_log
```sql
CREATE TABLE events_log (
  id BIGSERIAL PRIMARY KEY,
  event_id UUID NOT NULL UNIQUE,
  event_type VARCHAR(20) NOT NULL, -- impression, click
  campaign_id UUID NOT NULL REFERENCES campaigns(id),
  ad_id UUID NOT NULL REFERENCES ads(id),
  user_id VARCHAR(255) NOT NULL,
  placement_id VARCHAR(255) NOT NULL,
  bid_price DECIMAL(10, 4),
  timestamp TIMESTAMP NOT NULL,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_events_campaign ON events_log(campaign_id);
CREATE INDEX idx_events_timestamp ON events_log(timestamp DESC);
CREATE INDEX idx_events_type ON events_log(event_type);
```

### Redis Keys

```
# Campaign cache (expires every 5 minutes)
campaign::{campaignId} → Campaign object (JSON)
campaigns::all → List of all active campaign IDs

# Analytics cache (expires every 1 hour)
analytics::{campaignId}::{date} → Daily metrics JSON

# Real-time stats (counter, can be cleared daily)
stats::impressions::{campaignId}::{date} → Counter
stats::clicks::{campaignId}::{date} → Counter
stats::spend::{campaignId}::{date} → Counter

# Kafka consumer offsets (for idempotency)
kafka::offset::{topicName}::{partitionId} → Offset number
```

---

## Non-Functional Requirements

### Performance
- **Latency (Ad Server):** p95 < 100ms, p99 < 200ms at 10k QPS
- **Latency (Bid Engine):** p95 < 50ms
- **Event Latency:** < 1 second from occurrence to Kafka
- **Analytics Query:** < 500ms for date range queries

### Availability
- **Uptime Target:** 99.9% (SLA)
- **Graceful Degradation:** Return cached results if database is down
- **Health Checks:** /health endpoints on all services

### Scalability
- **Horizontal Scaling:** All services are stateless and scalable
- **Kafka Partitions:** 10+ partitions for throughput scaling
- **Database:** Connection pooling with 50-100 concurrent connections
- **Redis:** Cluster mode for high availability

### Fault Tolerance
- **Retry Logic:** Exponential backoff (100ms, 200ms, 400ms, 800ms, 1600ms)
- **Circuit Breaker:** Stop requests after 5 consecutive failures
- **Idempotency:** All operations use event IDs to prevent duplicates
- **Persistence:** Kafka broker replication factor = 3

### Observability
- **Logging:** Structured JSON logs with correlation IDs
- **Metrics:** Prometheus format (counter, gauge, histogram, summary)
- **Tracing:** Distributed tracing headers (W3C Trace Context)
- **Alerting:** Grafana alerts for latency, error rate, Kafka lag

---

## Deployment Architecture

### Local Development (Docker Compose)

```yaml
services:
  postgres:
    image: postgres:15-alpine
    environment:
      POSTGRES_DB: ads_db
      POSTGRES_PASSWORD: postgres
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"

  kafka:
    image: confluentinc/cp-kafka:7.5.0
    environment:
      KAFKA_BROKER_ID: 1
      KAFKA_ZOOKEEPER_CONNECT: zookeeper:2181
    depends_on:
      - zookeeper
    ports:
      - "9092:9092"

  zookeeper:
    image: confluentinc/cp-zookeeper:7.5.0
    environment:
      ZOOKEEPER_CLIENT_PORT: 2181
    ports:
      - "2181:2181"

  prometheus:
    image: prom/prometheus:latest
    ports:
      - "9090:9090"
    volumes:
      - ./config/prometheus.yml:/etc/prometheus/prometheus.yml

  grafana:
    image: grafana/grafana:latest
    ports:
      - "3000:3000"
    environment:
      GF_SECURITY_ADMIN_PASSWORD: admin
    depends_on:
      - prometheus

  ad-server:
    build: ./ad-server
    ports:
      - "8080:8080"
    depends_on:
      - postgres
      - redis
      - kafka
    environment:
      DATABASE_URL: postgresql://postgres:postgres@postgres:5432/ads_db
      REDIS_URL: redis://redis:6379
      KAFKA_BROKERS: kafka:9092

  bid-engine:
    build: ./bid-engine
    ports:
      - "8081:8081"
    depends_on:
      - postgres
      - redis
    environment:
      DATABASE_URL: postgresql://postgres:postgres@postgres:5432/ads_db
      REDIS_URL: redis://redis:6379

  event-consumer:
    build: ./event-consumer
    depends_on:
      - postgres
      - kafka
    environment:
      DATABASE_URL: postgresql://postgres:postgres@postgres:5432/ads_db
      KAFKA_BROKERS: kafka:9092

  analytics-service:
    build: ./analytics-service
    ports:
      - "8082:8082"
    depends_on:
      - postgres
      - redis
    environment:
      DATABASE_URL: postgresql://postgres:postgres@postgres:5432/ads_db
      REDIS_URL: redis://redis:6379

volumes:
  postgres_data:
```

### Production Deployment (Kubernetes)

```yaml
# Typical k8s deployment with:
# - HPA (Horizontal Pod Autoscaler) based on CPU and requests/second
# - ConfigMaps for environment configuration
# - Secrets for database credentials
# - Service mesh (optional) for inter-service communication
# - PersistentVolumes for stateful services (PostgreSQL, Kafka)
# - NetworkPolicies for security isolation
```

---

## Next Steps

This architecture document provides the blueprint for implementation. See the accompanying **STEP_BY_STEP_IMPLEMENTATION_GUIDE.md** for detailed implementation instructions.

