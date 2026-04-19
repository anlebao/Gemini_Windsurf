# CoreHub - Comparison and Improvement Plan

**Date:** April 11, 2026  
**Module:** 3_CoreHub  
**Status:** Analysis comparison and improvement planning

---

## **1. REALISTIC vs IDEAL COMPARISON**

### **1.1 Architecture Comparison**

| **Aspect** | **Realistic** | **Ideal** | **Gap Analysis** |
|------------|--------------|-----------|------------------|
| **Architecture** | Basic services | Clean Architecture with DDD | **High** - Need complete redesign |
| **Domain Logic** | Limited business logic | Rich domain models | **High** - Need domain enhancement |
| **Service Layer** | Basic CRUD operations | Rich domain services | **High** - Need service enhancement |
| **Data Access** | Basic repository pattern | Advanced repository with specifications | **Medium** - Need repository enhancement |
| **Event System** | No domain events | Event-driven architecture | **High** - Need event implementation |

### **1.2 Business Logic Comparison**

| **Aspect** | **Realistic** | **Ideal** | **Gap Analysis** |
|------------|--------------|-----------|------------------|
| **Domain Rules** | Hard-coded in services | Domain rules engine | **High** - Need rules engine |
| **Business Processes** | Simple workflows | Complex process orchestration | **High** - Need process enhancement |
| **Validation** | Basic attributes | FluentValidation with business rules | **Medium** - Need validation framework |
| **Calculations** | Simple math | Complex business calculations | **Medium** - Need calculation engine |
| **Workflows** | No workflow system | Workflow orchestration | **High** - Need workflow system |

### **1.3 Data Access Comparison**

| **Aspect** | **Realistic** | **Ideal** | **Gap Analysis** |
|------------|--------------|-----------|------------------|
| **Repository Pattern** | Basic implementation | Advanced with specifications | **Medium** - Need repository enhancement |
| **Query Optimization** | Basic LINQ queries | Optimized queries with caching | **Medium** - Need query optimization |
| **Transaction Management** | Basic transactions | Advanced transaction handling | **Medium** - Need transaction enhancement |
| **Data Migrations** | Basic migrations | Advanced migration strategies | **Medium** - Need migration enhancement |
| **Connection Pooling** | Default EF Core pooling | Optimized connection management | **Low** - Minor optimization needed |

### **1.4 Performance & Scalability Comparison**

| **Aspect** | **Realistic** | **Ideal** | **Gap Analysis** |
|------------|--------------|-----------|------------------|
| **Caching** | No caching | Multi-layer caching strategy | **High** - Need caching implementation |
| **Async Operations** | Limited async | Full async implementation | **Medium** - Need async patterns |
| **Batch Processing** | No batch operations | Efficient batch processing | **Medium** - Need batch operations |
| **Memory Management** | Basic EF Core tracking | Optimized memory usage | **Medium** - Need memory optimization |
| **Scalability** | Single instance | Horizontal scaling support | **High** - Need scaling strategy |

---

## **2. PRIORITY ASSESSMENT**

### **2.1 Critical Issues (Priority 1)**
1. **No Domain Events** - Complete lack of event-driven architecture
2. **No Rich Domain Models** - Basic POCO entities only
3. **No Business Rules Engine** - Hard-coded business logic
4. **No CQRS Implementation** - Simple CRUD operations only
5. **No Performance Optimization** - No caching or optimization

### **2.2 Important Issues (Priority 2)**
1. **No Workflow System** - No process orchestration
2. **No Advanced Repository** - Basic repository pattern only
3. **No Validation Framework** - Basic validation only
4. **No Batch Processing** - No efficient bulk operations
5. **No Transaction Management** - Basic transactions only

### **2.3 Nice to Have (Priority 3)**
1. **No Business Intelligence** - No analytics or reporting
2. **No Advanced Caching** - No distributed caching
3. **No Event Sourcing** - No event persistence
4. **No Saga Pattern** - No distributed transactions
5. **No Advanced Monitoring** - Limited performance monitoring

---

## **3. IMPROVEMENT PLAN**

### **3.1 Phase 1: Domain Enhancement (Week 1-2)**

