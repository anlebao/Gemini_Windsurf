# HrApp - So Sánh và Plan C?i Ti?n

**Ngày:** 11 tháng 4, 2026  
**Module:** 4_MobileApps/HRApp  
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
| **Authentication** | None | Biometric + JWT authentication | **High** - C?n authentication system |
| **HR Features** | None | Complete HR management | **High** - C?n HR functionality |
| **Offline Support** | None | Offline-first with sync | **High** - C?n offline capability |
| **Real-time Updates** | None | SignalR real-time updates | **High** - C?n real-time features |
| **Push Notifications** | None | Push notification system | **High** - C?n notification system |

### **1.3 UI/UX Comparison**

| **Aspect** | **Th?c T?** | **Lý T??NG** | **Gap Analysis** |
|------------|--------------|--------------|------------------|
| **UI Design** | Basic MAUI template | Professional HR app design | **High** - C?n UI redesign |
| **Responsive Design** | Basic | Multi-device responsive | **Medium** - C?n responsive enhancement |
| **Accessibility** | None | Full accessibility support | **High** - C?n accessibility features |
| **Theming** | Basic | Dynamic theming system | **Medium** - C?n theming enhancement |
| **Localization** | None | Multi-language support | **High** - C?n localization |

### **1.4 Technical Comparison**

| **Aspect** | **Th?c T?** | **Lý T??NG** | **Gap Analysis** |
|------------|--------------|--------------|------------------|
| **Performance** | Basic | Optimized performance | **Medium** - C?n performance optimization |
| **Security** | None | Comprehensive security | **High** - C?n security implementation |
| **Testing** | None | Comprehensive test suite | **High** - C?n testing framework |
| **CI/CD** | None | Mobile CI/CD pipeline | **High** - C?n CI/CD setup |
| **Analytics** | None | Usage analytics & crash reporting | **High** - C?n analytics system |

---

## **2. DÁNH GIÁ M?C ?? ?U TIÊN**

### **2.1 Critical Issues (Priority 1)**
1. **No HR Features** - Complete lack of HR functionality
2. **No Authentication** - No user authentication system
3. **No Offline Support** - No offline capability
4. **No Real-time Updates** - No live data updates
5. **No Testing** - No test coverage

### **2.2 Important Issues (Priority 2)**
1. **No Professional UI** - Basic template design
2. **No Security** - No security measures
3. **No Performance Optimization** - Basic performance
4. **No Error Handling** - No error management
5. **No Documentation** - No app documentation

### **2.3 Nice to Have (Priority 3)**
1. **No Analytics** - No usage tracking
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
    protected readonly IAuthService _authService;

    private bool _isBusy;
    private string _title = string.Empty;
    private bool _isRefreshing;

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

    protected BaseViewModel(
        INavigationService navigationService,
        IDialogService dialogService,
        IConnectivityService connectivityService,
        IAuthService authService)
    {
        _navigationService = navigationService;
        _dialogService = dialogService;
        _connectivityService = connectivityService;
        _authService = authService;
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
}

// ViewModels/DashboardViewModel.cs
public class DashboardViewModel : BaseViewModel
{
    private readonly IEmployeeService _employeeService;
    private readonly IAttendanceService _attendanceService;
    private readonly IPayrollService _payrollService;

    private Employee _currentEmployee;
    private ObservableCollection<AttendanceRecord> _recentAttendance;
    private PayrollSummary _payrollSummary;
    private ObservableCollection<Notification> _notifications;

    public Employee CurrentEmployee
    {
        get => _currentEmployee;
        set => SetProperty(ref _currentEmployee, value);
    }

    public ObservableCollection<AttendanceRecord> RecentAttendance
    {
        get => _recentAttendance ??= new ObservableCollection<AttendanceRecord>();
        set => SetProperty(ref _recentAttendance, value);
    }

    public PayrollSummary PayrollSummary
    {
        get => _payrollSummary;
        set => SetProperty(ref _payrollSummary, value);
    }

    public ObservableCollection<Notification> Notifications
    {
        get => _notifications ??= new ObservableCollection<Notification>();
        set => SetProperty(ref _notifications, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand ViewAttendanceCommand { get; }
    public ICommand ViewPayrollCommand { get; }
    public ICommand ViewProfileCommand { get; }

    public DashboardViewModel(
        INavigationService navigationService,
        IDialogService dialogService,
        IConnectivityService connectivityService,
        IAuthService authService,
        IEmployeeService employeeService,
        IAttendanceService attendanceService,
        IPayrollService payrollService) : base(navigationService, dialogService, connectivityService, authService)
    {
        _employeeService = employeeService;
        _attendanceService = attendanceService;
        _payrollService = payrollService;

        RefreshCommand = new Command(async () => await RefreshDataAsync());
        ViewAttendanceCommand = new Command(async () => await NavigateToAttendanceAsync());
        ViewPayrollCommand = new Command(async () => await NavigateToPayrollAsync());
        ViewProfileCommand = new Command(async () => await NavigateToProfileAsync());
    }

    protected override async Task OnLoadDataAsync()
    {
        // Load current employee
        CurrentEmployee = await _employeeService.GetCurrentEmployeeAsync();

        // Load recent attendance
        var attendance = await _attendanceService.GetRecentAttendanceAsync(CurrentEmployee.Id, 5);
        RecentAttendance.Clear();
        foreach (var record in attendance)
        {
            RecentAttendance.Add(record);
        }

        // Load payroll summary
        PayrollSummary = await _payrollService.GetPayrollSummaryAsync(CurrentEmployee.Id);

        // Load notifications
        var notifications = await _authService.GetNotificationsAsync();
        Notifications.Clear();
        foreach (var notification in notifications)
        {
            Notifications.Add(notification);
        }
    }

    protected override async Task OnRefreshDataAsync()
    {
        await base.OnRefreshDataAsync();
        await LoadDataAsync();
    }

    private async Task NavigateToAttendanceAsync()
    {
        await _navigationService.NavigateToAsync(nameof(AttendancePage));
    }

    private async Task NavigateToPayrollAsync()
    {
        await _navigationService.NavigateToAsync(nameof(PayrollPage));
    }

    private async Task NavigateToProfileAsync()
    {
        await _navigationService.NavigateToAsync(nameof(ProfilePage));
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
        builder.Services.AddSingleton<IBiometricService, BiometricService>();
        builder.Services.AddSingleton<IPushNotificationService, PushNotificationService>();
        builder.Services.AddSingleton<IOfflineDataService, OfflineDataService>();

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

        // Register business services
        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddSingleton<IEmployeeService, EmployeeService>();
        builder.Services.AddSingleton<IAttendanceService, AttendanceService>();
        builder.Services.AddSingleton<IPayrollService, PayrollService>();
        builder.Services.AddSingleton<ILeaveService, LeaveService>();

        // Register ViewModels
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<AttendanceViewModel>();
        builder.Services.AddTransient<PayrollViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<LeaveViewModel>();

        // Register Pages
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<AttendancePage>();
        builder.Services.AddTransient<PayrollPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<LeavePage>();

        return builder.Build();
    }

    private static async Task<string> GetStoredTokenAsync()
    {
        await using var secureStorage = new SecureStorageService();
        return await secureStorage.GetAsync("auth_token");
    }
}

// Services/NavigationService.cs
public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task NavigateToAsync<T>() where T : Page
    {
        var page = _serviceProvider.GetService<T>();
        if (page != null)
        {
            await Application.Current.MainPage.Navigation.PushAsync(page);
        }
    }

    public async Task NavigateToAsync<T>(object parameter) where T : Page
    {
        var page = _serviceProvider.GetService<T>();
        if (page != null)
        {
            if (page is IParameterized parameterized)
            {
                parameterized.SetParameter(parameter);
            }
            await Application.Current.MainPage.Navigation.PushAsync(page);
        }
    }

    public async Task GoBackAsync()
    {
        if (Application.Current.MainPage.Navigation.NavigationStack.Count > 1)
        {
            await Application.Current.MainPage.Navigation.PopAsync();
        }
    }

    public async Task ClearBackStackAsync()
    {
        Application.Current.MainPage.Navigation.ClearBackStack();
    }
}
```

#### **Day 6-7: Authentication System**
```csharp
// Services/AuthService.cs
public class AuthService : IAuthService
{
    private readonly IApiService _apiService;
    private readonly IBiometricService _biometricService;
    private readonly ISecureStorageService _secureStorage;
    private readonly IPushNotificationService _pushNotificationService;

