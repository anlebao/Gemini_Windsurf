# CoreHub - So Sánh và Plan C?i Ti?n

**Ngày:** 11 tháng 4, 2026  
**Module:** 3_CoreHub  
**Tr?ng thái:** Phân tích so sánh và k? ho?ch c?i ti?n

---

## **1. SO SÁNH TH?C T? vs LÝ T??NG**

### **1.1 Architecture Comparison**

| **Aspect** | **Th?c T?** | **Lý T??ng** | **Gap Analysis** |
|------------|--------------|--------------|------------------|
| **Clean Architecture** | Partial implementation | Full Clean Architecture compliance | **High** - C?n complete layering |
| **Domain Design** | Basic domain models | Rich domain models with behaviors | **High** - C?n domain logic |
| **Repository Pattern** | Basic repositories | Generic repositories with specifications | **Medium** - C?n repository enhancement |
| **Service Layer** | Basic services | Rich services with domain events | **High** - C?n service improvement |
| **Dependency Injection** | Basic DI | Advanced DI with lifetimes and decorators | **Medium** - C?n DI optimization |

### **1.2 Business Logic Comparison**

| **Aspect** | **Th?c T?** | **Lý T??NG** | **Gap Analysis** |
|------------|--------------|--------------|------------------|
| **Domain Events** | None | Event-driven architecture | **High** - C?n event system |
| **Business Rules** | Hard-coded | Rule engine with validation | **High** - C?n rule engine |
| **Workflows** | Basic services | Workflow orchestration | **High** - C?n workflow engine |
| **Validation** | Basic validation | Comprehensive validation framework | **Medium** - C?n validation enhancement |
| **Error Handling** | Basic exceptions | Domain-specific exceptions | **Medium** - C?n error handling |

### **1.3 Data Access Comparison**

| **Aspect** | **Th?c T?** | **Lý T??NG** | **Gap Analysis** |
|------------|--------------|--------------|------------------|
| **EF Core** | Basic usage | Advanced EF Core with optimization | **Medium** - C?n EF Core optimization |
| **Migrations** | Basic migrations | Versioned migrations with rollback | **Medium** - C?n migration improvement |
| **Query Optimization** | None | Query optimization and caching | **High** - C?n query optimization |
| **Multi-tenancy** | Basic implementation | Advanced multi-tenancy with isolation | **High** - C?n tenancy enhancement |
| **Transactions** | Basic transactions | Distributed transactions | **Medium** - C?n transaction handling |

### **1.4 Performance & Scalability Comparison**

| **Aspect** | **Th?c T?** | **Lý T??NG** | **Gap Analysis** |
|------------|--------------|--------------|------------------|
| **Caching** | None | Multi-level caching strategy | **High** - C?n caching implementation |
| **Async/Await** | Partial | Full async implementation | **Medium** - C?n async completion |
| **Connection Pooling** | Default | Optimized connection pooling | **Medium** - C?n connection optimization |
| **Bulk Operations** | None | Bulk operations for performance | **High** - C?n bulk operations |
| **Monitoring** | None | Performance monitoring and metrics | **High** - C?n monitoring system |

---

## **2. DÁNH GIÁ M?C ?? ?U TIÊN**

### **2.1 Critical Issues (Priority 1)**
1. **No Domain Events** - Event-driven architecture missing
2. **No Business Rules Engine** - Hard-coded business logic
3. **No Workflow Orchestration** - Complex workflows not supported
4. **No Caching Strategy** - Performance issues
5. **No Query Optimization** - Database performance issues

### **2.2 Important Issues (Priority 2)**
1. **Incomplete Domain Models** - Missing domain behaviors
2. **No Advanced Validation** - Input validation gaps
3. **No Bulk Operations** - Performance bottlenecks
4. **No Distributed Transactions** - Data consistency issues
5. **No Monitoring** - Observability gaps

### **2.3 Nice to Have (Priority 3)**
1. **Advanced Repository Pattern** - Generic repositories
2. **Rule Engine** - Configurable business rules
3. **Event Sourcing** - Audit trail and replay
4. **CQRS Pattern** - Read/write separation
5. **Advanced Multi-tenancy** - Enhanced isolation

---

## **3. K? HO?CH C?I TI?N**

### **3.1 Phase 1: Domain Enhancement (Week 1-2)**

