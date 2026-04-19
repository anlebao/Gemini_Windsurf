# Shared - Lu?ng X? Lý T??ng (Ideal Architecture)

**Ngày:** 11 tháng 4, 2026  
**Module:** 1_Shared  
**Tr?ng thái:** Thi?t k? lu?ng x? lý lý t??ng ho?n ch?nh

---

## **1. T?NG QUAN ARCHITECTURE LÝ T??NG**

### **1.1 Domain-Driven Design Structure**
```
1_Shared/
?? Domain/                    (Domain Layer)
   ?? Entities/
      ?? Order.cs              (Aggregate Root)
      ?? Customer.cs           (Aggregate Root)
      ?? Product.cs            (Entity)
      ?? OrderItem.cs          (Entity)
      ?? Ingredient.cs        (Entity)
      ?? Recipe.cs             (Entity)
      ?? Inventory.cs         (Entity)
      ?? Shop.cs               (Aggregate Root)
      ?? AccountingEntry.cs   (Entity)
   ?? Value Objects/
      ?? OrderId.cs
      ?? CustomerId.cs
      ?? ProductId.cs
      ?? Money.cs
      ?? Quantity.cs
      ?? Percentage.cs
      ?? VatRate.cs
      ?? Address.cs
      ?? PhoneNumber.cs
      ?? Email.cs
      ?? KitchenStatus.cs
      ?? OrderStatus.cs
      ?? PaymentMethod.cs
   ?? Events/
      ?? IDomainEvent.cs
      ?? OrderCreatedEvent.cs
      ?? OrderConfirmedEvent.cs
      ?? OrderPaidEvent.cs
      ?? OrderCompletedEvent.cs
      ?? CustomerRegisteredEvent.cs
      ?? CustomerTierUpdatedEvent.cs
      ?? InventoryLowEvent.cs
      ?? KitchenItemStatusChangedEvent.cs
   ?? Services/
      ?? IDomainService.cs
      ?? IOrderDomainService.cs
      ?? IPricingService.cs
      ?? IInventoryDomainService.cs
      ?? ICustomerDomainService.cs
   ?? Specifications/
      ?? ISpecification.cs
      ?? OrderSpecification.cs
      ?? CustomerSpecification.cs
      ?? ProductSpecification.cs
   ?? Repositories/
      ?? IRepository.cs
      ?? IOrderRepository.cs
      ?? ICustomerRepository.cs
      ?? IProductRepository.cs
      ?? IInventoryRepository.cs
   ?? Exceptions/
      ?? DomainException.cs
      ?? BusinessRuleViolationException.cs
      ?? ConcurrencyException.cs
      ?? NotFoundException.cs
   ?? Common/
      ?? BaseEntity.cs
      ?? AggregateRoot.cs
      ?? Entity.cs
      ?? ValueObject.cs
      ?? IMustHaveTenant.cs
      ?? IAuditable.cs
      ?? ISoftDeletable.cs
?? Application/              (Application Layer)
   ?? Commands/
      ?? CreateOrderCommand.cs
      ?? UpdateOrderCommand.cs
      ?? RegisterCustomerCommand.cs
      ?? UpdateInventoryCommand.cs
   ?? Queries/
      ?? GetOrderQuery.cs
      ?? GetCustomerQuery.cs
      ?? GetProductQuery.cs
      ?? GetInventoryQuery.cs
   ?? DTOs/
      ?? OrderDto.cs
      ?? CustomerDto.cs
      ?? ProductDto.cs
      ?? InventoryDto.cs
      ?? KitchenItemDto.cs
      ?? VoiceNoteDto.cs
      ?? VietQrDto.cs
   ?? Validators/
      ?? IValidator.cs
      ?? CreateOrderCommandValidator.cs
      ?? RegisterCustomerCommandValidator.cs
      ?? UpdateInventoryCommandValidator.cs
   ?? Services/
      ?? IOrderApplicationService.cs
      ?? ICustomerApplicationService.cs
      ?? IProductApplicationService.cs
      ?? IInventoryApplicationService.cs
   ?? Events/
      ?? IApplicationEvent.cs
      ?? OrderCreatedApplicationEvent.cs
      ?? CustomerRegisteredApplicationEvent.cs
?? Infrastructure/            (Infrastructure Layer)
   ?? Persistence/
      ?? Configurations/
         ?? OrderConfiguration.cs
         ?? CustomerConfiguration.cs
         ?? ProductConfiguration.cs
         ?? InventoryConfiguration.cs
      ?? ValueConverters/
         ?? OrderIdConverter.cs
         ?? CustomerIdConverter.cs
         ?? MoneyConverter.cs
         ?? QuantityConverter.cs
   ?? External/
      ?? Services/
         ?? IVietQrService.cs
         ?? ISpeechRecognitionService.cs
         ?? IEmailService.cs
         ?? ISmsService.cs
         ?? IPushNotificationService.cs
   ?? Caching/
      ?? ICacheService.cs
      ?? RedisCacheService.cs
      ?? MemoryCacheService.cs
   ?? Messaging/
      ?? IEventBus.cs
      ?? IMessageBroker.cs
      ?? RabbitMqEventBus.cs
      ?? KafkaEventBus.cs
   ?? Logging/
      ?? IDomainLogger.cs
      ?? StructuredLogger.cs
      ?? AuditLogger.cs
?? CrossCutting/             (Cross-cutting Concerns)
   ?? Security/
      ?? ITenantProvider.cs
      ?? IUserContext.cs
      ?? IEncryptionService.cs
      ?? IHashingService.cs
   ?? Validation/
      ?? IValidationService.cs
      ?? FluentValidationService.cs
      ?? BusinessRuleValidator.cs
   ?? Localization/
      ?? ILocalizationService.cs
      ?? ITranslationProvider.cs
      ?? ResourceTranslationProvider.cs
      ?? DatabaseTranslationProvider.cs
   ?? Auditing/
      ?? IAuditService.cs
      ?? ChangeTracker.cs
      ?? AuditTrailService.cs
   ?? Metrics/
      ?? IMetricsService.cs
      ?? DomainMetricsCollector.cs
      ?? PerformanceTracker.cs
```

---

## **2. LU?NG X? LÝ LÝ T??NG**

### **2.1 Domain Entities with Rich Behavior**

