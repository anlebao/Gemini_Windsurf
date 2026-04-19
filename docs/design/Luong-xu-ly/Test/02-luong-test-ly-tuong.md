# Test - Lu?ng X? Lý T??ng (Ideal Architecture)

**Ngày:** 11 tháng 4, 2026  
**Module:** 6_Tests  
**Tr?ng thái:** Thi?t k? lu?ng x? lý lý t??ng ho?n ch?nh

---

## **1. T?NG QUAN ARCHITECTURE LÝ T??NG**

### **1.1 4-Layer Testing Pyramid**
```
6_Tests/
?? Layer1-Unit/                (Fast, Isolated Tests)
   ?? Domain/
      ?? OrderTests.cs
      ?? CustomerTests.cs
      ?? ProductTests.cs
      ?? KitchenItemTests.cs
      ?? ValueObjects/
         ?? MoneyTests.cs
         ?? QuantityTests.cs
         ?? PhoneNumberTests.cs
   ?? Services/
      ?? OrderServiceTests.cs
      ?? CustomerServiceTests.cs
      ?? KitchenServiceTests.cs
      ?? PricingServiceTests.cs
      ?? InventoryServiceTests.cs
   ?? TestInfrastructure/
      ?? TestBase.cs
      ?? MockFactory.cs
      ?? TestDataFactory.cs
      ?? AssertionExtensions.cs
?? Layer2-Integration/         (Database, Services Tests)
   ?? Repositories/
      ?? OrderRepositoryTests.cs
      ?? CustomerRepositoryTests.cs
      ?? ProductRepositoryTests.cs
      ?? InventoryRepositoryTests.cs
   ?? Services/
      ?? OrderWorkflowServiceTests.cs
      ?? KitchenServiceIntegrationTests.cs
      ?? VietQrServiceIntegrationTests.cs
      ?? NotificationServiceIntegrationTests.cs
   ?? TestInfrastructure/
      ?? IntegrationTestBase.cs
      ?? TestDatabaseFactory.cs
      ?? TestDbContextFactory.cs
      ?? TestDataSeeder.cs
?? Layer3-API/                (HTTP Endpoint Tests)
   ?? Controllers/
      ?? OrdersControllerTests.cs
      ?? KitchenControllerTests.cs
      ?? VietQrControllerTests.cs
      ?? VoiceCommandControllerTests.cs
      ?? CustomerControllerTests.cs
   ?? Middleware/
      ?? AuthenticationMiddlewareTests.cs
      ?? TenantMiddlewareTests.cs
      ?? RateLimitingMiddlewareTests.cs
   ?? TestInfrastructure/
      ?? ApiTestBase.cs
      ?? TestServerFactory.cs
      ?? TestHttpClientFactory.cs
      ?? AuthenticationTestHelper.cs
?? Layer4-E2E/                (Full User Journey Tests)
   ?? UserJourneys/
      ?? OrderCreationE2ETests.cs
      ?? KitchenWorkflowE2ETests.cs
      ?? PaymentE2ETests.cs
      ?? CustomerRegistrationE2ETests.cs
   ?? CrossBrowser/
      ?? ChromeTests.cs
      ?? FirefoxTests.cs
      ?? SafariTests.cs
      ?? MobileTests.cs
   ?? TestInfrastructure/
      ?? E2ETestBase.cs
      ?? PageObjectFactory.cs
      ?? BrowserFactory.cs
      ?? ScreenshotCapture.cs
      ?? VideoRecorder.cs
?? Layer5-Performance/         (Load & Stress Tests)
   ?? LoadTests/
      ?? OrderCreationLoadTests.cs
      ?? KitchenOperationsLoadTests.cs
      ?? CustomerRegistrationLoadTests.cs
      ?? PaymentProcessingLoadTests.cs
   ?? StressTests/
      ?? HighConcurrencyTests.cs
      ?? MemoryLeakTests.cs
      ?? DatabaseStressTests.cs
   ?? TestInfrastructure/
      ?? LoadTestBase.cs
      ?? PerformanceMetrics.cs
      ?? TestOrchestrator.cs
      ?? ResultAnalyzer.cs
?? Layer6-Security/            (Security & Penetration Tests)
   ?? Authentication/
      ?? LoginSecurityTests.cs
      ?? TokenValidationTests.cs
      ?? PasswordPolicyTests.cs
   ?? Authorization/
      ?? RoleBasedAccessTests.cs
      ?? TenantIsolationTests.cs
      ?? DataAccessTests.cs
   ?? Vulnerability/
      ?? SqlInjectionTests.cs
      ?? XssProtectionTests.cs
      ?? CsrfProtectionTests.cs
   ?? TestInfrastructure/
      ?? SecurityTestBase.cs
      ?? VulnerabilityScanner.cs
      ?? SecurityTestHelper.cs
?? TestInfrastructure/         (Shared Testing Framework)
   ?? Configuration/
      ?? TestSettings.cs
      ?? TestEnvironment.cs
      ?? TestDatabaseSettings.cs
   ?? DataManagement/
      ?? TestDataBuilder.cs
      ?? TestDataFactory.cs
      ?? TestDataManager.cs
      ?? TestCleanupManager.cs
   ?? Reporting/
      ?? TestReporter.cs
      ?? CoverageReporter.cs
      ?? PerformanceReporter.cs
      ?? TestDashboard.cs
   ?? Utilities/
      ?? TestLogger.cs
      ?? TestTimer.cs
      ?? TestHelper.cs
      ?? AssertionHelper.cs
```

---

## **2. LU?NG X? LÝ LÝ T??NG**

### **2.1 Unit Tests Layer (Layer 1)**

