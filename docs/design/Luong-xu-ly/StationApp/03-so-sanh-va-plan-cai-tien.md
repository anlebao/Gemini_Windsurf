# StationApp - So Sánh và Plan C?i Ti?n

**Ngày:** 11 tháng 4, 2026  
**Module:** 4_MobileApps/StationApp  
**Tr?ng thái:** Phân tích so sánh và k? ho?ch c?i ti?n

---

## **1. SO SÁNH TH?C T? vs LÝ T??NG**

### **1.1 Architecture Comparison**

| **Aspect** | **Th?c T?** | **Lý T??NG** | **Gap Analysis** |
|------------|--------------|--------------|------------------|
| **Architecture** | Basic MAUI template | MVVM with Clean Architecture | **High** - C?n complete architecture |
| **DI Container** | Basic DI setup | Comprehensive DI with lifetimes | **Medium** - C?n proper service lifetimes |
| **Navigation** | Basic Shell navigation | Advanced navigation with routing | **Medium** - C?n navigation enhancement |
| **State Management** | Basic state | Redux-like state management | **High** - C?n state management |
| **Error Handling** | Basic exceptions | Global error handling | **High** - C?n error middleware |

### **1.2 Features Comparison**

| **Aspect** | **Th?c T?** | **Lý T??NG** | **Gap Analysis** |
|------------|--------------|--------------|------------------|
| **Station Features** | None | Complete kitchen station management | **High** - C?n station functionality |
| **Order Display** | None | Real-time order display | **High** - C?n order display |
| **Voice Commands** | None | Voice command processing | **High** - C?n voice integration |
| **Kitchen Display** | None | Professional KDS system | **High** - C?n KDS implementation |
| **Hardware Integration** | None | Barcode scanner, printer integration | **High** - C?n hardware support |

### **1.3 Kitchen Operations Comparison**

| **Aspect** | **Th?c T?** | **Lý T??NG** | **Gap Analysis** |
|------------|--------------|--------------|------------------|
| **Order Management** | None | Complete order lifecycle | **High** - C?n order management |
| **Preparation Tracking** | None | Real-time preparation tracking | **High** - C?n preparation tracking |
| **Quality Control** | None | Quality check system | **High** - C?n quality control |
| **Timer Management** | None | Multiple timer system | **High** - C?n timer functionality |
| **Inventory Integration** | None | Real-time inventory updates | **High** - C?n inventory integration |

### **1.4 Technical Comparison**

| **Aspect** | **Th?c T?** | **Lý T??NG** | **Gap Analysis** |
|------------|--------------|--------------|------------------|
| **Real-time Updates** | None | SignalR real-time updates | **High** - C?n real-time features |
| **Offline Support** | None | Offline-first with sync | **High** - C?n offline capability |
| **Performance** | Basic | Optimized for kitchen environment | **Medium** - C?n performance optimization |
| **Security** | None | Station authentication | **High** - C?n security implementation |
| **Testing** | None | Comprehensive test suite | **High** - C?n testing framework |

---

## **2. DÁNH GIÁ M?C ?? ?U TIÊN**

### **2.1 Critical Issues (Priority 1)**
1. **No Station Features** - Complete lack of kitchen functionality
2. **No Order Display** - No order management system
3. **No Real-time Updates** - No live data synchronization
4. **No Voice Commands** - No voice integration
5. **No Hardware Support** - No device integration

### **2.2 Important Issues (Priority 2)**
1. **No Professional UI** - Basic template design
2. **No Kitchen Display** - No KDS system
3. **No Quality Control** - No quality management
4. **No Timer System** - No preparation timers
5. **No Inventory Integration** - No stock management

### **2.3 Nice to Have (Priority 3)**
1. **No Analytics** - No performance tracking
2. **No Accessibility** - No accessibility features
3. **No Localization** - No multi-language support
4. **No Theming** - No dynamic theming
5. **No CI/CD** - No automated deployment

---

## **3. K? HO?CH C?I TI?N**

### **3.1 Phase 1: Foundation & Architecture (Week 1-2)**

