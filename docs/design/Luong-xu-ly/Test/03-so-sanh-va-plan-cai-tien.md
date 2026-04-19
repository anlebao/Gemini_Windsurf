# Test - So Sánh và Plan C?i Ti?n

**Ngày:** 11 tháng 4, 2026  
**Module:** 6_Tests  
**Tr?ng thái:** Phân tích so sánh và k? ho?ch c?i ti?n

---

## **1. SO SÁNH TH?C T? vs LÝ T??NG**

### **1.1 Test Architecture Comparison**

| **Aspect** | **Th?c T?** | **Lý T??ng** | **Gap Analysis** |
|------------|--------------|--------------|------------------|
| **Test Layers** | 2 layers (Unit, Integration) | 4 layers (Unit, Integration, System, E2E) | **High** - C?n System & E2E layers |
| **Test Coverage** | 14/14 tests (100%) | 100% coverage with quality gates | **Medium** - C?n quality metrics |
| **Test Framework** | xUnit + FluentAssertions | xUnit + FluentAssertions + Moq + SpecFlow | **Medium** - C?n additional frameworks |
| **Test Data** | Hard-coded test data | Test data builders & factories | **Medium** - C?n test data management |
| **CI/CD Integration** | Basic GitHub Actions | Full CI/CD with quality gates | **High** - C?n CI/CD enhancement |

### **1.2 Test Quality Comparison**

| **Aspect** | **Th?c T?** | **Lý T??NG** | **Gap Analysis** |
|------------|--------------|--------------|------------------|
| **Test Naming** | Basic naming | BDD-style naming | **Medium** - C?n naming conventions |
| **Test Organization** | Basic class structure | Feature-based organization | **Medium** - C?n better organization |
| **Test Documentation** | Minimal | Comprehensive test documentation | **High** - C?n test documentation |
| **Test Maintenance** | Manual updates | Automated test maintenance | **High** - C?n maintenance automation |
| **Test Performance** | Fast execution | Optimized test execution | **Medium** - C?n performance optimization |

### **1.3 Test Infrastructure Comparison**

| **Aspect** | **Th?c T?** | **Lý T??NG** | **Gap Analysis** |
|------------|--------------|--------------|------------------|
| **Test Database** | In-memory SQLite | Multiple database providers | **Medium** - C?n database variety |
| **Test Containers** | None | Docker test containers | **High** - C?n containerization |
| **Mock Services** | Basic mocking | Advanced mocking strategies | **Medium** - C?n mocking enhancement |
| **Test Reports** | Basic console output | Comprehensive test reports | **High** - C?n reporting system |
| **Test Analytics** | None | Test analytics and metrics | **High** - C?n analytics system |

### **1.4 Test Strategy Comparison**

| **Aspect** | **Th?c T?** | **Lý T??NG** | **Gap Analysis** |
|------------|--------------|--------------|------------------|
| **Test Pyramid** | Inverted pyramid | Proper test pyramid | **High** - C?n pyramid balancing |
| **Test Automation** | Manual execution | Full automation | **Medium** - C?n automation enhancement |
| **Test Parallelization** | None | Parallel test execution | **Medium** - C?n parallelization |
| **Test Environment** | Single environment | Multiple test environments | **High** - C?n environment management |
| **Test Data Management** | Hard-coded | Dynamic test data generation | **High** - C?n data management |

---

## **2. DÁNH GIÁ M?C ?? ?U TIÊN**

### **2.1 Critical Issues (Priority 1)**
1. **Missing System Tests** - No API integration tests
2. **Missing E2E Tests** - No UI automation tests
3. **No Test Containers** - No containerized testing
4. **No Test Reports** - No comprehensive reporting
5. **No Test Analytics** - No test metrics and insights

### **2.2 Important Issues (Priority 2)**
1. **No Test Data Management** - Hard-coded test data
2. **No Test Parallelization** - Sequential execution
3. **No Test Documentation** - Minimal test documentation
4. **No Quality Gates** - No automated quality checks
5. **No Performance Testing** - No performance validation

### **2.3 Nice to Have (Priority 3)**
1. **No Visual Testing** - No UI regression testing
2. **No Security Testing** - No security validation
3. **No Accessibility Testing** - No accessibility checks
4. **No Load Testing** - No performance under load
5. **No Chaos Testing** - No resilience testing

---

## **3. K? HO?CH C?I TI?N**

### **3.1 Phase 1: System Tests (Week 1-2)**

#### **Day 1-3: API Integration Tests**
```csharp
// Create System Tests project
// 6_Tests/VanAn.System.Tests/VanAn.System.Tests.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
    <PackageReference Include="xunit" Version="2.4.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.4.5" />
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
    <PackageReference Include="Moq" Version="4.20.69" />
    <PackageReference Include="Testcontainers" Version="3.5.0" />
    <PackageReference Include="Testcontainers.PostgreSql" Version="3.5.0" />
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\2_Gateway\VanAn.Gateway.csproj" />
    <ProjectReference Include="..\..\3_CoreHub\VanAn.CoreHub.csproj" />
    <ProjectReference Include="..\..\1_Shared\VanAn.Shared.csproj" />
  </ItemGroup>

</Project>
```