#### **Day 1-3: Rich Domain Models**
```csharp
// Domain/Order.cs - Enhanced with domain logic
public class Order : AggregateRoot, IAggregateRoot
{
    private readonly List<OrderItem> _items = new();
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public Money TotalAmount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public Guid? AssignedStaffId { get; private set; }
    
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private Order() { } // For EF Core

    public Order(Guid customerId, List<OrderItem> items)
    {
        Id = Guid.NewGuid();
        CustomerId = customerId;
        Status = OrderStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        
        ValidateOrder(items);
        
        foreach (var item in items)
        {
            _items.Add(item);
        }
        
        CalculateTotalAmount();
        
        AddDomainEvent(new OrderCreatedEvent(Id, CustomerId, TotalAmount));
    }

    public void AddItem(ProductId productId, int quantity, Money unitPrice)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than 0");
        
        if (Status != OrderStatus.Pending)
            throw new DomainException("Cannot add items to order in status: " + Status);

        var existingItem = _items.FirstOrDefault(i => i.ProductId == productId);
        if (existingItem != null)
        {
            existingItem.UpdateQuantity(existingItem.Quantity + quantity);
        }
        else
        {
            var newItem = new OrderItem(productId, quantity, unitPrice);
            _items.Add(newItem);
        }

        CalculateTotalAmount();
        
        AddDomainEvent(new OrderItemAddedEvent(Id, productId, quantity, unitPrice));
    }

    public void RemoveItem(ProductId productId)
    {
        if (Status != OrderStatus.Pending)
            throw new DomainException("Cannot remove items from order in status: " + Status);

        var item = _items.FirstOrDefault(i => i.ProductId == productId);
        if (item != null)
        {
            _items.Remove(item);
            CalculateTotalAmount();
            
            AddDomainEvent(new OrderItemRemovedEvent(Id, productId));
        }
    }

    public void ConfirmOrder()
    {
        if (Status != OrderStatus.Pending)
            throw new DomainException("Order must be in Pending status to confirm");

        Status = OrderStatus.Confirmed;
        
        AddDomainEvent(new OrderConfirmedEvent(Id, CustomerId, TotalAmount));
    }

    public void StartPreparation(Guid staffId)
    {
        if (Status != OrderStatus.Confirmed)
            throw new DomainException("Order must be Confirmed to start preparation");

        Status = OrderStatus.InPreparation;
        AssignedStaffId = staffId;
        
        AddDomainEvent(new OrderPreparationStartedEvent(Id, staffId));
    }

    public void CompleteOrder()
    {
        if (Status != OrderStatus.InPreparation)
            throw new DomainException("Order must be In Preparation to complete");

        Status = OrderStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        
        AddDomainEvent(new OrderCompletedEvent(Id, CustomerId, TotalAmount, CompletedAt.Value));
    }

    public void CancelOrder(string reason)
    {
        if (Status == OrderStatus.Completed)
            throw new DomainException("Cannot cancel completed order");

        Status = OrderStatus.Cancelled;
        
        AddDomainEvent(new OrderCancelledEvent(Id, CustomerId, reason));
    }

    private void ValidateOrder(List<OrderItem> items)
    {
        if (!items.Any())
            throw new DomainException("Order must have at least one item");

        if (CustomerId == Guid.Empty)
            throw new DomainException("Customer ID is required");

        foreach (var item in items)
        {
            if (item.Quantity <= 0)
                throw new DomainException($"Item {item.ProductId} has invalid quantity");
        }
    }

    private void CalculateTotalAmount()
    {
        TotalAmount = _items.Sum(item => item.TotalPrice);
    }

    protected override void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

// Domain/OrderItem.cs - Enhanced with domain logic
public class OrderItem : ValueObject
{
    public ProductId ProductId { get; private set; }
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; }
    public Money TotalPrice => UnitPrice * Quantity;

    protected OrderItem() { } // For EF Core

    public OrderItem(ProductId productId, int quantity, Money unitPrice)
    {
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        
        Validate();
    }

    public void UpdateQuantity(int newQuantity)
    {
        if (newQuantity <= 0)
            throw new DomainException("Quantity must be greater than 0");

        Quantity = newQuantity;
    }

    public void UpdateUnitPrice(Money newUnitPrice)
    {
        if (newUnitPrice <= 0)
            throw new DomainException("Unit price must be greater than 0");

        UnitPrice = newUnitPrice;
    }

    private void Validate()
    {
        if (ProductId == Guid.Empty)
            throw new DomainException("Product ID is required");

        if (Quantity <= 0)
            throw new DomainException("Quantity must be greater than 0");

        if (UnitPrice <= 0)
            throw new DomainException("Unit price must be greater than 0");
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return ProductId;
        yield return Quantity;
        yield return UnitPrice;
    }
}

// Domain/ValueObjects/Money.cs
public class Money : ValueObject
{
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }

    public static Money Zero => new Money(0, "VND");

    protected Money() { } // For EF Core

    public Money(decimal amount, string currency = "VND")
    {
        if (amount < 0)
            throw new DomainException("Amount cannot be negative");

        if (string.IsNullOrEmpty(currency))
            throw new DomainException("Currency is required");

        Amount = amount;
        Currency = currency;
    }

    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new DomainException("Cannot add money with different currencies");

        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator *(Money money, int multiplier)
    {
        return new Money(money.Amount * multiplier, money.Currency);
    }

    public static Money operator *(Money money, decimal multiplier)
    {
        return new Money(money.Amount * multiplier, money.Currency);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
```

#### **Day 4-5: Domain Events System**
```csharp
// Domain/Events/IDomainEvent.cs
public interface IDomainEvent
{
    DateTime OccurredOn { get; }
    Guid EventId { get; }
}

// Domain/Events/OrderCreatedEvent.cs
public class OrderCreatedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    
    public Guid OrderId { get; }
    public Guid CustomerId { get; }
    public Money TotalAmount { get; }

    public OrderCreatedEvent(Guid orderId, Guid customerId, Money totalAmount)
    {
        OrderId = orderId;
        CustomerId = customerId;
        TotalAmount = totalAmount;
    }
}

// Domain/Events/OrderConfirmedEvent.cs
public class OrderConfirmedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    
    public Guid OrderId { get; }
    public Guid CustomerId { get; }
    public Money TotalAmount { get; }

    public OrderConfirmedEvent(Guid orderId, Guid customerId, Money totalAmount)
    {
        OrderId = orderId;
        CustomerId = customerId;
        TotalAmount = totalAmount;
    }
}

// Domain/Events/OrderCompletedEvent.cs
public class OrderCompletedEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    
    public Guid OrderId { get; }
    public Guid CustomerId { get; }
    public Money TotalAmount { get; }
    public DateTime CompletedAt { get; }

    public OrderCompletedEvent(Guid orderId, Guid customerId, Money totalAmount, DateTime completedAt)
    {
        OrderId = orderId;
        CustomerId = customerId;
        TotalAmount = totalAmount;
        CompletedAt = completedAt;
    }
}

// Infrastructure/Events/DomainEventDispatcher.cs
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IDomainEvent domainEvent);
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents);
}

public class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(IServiceProvider serviceProvider, ILogger<DomainEventDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task DispatchAsync(IDomainEvent domainEvent)
    {
        var eventType = domainEvent.GetType();
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);

        var handlers = _serviceProvider.GetServices(handlerType);
        
        foreach (var handler in handlers)
        {
            try
            {
                var method = handlerType.GetMethod("HandleAsync", new[] { eventType });
                await (Task)method.Invoke(handler, new[] { domainEvent });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling domain event {EventType}", eventType.Name);
                throw;
            }
        }
    }

    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents)
    {
        foreach (var domainEvent in domainEvents)
        {
            await DispatchAsync(domainEvent);
        }
    }
}

// Infrastructure/Events/IDomainEventHandler.cs
public interface IDomainEventHandler<T> where T : IDomainEvent
{
    Task HandleAsync(T domainEvent);
}

// Application/EventHandlers/OrderCreatedEventHandler.cs
public class OrderCreatedEventHandler : IDomainEventHandler<OrderCreatedEvent>
{
    private readonly IInventoryService _inventoryService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<OrderCreatedEventHandler> _logger;

    public OrderCreatedEventHandler(
        IInventoryService inventoryService,
        INotificationService notificationService,
        ILogger<OrderCreatedEventHandler> logger)
    {
        _inventoryService = inventoryService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(OrderCreatedEvent domainEvent)
    {
        _logger.LogInformation("Handling OrderCreatedEvent for order {OrderId}", domainEvent.OrderId);

        // Reserve inventory
        await _inventoryService.ReserveInventoryAsync(domainEvent.OrderId);

        // Send notification to customer
        await _notificationService.SendOrderConfirmationAsync(domainEvent.CustomerId, domainEvent.OrderId);

        // Send notification to kitchen staff
        await _notificationService.SendNewOrderNotificationAsync(domainEvent.OrderId);
    }
}

// Application/EventHandlers/OrderCompletedEventHandler.cs
public class OrderCompletedEventHandler : IDomainEventHandler<OrderCompletedEvent>
{
    private readonly IInventoryService _inventoryService;
    private readonly IPaymentService _paymentService;
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<OrderCompletedEventHandler> _logger;

    public OrderCompletedEventHandler(
        IInventoryService inventoryService,
        IPaymentService paymentService,
        IAnalyticsService analyticsService,
        ILogger<OrderCompletedEventHandler> logger)
    {
        _inventoryService = inventoryService;
        _paymentService = paymentService;
        _analyticsService = analyticsService;
        _logger = logger;
    }

    public async Task HandleAsync(OrderCompletedEvent domainEvent)
    {
        _logger.LogInformation("Handling OrderCompletedEvent for order {OrderId}", domainEvent.OrderId);

        // Deduct inventory
        await _inventoryService.DeductInventoryAsync(domainEvent.OrderId);

        // Process payment
        await _paymentService.ProcessPaymentAsync(domainEvent.OrderId);

        // Update analytics
        await _analyticsService.RecordCompletedOrderAsync(domainEvent.OrderId, domainEvent.TotalAmount);

        // Send completion notification
        await _notificationService.SendOrderCompletionNotificationAsync(domainEvent.CustomerId, domainEvent.OrderId);
    }
}
```