#### **Day 1-3: MVVM Architecture Setup**
```csharp
// Create ViewModels folder structure
// ViewModels/BaseViewModel.cs
public abstract class BaseViewModel : ObservableObject
{
    protected readonly INavigationService _navigationService;
    protected readonly IDialogService _dialogService;
    protected readonly IConnectivityService _connectivityService;
    protected readonly IStationService _stationService;
    protected readonly ISignalRService _signalRService;

    private bool _isBusy;
    private string _title = string.Empty;
    private bool _isRefreshing;
    private Station _currentStation;

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

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    public Station CurrentStation
    {
        get => _currentStation;
        set => SetProperty(ref _currentStation, value);
    }

    protected BaseViewModel(
        INavigationService navigationService,
        IDialogService dialogService,
        IConnectivityService connectivityService,
        IStationService stationService,
        ISignalRService signalRService)
    {
        _navigationService = navigationService;
        _dialogService = dialogService;
        _connectivityService = connectivityService;
        _stationService = stationService;
        _signalRService = signalRService;

        // Subscribe to real-time updates
        _signalRService.OrderUpdated += OnOrderUpdated;
        _signalRService.StationStatusChanged += OnStationStatusChanged;
    }

    public virtual async Task InitializeAsync()
    {
        await LoadDataAsync();
    }

    protected virtual async Task LoadDataAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            await OnLoadDataAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected abstract Task OnLoadDataAsync();

    protected virtual async Task HandleErrorAsync(Exception exception)
    {
        await _dialogService.DisplayAlertAsync("Error", exception.Message, "OK");
    }

    protected virtual async Task RefreshDataAsync()
    {
        if (IsRefreshing) return;

        try
        {
            IsRefreshing = true;
            await OnRefreshDataAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    protected virtual Task OnRefreshDataAsync()
    {
        return OnLoadDataAsync();
    }

    protected virtual void OnOrderUpdated(object sender, OrderUpdatedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            HandleOrderUpdate(e.Order);
        });
    }

    protected virtual void OnStationStatusChanged(object sender, StationStatusChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            HandleStationStatusChange(e.Status);
        });
    }

    protected virtual void HandleOrderUpdate(Order order) { }
    protected virtual void HandleStationStatusChange(StationStatus status) { }
}

// ViewModels/StationDashboardViewModel.cs
public class StationDashboardViewModel : BaseViewModel
{
    private readonly IOrderService _orderService;
    private readonly IVoiceCommandService _voiceCommandService;
    private readonly ITimerService _timerService;
    private readonly IHardwareService _hardwareService;

    private ObservableCollection<Order> _activeOrders;
    private ObservableCollection<Order> _completedOrders;
    private ObservableCollection<Timer> _activeTimers;
    private StationStatus _stationStatus;
    private int _totalOrdersToday;
    private TimeSpan _averagePreparationTime;

    public ObservableCollection<Order> ActiveOrders
    {
        get => _activeOrders ??= new ObservableCollection<Order>();
        set => SetProperty(ref _activeOrders, value);
    }

    public ObservableCollection<Order> CompletedOrders
    {
        get => _completedOrders ??= new ObservableCollection<Order>();
        set => SetProperty(ref _completedOrders, value);
    }

    public ObservableCollection<Timer> ActiveTimers
    {
        get => _activeTimers ??= new ObservableCollection<Timer>();
        set => SetProperty(ref _activeTimers, value);
    }

    public StationStatus StationStatus
    {
        get => _stationStatus;
        set => SetProperty(ref _stationStatus, value);
    }

    public int TotalOrdersToday
    {
        get => _totalOrdersToday;
        set => SetProperty(ref _totalOrdersToday, value);
    }

    public TimeSpan AveragePreparationTime
    {
        get => _averagePreparationTime;
        set => SetProperty(ref _averagePreparationTime, value);
    }

    public ICommand StartOrderCommand { get; }
    public ICommand CompleteOrderCommand { get; }
    public ICommand StartTimerCommand { get; }
    public ICommand StopTimerCommand { get; }
    public ICommand VoiceCommandCommand { get; }
    public ICommand ScanBarcodeCommand { get; }
    public ICommand PrintReceiptCommand { get; }

    public StationDashboardViewModel(
        INavigationService navigationService,
        IDialogService dialogService,
        IConnectivityService connectivityService,
        IStationService stationService,
        ISignalRService signalRService,
        IOrderService orderService,
        IVoiceCommandService voiceCommandService,
        ITimerService timerService,
        IHardwareService hardwareService) : base(navigationService, dialogService, connectivityService, stationService, signalRService)
    {
        _orderService = orderService;
        _voiceCommandService = voiceCommandService;
        _timerService = timerService;
        _hardwareService = hardwareService;

        StartOrderCommand = new Command<Order>(async (order) => await StartOrderAsync(order));
        CompleteOrderCommand = new Command<Order>(async (order) => await CompleteOrderAsync(order));
        StartTimerCommand = new Command<Order>(async (order) => await StartTimerAsync(order));
        StopTimerCommand = new Command<Timer>(async (timer) => await StopTimerAsync(timer));
        VoiceCommandCommand = new Command(async () => await StartVoiceCommandAsync());
        ScanBarcodeCommand = new Command(async () => await ScanBarcodeAsync());
        PrintReceiptCommand = new Command<Order>(async (order) => await PrintReceiptAsync(order));
    }

    protected override async Task OnLoadDataAsync()
    {
        // Load current station
        CurrentStation = await _stationService.GetCurrentStationAsync();

        // Load active orders
        var activeOrders = await _orderService.GetActiveOrdersAsync(CurrentStation.Id);
        ActiveOrders.Clear();
        foreach (var order in activeOrders)
        {
            ActiveOrders.Add(order);
        }

        // Load completed orders
        var completedOrders = await _orderService.GetCompletedOrdersAsync(CurrentStation.Id, DateTime.Today);
        CompletedOrders.Clear();
        foreach (var order in completedOrders.Take(10))
        {
            CompletedOrders.Add(order);
        }

        // Load active timers
        var activeTimers = await _timerService.GetActiveTimersAsync(CurrentStation.Id);
        ActiveTimers.Clear();
        foreach (var timer in activeTimers)
        {
            ActiveTimers.Add(timer);
        }

        // Load station statistics
        var stats = await _stationService.GetStationStatisticsAsync(CurrentStation.Id);
        TotalOrdersToday = stats.TotalOrdersToday;
        AveragePreparationTime = stats.AveragePreparationTime;
        StationStatus = stats.Status;

        // Start voice command listening
        await _voiceCommandService.StartListeningAsync();
    }

    private async Task StartOrderAsync(Order order)
    {
        try
        {
            order.Status = OrderStatus.InProgress;
            order.StartTime = DateTime.UtcNow;

            await _orderService.UpdateOrderAsync(order);
            
            // Start preparation timer
            await _timerService.StartTimerAsync(new TimerRequest
            {
                OrderId = order.Id,
                StationId = CurrentStation.Id,
                Duration = TimeSpan.FromMinutes(15), // Default 15 minutes
                Type = TimerType.Preparation
            });

            // Notify other stations
            await _signalRService.NotifyOrderStatusChanged(order);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task CompleteOrderAsync(Order order)
    {
        try
        {
            order.Status = OrderStatus.Completed;
            order.CompletedTime = DateTime.UtcNow;

            await _orderService.UpdateOrderAsync(order);

            // Stop preparation timer
            await _timerService.StopTimerAsync(order.Id, TimerType.Preparation);

            // Move to completed list
            ActiveOrders.Remove(order);
            CompletedOrders.Insert(0, order);

            // Update statistics
            TotalOrdersToday++;
            await UpdateStatisticsAsync();

            // Notify other stations
            await _signalRService.NotifyOrderStatusChanged(order);

            // Print receipt if enabled
            if (CurrentStation.AutoPrintReceipts)
            {
                await PrintReceiptAsync(order);
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task StartTimerAsync(Order order)
    {
        try
        {
            var timer = await _timerService.StartTimerAsync(new TimerRequest
            {
                OrderId = order.Id,
                StationId = CurrentStation.Id,
                Duration = TimeSpan.FromMinutes(15),
                Type = TimerType.Preparation
            });

            ActiveTimers.Add(timer);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task StopTimerAsync(Timer timer)
    {
        try
        {
            await _timerService.StopTimerAsync(timer.Id, timer.Type);
            ActiveTimers.Remove(timer);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task StartVoiceCommandAsync()
    {
        try
        {
            var command = await _voiceCommandService.ListenForCommandAsync();
            await ProcessVoiceCommandAsync(command);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task ScanBarcodeAsync()
    {
        try
        {
            var barcode = await _hardwareService.ScanBarcodeAsync();
            if (!string.IsNullOrEmpty(barcode))
            {
                await ProcessBarcodeAsync(barcode);
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task PrintReceiptAsync(Order order)
    {
        try
        {
            await _hardwareService.PrintReceiptAsync(order);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task ProcessVoiceCommandAsync(VoiceCommand command)
    {
        switch (command.Type)
        {
            case VoiceCommandType.StartOrder:
                var orderToStart = ActiveOrders.FirstOrDefault(o => o.OrderNumber.ToString() == command.Parameter);
                if (orderToStart != null)
                {
                    await StartOrderAsync(orderToStart);
                }
                break;
            case VoiceCommandType.CompleteOrder:
                var orderToComplete = ActiveOrders.FirstOrDefault(o => o.OrderNumber.ToString() == command.Parameter);
                if (orderToComplete != null)
                {
                    await CompleteOrderAsync(orderToComplete);
                }
                break;
            case VoiceCommandType.StartTimer:
                // Handle timer commands
                break;
        }
    }

    private async Task ProcessBarcodeAsync(string barcode)
    {
        // Process barcode for inventory or order lookup
        var item = await _stationService.GetItemByBarcodeAsync(barcode);
        if (item != null)
        {
            // Handle item processing
        }
    }

    private async Task UpdateStatisticsAsync()
    {
        var stats = await _stationService.GetStationStatisticsAsync(CurrentStation.Id);
        AveragePreparationTime = stats.AveragePreparationTime;
        StationStatus = stats.Status;
    }

    protected override void HandleOrderUpdate(Order order)
    {
        // Update order in UI
        var existingOrder = ActiveOrders.FirstOrDefault(o => o.Id == order.Id);
        if (existingOrder != null)
        {
            var index = ActiveOrders.IndexOf(existingOrder);
            ActiveOrders[index] = order;
        }
    }

    protected override void HandleStationStatusChange(StationStatus status)
    {
        StationStatus = status;
    }
}
```

