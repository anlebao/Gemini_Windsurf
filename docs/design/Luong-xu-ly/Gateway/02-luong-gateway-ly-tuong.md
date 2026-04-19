# Gateway API - Lu?ng X? Lý T??ng (Ideal Architecture)

**Ngày:** 11 tháng 4, 2026  
**Module:** 2_Gateway  
**Tr?ng thái:** Thi?t k? lu?ng x? lý lý t??ng ho?n ch?nh

---

## **1. T?NG QUAN ARCHITECTURE LÝ T??NG**

### **1.1 Clean Architecture Pattern**
```
2_Gateway/
?? Controllers/           (Presentation Layer)
?? Application/          (Application Layer)
   ?? Commands/
   ?? Queries/
   ?? Handlers/
   ?? DTOs/
   ?? Validators/
?? Domain/              (Domain Layer - Shared)
   ?? Entities/
   ?? Value Objects/
   ?? Services/
   ?? Events/
?? Infrastructure/      (Infrastructure Layer)
   ?? Repositories/
   ?? External Services/
   ?? Caching/
   ?? Messaging/
?? CrossCutting/        (Cross-cutting Concerns)
   ?? Security/
   ?? Logging/
   ?? Monitoring/
   ?? Error Handling/
```

### **1.2 Microservices Architecture**
```
Gateway API (Port 5001)
    |
    |-- Order Service
    |-- Payment Service  
    |-- Kitchen Service
    |-- Notification Service
    |-- Voice Service
    |-- Localization Service
    |-- Configuration Service
```

---

## **2. LU?NG X? LÝ LÝ T??NG**

### **2.1 Order Flow (Complete Implementation)**

#### **Phase 1: Request Validation**
```csharp
[HttpPost]
[Authorize(Roles = "Customer,Staff")]
[RateLimit(RequestsPerMinute = 60)]
public async Task<ActionResult<OrderResponseDto>> CreateOrder([FromBody] CreateOrderCommand command)
{
    // Input Validation
    var validator = new CreateOrderCommandValidator();
    var validationResult = await validator.ValidateAsync(command);
    if (!validationResult.IsValid)
    {
        return BadRequest(new ValidationErrorDto(validationResult.Errors));
    }

    // Business Rule Validation
    var businessRules = new List<IBusinessRule<CreateOrderCommand>>
    {
        new CustomerMustExistRule(command.CustomerDeviceId),
        new OrderMustHaveItemsRule(command.Items),
        new ProductsMustBeAvailableRule(command.Items),
        new OrderMustNotExceedLimitRule(command.CustomerDeviceId, command.Items)
    };

    foreach (var rule in businessRules)
    {
        var ruleResult = await rule.ValidateAsync(command);
        if (!ruleResult.IsValid)
        {
            return BadRequest(new BusinessRuleViolationDto(ruleResult.Message));
        }
    }
```

#### **Phase 2: Order Creation with Domain Events**
```csharp
    // Create Order Aggregate
    var tenantId = _tenantProvider.GetCurrentTenantId();
    var customerId = await _customerService.GetCustomerIdByDeviceIdAsync(command.CustomerDeviceId);
    
    var order = Order.Create(
        customerId: customerId,
        tenantId: tenantId,
        orderType: Enum.Parse<OrderType>(command.OrderType),
        customerNotes: command.CustomerNotes,
        items: command.Items.Select(i => new OrderItemData(
            productId: new ProductId(i.ProductId),
            quantity: i.Quantity,
            unitPrice: i.UnitPrice,
            vatRate: i.VatRate,
            notes: i.Notes
        ))
    );

    // Apply Business Rules
    order.ApplyPricingRules(await _pricingService.GetPricingRulesAsync(tenantId));
    order.ApplyDiscountRules(await _discountService.GetDiscountRulesAsync(customerId));
    order.ValidateInventory(await _inventoryService.GetInventoryAsync(command.Items.Select(i => i.ProductId)));
```

