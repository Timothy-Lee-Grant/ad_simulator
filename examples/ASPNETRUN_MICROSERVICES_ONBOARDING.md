# ASP.NET Run Microservices - Complete Onboarding Guide
## For Entry-Level Engineers Learning Microservices Architecture

**Repository:** https://github.com/aspnetrun/run-aspnetcore-microservices  
**Stars:** 3,138 | **Forks:** 1,700+ | **Language:** C# (73.2%), HTML (22.4%), Dockerfile (3.3%)  
**Last Updated:** November 2025  
**Author:** Mehmet Ozkaya  

**This document is an in-depth onboarding guide to help you understand the complete architecture, organization, and implementation patterns used in production microservices.**

---

## Table of Contents

1. [Overview & Vision](#1-overview--vision)
2. [Core Concepts Explained](#2-core-concepts-explained)
3. [Architecture Diagram](#3-architecture-diagram)
4. [Project Organization](#4-project-organization)
5. [Microservices Deep Dive](#5-microservices-deep-dive)
6. [Communication Patterns](#6-communication-patterns)
7. [Technology Stack & Why](#7-technology-stack--why)
8. [Design Patterns & Best Practices](#8-design-patterns--best-practices)
9. [Development Environment Setup](#9-development-environment-setup)
10. [How to Run the Project](#10-how-to-run-the-project)
11. [Learning Path](#11-learning-path)
12. [Common Issues & Solutions](#12-common-issues--solutions)

---

## 1. Overview & Vision

### What is This Project?

The **ASP.NET Run Microservices** project is a **production-grade example** of a real-world **e-commerce microservices system** built with modern .NET technologies. It demonstrates how large applications should be decomposed into independent, scalable, and maintainable services.

### Real-World Problem It Solves

Imagine you're building an Amazon-like shopping platform:

- **100+ developers** need to work independently
- **Different teams** manage different features (catalog, shopping cart, payments, orders)
- **One service failure** shouldn't crash the entire application
- **Features need independent deployment** without waiting for other teams
- **Traffic spikes** on different services need independent scaling

**Traditional monolithic approach:** All code in one massive application = merge conflicts, deployment locks, system-wide outages, slow scaling.

**This microservices approach:** Independent services = teams move fast, services scale independently, failures are isolated.

### Key Business Benefits

| Benefit | How It Works |
|---------|------------|
| **Speed to Market** | Teams deploy independently without waiting for others |
| **Scalability** | Scale only the services under load (e.g., Basket during sales) |
| **Reliability** | Catalog service down ≠ ordering works |
| **Team Autonomy** | Each team owns their service end-to-end |
| **Technology Flexibility** | Mix .NET, Java, Go in the same system |

---

## 2. Core Concepts Explained

### 2.1 What is a Microservice?

A **microservice** is:

```
┌─────────────────────────────────────┐
│  Small, Independent Application     │
├─────────────────────────────────────┤
│ • Single responsibility              │
│ • Own database                       │
│ • HTTP/gRPC communication           │
│ • Independently deployable          │
│ • Independently scalable            │
└─────────────────────────────────────┘
```

**Example: Catalog Microservice**
- Responsibility: Manage products
- Database: PostgreSQL (own schema)
- APIs: REST endpoints for product queries
- Deploy: Independent of other services
- Scale: Add replicas when product queries surge

### 2.2 Key Microservices Concepts

#### CQRS (Command Query Responsibility Segregation)

**Problem:** 
- Reading data (queries) and writing data (commands) have different performance requirements
- Users browse 1000x more than they buy
- Single database can't optimize for both

**Solution:** Split into two models:

```
Write Side (Command)              Read Side (Query)
─────────────────────────────────────────────────
Save to Database          →       Read Cache
Apply Business Logic      →       Pre-computed Views
                          →       Optimized for speed
```

**Real Example in Project:**
```csharp
// COMMAND: Create Order (write)
CreateOrderCommand → ValidateInventory → SaveToDatabase

// QUERY: Get Order Details (read)
GetOrderQuery → ReadFromCache/ReadModel → Instant response
```

#### DDD (Domain-Driven Design)

**Concept:** Code structure should match your business domain

**Anti-pattern:**
```
Folder Structure:
├── Controllers/
├── Services/
├── Models/
├── Repositories/
```
❌ Doesn't tell you what the service does

**DDD Pattern:**
```
Folder Structure:
├── Features/
│   ├── Catalog/
│   │   ├── GetProducts/
│   │   │   ├── GetProductsQuery.cs
│   │   │   ├── GetProductsHandler.cs
│   │   │   └── GetProductsResponse.cs
│   │   └── CreateProduct/
│   │       ├── CreateProductCommand.cs
│   │       └── CreateProductHandler.cs
```
✅ You immediately know: this service manages Products with features

#### Vertical Slice Architecture

**Traditional Layers (Horizontal):**
```
┌────────────────────────────┐
│      Controller Layer      │ ← HTTP Request comes here
├────────────────────────────┤
│      Service Layer         │ ← Business logic
├────────────────────────────┤
│      Repository Layer      │ ← Database access
├────────────────────────────┤
│      Database Layer        │ ← SQL queries
└────────────────────────────┘
```

**Problem:** Every feature must travel through all 4 layers = complex, slow to change

**Vertical Slice (This Project):**
```
Feature: GetProducts
├── GetProductsRequest (input)
├── GetProductsHandler (logic)
├── GetProductsResponse (output)
└── All in one .cs file or folder
```

**Benefit:** Feature is isolated, easy to understand, fast to develop

### 2.3 Communication Patterns

#### Synchronous Communication (Immediate Response)

```
Request/Response Pattern:
Basket Service              Discount Service
    │                            │
    ├─────── gRPC Call ────────→ │
    │  "Get discount for item 5" │
    │                            │
    │ ← Apply 10% discount ─────┤
    │                            │
```

**Use Case:** Basket needs discount NOW to calculate total price  
**Technology:** gRPC (fast, binary protocol)  
**Drawback:** If Discount service is slow, entire request is slow

#### Asynchronous Communication (Fire and Forget)

```
Publish/Subscribe Pattern:
Basket Service          Message Queue          Ordering Service
    │                      │                        │
    ├──→ PublishEvent ────→│                        │
    │    "OrderCreated"    │                        │
    │                      ├───→ Subscribe ────────→│
    │                      │    "Process Order"     │
```

**Use Case:** After checkout, notify ordering system about new order  
**Technology:** RabbitMQ (message broker)  
**Benefit:** Services are decoupled, can fail independently

---

## 3. Architecture Diagram

### High-Level System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Client Application                      │
│                    (Web Browser/Mobile)                     │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
      ┌──────────────────────────────┐
      │   YARP API Gateway           │
      │  (Reverse Proxy/Router)      │
      │  • Routes requests           │
      │  • Rate limiting             │
      │  • Load balancing            │
      └──────────────────────────────┘
         │          │        │        │
         ▼          ▼        ▼        ▼
    ┌────────┐ ┌────────┐ ┌────────┐ ┌──────────┐
    │Catalog │ │Basket  │ │Discount│ │ Ordering │
    │Service │ │Service │ │Service │ │ Service  │
    └───┬────┘ └───┬────┘ └───┬────┘ └────┬─────┘
        │          │          │           │
        └──────────┼──────────┼───────────┘
                   │          │
         ┌─────────▼──────────▼────────┐
         │    RabbitMQ (Message Bus)    │
         │  (Async Communication)       │
         └──────────────────────────────┘
         
        ┌──────────────────────────────────────┐
        │          Data Layer                   │
        ├──────────────────────────────────────┤
        │ PostgreSQL │ Redis  │ SQLite │ SqlSrv │
        │  (Catalog) │(Cache) │(Disct) │ (Order)│
        └──────────────────────────────────────┘
```

### Request Flow Example: Customer Checking Out

```
1. User clicks "Checkout" in Web UI
   └─→ POST /api/basket/checkout to YARP Gateway

2. YARP routes to Basket Service
   └─→ Basket Service validates items exist
       └─→ gRPC Call to Discount Service
           └─→ Get discounts for each item
           └─→ Return discounted prices
       └─→ Create BasketCheckoutEvent
       └─→ Publish to RabbitMQ

3. RabbitMQ distributes event
   └─→ Ordering Service subscribes
       └─→ Consumes BasketCheckoutEvent
       └─→ Creates Order in SQL Server
       └─→ Returns Order ID

4. Response flows back through YARP to UI
   └─→ "Order created: #12345"
```

---

## 4. Project Organization

### Directory Structure Overview

```
run-aspnetcore-microservices/
├── src/                                  # All source code
│   ├── ApiGateways/                     # API Gateway
│   │   └── YarpApiGateway/              # YARP reverse proxy
│   │       ├── Program.cs
│   │       ├── appsettings.json
│   │       ├── Routes/
│   │       │   ├── CatalogRoutes.cs
│   │       │   ├── BasketRoutes.cs
│   │       │   ├── OrderingRoutes.cs
│   │       │   └── DiscountRoutes.cs
│   │       └── Dockerfile
│   │
│   ├── Services/                        # 4 Microservices
│   │   ├── Catalog/                     # Service 1
│   │   ├── Basket/                      # Service 2
│   │   ├── Discount/                    # Service 3
│   │   └── Ordering/                    # Service 4
│   │
│   ├── BuildingBlocks/                  # Shared Libraries
│   │   ├── BuildingBlocks/              # Common utilities
│   │   │   ├── CQRS/                    # CQRS framework
│   │   │   ├── Behaviors/               # Pipeline behaviors
│   │   │   └── Exceptions/              # Exception handling
│   │   │
│   │   └── BuildingBlocks.Messaging/    # Message contracts
│   │       └── Events/                  # Shared event definitions
│   │
│   ├── WebApps/                         # Web User Interface
│   │   └── ShoppingWebApp/
│   │       ├── Pages/
│   │       ├── Services/
│   │       └── Views/
│   │
│   ├── docker-compose.yml               # Orchestration
│   ├── docker-compose.override.yml      # Local overrides
│   └── eshop-microservices.sln          # Solution file
│
└── README.md                            # Main documentation
```

### Key Folders Explained

#### 1. **ApiGateways/YarpApiGateway**

**Purpose:** Single entry point for all client requests

**Key Concept:** Instead of clients calling services directly:
```
❌ Bad:
Client → Catalog Service
Client → Basket Service  (hard to manage, no security layer)
Client → Ordering Service

✅ Good:
Client → API Gateway → Routes to appropriate services
```

**What it does:**
- Routes requests to correct microservice
- Enforces rate limiting (prevent abuse)
- Adds authentication/authorization
- Monitors traffic

#### 2. **Services/** (The 4 Microservices)

Each service is a **complete application**:

**Catalog Service**
- Lists products
- Manages product inventory
- Database: PostgreSQL
- Architecture: Vertical Slice

**Basket Service**
- Shopping cart functionality
- Caches data in Redis
- Communicates with Discount via gRPC
- Publishes checkout events

**Discount Service**
- Calculates discounts
- Lightweight (Grpc only)
- Database: SQLite
- Responds to gRPC queries

**Ordering Service**
- Creates and manages orders
- Listens for basket checkouts via RabbitMQ
- Implements DDD with aggregates
- Database: SQL Server

#### 3. **BuildingBlocks/**

**Shared code** used by all microservices:

```
BuildingBlocks.Messaging/
└── Events/
    ├── BasketCheckoutEvent.cs        # Shared event definition
    ├── IntegrationEvent.cs           # Base event class
    └── ...
```

**BuildingBlocks/**
```
├── CQRS/
│   ├── ICommand.cs                   # Interface for commands
│   ├── IQuery.cs                     # Interface for queries
│   └── ICommandHandler.cs            # Processes commands
├── Behaviors/
│   ├── ValidationBehavior.cs         # Auto-validates requests
│   └── LoggingBehavior.cs            # Logs all requests
└── Exceptions/
    └── GlobalExceptionHandler.cs     # Catches all errors
```

**Why shared?** Different services but same patterns = consistent code

#### 4. **WebApps/ShoppingWebApp**

**Purpose:** Frontend for users

**Tech Stack:**
- ASP.NET Core (MVC)
- Bootstrap 4 (styling)
- Razor templates (HTML generation)

**How it works:**
```
1. User navigates to https://localhost:6065
2. Web app displays product list
3. User adds items to basket
4. User clicks checkout
5. Web app calls YARP Gateway API
6. YARP routes to services
7. Services process, return response
8. Web app displays confirmation
```

---

## 5. Microservices Deep Dive

### 5.1 Catalog Microservice

#### What It Does

Manages the product catalog - the source of truth for:
- Product information (name, description, price)
- Product categories
- Inventory levels

#### Architecture: Vertical Slice with CQRS

```
Features/
├── CreateProduct/
│   ├── CreateProductCommand.cs        # Input model
│   ├── CreateProductCommandHandler.cs # Business logic
│   └── CreateProductEndpoint.cs       # API endpoint
├── GetProducts/
│   ├── GetProductsQuery.cs            # Input model
│   ├── GetProductsQueryHandler.cs     # Business logic
│   └── GetProductsEndpoint.cs         # API endpoint
└── UpdateProduct/
    ├── UpdateProductCommand.cs
    ├── UpdateProductCommandHandler.cs
    └── UpdateProductEndpoint.cs
```

**Why this structure?**
- **CreateProduct/** folder = one feature
- All code for creating a product in one place
- Easy to navigate: "To add product creation, look in CreateProduct/"
- Easy to test: one feature, one test file

#### Technology Stack

| Technology | Purpose | Why? |
|-----------|---------|-----|
| **ASP.NET Core Minimal APIs** | Create REST endpoints | Modern, lightweight |
| **Marten** | Document DB on PostgreSQL | Fast queries, transactions |
| **Carter** | Endpoint definitions | Cleaner than traditional routing |
| **MediatR** | CQRS pattern | Separates commands/queries |
| **FluentValidation** | Validate input | Reusable validation rules |

#### Request Flow: Get All Products

```
1. Client: GET /api/catalog/products?pageNumber=1

2. Carter Endpoint captures request
   └─→ new GetProductsQuery { PageNumber = 1 }

3. MediatR Dispatcher sends query to handler
   └─→ GetProductsQueryHandler

4. Handler executes business logic
   └─→ var products = await _repository.GetProducts(1)

5. Return response
   └─→ List of products (JSON)

6. Client receives response
   └─→ [{ id: 1, name: "Laptop", price: 999 }, ...]
```

#### Vertical Slice Example: Get Product by ID

```csharp
// GetProductByIdEndpoint.cs - Complete feature in one file
namespace Catalog.Features.GetProductById;

// 1. Input model
public record GetProductByIdQuery(Guid Id) : IQuery<GetProductByIdResponse>;

// 2. Output model
public record GetProductByIdResponse(
    Guid Id,
    string Name,
    string Description,
    decimal Price);

// 3. Business logic handler
public class GetProductByIdQueryHandler : 
    IQueryHandler<GetProductByIdQuery, GetProductByIdResponse>
{
    private readonly IDocumentSession _session;
    
    public async Task<GetProductByIdResponse> Handle(
        GetProductByIdQuery query, CancellationToken ct)
    {
        var product = await _session.LoadAsync<Product>(query.Id, cancellation: ct);
        
        return new GetProductByIdResponse(
            product.Id,
            product.Name,
            product.Description,
            product.Price);
    }
}

// 4. API Endpoint
public class GetProductByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/catalog/products/{id}", GetProductById)
            .WithName("GetProductById")
            .WithOpenApi();
    }
    
    private async Task<IResult> GetProductById(Guid id, ISender mediator)
    {
        var query = new GetProductByIdQuery(id);
        var result = await mediator.Send(query);
        return Results.Ok(result);
    }
}
```

**Analysis:**
- Everything you need for "Get Product" in one file
- Easy to understand: Input → Handler → Output
- Easy to test: Mock session, test handler
- Easy to modify: Change just this file

#### CQRS in Action: Validation Pipeline

```
Request Flow:
────────────────────────────────────────────
1. Request: CreateProductCommand
   └─→ { Name: "", Price: -50 }  ← Invalid!

2. MediatR Validation Behavior
   └─→ Check: Name not empty? ✓
   └─→ Check: Price > 0? ✗
   └─→ Return: ValidationException

3. Global Exception Handler catches it
   └─→ Return 400 Bad Request
   └─→ Error message: "Price must be > 0"

4. Client receives:
   └─→ HTTP 400
   └─→ Error details
```

---

### 5.2 Basket Microservice

#### What It Does

Manages shopping carts - customers can:
- Add items to basket
- Remove items
- View basket contents
- Checkout (triggers order creation)

#### Key Features & Patterns

**1. Redis for Caching (High Speed)**

```
Traditional approach:
Request → Database → Response (slow)

Basket approach:
Request → Redis Cache → Response (fast!)
         (if miss) ↓
         Database → Update Cache
```

**Why Redis?**
- In-memory = nanosecond access
- Perfect for shopping carts (frequently accessed)
- Can expire automatically (cart abandoned after 24h)

**2. Cache-Aside Pattern**

```csharp
public async Task<CustomerBasket> GetBasketAsync(string customerId)
{
    // 1. Try Redis first
    var basket = await _cache.GetBasketAsync(customerId);
    if (basket != null) return basket;  // Hit! Return immediately
    
    // 2. Redis miss, query database
    basket = await _repository.GetBasketAsync(customerId);
    
    // 3. Update Redis for next time
    await _cache.UpdateBasketAsync(customerId, basket);
    
    return basket;
}
```

**Flow:**
```
First Request: Cache Miss
Request → Redis (empty) → Database → Redis (store) → Return

Second Request: Cache Hit
Request → Redis (found!) → Return (much faster)

Item expires after 24 hours:
Request → Redis (expired) → Database → Update Cache
```

**3. gRPC for Service-to-Service Communication**

```
Basket calls Discount Service:
─────────────────────────────────

Basket Service                  Discount Service
    │                               │
    ├──→ gRPC Call (binary) ───────→│
    │    "Get discount for item 5"  │
    │                               │
    │ ← gRPC Response (binary) ─────┤
    │    "10% discount applied"     │
    │                               │
```

**Why gRPC?**
- Binary protocol (smaller, faster than JSON)
- Strongly typed with protobuf
- Low latency (milliseconds)

**Proto definition (interface):**
```protobuf
service Discount {
  rpc GetDiscount (GetDiscountRequest) returns (GetDiscountResponse);
}

message GetDiscountRequest {
  int32 product_id = 1;
}

message GetDiscountResponse {
  double discount_percentage = 1;
}
```

**4. Publishing Events to Message Queue**

```csharp
public async Task<CartCheckoutResponse> CheckoutAsync(
    BasketCheckoutDto basketCheckout)
{
    // 1. Validate basket
    var basket = await GetBasketAsync(basketCheckout.UserName);
    if (basket == null) throw new Exception("Basket not found");
    
    // 2. Create event
    var @event = new BasketCheckoutEvent
    {
        UserName = basketCheckout.UserName,
        TotalPrice = basket.TotalPrice,
        CreatedTime = DateTime.Now
    };
    
    // 3. Publish to RabbitMQ (fire and forget)
    await _publishEndpoint.Publish(@event);
    
    // 4. Delete basket (checkout complete)
    await _cache.DeleteBasketAsync(basketCheckout.UserName);
    
    return new CartCheckoutResponse { Success = true };
}
```

**Event published to message queue:**
```json
{
  "userName": "john@example.com",
  "totalPrice": 499.99,
  "createdTime": "2025-11-30T15:30:00Z"
}
```

**Who listens?** → Ordering Service (subscribes to BasketCheckoutEvent)

---

### 5.3 Discount Microservice

#### What It Does

Provides discount information - **very simple on purpose**.

```csharp
// Discount Service API
public decimal GetDiscount(int productId)
{
    // Database lookup
    var discount = _db.Discounts.FirstOrDefault(d => d.ProductId == productId);
    return discount?.DiscountPercentage ?? 0;
}
```

#### Why So Simple?

Demonstrates a key principle: **"Don't over-engineer"**

This service:
- Has ONE responsibility: return discounts
- Uses SQLite (simple database)
- Exposes gRPC only (called by Basket)
- No complex business logic

#### Running as gRPC Service

**Program.cs:**
```csharp
var builder = WebApplication.CreateBuilder(args);

// Add gRPC service
builder.Services.AddGrpc();

var app = builder.Build();

// Map gRPC endpoint
app.MapGrpcService<DiscountService>();

app.Run();
```

**Grpc Service Implementation:**
```csharp
public class DiscountService : Discount.DiscountBase
{
    private readonly DiscountContext _context;
    
    public override async Task<GetDiscountResponse> GetDiscount(
        GetDiscountRequest request,
        ServerCallContext context)
    {
        var coupon = await _context.Coupons
            .FirstOrDefaultAsync(c => c.ProductId == request.ProductId);
            
        return new GetDiscountResponse
        {
            DiscountAmount = coupon?.Amount ?? 0
        };
    }
}
```

**Key Lesson:** Not every service needs to be complex. Simple services are maintainable.

---

### 5.4 Ordering Microservice

#### What It Does

Manages orders - the most complex service because:
- Orders aggregate data from multiple services
- Must handle distributed transactions
- Implements business rules

#### DDD Implementation: Order Aggregate

```csharp
public class Order : Entity  // Aggregate Root
{
    public string UserName { get; private set; }
    public decimal TotalPrice { get; private set; }
    public List<OrderItem> Items { get; private set; } = new();
    
    // Business rule: Create order with validation
    public static Order Create(string userName, List<OrderItem> items)
    {
        if (string.IsNullOrEmpty(userName))
            throw new Exception("UserName required");
        
        if (!items.Any())
            throw new Exception("Order must have items");
        
        var order = new Order
        {
            UserName = userName,
            Items = items,
            TotalPrice = items.Sum(i => i.Price)
        };
        
        return order;
    }
    
    public void AddItem(OrderItem item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));
        
        Items.Add(item);
        TotalPrice += item.Price;
    }
}
```

**Why this approach?**
- Order is the **aggregate root** (responsible for consistency)
- All business rules in Order class
- Can't create invalid Order (constructor ensures it)
- Easy to test: new Order() - does it validate?

#### CQRS Pattern: Create Order

```csharp
// Step 1: Define Command
public record CreateOrderCommand(
    string UserName,
    List<OrderItemDto> Items,
    decimal TotalPrice) : ICommand<CreateOrderResponse>;

public record CreateOrderResponse(int Id);

// Step 2: Handle Command
public class CreateOrderCommandHandler : 
    ICommandHandler<CreateOrderCommand, CreateOrderResponse>
{
    private readonly IRepository<Order> _repository;
    
    public async Task<CreateOrderResponse> Handle(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Create order aggregate
        var items = command.Items
            .Select(i => OrderItem.Create(i.ProductId, i.Price, i.Quantity))
            .ToList();
        
        var order = Order.Create(command.UserName, items);
        
        // 2. Save to database
        await _repository.AddAsync(order);
        await _repository.SaveChangesAsync();
        
        // 3. Return result
        return new CreateOrderResponse(order.Id);
    }
}
```

#### Consuming Events: Listen for BasketCheckout

```csharp
public class BasketCheckoutEventConsumer : 
    IConsumer<BasketCheckoutEvent>
{
    private readonly IMediator _mediator;
    
    public async Task Consume(ConsumeContext<BasketCheckoutEvent> context)
    {
        var @event = context.Message;
        
        // 1. Convert basket checkout to order
        var items = @event.Items
            .Select(i => new OrderItemDto(i.ProductId, i.Price, i.Quantity))
            .ToList();
        
        // 2. Create order using CQRS
        var command = new CreateOrderCommand(
            @event.UserName,
            items,
            @event.TotalPrice);
        
        // 3. Execute command
        var result = await _mediator.Send(command);
        
        // 4. Log success
        _logger.LogInformation(
            "Order created: {OrderId} for {UserName}",
            result.Id,
            @event.UserName);
    }
}
```

**Flow visualization:**
```
Basket Service publishes event:
"UserJohn finished shopping with 3 items"
           ↓
RabbitMQ receives and queues
           ↓
Ordering Service subscribes and consumes
           ↓
Creates Order in database
           ↓
Stores: Order ID 12345, UserJohn, Total $499
```

---

## 6. Communication Patterns

### 6.1 Synchronous: gRPC (Basket ↔ Discount)

#### When to Use Synchronous

```
✅ Use Sync when:
   - You need IMMEDIATE response
   - Caller must know if it succeeded
   - Latency is acceptable (<100ms)
   - Service is always available

Basket → Discount:
"Get discount for product 5"
(User waiting, show final price NOW)
```

#### gRPC Implementation

**Proto definition (shared between services):**
```protobuf
// discount.proto
service DiscountService {
  rpc GetDiscount (GetDiscountRequest) returns (GetDiscountResponse);
}

message GetDiscountRequest {
  int32 product_id = 1;
}

message GetDiscountResponse {
  double discount_percentage = 1;
}
```

**Discount Service (Server):**
```csharp
public class DiscountService : Discount.DiscountBase
{
    public override async Task<GetDiscountResponse> GetDiscount(
        GetDiscountRequest request,
        ServerCallContext context)
    {
        var discount = await _db.GetDiscountAsync(request.ProductId);
        return new GetDiscountResponse { DiscountPercentage = discount };
    }
}
```

**Basket Service (Client):**
```csharp
public class DiscountGrpcService
{
    private readonly Discount.DiscountClient _discountClient;
    
    public async Task<decimal> GetDiscount(int productId)
    {
        var request = new GetDiscountRequest { ProductId = productId };
        var response = await _discountClient.GetDiscountAsync(request);
        return response.DiscountPercentage;
    }
}
```

**Request/Response Flow:**
```
Basket makes gRPC call:
GET_DISCOUNT(ProductId=5)
         ↓
Sent over HTTP/2 (binary protocol)
         ↓
Discount service processes
         ↓
Returns binary response
         ↓
Basket receives: 10% discount
         ↓
Applies to cart total
```

---

### 6.2 Asynchronous: RabbitMQ (Basket → Ordering)

#### When to Use Asynchronous

```
✅ Use Async when:
   - Caller doesn't need immediate response
   - Services should be loosely coupled
   - Can tolerate delayed processing
   - Want to scale independently

Basket → Ordering:
"User finished shopping" 
(Fire event, don't wait)
Ordering service processes later
```

#### RabbitMQ Architecture

```
┌──────────────────┐
│   RabbitMQ       │
│  Message Broker  │
│                  │
│ Topic: Orders    │
│ ├─ Queue 1       │
│ └─ Queue 2       │
└──────────────────┘
       ↑      ↓
   Publish Subscribe
       │      │
   Basket  Ordering
```

#### Event Definition

```csharp
// BuildingBlocks.Messaging/Events/BasketCheckoutEvent.cs
public class BasketCheckoutEvent : IntegrationEvent
{
    public string UserName { get; set; }
    public decimal TotalPrice { get; set; }
    public List<BasketItemDto> Items { get; set; }
}
```

**Inheritance:** `BasketCheckoutEvent` extends `IntegrationEvent`
```csharp
public abstract class IntegrationEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
}
```

#### Publishing Event (Basket Service)

```csharp
public class CheckoutBasketCommandHandler : 
    ICommandHandler<CheckoutBasketCommand, CheckoutBasketResponse>
{
    private readonly IPublishEndpoint _publishEndpoint;
    
    public async Task<CheckoutBasketResponse> Handle(
        CheckoutBasketCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Process checkout
        var basket = await _repository.GetBasketAsync(command.UserName);
        
        // 2. Create event
        var @event = new BasketCheckoutEvent
        {
            UserName = command.UserName,
            TotalPrice = basket.TotalPrice,
            Items = basket.Items.ToList()
        };
        
        // 3. Publish to RabbitMQ (fire and forget)
        await _publishEndpoint.Publish(@event);
        
        // 4. Clear basket
        await _repository.DeleteBasketAsync(command.UserName);
        
        return new CheckoutBasketResponse { Success = true };
    }
}
```

#### Consuming Event (Ordering Service)

```csharp
public class BasketCheckoutEventConsumer : 
    IConsumer<BasketCheckoutEvent>
{
    private readonly IMediator _mediator;
    
    // MassTransit automatically calls this when event arrives
    public async Task Consume(ConsumeContext<BasketCheckoutEvent> context)
    {
        var @event = context.Message;
        
        // Create order from event
        var command = new CreateOrderCommand(
            @event.UserName,
            @event.Items,
            @event.TotalPrice);
        
        await _mediator.Send(command);
    }
}
```

#### Event Flow

```
Timeline:
─────────────────────────────────────────────
T=0ms:  User clicks checkout in Web UI
        └─→ Basket receives request

T=10ms: Basket validates cart
        └─→ Publishes BasketCheckoutEvent to RabbitMQ
        └─→ Returns response to user ("Order submitted")

T=50ms: User sees "Order being processed..."
        RabbitMQ routes event to Ordering Service

T=100ms: Ordering Service receives event
         └─→ Creates Order in database
         └─→ Order ID: 12345

T=2000ms: User refreshes page
          └─→ Sees "Order confirmed: #12345"
```

**Key Benefits:**
- Basket returns immediately (user happy)
- If Ordering is slow, Basket isn't affected
- If Ordering crashes, RabbitMQ retries
- Services are independent

---

## 7. Technology Stack & Why

### API Layer

| Tech | Purpose | Why? |
|------|---------|-----|
| **ASP.NET Core** | Build web APIs | Modern, fast, unified platform |
| **Carter** | Define endpoints | Cleaner than traditional routing |
| **Minimal APIs** | Lightweight endpoints | No controller boilerplate |

### Data Access

| Tech | Service | Why? |
|------|---------|-----|
| **Entity Framework Core** | ORM for SQL databases | Strongly typed, migrations, LINQ |
| **Marten** | PostgreSQL document DB | JSON documents, transactions |
| **SQLite** | Discount service | Embedded, no setup needed |
| **SQL Server** | Ordering service | Enterprise, complex queries |

### Communication

| Tech | Pattern | Why? |
|------|---------|-----|
| **gRPC** | Sync service calls | Binary, low latency, typed |
| **RabbitMQ** | Async events | Reliable, scalable, proven |
| **MassTransit** | RabbitMQ abstraction | Simplifies publishing/consuming |

### Caching

| Tech | Where | Why? |
|------|-------|-----|
| **Redis** | Basket service | In-memory, microsecond access |
| **Cache-Aside** | Pattern | Simpler than write-through |

### Business Logic

| Tech | Purpose | Why? |
|------|---------|-----|
| **MediatR** | CQRS pattern | Separates commands/queries |
| **FluentValidation** | Input validation | Reusable, fluent API |
| **Domain-Driven Design** | Structure code | Matches business domain |
| **Vertical Slices** | Organize features | Feature-focused, not layer-focused |

### Containerization

| Tech | Purpose | Why? |
|------|---------|-----|
| **Docker** | Container images | Consistent across environments |
| **Docker Compose** | Local orchestration | All services in one command |

---

## 8. Design Patterns & Best Practices

### 8.1 Command Query Responsibility Segregation (CQRS)

**Pattern: Separate Read and Write Models**

```csharp
// ❌ Typical approach (mixed concerns)
public class ProductService
{
    public List<Product> GetProducts() { }        // Query
    public Product CreateProduct(Product p) { }   // Command
    public void DeleteProduct(int id) { }         // Command
}

// ✅ CQRS approach (separated)
// WRITE SIDE
public class CreateProductCommand : ICommand { }
public class CreateProductHandler : ICommandHandler<CreateProductCommand> { }

// READ SIDE
public class GetProductsQuery : IQuery<List<Product>> { }
public class GetProductsHandler : IQueryHandler<GetProductsQuery> { }
```

**Benefits:**
- Different optimization for reads vs writes
- Easier to scale: add read replicas
- Clear intent: is this reading or writing?
- Better testability

### 8.2 Domain-Driven Design (DDD)

**Principle: Organize code by business domain, not technical layers**

```
Business Domain:        Folder Structure:
─────────────────       ────────────────
Managing Orders     ────→ Features/
  - Create Order          ├── CreateOrder/
  - View Orders           ├── UpdateOrder/
  - Cancel Order          └── CancelOrder/

Managing Products   ────→ Services/Catalog/
  - Browse catalog        ├── GetProducts/
  - Search products       ├── CreateProduct/
  - Update prices         └── UpdatePrice/
```

**Aggregate Example: Order Aggregate**

```csharp
public class Order : AggregateRoot  // Consistency boundary
{
    private List<OrderItem> _items = new();  // Private collection
    
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();
    
    // Business rule: Orders require items
    public static Order Create(string userName, List<OrderItem> items)
    {
        if (!items.Any()) throw new Exception("Order needs items");
        return new Order { UserName = userName, Items = items };
    }
    
    // Only through aggregate can modify
    public void AddItem(OrderItem item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        _items.Add(item);
    }
    
    // Can't directly modify Items
    // _items.Add(new OrderItem()); // ❌ Compiler prevents this
}
```

**Benefits:**
- Business rules enforced in code
- Can't accidentally create invalid orders
- Encapsulation: internal data protected
- Easy to understand: code matches business

### 8.3 Vertical Slice Architecture

**Principle: Organize by feature, not by layer**

```
❌ Horizontal Slices (by layer)
────────────────────────────────
Controllers/
├── ProductController.cs
├── OrderController.cs
└── BasketController.cs

Services/
├── ProductService.cs
├── OrderService.cs
└── BasketService.cs

Data/
├── ProductRepository.cs
├── OrderRepository.cs
└── BasketRepository.cs

Problems:
- To add a feature, modify 3 folders
- Controllers might have 20 responsibilities
- Services become god objects
- Hard to delete feature (scattered everywhere)

✅ Vertical Slices (by feature)
──────────────────────────────────
Features/
├── GetProducts/
│   ├── GetProductsQuery.cs
│   ├── GetProductsHandler.cs
│   └── GetProductsEndpoint.cs
├── CreateProduct/
│   ├── CreateProductCommand.cs
│   ├── CreateProductHandler.cs
│   └── CreateProductEndpoint.cs
└── DeleteProduct/
    ├── DeleteProductCommand.cs
    ├── DeleteProductHandler.cs
    └── DeleteProductEndpoint.cs

Benefits:
- One feature per folder
- To add feature: one folder
- To remove feature: delete one folder
- Complete understanding: read one folder
```

### 8.4 Pipeline Behaviors (Cross-Cutting Concerns)

**Problem:** Every command/query needs validation, logging, etc.

```
Command
   ├─→ Logging
   ├─→ Validation
   ├─→ Business Logic
   ├─→ Caching
   └─→ Error Handling
```

**Solution: Pipeline Behaviors**

```csharp
// Validation behavior applied to ALL commands
public class ValidationBehavior<TRequest, TResponse> : 
    IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IValidator<TRequest> _validator;
    
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Validate before handler
        var validationResult = await _validator.ValidateAsync(request);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);
        
        // Execute handler
        return await next();
    }
}

// Register in Program.cs
services.AddTransient(
    typeof(IPipelineBehavior<,>),
    typeof(ValidationBehavior<,>));
```

**Result:** Every command is validated automatically, no code duplication.

### 8.5 Global Exception Handling

**Problem:** Exceptions thrown in different places = inconsistent error responses

```csharp
// Exception thrown in Catalog service
throw new Exception("Product not found");

// Client receives: 500 Internal Server Error (wrong!)
// Should be: 404 Not Found
```

**Solution: Global Exception Middleware**

```csharp
app.UseGlobalExceptionHandler();  // Middleware

public class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var response = new ErrorResponse();
        
        switch (exception)
        {
            case NotFoundException:
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                response.Message = "Resource not found";
                break;
                
            case ValidationException:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                response.Message = "Validation failed";
                break;
                
            default:
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                response.Message = "Internal server error";
                break;
        }
        
        await context.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }
}
```

**Result:** All services return consistent error responses

---

## 9. Development Environment Setup

### Prerequisites

Your machine needs these installed:

```
✅ Required:
├── .NET 8 SDK
│   └─ Contains: C# compiler, runtime, libraries
│
├── Docker Desktop
│   └─ Contains: Docker Engine, Docker Compose
│
├── Visual Studio 2022 (Community is free!)
│   └─ IDE for C# development
│   └─ Built-in Docker support
│
├── SQL Server (Express Edition is free)
│   └─ Database for Ordering service
│   └─ Can also use Docker image
│
└── Optional but helpful:
    ├── Postman (test APIs)
    ├── Azure Data Studio (database GUI)
    └── RabbitMQ Management UI (built-in)
```

### Installation Steps

#### 1. Install .NET 8 SDK

**macOS/Linux:**
```bash
# Using brew
brew install dotnet

# Verify
dotnet --version  # Should show 8.x.x
```

**Windows:**
- Download from https://dotnet.microsoft.com/download/dotnet/8.0
- Run installer
- Verify: `dotnet --version`

#### 2. Install Docker Desktop

**All platforms:**
- Download from https://www.docker.com/products/docker-desktop
- Run installer
- Verify: `docker --version`
- **Important:** Allocate 4GB RAM in Docker settings

#### 3. Install Visual Studio 2022

**Windows:**
- Download from https://visualstudio.microsoft.com/downloads/
- Run installer
- Select workloads:
  - ✅ ASP.NET and web development
  - ✅ Docker support

**macOS:**
- Download Visual Studio for Mac from https://visualstudio.microsoft.com/vs/mac/
- Or use VS Code with C# extension

---

## 10. How to Run the Project

### Step 1: Clone Repository

```bash
git clone https://github.com/aspnetrun/run-aspnetcore-microservices.git
cd run-aspnetcore-microservices/src
```

### Step 2: Check Docker Resources

**Before starting, ensure Docker has enough resources:**

```bash
# Docker must have:
# - Memory: 4GB minimum
# - CPU: 2 cores minimum

# On Windows/Mac: Docker Desktop → Preferences → Resources
# On Linux: Docker uses system resources (no limit)
```

### Step 3: Start All Services with Docker Compose

**Option A: Command Line**

```bash
cd src/

# Start all services
docker-compose -f docker-compose.yml -f docker-compose.override.yml up -d

# What this starts:
# - PostgreSQL (Catalog database)
# - SQL Server (Ordering database)
# - SQLite (Discount database) 
# - Redis (Basket cache)
# - RabbitMQ (Message broker)
# - All 4 microservices
# - API Gateway
# - Web UI
```

**Option B: Visual Studio**

```
1. Open src/eshop-microservices.sln
2. Right-click docker-compose
3. Select "Set as Startup Project"
4. Press F5 (Debug) or Ctrl+F5 (Run)
```

### Step 4: Wait for Services to Start

```bash
# Check logs
docker-compose logs -f

# Services to wait for:
# ✓ Catalog service (http://localhost:8080)
# ✓ Basket service (http://localhost:8081)
# ✓ Discount service (gRPC on :8082)
# ✓ Ordering service (http://localhost:8083)
# ✓ API Gateway (http://localhost:6000)
# ✓ RabbitMQ (http://localhost:15672)
```

### Step 5: Access the Application

```
🏠 Shopping Web UI
   └─ http://localhost:6065

🔧 RabbitMQ Management UI
   └─ http://localhost:15672
   └─ Username: guest, Password: guest

📊 Swagger/OpenAPI (API documentation)
   └─ Each service has Swagger:
   └─ http://localhost:8080/swagger  (Catalog)
   └─ http://localhost:8081/swagger  (Basket)
   └─ http://localhost:8083/swagger  (Ordering)

🌐 API Gateway (all routes)
   └─ http://localhost:6000/swagger
```

### Step 6: Test the Application

**In Web UI:**
```
1. Open http://localhost:6065
2. See products (fetched from Catalog service)
3. Add items to basket
4. Click "Checkout"
5. Order created (Ordering service)
```

**In Postman (API testing):**
```
Import the Postman collection:
src/EShopMicroservices.postman_collection.json

Or manually test:

GET http://localhost:6000/api/catalog/products
Response:
[
  {
    "id": "550e8400",
    "name": "Laptop",
    "price": 999.99
  },
  ...
]

POST http://localhost:6000/api/basket/additem
Body:
{
  "userName": "john@example.com",
  "productId": "550e8400",
  "quantity": 1,
  "price": 999.99
}
```

### Step 7: Stop Services

```bash
# Stop all containers
docker-compose down

# Stop and remove volumes (clears databases)
docker-compose down -v

# View running containers
docker-compose ps
```

---

## 11. Learning Path

### Week 1: Fundamentals (20 hours)

**Days 1-2: Microservices Concepts**
- [ ] Read: What is a microservice?
- [ ] Watch: Introduction to microservices video
- [ ] Understand: Monolith vs Microservices trade-offs
- [ ] Code: Run project locally (Section 10)

**Days 3-4: CQRS Pattern**
- [ ] Understand: Command vs Query
- [ ] Study: MediatR library
- [ ] Read: `src/Services/Catalog/Features/` (vertical slices)
- [ ] Task: Modify GetProductsQuery to add filtering

**Days 5-7: Communication Patterns**
- [ ] Understand: Sync vs Async
- [ ] Study: gRPC basics
- [ ] Study: RabbitMQ Publish/Subscribe
- [ ] Trace: Checkout flow (Basket → Ordering)

### Week 2: Deep Dive (30 hours)

**Days 8-10: Catalog Service**
- [ ] Read entire `Services/Catalog/` folder
- [ ] Understand: Marten (document DB)
- [ ] Task: Add a new feature (e.g., GetProductsByCategory)
- [ ] Test: Write unit tests for handler

**Days 11-12: Basket Service**
- [ ] Read entire `Services/Basket/` folder
- [ ] Understand: Redis cache, cache-aside pattern
- [ ] Task: Add RemoveItem endpoint
- [ ] Trace: gRPC call to Discount service

**Days 13-14: Ordering Service**
- [ ] Read entire `Services/Ordering/` folder
- [ ] Understand: DDD, Aggregates
- [ ] Task: Add OrderStatus update flow
- [ ] Understand: Event consumer pattern

### Week 3: Advanced Topics (25 hours)

**Days 15-16: Data Consistency**
- [ ] Understand: CAP theorem
- [ ] Study: Eventual consistency
- [ ] Problem: What if event publishing fails?
- [ ] Solution: Outbox pattern (research)

**Days 17-18: Error Handling**
- [ ] Global exception handler
- [ ] Retry policies
- [ ] Circuit breaker pattern (Polly library)

**Days 19-21: Deployment & Production**
- [ ] Build Docker images
- [ ] Kubernetes basics
- [ ] Monitoring & logging
- [ ] Health checks

### Projects to Build

**Project 1: Review Service**
```
Build a new microservice that:
- Has own database
- Exposes REST API
- Communicates with Ordering (get orders)
- Listens for OrderCreated events
- Allows users to review products
```

**Project 2: Recommendation Service**
```
Build a service that:
- Suggests products based on history
- Uses gRPC to call Catalog
- Caches recommendations in Redis
```

**Project 3: Payment Service** (Complex)
```
Build a service that:
- Integrates with payment gateway
- Implements idempotency (same request = same result)
- Handles refunds
- Uses saga pattern for distributed transactions
```

---

## 12. Common Issues & Solutions

### Issue 1: Docker Services Won't Start

**Symptom:**
```
docker-compose up -d
ERROR: Container exited with code 1
```

**Solutions:**

1. Check Docker has enough memory
   ```bash
   docker stats
   # Memory usage should be <4GB
   ```

2. Check logs for specific error
   ```bash
   docker-compose logs [service-name]
   # Example: docker-compose logs catalog
   ```

3. Clean everything and restart
   ```bash
   docker-compose down -v
   docker-compose up -d --build
   ```

### Issue 2: Port Already in Use

**Symptom:**
```
ERROR: Address already in use: 0.0.0.0:6000
```

**Solution:**
```bash
# Find what's using port 6000
lsof -i :6000  # macOS/Linux
netstat -ano | findstr :6000  # Windows

# Kill the process
kill -9 <PID>  # macOS/Linux
taskkill /PID <PID> /F  # Windows

# Or change port in docker-compose.override.yml
```

### Issue 3: Database Connection Errors

**Symptom:**
```
Cannot connect to SqlServer
Connection timeout: 30 seconds
```

**Reasons & Solutions:**

1. SQL Server container not started
   ```bash
   docker-compose ps
   # If not running: docker-compose up -d
   ```

2. Wait for SQL Server to be ready
   ```bash
   # SQL Server takes 30-60 seconds to start
   # Don't start other services until it's ready
   docker-compose logs sql.data
   ```

3. Check connection string in appsettings.json
   ```json
   {
     "ConnectionStrings": {
       "OrderingConnectionString": "Server=sql.data;Database=OrderDb;..."
     }
   }
   ```

### Issue 4: RabbitMQ Not Receiving Messages

**Symptom:**
```
Basket publishes event
Ordering doesn't receive it
Order never created
```

**Debugging Steps:**

1. Check RabbitMQ dashboard
   ```
   http://localhost:15672 (guest/guest)
   → Queues tab
   → See if BasketCheckoutEvent queue exists
   → Check queue messages
   ```

2. Check Ordering service is consuming
   ```bash
   docker-compose logs ordering
   # Should see: "Subscribing to BasketCheckoutEvent"
   ```

3. Verify MassTransit configuration
   ```csharp
   // In Ordering service Program.cs
   services.AddMassTransit(x =>
   {
       x.AddConsumer<BasketCheckoutEventConsumer>();
       x.UsingRabbitMq((context, cfg) =>
       {
           cfg.Host(new Uri("rabbitmq://rabbitmq:5672"));
           cfg.ConfigureEndpoints(context);
       });
   });
   ```

### Issue 5: Cache Not Working (Redis Issues)

**Symptom:**
```
Basket takes 2+ seconds to respond
Redis not speeding up requests
```

**Check Redis:**

```bash
# Connect to Redis
docker exec -it redis redis-cli

# Test Redis
> SET mykey "Hello"
> GET mykey
"Hello"

# Check memory usage
> INFO memory
```

**Common issues:**

1. Redis disconnected
   ```csharp
   // Check connection in Basket service
   var result = await _cache.GetBasketAsync(customerId);
   if (result == null) // Cache miss = disconnected?
   ```

2. Cache key wrong
   ```csharp
   // Key format consistent?
   var key = $"basket_{customerId}";
   ```

3. Expiration too short
   ```csharp
   // How long does cache live?
   await _cache.UpdateBasketAsync(customerId, basket, 
       expiration: TimeSpan.FromHours(24));
   ```

### Issue 6: gRPC Service Not Callable

**Symptom:**
```
Basket can't call Discount gRPC service
System.Net.Http.HttpRequestException
```

**Solutions:**

1. Check Discount service is running
   ```bash
   docker-compose ps
   # discount service should be running
   ```

2. Check gRPC channel configuration
   ```csharp
   var channel = GrpcChannel.ForAddress("http://discount:80");
   var client = new DiscountService.DiscountServiceClient(channel);
   ```

3. Verify service is actually gRPC
   ```bash
   curl -i http://localhost:8082/  # Should show grpc headers
   ```

4. Check firewall/networking
   ```bash
   docker-compose exec basket ping discount
   # Should respond
   ```

---

## Appendix: Glossary

### A

**Aggregate Root**
- The main entity in a domain model that enforces consistency
- Example: Order is aggregate root, OrderItems are children

**Asynchronous**
- Non-blocking communication, doesn't wait for response
- Used for publishing events

### C

**CQRS (Command Query Responsibility Segregation)**
- Separate read operations (queries) from write operations (commands)
- Different optimization strategies

**Carter**
- Lightweight library for defining ASP.NET Core endpoints
- Alternative to traditional controller routing

### D

**Domain-Driven Design (DDD)**
- Design code structure to match business domains
- Aggregate roots, entities, value objects

**Docker Compose**
- Tool to define and run multi-container Docker applications
- Single docker-compose.yml file deploys entire system

### G

**gRPC**
- High-performance RPC framework using HTTP/2 and Protocol Buffers
- Faster than REST/JSON

### M

**MediatR**
- Library implementing mediator pattern
- Handles CQRS commands and queries

**Marten**
- .NET library for using PostgreSQL as document database
- Combines SQL and NoSQL benefits

**MassTransit**
- Abstracts messaging layer (RabbitMQ, ServiceBus, etc.)
- Simplifies publish/subscribe

### R

**Redis**
- In-memory data store
- Used for caching

**RabbitMQ**
- Message broker for async communication
- Publish/Subscribe pattern

### S

**Synchronous**
- Blocking communication, waits for response
- Used for immediate queries

### V

**Vertical Slice Architecture**
- Organize code by feature, not by layer
- One feature = one folder = complete understanding

---

## Conclusion: Key Takeaways

1. **Microservices = Independence**
   - Each service owns its data
   - Teams develop independently
   - Services scale independently

2. **Sync vs Async**
   - Use gRPC for immediate responses
   - Use RabbitMQ for fire-and-forget events
   - Know the trade-offs

3. **Patterns Matter**
   - CQRS separates reads and writes
   - DDD matches business domain
   - Vertical slices focus on features

4. **Data Consistency is Hard**
   - Distributed transactions = complex
   - Eventual consistency = simpler
   - Events help coordinate services

5. **Testing is Crucial**
   - Unit tests for business logic
   - Integration tests for service communication
   - Contract tests for APIs

---

## Additional Learning Resources

### Books
- *Building Microservices* by Sam Newman
- *Domain-Driven Design* by Eric Evans
- *Release It!* by Michael Nygard (resilience patterns)

### Online Courses
- https://www.udemy.com/course/microservices-architecture-and-implementation-on-dotnet/ (Author's course)
- Microsoft Learn: ASP.NET Core fundamentals

### Documentation
- https://docs.microsoft.com/aspnet/core
- https://grpc.io/docs/
- https://www.rabbitmq.com/tutorials
- https://masstransit-project.com/

### Code Repositories
- This repo: https://github.com/aspnetrun/run-aspnetcore-microservices
- eShopOnContainers: https://github.com/dotnet-architecture/eShopOnContainers

---

**Document Created:** November 30, 2025  
**For:** Entry-Level Software Engineers  
**Difficulty Level:** Beginner to Intermediate  
**Estimated Reading Time:** 2-3 hours  
**Estimated Lab Time (Running project):** 1-2 hours  
**Total Learning Path:** 4-5 weeks to master

**Happy Learning! 🚀**
