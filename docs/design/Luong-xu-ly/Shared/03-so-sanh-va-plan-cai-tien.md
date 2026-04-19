# Shared - So Sánh và Plan C?i Ti?n

**Ngày:** 11 tháng 4, 2026  
**Module:** 1_Shared  
**Tr?ng thái:** Phân tích so sánh và k? ho?ch c?i ti?n

---

## **1. SO SÁNH TH?C T? vs LÝ T??NG**

### **1.1 Domain Model Comparison**

| **Aspect** | **Th?c T?** | **Lý T??NG** | **Gap Analysis** |
|------------|--------------|--------------|------------------|
| **Entity Design** | Basic POCOs | Rich entities with behaviors | **High** - C?n domain logic |
| **Value Objects** | Basic records | Immutable value objects | **Medium** - C?n proper immutability |
| **Aggregates** | No aggregate roots | Proper aggregate boundaries | **High** - C?n aggregate design |
| **Domain Events** | None | Event-driven architecture | **High** - C?n event system |
| **Business Rules** | Hard-coded | Encapsulated in domain | **High** - C?n rule encapsulation |

### **1.2 Data Structure Comparison**

| **Aspect** | **Th?c T?** | **Lý T??NG** | **Gap Analysis** |
|------------|--------------|--------------|------------------|
| **Type Safety** | Basic types | Strong typing with records | **Medium** - C?n type safety |
| **Immutability** | Mutable objects | Immutable by design | **High** - C?n immutability |
| **Validation** | Basic validation | Rich validation rules | **Medium** - C?n validation enhancement |
| **Serialization** | Basic JSON | Custom serialization | **Medium** - C?n serialization optimization |
| **Equality** | Reference equality | Value-based equality | **Medium** - C?n proper equality |

### **1.3 Architecture Comparison**

| **Aspect** | **Th?c T?** | **Lý T??NG** | **Gap Analysis** |
|------------|--------------|--------------|------------------|
| **Clean Architecture** | Partial implementation | Full Clean Architecture | **High** - C?n complete architecture |
| **DDD Principles** | Basic DDD | Full DDD implementation | **High** - C?n DDD principles |
| **Multi-tenancy** | Basic support | Advanced multi-tenancy | **Medium** - C?n tenancy enhancement |
| **Audit Trail** | Basic audit | Comprehensive audit system | **High** - C?n audit enhancement |
| **Soft Delete** | Basic implementation | Advanced soft delete | **Medium** - C?n delete enhancement |

### **1.4 Infrastructure Comparison**

| **Aspect** | **Th?c T?** | **Lý T??NG** | **Gap Analysis** |
|------------|--------------|--------------|------------------|
| **EF Core Config** | Basic configuration | Advanced EF Core setup | **Medium** - C?n EF optimization |
| **Migrations** | Basic migrations | Versioned migrations | **Medium** - C?n migration enhancement |
| **Query Optimization** | None | Query optimization | **High** - C?n query optimization |
| **Caching** | None | Domain caching | **Medium** - C?n caching strategy |
| **Validation Attributes** | Basic attributes | FluentValidation | **Medium** - C?n validation framework |

---

## **2. DÁNH GIÁ M?C ?? ?U TIÊN**

### **2.1 Critical Issues (Priority 1)**
1. **No Domain Events** - Event-driven architecture missing
2. **No Aggregate Roots** - Aggregate boundaries not defined
3. **No Business Logic** - Domain models are anemic
4. **No Immutability** - Mutable domain objects
5. **No Advanced Validation** - Basic validation only

### **2.2 Important Issues (Priority 2)**
1. **No Type Safety** - Basic types instead of value objects
2. **No Audit Trail** - Limited audit capabilities
3. **No Query Optimization** - Performance issues
4. **No Caching Strategy** - Performance bottlenecks
5. **No Serialization Optimization** - Performance issues

### **2.3 Nice to Have (Priority 3)**
1. **No Event Sourcing** - Audit trail enhancement
2. **No CQRS** - Read/write separation
3. **No Specification Pattern** - Query complexity
4. **No Repository Pattern** - Data access abstraction
5. **No Domain Services** - Complex business logic

---

## **3. K? HO?CH C?I TI?N**

### **3.1 Phase 1: Domain Model Enhancement (Week 1-2)**