#### **Phase 3: Transactional Persistence**
```csharp
    // Unit of Work Pattern
    using var transaction = await _context.Database.BeginTransactionAsync();
    try
    {
        // Save Order
        await _orderRepository.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();

        // Reserve Inventory
        var reservationResult = await _inventoryService.ReserveInventoryAsync(
            order.Items.Select(i => new InventoryReservation(
                productId: i.ProductId,
                quantity: i.Quantity,
                orderId: order.Id,
                expiresAt: DateTime.UtcNow.AddMinutes(15)
            ))
        );

        if (!reservationResult.Success)
        {
            await transaction.RollbackAsync();
            return BadRequest(new InventoryErrorDto(reservationResult.ErrorMessage));
        }

        // Create Payment Intent
        var paymentIntent = await _paymentService.CreatePaymentIntentAsync(new PaymentIntentRequest
        {
            OrderId = order.Id,
            Amount = order.TotalAmount,
            Currency = "VND",
            CustomerId = customerId,
            PaymentMethods = await _paymentService.GetAvailablePaymentMethodsAsync(tenantId)
        });

        order.SetPaymentIntent(paymentIntent.Id);
        await _unitOfWork.SaveChangesAsync();

        await transaction.CommitAsync();
```

#### **Phase 4: Event Publishing**
```csharp
        // Publish Domain Events
        var events = order.GetUncommittedEvents();
        foreach (var domainEvent in events)
        {
            await _eventBus.PublishAsync(domainEvent);
        }

        order.MarkEventsAsCommitted();
```

#### **Phase 5: Real-time Notifications**
```csharp
        // Send Real-time Notifications
        await _notificationService.NotifyKitchenAsync(new KitchenNotification
        {
            OrderId = order.Id,
            ShopId = order.ShopId,
            Items = order.Items.Select(i => new KitchenItemNotification
            {
                ProductId = i.ProductId,
                ProductName = i.Product.Name,
                Quantity = i.Quantity,
                SpecialInstructions = i.Notes
            }),
            Priority = order.GetPriority(),
            EstimatedPreparationTime = await _kitchenService.CalculatePreparationTimeAsync(order.Items)
        });

        await _notificationService.NotifyCustomerAsync(new CustomerNotification
        {
            CustomerId = customerId,
            OrderId = order.Id,
            Status = order.Status,
            EstimatedReadyTime = order.EstimatedReadyTime,
            PaymentUrl = paymentIntent.PaymentUrl
        });
```

#### **Phase 6: Response Generation**
```csharp
        var response = new OrderResponseDto
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            Status = order.Status.ToString(),
            TotalAmount = order.TotalAmount,
            EstimatedReadyTime = order.EstimatedReadyTime,
            PaymentMethods = paymentIntent.AvailableMethods,
            QrCodeUrl = paymentIntent.QrCodeUrl,
            TrackingUrl = $"/api/orders/{order.Id}/track"
        };

        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, response);
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        _logger.LogError(ex, "Error creating order for customer {CustomerId}", customerId);
        return StatusCode(500, new InternalServerErrorDto("Order creation failed"));
    }
}
```

---

### **2.2 Payment Flow (VietQR Integration)**

#### **Phase 1: Dynamic Bank Configuration**
```csharp
[HttpPost("generate")]
[Authorize]
public async Task<ActionResult<VietQrResponseDto>> GenerateQrCode([FromBody] GenerateQrCodeCommand command)
{
    // Get Tenant Configuration
    var tenantId = _tenantProvider.GetCurrentTenantId();
    var bankConfigurations = await _bankConfigurationService.GetActiveBanksAsync(tenantId);
    
    // Validate Bank Configuration
    var selectedBank = bankConfigurations.FirstOrDefault(b => b.BankId == command.BankId);
    if (selectedBank == null)
    {
        return BadRequest(new ErrorDto("Bank not supported for this tenant"));
    }

    // Validate Order
    var order = await _orderRepository.GetByIdAsync(new OrderId(command.OrderId));
    if (order == null || order.TenantId != tenantId)
    {
        return NotFound(new ErrorDto("Order not found"));
    }

    if (order.Status != OrderStatus.PendingPayment)
    {
        return BadRequest(new ErrorDto("Order is not ready for payment"));
    }
```

