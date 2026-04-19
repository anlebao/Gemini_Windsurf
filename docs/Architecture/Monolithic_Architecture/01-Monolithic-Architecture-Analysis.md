# VanAn Ecosystem - Monolithic Architecture Analysis

**Ngày:** 14 tháng 4, 2026  
**Version:** Current Architecture  
**Status:** Production (Monolithic with Shared Libraries)

---

## **1. T NG QUAN KI N TR C**

### **1.1 Architecture Type**
**Monolithic Architecture with Shared Libraries**

Không phân tách thành microservices, mà là single deployment unit with shared dependencies.

### **1.2 Project Structure**
```
VanAn Ecosystem/
|
|-- 1_Shared/                    # Domain Models & Shared Logic
|-- 2_Gateway/                   # API Layer + Business Logic
|-- 3_CoreHub/                   # Business Services Layer
|-- 4_MobileApps/                # MAUI Applications (Not Active)
|-- 5_WebApps/
|   |-- KhachLink/               # Customer Portal (Port 5002)
|   |-- ShopERP/                 # Shop Management (Port 5003)
|   `-- KhachLink/               # Customer Portal (Port 5002)
|-- 6_Tests/                     # Testing Framework
`-- docs/                        # Documentation
```

---

## **2. DETAILED ARCHITECTURE ANALYSIS**

### **2.1 Component Dependencies**

#### **A. Shared Layer (1_Shared)**
```xml
<!-- VanAn.Shared.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  
  <ItemGroup>
    <!-- Domain Models -->
    <Compile Include="Domain\*.cs" />
    <!-- Value Objects -->
    <Compile Include="Models\*.cs" />
    <!-- Extensions -->
    <Compile Include="Extensions\*.cs" />
  </ItemGroup>
</Project>
```

**Contents:**
- **Domain Models:** Product, Order, Customer, Ingredient, Recipe
- **Value Objects:** OrderId, CustomerId, ProductId, etc.
- **Shared Services:** IVietQrService, etc.
- **Extensions:** ThemeTypeExtensions, etc.

#### **B. CoreHub Layer (3_CoreHub)**
```xml
<!-- VanAn.CoreHub.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\1_Shared\VanAn.Shared.csproj" />
  </ItemGroup>
  
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.8" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.8" />
  </ItemGroup>
</Project>
```

**Contents:**
- **Business Services:** 25+ services (OrderService, CustomerService, etc.)
- **Repository Pattern:** ICustomerRepository, etc.
- **Infrastructure:** VanAnDbContext, Migrations
- **Domain Logic:** Business rules implementation

#### **C. Gateway Layer (2_Gateway)**
```xml
<!-- VanAn.Gateway.csproj -->
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <ProjectReference Include="..\1_Shared\VanAn.Shared.csproj" />
    <ProjectReference Include="..\3_CoreHub\VanAn.CoreHub.csproj" />
  </ItemGroup>
  
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.SignalR" />
    <PackageReference Include="Yarp.ReverseProxy" />
  </ItemGroup>
</Project>
```

**Contents:**
- **API Controllers:** 8 controllers (Orders, Kitchen, VietQR, etc.)
- **SignalR Hubs:** OrderHub, KitchenHub
- **Reverse Proxy:** YARP configuration
- **Business Logic:** Order creation, VietQR generation

#### **D. Web Applications (5_WebApps)**

##### **KhachLink (Port 5002)**
```xml
<!-- VanAn.KhachLink.csproj -->
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <ProjectReference Include="..\1_Shared\VanAn.Shared.csproj" />
    <ProjectReference Include="..\3_CoreHub\VanAn.CoreHub.csproj" />
  </ItemGroup>
  
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.0" />
  </ItemGroup>
</Project>
```

**Contents:**
- **Razor Pages:** Index.cshtml, Campaign.cshtml
- **Blazor Components:** Cart.razor, Checkout.razor
- **Local Services:** CartService (localStorage)
- **SQLite Database:** vanan_khachlink.db

##### **ShopERP (Port 5003)**
```xml
<!-- VanAn.ShopERP.csproj -->
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <ProjectReference Include="..\1_Shared\VanAn.Shared.csproj" />
    <ProjectReference Include="..\3_CoreHub\VanAn.CoreHub.csproj" />
  </ItemGroup>
  
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.0" />
  </ItemGroup>
</Project>
```

**Contents:**
- **Razor Pages:** Kitchen/Index.cshtml, Dashboard.cshtml
- **Authentication:** Cookie-based auth
- **SQLite Database:** vanan_shoperp.db
- **Authorization:** Role-based access control

---

## **3. COMMUNICATION PATTERNS**