#### **Phase 1: Order Aggregate Root**
```csharp
// Domain Entity - Order.cs
public class Order : AggregateRoot<OrderId>, IMustHaveTenant, IAuditable, ISoftDeletable
{
    public CustomerId CustomerId { get; private set; }
    public TenantId TenantId { get; private set; }
    public OrderType OrderType { get; private set; }
    public OrderStatus Status { get; private set; }
    public Money SubTotal { get; private set; }
    public Money VatAmount { get; private set; }
    public Money DiscountAmount { get; private set; }
    public Money ShippingFee { get; private set; }
    public Money TotalAmount { get; private set; }
    public PaymentMethod? PaymentMethod { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; }
    public string CustomerNotes { get; private set; }
    public string StaffNotes { get; private set; }
    public string TrackingCode { get; private set; }
    public DateTime OrderDate { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? VietQRTransactionId { get; private set; }
    public string? VietQRPayload { get; private set; }
    public KitchenStatus KitchenStatus { get; private set; }
    public VoiceNote? VoiceNote { get; private set; }
    
    private readonly List<OrderItem> _items = new();
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    // Factory Method
    public static Order Create(
        CustomerId customerId,
        TenantId tenantId,
        OrderType orderType,
        string customerNotes,
        List<OrderItemData> itemsData,
        string? trackingCode = null)
    {
        // Business Rules
        if (itemsData == null || !itemsData.Any())
            throw new BusinessRuleViolationException("Order must have at least one item");

        if (itemsData.Count > 50)
            throw new BusinessRuleViolationException("Order cannot have more than 50 items");

        if (string.IsNullOrWhiteSpace(customerNotes))
            customerNotes = string.Empty;

        var order = new Order
        {
            Id = new OrderId(Guid.NewGuid()),
            CustomerId = customerId,
            TenantId = tenantId,
            OrderType = orderType,
            Status = OrderStatus.Draft,
            CustomerNotes = customerNotes,
            TrackingCode = trackingCode,
            OrderDate = DateTime.UtcNow,
            KitchenStatus = KitchenStatus.Pending,
            PaymentStatus = PaymentStatus.Pending
        };

        // Add Order Items
        foreach (var itemData in itemsData)
        {
            var orderItem = OrderItem.Create(
                orderId: order.Id,
                productId: itemData.ProductId,
                quantity: itemData.Quantity,
                unitPrice: itemData.UnitPrice,
                vatRate: itemData.VatRate,
                notes: itemData.Notes
            );
            order._items.Add(orderItem);
        }

        // Calculate Totals
        order.CalculateTotals();

        // Add Domain Event
        order.AddDomainEvent(new OrderCreatedEvent(
            orderId: order.Id,
            customerId: order.CustomerId,
            tenantId: order.TenantId,
            orderType: order.OrderType,
            totalAmount: order.TotalAmount,
            items: order.Items.Select(i => new OrderItemEvent(
                productId: i.ProductId,
                quantity: i.Quantity,
                unitPrice: i.UnitPrice,
                totalPrice: i.TotalAmount
            )),
            trackingCode: order.TrackingCode,
            orderDate: order.OrderDate
        ));

        return order;
    }

    // Business Methods
    public void Confirm()
    {
        if (Status != OrderStatus.Draft)
            throw new BusinessRuleViolationException($"Cannot confirm order in {Status} status");

        if (!Items.Any())
            throw new BusinessRuleViolationException("Cannot confirm order without items");

        Status = OrderStatus.Confirmed;
        ConfirmedAt = DateTime.UtcNow;
        KitchenStatus = KitchenStatus.Pending;

        AddDomainEvent(new OrderConfirmedEvent(
            orderId: Id,
            customerId: CustomerId,
            tenantId: TenantId,
            confirmedAt: ConfirmedAt.Value
        ));
    }

    public void StartPayment(PaymentMethod paymentMethod)
    {
        if (Status != OrderStatus.Confirmed)
            throw new BusinessRuleViolationException($"Cannot start payment for order in {Status} status");

        PaymentMethod = paymentMethod;
        Status = OrderStatus.PendingPayment;

        AddDomainEvent(new OrderPaymentStartedEvent(
            orderId: Id,
            customerId: CustomerId,
            paymentMethod: paymentMethod,
            amount: TotalAmount,
            startedAt: DateTime.UtcNow
        ));
    }

    public void CompletePayment(string transactionId, string payload)
    {
        if (Status != OrderStatus.PendingPayment)
            throw new BusinessRuleViolationException($"Cannot complete payment for order in {Status} status");

        PaymentStatus = PaymentStatus.Paid;
        Status = OrderStatus.Paid;
        PaidAt = DateTime.UtcNow;
        VietQRTransactionId = transactionId;
        VietQRPayload = payload;

        AddDomainEvent(new OrderPaidEvent(
            orderId: Id,
            customerId: CustomerId,
            paymentMethod: PaymentMethod!.Value,
            amount: TotalAmount,
            transactionId: transactionId,
            paidAt: PaidAt.Value
        ));
    }

    public void StartPreparation()
    {
        if (Status != OrderStatus.Paid)
            throw new BusinessRuleViolationException($"Cannot start preparation for order in {Status} status");

        Status = OrderStatus.Preparing;
        KitchenStatus = KitchenStatus.Preparing;

        AddDomainEvent(new OrderPreparationStartedEvent(
            orderId: Id,
            customerId: CustomerId,
            startedAt: DateTime.UtcNow
        ));
    }

    public void Complete()
    {
        if (Status != OrderStatus.Preparing)
            throw new BusinessRuleViolationException($"Cannot complete order in {Status} status");

        Status = OrderStatus.Completed;
        KitchenStatus = KitchenStatus.Completed;
        CompletedAt = DateTime.UtcNow;

        AddDomainEvent(new OrderCompletedEvent(
            orderId: Id,
            customerId: CustomerId,
            completedAt: CompletedAt.Value
        ));
    }

    public void Cancel(string reason)
    {
        if (Status == OrderStatus.Completed || Status == OrderStatus.Cancelled)
            throw new BusinessRuleViolationException($"Cannot cancel order in {Status} status");

        Status = OrderStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        StaffNotes = $"Cancelled: {reason}";

        AddDomainEvent(new OrderCancelledEvent(
            orderId: Id,
            customerId: CustomerId,
            reason: reason,
            cancelledAt: CancelledAt.Value
        ));
    }

    public void AttachVoiceNote(VoiceNote voiceNote)
    {
        if (voiceNote == null)
            throw new DomainException("Voice note cannot be null");

        VoiceNote = voiceNote;

        AddDomainEvent(new VoiceNoteAttachedEvent(
            orderId: Id,
            voiceNoteId: voiceNote.Id,
            attachedAt: DateTime.UtcNow
        ));
    }

    public void UpdateKitchenStatus(KitchenStatus newStatus)
    {
        if (!IsValidKitchenStatusTransition(KitchenStatus, newStatus))
            throw new BusinessRuleViolationException($"Invalid kitchen status transition from {KitchenStatus} to {newStatus}");

        var oldStatus = KitchenStatus;
        KitchenStatus = newStatus;

        AddDomainEvent(new KitchenStatusChangedEvent(
            orderId: Id,
            oldStatus: oldStatus,
            newStatus: newStatus,
            changedAt: DateTime.UtcNow
        ));
    }

    public void ApplyDiscount(Money discountAmount, string discountReason)
    {
        if (Status != OrderStatus.Draft && Status != OrderStatus.Confirmed)
            throw new BusinessRuleViolationException($"Cannot apply discount to order in {Status} status");

        if (discountAmount.Value <= 0)
            throw new BusinessRuleViolationException("Discount amount must be positive");

        if (discountAmount.Value > SubTotal.Value)
            throw new BusinessRuleViolationException("Discount cannot exceed subtotal");

        var oldDiscount = DiscountAmount;
        DiscountAmount = discountAmount;
        CalculateTotals();

        AddDomainEvent(new OrderDiscountAppliedEvent(
            orderId: Id,
            oldDiscountAmount: oldDiscount,
            newDiscountAmount: discountAmount,
            reason: discountReason,
            appliedAt: DateTime.UtcNow
        ));
    }

    private void CalculateTotals()
    {
        SubTotal = new Money(Items.Sum(item => item.SubTotal.Value));
        
        // Calculate VAT on discounted amount
        var discountedSubtotal = SubTotal.Value - DiscountAmount.Value;
        VatAmount = new Money(discountedSubtotal * 0.10m); // 10% VAT
        
        TotalAmount = new Money(discountedSubtotal + VatAmount.Value + ShippingFee.Value);
    }

    private static bool IsValidKitchenStatusTransition(KitchenStatus from, KitchenStatus to)
    {
        return (from, to) switch
        {
            (KitchenStatus.Pending, KitchenStatus.Preparing) => true,
            (KitchenStatus.Preparing, KitchenStatus.Completed) => true,
            (KitchenStatus.Preparing, KitchenStatus.Cancelled) => true,
            (KitchenStatus.Pending, KitchenStatus.Cancelled) => true,
            _ => false
        };
    }
}
```