#### **Phase 1: Domain Entity Tests**
```csharp
// OrderTests.cs
public class OrderTests : TestBase
{
    [Theory]
    [InlineData("DINEIN", "Table 5", 2)]
    [InlineData("TAKEAWAY", "Takeaway order", 1)]
    [InlineData("DELIVERY", "Delivery to address", 3)]
    public async Task CreateOrder_WithValidData_ShouldCreateOrder(
        string orderType, 
        string customerNotes, 
        int itemCount)
    {
        // Arrange
        var customerId = new CustomerId(Guid.NewGuid());
        var tenantId = new TenantId(Guid.NewGuid());
        var itemsData = CreateTestOrderItems(itemCount);

        // Act
        var order = Order.Create(
            customerId: customerId,
            tenantId: tenantId,
            orderType: Enum.Parse<OrderType>(orderType),
            customerNotes: customerNotes,
            itemsData: itemsData
        );

        // Assert
        order.Should().NotBeNull();
        order.CustomerId.Should().Be(customerId);
        order.TenantId.Should().Be(tenantId);
        order.OrderType.Should().Be(Enum.Parse<OrderType>(orderType));
        order.CustomerNotes.Should().Be(customerNotes);
        order.Status.Should().Be(OrderStatus.Draft);
        order.Items.Should().HaveCount(itemCount);
        order.TotalAmount.Should().BeGreaterThan(Money.Zero);
        order.GetUncommittedEvents().Should().ContainSingle(e => e is OrderCreatedEvent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(51)]
    public void CreateOrder_WithInvalidItemCount_ShouldThrowException(int itemCount)
    {
        // Arrange
        var customerId = new CustomerId(Guid.NewGuid());
        var tenantId = new TenantId(Guid.NewGuid());
        var itemsData = itemCount > 0 && itemCount <= 50 
            ? CreateTestOrderItems(itemCount)
            : new List<OrderItemData>();

        // Act & Assert
        Action act = () => Order.Create(
            customerId: customerId,
            tenantId: tenantId,
            orderType: OrderType.DineIn,
            customerNotes: "Test",
            itemsData: itemsData
        );

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage(itemCount == 0 
                ? "Order must have at least one item"
                : "Order cannot have more than 50 items");
    }

    [Fact]
    public void ConfirmOrder_FromDraftStatus_ShouldConfirm()
    {
        // Arrange
        var order = CreateTestOrder();

        // Act
        order.Confirm();

        // Assert
        order.Status.Should().Be(OrderStatus.Confirmed);
        order.ConfirmedAt.Should().NotBeNull();
        order.GetUncommittedEvents().Should().ContainSingle(e => e is OrderConfirmedEvent);
    }

    [Fact]
    public void ConfirmOrder_FromNonDraftStatus_ShouldThrowException()
    {
        // Arrange
        var order = CreateTestOrder();
        order.Confirm(); // Put in Confirmed status

        // Act & Assert
        Action act = () => order.Confirm();

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("Cannot confirm order in Confirmed status");
    }

    [Fact]
    public void CompletePayment_WithValidData_ShouldCompletePayment()
    {
        // Arrange
        var order = CreateTestOrder();
        order.Confirm();
        order.StartPayment(PaymentMethod.VietQR);

        var transactionId = "TXN123456";
        var payload = "QR_PAYLOAD";

        // Act
        order.CompletePayment(transactionId, payload);

        // Assert
        order.PaymentStatus.Should().Be(PaymentStatus.Paid);
        order.Status.Should().Be(OrderStatus.Paid);
        order.PaidAt.Should().NotBeNull();
        order.VietQRTransactionId.Should().Be(transactionId);
        order.VietQRPayload.Should().Be(payload);
        order.GetUncommittedEvents().Should().ContainSingle(e => e is OrderPaidEvent);
    }

    private Order CreateTestOrder()
    {
        var customerId = new CustomerId(Guid.NewGuid());
        var tenantId = new TenantId(Guid.NewGuid());
        var itemsData = CreateTestOrderItems(2);

        return Order.Create(
            customerId: customerId,
            tenantId: tenantId,
            orderType: OrderType.DineIn,
            customerNotes: "Table 5",
            itemsData: itemsData
        );
    }

    private List<OrderItemData> CreateTestOrderItems(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => new OrderItemData(
                productId: new ProductId(Guid.NewGuid()),
                quantity: new Quantity(i),
                unitPrice: new Money(10000 * i),
                vatRate: new Percentage(0.10m),
                notes: $"Item {i}"
            ))
            .ToList();
    }
}
```

#### **Phase 2: Value Object Tests**
```csharp
// MoneyTests.cs
public class MoneyTests : TestBase
{
    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(999999999.99)]
    public void CreateMoney_WithValidValue_ShouldCreateMoney(decimal value)
    {
        // Act
        var money = new Money(value);

        // Assert
        money.Value.Should().Be(value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(-999999999.99)]
    public void CreateMoney_WithNegativeValue_ShouldThrowException(decimal value)
    {
        // Act & Assert
        Action act = () => new Money(value);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("Money cannot be negative");
    }

    [Theory]
    [InlineData(100, 50, 150)]
    [InlineData(0, 0, 0)]
    [InlineData(999999999.99, 0.01, 1000000000)]
    public void AddMoney_WithValidValues_ShouldReturnCorrectSum(
        decimal value1, decimal value2, decimal expected)
    {
        // Arrange
        var money1 = new Money(value1);
        var money2 = new Money(value2);

        // Act
        var result = money1 + money2;

        // Assert
        result.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData(100, 50, 50)]
    [InlineData(100, 100, 0)]
    [InlineData(100, 0.01, 99.99)]
    public void SubtractMoney_WithValidValues_ShouldReturnCorrectDifference(
        decimal value1, decimal value2, decimal expected)
    {
        // Arrange
        var money1 = new Money(value1);
        var money2 = new Money(value2);

        // Act
        var result = money1 - money2;

        // Assert
        result.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData(100, 150)]
    [InlineData(0, 1)]
    [InlineData(50, 100)]
    public void SubtractMoney_WithInsufficientFunds_ShouldThrowException(
        decimal value1, decimal value2)
    {
        // Arrange
        var money1 = new Money(value1);
        var money2 = new Money(value2);

        // Act & Assert
        Action act = () => money1 - money2;

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("Resulting money cannot be negative");
    }

    [Theory]
    [InlineData(100, 0.10, 10)]
    [InlineData(200, 0.05, 10)]
    [InlineData(1000, 0.15, 150)]
    public void ApplyVat_WithValidRate_ShouldReturnCorrectAmount(
        decimal value, decimal vatRate, decimal expected)
    {
        // Arrange
        var money = new Money(value);
        var vat = new VatRate(vatRate);

        // Act
        var result = money.ApplyVat(vat);

        // Assert
        result.Value.Should().Be(expected);
    }
}

// PhoneNumberTests.cs
public class PhoneNumberTests : TestBase
{
    [Theory]
    [InlineData("0123456789")]
    [InlineData("0987654321")]
    [InlineData("0345678901")]
    [InlineData("+84123456789")]
    public void CreatePhoneNumber_WithValidVietnameseNumber_ShouldCreatePhoneNumber(
        string phoneNumber)
    {
        // Act
        var phone = new PhoneNumber(phoneNumber);

        // Assert
        phone.Value.Should().Be(phoneNumber.Replace(" ", "").Replace("-", ""));
    }

    [Theory]
    [InlineData("123456789")]
    [InlineData("01234567890")]
    [InlineData("987654321")]
    [InlineData("abc1234567")]
    [InlineData("")]
    [InlineData(null)]
    public void CreatePhoneNumber_WithInvalidNumber_ShouldThrowException(string phoneNumber)
    {
        // Act & Assert
        Action act = () => new PhoneNumber(phoneNumber);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("Invalid Vietnamese phone number format");
    }

    [Theory]
    [InlineData("0123456789", "012 345 6789")]
    [InlineData("0987654321", "098 765 4321")]
    [InlineData("+84123456789", "+84 123 456 789")]
    public void GetFormattedNumber_ShouldReturnCorrectFormat(
        string input, string expected)
    {
        // Arrange
        var phone = new PhoneNumber(input);

        // Act
        var formatted = phone.GetFormattedNumber();

        // Assert
        formatted.Should().Be(expected);
    }

    [Theory]
    [InlineData("0123456789", "+84123456789")]
    [InlineData("+84123456789", "+84123456789")]
    public void GetInternationalFormat_ShouldReturnCorrectFormat(
        string input, string expected)
    {
        // Arrange
        var phone = new PhoneNumber(input);

        // Act
        var international = phone.GetInternationalFormat();

        // Assert
        international.Should().Be(expected);
    }
}
```

---

### **2.2 Integration Tests Layer (Layer 2)**

