# CoreHub - Lu?ng X? Lý T??ng (Ideal Architecture)

**Ngày:** 11 tháng 4, 2026  
**Module:** 3_CoreHub  
**Tr?ng thái:** Thi?t k? lu?ng x? lý lý t??ng ho?n ch?nh

---

## **1. T?NG QUAN ARCHITECTURE LÝ T??NG**

### **1.1 Clean Architecture Pattern**
```
3_CoreHub/
?? Domain/                    (Domain Layer)
   ?? Entities/
      ?? Order.cs
      ?? Customer.cs
      ?? Product.cs
      ?? KitchenItem.cs
      ?? [và 20+ entities]
   ?? Value Objects/
      ?? OrderId.cs
      ?? CustomerId.cs
      ?? ProductId.cs
      ?? [và 15+ value objects]
   ?? Events/
      ?? OrderCreatedEvent.cs
      ?? KitchenItemStatusChangedEvent.cs
      ?? [và 10+ domain events]
   ?? Services/
      ?? IDomainService.cs
      ?? IOrderDomainService.cs
      ?? [và 5+ domain services]
   ?? Repositories/
      ?? IOrderRepository.cs
      ?? ICustomerRepository.cs
      ?? [và 10+ repository interfaces]
   ?? Specifications/
      ?? OrderSpecification.cs
      ?? CustomerSpecification.cs
      ?? [và 5+ specifications]
?? Application/              (Application Layer)
   ?? Commands/
      ?? CreateOrderCommand.cs
      ?? UpdateKitchenItemStatusCommand.cs
      ?? [và 20+ commands]
   ?? Queries/
      ?? GetOrderQuery.cs
      ?? GetKitchenItemsQuery.cs
      ?? [và 15+ queries]
   ?? Handlers/
      ?? CreateOrderCommandHandler.cs
      ?? GetOrderQueryHandler.cs
      ?? [và 35+ handlers]
   ?? DTOs/
      ?? OrderDto.cs
      ?? KitchenItemDto.cs
      ?? [và 25+ DTOs]
   ?? Validators/
      ?? CreateOrderCommandValidator.cs
      ?? [và 15+ validators]
   ?? Services/
      ?? IOrderApplicationService.cs
      ?? IKitchenApplicationService.cs
      ?? [và 10+ application services]
?? Infrastructure/            (Infrastructure Layer)
   ?? Persistence/
      ?? Repositories/
         ?? OrderRepository.cs
         ?? CustomerRepository.cs
         ?? [và 10+ repository implementations]
      ?? Configurations/
         ?? OrderConfiguration.cs
         ?? CustomerConfiguration.cs
         ?? [và 15+ EF configurations]
      ?? ValueConverters/
         ?? OrderIdConverter.cs
         ?? [và 15+ value converters]
   ?? External/
      ?? Services/
         ?? IVietQrExternalService.cs
         ?? ISpeechRecognitionService.cs
         ?? [và 10+ external services]
   ?? Caching/
      ?? ICacheService.cs
      ?? RedisCacheService.cs
      ?? [và 5+ caching services]
   ?? Messaging/
      ?? IEventBus.cs
      ?? RabbitMqEventBus.cs
      ?? [và 5+ messaging services]
   ?? Logging/
      ?? ICoreLogger.cs
      ?? StructuredLogger.cs
      ?? [và 5+ logging services]
?? CrossCutting/             (Cross-cutting Concerns)
   ?? Security/
      ?? ITenantProvider.cs
      ?? IUserContext.cs
      ?? [và 5+ security services]
   ?? Validation/
      ?? IValidationService.cs
      ?? FluentValidationService.cs
      ?? [và 5+ validation services]
   ?? Monitoring/
      ?? IMetricsService.cs
      ?? PrometheusMetricsService.cs
      ?? [và 5+ monitoring services]
   ?? ErrorHandling/
      ?? IErrorHandler.cs
      ?? GlobalErrorHandler.cs
      ?? [và 5+ error handling services]
```

---

## **2. LU?NG X? LÝ LÝ T??NG**

### **2.1 Order Domain Flow (Complete Implementation)**

#### **Phase 1: Order Creation with Domain Events**
```csharp
// Domain Entity - Order.cs
public class Order : AggregateRoot<OrderId>
{
    public CustomerId CustomerId { get; private set; }
    public TenantId TenantId { get; private set; }
    public OrderType OrderType { get; private set; }
    public OrderStatus Status { get; private set; }
    public Money TotalAmount { get; private set; }
    public DateTime OrderDate { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string CustomerNotes { get; private set; }
    private readonly List<OrderItem> _items = new();
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    // Factory Method
    public static Order Create(
        CustomerId customerId,
        TenantId tenantId,
        OrderType orderType,
        string customerNotes,
        List<OrderItemData> itemsData)
    {
        // Business Rules
        if (itemsData == null || !itemsData.Any())
            throw new DomainException("Order must have at least one item");

        if (itemsData.Count > 50)
            throw new DomainException("Order cannot have more than 50 items");

        var order = new Order
        {
            Id = new OrderId(Guid.NewGuid()),
            CustomerId = customerId,
            TenantId = tenantId,
            OrderType = orderType,
            Status = OrderStatus.Draft,
            OrderDate = DateTime.UtcNow,
            CustomerNotes = customerNotes?.Trim() ?? string.Empty
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
            totalAmount: order.TotalAmount,
            items: order.Items.Select(i => new OrderItemEvent(
                productId: i.ProductId,
                quantity: i.Quantity,
                unitPrice: i.UnitPrice
            ))
        ));

        return order;
    }

    // Business Methods
    public void Confirm()
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException($"Cannot confirm order in {Status} status");

        Status = OrderStatus.Confirmed;
        AddDomainEvent(new OrderConfirmedEvent(Id, CustomerId, TenantId));
    }

    public void StartPayment()
    {
        if (Status != OrderStatus.Confirmed)
            throw new DomainException($"Cannot start payment for order in {Status} status");

        Status = OrderStatus.PendingPayment;
        AddDomainEvent(new OrderPaymentStartedEvent(Id, CustomerId, TotalAmount));
    }

    public void CompletePayment(PaymentId paymentId)
    {
        if (Status != OrderStatus.PendingPayment)
            throw new DomainException($"Cannot complete payment for order in {Status} status");

        Status = OrderStatus.Paid;
        AddDomainEvent(new OrderPaymentCompletedEvent(Id, CustomerId, paymentId, TotalAmount));
    }

    public void StartPreparation()
    {
        if (Status != OrderStatus.Paid)
            throw new DomainException($"Cannot start preparation for order in {Status} status");

        Status = OrderStatus.Preparing;
        AddDomainEvent(new OrderPreparationStartedEvent(Id, CustomerId));
    }

    public void Complete()
    {
        if (Status != OrderStatus.Preparing)
            throw new DomainException($"Cannot complete order in {Status} status");

        Status = OrderStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        AddDomainEvent(new OrderCompletedEvent(Id, CustomerId, CompletedAt.Value));
    }

    private void CalculateTotals()
    {
        var subtotal = _items.Sum(i => i.TotalAmount);
        var totalVat = _items.Sum(i => i.VatAmount);
        TotalAmount = new Money(subtotal + totalVat);
    }
}
```

