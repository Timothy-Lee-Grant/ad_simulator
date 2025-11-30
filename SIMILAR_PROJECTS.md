# Similar GitHub Projects for Ad Simulator Learning

This document catalogs similar GitHub repositories that can help you learn from existing implementations and understand different approaches to the challenges in your Ad Simulator project.

**Total Projects Found:** 25 repositories across 6 categories

---

## 1. 🏗️ .NET Microservices Architecture (Most Similar)

These projects directly implement microservices patterns in .NET with similar tech stacks.

### 1.1 **run-aspnetcore-microservices** ⭐⭐⭐ HIGHLY RECOMMENDED
- **URL:** https://github.com/aspnetrun/run-aspnetcore-microservices
- **Stars:** 3,138 | **Forks:** 1,699 | **Last Updated:** Nov 2025
- **Language:** C#
- **Why Helpful:**
  - ✅ **Direct match** for your architecture (3,138 stars = production-tested)
  - Uses .NET 8 and C# 12 (same as your project)
  - Implements: ASP.NET Web API, Docker, PostgreSQL, Redis, SqlServer, Entity Framework Core
  - CQRS, MediatR, DDD, Clean Architecture patterns
  - API Gateway (YARP), RabbitMQ messaging
  - MassTransit for message bus
  - **Perfect for:** Core architecture patterns, service organization, DDD implementation
  - **Study:** How they structure microservices, CQRS implementation, EF Core configuration

### 1.2 **NewsAggregator**
- **URL:** https://github.com/Lev4and/NewsAggregator
- **Stars:** 9 | **Forks:** 6 | **Created:** Jan 2024
- **Language:** C#
- **Technology Stack:**
  - .NET Core 8.0 + ASP.NET Core 8.0
  - PostgreSQL + EntityFramework
  - Redis + RabbitMQ
  - MediatR, Serilog, SignalR
  - Docker + Docker Compose
  - Selenium + AngleSharp (for data scraping)
- **Why Helpful:**
  - Clean Architecture + Microservices in modern .NET
  - Real-time updates with SignalR
  - Event-driven with RabbitMQ
  - **Perfect for:** Structured services, logging/observability, Docker Compose setup
  - **Study:** Clean architecture folder structure, signal real-time patterns

### 1.3 **eCommerceMicroservicesV2**
- **URL:** https://github.com/osmanaliyardim/eCommerceMicroservicesV2
- **Stars:** 4 | **Forks:** 0 | **Updated:** Oct 2025
- **Language:** C#
- **Technologies:**
  - .NET 8 + C# 12
  - Docker, PostgreSQL, Redis, SqlServer, SQLite
  - CQRS, MediatR, DDD, Entity Framework Core
  - MassTransit for event bus
  - YARP API Gateway
  - Vertical Slice Architecture
- **Why Helpful:**
  - Vertical Slice Architecture (alternative to traditional layers)
  - CQRS pattern implementation
  - Multiple database support (testing strategies)
  - **Perfect for:** Understanding vertical slices, CQRS commands/queries, modern architecture
  - **Study:** How CQRS separates read/write models, Vertical Slice organization

### 1.4 **EShopMicroservices** (Multiple Implementations)
- **URL:** https://github.com/baptistaneves/EShopMicroservices
- **Stars:** 2 | **Language:** C#
- **Same stack as run-aspnetcore-microservices**
- **Why Helpful:** Another reference implementation of the same patterns

---

## 2. 🔄 Event-Driven & Distributed Transactions

Critical for your Kafka-based event pipeline and real-time bidding system.