    private AuthToken _currentToken;

    public AuthService(
        IApiService apiService,
        IBiometricService biometricService,
        ISecureStorageService secureStorage,
        IPushNotificationService pushNotificationService)
    {
        _apiService = apiService;
        _biometricService = biometricService;
        _secureStorage = secureStorage;
        _pushNotificationService = pushNotificationService;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        if (_currentToken != null && !_currentToken.IsExpired)
        {
            return true;
        }

        var storedToken = await _secureStorage.GetAsync("auth_token");
        if (!string.IsNullOrEmpty(storedToken))
        {
            _currentToken = JsonSerializer.Deserialize<AuthToken>(storedToken);
            return _currentToken != null && !_currentToken.IsExpired;
        }

        return false;
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _apiService.PostAsync<AuthResponse>("/api/auth/login", request);
            
            if (response.Success)
            {
                _currentToken = response.Data.Token;
                await _secureStorage.SetAsync("auth_token", 
                    JsonSerializer.Serialize(_currentToken));
                
                // Setup push notifications
                await _pushNotificationService.RegisterDeviceAsync(_currentToken.UserId);
                
                return AuthResult.Success(response.Data.Employee);
            }
            else
            {
                return AuthResult.Failure(response.Message);
            }
        }
        catch (Exception ex)
        {
            return AuthResult.Failure(ex.Message);
        }
    }

    public async Task<AuthResult> BiometricLoginAsync()
    {
        if (!await _biometricService.IsAvailableAsync())
        {
            return AuthResult.Failure("Biometric authentication is not available");
        }

        var biometricResult = await _biometricService.AuthenticateAsync(
            "Login with biometrics",
            "Verify your identity to login");

        if (!biometricResult.Success)
        {
            return AuthResult.Failure("Biometric authentication failed");
        }

        // Get stored credentials
        var storedCredentials = await _secureStorage.GetAsync("biometric_credentials");
        if (string.IsNullOrEmpty(storedCredentials))
        {
            return AuthResult.Failure("No biometric credentials stored");
        }

        var credentials = JsonSerializer.Deserialize<BiometricCredentials>(storedCredentials);
        return await LoginAsync(new LoginRequest
        {
            EmployeeId = credentials.EmployeeId,
            Password = credentials.EncryptedPassword
        });
    }

    public async Task LogoutAsync()
    {
        await _secureStorage.RemoveAsync("auth_token");
        await _secureStorage.RemoveAsync("biometric_credentials");
        await _pushNotificationService.UnregisterDeviceAsync();
        _currentToken = null;
    }

    public async Task<bool> EnableBiometricLoginAsync(LoginRequest request)
    {
        if (!await _biometricService.IsAvailableAsync())
        {
            return false;
        }

        var biometricResult = await _biometricService.AuthenticateAsync(
            "Enable biometric login",
            "Verify your identity to enable biometric login");

        if (biometricResult.Success)
        {
            // Store encrypted credentials
            var credentials = new BiometricCredentials
            {
                EmployeeId = request.EmployeeId,
                EncryptedPassword = await _biometricService.EncryptAsync(request.Password)
            };

            await _secureStorage.SetAsync("biometric_credentials", 
                JsonSerializer.Serialize(credentials));
            return true;
        }

        return false;
    }

    public async Task<List<Notification>> GetNotificationsAsync()
    {
        try
        {
            var response = await _apiService.GetAsync<List<Notification>>("/api/notifications");
            return response.Success ? response.Data : new List<Notification>();
        }
        catch
        {
            return new List<Notification>();
        }
    }
}

// ViewModels/LoginViewModel.cs
public class LoginViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly IBiometricService _biometricService;

    private string _employeeId;
    private string _password;
    private bool _rememberMe;
    private bool _biometricAvailable;

    public string EmployeeId
    {
        get => _employeeId;
        set => SetProperty(ref _employeeId, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public bool RememberMe
    {
        get => _rememberMe;
        set => SetProperty(ref _rememberMe, value);
    }

    public bool BiometricAvailable
    {
        get => _biometricAvailable;
        set => SetProperty(ref _biometricAvailable, value);
    }

    public ICommand LoginCommand { get; }
    public ICommand BiometricLoginCommand { get; }

    public LoginViewModel(
        INavigationService navigationService,
        IDialogService dialogService,
        IConnectivityService connectivityService,
        IAuthService authService,
        IBiometricService biometricService) : base(navigationService, dialogService, connectivityService, authService)
    {
        _authService = authService;
        _biometricService = biometricService;

        LoginCommand = new Command(async () => await LoginAsync(), CanLogin);
        BiometricLoginCommand = new Command(async () => await BiometricLoginAsync(), CanBiometricLogin);

        PropertyChanged += OnPropertyChanged;
    }

    private async void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EmployeeId) || e.PropertyName == nameof(Password))
        {
            ((Command)LoginCommand).ChangeCanExecute();
        }
    }

    private bool CanLogin()
    {
        return !string.IsNullOrWhiteSpace(EmployeeId) && !string.IsNullOrWhiteSpace(Password) && !IsBusy;
    }

    private bool CanBiometricLogin()
    {
        return BiometricAvailable && !IsBusy;
    }

    protected override async Task OnLoadDataAsync()
    {
        // Check if already authenticated
        if (await _authService.IsAuthenticatedAsync())
        {
            await _navigationService.NavigateToAsync<DashboardPage>();
            return;
        }

        // Check biometric availability
        BiometricAvailable = await _biometricService.IsAvailableAsync();

        // Load remembered credentials if available
        if (BiometricAvailable)
        {
            await LoadRememberedCredentialsAsync();
        }
    }

    private async Task LoadRememberedCredentialsAsync()
    {
        var storedCredentials = await _secureStorage.GetAsync("remembered_credentials");
        if (!string.IsNullOrEmpty(storedCredentials))
        {
            var credentials = JsonSerializer.Deserialize<LoginRequest>(storedCredentials);
            EmployeeId = credentials.EmployeeId;
            Password = credentials.Password;
            RememberMe = true;
        }
    }

    private async Task LoginAsync()
    {
        if (!await _connectivityService.IsConnectedAsync())
        {
            await _dialogService.DisplayAlertAsync("Error", "No internet connection", "OK");
            return;
        }

        var loginRequest = new LoginRequest
        {
            EmployeeId = EmployeeId,
            Password = Password
        };

        var result = await _authService.LoginAsync(loginRequest);

        if (result.Success)
        {
            if (RememberMe)
            {
                await _secureStorage.SetAsync("remembered_credentials", 
                    JsonSerializer.Serialize(loginRequest));
            }

            await _navigationService.NavigateToAsync<DashboardPage>();
        }
        else
        {
            await _dialogService.DisplayAlertAsync("Login Failed", result.ErrorMessage, "OK");
        }
    }

    private async Task BiometricLoginAsync()
    {
        var result = await _authService.BiometricLoginAsync();

        if (result.Success)
        {
            await _navigationService.NavigateToAsync<DashboardPage>();
        }
        else
        {
            await _dialogService.DisplayAlertAsync("Biometric Login Failed", result.ErrorMessage, "OK");
        }
    }
}
```

### **3.2 Phase 2: HR Features Implementation (Week 3-4)**

#### **Day 8-10: Attendance Management**
```csharp
// Services/AttendanceService.cs
public class AttendanceService : IAttendanceService
{
    private readonly IApiService _apiService;
    private readonly IOfflineDataService _offlineDataService;
    private readonly IGeolocationService _geolocationService;

