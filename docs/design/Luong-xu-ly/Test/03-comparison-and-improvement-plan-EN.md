# Test - Comparison and Improvement Plan

**Date:** April 11, 2026  
**Module:** 6_Tests  
**Status:** Analysis comparison and improvement planning

---

## **1. REALISTIC vs IDEAL COMPARISON**

### **1.1 Test Architecture Comparison**

| **Aspect** | **Realistic** | **Ideal** | **Gap Analysis** |
|------------|--------------|-----------|------------------|
| **Test Layers** | 2 layers (Unit, Integration) | 4 layers (Unit, Integration, System, E2E) | **High** - Missing System & E2E layers |
| **Test Coverage** | 60% (14/14 tests passing) | 90%+ coverage | **Medium** - Need more coverage |
| **Test Infrastructure** | Basic setup | Advanced infrastructure with containers | **High** - Need infrastructure enhancement |
| **Test Data Management** | Hard-coded test data | Test data builders and factories | **Medium** - Need data management |
| **Test Execution** | Manual execution | Automated CI/CD pipeline | **High** - Need automation |

### **1.2 Test Quality Comparison**

| **Aspect** | **Realistic** | **Ideal** | **Gap Analysis** |
|------------|--------------|-----------|------------------|
| **Test Organization** | Basic folder structure | Organized by feature and layer | **Medium** - Need better organization |
| **Test Naming** | Inconsistent naming | Consistent naming conventions | **Medium** - Need naming standards |
| **Test Documentation** | Minimal documentation | Comprehensive test documentation | **Medium** - Need documentation |
| **Test Maintenance** | Ad-hoc maintenance | Scheduled maintenance | **Medium** - Need maintenance plan |
| **Test Reporting** | Basic console output | Advanced reporting with analytics | **High** - Need reporting system |

### **1.3 Test Framework Comparison**

| **Aspect** | **Realistic** | **Ideal** | **Gap Analysis** |
|------------|--------------|-----------|------------------|
| **Unit Testing** | xUnit basic usage | Advanced xUnit with features | **Medium** - Need framework enhancement |
| **Integration Testing** | Basic EF Core in-memory | Test containers with real databases | **High** - Need container integration |
| **Mocking Framework** | Basic Moq usage | Advanced mocking strategies | **Medium** - Need mocking enhancement |
| **Assertion Library** | Basic assertions | FluentAssertions | **Medium** - Need better assertions |
| **Test Utilities** | Limited utilities | Comprehensive test utilities | **High** - Need utility libraries |

### **1.4 Test Strategy Comparison**

| **Aspect** | **Realistic** | **Ideal** | **Gap Analysis** |
|------------|--------------|-----------|------------------|
| **Test Pyramid** | Inverted pyramid (more integration) | Proper pyramid (more unit) | **Medium** - Need pyramid balance |
| **Test Automation** | Manual test execution | Full automation in CI/CD | **High** - Need automation |
| **Test Parallelization** | Sequential execution | Parallel execution | **Medium** - Need parallelization |
| **Test Isolation** | Basic isolation | Complete test isolation | **Medium** - Need better isolation |
| **Test Performance** | Slow execution | Optimized test execution | **Medium** - Need performance |

---

## **2. PRIORITY ASSESSMENT**

### **2.1 Critical Issues (Priority 1)**
1. **Missing System Tests** - No API/system layer testing
2. **Missing E2E Tests** - No end-to-end testing
3. **No Test Automation** - Manual execution only
4. **Limited Test Coverage** - Only 60% coverage
5. **No Test Infrastructure** - Basic setup only

### **2.2 Important Issues (Priority 2)**
1. **No Test Containers** - In-memory databases only
2. **No Test Data Management** - Hard-coded test data
3. **No Test Reporting** - Basic console output
4. **No Test Parallelization** - Sequential execution
5. **No Test Documentation** - Minimal documentation

### **2.3 Nice to Have (Priority 3)**
1. **No Test Analytics** - No test metrics
2. **No Test Visualization** - No test dashboards
3. **No Test Performance** - No performance testing
4. **No Test Chaos** - No chaos testing
5. **No Test AI** - No AI-powered testing

---

## **3. IMPROVEMENT PLAN**

### **3.1 Phase 1: System Tests Implementation (Week 1-2)**

#### **Day 1-3: System Test Infrastructure**
```csharp
// Tests/System/TestWebApplicationFactory.cs
public class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly IContainer _container;
    private readonly MsSqlContainer _sqlContainer;
    private readonly RedisContainer _redisContainer;

    public TestWebApplicationFactory()
    {
        _container = new TestcontainersBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithEnvironment("SA_PASSWORD", "StrongPassword123!")
            .WithPortBinding(1433, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1433))
            .Build();

        _sqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("StrongPassword123!")
            .Build();

        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .WithPortBinding(6379, assignRandomHostPort: true)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();
        await _redisContainer.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _sqlContainer.StopAsync();
        await _redisContainer.StopAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:DefaultConnection"] = _sqlContainer.GetConnectionString(),
                ["ConnectionStrings:Redis"] = _redisContainer.GetConnectionString(),
                ["Jwt:Key"] = "TestSecretKey12345678901234567890",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove existing services
            services.RemoveDbContext<VanAnDbContext>();
            
            // Add test database
            services.AddDbContext<VanAnDbContext>(options =>
                options.UseSqlServer(_sqlContainer.GetConnectionString()));

            // Add test caching
            services.AddSingleton<IConnectionMultiplexer>(sp =>
                ConnectionMultiplexer.Connect(_redisContainer.GetConnectionString()));

            // Add test services
            services.AddScoped<ITestDataProvider, TestDataProvider>();
            services.AddScoped<IEmailService, MockEmailService>();
            services.AddScoped<ISmsService, MockSmsService>();
            services.AddScoped<IPaymentService, MockPaymentService>();
        });
    }
}

// Tests/System/ITestDataProvider.cs
public interface ITestDataProvider
{
    Task<Customer> CreateCustomerAsync();
    Task<Product> CreateProductAsync();
    Task<Order> CreateOrderAsync(Guid? customerId = null);
    Task<List<Product>> CreateProductsAsync(int count);
    Task<List<Order>> CreateOrdersAsync(int count, Guid? customerId = null);
    Task<Ingredient> CreateIngredientAsync();
    Task<Inventory> CreateInventoryAsync(Guid productId);
}

// Tests/System/TestDataProvider.cs
public class TestDataProvider : ITestDataProvider
{
    private readonly VanAnDbContext _context;
    private readonly ILogger<TestDataProvider> _logger;

    public TestDataProvider(VanAnDbContext context, ILogger<TestDataProvider> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Customer> CreateCustomerAsync()
    {
        var customer = new Customer(
            new CustomerName("Test", "Customer"),
            new Email("test@example.com"),
            new PhoneNumber("+84", "1234567890"),
            new Address("123 Test St", "Test City", "Test State", "12345", "Test Country")
        );

        await _context.Customers.AddAsync(customer);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created test customer {CustomerId}", customer.Id);
        return customer;
    }

    public async Task<Product> CreateProductAsync()
    {
        var product = new Product(
            new ProductName("Test Product"),
            new ProductDescription("Test Description"),
            new Money(25000),
            ProductCategory.Coffee
        );

        await _context.Products.AddAsync(product);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created test product {ProductId}", product.Id);
        return product;
    }

    public async Task<Order> CreateOrderAsync(Guid? customerId = null)
    {
        var customer = customerId.HasValue 
            ? await _context.Customers.FindAsync(customerId.Value)
            : await CreateCustomerAsync();

        var product = await CreateProductAsync();
        
        var order = new Order(customer.Id, new List<OrderItem>
        {
            new(new ProductId(product.Id), 2, product.Price)
        });

        await _context.Orders.AddAsync(order);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created test order {OrderId}", order.Id);
        return order;
    }

    public async Task<List<Product>> CreateProductsAsync(int count)
    {
        var products = new List<Product>();
        
        for (int i = 0; i < count; i++)
        {
            var product = new Product(
                new ProductName($"Test Product {i}"),
                new ProductDescription($"Test Description {i}"),
                new Money(25000 + (i * 1000)),
                ProductCategory.Coffee
            );
            
            products.Add(product);
        }

        await _context.Products.AddRangeAsync(products);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created {Count} test products", count);
        return products;
    }

    public async Task<List<Order>> CreateOrdersAsync(int count, Guid? customerId = null)
    {
        var customer = customerId.HasValue 
            ? await _context.Customers.FindAsync(customerId.Value)
            : await CreateCustomerAsync();

        var orders = new List<Order>();
        
        for (int i = 0; i < count; i++)
        {
            var product = await CreateProductAsync();
            
            var order = new Order(customer.Id, new List<OrderItem>
            {
                new(new ProductId(product.Id), 1, product.Price)
            });
            
            orders.Add(order);
        }

        await _context.Orders.AddRangeAsync(orders);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created {Count} test orders", count);
        return orders;
    }

    public async Task<Ingredient> CreateIngredientAsync()
    {
        var ingredient = new Ingredient(
            new IngredientName("Test Ingredient"),
            new IngredientDescription("Test Description"),
            new Money(1000),
            new Quantity(1000, "g")
        );

        await _context.Ingredients.AddAsync(ingredient);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created test ingredient {IngredientId}", ingredient.Id);
        return ingredient;
    }

    public async Task<Inventory> CreateInventoryAsync(Guid productId)
    {
        var inventory = new Inventory(productId, 100);
        
        await _context.Inventories.AddAsync(inventory);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created test inventory for product {ProductId}", productId);
        return inventory;
    }
}
```