#### **Day 1-3: Domain Events Implementation**
```csharp
// Create domain event system
public abstract class DomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public string EventType { get; protected set; }
}

public class OrderCreatedEvent : DomainEvent
{
    public Guid OrderId { get; }
    public Guid CustomerId { get; }
    public decimal TotalAmount { get; }

    public OrderCreatedEvent(Guid orderId, Guid customerId, decimal totalAmount)
    {
        OrderId = orderId;
        CustomerId = customerId;
        TotalAmount = totalAmount;
        EventType = nameof(OrderCreatedEvent);
    }
}

// Add domain events to aggregate root
public abstract class AggregateRoot
{
    private readonly List<DomainEvent> _domainEvents = new();

    public IReadOnlyCollection<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void RaiseEvent(DomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }
}

// Update Order aggregate
public class Order : AggregateRoot
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public List<OrderItem> Items { get; private set; } = new();

    public static Order Create(Guid customerId, List<OrderItem> items)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Status = OrderStatus.Created,
            Items = items
        };

        order.RaiseEvent(new OrderCreatedEvent(order.Id, order.CustomerId, order.GetTotalAmount()));
        return order;
    }
}
```

#### **Day 4-5: Domain Events Handler**
```csharp
// Create domain event dispatcher
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<DomainEvent> events);
    Task DispatchAsync(DomainEvent @event);
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

    public async Task DispatchAsync(IEnumerable<DomainEvent> events)
    {
        foreach (var @event in events)
        {
            await DispatchAsync(@event);
        }
    }

    public async Task DispatchAsync(DomainEvent @event)
    {
        var eventType = @event.GetType();
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);

        var handlers = _serviceProvider.GetServices(handlerType);
        
        foreach (var handler in handlers)
        {
            var method = handlerType.GetMethod("HandleAsync");
            await (Task)method.Invoke(handler, new object[] { @event });
        }
    }
}

// Create event handler interface
public interface IDomainEventHandler<T> where T : DomainEvent
{
    Task HandleAsync(T domainEvent);
}

// Implement specific handlers
public class OrderCreatedEventHandler : IDomainEventHandler<OrderCreatedEvent>
{
    private readonly IInventoryService _inventoryService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<OrderCreatedEventHandler> _logger;

    public async Task HandleAsync(OrderCreatedEvent domainEvent)
    {
        // Reserve inventory
        await _inventoryService.ReserveInventoryAsync(domainEvent.Items);
        
        // Send notification
        await _notificationService.SendOrderCreatedNotificationAsync(domainEvent.OrderId);
        
        _logger.LogInformation("Order {OrderId} created and processed", domainEvent.OrderId);
    }
}
```

#### **Day 6-7: Business Rules Engine**
```csharp
// Create business rules engine
public interface IBusinessRule
{
    string Name { get; }
    string Description { get; }
    bool IsSatisfied(object context);
    string ErrorMessage { get; }
}

public class OrderTotalAmountRule : IBusinessRule
{
    private readonly decimal _maximumAmount;

    public OrderTotalAmountRule(decimal maximumAmount)
    {
        _maximumAmount = maximumAmount;
    }

    public string Name => "OrderTotalAmountRule";
    public string Description => $"Order total cannot exceed {_maximumAmount:C}";
    
    public bool IsSatisfied(object context)
    {
        if (context is Order order)
        {
            return order.GetTotalAmount() <= _maximumAmount;
        }
        return false;
    }

    public string ErrorMessage => $"Order total exceeds maximum allowed amount of {_maximumAmount:C}";
}

// Create rule validator
public class BusinessRuleValidator
{
    public static ValidationResult ValidateRules<T>(T context, params IBusinessRule[] rules)
    {
        var failures = new List<string>();
        
        foreach (var rule in rules)
        {
            if (!rule.IsSatisfied(context))
            {
                failures.Add(rule.ErrorMessage);
            }
        }

        return new ValidationResult
        {
            IsValid = !failures.Any(),
            Failures = failures
        };
    }
}

// Apply rules in domain
public class Order : AggregateRoot
{
    public static Result<Order> Create(Guid customerId, List<OrderItem> items)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Status = OrderStatus.Created,
            Items = items
        };

        // Validate business rules
        var rules = new IBusinessRule[]
        {
            new OrderTotalAmountRule(10000m),
            new OrderItemsLimitRule(50),
            new CustomerActiveRule(customerId)
        };

        var validationResult = BusinessRuleValidator.ValidateRules(order, rules);
        
        if (!validationResult.IsValid)
        {
            return Result<Order>.Failure(validationResult.Failures);
        }

        order.RaiseEvent(new OrderCreatedEvent(order.Id, order.CustomerId, order.GetTotalAmount()));
        return Result<Order>.Success(order);
    }
}
```