#### **Day 1-3: Rich Domain Models**
```csharp
// Current: Basic POCO
public class Order
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public List<OrderItem> Items { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Enhanced: Rich domain model with behaviors
public class Order : AggregateRoot
{
    private readonly List<OrderItem> _items = new();
    
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
    
    // Private constructor for factory pattern
    private Order(Guid customerId)
    {
        Id = Guid.NewGuid();
        CustomerId = customerId;
        Status = OrderStatus.Draft;
        CreatedAt = DateTime.UtcNow;
    }
    
    // Factory method with validation
    public static Result<Order> Create(Guid customerId, List<OrderItemData> itemsData)
    {
        // Validate business rules
        var validationResult = ValidateOrderCreation(customerId, itemsData);
        if (!validationResult.IsValid)
        {
            return Result<Order>.Failure(validationResult.Errors);
        }
        
        var order = new Order(customerId);
        
        // Add items with validation
        foreach (var itemData in itemsData)
        {
            var addItemResult = order.AddItem(itemData.ProductId, itemData.Quantity, itemData.UnitPrice);
            if (!addItemResult.Success)
            {
                return Result<Order>.Failure(addItemResult.Errors);
            }
        }
        
        // Raise domain event
        order.RaiseEvent(new OrderCreatedEvent(order.Id, order.CustomerId, order.GetTotalAmount()));
        
        return Result<Order>.Success(order);
    }
    
    // Domain behavior
    public Result<OrderItem> AddItem(Guid productId, int quantity, decimal unitPrice)
    {
        // Business rules
        if (quantity <= 0)
        {
            return Result<OrderItem>.Failure("Quantity must be greater than 0");
        }
        
        if (unitPrice <= 0)
        {
            return Result<OrderItem>.Failure("Unit price must be greater than 0");
        }
        
        if (_items.Any(i => i.ProductId == productId))
        {
            return Result<OrderItem>.Failure("Product already exists in order");
        }
        
        var item = OrderItem.Create(Id, productId, quantity, unitPrice);
        _items.Add(item);
        
        // Raise event
        RaiseEvent(new OrderItemAddedEvent(Id, item.Id, productId, quantity));
        
        return Result<OrderItem>.Success(item);
    }
    
    public Result RemoveItem(Guid orderItemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == orderItemId);
        if (item == null)
        {
            return Result.Failure("Order item not found");
        }
        
        if (Status != OrderStatus.Draft)
        {
            return Result.Failure("Cannot remove items from order in current status");
        }
        
        _items.Remove(item);
        
        // Raise event
        RaiseEvent(new OrderItemRemovedEvent(Id, item.Id, item.ProductId));
        
        return Result.Success();
    }
    
    public Result UpdateStatus(OrderStatus newStatus)
    {
        // Business rules for status transitions
        var validationResult = ValidateStatusTransition(Status, newStatus);
        if (!validationResult.IsValid)
        {
            return Result.Failure(validationResult.Errors);
        }
        
        var oldStatus = Status;
        Status = newStatus;
        
        // Raise event
        RaiseEvent(new OrderStatusChangedEvent(Id, oldStatus, newStatus));
        
        return Result.Success();
    }
    
    // Domain calculations
    public decimal GetTotalAmount() => _items.Sum(i => i.GetTotalPrice());
    
    public int GetTotalQuantity() => _items.Sum(i => i.Quantity);
    
    // Private validation methods
    private static ValidationResult ValidateOrderCreation(Guid customerId, List<OrderItemData> itemsData)
    {
        var errors = new List<string>();
        
        if (customerId == Guid.Empty)
        {
            errors.Add("Customer ID is required");
        }
        
        if (itemsData == null || !itemsData.Any())
        {
            errors.Add("At least one item is required");
        }
        
        if (itemsData.Count > 50)
        {
            errors.Add("Order cannot contain more than 50 items");
        }
        
        return new ValidationResult
        {
            IsValid = !errors.Any(),
            Errors = errors
        };
    }
    
    private static ValidationResult ValidateStatusTransition(OrderStatus currentStatus, OrderStatus newStatus)
    {
        var errors = new List<string>();
        
        // Define valid transitions
        var validTransitions = new Dictionary<OrderStatus, List<OrderStatus>>
        {
            [OrderStatus.Draft] = new() { OrderStatus.Confirmed, OrderStatus.Cancelled },
            [OrderStatus.Confirmed] = new() { OrderStatus.Processing, OrderStatus.Cancelled },
            [OrderStatus.Processing] = new() { OrderStatus.Completed, OrderStatus.Cancelled },
            [OrderStatus.Completed] = new List<OrderStatus>(),
            [OrderStatus.Cancelled] = new List<OrderStatus>()
        };
        
        if (validTransitions.ContainsKey(currentStatus) && 
            !validTransitions[currentStatus].Contains(newStatus))
        {
            errors.Add($"Cannot transition from {currentStatus} to {newStatus}");
        }
        
        return new ValidationResult
        {
            IsValid = !errors.Any(),
            Errors = errors
        };
    }
}
```