#### **Day 4-5: API Test Infrastructure**
```csharp
// 6_Tests/VanAn.System.Tests/Infrastructure/TestWebApplicationFactory.cs
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly RedisContainer _redisContainer;

    public TestWebApplicationFactory()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:15")
            .WithDatabase("vanan_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();

        _redisContainer = new RedisBuilder()
            .WithImage("redis:7")
            .Build();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace database with test container
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<VanAnDbContext>));
            
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<VanAnDbContext>(options =>
                options.UseNpgsql(_postgresContainer.GetConnectionString()));

            // Replace Redis with test container
            var redisDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IConnectionMultiplexer));
            
            if (redisDescriptor != null)
            {
                services.Remove(redisDescriptor);
            }

            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(_redisContainer.GetConnectionString()));
        });

        builder.UseEnvironment("Testing");
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
        await _redisContainer.StartAsync();

        // Run migrations
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgresContainer.StopAsync();
        await _redisContainer.StopAsync();
    }
}

// 6_Tests/VanAn.System.Tests/Infrastructure/SystemTestBase.cs
public abstract class SystemTestBase : IAsyncLifetime
{
    protected readonly TestWebApplicationFactory _factory;
    protected readonly HttpClient _client;
    protected readonly VanAnDbContext _context;
    protected readonly IServiceProvider _serviceProvider;

    protected SystemTestBase()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();
        _serviceProvider = _factory.Services;
        _context = _serviceProvider.GetRequiredService<VanAnDbContext>();
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
        await SeedTestDataAsync();
    }

    public async Task DisposeAsync()
    {
        await CleanupTestDataAsync();
        await _factory.DisposeAsync();
    }

    protected virtual async Task SeedTestDataAsync()
    {
        // Seed test data
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Test Customer",
            Email = "test@example.com",
            Phone = "0912345678"
        };

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            Price = 25000,
            Category = "Beverages"
        };

        _context.Customers.Add(customer);
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
    }

    protected virtual async Task CleanupTestDataAsync()
    {
        _context.Orders.RemoveRange(_context.Orders);
        _context.Customers.RemoveRange(_context.Customers);
        _context.Products.RemoveRange(_context.Products);
        await _context.SaveChangesAsync();
    }

    protected async Task<T?> GetEntityAsync<T>(Guid id) where T : class
    {
        return await _context.Set<T>().FindAsync(id);
    }

    protected async Task<TEntity> CreateEntityAsync<TEntity>(TEntity entity) where TEntity : class
    {
        _context.Set<TEntity>().Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }
}
```

#### **Day 6-7: Order API Tests**
```csharp
// 6_Tests/VanAn.System.Tests/Controllers/OrdersControllerTests.cs
public class OrdersControllerTests : SystemTestBase
{
    [Fact]
    public async Task CreateOrder_Should_Return_Created_Order()
    {
        // Arrange
        var customer = await CreateEntityAsync(new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Test Customer",
            Email = "test@example.com",
            Phone = "0912345678"
        });

        var product = await CreateEntityAsync(new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            Price = 25000,
            Category = "Beverages"
        });

        var createOrderRequest = new CreateOrderRequest
        {
            CustomerId = customer.Id,
            Items = new List<OrderItemRequest>
            {
                new() { ProductId = product.Id, Quantity = 2 }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/orders", createOrderRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var orderResponse = await response.Content.ReadFromJsonAsync<OrderResponse>();
        orderResponse.Should().NotBeNull();
        orderResponse.CustomerId.Should().Be(customer.Id);
        orderResponse.Items.Should().HaveCount(1);
        orderResponse.TotalAmount.Should().Be(50000);
        orderResponse.Status.Should().Be(OrderStatus.Draft);
    }

    [Fact]
    public async Task GetOrders_Should_Return_All_Orders()
    {
        // Arrange
        var customer = await CreateEntityAsync(new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Test Customer",
            Email = "test@example.com",
            Phone = "0912345678"
        });

        var order = await CreateEntityAsync(new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Status = OrderStatus.Draft,
            TotalAmount = 50000,
            CreatedAt = DateTime.UtcNow
        });

        // Act
        var response = await _client.GetAsync("/api/v1/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var ordersResponse = await response.Content.ReadFromJsonAsync<List<OrderResponse>>();
        ordersResponse.Should().NotBeNull();
        ordersResponse.Should().HaveCount(1);
        ordersResponse.First().Id.Should().Be(order.Id);
    }

    [Fact]
    public async Task GetOrderById_Should_Return_Order_When_Exists()
    {
        // Arrange
        var customer = await CreateEntityAsync(new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Test Customer",
            Email = "test@example.com",
            Phone = "0912345678"
        });

        var order = await CreateEntityAsync(new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Status = OrderStatus.Draft,
            TotalAmount = 50000,
            CreatedAt = DateTime.UtcNow
        });

        // Act
        var response = await _client.GetAsync($"/api/v1/orders/{order.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var orderResponse = await response.Content.ReadFromJsonAsync<OrderResponse>();
        orderResponse.Should().NotBeNull();
        orderResponse.Id.Should().Be(order.Id);
    }

    [Fact]
    public async Task UpdateOrderStatus_Should_Update_Status_When_Valid()
    {
        // Arrange
        var customer = await CreateEntityAsync(new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Test Customer",
            Email = "test@example.com",
            Phone = "0912345678"
        });

        var order = await CreateEntityAsync(new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Status = OrderStatus.Draft,
            TotalAmount = 50000,
            CreatedAt = DateTime.UtcNow
        });

        var updateRequest = new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Confirmed
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/orders/{order.Id}/status", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var orderResponse = await response.Content.ReadFromJsonAsync<OrderResponse>();
        orderResponse.Should().NotBeNull();
        orderResponse.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public async Task CreateOrder_Should_Return_BadRequest_When_Invalid_Data()
    {
        // Arrange
        var createOrderRequest = new CreateOrderRequest
        {
            CustomerId = Guid.Empty,
            Items = new List<OrderItemRequest>()
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/orders", createOrderRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

### **3.2 Phase 2: E2E Tests (Week 3-4)**

#### **Day 8-10: E2E Test Setup**
```csharp
// 6_Tests/VanAn.E2E.Tests/VanAn.E2E.Tests.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Playwright" Version="1.44.0" />
    <PackageReference Include="Microsoft.Playwright.NUnit" Version="1.44.0" />
    <PackageReference Include="xunit" Version="2.4.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.4.5" />
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
    <PackageReference Include="Testcontainers" Version="3.5.0" />
    <PackageReference Include="Testcontainers.PostgreSql" Version="3.5.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\5_WebApps\KhachLink\VanAn.KhachLink.csproj" />
    <ProjectReference Include="..\..\2_Gateway\VanAn.Gateway.csproj" />
    <ProjectReference Include="..\..\3_CoreHub\VanAn.CoreHub.csproj" />
  </ItemGroup>

