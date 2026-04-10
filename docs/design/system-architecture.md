# PHÂN TÍCH THIÊT KÊ HÊ THÔNG - VANAN ECOSYSTEM

## **THÔNG TIN CHUNG**

**Tên Hê Thông:** VanAn Ecosystem  
**Phiên Ban Hiên Tai:** MVP 1.0  
**Ngày Câp Nhât:** 10/04/2026  
**Kiên Trúc:** Clean Architecture + DDD  
**Trang Thái:** Build thành công, Domain purity maintained

---

## **1. TÔNG QUAN KIÊN TRÚC**

### **1.1 Clean Architecture Layers**

```
1_Shared/          (Domain Layer)
  - Domain.cs      (Value Objects, Entities)
  - Services/      (Domain Services)
  - Extensions/    (Shared Extensions)

3_CoreHub/         (Application + Infrastructure)
  - Domain/        (Application Entities)
  - Services/      (Application Services)
  - Infrastructure/
    - VanAnDbContext.cs
    - ValueConverters/
    - Configurations/

2_Gateway/         (API Gateway)
  - Controllers/
  - Middleware/

5_WebApps/         (Presentation Layer)
  - ShopERP/       (Admin Dashboard)
  - KhachLink/     (Customer Portal)

6_Tests/           (Testing Layer)
```

### **1.2 DDD Principles Applied**

- **Value Objects:** LeadId, CustomerId, OrderId, etc.
- **Entities:** Lead, Customer, Order, Product, etc.
- **Aggregates:** Order (with OrderItems)
- **Domain Services:** LeadService, CustomerService
- **Infrastructure:** EF Core, ValueConverters

---

## **2. DOMAIN LAYER ANALYSIS**

### **2.1 Value Objects (1_Shared/Domain.cs)**

```csharp
// Core Value Objects
public record ProductId(Guid Value);
public record IngredientId(Guid Value);
public record RecipeId(Guid Value);
public record InventoryId(Guid Value);
public record OrderId(Guid Value);
public record OrderStatusId(string Value);
public record ShopId(Guid Value);
public record CustomerId(Guid Value);
public record OrderItemId(Guid Value);
public record LeadId(Guid Value);
```

**Design Principles:**
- Immutable (record type)
- Identity-less (no primary keys)
- Single source of truth
- 2-way ValueConverter mapping

### **2.2 Core Entities**

```csharp
// Business Entities
public class Order : BaseEntity
{
    public OrderId OrderId { get; set; }
    public Guid? CustomerId { get; set; }
    public OrderStatusId Status { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TotalVatAmount { get; set; }
    public ICollection<OrderItem> Items { get; set; }
}

public class Customer : BaseEntity
{
    public CustomerId CustomerId { get; set; }
    public string FullName { get; set; }
    public string PhoneNumber { get; set; }
    public string CustomerTier { get; set; }
    public int LoyaltyPoints { get; set; }
}

public class Lead : BaseEntity
{
    public LeadId LeadId { get; set; }
    public LeadSource Source { get; set; }
    public LeadStatus Status { get; set; }
    public Guid? ConvertedCustomerId { get; set; }
}
```

---

## **3. INFRASTRUCTURE LAYER ANALYSIS**

### **3.1 EF Core Configuration**

**VanAnDbContext.cs:**
- **ConfigureConventions:** Global ValueObject mapping
- **OnModelCreating:** Auto-discover configurations
- **Multi-tenancy:** Tenant isolation filters

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    // Global ValueObject converters
    configurationBuilder.Properties<LeadId>()
        .HaveConversion<LeadIdConverter>();
    configurationBuilder.Properties<CustomerId>()
        .HaveConversion<CustomerIdConverter>();
    // ... other ValueObjects
}

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Auto-discover all configurations
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(VanAnDbContext).Assembly);
}
```

### **3.2 Value Converters**

**Architecture:**
- All converters in separate files (`Infrastructure/ValueConverters/`)
- 2-way conversion (VO <-> Primitive)
- Consistent naming pattern

```csharp
public class LeadIdConverter : ValueConverter<LeadId, Guid>
{
    public LeadIdConverter() : base(
        id => id.Value,
        value => new LeadId(value))
    {
    }
}
```

---

## **4. MULTI-TENANCY DESIGN**

### **4.1 Tenant Isolation Strategy**

**Database Level:**
- `TenantId` column in all entities
- Global query filters for tenant separation
- Automatic tenant injection

```csharp
public class BaseEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