#### **Phase 2: Order Item Entity**
```csharp
// Domain Entity - OrderItem.cs
public class OrderItem : Entity<OrderItemId>
{
    public OrderId OrderId { get; private set; }
    public ProductId ProductId { get; private set; }
    public Quantity Quantity { get; private set; }
    public Money UnitPrice { get; private set; }
    public Percentage VatRate { get; private set; }
    public Money SubTotal => new Money(Quantity.Value * UnitPrice.Value);
    public Money VatAmount => new Money(SubTotal.Value * VatRate.Value);
    public Money TotalAmount => new Money(SubTotal.Value + VatAmount.Value);
    public string Notes { get; private set; }
    public KitchenStatus KitchenStatus { get; private set; }
    public VoiceNote? VoiceNote { get; private set; }

    // Factory Method
    public static OrderItem Create(
        OrderId orderId,
        ProductId productId,
        Quantity quantity,
        Money unitPrice,
        Percentage vatRate,
        string notes)
    {
        // Business Rules
        if (quantity.Value <= 0)
            throw new BusinessRuleViolationException("Quantity must be greater than 0");

        if (quantity.Value > 100)
            throw new BusinessRuleViolationException("Quantity cannot exceed 100");

        if (unitPrice.Value <= 0)
            throw new BusinessRuleViolationException("Unit price must be greater than 0");

        if (vatRate.Value < 0 || vatRate.Value > 1)
            throw new BusinessRuleViolationException("VAT rate must be between 0 and 1");

        return new OrderItem
        {
            Id = new OrderItemId(Guid.NewGuid()),
            OrderId = orderId,
            ProductId = productId,
            Quantity = quantity,
            UnitPrice = unitPrice,
            VatRate = vatRate,
            Notes = notes?.Trim() ?? string.Empty,
            KitchenStatus = KitchenStatus.Pending
        };
    }

    // Business Methods
    public void UpdateKitchenStatus(KitchenStatus newStatus)
    {
        if (!IsValidKitchenStatusTransition(KitchenStatus, newStatus))
            throw new BusinessRuleViolationException($"Invalid kitchen status transition from {KitchenStatus} to {newStatus}");

        var oldStatus = KitchenStatus;
        KitchenStatus = newStatus;

        AddDomainEvent(new KitchenItemStatusChangedEvent(
            orderItemId: Id,
            orderId: OrderId,
            productId: ProductId,
            oldStatus: oldStatus,
            newStatus: newStatus,
            changedAt: DateTime.UtcNow
        ));
    }

    public void AttachVoiceNote(VoiceNote voiceNote)
    {
        if (voiceNote == null)
            throw new DomainException("Voice note cannot be null");

        VoiceNote = voiceNote;

        AddDomainEvent(new VoiceNoteAttachedEvent(
            orderItemId: Id,
            orderId: OrderId,
            voiceNoteId: voiceNote.Id,
            attachedAt: DateTime.UtcNow
        ));
    }

    public void UpdateNotes(string notes)
    {
        if (notes?.Length > 500)
            throw new BusinessRuleViolationException("Notes cannot exceed 500 characters");

        Notes = notes?.Trim() ?? string.Empty;
    }

    private static bool IsValidKitchenStatusTransition(KitchenStatus from, KitchenStatus to)
    {
        return (from, to) switch
        {
            (KitchenStatus.Pending, KitchenStatus.Preparing) => true,
            (KitchenStatus.Preparing, KitchenStatus.Completed) => true,
            (KitchenStatus.Preparing, KitchenStatus.Cancelled) => true,
            (KitchenStatus.Pending, KitchenStatus.Cancelled) => true,
            _ => false
        };
    }
}
```

---

### **2.2 Value Objects with Rich Behavior**

#### **Phase 1: Money Value Object**
```csharp
// Value Object - Money.cs
public record Money : ValueObject
{
    public decimal Value { get; }

    public static Money Zero => new Money(0);

    public Money(decimal value)
    {
        if (value < 0)
            throw new BusinessRuleViolationException("Money cannot be negative");

        if (value > 999999999.99m)
            throw new BusinessRuleViolationException("Money value too large");

        Value = Math.Round(value, 2);
    }

    public static Money operator +(Money left, Money right)
    {
        if (left is null) throw new ArgumentNullException(nameof(left));
        if (right is null) throw new ArgumentNullException(nameof(right));
        
        return new Money(left.Value + right.Value);
    }

    public static Money operator -(Money left, Money right)
    {
        if (left is null) throw new ArgumentNullException(nameof(left));
        if (right is null) throw new ArgumentNullException(nameof(right));
        
        var result = left.Value - right.Value;
        if (result < 0)
            throw new BusinessRuleViolationException("Resulting money cannot be negative");
        
        return new Money(result);
    }

    public static Money operator *(Money money, decimal multiplier)
    {
        if (money is null) throw new ArgumentNullException(nameof(money));
        
        if (multiplier < 0)
            throw new BusinessRuleViolationException("Multiplier cannot be negative");
        
        return new Money(money.Value * multiplier);
    }

    public static Money operator *(decimal multiplier, Money money)
    {
        return money * multiplier;
    }

    public static Money operator /(Money money, decimal divisor)
    {
        if (money is null) throw new ArgumentNullException(nameof(money));
        
        if (divisor <= 0)
            throw new BusinessRuleViolationException("Divisor must be positive");
        
        return new Money(money.Value / divisor);
    }

    public Money ApplyPercentage(Percentage percentage)
    {
        return new Money(Value * percentage.Value);
    }

    public Money ApplyVat(VatRate vatRate)
    {
        return new Money(Value * vatRate.Value);
    }

    public Money ApplyDiscount(Percentage discountPercentage)
    {
        return new Money(Value * (1 - discountPercentage.Value));
    }

    public bool IsGreaterThan(Money other)
    {
        if (other is null) throw new ArgumentNullException(nameof(other));
        return Value > other.Value;
    }

    public bool IsLessThan(Money other)
    {
        if (other is null) throw new ArgumentNullException(nameof(other));
        return Value < other.Value;
    }

    public bool IsZero() => Value == 0;

    public override string ToString()
    {
        return Value.ToString("N2", CultureInfo.InvariantCulture);
    }
}
```