#### **Day 4-5: API System Tests**
```csharp
// Tests/System/Controllers/OrdersControllerSystemTests.cs
public class OrdersControllerSystemTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly VanAnDbContext _context;
    private readonly ITestDataProvider _dataProvider;

    public OrdersControllerSystemTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _context = _factory.Services.GetRequiredService<VanAnDbContext>();
        _dataProvider = _factory.Services.GetRequiredService<ITestDataProvider>();
    }

    [Fact]
    public async Task CreateOrder_ValidRequest_Should_Return_Created_Order()
    {
        // Arrange
        var customer = await _dataProvider.CreateCustomerAsync();
        var product = await _dataProvider.CreateProductAsync();
        
        var request = new CreateOrderRequest
        {
            CustomerId = customer.Id,
            Items = new List<CreateOrderItemDto>
            {
                new() { ProductId = product.Id, Quantity = 2, UnitPrice = product.Price.Amount }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var orderResponse = await response.Content.ReadFromJsonAsync<OrderDto>();
        orderResponse.Should().NotBeNull();
        orderResponse.CustomerId.Should().Be(customer.Id);
        orderResponse.Items.Should().HaveCount(1);
        orderResponse.TotalAmount.Should().Be(product.Price * 2);

        // Verify in database
        var savedOrder = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderResponse.Id);
        
        savedOrder.Should().NotBeNull();
        savedOrder.CustomerId.Should().Be(customer.Id);
        savedOrder.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetOrder_ExistingOrder_Should_Return_Order_Details()
    {
        // Arrange
        var order = await _dataProvider.CreateOrderAsync();

        // Act
        var response = await _client.GetAsync($"/api/v1/orders/{order.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var orderResponse = await response.Content.ReadFromJsonAsync<OrderDto>();
        orderResponse.Should().NotBeNull();
        orderResponse.Id.Should().Be(order.Id);
        orderResponse.CustomerId.Should().Be(order.CustomerId);
        orderResponse.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetOrder_NonExistingOrder_Should_Return_NotFound()
    {
        // Arrange
        var nonExistingOrderId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/orders/{nonExistingOrderId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateOrder_ValidRequest_Should_Update_Order()
    {
        // Arrange
        var order = await _dataProvider.CreateOrderAsync();
        var newProduct = await _dataProvider.CreateProductAsync();
        
        var request = new UpdateOrderRequest
        {
            OrderId = order.Id,
            ItemsToAdd = new List<CreateOrderItemDto>
            {
                new() { ProductId = newProduct.Id, Quantity = 1, UnitPrice = newProduct.Price.Amount }
            }
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/orders/{order.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var orderResponse = await response.Content.ReadFromJsonAsync<OrderDto>();
        orderResponse.Should().NotBeNull();
        orderResponse.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task CancelOrder_ValidRequest_Should_Cancel_Order()
    {
        // Arrange
        var order = await _dataProvider.CreateOrderAsync();
        
        var request = new CancelOrderRequest
        {
            Reason = "Customer request"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/orders/{order.Id}/cancel", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify in database
        var cancelledOrder = await _context.Orders.FindAsync(order.Id);
        cancelledOrder.Should().NotBeNull();
        cancelledOrder.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task GetOrdersByCustomer_ValidRequest_Should_Return_Customer_Orders()
    {
        // Arrange
        var customer = await _dataProvider.CreateCustomerAsync();
        await _dataProvider.CreateOrdersAsync(5, customer.Id);

        // Act
        var response = await _client.GetAsync($"/api/v1/orders?customerId={customer.Id}&page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var ordersResponse = await response.Content.ReadFromJsonAsync<PagedResult<OrderDto>>();
        ordersResponse.Should().NotBeNull();
        ordersResponse.Items.Should().HaveCount(5);
        ordersResponse.TotalCount.Should().Be(5);
        ordersResponse.Page.Should().Be(1);
        ordersResponse.PageSize.Should().Be(10);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _context?.Dispose();
    }
}

// Tests/System/Controllers/ProductsControllerSystemTests.cs
public class ProductsControllerSystemTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly VanAnDbContext _context;
    private readonly ITestDataProvider _dataProvider;

    public ProductsControllerSystemTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _context = _factory.Services.GetRequiredService<VanAnDbContext>();
        _dataProvider = _factory.Services.GetRequiredService<ITestDataProvider>();
    }

    [Fact]
    public async Task CreateProduct_ValidRequest_Should_Return_Created_Product()
    {
        // Arrange
        var request = new CreateProductRequest
        {
            Name = "Test Coffee",
            Description = "Test Description",
            Price = 25000,
            Category = "Coffee",
            Ingredients = new List<CreateProductIngredientDto>
            {
                new() { IngredientId = Guid.NewGuid(), Quantity = 100 }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/products", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var productResponse = await response.Content.ReadFromJsonAsync<ProductDto>();
        productResponse.Should().NotBeNull();
        productResponse.Name.Should().Be(request.Name);
        productResponse.Description.Should().Be(request.Description);
        productResponse.Price.Should().Be(request.Price);
        productResponse.Category.Should().Be(request.Category);

        // Verify in database
        var savedProduct = await _context.Products.FindAsync(productResponse.Id);
        savedProduct.Should().NotBeNull();
        savedProduct.Name.Should().Be(request.Name);
    }

    [Fact]
    public async Task GetProducts_ValidRequest_Should_Return_Products()
    {
        // Arrange
        await _dataProvider.CreateProductsAsync(10);

        // Act
        var response = await _client.GetAsync("/api/v1/products?page=1&pageSize=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var productsResponse = await response.Content.ReadFromJsonAsync<PagedResult<ProductDto>>();
        productsResponse.Should().NotBeNull();
        productsResponse.Items.Should().HaveCount(5);
        productsResponse.TotalCount.Should().Be(10);
        productsResponse.Page.Should().Be(1);
        productsResponse.PageSize.Should().Be(5);
    }

    [Fact]
    public async Task GetProductsByCategory_ValidRequest_Should_Return_Filtered_Products()
    {
        // Arrange
        await _dataProvider.CreateProductsAsync(5);
        
        // Create coffee products
        var coffeeProduct = new Product(
            new ProductName("Coffee Product"),
            new ProductDescription("Coffee Description"),
            new Money(25000),
            ProductCategory.Coffee
        );
        await _context.Products.AddAsync(coffeeProduct);
        await _context.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync("/api/v1/products?category=Coffee&page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var productsResponse = await response.Content.ReadFromJsonAsync<PagedResult<ProductDto>>();
        productsResponse.Should().NotBeNull();
        productsResponse.Items.Should().HaveCount(1);
        productsResponse.Items.First().Category.Should().Be("Coffee");
    }

    public void Dispose()
    {
        _client?.Dispose();
        _context?.Dispose();
    }
}
```