#### **Day 4-5: Value Objects**
```csharp
// Current: Basic types
public class Customer
{
    public string PhoneNumber { get; set; }
    public string Email { get; set; }
    public decimal LoyaltyPoints { get; set; }
}

// Enhanced: Value objects with immutability
public record PhoneNumber(string Value)
{
    public string Value { get; } = ValidatePhoneNumber(Value);
    
    private static string ValidatePhoneNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Phone number is required");
        }
        
        // Vietnamese phone number validation
        if (!Regex.IsMatch(value, @"^(0[3-9]\d{8,9})$"))
        {
            throw new ArgumentException("Invalid Vietnamese phone number format");
        }
        
        return value;
    }
    
    public static implicit operator string(PhoneNumber phoneNumber) => phoneNumber.Value;
    public static implicit operator PhoneNumber(string value) => new PhoneNumber(value);
}

public record Email(string Value)
{
    public string Value { get; } = ValidateEmail(Value);
    
    private static string ValidateEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Email is required");
        }
        
        if (!Regex.IsMatch(value, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            throw new ArgumentException("Invalid email format");
        }
        
        return value.ToLowerInvariant();
    }
    
    public static implicit operator string(Email email) => email.Value;
    public static implicit operator Email(string value) => new Email(value);
}

public record LoyaltyPoints(int Value)
{
    public int Value { get; } = ValidatePoints(Value);
    
    private static int ValidatePoints(int value)
    {
        if (value < 0)
        {
            throw new ArgumentException("Loyalty points cannot be negative");
        }
        
        return value;
    }
    
    public LoyaltyPoints Add(LoyaltyPoints other) => new(Value + other.Value);
    public LoyaltyPoints Subtract(LoyaltyPoints other) => new(Value - other.Value);
    
    public static implicit operator int(LoyaltyPoints points) => points.Value;
    public static implicit operator LoyaltyPoints(int value) => new LoyaltyPoints(value);
}

// Enhanced Customer entity
public class Customer : AggregateRoot
{
    public Guid Id { get; private set; }
    public CustomerId CustomerId { get; private set; }
    public FullName FullName { get; private set; }
    public PhoneNumber PhoneNumber { get; private set; }
    public Email Email { get; private set; }
    public LoyaltyPoints LoyaltyPoints { get; private set; }
    public CustomerTier Tier { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastOrderDate { get; private set; }
    
    private Customer(FullName fullName, PhoneNumber phoneNumber, Email email)
    {
        Id = Guid.NewGuid();
        CustomerId = new CustomerId(Id);
        FullName = fullName;
        PhoneNumber = phoneNumber;
        Email = email;
        LoyaltyPoints = new LoyaltyPoints(0);
        Tier = CustomerTier.Bronze;
        CreatedAt = DateTime.UtcNow;
    }
    
    public static Result<Customer> Create(FullName fullName, PhoneNumber phoneNumber, Email email)
    {
        try
        {
            var customer = new Customer(fullName, phoneNumber, email);
            
            customer.RaiseEvent(new CustomerCreatedEvent(
                customer.Id, 
                customer.FullName, 
                customer.PhoneNumber, 
                customer.Email));
            
            return Result<Customer>.Success(customer);
        }
        catch (Exception ex)
        {
            return Result<Customer>.Failure(ex.Message);
        }
    }
    
    public Result UpdateContactInfo(PhoneNumber phoneNumber, Email email)
    {
        PhoneNumber = phoneNumber;
        Email = email;
        
        RaiseEvent(new CustomerContactUpdatedEvent(Id, phoneNumber, email));
        
        return Result.Success();
    }
    
    public Result AddLoyaltyPoints(LoyaltyPoints points)
    {
        LoyaltyPoints = LoyaltyPoints.Add(points);
        UpdateTier();
        
        RaiseEvent(new LoyaltyPointsAddedEvent(Id, points, LoyaltyPoints));
        
        return Result.Success();
    }
    
    public Result RedeemLoyaltyPoints(LoyaltyPoints points)
    {
        if (LoyaltyPoints.Value < points.Value)
        {
            return Result.Failure("Insufficient loyalty points");
        }
        
        LoyaltyPoints = LoyaltyPoints.Subtract(points);
        UpdateTier();
        
        RaiseEvent(new LoyaltyPointsRedeemedEvent(Id, points, LoyaltyPoints));
        
        return Result.Success();
    }
    
    private void UpdateTier()
    {
        var newTier = LoyaltyPoints.Value switch
        {
            >= 1000 => CustomerTier.Gold,
            >= 500 => CustomerTier.Silver,
            >= 100 => CustomerTier.Bronze,
            _ => CustomerTier.Standard
        };
        
        if (newTier != Tier)
        {
            var oldTier = Tier;
            Tier = newTier;
            
            RaiseEvent(new CustomerTierChangedEvent(Id, oldTier, newTier));
        }
    }
}
```

#### **Day 6-7: Domain Events System**
```csharp
// Domain event base class
public abstract class DomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public string EventType { get; protected set; }
    public int Version { get; protected set; } = 1;
}

// Specific domain events
public record OrderCreatedEvent(Guid OrderId, Guid CustomerId, decimal TotalAmount) : DomainEvent
{
    public override string EventType => nameof(OrderCreatedEvent);
}

public record OrderItemAddedEvent(Guid OrderId, Guid OrderItemId, Guid ProductId, int Quantity) : DomainEvent
{
    public override string EventType => nameof(OrderItemAddedEvent);
}

public record CustomerCreatedEvent(Guid CustomerId, FullName FullName, PhoneNumber PhoneNumber, Email Email) : DomainEvent
{
    public override string EventType => nameof(CustomerCreatedEvent);
}

public record LoyaltyPointsAddedEvent(Guid CustomerId, LoyaltyPoints PointsAdded, LoyaltyPoints NewBalance) : DomainEvent
{
    public override string EventType => nameof(LoyaltyPointsAddedEvent);
}

// Aggregate root base class
public abstract class AggregateRoot
{
    private readonly List<DomainEvent> _domainEvents = new();
    
    public Guid Id { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }
    public string CreatedBy { get; protected set; }
    public string UpdatedBy { get; protected set; }
    
    public IReadOnlyCollection<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();
    
    protected void RaiseEvent(DomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
        UpdatedAt = DateTime.UtcNow;
    }
    
    protected void ApplyEvent(DomainEvent domainEvent)
    {
        // Apply event changes to aggregate
        // This would be used for event sourcing
        When(domainEvent);
        UpdatedAt = DateTime.UtcNow;
    }
    
    protected abstract void When(DomainEvent domainEvent);
}
```