#### **Phase 2: Quantity Value Object**
```csharp
// Value Object - Quantity.cs
public record Quantity : ValueObject
{
    public int Value { get; }

    public static Quantity Zero => new Quantity(0);
    public static Quantity One => new Quantity(1);

    public Quantity(int value)
    {
        if (value < 0)
            throw new BusinessRuleViolationException("Quantity cannot be negative");

        if (value > 10000)
            throw new BusinessRuleViolationException("Quantity too large");

        Value = value;
    }

    public static Quantity operator +(Quantity left, Quantity right)
    {
        if (left is null) throw new ArgumentNullException(nameof(left));
        if (right is null) throw new ArgumentNullException(nameof(right));
        
        return new Quantity(left.Value + right.Value);
    }

    public static Quantity operator -(Quantity left, Quantity right)
    {
        if (left is null) throw new ArgumentNullException(nameof(left));
        if (right is null) throw new ArgumentNullException(nameof(right));
        
        var result = left.Value - right.Value;
        if (result < 0)
            throw new BusinessRuleViolationException("Resulting quantity cannot be negative");
        
        return new Quantity(result);
    }

    public static Quantity operator *(Quantity quantity, int multiplier)
    {
        if (quantity is null) throw new ArgumentNullException(nameof(quantity));
        
        if (multiplier < 0)
            throw new BusinessRuleViolationException("Multiplier cannot be negative");
        
        return new Quantity(quantity.Value * multiplier);
    }

    public static Quantity operator *(int multiplier, Quantity quantity)
    {
        return quantity * multiplier;
    }

    public static Quantity operator /(Quantity quantity, int divisor)
    {
        if (quantity is null) throw new ArgumentNullException(nameof(quantity));
        
        if (divisor <= 0)
            throw new BusinessRuleViolationException("Divisor must be positive");
        
        return new Quantity(quantity.Value / divisor);
    }

    public bool IsGreaterThan(Quantity other)
    {
        if (other is null) throw new ArgumentNullException(nameof(other));
        return Value > other.Value;
    }

    public bool IsLessThan(Quantity other)
    {
        if (other is null) throw new ArgumentNullException(nameof(other));
        return Value < other.Value;
    }

    public bool IsZero() => Value == 0;

    public bool IsPositive() => Value > 0;

    public override string ToString()
    {
        return Value.ToString();
    }
}
```

#### **Phase 3: PhoneNumber Value Object**
```csharp
// Value Object - PhoneNumber.cs
public record PhoneNumber : ValueObject
{
    public string Value { get; }

    public PhoneNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new BusinessRuleViolationException("Phone number is required");

        var cleaned = value.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");

        if (!IsValidVietnamesePhoneNumber(cleaned))
            throw new BusinessRuleViolationException("Invalid Vietnamese phone number format");

        Value = cleaned;
    }

    private static bool IsValidVietnamesePhoneNumber(string phoneNumber)
    {
        // Vietnamese phone number validation
        return Regex.IsMatch(phoneNumber, @"^(0|\+84)[3-9][0-9]{8,9}$");
    }

    public string GetFormattedNumber()
    {
        if (Value.StartsWith("+84"))
        {
            return $"+84 {Value.Substring(3, 3)} {Value.Substring(6)}";
        }
        else
        {
            return $"{Value.Substring(0, 3)} {Value.Substring(3, 3)} {Value.Substring(6)}";
        }
    }

    public string GetInternationalFormat()
    {
        if (Value.StartsWith("0"))
        {
            return $"+84{Value.Substring(1)}";
        }
        return Value;
    }

    public override string ToString()
    {
        return GetFormattedNumber();
    }
}
```

---

### **2.3 Domain Events**

#### **Phase 1: Order Events**
```csharp
// Domain Event - OrderCreatedEvent.cs
public record OrderCreatedEvent(
    Guid EventId,
    DateTime EventDate,
    OrderId OrderId,
    CustomerId CustomerId,
    TenantId TenantId,
    OrderType OrderType,
    Money TotalAmount,
    List<OrderItemEvent> Items,
    string? TrackingCode,
    DateTime OrderDate
) : IDomainEvent
{
    public OrderCreatedEvent(
        OrderId orderId,
        CustomerId customerId,
        TenantId tenantId,
        OrderType orderType,
        Money totalAmount,
        List<OrderItemEvent> items,
        string? trackingCode,
        DateTime orderDate
    ) : this(
        Guid.NewGuid(),
        DateTime.UtcNow,
        orderId,
        customerId,
        tenantId,
        orderType,
        totalAmount,
        items,
        trackingCode,
        orderDate)
    {
    }
}

// Domain Event - OrderPaidEvent.cs
public record OrderPaidEvent(
    Guid EventId,
    DateTime EventDate,
    OrderId OrderId,
    CustomerId CustomerId,
    PaymentMethod PaymentMethod,
    Money Amount,
    string TransactionId,
    DateTime PaidAt
) : IDomainEvent
{
    public OrderPaidEvent(
        OrderId orderId,
        CustomerId customerId,
        PaymentMethod paymentMethod,
        Money amount,
        string transactionId,
        DateTime paidAt
    ) : this(
        Guid.NewGuid(),
        DateTime.UtcNow,
        orderId,
        customerId,
        paymentMethod,
        amount,
        transactionId,
        paidAt)
    {
    }
}

// Domain Event - KitchenItemStatusChangedEvent.cs
public record KitchenItemStatusChangedEvent(
    Guid EventId,
    DateTime EventDate,
    OrderItemId OrderItemId,
    OrderId OrderId,
    ProductId ProductId,
    KitchenStatus OldStatus,
    KitchenStatus NewStatus,
    DateTime ChangedAt
) : IDomainEvent
{
    public KitchenItemStatusChangedEvent(
        OrderItemId orderItemId,
        OrderId orderId,
        ProductId productId,
        KitchenStatus oldStatus,
        KitchenStatus newStatus,
        DateTime changedAt
    ) : this(
        Guid.NewGuid(),
        DateTime.UtcNow,
        orderItemId,
        orderId,
        productId,
        oldStatus,
        newStatus,
        changedAt)
    {
    }
}
```

---

### **2.4 Domain Services**