#### **Phase 2: Order Item Domain**
```csharp
// Domain Entity - OrderItem.cs
public class OrderItem : Entity<OrderItemId>
{
    public OrderId OrderId { get; private set; }
    public ProductId ProductId { get; private set; }
    public Quantity Quantity { get; private set; }
    public Money UnitPrice { get; private set; }
    public Percentage VatRate { get; private set; }
    public Money TotalAmount { get; private set; }
    public Money VatAmount { get; private set; }
    public KitchenStatus KitchenStatus { get; private set; }
    public string Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

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
            throw new DomainException("Quantity must be greater than 0");

        if (quantity.Value > 100)
            throw new DomainException("Quantity cannot exceed 100");

        if (unitPrice.Value <= 0)
            throw new DomainException("Unit price must be greater than 0");

        var orderItem = new OrderItem
        {
            Id = new OrderItemId(Guid.NewGuid()),
            OrderId = orderId,
            ProductId = productId,
            Quantity = quantity,
            UnitPrice = unitPrice,
            VatRate = vatRate,
            Notes = notes?.Trim() ?? string.Empty,
            KitchenStatus = KitchenStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        orderItem.CalculateTotals();

        return orderItem;
    }

    // Business Methods
    public void UpdateKitchenStatus(KitchenStatus newStatus, Guid updatedBy)
    {
        if (!IsValidStatusTransition(KitchenStatus, newStatus))
            throw new DomainException($"Invalid status transition from {KitchenStatus} to {newStatus}");

        var oldStatus = KitchenStatus;
        KitchenStatus = newStatus;
        UpdatedAt = DateTime.UtcNow;

        // Add Domain Event
        AddDomainEvent(new KitchenItemStatusChangedEvent(
            orderItemId: Id,
            orderId: OrderId,
            productId: ProductId,
            oldStatus: oldStatus,
            newStatus: newStatus,
            updatedBy: updatedBy,
            updatedAt: UpdatedAt.Value
        ));
    }

    public void AttachVoiceNote(VoiceNote voiceNote)
    {
        if (voiceNote == null)
            throw new DomainException("Voice note cannot be null");

        AddDomainEvent(new VoiceNoteAttachedEvent(
            orderItemId: Id,
            orderId: OrderId,
            voiceNoteId: voiceNote.Id,
            attachedAt: DateTime.UtcNow
        ));
    }

    private void CalculateTotals()
    {
        var subtotal = Quantity.Value * UnitPrice.Value;
        VatAmount = new Money(subtotal * (VatRate.Value / 100));
        TotalAmount = new Money(subtotal + VatAmount.Value);
    }

    private static bool IsValidStatusTransition(KitchenStatus from, KitchenStatus to)
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

#### **Phase 3: Application Service**
```csharp
// Application Service - OrderApplicationService.cs
public class OrderApplicationService : IOrderApplicationService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IInventoryService _inventoryService;
    private readonly IPricingService _pricingService;
    private readonly IEventBus _eventBus;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<OrderApplicationService> _logger;

    public async Task<OrderDto> CreateOrderAsync(CreateOrderCommand command)
    {
        // Validate Tenant
        var tenantId = _tenantProvider.GetCurrentTenantId();
        
        // Validate Customer
        var customer = await _customerRepository.GetByIdAsync(command.CustomerId);
        if (customer == null || customer.TenantId != tenantId)
            throw new NotFoundException("Customer not found");

        // Validate Products and Inventory
        var productIds = command.Items.Select(i => i.ProductId).ToList();
        var products = await _productRepository.GetByIdsAsync(productIds);
        
        if (products.Count != productIds.Count)
            throw new DomainException("One or more products not found");

        // Check Inventory
        var inventoryChecks = await _inventoryService.CheckInventoryAsync(
            command.Items.Select(i => new InventoryCheck(
                productId: i.ProductId,
                quantity: i.Quantity
            ))
        );

        if (inventoryChecks.Any(c => !c.IsAvailable))
        {
            var unavailableProducts = inventoryChecks
                .Where(c => !c.IsAvailable)
                .Select(c => c.ProductId);
            throw new DomainException($"Insufficient inventory for products: {string.Join(", ", unavailableProducts)}");
        }

        // Get Pricing
        var pricingRequests = command.Items.Select(i => new PricingRequest(
            productId: i.ProductId,
            quantity: i.Quantity,
            customerId: command.CustomerId,
            tenantId: tenantId
        ));

        var pricingResults = await _pricingService.GetPricingAsync(pricingRequests);

        // Create Order Items Data
        var orderItemsData = command.Items.Select(item => new OrderItemData(
            productId: item.ProductId,
            quantity: item.Quantity,
            unitPrice: pricingResults.First(p => p.ProductId == item.ProductId).UnitPrice,
            vatRate: pricingResults.First(p => p.ProductId == item.ProductId).VatRate,
            notes: item.Notes
        )).ToList();

        // Create Order
        var order = Order.Create(
            customerId: command.CustomerId,
            tenantId: tenantId,
            orderType: command.OrderType,
            customerNotes: command.CustomerNotes,
            itemsData: orderItemsData
        );

        // Reserve Inventory
        await _inventoryService.ReserveInventoryAsync(
            order.Items.Select(i => new InventoryReservation(
                productId: i.ProductId,
                quantity: i.Quantity,
                orderId: order.Id,
                expiresAt: DateTime.UtcNow.AddMinutes(15)
            ))
        );

        // Save Order
        await _orderRepository.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        // Publish Domain Events
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
            TotalAmount = order.TotalAmount,
            OrderDate = order.OrderDate,
            Items = order.Items.Select(i => new OrderItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalAmount = i.TotalAmount,
                KitchenStatus = i.KitchenStatus,
                Notes = i.Notes
            }).ToList()
        };
    }
}
```

---

### **2.2 Kitchen Domain Flow (Real-time Kitchen Operations)**

#### **Phase 1: Kitchen Service Domain**
```csharp
// Domain Service - KitchenDomainService.cs
public class KitchenDomainService : IKitchenDomainService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStationRepository _stationRepository;

    public async Task<List<KitchenItemGroup>> GetGroupedKitchenItemsAsync(ShopId shopId)
    {
        // Get all pending and preparing items
        var orderItems = await _orderRepository.GetKitchenItemsAsync(shopId);
        
        // Group by Product with FIFO ordering
        var groupedItems = orderItems
            .GroupBy(item => new { item.ProductId, item.ProductName })
            .Select(group => KitchenItemGroup.Create(
                productId: group.Key.ProductId,
                productName: group.Key.ProductName,
                items: group.OrderBy(i => i.OrderCreatedAt).ToList()
            ))
            .OrderBy(group => group.GetOldestOrderTime())
            .ToList();

        return groupedItems;
    }

    public async Task<KitchenPreparationPlan> GeneratePreparationPlanAsync(ShopId shopId)
    {
        var groupedItems = await GetGroupedKitchenItemsAsync(shopId);
        var stations = await _stationRepository.GetByShopIdAsync(shopId);

        var plan = KitchenPreparationPlan.Create(
            shopId: shopId,
            groups: groupedItems,
            stations: stations
        );

        return plan;
    }
}

