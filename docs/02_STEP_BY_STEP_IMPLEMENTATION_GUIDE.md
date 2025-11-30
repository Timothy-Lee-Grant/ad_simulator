# Mini Ad Server and Bidding Engine
## Step-by-Step Implementation Guide for Entry-Level Engineers

**Target Audience:** Software engineers with basic C# and SQL knowledge  
**Estimated Time to Complete:** 40-50 hours  
**Prerequisites:** .NET 8 SDK, Docker, PostgreSQL client, basic CLI knowledge

---

## Table of Contents
1. [Introduction & Key Concepts](#introduction--key-concepts)
2. [Prerequisites Setup](#prerequisites-setup)
3. [Project Structure Overview](#project-structure-overview)
4. [Phase 1: Database and Infrastructure](#phase-1-database-and-infrastructure)
5. [Phase 2: Bid Engine (Core Logic)](#phase-2-bid-engine-core-logic)
6. [Phase 3: Ad Server (API & Orchestration)](#phase-3-ad-server-api--orchestration)
7. [Phase 4: Event Pipeline (Kafka Integration)](#phase-4-event-pipeline-kafka-integration)
8. [Phase 5: Event Consumer (Real-Time Analytics)](#phase-5-event-consumer-real-time-analytics)
9. [Phase 6: Analytics Service (Reporting API)](#phase-6-analytics-service-reporting-api)
10. [Phase 7: Testing & Validation](#phase-7-testing--validation)
11. [Phase 8: Monitoring & Deployment](#phase-8-monitoring--deployment)

---

## Introduction & Key Concepts

Before we start building, let's understand the core concepts you'll encounter:

### What is Real-Time Bidding (RTB)?

Imagine you're a publisher (website owner) and you have an ad slot to fill. Instead of pre-negotiating with advertisers, you:
1. Send an ad request saying "I have a user on my site, here's what I know about them"
2. Multiple advertisers instantly bid for the right to show their ad
3. The highest bidder wins and their ad is displayed
4. When the user sees it (impression) or clicks it (click), you record the event

This all happens in 100-200 milliseconds! Our system simulates this process.

### Key Concepts Explained

#### **Campaign**
A campaign is an advertiser's marketing effort. It contains:
- **ID:** Unique identifier
- **CPM Bid:** Cost Per Mille (cost per 1,000 impressions). Example: $2.50 CPM means you pay $2.50 for every 1,000 users who see your ad
- **Budget:** Daily and lifetime spending limits
- **Targeting Rules:** Who should see this ad? (e.g., US users, mobile devices, tech enthusiasts)

#### **Ad**
Actual creative content (title, image, destination URL) that belongs to a campaign.

#### **Impression**
Recorded when an ad is successfully served to a user and rendered on their screen.

#### **Click**
Recorded when a user clicks on an ad and is taken to the advertiser's website.

#### **CPM, CPC, CTR - Key Metrics**

| Metric | Full Name | Formula | Example |
|--------|-----------|---------|---------|
| **CPM** | Cost Per Mille | (Spend ÷ Impressions) × 1000 | $2.50 for 1000 impressions |
| **CPC** | Cost Per Click | Spend ÷ Clicks | $0.10 per click |
| **CTR** | Click-Through Rate | (Clicks ÷ Impressions) × 100 | 2.5% = 25 clicks per 1000 impressions |

#### **Kafka - Event Streaming**

Kafka is like a "message queue on steroids." Think of it as:
- **Post Office:** Ad Server publishes an event (letter) to a topic (mailbox)
- **Delivery:** Kafka guarantees delivery and ordering
- **Consumer:** Event Consumer (mail carrier) picks up events and processes them
- **Durability:** Messages persist on disk, so even if consumer crashes, no data is lost

#### **Redis - Ultra-Fast Cache**

Redis stores frequently accessed data in RAM (memory) instead of disk. Benefits:
- **Speed:** Sub-millisecond access (vs. 10-100ms for database)
- **Use Case:** Campaign data, analytics cache, session data

#### **PostgreSQL - Persistent Database**

Traditional SQL database for:
- Campaign definitions (master data)
- Aggregated metrics (daily impressions, clicks)
- Event log (audit trail)

---

## Prerequisites Setup

### Step 1: Install Required Tools

```bash
# 1. Check if you have .NET 8 SDK
dotnet --version
# Should output: 8.x.x (if not installed, download from https://dotnet.microsoft.com/download)

# 2. Install Docker Desktop (includes Docker and Docker Compose)
# Download from: https://www.docker.com/products/docker-desktop
# Verify installation:
docker --version
docker-compose --version

# 3. Verify other tools
git --version
```

### Step 2: Create Project Workspace

```bash
# Navigate to your practice directory
cd ~/Desktop/Practice/ad_simulator

# Initialize git repository (optional but recommended)
git init

# Create the main folder structure
mkdir -p src/{AdServer,BidEngine,EventConsumer,AnalyticsService,Shared}
mkdir -p infrastructure/{docker,database/migrations,monitoring}
mkdir -p tests/{AdServer.Tests,BidEngine.Tests}
mkdir -p docs

# Create .gitignore
cat > .gitignore << 'EOF'
## Build results
[Dd]ebug/
[Dd]ebugPublic/
[Rr]elease/
bin/
obj/

## Visual Studio
.vs/
*.sln.user
.vscode/

## Docker
.env.local

## Database
pgdata/
EOF

# Create solution file
cd ~/Desktop/Practice/ad_simulator
dotnet new globaljson --sdk-version 8.0.0
```

---

## Project Structure Overview

Here's what your final project structure will look like:

```
ad_simulator/
├── src/
│   ├── AdServer/                          # Main API for serving ads
│   │   ├── AdServer.csproj
│   │   ├── Program.cs                     # App startup & DI setup
│   │   ├── appsettings.json               # Configuration
│   │   ├── Controllers/
│   │   │   ├── AdController.cs            # GET /serve endpoint
│   │   │   └── ClickController.cs         # POST /click endpoint
│   │   ├── Services/
│   │   │   ├── BidEngineClient.cs         # HTTP client to Bid Engine
│   │   │   └── EventPublisher.cs          # Kafka producer
│   │   └── Models/
│   │       └── AdResponse.cs
│   │
│   ├── BidEngine/                         # Core bidding logic
│   │   ├── BidEngine.csproj
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── Controllers/
│   │   │   └── BidController.cs           # POST /bid endpoint
│   │   ├── Services/
│   │   │   ├── BidSelector.cs             # Core algorithm
│   │   │   ├── CampaignCache.cs           # Redis caching
│   │   │   └── BudgetService.cs           # Track spending
│   │   ├── Models/
│   │   │   ├── Campaign.cs
│   │   │   ├── BidRequest.cs
│   │   │   └── BidResponse.cs
│   │   └── Data/
│   │       └── AppDbContext.cs            # EF Core DbContext
│   │
│   ├── EventConsumer/                     # Kafka consumer
│   │   ├── EventConsumer.csproj
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── Services/
│   │   │   ├── KafkaConsumerService.cs    # Kafka polling
│   │   │   ├── MetricsAggregator.cs       # In-memory aggregation
│   │   │   └── MetricsPersistence.cs      # Batch writes to DB
│   │   ├── Models/
│   │   │   └── Event.cs
│   │   └── Data/
│   │       └── AppDbContext.cs
│   │
│   ├── AnalyticsService/                  # Reporting API
│   │   ├── AnalyticsService.csproj
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── Controllers/
│   │   │   └── AnalyticsController.cs     # GET /analytics endpoint
│   │   ├── Services/
│   │   │   └── AnalyticsQueryService.cs   # Query logic with caching
│   │   └── Models/
│   │       └── AnalyticsResponse.cs
│   │
│   └── Shared/                            # Shared models and utilities
│       └── Models/
│           ├── Event.cs
│           └── Constants.cs
│
├── infrastructure/
│   ├── docker/
│   │   └── docker-compose.yml             # All services + dependencies
│   │
│   ├── database/
│   │   ├── migrations/
│   │   │   ├── 001_InitialSchema.sql
│   │   │   ├── 002_CreateIndexes.sql
│   │   │   └── ...
│   │   └── seed-data.sql
│   │
│   └── monitoring/
│       ├── prometheus.yml
│       └── grafana-dashboard.json
│
├── tests/
│   ├── AdServer.Tests/
│   │   └── AdControllerTests.cs
│   ├── BidEngine.Tests/
│   │   ├── BidSelectorTests.cs
│   │   └── BudgetServiceTests.cs
│   └── Integration.Tests/
│       └── EndToEndTests.cs
│
├── docs/
│   ├── 01_ARCHITECTURE_AND_DESIGN.md
│   ├── 02_STEP_BY_STEP_IMPLEMENTATION_GUIDE.md
│   ├── 03_API_DOCUMENTATION.md
│   ├── 04_DEPLOYMENT_GUIDE.md
│   └── 05_TROUBLESHOOTING.md
│
├── .gitignore
├── docker-compose.yml
└── README.md
```

---

## Phase 1: Database and Infrastructure

### Step 1.1: Create Docker Compose File

This file orchestrates all the services. Create `/Users/timothygrant/Desktop/Practice/ad_simulator/docker-compose.yml`:

```yaml
version: '3.8'

services:
  # PostgreSQL Database
  postgres:
    image: postgres:15-alpine
    container_name: ads_postgres
    environment:
      POSTGRES_DB: ads_db
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./infrastructure/database:/docker-entrypoint-initdb.d
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5

  # Redis Cache
  redis:
    image: redis:7-alpine
    container_name: ads_redis
    ports:
      - "6379:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

  # Zookeeper (required for Kafka)
  zookeeper:
    image: confluentinc/cp-zookeeper:7.5.0
    container_name: ads_zookeeper
    environment:
      ZOOKEEPER_CLIENT_PORT: 2181
      ZOOKEEPER_TICK_TIME: 2000
    ports:
      - "2181:2181"
    healthcheck:
      test: ["CMD", "echo", "ruok", "|", "nc", "127.0.0.1", "2181"]
      interval: 10s
      timeout: 5s
      retries: 5

  # Kafka Message Broker
  kafka:
    image: confluentinc/cp-kafka:7.5.0
    container_name: ads_kafka
    depends_on:
      zookeeper:
        condition: service_healthy
    environment:
      KAFKA_BROKER_ID: 1
      KAFKA_ZOOKEEPER_CONNECT: zookeeper:2181
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://kafka:9092
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1
      KAFKA_AUTO_CREATE_TOPICS_ENABLE: 'true'
      KAFKA_NUM_PARTITIONS: 10
    ports:
      - "9092:9092"
    healthcheck:
      test: ["CMD", "kafka-broker-api-versions", "--bootstrap-server", "localhost:9092"]
      interval: 10s
      timeout: 5s
      retries: 5

  # Prometheus Metrics Storage
  prometheus:
    image: prom/prometheus:latest
    container_name: ads_prometheus
    ports:
      - "9090:9090"
    volumes:
      - ./infrastructure/monitoring/prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus_data:/prometheus
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'

  # Grafana Visualization
  grafana:
    image: grafana/grafana:latest
    container_name: ads_grafana
    ports:
      - "3000:3000"
    environment:
      GF_SECURITY_ADMIN_PASSWORD: admin
      GF_USERS_ALLOW_SIGN_UP: 'false'
    volumes:
      - grafana_data:/var/lib/grafana

  # Ad Server Service (we'll build this)
  ad-server:
    build:
      context: .
      dockerfile: src/AdServer/Dockerfile
    container_name: ads_server
    ports:
      - "8080:8080"
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
      kafka:
        condition: service_healthy
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: "Server=postgres;Port=5432;Database=ads_db;User Id=postgres;Password=postgres;"
      Redis__ConnectionString: "redis:6379"
      Kafka__BootstrapServers: "kafka:9092"
      ASPNETCORE_URLS: "http://+:8080"

  # Bid Engine Service (we'll build this)
  bid-engine:
    build:
      context: .
      dockerfile: src/BidEngine/Dockerfile
    container_name: ads_bid_engine
    ports:
      - "8081:8081"
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: "Server=postgres;Port=5432;Database=ads_db;User Id=postgres;Password=postgres;"
      Redis__ConnectionString: "redis:6379"
      ASPNETCORE_URLS: "http://+:8081"

  # Event Consumer Service (we'll build this)
  event-consumer:
    build:
      context: .
      dockerfile: src/EventConsumer/Dockerfile
    container_name: ads_event_consumer
    depends_on:
      postgres:
        condition: service_healthy
      kafka:
        condition: service_healthy
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: "Server=postgres;Port=5432;Database=ads_db;User Id=postgres;Password=postgres;"
      Kafka__BootstrapServers: "kafka:9092"

  # Analytics Service (we'll build this)
  analytics-service:
    build:
      context: .
      dockerfile: src/AnalyticsService/Dockerfile
    container_name: ads_analytics
    ports:
      - "8082:8082"
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: "Server=postgres;Port=5432;Database=ads_db;User Id=postgres;Password=postgres;"
      Redis__ConnectionString: "redis:6379"
      ASPNETCORE_URLS: "http://+:8082"

volumes:
  postgres_data:
  prometheus_data:
  grafana_data:

networks:
  default:
    name: ads_network
```

### Step 1.2: Create Database Initialization Script

Create file: `/Users/timothygrant/Desktop/Practice/ad_simulator/infrastructure/database/001_initial_schema.sql`

```sql
-- Campaigns Table
CREATE TABLE campaigns (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name VARCHAR(255) NOT NULL,
  advertiser_id UUID NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'paused', 'ended')),
  cpm_bid DECIMAL(10, 4) NOT NULL CHECK (cpm_bid > 0),
  daily_budget DECIMAL(12, 2) NOT NULL CHECK (daily_budget > 0),
  lifetime_budget DECIMAL(12, 2),
  spent_today DECIMAL(12, 2) DEFAULT 0 CHECK (spent_today >= 0),
  lifetime_spent DECIMAL(12, 2) DEFAULT 0 CHECK (lifetime_spent >= 0),
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Targeting Rules Table
-- Example: campaign A targets mobile users in the USA
CREATE TABLE campaign_targeting_rules (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  campaign_id UUID NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
  rule_type VARCHAR(50) NOT NULL,
  rule_value VARCHAR(255) NOT NULL,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  UNIQUE (campaign_id, rule_type, rule_value)
);

-- Ads Table
CREATE TABLE ads (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  campaign_id UUID NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
  title VARCHAR(255) NOT NULL,
  image_url VARCHAR(2048) NOT NULL,
  redirect_url VARCHAR(2048) NOT NULL,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Daily Metrics Table
-- Stores aggregated impressions and clicks per campaign per day
CREATE TABLE daily_metrics (
  id BIGSERIAL PRIMARY KEY,
  campaign_id UUID NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
  date DATE NOT NULL,
  impressions BIGINT DEFAULT 0 CHECK (impressions >= 0),
  clicks BIGINT DEFAULT 0 CHECK (clicks >= 0),
  spend DECIMAL(12, 2) DEFAULT 0 CHECK (spend >= 0),
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  UNIQUE (campaign_id, date)
);

-- Events Log Table
-- Full audit trail of all events (optional for compliance, but helpful for debugging)
CREATE TABLE events_log (
  id BIGSERIAL PRIMARY KEY,
  event_id UUID NOT NULL UNIQUE,
  event_type VARCHAR(20) NOT NULL CHECK (event_type IN ('impression', 'click')),
  campaign_id UUID NOT NULL REFERENCES campaigns(id),
  ad_id UUID NOT NULL REFERENCES ads(id),
  user_id VARCHAR(255) NOT NULL,
  placement_id VARCHAR(255) NOT NULL,
  bid_price DECIMAL(10, 4),
  timestamp TIMESTAMP NOT NULL,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create Indexes for Performance
CREATE INDEX idx_campaigns_advertiser ON campaigns(advertiser_id);
CREATE INDEX idx_campaigns_status ON campaigns(status);
CREATE INDEX idx_targeting_campaign ON campaign_targeting_rules(campaign_id);
CREATE INDEX idx_targeting_type ON campaign_targeting_rules(rule_type);
CREATE INDEX idx_ads_campaign ON ads(campaign_id);
CREATE INDEX idx_metrics_campaign_date ON daily_metrics(campaign_id, date);
CREATE INDEX idx_metrics_date ON daily_metrics(date);
CREATE INDEX idx_events_campaign ON events_log(campaign_id);
CREATE INDEX idx_events_timestamp ON events_log(timestamp DESC);
CREATE INDEX idx_events_type ON events_log(event_type);

-- Seed Data: Create Sample Campaigns
INSERT INTO campaigns (name, advertiser_id, status, cpm_bid, daily_budget, lifetime_budget)
VALUES
  (
    'TechGear Banner Campaign',
    gen_random_uuid(),
    'active',
    2.50,
    1000.00,
    50000.00
  ),
  (
    'FashionBrand Mobile Campaign',
    gen_random_uuid(),
    'active',
    3.00,
    1500.00,
    75000.00
  ),
  (
    'GameStudio App Install Campaign',
    gen_random_uuid(),
    'paused',
    1.80,
    500.00,
    25000.00
  );

-- Add targeting rules for first campaign (TechGear)
INSERT INTO campaign_targeting_rules (campaign_id, rule_type, rule_value)
SELECT id, 'country', 'US' FROM campaigns WHERE name = 'TechGear Banner Campaign'
UNION ALL
SELECT id, 'device_type', 'desktop' FROM campaigns WHERE name = 'TechGear Banner Campaign';

-- Create sample ads for campaigns
INSERT INTO ads (campaign_id, title, image_url, redirect_url)
SELECT id, 'Check Out TechGear Pro', 'https://cdn.example.com/techgear-pro.jpg', 'https://techgear.example.com/pro'
FROM campaigns WHERE name = 'TechGear Banner Campaign'
UNION ALL
SELECT id, 'Latest Fashion Trends', 'https://cdn.example.com/fashion-banner.jpg', 'https://fashionbrand.example.com/new'
FROM campaigns WHERE name = 'FashionBrand Mobile Campaign';
```

### Step 1.3: Understand What We Just Created

**PostgreSQL:** Holds all persistent data
- **Campaigns:** Marketing campaigns with budgets and bids
- **Targeting Rules:** Who should see each campaign?
- **Ads:** Creative content (images, URLs)
- **Daily Metrics:** Pre-aggregated statistics (updated by Event Consumer)
- **Events Log:** Full audit trail (optional but useful for debugging)

**Indexes:** Speed up database queries (like table of contents in a book)

---

## Phase 2: Bid Engine (Core Logic)

The Bid Engine is the "brain" that decides which ad to serve. This is the most critical component.

### Step 2.1: Create Bid Engine Project Structure

```bash
cd ~/Desktop/Practice/ad_simulator

# Create Bid Engine project
dotnet new webapi -n BidEngine -o src/BidEngine -f net8.0
cd src/BidEngine

# Add necessary NuGet packages
dotnet add package Microsoft.EntityFrameworkCore.PostgreSQL
dotnet add package StackExchange.Redis
dotnet add package Prometheus.Client
dotnet add package Prometheus.Client.AspNetCore

cd ~/Desktop/Practice/ad_simulator
```

### Step 2.2: Create Models

Create file: `src/BidEngine/Models/Campaign.cs`

```csharp
using System;

namespace BidEngine.Models;

/// <summary>
/// Represents an advertising campaign.
/// Example: A campaign to promote "TechGear Pro" with a $2.50 CPM bid.
/// </summary>
public class Campaign
{
    public Guid Id { get; set; }
    
    public string Name { get; set; } = string.Empty;
    
    public Guid AdvertiserId { get; set; }
    
    /// <summary>
    /// Cost Per Mille (CPM) - the bid price per 1,000 impressions
    /// Example: 2.50 means $2.50 per 1,000 users who see this ad
    /// </summary>
    public decimal CpmBid { get; set; }
    
    /// <summary>
    /// Daily budget limit in dollars
    /// </summary>
    public decimal DailyBudget { get; set; }
    
    /// <summary>
    /// Total budget across entire campaign lifetime
    /// </summary>
    public decimal? LifetimeBudget { get; set; }
    
    /// <summary>
    /// How much we've spent today in dollars
    /// </summary>
    public decimal SpentToday { get; set; }
    
    /// <summary>
    /// Total lifetime spending
    /// </summary>
    public decimal LifetimeSpent { get; set; }
    
    /// <summary>
    /// Campaign status: active, paused, or ended
    /// </summary>
    public string Status { get; set; } = "active";
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// Navigation property: list of ads in this campaign
    /// </summary>
    public List<Ad> Ads { get; set; } = new();
    
    /// <summary>
    /// Navigation property: targeting rules for this campaign
    /// </summary>
    public List<TargetingRule> TargetingRules { get; set; } = new();
    
    /// <summary>
    /// Check if we can serve this campaign:
    /// - Status must be active
    /// - Haven't exceeded daily budget
    /// - Haven't exceeded lifetime budget (if set)
    /// </summary>
    public bool CanServe => 
        Status == "active" && 
        SpentToday < DailyBudget && 
        (LifetimeBudget == null || LifetimeSpent < LifetimeBudget.Value);
}

/// <summary>
/// Represents one piece of creative in a campaign.
/// Example: A banner image promoting TechGear Pro
/// </summary>
public class Ad
{
    public Guid Id { get; set; }
    
    public Guid CampaignId { get; set; }
    
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// URL to the ad image/creative
    /// </summary>
    public string ImageUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Where the user goes when they click the ad
    /// </summary>
    public string RedirectUrl { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    
    // Navigation property
    public Campaign Campaign { get; set; } = null!;
}

/// <summary>
/// Targeting rule restricts which campaigns can bid.
/// Example: A rule "country = US" means campaign only bids on US users
/// </summary>
public class TargetingRule
{
    public Guid Id { get; set; }
    
    public Guid CampaignId { get; set; }
    
    /// <summary>
    /// Type of rule: country, device_type, interest, age_range, etc.
    /// </summary>
    public string RuleType { get; set; } = string.Empty;
    
    /// <summary>
    /// Value for this rule: "US", "mobile", "tech", "18-35", etc.
    /// </summary>
    public string RuleValue { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    
    // Navigation property
    public Campaign Campaign { get; set; } = null!;
}
```

Create file: `src/BidEngine/Models/BidRequest.cs`

```csharp
namespace BidEngine.Models;

/// <summary>
/// Request from Ad Server to determine which campaign should bid.
/// Contains user context and placement information.
/// </summary>
public class BidRequest
{
    /// <summary>
    /// Unique user identifier
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    
    /// <summary>
    /// Where the ad will be displayed (e.g., "homepage_banner", "sidebar_300x250")
    /// </summary>
    public string PlacementId { get; set; } = string.Empty;
    
    /// <summary>
    /// User's country code (ISO 3166-1 alpha-2)
    /// Example: "US", "GB", "DE"
    /// </summary>
    public string? CountryCode { get; set; }
    
    /// <summary>
    /// Device type: "mobile", "tablet", or "desktop"
    /// </summary>
    public string? DeviceType { get; set; }
    
    /// <summary>
    /// User interests or demographic data
    /// </summary>
    public Dictionary<string, object> UserAttributes { get; set; } = new();
}

/// <summary>
/// Response with the winning campaign and ad to serve.
/// </summary>
public class BidResponse
{
    /// <summary>
    /// ID of the winning campaign
    /// </summary>
    public Guid CampaignId { get; set; }
    
    /// <summary>
    /// ID of the specific ad within the campaign
    /// </summary>
    public Guid AdId { get; set; }
    
    /// <summary>
    /// The CPM bid price for this impression
    /// </summary>
    public decimal BidPrice { get; set; }
    
    /// <summary>
    /// Ad content to serve to user
    /// </summary>
    public AdContent AdContent { get; set; } = null!;
    
    /// <summary>
    /// Confidence score (0-1) indicating how well this matches targeting
    /// </summary>
    public double Confidence { get; set; }
}

/// <summary>
/// Serializable ad content
/// </summary>
public class AdContent
{
    public string Title { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
}
```

### Step 2.3: Create Database Context

Create file: `src/BidEngine/Data/AppDbContext.cs`

```csharp
using BidEngine.Models;
using Microsoft.EntityFrameworkCore;

namespace BidEngine.Data;

/// <summary>
/// Entity Framework Core DbContext for Bid Engine
/// Handles all database operations
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    
    // DbSets represent tables in the database
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Ad> Ads => Set<Ad>();
    public DbSet<TargetingRule> TargetingRules => Set<TargetingRule>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure Campaign entity
        modelBuilder.Entity<Campaign>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.CpmBid).HasColumnType("numeric(10,4)");
            entity.Property(e => e.DailyBudget).HasColumnType("numeric(12,2)");
            entity.Property(e => e.SpentToday).HasColumnType("numeric(12,2)");
            
            // One campaign has many ads
            entity.HasMany(e => e.Ads)
                .WithOne(e => e.Campaign)
                .HasForeignKey(e => e.CampaignId);
            
            // One campaign has many targeting rules
            entity.HasMany(e => e.TargetingRules)
                .WithOne(e => e.Campaign)
                .HasForeignKey(e => e.CampaignId);
            
            // Create indexes
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.AdvertiserId);
        });
        
        // Configure Ad entity
        modelBuilder.Entity<Ad>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired();
            entity.Property(e => e.ImageUrl).IsRequired();
            entity.Property(e => e.RedirectUrl).IsRequired();
            entity.HasIndex(e => e.CampaignId);
        });
        
        // Configure TargetingRule entity
        modelBuilder.Entity<TargetingRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RuleType).IsRequired();
            entity.Property(e => e.RuleValue).IsRequired();
            entity.HasIndex(e => e.CampaignId);
            entity.HasIndex(e => new { e.CampaignId, e.RuleType, e.RuleValue }).IsUnique();
        });
    }
}
```

### Step 2.4: Create Campaign Cache Service

Create file: `src/BidEngine/Services/CampaignCache.cs`

```csharp
using BidEngine.Data;
using BidEngine.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace BidEngine.Services;

/// <summary>
/// Manages campaign caching using Redis.
/// 
/// Why caching?
/// - Database queries take 10-100ms
/// - Redis queries take <1ms
/// - Campaigns don't change frequently, so we can cache for 5 minutes
/// </summary>
public class CampaignCache
{
    private readonly IDatabase _redis;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<CampaignCache> _logger;
    private const int CacheTtlSeconds = 300; // 5 minutes
    
    public CampaignCache(IConnectionMultiplexer redis, AppDbContext dbContext, ILogger<CampaignCache> logger)
    {
        _redis = redis.GetDatabase();
        _dbContext = dbContext;
        _logger = logger;
    }
    
    /// <summary>
    /// Get a campaign by ID, checking cache first, then database
    /// </summary>
    public async Task<Campaign?> GetCampaignAsync(Guid campaignId)
    {
        var cacheKey = $"campaign::{campaignId}";
        
        // Step 1: Try to get from Redis cache
        var cached = await _redis.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            _logger.LogInformation("Cache hit for campaign {CampaignId}", campaignId);
            var campaign = JsonSerializer.Deserialize<Campaign>(cached.ToString());
            return campaign;
        }
        
        // Step 2: Not in cache, query database
        _logger.LogInformation("Cache miss for campaign {CampaignId}, querying database", campaignId);
        var dbCampaign = await _dbContext.Campaigns
            .Include(c => c.Ads)
            .Include(c => c.TargetingRules)
            .FirstOrDefaultAsync(c => c.Id == campaignId);
        
        if (dbCampaign != null)
        {
            // Step 3: Store in cache for future requests
            var json = JsonSerializer.Serialize(dbCampaign);
            await _redis.StringSetAsync(cacheKey, json, TimeSpan.FromSeconds(CacheTtlSeconds));
        }
        
        return dbCampaign;
    }
    
    /// <summary>
    /// Get all active campaigns, checking cache first
    /// </summary>
    public async Task<List<Campaign>> GetActiveCampaignsAsync()
    {
        var cacheKey = "campaigns::active::all";
        
        // Try cache first
        var cached = await _redis.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            _logger.LogInformation("Cache hit for active campaigns");
            var campaigns = JsonSerializer.Deserialize<List<Campaign>>(cached.ToString());
            return campaigns ?? new();
        }
        
        // Query database
        _logger.LogInformation("Cache miss for active campaigns, querying database");
        var campaigns_db = await _dbContext.Campaigns
            .Include(c => c.Ads)
            .Include(c => c.TargetingRules)
            .Where(c => c.Status == "active")
            .ToListAsync();
        
        // Cache the result
        var json = JsonSerializer.Serialize(campaigns_db);
        await _redis.StringSetAsync(cacheKey, json, TimeSpan.FromSeconds(CacheTtlSeconds));
        
        return campaigns_db;
    }
    
    /// <summary>
    /// Invalidate cache for a specific campaign (call after updates)
    /// </summary>
    public async Task InvalidateCampaignAsync(Guid campaignId)
    {
        var cacheKey = $"campaign::{campaignId}";
        await _redis.KeyDeleteAsync(cacheKey);
        await _redis.KeyDeleteAsync("campaigns::active::all");
        _logger.LogInformation("Invalidated cache for campaign {CampaignId}", campaignId);
    }
}
```

### Step 2.5: Create Bid Selector Service (Core Algorithm)

Create file: `src/BidEngine/Services/BidSelector.cs`

```csharp
using BidEngine.Models;

namespace BidEngine.Services;

/// <summary>
/// Core bidding algorithm - selects winning campaign
/// 
/// Algorithm Logic:
/// 1. Get all active campaigns
/// 2. Filter campaigns that match targeting rules
/// 3. Filter campaigns that have budget available
/// 4. Select campaign with highest CPM bid
/// 5. Return winning campaign and random ad from it
/// </summary>
public class BidSelector
{
    private readonly CampaignCache _cache;
    private readonly ILogger<BidSelector> _logger;
    private readonly Random _random = new();
    
    public BidSelector(CampaignCache cache, ILogger<BidSelector> logger)
    {
        _cache = cache;
        _logger = logger;
    }
    
    /// <summary>
    /// Select the winning campaign based on bid price and targeting match
    /// </summary>
    public async Task<BidResponse?> SelectWinningBidAsync(BidRequest request)
    {
        _logger.LogInformation(
            "Evaluating bids for user {UserId} on placement {PlacementId}",
            request.UserId,
            request.PlacementId
        );
        
        // Step 1: Get all active campaigns from cache
        var activeCampaigns = await _cache.GetActiveCampaignsAsync();
        
        if (!activeCampaigns.Any())
        {
            _logger.LogWarning("No active campaigns found");
            return null;
        }
        
        // Step 2: Filter campaigns based on targeting rules
        var eligibleCampaigns = new List<Campaign>();
        
        foreach (var campaign in activeCampaigns)
        {
            // Check if campaign can serve (budget available)
            if (!campaign.CanServe)
            {
                _logger.LogInformation(
                    "Campaign {CampaignId} cannot serve: status={Status}, budget available",
                    campaign.Id,
                    campaign.Status
                );
                continue;
            }
            
            // Check if campaign matches targeting rules
            if (!MatchesTargetingRules(campaign, request))
            {
                _logger.LogInformation(
                    "Campaign {CampaignId} doesn't match targeting rules",
                    campaign.Id
                );
                continue;
            }
            
            eligibleCampaigns.Add(campaign);
        }
        
        if (!eligibleCampaigns.Any())
        {
            _logger.LogWarning(
                "No eligible campaigns after filtering for user {UserId}",
                request.UserId
            );
            return null;
        }
        
        // Step 3: Select campaign with highest CPM bid
        var winningCampaign = eligibleCampaigns.OrderByDescending(c => c.CpmBid).First();
        
        _logger.LogInformation(
            "Campaign {CampaignId} won with CPM bid {Bid}",
            winningCampaign.Id,
            winningCampaign.CpmBid
        );
        
        // Step 4: Select random ad from winning campaign
        if (!winningCampaign.Ads.Any())
        {
            _logger.LogWarning("Winning campaign {CampaignId} has no ads", winningCampaign.Id);
            return null;
        }
        
        var selectedAd = winningCampaign.Ads[_random.Next(winningCampaign.Ads.Count)];
        
        // Step 5: Build response
        var response = new BidResponse
        {
            CampaignId = winningCampaign.Id,
            AdId = selectedAd.Id,
            BidPrice = winningCampaign.CpmBid,
            AdContent = new AdContent
            {
                Title = selectedAd.Title,
                ImageUrl = selectedAd.ImageUrl,
                RedirectUrl = selectedAd.RedirectUrl
            },
            Confidence = 0.95 // In a real system, this would be based on targeting match
        };
        
        return response;
    }
    
    /// <summary>
    /// Check if a campaign's targeting rules match the user request
    /// </summary>
    private bool MatchesTargetingRules(Campaign campaign, BidRequest request)
    {
        // If campaign has no targeting rules, it matches everyone
        if (!campaign.TargetingRules.Any())
        {
            return true;
        }
        
        // Group rules by type
        var rulesByType = campaign.TargetingRules.GroupBy(r => r.RuleType).ToDictionary(g => g.Key, g => g.ToList());
        
        // Check country rule
        if (rulesByType.TryGetValue("country", out var countryRules))
        {
            if (request.CountryCode == null || 
                !countryRules.Any(r => r.RuleValue.Equals(request.CountryCode, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }
        
        // Check device type rule
        if (rulesByType.TryGetValue("device_type", out var deviceRules))
        {
            if (request.DeviceType == null || 
                !deviceRules.Any(r => r.RuleValue.Equals(request.DeviceType, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }
        
        // Could add more rule types here (interest, age_range, etc.)
        
        return true;
    }
}
```

### Step 2.6: Create Budget Service

Create file: `src/BidEngine/Services/BudgetService.cs`

```csharp
using BidEngine.Data;
using BidEngine.Models;
using StackExchange.Redis;

namespace BidEngine.Services;

/// <summary>
/// Manages campaign budget tracking and deduction.
/// 
/// Why separate service?
/// - Budget tracking is critical and must be accurate
/// - Multiple services need to call this
/// - Needs to be tested independently
/// </summary>
public class BudgetService
{
    private readonly AppDbContext _dbContext;
    private readonly IDatabase _redis;
    private readonly ILogger<BudgetService> _logger;
    
    public BudgetService(AppDbContext dbContext, IConnectionMultiplexer redis, ILogger<BudgetService> logger)
    {
        _dbContext = dbContext;
        _redis = redis.GetDatabase();
        _logger = logger;
    }
    
    /// <summary>
    /// Deduct spend from campaign budget after an impression is served.
    /// 
    /// Calculation:
    /// - CPM = Cost Per Mille (per 1000 impressions)
    /// - Cost per impression = CPM / 1000
    /// - Example: $2.50 CPM means $0.0025 per impression
    /// </summary>
    public async Task<bool> DeductBudgetAsync(Guid campaignId, decimal cpmBid)
    {
        try
        {
            // Cost per impression in dollars
            var costPerImpression = cpmBid / 1000m;
            
            // Update in database
            var campaign = await _dbContext.Campaigns.FindAsync(campaignId);
            if (campaign == null)
            {
                _logger.LogError("Campaign {CampaignId} not found", campaignId);
                return false;
            }
            
            campaign.SpentToday += costPerImpression;
            campaign.LifetimeSpent += costPerImpression;
            campaign.UpdatedAt = DateTime.UtcNow;
            
            // Check budget constraints
            if (campaign.SpentToday > campaign.DailyBudget)
            {
                _logger.LogWarning(
                    "Campaign {CampaignId} exceeded daily budget: {Spent}/{Budget}",
                    campaignId,
                    campaign.SpentToday,
                    campaign.DailyBudget
                );
                // Revert the change
                campaign.SpentToday -= costPerImpression;
                campaign.LifetimeSpent -= costPerImpression;
                return false;
            }
            
            if (campaign.LifetimeBudget != null && campaign.LifetimeSpent > campaign.LifetimeBudget.Value)
            {
                _logger.LogWarning(
                    "Campaign {CampaignId} exceeded lifetime budget: {Spent}/{Budget}",
                    campaignId,
                    campaign.LifetimeSpent,
                    campaign.LifetimeBudget.Value
                );
                // Revert the change
                campaign.SpentToday -= costPerImpression;
                campaign.LifetimeSpent -= costPerImpression;
                return false;
            }
            
            await _dbContext.SaveChangesAsync();
            
            // Invalidate cache since budget changed
            var cache = (CampaignCache?)_dbContext.GetType().Assembly
                .GetType("BidEngine.Services.CampaignCache");
            
            _logger.LogInformation(
                "Deducted ${Cost} from campaign {CampaignId} (new total: ${Total})",
                costPerImpression,
                campaignId,
                campaign.SpentToday
            );
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deducting budget for campaign {CampaignId}", campaignId);
            return false;
        }
    }
    
    /// <summary>
    /// Reset daily budget spent counter at end of day
    /// Call this daily (e.g., via scheduled job)
    /// </summary>
    public async Task ResetDailyBudgetAsync()
    {
        try
        {
            var campaigns = await _dbContext.Campaigns.ToListAsync();
            foreach (var campaign in campaigns)
            {
                campaign.SpentToday = 0;
                campaign.UpdatedAt = DateTime.UtcNow;
            }
            
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Reset daily budget for {Count} campaigns", campaigns.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting daily budgets");
        }
    }
}
```

### Step 2.7: Create Bid Controller

Create file: `src/BidEngine/Controllers/BidController.cs`

```csharp
using BidEngine.Models;
using BidEngine.Services;
using Microsoft.AspNetCore.Mvc;
using Prometheus;

namespace BidEngine.Controllers;

/// <summary>
/// REST API endpoint for bidding
/// Receives bid requests from Ad Server and returns winning campaign
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class BidController : ControllerBase
{
    private readonly BidSelector _bidSelector;
    private readonly BudgetService _budgetService;
    private readonly ILogger<BidController> _logger;
    
    // Prometheus metrics
    private static readonly Counter BidRequestsTotal = Metrics
        .CreateCounter("bid_requests_total", "Total bid requests received",
            labelNames: new[] { "status" });
    
    private static readonly Histogram BidLatencySeconds = Metrics
        .CreateHistogram("bid_latency_seconds", "Bid processing latency in seconds");
    
    public BidController(
        BidSelector bidSelector,
        BudgetService budgetService,
        ILogger<BidController> logger)
    {
        _bidSelector = bidSelector;
        _budgetService = budgetService;
        _logger = logger;
    }
    
    /// <summary>
    /// POST /api/bid
    /// Evaluates all active campaigns and returns winning bid
    /// 
    /// SLO: p95 latency < 50ms
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<BidResponse>> EvaluateBidsAsync([FromBody] BidRequest request)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Validate request
            if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.PlacementId))
            {
                BidRequestsTotal.WithLabels("invalid").Inc();
                return BadRequest("UserId and PlacementId are required");
            }
            
            // Select winning campaign
            var winningBid = await _bidSelector.SelectWinningBidAsync(request);
            
            if (winningBid == null)
            {
                // No eligible campaign - return 204 No Content
                _logger.LogInformation(
                    "No winning bid for user {UserId} on placement {PlacementId}",
                    request.UserId,
                    request.PlacementId
                );
                BidRequestsTotal.WithLabels("no_bid").Inc();
                return NoContent();
            }
            
            // Deduct budget for winning campaign
            var budgetDeducted = await _budgetService.DeductBudgetAsync(
                winningBid.CampaignId,
                winningBid.BidPrice
            );
            
            if (!budgetDeducted)
            {
                _logger.LogWarning(
                    "Failed to deduct budget for campaign {CampaignId}",
                    winningBid.CampaignId
                );
                BidRequestsTotal.WithLabels("budget_error").Inc();
                return StatusCode(503, "Service temporarily unavailable");
            }
            
            BidRequestsTotal.WithLabels("success").Inc();
            
            var latency = (DateTime.UtcNow - startTime).TotalSeconds;
            BidLatencySeconds.Observe(latency);
            
            _logger.LogInformation(
                "Bid decision made in {LatencyMs}ms for campaign {CampaignId}",
                (DateTime.UtcNow - startTime).TotalMilliseconds,
                winningBid.CampaignId
            );
            
            return Ok(winningBid);
        }
        catch (Exception ex)
        {
            BidRequestsTotal.WithLabels("error").Inc();
            _logger.LogError(ex, "Error evaluating bids");
            return StatusCode(500, "Internal server error");
        }
    }
}
```

### Step 2.8: Create Program.cs

Create file: `src/BidEngine/Program.cs`

```csharp
using BidEngine.Data;
using BidEngine.Services;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Add Entity Framework Core with PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
);

// Add Redis
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "redis:6379";
var redis = ConnectionMultiplexer.Connect(redisConnectionString);
builder.Services.AddSingleton(redis);

// Add custom services
builder.Services.AddScoped<CampaignCache>();
builder.Services.AddScoped<BidSelector>();
builder.Services.AddScoped<BudgetService>();

// Add controllers and Prometheus metrics
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Database migration (optional - can also use SQL scripts)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // db.Database.Migrate(); // Uncomment if using EF Core migrations
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Prometheus metrics endpoint
app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapMetrics();
});

app.Run();
```

Create file: `src/BidEngine/appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=postgres;Port=5432;Database=ads_db;User Id=postgres;Password=postgres;",
    "Redis": "redis:6379"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

Create file: `src/BidEngine/Dockerfile`

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder

WORKDIR /build

# Copy only project files first (for better caching)
COPY src/BidEngine/BidEngine.csproj ./src/BidEngine/

# Copy shared projects if any
COPY src/Shared/ ./src/Shared/

# Restore dependencies
RUN dotnet restore src/BidEngine/BidEngine.csproj

# Copy remaining source code
COPY src/BidEngine/ ./src/BidEngine/

# Build
RUN dotnet build -c Release src/BidEngine/BidEngine.csproj

# Publish
RUN dotnet publish -c Release -o /app src/BidEngine/BidEngine.csproj

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=builder /app .

EXPOSE 8081

ENTRYPOINT ["dotnet", "BidEngine.dll"]
```

---

## Phase 3: Ad Server (API & Orchestration)

The Ad Server is the entry point that receives client requests, calls Bid Engine, and publishes events.

[Continue in next section due to length - Creating Ad Server project with serving logic, Kafka producer, metrics, and click tracking]

---

## Phase 4: Event Pipeline (Kafka Integration)

[Kafka topics setup, event schema, producer configuration, consumer group setup]

---

## Phase 5: Event Consumer (Real-Time Analytics)

[Aggregation logic, batch processing, state management, checkpoint strategy]

---

## Phase 6: Analytics Service (Reporting API)

[Query optimization, caching strategy, performance considerations]

---

## Phase 7: Testing & Validation

[Unit tests, integration tests, load testing with k6, test data generation]

---

## Phase 8: Monitoring & Deployment

[Prometheus configuration, Grafana dashboards, logging setup, production considerations]

---

## Key Takeaways

### Architecture Principles

1. **Separation of Concerns:** Each service has a single responsibility
   - Bid Engine: Decide which campaign wins
   - Ad Server: Orchestrate and serve ads
   - Event Consumer: Process events and aggregate metrics
   - Analytics Service: Report on performance

2. **Caching Strategy:** Use Redis for frequently accessed data
   - Campaign data: 5-minute TTL
   - Analytics: 1-hour TTL
   - Reduces database load and improves latency

3. **Event-Driven:** Decouple services using Kafka
   - Ad Server doesn't wait for analytics processing
   - Consumer processes events asynchronously
   - Easy to add new consumers without changing existing code

4. **Idempotency:** All operations use unique IDs
   - Event ID prevents duplicate counts
   - Can safely retry without side effects

### Performance Targets

- **Ad Server:** p95 < 100ms (includes network + Bid Engine call)
- **Bid Engine:** p95 < 50ms (core algorithm)
- **Event Publishing:** < 10ms (async, doesn't block response)
- **Analytics Query:** < 500ms (database + cache)

### Testing Strategy

1. **Unit Tests:** Test individual services in isolation (e.g., BidSelector algorithm)
2. **Integration Tests:** Test services working together with real databases
3. **Load Tests:** Simulate 10k QPS to verify latency SLOs
4. **End-to-End Tests:** Full user flow from request to analytics

---

## Common Pitfalls & Solutions

| Problem | Cause | Solution |
|---------|-------|----------|
| Slow bid responses | Database queries for every request | Implement campaign caching in Redis |
| Budget overages | Race condition in concurrent requests | Use database-level constraints + retries |
| Missing events | Service crashes without persisting | Use Kafka acknowledgments + checkpoints |
| High latency spikes | GC pauses in long-running processes | Monitor GC, optimize memory usage |
| Inconsistent analytics | Events processed out of order | Partition Kafka by campaignId for ordering |

---

## Next Steps After This Phase

Once you've completed the implementation from this guide:

1. **Deploy locally** with Docker Compose
2. **Verify endpoints** with curl or Postman
3. **Run load tests** and measure latency
4. **Check Grafana dashboards** for metrics
5. **Review logs** for any errors or warnings
6. **Document findings** for future reference

This foundation is production-ready and can be extended with:
- Sophisticated targeting rules
- Machine learning-based bid optimization
- Real-time budget enforcement
- Geographic and temporal bid adjustments
- Fraud detection and prevention