**Application Level:**
- `ITenantProvider` service
- Automatic tenant context
- Tenant-aware repositories

### **4.2 Tenant Provider**

```csharp
public interface ITenantProvider
{
    Guid TenantId { get; }
    string TenantName { get; }
}

// Implementation in tests
public class TestTenantProvider : ITenantProvider
{
    public Guid TenantId => Guid.Parse("12345678-1234-1234-1234-123456789abc");
    public string TenantName => "Test Shop";
}
```

---

## **5. API GATEWAY DESIGN**

### **5.1 Gateway Architecture**

**2_Gateway/ Structure:**
- **Controllers:** API endpoints
- **Middleware:** Localization, Logging
- **Hubs:** SignalR for real-time

```csharp
// Order Management
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OrderDto>> CreateOrder(CreateOrderRequest request)
    {
        // Order creation logic
    }
}

// Real-time Kitchen Display
[Hub]
public class KitchenHub : Hub
{
    public async Task JoinKitchen(string shopId)
    {
        // Join kitchen group
    }
}
```

### **5.2 Middleware Pipeline**

```csharp
// Localization Middleware
public class LocalizationMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Set culture based on request
    }
}

// Voice Command Logger
public class VoiceCommandLogger
{
    public void LogCommand(string command, string userId)
    {
        // Log voice commands
    }
}
```

---

## **6. DATABASE DESIGN**

### **6.1 Schema Overview**

**Core Tables:**
- **Orders:** Order information
- **OrderItems:** Order line items
- **Customers:** Customer data
- **Products:** Menu items
- **Ingredients:** Raw materials
- **Inventory:** Stock levels
- **Leads:** Potential customers

**Relationships:**
- Orders -> OrderItems (1:N)
- Orders -> Customers (Optional)
- Products -> Ingredients (N:M via Recipes)
- Inventory -> Ingredients (1:N)

### **6.2 Multi-tenancy Schema**

```sql
-- All tables have TenantId
CREATE TABLE Orders (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL,
    OrderId UNIQUEIDENTIFIER NOT NULL,
    CustomerId UNIQUEIDENTIFIER NULL,
    Status NVARCHAR(50) NOT NULL,
    SubTotal DECIMAL(18,2) NOT NULL,
    CreatedAt DATETIME2 NOT NULL,
    UpdatedAt DATETIME2 NOT NULL
);

-- Tenant isolation index
CREATE INDEX IX_Orders_TenantId ON Orders(TenantId);
```

---

## **7. INTEGRATION ARCHITECTURE**

### **7.1 Facebook Leads Integration**

**Flow:**
1. Facebook Lead Ads -> Webhook
2. Gateway -> Lead Service
3. Create Lead in Domain
4. Optional: Convert to Customer

```csharp
public class FacebookLeadController : ControllerBase
{
    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveLead(FacebookLeadPayload payload)
    {
        // Process Facebook lead
        var lead = _leadService.CreateFromFacebook(payload);
        return Ok();
    }
}
```

### **7.2 Omnichannel Sync**

**Strategy:**
- Real-time sync via SignalR
- Event-driven architecture
- Conflict resolution

```csharp
public class OmnichannelService
{
    public async Task SyncOrder(Order order)
    {
        // Sync to all channels
        await _hubContext.Clients.All.SendAsync("OrderUpdated", order);
    }
}
```

---

## **8. SECURITY ARCHITECTURE**

### **8.1 Authentication & Authorization**

**JWT Token Structure:**
```json
{
  "sub": "user-id",
  "tenantId": "shop-id",
  "roles": ["staff", "admin"],
  "exp": 1640995200
}
```