#### **Phase 1: Repository Integration Tests**
```csharp
// OrderRepositoryTests.cs
public class OrderRepositoryTests : IntegrationTestBase
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;

    public OrderRepositoryTests()
        : base()
    {
        _orderRepository = ServiceProvider.GetRequiredService<IOrderRepository>();
        _customerRepository = ServiceProvider.GetRequiredService<ICustomerRepository>();
        _productRepository = ServiceProvider.GetRequiredService<IProductRepository>();
    }

    [Fact]
    public async Task AddOrder_WithValidData_ShouldPersistOrder()
    {
        // Arrange
        var tenantId = new TenantId(Guid.NewGuid());
        var customer = await CreateTestCustomerAsync(tenantId);
        var product = await CreateTestProductAsync(tenantId);
        var order = CreateTestOrder(customer.Id, tenantId, product.Id);

        // Act
        await _orderRepository.AddAsync(order);
        await DbContext.SaveChangesAsync();

        // Assert
        var savedOrder = await _orderRepository.GetByIdAsync(order.Id);
        savedOrder.Should().NotBeNull();
        savedOrder.CustomerId.Should().Be(customer.Id);
        savedOrder.TenantId.Should().Be(tenantId);
        savedOrder.Items.Should().HaveCount(1);
        savedOrder.TotalAmount.Should().Be(order.TotalAmount);
    }

    [Fact]
    public async Task GetOrdersByTenant_WithValidTenant_ShouldReturnOrders()
    {
        // Arrange
        var tenantId = new TenantId(Guid.NewGuid());
        var customer = await CreateTestCustomerAsync(tenantId);
        var product = await CreateTestProductAsync(tenantId);
        
        var orders = new List<Order>();
        for (int i = 0; i < 5; i++)
        {
            var order = CreateTestOrder(customer.Id, tenantId, product.Id);
            await _orderRepository.AddAsync(order);
            orders.Add(order);
        }
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _orderRepository.GetByTenantIdAsync(tenantId);

        // Assert
        result.Should().HaveCount(5);
        result.All(o => o.TenantId == tenantId).Should().BeTrue();
        result.All(o => !o.IsDeleted).Should().BeTrue();
    }

    [Fact]
    public async Task GetPagedOrders_WithPagination_ShouldReturnPagedResults()
    {
        // Arrange
        var tenantId = new TenantId(Guid.NewGuid());
        var customer = await CreateTestCustomerAsync(tenantId);
        var product = await CreateTestProductAsync(tenantId);
        
        var orders = new List<Order>();
        for (int i = 0; i < 25; i++)
        {
            var order = CreateTestOrder(customer.Id, tenantId, product.Id);
            order.OrderDate = DateTime.UtcNow.AddDays(-i); // Different dates
            await _orderRepository.AddAsync(order);
            orders.Add(order);
        }
        await DbContext.SaveChangesAsync();

        var filter = new OrderFilter { TenantId = tenantId };
        var pagination = new Pagination { Page = 1, Size = 10 };

        // Act
        var result = await _orderRepository.GetPagedAsync(filter, pagination);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(25);
        result.Items.Should().HaveCount(10);
        result.Page.Should().Be(1);
        result.Size.Should().Be(10);
        result.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task UpdateOrderStatus_WithValidStatus_ShouldUpdateOrder()
    {
        // Arrange
        var tenantId = new TenantId(Guid.NewGuid());
        var customer = await CreateTestCustomerAsync(tenantId);
        var product = await CreateTestProductAsync(tenantId);
        var order = CreateTestOrder(customer.Id, tenantId, product.Id);
        
        await _orderRepository.AddAsync(order);
        await DbContext.SaveChangesAsync();

        // Act
        order.Confirm();
        await _orderRepository.UpdateAsync(order);
        await DbContext.SaveChangesAsync();

        // Assert
        var updatedOrder = await _orderRepository.GetByIdAsync(order.Id);
        updatedOrder.Should().NotBeNull();
        updatedOrder.Status.Should().Be(OrderStatus.Confirmed);
        updatedOrder.ConfirmedAt.Should().NotBeNull();
    }

    private async Task<Customer> CreateTestCustomerAsync(TenantId tenantId)
    {
        var customer = Customer.Create(
            deviceId: Guid.NewGuid(),
            displayName: "Test Customer",
            tenantId: tenantId
        );
        
        await _customerRepository.AddAsync(customer);
        await DbContext.SaveChangesAsync();
        
        return customer;
    }

    private async Task<Product> CreateTestProductAsync(TenantId tenantId)
    {
        var product = Product.Create(
            name: "Test Product",
            description: "Test Description",
            basePrice: new Money(10000),
            tenantId: tenantId
        );
        
        await _productRepository.AddAsync(product);
        await DbContext.SaveChangesAsync();
        
        return product;
    }

    private Order CreateTestOrder(CustomerId customerId, TenantId tenantId, ProductId productId)
    {
        var itemsData = new List<OrderItemData>
        {
            new OrderItemData(
                productId: productId,
                quantity: new Quantity(2),
                unitPrice: new Money(10000),
                vatRate: new Percentage(0.10m),
                notes: "Test notes"
            )
        };

        return Order.Create(
            customerId: customerId,
            tenantId: tenantId,
            orderType: OrderType.DineIn,
            customerNotes: "Test order",
            itemsData: itemsData
        );
    }
}
```