#### **Phase 1: Pricing Domain Service**
```csharp
// Domain Service - IPricingDomainService.cs
public interface IPricingDomainService
{
    Task<Money> CalculatePriceAsync(ProductId productId, Quantity quantity, CustomerId? customerId = null);
    Task<List<PricingRule>> GetApplicablePricingRulesAsync(ProductId productId, CustomerId? customerId = null);
    Task<Money> ApplyDiscountAsync(Money basePrice, List<DiscountRule> discountRules);
}

// Domain Service - PricingDomainService.cs
public class PricingDomainService : IPricingDomainService
{
    private readonly IProductRepository _productRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IPricingRuleRepository _pricingRuleRepository;

    public async Task<Money> CalculatePriceAsync(ProductId productId, Quantity quantity, CustomerId? customerId = null)
    {
        // Get product
        var product = await _productRepository.GetByIdAsync(productId);
        if (product == null)
            throw new NotFoundException("Product not found");

        // Get base price
        var basePrice = product.BasePrice;

        // Get applicable pricing rules
        var pricingRules = await GetApplicablePricingRulesAsync(productId, customerId);

        // Apply pricing rules
        var finalPrice = basePrice;
        foreach (var rule in pricingRules)
        {
            finalPrice = rule.Apply(finalPrice, quantity);
        }

        return finalPrice * quantity;
    }

    public async Task<List<PricingRule>> GetApplicablePricingRulesAsync(ProductId productId, CustomerId? customerId = null)
    {
        var rules = new List<PricingRule>();

        // Get product-specific rules
        var productRules = await _pricingRuleRepository.GetByProductIdAsync(productId);
        rules.AddRange(productRules);

        // Get customer-specific rules
        if (customerId.HasValue)
        {
            var customer = await _customerRepository.GetByIdAsync(customerId.Value);
            if (customer != null)
            {
                var customerRules = await _pricingRuleRepository.GetByCustomerTierAsync(customer.Tier);
                rules.AddRange(customerRules);
            }
        }

        // Get global rules
        var globalRules = await _pricingRuleRepository.GetGlobalRulesAsync();
        rules.AddRange(globalRules);

        // Sort by priority
        return rules.OrderByDescending(r => r.Priority).ToList();
    }

    public async Task<Money> ApplyDiscountAsync(Money basePrice, List<DiscountRule> discountRules)
    {
        var finalPrice = basePrice;

        foreach (var rule in discountRules)
        {
            if (rule.IsApplicable(finalPrice))
            {
                finalPrice = rule.Apply(finalPrice);
            }
        }

        return finalPrice;
    }
}
```

#### **Phase 2: Inventory Domain Service**
```csharp
// Domain Service - IInventoryDomainService.cs
public interface IInventoryDomainService
{
    Task<bool> CheckAvailabilityAsync(ProductId productId, Quantity quantity);
    Task<List<InventoryCheckResult>> CheckAvailabilityAsync(Dictionary<ProductId, Quantity> items);
    Task ReserveInventoryAsync(OrderId orderId, Dictionary<ProductId, Quantity> items);
    Task ReleaseInventoryAsync(OrderId orderId);
    Task ConsumeInventoryAsync(OrderId orderId);
}

// Domain Service - InventoryDomainService.cs
public class InventoryDomainService : IInventoryDomainService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IProductRepository _productRepository;

    public async Task<bool> CheckAvailabilityAsync(ProductId productId, Quantity quantity)
    {
        var inventory = await _inventoryRepository.GetByProductIdAsync(productId);
        if (inventory == null)
            return false;

        return inventory.AvailableQuantity.IsGreaterThanOrEqual(quantity);
    }

    public async Task<List<InventoryCheckResult>> CheckAvailabilityAsync(Dictionary<ProductId, Quantity> items)
    {
        var results = new List<InventoryCheckResult>();

        foreach (var item in items)
        {
            var isAvailable = await CheckAvailabilityAsync(item.Key, item.Value);
            results.Add(new InventoryCheckResult
            {
                ProductId = item.Key,
                RequiredQuantity = item.Value,
                IsAvailable = isAvailable,
                AvailableQuantity = isAvailable ? item.Value : Quantity.Zero
            });
        }

        return results;
    }

    public async Task ReserveInventoryAsync(OrderId orderId, Dictionary<ProductId, Quantity> items)
    {
        foreach (var item in items)
        {
            var inventory = await _inventoryRepository.GetByProductIdAsync(item.Key);
            if (inventory == null)
                throw new NotFoundException($"Inventory not found for product {item.Key}");

            if (!inventory.AvailableQuantity.IsGreaterThanOrEqual(item.Value))
                throw new BusinessRuleViolationException($"Insufficient inventory for product {item.Key}");

            inventory.ReserveQuantity(orderId, item.Value);
            await _inventoryRepository.UpdateAsync(inventory);
        }
    }

    public async Task ReleaseInventoryAsync(OrderId orderId)
    {
        var reservations = await _inventoryRepository.GetReservationsByOrderIdAsync(orderId);
        
        foreach (var reservation in reservations)
        {
            var inventory = await _inventoryRepository.GetByIdAsync(reservation.InventoryId);
            if (inventory != null)
            {
                inventory.ReleaseReservation(orderId, reservation.Quantity);
                await _inventoryRepository.UpdateAsync(inventory);
            }
        }
    }

    public async Task ConsumeInventoryAsync(OrderId orderId)
    {
        var reservations = await _inventoryRepository.GetReservationsByOrderIdAsync(orderId);
        
        foreach (var reservation in reservations)
        {
            var inventory = await _inventoryRepository.GetByIdAsync(reservation.InventoryId);
            if (inventory != null)
            {
                inventory.ConsumeReservation(orderId, reservation.Quantity);
                await _inventoryRepository.UpdateAsync(inventory);

                // Check if inventory is low
                if (inventory.AvailableQuantity.IsLessThan(inventory.MinStockThreshold))
                {
                    // Add domain event for low inventory
                    inventory.AddDomainEvent(new InventoryLowEvent(
                        productId: inventory.ProductId,
                        currentQuantity: inventory.AvailableQuantity,
                        minThreshold: inventory.MinStockThreshold,
                        lowAt: DateTime.UtcNow
                    ));
                }
            }
        }
    }
}
```

---

### **2.5 Application Services**