#### **Day 6-7: Business Rules Engine**
```csharp
// Domain/Rules/IBusinessRule.cs
public interface IBusinessRule
{
    bool IsBroken();
    string Message { get; }
}

// Domain/Rules/OrderMustHaveAtLeastOneItem.cs
public class OrderMustHaveAtLeastOneItem : IBusinessRule
{
    private readonly List<OrderItem> _items;

    public OrderMustHaveAtLeastOneItem(List<OrderItem> items)
    {
        _items = items;
    }

    public bool IsBroken() => !_items.Any();

    public string Message => "Order must have at least one item";
}

// Domain/Rules/OrderQuantityMustBeValid.cs
public class OrderQuantityMustBeValid : IBusinessRule
{
    private readonly List<OrderItem> _items;

    public OrderQuantityMustBeValid(List<OrderItem> items)
    {
        _items = items;
    }

    public bool IsBroken() => _items.Any(item => item.Quantity <= 0);

    public string Message => "All order items must have quantity greater than 0";
}

// Domain/Rules/BusinessRuleValidator.cs
public class BusinessRuleValidator
{
    public static void Validate(params IBusinessRule[] rules)
    {
        var brokenRules = rules.Where(rule => rule.IsBroken()).ToList();
        
        if (brokenRules.Any())
        {
            throw new BusinessRuleValidationException(brokenRules);
        }
    }
}

// Domain/Exceptions/BusinessRuleValidationException.cs
public class BusinessRuleValidationException : DomainException
{
    public IReadOnlyCollection<IBusinessRule> BrokenRules { get; }

    public BusinessRuleValidationException(IEnumerable<IBusinessRule> brokenRules)
        : base(BuildErrorMessage(brokenRules))
    {
        BrokenRules = brokenRules.ToList().AsReadOnly();
    }

    private static string BuildErrorMessage(IEnumerable<IBusinessRule> brokenRules)
    {
        var messages = brokenRules.Select(rule => rule.Message);
        return string.Join(Environment.NewLine, messages);
    }
}

// Domain/Services/OrderDomainService.cs
public class OrderDomainService
{
    private readonly IProductRepository _productRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IPromotionService _promotionService;

    public OrderDomainService(
        IProductRepository productRepository,
        ICustomerRepository customerRepository,
        IPromotionService promotionService)
    {
        _productRepository = productRepository;
        _customerRepository = customerRepository;
        _promotionService = promotionService;
    }

    public async Task<Order> CreateOrderAsync(Guid customerId, List<CreateOrderItemRequest> items)
    {
        // Validate customer exists
        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer == null)
            throw new DomainException($"Customer {customerId} not found");

        // Validate products and get prices
        var productIds = items.Select(i => i.ProductId).ToList();
        var products = await _productRepository.GetByIdsAsync(productIds);
        
        if (products.Count != productIds.Count)
            throw new DomainException("One or more products not found");

        // Create order items with current prices
        var orderItems = items.Select(item =>
        {
            var product = products.First(p => p.Id == item.ProductId);
            return new OrderItem(item.ProductId, item.Quantity, product.Price);
        }).ToList();

        // Apply business rules
        BusinessRuleValidator.Validate(
            new OrderMustHaveAtLeastOneItem(orderItems),
            new OrderQuantityMustBeValid(orderItems)
        );

        // Create order
        var order = new Order(customerId, orderItems);

        // Apply promotions
        await _promotionService.ApplyPromotionsAsync(order);

        return order;
    }

    public async Task<bool> CanCancelOrderAsync(Order order)
    {
        // Business rule: Orders can be cancelled within 5 minutes of creation
        if (order.Status == OrderStatus.Completed)
            return false;

        if (DateTime.UtcNow - order.CreatedAt > TimeSpan.FromMinutes(5))
            return false;

        // Check if any items are already being prepared
        // This would involve checking with the kitchen service
        return true;
    }

    public async Task<Money> CalculateDeliveryFeeAsync(Order order)
    {
        // Business rule: Free delivery for orders over 100,000 VND
        if (order.TotalAmount >= 100000)
            return Money.Zero;

        // Calculate distance-based delivery fee
        var customer = await _customerRepository.GetByIdAsync(order.CustomerId);
        var distance = await CalculateDistanceAsync(customer.Address);
        
        return distance switch
        {
            <= 5 => new Money(15000), // 5km or less
            <= 10 => new Money(25000), // 5-10km
            _ => new Money(35000) // Over 10km
        };
    }

    private async Task<double> CalculateDistanceAsync(Address address)
    {
        // Implementation would integrate with mapping service
        return await Task.FromResult(5.0); // Placeholder
    }
}
```

### **3.2 Phase 2: Service Layer Enhancement (Week 3-4)**