#### **Phase 2: Service Integration Tests**
```csharp
// KitchenServiceIntegrationTests.cs
public class KitchenServiceIntegrationTests : IntegrationTestBase
{
    private readonly IKitchenService _kitchenService;
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;

    public KitchenServiceIntegrationTests()
        : base()
    {
        _kitchenService = ServiceProvider.GetRequiredService<IKitchenService>();
        _orderRepository = ServiceProvider.GetRequiredService<IOrderRepository>();
        _customerRepository = ServiceProvider.GetRequiredService<ICustomerRepository>();
        _productRepository = ServiceProvider.GetRequiredService<IProductRepository>();
    }

    [Fact]
    public async Task GetGroupedKitchenItems_WithMultipleOrders_ShouldGroupCorrectly()
    {
        // Arrange
        var tenantId = new TenantId(Guid.NewGuid());
        var customer = await CreateTestCustomerAsync(tenantId);
        var product1 = await CreateTestProductAsync(tenantId, "Coffee");
        var product2 = await CreateTestProductAsync(tenantId, "Tea");

        // Create orders with same product
        var order1 = await CreateTestOrderAsync(customer.Id, tenantId, product1.Id, 2);
        var order2 = await CreateTestOrderAsync(customer.Id, tenantId, product1.Id, 1);
        var order3 = await CreateTestOrderAsync(customer.Id, tenantId, product2.Id, 3);

        // Act
        var result = await _kitchenService.GetGroupedKitchenItemsAsync(tenantId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2); // 2 different products

        var coffeeGroup = result.FirstOrDefault(g => g.ProductId == product1.Id);
        coffeeGroup.Should().NotBeNull();
        coffeeGroup.ProductName.Should().Be("Coffee");
        coffeeGroup.TotalQuantity.Should().Be(3); // 2 + 1
        coffeeGroup.Items.Should().HaveCount(2); // 2 orders
        coffeeGroup.GroupStatus.Should().Be(KitchenStatus.Pending);

        var teaGroup = result.FirstOrDefault(g => g.ProductId == product2.Id);
        teaGroup.Should().NotBeNull();
        teaGroup.ProductName.Should().Be("Tea");
        teaGroup.TotalQuantity.Should().Be(3);
        teaGroup.Items.Should().HaveCount(1);
        teaGroup.GroupStatus.Should().Be(KitchenStatus.Pending);
    }

    [Fact]
    public async Task UpdateItemStatus_WithValidData_ShouldUpdateStatus()
    {
        // Arrange
        var tenantId = new TenantId(Guid.NewGuid());
        var customer = await CreateTestCustomerAsync(tenantId);
        var product = await CreateTestProductAsync(tenantId);
        var order = await CreateTestOrderAsync(customer.Id, tenantId, product.Id, 2);

        var orderItem = order.Items.First();
        var userId = Guid.NewGuid();
        var updateDto = new KitchenStatusUpdateDto
        {
            OrderItemId = orderItem.Id,
            NewStatus = KitchenStatus.Preparing,
            Notes = "Started preparation"
        };

        // Act
        var result = await _kitchenService.UpdateItemStatusAsync(updateDto, userId);

        // Assert
        result.Should().BeTrue();

        var updatedOrder = await _orderRepository.GetByIdAsync(order.Id);
        var updatedItem = updatedOrder.Items.First(i => i.Id == orderItem.Id);
        updatedItem.KitchenStatus.Should().Be(KitchenStatus.Preparing);
    }

    [Fact]
    public async Task GetKitchenAnalytics_WithValidData_ShouldReturnAnalytics()
    {
        // Arrange
        var tenantId = new TenantId(Guid.NewGuid());
        var customer = await CreateTestCustomerAsync(tenantId);
        var product = await CreateTestProductAsync(tenantId);

        // Create orders with different statuses
        var order1 = await CreateTestOrderAsync(customer.Id, tenantId, product.Id, 1);
        var order2 = await CreateTestOrderAsync(customer.Id, tenantId, product.Id, 1);
        var order3 = await CreateTestOrderAsync(customer.Id, tenantId, product.Id, 1);

        // Update statuses
        order1.StartPreparation();
        order2.Complete();
        // order3 remains pending

        await _orderRepository.UpdateAsync(order1);
        await _orderRepository.UpdateAsync(order2);
        await _orderRepository.UpdateAsync(order3);
        await DbContext.SaveChangesAsync();

        var fromDate = DateTime.UtcNow.AddDays(-1);

        // Act
        var analytics = await _kitchenService.GetKitchenAnalyticsAsync(tenantId, fromDate);

        // Assert
        analytics.Should().NotBeNull();
        analytics.ShopId.Should().Be(tenantId);
        analytics.TotalOrders.Should().Be(3);
        analytics.CompletedOrders.Should().Be(1);
        analytics.PendingOrders.Should().Be(1); // order3
        analytics.AveragePreparationTime.Should().BeGreaterThan(0);
    }

    private async Task<Order> CreateTestOrderAsync(
        CustomerId customerId, 
        TenantId tenantId, 
        ProductId productId, 
        int quantity)
    {
        var itemsData = new List<OrderItemData>
        {
            new OrderItemData(
                productId: productId,
                quantity: new Quantity(quantity),
                unitPrice: new Money(10000),
                vatRate: new Percentage(0.10m),
                notes: "Test notes"
            )
        };

        var order = Order.Create(
            customerId: customerId,
            tenantId: tenantId,
            orderType: OrderType.DineIn,
            customerNotes: "Test order",
            itemsData: itemsData
        );

        await _orderRepository.AddAsync(order);
        await DbContext.SaveChangesAsync();

        return order;
    }
}
```

---

### **2.3 API Tests Layer (Layer 3)**

#### **Phase 1: Controller Tests**
```csharp
// OrdersControllerTests.cs
public class OrdersControllerTests : ApiTestBase
{
    private readonly HttpClient _client;
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;

    public OrdersControllerTests()
        : base()
    {
        _client = CreateHttpClient();
        _orderRepository = ServiceProvider.GetRequiredService<IOrderRepository>();
        _customerRepository = ServiceProvider.GetRequiredService<ICustomerRepository>();
        _productRepository = ServiceProvider.GetRequiredService<IProductRepository>();
    }

    [Fact]
    public async Task CreateOrder_WithValidData_ShouldReturnCreated()
    {
        // Arrange
        var tenantId = new TenantId(Guid.NewGuid());
        var customer = await CreateTestCustomerAsync(tenantId);
        var product = await CreateTestProductAsync(tenantId);

        var request = new CreateOrderRequest
        {
            CustomerId = customer.Id,
            OrderType = "DINEIN",
            CustomerNotes = "Test order",
            Items = new List<CreateOrderItemRequest>
            {
                new CreateOrderItemRequest
                {
                    ProductId = product.Id,
                    Quantity = 2,
                    Notes = "No ice"
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var orderResponse = await response.Content.ReadFromJsonAsync<OrderResponse>();
        orderResponse.Should().NotBeNull();
        orderResponse.CustomerId.Should().Be(customer.Id);
        orderResponse.OrderType.Should().Be("DINEIN");
        orderResponse.CustomerNotes.Should().Be("Test order");
        orderResponse.Items.Should().HaveCount(1);
        orderResponse.TotalAmount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateOrder_WithInvalidData_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = Guid.Empty,
            OrderType = "INVALID_TYPE",
            CustomerNotes = "",
            Items = new List<CreateOrderItemRequest>() // Empty items
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var errorResponse = await response.Content.ReadFromJsonAsync<ValidationErrorResponse>();
        errorResponse.Should().NotBeNull();
        errorResponse.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetOrder_WithValidId_ShouldReturnOrder()
    {
        // Arrange
        var tenantId = new TenantId(Guid.NewGuid());
        var customer = await CreateTestCustomerAsync(tenantId);
        var product = await CreateTestProductAsync(tenantId);
        var order = await CreateTestOrderAsync(customer.Id, tenantId, product.Id, 1);

        // Act
        var response = await _client.GetAsync($"/api/orders/{order.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var orderResponse = await response.Content.ReadFromJsonAsync<OrderResponse>();
        orderResponse.Should().NotBeNull();
        orderResponse.Id.Should().Be(order.Id);
        orderResponse.CustomerId.Should().Be(customer.Id);
    }

    [Fact]
    public async Task GetOrder_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        var invalidOrderId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/orders/{invalidOrderId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetOrders_WithValidFilter_ShouldReturnOrders()
    {
        // Arrange
        var tenantId = new TenantId(Guid.NewGuid());
        var customer = await CreateTestCustomerAsync(tenantId);
        var product = await CreateTestProductAsync(tenantId);

        // Create multiple orders
        var orders = new List<Order>();
        for (int i = 0; i < 5; i++)
        {
            var order = await CreateTestOrderAsync(customer.Id, tenantId, product.Id, 1);
            orders.Add(order);
        }

        // Act
        var response = await _client.GetAsync("/api/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var ordersResponse = await response.Content.ReadFromJsonAsync<PagedResponse<OrderResponse>>();
        ordersResponse.Should().NotBeNull();
        ordersResponse.Items.Should().HaveCount(5);
        ordersResponse.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task ConfirmOrder_WithValidId_ShouldConfirmOrder()
    {
        // Arrange
        var tenantId = new TenantId(Guid.NewGuid());
        var customer = await CreateTestCustomerAsync(tenantId);
        var product = await CreateTestProductAsync(tenantId);
        var order = await CreateTestOrderAsync(customer.Id, tenantId, product.Id, 1);

        var request = new ConfirmOrderRequest
        {
            OrderId = order.Id
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders/confirm", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var orderResponse = await response.Content.ReadFromJsonAsync<OrderResponse>();
        orderResponse.Should().NotBeNull();
        orderResponse.Status.Should().Be("Confirmed");
        orderResponse.ConfirmedAt.Should().NotBeNull();
    }

    private async Task<Customer> CreateTestCustomerAsync(TenantId tenantId)
    {
        var customer = Customer.Create(
            deviceId: Guid.NewGuid(),
            displayName: "Test Customer",
            tenantId: tenantId
        );
        
        await _customerRepository.AddAsync(customer);
        await DbContext.SaveChangesAsync();
        
        return customer;
    }

    private async Task<Product> CreateTestProductAsync(TenantId tenantId, string name = "Test Product")
    {
        var product = Product.Create(
            name: name,
            description: "Test Description",
            basePrice: new Money(10000),
            tenantId: tenantId
        );
        
        await _productRepository.AddAsync(product);
        await DbContext.SaveChangesAsync();
        
        return product;
    }

    private async Task<Order> CreateTestOrderAsync(
        CustomerId customerId, 
        TenantId tenantId, 
        ProductId productId, 
        int quantity)
    {
        var itemsData = new List<OrderItemData>
        {
            new OrderItemData(
                productId: productId,
                quantity: new Quantity(quantity),
                unitPrice: new Money(10000),
                vatRate: new Percentage(0.10m),
                notes: "Test notes"
            )
        };

        var order = Order.Create(
            customerId: customerId,
            tenantId: tenantId,
            orderType: OrderType.DineIn,
            customerNotes: "Test order",
            itemsData: itemsData
        );

        await _orderRepository.AddAsync(order);
        await DbContext.SaveChangesAsync();

        return order;
    }
}
```