### **3.2 Phase 2: Advanced Validation (Week 3-4)**

#### **Day 8-10: FluentValidation Integration**
```csharp
// Create validation rules for domain objects
public class OrderValidator : AbstractValidator<Order>
{
    public OrderValidator()
    {
        RuleFor(o => o.CustomerId)
            .NotEmpty()
            .WithMessage("Customer ID is required");
        
        RuleFor(o => o.Items)
            .NotEmpty()
            .WithMessage("Order must contain at least one item");
        
        RuleFor(o => o.Items.Count)
            .LessThanOrEqualTo(50)
            .WithMessage("Order cannot contain more than 50 items");
        
        RuleFor(o => o.GetTotalAmount())
            .GreaterThan(0)
            .WithMessage("Order total must be greater than 0")
            .LessThanOrEqualTo(1000000)
            .WithMessage("Order total cannot exceed 1,000,000 VND");
        
        RuleForEach(o => o.Items)
            .SetValidator(new OrderItemValidator());
    }
}

public class OrderItemValidator : AbstractValidator<OrderItem>
{
    public OrderItemValidator()
    {
        RuleFor(i => i.ProductId)
            .NotEmpty()
            .WithMessage("Product ID is required");
        
        RuleFor(i => i.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than 0")
            .LessThanOrEqualTo(100)
            .WithMessage("Quantity cannot exceed 100");
        
        RuleFor(i => i.UnitPrice)
            .GreaterThan(0)
            .WithMessage("Unit price must be greater than 0")
            .LessThanOrEqualTo(100000)
            .WithMessage("Unit price cannot exceed 100,000 VND");
    }
}

// Create validation service
public interface IDomainValidator<T>
{
    ValidationResult Validate(T instance);
}

public class DomainValidator<T> : IDomainValidator<T> where T : class
{
    private readonly IValidator<T> _validator;
    private readonly ILogger<DomainValidator<T>> _logger;

    public DomainValidator(IValidator<T> validator, ILogger<DomainValidator<T>> logger)
    {
        _validator = validator;
        _logger = logger;
    }

    public ValidationResult Validate(T instance)
    {
        var result = _validator.Validate(instance);
        
        if (!result.IsValid)
        {
            _logger.LogWarning("Validation failed for {EntityType}: {Errors}", 
                typeof(T).Name, string.Join(", ", result.Errors.Select(e => e.ErrorMessage)));
        }
        
        return new ValidationResult
        {
            IsValid = result.IsValid,
            Errors = result.Errors.Select(e => e.ErrorMessage).ToList()
        };
    }
}

// Apply validation in domain
public class Order : AggregateRoot
{
    public static Result<Order> Create(Guid customerId, List<OrderItemData> itemsData, IDomainValidator<Order> validator = null)
    {
        var order = new Order(customerId);
        
        // Add items
        foreach (var itemData in itemsData)
        {
            var addItemResult = order.AddItem(itemData.ProductId, itemData.Quantity, itemData.UnitPrice);
            if (!addItemResult.Success)
            {
                return Result<Order>.Failure(addItemResult.Errors);
            }
        }
        
        // Validate using FluentValidation
        if (validator != null)
        {
            var validationResult = validator.Validate(order);
            if (!validationResult.IsValid)
            {
                return Result<Order>.Failure(validationResult.Errors);
            }
        }
        
        order.RaiseEvent(new OrderCreatedEvent(order.Id, order.CustomerId, order.GetTotalAmount()));
        
        return Result<Order>.Success(order);
    }
}
```