// Domain Entity - KitchenItemGroup.cs
public class KitchenItemGroup : ValueObject
{
    public ProductId ProductId { get; }
    public string ProductName { get; }
    public Quantity TotalQuantity { get; }
    public KitchenStatus GroupStatus { get; }
    public DateTime OldestOrderTime { get; }
    public TimeSpan EstimatedPreparationTime { get; }
    public IReadOnlyCollection<KitchenOrderItem> Items { get; }

    public static KitchenItemGroup Create(
        ProductId productId,
        string productName,
        List<KitchenOrderItem> items)
    {
        if (items == null || !items.Any())
            throw new DomainException("Kitchen item group must have at least one item");

        var totalQuantity = new Quantity(items.Sum(i => i.Quantity.Value));
        var groupStatus = items.All(i => i.Status == KitchenStatus.Pending) 
            ? KitchenStatus.Pending 
            : KitchenStatus.Preparing;

        var oldestOrderTime = items.Min(i => i.OrderCreatedAt);
        var estimatedPreparationTime = CalculatePreparationTime(items);

        return new KitchenItemGroup(
            productId: productId,
            productName: productName,
            totalQuantity: totalQuantity,
            groupStatus: groupStatus,
            oldestOrderTime: oldestOrderTime,
            estimatedPreparationTime: estimatedPreparationTime,
            items: items.AsReadOnly()
        );
    }

    private static TimeSpan CalculatePreparationTime(List<KitchenOrderItem> items)
    {
        // Complex preparation time calculation based on:
        // - Product complexity
        // - Quantity
        // - Station availability
        // - Concurrent preparation capability
        
        var baseTime = TimeSpan.FromMinutes(5); // Base preparation time
        var quantityMultiplier = Math.Max(1, items.Sum(i => i.Quantity.Value) / 10.0);
        var complexityMultiplier = items.Any(i => i.IsComplex) ? 1.5 : 1.0;
        
        return TimeSpan.FromMinutes(baseTime.TotalMinutes * quantityMultiplier * complexityMultiplier);
    }
}
```

#### **Phase 2: Voice Processing Domain**
```csharp
// Domain Entity - VoiceNote.cs
public class VoiceNote : Entity<VoiceNoteId>
{
    public OrderId OrderId { get; private set; }
    public OrderItemId? OrderItemId { get; private set; }
    public string Text { get; private set; }
    public byte[] AudioBlob { get; private set; }
    public VoiceNoteStatus Status { get; private set; }
    public Language Language { get; private set; }
    public DateTime RecordedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public string TranscriptionError { get; private set; }

    public static VoiceNote Create(
        OrderId orderId,
        OrderItemId? orderItemId,
        byte[] audioBlob,
        Language language = Language.Vietnamese)
    {
        // Validation
        if (audioBlob == null || audioBlob.Length == 0)
            throw new DomainException("Audio blob cannot be empty");

        if (audioBlob.Length > 5 * 1024 * 1024) // 5MB limit
            throw new DomainException("Audio blob too large");

        var voiceNote = new VoiceNote
        {
            Id = new VoiceNoteId(Guid.NewGuid()),
            OrderId = orderId,
            OrderItemId = orderItemId,
            AudioBlob = audioBlob,
            Language = language,
            Status = VoiceNoteStatus.Pending,
            RecordedAt = DateTime.UtcNow
        };

        voiceNote.AddDomainEvent(new VoiceNoteCreatedEvent(
            voiceNoteId: voiceNote.Id,
            orderId: orderId,
            orderItemId: orderItemId,
            language: language,
            recordedAt: voiceNote.RecordedAt
        ));

        return voiceNote;
    }

    public void ProcessTranscription(string transcribedText, bool isSuccess, string error = null)
    {
        if (Status != VoiceNoteStatus.Pending)
            throw new DomainException($"Cannot process transcription for voice note in {Status} status");

        if (isSuccess)
        {
            Text = transcribedText?.Trim() ?? string.Empty;
            Status = VoiceNoteStatus.Processed;
            ProcessedAt = DateTime.UtcNow;

            AddDomainEvent(new VoiceNoteTranscriptionCompletedEvent(
                voiceNoteId: Id,
                orderId: OrderId,
                transcribedText: Text,
                processedAt: ProcessedAt.Value
            ));
        }
        else
        {
            Status = VoiceNoteStatus.Failed;
            TranscriptionError = error;
            ProcessedAt = DateTime.UtcNow;

            AddDomainEvent(new VoiceNoteTranscriptionFailedEvent(
                voiceNoteId: Id,
                orderId: OrderId,
                error: error,
                processedAt: ProcessedAt.Value
            ));
        }
    }