---

### **2.4 E2E Tests Layer (Layer 4)**

#### **Phase 1: User Journey Tests**
```csharp
// OrderCreationE2ETests.cs
public class OrderCreationE2ETests : E2ETestBase
{
    private readonly OrderPage _orderPage;
    private readonly KitchenPage _kitchenPage;
    private readonly PaymentPage _paymentPage;

    public OrderCreationE2ETests()
        : base()
    {
        _orderPage = PageFactory.CreatePage<OrderPage>(Page);
        _kitchenPage = PageFactory.CreatePage<KitchenPage>(Page);
        _paymentPage = PageFactory.CreatePage<PaymentPage>(Page);
    }

    [Fact]
    public async Task CompleteOrderFlow_ShouldWorkEndToEnd()
    {
        // Arrange
        var customerName = $"Test Customer {DateTime.UtcNow.Ticks}";
        var productName = "Cà phê noir";
        var quantity = 2;

        // Act & Assert - Step 1: Customer Registration
        await _orderPage.NavigateAsync();
        await _orderPage.RegisterCustomerAsync(customerName, "0123456789", "test@example.com");
        
        var customerDashboard = await _orderPage.GetCustomerDashboardAsync();
        customerDashboard.Should().NotBeNull();
        customerDashboard.CustomerName.Should().Be(customerName);

        // Act & Assert - Step 2: Product Selection
        await _orderPage.SelectProductAsync(productName);
        await _orderPage.SetQuantityAsync(quantity);
        await _orderPage.AddToCartAsync();

        var cart = await _orderPage.GetCartAsync();
        cart.Should().NotBeNull();
        cart.Items.Should().HaveCount(1);
        cart.Items.First().ProductName.Should().Be(productName);
        cart.Items.First().Quantity.Should().Be(quantity);

        // Act & Assert - Step 3: Order Placement
        await _orderPage.SetOrderTypeAsync("DINEIN");
        await _orderPage.SetCustomerNotesAsync("Table 5");
        await _orderPage.PlaceOrderAsync();

        var orderConfirmation = await _orderPage.GetOrderConfirmationAsync();
        orderConfirmation.Should().NotBeNull();
        orderConfirmation.OrderId.Should().NotBeEmpty();
        orderConfirmation.TotalAmount.Should().BeGreaterThan(0);

        // Act & Assert - Step 4: Kitchen Display
        await _kitchenPage.NavigateAsync();
        await _kitchenPage.WaitForOrderAsync(orderConfirmation.OrderId);

        var kitchenOrder = await _kitchenPage.GetOrderAsync(orderConfirmation.OrderId);
        kitchenOrder.Should().NotBeNull();
        kitchenOrder.ProductName.Should().Be(productName);
        kitchenOrder.Quantity.Should().Be(quantity);
        kitchenOrder.Status.Should().Be("Pending");

        // Act & Assert - Step 5: Kitchen Preparation
        await _kitchenPage.StartPreparationAsync(orderConfirmation.OrderId);
        
        var updatedKitchenOrder = await _kitchenPage.GetOrderAsync(orderConfirmation.OrderId);
        updatedKitchenOrder.Status.Should().Be("Preparing");

        await _kitchenPage.CompletePreparationAsync(orderConfirmation.OrderId);
        
        var completedKitchenOrder = await _kitchenPage.GetOrderAsync(orderConfirmation.OrderId);
        completedKitchenOrder.Status.Should().Be("Completed");

        // Act & Assert - Step 6: Payment Processing
        await _paymentPage.NavigateWithOrderIdAsync(orderConfirmation.OrderId);
        await _paymentPage.SelectPaymentMethodAsync("VIETQR");
        await _paymentPage.ConfirmPaymentAsync();

        var paymentConfirmation = await _paymentPage.GetPaymentConfirmationAsync();
        paymentConfirmation.Should().NotBeNull();
        paymentConfirmation.Status.Should().Be("Paid");
        paymentConfirmation.TransactionId.Should().NotBeEmpty();

        // Act & Assert - Step 7: Order Completion
        await _orderPage.NavigateToOrderTrackingAsync(orderConfirmation.OrderId);
        var orderStatus = await _orderPage.GetOrderStatusAsync();
        orderStatus.Should().Be("Completed");
    }

    [Fact]
    public async Task OrderWithVoiceCommand_ShouldWorkEndToEnd()
    {
        // Arrange
        var customerName = $"Voice Customer {DateTime.UtcNow.Ticks}";
        var voiceCommand = "Tôi mu?n 2 cà phê không ?";

        // Act & Assert - Step 1: Customer Registration
        await _orderPage.NavigateAsync();
        await _orderPage.RegisterCustomerAsync(customerName, "0123456789", "voice@example.com");

        // Act & Assert - Step 2: Voice Command
        await _orderPage.StartVoiceCommandAsync();
        await _orderPage.SpeakCommandAsync(voiceCommand);
        await _orderPage.WaitForVoiceProcessingAsync();

        var voiceResult = await _orderPage.GetVoiceCommandResultAsync();
        voiceResult.Should().NotBeNull();
        voiceResult.RecognizedText.Should().Contain("cà phê");
        voiceResult.Intent.Should().Be("CreateOrder");
        voiceResult.Entities.Should().ContainKey("quantity");
        voiceResult.Entities["quantity"].Should().Be(2);

        // Act & Assert - Step 3: Order Confirmation
        await _orderPage.ConfirmVoiceOrderAsync();

        var orderConfirmation = await _orderPage.GetOrderConfirmationAsync();
        orderConfirmation.Should().NotBeNull();
        orderConfirmation.Items.Should().HaveCount(1);
        orderConfirmation.Items.First().ProductName.Should().Contain("cà phê");
        orderConfirmation.Items.First().Quantity.Should().Be(2);
    }

    [Fact]
    public async Task OrderWithVietQR_ShouldWorkEndToEnd()
    {
        // Arrange
        var customerName = $"QR Customer {DateTime.UtcNow.Ticks}";
        var productName = "Trà sen";
        var quantity = 1;

        // Act & Assert - Step 1: Create Order
        await _orderPage.NavigateAsync();
        await _orderPage.RegisterCustomerAsync(customerName, "0123456789", "qr@example.com");
        await _orderPage.SelectProductAsync(productName);
        await _orderPage.SetQuantityAsync(quantity);
        await _orderPage.AddToCartAsync();
        await _orderPage.PlaceOrderAsync();

        var orderConfirmation = await _orderPage.GetOrderConfirmationAsync();

        // Act & Assert - Step 2: VietQR Payment
        await _paymentPage.NavigateWithOrderIdAsync(orderConfirmation.OrderId);
        await _paymentPage.SelectPaymentMethodAsync("VIETQR");

        var qrCode = await _paymentPage.GetVietQRCodeAsync();
        qrCode.Should().NotBeNull();
        qrCode.QrImageUrl.Should().NotBeEmpty();
        qrCode.Amount.Should().BeGreaterThan(0);

        // Simulate QR payment
        await _paymentPage.SimulateQRPaymentAsync();

        var paymentConfirmation = await _paymentPage.GetPaymentConfirmationAsync();
        paymentConfirmation.Should().NotBeNull();
        paymentConfirmation.Status.Should().Be("Paid");
        paymentConfirmation.PaymentMethod.Should().Be("VIETQR");
    }
}
```