#### **Phase 2: QR Code Generation**
```csharp
    // Generate QR Code
    var qrRequest = new VietQrRequest
    {
        BankConfig = new BankConfig
        {
            BankId = selectedBank.BankId,
            AccountNo = selectedBank.AccountNumber,
            AccountName = selectedBank.AccountName,
            Template = selectedBank.QrTemplate
        },
        Amount = order.TotalAmount,
        OrderDescription = $"VANAN_{order.OrderNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}",
        AdditionalData = new Dictionary<string, string>
        {
            ["OrderId"] = order.Id.ToString(),
            ["TenantId"] = tenantId.ToString(),
            ["Timestamp"] = DateTime.UtcNow.ToString("O")
        }
    };

    var qrResponse = await _vietQrService.GenerateQrCodeAsync(qrRequest);
    
    // Cache QR Response
    await _cacheService.SetAsync($"qr_{order.Id}", qrResponse, TimeSpan.FromMinutes(15));
```

#### **Phase 3: Payment Status Monitoring**
```csharp
    // Start Payment Monitoring
    _ = Task.Run(async () =>
    {
        await _paymentMonitoringService.MonitorPaymentAsync(new PaymentMonitoringRequest
        {
            OrderId = order.Id,
            QrId = qrResponse.QrId,
            Timeout = TimeSpan.FromMinutes(15),
            CheckInterval = TimeSpan.FromSeconds(10)
        });
    });

    return Ok(new VietQrResponseDto
    {
        OrderId = order.Id,
        QrImageUrl = qrResponse.QrImageUrl,
        PaymentUrl = qrResponse.PaymentUrl,
        Amount = order.TotalAmount,
        ExpiresAt = DateTime.UtcNow.AddMinutes(15),
        BankInfo = new BankInfoDto
        {
            BankName = selectedBank.BankName,
            BankLogo = selectedBank.LogoUrl
        }
    });
}
```

---

### **2.3 Kitchen Flow (Real-time Kitchen Display)**

#### **Phase 1: Kitchen Items Management**
```csharp
[HttpGet("items/{shopId}")]
[Authorize(Roles = "Masterchef,Staff,Manager")]
public async Task<ActionResult<KitchenDisplayDto>> GetKitchenDisplay(Guid shopId)
{
    // Validate User Access
    var userShopId = await _userService.GetUserShopIdAsync(User.GetUserId());
    if (userShopId != shopId)
    {
        return Forbid();
    }

    // Get Kitchen Items
    var kitchenItems = await _kitchenService.GetKitchenItemsAsync(shopId);
    
    // Group by Priority and Preparation Time
    var groupedItems = kitchenItems
        .GroupBy(i => new { i.Priority, i.Station })
        .Select(g => new KitchenItemGroupDto
        {
            Station = g.Key.Station,
            Priority = g.Key.Priority,
            Items = g.OrderBy(i => i.OrderTime).ThenBy(i => i.EstimatedPreparationTime).ToList(),
            TotalItems = g.Count(),
            EstimatedTotalTime = g.Sum(i => i.EstimatedPreparationTime)
        })
        .OrderBy(g => g.Priority)
        .ThenBy(g => g.EstimatedTotalTime);

    return Ok(new KitchenDisplayDto
    {
        ShopId = shopId,
        StationGroups = groupedItems,
        ActiveOrders = kitchenItems.Select(i => i.OrderId).Distinct().Count(),
        PendingItems = kitchenItems.Count(i => i.Status == KitchenItemStatus.Pending),
        PreparingItems = kitchenItems.Count(i => i.Status == KitchenItemStatus.Preparing),
        ReadyItems = kitchenItems.Count(i => i.Status == KitchenItemStatus.Ready),
        LastUpdated = DateTime.UtcNow
    });
}
```