#### **Day 8-10: Rich Domain Services**
```csharp
// Application/Services/OrderService.cs
public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderDomainService _orderDomainService;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        IOrderDomainService orderDomainService,
        IDomainEventDispatcher eventDispatcher,
        IUnitOfWork unitOfWork,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _orderDomainService = orderDomainService;
        _eventDispatcher = eventDispatcher;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OrderResult> CreateOrderAsync(CreateOrderRequest request)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync();

            var order = await _orderDomainService.CreateOrderAsync(request.CustomerId, request.Items);
            
            await _orderRepository.AddAsync(order);
            await _unitOfWork.SaveChangesAsync();

            await _eventDispatcher.DispatchAsync(order.DomainEvents);
            order.ClearDomainEvents();

            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Order {OrderId} created successfully", order.Id);

            return OrderResult.Success(order);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync();
            _logger.LogError(ex, "Error creating order for customer {CustomerId}", request.CustomerId);
            return OrderResult.Failure(ex.Message);
        }
    }

    public async Task<OrderResult> UpdateOrderAsync(UpdateOrderRequest request)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync();

            var order = await _orderRepository.GetByIdAsync(request.OrderId);
            if (order == null)
                return OrderResult.Failure("Order not found");

            // Update order logic based on request
            if (request.ItemsToAdd != null)
            {
                foreach (var item in request.ItemsToAdd)
                {
                    order.AddItem(item.ProductId, item.Quantity, item.UnitPrice);
                }
            }

            if (request.ItemsToRemove != null)
            {
                foreach (var productId in request.ItemsToRemove)
                {
                    order.RemoveItem(productId);
                }
            }

            await _orderRepository.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();

            await _eventDispatcher.DispatchAsync(order.DomainEvents);
            order.ClearDomainEvents();

            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Order {OrderId} updated successfully", order.Id);

            return OrderResult.Success(order);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync();
            _logger.LogError(ex, "Error updating order {OrderId}", request.OrderId);
            return OrderResult.Failure(ex.Message);
        }
    }

    public async Task<OrderResult> CancelOrderAsync(Guid orderId, string reason)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync();

            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
                return OrderResult.Failure("Order not found");

            if (!await _orderDomainService.CanCancelOrderAsync(order))
                return OrderResult.Failure("Order cannot be cancelled");

            order.CancelOrder(reason);

            await _orderRepository.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();

            await _eventDispatcher.DispatchAsync(order.DomainEvents);
            order.ClearDomainEvents();

            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Order {OrderId} cancelled successfully", orderId);

            return OrderResult.Success(order);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync();
            _logger.LogError(ex, "Error cancelling order {OrderId}", orderId);
            return OrderResult.Failure(ex.Message);
        }
    }

    public async Task<OrderDto> GetByIdAsync(Guid orderId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        return order == null ? null : MapToDto(order);
    }

    public async Task<List<OrderDto>> GetByCustomerIdAsync(Guid customerId, int page = 1, int pageSize = 20)
    {
        var orders = await _orderRepository.GetByCustomerIdAsync(customerId, page, pageSize);
        return orders.Select(MapToDto).ToList();
    }

    private OrderDto MapToDto(Order order)
    {
        return new OrderDto
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            Status = order.Status,
            TotalAmount = order.TotalAmount,
            CreatedAt = order.CreatedAt,
            CompletedAt = order.CompletedAt,
            Items = order.Items.Select(item => new OrderItemDto
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName, // Would need to be loaded
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice
            }).ToList()
        };
    }
}

// Application/Services/InventoryService.cs
public class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IProductRepository _productRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(
        IInventoryRepository inventoryRepository,
        IProductRepository productRepository,
        IDomainEventDispatcher eventDispatcher,
        IUnitOfWork unitOfWork,
        ILogger<InventoryService> logger)
    {
        _inventoryRepository = inventoryRepository;
        _productRepository = productRepository;
        _eventDispatcher = eventDispatcher;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> ReserveInventoryAsync(Guid orderId)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync();

            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
                return false;

            foreach (var item in order.Items)
            {
                var inventory = await _inventoryRepository.GetByProductIdAsync(item.ProductId);
                if (inventory == null)
                {
                    await _unitOfWork.RollbackAsync();
                    return false;
                }

                if (!inventory.HasEnoughStock(item.Quantity))
                {
                    await _unitOfWork.RollbackAsync();
                    return false;
                }

                inventory.ReserveStock(item.Quantity);
                await _inventoryRepository.UpdateAsync(inventory);
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Inventory reserved for order {OrderId}", orderId);
            return true;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync();
            _logger.LogError(ex, "Error reserving inventory for order {OrderId}", orderId);
            return false;
        }
    }

    public async Task<bool> DeductInventoryAsync(Guid orderId)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync();

            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
                return false;

            foreach (var item in order.Items)
            {
                var inventory = await _inventoryRepository.GetByProductIdAsync(item.ProductId);
                if (inventory == null)
                {
                    await _unitOfWork.RollbackAsync();
                    return false;
                }

                inventory.DeductStock(item.Quantity);
                await _inventoryRepository.UpdateAsync(inventory);

                // Check if stock is low and create alert
                if (inventory.IsLowStock)
                {
                    await _eventDispatcher.DispatchAsync(new LowStockAlertEvent(inventory.ProductId, inventory.CurrentStock));
                }
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Inventory deducted for order {OrderId}", orderId);
            return true;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync();
            _logger.LogError(ex, "Error deducting inventory for order {OrderId}", orderId);
            return false;
        }
    }

    public async Task<bool> RestockInventoryAsync(Guid productId, int quantity)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync();

            var inventory = await _inventoryRepository.GetByProductIdAsync(productId);
            if (inventory == null)
            {
                inventory = new Inventory(productId, quantity);
                await _inventoryRepository.AddAsync(inventory);
            }
            else
            {
                inventory.Restock(quantity);
                await _inventoryRepository.UpdateAsync(inventory);
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Inventory restocked for product {ProductId}, quantity: {Quantity}", productId, quantity);
            return true;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync();
            _logger.LogError(ex, "Error restocking inventory for product {ProductId}", productId);
            return false;
        }
    }
}
```