#### **Phase 1: Order Application Service**
```csharp
// Application Service - IOrderApplicationService.cs
public interface IOrderApplicationService
{
    Task<OrderDto> CreateOrderAsync(CreateOrderCommand command);
    Task<OrderDto> GetOrderAsync(OrderId orderId);
    Task<PagedResult<OrderDto>> GetOrdersAsync(OrderFilter filter, Pagination pagination);
    Task<OrderDto> UpdateOrderAsync(UpdateOrderCommand command);
    Task CancelOrderAsync(CancelOrderCommand command);
    Task<OrderDto> ConfirmOrderAsync(ConfirmOrderCommand command);
    Task<OrderDto> StartPaymentAsync(StartPaymentCommand command);
    Task<OrderDto> CompletePaymentAsync(CompletePaymentCommand command);
}

// Application Service - OrderApplicationService.cs
public class OrderApplicationService : IOrderApplicationService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IInventoryDomainService _inventoryService;
    private readonly IPricingDomainService _pricingService;
    private readonly IEventBus _eventBus;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantProvider _tenantProvider;
    private readonly IValidationService _validationService;
    private readonly ILogger<OrderApplicationService> _logger;

    public async Task<OrderDto> CreateOrderAsync(CreateOrderCommand command)
    {
        // Validate command
        var validationResult = await _validationService.ValidateAsync(command);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        // Get tenant
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Validate customer
        var customer = await _customerRepository.GetByIdAsync(command.CustomerId);
        if (customer == null || customer.TenantId != tenantId)
        {
            throw new NotFoundException("Customer not found");
        }

        // Validate products and pricing
        var pricingResults = new Dictionary<ProductId, Money>();
        foreach (var item in command.Items)
        {
            var price = await _pricingService.CalculatePriceAsync(
                item.ProductId, 
                item.Quantity, 
                command.CustomerId
            );
            pricingResults[item.ProductId] = price;
        }

        // Check inventory availability
        var inventoryCheck = await _inventoryService.CheckAvailabilityAsync(
            command.Items.ToDictionary(i => i.ProductId, i => i.Quantity)
        );

        var unavailableItems = inventoryCheck.Where(i => !i.IsAvailable).ToList();
        if (unavailableItems.Any())
        {
            throw new BusinessRuleViolationException(
                $"Insufficient inventory for products: {string.Join(", ", unavailableItems.Select(i => i.ProductId))}"
            );
        }

        // Create order items data
        var orderItemsData = command.Items.Select(item => new OrderItemData(
            productId: item.ProductId,
            quantity: item.Quantity,
            unitPrice: pricingResults[item.ProductId],
            vatRate: new Percentage(0.10m), // 10% VAT
            notes: item.Notes
        )).ToList();

        // Create order
        var order = Order.Create(
            customerId: command.CustomerId,
            tenantId: tenantId,
            orderType: command.OrderType,
            customerNotes: command.CustomerNotes,
            itemsData: orderItemsData,
            trackingCode: command.TrackingCode
        );

        // Reserve inventory
        await _inventoryService.ReserveInventoryAsync(
            order.Id,
            command.Items.ToDictionary(i => i.ProductId, i => i.Quantity)
        );

        // Save order
        await _orderRepository.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        // Publish domain events
        var domainEvents = order.GetUncommittedEvents();
        foreach (var domainEvent in domainEvents)
        {
            await _eventBus.PublishAsync(domainEvent);
        }

        order.MarkEventsAsCommitted();

        _logger.LogInformation("Order {OrderId} created for customer {CustomerId}", order.Id, customer.Id);

        return new OrderDto
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            TenantId = order.TenantId,
            OrderType = order.OrderType,
            Status = order.Status,
            SubTotal = order.SubTotal,
            VatAmount = order.VatAmount,
            DiscountAmount = order.DiscountAmount,
            TotalAmount = order.TotalAmount,
            OrderDate = order.OrderDate,
            Items = order.Items.Select(i => new OrderItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                SubTotal = i.SubTotal,
                VatAmount = i.VatAmount,
                TotalAmount = i.TotalAmount,
                KitchenStatus = i.KitchenStatus,
                Notes = i.Notes
            }).ToList()
        };
    }

    public async Task<OrderDto> GetOrderAsync(OrderId orderId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
        {
            throw new NotFoundException("Order not found");
        }

        return new OrderDto
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            TenantId = order.TenantId,
            OrderType = order.OrderType,
            Status = order.Status,
            SubTotal = order.SubTotal,
            VatAmount = order.VatAmount,
            DiscountAmount = order.DiscountAmount,
            TotalAmount = order.TotalAmount,
            OrderDate = order.OrderDate,
            ConfirmedAt = order.ConfirmedAt,
            PaidAt = order.PaidAt,
            CompletedAt = order.CompletedAt,
            CancelledAt = order.CancelledAt,
            Items = order.Items.Select(i => new OrderItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                SubTotal = i.SubTotal,
                VatAmount = i.VatAmount,
                TotalAmount = i.TotalAmount,
                KitchenStatus = i.KitchenStatus,
                Notes = i.Notes
            }).ToList()
        };
    }
}
```

---

### **2.6 Validation Framework**

#### **Phase 1: Command Validators**
```csharp
// Validator - CreateOrderCommandValidator.cs
public class CreateOrderCommandValidator : IValidator<CreateOrderCommand>
{
    public async Task<ValidationResult> ValidateAsync(CreateOrderCommand command)
    {
        var errors = new List<ValidationError>();

        // Validate customer
        if (command.CustomerId == Guid.Empty)
        {
            errors.Add(new ValidationError(nameof(CreateOrderCommand.CustomerId), "Customer ID is required"));
        }

        // Validate items
        if (command.Items == null || !command.Items.Any())
        {
            errors.Add(new ValidationError(nameof(CreateOrderCommand.Items), "Order must have at least one item"));
        }
        else
        {
            if (command.Items.Count > 50)
            {
                errors.Add(new ValidationError(nameof(CreateOrderCommand.Items), "Order cannot have more than 50 items"));
            }

            for (int i = 0; i < command.Items.Count; i++)
            {
                var item = command.Items[i];
                var prefix = $"{nameof(CreateOrderCommand.Items)}[{i}]";

                if (item.ProductId == Guid.Empty)
                {
                    errors.Add(new ValidationError($"{prefix}.{nameof(item.ProductId)}", "Product ID is required"));
                }

                if (item.Quantity <= 0)
                {
                    errors.Add(new ValidationError($"{prefix}.{nameof(item.Quantity)}", "Quantity must be greater than 0"));
                }

                if (item.Quantity > 100)
                {
                    errors.Add(new ValidationError($"{prefix}.{nameof(item.Quantity)}", "Quantity cannot exceed 100"));
                }

                if (item.UnitPrice <= 0)
                {
                    errors.Add(new ValidationError($"{prefix}.{nameof(item.UnitPrice)}", "Unit price must be greater than 0"));
                }

                if (!string.IsNullOrEmpty(item.Notes) && item.Notes.Length > 500)
                {
                    errors.Add(new ValidationError($"{prefix}.{nameof(item.Notes)}", "Notes cannot exceed 500 characters"));
                }
            }
        }

        // Validate customer notes
        if (!string.IsNullOrEmpty(command.CustomerNotes) && command.CustomerNotes.Length > 1000)
        {
            errors.Add(new ValidationError(nameof(CreateOrderCommand.CustomerNotes), "Customer notes cannot exceed 1000 characters"));
        }

        // Validate tracking code
        if (!string.IsNullOrEmpty(command.TrackingCode) && command.TrackingCode.Length > 50)
        {
            errors.Add(new ValidationError(nameof(CreateOrderCommand.TrackingCode), "Tracking code cannot exceed 50 characters"));
        }

        return errors.Any() 
            ? ValidationResult.Failed(errors) 
            : ValidationResult.Success();
    }
}
```

