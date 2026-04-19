# StationApp - Comparison and Improvement Plan

**Date:** April 11, 2026  
**Module:** 4_MobileApps/StationApp  
**Status:** Analysis comparison and improvement planning

---

## **1. REALISTIC vs IDEAL COMPARISON**

### **1.1 Application Architecture Comparison**

| **Aspect** | **Realistic** | **Ideal** | **Gap Analysis** |
|------------|--------------|-----------|------------------|
| **Architecture** | Basic MAUI template | MVVM with Clean Architecture | **High** - Need complete redesign |
| **UI Framework** | Basic XAML pages | Advanced UI with custom controls | **High** - Need UI enhancement |
| **Navigation** | Basic shell navigation | Advanced navigation with deep linking | **Medium** - Need navigation enhancement |
| **State Management** | No state management | Redux-like state management | **High** - Need state management |
| **Data Binding** | Basic data binding | Advanced binding with validation | **Medium** - Need binding enhancement |

### **1.2 Station Features Comparison**

| **Aspect** | **Realistic** | **Ideal** | **Gap Analysis** |
|------------|--------------|-----------|------------------|
| **Order Management** | No station features | Complete order management | **High** - Need station features |
| **Kitchen Display** | No kitchen display | Real-time kitchen display | **High** - Need kitchen display |
| **Inventory Management** | No inventory tracking | Real-time inventory tracking | **High** - Need inventory system |
| **Staff Management** | No staff features | Staff scheduling and management | **High** - Need staff system |
| **Performance Analytics** | No analytics | Real-time performance analytics | **High** - Need analytics system |

### **1.3 Technical Implementation Comparison**

| **Aspect** | **Realistic** | **Ideal** | **Gap Analysis** |
|------------|--------------|-----------|------------------|
| **API Integration** | No API integration | Real-time WebSocket integration | **High** - Need API integration |
| **Authentication** | No authentication | Role-based authentication | **High** - Need auth system |
| **Offline Support** | No offline support | Offline-first architecture | **Medium** - Need offline support |
| **Data Synchronization** | No sync mechanism | Real-time synchronization | **High** - Need sync system |
| **Hardware Integration** | No hardware integration | Printer, display, scanner integration | **High** - Need hardware integration |

### **1.4 User Experience Comparison**

| **Aspect** | **Realistic** | **Ideal** | **Gap Analysis** |
|------------|--------------|-----------|------------------|
| **User Interface** | Template UI | Professional station interface | **High** - Need UI redesign |
| **User Experience** | Basic UX | Intuitive station workflow | **High** - Need UX enhancement |
| **Accessibility** | No accessibility | Full accessibility support | **Medium** - Need accessibility |
| **Localization** | No localization | Multi-language support | **Medium** - Need localization |
| **Performance** | Basic performance | Optimized performance | **Medium** - Need optimization |

---

## **2. PRIORITY ASSESSMENT**

### **2.1 Critical Issues (Priority 1)**
1. **No Station Features** - Complete lack of station functionality
2. **No API Integration** - No backend connectivity
3. **No Authentication** - No security or user management
4. **No Data Management** - No data persistence or sync
5. **No Professional UI** - Template interface only

### **2.2 Important Issues (Priority 2)**
1. **No State Management** - No application state handling
2. **No Offline Support** - No offline capability
3. **No Performance Optimization** - Basic performance only
4. **No Accessibility** - No accessibility features
5. **No Localization** - No multi-language support

### **2.3 Nice to Have (Priority 3)**
1. **No Advanced Features** - No advanced station features
2. **No Analytics** - No station analytics or reporting
3. **No Integration** - No third-party integrations
4. **No Customization** - No customizable features
5. **No Automation** - No workflow automation

---

## **3. IMPROVEMENT PLAN**

### **3.1 Phase 1: Foundation Architecture (Week 1-2)**

