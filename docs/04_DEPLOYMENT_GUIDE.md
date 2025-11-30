# Mini Ad Server and Bidding Engine
## Deployment & Operations Guide

**Version:** 1.0  
**Last Updated:** November 29, 2025  
**Audience:** DevOps Engineers, System Administrators, Operators

---

## Table of Contents
1. [Local Development Setup](#local-development-setup)
2. [Docker Containerization](#docker-containerization)
3. [Production Deployment](#production-deployment)
4. [Monitoring & Observability](#monitoring--observability)
5. [Troubleshooting](#troubleshooting)
6. [Performance Tuning](#performance-tuning)
7. [Disaster Recovery](#disaster-recovery)

---

## Local Development Setup

### Prerequisites

```bash
# Check installed versions
dotnet --version      # Should be 8.0+
docker --version      # Latest
docker-compose --version
git --version

# Install if missing (macOS)
brew install dotnet
brew install docker docker-compose
```

### Initial Setup

```bash
# 1. Clone/navigate to project
cd ~/Desktop/Practice/ad_simulator

# 2. Create Docker Compose file
# (See docker-compose.yml in previous section)

# 3. Start all services
docker-compose up -d

# 4. Verify services
docker-compose ps

# Output should show:
# Container Name        Status
# ads_postgres          healthy
# ads_redis             healthy
# ads_zookeeper         Up
# ads_kafka             healthy
# ads_prometheus        Up
# ads_grafana           Up
# ads_server            Up
# ads_bid_engine        Up
# ads_event_consumer    Up
# ads_analytics         Up

# 5. Check service logs
docker-compose logs -f ad-server
```

### Database Initialization

```bash
# 1. Wait for PostgreSQL to be ready
sleep 10

# 2. Connect to database
psql -h localhost -U postgres -d ads_db

# 3. Verify schema created
\dt  # List tables
\q   # Quit

# 4. Seed data (if needed)
psql -h localhost -U postgres -d ads_db < infrastructure/database/seed-data.sql
```

### Testing Local Setup

```bash
# 1. Health check all services
curl http://localhost:8080/health  # Ad Server
curl http://localhost:8081/health  # Bid Engine
curl http://localhost:8082/health  # Analytics

# 2. Create test campaign
curl -X POST http://localhost:8081/api/campaigns \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Test Campaign",
    "cpmBid": 2.50,
    "dailyBudget": 1000
  }'

# 3. Serve an ad
curl "http://localhost:8080/serve?userId=test-user&placementId=test-placement"

# 4. Check Prometheus metrics
open http://localhost:9090

# 5. Check Grafana dashboard
open http://localhost:3000
# Login: admin / admin
```

---

## Docker Containerization

### Building Services

Each service needs a Dockerfile. Here's the pattern:

```dockerfile
# Multi-stage build to minimize image size

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder

WORKDIR /build

# Copy project files
COPY src/YourService/YourService.csproj ./

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY src/YourService/ ./

# Build
RUN dotnet build -c Release

# Publish
RUN dotnet publish -c Release -o /app

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

# Copy published app from builder
COPY --from=builder /app .

# Expose port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=10s --timeout=5s --retries=5 \
  CMD curl -f http://localhost:8080/health || exit 1

# Run app
ENTRYPOINT ["dotnet", "YourService.dll"]
```

### Docker Compose Optimization

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:15-alpine  # Alpine = smaller images
    # ... rest of config

  redis:
    image: redis:7-alpine

  # Include build context for custom services
  ad-server:
    build:
      context: .
      dockerfile: src/AdServer/Dockerfile
    # Use explicit version/digest for reproducibility
    # Build args for flexibility
    build:
      context: .
      dockerfile: src/AdServer/Dockerfile
      args:
        - DOTNET_VERSION=8.0
```

### Image Optimization

**Best Practices:**

1. **Use Alpine base images** (~40MB vs. 200MB+ for full OS)
2. **Multi-stage builds** (only runtime files in final image)
3. **Minimize layers** (combine RUN commands with &&)
4. **Leverage Docker cache** (put stable layers first)

**Example (optimized):**

```dockerfile
# Good ✅
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder
COPY . .
RUN dotnet restore && \
    dotnet build -c Release && \
    dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
COPY --from=builder /app . 
ENTRYPOINT ["dotnet", "App.dll"]

# Bad ❌
RUN dotnet restore
RUN dotnet build
RUN dotnet publish
# Creates 3 layers instead of 1
```

### Container Networking

```bash
# Containers can reach each other by service name
# Example: bid-engine connects to postgres at "postgres:5432"

# List networks
docker network ls

# Inspect network
docker network inspect ads_network

# Test connectivity
docker exec ads_server ping postgres
docker exec ads_server ping redis
```

---

## Production Deployment

### Architecture Considerations

**Single Machine (Development):**
```
┌─────────────────────────────┐
│  Docker Host                │
│                             │
│  ┌─ ad-server              │
│  ├─ bid-engine             │
│  ├─ event-consumer         │
│  ├─ analytics-service      │
│  ├─ postgres               │
│  ├─ redis                  │
│  ├─ kafka                  │
│  └─ zookeeper              │
│                             │
└─────────────────────────────┘
```

**Kubernetes Cluster (Production):**
```
┌──────────────────────────────────────────┐
│  Kubernetes Cluster (3+ nodes)           │
│                                          │
│  ┌─ Stateless Services (Horizontally     │
│  │  scalable)                           │
│  │  - ad-server (replica: 3+)           │
│  │  - bid-engine (replica: 2+)          │
│  │  - analytics-service (replica: 2)    │
│  │                                      │
│  ├─ Stateful Services                  │
│  │  - kafka (replica: 3)                │
│  │  - postgres (replica: 1, backup)     │
│  │  - redis (cluster mode)              │
│  │                                      │
│  └─ Infrastructure                     │
│     - Prometheus (monitoring)           │
│     - Grafana (dashboards)              │
│     - Ingress (load balancer)           │
│                                          │
└──────────────────────────────────────────┘
```

### Kubernetes Deployment Example

Create `k8s/ad-server-deployment.yaml`:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: ad-server
  labels:
    app: ad-server
spec:
  replicas: 3  # Horizontal scaling
  selector:
    matchLabels:
      app: ad-server
  template:
    metadata:
      labels:
        app: ad-server
    spec:
      containers:
      - name: ad-server
        image: registry.example.com/ad-server:v1.0.0
        imagePullPolicy: Always
        
        ports:
        - containerPort: 8080
        
        # Environment variables from ConfigMap and Secrets
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: db-credentials
              key: connection-string
        - name: Redis__ConnectionString
          valueFrom:
            configMapKeyRef:
              name: app-config
              key: redis-url
        
        # Resource limits
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        
        # Health checks
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 5
        
        # Graceful shutdown
        lifecycle:
          preStop:
            exec:
              command: ["/bin/sh", "-c", "sleep 15"]

---
apiVersion: v1
kind: Service
metadata:
  name: ad-server
spec:
  selector:
    app: ad-server
  type: LoadBalancer
  ports:
  - protocol: TCP
    port: 80
    targetPort: 8080
```

### Environment Configuration

**Development (appsettings.Development.json):**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=5432;Database=ads_db_dev;User Id=postgres;Password=postgres;",
    "Redis": "localhost:6379"
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

**Production (appsettings.Production.json):**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "[ENCRYPTED_CONNECTION_STRING]",
    "Redis": "[REDIS_CLUSTER_ENDPOINTS]"
  },
  "Kafka": {
    "BootstrapServers": "[KAFKA_BROKERS]",
    "SecurityProtocol": "Ssl",
    "SaslMechanism": "Plain",
    "SaslUsername": "[USERNAME]",
    "SaslPassword": "[PASSWORD]"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### Secrets Management

```bash
# Using Kubernetes Secrets
kubectl create secret generic db-credentials \
  --from-literal=connection-string="..." \
  --from-literal=password="..."

# Using HashiCorp Vault (production)
vault kv put secret/ads-platform/db \
  connection-string="..." \
  password="..."

# Using Docker Secrets (Swarm)
echo "password123" | docker secret create db_password -
```

---

## Monitoring & Observability

### Prometheus Configuration

Create `infrastructure/monitoring/prometheus.yml`:

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s
  external_labels:
    cluster: 'ads-platform'
    environment: 'production'

scrape_configs:
  # Ad Server metrics
  - job_name: 'ad-server'
    static_configs:
      - targets: ['localhost:8080']
    metrics_path: '/metrics'

  # Bid Engine metrics
  - job_name: 'bid-engine'
    static_configs:
      - targets: ['localhost:8081']

  # Analytics Service metrics
  - job_name: 'analytics-service'
    static_configs:
      - targets: ['localhost:8082']

  # PostgreSQL exporter
  - job_name: 'postgres'
    static_configs:
      - targets: ['localhost:9187']

  # Redis exporter
  - job_name: 'redis'
    static_configs:
      - targets: ['localhost:9121']

  # Kafka exporter
  - job_name: 'kafka'
    static_configs:
      - targets: ['localhost:9308']

alerting:
  alertmanagers:
    - static_configs:
        - targets: ['localhost:9093']

rule_files:
  - '/etc/prometheus/rules/*.yml'
```

### Alert Rules

Create `infrastructure/monitoring/rules.yml`:

```yaml
groups:
- name: ad_serving
  rules:
  # Alert if p95 latency > 200ms
  - alert: HighAdServerLatency
    expr: histogram_quantile(0.95, rate(http_request_duration_seconds_bucket{job="ad-server"}[5m])) > 0.2
    for: 5m
    annotations:
      summary: "Ad Server latency high"
      description: "p95 latency is {{ $value }}s"

  # Alert if error rate > 1%
  - alert: HighErrorRate
    expr: rate(http_requests_total{job="ad-server",status=~"5.."}[5m]) > 0.01
    for: 5m
    annotations:
      summary: "High error rate on Ad Server"
      description: "Error rate is {{ $value | humanizePercentage }}"

  # Alert if no bids for 5 minutes
  - alert: NoBidsAvailable
    expr: rate(bid_requests_total{status="no_bid"}[5m]) > 0.5
    for: 5m
    annotations:
      summary: "No eligible campaigns for bidding"

- name: infrastructure
  rules:
  # Alert if Kafka lag > 10000 messages
  - alert: HighKafkaLag
    expr: kafka_consumergroup_lag > 10000
    for: 10m
    annotations:
      summary: "High Kafka consumer lag"
      description: "Consumer group lag is {{ $value }} messages"

  # Alert if database connections > 80
  - alert: HighDatabaseConnections
    expr: pg_stat_activity_count > 80
    for: 5m
    annotations:
      summary: "High database connection count"

  # Alert if Redis memory > 80%
  - alert: HighRedisMemory
    expr: redis_memory_used_bytes / redis_memory_max_bytes > 0.8
    for: 5m
    annotations:
      summary: "High Redis memory usage"
```

### Grafana Dashboards

Key dashboards to create:

**1. Service Health Dashboard**
- Pod status (running/crashed/pending)
- Resource usage (CPU, memory)
- Network I/O

**2. Ad Server Performance**
- Requests per second (QPS)
- Latency: p50, p95, p99
- Error rate by endpoint
- Bid success rate

**3. Bid Engine Performance**
- Campaign selection latency
- Budget tracking accuracy
- Cache hit rate
- Targeting rule match rate

**4. Event Pipeline Health**
- Events published per second
- Kafka producer latency
- Consumer lag per partition
- Events persisted to database

**5. Database Performance**
- Query latency
- Connection pool usage
- Slow query log
- Index efficiency

**6. Business Metrics**
- Total impressions
- Total clicks
- Average CTR
- Revenue (spend)
- Top campaigns

---

## Logging Strategy

### Structured Logging with Serilog

```csharp
// Configure in Program.cs
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Environment", environment)
    .Enrich.WithProperty("Service", "AdServer")
    .WriteTo.Console(new JsonFormatter())
    .WriteTo.File("logs/ad-server-.json", 
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Message:lj}{NewLine}{Exception}")
    .CreateLogger();

// Usage with structured data
_logger.LogInformation("Ad served to user {UserId} from campaign {CampaignId} with latency {LatencyMs}ms",
    request.UserId,
    bidResponse.CampaignId,
    stopwatch.ElapsedMilliseconds);

// Output JSON:
{
  "Timestamp": "2025-11-29T14:30:45.1234567Z",
  "Level": "Information",
  "MessageTemplate": "Ad served to user {UserId} from campaign {CampaignId} with latency {LatencyMs}ms",
  "Properties": {
    "UserId": "user-123",
    "CampaignId": "campaign-456",
    "LatencyMs": 45,
    "Service": "AdServer",
    "Environment": "Production"
  }
}
```

### Log Aggregation

**ELK Stack (Elasticsearch, Logstash, Kibana):**

```yaml
# docker-compose.yml extension
elasticsearch:
  image: docker.elastic.co/elasticsearch/elasticsearch:8.0.0
  environment:
    - discovery.type=single-node
  ports:
    - "9200:9200"

kibana:
  image: docker.elastic.co/kibana/kibana:8.0.0
  ports:
    - "5601:5601"
  depends_on:
    - elasticsearch

logstash:
  image: docker.elastic.co/logstash/logstash:8.0.0
  volumes:
    - ./logstash.conf:/usr/share/logstash/config/logstash.conf
  ports:
    - "5000:5000"
  depends_on:
    - elasticsearch
```

**Logstash Configuration:**

```
input {
  tcp {
    port => 5000
    codec => json
  }
}

filter {
  if [MessageTemplate] {
    mutate {
      add_field => { "[@metadata][index_name]" => "logs-%{Service}-%{+YYYY.MM.dd}" }
    }
  }
}

output {
  elasticsearch {
    hosts => ["elasticsearch:9200"]
    index => "%{[@metadata][index_name]}"
  }
}
```

---

## Troubleshooting

### Service Won't Start

```bash
# Check container logs
docker-compose logs ad-server

# Common issues:
# 1. Port already in use
lsof -i :8080
kill -9 <PID>

# 2. Database not ready
docker-compose logs postgres | grep -i error

# 3. Configuration error
docker exec ads_server cat appsettings.json | jq .
```

### High Latency

```bash
# 1. Check where time is spent
# (Add timing logs around each operation)
log.Information("Step 1: {Duration}ms", sw.ElapsedMilliseconds);

# 2. Analyze slow queries
psql -h localhost -U postgres -d ads_db << EOF
SELECT query, calls, total_time, mean_time
FROM pg_stat_statements
ORDER BY mean_time DESC
LIMIT 10;
EOF

# 3. Check Redis performance
redis-cli --latency-history

# 4. Monitor Kafka latency
kafka-run-class kafka.tools.VerifyConsumerLag \
  --broker-list kafka:9092 \
  --group ads-event-consumer \
  --verbose

# 5. Check network latency
docker exec ads_server ping postgres
```

### Database Connection Issues

```bash
# 1. Check connection string
echo $CONNECTION_STRING

# 2. Test connectivity
psql -h postgres -U postgres -d ads_db -c "SELECT 1;"

# 3. Monitor connections
psql -h localhost -U postgres << EOF
SELECT count(*), state FROM pg_stat_activity GROUP BY state;
EOF

# 4. Kill idle connections (if needed)
psql -h localhost -U postgres << EOF
SELECT pg_terminate_backend(pid)
FROM pg_stat_activity
WHERE state = 'idle'
  AND query_start < now() - interval '10 minutes';
EOF
```

### Kafka Issues

```bash
# 1. Check broker health
docker exec ads_kafka kafka-broker-api-versions --bootstrap-server localhost:9092

# 2. List topics
docker exec ads_kafka kafka-topics --bootstrap-server localhost:9092 --list

# 3. Check consumer group status
docker exec ads_kafka kafka-consumer-groups \
  --bootstrap-server localhost:9092 \
  --group ads-event-consumer \
  --describe

# 4. Reset consumer offset (if needed - DANGEROUS!)
docker exec ads_kafka kafka-consumer-groups \
  --bootstrap-server localhost:9092 \
  --group ads-event-consumer \
  --reset-offsets \
  --to-earliest \
  --execute \
  --topic impressions
```

---

## Performance Tuning

### Database Tuning

```sql
-- Check slow queries
CREATE EXTENSION pg_stat_statements;

-- Find missing indexes
SELECT schemaname, tablename, attname
FROM pg_stat_user_tables t
JOIN pg_attribute a ON t.relid = a.attrelid
WHERE seq_scan > 1000
  AND indexrelname IS NULL
ORDER BY seq_scan DESC;

-- Analyze query plans
EXPLAIN ANALYZE
SELECT * FROM campaigns
WHERE status = 'active'
  AND spent_today < daily_budget;

-- Vacuum and analyze
VACUUM ANALYZE campaigns;
```

### Redis Tuning

```bash
# Monitor memory usage
redis-cli INFO memory

# Check eviction policy
redis-cli CONFIG GET maxmemory-policy
# Options: noeviction, allkeys-lru, allkeys-lfu, volatile-lru, volatile-lfu

# Optimize for throughput
redis-cli CONFIG SET tcp-keepalive 300
redis-cli CONFIG SET timeout 0
redis-cli CONFIG SET tcp-backlog 511

# Monitor key size
redis-cli --bigkeys

# Persistence settings
redis-cli CONFIG GET save
# Trade-off: RDB persistence vs. performance
```

### Kafka Tuning

```properties
# broker config (server.properties)

# Throughput tuning
num.network.threads=8
num.io.threads=8
socket.send.buffer.bytes=102400
socket.receive.buffer.bytes=102400
socket.request.max.bytes=104857600

# Log optimization
log.segment.bytes=1073741824
log.cleanup.policy=delete
log.retention.hours=168

# Compression
compression.type=snappy
```

### Application Tuning

```csharp
// C# DbContext pooling
services.AddDbContextPool<AppDbContext>(options =>
    options.UseNpgsql(connectionString),
    poolSize: 128);

// HTTP client pooling
services.AddHttpClient<BidEngineClient>()
    .ConfigureHttpClient(client => {
        client.Timeout = TimeSpan.FromSeconds(1);
    })
    .AddPolicyHandler(GetRetryPolicy());

// Async patterns
// Use async/await, not Task.Result
public async Task<Ad> GetAdAsync() {
    var bid = await _bidEngineClient.GetBidAsync(request);
    return ConvertToAd(bid);
}
```

---

## Disaster Recovery

### Backup Strategy

**PostgreSQL Backups:**

```bash
# Full backup
pg_dump -h localhost -U postgres -d ads_db > /backups/ads_db_$(date +%Y%m%d).sql

# Point-in-time recovery setup
# Edit postgresql.conf:
# wal_level = replica
# archive_mode = on
# archive_command = 'cp %p /archive/%f'

# Restore from backup
psql -h localhost -U postgres -d ads_db < /backups/ads_db_20251129.sql
```

**Redis Snapshots:**

```bash
# RDB snapshot (append-only for durability)
redis-cli BGSAVE
# Creates /data/dump.rdb

# Restore
redis-cli SHUTDOWN
cp /backups/dump.rdb /data/
redis-server
```

**Kafka Retention:**

```bash
# Configure topic retention
docker exec ads_kafka kafka-configs \
  --bootstrap-server localhost:9092 \
  --entity-type topics \
  --entity-name impressions \
  --alter \
  --add-config retention.ms=604800000  # 7 days

# Check configuration
docker exec ads_kafka kafka-configs \
  --bootstrap-server localhost:9092 \
  --entity-type topics \
  --entity-name impressions \
  --describe
```

### Failover Procedures

**If Ad Server crashes:**
1. Kubernetes automatically restarts pod
2. Traffic routed to healthy replicas
3. No data loss (stateless service)

**If Bid Engine crashes:**
1. Ad Server retries requests (circuit breaker)
2. Returns 503 Service Unavailable
3. Client can retry or show default ad
4. Kubernetes restarts service

**If PostgreSQL crashes:**
1. Immediate alert triggered
2. Read-only replica takes over (if configured)
3. Automated failover to backup instance
4. Database transactions rolled back

**If Kafka broker crashes:**
1. Producer/consumer reconnects to other brokers
2. Replication factor = 3 ensures no data loss
3. Broker automatically rejoins when recovered
4. No manual intervention needed

### Recovery Procedures

```bash
# 1. Full service restart
docker-compose down
docker-compose up -d

# 2. Database recovery from backup
psql -h localhost -U postgres -d ads_db < /backups/latest.sql

# 3. Kafka topic recovery
docker exec ads_kafka kafka-topics \
  --bootstrap-server localhost:9092 \
  --create \
  --topic impressions \
  --partitions 10 \
  --replication-factor 3

# 4. Redis cache warm-up
# Cache will repopulate on next request (TTL: 5 minutes)
# Or manually load campaigns from database

# 5. Event Consumer resume from checkpoint
docker-compose restart event-consumer
# Automatically resumes from last committed offset
```

---

## Maintenance Windows

### Planned Maintenance

```bash
# 1. Notify users (SLA: 1 hour notice)
# Send: "Maintenance window: 2025-12-01 02:00-02:30 UTC"

# 2. Gradual traffic shift
# Scale down ad-server replicas: 3 → 2 → 1

# 3. Perform maintenance
# Database migrations, dependency updates, etc.

# 4. Verify changes
docker-compose restart postgres
psql -h localhost -U postgres -d ads_db -c "SELECT version();"

# 5. Gradual traffic restore
# Scale up: 1 → 2 → 3

# 6. Monitor metrics for 30 minutes
open http://localhost:3000

# 7. Complete notification
# "Maintenance complete. All systems nominal."
```

---

## Checklist: Before Going to Production

- [ ] All integration tests passing
- [ ] Load tests validate p95 < 100ms SLO
- [ ] Database indexes created
- [ ] Replication configured (3+ replicas)
- [ ] Backups tested and automated
- [ ] Monitoring dashboards created
- [ ] Alert thresholds set and tested
- [ ] Log aggregation working
- [ ] Secrets management in place
- [ ] Network security groups configured
- [ ] SSL/TLS certificates installed
- [ ] Disaster recovery procedure documented
- [ ] Runbooks written for common issues
- [ ] On-call rotation established
- [ ] Status page created
- [ ] Documentation reviewed and approved

---

**Deployment Complete!**

Your Ad Server and Bidding Engine is now production-ready. Monitor metrics closely in the first week and be ready to adjust thresholds and configurations based on real-world performance data.