#### **Day 11-12: CQRS Implementation**
```csharp
// Application/Commands/CreateOrderCommand.cs
public class CreateOrderCommand : IRequest<OrderResult>
{
    public Guid CustomerId { get; set; }
    public List<CreateOrderItemDto> Items { get; set; }
}

// Application/Commands/CreateOrderCommandHandler.cs
public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, OrderResult>
{
    private readonly IOrderService _orderService;
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    public CreateOrderCommandHandler(IOrderService orderService, ILogger<CreateOrderCommandHandler> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    public async Task<OrderResult> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating order for customer {CustomerId}", request.CustomerId);

        var createRequest = new CreateOrderRequest
        {
            CustomerId = request.CustomerId,
            Items = request.Items.Select(i => new CreateOrderItemRequest
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList()
        };

        return await _orderService.CreateOrderAsync(createRequest);
    }
}

// Application/Queries/GetOrderByIdQuery.cs
public class GetOrderByIdQuery : IRequest<OrderDto>
{
    public Guid OrderId { get; set; }
}

// Application/Queries/GetOrderByIdQueryHandler.cs
public class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderDto>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<GetOrderByIdQueryHandler> _logger;

    public GetOrderByIdQueryHandler(
        IOrderRepository orderRepository,
        IMapper mapper,
        ILogger<GetOrderByIdQueryHandler> logger)
    {
        _orderRepository = orderRepository;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<OrderDto> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting order {OrderId}", request.OrderId);

        var order = await _orderRepository.GetByIdAsync(request.OrderId);
        return order == null ? null : _mapper.Map<OrderDto>(order);
    }
}

// Application/Queries/GetOrdersByCustomerQuery.cs
public class GetOrdersByCustomerQuery : IRequest<PagedResult<OrderDto>>
{
    public Guid CustomerId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

// Application/Queries/GetOrdersByCustomerQueryHandler.cs
public class GetOrdersByCustomerQueryHandler : IRequestHandler<GetOrdersByCustomerQuery, PagedResult<OrderDto>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<GetOrdersByCustomerQueryHandler> _logger;

    public GetOrdersByCustomerQueryHandler(
        IOrderRepository orderRepository,
        IMapper mapper,
        ILogger<GetOrdersByCustomerQueryHandler> logger)
    {
        _orderRepository = orderRepository;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<PagedResult<OrderDto>> Handle(GetOrdersByCustomerQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting orders for customer {CustomerId}, page {Page}", request.CustomerId, request.Page);

        var orders = await _orderRepository.GetByCustomerIdAsync(request.CustomerId, request.Page, request.PageSize);
        var totalCount = await _orderRepository.GetCountByCustomerIdAsync(request.CustomerId);

        var orderDtos = _mapper.Map<List<OrderDto>>(orders);

        return new PagedResult<OrderDto>
        {
            Items = orderDtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / request.PageSize)
        };
    }
}
```

### **3.3 Phase 3: Event-Driven Architecture (Week 5-6)**

#### **Day 13-15: Event Sourcing**
```csharp
// Domain/Events/IEventStore.cs
public interface IEventStore
{
    Task SaveEventsAsync(Guid aggregateId, IEnumerable<IDomainEvent> events, int expectedVersion);
    Task<List<IDomainEvent>> GetEventsAsync(Guid aggregateId, int fromVersion = 0);
    Task<List<IDomainEvent>> GetEventsAsync(Guid aggregateId, DateTime fromTimestamp);
}

// Infrastructure/Events/InMemoryEventStore.cs
public class InMemoryEventStore : IEventStore
{
    private readonly Dictionary<Guid, List<EventDescriptor>> _events = new();
    private readonly ILogger<InMemoryEventStore> _logger;

    public InMemoryEventStore(ILogger<InMemoryEventStore> logger)
    {
        _logger = logger;
    }

    public async Task SaveEventsAsync(Guid aggregateId, IEnumerable<IDomainEvent> events, int expectedVersion)
    {
        if (!_events.ContainsKey(aggregateId))
        {
            _events[aggregateId] = new List<EventDescriptor>();
        }

        var eventList = _events[aggregateId];
        var currentVersion = eventList.Count;

        if (currentVersion != expectedVersion)
        {
            throw new ConcurrencyException($"Expected version {expectedVersion} but found {currentVersion}");
        }

        var version = currentVersion;
        foreach (var @event in events)
        {
            eventList.Add(new EventDescriptor
            {
                EventId = @event.EventId,
                EventData = @event,
                EventType = @event.GetType().Name,
                Version = ++version,
                Timestamp = @event.OccurredOn
            });
        }

        await Task.CompletedTask;
        _logger.LogInformation("Saved {Count} events for aggregate {AggregateId}", events.Count(), aggregateId);
    }

    public async Task<List<IDomainEvent>> GetEventsAsync(Guid aggregateId, int fromVersion = 0)
    {
        if (!_events.ContainsKey(aggregateId))
        {
            return new List<IDomainEvent>();
        }

        var eventList = _events[aggregateId]
            .Where(e => e.Version > fromVersion)
            .OrderBy(e => e.Version)
            .Select(e => e.EventData)
            .ToList();

        await Task.CompletedTask;
        return eventList;
    }

    public async Task<List<IDomainEvent>> GetEventsAsync(Guid aggregateId, DateTime fromTimestamp)
    {
        if (!_events.ContainsKey(aggregateId))
        {
            return new List<IDomainEvent>();
        }

        var eventList = _events[aggregateId]
            .Where(e => e.Timestamp >= fromTimestamp)
            .OrderBy(e => e.Version)
            .Select(e => e.EventData)
            .ToList();

        await Task.CompletedTask;
        return eventList;
    }

    private class EventDescriptor
    {
        public Guid EventId { get; set; }
        public IDomainEvent EventData { get; set; }
        public string EventType { get; set; }
        public int Version { get; set; }
        public DateTime Timestamp { get; set; }
    }
}

// Domain/Aggregates/EventSourcedAggregateRoot.cs
public abstract class EventSourcedAggregateRoot : IAggregateRoot
{
    private readonly List<IDomainEvent> _changes = new();
    private int _version = -1;

    public Guid Id { get; protected set; }
    public int Version => _version;
    public IReadOnlyCollection<IDomainEvent> GetUncommittedChanges() => _changes.AsReadOnly();

    public void MarkChangesAsCommitted()
    {
        _changes.Clear();
    }

    public void LoadFromHistory(IEnumerable<IDomainEvent> history)
    {
        foreach (var e in history)
        {
            ApplyChange(e, true);
        }
    }

    protected void ApplyChange(IDomainEvent @event)
    {
        ApplyChange(@event, false);
    }

    private void ApplyChange(IDomainEvent @event, bool isHistorical)
    {
        dynamic d = this;
        d.Apply((dynamic)@event);

        if (!isHistorical)
        {
            _changes.Add(@event);
        }
        else
        {
            _version++;
        }
    }
}

// Application/Services/EventSourcedOrderService.cs
public class EventSourcedOrderService : IOrderService
{
    private readonly IEventStore _eventStore;
    private readonly IOrderRepository _orderRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly ILogger<EventSourcedOrderService> _logger;

    public EventSourcedOrderService(
        IEventStore eventStore,
        IOrderRepository orderRepository,
        IDomainEventDispatcher eventDispatcher,
        ILogger<EventSourcedOrderService> logger)
    {
        _eventStore = eventStore;
        _orderRepository = orderRepository;
        _eventDispatcher = eventDispatcher;
        _logger = logger;
    }

    public async Task<OrderResult> CreateOrderAsync(CreateOrderRequest request)
    {
        try
        {
            var order = new EventSourcedOrder(request.CustomerId, request.Items);
            
            await _eventStore.SaveEventsAsync(order.Id, order.GetUncommittedChanges(), -1);
            
            await _eventDispatcher.DispatchAsync(order.GetUncommittedChanges());
            
            order.MarkChangesAsCommitted();

            _logger.LogInformation("Order {OrderId} created successfully", order.Id);

            return OrderResult.Success(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order for customer {CustomerId}", request.CustomerId);
            return OrderResult.Failure(ex.Message);
        }
    }

    public async Task<OrderDto> GetByIdAsync(Guid orderId)
    {
        var events = await _eventStore.GetEventsAsync(orderId);
        if (!events.Any())
        {
            return null;
        }

        var order = new EventSourcedOrder();
        order.LoadFromHistory(events);

        return MapToDto(order);
    }

    // Other methods would be implemented similarly...
}
```