#### **Day 1-3: MVVM Architecture Implementation**
```csharp
// StationApp/ViewModels/BaseViewModel.cs
public abstract class BaseViewModel : ObservableObject
{
    protected readonly INavigationService _navigationService;
    protected readonly IDialogService _dialogService;
    protected readonly IApiService _apiService;
    protected readonly IAuthenticationService _authService;
    protected readonly IWebSocketService _webSocketService;

    private bool _isBusy;
    private string _title;
    private bool _isConnected;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    protected BaseViewModel(
        INavigationService navigationService,
        IDialogService dialogService,
        IApiService apiService,
        IAuthenticationService authService,
        IWebSocketService webSocketService)
    {
        _navigationService = navigationService;
        _dialogService = dialogService;
        _apiService = apiService;
        _authService = authService;
        _webSocketService = webSocketService;

        // Subscribe to WebSocket connection events
        _webSocketService.ConnectionChanged += OnConnectionChanged;
    }

    protected virtual async Task ExecuteAsync(Func<Task> operation, string loadingMessage = null)
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            
            if (!string.IsNullOrEmpty(loadingMessage))
            {
                await _dialogService.ShowLoadingAsync(loadingMessage);
            }

            await operation();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsBusy = false;
            
            if (!string.IsNullOrEmpty(loadingMessage))
            {
                await _dialogService.HideLoadingAsync();
            }
        }
    }

    protected virtual async Task HandleErrorAsync(Exception exception)
    {
        await _dialogService.ShowErrorAsync(exception.Message);
    }

    protected virtual async Task ShowSuccessAsync(string message)
    {
        await _dialogService.ShowSuccessAsync(message);
    }

    protected virtual async Task<bool> ShowConfirmationAsync(string message, string title = "Confirm")
    {
        return await _dialogService.ShowConfirmationAsync(message, title);
    }

    protected virtual void OnConnectionChanged(bool isConnected)
    {
        IsConnected = isConnected;
    }

    protected virtual async Task SendWebSocketMessageAsync<T>(T message)
    {
        if (IsConnected)
        {
            await _webSocketService.SendMessageAsync(message);
        }
        else
        {
            await _dialogService.ShowErrorAsync("No connection to server");
        }
    }

    public virtual void Dispose()
    {
        _webSocketService.ConnectionChanged -= OnConnectionChanged;
    }
}

// StationApp/ViewModels/KitchenDisplayViewModel.cs
public class KitchenDisplayViewModel : BaseViewModel
{
    private readonly IOrderService _orderService;
    private readonly IStationService _stationService;
    private readonly IPrinterService _printerService;

    private ObservableCollection<OrderItem> _pendingOrders;
    private ObservableCollection<OrderItem> _preparingOrders;
    private ObservableCollection<OrderItem> _readyOrders;
    private StationInfo _stationInfo;
    private int _pendingCount;
    private int _preparingCount;
    private int _readyCount;

    public ObservableCollection<OrderItem> PendingOrders
    {
        get => _pendingOrders;
        set => SetProperty(ref _pendingOrders, value);
    }

    public ObservableCollection<OrderItem> PreparingOrders
    {
        get => _preparingOrders;
        set => SetProperty(ref _preparingOrders, value);
    }

    public ObservableCollection<OrderItem> ReadyOrders
    {
        get => _readyOrders;
        set => SetProperty(ref _readyOrders, value);
    }

    public StationInfo StationInfo
    {
        get => _stationInfo;
        set => SetProperty(ref _stationInfo, value);
    }

    public int PendingCount
    {
        get => _pendingCount;
        set => SetProperty(ref _pendingCount, value);
    }

    public int PreparingCount
    {
        get => _preparingCount;
        set => SetProperty(ref _preparingCount, value);
    }

    public int ReadyCount
    {
        get => _readyCount;
        set => SetProperty(ref _readyCount, value);
    }

    public ICommand StartPreparingCommand { get; }
    public ICommand CompletePreparingCommand { get; }
    public ICommand MarkReadyCommand { get; }
    public ICommand ServeOrderCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand PrintOrderCommand { get; }

    public KitchenDisplayViewModel(
        INavigationService navigationService,
        IDialogService dialogService,
        IApiService apiService,
        IAuthenticationService authService,
        IWebSocketService webSocketService,
        IOrderService orderService,
        IStationService stationService,
        IPrinterService printerService)
        : base(navigationService, dialogService, apiService, authService, webSocketService)
    {
        _orderService = orderService;
        _stationService = stationService;
        _printerService = printerService;

        StartPreparingCommand = new RelayCommand<OrderItem>(async (order) => await StartPreparingAsync(order));
        CompletePreparingCommand = new RelayCommand<OrderItem>(async (order) => await CompletePreparingAsync(order));
        MarkReadyCommand = new RelayCommand<OrderItem>(async (order) => await MarkReadyAsync(order));
        ServeOrderCommand = new RelayCommand<OrderItem>(async (order) => await ServeOrderAsync(order));
        RefreshCommand = new RelayCommand(async () => await LoadOrdersAsync());
        PrintOrderCommand = new RelayCommand<OrderItem>(async (order) => await PrintOrderAsync(order));

        Title = "Kitchen Display";
        
        // Initialize collections
        PendingOrders = new ObservableCollection<OrderItem>();
        PreparingOrders = new ObservableCollection<OrderItem>();
        ReadyOrders = new ObservableCollection<OrderItem>();
    }

    public async Task InitializeAsync()
    {
        await ExecuteAsync(async () =>
        {
            await Task.WhenAll(
                LoadStationInfoAsync(),
                LoadOrdersAsync(),
                SubscribeToOrderUpdatesAsync()
            );
        }, "Initializing kitchen display...");
    }

    private async Task LoadStationInfoAsync()
    {
        var stationId = await _authService.GetCurrentStationIdAsync();
        StationInfo = await _stationService.GetStationInfoAsync(stationId);
    }

    private async Task LoadOrdersAsync()
    {
        var stationId = await _authService.GetCurrentStationIdAsync();
        var orders = await _orderService.GetStationOrdersAsync(stationId);

        // Clear existing orders
        PendingOrders.Clear();
        PreparingOrders.Clear();
        ReadyOrders.Clear();

        // Categorize orders
        foreach (var order in orders)
        {
            switch (order.Status)
            {
                case OrderStatus.Pending:
                    PendingOrders.Add(order);
                    break;
                case OrderStatus.Preparing:
                    PreparingOrders.Add(order);
                    break;
                case OrderStatus.Ready:
                    ReadyOrders.Add(order);
                    break;
            }
        }

        UpdateCounts();
    }

    private async Task SubscribeToOrderUpdatesAsync()
    {
        await _webSocketService.SubscribeToAsync<OrderUpdate>("order-updates", async (update) =>
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await HandleOrderUpdateAsync(update);
            });
        });
    }

    private async Task HandleOrderUpdateAsync(OrderUpdate update)
    {
        var order = update.Order;
        
        // Remove from existing collection
        RemoveOrderFromCollections(order.Id);
        
        // Add to appropriate collection
        switch (order.Status)
        {
            case OrderStatus.Pending:
                PendingOrders.Insert(0, order);
                break;
            case OrderStatus.Preparing:
                PreparingOrders.Insert(0, order);
                break;
            case OrderStatus.Ready:
                ReadyOrders.Insert(0, order);
                break;
            case OrderStatus.Completed:
                // Don't add completed orders to display
                break;
        }

        UpdateCounts();

        // Show notification for important updates
        if (update.Type == OrderUpdateType.StatusChanged)
        {
            await ShowOrderNotificationAsync(order);
        }
    }

    private void RemoveOrderFromCollections(Guid orderId)
    {
        var pendingOrder = PendingOrders.FirstOrDefault(o => o.Id == orderId);
        if (pendingOrder != null) PendingOrders.Remove(pendingOrder);

        var preparingOrder = PreparingOrders.FirstOrDefault(o => o.Id == orderId);
        if (preparingOrder != null) PreparingOrders.Remove(preparingOrder);

        var readyOrder = ReadyOrders.FirstOrDefault(o => o.Id == orderId);
        if (readyOrder != null) ReadyOrders.Remove(readyOrder);
    }

    private void UpdateCounts()
    {
        PendingCount = PendingOrders.Count;
        PreparingCount = PreparingOrders.Count;
        ReadyCount = ReadyOrders.Count;
    }

    private async Task StartPreparingAsync(OrderItem order)
    {
        await ExecuteAsync(async () =>
        {
            await _orderService.UpdateOrderStatusAsync(order.Id, OrderStatus.Preparing);
            
            // Send WebSocket update
            await SendWebSocketMessageAsync(new OrderStatusUpdate
            {
                OrderId = order.Id,
                Status = OrderStatus.Preparing,
                StationId = StationInfo.Id,
                UpdatedBy = await _authService.GetCurrentUserIdAsync(),
                UpdatedAt = DateTime.UtcNow
            });

            await ShowSuccessAsync("Order preparation started");
        }, "Starting preparation...");
    }

    private async Task CompletePreparingAsync(OrderItem order)
    {
        await ExecuteAsync(async () =>
        {
            await _orderService.UpdateOrderStatusAsync(order.Id, OrderStatus.Ready);
            
            // Send WebSocket update
            await SendWebSocketMessageAsync(new OrderStatusUpdate
            {
                OrderId = order.Id,
                Status = OrderStatus.Ready,
                StationId = StationInfo.Id,
                UpdatedBy = await _authService.GetCurrentUserIdAsync(),
                UpdatedAt = DateTime.UtcNow
            });

            await ShowSuccessAsync("Order is ready for serving");
        }, "Completing preparation...");
    }

    private async Task MarkReadyAsync(OrderItem order)
    {
        await CompletePreparingAsync(order);
    }

    private async Task ServeOrderAsync(OrderItem order)
    {
        await ExecuteAsync(async () =>
        {
            await _orderService.UpdateOrderStatusAsync(order.Id, OrderStatus.Completed);
            
            // Send WebSocket update
            await SendWebSocketMessageAsync(new OrderStatusUpdate
            {
                OrderId = order.Id,
                Status = OrderStatus.Completed,
                StationId = StationInfo.Id,
                UpdatedBy = await _authService.GetCurrentUserIdAsync(),
                UpdatedAt = DateTime.UtcNow
            });

            await ShowSuccessAsync("Order served successfully");
        }, "Serving order...");
    }

    private async Task PrintOrderAsync(OrderItem order)
    {
        await ExecuteAsync(async () =>
        {
            await _printerService.PrintOrderAsync(order);
            await ShowSuccessAsync("Order printed successfully");
        }, "Printing order...");
    }

    private async Task ShowOrderNotificationAsync(OrderItem order)
    {
        var message = order.Status switch
        {
            OrderStatus.Preparing => $"Order #{order.OrderNumber} is now being prepared",
            OrderStatus.Ready => $"Order #{order.OrderNumber} is ready for serving",
            OrderStatus.Completed => $"Order #{order.OrderNumber} has been served",
            _ => $"Order #{order.OrderNumber} status updated"
        };

        await _dialogService.ShowToastAsync(message);
    }
}

// StationApp/ViewModels/StationDashboardViewModel.cs
public class StationDashboardViewModel : BaseViewModel
{
    private readonly IStationService _stationService;
    private readonly IOrderService _orderService;
    private readonly IInventoryService _inventoryService;
    private readonly IStaffService _staffService;

    private StationInfo _stationInfo;
    private StationPerformance _performance;
    private ObservableCollection<OrderItem> _recentOrders;
    private ObservableCollection<InventoryItem> _lowStockItems;
    private ObservableCollection<StaffMember> _activeStaff;
    private DateTime _selectedDate;

    public StationInfo StationInfo
    {
        get => _stationInfo;
        set => SetProperty(ref _stationInfo, value);
    }

    public StationPerformance Performance
    {
        get => _performance;
        set => SetProperty(ref _performance, value);
    }

    public ObservableCollection<OrderItem> RecentOrders
    {
        get => _recentOrders;
        set => SetProperty(ref _recentOrders, value);
    }

    public ObservableCollection<InventoryItem> LowStockItems
    {
        get => _lowStockItems;
        set => SetProperty(ref _lowStockItems, value);
    }

    public ObservableCollection<StaffMember> ActiveStaff
    {
        get => _activeStaff;
        set => SetProperty(ref _activeStaff, value);
    }

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (SetProperty(ref _selectedDate, value))
            {
                _ = LoadDashboardDataAsync();
            }
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand ViewOrdersCommand { get; }
    public ICommand ViewInventoryCommand { get; }
    public ICommand ViewStaffCommand { get; }
    public ICommand ViewReportsCommand { get; }

    public StationDashboardViewModel(
        INavigationService navigationService,
        IDialogService dialogService,
        IApiService apiService,
        IAuthenticationService authService,
        IWebSocketService webSocketService,
        IStationService stationService,
        IOrderService orderService,
        IInventoryService inventoryService,
        IStaffService staffService)
        : base(navigationService, dialogService, apiService, authService, webSocketService)
    {
        _stationService = stationService;
        _orderService = orderService;
        _inventoryService = inventoryService;
        _staffService = staffService;

        RefreshCommand = new RelayCommand(async () => await LoadDashboardDataAsync());
        ViewOrdersCommand = new RelayCommand(async () => await ViewOrdersAsync());
        ViewInventoryCommand = new RelayCommand(async () => await ViewInventoryAsync());
        ViewStaffCommand = new RelayCommand(async () => await ViewStaffAsync());
        ViewReportsCommand = new RelayCommand(async () => await ViewReportsAsync());

        Title = "Station Dashboard";
        SelectedDate = DateTime.Today;
        
        // Initialize collections
        RecentOrders = new ObservableCollection<OrderItem>();
        LowStockItems = new ObservableCollection<InventoryItem>();
        ActiveStaff = new ObservableCollection<StaffMember>();
    }

    public async Task InitializeAsync()
    {
        await LoadDashboardDataAsync();
    }

    private async Task LoadDashboardDataAsync()
    {
        await ExecuteAsync(async () =>
        {
            var stationId = await _authService.GetCurrentStationIdAsync();
            
            await Task.WhenAll(
                LoadStationInfoAsync(stationId),
                LoadPerformanceAsync(stationId),
                LoadRecentOrdersAsync(stationId),
                LoadLowStockItemsAsync(stationId),
                LoadActiveStaffAsync(stationId)
            );
        }, "Loading dashboard data...");
    }

    private async Task LoadStationInfoAsync(Guid stationId)
    {
        StationInfo = await _stationService.GetStationInfoAsync(stationId);
    }

    private async Task LoadPerformanceAsync(Guid stationId)
    {
        Performance = await _stationService.GetStationPerformanceAsync(stationId, SelectedDate);
    }

    private async Task LoadRecentOrdersAsync(Guid stationId)
    {
        var orders = await _orderService.GetRecentOrdersAsync(stationId, 10);
        RecentOrders = new ObservableCollection<OrderItem>(orders);
    }

    private async Task LoadLowStockItemsAsync(Guid stationId)
    {
        var items = await _inventoryService.GetLowStockItemsAsync(stationId);
        LowStockItems = new ObservableCollection<InventoryItem>(items);
    }

    private async Task LoadActiveStaffAsync(Guid stationId)
    {
        var staff = await _staffService.GetActiveStaffAsync(stationId);
        ActiveStaff = new ObservableCollection<StaffMember>(staff);
    }

    private async Task ViewOrdersAsync()
    {
        await _navigationService.NavigateToAsync<OrderManagementViewModel>();
    }

    private async Task ViewInventoryAsync()
    {
        await _navigationService.NavigateToAsync<InventoryManagementViewModel>();
    }

    private async Task ViewStaffAsync()
    {
        await _navigationService.NavigateToAsync<StaffManagementViewModel>();
    }

    private async Task ViewReportsAsync()
    {
        await _navigationService.NavigateToAsync<ReportsViewModel>();
    }
}
```