</Project>
```

#### **Day 11-12: E2E Test Infrastructure**
```csharp
// 6_Tests/VanAn.E2E.Tests/Infrastructure/E2ETestBase.cs
public abstract class E2ETestBase : IAsyncLifetime
{
    protected readonly IPlaywright _playwright;
    protected readonly IBrowser _browser;
    protected readonly IPage _page;
    protected readonly ITestOutputHelper _output;

    protected E2ETestBase(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
            SlowMo = 100,
            Args = new[] { "--start-maximized" }
        });

        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize(1920, 1080),
            IgnoreHTTPSErrors = true
        });

        _page = await context.NewPageAsync();
        
        // Setup logging
        _page.Console += (_, e) => _output.WriteLine($"Console: {e.Text}");
        _page.PageError += (_, e) => _output.WriteLine($"Page Error: {e}");
    }

    public async Task DisposeAsync()
    {
        await _page.CloseAsync();
        await _browser.CloseAsync();
        await _playwright.DisposeAsync();
    }

    protected async Task NavigateToPageAsync(string url)
    {
        await _page.GotoAsync(url);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    protected async Task ClickElementAsync(string selector)
    {
        await _page.ClickAsync(selector);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    protected async Task FillInputAsync(string selector, string value)
    {
        await _page.FillAsync(selector, value);
    }

    protected async Task WaitForElementAsync(string selector)
    {
        await _page.WaitForSelectorAsync(selector);
    }

    protected async Task AssertTextContentAsync(string selector, string expectedText)
    {
        var element = await _page.QuerySelectorAsync(selector);
        var text = await element.TextContentAsync();
        text.Should().Contain(expectedText);
    }

    protected async Task AssertElementVisibleAsync(string selector)
    {
        var element = await _page.QuerySelectorAsync(selector);
        element.Should().NotBeNull();
        await element.IsVisibleAsync().Should().BeTrue();
    }

    protected async Task TakeScreenshotAsync(string fileName)
    {
        await _page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = $"screenshots/{fileName}_{DateTime.Now:yyyyMMdd_HHmmss}.png",
            FullPage = true
        });
    }
}
```

#### **Day 13-14: Order Flow E2E Tests**
```csharp
// 6_Tests/VanAn.E2E.Tests/Flows/OrderFlowTests.cs
public class OrderFlowTests : E2ETestBase
{
    private readonly string _baseUrl = "https://localhost:3001";

    public OrderFlowTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task Complete_Order_Flow_Should_Work_End_To_End()
    {
        // Arrange
        await NavigateToPageAsync($"{_baseUrl}/");

        // Act & Assert - Step 1: View Products
        await AssertElementVisibleAsync(".product-catalog");
        await AssertTextContentAsync("h1", "Our Products");

        // Step 2: Add Product to Cart
        await ClickElementAsync(".product-card:first-child .add-to-cart");
        await WaitForElementAsync(".cart-badge");
        await AssertTextContentAsync(".cart-badge", "1");

        // Step 3: View Cart
        await ClickElementAsync(".cart-icon");
        await WaitForElementAsync(".cart-items");
        await AssertElementVisibleAsync(".cart-item");

        // Step 4: Checkout
        await ClickElementAsync(".checkout-button");
        await WaitForElementAsync(".checkout-form");

        // Step 5: Fill Customer Information
        await FillInputAsync("#customer-name", "Test Customer");
        await FillInputAsync("#customer-phone", "0912345678");
        await FillInputAsync("#customer-email", "test@example.com");

        // Step 6: Submit Order
        await ClickElementAsync(".submit-order-button");
        await WaitForElementAsync(".order-confirmation");

        // Assert - Order Confirmation
        await AssertTextContentAsync(".order-confirmation", "Order Placed Successfully");
        await AssertElementVisibleAsync(".order-number");

        // Take screenshot for verification
        await TakeScreenshotAsync("order_flow_complete");
    }