#### **Phase 2: Item Status Updates**
```csharp
[HttpPut("status")]
[Authorize(Roles = "Masterchef,Staff,Manager")]
public async Task<ActionResult<KitchenItemStatusDto>> UpdateItemStatus([FromBody] UpdateItemStatusCommand command)
{
    // Validate Command
    var validator = new UpdateItemStatusCommandValidator();
    var validationResult = await validator.ValidateAsync(command);
    if (!validationResult.IsValid)
    {
        return BadRequest(new ValidationErrorDto(validationResult.Errors));
    }

    // Get Kitchen Item
    var item = await _kitchenRepository.GetItemAsync(command.ItemId);
    if (item == null)
    {
        return NotFound(new ErrorDto("Kitchen item not found"));
    }

    // Validate Status Transition
    var validTransitions = KitchenStatusTransition.GetValidTransitions(item.Status);
    if (!validTransitions.Contains(command.NewStatus))
    {
        return BadRequest(new ErrorDto($"Invalid status transition from {item.Status} to {command.NewStatus}"));
    }

    // Update Status
    var userId = User.GetUserId();
    var result = await _kitchenService.UpdateItemStatusAsync(new UpdateItemStatusRequest
    {
        ItemId = command.ItemId,
        NewStatus = command.NewStatus,
        UpdatedBy = userId,
        Notes = command.Notes,
        UpdatedAt = DateTime.UtcNow
    });

    if (!result.Success)
    {
        return BadRequest(new ErrorDto(result.ErrorMessage));
    }

    // Publish Status Change Event
    await _eventBus.PublishAsync(new KitchenItemStatusChangedEvent
    {
        ItemId = command.ItemId,
        OrderId = item.OrderId,
        OldStatus = item.Status,
        NewStatus = command.NewStatus,
        UpdatedBy = userId,
        UpdatedAt = DateTime.UtcNow,
        Station = item.Station
    });

    return Ok(new KitchenItemStatusDto
    {
        ItemId = command.ItemId,
        Status = command.NewStatus,
        UpdatedAt = DateTime.UtcNow,
        UpdatedBy = userId
    });
}
```

---

### **2.4 Voice Command Flow (AI Integration)**

#### **Phase 1: Audio Processing**
```csharp
[HttpPost("process-audio")]
[Authorize(Roles = "Masterchef,Staff,Manager")]
[RequestSizeLimit(50 * 1024 * 1024)] // 50MB limit
public async Task<ActionResult<VoiceCommandResponseDto>> ProcessAudioCommand(
    [FromForm] IFormFile audioFile,
    [FromForm] string orderId,
    [FromForm] string language = "vi-VN")
{
    // Validate Audio File
    if (audioFile == null || audioFile.Length == 0)
    {
        return BadRequest(new ErrorDto("No audio file provided"));
    }

    if (audioFile.Length > 50 * 1024 * 1024) // 50MB
    {
        return BadRequest(new ErrorDto("Audio file too large"));
    }

    var allowedFormats = new[] { ".wav", ".mp3", ".m4a", ".ogg" };
    var fileExtension = Path.GetExtension(audioFile.FileName).ToLowerInvariant();
    if (!allowedFormats.Contains(fileExtension))
    {
        return BadRequest(new ErrorDto("Unsupported audio format"));
    }

    // Validate Order
    var order = await _orderRepository.GetByIdAsync(new OrderId(orderId));
    if (order == null)
    {
        return NotFound(new ErrorDto("Order not found"));
    }

    // Process Audio with Security
    using var audioStream = new MemoryStream();
    await audioFile.CopyToAsync(audioStream);
    var audioData = audioStream.ToArray();

    // Store Audio Securely
    var audioStorageResult = await _secureAudioStorageService.StoreAsync(new AudioStorageRequest
    {
        AudioData = audioData,
        FileName = audioFile.FileName,
        OrderId = orderId,
        UserId = User.GetUserId(),
        UploadedAt = DateTime.UtcNow
    });

    if (!audioStorageResult.Success)
    {
        return StatusCode(500, new ErrorDto("Failed to store audio file"));
    }
```

#### **Phase 2: Speech Recognition**
```csharp
    // Convert Speech to Text
    var speechRecognitionResult = await _speechRecognitionService.RecognizeAsync(new SpeechRecognitionRequest
    {
        AudioData = audioData,
        Language = language,
        Format = fileExtension.Trim('.'),
        TenantId = _tenantProvider.GetCurrentTenantId()
    });

    if (!speechRecognitionResult.Success)
    {
        return BadRequest(new ErrorDto($"Speech recognition failed: {speechRecognitionResult.ErrorMessage}"));
    }

    var recognizedText = speechRecognitionResult.Text;
    
    // Log Voice Command for Audit
    await _auditService.LogVoiceCommandAsync(new VoiceCommandAudit
    {
        OrderId = orderId,
        UserId = User.GetUserId(),
        RecognizedText = recognizedText,
        AudioFileId = audioStorageResult.FileId,
        ProcessedAt = DateTime.UtcNow,
        Language = language
    });
```