---

### **2.5 Performance Tests Layer (Layer 5)**

#### **Phase 1: Load Tests**
```csharp
// OrderCreationLoadTests.cs
public class OrderCreationLoadTests : LoadTestBase
{
    [Fact]
    public async Task ConcurrentOrderCreation_ShouldHandleLoad()
    {
        // Arrange
        var concurrentUsers = 50;
        var ordersPerUser = 10;
        var totalOrders = concurrentUsers * ordersPerUser;

        var metrics = new PerformanceMetrics();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var tasks = Enumerable.Range(0, concurrentUsers)
            .Select(userId => CreateOrdersForUserAsync(userId, ordersPerUser, metrics))
            .ToArray();

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        metrics.TotalRequests.Should().Be(totalOrders);
        metrics.SuccessfulRequests.Should().Be(totalOrders);
        metrics.FailedRequests.Should().Be(0);
        metrics.AverageResponseTime.Should().BeLessThan(TimeSpan.FromSeconds(2));
        metrics.RequestsPerSecond.Should().BeGreaterThan(10);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMinutes(2));

        // Verify database state
        var orderCount = await GetOrderCountAsync();
        orderCount.Should().Be(totalOrders);
    }

    [Fact]
    public async Task KitchenOperations_ShouldHandleConcurrentUpdates()
    {
        // Arrange
        var concurrentChefs = 10;
        var itemsPerChef = 20;
        var totalItems = concurrentChefs * itemsPerChef;

        // Create test orders
        await CreateTestOrdersAsync(totalItems);

        var metrics = new PerformanceMetrics();

        // Act
        var tasks = Enumerable.Range(0, concurrentChefs)
            .Select(chefId => UpdateKitchenItemsAsync(chefId, itemsPerChef, metrics))
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert
        metrics.TotalRequests.Should().Be(totalItems);
        metrics.SuccessfulRequests.Should().Be(totalItems);
        metrics.FailedRequests.Should().Be(0);
        metrics.AverageResponseTime.Should().BeLessThan(TimeSpan.FromSeconds(1));
        metrics.RequestsPerSecond.Should().BeGreaterThan(20);
    }

    [Fact]
    public async Task VietQRGeneration_ShouldHandleHighVolume()
    {
        // Arrange
        var concurrentRequests = 100;
        var metrics = new PerformanceMetrics();

        // Act
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(i => GenerateVietQRAsync(i, metrics))
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert
        metrics.TotalRequests.Should().Be(concurrentRequests);
        metrics.SuccessfulRequests.Should().Be(concurrentRequests);
        metrics.FailedRequests.Should().Be(0);
        metrics.AverageResponseTime.Should().BeLessThan(TimeSpan.FromSeconds(3));
        metrics.RequestsPerSecond.Should().BeGreaterThan(30);
    }

    private async Task CreateOrdersForUserAsync(int userId, int orderCount, PerformanceMetrics metrics)
    {
        for (int i = 0; i < orderCount; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                await CreateOrderAsync($"User{userId}", $"Order{userId}_{i}");
                metrics.RecordSuccess(stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                metrics.RecordFailure(stopwatch.ElapsedMilliseconds, ex.Message);
            }
        }
    }

    private async Task UpdateKitchenItemsAsync(int chefId, int itemCount, PerformanceMetrics metrics)
    {
        for (int i = 0; i < itemCount; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                await UpdateKitchenItemStatusAsync($"Item{chefId}_{i}", KitchenStatus.Preparing);
                metrics.RecordSuccess(stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                metrics.RecordFailure(stopwatch.ElapsedMilliseconds, ex.Message);
            }
        }
    }

    private async Task GenerateVietQRAsync(int requestId, PerformanceMetrics metrics)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await GenerateVietQRAsync(requestId, 10000);
            metrics.RecordSuccess(stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            metrics.RecordFailure(stopwatch.ElapsedMilliseconds, ex.Message);
        }
    }
}
```

---

## **3. TESTING INFRASTRUCTURE LÝ T??NG**