#### **Day 11-12: Business Rules Engine**
```csharp
// Business rule interface
public interface IBusinessRule<T>
{
    string Name { get; }
    string Description { get; }
    bool IsSatisfied(T subject);
    string ErrorMessage { get; }
}

// Specific business rules
public class OrderTotalAmountRule : IBusinessRule<Order>
{
    private readonly decimal _maximumAmount;

    public OrderTotalAmountRule(decimal maximumAmount)
    {
        _maximumAmount = maximumAmount;
    }

    public string Name => "OrderTotalAmountRule";
    public string Description => $"Order total cannot exceed {_maximumAmount:C}";
    
    public bool IsSatisfied(Order subject)
    {
        return subject.GetTotalAmount() <= _maximumAmount;
    }

    public string ErrorMessage => $"Order total exceeds maximum allowed amount of {_maximumAmount:C}";
}

public class CustomerLoyaltyPointsRule : IBusinessRule<Customer>
{
    private readonly int _minimumPointsForDiscount;

    public CustomerLoyaltyPointsRule(int minimumPointsForDiscount)
    {
        _minimumPointsForDiscount = minimumPointsForDiscount;
    }

    public string Name => "CustomerLoyaltyPointsRule";
    public string Description => $"Customer must have at least {_minimumPointsForDiscount} loyalty points for discount";
    
    public bool IsSatisfied(Customer subject)
    {
        return subject.LoyaltyPoints.Value >= _minimumPointsForDiscount;
    }

    public string ErrorMessage => $"Insufficient loyalty points. Minimum required: {_minimumPointsForDiscount}";
}

// Rule validator
public class BusinessRuleValidator
{
    public static ValidationResult ValidateRules<T>(T subject, params IBusinessRule<T>[] rules)
    {
        var failures = new List<string>();
        
        foreach (var rule in rules)
        {
            if (!rule.IsSatisfied(subject))
            {
                failures.Add(rule.ErrorMessage);
            }
        }

        return new ValidationResult
        {
            IsValid = !failures.Any(),
            Errors = failures
        };
    }
}

// Apply business rules in domain
public class OrderService
{
    private readonly List<IBusinessRule<Order>> _orderRules;
    private readonly List<IBusinessRule<Customer>> _customerRules;

    public OrderService()
    {
        _orderRules = new List<IBusinessRule<Order>>
        {
            new OrderTotalAmountRule(1000000m),
            new OrderItemsLimitRule(50),
            new OrderBusinessHoursRule()
        };
        
        _customerRules = new List<IBusinessRule<Customer>>
        {
            new CustomerLoyaltyPointsRule(100),
            new CustomerActiveRule()
        };
    }

    public Result<Order> CreateOrder(Guid customerId, List<OrderItemData> itemsData)
    {
        // Get customer
        var customer = GetCustomer(customerId);
        if (customer == null)
        {
            return Result<Order>.Failure("Customer not found");
        }

        // Validate customer rules
        var customerValidationResult = BusinessRuleValidator.ValidateRules(customer, _customerRules.ToArray());
        if (!customerValidationResult.IsValid)
        {
            return Result<Order>.Failure(customerValidationResult.Errors);
        }

        // Create order
        var orderResult = Order.Create(customerId, itemsData);
        if (!orderResult.Success)
        {
            return Result<Order>.Failure(orderResult.Errors);
        }

        // Validate order rules
        var orderValidationResult = BusinessRuleValidator.ValidateRules(orderResult.Value, _orderRules.ToArray());
        if (!orderValidationResult.IsValid)
        {
            return Result<Order>.Failure(orderValidationResult.Errors);
        }

        return orderResult;
    }
}
```

#### **Day 13-14: Audit Trail Enhancement**
```csharp
// Enhanced audit trail
public abstract class AuditableEntity : BaseEntity
{
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public string CreatedBy { get; private set; }
    public string UpdatedBy { get; private set; }
    public string IpAddress { get; private set; }
    public string UserAgent { get; private set; }
    
    protected void SetAuditInfo(string createdBy, string ipAddress, string userAgent)
    {
        CreatedAt = DateTime.UtcNow;
        CreatedBy = createdBy;
        IpAddress = ipAddress;
        UserAgent = userAgent;
    }
    
    protected void UpdateAuditInfo(string updatedBy, string ipAddress, string userAgent)
    {
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
        IpAddress = ipAddress;
        UserAgent = userAgent;
    }
}

// Audit trail service
public interface IAuditService
{
    Task LogAsync(AuditEntry entry);
    Task<List<AuditEntry>> GetAuditHistoryAsync(Guid entityId, string entityType);
}

public class AuditService : IAuditService
{
    private readonly VanAnDbContext _context;
    private readonly ILogger<AuditService> _logger;

    public async Task LogAsync(AuditEntry entry)
    {
        try
        {
            _context.AuditEntries.Add(entry);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Audit entry created: {EntityType} {EntityId} {Action}", 
                entry.EntityType, entry.EntityId, entry.Action);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating audit entry");
        }
    }

    public async Task<List<AuditEntry>> GetAuditHistoryAsync(Guid entityId, string entityType)
    {
        return await _context.AuditEntries
            .Where(e => e.EntityId == entityId && e.EntityType == entityType)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync();
    }
}

// Apply audit in domain
public class Order : AuditableEntity
{
    public static Result<Order> Create(Guid customerId, List<OrderItemData> itemsData, 
        string createdBy, string ipAddress, string userAgent)
    {
        var order = new Order(customerId, itemsData);
        order.SetAuditInfo(createdBy, ipAddress, userAgent);
        
        // Log creation
        var auditEntry = new AuditEntry
        {
            EntityId = order.Id,
            EntityType = nameof(Order),
            Action = "Created",
            OldValues = null,
            NewValues = JsonSerializer.Serialize(order),
            Timestamp = DateTime.UtcNow,
            UserId = createdBy,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };
        
        // This would be injected and called from service
        // await _auditService.LogAsync(auditEntry);
        
        return Result<Order>.Success(order);
    }
}
```

### **3.3 Phase 3: Performance Optimization (Week 5-6)**