#### **Day 16-17: Saga Pattern**
```csharp
// Application/Sagas/ISaga.cs
public interface ISaga
{
    Guid SagaId { get; }
    SagaStatus Status { get; }
    List<ISagaData> SagaData { get; }
    Task HandleAsync(ISagaEvent sagaEvent);
    Task CompensateAsync();
}

// Application/Sagas/SagaStatus.cs
public enum SagaStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Compensating,
    Compensated
}

// Application/Sagas/OrderProcessingSaga.cs
public class OrderProcessingSaga : ISaga
{
    public Guid SagaId { get; private set; }
    public SagaStatus Status { get; private set; }
    public List<ISagaData> SagaData { get; private set; } = new();

    private readonly IOrderService _orderService;
    private readonly IInventoryService _inventoryService;
    private readonly IPaymentService _paymentService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<OrderProcessingSaga> _logger;

    public OrderProcessingSaga(
        IOrderService orderService,
        IInventoryService inventoryService,
        IPaymentService paymentService,
        INotificationService notificationService,
        ILogger<OrderProcessingSaga> logger)
    {
        _orderService = orderService;
        _inventoryService = inventoryService;
        _paymentService = paymentService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(ISagaEvent sagaEvent)
    {
        switch (sagaEvent)
        {
            case OrderCreatedEvent orderCreated:
                await HandleOrderCreatedAsync(orderCreated);
                break;
            case InventoryReservedEvent inventoryReserved:
                await HandleInventoryReservedAsync(inventoryReserved);
                break;
            case PaymentProcessedEvent paymentProcessed:
                await HandlePaymentProcessedAsync(paymentProcessed);
                break;
            case PaymentFailedEvent paymentFailed:
                await HandlePaymentFailedAsync(paymentFailed);
                break;
            case InventoryReservationFailedEvent inventoryFailed:
                await HandleInventoryReservationFailedAsync(inventoryFailed);
                break;
        }
    }

    private async Task HandleOrderCreatedAsync(OrderCreatedEvent @event)
    {
        Status = SagaStatus.Running;
        _logger.LogInformation("Processing order {OrderId}", @event.OrderId);

        try
        {
            // Reserve inventory
            var inventoryReserved = await _inventoryService.ReserveInventoryAsync(@event.OrderId);
            if (!inventoryReserved)
            {
                await CompensateAsync();
                return;
            }

            // Process payment
            var paymentProcessed = await _paymentService.ProcessPaymentAsync(@event.OrderId, @event.TotalAmount);
            if (!paymentProcessed)
            {
                await CompensateAsync();
                return;
            }

            Status = SagaStatus.Completed;
            _logger.LogInformation("Order processing completed for order {OrderId}", @event.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order {OrderId}", @event.OrderId);
            await CompensateAsync();
        }
    }

    private async Task HandleInventoryReservedAsync(InventoryReservedEvent @event)
    {
        // Continue with payment processing
        _logger.LogInformation("Inventory reserved for order {OrderId}", @event.OrderId);
    }

    private async Task HandlePaymentProcessedAsync(PaymentProcessedEvent @event)
    {
        // Complete the order
        await _orderService.CompleteOrderAsync(@event.OrderId);
        await _notificationService.SendOrderCompletedNotificationAsync(@event.OrderId);
        
        Status = SagaStatus.Completed;
        _logger.LogInformation("Order processing completed for order {OrderId}", @event.OrderId);
    }

    private async Task HandlePaymentFailedAsync(PaymentFailedEvent @event)
    {
        _logger.LogWarning("Payment failed for order {OrderId}", @event.OrderId);
        await CompensateAsync();
    }

    private async Task HandleInventoryReservationFailedAsync(InventoryReservationFailedEvent @event)
    {
        _logger.LogWarning("Inventory reservation failed for order {OrderId}", @event.OrderId);
        await CompensateAsync();
    }

    public async Task CompensateAsync()
    {
        Status = SagaStatus.Compensating;
        _logger.LogInformation("Compensating saga {SagaId}", SagaId);

        try
        {
            // Release inventory reservation
            // Refund payment if processed
            // Cancel order
            // Notify customer of failure

            Status = SagaStatus.Compensated;
            _logger.LogInformation("Saga compensation completed for {SagaId}", SagaId);
        }
        catch (Exception ex)
        {
            Status = SagaStatus.Failed;
            _logger.LogError(ex, "Saga compensation failed for {SagaId}", SagaId);
        }
    }
}
```

### **3.4 Phase 4: Performance & Testing (Week 7-8)**