### **3.2 Phase 2: Service Layer Enhancement (Week 3-4)**

#### **Day 8-10: Workflow Orchestration**
```csharp
// Create workflow engine
public interface IWorkflow
{
    string Name { get; }
    Task<WorkflowResult> ExecuteAsync<T>(T context);
}

public class OrderProcessingWorkflow : IWorkflow
{
    private readonly IOrderService _orderService;
    private readonly IInventoryService _inventoryService;
    private readonly IPaymentService _paymentService;
    private readonly INotificationService _notificationService;

    public string Name => "OrderProcessingWorkflow";

    public async Task<WorkflowResult> ExecuteAsync<T>(T context)
    {
        if (context is not Guid orderId)
        {
            return WorkflowResult.Failure("Invalid context type");
        }

        try
        {
            // Step 1: Validate order
            var order = await _orderService.GetOrderAsync(orderId);
            if (order == null)
            {
                return WorkflowResult.Failure("Order not found");
            }

            // Step 2: Reserve inventory
            var inventoryResult = await _inventoryService.ReserveInventoryAsync(order.Items);
            if (!inventoryResult.Success)
            {
                return WorkflowResult.Failure($"Inventory reservation failed: {inventoryResult.Message}");
            }

            // Step 3: Process payment
            var paymentResult = await _paymentService.ProcessPaymentAsync(order);
            if (!paymentResult.Success)
            {
                await _inventoryService.ReleaseInventoryAsync(order.Items);
                return WorkflowResult.Failure($"Payment failed: {paymentResult.Message}");
            }

            // Step 4: Update order status
            await _orderService.UpdateOrderStatusAsync(orderId, OrderStatus.Processing);

            // Step 5: Send notifications
            await _notificationService.SendOrderProcessingNotificationAsync(orderId);

            return WorkflowResult.Success("Order processed successfully");
        }
        catch (Exception ex)
        {
            return WorkflowResult.Failure($"Workflow execution failed: {ex.Message}");
        }
    }
}

// Create workflow service
public interface IWorkflowService
{
    Task<WorkflowResult> ExecuteWorkflowAsync<T>(string workflowName, T context);
}

public class WorkflowService : IWorkflowService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkflowService> _logger;

    public async Task<WorkflowResult> ExecuteWorkflowAsync<T>(string workflowName, T context)
    {
        var workflowType = GetWorkflowType(workflowName);
        var workflow = (IWorkflow)_serviceProvider.GetService(workflowType);

        if (workflow == null)
        {
            return WorkflowResult.Failure($"Workflow '{workflowName}' not found");
        }

        _logger.LogInformation("Executing workflow {WorkflowName}", workflowName);
        
        var result = await workflow.ExecuteAsync(context);
        
        _logger.LogInformation("Workflow {WorkflowName} completed with status: {Status}", 
            workflowName, result.Success ? "Success" : "Failure");

        return result;
    }

    private Type GetWorkflowType(string workflowName)
    {
        return workflowName switch
        {
            "OrderProcessing" => typeof(OrderProcessingWorkflow),
            "InventoryManagement" => typeof(InventoryManagementWorkflow),
            "CustomerOnboarding" => typeof(CustomerOnboardingWorkflow),
            _ => throw new ArgumentException($"Unknown workflow: {workflowName}")
        };
    }
}
```