#### **Day 15-17: EF Core Optimization**
```csharp
// Optimized EF Core configuration
public class VanAnDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure entities with optimizations
        ConfigureOrder(modelBuilder);
        ConfigureCustomer(modelBuilder);
        ConfigureProduct(modelBuilder);
        
        // Add global query filters for soft delete
        ApplyGlobalQueryFilters(modelBuilder);
        
        // Configure indexes for performance
        ConfigureIndexes(modelBuilder);
    }
    
    private void ConfigureOrder(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            // Configure table name and schema
            entity.ToTable("Orders", "dbo");
            
            // Configure properties
            entity.Property(e => e.Id)
                .HasDefaultValueSql("newid()");
            
            entity.Property(e => e.TotalAmount)
                .HasPrecision(18, 2);
            
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("getutcdate()");
            
            // Configure relationships
            entity.HasOne(o => o.Customer)
                .WithMany(c => c.Orders)
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasMany(o => o.Items)
                .WithOne(i => i.Order)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Configure indexes
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.CustomerId, e.Status });
        });
    }
    
    private void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        // Soft delete filter
        modelBuilder.Entity<Order>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Customer>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Product>().HasQueryFilter(e => !e.IsDeleted);
        
        // Multi-tenancy filter
        modelBuilder.Entity<Order>().HasQueryFilter(e => EF.Property<string>(e, "TenantId") == _currentTenantId);
        modelBuilder.Entity<Customer>().HasQueryFilter(e => EF.Property<string>(e, "TenantId") == _currentTenantId);
    }
    
    private void ConfigureIndexes(ModelBuilder modelBuilder)
    {
        // Performance indexes
        modelBuilder.Entity<Order>()
            .HasIndex(e => new { e.CustomerId, e.Status, e.CreatedAt })
            .HasDatabaseName("IX_Orders_Customer_Status_CreatedAt");
        
        modelBuilder.Entity<OrderItem>()
            .HasIndex(e => new { e.OrderId, e.ProductId })
            .HasDatabaseName("IX_OrderItems_Order_Product");
        
        modelBuilder.Entity<Customer>()
            .HasIndex(e => e.PhoneNumber)
            .HasDatabaseName("IX_Customers_PhoneNumber")
            .IsUnique();
    }
}

// Optimized repository with query optimization
public class OrderRepository : IOrderRepository
{
    private readonly VanAnDbContext _context;
    private readonly ILogger<OrderRepository> _logger;

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<List<Order>> GetByCustomerAsync(Guid customerId, int pageIndex = 0, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Order>> GetActiveOrdersAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .Where(o => o.Status == OrderStatus.Processing || o.Status == OrderStatus.Confirmed)
            .OrderBy(o => o.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<OrderStatistics> GetStatisticsAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var stats = await _context.Orders
            .Where(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate)
            .GroupBy(o => o.Status)
            .Select(g => new
            {
                Status = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(o => o.TotalAmount)
            })
            .ToListAsync(cancellationToken);

        return new OrderStatistics
        {
            TotalOrders = stats.Sum(s => s.Count),
            TotalAmount = stats.Sum(s => s.TotalAmount),
            StatusBreakdown = stats.ToDictionary(s => s.Status, s => new OrderStatusStats
            {
                Count = s.Count,
                TotalAmount = s.TotalAmount
            })
        };
    }
}
```

#### **Day 18-19: Caching Strategy**
```csharp
// Domain caching service
public interface IDomainCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);
}

public class DomainCacheService : IDomainCacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<DomainCacheService> _logger;

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

// Cached repository decorator
public class CachedOrderRepository : IOrderRepository
{
    private readonly IOrderRepository _innerRepository;
    private readonly IDomainCacheService _cacheService;

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

    public async Task<List<Order>> GetByCustomerAsync(Guid customerId, int pageIndex = 0, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"orders_customer_{customerId}_{pageIndex}_{pageSize}";
        
        var orders = await _cacheService.GetAsync<List<Order>>(cacheKey, cancellationToken);
        if (orders != null)
        {
            return orders;
        }

        orders = await _innerRepository.GetByCustomerAsync(customerId, pageIndex, pageSize, cancellationToken);
        await _cacheService.SetAsync(cacheKey, orders, TimeSpan.FromMinutes(5), cancellationToken);

        return orders;
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        await _innerRepository.AddAsync(order, cancellationToken);
        
        // Invalidate cache
        await _cacheService.RemoveAsync($"order_{order.Id}", cancellationToken);
        await _cacheService.RemoveByPatternAsync("orders_customer_*", cancellationToken);
    }
}
```

#### **Day 20-21: Serialization Optimization**
```csharp
// Custom JSON converters for domain objects
public class PhoneNumberJsonConverter : JsonConverter<PhoneNumber>
{
    public override PhoneNumber Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return new PhoneNumber(value);
    }

    public override void Write(Utf8JsonWriter writer, PhoneNumber value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

public class EmailJsonConverter : JsonConverter<Email>
{
    public override Email Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return new Email(value);
    }

    public override void Write(Utf8JsonWriter writer, Email value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

// Optimized serialization options
public static class DomainJsonOptions
{
    public static JsonSerializerOptions Default => new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new PhoneNumberJsonConverter(),
            new EmailJsonConverter(),
            new LoyaltyPointsJsonConverter(),
            new JsonStringEnumConverter(),
            new UuidJsonConverter()
        }
    };
}

// Apply serialization in domain events
public class OrderCreatedEvent : DomainEvent
{
    public Guid OrderId { get; }
    public Guid CustomerId { get; }
    public decimal TotalAmount { get; }
    public DateTime CreatedAt { get; }

    public OrderCreatedEvent(Guid orderId, Guid customerId, decimal totalAmount)
    {
        OrderId = orderId;
        CustomerId = customerId;
        TotalAmount = totalAmount;
        CreatedAt = DateTime.UtcNow;
        EventType = nameof(OrderCreatedEvent);
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, DomainJsonOptions.Default);
    }

    public static OrderCreatedEvent FromJson(string json)
    {
        return JsonSerializer.Deserialize<OrderCreatedEvent>(json, DomainJsonOptions.Default);
    }
}
```