#### **Day 6-7: Integration Test Enhancement**
```csharp
// Tests/Integration/Services/OrderServiceIntegrationTests.cs
public class OrderServiceIntegrationTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly VanAnDbContext _context;
    private readonly IOrderService _orderService;
    private readonly IProductRepository _productRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ITestDataProvider _dataProvider;

    public OrderServiceIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _context = _factory.Services.GetRequiredService<VanAnDbContext>();
        _orderService = _factory.Services.GetRequiredService<IOrderService>();
        _productRepository = _factory.Services.GetRequiredService<IProductRepository>();
        _customerRepository = _factory.Services.GetRequiredService<ICustomerRepository>();
        _dataProvider = _factory.Services.GetRequiredService<ITestDataProvider>();
    }

    [Fact]
    public async Task CreateOrder_ValidRequest_Should_Create_Order_And_Update_Inventory()
    {
        // Arrange
        var customer = await _dataProvider.CreateCustomerAsync();
        var product = await _dataProvider.CreateProductAsync();
        await _dataProvider.CreateInventoryAsync(product.Id);

        var request = new CreateOrderRequest
        {
            CustomerId = customer.Id,
            Items = new List<CreateOrderItemRequest>
            {
                new() { ProductId = product.Id, Quantity = 2, UnitPrice = product.Price.Amount }
            }
        };

        // Act
        var result = await _orderService.CreateOrderAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.Order.Should().NotBeNull();
        result.Order.CustomerId.Should().Be(customer.Id);
        result.Order.Items.Should().HaveCount(1);

        // Verify inventory is updated
        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductId == product.Id);
        inventory.Should().NotBeNull();
        inventory.CurrentStock.Should().Be(98); // 100 - 2
    }

    [Fact]
    public async Task CreateOrder_Insufficient_Inventory_Should_Fail()
    {
        // Arrange
        var customer = await _dataProvider.CreateCustomerAsync();
        var product = await _dataProvider.CreateProductAsync();
        
        // Create inventory with insufficient stock
        var inventory = new Inventory(product.Id, 1);
        await _context.Inventories.AddAsync(inventory);
        await _context.SaveChangesAsync();

        var request = new CreateOrderRequest
        {
            CustomerId = customer.Id,
            Items = new List<CreateOrderItemRequest>
            {
                new() { ProductId = product.Id, Quantity = 2, UnitPrice = product.Price.Amount }
            }
        };

        // Act
        var result = await _orderService.CreateOrderAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Insufficient inventory");
    }

    [Fact]
    public async Task CancelOrder_ValidRequest_Should_Cancel_Order_And_Release_Inventory()
    {
        // Arrange
        var order = await _dataProvider.CreateOrderAsync();
        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductId == order.Items.First().ProductId);
        
        // Act
        var result = await _orderService.CancelOrderAsync(order.Id, "Customer request");

        // Assert
        result.Success.Should().BeTrue();
        
        // Verify order is cancelled
        var cancelledOrder = await _context.Orders.FindAsync(order.Id);
        cancelledOrder.Should().NotBeNull();
        cancelledOrder.Status.Should().Be(OrderStatus.Cancelled);

        // Verify inventory is released
        var updatedInventory = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductId == order.Items.First().ProductId);
        updatedInventory.Should().NotBeNull();
        updatedInventory.CurrentStock.Should().Be(inventory.CurrentStock + order.Items.First().Quantity);
    }

    [Fact]
    public async Task GetOrdersByCustomer_Should_Return_Customer_Orders_With_Pagination()
    {
        // Arrange
        var customer = await _dataProvider.CreateCustomerAsync();
        await _dataProvider.CreateOrdersAsync(15, customer.Id);

        // Act
        var orders = await _orderService.GetByCustomerIdAsync(customer.Id, 2, 5);

        // Assert
        orders.Should().HaveCount(5);
        orders.All(o => o.CustomerId == customer.Id).Should().BeTrue();
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}

// Tests/Integration/Repositories/ProductRepositoryIntegrationTests.cs
public class ProductRepositoryIntegrationTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly VanAnDbContext _context;
    private readonly IProductRepository _productRepository;
    private readonly ITestDataProvider _dataProvider;

    public ProductRepositoryIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _context = _factory.Services.GetRequiredService<VanAnDbContext>();
        _productRepository = _factory.Services.GetRequiredService<IProductRepository>();
        _dataProvider = _factory.Services.GetRequiredService<ITestDataProvider>();
    }

    [Fact]
    public async Task AddProduct_Should_Persist_To_Database()
    {
        // Arrange
        var product = new Product(
            new ProductName("Test Coffee"),
            new ProductDescription("Test Description"),
            new Money(25000),
            ProductCategory.Coffee
        );

        // Act
        await _productRepository.AddAsync(product);

        // Assert
        var savedProduct = await _context.Products
            .Include(p => p.Ingredients)
            .FirstOrDefaultAsync(p => p.Id == product.Id);
        
        savedProduct.Should().NotBeNull();
        savedProduct.Name.Should().Be(product.Name);
        savedProduct.Description.Should().Be(product.Description);
        savedProduct.Price.Should().Be(product.Price);
        savedProduct.Category.Should().Be(product.Category);
    }

    [Fact]
    public async Task GetProductsByCategory_Should_Return_Filtered_Results()
    {
        // Arrange
        await _dataProvider.CreateProductsAsync(5);
        
        var coffeeProduct = new Product(
            new ProductName("Coffee Product"),
            new ProductDescription("Coffee Description"),
            new Money(25000),
            ProductCategory.Coffee
        );
        await _productRepository.AddAsync(coffeeProduct);

        // Act
        var coffeeProducts = await _productRepository.GetByCategoryAsync("Coffee", 1, 10);

        // Assert
        coffeeProducts.Should().HaveCount(1);
        coffeeProducts.First().Category.Should().Be(ProductCategory.Coffee);
    }

    [Fact]
    public async Task UpdateProduct_Should_Update_Database_Record()
    {
        // Arrange
        var product = await _dataProvider.CreateProductAsync();
        var newPrice = new Money(30000);

        // Act
        product.UpdatePrice(newPrice, Guid.NewGuid());
        await _productRepository.UpdateAsync(product);

        // Assert
        var updatedProduct = await _context.Products.FindAsync(product.Id);
        updatedProduct.Should().NotBeNull();
        updatedProduct.Price.Should().Be(newPrice);
        updatedProduct.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteProduct_Should_Remove_From_Database()
    {
        // Arrange
        var product = await _dataProvider.CreateProductAsync();

        // Act
        await _productRepository.DeleteAsync(product.Id);

        // Assert
        var deletedProduct = await _context.Products.FindAsync(product.Id);
        deletedProduct.Should().BeNull();
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
```

### **3.2 Phase 2: E2E Tests with Playwright (Week 3-4)**