#### **Day 11-12: Service Enhancement**
```csharp
// Enhance order service with domain events
public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly IWorkflowService _workflowService;
    private readonly ILogger<OrderService> _logger;

    public async Task<Result<Order>> CreateOrderAsync(CreateOrderRequest request)
    {
        try
        {
            // Create order with business rules validation
            var orderResult = Order.Create(request.CustomerId, request.Items);
            
            if (!orderResult.Success)
            {
                return Result<Order>.Failure(orderResult.Errors);
            }

            // Save order
            await _orderRepository.AddAsync(orderResult.Value);
            await _orderRepository.SaveChangesAsync();

            // Dispatch domain events
            await _eventDispatcher.DispatchAsync(orderResult.Value.GetDomainEvents());
            orderResult.Value.ClearDomainEvents();

            // Execute workflow
            await _workflowService.ExecuteWorkflowAsync("OrderProcessing", orderResult.Value.Id);

            _logger.LogInformation("Order {OrderId} created successfully", orderResult.Value.Id);
            return Result<Order>.Success(orderResult.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order");
            return Result<Order>.Failure("Failed to create order");
        }
    }
}
```

#### **Day 13-14: Advanced Validation**
```csharp
// Create comprehensive validation framework
public interface IValidator<T>
{
    ValidationResult Validate(T instance);
}

public class CreateOrderRequestValidator : IValidator<CreateOrderRequest>
{
    private readonly ICustomerService _customerService;
    private readonly IProductService _productService;

    public ValidationResult Validate(CreateOrderRequest request)
    {
        var failures = new List<string>();

        // Validate customer
        if (request.CustomerId == Guid.Empty)
        {
            failures.Add("Customer ID is required");
        }

        // Validate items
        if (!request.Items.Any())
        {
            failures.Add("At least one item is required");
        }

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
            {
                failures.Add($"Item {item.ProductId} quantity must be greater than 0");
            }
        }

        return new ValidationResult
        {
            IsValid = !failures.Any(),
            Failures = failures
        };
    }
}

// Add validation to service
public class OrderService : IOrderService
{
    private readonly IValidator<CreateOrderRequest> _validator;

    public async Task<Result<Order>> CreateOrderAsync(CreateOrderRequest request)
    {
        // Validate request
        var validationResult = _validator.Validate(request);
        if (!validationResult.IsValid)
        {
            return Result<Order>.Failure(validationResult.Failures);
        }

        // Continue with order creation...
    }
}
```

### **3.3 Phase 3: Data Access Optimization (Week 5-6)**

#### **Day 15-17: Query Optimization**
```csharp
// Create optimized repository with specifications
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TResult>> ListAsync<TResult>(ISpecification<T, TResult> specification, CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
    Task<int> CountAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);
}

// Create specification pattern
public interface ISpecification<T>
{
    Expression<Func<T, bool>> Criteria { get; }
    Func<IQueryable<T>, IIncludableQueryable<T, object>> Include { get; }
    Func<IQueryable<T>, IOrderedQueryable<T>> OrderBy { get; }
    Expression<Func<T, object>> GroupBy { get; }
    int Take { get; }
    int Skip { get; }
    bool IsPagingEnabled { get; }
}

public class OrderByCustomerSpecification : ISpecification<Order>
{
    public Guid CustomerId { get; }

    public OrderByCustomerSpecification(Guid customerId)
    {
        CustomerId = customerId;
    }

    public Expression<Func<Order, bool>> Criteria => order => order.CustomerId == CustomerId;
    public Func<IQueryable<Order>, IIncludableQueryable<Order, object>> Include => query => query.Include(o => o.Items);
    public Func<IQueryable<Order>, IOrderedQueryable<Order>> OrderBy => query => query.OrderByDescending(o => o.CreatedAt);
    public Expression<Func<Order, object>> GroupBy => null;
    public int Take => 20;
    public int Skip => 0;
    public bool IsPagingEnabled => true;
}

// Implement optimized repository
public class EfRepository<T> : IRepository<T> where T : class
{
    protected readonly DbContext _context;
    protected readonly DbSet<T> _dbSet;

    public EfRepository(DbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
    {
        var query = ApplySpecification(specification);
        return await query.ToListAsync(cancellationToken);
    }

    private IQueryable<T> ApplySpecification(ISpecification<T> specification)
    {
        var query = _dbSet.AsQueryable();

        if (specification.Criteria != null)
        {
            query = query.Where(specification.Criteria);
        }

        if (specification.Include != null)
        {
            query = specification.Include(query);
        }

        if (specification.OrderBy != null)
        {
            query = specification.OrderBy(query);
        }

        if (specification.GroupBy != null)
        {
            query = query.GroupBy(specification.GroupBy).SelectMany(g => g);
        }

        if (specification.IsPagingEnabled)
        {
            query = query.Skip(specification.Skip).Take(specification.Take);
        }

        return query;
    }
}
```