#### **Day 4-5: Service Layer Implementation**
```csharp
// StationApp/Services/StationService.cs
public class StationService : IStationService
{
    private readonly IApiService _apiService;
    private readonly ICacheService _cacheService;
    private readonly IWebSocketService _webSocketService;
    private readonly ILogger<StationService> _logger;

    public StationService(
        IApiService apiService,
        ICacheService cacheService,
        IWebSocketService webSocketService,
        ILogger<StationService> logger)
    {
        _apiService = apiService;
        _cacheService = cacheService;
        _webSocketService = webSocketService;
        _logger = logger;
    }

    public async Task<StationInfo> GetStationInfoAsync(Guid stationId)
    {
        try
        {
            // Try to get from cache first
            var cacheKey = $"station_{stationId}";
            var cachedStation = await _cacheService.GetAsync<StationInfo>(cacheKey);
            
            if (cachedStation != null)
            {
                return cachedStation;
            }

            // Get from API
            var station = await _apiService.GetAsync<StationInfo>($"/api/stations/{stationId}");
            
            // Cache for 30 minutes
            await _cacheService.SetAsync(cacheKey, station, TimeSpan.FromMinutes(30));
            
            return station;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting station info for {StationId}", stationId);
            throw;
        }
    }

    public async Task<List<StationInfo>> GetAllStationsAsync()
    {
        try
        {
            var stations = await _apiService.GetAsync<List<StationInfo>>("/api/stations");
            return stations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all stations");
            throw;
        }
    }

    public async Task<StationInfo> UpdateStationInfoAsync(StationInfo stationInfo)
    {
        try
        {
            var updatedStation = await _apiService.PutAsync<StationInfo>($"/api/stations/{stationInfo.Id}", stationInfo);
            
            // Update cache
            var cacheKey = $"station_{stationInfo.Id}";
            await _cacheService.SetAsync(cacheKey, updatedStation, TimeSpan.FromMinutes(30));
            
            // Send WebSocket update
            await _webSocketService.SendMessageAsync(new StationUpdate
            {
                StationId = stationInfo.Id,
                Type = StationUpdateType.InfoUpdated,
                Data = updatedStation,
                UpdatedAt = DateTime.UtcNow
            });
            
            return updatedStation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating station info for {StationId}", stationInfo.Id);
            throw;
        }
    }

    public async Task<StationPerformance> GetStationPerformanceAsync(Guid stationId, DateTime date)
    {
        try
        {
            var cacheKey = $"station_performance_{stationId}_{date:yyyy-MM-dd}";
            var cachedPerformance = await _cacheService.GetAsync<StationPerformance>(cacheKey);
            
            if (cachedPerformance != null)
            {
                return cachedPerformance;
            }

            var performance = await _apiService.GetAsync<StationPerformance>(
                $"/api/stations/{stationId}/performance?date={date:yyyy-MM-dd}");
            
            // Cache for 15 minutes
            await _cacheService.SetAsync(cacheKey, performance, TimeSpan.FromMinutes(15));
            
            return performance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting station performance for {StationId} on {Date}", stationId, date);
            throw;
        }
    }

    public async Task<List<StationPerformance>> GetStationPerformanceRangeAsync(Guid stationId, DateTime startDate, DateTime endDate)
    {
        try
        {
            var performance = await _apiService.GetAsync<List<StationPerformance>>(
                $"/api/stations/{stationId}/performance?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");
            
            return performance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting station performance range for {StationId}", stationId);
            throw;
        }
    }

    public async Task<bool> UpdateStationStatusAsync(Guid stationId, StationStatus status)
    {
        try
        {
            var success = await _apiService.PostAsync<bool>($"/api/stations/{stationId}/status", new { status });
            
            if (success)
            {
                // Send WebSocket update
                await _webSocketService.SendMessageAsync(new StationUpdate
                {
                    StationId = stationId,
                    Type = StationUpdateType.StatusChanged,
                    Data = new { Status = status },
                    UpdatedAt = DateTime.UtcNow
                });
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating station status for {StationId}", stationId);
            throw;
        }
    }

    public async Task<List<StationSettings>> GetStationSettingsAsync(Guid stationId)
    {
        try
        {
            var settings = await _apiService.GetAsync<List<StationSettings>>($"/api/stations/{stationId}/settings");
            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting station settings for {StationId}", stationId);
            throw;
        }
    }

    public async Task<StationSettings> UpdateStationSettingsAsync(Guid stationId, StationSettings settings)
    {
        try
        {
            var updatedSettings = await _apiService.PutAsync<StationSettings>($"/api/stations/{stationId}/settings", settings);
            
            // Send WebSocket update
            await _webSocketService.SendMessageAsync(new StationUpdate
            {
                StationId = stationId,
                Type = StationUpdateType.SettingsUpdated,
                Data = updatedSettings,
                UpdatedAt = DateTime.UtcNow
            });
            
            return updatedSettings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating station settings for {StationId}", stationId);
            throw;
        }
    }
}

// StationApp/Services/OrderService.cs
public class OrderService : IOrderService
{
    private readonly IApiService _apiService;
    private readonly IWebSocketService _webSocketService;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IApiService apiService,
        IWebSocketService webSocketService,
        ILogger<OrderService> logger)
    {
        _apiService = apiService;
        _webSocketService = webSocketService;
        _logger = logger;
    }

    public async Task<List<OrderItem>> GetStationOrdersAsync(Guid stationId)
    {
        try
        {
            var orders = await _apiService.GetAsync<List<OrderItem>>($"/api/orders/station/{stationId}");
            return orders.OrderByDescending(o => o.CreatedAt).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting station orders for {StationId}", stationId);
            throw;
        }
    }

    public async Task<List<OrderItem>> GetRecentOrdersAsync(Guid stationId, int count)
    {
        try
        {
            var orders = await _apiService.GetAsync<List<OrderItem>>($"/api/orders/station/{stationId}/recent?count={count}");
            return orders;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent orders for {StationId}", stationId);
            throw;
        }
    }

    public async Task<OrderItem> GetOrderAsync(Guid orderId)
    {
        try
        {
            var order = await _apiService.GetAsync<OrderItem>($"/api/orders/{orderId}");
            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order {OrderId}", orderId);
            throw;
        }
    }

    public async Task<OrderItem> UpdateOrderStatusAsync(Guid orderId, OrderStatus status)
    {
        try
        {
            var updateRequest = new OrderStatusUpdate
            {
                OrderId = orderId,
                Status = status,
                UpdatedAt = DateTime.UtcNow
            };

            var updatedOrder = await _apiService.PutAsync<OrderItem>($"/api/orders/{orderId}/status", updateRequest);
            
            _logger.LogInformation("Order {OrderId} status updated to {Status}", orderId, status);
            
            return updatedOrder;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order status for {OrderId}", orderId);
            throw;
        }
    }

    public async Task<OrderItem> UpdateOrderItemAsync(OrderItem order)
    {
        try
        {
            var updatedOrder = await _apiService.PutAsync<OrderItem>($"/api/orders/{order.Id}", order);
            
            _logger.LogInformation("Order {OrderId} updated", order.Id);
            
            return updatedOrder;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order {OrderId}", order.Id);
            throw;
        }
    }

    public async Task<bool> CancelOrderAsync(Guid orderId, string reason)
    {
        try
        {
            var cancelRequest = new CancelOrderRequest
            {
                OrderId = orderId,
                Reason = reason,
                CancelledAt = DateTime.UtcNow
            };

            var success = await _apiService.PostAsync<bool>($"/api/orders/{orderId}/cancel", cancelRequest);
            
            if (success)
            {
                _logger.LogInformation("Order {OrderId} cancelled", orderId);
                
                // Send WebSocket update
                await _webSocketService.SendMessageAsync(new OrderUpdate
                {
                    OrderId = orderId,
                    Type = OrderUpdateType.Cancelled,
                    Reason = reason,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling order {OrderId}", orderId);
            throw;
        }
    }

    public async Task<List<OrderItem>> GetOrdersByStatusAsync(Guid stationId, OrderStatus status)
    {
        try
        {
            var orders = await _apiService.GetAsync<List<OrderItem>>($"/api/orders/station/{stationId}/status/{status}");
            return orders;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting orders by status {Status} for station {StationId}", status, stationId);
            throw;
        }
    }

    public async Task<List<OrderItem>> GetOrdersByDateRangeAsync(Guid stationId, DateTime startDate, DateTime endDate)
    {
        try
        {
            var orders = await _apiService.GetAsync<List<OrderItem>>(
                $"/api/orders/station/{stationId}/range?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");
            
            return orders;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting orders by date range for station {StationId}", stationId);
            throw;
        }
    }

    public async Task<OrderStatistics> GetOrderStatisticsAsync(Guid stationId, DateTime date)
    {
        try
        {
            var statistics = await _apiService.GetAsync<OrderStatistics>(
                $"/api/orders/station/{stationId}/statistics?date={date:yyyy-MM-dd}");
            
            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order statistics for station {StationId}", stationId);
            throw;
        }
    }
}

// StationApp/Services/InventoryService.cs
public class InventoryService : IInventoryService
{
    private readonly IApiService _apiService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(
        IApiService apiService,
        ICacheService cacheService,
        ILogger<InventoryService> logger)
    {
        _apiService = apiService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<List<InventoryItem>> GetStationInventoryAsync(Guid stationId)
    {
        try
        {
            var cacheKey = $"station_inventory_{stationId}";
            var cachedInventory = await _cacheService.GetAsync<List<InventoryItem>>(cacheKey);
            
            if (cachedInventory != null)
            {
                return cachedInventory;
            }

            var inventory = await _apiService.GetAsync<List<InventoryItem>>($"/api/inventory/station/{stationId}");
            
            // Cache for 10 minutes
            await _cacheService.SetAsync(cacheKey, inventory, TimeSpan.FromMinutes(10));
            
            return inventory;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting station inventory for {StationId}", stationId);
            throw;
        }
    }

    public async Task<List<InventoryItem>> GetLowStockItemsAsync(Guid stationId)
    {
        try
        {
            var items = await _apiService.GetAsync<List<InventoryItem>>($"/api/inventory/station/{stationId}/lowstock");
            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting low stock items for station {StationId}", stationId);
            throw;
        }
    }

    public async Task<InventoryItem> UpdateInventoryItemAsync(InventoryItem item)
    {
        try
        {
            var updatedItem = await _apiService.PutAsync<InventoryItem>($"/api/inventory/{item.Id}", item);
            
            // Update cache
            await InvalidateStationInventoryCache(item.StationId);
            
            _logger.LogInformation("Inventory item {ItemId} updated", item.Id);
            
            return updatedItem;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating inventory item {ItemId}", item.Id);
            throw;
        }
    }

    public async Task<InventoryItem> AddInventoryItemAsync(InventoryItem item)
    {
        try
        {
            var addedItem = await _apiService.PostAsync<InventoryItem>("/api/inventory", item);
            
            // Update cache
            await InvalidateStationInventoryCache(item.StationId);
            
            _logger.LogInformation("Inventory item {ItemId} added", addedItem.Id);
            
            return addedItem;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding inventory item");
            throw;
        }
    }

    public async Task<bool> DeleteInventoryItemAsync(Guid itemId)
    {
        try
        {
            var success = await _apiService.DeleteAsync<bool>($"/api/inventory/{itemId}");
            
            if (success)
            {
                _logger.LogInformation("Inventory item {ItemId} deleted", itemId);
                
                // Update cache
                // Note: We would need to know the stationId to invalidate cache
                // This could be improved by passing stationId as a parameter
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting inventory item {ItemId}", itemId);
            throw;
        }
    }

    public async Task<bool> AdjustInventoryAsync(Guid itemId, int quantity, string reason)
    {
        try
        {
            var adjustmentRequest = new InventoryAdjustment
            {
                ItemId = itemId,
                Quantity = quantity,
                Reason = reason,
                AdjustedAt = DateTime.UtcNow
            };

            var success = await _apiService.PostAsync<bool>($"/api/inventory/{itemId}/adjust", adjustmentRequest);
            
            if (success)
            {
                _logger.LogInformation("Inventory item {ItemId} adjusted by {Quantity}", itemId, quantity);
                
                // Update cache
                await InvalidateInventoryItemCache(itemId);
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adjusting inventory item {ItemId}", itemId);
            throw;
        }
    }

    public async Task<List<InventoryTransaction>> GetInventoryTransactionsAsync(Guid stationId, DateTime startDate, DateTime endDate)
    {
        try
        {
            var transactions = await _apiService.GetAsync<List<InventoryTransaction>>(
                $"/api/inventory/station/{stationId}/transactions?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");
            
            return transactions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting inventory transactions for station {StationId}", stationId);
            throw;
        }
    }

    private async Task InvalidateStationInventoryCache(Guid stationId)
    {
        var cacheKey = $"station_inventory_{stationId}";
        await _cacheService.RemoveAsync(cacheKey);
    }

    private async Task InvalidateInventoryItemCache(Guid itemId)
    {
        var cacheKey = $"inventory_item_{itemId}";
        await _cacheService.RemoveAsync(cacheKey);
    }
}
```