#### **Day 4-5: Dependency Injection Setup**
```csharp
// MauiProgram.cs
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register services
        builder.Services.AddSingleton<IConnectivityService, ConnectivityService>();
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<ISecureStorageService, SecureStorageService>();
        builder.Services.AddSingleton<IPermissionService, PermissionService>();
        builder.Services.AddSingleton<IAudioService, AudioService>();
        builder.Services.AddSingleton<ISpeechRecognitionService, SpeechRecognitionService>();
        builder.Services.AddSingleton<ISpeechSynthesisService, SpeechSynthesisService>();

        // Register HTTP client
        builder.Services.AddHttpClient<IApiService, ApiService>(client =>
        {
            client.BaseAddress = new Uri(DeviceInfo.Platform == DevicePlatform.Android 
                ? "http://10.0.2.2:5001" 
                : "http://localhost:5001");
            client.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", 
                    GetStoredTokenAsync().Result);
        });

        // Register SignalR
        builder.Services.AddSingleton<ISignalRService, SignalRService>();

        // Register business services
        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddSingleton<IStationService, StationService>();
        builder.Services.AddSingleton<IOrderService, OrderService>();
        builder.Services.AddSingleton<IVoiceCommandService, VoiceCommandService>();
        builder.Services.AddSingleton<ITimerService, TimerService>();
        builder.Services.AddSingleton<IHardwareService, HardwareService>();
        builder.Services.AddSingleton<IInventoryService, InventoryService>();

        // Register ViewModels
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<StationDashboardViewModel>();
        builder.Services.AddTransient<OrderDetailViewModel>();
        builder.Services.AddTransient<TimerViewModel>();
        builder.Services.AddTransient<VoiceCommandViewModel>();
        builder.Services.AddTransient<InventoryViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // Register Pages
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<StationDashboardPage>();
        builder.Services.AddTransient<OrderDetailPage>();
        builder.Services.AddTransient<TimerPage>();
        builder.Services.AddTransient<VoiceCommandPage>();
        builder.Services.AddTransient<InventoryPage>();
        builder.Services.AddTransient<SettingsPage>();

        return builder.Build();
    }

    private static async Task<string> GetStoredTokenAsync()
    {
        await using var secureStorage = new SecureStorageService();
        return await secureStorage.GetAsync("auth_token");
    }
}

// Services/SignalRService.cs
public class SignalRService : ISignalRService
{
    private readonly HubConnection _hubConnection;
    private readonly IAuthService _authService;

    public event EventHandler<OrderUpdatedEventArgs> OrderUpdated;
    public event EventHandler<StationStatusChangedEventArgs> StationStatusChanged;
    public event EventHandler<InventoryUpdatedEventArgs> InventoryUpdated;

    public SignalRService(IAuthService authService)
    {
        _authService = authService;
        
        var token = _authService.GetStoredTokenAsync().Result;
        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:5001" : "http://localhost:5001"}/hubs/kitchen", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(token);
            })
            .WithAutomaticReconnect()
            .Build();

        SetupEventHandlers();
    }

    private void SetupEventHandlers()
    {
        _hubConnection.On<Order>("OrderUpdated", (order) =>
        {
            OrderUpdated?.Invoke(this, new OrderUpdatedEventArgs(order));
        });

        _hubConnection.On<StationStatus>("StationStatusChanged", (status) =>
        {
            StationStatusChanged?.Invoke(this, new StationStatusChangedEventArgs(status));
        });

        _hubConnection.On<InventoryItem>("InventoryUpdated", (item) =>
        {
            InventoryUpdated?.Invoke(this, new InventoryUpdatedEventArgs(item));
        });
    }

    public async Task ConnectAsync()
    {
        try
        {
            await _hubConnection.StartAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to connect to SignalR: {ex.Message}");
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            await _hubConnection.StopAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to disconnect from SignalR: {ex.Message}");
        }
    }

    public async Task NotifyOrderStatusChanged(Order order)
    {
        try
        {
            await _hubConnection.InvokeAsync("OrderStatusChanged", order);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to notify order status change: {ex.Message}");
        }
    }

    public async Task NotifyStationStatusChanged(StationStatus status)
    {
        try
        {
            await _hubConnection.InvokeAsync("StationStatusChanged", status);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to notify station status change: {ex.Message}");
        }
    }

    public async Task JoinStationGroupAsync(Guid stationId)
    {
        try
        {
            await _hubConnection.InvokeAsync("JoinStationGroup", stationId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to join station group: {ex.Message}");
        }
    }

    public async Task LeaveStationGroupAsync(Guid stationId)
    {
        try
        {
            await _hubConnection.InvokeAsync("LeaveStationGroup", stationId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to leave station group: {ex.Message}");
        }
    }

    public bool IsConnected => _hubConnection.State == HubConnectionState.Connected;
}
```

#### **Day 6-7: Voice Command System**
```csharp
// Services/VoiceCommandService.cs
public class VoiceCommandService : IVoiceCommandService
{
    private readonly ISpeechRecognitionService _speechRecognition;
    private readonly ISpeechSynthesisService _speechSynthesis;
    private readonly IOrderService _orderService;
    private readonly ITimerService _timerService;

    public VoiceCommandService(
        ISpeechRecognitionService speechRecognition,
        ISpeechSynthesisService speechSynthesis,
        IOrderService orderService,
        ITimerService timerService)
    {
        _speechRecognition = speechRecognition;
        _speechSynthesis = speechSynthesis;
        _orderService = orderService;
        _timerService = timerService;
    }

    public async Task StartListeningAsync()
    {
        try
        {
            await _speechRecognition.StartListeningAsync();
            _speechRecognition.SpeechRecognized += OnSpeechRecognized;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start voice recognition: {ex.Message}");
        }
    }

    public async Task StopListeningAsync()
    {
        try
        {
            await _speechRecognition.StopListeningAsync();
            _speechRecognition.SpeechRecognized -= OnSpeechRecognized;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to stop voice recognition: {ex.Message}");
        }
    }

    public async Task<VoiceCommand> ListenForCommandAsync()
    {
        try
        {
            await _speechSynthesis.SpeakAsync("Listening for command...");
            var result = await _speechRecognition.RecognizeSpeechAsync();
            
            if (result.Success)
            {
                var command = ParseVoiceCommand(result.Text);
                await _speechSynthesis.SpeakAsync($"Command recognized: {command.Type}");
                return command;
            }
            else
            {
                await _speechSynthesis.SpeakAsync("Sorry, I didn't understand that. Please try again.");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to listen for command: {ex.Message}");
            return null;
        }
    }

    private void OnSpeechRecognized(object sender, SpeechRecognizedEventArgs e)
    {
        var command = ParseVoiceCommand(e.Text);
        if (command != null)
        {
            ProcessVoiceCommand(command);
        }
    }

    private VoiceCommand ParseVoiceCommand(string speechText)
    {
        var text = speechText.ToLowerInvariant().Trim();

        // Start order commands
        if (text.Contains("start") && text.Contains("order"))
        {
            var orderNumber = ExtractOrderNumber(text);
            return new VoiceCommand
            {
                Type = VoiceCommandType.StartOrder,
                Parameter = orderNumber,
                OriginalText = speechText
            };
        }

        // Complete order commands
        if (text.Contains("complete") && text.Contains("order"))
        {
            var orderNumber = ExtractOrderNumber(text);
            return new VoiceCommand
            {
                Type = VoiceCommandType.CompleteOrder,
                Parameter = orderNumber,
                OriginalText = speechText
            };
        }

        // Timer commands
        if (text.Contains("start") && text.Contains("timer"))
        {
            var duration = ExtractDuration(text);
            return new VoiceCommand
            {
                Type = VoiceCommandType.StartTimer,
                Parameter = duration.ToString(),
                OriginalText = speechText
            };
        }

        if (text.Contains("stop") && text.Contains("timer"))
        {
            return new VoiceCommand
            {
                Type = VoiceCommandType.StopTimer,
                OriginalText = speechText
            };
        }

        // Status commands
        if (text.Contains("status") || text.Contains("how are we"))
        {
            return new VoiceCommand
            {
                Type = VoiceCommandType.GetStatus,
                OriginalText = speechText
            };
        }

        // Help commands
        if (text.Contains("help") || text.Contains("what can i say"))
        {
            return new VoiceCommand
            {
                Type = VoiceCommandType.Help,
                OriginalText = speechText
            };
        }

        return null;
    }

    private string ExtractOrderNumber(string text)
    {
        var patterns = new[]
        {
            @"order\s+(\d+)",
            @"number\s+(\d+)",
            @"(\d+)"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return null;
    }

    private TimeSpan ExtractDuration(string text)
    {
        var patterns = new[]
        {
            @"(\d+)\s*minutes?",
            @"(\d+)\s*min",
            @"(\d+)\s*hours?",
            @"(\d+)\s*hr"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern);
            if (match.Success)
            {
                var value = int.Parse(match.Groups[1].Value);
                return text.Contains("hour") || text.Contains("hr") 
                    ? TimeSpan.FromHours(value) 
                    : TimeSpan.FromMinutes(value);
            }
        }

        return TimeSpan.FromMinutes(15); // Default 15 minutes
    }

    private async Task ProcessVoiceCommand(VoiceCommand command)
    {
        try
        {
            switch (command.Type)
            {
                case VoiceCommandType.StartOrder:
                    await ProcessStartOrderCommand(command.Parameter);
                    break;
                case VoiceCommandType.CompleteOrder:
                    await ProcessCompleteOrderCommand(command.Parameter);
                    break;
                case VoiceCommandType.StartTimer:
                    await ProcessStartTimerCommand(command.Parameter);
                    break;
                case VoiceCommandType.StopTimer:
                    await ProcessStopTimerCommand();
                    break;
                case VoiceCommandType.GetStatus:
                    await ProcessGetStatusCommand();
                    break;
                case VoiceCommandType.Help:
                    await ProcessHelpCommand();
                    break;
            }
        }
        catch (Exception ex)
        {
            await _speechSynthesis.SpeakAsync($"Sorry, I encountered an error: {ex.Message}");
        }
    }

    private async Task ProcessStartOrderCommand(string orderNumber)
    {
        if (int.TryParse(orderNumber, out var number))
        {
            var order = await _orderService.GetOrderByNumberAsync(number);
            if (order != null)
            {
                await _orderService.StartOrderAsync(order.Id);
                await _speechSynthesis.SpeakAsync($"Started order {number}");
            }
            else
            {
                await _speechSynthesis.SpeakAsync($"Order {number} not found");
            }
        }
        else
        {
            await _speechSynthesis.SpeakAsync("Please specify a valid order number");
        }
    }

    private async Task ProcessCompleteOrderCommand(string orderNumber)
    {
        if (int.TryParse(orderNumber, out var number))
        {
            var order = await _orderService.GetOrderByNumberAsync(number);
            if (order != null)
            {
                await _orderService.CompleteOrderAsync(order.Id);
                await _speechSynthesis.SpeakAsync($"Completed order {number}");
            }
            else
            {
                await _speechSynthesis.SpeakAsync($"Order {number} not found");
            }
        }
        else
        {
            await _speechSynthesis.SpeakAsync("Please specify a valid order number");
        }
    }

    private async Task ProcessStartTimerCommand(string duration)
    {
        if (TimeSpan.TryParse(duration, out var timeSpan))
        {
            await _timerService.StartTimerAsync(new TimerRequest
            {
                Duration = timeSpan,
                Type = TimerType.Manual
            });
            await _speechSynthesis.SpeakAsync($"Started timer for {timeSpan.TotalMinutes:F0} minutes");
        }
        else
        {
            await _speechSynthesis.SpeakAsync("Please specify a valid duration");
        }
    }

    private async Task ProcessStopTimerCommand()
    {
        await _timerService.StopAllTimersAsync();
        await _speechSynthesis.SpeakAsync("Stopped all timers");
    }

    private async Task ProcessGetStatusCommand()
    {
        var activeOrders = await _orderService.GetActiveOrdersCountAsync();
        var completedOrders = await _orderService.GetCompletedOrdersCountAsync(DateTime.Today);
        
        await _speechSynthesis.SpeakAsync($"We have {activeOrders} active orders and {completedOrders} completed orders today");
    }

    private async Task ProcessHelpCommand()
    {
        var helpText = "You can say: 'Start order number', 'Complete order number', 'Start timer for 15 minutes', 'Stop timer', 'Status', or 'Help'";
        await _speechSynthesis.SpeakAsync(helpText);
    }
}
```