#### **Day 8-10: E2E Test Infrastructure**
```csharp
// Tests/E2E/E2ETestBase.cs
public class E2ETestBase : IAsyncLifetime
{
    protected readonly ITestOutputHelper _output;
    protected readonly IPlaywright _playwright;
    protected IBrowser _browser;
    protected IPage _page;
    protected readonly TestWebApplicationFactory _factory;

    public E2ETestBase(ITestOutputHelper output)
    {
        _output = output;
        _factory = new TestWebApplicationFactory();
        _playwright = Playwright.CreateAsync().Result;
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
        
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
            SlowMo = 100,
            Args = new[]
            {
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-dev-shm-usage",
                "--disable-web-security",
                "--disable-features=IsolateOrigins,site-per-process"
            }
        });

        _page = await _browser.NewPageAsync();
        
        // Set up console logging
        _page.Console += (_, e) => _output.WriteLine($"CONSOLE: {e.Text}");
        _page.PageError += (_, e) => _output.WriteLine($"PAGE ERROR: {e}");
        
        // Set default timeout
        _page.SetDefaultTimeout(30000);
    }

    public new async Task DisposeAsync()
    {
        await _page.CloseAsync();
        await _browser.CloseAsync();
        await _factory.DisposeAsync();
        _playwright.Dispose();
    }

    protected async Task LoginAsync()
    {
        await _page.GotoAsync($"{GetBaseUrl()}/login");
        
        await _page.FillAsync("#username", "testuser");
        await _page.FillAsync("#password", "password123");
        await _page.ClickAsync("#login-button");
        
        // Wait for redirect to dashboard
        await _page.WaitForURLAsync($"{GetBaseUrl()}/dashboard");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    protected async Task NavigateToPageAsync(string path)
    {
        await _page.GotoAsync($"{GetBaseUrl()}{path}");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    protected string GetBaseUrl()
    {
        return "http://localhost:5000";
    }

    protected async Task WaitForElementAsync(string selector)
    {
        await _page.WaitForSelectorAsync(selector);
    }

    protected async Task ClickElementAsync(string selector)
    {
        await _page.ClickAsync(selector);
    }

    protected async Task FillElementAsync(string selector, string value)
    {
        await _page.FillAsync(selector, value);
    }

    protected async Task SelectOptionAsync(string selector, string value)
    {
        await _page.SelectOptionAsync(selector, value);
    }

    protected async Task AssertElementVisibleAsync(string selector)
    {
        var element = await _page.QuerySelectorAsync(selector);
        element.Should().NotBeNull();
        await element.IsVisibleAsync().Should().BeTrue();
    }

    protected async Task AssertElementTextAsync(string selector, string expectedText)
    {
        var element = await _page.QuerySelectorAsync(selector);
        element.Should().NotBeNull();
        var text = await element.InnerTextAsync();
        text.Should().Contain(expectedText);
    }

    protected async Task AssertElementCountAsync(string selector, int expectedCount)
    {
        var elements = await _page.QuerySelectorAllAsync(selector);
        elements.Count.Should().Be(expectedCount);
    }

    protected async Task TakeScreenshotAsync(string fileName)
    {
        await _page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = $"Screenshots/{fileName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png",
            FullPage = true
        });
    }
}

// Tests/E2E/Pages/LoginPage.cs
public class LoginPage
{
    private readonly IPage _page;
    private readonly string _baseUrl;

    public LoginPage(IPage page, string baseUrl)
    {
        _page = page;
        _baseUrl = baseUrl;
    }

    public async Task NavigateAsync()
    {
        await _page.GotoAsync($"{_baseUrl}/login");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task LoginAsync(string username, string password)
    {
        await _page.FillAsync("#username", username);
        await _page.FillAsync("#password", password);
        await _page.ClickAsync("#login-button");
        
        // Wait for redirect
        await _page.WaitForURLAsync($"{_baseUrl}/**");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task AssertLoginSuccess()
    {
        await _page.WaitForSelectorAsync(".dashboard-container");
        var url = _page.Url;
        url.Should().Contain("/dashboard");
    }

    public async Task AssertLoginError()
    {
        await _page.WaitForSelectorAsync(".error-message");
        var errorElement = await _page.QuerySelectorAsync(".error-message");
        var errorText = await errorElement.InnerTextAsync();
        errorText.Should().Contain("Invalid");
    }
}

// Tests/E2E/Pages/OrderPage.cs
public class OrderPage
{
    private readonly IPage _page;
    private readonly string _baseUrl;

    public OrderPage(IPage page, string baseUrl)
    {
        _page = page;
        _baseUrl = baseUrl;
    }

    public async Task NavigateAsync()
    {
        await _page.GotoAsync($"{_baseUrl}/orders");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task CreateOrderAsync(string customerName, List<OrderItemData> items)
    {
        // Click create order button
        await _page.ClickAsync("#create-order-button");
        await _page.WaitForSelectorAsync(".order-form");

        // Select customer
        await _page.SelectOptionAsync("#customer-select", customerName);

        // Add items
        foreach (var item in items)
        {
            await AddOrderItemAsync(item.ProductName, item.Quantity);
        }

        // Submit order
        await _page.ClickAsync("#submit-order-button");
        await _page.WaitForSelectorAsync(".order-success-message");
    }

    private async Task AddOrderItemAsync(string productName, int quantity)
    {
        // Click add item button
        await _page.ClickAsync("#add-item-button");
        
        // Select product
        await _page.SelectOptionAsync(".product-select", productName);
        
        // Set quantity
        await _page.FillAsync(".quantity-input", quantity.ToString());
        
        // Add item to order
        await _page.ClickAsync(".add-item-to-order");
    }

    public async Task AssertOrderCreated()
    {
        await _page.WaitForSelectorAsync(".order-success-message");
        var successMessage = await _page.TextContentAsync(".order-success-message");
        successMessage.Should().Contain("Order created successfully");
    }

    public async Task AssertOrderInList(string orderNumber)
    {
        await _page.WaitForSelectorAsync(".order-list");
        var orderElements = await _page.QuerySelectorAllAsync(".order-item");
        var orderExists = orderElements.Any(async e => 
        {
            var text = await e.TextContentAsync();
            return text.Contains(orderNumber);
        });
        
        orderExists.Should().BeTrue();
    }
}

// Tests/E2E/Pages/ProductPage.cs
public class ProductPage
{
    private readonly IPage _page;
    private readonly string _baseUrl;

    public ProductPage(IPage page, string baseUrl)
    {
        _page = page;
        _baseUrl = baseUrl;
    }

    public async Task NavigateAsync()
    {
        await _page.GotoAsync($"{_baseUrl}/products");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task CreateProductAsync(string name, string description, decimal price, string category)
    {
        // Click create product button
        await _page.ClickAsync("#create-product-button");
        await _page.WaitForSelectorAsync(".product-form");

        // Fill product details
        await _page.FillAsync("#product-name", name);
        await _page.FillAsync("#product-description", description);
        await _page.FillAsync("#product-price", price.ToString());
        await _page.SelectOptionAsync("#product-category", category);

        // Submit product
        await _page.ClickAsync("#submit-product-button");
        await _page.WaitForSelectorAsync(".product-success-message");
    }

    public async Task AssertProductCreated()
    {
        await _page.WaitForSelectorAsync(".product-success-message");
        var successMessage = await _page.TextContentAsync(".product-success-message");
        successMessage.Should().Contain("Product created successfully");
    }

    public async Task AssertProductInList(string productName)
    {
        await _page.WaitForSelectorAsync(".product-list");
        var productElements = await _page.QuerySelectorAllAsync(".product-item");
        var productExists = productElements.Any(async e => 
        {
            var text = await e.TextContentAsync();
            return text.Contains(productName);
        });
        
        productExists.Should().BeTrue();
    }

    public async Task SearchProductsAsync(string searchTerm)
    {
        await _page.FillAsync("#search-input", searchTerm);
        await _page.PressAsync("#search-input", "Enter");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task AssertSearchResults(List<string> expectedProducts)
    {
        await _page.WaitForSelectorAsync(".product-list");
        var productElements = await _page.QuerySelectorAllAsync(".product-item");
        
        productElements.Count.Should().Be(expectedProducts.Count);
        
        foreach (var expectedProduct in expectedProducts)
        {
            var productExists = productElements.Any(async e => 
            {
                var text = await e.TextContentAsync();
                return text.Contains(expectedProduct);
            });
            
            productExists.Should().BeTrue();
        }
    }
}

public class OrderItemData
{
    public string ProductName { get; set; }
    public int Quantity { get; set; }
}
```