### **3.2 Phase 2: Station Features Implementation (Week 3-4)**

#### **Day 8-10: Order Management System**
```csharp
// StationApp/ViewModels/OrderManagementViewModel.cs
public class OrderManagementViewModel : BaseViewModel
{
    private readonly IOrderService _orderService;
    private readonly IStationService _stationService;

    private ObservableCollection<OrderItem> _orders;
    private ObservableCollection<OrderItem> _filteredOrders;
    private OrderItem _selectedOrder;
    private OrderStatus _selectedStatus;
    private string _searchText;
    private DateTime _filterStartDate;
    private DateTime _filterEndDate;

    public ObservableCollection<OrderItem> Orders
    {
        get => _orders;
        set => SetProperty(ref _orders, value);
    }

    public ObservableCollection<OrderItem> FilteredOrders
    {
        get => _filteredOrders;
        set => SetProperty(ref _filteredOrders, value);
    }

    public OrderItem SelectedOrder
    {
        get => _selectedOrder;
        set
        {
            if (SetProperty(ref _selectedOrder, value))
            {
                _ = LoadOrderDetailsAsync(value);
            }
        }
    }

    public OrderStatus SelectedStatus
    {
        get => _selectedStatus;
        set
        {
            if (SetProperty(ref _selectedStatus, value))
            {
                _ = FilterOrdersAsync();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _ = FilterOrdersAsync();
            }
        }
    }

    public DateTime FilterStartDate
    {
        get => _filterStartDate;
        set
        {
            if (SetProperty(ref _filterStartDate, value))
            {
                _ = FilterOrdersAsync();
            }
        }
    }

    public DateTime FilterEndDate
    {
        get => _filterEndDate;
        set
        {
            if (SetProperty(ref _filterEndDate, value))
            {
                _ = FilterOrdersAsync();
            }
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand UpdateStatusCommand { get; }
    public ICommand CancelOrderCommand { get; }
    public ICommand ViewDetailsCommand { get; }
    public ICommand PrintOrderCommand { get; }
    public ICommand ClearFiltersCommand { get; }

    public OrderManagementViewModel(
        INavigationService navigationService,
        IDialogService dialogService,
        IApiService apiService,
        IAuthenticationService authService,
        IWebSocketService webSocketService,
        IOrderService orderService,
        IStationService stationService)
        : base(navigationService, dialogService, apiService, authService, webSocketService)
    {
        _orderService = orderService;
        _stationService = stationService;

        RefreshCommand = new RelayCommand(async () => await LoadOrdersAsync());
        UpdateStatusCommand = new RelayCommand<OrderStatus>(async (status) => await UpdateOrderStatusAsync(status));
        CancelOrderCommand = new RelayCommand(async () => await CancelSelectedOrderAsync());
        ViewDetailsCommand = new RelayCommand<OrderItem>(async (order) => await ViewOrderDetailsAsync(order));
        PrintOrderCommand = new RelayCommand<OrderItem>(async (order) => await PrintOrderAsync(order));
        ClearFiltersCommand = new RelayCommand(async () => await ClearFiltersAsync());

        Title = "Order Management";
        
        // Initialize collections
        Orders = new ObservableCollection<OrderItem>();
        FilteredOrders = new ObservableCollection<OrderItem>();
        
        // Initialize filters
        SelectedStatus = OrderStatus.All;
        FilterStartDate = DateTime.Today.AddDays(-7);
        FilterEndDate = DateTime.Today;
    }

    public async Task InitializeAsync()
    {
        await LoadOrdersAsync();
        await SubscribeToOrderUpdatesAsync();
    }

    private async Task LoadOrdersAsync()
    {
        await ExecuteAsync(async () =>
        {
            var stationId = await _authService.GetCurrentStationIdAsync();
            var orders = await _orderService.GetStationOrdersAsync(stationId);
            
            Orders = new ObservableCollection<OrderItem>(orders);
            await FilterOrdersAsync();
        }, "Loading orders...");
    }

    private async Task SubscribeToOrderUpdatesAsync()
    {
        await _webSocketService.SubscribeToAsync<OrderUpdate>("order-updates", async (update) =>
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await HandleOrderUpdateAsync(update);
            });
        });
    }

    private async Task HandleOrderUpdateAsync(OrderUpdate update)
    {
        var order = update.Order;
        
        // Find and update existing order
        var existingOrder = Orders.FirstOrDefault(o => o.Id == order.Id);
        if (existingOrder != null)
        {
            var index = Orders.IndexOf(existingOrder);
            Orders[index] = order;
        }
        else
        {
            // Add new order
            Orders.Insert(0, order);
        }

        // Apply filters
        await FilterOrdersAsync();

        // Show notification for important updates
        if (update.Type == OrderUpdateType.StatusChanged)
        {
            await ShowOrderNotificationAsync(order);
        }
    }

    private async Task FilterOrdersAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var filtered = Orders.AsEnumerable();

            // Filter by status
            if (SelectedStatus != OrderStatus.All)
            {
                filtered = filtered.Where(o => o.Status == SelectedStatus);
            }

            // Filter by search text
            if (!string.IsNullOrEmpty(SearchText))
            {
                filtered = filtered.Where(o => 
                    o.OrderNumber.ToString().Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    o.CustomerName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            // Filter by date range
            filtered = filtered.Where(o => o.CreatedAt.Date >= FilterStartDate.Date && o.CreatedAt.Date <= FilterEndDate.Date);

            FilteredOrders = new ObservableCollection<OrderItem>(filtered.ToList());
        });
    }

    private async Task LoadOrderDetailsAsync(OrderItem order)
    {
        if (order == null) return;

        await ExecuteAsync(async () =>
        {
            var detailedOrder = await _orderService.GetOrderAsync(order.Id);
            
            // Update the order in the collection
            var index = Orders.IndexOf(order);
            if (index >= 0)
            {
                Orders[index] = detailedOrder;
            }
        }, "Loading order details...");
    }

    private async Task UpdateOrderStatusAsync(OrderStatus status)
    {
        if (SelectedOrder == null) return;

        await ExecuteAsync(async () =>
        {
            var stationId = await _authService.GetCurrentStationIdAsync();
            var updatedOrder = await _orderService.UpdateOrderStatusAsync(SelectedOrder.Id, status);
            
            // Send WebSocket update
            await SendWebSocketMessageAsync(new OrderStatusUpdate
            {
                OrderId = SelectedOrder.Id,
                Status = status,
                StationId = stationId,
                UpdatedBy = await _authService.GetCurrentUserIdAsync(),
                UpdatedAt = DateTime.UtcNow
            });

            await ShowSuccessAsync($"Order status updated to {status}");
        }, "Updating order status...");
    }

    private async Task CancelSelectedOrderAsync()
    {
        if (SelectedOrder == null) return;

        var confirmed = await ShowConfirmationAsync($"Are you sure you want to cancel order #{SelectedOrder.OrderNumber}?", "Cancel Order");
        if (!confirmed) return;

        await ExecuteAsync(async () =>
        {
            var reason = await _dialogService.PromptAsync("Please provide a reason for cancellation:", "Cancellation Reason");
            if (string.IsNullOrEmpty(reason)) return;

            var success = await _orderService.CancelOrderAsync(SelectedOrder.Id, reason);
            
            if (success)
            {
                await ShowSuccessAsync("Order cancelled successfully");
            }
        }, "Cancelling order...");
    }

    private async Task ViewOrderDetailsAsync(OrderItem order)
    {
        var parameters = new Dictionary<string, object>
        {
            { "order", order }
        };

        await _navigationService.NavigateToAsync<OrderDetailsViewModel>(parameters);
    }

    private async Task PrintOrderAsync(OrderItem order)
    {
        await ExecuteAsync(async () =>
        {
            // This would integrate with a printer service
            await ShowSuccessAsync($"Order #{order.OrderNumber} printed successfully");
        }, "Printing order...");
    }

    private async Task ClearFiltersAsync()
    {
        SelectedStatus = OrderStatus.All;
        SearchText = string.Empty;
        FilterStartDate = DateTime.Today.AddDays(-7);
        FilterEndDate = DateTime.Today;
    }

    private async Task ShowOrderNotificationAsync(OrderItem order)
    {
        var message = order.Status switch
        {
            OrderStatus.Pending => $"New order #{order.OrderNumber} received",
            OrderStatus.Preparing => $"Order #{order.OrderNumber} is being prepared",
            OrderStatus.Ready => $"Order #{order.OrderNumber} is ready",
            OrderStatus.Completed => $"Order #{order.OrderNumber} completed",
            OrderStatus.Cancelled => $"Order #{order.OrderNumber} cancelled",
            _ => $"Order #{order.OrderNumber} updated"
        };

        await _dialogService.ShowToastAsync(message);
    }
}

// StationApp/ViewModels/InventoryManagementViewModel.cs
public class InventoryManagementViewModel : BaseViewModel
{
    private readonly IInventoryService _inventoryService;

    private ObservableCollection<InventoryItem> _inventoryItems;
    private ObservableCollection<InventoryItem> _lowStockItems;
    private InventoryItem _selectedItem;
    private string _searchText;

    public ObservableCollection<InventoryItem> InventoryItems
    {
        get => _inventoryItems;
        set => SetProperty(ref _inventoryItems, value);
    }

    public ObservableCollection<InventoryItem> LowStockItems
    {
        get => _lowStockItems;
        set => SetProperty(ref _lowStockItems, value);
    }

    public InventoryItem SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _ = FilterInventoryAsync();
            }
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand AddItemCommand { get; }
    public ICommand EditItemCommand { get; }
    public ICommand DeleteItemCommand { get; }
    public ICommand AdjustInventoryCommand { get; }
    public ICommand ViewTransactionsCommand { get; }

    public InventoryManagementViewModel(
        INavigationService navigationService,
        IDialogService dialogService,
        IApiService apiService,
        IAuthenticationService authService,
        IWebSocketService webSocketService,
        IInventoryService inventoryService)
        : base(navigationService, dialogService, apiService, authService, webSocketService)
    {
        _inventoryService = inventoryService;

        RefreshCommand = new RelayCommand(async () => await LoadInventoryAsync());
        AddItemCommand = new RelayCommand(async () => await AddItemAsync());
        EditItemCommand = new RelayCommand<InventoryItem>(async (item) => await EditItemAsync(item));
        DeleteItemCommand = new RelayCommand<InventoryItem>(async (item) => await DeleteItemAsync(item));
        AdjustInventoryCommand = new RelayCommand<InventoryItem>(async (item) => await AdjustInventoryAsync(item));
        ViewTransactionsCommand = new RelayCommand<InventoryItem>(async (item) => await ViewTransactionsAsync(item));

        Title = "Inventory Management";
        
        // Initialize collections
        InventoryItems = new ObservableCollection<InventoryItem>();
        LowStockItems = new ObservableCollection<InventoryItem>();
    }

    public async Task InitializeAsync()
    {
        await LoadInventoryAsync();
    }

    private async Task LoadInventoryAsync()
    {
        await ExecuteAsync(async () =>
        {
            var stationId = await _authService.GetCurrentStationIdAsync();
            
            await Task.WhenAll(
                LoadStationInventoryAsync(stationId),
                LoadLowStockItemsAsync(stationId)
            );
        }, "Loading inventory...");
    }

    private async Task LoadStationInventoryAsync(Guid stationId)
    {
        var items = await _inventoryService.GetStationInventoryAsync(stationId);
        InventoryItems = new ObservableCollection<InventoryItem>(items);
    }

    private async Task LoadLowStockItemsAsync(Guid stationId)
    {
        var items = await _inventoryService.GetLowStockItemsAsync(stationId);
        LowStockItems = new ObservableCollection<InventoryItem>(items);
    }

    private async Task FilterInventoryAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var filtered = InventoryItems.AsEnumerable();

            if (!string.IsNullOrEmpty(SearchText))
            {
                filtered = filtered.Where(item => 
                    item.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    item.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            // Update the collection in place
            var filteredList = filtered.ToList();
            
            // Remove items not in filtered list
            for (int i = InventoryItems.Count - 1; i >= 0; i--)
            {
                if (!filteredList.Contains(InventoryItems[i]))
                {
                    InventoryItems.RemoveAt(i);
                }
            }

            // Add items that are in filtered list but not in collection
            foreach (var item in filteredList)
            {
                if (!InventoryItems.Contains(item))
                {
                    InventoryItems.Add(item);
                }
            }
        });
    }

    private async Task AddItemAsync()
    {
        await _navigationService.NavigateToAsync<InventoryItemViewModel>();
    }

    private async Task EditItemAsync(InventoryItem item)
    {
        var parameters = new Dictionary<string, object>
        {
            { "inventoryItem", item }
        };

        await _navigationService.NavigateToAsync<InventoryItemViewModel>(parameters);
    }

    private async Task DeleteItemAsync(InventoryItem item)
    {
        var confirmed = await ShowConfirmationAsync($"Are you sure you want to delete {item.Name}?", "Delete Item");
        if (!confirmed) return;

        await ExecuteAsync(async () =>
        {
            var success = await _inventoryService.DeleteInventoryItemAsync(item.Id);
            
            if (success)
            {
                InventoryItems.Remove(item);
                await ShowSuccessAsync("Item deleted successfully");
            }
        }, "Deleting item...");
    }

    private async Task AdjustInventoryAsync(InventoryItem item)
    {
        var parameters = new Dictionary<string, object>
        {
            { "inventoryItem", item }
        };

        await _navigationService.NavigateToAsync<InventoryAdjustmentViewModel>(parameters);
    }

    private async Task ViewTransactionsAsync(InventoryItem item)
    {
        var parameters = new Dictionary<string, object>
        {
            { "inventoryItem", item }
        };

        await _navigationService.NavigateToAsync<InventoryTransactionsViewModel>(parameters);
    }
}
```