### **3.1 Test Base Classes**
```csharp
// TestBase.cs - Unit Test Base
public abstract class TestBase
{
    protected MockFactory MockFactory { get; }
    protected TestDataFactory TestDataFactory { get; }

    protected TestBase()
    {
        MockFactory = new MockFactory();
        TestDataFactory = new TestDataFactory();
    }

    protected virtual void Dispose()
    {
        MockFactory?.Dispose();
    }
}

// IntegrationTestBase.cs - Integration Test Base
public abstract class IntegrationTestBase : TestBase, IAsyncLifetime
{
    protected IServiceProvider ServiceProvider { get; private set; }
    protected VanAnDbContext DbContext { get; private set; }
    protected TestDataSeeder TestDataSeeder { get; private set; }

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        
        // Configure test services
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<VanAnDbContext>(options =>
            options.UseSqlite("DataSource=:memory:"));
        
        // Register application services
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IKitchenService, KitchenService>();
        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<IInventoryService, InventoryService>();
        
        ServiceProvider = services.BuildServiceProvider();
        DbContext = ServiceProvider.GetRequiredService<VanAnDbContext>();
        TestDataSeeder = new TestDataSeeder(DbContext);
        
        await DbContext.Database.EnsureCreatedAsync();
        await TestDataSeeder.SeedDefaultDataAsync();
    }

    public async Task DisposeAsync()
    {
        await DbContext.Database.EnsureDeletedAsync();
        await DbContext.DisposeAsync();
        await ServiceProvider.DisposeAsync();
    }
}

// ApiTestBase.cs - API Test Base
public abstract class ApiTestBase : IntegrationTestBase
{
    protected HttpClient HttpClient { get; private set; }
    protected TestServerFactory TestServerFactory { get; }

    protected ApiTestBase()
    {
        TestServerFactory = new TestServerFactory();
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        
        var server = TestServerFactory.CreateServer(ServiceProvider);
        HttpClient = server.CreateClient();
        
        // Configure authentication
        await ConfigureAuthenticationAsync();
    }

    protected virtual async Task ConfigureAuthenticationAsync()
    {
        // Setup test authentication
        var token = await GenerateTestTokenAsync();
        HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    protected HttpClient CreateHttpClient()
    {
        return HttpClient;
    }

    protected async Task<string> GenerateTestTokenAsync()
    {
        // Generate JWT token for testing
        return "test-jwt-token";
    }
}

// E2ETestBase.cs - E2E Test Base
public abstract class E2ETestBase : IAsyncLifetime
{
    protected IPlaywright Playwright { get; private set; }
    protected IBrowser Browser { get; private set; }
    protected IPage Page { get; private set; }
    protected PageObjectFactory PageFactory { get; private set; }
    protected ScreenshotCapture ScreenshotCapture { get; private set; }
    protected VideoRecorder VideoRecorder { get; private set; }
    protected TestServerFactory TestServerFactory { get; }

    public async Task InitializeAsync()
    {
        // Initialize Playwright
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        
        // Configure browser for CI/CD
        var browserOptions = new BrowserTypeLaunchOptions
        {
            Headless = true, // Headless for CI/CD
            SlowMo = 0,      // No delay for fast execution
            Args = new[]
            {
                "--disable-web-security",
                "--disable-features=VizDisplayCompositor",
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-dev-shm-usage",
                "--disable-gpu"
            }
        };

        Browser = await Playwright.Chromium.LaunchAsync(browserOptions);
        Page = await Browser.NewPageAsync();
        
        // Initialize test infrastructure
        PageFactory = new PageObjectFactory(Page);
        ScreenshotCapture = new ScreenshotCapture(Page);
        VideoRecorder = new VideoRecorder(Page);
        TestServerFactory = new TestServerFactory();
        
        // Start test server
        await TestServerFactory.StartAsync();
        
        // Configure error handling
        Page.Error += (sender, e) => TestLogger.LogError($"Page error: {e.Error}");
        Page.Console += (sender, e) => 
        {
            if (e.Type == "error")
                TestLogger.LogError($"Console error: {e.Text}");
        };
    }

    public async Task DisposeAsync()
    {
        try
        {
            await Page?.CloseAsync();
            await Browser?.CloseAsync();
            await VideoRecorder?.StopAsync();
            await TestServerFactory?.StopAsync();
            Playwright?.Dispose();
        }
        catch (Exception ex)
        {
            TestLogger.LogError(ex, "Error disposing E2E test resources");
        }
    }

    protected async Task CaptureScreenshotAsync(string testName)
    {
        await ScreenshotCapture.CaptureAsync($"{testName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
    }

    protected async Task StartVideoRecordingAsync(string testName)
    {
        await VideoRecorder.StartAsync($"{testName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
    }
}

// LoadTestBase.cs - Load Test Base
public abstract class LoadTestBase : IntegrationTestBase
{
    protected TestOrchestrator TestOrchestrator { get; }
    protected ResultAnalyzer ResultAnalyzer { get; }

    protected LoadTestBase()
    {
        TestOrchestrator = new TestOrchestrator();
        ResultAnalyzer = new ResultAnalyzer();
    }

    protected async Task<int> GetOrderCountAsync()
    {
        using var scope = ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
        return await context.Orders.CountAsync();
    }

    protected async Task CreateOrderAsync(string customerName, string orderNotes)
    {
        // Implementation for creating test orders
        await Task.Delay(100); // Simulate network latency
    }

    protected async Task UpdateKitchenItemStatusAsync(string itemId, KitchenStatus status)
    {
        // Implementation for updating kitchen item status
        await Task.Delay(50); // Simulate processing time
    }

    protected async Task GenerateVietQRAsync(int requestId, decimal amount)
    {
        // Implementation for generating VietQR
        await Task.Delay(200); // Simulate external API call
    }
}
```

---