### **3.2 Phase 2: Kitchen Display System (Week 3-4)**

#### **Day 8-10: Professional KDS UI**
```xml
<!-- Pages/StationDashboardPage.xaml -->
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:viewModels="clr-namespace:VanAn.StationApp.ViewModels"
             x:DataType="viewModels:StationDashboardViewModel"
             x:Class="VanAn.StationApp.Pages.StationDashboardPage"
             Title="Kitchen Station">

    <Grid RowDefinitions="Auto,Auto,*,Auto">
        <!-- Header -->
        <Frame Grid.Row="0" BackgroundColor="{StaticResource Primary}" Padding="20" HasShadow="True">
            <Grid ColumnDefinitions="Auto,*,Auto,Auto">
                <Image Grid.Column="0" Source="station_icon.png" 
                       WidthRequest="40" HeightRequest="40" Aspect="AspectFit" />
                <StackLayout Grid.Column="1" Margin="15,0,0,0" VerticalOptions="Center">
                    <Label Text="{Binding CurrentStation.Name}" 
                           FontSize="20" FontAttributes="Bold" TextColor="White" />
                    <Label Text="{Binding StationStatus, StringFormat='Status: {0}'}" 
                           FontSize="14" TextColor="White" Opacity="0.8" />
                </StackLayout>
                <Button Grid.Column="2" Text="Voice" BackgroundColor="{StaticResource Accent}" 
                        TextColor="White" Command="{Binding VoiceCommandCommand}" />
                <Button Grid.Column="3" Text="Scan" BackgroundColor="{StaticResource Success}" 
                        TextColor="White" Command="{Binding ScanBarcodeCommand}" />
            </Grid>
        </Frame>

        <!-- Statistics Bar -->
        <Frame Grid.Row="1" BackgroundColor="{StaticResource CardBackground}" Padding="15" Margin="20,10">
            <Grid ColumnDefinitions="*,*,*">
                <StackLayout Grid.Column="0" HorizontalOptions="Center">
                    <Label Text="{Binding ActiveOrders.Count}" FontSize="24" FontAttributes="Bold" 
                           TextColor="{StaticResource Primary}" HorizontalOptions="Center" />
                    <Label Text="Active Orders" FontSize="12" TextColor="Gray" HorizontalOptions="Center" />
                </StackLayout>
                <StackLayout Grid.Column="1" HorizontalOptions="Center">
                    <Label Text="{Binding TotalOrdersToday}" FontSize="24" FontAttributes="Bold" 
                           TextColor="{StaticResource Success}" HorizontalOptions="Center" />
                    <Label Text="Today's Total" FontSize="12" TextColor="Gray" HorizontalOptions="Center" />
                </StackLayout>
                <StackLayout Grid.Column="2" HorizontalOptions="Center">
                    <Label Text="{Binding AveragePreparationTime, StringFormat='{0:mm\\:ss}'}" 
                           FontSize="24" FontAttributes="Bold" TextColor="{StaticResource Warning}" 
                           HorizontalOptions="Center" />
                    <Label Text="Avg Time" FontSize="12" TextColor="Gray" HorizontalOptions="Center" />
                </StackLayout>
            </Grid>
        </Frame>

        <!-- Main Content -->
        <ScrollView Grid.Row="2" Padding="20">
            <Grid ColumnDefinitions="*,*">
                <!-- Active Orders -->
                <StackLayout Grid.Column="0" Margin="0,0,10,0">
                    <Label Text="Active Orders" FontSize="18" FontAttributes="Bold" />
                    <CollectionView ItemsSource="{Binding ActiveOrders}" HeightRequest="400">
                        <CollectionView.ItemTemplate>
                            <DataTemplate>
                                <Frame BackgroundColor="{StaticResource CardBackground}" 
                                       Padding="15" Margin="0,5" HasShadow="True">
                                    <Grid RowDefinitions="Auto,Auto,Auto,Auto">
                                        <Label Grid.Row="0" Text="{Binding OrderNumber, StringFormat='Order #{0}'}" 
                                               FontSize="16" FontAttributes="Bold" />
                                        <Label Grid.Row="1" Text="{Binding CustomerName}" FontSize="14" />
                                        <Label Grid.Row="2" Text="{Binding Items.Count, StringFormat='Items: {0}'}" 
                                               FontSize="12" TextColor="Gray" />
                                        <Grid Grid.Row="3" ColumnDefinitions="*,*" ColumnSpacing="10">
                                            <Button Grid.Column="0" Text="Start" BackgroundColor="{StaticResource Success}" 
                                                    TextColor="White" Command="{Binding Source={RelativeSource AncestorType={x:Type viewModels:StationDashboardViewModel}}, Path=StartOrderCommand}"
                                                    CommandParameter="{Binding .}" />
                                            <Button Grid.Column="1" Text="Complete" BackgroundColor="{StaticResource Primary}" 
                                                    TextColor="White" Command="{Binding Source={RelativeSource AncestorType={x:Type viewModels:StationDashboardViewModel}}, Path=CompleteOrderCommand}"
                                                    CommandParameter="{Binding .}" />
                                        </Grid>
                                    </Grid>
                                </Frame>
                            </DataTemplate>
                        </CollectionView.ItemTemplate>
                    </CollectionView>
                </StackLayout>

                <!-- Active Timers -->
                <StackLayout Grid.Column="1" Margin="10,0,0,0">
                    <Label Text="Active Timers" FontSize="18" FontAttributes="Bold" />
                    <CollectionView ItemsSource="{Binding ActiveTimers}" HeightRequest="400">
                        <CollectionView.ItemTemplate>
                            <DataTemplate>
                                <Frame BackgroundColor="{StaticResource CardBackground}" 
                                       Padding="15" Margin="0,5" HasShadow="True">
                                    <Grid RowDefinitions="Auto,Auto,Auto">
                                        <Label Grid.Row="0" Text="{Binding Type}" FontSize="14" FontAttributes="Bold" />
                                        <Label Grid.Row="1" Text="{Binding RemainingTime, StringFormat='Time: {0:mm\\:ss}'}" 
                                               FontSize="12" TextColor="Gray" />
                                        <ProgressBar Grid.Row="2" Progress="{Binding Progress}" 
                                                     ProgressColor="{StaticResource Primary}" HeightRequest="10" />
                                    </Grid>
                                </Frame>
                            </DataTemplate>
                        </CollectionView.ItemTemplate>
                    </CollectionView>
                </StackLayout>
            </Grid>
        </ScrollView>

        <!-- Voice Command Indicator -->
        <Frame Grid.Row="3" BackgroundColor="{StaticResource Accent}" Padding="15" Margin="20,0,20,20" 
                IsVisible="{Binding IsListening}" HasShadow="True">
            <Grid ColumnDefinitions="Auto,*">
                <ActivityIndicator Grid.Column="0" IsRunning="True" Color="White" WidthRequest="20" HeightRequest="20" />
                <Label Grid.Column="1" Text="Listening for voice commands..." TextColor="White" 
                       VerticalOptions="Center" Margin="10,0,0,0" />
            </Grid>
        </Frame>

        <!-- Refresh Indicator -->
        <RefreshView Grid.Row="2" Command="{Binding RefreshCommand}" IsRefreshing="{Binding IsRefreshing}">
            <!-- Content goes here -->
        </RefreshView>
    </Grid>
</ContentPage>
```