    public void ProcessCommand(VoiceCommand command)
    {
        if (Status != VoiceNoteStatus.Processed)
            throw new DomainException($"Cannot process command for voice note in {Status} status");

        if (command == null)
            throw new DomainException("Command cannot be null");

        AddDomainEvent(new VoiceCommandProcessedEvent(
            voiceNoteId: Id,
            orderId: OrderId,
            command: command,
            processedAt: DateTime.UtcNow
        ));
    }
}

// Domain Entity - VoiceCommand.cs
public class VoiceCommand : ValueObject
{
    public VoiceCommandIntent Intent { get; }
    public Dictionary<string, object> Entities { get; }
    public double Confidence { get; }
    public string OriginalText { get; }

    public static VoiceCommand Parse(string text, double confidence)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("Command text cannot be empty");

        if (confidence < 0.5)
            throw new DomainException("Command confidence too low");

        var intent = ExtractIntent(text);
        var entities = ExtractEntities(text, intent);

        return new VoiceCommand(
            intent: intent,
            entities: entities,
            confidence: confidence,
            originalText: text
        );
    }

    private static VoiceCommandIntent ExtractIntent(string text)
    {
        var normalizedText = text.ToLowerInvariant();

        return normalizedText switch
        {
            var t when t.Contains("b?t") || t.Contains("bep") => VoiceCommandIntent.StartPreparation,
            var t when t.Contains("hoàn thành") || t.Contains("xong") => VoiceCommandIntent.CompleteItem,
            var t when t.Contains("h?y") || t.Contains("cancel") => VoiceCommandIntent.CancelItem,
            var t when t.Contains("ghi chú") || t.Contains("note") => VoiceCommandIntent.AddNote,
            _ => VoiceCommandIntent.Unknown
        };
    }

    private static Dictionary<string, object> ExtractEntities(string text, VoiceCommandIntent intent)
    {
        var entities = new Dictionary<string, object>();

        switch (intent)
        {
            case VoiceCommandIntent.StartPreparation:
            case VoiceCommandIntent.CompleteItem:
            case VoiceCommandIntent.CancelItem:
                // Extract product name
                var productMatch = Regex.Match(text, @"(\w+)\s+(?:cho|c?a)");
                if (productMatch.Success)
                {
                    entities["ProductName"] = productMatch.Groups[1].Value;
                }
                break;

            case VoiceCommandIntent.AddNote:
                // Extract note text
                var noteMatch = Regex.Match(text, @"ghi chú\s+(.+)");
                if (noteMatch.Success)
                {
                    entities["Note"] = noteMatch.Groups[1].Value;
                }
                break;
        }

        return entities;
    }
}
```

---

### **2.3 Customer Domain Flow (Customer Management)**

#### **Phase 1: Customer Domain**
```csharp
// Domain Entity - Customer.cs
public class Customer : AggregateRoot<CustomerId>
{
    public string DeviceId { get; private set; }
    public string DisplayName { get; private set; }
    public string Email { get; private set; }
    public string PhoneNumber { get; private set; }
    public CustomerStatus Status { get; private set; }
    public CustomerTier Tier { get; private set; }
    public Money TotalSpent { get; private set; }
    public int OrderCount { get; private set; }
    public DateTime? LastOrderAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public TenantId TenantId { get; private set; }

    // Factory Methods
    public static Customer Create(
        string deviceId,
        string displayName,
        TenantId tenantId)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new DomainException("Device ID is required");

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = $"Customer_{deviceId.Substring(0, 8)}";

        var customer = new Customer
        {
            Id = new CustomerId(Guid.NewGuid()),
            DeviceId = deviceId,
            DisplayName = displayName,
            Status = CustomerStatus.Active,
            Tier = CustomerTier.Regular,
            TotalSpent = Money.Zero,
            OrderCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TenantId = tenantId
        };

        customer.AddDomainEvent(new CustomerCreatedEvent(
            customerId: customer.Id,
            deviceId: deviceId,
            displayName: displayName,
            tenantId: tenantId,
            createdAt: customer.CreatedAt
        ));