    public AttendanceService(
        IApiService apiService,
        IOfflineDataService offlineDataService,
        IGeolocationService geolocationService)
    {
        _apiService = apiService;
        _offlineDataService = offlineDataService;
        _geolocationService = geolocationService;
    }

    public async Task<bool> CheckInAsync(Guid employeeId)
    {
        try
        {
            var location = await _geolocationService.GetLocationAsync();
            
            var checkInRequest = new CheckInRequest
            {
                EmployeeId = employeeId,
                CheckInTime = DateTime.UtcNow,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                DeviceInfo = GetDeviceInfo()
            };

            if (await _connectivityService.IsConnectedAsync())
            {
                var response = await _apiService.PostAsync<AttendanceResponse>("/api/attendance/checkin", checkInRequest);
                if (response.Success)
                {
                    await _offlineDataService.SaveAttendanceAsync(response.Data);
                    return true;
                }
            }
            else
            {
                // Save for offline sync
                var attendance = new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = employeeId,
                    CheckInTime = checkInRequest.CheckInTime,
                    Latitude = checkInRequest.Latitude,
                    Longitude = checkInRequest.Longitude,
                    IsSynced = false
                };

                await _offlineDataService.SaveAttendanceAsync(attendance);
                return true;
            }
        }
        catch (Exception ex)
        {
            // Log error
            Console.WriteLine($"Check-in failed: {ex.Message}");
        }