#### **Day 18-19: Bulk Operations**
```csharp
// Create bulk operations service
public interface IBulkOperationService
{
    Task BulkInsertAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    Task BulkUpdateAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    Task BulkDeleteAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default);
}

public class BulkOperationService : IBulkOperationService
{
    private readonly DbContext _context;
    private readonly ILogger<BulkOperationService> _logger;

    public async Task BulkInsertAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.Set<T>().AddRangeAsync(entities, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("Bulk inserted {Count} entities of type {EntityType}", 
                entities.Count(), typeof(T).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk insert of type {EntityType}", typeof(T).Name);
            throw;
        }
    }

    public async Task BulkUpdateAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.Set<T>().UpdateRange(entities);
            await _context.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("Bulk updated {Count} entities of type {EntityType}", 
                entities.Count(), typeof(T).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk update of type {EntityType}", typeof(T).Name);
            throw;
        }
    }
}

// Use bulk operations in services
public class InventoryService : IInventoryService
{
    private readonly IBulkOperationService _bulkOperationService;

    public async Task UpdateInventoryLevelsAsync(IEnumerable<InventoryUpdate> updates)
    {
        var inventoryItems = updates.Select(u => new InventoryItem
        {
            ProductId = u.ProductId,
            Quantity = u.NewQuantity,
            LastUpdated = DateTime.UtcNow
        });

        await _bulkOperationService.BulkUpdateAsync(inventoryItems);
    }
}
```

#### **Day 20-21: Caching Strategy**
```csharp
// Create multi-level caching service
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);
}

public class CacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<CacheService> _logger;

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        // Try memory cache first
        if (_memoryCache.TryGetValue(key, out T value))
        {
            return value;
        }

        // Try distributed cache
        var cachedValue = await _distributedCache.GetStringAsync(key, cancellationToken);
        if (cachedValue != null)
        {
            var deserializedValue = JsonSerializer.Deserialize<T>(cachedValue);
            
            // Cache in memory for faster access
            _memoryCache.Set(key, deserializedValue, TimeSpan.FromMinutes(5));
            
            return deserializedValue;
        }

        return default(T);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        // Set in memory cache
        _memoryCache.Set(key, value, expiry ?? TimeSpan.FromMinutes(5));

        // Set in distributed cache
        var serializedValue = JsonSerializer.Serialize(value);
        await _distributedCache.SetStringAsync(key, serializedValue, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(5)
        }, cancellationToken);
    }
}

// Add caching to repository
public class CachedOrderRepository : IOrderRepository
{
    private readonly IOrderRepository _innerRepository;
    private readonly ICacheService _cacheService;

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"order_{id}";
        
        var order = await _cacheService.GetAsync<Order>(cacheKey, cancellationToken);
        if (order != null)
        {
            return order;
        }

        order = await _innerRepository.GetByIdAsync(id, cancellationToken);
        if (order != null)
        {
            await _cacheService.SetAsync(cacheKey, order, TimeSpan.FromMinutes(10), cancellationToken);
        }

        return order;
    }
}
```

### **3.4 Phase 4: Monitoring & Testing (Week 7-8)**

#### **Day 22-24: Performance Monitoring**
```csharp
// Create performance monitoring service
public class PerformanceMonitor
{
    private readonly IMeterFactory _meterFactory;
    private readonly Counter<int> _operationCounter;
    private readonly Histogram<double> _operationDuration;
    private readonly Counter<int> _errorCounter;

    public PerformanceMonitor(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("VanAn.CoreHub");
        _operationCounter = meter.CreateCounter<int>("operations_total");
        _operationDuration = meter.CreateHistogram<double>("operation_duration_seconds");
        _errorCounter = meter.CreateCounter<int>("errors_total");
    }

    public IDisposable MeasureOperation(string operationName)
    {
        return new OperationMeasurement(this, operationName);
    }

    private class OperationMeasurement : IDisposable
    {
        private readonly PerformanceMonitor _monitor;
        private readonly string _operationName;
        private readonly Stopwatch _stopwatch;

        public OperationMeasurement(PerformanceMonitor monitor, string operationName)
        {
            _monitor = monitor;
            _operationName = operationName;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _monitor._operationCounter.Add(1, new KeyValuePair<string, object>("operation", _operationName));
            _monitor._operationDuration.Record(_stopwatch.Elapsed.TotalSeconds, new KeyValuePair<string, object>("operation", _operationName));
        }
    }
}

// Add monitoring to services
public class OrderService : IOrderService
{
    private readonly PerformanceMonitor _performanceMonitor;

    public async Task<Result<Order>> CreateOrderAsync(CreateOrderRequest request)
    {
        using var measurement = _performanceMonitor.MeasureOperation("CreateOrder");
        
        try
        {
            // Service logic...
            return Result<Order>.Success(order);
        }
        catch (Exception ex)
        {
            _performanceMonitor._errorCounter.Add(1, new KeyValuePair<string, object>("operation", "CreateOrder"));
            throw;
        }
    }
}
```