#### **Day 11-12: Order Detail View**
```xml
<!-- Pages/OrderDetailPage.xaml -->
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:viewModels="clr-namespace:VanAn.StationApp.ViewModels"
             x:DataType="viewModels:OrderDetailViewModel"
             x:Class="VanAn.StationApp.Pages.OrderDetailPage"
             Title="Order Details">

    <Grid RowDefinitions="Auto,*,Auto">
        <!-- Header -->
        <Frame Grid.Row="0" BackgroundColor="{StaticResource Primary}" Padding="20" HasShadow="True">
            <Grid ColumnDefinitions="*,Auto">
                <StackLayout Grid.Column="0" VerticalOptions="Center">
                    <Label Text="{Binding Order.OrderNumber, StringFormat='Order #{0}'}" 
                           FontSize="20" FontAttributes="Bold" TextColor="White" />
                    <Label Text="{Binding Order.CustomerName}" FontSize="14" TextColor="White" Opacity="0.8" />
                </StackLayout>
                <Label Grid.Column="1" Text="{Binding Order.Status}" FontSize="16" TextColor="White" 
                       VerticalOptions="Center" />
            </Grid>
        </Frame>

        <!-- Order Items -->
        <ScrollView Grid.Row="1" Padding="20">
            <StackLayout Spacing="15">
                <Label Text="Order Items" FontSize="18" FontAttributes="Bold" />
                
                <CollectionView ItemsSource="{Binding Order.Items}">
                    <CollectionView.ItemTemplate>
                        <DataTemplate>
                            <Frame BackgroundColor="{StaticResource CardBackground}" 
                                   Padding="15" Margin="0,5" HasShadow="True">
                                <Grid ColumnDefinitions="Auto,*,Auto,Auto">
                                    <Label Grid.Column="0" Text="{Binding Quantity}" FontSize="16" 
                                           FontAttributes="Bold" VerticalOptions="Center" />
                                    <Label Grid.Column="1" Text="{Binding ProductName}" FontSize="14" 
                                           VerticalOptions="Center" Margin="10,0" />
                                    <Label Grid.Column="2" Text="{Binding UnitPrice, StringFormat='{0:C}'}" 
                                           FontSize="12" TextColor="Gray" VerticalOptions="Center" />
                                    <Label Grid.Column="3" Text="{Binding TotalPrice, StringFormat='{0:C}'}" 
                                           FontSize="14" FontAttributes="Bold" VerticalOptions="Center" />
                                </Grid>
                            </Frame>
                        </DataTemplate>
                    </CollectionView.ItemTemplate>
                </CollectionView>

                <!-- Special Instructions -->
                <StackLayout BindableLayout.ItemsSource="{Binding Order.SpecialInstructions}">
                    <BindableLayout.ItemTemplate>
                        <DataTemplate>
                            <Frame BackgroundColor="{StaticResource Warning}" Padding="15" Margin="0,5" HasShadow="True">
                                <StackLayout>
                                    <Label Text="Special Instructions" FontSize="14" FontAttributes="Bold" 
                                           TextColor="White" />
                                    <Label Text="{Binding}" FontSize="12" TextColor="White" />
                                </StackLayout>
                            </Frame>
                        </DataTemplate>
                    </BindableLayout.ItemTemplate>
                </StackLayout>

                <!-- Preparation Notes -->
                <StackLayout>
                    <Label Text="Preparation Notes" FontSize="16" FontAttributes="Bold" />
                    <Editor Text="{Binding PreparationNotes}" HeightRequest="100" 
                            BackgroundColor="{StaticResource CardBackground}" />
                </StackLayout>

                <!-- Quality Check -->
                <StackLayout>
                    <Label Text="Quality Check" FontSize="16" FontAttributes="Bold" />
                    <Grid ColumnDefinitions="*,*" ColumnSpacing="10">
                        <Button Grid.Column="0" Text="Pass" BackgroundColor="{StaticResource Success}" 
                                TextColor="White" Command="{Binding QualityPassCommand}" />
                        <Button Grid.Column="1" Text="Fail" BackgroundColor="{StaticResource Danger}" 
                                TextColor="White" Command="{Binding QualityFailCommand}" />
                    </Grid>
                </StackLayout>
            </StackLayout>
        </ScrollView>

        <!-- Action Buttons -->
        <Frame Grid.Row="2" BackgroundColor="{StaticResource CardBackground}" Padding="20" Margin="20,0,20,20">
            <Grid ColumnDefinitions="*,*" ColumnSpacing="10">
                <Button Grid.Column="0" Text="Start Preparation" BackgroundColor="{StaticResource Success}" 
                        TextColor="White" Command="{Binding StartPreparationCommand}" />
                <Button Grid.Column="1" Text="Complete Order" BackgroundColor="{StaticResource Primary}" 
                        TextColor="White" Command="{Binding CompleteOrderCommand}" />
            </Grid>
        </Frame>
    </Grid>
</ContentPage>
```

### **3.3 Phase 3: Hardware Integration (Week 5-6)**