#### **Day 11-12: Staff Management**
```csharp
// StationApp/ViewModels/StaffManagementViewModel.cs
public class StaffManagementViewModel : BaseViewModel
{
    private readonly IStaffService _staffService;

    private ObservableCollection<StaffMember> _staffMembers;
    private ObservableCollection<StaffMember> _activeStaff;
    private ObservableCollection<StaffMember> _onBreakStaff;
    private StaffMember _selectedStaff;
    private string _searchText;

    public ObservableCollection<StaffMember> StaffMembers
    {
        get => _staffMembers;
        set => SetProperty(ref _staffMembers, value);
    }

    public ObservableCollection<StaffMember> ActiveStaff
    {
        get => _activeStaff;
        set => SetProperty(ref _activeStaff, value);
    }

    public ObservableCollection<StaffMember> OnBreakStaff
    {
        get => _onBreakStaff;
        set => SetProperty(ref _onBreakStaff, value);
    }

    public StaffMember SelectedStaff
    {
        get => _selectedStaff;
        set => SetProperty(ref _selectedStaff, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _ = FilterStaffAsync();
            }
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand AddStaffCommand { get; }
    public ICommand EditStaffCommand { get; }
    public ICommand DeleteStaffCommand { get; }
    public ICommand ClockInCommand { get; }
    public ICommand ClockOutCommand { get; }
    public ICommand StartBreakCommand { get; }
    public ICommand EndBreakCommand { get; }

    public StaffManagementViewModel(
        INavigationService navigationService,
        IDialogService dialogService,
        IApiService apiService,
        IAuthenticationService authService,
        IWebSocketService webSocketService,
        IStaffService staffService)
        : base(navigationService, dialogService, apiService, authService, webSocketService)
    {
        _staffService = staffService;

        RefreshCommand = new RelayCommand(async () => await LoadStaffAsync());
        AddStaffCommand = new RelayCommand(async () => await AddStaffAsync());
        EditStaffCommand = new RelayCommand<StaffMember>(async (staff) => await EditStaffAsync(staff));
        DeleteStaffCommand = new RelayCommand<StaffMember>(async (staff) => await DeleteStaffAsync(staff));
        ClockInCommand = new RelayCommand<StaffMember>(async (staff) => await ClockInAsync(staff));
        ClockOutCommand = new RelayCommand<StaffMember>(async (staff) => await ClockOutAsync(staff));
        StartBreakCommand = new RelayCommand<StaffMember>(async (staff) => await StartBreakAsync(staff));
        EndBreakCommand = new RelayCommand<StaffMember>(async (staff) => await EndBreakAsync(staff));

        Title = "Staff Management";
        
        // Initialize collections
        StaffMembers = new ObservableCollection<StaffMember>();
        ActiveStaff = new ObservableCollection<StaffMember>();
        OnBreakStaff = new ObservableCollection<StaffMember>();
    }

    public async Task InitializeAsync()
    {
        await LoadStaffAsync();
        await SubscribeToStaffUpdatesAsync();
    }

    private async Task LoadStaffAsync()
    {
        await ExecuteAsync(async () =>
        {
            var stationId = await _authService.GetCurrentStationIdAsync();
            
            await Task.WhenAll(
                LoadAllStaffAsync(stationId),
                LoadActiveStaffAsync(stationId)
            );
        }, "Loading staff...");
    }

    private async Task LoadAllStaffAsync(Guid stationId)
    {
        var staff = await _staffService.GetStationStaffAsync(stationId);
        StaffMembers = new ObservableCollection<StaffMember>(staff);
    }

    private async Task LoadActiveStaffAsync(Guid stationId)
    {
        var activeStaff = await _staffService.GetActiveStaffAsync(stationId);
        ActiveStaff = new ObservableCollection<StaffMember>(activeStaff);
        
        // Categorize staff
        await CategorizeStaffAsync();
    }

    private async Task SubscribeToStaffUpdatesAsync()
    {
        await _webSocketService.SubscribeToAsync<StaffUpdate>("staff-updates", async (update) =>
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await HandleStaffUpdateAsync(update);
            });
        });
    }

    private async Task HandleStaffUpdateAsync(StaffUpdate update)
    {
        var staff = update.StaffMember;
        
        // Find and update existing staff
        var existingStaff = StaffMembers.FirstOrDefault(s => s.Id == staff.Id);
        if (existingStaff != null)
        {
            var index = StaffMembers.IndexOf(existingStaff);
            StaffMembers[index] = staff;
        }
        else
        {
            // Add new staff
            StaffMembers.Insert(0, staff);
        }

        // Update active staff collections
        await CategorizeStaffAsync();

        // Show notification for important updates
        if (update.Type == StaffUpdateType.StatusChanged)
        {
            await ShowStaffNotificationAsync(staff);
        }
    }

    private async Task CategorizeStaffAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var active = ActiveStaff.Where(s => s.Status == StaffStatus.Active).ToList();
            var onBreak = ActiveStaff.Where(s => s.Status == StaffStatus.OnBreak).ToList();

            ActiveStaff = new ObservableCollection<StaffMember>(active);
            OnBreakStaff = new ObservableCollection<StaffMember>(onBreak);
        });
    }

    private async Task FilterStaffAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var filtered = StaffMembers.AsEnumerable();

            if (!string.IsNullOrEmpty(SearchText))
            {
                filtered = filtered.Where(staff => 
                    staff.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    staff.Role.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            // Update the collection in place
            var filteredList = filtered.ToList();
            
            // Remove items not in filtered list
            for (int i = StaffMembers.Count - 1; i >= 0; i--)
            {
                if (!filteredList.Contains(StaffMembers[i]))
                {
                    StaffMembers.RemoveAt(i);
                }
            }

            // Add items that are in filtered list but not in collection
            foreach (var staff in filteredList)
            {
                if (!StaffMembers.Contains(staff))
                {
                    StaffMembers.Add(staff);
                }
            }
        });
    }

    private async Task AddStaffAsync()
    {
        await _navigationService.NavigateToAsync<StaffMemberViewModel>();
    }

    private async Task EditStaffAsync(StaffMember staff)
    {
        var parameters = new Dictionary<string, object>
        {
            { "staffMember", staff }
        };

        await _navigationService.NavigateToAsync<StaffMemberViewModel>(parameters);
    }

    private async Task DeleteStaffAsync(StaffMember staff)
    {
        var confirmed = await ShowConfirmationAsync($"Are you sure you want to delete {staff.Name}?", "Delete Staff");
        if (!confirmed) return;

        await ExecuteAsync(async () =>
        {
            var success = await _staffService.DeleteStaffMemberAsync(staff.Id);
            
            if (success)
            {
                StaffMembers.Remove(staff);
                await ShowSuccessAsync("Staff member deleted successfully");
            }
        }, "Deleting staff member...");
    }

    private async Task ClockInAsync(StaffMember staff)
    {
        await ExecuteAsync(async () =>
        {
            var stationId = await _authService.GetCurrentStationIdAsync();
            var updatedStaff = await _staffService.ClockInAsync(staff.Id, stationId);
            
            // Send WebSocket update
            await SendWebSocketMessageAsync(new StaffUpdate
            {
                StaffMember = updatedStaff,
                Type = StaffUpdateType.ClockedIn,
                StationId = stationId,
                UpdatedAt = DateTime.UtcNow
            });

            await ShowSuccessAsync($"{staff.Name} clocked in successfully");
        }, "Clocking in...");
    }

    private async Task ClockOutAsync(StaffMember staff)
    {
        await ExecuteAsync(async () =>
        {
            var stationId = await _authService.GetCurrentStationIdAsync();
            var updatedStaff = await _staffService.ClockOutAsync(staff.Id, stationId);
            
            // Send WebSocket update
            await SendWebSocketMessageAsync(new StaffUpdate
            {
                StaffMember = updatedStaff,
                Type = StaffUpdateType.ClockedOut,
                StationId = stationId,
                UpdatedAt = DateTime.UtcNow
            });

            await ShowSuccessAsync($"{staff.Name} clocked out successfully");
        }, "Clocking out...");
    }

    private async Task StartBreakAsync(StaffMember staff)
    {
        await ExecuteAsync(async () =>
        {
            var stationId = await _authService.GetCurrentStationIdAsync();
            var updatedStaff = await _staffService.StartBreakAsync(staff.Id, stationId);
            
            // Send WebSocket update
            await SendWebSocketMessageAsync(new StaffUpdate
            {
                StaffMember = updatedStaff,
                Type = StaffUpdateType.BreakStarted,
                StationId = stationId,
                UpdatedAt = DateTime.UtcNow
            });

            await ShowSuccessAsync($"{staff.Name} started break");
        }, "Starting break...");
    }

    private async Task EndBreakAsync(StaffMember staff)
    {
        await ExecuteAsync(async () =>
        {
            var stationId = await _authService.GetCurrentStationIdAsync();
            var updatedStaff = await _staffService.EndBreakAsync(staff.Id, stationId);
            
            // Send WebSocket update
            await SendWebSocketMessageAsync(new StaffUpdate
            {
                StaffMember = updatedStaff,
                Type = StaffUpdateType.BreakEnded,
                StationId = stationId,
                UpdatedAt = DateTime.UtcNow
            });

            await ShowSuccessAsync($"{staff.Name} ended break");
        }, "Ending break...");
    }

    private async Task ShowStaffNotificationAsync(StaffMember staff)
    {
        var message = staff.Status switch
        {
            StaffStatus.Active => $"{staff.Name} is now active",
            StaffStatus.OnBreak => $"{staff.Name} is on break",
            StaffStatus.Offline => $"{staff.Name} is offline",
            _ => $"{staff.Name} status updated"
        };

        await _dialogService.ShowToastAsync(message);
    }
}
```