### **3.1 Current Communication Flow**
```
Client (Browser)
    |
    v
KhachLink (Port 5002)      ShopERP (Port 5003)
    |                           |
    | Direct Dependency         | Direct Dependency
    v                           v
CoreHub Services (Business Logic)
    |
    v
Gateway (Port 5001) - SignalR Hub
    |
    v
PostgreSQL (Central Database)
```

### **3.2 Dependency Analysis**

#### **A. Compile-Time Dependencies**
```csharp
// KhachLink/Program.cs
builder.Services.AddScoped<IOrderWorkflowService, OrderWorkflowService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ILoyaltyRewardsService, LoyaltyRewardsService>();

// ShopERP/Program.cs  
builder.Services.AddScoped<IOrderWorkflowService, OrderWorkflowService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ILoyaltyRewardsService, LoyaltyRewardsService>();

// Gateway/Program.cs
builder.Services.AddScoped<IOrderWorkflowService, OrderWorkflowService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ILoyaltyRewardsService, LoyaltyRewardsService>();
```

**Problem:** All applications reference the same service implementations.

#### **B. Runtime Dependencies**
```csharp
// Gateway/Controllers/OrdersController.cs
public class OrdersController : ControllerBase
{
    private readonly VanAnDbContext _context;                    // Direct DB access
    private readonly IOrderWorkflowService _orderWorkflowService; // Direct service
    private readonly IHubContext<OrderHub> _orderHub;             // SignalR Hub
    
    // Business logic in controller
    [HttpPost]
    public async Task<ActionResult<VietQrResponse>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        // Direct database operations
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        
        // Direct business logic
        order.CalculateTotals();
        
        // Direct SignalR notification
        await _orderHub.Clients.All.SendAsync("NewOrderReceived", ...);
    }
}
```

---

## **4. DATABASE ARCHITECTURE**

### **4.1 Multi-Database Setup**

#### **A. Central PostgreSQL (Gateway & CoreHub)**
```csharp
// Gateway/Program.cs
builder.Services.AddDbContext<VanAnDbContext>(options =>
    options.UseNpgsql(connectionString));

// Connection String: Host=localhost;Port=5432;Database=vanan_central;Username=vanan;Password=...
```

#### **B. Local SQLite (KhachLink)**
```csharp
// KhachLink/Program.cs
var connectionString = $"Data Source={AppContext.BaseDirectory}vanan_khachlink.db";
builder.Services.AddDbContext<VanAnDbContext>(options => 
    options.UseSqlite(connectionString));
```

#### **C. Local SQLite (ShopERP)**
```csharp
// ShopERP/Program.cs
var connectionString = $"Data Source={AppContext.BaseDirectory}vanan_shoperp.db";
builder.Services.AddDbContext<VanAnDbContext>(options => 
    options.UseSqlite(connectionString));
```

### **4.2 Database Schema Issues**
- **Same VanAnDbContext** used across all databases
- **No data synchronization** between databases
- **Schema inconsistencies** possible
- **No multi-tenancy** enforcement

---

## **5. SERVICE LAYER ANALYSIS**

### **5.1 CoreHub Services (3_CoreHub/Services/)**

#### **Business Services List:**
```
AudioCleanupService.cs           (2632 bytes)
CustomerOnboardingService.cs     (8954 bytes)
CustomerService.cs               (3090 bytes)
DashboardService.cs              (14563 bytes)
DataVersioningService.cs         (24908 bytes)
InventoryService.cs              (4795 bytes)
KitchenService.cs                (10588 bytes)
LoyaltyRewardsService.cs         (6903 bytes)
NotificationService.cs           (2664 bytes)
OmnichannelOrderService.cs       (42324 bytes)
OmnichannelService.cs            (13960 bytes)
OrderService.cs                  (3306 bytes)
OrderWorkflowService.cs         (9456 bytes)
ProductionDeploymentService.cs   (67033 bytes)
RealTimeSyncService.cs           (40473 bytes)
ResponsiveUIService.cs           (13610 bytes)
ShopConfigService.cs             (1699 bytes)
ShopService.cs                   (2289 bytes)
SocialCampaignService.cs         (4858 bytes)
SyncStrategyService.cs           (25576 bytes)
VoiceCommandService.cs           (975 bytes)
```

#### **Service Pattern Analysis**
```csharp
// Typical Service Pattern
public class OrderService : IOrderService
{
    private readonly VanAnDbContext _context;
    private readonly ILogger<OrderService> _logger;

    public OrderService(VanAnDbContext context, ILogger<OrderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Order> CreateOrderAsync(Order order)
    {
        // Direct database access
        await _context.Orders.AddAsync(order);
        await _context.SaveChangesAsync();
        return order;
    }
}
```

**Issues:**
- **No repository abstraction** in many services
- **Direct database access** throughout
- **No unit of work pattern**
- **No transaction management** across services

---

## **6. API LAYER ANALYSIS**

### **6.1 Gateway Controllers (2_Gateway/Controllers/)**