    [Fact]
    public async Task Product_Search_Should_Filter_Products()
    {
        // Arrange
        await NavigateToPageAsync($"{_baseUrl}/");

        // Act
        await FillInputAsync(".search-input", "Coffee");
        await ClickElementAsync(".search-button");

        // Assert
        await WaitForElementAsync(".product-catalog");
        await AssertElementVisibleAsync(".product-card");
        
        var productCards = await _page.QuerySelectorAllAsync(".product-card");
        productCards.Should().NotBeEmpty();

        foreach (var card in productCards)
        {
            var productName = await card.QuerySelectorAsync(".product-name");
            var text = await productName.TextContentAsync();
            text.Should().Contain("Coffee", "All products should contain 'Coffee'");
        }
    }

    [Fact]
    public async Task Cart_Management_Should_Work_Correctly()
    {
        // Arrange
        await NavigateToPageAsync($"{_baseUrl}/");

        // Act - Add multiple items
        await ClickElementAsync(".product-card:nth-child(1) .add-to-cart");
        await ClickElementAsync(".product-card:nth-child(2) .add-to-cart");
        await ClickElementAsync(".product-card:nth-child(1) .add-to-cart");

        // Assert - Cart should show correct count
        await WaitForElementAsync(".cart-badge");
        await AssertTextContentAsync(".cart-badge", "3");

        // Act - View cart
        await ClickElementAsync(".cart-icon");
        await WaitForElementAsync(".cart-items");

        // Assert - Cart should show correct items
        var cartItems = await _page.QuerySelectorAllAsync(".cart-item");
        cartItems.Should().HaveCount(2);

        // Act - Update quantity
        await ClickElementAsync(".cart-item:first-child .quantity-increase");
        await WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Cart should update
        await AssertTextContentAsync(".cart-total", "4");
    }

    [Fact]
    public async Task Order_History_Should_Show_Previous_Orders()
    {
        // Arrange - Create a test order first
        await CreateTestOrderAsync();

        // Act
        await NavigateToPageAsync($"{_baseUrl}/orders");

        // Assert
        await WaitForElementAsync(".order-history");
        await AssertElementVisibleAsync(".order-item");
        
        var orderItems = await _page.QuerySelectorAllAsync(".order-item");
        orderItems.Should().NotBeEmpty();
    }

    private async Task CreateTestOrderAsync()
    {
        await NavigateToPageAsync($"{_baseUrl}/");
        await ClickElementAsync(".product-card:first-child .add-to-cart");
        await ClickElementAsync(".cart-icon");
        await ClickElementAsync(".checkout-button");
        await FillInputAsync("#customer-name", "Test Customer");
        await FillInputAsync("#customer-phone", "0912345678");
        await FillInputAsync("#customer-email", "test@example.com");
        await ClickElementAsync(".submit-order-button");
        await WaitForElementAsync(".order-confirmation");
    }
}
```

### **3.3 Phase 3: Test Infrastructure Enhancement (Week 5-6)**

#### **Day 15-17: Test Data Management**
```csharp
// 6_Tests/VanAn.Testing.Common/Data/TestDataBuilder.cs
public abstract class TestDataBuilder<T>
{
    protected readonly List<Action<T>> _configurations = new();

    public TestDataBuilder<T> With(Action<T> configuration)
    {
        _configurations.Add(configuration);
        return this;
    }

    public abstract T Build();

    protected void ApplyConfigurations(T entity)
    {
        foreach (var configuration in _configurations)
        {
            configuration(entity);
        }
    }
}

public class CustomerBuilder : TestDataBuilder<Customer>
{
    private Guid _id = Guid.NewGuid();
    private string _name = "Test Customer";
    private string _email = "test@example.com";
    private string _phone = "0912345678";

    public CustomerBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public CustomerBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public CustomerBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public CustomerBuilder WithPhone(string phone)
    {
        _phone = phone;
        return this;
    }

    public override Customer Build()
    {
        var customer = new Customer
        {
            Id = _id,
            Name = _name,
            Email = _email,
            Phone = _phone
        };

        ApplyConfigurations(customer);
        return customer;
    }
}

public class OrderBuilder : TestDataBuilder<Order>
{
    private Guid _id = Guid.NewGuid();
    private Guid _customerId = Guid.NewGuid();
    private OrderStatus _status = OrderStatus.Draft;
    private decimal _totalAmount = 0;
    private List<OrderItem> _items = new();

    public OrderBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public OrderBuilder WithCustomer(Guid customerId)
    {
        _customerId = customerId;
        return this;
    }

    public OrderBuilder WithStatus(OrderStatus status)
    {
        _status = status;
        return this;
    }

    public OrderBuilder WithTotalAmount(decimal totalAmount)
    {
        _totalAmount = totalAmount;
        return this;
    }

    public OrderBuilder WithItem(OrderItem item)
    {
        _items.Add(item);
        return this;
    }

    public override Order Build()
    {
        var order = new Order
        {
            Id = _id,
            CustomerId = _customerId,
            Status = _status,
            TotalAmount = _totalAmount,
            CreatedAt = DateTime.UtcNow,
            Items = _items
        };

        ApplyConfigurations(order);
        return order;
    }
}