### **3.3 Phase 3: Advanced Features (Week 5-6)**

#### **Day 13-15: Real-time WebSocket Integration**
```csharp
// StationApp/Services/WebSocketService.cs
public class WebSocketService : IWebSocketService
{
    private readonly ILogger<WebSocketService> _logger;
    private readonly IAuthenticationService _authService;
    private ClientWebSocket _webSocket;
    private CancellationTokenSource _cancellationTokenSource;
    private readonly Dictionary<string, List<Action<object>>> _subscriptions = new();

    public event Action<bool> ConnectionChanged;

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public WebSocketService(
        ILogger<WebSocketService> logger,
        IAuthenticationService authService)
    {
        _logger = logger;
        _authService = authService;
    }

    public async Task ConnectAsync()
    {
        try
        {
            if (IsConnected)
                return;

            _cancellationTokenSource = new CancellationTokenSource();
            _webSocket = new ClientWebSocket();

            // Add authentication header
            var token = await _authService.GetAuthTokenAsync();
            _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {token}");

            // Connect to WebSocket server
            var uri = new Uri("wss://localhost:5001/ws");
            await _webSocket.ConnectAsync(uri, _cancellationTokenSource.Token);

            _logger.LogInformation("WebSocket connected successfully");

            // Start listening for messages
            _ = Task.Run(ListenForMessagesAsync);

            ConnectionChanged?.Invoke(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting WebSocket");
            ConnectionChanged?.Invoke(false);
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
            }

            _cancellationTokenSource?.Cancel();
            _webSocket?.Dispose();
            _webSocket = null;

            _logger.LogInformation("WebSocket disconnected");
            ConnectionChanged?.Invoke(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting WebSocket");
        }
    }

    public async Task SendMessageAsync<T>(T message)
    {
        try
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("WebSocket is not connected");
            }

            var json = JsonSerializer.Serialize(message);
            var buffer = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, _cancellationTokenSource.Token);

            _logger.LogDebug("WebSocket message sent: {MessageType}", typeof(T).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending WebSocket message");
            throw;
        }
    }

    public async Task SubscribeToAsync<T>(string channel, Action<T> handler)
    {
        try
        {
            // Add subscription
            if (!_subscriptions.ContainsKey(channel))
            {
                _subscriptions[channel] = new List<Action<object>>();
            }

            _subscriptions[channel].Add(obj =>
            {
                if (obj is T typedMessage)
                {
                    handler(typedMessage);
                }
            });

            // Send subscription message
            await SendMessageAsync(new SubscriptionMessage
            {
                Channel = channel,
                Action = "subscribe",
                MessageType = typeof(T).Name
            });

            _logger.LogDebug("Subscribed to WebSocket channel: {Channel}", channel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to WebSocket channel: {Channel}", channel);
            throw;
        }
    }

    public async Task UnsubscribeFromAsync(string channel)
    {
        try
        {
            // Remove subscription
            if (_subscriptions.ContainsKey(channel))
            {
                _subscriptions.Remove(channel);
            }

            // Send unsubscribe message
            await SendMessageAsync(new SubscriptionMessage
            {
                Channel = channel,
                Action = "unsubscribe"
            });

            _logger.LogDebug("Unsubscribed from WebSocket channel: {Channel}", channel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unsubscribing from WebSocket channel: {Channel}", channel);
            throw;
        }
    }

    private async Task ListenForMessagesAsync()
    {
        var buffer = new byte[4096];

        try
        {
            while (_webSocket.State == WebSocketState.Open && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await ProcessMessageAsync(message);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await HandleDisconnectionAsync();
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listening for WebSocket messages");
            await HandleDisconnectionAsync();
        }
    }

    private async Task ProcessMessageAsync(string message)
    {
        try
        {
            var messageObject = JsonSerializer.Deserialize<object>(message);
            var messageDict = JsonSerializer.Deserialize<Dictionary<string, object>>(message);

            if (messageDict.TryGetValue("channel", out var channelValue) &&
                messageDict.TryGetValue("data", out var dataValue) &&
                _subscriptions.TryGetValue(channelValue.ToString(), out var handlers))
            {
                // Notify all subscribers
                foreach (var handler in handlers)
                {
                    try
                    {
                        handler(dataValue);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in WebSocket message handler");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing WebSocket message: {Message}", message);
        }
    }

    private async Task HandleDisconnectionAsync()
    {
        _logger.LogWarning("WebSocket disconnected unexpectedly");
        ConnectionChanged?.Invoke(false);

        // Attempt to reconnect after a delay
        await Task.Delay(5000);
        
        try
        {
            await ConnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reconnecting WebSocket");
        }
    }
}

// StationApp/Services/PrinterService.cs
public class PrinterService : IPrinterService
{
    private readonly ILogger<PrinterService> _logger;
    private readonly ISettingsService _settingsService;

    public PrinterService(
        ILogger<PrinterService> logger,
        ISettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
    }

    public async Task<bool> PrintOrderAsync(OrderItem order)
    {
        try
        {
            var printerSettings = await _settingsService.GetPrinterSettingsAsync();
            
            if (!printerSettings.IsEnabled)
            {
                _logger.LogWarning("Printer is disabled");
                return false;
            }

            // Generate order receipt
            var receipt = GenerateOrderReceipt(order);
            
            // Send to printer
            var success = await SendToPrinterAsync(receipt, printerSettings);
            
            if (success)
            {
                _logger.LogInformation("Order {OrderId} printed successfully", order.Id);
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error printing order {OrderId}", order.Id);
            return false;
        }
    }

    public async Task<bool> PrintKitchenTicketAsync(OrderItem order)
    {
        try
        {
            var printerSettings = await _settingsService.GetPrinterSettingsAsync();
            
            if (!printerSettings.IsEnabled)
            {
                return false;
            }

            // Generate kitchen ticket
            var ticket = GenerateKitchenTicket(order);
            
            // Send to printer
            var success = await SendToPrinterAsync(ticket, printerSettings);
            
            if (success)
            {
                _logger.LogInformation("Kitchen ticket for order {OrderId} printed successfully", order.Id);
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error printing kitchen ticket for order {OrderId}", order.Id);
            return false;
        }
    }

    public async Task<bool> PrintReportAsync(ReportData report)
    {
        try
        {
            var printerSettings = await _settingsService.GetPrinterSettingsAsync();
            
            if (!printerSettings.IsEnabled)
            {
                return false;
            }

            // Generate report
            var reportContent = GenerateReport(report);
            
            // Send to printer
            var success = await SendToPrinterAsync(reportContent, printerSettings);
            
            if (success)
            {
                _logger.LogInformation("Report printed successfully");
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error printing report");
            return false;
        }
    }

    public async Task<List<PrinterInfo>> GetAvailablePrintersAsync()
    {
        try
        {
            // This would use platform-specific APIs to get available printers
            var printers = new List<PrinterInfo>
            {
                new PrinterInfo { Name = "Default Printer", IsDefault = true },
                new PrinterInfo { Name = "Kitchen Printer", IsDefault = false },
                new PrinterInfo { Name = "Receipt Printer", IsDefault = false }
            };

            return printers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available printers");
            return new List<PrinterInfo>();
        }
    }

    private string GenerateOrderReceipt(OrderItem order)
    {
        var receipt = new StringBuilder();
        
        receipt.AppendLine("================================");
        receipt.AppendLine("           ORDER RECEIPT        ");
        receipt.AppendLine("================================");
        receipt.AppendLine($"Order #: {order.OrderNumber}");
        receipt.AppendLine($"Date: {order.CreatedAt:yyyy-MM-dd HH:mm}");
        receipt.AppendLine($"Customer: {order.CustomerName}");
        receipt.AppendLine("================================");
        receipt.AppendLine("ITEMS:");
        
        foreach (var item in order.Items)
        {
            receipt.AppendLine($"{item.Quantity}x {item.Name} - {item.Price:C}");
        }
        
        receipt.AppendLine("================================");
        receipt.AppendLine($"TOTAL: {order.TotalAmount:C}");
        receipt.AppendLine("================================");
        receipt.AppendLine("       Thank you!       ");
        receipt.AppendLine("================================");

        return receipt.ToString();
    }

    private string GenerateKitchenTicket(OrderItem order)
    {
        var ticket = new StringBuilder();
        
        ticket.AppendLine("********************************");
        ticket.AppendLine("         KITCHEN TICKET         ");
        ticket.AppendLine("********************************");
        ticket.AppendLine($"Order #: {order.OrderNumber}");
        ticket.AppendLine($"Time: {order.CreatedAt:HH:mm}");
        ticket.AppendLine($"Table: {order.TableNumber}");
        ticket.AppendLine("********************************");
        ticket.AppendLine("ITEMS TO PREPARE:");
        
        foreach (var item in order.Items)
        {
            ticket.AppendLine($"{item.Quantity}x {item.Name}");
            
            if (!string.IsNullOrEmpty(item.SpecialInstructions))
            {
                ticket.AppendLine($"  Notes: {item.SpecialInstructions}");
            }
        }
        
        ticket.AppendLine("********************************");
        ticket.AppendLine("      PRIORITY: NORMAL      ");
        ticket.AppendLine("********************************");

        return ticket.ToString();
    }

    private string GenerateReport(ReportData report)
    {
        var reportContent = new StringBuilder();
        
        reportContent.AppendLine("================================");
        reportContent.AppendLine($"           {report.Title}        ");
        reportContent.AppendLine("================================");
        reportContent.AppendLine($"Date: {report.GeneratedAt:yyyy-MM-dd HH:mm}");
        reportContent.AppendLine($"Period: {report.StartDate:yyyy-MM-dd} to {report.EndDate:yyyy-MM-dd}");
        reportContent.AppendLine("================================");
        
        foreach (var section in report.Sections)
        {
            reportContent.AppendLine(section.Title);
            reportContent.AppendLine("--------------------------------");
            
            foreach (var item in section.Items)
            {
                reportContent.AppendLine($"{item.Key}: {item.Value}");
            }
            
            reportContent.AppendLine("--------------------------------");
        }
        
        reportContent.AppendLine("================================");

        return reportContent.ToString();
    }

    private async Task<bool> SendToPrinterAsync(string content, PrinterSettings settings)
    {
        try
        {
            // This would use platform-specific printing APIs
            // For demonstration, we'll simulate printing
            
            _logger.LogDebug("Sending to printer: {PrinterName}", settings.PrinterName);
            _logger.LogDebug("Content: {Content}", content);
            
            // Simulate printing delay
            await Task.Delay(1000);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending to printer");
            return false;
        }
    }
}
```