#### **Controller List:**
```
BuildController.cs              (1909 bytes)
KitchenController.cs            (3325 bytes)
LocalizationController.cs      (3030 bytes)
OnboardingController.cs        (7232 bytes)
OrdersController.cs            (5742 bytes)
ShopConfigController.cs        (2729 bytes)
VietQrController.cs            (2602 bytes)
VoiceCommandController.cs       (9244 bytes)
```

#### **Controller Pattern Analysis**
```csharp
// OrdersController.cs - Typical Pattern
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    // Multiple dependencies
    private readonly VanAnDbContext _context;
    private readonly IVietQrService _vietQrService;
    private readonly IOrderWorkflowService _orderWorkflowService;
    private readonly IHubContext<OrderHub> _orderHub;

    [HttpPost]
    public async Task<ActionResult<VietQrResponse>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        // Business logic in controller
        var order = new Order { ... };
        
        // Database operations in controller
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        
        // External service calls in controller
        var payload = await _vietQrService.GenerateQrCodeAsync(...);
        
        // Real-time notifications in controller
        await _orderHub.Clients.All.SendAsync("NewOrderReceived", ...);
        
        return CreatedAtAction(...);
    }
}
```

**Issues:**
- **Business logic in controllers**
- **Multiple responsibilities** per controller
- **Direct database access**
- **No separation of concerns**

---

## **7. REAL-TIME COMMUNICATION**

### **7.1 SignalR Implementation**

#### **OrderHub (2_Gateway/Hubs/OrderHub.cs)**
```csharp
public class OrderHub : Hub
{
    private static readonly Dictionary<string, string> ConnectedShops = new();

    public async Task JoinShopGroup(string shopId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Shop_{shopId}");
        ConnectedShops[Context.ConnectionId] = shopId;
    }

    public async Task NotifyNewOrder(string shopId, Order order)
    {
        await Clients.Group($"Shop_{shopId}").SendAsync("NewOrderReceived", new
        {
            OrderId = order.OrderId.Value,
            CustomerDeviceId = order.CustomerDeviceId,
            // ... order data
        });
    }
}
```

#### **KitchenHub (2_Gateway/Hubs/KitchenHub.cs)**
```csharp
public class KitchenHub : Hub
{
    [Authorize]
    public async Task JoinKitchen(string shopId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"shop_{shopId}");
    }

    public async Task BroadcastStatusChange(KitchenStatusUpdateDto update)
    {
        await Clients.Group($"shop_{update.ShopId}").SendAsync("ItemStatusChanged", update);
    }
}
```

**Issues:**
- **No message persistence**
- **No guaranteed delivery**
- **No offline message handling**
- **Memory-based state management**

---

## **8. FRONTEND ARCHITECTURE**

### **8.1 KhachLink Frontend**

#### **Technology Stack:**
- **Razor Pages** (Index.cshtml, Campaign.cshtml)
- **Blazor Components** (Cart.razor, Checkout.razor)
- **Bootstrap** for styling
- **JavaScript** for interactions

#### **Frontend Pattern:**
```html
<!-- Index.cshtml - Razor Page -->
@page "/"
@model VanAn.KhachLink.Pages.IndexModel

<div class="container">
    @foreach (var item in Model.Products)
    {
        <div class="card">
            <h5>@item.Name</h5>
            <button class="btn btn-primary" onclick="addToCart('@item.Id')">
                Add to Cart
            </button>
        </div>
    }
</div>
```

```csharp
<!-- Cart.razor - Blazor Component -->
@page "/cart"
@inject CartService CartService

@if (cartState.GetTotalItems() == 0)
{
    <div class="alert alert-info">
        Cart is empty
    </div>
}
else
{
    @foreach (var item in cartState.Items)
    {
        <div class="cart-item">
            <span>@item.ProductName</span>
            <span>@item.Quantity</span>
        </div>
    }
}
```

### **8.2 ShopERP Frontend**

#### **Technology Stack:**
- **Razor Pages** (Kitchen/Index.cshtml, Dashboard.cshtml)
- **Authentication** (Cookie-based)
- **Authorization** (Role-based)
- **SignalR Client** for real-time updates

#### **Frontend Pattern:**
```html
<!-- Kitchen/Index.cshtml -->
<div class="kitchen-display">
    <div id="orders-container">
        <!-- Orders loaded via SignalR -->
    </div>
</div>

<script>
    connection.on("NewOrderReceived", function (order) {
        displayOrder(order);
    });
</script>
```

---

## **9. AUTHENTICATION & AUTHORIZATION**

### **9.1 Authentication Patterns**

#### **ShopERP Authentication**
```csharp
// ShopERP/Program.cs
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OwnerOnly", policy => policy.RequireRole(UserRole.Owner.ToString()));
    options.AddPolicy("StoreManagement", policy => 
        policy.RequireRole(UserRole.Owner.ToString(), UserRole.StoreKeeper.ToString()));
});
```