### **3.2 Test Data Management**
```csharp
// TestDataFactory.cs
public class TestDataFactory
{
    private readonly Random _random = new Random();

    public Customer CreateCustomer(string name = null, TenantId tenantId = null)
    {
        return Customer.Create(
            deviceId: Guid.NewGuid(),
            displayName: name ?? $"Customer {_random.Next(1000, 9999)}",
            tenantId: tenantId ?? new TenantId(Guid.NewGuid())
        );
    }

    public Product CreateProduct(string name = null, Money price = null, TenantId tenantId = null)
    {
        return Product.Create(
            name: name ?? $"Product {_random.Next(1000, 9999)}",
            description: "Test product description",
            basePrice: price ?? new Money(_random.Next(10000, 100000)),
            tenantId: tenantId ?? new TenantId(Guid.NewGuid())
        );
    }

    public Order CreateOrder(CustomerId customerId, TenantId tenantId, List<OrderItemData> items = null)
    {
        var orderItems = items ?? CreateOrderItems(3);
        
        return Order.Create(
            customerId: customerId,
            tenantId: tenantId,
            orderType: OrderType.DineIn,
            customerNotes: "Test order",
            itemsData: orderItems
        );
    }

    public List<OrderItemData> CreateOrderItems(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => new OrderItemData(
                productId: new ProductId(Guid.NewGuid()),
                quantity: new Quantity(_random.Next(1, 5)),
                unitPrice: new Money(_random.Next(10000, 50000)),
                vatRate: new Percentage(0.10m),
                notes: $"Item {i}"
            ))
            .ToList();
    }

    public KitchenStatusUpdateDto CreateKitchenStatusUpdate(OrderItemId itemId, KitchenStatus status)
    {
        return new KitchenStatusUpdateDto
        {
            OrderItemId = itemId,
            NewStatus = status,
            Notes = $"Updated to {status}"
        };
    }
}

// TestDataBuilder.cs
public class TestDataBuilder
{
    private readonly VanAnDbContext _context;
    private readonly List<object> _entities = new();

    public TestDataBuilder(VanAnDbContext context)
    {
        _context = context;
    }

    public TestDataBuilder WithShop(TenantId tenantId, string shopName = "Test Shop")
    {
        var shop = new Shop
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = shopName,
            Address = "Test Address",
            Phone = "0123456789",
            Email = "test@shop.com",
            IsActive = true
        };
        _entities.Add(shop);
        return this;
    }

    public TestDataBuilder WithCustomer(CustomerId customerId, TenantId tenantId, string customerName = "Test Customer")
    {
        var customer = Customer.Create(
            deviceId: Guid.NewGuid(),
            displayName: customerName,
            tenantId: tenantId
        );
        _entities.Add(customer);
        return this;
    }

    public TestDataBuilder WithProduct(ProductId productId, TenantId tenantId, string productName = "Test Product")
    {
        var product = Product.Create(
            name: productName,
            description: "Test product description",
            basePrice: new Money(10000),
            tenantId: tenantId
        );
        _entities.Add(product);
        return this;
    }

    public TestDataBuilder WithOrder(OrderId orderId, CustomerId customerId, TenantId tenantId)
    {
        var itemsData = new List<OrderItemData>
        {
            new OrderItemData(
                productId: new ProductId(Guid.NewGuid()),
                quantity: new Quantity(2),
                unitPrice: new Money(10000),
                vatRate: new Percentage(0.10m),
                notes: "Test item"
            )
        };

        var order = Order.Create(
            customerId: customerId,
            tenantId: tenantId,
            orderType: OrderType.DineIn,
            customerNotes: "Test order",
            itemsData: itemsData
        );
        _entities.Add(order);
        return this;
    }

    public async Task BuildAsync()
    {
        await _context.AddRangeAsync(_entities);
        await _context.SaveChangesAsync();
    }

    public async Task ResetAsync()
    {
        _entities.Clear();
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.EnsureCreatedAsync();
    }
}

// MockFactory.cs
public class MockFactory : IDisposable
{
    private readonly List<IMock> _mocks = new();

    public TMock Create<TMock>() where TMock : class
    {
        var mock = new Mock<TMock>();
        _mocks.Add(mock);
        return mock.Object;
    }

    public TMock Create<TMock>(MockBehavior behavior) where TMock : class
    {
        var mock = new Mock<TMock>(behavior);
        _mocks.Add(mock);
        return mock.Object;
    }

    public void VerifyAll()
    {
        foreach (var mock in _mocks)
        {
            mock.VerifyAll();
        }
    }

    public void Dispose()
    {
        foreach (var mock in _mocks)
        {
            mock?.Reset();
        }
        _mocks.Clear();
    }
}
```

---

## **4. TEST EXECUTION FLOW LÝ T??NG**

### **4.1 Unit Test Execution**
```
1. Initialize Test Base
2. Create Mock Objects
3. Setup Test Data
4. Execute Test Logic
5. Verify Mock Interactions
6. Assert Results
7. Cleanup Resources
```

### **4.2 Integration Test Execution**
```
1. Initialize DI Container
2. Configure In-Memory Database
3. Register Services
4. Seed Test Data
5. Execute Test Logic
6. Verify Database State
7. Cleanup Database
8. Dispose Resources
```

### **4.3 API Test Execution**
```
1. Initialize Integration Test Base
2. Start Test Server
3. Configure HTTP Client
4. Setup Authentication
5. Execute HTTP Requests
6. Verify Responses
7. Cleanup Server
8. Dispose Resources
```

### **4.4 E2E Test Execution**
```
1. Initialize Playwright
2. Launch Headless Browser
3. Start Test Server
4. Configure Page Objects
5. Start Video Recording
6. Execute User Journey
7. Capture Screenshots
8. Stop Video Recording
9. Cleanup Browser
10. Dispose Resources
```

### **4.5 Load Test Execution**
```
1. Initialize Load Test Base
2. Configure Test Orchestration
3. Start Performance Monitoring
4. Execute Concurrent Operations
5. Collect Performance Metrics
6. Analyze Results
7. Generate Reports
8. Cleanup Resources
```

---

## **5. TESTING FRAMEWORK FEATURES**

### **5.1 Test Categories**
```csharp
[Trait("Category", "Unit")]
[Trait("Category", "Integration")]
[Trait("Category", "API")]
[Trait("Category", "E2E")]
[Trait("Category", "Performance")]
[Trait("Category", "Security")]
```

### **5.2 Test Priorities**
```csharp
[Trait("Priority", "Critical")]
[Trait("Priority", "High")]
[Trait("Priority", "Medium")]
[Trait("Priority", "Low")]
```

### **5.3 Test Environments**
```csharp
[Trait("Environment", "Development")]
[Trait("Environment", "Testing")]
[Trait("Environment", "Staging")]
[Trait("Environment", "Production")]
```

---

## **6. REPORTING & ANALYTICS**

### **6.1 Test Reports**
```csharp
public class TestReporter
{
    public async Task GenerateHtmlReportAsync(TestResults results)
    {
        // Generate HTML test report
    }

    public async Task GenerateJsonReportAsync(TestResults results)
    {
        // Generate JSON test report
    }

    public async Task GenerateCoverageReportAsync(CoverageData coverage)
    {
        // Generate code coverage report
    }

    public async Task GeneratePerformanceReportAsync(PerformanceMetrics metrics)
    {
        // Generate performance test report
    }
}
```

### **6.2 Test Dashboard**
```csharp
public class TestDashboard
{
    public TestSummary GetTestSummary()
    {
        return new TestSummary
        {
            TotalTests = GetTotalTestCount(),
            PassedTests = GetPassedTestCount(),
            FailedTests = GetFailedTestCount(),
            SkippedTests = GetSkippedTestCount(),
            CoveragePercentage = GetCoveragePercentage(),
            AverageExecutionTime = GetAverageExecutionTime()
        };
    }

    public List<TestTrend> GetTestTrends()
    {
        // Get historical test trends
    }

    public List<FlakyTest> GetFlakyTests()
    {
        // Get flaky test analysis
    }
}
```

---

## **7. SUMMARY**

### **7.1 Key Features of Ideal Test Module**
- **6-Layer Testing Pyramid:** Comprehensive coverage
- **Proper Test Isolation:** No test interference
- **Headless E2E Testing:** CI/CD compatible
- **Load Testing:** Performance validation
- **Security Testing:** Vulnerability assessment
- **Test Data Factories:** Flexible test data
- **Mock Framework:** Proper unit testing
- **Coverage Reporting:** Quality metrics
- **Test Dashboard:** Real-time monitoring
- **Parallel Execution:** Fast test runs

### **7.2 Technical Excellence**
- **Clean Architecture:** Proper test organization
- **SOLID Principles:** Single responsibility
- **Test Automation:** Full CI/CD integration
- **Performance Optimization:** Fast test execution
- **Error Handling:** Robust failure recovery
- **Logging:** Comprehensive test logging
- **Documentation:** Self-documenting tests

### **7.3 Business Value**
- **Quality Assurance:** High code quality
- **Regression Prevention:** Catch issues early
- **Performance Monitoring:** Ensure scalability
- **Security Validation:** Protect against vulnerabilities
- **Continuous Integration:** Automated quality gates
- **Risk Mitigation:** Reduce production issues

This ideal Test module provides a comprehensive, professional-grade testing framework that ensures code quality, performance, and security while maintaining fast execution times and excellent developer experience.