### **3.4 Phase 4: UI/UX Enhancement (Week 7-8)**

#### **Day 18-20: Advanced Station UI**
```xml
<!-- StationApp/Pages/KitchenDisplayPage.xaml -->
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:StationApp.ViewModels"
             xmlns:controls="clr-namespace:StationApp.Controls"
             x:DataType="vm:KitchenDisplayViewModel"
             x:Class="StationApp.Pages.KitchenDisplayPage">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Header -->
        <Frame Grid.Row="0" BackgroundColor="{StaticResource PrimaryColor}" Padding="20">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <StackLayout Grid.Column="0" Spacing="5">
                    <Label Text="Kitchen Display" TextColor="White" FontSize="20" FontAttributes="Bold"/>
                    <Label Text="{Binding StationInfo.Name}" TextColor="White" FontSize="14"/>
                </StackLayout>

                <StackLayout Grid.Column="1" Orientation="Horizontal" Spacing="10" HorizontalOptions="End">
                    <Frame BackgroundColor="{Binding IsConnected, Converter={StaticResource BoolToColorConverter}}" 
                           CornerRadius="25" Padding="10,5">
                        <Label Text="{Binding IsConnected, Converter={StaticResource BoolToStatusConverter}}" 
                               TextColor="White" FontSize="12"/>
                    </Frame>
                </StackLayout>

                <ImageButton Grid.Column="2" 
                             Source="refresh.png" 
                             Command="{Binding RefreshCommand}"
                             BackgroundColor="Transparent"
                             WidthRequest="30" HeightRequest="30"/>
            </Grid>
        </Frame>

        <!-- Main Content -->
        <ScrollView Grid.Row="1">
            <StackLayout Padding="10" Spacing="10">

                <!-- Order Status Tabs -->
                <Frame BackgroundColor="White" CornerRadius="10" Padding="5" HasShadow="True">
                    <ScrollView Orientation="Horizontal">
                        <StackLayout Orientation="Horizontal" Spacing="5">
                            <Button Text="Pending" 
                                    Command="{Binding RefreshCommand}"
                                    BackgroundColor="{StaticResource WarningColor}"
                                    TextColor="White"
                                    CornerRadius="20"
                                    Padding="20,10"/>
                            
                            <StackLayout Orientation="Horizontal" VerticalOptions="Center">
                                <Label Text="Preparing" FontSize="14" TextColor="Gray"/>
                                <Label Text="{Binding PreparingCount}" FontSize="14" FontAttributes="Bold" TextColor="{StaticResource PrimaryColor}"/>
                            </StackLayout>
                            
                            <StackLayout Orientation="Horizontal" VerticalOptions="Center">
                                <Label Text="Ready" FontSize="14" TextColor="Gray"/>
                                <Label Text="{Binding ReadyCount}" FontSize="14" FontAttributes="Bold" TextColor="{StaticResource SuccessColor}"/>
                            </StackLayout>
                        </StackLayout>
                    </ScrollView>
                </Frame>

                <!-- Pending Orders -->
                <Frame BackgroundColor="White" CornerRadius="15" Padding="15" HasShadow="True">
                    <StackLayout Spacing="10">
                        <Label Text="Pending Orders" FontSize="16" FontAttributes="Bold" TextColor="{StaticResource WarningColor}"/>
                        
                        <CollectionView ItemsSource="{Binding PendingOrders}">
                            <CollectionView.ItemTemplate>
                                <DataTemplate>
                                    <Frame BackgroundColor="{StaticResource LightBackgroundColor}" 
                                           CornerRadius="10" Padding="15" Margin="0,5">
                                        <Grid>
                                            <Grid.RowDefinitions>
                                                <RowDefinition Height="Auto"/>
                                                <RowDefinition Height="Auto"/>
                                                <RowDefinition Height="Auto"/>
                                                <RowDefinition Height="Auto"/>
                                            </Grid.RowDefinitions>
                                            <Grid.ColumnDefinitions>
                                                <ColumnDefinition Width="*"/>
                                                <ColumnDefinition Width="Auto"/>
                                            </Grid.ColumnDefinitions>

                                            <Label Grid.Row="0" Grid.Column="0" 
                                                   Text="{Binding OrderNumber, StringFormat='Order #{0}'}" 
                                                   FontSize="16" FontAttributes="Bold"/>
                                            
                                            <Label Grid.Row="0" Grid.Column="1" 
                                                   Text="{Binding CreatedAt, StringFormat='{0:HH:mm}'}" 
                                                   TextColor="Gray" FontSize="12"/>

                                            <Label Grid.Row="1" Grid.Column="0" 
                                                   Text="{Binding CustomerName}" 
                                                   TextColor="Gray" FontSize="14"/>
                                            
                                            <Label Grid.Row="1" Grid.Column="1" 
                                                   Text="{Binding TableNumber, StringFormat='Table {0}'}" 
                                                   TextColor="Gray" FontSize="12"/>

                                            <BoxView Grid.Row="2" Grid.Column="0" Grid.ColumnSpan="2" 
                                                    HeightRequest="1" 
                                                    BackgroundColor="LightGray" 
                                                    Margin="0,5"/>

                                            <CollectionView Grid.Row="3" Grid.Column="0" Grid.ColumnSpan="2" 
                                                          ItemsSource="{Binding Items}">
                                                <CollectionView.ItemTemplate>
                                                    <DataTemplate>
                                                        <StackLayout Orientation="Horizontal" Spacing="10">
                                                            <Label Text="{Binding Quantity}" FontSize="14" FontAttributes="Bold"/>
                                                            <Label Text="{Binding Name}" FontSize="14"/>
                                                            <Label Text="{Binding SpecialInstructions}" 
                                                                   TextColor="Orange" FontSize="12" 
                                                                   IsVisible="{Binding SpecialInstructions, Converter={StaticResource StringToBoolConverter}}"/>
                                                        </StackLayout>
                                                    </DataTemplate>
                                                </CollectionView.ItemTemplate>
                                            </CollectionView>

                                            <Button Grid.Row="0" Grid.Column="1" 
                                                    Text="Start" 
                                                    Command="{Binding Source={RelativeSource AncestorType={x:Type vm:KitchenDisplayViewModel}}, Path=StartPreparingCommand}"
                                                    CommandParameter="{Binding}"
                                                    BackgroundColor="{StaticResource PrimaryColor}"
                                                    TextColor="White"
                                                    CornerRadius="5"
                                                    WidthRequest="60"
                                                    HeightRequest="30"
                                                    Margin="5,0"/>
                                        </Grid>
                                    </Frame>
                                </DataTemplate>
                            </CollectionView.ItemTemplate>
                        </CollectionView>
                    </StackLayout>
                </Frame>

                <!-- Preparing Orders -->
                <Frame BackgroundColor="White" CornerRadius="15" Padding="15" HasShadow="True">
                    <StackLayout Spacing="10">
                        <Label Text="Preparing Orders" FontSize="16" FontAttributes="Bold" TextColor="{StaticResource PrimaryColor}"/>
                        
                        <CollectionView ItemsSource="{Binding PreparingOrders}">
                            <CollectionView.ItemTemplate>
                                <DataTemplate>
                                    <Frame BackgroundColor="#E3F2FD" 
                                           CornerRadius="10" Padding="15" Margin="0,5">
                                        <Grid>
                                            <Grid.RowDefinitions>
                                                <RowDefinition Height="Auto"/>
                                                <RowDefinition Height="Auto"/>
                                                <RowDefinition Height="Auto"/>
                                                <RowDefinition Height="Auto"/>
                                            </Grid.RowDefinitions>
                                            <Grid.ColumnDefinitions>
                                                <ColumnDefinition Width="*"/>
                                                <ColumnDefinition Width="Auto"/>
                                            </Grid.ColumnDefinitions>

                                            <Label Grid.Row="0" Grid.Column="0" 
                                                   Text="{Binding OrderNumber, StringFormat='Order #{0}'}" 
                                                   FontSize="16" FontAttributes="Bold"/>
                                            
                                            <Label Grid.Row="0" Grid.Column="1" 
                                                   Text="{Binding CreatedAt, StringFormat='{0:HH:mm}'}" 
                                                   TextColor="Gray" FontSize="12"/>

                                            <Label Grid.Row="1" Grid.Column="0" 
                                                   Text="{Binding CustomerName}" 
                                                   TextColor="Gray" FontSize="14"/>
                                            
                                            <Label Grid.Row="1" Grid.Column="1" 
                                                   Text="{Binding TableNumber, StringFormat='Table {0}'}" 
                                                   TextColor="Gray" FontSize="12"/>

                                            <BoxView Grid.Row="2" Grid.Column="0" Grid.ColumnSpan="2" 
                                                    HeightRequest="1" 
                                                    BackgroundColor="LightGray" 
                                                    Margin="0,5"/>

                                            <CollectionView Grid.Row="3" Grid.Column="0" Grid.ColumnSpan="2" 
                                                          ItemsSource="{Binding Items}">
                                                <CollectionView.ItemTemplate>
                                                    <DataTemplate>
                                                        <StackLayout Orientation="Horizontal" Spacing="10">
                                                            <Label Text="{Binding Quantity}" FontSize="14" FontAttributes="Bold"/>
                                                            <Label Text="{Binding Name}" FontSize="14"/>
                                                            <Label Text="{Binding SpecialInstructions}" 
                                                                   TextColor="Orange" FontSize="12" 
                                                                   IsVisible="{Binding SpecialInstructions, Converter={StaticResource StringToBoolConverter}}"/>
                                                        </StackLayout>
                                                    </DataTemplate>
                                                </CollectionView.ItemTemplate>
                                            </CollectionView>

                                            <Button Grid.Row="0" Grid.Column="1" 
                                                    Text="Ready" 
                                                    Command="{Binding Source={RelativeSource AncestorType={x:Type vm:KitchenDisplayViewModel}}, Path=CompletePreparingCommand}"
                                                    CommandParameter="{Binding}"
                                                    BackgroundColor="{StaticResource SuccessColor}"
                                                    TextColor="White"
                                                    CornerRadius="5"
                                                    WidthRequest="60"
                                                    HeightRequest="30"
                                                    Margin="5,0"/>
                                        </Grid>
                                    </Frame>
                                </DataTemplate>
                            </CollectionView.ItemTemplate>
                        </CollectionView>
                    </StackLayout>
                </Frame>

                <!-- Ready Orders -->
                <Frame BackgroundColor="White" CornerRadius="15" Padding="15" HasShadow="True">
                    <StackLayout Spacing="10">
                        <Label Text="Ready Orders" FontSize="16" FontAttributes="Bold" TextColor="{StaticResource SuccessColor}"/>
                        
                        <CollectionView ItemsSource="{Binding ReadyOrders}">
                            <CollectionView.ItemTemplate>
                                <DataTemplate>
                                    <Frame BackgroundColor="#E8F5E8" 
                                           CornerRadius="10" Padding="15" Margin="0,5">
                                        <Grid>
                                            <Grid.RowDefinitions>
                                                <RowDefinition Height="Auto"/>
                                                <RowDefinition Height="Auto"/>
                                                <RowDefinition Height="Auto"/>
                                                <RowDefinition Height="Auto"/>
                                            </Grid.RowDefinitions>
                                            <Grid.ColumnDefinitions>
                                                <ColumnDefinition Width="*"/>
                                                <ColumnDefinition Width="Auto"/>
                                            </Grid.ColumnDefinitions>

                                            <Label Grid.Row="0" Grid.Column="0" 
                                                   Text="{Binding OrderNumber, StringFormat='Order #{0}'}" 
                                                   FontSize="16" FontAttributes="Bold"/>
                                            
                                            <Label Grid.Row="0" Grid.Column="1" 
                                                   Text="{Binding CreatedAt, StringFormat='{0:HH:mm}'}" 
                                                   TextColor="Gray" FontSize="12"/>

                                            <Label Grid.Row="1" Grid.Column="0" 
                                                   Text="{Binding CustomerName}" 
                                                   TextColor="Gray" FontSize="14"/>
                                            
                                            <Label Grid.Row="1" Grid.Column="1" 
                                                   Text="{Binding TableNumber, StringFormat='Table {0}'}" 
                                                   TextColor="Gray" FontSize="12"/>

                                            <BoxView Grid.Row="2" Grid.Column="0" Grid.ColumnSpan="2" 
                                                    HeightRequest="1" 
                                                    BackgroundColor="LightGray" 
                                                    Margin="0,5"/>

                                            <CollectionView Grid.Row="3" Grid.Column="0" Grid.ColumnSpan="2" 
                                                          ItemsSource="{Binding Items}">
                                                <CollectionView.ItemTemplate>
                                                    <DataTemplate>
                                                        <StackLayout Orientation="Horizontal" Spacing="10">
                                                            <Label Text="{Binding Quantity}" FontSize="14" FontAttributes="Bold"/>
                                                            <Label Text="{Binding Name}" FontSize="14"/>
                                                        </StackLayout>
                                                    </DataTemplate>
                                                </CollectionView.ItemTemplate>
                                            </CollectionView>

                                            <Button Grid.Row="0" Grid.Column="1" 
                                                    Text="Serve" 
                                                    Command="{Binding Source={RelativeSource AncestorType={x:Type vm:KitchenDisplayViewModel}}, Path=ServeOrderCommand}"
                                                    CommandParameter="{Binding}"
                                                    BackgroundColor="{StaticResource DangerColor}"
                                                    TextColor="White"
                                                    CornerRadius="5"
                                                    WidthRequest="60"
                                                    HeightRequest="30"
                                                    Margin="5,0"/>
                                        </Grid>
                                    </Frame>
                                </DataTemplate>
                            </CollectionView.ItemTemplate>
                        </CollectionView>
                    </StackLayout>
                </Frame>

            </StackLayout>
        </ScrollView>

        <!-- Bottom Status Bar -->
        <Frame Grid.Row="2" BackgroundColor="White" Padding="10" HasShadow="True">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>

                <StackLayout Grid.Column="0" HorizontalOptions="Center" Spacing="5">
                    <Label Text="{Binding PendingCount}" FontSize="20" FontAttributes="Bold" TextColor="{StaticResource WarningColor}" HorizontalOptions="Center"/>
                    <Label Text="Pending" FontSize="10" TextColor="Gray" HorizontalOptions="Center"/>
                </StackLayout>

                <StackLayout Grid.Column="1" HorizontalOptions="Center" Spacing="5">
                    <Label Text="{Binding PreparingCount}" FontSize="20" FontAttributes="Bold" TextColor="{StaticResource PrimaryColor}" HorizontalOptions="Center"/>
                    <Label Text="Preparing" FontSize="10" TextColor="Gray" HorizontalOptions="Center"/>
                </StackLayout>

                <StackLayout Grid.Column="2" HorizontalOptions="Center" Spacing="5">
                    <Label Text="{Binding ReadyCount}" FontSize="20" FontAttributes="Bold" TextColor="{StaticResource SuccessColor}" HorizontalOptions="Center"/>
                    <Label Text="Ready" FontSize="10" TextColor="Gray" HorizontalOptions="Center"/>
                </StackLayout>
            </Grid>
        </Frame>
    </Grid>
</ContentPage>
```