        return customer;
    }

    public static Customer Register(
        string deviceId,
        string displayName,
        string email,
        string phoneNumber,
        TenantId tenantId)
    {
        var customer = Create(deviceId, displayName, tenantId);

        customer.UpdateContactInfo(email, phoneNumber);

        customer.Status = CustomerStatus.Registered;
        customer.AddDomainEvent(new CustomerRegisteredEvent(
            customerId: customer.Id,
            deviceId: deviceId,
            email: email,
            phoneNumber: phoneNumber,
            tenantId: tenantId,
            registeredAt: DateTime.UtcNow
        ));

        return customer;
    }

    // Business Methods
    public void UpdateContactInfo(string email, string phoneNumber)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            if (!IsValidEmail(email))
                throw new DomainException("Invalid email format");
            Email = email.ToLowerInvariant().Trim();
        }

        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            if (!IsValidPhoneNumber(phoneNumber))
                throw new DomainException("Invalid phone number format");
            PhoneNumber = phoneNumber;
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new DomainException("Display name is required");

        if (displayName.Length > 100)
            throw new DomainException("Display name too long");

        var oldDisplayName = DisplayName;
        DisplayName = displayName.Trim();
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new CustomerDisplayNameUpdatedEvent(
            customerId: Id,
            oldDisplayName: oldDisplayName,
            newDisplayName: DisplayName,
            updatedAt: UpdatedAt.Value
        ));
    }

    public void RecordOrder(Money orderAmount)
    {
        if (orderAmount.Value <= 0)
            throw new DomainException("Order amount must be positive");

        var oldTier = Tier;
        
        TotalSpent = new Money(TotalSpent.Value + orderAmount.Value);
        OrderCount++;
        LastOrderAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        // Update tier based on total spent
        Tier = CalculateTier(TotalSpent);

        if (Tier != oldTier)
        {
            AddDomainEvent(new CustomerTierUpdatedEvent(
                customerId: Id,
                oldTier: oldTier,
                newTier: Tier,
                totalSpent: TotalSpent,
                orderCount: OrderCount,
                updatedAt: UpdatedAt.Value
            ));
        }

        AddDomainEvent(new CustomerOrderRecordedEvent(
            customerId: Id,
            orderAmount: orderAmount,
            totalSpent: TotalSpent,
            orderCount: OrderCount,
            orderDate: LastOrderAt.Value
        ));
    }

    public void Deactivate()
    {
        if (Status == CustomerStatus.Inactive)
            throw new DomainException("Customer is already inactive");

        Status = CustomerStatus.Inactive;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new CustomerDeactivatedEvent(
            customerId: Id,
            deactivatedAt: UpdatedAt.Value
        ));
    }

    public void Reactivate()
    {
        if (Status == CustomerStatus.Active)
            throw new DomainException("Customer is already active");

        Status = CustomerStatus.Active;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new CustomerReactivatedEvent(
            customerId: Id,
            reactivatedAt: UpdatedAt.Value
        ));
    }

    private static CustomerTier CalculateTier(Money totalSpent)
    {
        return totalSpent.Value switch
        {
            >= 10000000 => CustomerTier.VIP,      // 10M+ VND
            >= 5000000 => CustomerTier.Gold,     // 5M+ VND
            >= 1000000 => CustomerTier.Silver,   // 1M+ VND
            _ => CustomerTier.Regular
        };
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var mailAddress = new System.Net.Mail.MailAddress(email);
            return mailAddress.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidPhoneNumber(string phoneNumber)
    {
        // Vietnamese phone number validation
        return Regex.IsMatch(phoneNumber, @"^(0|\+84)[3-9][0-9]{8,9}$");
    }
}
```

---

### **2.4 Data Versioning & Sync Flow**

#### **Phase 1: Data Versioning Domain**
```csharp
// Domain Entity - DataVersion.cs
public class DataVersion : Entity<DataVersionId>
{
    public TenantId TenantId { get; private set; }
    public string EntityType { get; private set; }
    public string Version { get; private set; }
    public DateTime VersionDate { get; private set; }
    public string Description { get; private set; }
    public bool IsCurrent { get; private set; }
    public Dictionary<string, object> Metadata { get; private set; }

    public static DataVersion Create(
        TenantId tenantId,
        string entityType,
        string version,
        string description,
        Dictionary<string, object> metadata = null)
    {
        if (string.IsNullOrWhiteSpace(entityType))
            throw new DomainException("Entity type is required");

        if (string.IsNullOrWhiteSpace(version))
            throw new DomainException("Version is required");

        if (!IsValidVersionFormat(version))
            throw new DomainException("Invalid version format");

        var dataVersion = new DataVersion
        {
            Id = new DataVersionId(Guid.NewGuid()),
            TenantId = tenantId,
            EntityType = entityType,
            Version = version,
            VersionDate = DateTime.UtcNow,
            Description = description ?? string.Empty,
            IsCurrent = true,
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        dataVersion.AddDomainEvent(new DataVersionCreatedEvent(
            dataVersionId: dataVersion.Id,
            tenantId: tenantId,
            entityType: entityType,
            version: version,
            description: description,
            createdAt: dataVersion.VersionDate
        ));

        return dataVersion;
    }

    public void Deprecate()
    {
        if (!IsCurrent)
            throw new DomainException("Version is already deprecated");

        IsCurrent = false;
        AddDomainEvent(new DataVersionDeprecatedEvent(
            dataVersionId: Id,
            tenantId: TenantId,
            entityType: EntityType,
            version: Version,
            deprecatedAt: DateTime.UtcNow
        ));
    }

    public bool IsCompatibleWith(string requiredVersion)
    {
        // Semantic version compatibility check
        if (!SemanticVersion.TryParse(Version, out var currentVersion))
            return false;

        if (!SemanticVersion.TryParse(requiredVersion, out var requiredVersionObj))
            return false;

        // Major version must match
        if (currentVersion.Major != requiredVersionObj.Major)
            return false;

        // Current version must be >= required version
        return currentVersion >= requiredVersionObj;
    }

    private static bool IsValidVersionFormat(string version)
    {
        return SemanticVersion.TryParse(version, out _);
    }
}

// Domain Service - DataSyncService.cs
public class DataSyncService : IDataSyncService
{
    private readonly IDataVersionRepository _versionRepository;
    private readonly IEventBus _eventBus;
    private readonly ILogger<DataSyncService> _logger;

    public async Task<SyncResult> SyncEntityAsync<T>(
        TenantId tenantId,
        string entityType,
        T entity,
        string sourceVersion) where T : class
    {
        // Get current version
        var currentVersion = await _versionRepository.GetCurrentVersionAsync(tenantId, entityType);
        
        if (currentVersion == null)
        {
            return new SyncResult
            {
                Success = false,
                Error = "No current version found",
                RequiresUpgrade = true
            };
        }

        // Check compatibility
        if (!currentVersion.IsCompatibleWith(sourceVersion))
        {
            return new SyncResult
            {
                Success = false,
                Error = $"Version incompatibility: current={currentVersion.Version}, required={sourceVersion}",
                RequiresUpgrade = true
            };
        }

        // Perform sync
        try
        {
            await PerformEntitySync(tenantId, entityType, entity);

            var syncResult = new SyncResult
            {
                Success = true,
                SyncedAt = DateTime.UtcNow,
                Version = currentVersion.Version
            };

            // Publish sync event
            await _eventBus.PublishAsync(new EntitySyncedEvent<T>(
                tenantId: tenantId,
                entityType: entityType,
                entity: entity,
                version: currentVersion.Version,
                syncedAt: syncResult.SyncedAt.Value
            ));

            return syncResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing entity {EntityType} for tenant {TenantId}", entityType, tenantId);
            
            return new SyncResult
            {
                Success = false,
                Error = ex.Message,
                RequiresRetry = true
            };
        }
    }