---

### **2.7 Localization Framework**

#### **Phase 1: Advanced Localization Service**
```csharp
// Service - ILocalizationService.cs
public interface ILocalizationService
{
    Task<string> TranslateAsync(string key, string language = "vi", params object[] args);
    Task<string> TranslateAsync(string key, CultureInfo culture, params object[] args);
    Task<Dictionary<string, string>> GetTranslationsAsync(string language);
    Task<bool> IsLanguageSupportedAsync(string language);
    Task<List<string>> GetSupportedLanguagesAsync();
    Task SetCultureAsync(string language);
    CultureInfo GetCurrentCulture();
}

// Service - DatabaseLocalizationService.cs
public class DatabaseLocalizationService : ILocalizationService
{
    private readonly ITranslationRepository _translationRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<DatabaseLocalizationService> _logger;
    private readonly AsyncLocal<CultureInfo> _currentCulture = new();

    public async Task<string> TranslateAsync(string key, string language = "vi", params object[] args)
    {
        var culture = new CultureInfo(language);
        return await TranslateAsync(key, culture, args);
    }

    public async Task<string> TranslateAsync(string key, CultureInfo culture, params object[] args)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        var cacheKey = $"translation_{culture.Name}_{key}";
        
        // Try cache first
        var cached = await _cacheService.GetAsync<string>(cacheKey);
        if (cached != null)
        {
            return FormatTranslation(cached, args);
        }

        // Get from database
        var translation = await _translationRepository.GetByKeyAndLanguageAsync(key, culture.Name);
        
        if (translation == null)
        {
            // Try fallback to default language
            if (culture.Name != "vi")
            {
                var fallback = await TranslateAsync(key, "vi", args);
                await _cacheService.SetAsync(cacheKey, fallback, TimeSpan.FromHours(1));
                return fallback;
            }

            // Return key if not found
            await _cacheService.SetAsync(cacheKey, key, TimeSpan.FromHours(1));
            return FormatTranslation(key, args);
        }

        await _cacheService.SetAsync(cacheKey, translation.Value, TimeSpan.FromHours(1));
        return FormatTranslation(translation.Value, args);
    }

    public async Task<Dictionary<string, string>> GetTranslationsAsync(string language)
    {
        var cacheKey = $"translations_{language}";
        
        var cached = await _cacheService.GetAsync<Dictionary<string, string>>(cacheKey);
        if (cached != null)
        {
            return cached;
        }

        var translations = await _translationRepository.GetByLanguageAsync(language);
        var translationDict = translations.ToDictionary(t => t.Key, t => t.Value);

        await _cacheService.SetAsync(cacheKey, translationDict, TimeSpan.FromHours(1));
        return translationDict;
    }

    public async Task<bool> IsLanguageSupportedAsync(string language)
    {
        var cacheKey = $"supported_languages";
        
        var supportedLanguages = await _cacheService.GetAsync<List<string>>(cacheKey);
        if (supportedLanguages == null)
        {
            supportedLanguages = await _translationRepository.GetSupportedLanguagesAsync();
            await _cacheService.SetAsync(cacheKey, supportedLanguages, TimeSpan.FromDays(1));
        }

        return supportedLanguages.Contains(language);
    }

    public async Task<List<string>> GetSupportedLanguagesAsync()
    {
        var cacheKey = $"supported_languages";
        
        var supportedLanguages = await _cacheService.GetAsync<List<string>>(cacheKey);
        if (supportedLanguages == null)
        {
            supportedLanguages = await _translationRepository.GetSupportedLanguagesAsync();
            await _cacheService.SetAsync(cacheKey, supportedLanguages, TimeSpan.FromDays(1));
        }

        return supportedLanguages;
    }

    public async Task SetCultureAsync(string language)
    {
        if (!await IsLanguageSupportedAsync(language))
        {
            throw new ArgumentException($"Language '{language}' is not supported");
        }

        _currentCulture.Value = new CultureInfo(language);
    }

    public CultureInfo GetCurrentCulture()
    {
        return _currentCulture.Value ?? new CultureInfo("vi");
    }

    private string FormatTranslation(string translation, object[] args)
    {
        if (args == null || args.Length == 0)
        {
            return translation;
        }

        try
        {
            return string.Format(translation, args);
        }
        catch (FormatException)
        {
            _logger.LogWarning("Translation formatting failed for: {Translation}", translation);
            return translation;
        }
    }
}
```

---

## **3. INFRASTRUCTURE LÝ T??NG**

### **3.1 Repository Pattern with Specifications**
```csharp
// Specification - OrderSpecification.cs
public class OrderSpecification : Specification<Order>
{
    public Specification<Order> ByCustomer(CustomerId customerId)
    {
        return order => order.CustomerId == customerId;
    }

    public Specification<Order> ByStatus(OrderStatus status)
    {
        return order => order.Status == status;
    }

    public Specification<Order> ByDateRange(DateTime startDate, DateTime endDate)
    {
        return order => order.OrderDate >= startDate && order.OrderDate <= endDate;
    }

    public Specification<Order> ByTenant(TenantId tenantId)
    {
        return order => order.TenantId == tenantId;
    }

    public Specification<Order> WithTrackingCode(string trackingCode)
    {
        return order => order.TrackingCode == trackingCode;
    }

    public Specification<Order> Active()
    {
        return order => !order.IsDeleted;
    }
}

// Repository - OrderRepository.cs
public class OrderRepository : IOrderRepository
{
    private readonly VanAnDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly ILogger<OrderRepository> _logger;

    public async Task<Order?> GetByIdAsync(OrderId id)
    {
        var cacheKey = $"order_{id}";
        
        var cached = await _cacheService.GetAsync<Order>(cacheKey);
        if (cached != null)
            return cached;

        var order = await _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);

        if (order != null)
        {
            await _cacheService.SetAsync(cacheKey, order, TimeSpan.FromMinutes(5));
        }

        return order;
    }

    public async Task<PagedResult<Order>> GetPagedAsync(Specification<Order> specification, Pagination pagination)
    {
        var query = ApplySpecification(_context.Orders, specification);

        var totalCount = await query.CountAsync();
        
        var orders = await query
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Include(o => o.Customer)
            .OrderByDescending(o => o.OrderDate)
            .Skip(pagination.Page * pagination.Size)
            .Take(pagination.Size)
            .AsNoTracking()
            .ToListAsync();

        return new PagedResult<Order>(orders, totalCount, pagination.Page, pagination.Size);
    }

    private IQueryable<Order> ApplySpecification(IQueryable<Order> query, Specification<Order> specification)
    {
        return specification.Apply(query);
    }
}
```

---

## **4. CROSS-CUTTING CONCERNS**