#### **Day 13-15: Barcode Scanner Integration**
```csharp
// Services/HardwareService.cs
public class HardwareService : IHardwareService
{
    private readonly IBarcodeScannerService _barcodeScanner;
    private readonly IPrinterService _printer;
    private readonly IDisplayService _display;

    public HardwareService(
        IBarcodeScannerService barcodeScanner,
        IPrinterService printer,
        IDisplayService display)
    {
        _barcodeScanner = barcodeScanner;
        _printer = printer;
        _display = display;
    }

    public async Task<string> ScanBarcodeAsync()
    {
        try
        {
            var result = await _barcodeScanner.ScanAsync();
            return result.Success ? result.Barcode : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Barcode scan failed: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> PrintReceiptAsync(Order order)
    {
        try
        {
            var receiptData = GenerateReceiptData(order);
            var result = await _printer.PrintAsync(receiptData);
            return result.Success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Print receipt failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> PrintKitchenTicketAsync(Order order)
    {
        try
        {
            var ticketData = GenerateKitchenTicketData(order);
            var result = await _printer.PrintAsync(ticketData);
            return result.Success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Print kitchen ticket failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateDisplayAsync(string message)
    {
        try
        {
            var result = await _display.UpdateAsync(message);
            return result.Success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Display update failed: {ex.Message}");
            return false;
        }
    }

    private PrintData GenerateReceiptData(Order order)
    {
        var lines = new List<string>
        {
            "================================",
            "           VAN AN CAFE",
            "================================",
            $"Order #: {order.OrderNumber}",
            $"Date: {order.CreatedAt:yyyy-MM-dd HH:mm}",
            $"Customer: {order.CustomerName}",
            "--------------------------------",
            "ITEMS:",
            ""
        };

        foreach (var item in order.Items)
        {
            lines.Add($"{item.Quantity}x {item.ProductName}");
            lines.Add($"   {item.TotalPrice:C}");
            lines.Add("");
        }

        lines.Add("================================");
        lines.Add($"TOTAL: {order.TotalAmount:C}");
        lines.Add("================================");
        lines.Add("Thank you for your order!");
        lines.Add("================================");

        return new PrintData
        {
            Lines = lines,
            FontSize = 12,
            Alignment = PrintAlignment.Center
        };
    }

    private PrintData GenerateKitchenTicketData(Order order)
    {
        var lines = new List<string>
        {
            "================================",
            "         KITCHEN TICKET",
            "================================",
            $"Order #: {order.OrderNumber}",
            $"Time: {order.CreatedAt:HH:mm}",
            $"Station: {order.AssignedStation?.Name ?? "Unassigned"}",
            "--------------------------------",
            "ITEMS:",
            ""
        };

        foreach (var item in order.Items)
        {
            lines.Add($"{item.Quantity}x {item.ProductName}");
            if (!string.IsNullOrEmpty(item.SpecialInstructions))
            {
                lines.Add($"  Note: {item.SpecialInstructions}");
            }
            lines.Add("");
        }

        if (order.SpecialInstructions.Any())
        {
            lines.Add("--------------------------------");
            lines.Add("SPECIAL INSTRUCTIONS:");
            foreach (var instruction in order.SpecialInstructions)
            {
                lines.Add($"- {instruction}");
            }
            lines.Add("");
        }

        lines.Add("================================");

        return new PrintData
        {
            Lines = lines,
            FontSize = 10,
            Alignment = PrintAlignment.Left
        };
    }
}

// Services/BarcodeScannerService.cs
public class BarcodeScannerService : IBarcodeScannerService
{
    private readonly ICameraService _cameraService;
    private readonly IBarcodeDecoder _barcodeDecoder;

    public BarcodeScannerService(
        ICameraService cameraService,
        IBarcodeDecoder barcodeDecoder)
    {
        _cameraService = cameraService;
        _barcodeDecoder = barcodeDecoder;
    }

    public async Task<BarcodeScanResult> ScanAsync()
    {
        try
        {
            // Check camera permission
            var permission = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (permission != PermissionStatus.Granted)
            {
                permission = await Permissions.RequestAsync<Permissions.Camera>();
                if (permission != PermissionStatus.Granted)
                {
                    return new BarcodeScanResult { Success = false, Error = "Camera permission denied" };
                }
            }

            // Capture image
            var imageResult = await _cameraService.CaptureImageAsync();
            if (!imageResult.Success)
            {
                return new BarcodeScanResult { Success = false, Error = imageResult.Error };
            }

            // Decode barcode
            var decodeResult = await _barcodeDecoder.DecodeAsync(imageResult.ImageData);
            if (decodeResult.Success)
            {
                return new BarcodeScanResult { Success = true, Barcode = decodeResult.Barcode };
            }
            else
            {
                return new BarcodeScanResult { Success = false, Error = "No barcode detected" };
            }
        }
        catch (Exception ex)
        {
            return new BarcodeScanResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<BarcodeScanResult> ScanContinuousAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Start continuous scanning
            await _cameraService.StartContinuousCaptureAsync();

            var result = await Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var imageResult = await _cameraService.GetLatestImageAsync();
                    if (imageResult.Success)
                    {
                        var decodeResult = await _barcodeDecoder.DecodeAsync(imageResult.ImageData);
                        if (decodeResult.Success)
                        {
                            return new BarcodeScanResult { Success = true, Barcode = decodeResult.Barcode };
                        }
                    }

                    await Task.Delay(100, cancellationToken);
                }

                return new BarcodeScanResult { Success = false, Error = "Scan cancelled" };
            }, cancellationToken);

            await _cameraService.StopContinuousCaptureAsync();
            return result;
        }
        catch (Exception ex)
        {
            return new BarcodeScanResult { Success = false, Error = ex.Message };
        }
    }
}
```

#### **Day 16-17: Printer Integration**
```csharp
// Services/PrinterService.cs
public class PrinterService : IPrinterService
{
    private readonly List<IPrinter> _availablePrinters;
    private IPrinter _defaultPrinter;

    public PrinterService()
    {
        _availablePrinters = new List<IPrinter>();
        DiscoverPrinters();
    }

    private void DiscoverPrinters()
    {
        // Discover available printers
        // This would integrate with specific printer SDKs
        // For now, we'll simulate printer discovery
        
        _availablePrinters.Add(new BluetoothPrinter("Kitchen Printer 1", "BT:001"));
        _availablePrinters.Add(new NetworkPrinter("Kitchen Printer 2", "192.168.1.100"));
        _availablePrinters.Add(new USBPrinter("Receipt Printer", "USB:001"));
        
        _defaultPrinter = _availablePrinters.FirstOrDefault();
    }

    public async Task<PrintResult> PrintAsync(PrintData printData)
    {
        try
        {
            if (_defaultPrinter == null)
            {
                return new PrintResult { Success = false, Error = "No printer available" };
            }

            var result = await _defaultPrinter.PrintAsync(printData);
            return result;
        }
        catch (Exception ex)
        {
            return new PrintResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<PrintResult> PrintAsync(PrintData printData, string printerName)
    {
        try
        {
            var printer = _availablePrinters.FirstOrDefault(p => p.Name == printerName);
            if (printer == null)
            {
                return new PrintResult { Success = false, Error = $"Printer '{printerName}' not found" };
            }

            var result = await printer.PrintAsync(printData);
            return result;
        }
        catch (Exception ex)
        {
            return new PrintResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<List<PrinterStatus>> GetPrinterStatusesAsync()
    {
        var statuses = new List<PrinterStatus>();
        
        foreach (var printer in _availablePrinters)
        {
            try
            {
                var status = await printer.GetStatusAsync();
                statuses.Add(status);
            }
            catch (Exception ex)
            {
                statuses.Add(new PrinterStatus
                {
                    Name = printer.Name,
                    IsOnline = false,
                    Error = ex.Message
                });
            }
        }

        return statuses;
    }

    public async Task<bool> SetDefaultPrinterAsync(string printerName)
    {
        var printer = _availablePrinters.FirstOrDefault(p => p.Name == printerName);
        if (printer != null)
        {
            _defaultPrinter = printer;
            return true;
        }

        return false;
    }

    public List<string> GetAvailablePrinters()
    {
        return _availablePrinters.Select(p => p.Name).ToList();
    }
}

// Implementation of specific printer types
public class BluetoothPrinter : IPrinter
{
    public string Name { get; }
    public string Address { get; }

    public BluetoothPrinter(string name, string address)
    {
        Name = name;
        Address = address;
    }

    public async Task<PrintResult> PrintAsync(PrintData printData)
    {
        try
        {
            // Connect to Bluetooth printer
            // Send print data
            // Disconnect
            
            return new PrintResult { Success = true };
        }
        catch (Exception ex)
        {
            return new PrintResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<PrinterStatus> GetStatusAsync()
    {
        // Check Bluetooth connection status
        return new PrinterStatus
        {
            Name = Name,
            IsOnline = true, // Would check actual connection
            PaperLevel = 80,
            InkLevel = 90
        };
    }
}

public class NetworkPrinter : IPrinter
{
    public string Name { get; }
    public string IPAddress { get; }

    public NetworkPrinter(string name, string ipAddress)
    {
        Name = name;
        IPAddress = ipAddress;
    }

    public async Task<PrintResult> PrintAsync(PrintData printData)
    {
        try
        {
            // Connect to network printer
            // Send print data via network protocol
            // Disconnect
            
            return new PrintResult { Success = true };
        }
        catch (Exception ex)
        {
            return new PrintResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<PrinterStatus> GetStatusAsync()
    {
        // Check network connection and printer status
        return new PrinterStatus
        {
            Name = Name,
            IsOnline = true,
            PaperLevel = 75,
            InkLevel = 85
        };
    }
}
```