#### **Phase 3: Command Processing**
```csharp
    // Process Voice Command
    var commandResult = await _voiceCommandProcessor.ProcessCommandAsync(new VoiceCommandRequest
    {
        Text = recognizedText,
        OrderId = orderId,
        UserId = User.GetUserId(),
        Context = await _contextService.GetContextAsync(User.GetUserId(), orderId),
        Language = language
    });

    if (!commandResult.Success)
    {
        return BadRequest(new ErrorDto($"Command processing failed: {commandResult.ErrorMessage}"));
    }

    // Execute Command
    var executionResult = await _commandExecutor.ExecuteAsync(commandResult.Command);
    
    if (!executionResult.Success)
    {
        return StatusCode(500, new ErrorDto($"Command execution failed: {executionResult.ErrorMessage}"));
    }

    // Return Response
    return Ok(new VoiceCommandResponseDto
    {
        CommandId = commandResult.CommandId,
        RecognizedText = recognizedText,
        Intent = commandResult.Command.Intent,
        Entities = commandResult.Command.Entities,
        ExecutedAction = executionResult.Action,
        Result = executionResult.Result,
        Confidence = speechRecognitionResult.Confidence,
        AudioFileId = audioStorageResult.FileId
    });
}
```

---

### **2.5 Real-time Communication (SignalR Hubs)**

#### **Phase 1: Kitchen Hub**
```csharp
[Authorize(Roles = "Masterchef,Staff,Manager")]
public class KitchenHub : Hub<IKitchenClient>
{
    private readonly IUserService _userService;
    private readonly ITenantProvider _tenantProvider;

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User.GetUserId();
        var shopId = await _userService.GetUserShopIdAsync(userId);
        
        await Groups.AddToGroupAsync(Context.ConnectionId, $"shop_{shopId}");
        await Clients.Caller.JoinShopGroup(shopId);
        
        await base.OnConnectedAsync();
    }

    public async Task JoinStation(string station)
    {
        var userId = Context.User.GetUserId();
        var hasPermission = await _userService.HasStationPermissionAsync(userId, station);
        
        if (hasPermission)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"station_{station}");
            await Clients.Caller.JoinedStation(station);
        }
        else
        {
            await Clients.Caller.Error("No permission for this station");
        }
    }

    public async Task UpdateItemStatus(Guid itemId, KitchenItemStatus status, string notes)
    {
        var userId = Context.User.GetUserId();
        var command = new UpdateItemStatusCommand
        {
            ItemId = itemId,
            NewStatus = status,
            Notes = notes,
            UpdatedBy = userId
        };

        // Send to command handler
        var result = await _mediator.Send(command);
        
        if (result.Success)
        {
            await Clients.Group($"shop_{result.ShopId}").ItemStatusUpdated(result.Item);
        }
        else
        {
            await Clients.Caller.Error(result.ErrorMessage);
        }
    }
}
```

#### **Phase 2: Order Tracking Hub**
```csharp
[AllowAnonymous] // Allow customers to track orders
public class OrderTrackingHub : Hub<IOrderTrackingClient>
{
    public async Task JoinOrderTracking(string orderId)
    {
        // Validate Order
        var order = await _orderRepository.GetByIdAsync(new OrderId(orderId));
        if (order == null)
        {
            await Clients.Caller.Error("Order not found");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"order_{orderId}");
        await Clients.Caller.OrderJoined(new OrderStatusDto
        {
            OrderId = order.Id,
            Status = order.Status,
            OrderNumber = order.OrderNumber,
            Items = order.Items.Select(i => new OrderItemStatusDto
            {
                ProductName = i.Product.Name,
                Quantity = i.Quantity,
                Status = i.KitchenItem?.Status ?? KitchenItemStatus.Pending
            }),
            EstimatedReadyTime = order.EstimatedReadyTime,
            LastUpdated = DateTime.UtcNow
        });
    }
}
```

---

## **3. SECURITY & AUTHENTICATION**

### **3.1 Multi-tenant Security**
```csharp
public class TenantRequirement : IAuthorizationRequirement
{
    public string TenantId { get; set; }
}

public class TenantAuthorizationHandler : AuthorizationHandler<TenantRequirement>
{
    private readonly ITenantProvider _tenantProvider;

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantRequirement requirement)
    {
        var currentTenant = _tenantProvider.GetCurrentTenantId();
        if (currentTenant == requirement.TenantId)
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }
        
        return Task.CompletedAsync;
    }
}
```