#### **Day 25-26: Advanced Testing**
```csharp
// Create comprehensive test base
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly ITestOutputHelper Output;
    protected readonly VanAnDbContext Context;
    protected readonly IServiceProvider ServiceProvider;

    protected IntegrationTestBase(ITestOutputHelper output)
    {
        Output = output;
        var options = new DbContextOptionsBuilder<VanAnDbContext>()
            .UseInMemory(Guid.NewGuid().ToString())
            .Options;
        
        Context = new VanAnDbContext(options);
        
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContext<VanAnDbContext>(options =>
            options.UseInMemory(Guid.NewGuid().ToString()));
        
        // Add test services
        services.AddScoped<IOrderRepository, EfRepository<Order>>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IDomainEventDispatcher, TestDomainEventDispatcher>();
    }

    public async Task InitializeAsync()
    {
        await Context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await Context.Database.EnsureDeletedAsync();
        await Context.DisposeAsync();
    }
}

// Create domain events test helper
public class TestDomainEventDispatcher : IDomainEventDispatcher
{
    public List<DomainEvent> PublishedEvents { get; } = new();

    public Task DispatchAsync(IEnumerable<DomainEvent> events)
    {
        PublishedEvents.AddRange(events);
        return Task.CompletedTask;
    }

    public Task DispatchAsync(DomainEvent @event)
    {
        PublishedEvents.Add(@event);
        return Task.CompletedTask;
    }

    public void ClearEvents()
    {
        PublishedEvents.Clear();
    }
}

// Example integration test
public class OrderServiceTests : IntegrationTestBase
{
    private readonly IOrderService _orderService;
    private readonly TestDomainEventDispatcher _eventDispatcher;

    public OrderServiceTests(ITestOutputHelper output) : base(output)
    {
        _orderService = ServiceProvider.GetRequiredService<IOrderService>();
        _eventDispatcher = (TestDomainEventDispatcher)ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
    }

    [Fact]
    public async Task CreateOrder_Should_Publish_DomainEvents()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = Guid.NewGuid(),
            Items = new List<OrderItemDto>
            {
                new() { ProductId = Guid.NewGuid(), Quantity = 2, UnitPrice = 100 }
            }
        };

        // Act
        var result = await _orderService.CreateOrderAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        _eventDispatcher.PublishedEvents.Should().Contain(e => e is OrderCreatedEvent);
    }
}
```

#### **Day 27-28: Documentation & Cleanup**
```csharp
// Create comprehensive documentation
/// <summary>
/// Order service handles all order-related business operations including creation,
/// updates, status management, and workflow orchestration.
/// </summary>
/// <remarks>
/// This service implements the following patterns:
/// - Domain-Driven Design (DDD)
/// - Event-Driven Architecture
/// - CQRS (Command Query Responsibility Segregation)
/// - Repository Pattern
/// </remarks>
public class OrderService : IOrderService
{
    /// <summary>
    /// Creates a new order with business rule validation and workflow execution.
    /// </summary>
    /// <param name="request">The order creation request containing customer and item information.</param>
    /// <returns>
    /// A Result containing the created order if successful, 
    /// or validation errors if the request fails business rules.
    /// </returns>
    /// <exception cref="ValidationException">Thrown when the request fails validation.</exception>
    /// <example>
    /// <code>
    /// var request = new CreateOrderRequest
    /// {
    ///     CustomerId = customerId,
    ///     Items = new List&lt;OrderItemDto&gt; { /* items */ }
    /// };
    /// var result = await _orderService.CreateOrderAsync(request);
    /// </code>
    /// </example>
    public async Task<Result<Order>> CreateOrderAsync(CreateOrderRequest request)
    {
        // Implementation...
    }
}
```