#### **Day 18-20: Performance Optimization**
```csharp
// Infrastructure/Caching/RedisCacheService.cs
public class RedisCacheService : ICacheService
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _database = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<T> GetAsync<T>(string key)
    {
        try
        {
            var value = await _database.StringGetAsync(key);
            return value.HasValue ? JsonSerializer.Deserialize<T>(value) : default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache value for key {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            var serialized = JsonSerializer.Serialize(value);
            await _database.StringSetAsync(key, serialized, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache value for key {Key}", key);
        }
    }

    // Other cache methods...
}

// Application/Services/CachedOrderService.cs
public class CachedOrderService : IOrderService
{
    private readonly IOrderService _orderService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CachedOrderService> _logger;

    public CachedOrderService(
        IOrderService orderService,
        ICacheService cacheService,
        ILogger<CachedOrderService> logger)
    {
        _orderService = orderService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<OrderDto> GetByIdAsync(Guid orderId)
    {
        var cacheKey = $"order:{orderId}";
        var cachedOrder = await _cacheService.GetAsync<OrderDto>(cacheKey);

        if (cachedOrder != null)
        {
            _logger.LogDebug("Cache hit for order {OrderId}", orderId);
            return cachedOrder;
        }

        _logger.LogDebug("Cache miss for order {OrderId}", orderId);

        var order = await _orderService.GetByIdAsync(orderId);
        if (order != null)
        {
            await _cacheService.SetAsync(cacheKey, order, TimeSpan.FromMinutes(10));
        }

        return order;
    }

    public async Task<List<OrderDto>> GetByCustomerIdAsync(Guid customerId, int page = 1, int pageSize = 20)
    {
        var cacheKey = $"customer_orders:{customerId}:{page}:{pageSize}";
        var cachedOrders = await _cacheService.GetAsync<List<OrderDto>>(cacheKey);

        if (cachedOrders != null)
        {
            _logger.LogDebug("Cache hit for customer orders {CustomerId}", customerId);
            return cachedOrders;
        }

        _logger.LogDebug("Cache miss for customer orders {CustomerId}", customerId);

        var orders = await _orderService.GetByCustomerIdAsync(customerId, page, pageSize);
        await _cacheService.SetAsync(cacheKey, orders, TimeSpan.FromMinutes(5));

        return orders;
    }

    // Other methods would delegate to the underlying service and cache results
}

// Infrastructure/Repositories/OptimizedOrderRepository.cs
public class OptimizedOrderRepository : IOrderRepository
{
    private readonly VanAnDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly ILogger<OptimizedOrderRepository> _logger;

    public OptimizedOrderRepository(
        VanAnDbContext context,
        ICacheService cacheService,
        ILogger<OptimizedOrderRepository> logger)
    {
        _context = context;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Order> GetByIdAsync(Guid id)
    {
        var cacheKey = $"order_entity:{id}";
        var cachedOrder = await _cacheService.GetAsync<Order>(cacheKey);

        if (cachedOrder != null)
        {
            _logger.LogDebug("Cache hit for order entity {OrderId}", id);
            return cachedOrder;
        }

        _logger.LogDebug("Cache miss for order entity {OrderId}", id);

        var order = await _context.Orders
            .Include(o => o.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order != null)
        {
            await _cacheService.SetAsync(cacheKey, order, TimeSpan.FromMinutes(10));
        }

        return order;
    }

    public async Task<List<Order>> GetByCustomerIdAsync(Guid customerId, int page = 1, int pageSize = 20)
    {
        var cacheKey = $"customer_order_entities:{customerId}:{page}:{pageSize}";
        var cachedOrders = await _cacheService.GetAsync<List<Order>>(cacheKey);

        if (cachedOrders != null)
        {
            _logger.LogDebug("Cache hit for customer order entities {CustomerId}", customerId);
            return cachedOrders;
        }

        _logger.LogDebug("Cache miss for customer order entities {CustomerId}", customerId);

        var orders = await _context.Orders
            .Include(o => o.Items)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        await _cacheService.SetAsync(cacheKey, orders, TimeSpan.FromMinutes(5));

        return orders;
    }

    // Other repository methods with caching...
}
```