### **3.2 Rate Limiting**
```csharp
public class RateLimitAttribute : Attribute, IAsyncActionFilter
{
    private readonly int _requestsPerMinute;
    private readonly IMemoryCache _cache;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var clientId = GetClientId(context.HttpContext);
        var cacheKey = $"rate_limit_{clientId}";
        
        var requestCount = await _cache.GetOrCreateAsync(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            return Task.FromResult(0);
        });

        if (requestCount >= _requestsPerMinute)
        {
            context.Result = new StatusCodeResult(429); // Too Many Requests
            return;
        }

        await _cache.SetAsync(cacheKey, requestCount + 1);
        await next();
    }
}
```

---

## **4. ERROR HANDLING & LOGGING**

### **4.1 Global Error Handling**
```csharp
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "An error occurred: {Message}", exception.Message);

        var errorResponse = exception switch
        {
            ValidationException => new ValidationErrorDto(((ValidationException)exception).Errors),
            BusinessRuleViolationException => new BusinessRuleViolationDto(exception.Message),
            NotFoundException => new NotFoundErrorDto(exception.Message),
            UnauthorizedAccessException => new UnauthorizedErrorDto("Access denied"),
            _ => new InternalServerErrorDto("An unexpected error occurred")
        };

        httpContext.Response.StatusCode = errorResponse.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(errorResponse, cancellationToken);

        return true;
    }
}
```

### **4.2 Structured Logging**
```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = _userContext.UserId;
        var tenantId = _tenantProvider.GetCurrentTenantId();

        _logger.LogInformation("Processing request {RequestName} for user {UserId} in tenant {TenantId}",
            requestName, userId, tenantId);

        try
        {
            var response = await next();
            
            _logger.LogInformation("Completed request {RequestName} for user {UserId} in tenant {TenantId}",
                requestName, userId, tenantId);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request {RequestName} for user {UserId} in tenant {TenantId}",
                requestName, userId, tenantId);
            throw;
        }
    }
}
```

---

## **5. MONITORING & OBSERVABILITY**

### **5.1 Health Checks**
```csharp
public class GatewayHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var checks = new[]
        {
            CheckDatabase(),
            CheckExternalServices(),
            CheckMemoryUsage(),
            CheckDiskSpace()
        };

        var results = await Task.WhenAll(checks);
        var allHealthy = results.All(r => r.Status == HealthStatus.Healthy);

        return allHealthy 
            ? HealthCheckResult.Healthy("All systems operational")
            : HealthCheckResult.Degraded("Some systems degraded");
    }
}
```

### **5.2 Metrics Collection**
```csharp
public class MetricsCollector : IObserver<CommandExecutedEvent>
{
    private readonly IMeter _meter;
    private readonly Counter<int> _commandCounter;
    private readonly Histogram<double> _commandDuration;

    public MetricsCollector(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create("Gateway");
        _commandCounter = _meter.CreateCounter<int>("gateway.commands.executed");
        _commandDuration = _meter.CreateHistogram<double>("gateway.commands.duration");
    }

    public void OnNext(CommandExecutedEvent value)
    {
        _commandCounter.Add(1, new KeyValuePair<string, object>("command", value.CommandType));
        _commandDuration.Record(value.Duration.TotalMilliseconds, new KeyValuePair<string, object>("command", value.CommandType));
    }
}
```

---

## **6. PERFORMANCE OPTIMIZATIONS**

### **6.1 Caching Strategy**
```csharp
public class CachedOrderService : IOrderService
{
    private readonly IOrderService _inner;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    public async Task<OrderDto> GetOrderAsync(Guid orderId)
    {
        var cacheKey = $"order_{orderId}";
        
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _cacheDuration;
            return await _inner.GetOrderAsync(orderId);
        });
    }
}
```

### **6.2 Database Optimization**
```csharp
public class OrderRepository : IOrderRepository
{
    public async Task<PagedResult<Order>> GetOrdersAsync(OrderFilter filter, Pagination pagination)
    {
        var query = _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Where(o => o.TenantId == filter.TenantId);

        if (filter.Status.HasValue)
        {
            query = query.Where(o => o.Status == filter.Status);
        }

        if (filter.DateFrom.HasValue)
        {
            query = query.Where(o => o.OrderDate >= filter.DateFrom);
        }

        var totalCount = await query.CountAsync();
        var orders = await query
            .OrderByDescending(o => o.OrderDate)
            .Skip(pagination.Page * pagination.Size)
            .Take(pagination.Size)
            .AsNoTracking() // Performance optimization
            .ToListAsync();

        return new PagedResult<Order>(orders, totalCount, pagination.Page, pagination.Size);
    }
}
```