### **3.4 Phase 4: Testing & Documentation (Week 7-8)**

#### **Day 22-24: Comprehensive Testing**
```csharp
// Domain model tests
public class OrderTests
{
    [Fact]
    public void Create_Order_WithValidData_ShouldSucceed()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var itemsData = new List<OrderItemData>
        {
            new() { ProductId = Guid.NewGuid(), Quantity = 2, UnitPrice = 25000 }
        };

        // Act
        var result = Order.Create(customerId, itemsData);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.CustomerId.Should().Be(customerId);
        result.Value.Items.Should().HaveCount(1);
        result.Value.Status.Should().Be(OrderStatus.Draft);
    }

    [Fact]
    public void Create_Order_WithInvalidQuantity_ShouldFail()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var itemsData = new List<OrderItemData>
        {
            new() { ProductId = Guid.NewGuid(), Quantity = 0, UnitPrice = 25000 }
        };

        // Act
        var result = Order.Create(customerId, itemsData);

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain("Quantity must be greater than 0");
    }

    [Fact]
    public void AddItem_ToOrder_ShouldRaiseEvent()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), new List<OrderItemData>
        {
            new() { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 25000 }
        }).Value;

        // Act
        var result = order.AddItem(Guid.NewGuid(), 2, 30000);

        // Assert
        result.Success.Should().BeTrue();
        order.GetDomainEvents().Should().Contain(e => e is OrderItemAddedEvent);
    }
}

// Value object tests
public class PhoneNumberTests
{
    [Theory]
    [InlineData("0912345678")]
    [InlineData("0387654321")]
    [InlineData("09876543210")]
    public void Create_ValidPhoneNumber_ShouldSucceed(string phoneNumber)
    {
        // Act
        var phone = new PhoneNumber(phoneNumber);

        // Assert
        phone.Value.Should().Be(phoneNumber);
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("091234567")]
    [InlineData("")]
    [InlineData(null)]
    public void Create_InvalidPhoneNumber_ShouldThrowException(string phoneNumber)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new PhoneNumber(phoneNumber));
    }
}

// Integration tests
public class OrderRepositoryTests : IntegrationTestBase
{
    [Fact]
    public async Task Add_Order_Should_Persist_To_Database()
    {
        // Arrange
        var repository = new OrderRepository(Context);
        var order = Order.Create(Guid.NewGuid(), new List<OrderItemData>
        {
            new() { ProductId = Guid.NewGuid(), Quantity = 2, UnitPrice = 25000 }
        }).Value;

        // Act
        await repository.AddAsync(order);
        await Context.SaveChangesAsync();

        // Assert
        var savedOrder = await Context.Orders.FindAsync(order.Id);
        savedOrder.Should().NotBeNull();
        savedOrder.CustomerId.Should().Be(order.CustomerId);
    }
}
```

#### **Day 25-26: Documentation Generation**
```csharp
// XML documentation for domain models
/// <summary>
/// Represents an order in the VanAn ecosystem with rich domain behaviors and validation.
/// </summary>
/// <remarks>
/// <para>
/// This entity follows Domain-Driven Design principles with:
/// - Encapsulated business logic
/// - Domain events for side effects
/// - Immutable aggregate boundaries
/// - Business rule validation
/// </para>
/// <para>
/// Order lifecycle:
/// 1. Created as Draft
/// 2. Confirmed (validation passed)
/// 3. Processing (kitchen preparation)
/// 4. Completed (order fulfilled)
/// 5. Cancelled (if applicable)
/// </para>
/// </remarks>
public class Order : AggregateRoot
{
    /// <summary>
    /// Gets the unique identifier for the order.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the customer identifier who placed the order.
    /// </summary>
    public Guid CustomerId { get; private set; }

    /// <summary>
    /// Gets the current status of the order.
    /// </summary>
    public OrderStatus Status { get; private set; }

    /// <summary>
    /// Gets the collection of order items.
    /// </summary>
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    /// <summary>
    /// Creates a new order with the specified customer and items.
    /// </summary>
    /// <param name="customerId">The customer identifier.</param>
    /// <param name="itemsData">The collection of order item data.</param>
    /// <returns>A Result containing the created order or validation errors.</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    /// <example>
    /// <code>
    /// var itemsData = new List&lt;OrderItemData&gt;
    /// {
    ///     new() { ProductId = productId, Quantity = 2, UnitPrice = 25000 }
    /// };
    /// var result = Order.Create(customerId, itemsData);
    /// </code>
    /// </example>
    public static Result<Order> Create(Guid customerId, List<OrderItemData> itemsData)
    {
        // Implementation...
    }

    /// <summary>
    /// Adds an item to the order.
    /// </summary>
    /// <param name="productId">The product identifier.</param>
    /// <param name="quantity">The quantity to add.</param>
    /// <param name="unitPrice">The unit price.</param>
    /// <returns>A Result containing the added order item or validation errors.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the order is not in Draft status.</exception>
    public Result<OrderItem> AddItem(Guid productId, int quantity, decimal unitPrice)
    {
        // Implementation...
    }
}
```