---

## **4. IMPLEMENTATION PLAN**

### **4.1 Week 1-2: Foundation Architecture**
- [ ] Implement MVVM architecture
- [ ] Setup dependency injection
- [ ] Create base view models
- [ ] Implement navigation service
- [ ] Setup WebSocket integration
- [ ] Add authentication service

### **4.2 Week 3-4: Station Features Implementation**
- [ ] Implement kitchen display system
- [ ] Add order management
- [ ] Create inventory management
- [ ] Setup staff management
- [ ] Add real-time updates
- [ ] Implement printer integration

### **4.3 Week 5-6: Advanced Features**
- [ ] Add offline support
- [ ] Implement data synchronization
- [ ] Create notification system
- [ ] Add reporting features
- [ ] Setup analytics
- [ ] Implement hardware integration

### **4.4 Week 7-8: UI/UX Enhancement**
- [ ] Design professional station UI
- [ ] Create custom controls
- [ ] Add animations
- [ ] Implement accessibility
- [ ] Add localization
- [ ] Optimize performance

---

## **5. SUCCESS METRICS**

### **5.1 User Experience Metrics**
- **App Rating:** >4.5 stars
- **User Engagement:** >90% daily active users
- **Task Completion:** >95% task completion rate
- **User Satisfaction:** >90% satisfaction score

### **5.2 Technical Metrics**
- **App Performance:** <2 seconds startup time
- **WebSocket Latency:** <100ms message delivery
- **Crash Rate:** <0.1% crash rate
- **Battery Usage:** <5% battery consumption

### **5.3 Business Metrics**
- **Order Processing:** <30 seconds average processing time
- **Kitchen Efficiency:** >25% improvement in kitchen throughput
- **Inventory Accuracy:** >99% inventory accuracy
- **Staff Productivity:** >20% improvement in staff efficiency

---

## **6. RISK MITIGATION**

### **6.1 Technical Risks**
1. **WebSocket Connectivity** - Robust reconnection logic
2. **Hardware Integration** - Platform-specific implementations
3. **Real-time Performance** - Optimized data handling
4. **Offline Sync** - Robust synchronization

### **6.2 Business Risks**
1. **User Adoption** - Intuitive station workflow
2. **Data Integrity** - Real-time validation
3. **Hardware Compatibility** - Broad hardware support
4. **Network Reliability** - Offline-first design

---

## **7. NEXT STEPS**

### **7.1 Immediate Actions (This Week)**
1. **Setup Architecture** - MVVM foundation
2. **Implement WebSocket** - Real-time connectivity
3. **Create Kitchen Display** - Core station UI
4. **Add Order Management** - Station workflow

### **7.2 Short-term Goals (2 Weeks)**
1. **Complete Station Features** - Order, inventory, staff
2. **Add Real-time Updates** - WebSocket integration
3. **Implement Hardware** - Printer, display integration
4. **Add Offline Support** - Offline capability

### **7.3 Long-term Goals (2 Months)**
1. **Advanced Analytics** - Station performance insights
2. **AI Integration** - Smart kitchen optimization
3. **Multi-station** - Station coordination
4. **Deployment** - Production deployment

---

## **8. SUMMARY**

### **8.1 Current State**
- **Basic MAUI template** with no station features
- **No API integration** or real-time connectivity
- **No authentication** or security measures
- **Template UI** only
- **No data management** or persistence

### **8.2 Target State**
- **Professional station app** with comprehensive features
- **Real-time WebSocket** integration with live updates
- **Role-based authentication** and station management
- **Modern station UI** with intuitive workflow
- **Hardware integration** for printers and displays

### **8.3 Implementation Strategy**
- **8-week phased approach** with clear milestones
- **Real-time first** design philosophy
- **Station-centric** workflow optimization
- **Performance-optimized** development

**Status:** StationApp module needs complete transformation from template to professional station management application with real-time capabilities.