// 6_Tests/VanAn.Testing.Common/Data/TestDataFactory.cs
public class TestDataFactory
{
    public static CustomerBuilder Customer() => new CustomerBuilder();
    public static OrderBuilder Order() => new OrderBuilder();
    public static ProductBuilder Product() => new ProductBuilder();
    public static OrderItemBuilder OrderItem() => new OrderItemBuilder();

    public static List<Customer> CreateCustomers(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => Customer()
                .WithName($"Customer {i}")
                .WithEmail($"customer{i}@example.com")
                .WithPhone($"091234567{i}")
                .Build())
            .ToList();
    }

    public static List<Order> CreateOrders(int count, Guid? customerId = null)
    {
        return Enumerable.Range(0, count)
            .Select(i => Order()
                .WithCustomer(customerId ?? Guid.NewGuid())
                .WithStatus(OrderStatus.Draft)
                .WithTotalAmount(25000 * (i + 1))
                .Build())
            .ToList();
    }
}
```

#### **Day 18-19: Test Parallelization**
```csharp
// 6_Tests/VanAn.Testing.Common/Parallelization/ParallelTestRunner.cs
public class ParallelTestRunner
{
    private readonly ITestOutputHelper _output;
    private readonly int _maxParallelTests;

    public ParallelTestRunner(ITestOutputHelper output, int maxParallelTests = 4)
    {
        _output = output;
        _maxParallelTests = maxParallelTests;
    }