#### **Day 21-22: Comprehensive Testing**
```csharp
// Tests/Unit/OrderDomainTests.cs
public class OrderDomainTests
{
    [Fact]
    public void CreateOrder_Should_Set_Initial_Status_To_Pending()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var items = new List<OrderItem>
        {
            new(new ProductId(Guid.NewGuid()), 2, new Money(25000))
        };

        // Act
        var order = new Order(customerId, items);

        // Assert
        order.Status.Should().Be(OrderStatus.Pending);
        order.CustomerId.Should().Be(customerId);
        order.Items.Should().HaveCount(1);
        order.TotalAmount.Should().Be(new Money(50000));
    }

    [Fact]
    public void AddItem_Should_Increase_Total_Amount()
    {
        // Arrange
        var order = CreateTestOrder();
        var initialTotal = order.TotalAmount;

        // Act
        order.AddItem(new ProductId(Guid.NewGuid()), 1, new Money(30000));

        // Assert
        order.TotalAmount.Should().BeGreaterThan(initialTotal);
        order.Items.Should().HaveCount(2);
    }

    [Fact]
    public void ConfirmOrder_Should_Change_Status_To_Confirmed()
    {
        // Arrange
        var order = CreateTestOrder();

        // Act
        order.ConfirmOrder();

        // Assert
        order.Status.Should().Be(OrderStatus.Completed);
        order.DomainEvents.Should().Contain(e => e is OrderConfirmedEvent);
    }

    [Fact]
    public void CancelOrder_Should_Change_Status_To_Cancelled()
    {
        // Arrange
        var order = CreateTestOrder();

        // Act
        order.CancelOrder("Customer request");

        // Assert
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.DomainEvents.Should().Contain(e => e is OrderCancelledEvent);
    }

    [Fact]
    public void AddItem_To_Completed_Order_Should_Throw_Exception()
    {
        // Arrange
        var order = CreateTestOrder();
        order.ConfirmOrder();

        // Act & Assert
        Assert.Throws<DomainException>(() => 
            order.AddItem(new ProductId(Guid.NewGuid()), 1, new Money(25000)));
    }

    private Order CreateTestOrder()
    {
        var customerId = Guid.NewGuid();
        var items = new List<OrderItem>
        {
            new(new ProductId(Guid.NewGuid()), 2, new Money(25000))
        };

        return new Order(customerId, items);
    }
}

// Tests/Integration/OrderServiceIntegrationTests.cs
public class OrderServiceIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly IOrderService _orderService;
    private readonly VanAnDbContext _context;

    public OrderServiceIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _orderService = _factory.Services.GetRequiredService<IOrderService>();
        _context = _factory.Services.GetRequiredService<VanAnDbContext>();
    }

    [Fact]
    public async Task CreateOrder_Should_Persist_To_Database()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var request = new CreateOrderRequest
        {
            CustomerId = customerId,
            Items = new List<CreateOrderItemRequest>
            {
                new() { ProductId = Guid.NewGuid(), Quantity = 2, UnitPrice = 25000 }
            }
        };

        // Act
        var result = await _orderService.CreateOrderAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        
        var savedOrder = await _context.Orders.FindAsync(result.Order.Id);
        savedOrder.Should().NotBeNull();
        savedOrder.CustomerId.Should().Be(customerId);
        savedOrder.Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task GetOrderById_Should_Return_Cached_Result()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = new Order(orderId, Guid.NewGuid(), new List<OrderItem>());
        await _context.Orders.AddAsync(order);
        await _context.SaveChangesAsync();

        // Act
        var result1 = await _orderService.GetByIdAsync(orderId);
        var result2 = await _orderService.GetByIdAsync(orderId);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.Id.Should().Be(result2.Id);
    }

    [Fact]
    public async Task CancelOrder_Should_Fail_If_Order_Is_Completed()
    {
        // Arrange
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), new List<OrderItem>());
        order.ConfirmOrder();
        await _context.Orders.AddAsync(order);
        await _context.SaveChangesAsync();

        // Act
        var result = await _orderService.CancelOrderAsync(order.Id, "Test cancellation");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cannot be cancelled");
    }
}

// Tests/Performance/OrderServicePerformanceTests.cs
public class OrderServicePerformanceTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly IOrderService _orderService;

    public OrderServicePerformanceTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _orderService = _factory.Services.GetRequiredService<IOrderService>();
    }

    [Fact]
    public async Task CreateOrder_Should_Handle_100_Orders_Per_Second()
    {
        // Arrange
        var tasks = new List<Task<OrderResult>>();
        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 100; i++)
        {
            var request = new CreateOrderRequest
            {
                CustomerId = Guid.NewGuid(),
                Items = new List<CreateOrderItemRequest>
                {
                    new() { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 25000 }
                }
            };

            tasks.Add(_orderService.CreateOrderAsync(request));
        }

        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        var successCount = results.Count(r => r.Success);
        successCount.Should().BeGreaterOrEqualTo(95); // 95% success rate
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000); // Within 1 second
    }

    [Fact]
    public async Task GetOrderById_Should_Complete_Within_100ms()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _orderService.GetByIdAsync(orderId);
        
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100);
    }
}
```

---

## **4. IMPLEMENTATION PLAN**

### **4.1 Week 1-2: Domain Enhancement**
- [ ] Implement rich domain models
- [ ] Add domain events system
- [ ] Create business rules engine
- [ ] Add domain services
- [ ] Implement value objects
- [ ] Add aggregate boundaries

### **4.2 Week 3-4: Service Layer Enhancement**
- [ ] Implement rich domain services
- [ ] Add CQRS pattern
- [ ] Create command handlers
- [ ] Add query handlers
- [ ] Implement unit of work
- [ ] Add transaction management

### **4.3 Week 5-6: Event-Driven Architecture**
- [ ] Implement event sourcing
- [ ] Add saga pattern
- [ ] Create event store
- [ ] Add event dispatchers
- [ ] Implement compensation
- [ ] Add event handlers

### **4.4 Week 7-8: Performance & Testing**
- [ ] Implement caching strategy
- [ ] Add performance optimization
- [ ] Create comprehensive tests
- [ ] Add performance tests
- [ ] Implement monitoring
- [ ] Add documentation

---

## **5. SUCCESS METRICS**

### **5.1 Quality Metrics**
- **Code Coverage:** >90% for domain and services
- **Domain Logic:** 100% encapsulated in domain
- **Event Processing:** <100ms average
- **Cache Hit Rate:** >80%

### **5.2 Business Metrics**
- **Order Processing:** <500ms average
- **Event Throughput:** >1000 events/second
- **Transaction Success:** >99.9%
- **System Reliability:** >99.9% uptime

### **5.3 Technical Metrics**
- **Memory Usage:** <1GB under normal load
- **CPU Usage:** <70% under normal load
- **Database Queries:** <50ms average
- **Event Store Performance:** <10ms per event

---

## **6. RISK MITIGATION**

### **6.1 Technical Risks**
1. **Event Ordering** - Implement proper event sequencing
2. **Event Duplication** - Add idempotency handling
3. **Saga Failure** - Implement proper compensation
4. **Cache Invalidation** - Implement proper cache strategies

### **6.2 Business Risks**
1. **Domain Complexity** - Start with simple domain models
2. **Event Volume** - Implement event batching
3. **Performance Impact** - Monitor and optimize continuously
4. **Data Consistency** - Implement proper transaction handling

---

## **7. NEXT STEPS**

### **7.1 Immediate Actions (This Week)**
1. **Domain Modeling** - Review and enhance domain models
2. **Event Design** - Design domain events
3. **Service Architecture** - Plan service layer
4. **Testing Strategy** - Define testing approach

### **7.2 Short-term Goals (2 Weeks)**
1. **Rich Domain** - Complete domain enhancement
2. **Event System** - Implement event handling
3. **Service Layer** - Complete service enhancement
4. **Basic Testing** - Add unit and integration tests

### **7.3 Long-term Goals (2 Months)**
1. **Complete Architecture** - Full event-driven system
2. **Performance Optimization** - Full optimization
3. **Advanced Features** - Event sourcing, sagas
4. **Production Ready** - Full deployment pipeline

---

## **8. SUMMARY**

### **8.1 Current State**
- **Basic services** with limited business logic
- **No domain events** or event-driven architecture
- **Simple repository** pattern
- **No performance** optimization

### **8.2 Target State**
- **Rich domain models** with business logic
- **Event-driven architecture** with CQRS
- **Advanced repository** with specifications
- **High performance** with caching and optimization

### **8.3 Implementation Strategy**
- **8-week phased approach** with clear milestones
- **Domain-Driven Design** as foundation
- **Event-driven architecture** for scalability
- **Performance-focused** optimization

**Status:** CoreHub module has significant gaps but clear improvement plan with professional domain-driven architecture.