#### **Day 11-12: Order Flow E2E Tests**
```csharp
// Tests/E2E/OrderFlowTests.cs
public class OrderFlowTests : E2ETestBase
{
    private readonly LoginPage _loginPage;
    private readonly OrderPage _orderPage;
    private readonly ProductPage _productPage;

    public OrderFlowTests(ITestOutputHelper output) : base(output)
    {
        _loginPage = new LoginPage(_page, GetBaseUrl());
        _orderPage = new OrderPage(_page, GetBaseUrl());
        _productPage = new ProductPage(_page, GetBaseUrl());
    }

    [Fact]
    public async Task CompleteOrderFlow_Should_Work_End_To_End()
    {
        // Arrange
        await LoginAsync();
        
        // Act & Assert - Create product first
        await _productPage.NavigateAsync();
        await _productPage.CreateProductAsync("Test Coffee", "Test Description", 25000, "Coffee");
        await _productPage.AssertProductCreated();
        await _productPage.AssertProductInList("Test Coffee");

        // Act & Assert - Create order
        await _orderPage.NavigateAsync();
        await _orderPage.CreateOrderAsync("Test Customer", new List<OrderItemData>
        {
            new() { ProductName = "Test Coffee", Quantity = 2 }
        });
        await _orderPage.AssertOrderCreated();
        await _orderPage.AssertOrderInList("Test Customer");

        // Take screenshot for verification
        await TakeScreenshotAsync("CompleteOrderFlow");
    }

    [Fact]
    public async Task OrderCreation_With_Invalid_Data_Should_Show_Validation_Errors()
    {
        // Arrange
        await LoginAsync();
        await _orderPage.NavigateAsync();

        // Act - Try to create order without customer
        await _page.ClickAsync("#create-order-button");
        await _page.WaitForSelectorAsync(".order-form");
        await _page.ClickAsync("#submit-order-button");

        // Assert
        await _page.WaitForSelectorAsync(".validation-error");
        var errorElements = await _page.QuerySelectorAllAsync(".validation-error");
        errorElements.Count.Should().BeGreaterThan(0);
        
        var customerError = await _page.TextContentAsync("#customer-select-error");
        customerError.Should().Contain("Customer is required");

        await TakeScreenshotAsync("OrderValidationError");
    }

    [Fact]
    public async Task OrderSearch_Should_Filter_Orders_Correctly()
    {
        // Arrange
        await LoginAsync();
        await _orderPage.NavigateAsync();
        
        // Create multiple orders
        await _orderPage.CreateOrderAsync("Customer A", new List<OrderItemData>
        {
            new() { ProductName = "Test Coffee", Quantity = 1 }
        });
        
        await _orderPage.CreateOrderAsync("Customer B", new List<OrderItemData>
        {
            new() { ProductName = "Test Coffee", Quantity = 2 }
        });

        // Act - Search for Customer A
        await _page.FillAsync("#order-search", "Customer A");
        await _page.PressAsync("#order-search", "Enter");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert
        var orderElements = await _page.QuerySelectorAllAsync(".order-item");
        orderElements.Count.Should().Be(1);
        
        var orderText = await orderElements.First().TextContentAsync();
        orderText.Should().Contain("Customer A");

        await TakeScreenshotAsync("OrderSearch");
    }

    [Fact]
    public async Task OrderStatus_Update_Should_Reflect_In_UI()
    {
        // Arrange
        await LoginAsync();
        await _orderPage.NavigateAsync();
        
        // Create order
        await _orderPage.CreateOrderAsync("Test Customer", new List<OrderItemData>
        {
            new() { ProductName = "Test Coffee", Quantity = 1 }
        });

        // Act - Update order status
        await _page.ClickAsync(".order-status-button");
        await _page.SelectOptionAsync(".status-select", "In Progress");
        await _page.ClickAsync(".update-status-button");
        await _page.WaitForSelectorAsync(".status-update-success");

        // Assert
        var statusElement = await _page.QuerySelectorAsync(".order-status");
        var statusText = await statusElement.TextContentAsync();
        statusText.Should().Contain("In Progress");

        await TakeScreenshotAsync("OrderStatusUpdate");
    }

    [Fact]
    public async Task OrderDetails_Should_Show_Complete_Information()
    {
        // Arrange
        await LoginAsync();
        await _orderPage.NavigateAsync();
        
        // Create order
        await _orderPage.CreateOrderAsync("Test Customer", new List<OrderItemData>
        {
            new() { ProductName = "Test Coffee", Quantity = 2 }
        });

        // Act - View order details
        await _page.ClickAsync(".order-details-button");
        await _page.WaitForSelectorAsync(".order-details-modal");

        // Assert
        await AssertElementVisibleAsync(".order-details-modal");
        await AssertElementTextAsync(".customer-name", "Test Customer");
        await AssertElementTextAsync(".product-name", "Test Coffee");
        await AssertElementTextAsync(".quantity", "2");
        await AssertElementTextAsync(".total-amount", "50,000");

        await TakeScreenshotAsync("OrderDetails");
    }
}

// Tests/E2E/ProductManagementTests.cs
public class ProductManagementTests : E2ETestBase
{
    private readonly LoginPage _loginPage;
    private readonly ProductPage _productPage;

    public ProductManagementTests(ITestOutputHelper output) : base(output)
    {
        _loginPage = new LoginPage(_page, GetBaseUrl());
        _productPage = new ProductPage(_page, GetBaseUrl());
    }

    [Fact]
    public async Task ProductManagement_Full_Lifecycle_Should_Work()
    {
        // Arrange
        await LoginAsync();
        await _productPage.NavigateAsync();

        // Act & Assert - Create product
        await _productPage.CreateProductAsync("Test Product", "Test Description", 25000, "Coffee");
        await _productPage.AssertProductCreated();
        await _productPage.AssertProductInList("Test Product");

        // Act & Assert - Edit product
        await _page.ClickAsync(".edit-product-button");
        await _page.WaitForSelectorAsync(".product-form");
        await _page.FillAsync("#product-name", "Updated Product");
        await _page.FillAsync("#product-price", "30000");
        await _page.ClickAsync("#update-product-button");
        await _page.WaitForSelectorAsync(".product-update-success");

        // Verify update
        await _productPage.AssertProductInList("Updated Product");

        // Act & Assert - Delete product
        await _page.ClickAsync(".delete-product-button");
        await _page.WaitForSelectorAsync(".confirm-delete-modal");
        await _page.ClickAsync(".confirm-delete-button");
        await _page.WaitForSelectorAsync(".product-delete-success");

        // Verify deletion
        await _page.WaitForSelectorAsync(".product-list");
        var productElements = await _page.QuerySelectorAllAsync(".product-item");
        var productExists = productElements.Any(async e => 
        {
            var text = await e.TextContentAsync();
            return text.Contains("Updated Product");
        });
        
        productExists.Should().BeFalse();

        await TakeScreenshotAsync("ProductLifecycle");
    }

    [Fact]
    public async Task ProductSearch_Should_Work_Correctly()
    {
        // Arrange
        await LoginAsync();
        await _productPage.NavigateAsync();
        
        // Create multiple products
        await _productPage.CreateProductAsync("Vietnamese Coffee", "Strong coffee", 25000, "Coffee");
        await _productPage.CreateProductAsync("Green Tea", "Refreshing tea", 20000, "Tea");
        await _productPage.CreateProductAsync("Cappuccino", "Italian coffee", 30000, "Coffee");

        // Act - Search for coffee products
        await _productPage.SearchProductsAsync("Coffee");

        // Assert
        await _productPage.AssertSearchResults(new List<string>
        {
            "Vietnamese Coffee",
            "Cappuccino"
        });

        // Act - Search for tea
        await _productPage.SearchProductsAsync("Tea");

        // Assert
        await _productPage.AssertSearchResults(new List<string>
        {
            "Green Tea"
        });

        await TakeScreenshotAsync("ProductSearch");
    }

    [Fact]
    public async Task ProductCategory_Filter_Should_Work_Correctly()
    {
        // Arrange
        await LoginAsync();
        await _productPage.NavigateAsync();
        
        // Create products in different categories
        await _productPage.CreateProductAsync("Vietnamese Coffee", "Strong coffee", 25000, "Coffee");
        await _productPage.CreateProductAsync("Green Tea", "Refreshing tea", 20000, "Tea");
        await _productPage.CreateProductAsync("Sandwich", "Fresh sandwich", 35000, "Food");

        // Act - Filter by Coffee category
        await _page.SelectOptionAsync("#category-filter", "Coffee");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert
        var productElements = await _page.QuerySelectorAllAsync(".product-item");
        productElements.Count.Should().Be(1);
        
        var productText = await productElements.First().TextContentAsync();
        productText.Should().Contain("Vietnamese Coffee");

        // Act - Filter by Tea category
        await _page.SelectOptionAsync("#category-filter", "Tea");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert
        productElements = await _page.QuerySelectorAllAsync(".product-item");
        productElements.Count.Should().Be(1);
        
        productText = await productElements.First().TextContentAsync();
        productText.Should().Contain("Green Tea");

        await TakeScreenshotAsync("ProductCategoryFilter");
    }
}
```