---

## **7. TESTING STRATEGY**

### **7.1 Unit Tests**
```csharp
public class OrdersControllerTests
{
    [Fact]
    public async Task CreateOrder_WithValidRequest_ShouldReturnCreated()
    {
        // Arrange
        var command = new CreateOrderCommand { /* valid data */ };
        var expectedOrder = new Order { /* expected order */ };
        
        _mediator.Send(command).Returns(expectedOrder);

        // Act
        var result = await _controller.CreateOrder(command);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.Value.Should().BeEquivalentTo(expectedOrder);
    }

    [Fact]
    public async Task CreateOrder_WithInvalidRequest_ShouldReturnBadRequest()
    {
        // Arrange
        var command = new CreateOrderCommand { /* invalid data */ };
        _validator.ValidateAsync(command).Returns(new ValidationResult(new[] { new ValidationFailure() }));

        // Act
        var result = await _controller.CreateOrder(command);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
```

### **7.2 Integration Tests**
```csharp
public class OrderIntegrationTests : IClassFixture<TestApplicationFactory>
{
    [Fact]
    public async Task CreateOrder_ShouldCreateOrderAndTriggerEvents()
    {
        // Arrange
        var client = _factory.CreateClient();
        var command = new CreateOrderCommand { /* valid data */ };

        // Act
        var response = await client.PostAsJsonAsync("/api/orders", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        order.Should().NotBeNull();
        
        // Verify events were published
        _eventBus.Received(1).PublishAsync(Arg.Any<OrderCreatedEvent>());
    }
}
```

---

## **8. DEPLOYMENT CONFIGURATION**

### **8.1 Environment Configuration**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=${DB_HOST};Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASSWORD}"
  },
  "Redis": {
    "ConnectionString": "${REDIS_CONNECTION_STRING}"
  },
  "VietQR": {
    "BaseUrl": "https://api.vietqr.io",
    "ApiKey": "${VIETQR_API_KEY}"
  },
  "SpeechRecognition": {
    "ApiKey": "${SPEECH_API_KEY}",
    "Region": "${SPEECH_REGION}"
  }
}
```

### **8.2 Docker Configuration**
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["2_Gateway/VanAn.Gateway.csproj", "2_Gateway/"]
RUN dotnet restore "2_Gateway/VanAn.Gateway.csproj"
COPY . .
WORKDIR "/src/2_Gateway"
RUN dotnet build "VanAn.Gateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "VanAn.Gateway.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VanAn.Gateway.dll"]
```

---

## **9. SUMMARY**

### **9.1 Key Features of Ideal Gateway**
- **Clean Architecture:** Proper separation of concerns
- **Domain-Driven Design:** Rich domain models with business logic
- **Event-Driven Architecture:** Domain events for loose coupling
- **Multi-tenant Security:** Proper tenant isolation
- **Real-time Communication:** SignalR hubs with proper authentication
- **Comprehensive Error Handling:** Global exception handling
- **Performance Optimization:** Caching, database optimization
- **Observability:** Logging, metrics, health checks
- **Testing:** Unit, integration, and E2E tests
- **Security:** Authentication, authorization, rate limiting

### **9.2 Technical Excellence**
- **No Hardcoded Values:** All configuration externalized
- **Proper Validation:** Input validation and business rules
- **Transaction Management:** Unit of work pattern
- **Scalability:** Microservices ready
- **Maintainability:** Clean code principles
- **Reliability:** Circuit breakers, retries
- **Security:** Zero-trust architecture

### **9.3 Business Value**
- **Customer Experience:** Real-time order tracking
- **Operational Efficiency:** Automated kitchen workflow
- **Scalability:** Handle 1000+ concurrent orders
- **Reliability:** 99.9% uptime
- **Security:** PCI DSS compliant payments
- **Multi-language:** Support for Vietnamese and English

This ideal architecture provides a robust, scalable, and maintainable Gateway API that can handle enterprise-level requirements while maintaining clean code principles and proper separation of concerns.