**Authorization Rules:**
- **Admin:** Full access
- **Staff:** Limited to assigned shop
- **Customer:** Own data only

### **8.2 Data Protection**

**Encryption:**
- Connection strings encrypted
- Sensitive data at rest encrypted
- HTTPS enforced

**Tenant Isolation:**
- Row-level security
- Query filters
- Audit logging

---

## **9. PERFORMANCE DESIGN**

### **9.1 Caching Strategy**

**Redis Cache:**
- Menu items cache
- Customer preferences
- Session data

**In-Memory Cache:**
- Configuration data
- Lookup tables

### **9.2 Database Optimization**

**Indexing Strategy:**
- Primary keys (Id)
- Tenant isolation (TenantId)
- Foreign keys
- Query-specific indexes

**Query Optimization:**
- Efficient EF Core queries
- Avoid N+1 problems
- Pagination support

---

## **10. TESTING ARCHITECTURE**

### **10.1 Test Structure**

```
6_Tests/
  VanAn.Unit.Tests/      (Unit tests)
  VanAn.Integration.Tests/ (Integration tests)
  VanAn.OrderFlow.Tests/  (API tests)
  VanAn.Core.Tests/      (Core logic tests)
```

### **10.2 Test Infrastructure**

**Test DbContext:**
- In-memory SQLite for unit tests
- Isolated test databases
- Mock services

**Test Data:**
- Seed data factories
- Test tenant provider
- Mock HTTP clients

---

## **11. DEPLOYMENT ARCHITECTURE**

### **11.1 Container Strategy**

**Docker Compose:**
- **Gateway:** API gateway
- **CoreHub:** Business logic
- **Database:** PostgreSQL
- **Cache:** Redis
- **Frontend:** Web apps

### **11.2 Environment Configuration**

**Development:**
- Local development setup
- Hot reload enabled
- Debug logging

**Production:**
- Container orchestration
- Load balancing
- Monitoring

---

## **12. CURRENT IMPLEMENTATION STATUS**

### **12.1 Completed Components**

- [x] **Domain Layer:** Clean, pure domain with Value Objects
- [x] **Infrastructure Layer:** EF Core with Value Converters
- [x] **Multi-tenancy:** Tenant isolation implemented
- [x] **API Gateway:** RESTful endpoints
- [x] **Database Schema:** Proper relationships and indexes
- [x] **Testing Infrastructure:** Unit and integration tests
- [x] **Build System:** 0 errors, clean architecture

### **12.2 Architecture Validation**

**Build Status:** SUCCESS (0 errors, 6 warnings)  
**Domain Purity:** MAINTAINED (no EF in Domain)  
**Value Objects:** UNIFIED (single source of truth)  
**Converters:** 2-WAY IMPLEMENTED  
**Multi-tenancy:** ISOLATED  

### **12.3 Next Architecture Improvements**

- [ ] **Event Sourcing:** For audit trails
- [ ] **CQRS:** For complex queries
- [ ] **Microservices:** Split into smaller services
- [ ] **Advanced Caching:** Distributed caching
- [ ] **Performance Monitoring:** APM integration

---

## **13. ARCHITECTURAL DECISIONS LOG**

### **13.1 Key Decisions**

1. **Clean Architecture:** Chosen for maintainability
2. **DDD Value Objects:** For type safety and business logic
3. **Multi-tenancy:** Row-level security approach
4. **EF Core 8.0:** Latest features and performance
5. **Value Converters:** 2-way conversion for persistence

### **13.2 Trade-offs**

**Simplicity vs Flexibility:**
- Chose simpler approach for MVP
- Room for future enhancements

**Performance vs Complexity:**
- Optimized for common use cases
- Advanced features can be added later

---

## **PHÊ DUYÊT THIÊT KÊ**

**Ngày:** 10/04/2026  
**Kiên Trúc Sû:** [Tên]  
**Phê Duyêt Bôi:** [Tên]  
**Trang Thái:** Hoàn thành MVP

---

*Luu y: Tài liêu này se câp nhât khi có thay doi trong kiên trúc hoac khi thêm tính nang mói.*