    private async Task PerformEntitySync<T>(TenantId tenantId, string entityType, T entity) where T : class
    {
        // Entity-specific sync logic
        switch (entityType)
        {
            case "Order":
                await SyncOrderAsync(tenantId, (Order)(object)entity);
                break;
            case "Customer":
                await SyncCustomerAsync(tenantId, (Customer)(object)entity);
                break;
            // Add more entity types as needed
            default:
                throw new NotSupportedException($"Entity type {entityType} not supported for sync");
        }
    }

    private async Task SyncOrderAsync(TenantId tenantId, Order order)
    {
        // Order-specific sync logic
        // - Validate order data
        // - Check for conflicts
        // - Apply updates
        // - Trigger related events
        
        await _eventBus.PublishAsync(new OrderSyncedEvent(
            orderId: order.Id,
            tenantId: tenantId,
            syncedAt: DateTime.UtcNow
        ));
    }

    private async Task SyncCustomerAsync(TenantId tenantId, Customer customer)
    {
        // Customer-specific sync logic
        // - Validate customer data
        // - Check for conflicts
        // - Apply updates
        // - Update related data
        
        await _eventBus.PublishAsync(new CustomerSyncedEvent(
            customerId: customer.Id,
            tenantId: tenantId,
            syncedAt: DateTime.UtcNow
        ));
    }
}
```

---

### **2.5 Notification & Messaging Flow**

#### **Phase 1: Event Bus Implementation**
```csharp
// Interface - IEventBus.cs
public interface IEventBus
{
    Task PublishAsync<T>(T domainEvent) where T : IDomainEvent;
    Task PublishAsync(IEnumerable<IDomainEvent> domainEvents);
    Task SubscribeAsync<T>(IEventHandler<T> handler) where T : IDomainEvent;
    Task UnsubscribeAsync<T>(IEventHandler<T> handler) where T : IDomainEvent;
}

// Implementation - RabbitMqEventBus.cs
public class RabbitMqEventBus : IEventBus
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMqEventBus> _logger;
    private readonly Dictionary<Type, List<object>> _handlers = new();
    private readonly string _exchangeName = "vanan.events";

    public RabbitMqEventBus(
        IConnectionFactory connectionFactory,
        ILogger<RabbitMqEventBus> logger)
    {
        _connection = connectionFactory.CreateConnection();
        _channel = _connection.CreateModel();
        _logger = logger;

        _channel.ExchangeDeclare(_exchangeName, ExchangeType.Fanout, durable: true);
    }

    public async Task PublishAsync<T>(T domainEvent) where T : IDomainEvent
    {
        var eventType = typeof(T);
        var eventName = eventType.Name;
        var message = JsonSerializer.Serialize(domainEvent);
        var body = Encoding.UTF8.GetBytes(message);

        _channel.BasicPublish(
            exchange: _exchangeName,
            routingKey: eventName,
            basicProperties: null,
            body: body
        );

        _logger.LogInformation("Published event {EventType} with ID {EventId}", eventType.Name, domainEvent.EventId);
    }

    public async Task SubscribeAsync<T>(IEventHandler<T> handler) where T : IDomainEvent
    {
        var eventType = typeof(T);
        
        if (!_handlers.ContainsKey(eventType))
        {
            _handlers[eventType] = new List<object>();
            
            // Declare queue for this event type
            var queueName = $"vanan.{eventType.Name.ToLower()}";
            _channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind(queueName, _exchangeName, eventType.Name);
            
            // Start consuming
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                
                try
                {
                    var domainEvent = JsonSerializer.Deserialize<T>(message);
                    if (domainEvent != null)
                    {
                        await handler.Handle(domainEvent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling event {EventType}", eventType.Name);
                }
            };
            
            _channel.BasicConsume(queueName, autoAck: true, consumer: consumer);
        }
        
        _handlers[eventType].Add(handler);
        
        _logger.LogInformation("Subscribed handler for event {EventType}", eventType.Name);
    }
}

// Domain Event - OrderCreatedEvent.cs
public record OrderCreatedEvent(
    Guid EventId,
    DateTime EventDate,
    OrderId OrderId,
    CustomerId CustomerId,
    TenantId TenantId,
    Money TotalAmount,
    List<OrderItemEvent> Items
) : IDomainEvent
{
    public OrderCreatedEvent(
        OrderId orderId,
        CustomerId customerId,
        TenantId tenantId,
        Money totalAmount,
        List<OrderItemEvent> items
    ) : this(
        Guid.NewGuid(),
        DateTime.UtcNow,
        orderId,
        customerId,
        tenantId,
        totalAmount,
        items)
    {
    }
}

// Event Handler - OrderCreatedEventHandler.cs
public class OrderCreatedEventHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly IKitchenNotificationService _kitchenNotificationService;
    private readonly ICustomerNotificationService _customerNotificationService;
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<OrderCreatedEventHandler> _logger;

    public async Task Handle(OrderCreatedEvent domainEvent)
    {
        _logger.LogInformation("Handling OrderCreatedEvent for Order {OrderId}", domainEvent.OrderId);

        // Send kitchen notification
        await _kitchenNotificationService.NotifyNewOrderAsync(new KitchenNotification
        {
            OrderId = domainEvent.OrderId,
            TenantId = domainEvent.TenantId,
            Items = domainEvent.Items.Select(i => new KitchenItemNotification
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }),
            TotalAmount = domainEvent.TotalAmount,
            CreatedAt = domainEvent.EventDate
        });

        // Send customer notification
        await _customerNotificationService.NotifyOrderCreatedAsync(new CustomerNotification
        {
            CustomerId = domainEvent.CustomerId,
            OrderId = domainEvent.OrderId,
            TotalAmount = domainEvent.TotalAmount,
            CreatedAt = domainEvent.EventDate
        });

        // Update inventory projections
        await _inventoryService.UpdateInventoryProjectionsAsync(
            domainEvent.Items.Select(i => new InventoryUpdate
            {
                ProductId = i.ProductId,
                QuantityChange = -i.Quantity,
                Reason = "Order created",
                OrderId = domainEvent.OrderId
            })
        );

        _logger.LogInformation("Successfully handled OrderCreatedEvent for Order {OrderId}", domainEvent.OrderId);
    }
}
```

---

## **3. INFRASTRUCTURE LÝ T??NG**

### **3.1 Repository Pattern with Unit of Work**
```csharp
// Interface - IUnitOfWork.cs
public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}