### **4.1 Caching Strategy**
```csharp
// Service - RedisCacheService.cs
public class RedisCacheService : ICacheService
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisCacheService> _logger;

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var value = await _database.StringGetAsync(key);
            if (value.HasValue)
            {
                return JsonSerializer.Deserialize<T>(value);
            }
            return default;
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

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _database.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache value for key {Key}", key);
        }
    }

    public async Task RemoveByPatternAsync(string pattern)
    {
        try
        {
            var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints().First());
            var keys = server.Keys(database: _database.Database, pattern: pattern);
            
            foreach (var key in keys)
            {
                await _database.KeyDeleteAsync(key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache values by pattern {Pattern}", pattern);
        }
    }
}
```

### **4.2 Event Bus Implementation**
```csharp
// Service - InMemoryEventBus.cs
public class InMemoryEventBus : IEventBus
{
    private readonly List<IDomainEvent> _publishedEvents = new();
    private readonly Dictionary<Type, List<object>> _handlers = new();
    private readonly ILogger<InMemoryEventBus> _logger;

    public async Task PublishAsync<T>(T domainEvent) where T : IDomainEvent
    {
        _publishedEvents.Add(domainEvent);

        var eventType = typeof(T);
        if (_handlers.ContainsKey(eventType))
        {
            var handlers = _handlers[eventType];
            foreach (var handler in handlers)
            {
                try
                {
                    if (handler is IEventHandler<T> typedHandler)
                    {
                        await typedHandler.Handle(domainEvent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling event {EventType}", eventType.Name);
                }
            }
        }
    }

    public async Task PublishAsync(IEnumerable<IDomainEvent> domainEvents)
    {
        foreach (var domainEvent in domainEvents)
        {
            await PublishAsync(domainEvent);
        }
    }

    public async Task SubscribeAsync<T>(IEventHandler<T> handler) where T : IDomainEvent
    {
        var eventType = typeof(T);
        
        if (!_handlers.ContainsKey(eventType))
        {
            _handlers[eventType] = new List<object>();
        }

        _handlers[eventType].Add(handler);
        
        _logger.LogInformation("Subscribed handler for event {EventType}", eventType.Name);
    }

    public async Task UnsubscribeAsync<T>(IEventHandler<T> handler) where T : IDomainEvent
    {
        var eventType = typeof(T);
        
        if (_handlers.ContainsKey(eventType))
        {
            _handlers[eventType].Remove(handler);
        }
    }

    public IReadOnlyList<IDomainEvent> GetPublishedEvents()
    {
        return _publishedEvents.AsReadOnly();
    }

    public void Clear()
    {
        _publishedEvents.Clear();
    }
}
```

---

## **5. TESTING STRATEGY**

### **5.1 Domain Tests**
```csharp
// OrderTests.cs
public class OrderTests
{
    [Fact]
    public void CreateOrder_WithValidData_ShouldCreateOrder()
    {
        // Arrange
        var customerId = new CustomerId(Guid.NewGuid());
        var tenantId = new TenantId(Guid.NewGuid());
        var itemsData = new List<OrderItemData>
        {
            new OrderItemData(
                productId: new ProductId(Guid.NewGuid()),
                quantity: new Quantity(2),
                unitPrice: new Money(50000),
                vatRate: new Percentage(0.10m),
                notes: "No ice"
            )
        };

        // Act
        var order = Order.Create(
            customerId: customerId,
            tenantId: tenantId,
            orderType: OrderType.DineIn,
            customerNotes: "Table 5",
            itemsData: itemsData
        );

        // Assert
        order.Should().NotBeNull();
        order.CustomerId.Should().Be(customerId);
        order.TenantId.Should().Be(tenantId);
        order.Status.Should().Be(OrderStatus.Draft);
        order.Items.Should().HaveCount(1);
        order.TotalAmount.Value.Should().Be(110000); // 50000 * 2 * 1.1
        order.GetUncommittedEvents().Should().ContainSingle(e => e is OrderCreatedEvent);
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

    private Order CreateTestOrder()
    {
        var customerId = new CustomerId(Guid.NewGuid());
        var tenantId = new TenantId(Guid.NewGuid());
        var itemsData = new List<OrderItemData>
        {
            new OrderItemData(
                productId: new ProductId(Guid.NewGuid()),
                quantity: new Quantity(2),
                unitPrice: new Money(50000),
                vatRate: new Percentage(0.10m),
                notes: "No ice"
            )
        };

        return Order.Create(
            customerId: customerId,
            tenantId: tenantId,
            orderType: OrderType.DineIn,
            customerNotes: "Table 5",
            itemsData: itemsData
        );
    }
}
```

### **5.2 Value Object Tests**
```csharp
// MoneyTests.cs
public class MoneyTests
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
    public void CreateMoney_WithNegativeValue_ShouldThrowException(decimal value)
    {
        // Act & Assert
        Action act = () => new Money(value);

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("Money cannot be negative");
    }

    [Fact]
    public void AddMoney_WithValidValues_ShouldReturnCorrectSum()
    {
        // Arrange
        var money1 = new Money(100);
        var money2 = new Money(50);

        // Act
        var result = money1 + money2;

        // Assert
        result.Value.Should().Be(150);
    }

    [Fact]
    public void SubtractMoney_WithValidValues_ShouldReturnCorrectDifference()
    {
        // Arrange
        var money1 = new Money(100);
        var money2 = new Money(30);

        // Act
        var result = money1 - money2;

        // Assert
        result.Value.Should().Be(70);
    }

    [Fact]
    public void SubtractMoney_WithInsufficientFunds_ShouldThrowException()
    {
        // Arrange
        var money1 = new Money(100);
        var money2 = new Money(150);

        // Act & Assert
        Action act = () => money1 - money2;

        act.Should().Throw<BusinessRuleViolationException>()
            .WithMessage("Resulting money cannot be negative");
    }
}
```

---

## **6. SUMMARY**

### **6.1 Key Features of Ideal Shared Module**
- **Rich Domain Models:** Entities with business behavior
- **Value Objects:** Immutable types with business logic
- **Domain Events:** Event-driven architecture
- **Domain Services:** Complex business logic
- **Specifications:** Flexible query patterns
- **Application Services:** Use case orchestration
- **Validation Framework:** Comprehensive validation
- **Localization:** Multi-language support
- **Caching:** Performance optimization
- **Event Bus:** Asynchronous messaging

### **6.2 Technical Excellence**
- **DDD Principles:** Proper domain modeling
- **Clean Architecture:** Clear separation of concerns
- **SOLID Principles:** Single responsibility, etc.
- **Testability:** Comprehensive test coverage
- **Performance:** Caching and optimization
- **Security:** Proper validation and error handling
- **Scalability:** Event-driven architecture
- **Maintainability:** Clean, testable code

### **6.3 Business Value**
- **Type Safety:** Strong typing prevents errors
- **Business Logic:** Encapsulated in domain
- **Flexibility:** Easy to extend and modify
- **Reliability:** Comprehensive validation
- **Performance:** Optimized for scale
- **Internationalization:** Multi-language support
- **Auditability:** Complete event tracking

This ideal Shared module provides a robust foundation for the entire application with proper domain modeling, comprehensive business logic, and excellent testability while maintaining clean architecture principles.