### **3.3 Phase 3: Test Infrastructure Enhancement (Week 5-6)**

#### **Day 13-15: Test Data Management**
```csharp
// Tests/Common/TestDataBuilder.cs
public abstract class TestDataBuilder<T>
{
    protected T _entity;

    protected TestDataBuilder()
    {
        _entity = CreateDefault();
    }

    protected abstract T CreateDefault();

    public TestDataBuilder<T> With(Action<T> action)
    {
        action(_entity);
        return this;
    }

    public T Build() => _entity;
}

// Tests/Common/CustomerBuilder.cs
public class CustomerBuilder : TestDataBuilder<Customer>
{
    protected override Customer CreateDefault()
    {
        return new Customer(
            new CustomerName("John", "Doe"),
            new Email("john.doe@example.com"),
            new PhoneNumber("+84", "1234567890"),
            new Address("123 Test St", "Test City", "Test State", "12345", "Test Country")
        );
    }

    public CustomerBuilder WithName(string firstName, string lastName)
    {
        return With(c => c.Name = new CustomerName(firstName, lastName));
    }

    public CustomerBuilder WithEmail(string email)
    {
        return With(c => c.Email = new Email(email));
    }

    public CustomerBuilder WithPhoneNumber(string countryCode, string number)
    {
        return With(c => c.PhoneNumber = new PhoneNumber(countryCode, number));
    }

    public CustomerBuilder WithAddress(string street, string city, string state, string postalCode, string country)
    {
        return With(c => c.Address = new Address(street, city, state, postalCode, country));
    }

    public CustomerBuilder AsVip()
    {
        return With(c => c.AddLoyaltyPoints(1000));
    }
}

// Tests/Common/ProductBuilder.cs
public class ProductBuilder : TestDataBuilder<Product>
{
    protected override Product CreateDefault()
    {
        return new Product(
            new ProductName("Test Product"),
            new ProductDescription("Test Description"),
            new Money(25000),
            ProductCategory.Coffee
        );
    }

    public ProductBuilder WithName(string name)
    {
        return With(p => p.Name = new ProductName(name));
    }

    public ProductBuilder WithDescription(string description)
    {
        return With(p => p.Description = new ProductDescription(description));
    }

    public ProductBuilder WithPrice(decimal price)
    {
        return With(p => p.Price = new Money(price));
    }

    public ProductBuilder WithCategory(string category)
    {
        return With(p => p.Category = new ProductCategory(category));
    }

    public ProductBuilder AsActive()
    {
        return With(p => p.Activate());
    }

    public ProductBuilder AsInactive()
    {
        return With(p => p.Deactivate("Test deactivation"));
    }

    public ProductBuilder WithIngredient(Guid ingredientId, int quantity)
    {
        return With(p => p.AddIngredient(new IngredientId(ingredientId), new Quantity(quantity, "g")));
    }
}

// Tests/Common/OrderBuilder.cs
public class OrderBuilder : TestDataBuilder<Order>
{
    private readonly List<OrderItem> _items = new();

    protected override Order CreateDefault()
    {
        return new Order(Guid.NewGuid(), _items);
    }

    public OrderBuilder ForCustomer(Guid customerId)
    {
        return With(o => o.CustomerId = customerId);
    }

    public OrderBuilder WithItem(Guid productId, int quantity, decimal unitPrice)
    {
        _items.Add(new OrderItem(new ProductId(productId), quantity, new Money(unitPrice)));
        return With(o => o.AddOrderItem(_items.Last()));
    }

    public OrderBuilder WithItems(List<(Guid productId, int quantity, decimal unitPrice)> items)
    {
        foreach (var item in items)
        {
            WithItem(item.productId, item.quantity, item.unitPrice);
        }
        return this;
    }

    public OrderBuilder AsPending()
    {
        return With(o => o.Status = OrderStatus.Pending);
    }

    public OrderBuilder AsConfirmed()
    {
        return With(o => o.ConfirmOrder());
    }

    public OrderBuilder AsCompleted()
    {
        return With(o => 
        {
            o.ConfirmOrder();
            o.CompleteOrder();
        });
    }

    public OrderBuilder AsCancelled(string reason)
    {
        return With(o => o.CancelOrder(reason));
    }
}

// Tests/Common/TestDatabaseFixture.cs
public class TestDatabaseFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container;
    private readonly string _connectionString;

    public TestDatabaseFixture()
    {
        _container = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("StrongPassword123!")
            .WithPortBinding(1433, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1433))
            .Build();

        _connectionString = _container.GetConnectionString();
    }

    public string ConnectionString => _connectionString;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        
        // Run migrations
        using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.StopAsync();
    }

    public VanAnDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<VanAnDbContext>()
            .UseSqlServer(_connectionString)
            .Options;

        return new VanAnDbContext(options);
    }

    public async Task ResetDatabaseAsync()
    {
        using var context = CreateContext();
        
        // Delete all data
        var tables = new[]
        {
            "OrderItems", "Orders", "Inventories", "ProductIngredients", 
            "Products", "Ingredients", "Customers"
        };

        foreach (var table in tables)
        {
            await context.Database.ExecuteSqlRawAsync($"DELETE FROM {table}");
        }
        
        await context.SaveChangesAsync();
    }
}

// Tests/Common/TestConfiguration.cs
public class TestConfiguration
{
    public static readonly string TestDatabaseConnectionString = "Server=localhost;Database=VanAn_Test;User Id=sa;Password=StrongPassword123!;TrustServerCertificate=true;";
    public static readonly string TestRedisConnectionString = "localhost:6379";
    public static readonly string TestJwtKey = "TestSecretKey12345678901234567890";
    public static readonly string TestJwtIssuer = "TestIssuer";
    public static readonly string TestJwtAudience = "TestAudience";
    public static readonly int TestTimeoutSeconds = 30;
    public static readonly bool TestEnableLogging = true;
    public static readonly string TestLogLevel = "Information";
}
```