    public async Task RunTestsInParallel<T>(IEnumerable<Func<Task<T>>> testFunctions)
    {
        var semaphore = new SemaphoreSlim(_maxParallelTests, _maxParallelTests);
        var tasks = testFunctions.Select(async testFunc =>
        {
            await semaphore.WaitAsync();
            try
            {
                return await testFunc();
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        
        _output.WriteLine($"Completed {results.Length} tests in parallel");
    }
}

// 6_Tests/VanAn.Testing.Common/Parallelization/IsolatedTestDatabase.cs
public class IsolatedTestDatabase : IDisposable
{
    private readonly string _databaseName;
    private readonly PostgreSqlContainer _container;

    public string ConnectionString { get; private set; }

    public IsolatedTestDatabase()
    {
        _databaseName = $"vanan_test_{Guid.NewGuid():N}";
        
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:15")
            .WithDatabase(_databaseName)
            .WithUsername("test")
            .WithPassword("test")
            .WithPortBinding(5432, true) // Random port
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
        
        // Run migrations
        using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public VanAnDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<VanAnDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new VanAnDbContext(options);
    }

    public void Dispose()
    {
        _container.StopAsync().Wait();
        _container.DisposeAsync().Wait();
    }
}
```

#### **Day 20-21: Test Reporting**
```csharp
// 6_Tests/VanAn.Testing.Common/Reporting/TestReportGenerator.cs
public class TestReportGenerator
{
    private readonly ITestOutputHelper _output;

    public TestReportGenerator(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task GenerateReportAsync(TestResults testResults)
    {
        var report = new TestReport
        {
            GeneratedAt = DateTime.UtcNow,
            TotalTests = testResults.TotalTests,
            PassedTests = testResults.PassedTests,
            FailedTests = testResults.FailedTests,
            SkippedTests = testResults.SkippedTests,
            Duration = testResults.Duration,
            TestSuites = testResults.TestSuites
        };

        // Generate HTML report
        var htmlReport = GenerateHtmlReport(report);
        await File.WriteAllTextAsync("test-reports/index.html", htmlReport);

        // Generate JSON report
        var jsonReport = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync("test-reports/results.json", jsonReport);

        // Generate JUnit XML report
        var junitReport = GenerateJunitReport(report);
        await File.WriteAllTextAsync("test-reports/junit.xml", junitReport);

        _output.WriteLine($"Test report generated: test-reports/index.html");
    }

    private string GenerateHtmlReport(TestReport report)
    {
        var template = @"
<!DOCTYPE html>
<html>
<head>
    <title>VanAn Test Report</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        .header { background: #f0f0f0; padding: 20px; border-radius: 5px; }
        .summary { display: flex; gap: 20px; margin: 20px 0; }
        .metric { background: #e8f4fd; padding: 15px; border-radius: 5px; text-align: center; }
        .passed { background: #d4edda; }
        .failed { background: #f8d7da; }
        .skipped { background: #fff3cd; }
        table { width: 100%; border-collapse: collapse; margin: 20px 0; }
        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
        th { background: #f2f2f2; }
    </style>
</head>
<body>
    <div class='header'>
        <h1>VanAn Test Report</h1>
        <p>Generated: {{GeneratedAt}}</p>
    </div>
    
    <div class='summary'>
        <div class='metric'>
            <h3>{{TotalTests}}</h3>
            <p>Total Tests</p>
        </div>
        <div class='metric passed'>
            <h3>{{PassedTests}}</h3>
            <p>Passed</p>
        </div>
        <div class='metric failed'>
            <h3>{{FailedTests}}</h3>
            <p>Failed</p>
        </div>
        <div class='metric skipped'>
            <h3>{{SkippedTests}}</h3>
            <p>Skipped</p>
        </div>
    </div>
    
    <h2>Test Suites</h2>
    <table>
        <tr>
            <th>Suite</th>
            <th>Tests</th>
            <th>Passed</th>
            <th>Failed</th>
            <th>Duration</th>
        </tr>
        {{#TestSuites}}
        <tr>
            <td>{{Name}}</td>
            <td>{{TotalTests}}</td>
            <td>{{PassedTests}}</td>
            <td>{{FailedTests}}</td>
            <td>{{Duration}}</td>
        </tr>
        {{/TestSuites}}
    </table>
</body>
</html>";

        return template
            .Replace("{{GeneratedAt}}", report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"))
            .Replace("{{TotalTests}}", report.TotalTests.ToString())
            .Replace("{{PassedTests}}", report.PassedTests.ToString())
            .Replace("{{FailedTests}}", report.FailedTests.ToString())
            .Replace("{{SkippedTests}}", report.SkippedTests.ToString())
            .Replace("{{Duration}}", report.Duration.ToString(@"hh\:mm\:ss"));
    }

    private string GenerateJunitReport(TestReport report)
    {
        var xml = new XElement("testsuites",
            new XAttribute("name", "VanAn.Tests"),
            new XAttribute("tests", report.TotalTests),
            new XAttribute("failures", report.FailedTests),
            new XAttribute("time", report.Duration.TotalSeconds.ToString("F2")),
            
            report.TestSuites.Select(suite => new XElement("testsuite",
                new XAttribute("name", suite.Name),
                new XAttribute("tests", suite.TotalTests),
                new XAttribute("failures", suite.FailedTests),
                new XAttribute("time", suite.Duration.TotalSeconds.ToString("F2")),
                
                suite.Tests.Select(test => new XElement("testcase",
                    new XAttribute("name", test.Name),
                    new XAttribute("classname", test.ClassName),
                    new XAttribute("time", test.Duration.TotalSeconds.ToString("F2")),
                    
                    test.Status == TestStatus.Failed ? new XElement("failure",
                        new XAttribute("message", test.ErrorMessage),
                        test.ErrorMessage
                    ) : null
                ))
            ))
        );

        return xml.ToString();
    }
}
```

### **3.4 Phase 4: Quality Gates & CI/CD (Week 7-8)**

#### **Day 22-24: Quality Gates**
```csharp
// 6_Tests/VanAn.Testing.Common/Quality/QualityGate.cs
public class QualityGate
{
    private readonly ITestOutputHelper _output;
    private readonly QualityGateConfig _config;

    public QualityGate(ITestOutputHelper output, QualityGateConfig config)
    {
        _output = output;
        _config = config;
    }

    public QualityGateResult Evaluate(TestResults testResults)
    {
        var result = new QualityGateResult
        {
            Passed = true,
            Violations = new List<QualityViolation>()
        };

        // Check code coverage
        if (testResults.CodeCoverage < _config.MinimumCodeCoverage)
        {
            result.Passed = false;
            result.Violations.Add(new QualityViolation
            {
                Type = ViolationType.CodeCoverage,
                Message = $"Code coverage {testResults.CodeCoverage}% is below minimum {_config.MinimumCodeCoverage}%"
            });
        }

        // Check test failure rate
        var failureRate = (double)testResults.FailedTests / testResults.TotalTests;
        if (failureRate > _config.MaximumFailureRate)
        {
            result.Passed = false;
            result.Violations.Add(new QualityViolation
            {
                Type = ViolationType.FailureRate,
                Message = $"Test failure rate {failureRate:P} exceeds maximum {_config.MaximumFailureRate:P}"
            });
        }

        // Check test duration
        if (testResults.Duration > _config.MaximumTestDuration)
        {
            result.Passed = false;
            result.Violations.Add(new QualityViolation
            {
                Type = ViolationType.TestDuration,
                Message = $"Test duration {testResults.Duration} exceeds maximum {_config.MaximumTestDuration}"
            });
        }

        // Check flaky tests
        var flakyTests = testResults.Tests.Where(t => t.IsFlaky).ToList();
        if (flakyTests.Count > _config.MaximumFlakyTests)
        {
            result.Passed = false;
            result.Violations.Add(new QualityViolation
            {
                Type = ViolationType.FlakyTests,
                Message = $"Flaky tests count {flakyTests.Count} exceeds maximum {_config.MaximumFlakyTests}"
            });
        }

        return result;
    }
}

// 6_Tests/VanAn.Testing.Common/Quality/QualityGateConfig.cs
public class QualityGateConfig
{
    public double MinimumCodeCoverage { get; set; } = 80.0;
    public double MaximumFailureRate { get; set; } = 0.05; // 5%
    public TimeSpan MaximumTestDuration { get; set; } = TimeSpan.FromMinutes(10);
    public int MaximumFlakyTests { get; set; } = 0;
    public bool RequireAllTestsPass { get; set; } = true;
}

// 6_Tests/VanAn.Testing.Common/Quality/TestFlakinessDetector.cs
public class TestFlakinessDetector
{
    private readonly Dictionary<string, List<TestRun>> _testHistory = new();

    public void RecordTestRun(string testName, bool passed, TimeSpan duration)
    {
        if (!_testHistory.ContainsKey(testName))
        {
            _testHistory[testName] = new List<TestRun>();
        }

        _testHistory[testName].Add(new TestRun
        {
            Passed = passed,
            Duration = duration,
            Timestamp = DateTime.UtcNow
        });

        // Keep only last 10 runs
        if (_testHistory[testName].Count > 10)
        {
            _testHistory[testName].RemoveAt(0);
        }
    }

    public bool IsFlaky(string testName, int threshold = 3)
    {
        if (!_testHistory.ContainsKey(testName) || _testHistory[testName].Count < threshold)
        {
            return false;
        }

        var recentRuns = _testHistory[testName].TakeLast(threshold);
        var failures = recentRuns.Count(r => !r.Passed);
        
        return failures > 0 && failures < threshold;
    }

    public double GetFlakinessRate(string testName)
    {
        if (!_testHistory.ContainsKey(testName))
        {
            return 0;
        }

        var runs = _testHistory[testName];
        var failures = runs.Count(r => !r.Passed);
        
        return (double)failures / runs.Count;
    }
}
```

#### **Day 25-26: CI/CD Integration**
```yaml
# .github/workflows/test.yml
name: Test Pipeline

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  test:
    runs-on: ubuntu-latest
    
    services:
      postgres:
        image: postgres:15
        env:
          POSTGRES_PASSWORD: test
          POSTGRES_USER: test
          POSTGRES_DB: vanan_test
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
        ports:
          - 5432:5432

      redis:
        image: redis:7
        options: >-
          --health-cmd "redis-cli ping"
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
        ports:
          - 6379:6379

    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'
    
    - name: Cache NuGet packages
      uses: actions/cache@v4
      with:
        path: ~/.nuget/packages
        key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
        restore-keys: |
          ${{ runner.os }}-nuget-
    
    - name: Install dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore --configuration Release
    
    - name: Run Unit Tests
      run: dotnet test 6_Tests/VanAn.Core.Tests --no-build --configuration Release --verbosity normal --collect:"XPlat Code Coverage"
    
    - name: Run Integration Tests
      run: dotnet test 6_Tests/VanAn.Integration.Tests --no-build --configuration Release --verbosity normal --collect:"XPlat Code Coverage"
    
    - name: Run System Tests
      run: dotnet test 6_Tests/VanAn.System.Tests --no-build --configuration Release --verbosity normal --collect:"XPlat Code Coverage"
      env:
        ConnectionStrings__VanAnDbContext: "Host=localhost;Port=5432;Database=vanan_test;Username=test;Password=test"
        ConnectionStrings__Redis: "localhost:6379"
    
    - name: Setup Playwright
      run: dotnet tool install --global Microsoft.Playwright.CLI && playwright install --with-deps
    
    - name: Run E2E Tests
      run: dotnet test 6_Tests/VanAn.E2E.Tests --no-build --configuration Release --verbosity normal
    
    - name: Upload coverage reports
      uses: codecov/codecov-action@v4
      with:
        files: '**/coverage.cobertura.xml'
        fail_ci_if_error: false
    
    - name: Generate Test Report
      run: |
        dotnet test 6_Tests/ --no-build --configuration Release --logger:"junit;LogFilePath=test-results.xml;MethodFormat=Class"
        dotnet tool install --global dotnet-reportgenerator-globaltool
        reportgenerator -reports:**/coverage.cobertura.xml -targetdir:TestResults -reporttypes:Html;Badges
    
    - name: Upload Test Results
      uses: actions/upload-artifact@v4
      if: always()
      with:
        name: test-results
        path: |
          TestResults/
          test-results.xml
          6_Tests/**/screenshots/
    
    - name: Quality Gate Check
      run: |
        # Custom quality gate script
        dotnet run --project 6_Tests/VanAn.Testing.CLI -- check-quality-gate
```

#### **Day 27-28: Performance Testing**
```csharp
// 6_Tests/VanAn.Performance.Tests/PerformanceTestBase.cs
public abstract class PerformanceTestBase
{
    protected readonly ITestOutputHelper _output;
    protected readonly HttpClient _client;

    protected PerformanceTestBase(ITestOutputHelper output)
    {
        _output = output;
        _client = new HttpClient();
    }

    protected async Task<PerformanceResult> MeasurePerformanceAsync(string endpoint, HttpMethod method, HttpContent content = null)
    {
        var stopwatch = Stopwatch.StartNew();
        
        var request = new HttpRequestMessage(method, endpoint);
        if (content != null)
        {
            request.Content = content;
        }

        var response = await _client.SendAsync(request);
        stopwatch.Stop();

        return new PerformanceResult
        {
            Endpoint = endpoint,
            Method = method.Method,
            StatusCode = response.StatusCode,
            ResponseTime = stopwatch.Elapsed,
            ResponseSize = response.Content.Headers.ContentLength ?? 0,
            Success = response.IsSuccessStatusCode
        };
    }

    protected async Task<LoadTestResult> RunLoadTestAsync(string endpoint, int concurrentUsers, TimeSpan duration)
    {
        var tasks = new List<Task<PerformanceResult>>();
        var startTime = DateTime.UtcNow;

        for (int i = 0; i < concurrentUsers; i++)
        {
            tasks.Add(MeasurePerformanceAsync(endpoint, HttpMethod.Get));
        }

        await Task.WhenAll(tasks);

        var results = await Task.WhenAll(tasks);
        
        return new LoadTestResult
        {
            Endpoint = endpoint,
            ConcurrentUsers = concurrentUsers,
            Duration = duration,
            Results = results,
            StartTime = startTime,
            EndTime = DateTime.UtcNow
        };
    }
}

// 6_Tests/VanAn.Performance.Tests/Controllers/OrdersPerformanceTests.cs
public class OrdersPerformanceTests : PerformanceTestBase
{
    private readonly string _baseUrl = "https://localhost:5001";

    public OrdersPerformanceTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task GetOrders_Should_Complete_Under_100ms()
    {
        // Act
        var result = await MeasurePerformanceAsync($"{_baseUrl}/api/v1/orders", HttpMethod.Get);

        // Assert
        result.Success.Should().BeTrue();
        result.ResponseTime.Should().BeLessThan(TimeSpan.FromMilliseconds(100));
        
        _output.WriteLine($"GetOrders completed in {result.ResponseTime.TotalMilliseconds}ms");
    }

    [Fact]
    public async Task CreateOrder_Should_Handle_100_Concurrent_Users()
    {
        // Act
        var result = await RunLoadTestAsync($"{_baseUrl}/api/v1/orders", 100, TimeSpan.FromSeconds(30));

        // Assert
        result.Results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
        result.AverageResponseTime.Should().BeLessThan(TimeSpan.FromMilliseconds(500));
        result.Throughput.Should().BeGreaterThan(50); // requests per second
        
        _output.WriteLine($"Load test completed: {result.Throughput:F2} RPS, Average: {result.AverageResponseTime.TotalMilliseconds}ms");
    }
}
```

---

## **4. IMPLEMENTATION PLAN**

### **4.1 Week 1-2: System Tests**
- [ ] Create System Tests project
- [ ] Set up test containers
- [ ] Implement API integration tests
- [ ] Add test infrastructure
- [ ] Create test data builders
- [ ] Add API test coverage

### **4.2 Week 3-4: E2E Tests**
- [ ] Create E2E Tests project
- [ ] Set up Playwright infrastructure
- [ ] Implement UI automation tests
- [ ] Add test flows
- [ ] Create test utilities
- [ ] Add visual testing

### **4.3 Week 5-6: Test Infrastructure**
- [ ] Implement test data management
- [ ] Add test parallelization
- [ ] Create test reporting system
- [ ] Add test analytics
- [ ] Implement test isolation
- [ ] Add performance monitoring

### **4.4 Week 7-8: Quality Gates & CI/CD**
- [ ] Implement quality gates
- [ ] Add CI/CD integration
- [ ] Create performance tests
- [ ] Add test automation
- [ ] Implement test monitoring
- [ ] Create test documentation

---

## **5. SUCCESS METRICS**

### **5.1 Quality Metrics**
- **Test Coverage:** >90% for all layers
- **Test Pass Rate:** >95% consistency
- **Test Execution Time:** <5 minutes for full suite
- **Test Reliability:** <1% flaky tests

### **5.2 Performance Metrics**
- **API Response Time:** <100ms for 95th percentile
- **E2E Test Time:** <3 minutes for full flow
- **Load Testing:** 100+ concurrent users
- **Memory Usage:** <1GB for test execution

### **5.3 Automation Metrics**
- **CI/CD Success Rate:** >95%
- **Test Automation:** 100% automated
- **Quality Gate Pass Rate:** >90%
- **Test Report Generation:** 100%

---

## **6. RISK MITIGATION**

### **6.1 Technical Risks**
1. **Test Flakiness** - Implement flakiness detection
2. **Test Performance** - Optimize test execution
3. **Test Environment** - Use containers for isolation
4. **Test Data** - Implement data management

### **6.2 Process Risks**
1. **CI/CD Failures** - Implement retry logic
2. **Quality Gate Failures** - Clear violation messages
3. **Test Maintenance** - Automated test updates
4. **Team Adoption** - Training and documentation

---

## **7. NEXT STEPS**

### **7.1 Immediate Actions (This Week)**
1. **Setup Test Infrastructure** - Install test frameworks
2. **Create System Tests** - Start with API tests
3. **Setup Test Containers** - Docker integration
4. **Add Test Data Builders** - Data management

### **7.2 Short-term Goals (2 Weeks)**
1. **Complete System Tests** - Full API coverage
2. **Implement E2E Tests** - UI automation
3. **Add Test Reporting** - Comprehensive reports
4. **Setup CI/CD** - Automated pipeline

### **7.3 Long-term Goals (2 Months)**
1. **Complete All Tests** - Full test pyramid
2. **Achieve Quality Gates** - Automated quality checks
3. **Performance Testing** - Load and stress testing
4. **Test Analytics** - Test insights and metrics

---

## **8. SUMMARY**

### **8.1 Current State**
- **2 test layers** (Unit, Integration) only
- **14/14 tests passing** but missing System & E2E
- **Basic test infrastructure** with limited automation
- **No quality gates** or comprehensive reporting

### **8.2 Target State**
- **4 test layers** (Unit, Integration, System, E2E)
- **Comprehensive test coverage** with quality gates
- **Automated test pipeline** with CI/CD integration
- **Performance testing** and analytics

### **8.3 Implementation Strategy**
- **8-week phased approach** with clear milestones
- **Test-first approach** with TDD principles
- **Quality focus** with automated quality gates
- **Continuous improvement** with test analytics

**Status:** Test module có solid foundation but missing critical System & E2E layers. C?n complete test pyramid v?i automation and quality gates.