### **3.4 Phase 4: Testing & Deployment (Week 7-8)**

#### **Day 18-20: Unit Testing**
```csharp
// Tests/ViewModels/StationDashboardViewModelTests.cs
public class StationDashboardViewModelTests
{
    private readonly Mock<INavigationService> _navigationServiceMock;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<IConnectivityService> _connectivityServiceMock;
    private readonly Mock<IStationService> _stationServiceMock;
    private readonly Mock<ISignalRService> _signalRServiceMock;
    private readonly Mock<IOrderService> _orderServiceMock;
    private readonly Mock<IVoiceCommandService> _voiceCommandServiceMock;
    private readonly Mock<ITimerService> _timerServiceMock;
    private readonly Mock<IHardwareService> _hardwareServiceMock;

    private readonly StationDashboardViewModel _viewModel;

    public StationDashboardViewModelTests()
    {
        _navigationServiceMock = new Mock<INavigationService>();
        _dialogServiceMock = new Mock<IDialogService>();
        _connectivityServiceMock = new Mock<IConnectivityService>();
        _stationServiceMock = new Mock<IStationService>();
        _signalRServiceMock = new Mock<ISignalRService>();
        _orderServiceMock = new Mock<IOrderService>();
        _voiceCommandServiceMock = new Mock<IVoiceCommandService>();
        _timerServiceMock = new Mock<ITimerService>();
        _hardwareServiceMock = new Mock<IHardwareService>();

        _viewModel = new StationDashboardViewModel(
            _navigationServiceMock.Object,
            _dialogServiceMock.Object,
            _connectivityServiceMock.Object,
            _stationServiceMock.Object,
            _signalRServiceMock.Object,
            _orderServiceMock.Object,
            _voiceCommandServiceMock.Object,
            _timerServiceMock.Object,
            _hardwareServiceMock.Object);
    }

    [Fact]
    public async Task InitializeAsync_Should_Load_Station_Data()
    {
        // Arrange
        var station = new Station { Id = Guid.NewGuid(), Name = "Kitchen Station 1" };
        var activeOrders = new List<Order>
        {
            new() { Id = Guid.NewGuid(), OrderNumber = 1001, Status = OrderStatus.Pending }
        };
        var stats = new StationStatistics
        {
            TotalOrdersToday = 25,
            AveragePreparationTime = TimeSpan.FromMinutes(12),
            Status = StationStatus.Active
        };

        _stationServiceMock.Setup(x => x.GetCurrentStationAsync()).ReturnsAsync(station);
        _orderServiceMock.Setup(x => x.GetActiveOrdersAsync(station.Id)).ReturnsAsync(activeOrders);
        _stationServiceMock.Setup(x => x.GetStationStatisticsAsync(station.Id)).ReturnsAsync(stats);

        // Act
        await _viewModel.InitializeAsync();

        // Assert
        _viewModel.CurrentStation.Should().Be(station);
        _viewModel.ActiveOrders.Should().HaveCount(1);
        _viewModel.TotalOrdersToday.Should().Be(25);
        _viewModel.AveragePreparationTime.Should().Be(TimeSpan.FromMinutes(12));
        _viewModel.StationStatus.Should().Be(StationStatus.Active);
    }

    [Fact]
    public async Task StartOrderCommand_Should_Update_Order_Status()
    {
        // Arrange
        var order = new Order { Id = Guid.NewGuid(), OrderNumber = 1001, Status = OrderStatus.Pending };
        _viewModel.ActiveOrders.Add(order);

        // Act
        await _viewModel.StartOrderCommand.ExecuteAsync(order);

        // Assert
        order.Status.Should().Be(OrderStatus.InProgress);
        order.StartTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        _orderServiceMock.Verify(x => x.UpdateOrderAsync(order), Times.Once);
        _timerServiceMock.Verify(x => x.StartTimerAsync(It.IsAny<TimerRequest>()), Times.Once);
    }

    [Fact]
    public async Task CompleteOrderCommand_Should_Move_To_Completed()
    {
        // Arrange
        var order = new Order { Id = Guid.NewGuid(), OrderNumber = 1001, Status = OrderStatus.InProgress };
        _viewModel.ActiveOrders.Add(order);

        // Act
        await _viewModel.CompleteOrderCommand.ExecuteAsync(order);

        // Assert
        order.Status.Should().Be(OrderStatus.Completed);
        order.CompletedTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        _viewModel.ActiveOrders.Should().NotContain(order);
        _viewModel.CompletedOrders.Should().Contain(order);
        _viewModel.TotalOrdersToday.Should().Be(1);
    }

    [Fact]
    public async Task VoiceCommandCommand_Should_Start_Voice_Recognition()
    {
        // Act
        await _viewModel.VoiceCommandCommand.ExecuteAsync(null);

        // Assert
        _voiceCommandServiceMock.Verify(x => x.ListenForCommandAsync(), Times.Once);
    }

    [Fact]
    public async Task ScanBarcodeCommand_Should_Scan_Barcode()
    {
        // Act
        await _viewModel.ScanBarcodeCommand.ExecuteAsync(null);

        // Assert
        _hardwareServiceMock.Verify(x => x.ScanBarcodeAsync(), Times.Once);
    }
}

// Tests/Services/VoiceCommandServiceTests.cs
public class VoiceCommandServiceTests
{
    private readonly Mock<ISpeechRecognitionService> _speechRecognitionMock;
    private readonly Mock<ISpeechSynthesisService> _speechSynthesisMock;
    private readonly Mock<IOrderService> _orderServiceMock;
    private readonly Mock<ITimerService> _timerServiceMock;

    private readonly VoiceCommandService _voiceCommandService;

    public VoiceCommandServiceTests()
    {
        _speechRecognitionMock = new Mock<ISpeechRecognitionService>();
        _speechSynthesisMock = new Mock<ISpeechSynthesisService>();
        _orderServiceMock = new Mock<IOrderService>();
        _timerServiceMock = new Mock<ITimerService>();

        _voiceCommandService = new VoiceCommandService(
            _speechRecognitionMock.Object,
            _speechSynthesisMock.Object,
            _orderServiceMock.Object,
            _timerServiceMock.Object);
    }

    [Fact]
    public async Task ListenForCommandAsync_Should_Recognize_Start_Order_Command()
    {
        // Arrange
        var speechResult = new SpeechRecognitionResult
        {
            Success = true,
            Text = "Start order 1001"
        };

        _speechSynthesisMock.Setup(x => x.SpeakAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _speechRecognitionMock.Setup(x => x.RecognizeSpeechAsync()).ReturnsAsync(speechResult);

        // Act
        var command = await _voiceCommandService.ListenForCommandAsync();

        // Assert
        command.Should().NotBeNull();
        command.Type.Should().Be(VoiceCommandType.StartOrder);
        command.Parameter.Should().Be("1001");
    }

    [Fact]
    public async Task ListenForCommandAsync_Should_Recognize_Complete_Order_Command()
    {
        // Arrange
        var speechResult = new SpeechRecognitionResult
        {
            Success = true,
            Text = "Complete order 1002"
        };

        _speechSynthesisMock.Setup(x => x.SpeakAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _speechRecognitionMock.Setup(x => x.RecognizeSpeechAsync()).ReturnsAsync(speechResult);

        // Act
        var command = await _voiceCommandService.ListenForCommandAsync();

        // Assert
        command.Should().NotBeNull();
        command.Type.Should().Be(VoiceCommandType.CompleteOrder);
        command.Parameter.Should().Be("1002");
    }

    [Fact]
    public async Task ListenForCommandAsync_Should_Recognize_Timer_Command()
    {
        // Arrange
        var speechResult = new SpeechRecognitionResult
        {
            Success = true,
            Text = "Start timer for 15 minutes"
        };

        _speechSynthesisMock.Setup(x => x.SpeakAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _speechRecognitionMock.Setup(x => x.RecognizeSpeechAsync()).ReturnsAsync(speechResult);

        // Act
        var command = await _voiceCommandService.ListenForCommandAsync();

        // Assert
        command.Should().NotBeNull();
        command.Type.Should().Be(VoiceCommandType.StartTimer);
        command.Parameter.Should().Be("00:15:00");
    }
}
```