// Implementation - EfUnitOfWork.cs
public class EfUnitOfWork : IUnitOfWork
{
    private readonly VanAnDbContext _context;
    private IDbContextTransaction _transaction;

    public EfUnitOfWork(VanAnDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Concurrency conflict detected", ex);
        }
        catch (DbUpdateException ex)
        {
            throw new DataException("Data update failed", ex);
        }
    }

    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
            await _transaction.CommitAsync();
        }
        catch
        {
            await RollbackTransactionAsync();
            throw;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        await _transaction?.RollbackAsync();
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}

// Repository Implementation - OrderRepository.cs
public class OrderRepository : IOrderRepository
{
    private readonly VanAnDbContext _context;
    private readonly ICacheService _cacheService;

    public OrderRepository(VanAnDbContext context, ICacheService cacheService)
    {
        _context = context;
        _cacheService = cacheService;
    }

    public async Task<Order?> GetByIdAsync(OrderId id)
    {
        var cacheKey = $"order_{id}";
        
        var cached = await _cacheService.GetAsync<Order>(cacheKey);
        if (cached != null)
            return cached;

        var order = await _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);

        if (order != null)
        {
            await _cacheService.SetAsync(cacheKey, order, TimeSpan.FromMinutes(5));
        }

        return order;
    }

    public async Task<PagedResult<Order>> GetPagedAsync(OrderFilter filter, Pagination pagination)
    {
        var query = _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Where(o => o.TenantId == filter.TenantId && !o.IsDeleted);

        if (filter.Status.HasValue)
        {
            query = query.Where(o => o.Status == filter.Status.Value);
        }

        if (filter.CustomerId.HasValue)
        {
            query = query.Where(o => o.CustomerId == filter.CustomerId.Value);
        }

        if (filter.DateFrom.HasValue)
        {
            query = query.Where(o => o.OrderDate >= filter.DateFrom.Value);
        }

        if (filter.DateTo.HasValue)
        {
            query = query.Where(o => o.OrderDate <= filter.DateTo.Value);
        }

        var totalCount = await query.CountAsync();
        
        var orders = await query
            .OrderByDescending(o => o.OrderDate)
            .Skip(pagination.Page * pagination.Size)
            .Take(pagination.Size)
            .AsNoTracking()
            .ToListAsync();

        return new PagedResult<Order>(orders, totalCount, pagination.Page, pagination.Size);
    }

    public async Task AddAsync(Order order)
    {
        await _context.Orders.AddAsync(order);
        
        // Invalidate cache
        await _cacheService.RemoveAsync($"orders_tenant_{order.TenantId}");
    }

    public async Task UpdateAsync(Order order)
    {
        _context.Orders.Update(order);
        
        // Invalidate cache
        await _cacheService.RemoveAsync($"order_{order.Id}");
        await _cacheService.RemoveAsync($"orders_tenant_{order.TenantId}");
    }
}
```

---

## **4. CROSS-CUTTING CONCERNS**

### **4.1 Caching Strategy**
```csharp
// Interface - ICacheService.cs
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task RemoveAsync(string key);
    Task RemoveByPatternAsync(string pattern);
}

// Implementation - RedisCacheService.cs
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
}
```

### **4.2 Validation Service**
```csharp
// Interface - IValidationService.cs
public interface IValidationService
{
    Task<ValidationResult> ValidateAsync<T>(T instance);
}

// Implementation - FluentValidationService.cs
public class FluentValidationService : IValidationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FluentValidationService> _logger;

    public async Task<ValidationResult> ValidateAsync<T>(T instance)
    {
        var validator = _serviceProvider.GetService<IValidator<T>>();
        if (validator == null)
        {
            return ValidationResult.Success();
        }

        var result = await validator.ValidateAsync(instance);
        
        if (result.IsValid)
        {
            return ValidationResult.Success();
        }

        var errors = result.Errors
            .Select(e => new ValidationError(e.PropertyName, e.ErrorMessage))
            .ToList();

        return ValidationResult.Failed(errors);
    }
}
```

---

## **5. TESTING STRATEGY**

### **5.1 Unit Tests with Domain-Driven Approach**
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
                vatRate: new Percentage(10),
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
    public void CreateOrder_WithNoItems_ShouldThrowException()
    {
        // Arrange
        var customerId = new CustomerId(Guid.NewGuid());
        var tenantId = new TenantId(Guid.NewGuid());
        var itemsData = new List<OrderItemData>();

        // Act & Assert
        Action act = () => Order.Create(
            customerId: customerId,
            tenantId: tenantId,
            orderType: OrderType.DineIn,
            customerNotes: "Table 5",
            itemsData: itemsData
        );

        act.Should().Throw<DomainException>()
            .WithMessage("Order must have at least one item");
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

        act.Should().Throw<DomainException>()
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
                vatRate: new Percentage(10),
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

### **5.2 Integration Tests**
```csharp
// OrderApplicationServiceTests.cs
public class OrderApplicationServiceTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;
    private readonly IOrderApplicationService _orderService;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;

    public OrderApplicationServiceTests(TestApplicationFactory factory)
    {
        _factory = factory;
        _orderService = _factory.Services.GetRequiredService<IOrderApplicationService>();
        _customerRepository = _factory.Services.GetRequiredService<ICustomerRepository>();
        _productRepository = _factory.Services.GetRequiredService<IProductRepository>();
    }

    [Fact]
    public async Task CreateOrder_WithValidCommand_ShouldCreateOrder()
    {
        // Arrange
        var tenantId = new TenantId(Guid.NewGuid());
        var customer = Customer.Create("device123", "Test Customer", tenantId);
        await _customerRepository.AddAsync(customer);

        var product = Product.Create("Coffee", "Test Coffee", new Money(50000), tenantId);
        await _productRepository.AddAsync(product);

        var command = new CreateOrderCommand
        {
            CustomerId = customer.Id,
            OrderType = OrderType.DineIn,
            CustomerNotes = "Table 5",
            Items = new List<CreateOrderItemCommand>
            {
                new CreateOrderItemCommand
                {
                    ProductId = product.Id,
                    Quantity = 2,
                    Notes = "No ice"
                }
            }
        };

        // Act
        var result = await _orderService.CreateOrderAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.CustomerId.Should().Be(customer.Id);
        result.Items.Should().HaveCount(1);
        result.TotalAmount.Value.Should().Be(110000); // 50000 * 2 * 1.1

        // Verify domain events were published
        var eventBus = _factory.Services.GetRequiredService<IEventBus>() as InMemoryEventBus;
        eventBus.PublishedEvents.Should().Contain(e => e is OrderCreatedEvent);
    }
}
```

---

## **6. PERFORMANCE OPTIMIZATIONS**

### **6.1 Database Optimizations**
```csharp
// Optimized Queries with Projection
public async Task<List<OrderSummaryDto>> GetOrderSummariesAsync(TenantId tenantId, DateTime from, DateTime to)
{
    return await _context.Orders
        .Where(o => o.TenantId == tenantId && 
                   o.OrderDate >= from && 
                   o.OrderDate <= to &&
                   !o.IsDeleted)
        .Select(o => new OrderSummaryDto
        {
            Id = o.Id,
            OrderNumber = o.OrderNumber,
            CustomerId = o.CustomerId,
            Status = o.Status,
            TotalAmount = o.TotalAmount,
            OrderDate = o.OrderDate,
            ItemCount = o.Items.Count
        })
        .OrderByDescending(o => o.OrderDate)
        .AsNoTracking()
        .ToListAsync();
}