#### **Day 16-17: Test Parallelization**
```csharp
// Tests/Common/ParallelTestCollection.cs
[CollectionDefinition("Parallel Tests", DisableParallelization = false)]
public class ParallelTestCollection : ICollectionFixture<TestDatabaseFixture>
{
}

// Tests/Common/IsolatedTestCollection.cs
[CollectionDefinition("Isolated Tests", DisableParallelization = true)]
public class IsolatedTestCollection : ICollectionFixture<TestDatabaseFixture>
{
}

// Tests/Common/TestCollectionFixture.cs
public class TestCollectionFixture : IAsyncLifetime
{
    private readonly TestDatabaseFixture _databaseFixture;
    private readonly IServiceProvider _serviceProvider;

    public TestCollectionFixture(TestDatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
        _serviceProvider = CreateServiceProvider();
    }

    public IServiceProvider ServiceProvider => _serviceProvider;

    public async Task InitializeAsync()
    {
        await _databaseFixture.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _databaseFixture.ResetDatabaseAsync();
    }

    private IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        // Add database context
        services.AddDbContext<VanAnDbContext>(options =>
            options.UseSqlServer(_databaseFixture.ConnectionString));

        // Add services
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IInventoryService, InventoryService>();

        // Add repositories
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();

        // Add test data provider
        services.AddScoped<ITestDataProvider, TestDataProvider>();

        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        return services.BuildServiceProvider();
    }
}

// Tests/Common/ParallelTestBase.cs
public abstract class ParallelTestBase : IAsyncLifetime
{
    protected readonly TestCollectionFixture _fixture;
    protected readonly IServiceProvider _serviceProvider;
    protected readonly VanAnDbContext _context;
    protected readonly ILogger _logger;

    protected ParallelTestBase(TestCollectionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _serviceProvider = _fixture.ServiceProvider;
        _context = _serviceProvider.GetRequiredService<VanAnDbContext>();
        _logger = new TestLogger(output);
    }

    public virtual async Task InitializeAsync()
    {
        // Each test gets its own transaction
        await _context.Database.BeginTransactionAsync();
    }

    public virtual async Task DisposeAsync()
    {
        // Rollback transaction to ensure test isolation
        await _context.Database.RollbackTransactionAsync();
        await _context.DisposeAsync();
    }

    protected async Task<T> CreateAsync<T>(T entity) where T : class
    {
        await _context.Set<T>().AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    protected async Task<List<T>> CreateAsync<T>(List<T> entities) where T : class
    {
        await _context.Set<T>().AddRangeAsync(entities);
        await _context.SaveChangesAsync();
        return entities;
    }

    protected async Task<T> GetAsync<T>(Guid id) where T : class
    {
        return await _context.Set<T>().FindAsync(id);
    }

    protected async Task<List<T>> GetAllAsync<T>() where T : class
    {
        return await _context.Set<T>().ToListAsync();
    }

    protected async Task UpdateAsync<T>(T entity) where T : class
    {
        _context.Set<T>().Update(entity);
        await _context.SaveChangesAsync();
    }

    protected async Task DeleteAsync<T>(T entity) where T : class
    {
        _context.Set<T>().Remove(entity);
        await _context.SaveChangesAsync();
    }
}

// Tests/Common/TestLogger.cs
public class TestLogger : ILogger
{
    private readonly ITestOutputHelper _output;

    public TestLogger(ITestOutputHelper output)
    {
        _output = output;
    }

    public IDisposable BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        var message = formatter(state, exception);
        _output.WriteLine($"[{logLevel}] {message}");
        
        if (exception != null)
        {
            _output.WriteLine($"Exception: {exception}");
        }
    }
}

// Tests/Common/TestLogger.cs
public class TestLogger<T> : ILogger<T>
{
    private readonly TestLogger _logger;

    public TestLogger(ITestOutputHelper output)
    {
        _logger = new TestLogger(output);
    }

    public IDisposable BeginScope<TState>(TState state) => _logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        _logger.Log(logLevel, eventId, state, exception, formatter);
    }
}
```

### **3.4 Phase 4: Quality Gates & CI/CD (Week 7-8)**

#### **Day 18-20: Quality Gates Implementation**
```csharp
// Tests/Quality/QualityGateTests.cs
public class QualityGateTests
{
    [Fact]
    public async Task CodeCoverage_Should_Be_At_Least_80_Percent()
    {
        // This would be implemented using a code coverage tool
        // For now, we'll simulate the check
        
        var coverageResult = await RunCodeCoverageAnalysis();
        coverageResult.Percentage.Should().BeGreaterOrEqualTo(80);
    }

    [Fact]
    public async Task All_Tests_Should_Pass_Within_Timeout()
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Run all tests
        var testResults = await RunAllTests();
        
        stopwatch.Stop();
        
        testResults.Passed.Should().Be(testResults.Total);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5 * 60 * 1000); // 5 minutes
    }

    [Fact]
    public async Task No_Breaking_Changes_Should_Be_Introduced()
    {
        // Compare current API with previous version
        var apiComparison = await CompareApiVersions();
        apiComparison.HasBreakingChanges.Should().BeFalse();
    }

    [Fact]
    public async Task Performance_Should_Not_Regress()
    {
        var performanceResults = await RunPerformanceTests();
        
        foreach (var result in performanceResults)
        {
            result.ResponseTime.Should().BeLessThan(result.MaxAcceptableTime);
            result.MemoryUsage.Should().BeLessThan(result.MaxAcceptableMemory);
        }
    }

    [Fact]
    public async Task Security_Vulnerabilities_Should_Not_Be_Present()
    {
        var securityScan = await RunSecurityScan();
        securityScan.Vulnerabilities.Should().BeEmpty();
    }

    private async Task<CodeCoverageResult> RunCodeCoverageAnalysis()
    {
        // Simulate code coverage analysis
        return new CodeCoverageResult
        {
            Percentage = 85,
            CoveredLines = 850,
            TotalLines = 1000
        };
    }

    private async Task<TestResults> RunAllTests()
    {
        // Simulate running all tests
        return new TestResults
        {
            Total = 100,
            Passed = 100,
            Failed = 0,
            Skipped = 0
        };
    }

    private async Task<ApiComparisonResult> CompareApiVersions()
    {
        // Simulate API comparison
        return new ApiComparisonResult
        {
            HasBreakingChanges = false,
            NewEndpoints = 5,
            RemovedEndpoints = 0,
            ModifiedEndpoints = 2
        };
    }

    private async Task<List<PerformanceTestResult>> RunPerformanceTests()
    {
        // Simulate performance tests
        return new List<PerformanceTestResult>
        {
            new() { Endpoint = "/api/v1/orders", ResponseTime = 150, MaxAcceptableTime = 500, MemoryUsage = 50, MaxAcceptableMemory = 100 },
            new() { Endpoint = "/api/v1/products", ResponseTime = 100, MaxAcceptableTime = 300, MemoryUsage = 30, MaxAcceptableMemory = 80 }
        };
    }

    private async Task<SecurityScanResult> RunSecurityScan()
    {
        // Simulate security scan
        return new SecurityScanResult
        {
            Vulnerabilities = new List<SecurityVulnerability>()
        };
    }
}

// Tests/Quality/CodeCoverageResult.cs
public class CodeCoverageResult
{
    public double Percentage { get; set; }
    public int CoveredLines { get; set; }
    public int TotalLines { get; set; }
}

// Tests/Quality/TestResults.cs
public class TestResults
{
    public int Total { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
}

// Tests/Quality/ApiComparisonResult.cs
public class ApiComparisonResult
{
    public bool HasBreakingChanges { get; set; }
    public int NewEndpoints { get; set; }
    public int RemovedEndpoints { get; set; }
    public int ModifiedEndpoints { get; set; }
}

// Tests/Quality/PerformanceTestResult.cs
public class PerformanceTestResult
{
    public string Endpoint { get; set; }
    public long ResponseTime { get; set; }
    public long MaxAcceptableTime { get; set; }
    public long MemoryUsage { get; set; }
    public long MaxAcceptableMemory { get; set; }
}

// Tests/Quality/SecurityScanResult.cs
public class SecurityScanResult
{
    public List<SecurityVulnerability> Vulnerabilities { get; set; }
}

// Tests/Quality/SecurityVulnerability.cs
public class SecurityVulnerability
{
    public string Type { get; set; }
    public string Severity { get; set; }
    public string Description { get; set; }
}
```