#### **Day 21-22: Integration Testing**
```csharp
// Tests/Integration/StationIntegrationTests.cs
public class StationIntegrationTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly VanAnDbContext _context;

    public StationIntegrationTests()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();
        _context = _factory.Services.GetRequiredService<VanAnDbContext>();
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
        await SeedTestDataAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    private async Task SeedTestDataAsync()
    {
        var station = new Station
        {
            Id = Guid.NewGuid(),
            Name = "Kitchen Station 1",
            Type = StationType.Preparation,
            Status = StationStatus.Active
        };

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = 1001,
            CustomerName = "Test Customer",
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Items = new List<OrderItem>
            {
                new() { ProductName = "Coffee", Quantity = 2, UnitPrice = 25000 }
            }
        };

        _context.Stations.Add(station);
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task StartOrder_Should_Update_Order_Status()
    {
        // Arrange
        var order = await _context.Orders.FirstAsync();
        var station = await _context.Stations.FirstAsync();

        var startOrderRequest = new StartOrderRequest
        {
            OrderId = order.Id,
            StationId = station.Id,
            StartTime = DateTime.UtcNow
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders/start", startOrderRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var orderResponse = await response.Content.ReadFromJsonAsync<ApiResponse<Order>>();
        orderResponse.Success.Should().BeTrue();

        var updatedOrder = await _context.Orders.FindAsync(order.Id);
        updatedOrder.Status.Should().Be(OrderStatus.InProgress);
        updatedOrder.StartTime.Should().BeCloseTo(startOrderRequest.StartTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CompleteOrder_Should_Update_Order_Status_And_Add_Completion_Time()
    {
        // Arrange
        var order = await _context.Orders.FirstAsync();
        var station = await _context.Stations.FirstAsync();

        // First start the order
        order.Status = OrderStatus.InProgress;
        order.StartTime = DateTime.UtcNow.AddMinutes(-10);
        await _context.SaveChangesAsync();

        var completeOrderRequest = new CompleteOrderRequest
        {
            OrderId = order.Id,
            StationId = station.Id,
            CompletedTime = DateTime.UtcNow,
            QualityCheck = QualityCheck.Pass
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders/complete", completeOrderRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var orderResponse = await response.Content.ReadFromJsonAsync<ApiResponse<Order>>();
        orderResponse.Success.Should().BeTrue();

        var completedOrder = await _context.Orders.FindAsync(order.Id);
        completedOrder.Status.Should().Be(OrderStatus.Completed);
        completedOrder.CompletedTime.Should().BeCloseTo(completeOrderRequest.CompletedTime, TimeSpan.FromSeconds(1));
        completedOrder.QualityCheck.Should().Be(QualityCheck.Pass);
    }

    [Fact]
    public async Task GetStationOrders_Should_Return_Orders_For_Station()
    {
        // Arrange
        var station = await _context.Stations.FirstAsync();
        var order = await _context.Orders.FirstAsync();

        // Assign order to station
        order.AssignedStationId = station.Id;
        await _context.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync($"/api/stations/{station.Id}/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var ordersResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<Order>>>();
        ordersResponse.Success.Should().BeTrue();
        ordersResponse.Data.Should().HaveCount(1);
        ordersResponse.Data.First().Id.Should().Be(order.Id);
    }
}
```

---

## **4. IMPLEMENTATION PLAN**

### **4.1 Week 1-2: Foundation & Architecture**
- [ ] Setup MVVM architecture
- [ ] Implement dependency injection
- [ ] Create SignalR integration
- [ ] Setup navigation service
- [ ] Add voice command system
- [ ] Create base view models

### **4.2 Week 3-4: Kitchen Display System**
- [ ] Implement station dashboard
- [ ] Create order management
- [ ] Add real-time updates
- [ ] Implement timer system
- [ ] Create order detail view
- [ ] Add quality control

### **4.3 Week 5-6: Hardware Integration**
- [ ] Implement barcode scanner
- [ ] Add printer integration
- [ ] Create display service
- [ ] Add hardware detection
- [ ] Implement device management
- [ ] Add error handling

### **4.4 Week 7-8: Testing & Deployment**
- [ ] Create unit tests
- [ ] Add integration tests
- [ ] Implement UI tests
- [ ] Setup CI/CD pipeline
- [ ] Create deployment scripts
- [ ] Add crash reporting

---

## **5. SUCCESS METRICS**

### **5.1 Quality Metrics**
- **Code Coverage:** >80% for view models and services
- **Performance:** <1 second order processing
- **Memory Usage:** <150MB under normal usage
- **Battery Impact:** <8% battery drain per hour

### **5.2 Kitchen Operations Metrics**
- **Order Processing Time:** <30 seconds average
- **Voice Command Accuracy:** >90% recognition rate
- **Barcode Scan Success:** >95% scan rate
- **Print Success Rate:** >98% print success

### **5.3 User Experience Metrics**
- **App Launch Time:** <3 seconds
- **Screen Load Time:** <500ms
- **Real-time Update Latency:** <2 seconds
- **Offline Functionality:** 100% core features offline

---

## **6. RISK MITIGATION**

### **6.1 Technical Risks**
1. **Hardware Compatibility** - Support multiple printer/scanner types
2. **Voice Recognition Accuracy** - Implement fallback commands
3. **Real-time Sync Issues** - Implement offline sync
4. **Performance Issues** - Optimize for kitchen environment

### **6.2 Business Risks**
1. **Kitchen Environment** - Design for rugged use
2. **Staff Training** - Simple intuitive interface
3. **Integration Complexity** - Modular architecture
4. **Maintenance Overhead** - Remote monitoring

---

## **7. NEXT STEPS**

### **7.1 Immediate Actions (This Week)**
1. **Setup Architecture** - MVVM foundation
2. **Create SignalR** - Real-time updates
3. **Implement Voice** - Command system
4. **Add Navigation** - Page routing

### **7.2 Short-term Goals (2 Weeks)**
1. **Complete KDS** - Kitchen display system
2. **Implement Hardware** - Scanner & printer
3. **Add Real-time** - Live order updates
4. **Create Professional UI** - Kitchen-optimized design

### **7.3 Long-term Goals (2 Months)**
1. **Complete All Features** - Full station app
2. **Achieve Performance** - Optimize for speed
3. **User Testing** - Kitchen environment testing
4. **Production Deployment** - App store release

---

## **8. SUMMARY**

### **8.1 Current State**
- **Basic MAUI template** with no station features
- **No kitchen functionality** or hardware support
- **No real-time updates** or voice commands
- **No testing** or documentation

### **8.2 Target State**
- **Professional kitchen station app** with complete features
- **Real-time order management** with voice commands
- **Hardware integration** with scanners and printers
- **Comprehensive testing** and monitoring

### **8.3 Implementation Strategy**
- **8-week phased approach** with clear milestones
- **Kitchen-centric design** optimized for restaurant environment
- **Hardware-first approach** with device integration
- **Quality focus** with comprehensive testing

**Status:** StationApp module là completely empty template with significant gaps but có clear improvement plan v?i professional kitchen station architecture.