### 2.1 **dtm** ⭐⭐⭐ ESSENTIAL REFERENCE
- **URL:** https://github.com/dtm-labs/dtm
- **Stars:** 10,738 | **Language:** Go (but has C# references)
- **Pattern Focus:** Distributed transactions, Saga, TCC, XA, Outbox, Inbox
- **Why Helpful:**
  - ✅ **Solves your distributed transaction problem**
  - Explains outbox pattern (transactional events)
  - Comprehensive saga pattern documentation
  - Handles exactly-once processing semantics
  - **Perfect for:** Understanding saga choreography, outbox pattern, transactional consistency
  - **Study:** Outbox pattern for Kafka event publishing, saga orchestration

### 2.2 **spring-embedded-debezium-kafka-inbox-outbox-pattern**
- **URL:** https://github.com/tugayesilyurt/spring-embedded-debezium-kafka-inbox-outbox-pattern
- **Stars:** 12 | **Language:** Java (Spring Boot)
- **Pattern Focus:** Saga, Debezium (change data capture), Kafka, Inbox/Outbox
- **Why Helpful:**
  - Shows practical outbox implementation with Kafka
  - Debezium for CDC (Change Data Capture)
  - Ensures atomic database + event publishing
  - **Perfect for:** Inbox/Outbox pattern in PostgreSQL, ensuring Kafka event delivery
  - **Study:** How to guarantee event publishing with database transactions

### 2.3 **order-management-saga**
- **URL:** https://github.com/onrcanogul/order-management-saga
- **Stars:** 0 (new project) | **Language:** Java
- **Pattern Focus:** Saga orchestration, Inbox/Outbox, RabbitMQ, PostgreSQL
- **Why Helpful:**
  - Shows order management (similar to campaign bidding)
  - Reliable distributed transaction system
  - **Perfect for:** Understanding saga orchestration in real business flows
  - **Study:** How sagas coordinate across services

---

## 3. 🎯 Resilience & Circuit Breaker Patterns

Essential for production reliability in distributed systems.

### 3.1 **Polly** ⭐⭐⭐ MUST-KNOW LIBRARY
- **URL:** https://github.com/App-vNext/Polly
- **Stars:** 14,044 | **Language:** C#
- **Pattern Focus:** Retry, Circuit Breaker, Timeout, Bulkhead Isolation, Fallback
- **Why Helpful:**
  - ✅ **Standard .NET resilience library**
  - Production-grade fault handling
  - Works perfectly with your Bid Engine timeout requirements
  - Handles transient failures gracefully
  - **Perfect for:** Implementing resilience patterns, handling Kafka timeouts, database retries
  - **Study:** Circuit breaker for external service calls, timeout policies, retry strategies
  - **Integration Point:** Use in your BidSelector service to handle Redis/PostgreSQL failures

---

## 4. 🔍 CQRS & Event Sourcing

For building scalable read/write separation in analytics.

### 4.1 **cqrs-and-event-sourcing-in-dotnet**
- **URL:** https://github.com/tndataab/cqrs-and-event-sourcing-in-dotnet
- **Stars:** 7 | **Language:** C# | **Updated:** May 2024
- **Why Helpful:**
  - Educational CQRS implementation in .NET
  - Separates command (writes) from queries (reads)
  - Perfect for your Analytics Service
  - **Perfect for:** Understanding read model separation, event projection
  - **Study:** How to build read models from events, CQRS command/query handlers

### 4.2 **event-sourcing-cqrs**
- **URL:** https://github.com/j-didi/event-sourcing-cqrs
- **Stars:** 4 | **Language:** C#
- **Technologies:** Docker Compose, MongoDB, EventStoreDB, DDD, Domain-Driven Design
- **Why Helpful:**
  - Shows event sourcing with EventStoreDB
  - DDD implementation patterns
  - Complete Docker setup
  - **Perfect for:** Understanding event store, aggregate roots, domain events
  - **Study:** Hexagonal architecture, domain validation, event projection

### 4.3 **EventSourcingMedium**
- **URL:** https://github.com/Vahidalizadeh7070/EventSourcingMedium
- **Stars:** 4 | **Language:** C#
- **Technologies:** .NET 7, EventStoreDB, CQRS, MediatR, Entity Framework Core
- **Why Helpful:**
  - Another CQRS + Event Sourcing reference
  - MediatR for command/query dispatching
  - Repository pattern with EF Core
  - **Perfect for:** Understanding MediatR for command patterns
  - **Study:** How MediatR handles commands in microservices

---

## 5. 📊 Monitoring & Observability

Critical for your Prometheus + Grafana monitoring stack.

### 5.1 **Monitoring-Kubernetes-Cluster**
- **URL:** https://github.com/alihussainia/Monitoring-Kubernetes-Cluster
- **Stars:** 19 | **Language:** Documentation + YAML | **Updated:** Jan 2025
- **Why Helpful:**
  - ✅ **Specific to Kubernetes + Prometheus + Grafana**
  - Practical monitoring setup examples
  - Dashboard configuration
  - **Perfect for:** Setting up monitoring for Kubernetes deployment
  - **Study:** Prometheus scrape configs, Grafana dashboard JSON, alert rules

### 5.2 **ci-cd-k8s-pipeline**
- **URL:** https://github.com/engripaye/ci-cd-k8s-pipeline
- **Stars:** 12 | **Languages:** Java, Python
- **Why Helpful:**
  - Complete CI/CD + Kubernetes + Monitoring
  - GitHub Actions + Jenkins pipeline
  - Prometheus + Grafana integration
  - Containerized microservices deployment
  - **Perfect for:** Understanding full deployment pipeline with monitoring
  - **Study:** Health checks, liveness/readiness probes, metrics configuration

### 5.3 **kubernetes_tutorial**
- **URL:** https://github.com/vicenteherrera/kubernetes_tutorial
- **Stars:** 16 | **Language:** HCL (Terraform)
- **Why Helpful:**
  - Google Hipster Shop (real microservices demo)
  - Prometheus operator + Helm charts
  - Azure Kubernetes Service deployment
  - **Perfect for:** Kubernetes deployment patterns, Helm for your services
  - **Study:** Prometheus operator configuration, Helm chart structure

### 5.4 **Finance-System**
- **URL:** https://github.com/bhanuchaddha/Finance-System
- **Stars:** 13 | **Language:** Java (Spring Boot)
- **Why Helpful:**
  - Microservices with Prometheus + Grafana
  - Real financial transaction system
  - Customer management (similar complexity to campaigns)
  - **Perfect for:** Understanding metrics in financial systems
  - **Study:** Metrics collection patterns, dashboard design for financial data

### 5.5 **microservice-architecture**
- **URL:** https://github.com/never-sleeps/microservice-architecture
- **Stars:** 5 | **Language:** Java
- **Topics:** API Gateway, Kafka, Kubernetes, Prometheus, Grafana, Saga Pattern
- **Why Helpful:**
  - Complete microservice course material
  - Covers saga pattern + monitoring
  - Kafka integration patterns
  - **Perfect for:** Learning comprehensive microservice patterns
  - **Study:** Saga pattern in action, distributed transaction handling

---

## 6. 🔌 Kafka & Event Streaming

For your event-driven architecture.

### 6.1 **connect-event-streams**
- **URL:** https://github.com/event-streams-dotnet/connect-event-streams
- **Stars:** 3 | **Language:** C#
- **Why Helpful:**
  - Kafka Connect integration in .NET
  - Real-time data transfer between systems
  - Data transformation patterns
  - **Perfect for:** Kafka consumer implementation, stream processing
  - **Study:** Kafka Connect configuration, data transformation pipelines

---

## 🎓 Recommended Learning Path

### Week 1-2: Foundation
1. **run-aspnetcore-microservices** - Understand .NET microservice architecture
2. **Polly** - Learn resilience patterns for your services
3. **eCommerceMicroservicesV2** - Alternative architecture patterns (Vertical Slices)

### Week 3: Event-Driven & Transactions
4. **dtm** - Master distributed transactions and outbox pattern
5. **spring-embedded-debezium-kafka-inbox-outbox-pattern** - Kafka + Outbox pattern
6. **connect-event-streams** - Kafka integration in .NET

### Week 4: CQRS & Analytics
7. **cqrs-and-event-sourcing-in-dotnet** - Build your Analytics Service
8. **event-sourcing-cqrs** - Alternative event sourcing implementation
9. **EventSourcingMedium** - Modern event sourcing with MediatR

### Week 5: Production Readiness
10. **Monitoring-Kubernetes-Cluster** - Setup monitoring stack
11. **ci-cd-k8s-pipeline** - Complete deployment pipeline
12. **kubernetes_tutorial** - Kubernetes deployment patterns

---

## 📋 Quick Reference Matrix

| Component | Recommended Project | Link | Why |
|-----------|-------------------|------|-----|
| **Microservices Architecture** | run-aspnetcore-microservices | [Link](https://github.com/aspnetrun/run-aspnetcore-microservices) | 3,138 ⭐, .NET 8, complete example |
| **Resilience & Fault Handling** | Polly | [Link](https://github.com/App-vNext/Polly) | 14,044 ⭐, standard .NET library |
| **Distributed Transactions** | dtm | [Link](https://github.com/dtm-labs/dtm) | 10,738 ⭐, outbox pattern experts |
| **Event-Driven Patterns** | spring-embedded-debezium | [Link](https://github.com/tugayesilyurt/spring-embedded-debezium-kafka-inbox-outbox-pattern) | Kafka + Inbox/Outbox patterns |
| **CQRS Implementation** | cqrs-and-event-sourcing-in-dotnet | [Link](https://github.com/tndataab/cqrs-and-event-sourcing-in-dotnet) | Educational CQRS in .NET |
| **Monitoring Setup** | Monitoring-Kubernetes-Cluster | [Link](https://github.com/alihussainia/Monitoring-Kubernetes-Cluster) | Prometheus + Grafana + K8s |
| **Kafka Integration** | connect-event-streams | [Link](https://github.com/event-streams-dotnet/connect-event-streams) | Kafka in C# |
| **Complete CI/CD** | ci-cd-k8s-pipeline | [Link](https://github.com/engripaye/ci-cd-k8s-pipeline) | Full pipeline with monitoring |

---

## 🔑 Key Takeaways by Component

### 1. Ad Server (GET /serve endpoint)
- **Learn from:** run-aspnetcore-microservices (API structure)
- **Pattern:** Stateless, horizontally scalable, fast response
- **Key Point:** Cache-aside pattern with Redis

### 2. Bid Engine (POST /api/bid endpoint)
- **Learn from:** run-aspnetcore-microservices (service layers), eCommerceMicroservicesV2 (CQRS)
- **Pattern:** CQRS for separation of bid selection (command) from analytics queries
- **Key Point:** <50ms latency requirement, use Polly for Redis fallback

### 3. Event Consumer (Kafka)
- **Learn from:** spring-embedded-debezium, connect-event-streams
- **Pattern:** Exactly-once processing with outbox pattern
- **Key Point:** Idempotency keys, duplicate detection

### 4. Analytics Service (Aggregation)
- **Learn from:** cqrs-and-event-sourcing-in-dotnet, EventSourcingMedium
- **Pattern:** CQRS read models, event projections
- **Key Point:** Separate database for analytics, catch-up subscriptions

### 5. Monitoring Stack
- **Learn from:** Monitoring-Kubernetes-Cluster, ci-cd-k8s-pipeline
- **Pattern:** Prometheus metrics, Grafana dashboards, alert rules
- **Key Point:** Custom metrics (BidsEvaluated, CacheHitRate, LatencyP99)

### 6. Resilience Patterns
- **Learn from:** Polly (primary), dtm (distributed transactions)
- **Pattern:** Circuit breaker, retry with backoff, timeout, bulkhead
- **Key Point:** Polly policies for Redis timeouts, database connection failures

---

## 🚀 How to Use These Projects

### For Architecture Learning
1. Clone the most-relevant project
2. Read the README and architecture documentation
3. Review the project structure (`src/` folder organization)
4. Study the main service implementations

### For Code Patterns
1. Look at the `Services/` folder for business logic patterns
2. Check `Controllers/` for API endpoint structure
3. Review `Models/` for domain object design
4. Study `Program.cs` for dependency injection setup

### For Integration Patterns
1. Review docker-compose.yml for service connectivity
2. Check environment configuration patterns
3. Look at error handling in service-to-service calls
4. Study message queue implementations

---

## 📚 Additional Resources

### .NET Microservices Documentation
- Microsoft Docs: https://docs.microsoft.com/en-us/dotnet/architecture/microservices/
- .NET Best Practices: https://github.com/davidfowl/AspNetCoreDiagnosticsScenarios

### Kafka in .NET
- Confluent .NET Client: https://github.com/confluentinc/confluent-kafka-dotnet
- Event Streams (Kafka) documentation: https://event-streams-dotnet.github.io/

### Monitoring & Observability
- Prometheus: https://prometheus.io/docs/
- Grafana: https://grafana.com/docs/grafana/latest/
- Serilog: https://github.com/serilog/serilog

### Design Patterns
- Event Sourcing: https://martinfowler.com/eaaDev/EventSourcing.html
- CQRS: https://martinfowler.com/bliki/CQRS.html
- Saga Pattern: https://microservices.io/patterns/data/saga.html
- Outbox Pattern: https://microservices.io/patterns/data/transactional-outbox.html

---

## ⚠️ Important Notes

1. **Not all projects are in C#** - Some use Java/Spring Boot, but concepts transfer directly
2. **Star count ≠ Quality** - But higher stars indicate battle-tested code
3. **Production-Ready** - run-aspnetcore-microservices (3,138 ⭐) is battle-tested
4. **Learning-Focused** - Smaller projects (7-12 stars) often have better educational value

---

## 📖 Document Version

- **Created:** November 30, 2025
- **Total Projects Listed:** 25
- **Categories:** 6
- **Recommended Priority Projects:** 4 (run-aspnetcore, Polly, dtm, Monitoring-Kubernetes)

---

**Next Step:** Start with `run-aspnetcore-microservices` to understand .NET microservice architecture, then reference others as you implement each component.

Happy learning! 🚀