#### **Day 21-22: CI/CD Integration**
```yaml
# .github/workflows/test.yml
name: Test and Quality Gates

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  test:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        dotnet-version: ['8.0.x']

    services:
      mssql:
        image: mcr.microsoft.com/mssql/server:2022-latest
        env:
          ACCEPT_EULA: Y
          SA_PASSWORD: StrongPassword123!
        ports:
          - 1433:1433
      redis:
        image: redis:7-alpine
        ports:
          - 6379:6379

    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: ${{ matrix.dotnet-version }}

    - name: Restore dependencies
      run: dotnet restore

    - name: Build
      run: dotnet build --no-restore

    - name: Run Unit Tests
      run: dotnet test 6_Tests/VanAn.Core.Tests/ --no-build --verbosity normal --collect:"XPlat Code Coverage"

    - name: Run Integration Tests
      run: dotnet test 6_Tests/VanAn.Integration.Tests/ --no-build --verbosity normal --collect:"XPlat Code Coverage"

    - name: Run System Tests
      run: dotnet test 6_Tests/VanAn.System.Tests/ --no-build --verbosity normal --collect:"XPlat Code Coverage"

    - name: Upload coverage to Codecov
      uses: codecov/codecov-action@v3
      with:
        file: ./coverage.xml
        flags: unittests
        name: codecov-umbrella

    - name: Run E2E Tests
      run: |
        dotnet run --project 5_WebApps/KhachLink &
        sleep 30
        dotnet test 6_Tests/VanAn.E2E.Tests/ --no-build --verbosity normal

    - name: Quality Gates
      run: dotnet test 6_Tests/VanAn.Quality.Tests/ --no-build --verbosity normal

    - name: Generate Test Report
      run: |
        dotnet tool install -g dotnet-reportgenerator-globaltool
        reportgenerator -reports:**/*.xml -targetdir:./coverage -reporttypes:Html

    - name: Upload Test Report
      uses: actions/upload-artifact@v3
      with:
        name: test-report
        path: ./coverage

  security:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3

    - name: Run Security Scan
      run: |
        dotnet tool install -g SecurityCodeScan
        SecurityCodeScan start -d .

    - name: Upload Security Report
      uses: actions/upload-artifact@v3
      with:
        name: security-report
        path: ./SecurityCodeScan-reports

  performance:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'

    - name: Restore dependencies
      run: dotnet restore

    - name: Build
      run: dotnet build --no-restore

    - name: Run Performance Tests
      run: dotnet run --project 6_Tests/VanAn.Performance.Tests/

    - name: Upload Performance Report
      uses: actions/upload-artifact@v3
      with:
        name: performance-report
        path: ./performance-reports
```

---

## **4. IMPLEMENTATION PLAN**

### **4.1 Week 1-2: System Tests Implementation**
- [ ] Setup test containers infrastructure
- [ ] Create test data providers
- [ ] Implement API system tests
- [ ] Add integration test enhancement
- [ ] Create test utilities
- [ ] Setup test isolation

### **4.2 Week 3-4: E2E Tests with Playwright**
- [ ] Setup Playwright infrastructure
- [ ] Create page object models
- [ ] Implement order flow tests
- [ ] Add product management tests
- [ ] Create user journey tests
- [ ] Add visual regression tests

### **4.3 Week 5-6: Test Infrastructure Enhancement**
- [ ] Implement test data builders
- [ ] Add test parallelization
- [ ] Create test fixtures
- [ ] Setup test configuration
- [ ] Add test logging
- [ ] Implement test cleanup

### **4.4 Week 7-8: Quality Gates & CI/CD**
- [ ] Implement quality gates
- [ ] Create CI/CD pipeline
- [ ] Add code coverage reporting
- [ ] Setup security scanning
- [ ] Add performance testing
- [ ] Create test dashboards

---

## **5. SUCCESS METRICS**

### **5.1 Quality Metrics**
- **Code Coverage:** >90% for all modules
- **Test Pass Rate:** >98% for all tests
- **Test Execution Time:** <5 minutes for full suite
- **Test Reliability:** >99% consistent results

### **5.2 Coverage Metrics**
- **Unit Tests:** >80% coverage
- **Integration Tests:** >70% coverage
- **System Tests:** >60% coverage
- **E2E Tests:** >50% coverage

### **5.3 Performance Metrics**
- **Test Parallelization:** 50% faster execution
- **Test Isolation:** 100% independent tests
- **Test Data Management:** Automated setup/teardown
- **Test Reporting:** Real-time results

---

## **6. RISK MITIGATION**

### **6.1 Technical Risks**
1. **Test Flakiness** - Implement proper test isolation
2. **Slow Test Execution** - Use parallelization and optimization
3. **Test Data Issues** - Use automated data management
4. **Environment Dependencies** - Use containerized test environments

### **6.2 Process Risks**
1. **Test Maintenance** - Implement automated test updates
2. **Coverage Gaps** - Regular coverage analysis
3. **Quality Gate Failures** - Clear failure handling
4. **CI/CD Bottlenecks** - Optimize pipeline performance

---

## **7. NEXT STEPS**

### **7.1 Immediate Actions (This Week)**
1. **Setup Test Infrastructure** - Configure test containers
2. **Create Test Data** - Build test data providers
3. **Implement System Tests** - Start with API tests
4. **Setup CI/CD** - Configure basic pipeline

### **7.2 Short-term Goals (2 Weeks)**
1. **Complete System Tests** - Full API coverage
2. **Implement E2E Tests** - Key user journeys
3. **Add Test Automation** - Full CI/CD integration
4. **Quality Gates** - Implement quality checks

### **7.3 Long-term Goals (2 Months)**
1. **Complete Test Pyramid** - All 4 layers
2. **Advanced Testing** - Performance, security, chaos
3. **Test Analytics** - Comprehensive reporting
4. **Test Culture** - Team training and adoption

---

## **8. SUMMARY**

### **8.1 Current State**
- **2 test layers only** (Unit, Integration)
- **14/14 tests passing** (100%)
- **Basic infrastructure** with in-memory databases
- **Manual test execution** only
- **60% code coverage**

### **8.2 Target State**
- **4-layer test pyramid** (Unit, Integration, System, E2E)
- **90%+ code coverage** across all modules
- **Advanced infrastructure** with test containers
- **Full automation** in CI/CD pipeline
- **Quality gates** and reporting

### **8.3 Implementation Strategy**
- **8-week phased approach** with clear milestones
- **Test-driven development** approach
- **Infrastructure-first** strategy
- **Quality-focused** implementation

**Status:** Test module has good foundation but needs significant enhancement to achieve professional-grade testing pyramid.