---

## **4. IMPLEMENTATION PLAN**

### **4.1 Week 1-2: Domain Enhancement**
- [ ] Implement domain events system
- [ ] Create domain event dispatcher
- [ ] Add domain events to aggregates
- [ ] Implement business rules engine
- [ ] Create rule validator
- [ ] Add business rules to domain

### **4.2 Week 3-4: Service Layer Enhancement**
- [ ] Implement workflow engine
- [ ] Create order processing workflow
- [ ] Enhance services with domain events
- [ ] Add comprehensive validation
- [ ] Implement validation framework
- [ ] Add validation to services

### **4.3 Week 5-6: Data Access Optimization**
- [ ] Implement specification pattern
- [ ] Create optimized repository
- [ ] Add bulk operations
- [ ] Implement caching strategy
- [ ] Add multi-level caching
- [ ] Optimize database queries

### **4.4 Week 7-8: Monitoring & Testing**
- [ ] Add performance monitoring
- [ ] Create comprehensive tests
- [ ] Add integration tests
- [ ] Create documentation
- [ ] Add health checks
- [ ] Implement observability

---

## **5. SUCCESS METRICS**

### **5.1 Quality Metrics**
- **Code Coverage:** >90% for domain and services
- **Domain Events:** 100% of critical operations
- **Business Rules:** 100% validation coverage
- **Performance:** <100ms for 95th percentile

### **5.2 Architecture Metrics**
- **Clean Architecture Compliance:** 100%
- **Domain Model Richness:** >80% behaviors in domain
- **Event Coverage:** >90% of operations have events
- **Rule Coverage:** 100% of business rules

### **5.3 Performance Metrics**
- **Query Performance:** <50ms for complex queries
- **Cache Hit Rate:** >80%
- **Bulk Operations:** 10x performance improvement
- **Memory Usage:** <1GB under normal load

---

## **6. RISK MITIGATION**

### **6.1 Technical Risks**
1. **Domain Event Ordering** - Implement event ordering
2. **Event Duplication** - Add idempotency handling
3. **Performance Regression** - Continuous monitoring
4. **Data Consistency** - Implement distributed transactions

### **6.2 Business Risks**
1. **Rule Changes** - Implement configurable rules
2. **Workflow Complexity** - Visual workflow designer
3. **Data Migration** - Incremental migration strategy
4. **Team Adoption** - Training and documentation

---

## **7. NEXT STEPS**

### **7.1 Immediate Actions (This Week)**
1. **Setup Development Environment** - Ensure all tools ready
2. **Create Feature Branch** - Isolate changes
3. **Implement Domain Events** - Start with event system
4. **Add Unit Tests** - Test domain events

### **7.2 Short-term Goals (2 Weeks)**
1. **Complete Domain Enhancement** - Full domain events and rules
2. **Implement Workflow Engine** - Basic workflow support
3. **Add Validation Framework** - Comprehensive validation
4. **Create Integration Tests** - Test domain events

### **7.3 Long-term Goals (2 Months)**
1. **Complete All Improvements** - Full implementation
2. **Achieve Performance Goals** - Meet all performance metrics
3. **Team Training** - Ensure team understands new architecture
4. **Documentation Complete** - Full documentation set

---

## **8. SUMMARY**

### **8.1 Current State**
- **Basic Clean Architecture** with partial implementation
- **No domain events** or business rules engine
- **Basic data access** without optimization
- **Limited testing** and monitoring

### **8.2 Target State**
- **Full Clean Architecture** with rich domain models
- **Event-driven architecture** with domain events
- **Optimized data access** with caching and bulk operations
- **Comprehensive testing** and monitoring

### **8.3 Implementation Strategy**
- **8-week phased approach** with clear milestones
- **Domain-first approach** with business logic focus
- **Performance optimization** throughout implementation
- **Quality focus** with comprehensive testing

**Status:** CoreHub module có significant architectural gaps but có clear improvement plan v?i domain-driven approach.