// Batch Operations
public async Task BulkUpdateStatusesAsync(List<OrderId> orderIds, OrderStatus newStatus)
{
    await _context.Orders
        .Where(o => orderIds.Contains(o.Id))
        .ExecuteUpdateAsync(setters => setters
            .SetProperty(o => o.Status, newStatus)
            .SetProperty(o => o.UpdatedAt, DateTime.UtcNow));
}
```

### **6.2 Caching Strategy**
```csharp
// Multi-level Caching
public class CachedOrderService : IOrderService
{
    private readonly IOrderService _inner;
    private readonly ICacheService _cache;
    private readonly IDistributedCache _distributedCache;

    public async Task<OrderDto> GetOrderAsync(OrderId id)
    {
        // L1: In-memory cache
        var cacheKey = $"order_{id}";
        var cached = await _cache.GetAsync<OrderDto>(cacheKey);
        if (cached != null)
            return cached;

        // L2: Distributed cache
        var distributedCached = await _distributedCache.GetStringAsync(cacheKey);
        if (distributedCached != null)
        {
            var order = JsonSerializer.Deserialize<OrderDto>(distributedCached);
            await _cache.SetAsync(cacheKey, order, TimeSpan.FromMinutes(5));
            return order;
        }

        // L3: Database
        var order = await _inner.GetOrderAsync(id);
        if (order != null)
        {
            await _cache.SetAsync(cacheKey, order, TimeSpan.FromMinutes(5));
            await _distributedCache.SetStringAsync(cacheKey, 
                JsonSerializer.Serialize(order), 
                new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(15) });
        }

        return order;
    }
}
```

---

## **7. SECURITY & MULTI-TENANCY**

### **7.1 Tenant Provider**
```csharp
public class TenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<TenantProvider> _logger;

    public TenantId GetCurrentTenantId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            throw new InvalidOperationException("No HTTP context available");

        // Try to get from claims
        var tenantClaim = httpContext.User.FindFirst("tenant_id");
        if (tenantClaim != null && Guid.TryParse(tenantClaim.Value, out var tenantId))
        {
            return new TenantId(tenantId);
        }

        // Try to get from headers
        var tenantHeader = httpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (!string.IsNullOrEmpty(tenantHeader) && Guid.TryParse(tenantHeader, out var headerTenantId))
        {
            return new TenantId(headerTenantId);
        }

        throw new SecurityException("Tenant ID not found in request");
    }
}
```

### **7.2 Global Query Filters**
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Multi-tenant global filters
    modelBuilder.Entity<Order>().HasQueryFilter(o => 
        o.TenantId == _tenantProvider.GetCurrentTenantId() && !o.IsDeleted);

    modelBuilder.Entity<Customer>().HasQueryFilter(c => 
        c.TenantId == _tenantProvider.GetCurrentTenantId() && !c.IsDeleted);

    modelBuilder.Entity<Product>().HasQueryFilter(p => 
        p.TenantId == _tenantProvider.GetCurrentTenantId() && !p.IsDeleted);

    // Soft delete global filters
    modelBuilder.Entity<Order>().HasQueryFilter(o => !o.IsDeleted);
    modelBuilder.Entity<Customer>().HasQueryFilter(c => !c.IsDeleted);
    modelBuilder.Entity<Product>().HasQueryFilter(p => !p.IsDeleted);
}
```

---

## **8. SUMMARY**

### **8.1 Key Features of Ideal CoreHub**
- **Rich Domain Models:** Business logic encapsulated in entities
- **Domain Events:** Loose coupling through event-driven architecture
- **Repository Pattern:** Clean data access abstraction
- **Unit of Work:** Transaction management
- **Multi-tenancy:** Proper tenant isolation
- **Caching:** Multi-level caching strategy
- **Validation:** Comprehensive validation framework
- **Testing:** Unit and integration test coverage
- **Performance:** Optimized queries and batch operations
- **Security:** Tenant-based security

### **8.2 Technical Excellence**
- **Clean Architecture:** Proper separation of concerns
- **DDD Principles:** Rich domain models with behavior
- **Event-Driven:** Asynchronous processing
- **CQRS:** Separate read/write models
- **SOLID Principles:** Single responsibility, open/closed, etc.
- **Error Handling:** Comprehensive error management
- **Logging:** Structured logging with correlation
- **Monitoring:** Metrics and health checks

### **8.3 Business Value**
- **Scalability:** Handle 10,000+ concurrent orders
- **Reliability:** 99.99% uptime with proper error handling
- **Performance:** Sub-100ms response times
- **Maintainability:** Clean, testable code
- **Flexibility:** Easy to extend and modify
- **Quality:** Comprehensive test coverage

This ideal CoreHub architecture provides a robust, scalable, and maintainable business logic layer that properly implements Domain-Driven Design principles while maintaining clean architecture and comprehensive testing.