        return false;
    }

    public async Task<bool> CheckOutAsync(Guid employeeId)
    {
        try
        {
            var location = await _geolocationService.GetLocationAsync();
            
            var checkOutRequest = new CheckOutRequest
            {
                EmployeeId = employeeId,
                CheckOutTime = DateTime.UtcNow,
                Latitude = location.Latitude,
                Longitude = location.Longitude
            };

            if (await _connectivityService.IsConnectedAsync())
            {
                var response = await _apiService.PostAsync<AttendanceResponse>("/api/attendance/checkout", checkOutRequest);
                if (response.Success)
                {
                    await _offlineDataService.SaveAttendanceAsync(response.Data);
                    return true;
                }
            }
            else
            {
                // Save for offline sync
                var attendance = await _offlineDataService.GetTodayAttendanceAsync(employeeId);
                if (attendance != null)
                {
                    attendance.CheckOutTime = checkOutRequest.CheckOutTime;
                    attendance.Latitude = checkOutRequest.Latitude;
                    attendance.Longitude = checkOutRequest.Longitude;
                    attendance.IsSynced = false;
                    await _offlineDataService.SaveAttendanceAsync(attendance);
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Check-out failed: {ex.Message}");
        }

        return false;
    }

    public async Task<List<AttendanceRecord>> GetRecentAttendanceAsync(Guid employeeId, int count = 10)
    {
        try
        {
            if (await _connectivityService.IsConnectedAsync())
            {
                var response = await _apiService.GetAsync<List<AttendanceRecord>>($"/api/attendance/recent/{employeeId}?count={count}");
                if (response.Success)
                {
                    // Update local cache
                    foreach (var record in response.Data)
                    {
                        await _offlineDataService.SaveAttendanceAsync(record);
                    }
                    return response.Data;
                }
            }

            // Return cached data
            return await _offlineDataService.GetRecentAttendanceAsync(employeeId, count);
        }
        catch
        {
            return await _offlineDataService.GetRecentAttendanceAsync(employeeId, count);
        }
    }

    public async Task<AttendanceSummary> GetAttendanceSummaryAsync(Guid employeeId, DateTime from, DateTime to)
    {
        try
        {
            if (await _connectivityService.IsConnectedAsync())
            {
                var response = await _apiService.GetAsync<AttendanceSummary>($"/api/attendance/summary/{employeeId}?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
                if (response.Success)
                {
                    return response.Data;
                }
            }

            // Calculate from local data
            var attendance = await _offlineDataService.GetAttendanceInRangeAsync(employeeId, from, to);
            return CalculateSummary(attendance);
        }
        catch
        {
            return new AttendanceSummary();
        }
    }

    private AttendanceSummary CalculateSummary(List<AttendanceRecord> attendance)
    {
        var summary = new AttendanceSummary
        {
            TotalDays = attendance.Count,
            PresentDays = attendance.Count(a => a.CheckInTime.HasValue),
            LateDays = attendance.Count(a => a.CheckInTime.HasValue && a.CheckInTime.Value.TimeOfDay > new TimeSpan(9, 0, 0)),
            EarlyCheckOutDays = attendance.Count(a => a.CheckOutTime.HasValue && a.CheckOutTime.Value.TimeOfDay < new TimeSpan(18, 0, 0)),
            TotalHours = attendance.Where(a => a.CheckInTime.HasValue && a.CheckOutTime.HasValue)
                .Sum(a => (a.CheckOutTime.Value - a.CheckInTime.Value).TotalHours)
        };

        return summary;
    }

    private string GetDeviceInfo()
    {
        return $"{DeviceInfo.Model} - {DeviceInfo.Platform} {DeviceInfo.Version}";
    }

    public async Task SyncOfflineDataAsync()
    {
        if (!await _connectivityService.IsConnectedAsync())
        {
            return;
        }

        var offlineAttendance = await _offlineDataService.GetUnsyncedAttendanceAsync();
        
        foreach (var attendance in offlineAttendance)
        {
            try
            {
                if (attendance.CheckInTime.HasValue && !attendance.CheckOutTime.HasValue)
                {
                    var checkInRequest = new CheckInRequest
                    {
                        EmployeeId = attendance.EmployeeId,
                        CheckInTime = attendance.CheckInTime.Value,
                        Latitude = attendance.Latitude,
                        Longitude = attendance.Longitude,
                        DeviceInfo = attendance.DeviceInfo
                    };

                    var response = await _apiService.PostAsync<AttendanceResponse>("/api/attendance/checkin", checkInRequest);
                    if (response.Success)
                    {
                        attendance.IsSynced = true;
                        await _offlineDataService.SaveAttendanceAsync(attendance);
                    }
                }
                else if (attendance.CheckInTime.HasValue && attendance.CheckOutTime.HasValue)
                {
                    var checkOutRequest = new CheckOutRequest
                    {
                        EmployeeId = attendance.EmployeeId,
                        CheckOutTime = attendance.CheckOutTime.Value,
                        Latitude = attendance.Latitude,
                        Longitude = attendance.Longitude
                    };

                    var response = await _apiService.PostAsync<AttendanceResponse>("/api/attendance/checkout", checkOutRequest);
                    if (response.Success)
                    {
                        attendance.IsSynced = true;
                        await _offlineDataService.SaveAttendanceAsync(attendance);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Sync failed for attendance {attendance.Id}: {ex.Message}");
            }
        }
    }
}

// ViewModels/AttendanceViewModel.cs
public class AttendanceViewModel : BaseViewModel
{
    private readonly IAttendanceService _attendanceService;
    private readonly IGeolocationService _geolocationService;

    private ObservableCollection<AttendanceRecord> _attendanceHistory;
    private AttendanceRecord _todayAttendance;
    private AttendanceSummary _monthlySummary;
    private bool _canCheckIn;
    private bool _canCheckOut;

    public ObservableCollection<AttendanceRecord> AttendanceHistory
    {
        get => _attendanceHistory ??= new ObservableCollection<AttendanceRecord>();
        set => SetProperty(ref _attendanceHistory, value);
    }

    public AttendanceRecord TodayAttendance
    {
        get => _todayAttendance;
        set => SetProperty(ref _todayAttendance, value);
    }

    public AttendanceSummary MonthlySummary
    {
        get => _monthlySummary;
        set => SetProperty(ref _monthlySummary, value);
    }

    public bool CanCheckIn
    {
        get => _canCheckIn;
        set => SetProperty(ref _canCheckIn, value);
    }

    public bool CanCheckOut
    {
        get => _canCheckOut;
        set => SetProperty(ref _canCheckOut, value);
    }

    public ICommand CheckInCommand { get; }
    public ICommand CheckOutCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ViewDetailsCommand { get; }

    public AttendanceViewModel(
        INavigationService navigationService,
        IDialogService dialogService,
        IConnectivityService connectivityService,
        IAuthService authService,
        IAttendanceService attendanceService,
        IGeolocationService geolocationService) : base(navigationService, dialogService, connectivityService, authService)
    {
        _attendanceService = attendanceService;
        _geolocationService = geolocationService;

        CheckInCommand = new Command(async () => await CheckInAsync(), () => CanCheckIn && !IsBusy);
        CheckOutCommand = new Command(async () => await CheckOutAsync(), () => CanCheckOut && !IsBusy);
        RefreshCommand = new Command(async () => await RefreshDataAsync());
        ViewDetailsCommand = new Command<AttendanceRecord>(async (record) => await ViewDetailsAsync(record));
    }

    protected override async Task OnLoadDataAsync()
    {
        var currentEmployee = await _authService.GetCurrentEmployeeAsync();
        
        // Load today's attendance
        var todayAttendance = await _attendanceService.GetTodayAttendanceAsync(currentEmployee.Id);
        TodayAttendance = todayAttendance;

        // Update button states
        UpdateButtonStates(todayAttendance);

        // Load attendance history
        var history = await _attendanceService.GetRecentAttendanceAsync(currentEmployee.Id, 20);
        AttendanceHistory.Clear();
        foreach (var record in history)
        {
            AttendanceHistory.Add(record);
        }

        // Load monthly summary
        var now = DateTime.Now;
        var summary = await _attendanceService.GetAttendanceSummaryAsync(currentEmployee.Id, 
            new DateTime(now.Year, now.Month, 1), 
            new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month)));
        MonthlySummary = summary;
    }

    private void UpdateButtonStates(AttendanceRecord todayAttendance)
    {
        if (todayAttendance == null)
        {
            CanCheckIn = true;
            CanCheckOut = false;
        }
        else if (todayAttendance.CheckInTime.HasValue && !todayAttendance.CheckOutTime.HasValue)
        {
            CanCheckIn = false;
            CanCheckOut = true;
        }
        else if (todayAttendance.CheckInTime.HasValue && todayAttendance.CheckOutTime.HasValue)
        {
            CanCheckIn = false;
            CanCheckOut = false;
        }
        else
        {
            CanCheckIn = true;
            CanCheckOut = false;
        }

        ((Command)CheckInCommand).ChangeCanExecute();
        ((Command)CheckOutCommand).ChangeCanExecute();
    }

    private async Task CheckInAsync()
    {
        try
        {
            // Get location permission
            var locationPermission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (locationPermission != PermissionStatus.Granted)
            {
                locationPermission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (locationPermission != PermissionStatus.Granted)
                {
                    await _dialogService.DisplayAlertAsync("Permission Required", 
                        "Location permission is required for attendance tracking", "OK");
                    return;
                }
            }

            var currentEmployee = await _authService.GetCurrentEmployeeAsync();
            var success = await _attendanceService.CheckInAsync(currentEmployee.Id);

            if (success)
            {
                await _dialogService.DisplayAlertAsync("Success", "Check-in successful", "OK");
                await LoadDataAsync();
            }
            else
            {
                await _dialogService.DisplayAlertAsync("Error", "Check-in failed", "OK");
            }
        }
        catch (Exception ex)
        {
            await _dialogService.DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async Task CheckOutAsync()
    {
        try
        {
            var currentEmployee = await _authService.GetCurrentEmployeeAsync();
            var success = await _attendanceService.CheckOutAsync(currentEmployee.Id);

            if (success)
            {
                await _dialogService.DisplayAlertAsync("Success", "Check-out successful", "OK");
                await LoadDataAsync();
            }
            else
            {
                await _dialogService.DisplayAlertAsync("Error", "Check-out failed", "OK");
            }
        }
        catch (Exception ex)
        {
            await _dialogService.DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async Task ViewDetailsAsync(AttendanceRecord record)
    {
        var details = $"Check-in: {record.CheckInTime:yyyy-MM-dd HH:mm}\n" +
                     $"Check-out: {(record.CheckOutTime?.ToString("yyyy-MM-dd HH:mm") ?? "Not checked out")}\n" +
                     $"Location: {record.Latitude:F6}, {record.Longitude:F6}";

        await _dialogService.DisplayAlertAsync("Attendance Details", details, "OK");
    }
}
```

#### **Day 11-12: Payroll Management**
```csharp
// Services/PayrollService.cs
public class PayrollService : IPayrollService
{
    private readonly IApiService _apiService;
    private readonly IOfflineDataService _offlineDataService;

    public PayrollService(
        IApiService apiService,
        IOfflineDataService offlineDataService)
    {
        _apiService = apiService;
        _offlineDataService = offlineDataService;
    }

    public async Task<PayrollSummary> GetPayrollSummaryAsync(Guid employeeId)
    {
        try
        {
            if (await _connectivityService.IsConnectedAsync())
            {
                var response = await _apiService.GetAsync<PayrollSummary>($"/api/payroll/summary/{employeeId}");
                if (response.Success)
                {
                    await _offlineDataService.SavePayrollSummaryAsync(response.Data);
                    return response.Data;
                }
            }

            // Return cached data
            return await _offlineDataService.GetPayrollSummaryAsync(employeeId);
        }
        catch
        {
            return await _offlineDataService.GetPayrollSummaryAsync(employeeId);
        }
    }

    public async Task<List<PayrollRecord>> GetPayrollHistoryAsync(Guid employeeId, int months = 12)
    {
        try
        {
            if (await _connectivityService.IsConnectedAsync())
            {
                var response = await _apiService.GetAsync<List<PayrollRecord>>($"/api/payroll/history/{employeeId}?months={months}");
                if (response.Success)
                {
                    // Update local cache
                    foreach (var record in response.Data)
                    {
                        await _offlineDataService.SavePayrollRecordAsync(record);
                    }
                    return response.Data;
                }
            }

            // Return cached data
            return await _offlineDataService.GetPayrollHistoryAsync(employeeId, months);
        }
        catch
        {
            return await _offlineDataService.GetPayrollHistoryAsync(employeeId, months);
        }
    }

    public async Task<PaySlip> GetPaySlipAsync(Guid employeeId, Guid payrollId)
    {
        try
        {
            if (await _connectivityService.IsConnectedAsync())
            {
                var response = await _apiService.GetAsync<PaySlip>($"/api/payroll/payslip/{payrollId}");
                if (response.Success)
                {
                    await _offlineDataService.SavePaySlipAsync(response.Data);
                    return response.Data;
                }
            }

            // Return cached data
            return await _offlineDataService.GetPaySlipAsync(payrollId);
        }
        catch
        {
            return await _offlineDataService.GetPaySlipAsync(payrollId);
        }
    }

    public async Task<byte[]> GeneratePaySlipPdfAsync(Guid employeeId, Guid payrollId)
    {
        try
        {
            if (await _connectivityService.IsConnectedAsync())
            {
                var response = await _apiService.GetAsync<byte[]>($"/api/payroll/payslip/pdf/{payrollId}");
                if (response.Success)
                {
                    return response.Data;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to generate PDF: {ex.Message}");
        }

        return null;
    }
}

// ViewModels/PayrollViewModel.cs
public class PayrollViewModel : BaseViewModel
{
    private readonly IPayrollService _payrollService;

    private ObservableCollection<PayrollRecord> _payrollHistory;
    private PayrollSummary _currentSummary;
    private PaySlip _selectedPaySlip;

    public ObservableCollection<PayrollRecord> PayrollHistory
    {
        get => _payrollHistory ??= new ObservableCollection<PayrollRecord>();
        set => SetProperty(ref _payrollHistory, value);
    }

    public PayrollSummary CurrentSummary
    {
        get => _currentSummary;
        set => SetProperty(ref _currentSummary, value);
    }

    public PaySlip SelectedPaySlip
    {
        get => _selectedPaySlip;
        set => SetProperty(ref _selectedPaySlip, value);
    }

    public ICommand ViewPaySlipCommand { get; }
    public ICommand DownloadPdfCommand { get; }
    public ICommand RefreshCommand { get; }

    public PayrollViewModel(
        INavigationService navigationService,
        IDialogService dialogService,
        IConnectivityService connectivityService,
        IAuthService authService,
        IPayrollService payrollService) : base(navigationService, dialogService, connectivityService, authService)
    {
        _payrollService = payrollService;

        ViewPaySlipCommand = new Command<PayrollRecord>(async (record) => await ViewPaySlipAsync(record));
        DownloadPdfCommand = new Command<PayrollRecord>(async (record) => await DownloadPdfAsync(record));
        RefreshCommand = new Command(async () => await RefreshDataAsync());
    }

    protected override async Task OnLoadDataAsync()
    {
        var currentEmployee = await _authService.GetCurrentEmployeeAsync();
        
        // Load current payroll summary
        var summary = await _payrollService.GetPayrollSummaryAsync(currentEmployee.Id);
        CurrentSummary = summary;

        // Load payroll history
        var history = await _payrollService.GetPayrollHistoryAsync(currentEmployee.Id, 12);
        PayrollHistory.Clear();
        foreach (var record in history)
        {
            PayrollHistory.Add(record);
        }
    }

    private async Task ViewPaySlipAsync(PayrollRecord record)
    {
        try
        {
            var currentEmployee = await _authService.GetCurrentEmployeeAsync();
            var paySlip = await _payrollService.GetPaySlipAsync(currentEmployee.Id, record.Id);
            SelectedPaySlip = paySlip;

            // Navigate to pay slip details
            await _navigationService.NavigateToAsync<PaySlipPage>(paySlip);
        }
        catch (Exception ex)
        {
            await _dialogService.DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async Task DownloadPdfAsync(PayrollRecord record)
    {
        try
        {
            var currentEmployee = await _authService.GetCurrentEmployeeAsync();
            var pdfData = await _payrollService.GeneratePaySlipPdfAsync(currentEmployee.Id, record.Id);

            if (pdfData != null)
            {
                // Save and open PDF
                var fileName = $"PaySlip_{record.Period:yyyy-MM}.pdf";
                var filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);
                await File.WriteAllBytesAsync(filePath, pdfData);

                await Launcher.OpenAsync(new OpenFileRequest(fileName, filePath));
            }
            else
            {
                await _dialogService.DisplayAlertAsync("Error", "Failed to generate PDF", "OK");
            }
        }
        catch (Exception ex)
        {
            await _dialogService.DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}
```

### **3.3 Phase 3: UI/UX Enhancement (Week 5-6)**

#### **Day 13-15: Professional UI Design**
```xml
<!-- Pages/DashboardPage.xaml -->
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:viewModels="clr-namespace:VanAn.HRApp.ViewModels"
             x:DataType="viewModels:DashboardViewModel"
             x:Class="VanAn.HRApp.Pages.DashboardPage"
             Title="Dashboard">

    <Grid RowDefinitions="Auto,Auto,*">
        <!-- Header -->
        <Frame Grid.Row="0" BackgroundColor="{StaticResource Primary}" Padding="20" HasShadow="True">
            <Grid ColumnDefinitions="Auto,*">
                <Image Grid.Column="0" Source="profile_placeholder.png" 
                       WidthRequest="60" HeightRequest="60" Aspect="AspectFill" />
                <StackLayout Grid.Column="1" Margin="15,0,0,0" VerticalOptions="Center">
                    <Label Text="{Binding CurrentEmployee.Name}" 
                           FontSize="18" FontAttributes="Bold" TextColor="White" />
                    <Label Text="{Binding CurrentEmployee.Position}" 
                           FontSize="14" TextColor="White" Opacity="0.8" />
                    <Label Text="{Binding CurrentEmployee.Department}" 
                           FontSize="12" TextColor="White" Opacity="0.6" />
                </StackLayout>
            </Grid>
        </Frame>

        <!-- Quick Stats -->
        <ScrollView Grid.Row="1" Orientation="Horizontal" Padding="20,10">
            <StackLayout Orientation="Horizontal" BindableLayout.ItemsSource="{Binding QuickStats}">
                <BindableLayout.ItemTemplate>
                    <DataTemplate>
                        <Frame BackgroundColor="{StaticResource CardBackground}" 
                               Padding="15" Margin="5" WidthRequest="150" HasShadow="True">
                            <StackLayout>
                                <Label Text="{Binding Title}" FontSize="12" TextColor="Gray" />
                                <Label Text="{Binding Value}" FontSize="20" FontAttributes="Bold" 
                                       TextColor="{StaticResource Primary}" />
                                <Label Text="{Binding Subtitle}" FontSize="10" TextColor="Gray" />
                            </StackLayout>
                        </Frame>
                    </DataTemplate>
                </BindableLayout.ItemTemplate>
            </StackLayout>
        </ScrollView>

        <!-- Main Content -->
        <ScrollView Grid.Row="2" Padding="20">
            <StackLayout Spacing="20">
                <!-- Quick Actions -->
                <Label Text="Quick Actions" FontSize="18" FontAttributes="Bold" />
                <Grid ColumnDefinitions="*,*" RowDefinitions="*,*" ColumnSpacing="10" RowSpacing="10">
                    <Button Grid.Row="0" Grid.Column="0" Text="Check In" 
                            BackgroundColor="{StaticResource Success}" TextColor="White"
                            Command="{Binding CheckInCommand}" />
                    <Button Grid.Row="0" Grid.Column="1" Text="Check Out" 
                            BackgroundColor="{StaticResource Warning}" TextColor="White"
                            Command="{Binding CheckOutCommand}" />
                    <Button Grid.Row="1" Grid.Column="0" Text="Leave Request" 
                            BackgroundColor="{StaticResource Info}" TextColor="White"
                            Command="{Binding LeaveRequestCommand}" />
                    <Button Grid.Row="1" Grid.Column="1" Text="Profile" 
                            BackgroundColor="{StaticResource Secondary}" TextColor="White"
                            Command="{Binding ProfileCommand}" />
                </Grid>

                <!-- Recent Attendance -->
                <StackLayout>
                    <Grid ColumnDefinitions="*,Auto">
                        <Label Grid.Column="0" Text="Recent Attendance" FontSize="18" FontAttributes="Bold" />
                        <Button Grid.Column="1" Text="View All" FontSize="12" 
                                BackgroundColor="Transparent" TextColor="{StaticResource Primary}" />
                    </Grid>
                    
                    <CollectionView ItemsSource="{Binding RecentAttendance}" HeightRequest="200">
                        <CollectionView.ItemTemplate>
                            <DataTemplate>
                                <Frame BackgroundColor="{StaticResource CardBackground}" 
                                       Padding="15" Margin="0,5" HasShadow="True">
                                    <Grid ColumnDefinitions="Auto,*,Auto">
                                        <Label Grid.Column="0" Text="{Binding Date, StringFormat='{0:dd}'}" 
                                               FontSize="20" FontAttributes="Bold" VerticalOptions="Center" />
                                        <StackLayout Grid.Column="1" Margin="10,0">
                                            <Label Text="{Binding Date, StringFormat='{0:dddd, MMM dd}'}" 
                                                   FontSize="14" />
                                            <Label Text="{Binding CheckInTime, StringFormat='In: {0:HH:mm}'}" 
                                                   FontSize="12" TextColor="Gray" />
                                            <Label Text="{Binding CheckOutTime, StringFormat='Out: {0:HH:mm}'}" 
                                                   FontSize="12" TextColor="Gray" />
                                        </StackLayout>
                                        <Label Grid.Column="2" Text="{Binding Status}" 
                                               FontSize="12" VerticalOptions="Center" />
                                    </Grid>
                                </Frame>
                            </DataTemplate>
                        </CollectionView.ItemTemplate>
                    </CollectionView>
                </StackLayout>

                <!-- Notifications -->
                <StackLayout>
                    <Label Text="Notifications" FontSize="18" FontAttributes="Bold" />
                    <CollectionView ItemsSource="{Binding Notifications}" HeightRequest="150">
                        <CollectionView.ItemTemplate>
                            <DataTemplate>
                                <Frame BackgroundColor="{StaticResource CardBackground}" 
                                       Padding="15" Margin="0,5" HasShadow="True">
                                    <Grid ColumnDefinitions="Auto,*">
                                        <Label Grid.Column="0" Text="&#x1F514;" FontSize="20" VerticalOptions="Center" />
                                        <StackLayout Grid.Column="1" Margin="10,0">
                                            <Label Text="{Binding Title}" FontSize="14" FontAttributes="Bold" />
                                            <Label Text="{Binding Message}" FontSize="12" TextColor="Gray" />
                                            <Label Text="{Binding CreatedAt, StringFormat='{0:HH:mm}'}" 
                                                   FontSize="10" TextColor="Gray" />
                                        </StackLayout>
                                    </Grid>
                                </Frame>
                            </DataTemplate>
                        </CollectionView.ItemTemplate>
                    </CollectionView>
                </StackLayout>
            </StackLayout>
        </ScrollView>

        <!-- Refresh Indicator -->
        <RefreshView Grid.Row="2" Command="{Binding RefreshCommand}" IsRefreshing="{Binding IsRefreshing}">
            <!-- Content goes here -->
        </RefreshView>
    </Grid>
</ContentPage>
```

#### **Day 16-17: Theming System**
```csharp
// Resources/Styles/Theme.cs
public static class Theme
{
    public static Color Primary { get; } = Color.FromRgb(41, 128, 185);
    public static Color PrimaryDark { get; } = Color.FromRgb(31, 97, 141);
    public static Color PrimaryLight { get; } = Color.FromRgb(52, 152, 219);
    
    public static Color Secondary { get; } = Color.FromRgb(155, 89, 182);
    public static Color SecondaryDark { get; } = Color.FromRgb(142, 68, 173);
    public static Color SecondaryLight { get; } = Color.FromRgb(186, 104, 200);
    
    public static Color Success { get; } = Color.FromRgb(39, 174, 96);
    public static Color Warning { get; } = Color.FromRgb(243, 156, 18);
    public static Color Danger { get; } = Color.FromRgb(231, 76, 60);
    public static Color Info { get; } = Color.FromRgb(52, 152, 219);
    
    public static Color Background { get; } = Color.FromRgb(248, 249, 250);
    public static Color Surface { get; } = Colors.White;
    public static Color CardBackground { get; } = Colors.White;
    
    public static Color TextPrimary { get; } = Color.FromRgb(44, 62, 80);
    public static Color TextSecondary { get; } = Color.FromRgb(127, 140, 141);
    public static Color TextDisabled { get; } = Color.FromRgb(189, 195, 199);
    
    public static Color Border { get; } = Color.FromRgb(236, 240, 241);
    public static Color Shadow { get; } = Color.FromRgb(0, 0, 0).Multiply(0.1);
}

// Services/ThemeService.cs
public interface IThemeService
{
    event EventHandler<ThemeChangedEventArgs> ThemeChanged;
    Theme CurrentTheme { get; }
    Task SetThemeAsync(Theme theme);
    Task ToggleThemeAsync();
}

public class ThemeService : IThemeService
{
    private readonly ISecureStorageService _secureStorage;
    private Theme _currentTheme = Theme.Light;

    public event EventHandler<ThemeChangedEventArgs> ThemeChanged;

    public ThemeService(ISecureStorageService secureStorage)
    {
        _secureStorage = secureStorage;
        LoadThemeAsync();
    }

    public Theme CurrentTheme => _currentTheme;

    public async Task SetThemeAsync(Theme theme)
    {
        _currentTheme = theme;
        await _secureStorage.SetAsync("current_theme", theme.Name);
        
        Application.Current.Resources.MergedDictionaries.Clear();
        Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            { "Primary", theme.Primary },
            { "PrimaryDark", theme.PrimaryDark },
            { "PrimaryLight", theme.PrimaryLight },
            { "Secondary", theme.Secondary },
            { "Success", theme.Success },
            { "Warning", theme.Warning },
            { "Danger", theme.Danger },
            { "Info", theme.Info },
            { "Background", theme.Background },
            { "Surface", theme.Surface },
            { "CardBackground", theme.CardBackground },
            { "TextPrimary", theme.TextPrimary },
            { "TextSecondary", theme.TextSecondary },
            { "TextDisabled", theme.TextDisabled },
            { "Border", theme.Border },
            { "Shadow", theme.Shadow }
        });

        // Update status bar color
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            Platform.CurrentActivity?.Window?.SetStatusBarColor(theme.PrimaryDark.ToAndroid());
        }

        ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(theme));
    }

    public async Task ToggleThemeAsync()
    {
        var newTheme = _currentTheme.Name == "Light" ? Theme.Dark : Theme.Light;
        await SetThemeAsync(newTheme);
    }

    private async Task LoadThemeAsync()
    {
        var themeName = await _secureStorage.GetAsync("current_theme");
        var theme = themeName == "Dark" ? Theme.Dark : Theme.Light;
        await SetThemeAsync(theme);
    }
}

public class Theme
{
    public string Name { get; init; }
    public Color Primary { get; init; }
    public Color PrimaryDark { get; init; }
    public Color PrimaryLight { get; init; }
    public Color Secondary { get; init; }
    public Color Success { get; init; }
    public Color Warning { get; init; }
    public Color Danger { get; init; }
    public Color Info { get; init; }
    public Color Background { get; init; }
    public Color Surface { get; init; }
    public Color CardBackground { get; init; }
    public Color TextPrimary { get; init; }
    public Color TextSecondary { get; init; }
    public Color TextDisabled { get; init; }
    public Color Border { get; init; }
    public Color Shadow { get; init; }

    public static Theme Light { get; } = new Theme
    {
        Name = "Light",
        Primary = Color.FromRgb(41, 128, 185),
        PrimaryDark = Color.FromRgb(31, 97, 141),
        PrimaryLight = Color.FromRgb(52, 152, 219),
        Secondary = Color.FromRgb(155, 89, 182),
        Success = Color.FromRgb(39, 174, 96),
        Warning = Color.FromRgb(243, 156, 18),
        Danger = Color.FromRgb(231, 76, 60),
        Info = Color.FromRgb(52, 152, 219),
        Background = Color.FromRgb(248, 249, 250),
        Surface = Colors.White,
        CardBackground = Colors.White,
        TextPrimary = Color.FromRgb(44, 62, 80),
        TextSecondary = Color.FromRgb(127, 140, 141),
        TextDisabled = Color.FromRgb(189, 195, 199),
        Border = Color.FromRgb(236, 240, 241),
        Shadow = Color.FromRgb(0, 0, 0).Multiply(0.1)
    };

    public static Theme Dark { get; } = new Theme
    {
        Name = "Dark",
        Primary = Color.FromRgb(52, 152, 219),
        PrimaryDark = Color.FromRgb(41, 128, 185),
        PrimaryLight = Color.FromRgb(133, 193, 233),
        Secondary = Color.FromRgb(186, 104, 200),
        Success = Color.FromRgb(46, 204, 113),
        Warning = Color.FromRgb(241, 196, 15),
        Danger = Color.FromRgb(231, 76, 60),
        Info = Color.FromRgb(52, 152, 219),
        Background = Color.FromRgb(44, 62, 80),
        Surface = Color.FromRgb(52, 73, 94),
        CardBackground = Color.FromRgb(52, 73, 94),
        TextPrimary = Colors.White,
        TextSecondary = Color.FromRgb(189, 195, 199),
        TextDisabled = Color.FromRgb(149, 165, 166),
        Border = Color.FromRgb(52, 73, 94),
        Shadow = Color.FromRgb(0, 0, 0).Multiply(0.3)
    };
}
```

### **3.4 Phase 4: Testing & Deployment (Week 7-8)**

#### **Day 18-20: Unit Testing**
```csharp
// Tests/ViewModels/DashboardViewModelTests.cs
public class DashboardViewModelTests
{
    private readonly Mock<INavigationService> _navigationServiceMock;
    private readonly Mock<IDialogService> _dialogServiceMock;
    private readonly Mock<IConnectivityService> _connectivityServiceMock;
    private readonly Mock<IAuthService> _authServiceMock;
    private readonly Mock<IEmployeeService> _employeeServiceMock;
    private readonly Mock<IAttendanceService> _attendanceServiceMock;
    private readonly Mock<IPayrollService> _payrollServiceMock;

    private readonly DashboardViewModel _viewModel;

    public DashboardViewModelTests()
    {
        _navigationServiceMock = new Mock<INavigationService>();
        _dialogServiceMock = new Mock<IDialogService>();
        _connectivityServiceMock = new Mock<IConnectivityService>();
        _authServiceMock = new Mock<IAuthService>();
        _employeeServiceMock = new Mock<IEmployeeService>();
        _attendanceServiceMock = new Mock<IAttendanceService>();
        _payrollServiceMock = new Mock<IPayrollService>();

        _viewModel = new DashboardViewModel(
            _navigationServiceMock.Object,
            _dialogServiceMock.Object,
            _connectivityServiceMock.Object,
            _authServiceMock.Object,
            _employeeServiceMock.Object,
            _attendanceServiceMock.Object,
            _payrollServiceMock.Object);
    }

    [Fact]
    public async Task InitializeAsync_Should_Load_Data()
    {
        // Arrange
        var employee = new Employee { Id = Guid.NewGuid(), Name = "John Doe" };
        var attendance = new List<AttendanceRecord>
        {
            new() { Id = Guid.NewGuid(), EmployeeId = employee.Id, CheckInTime = DateTime.Now.AddHours(-8) }
        };
        var payrollSummary = new PayrollSummary { TotalSalary = 50000 };

        _authServiceMock.Setup(x => x.GetCurrentEmployeeAsync()).ReturnsAsync(employee);
        _attendanceServiceMock.Setup(x => x.GetRecentAttendanceAsync(employee.Id, 5)).ReturnsAsync(attendance);
        _payrollServiceMock.Setup(x => x.GetPayrollSummaryAsync(employee.Id)).ReturnsAsync(payrollSummary);

        // Act
        await _viewModel.InitializeAsync();

        // Assert
        _viewModel.CurrentEmployee.Should().Be(employee);
        _viewModel.RecentAttendance.Should().HaveCount(1);
        _viewModel.PayrollSummary.Should().Be(payrollSummary);
    }

    [Fact]
    public async Task RefreshDataAsync_Should_Reload_Data()
    {
        // Arrange
        var employee = new Employee { Id = Guid.NewGuid(), Name = "John Doe" };
        var attendance = new List<AttendanceRecord>
        {
            new() { Id = Guid.NewGuid(), EmployeeId = employee.Id, CheckInTime = DateTime.Now.AddHours(-8) }
        };

        _authServiceMock.Setup(x => x.GetCurrentEmployeeAsync()).ReturnsAsync(employee);
        _attendanceServiceMock.Setup(x => x.GetRecentAttendanceAsync(employee.Id, 5)).ReturnsAsync(attendance);

        // Act
        await _viewModel.RefreshDataAsync();

        // Assert
        _viewModel.RecentAttendance.Should().HaveCount(1);
        _viewModel.IsRefreshing.Should().BeFalse();
    }

    [Fact]
    public async Task ViewAttendanceCommand_Should_Navigate_To_Attendance_Page()
    {
        // Act
        await _viewModel.ViewAttendanceCommand.ExecuteAsync(null);

        // Assert
        _navigationServiceMock.Verify(x => x.NavigateToAsync(nameof(AttendancePage)), Times.Once);
    }
}

// Tests/Services/AttendanceServiceTests.cs
public class AttendanceServiceTests
{
    private readonly Mock<IApiService> _apiServiceMock;
    private readonly Mock<IOfflineDataService> _offlineDataServiceMock;
    private readonly Mock<IGeolocationService> _geolocationServiceMock;
    private readonly Mock<IConnectivityService> _connectivityServiceMock;

    private readonly AttendanceService _attendanceService;

    public AttendanceServiceTests()
    {
        _apiServiceMock = new Mock<IApiService>();
        _offlineDataServiceMock = new Mock<IOfflineDataService>();
        _geolocationServiceMock = new Mock<IGeolocationService>();
        _connectivityServiceMock = new Mock<IConnectivityService>();

        _attendanceService = new AttendanceService(
            _apiServiceMock.Object,
            _offlineDataServiceMock.Object,
            _geolocationServiceMock.Object);
    }

    [Fact]
    public async Task CheckInAsync_With_Connectivity_Should_Call_Api()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var location = new Location(10.0, 20.0);
        var attendanceResponse = new AttendanceResponse
        {
            Success = true,
            Data = new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                EmployeeId = employeeId,
                CheckInTime = DateTime.UtcNow
            }
        };

        _geolocationServiceMock.Setup(x => x.GetLocationAsync()).ReturnsAsync(location);
        _connectivityServiceMock.Setup(x => x.IsConnectedAsync()).ReturnsAsync(true);
        _apiServiceMock.Setup(x => x.PostAsync<AttendanceResponse>("/api/attendance/checkin", It.IsAny<CheckInRequest>()))
            .ReturnsAsync(new ApiResponse<AttendanceResponse> { Success = true, Data = attendanceResponse });

        // Act
        var result = await _attendanceService.CheckInAsync(employeeId);

        // Assert
        result.Should().BeTrue();
        _apiServiceMock.Verify(x => x.PostAsync<AttendanceResponse>("/api/attendance/checkin", It.IsAny<CheckInRequest>()), Times.Once);
        _offlineDataServiceMock.Verify(x => x.SaveAttendanceAsync(attendanceResponse.Data), Times.Once);
    }

    [Fact]
    public async Task CheckInAsync_Without_Connectivity_Should_Save_Offline()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var location = new Location(10.0, 20.0);

        _geolocationServiceMock.Setup(x => x.GetLocationAsync()).ReturnsAsync(location);
        _connectivityServiceMock.Setup(x => x.IsConnectedAsync()).ReturnsAsync(false);

        // Act
        var result = await _attendanceService.CheckInAsync(employeeId);

        // Assert
        result.Should().BeTrue();
        _offlineDataServiceMock.Verify(x => x.SaveAttendanceAsync(It.Is<AttendanceRecord>(a => 
            a.EmployeeId == employeeId && a.IsSynced == false)), Times.Once);
    }
}
```

#### **Day 21-22: Integration Testing**
```csharp
// Tests/Integration/AttendanceIntegrationTests.cs
public class AttendanceIntegrationTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly VanAnDbContext _context;

    public AttendanceIntegrationTests()
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
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            Name = "Test Employee",
            Email = "test@example.com",
            Position = "Developer"
        };

        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task CheckIn_Should_Create_Attendance_Record()
    {
        // Arrange
        var employee = await _context.Employees.FirstAsync();
        var checkInRequest = new CheckInRequest
        {
            EmployeeId = employee.Id,
            CheckInTime = DateTime.UtcNow,
            Latitude = 10.0,
            Longitude = 20.0,
            DeviceInfo = "Test Device"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/attendance/checkin", checkInRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var attendanceResponse = await response.Content.ReadFromJsonAsync<ApiResponse<AttendanceResponse>>();
        attendanceResponse.Success.Should().BeTrue();
        
        var attendance = await _context.AttendanceRecords.FindAsync(attendanceResponse.Data.Id);
        attendance.Should().NotBeNull();
        attendance.EmployeeId.Should().Be(employee.Id);
        attendance.CheckInTime.Should().BeCloseTo(checkInRequest.CheckInTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetRecentAttendance_Should_Return_Recent_Records()
    {
        // Arrange
        var employee = await _context.Employees.FirstAsync();
        
        // Create some attendance records
        for (int i = 0; i < 5; i++)
        {
            var attendance = new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                EmployeeId = employee.Id,
                CheckInTime = DateTime.UtcNow.AddDays(-i),
                CheckOutTime = DateTime.UtcNow.AddDays(-i).AddHours(8)
            };
            
            _context.AttendanceRecords.Add(attendance);
        }
        
        await _context.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync($"/api/attendance/recent/{employee.Id}?count=3");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var attendanceResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<AttendanceRecord>>>();
        attendanceResponse.Success.Should().BeTrue();
        attendanceResponse.Data.Should().HaveCount(3);
    }
}
```

---

## **4. IMPLEMENTATION PLAN**

### **4.1 Week 1-2: Foundation & Architecture**
- [ ] Setup MVVM architecture
- [ ] Implement dependency injection
- [ ] Create authentication system
- [ ] Setup navigation service
- [ ] Add biometric authentication
- [ ] Create base view models

### **4.2 Week 3-4: HR Features**
- [ ] Implement attendance management
- [ ] Add payroll management
- [ ] Create leave management
- [ ] Add employee profile
- [ ] Implement offline sync
- [ ] Add real-time updates

### **4.3 Week 5-6: UI/UX Enhancement**
- [ ] Design professional UI
- [ ] Implement theming system
- [ ] Add responsive design
- [ ] Create accessibility features
- [ ] Add localization support
- [ ] Implement animations

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
- **Performance:** <2 seconds app startup
- **Memory Usage:** <100MB under normal usage
- **Battery Impact:** <5% battery drain per hour

### **5.2 User Experience Metrics**
- **App Launch Time:** <2 seconds
- **Screen Load Time:** <1 second
- **Offline Functionality:** 100% core features offline
- **Biometric Success Rate:** >95%

### **5.3 Business Metrics**
- **User Adoption:** >80% employee adoption
- **Daily Active Users:** >70% of employees
- **Feature Usage:** >90% of features used monthly
- **User Satisfaction:** >4.5/5 rating

---

## **6. RISK MITIGATION**

### **6.1 Technical Risks**
1. **Performance Issues** - Implement performance monitoring
2. **Offline Sync Conflicts** - Implement conflict resolution
3. **Security Vulnerabilities** - Regular security audits
4. **Platform Compatibility** - Cross-platform testing

### **6.2 Business Risks**
1. **User Adoption** - User training and support
2. **Feature Complexity** - Simplify UI/UX design
3. **Data Privacy** - Implement data protection
4. **Maintenance Overhead** - Automated maintenance

---

## **7. NEXT STEPS**

### **7.1 Immediate Actions (This Week)**
1. **Setup Architecture** - MVVM foundation
2. **Create Authentication** - Login system
3. **Implement DI** - Service registration
4. **Add Navigation** - Page routing

### **7.2 Short-term Goals (2 Weeks)**
1. **Complete HR Features** - Attendance & payroll
2. **Implement Offline** - Offline sync
3. **Add Real-time** - Live updates
4. **Create Professional UI** - Modern design

### **7.3 Long-term Goals (2 Months)**
1. **Complete All Features** - Full HR app
2. **Achieve Performance** - Optimize performance
3. **User Testing** - Beta testing
4. **Production Deployment** - App store release

---

## **8. SUMMARY**

### **8.1 Current State**
- **Basic MAUI template** with no HR features
- **No authentication** or security
- **No offline support** or real-time features
- **No testing** or documentation

### **8.2 Target State**
- **Professional HR app** with complete features
- **Biometric authentication** and security
- **Offline-first** with real-time sync
- **Comprehensive testing** and monitoring

### **8.3 Implementation Strategy**
- **8-week phased approach** with clear milestones
- **User-centric design** with professional UI
- **Performance focus** with optimization
- **Quality focus** with comprehensive testing

**Status:** HrApp module là completely empty template with significant gaps but có clear improvement plan v?i professional HR app architecture.