#### **KhachLink Authentication**
```csharp
// KhachLink/Program.cs - No Authentication
// Device ID based identification only

var deviceId = Request.Cookies["customer_device_id"];
var customer = await _customerService.GetOrCreateCustomerByDeviceIdAsync(parsedDeviceId);
```

**Issues:**
- **Inconsistent authentication** across applications
- **No centralized auth system**
- **Device ID only** for customer identification
- **No JWT tokens**

---

## **10. DEPLOYMENT ARCHITECTURE**

### **10.1 Current Deployment**

#### **Port Allocation:**
```
Gateway (2_Gateway)     : Port 5001
KhachLink (5_WebApps)   : Port 5002
ShopERP (5_WebApps)     : Port 5003
```

#### **Database Connections:**
```
Gateway/Services: PostgreSQL (localhost:5432)
KhachLink:      SQLite (vanan_khachlink.db)
ShopERP:        SQLite (vanan_shoperp.db)
```

#### **Deployment Configuration:**
```csharp
// Gateway/Program.cs
var urls = builder.Configuration["ASPNETCORE_URLS"] ?? "http://0.0.0.0:5001";
app.Run(urls);

// KhachLink/Program.cs
var urls = builder.Configuration["ASPNETCORE_URLS"] ?? "http://0.0.0.0:5002";
await app.RunAsync(urls);

// ShopERP/Program.cs
var urls = builder.Configuration["ASPNETCORE_URLS"] ?? "http://0.0.0.0:5003";
await app.RunAsync(urls);
```

**Issues:**
- **Manual port management**
- **No service discovery**
- **No load balancing**
- **No container orchestration**

---

## **11. MONOLITHIC ARCHITECTURE PROBLEMS**

### **11.1 Tight Coupling**
- **Compile-time dependencies** between all projects
- **Shared database context** across applications
- **Direct service injection** throughout
- **No separation of concerns**

### **11.2 Scalability Issues**
- **Single deployment unit** for all functionality
- **Shared resources** (database, memory)
- **No independent scaling**
- **Performance bottlenecks**

### **11.3 Deployment Complexity**
- **All services must deploy together**
- **Database schema changes** affect all applications
- **Version compatibility** issues
- **Rollback complexity**

### **11.4 Development Challenges**
- **Large codebase** to maintain
- **Cross-team dependencies**
- **Difficult to test** in isolation
- **Slow development cycles**

---

## **12. CURRENT ARCHITECTURE BENEFITS**

### **12.1 Simplicity**
- **Easy to understand** architecture
- **Simple debugging** and troubleshooting
- **Fast development** for small teams
- **Low operational complexity**

### **12.2 Performance**
- **In-process communication** (fast)
- **No network latency** between services
- **Shared memory** access
- **Simple transaction management**

### **12.3 Development Speed**
- **Rapid prototyping** possible
- **Easy to add** new features
- **Simple deployment** for development
- **Fast feedback loops**

---

## **13. ARCHITECTURE DECISIONS ANALYSIS**

### **13.1 Why Monolithic Was Chosen**

#### **Initial Requirements:**
- **Small team** development
- **Rapid MVP** delivery
- **Simple deployment**
- **Cost efficiency**

#### **Business Context:**
- **Single shop** initially
- **Simple workflows**
- **Limited complexity**
- **Fast time-to-market**

### **13.2 Current Business Scale**
- **Multiple shops** needed
- **Complex workflows**
- **High availability** required
- **Scalability** important

---

## **14. MIGRATION PATH CONSIDERATIONS**

### **14.1 Strangler Fig Pattern**
1. **Create API Gateway** as entry point
2. **Extract services** one by one
3. **Migrate data** gradually
4. **Remove old dependencies**

### **14.2 Database Migration**
1. **Create separate databases** per service
2. **Implement data synchronization**
3. **Migrate data** gradually
4. **Decommission old databases**

### **14.3 Service Extraction Priority**
1. **Order Service** (core business logic)
2. **Customer Service** (user management)
3. **Product Service** (catalog management)
4. **Payment Service** (financial operations)

---

## **15. CONCLUSION**

### **15.1 Current State Assessment**
- **Architecture Type:** Monolithic with Shared Libraries
- **Complexity:** Medium-High
- **Scalability:** Low
- **Maintainability:** Medium
- **Development Speed:** Fast (for small changes)

### **15.2 Immediate Actions Required**
1. **Stop adding** new monolithic features
2. **Plan microservice migration**
3. **Implement API Gateway**
4. **Create service boundaries**

### **15.3 Long-term Vision**
- **Microservices architecture**
- **Event-driven communication**
- **Independent deployments**
- **Cloud-native scalability**

---

**Status:** Architecture analysis complete. Ready for microservice migration planning.