#### **Day 27-28: Performance Testing**
```csharp
// Performance tests for domain operations
public class OrderPerformanceTests
{
    [Fact]
    public async Task Create_1000_Orders_Should_Complete_Under_1_Second()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        var orders = new List<Order>();

        // Act
        for (int i = 0; i < 1000; i++)
        {
            var result = Order.Create(
                Guid.NewGuid(),
                new List<OrderItemData>
                {
                    new() { ProductId = Guid.NewGuid(), Quantity = 2, UnitPrice = 25000 }
                });
            
            if (result.Success)
            {
                orders.Add(result.Value);
            }
        }

        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000);
        orders.Should().HaveCount(1000);
    }

    [Fact]
    public void Calculate_TotalAmount_For_Large_Order_Should_Be_Fast()
    {
        // Arrange
        var itemsData = Enumerable.Range(0, 1000)
            .Select(i => new OrderItemData
            {
                ProductId = Guid.NewGuid(),
                Quantity = 1,
                UnitPrice = 25000
            }).ToList();

        var order = Order.Create(Guid.NewGuid(), itemsData).Value;

        // Act
        var stopwatch = Stopwatch.StartNew();
        var totalAmount = order.GetTotalAmount();
        stopwatch.Stop();

        // Assert
        totalAmount.Should().Be(25000000);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10);
    }
}
```

---

## **4. IMPLEMENTATION PLAN**

### **4.1 Week 1-2: Domain Model Enhancement**
- [ ] Implement rich domain models with behaviors
- [ ] Create immutable value objects
- [ ] Add domain events system
- [ ] Implement aggregate roots
- [ ] Add business rule validation
- [ ] Create domain event handlers

### **4.2 Week 3-4: Advanced Validation**
- [ ] Implement FluentValidation
- [ ] Create business rules engine
- [ ] Add comprehensive validation
- [ ] Implement validation service
- [ ] Add validation to domain
- [ ] Create custom validation rules

### **4.3 Week 5-6: Performance Optimization**
- [ ] Optimize EF Core configuration
- [ ] Add performance indexes
- [ ] Implement caching strategy
- [ ] Optimize serialization
- [ ] Add query optimization
- [ ] Implement bulk operations

### **4.4 Week 7-8: Testing & Documentation**
- [ ] Create comprehensive unit tests
- [ ] Add integration tests
- [ ] Implement performance tests
- [ ] Add XML documentation
- [ ] Create API documentation
- [ ] Add developer guide

---

## **5. SUCCESS METRICS**

### **5.1 Quality Metrics**
- **Code Coverage:** >95% for domain models
- **Domain Events:** 100% of critical operations
- **Business Rules:** 100% validation coverage
- **Performance:** <50ms for domain operations

### **5.2 Architecture Metrics**
- **DDD Compliance:** 100%
- **Immutability:** 100% for value objects
- **Event Coverage:** >90% of operations
- **Rule Coverage:** 100% of business rules

### **5.3 Performance Metrics**
- **Domain Operations:** <10ms for simple operations
- **Serialization:** <5ms for complex objects
- **Cache Hit Rate:** >80%
- **Memory Usage:** <100MB for 1000 entities

---

## **6. RISK MITIGATION**

### **6.1 Technical Risks**
1. **Breaking Changes** - Version domain events
2. **Performance Regression** - Continuous monitoring
3. **Data Migration** - Incremental migration
4. **Complexity Increase** - Proper documentation

### **6.2 Business Risks**
1. **Rule Changes** - Configurable rules
2. **Domain Evolution** - Flexible architecture
3. **Team Adoption** - Training and support
4. **Maintenance** - Clear ownership

---

## **7. NEXT STEPS**

### **7.1 Immediate Actions (This Week)**
1. **Setup Development Environment** - Ensure tools ready
2. **Create Feature Branch** - Isolate changes
3. **Implement Rich Domain Models** - Start with Order entity
4. **Add Unit Tests** - Test domain behaviors

### **7.2 Short-term Goals (2 Weeks)**
1. **Complete Domain Enhancement** - Full rich domain
2. **Implement Validation Framework** - Comprehensive validation
3. **Add Domain Events** - Event-driven architecture
4. **Create Integration Tests** - Test persistence

### **7.3 Long-term Goals (2 Months)**
1. **Complete All Improvements** - Full implementation
2. **Achieve Performance Goals** - Meet all metrics
3. **Team Training** - Ensure team adoption
4. **Documentation Complete** - Full documentation set

---

## **8. SUMMARY**

### **8.1 Current State**
- **Basic domain models** with limited behaviors
- **No domain events** or business rules
- **Basic validation** only
- **No performance optimization**

### **8.2 Target State**
- **Rich domain models** with encapsulated logic
- **Event-driven architecture** with domain events
- **Comprehensive validation** and business rules
- **Optimized performance** with caching

### **8.3 Implementation Strategy**
- **8-week phased approach** with clear milestones
- **Domain-first approach** with business focus
- **Performance optimization** throughout
- **Quality focus** with comprehensive testing

**Status:** Shared module là foundation c?a system v?i significant gaps but có clear improvement plan v?i domain-driven approach.
