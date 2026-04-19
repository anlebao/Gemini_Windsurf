# HrApp - Lu?ng X? Lý T??ng (Ideal Architecture)

**Ngày:** 11 tháng 4, 2026  
**Module:** 4_MobileApps/HRApp  
**Tr?ng thái:** Thi?t k? lu?ng x? lý lý t??ng ho?n ch?nh

---

## **1. T?NG QUAN ARCHITECTURE LÝ T??NG**

### **1.1 HR Mobile App Architecture**
```
4_MobileApps/HRApp/
?? Configuration/
   ?? appsettings.json
   ?? appsettings.Development.json
   ?? appsettings.Production.json
   ?? Constants/
      ?? AppConstants.cs
      ?? ApiEndpoints.cs
      ?? NavigationRoutes.cs
?? Core/
   ?? Models/
      ?? Employee/
         ?? Employee.cs
         ?? EmployeeProfile.cs
         ?? EmployeeContact.cs
      ?? Attendance/
         ?? AttendanceRecord.cs
         ?? Timesheet.cs
         ?? OvertimeRecord.cs
      ?? Leave/
         ?? LeaveRequest.cs
         ?? LeaveBalance.cs
         ?? LeaveType.cs
      ?? Payroll/
         ?? PayrollRecord.cs
         ?? SalaryComponent.cs
         ?? TaxInfo.cs
      ?? Performance/
         ?? PerformanceReview.cs
         ?? Goal.cs
         ?? Feedback.cs
   ?? ViewModels/
      ?? BaseViewModel.cs
      ?? Employee/
         ?? EmployeeListViewModel.cs
         ?? EmployeeDetailViewModel.cs
         ?? EmployeeProfileViewModel.cs
      ?? Attendance/
         ?? AttendanceViewModel.cs
         ?? TimesheetViewModel.cs
         ?? CheckInViewModel.cs
      ?? Leave/
         ?? LeaveRequestViewModel.cs
         ?? LeaveBalanceViewModel.cs
         ?? LeaveHistoryViewModel.cs
      ?? Payroll/
         ?? PayrollViewModel.cs
         ?? PayslipViewModel.cs
         ?? TaxViewModel.cs
      ?? Performance/
         ?? PerformanceViewModel.cs
         ?? GoalViewModel.cs
         ?? FeedbackViewModel.cs
      ?? Auth/
         ?? LoginViewModel.cs
         ?? ProfileViewModel.cs
         ?? SettingsViewModel.cs
   ?? Services/
      ?? Interfaces/
         ?? IAuthService.cs
         ?? IEmployeeService.cs
         ?? IAttendanceService.cs
         ?? ILeaveService.cs
         ?? IPayrollService.cs
         ?? IPerformanceService.cs
         ?? INotificationService.cs
         ?? IStorageService.cs
         ?? IGeolocationService.cs
         ?? IBiometricService.cs
      ?? Implementations/
         ?? AuthService.cs
         ?? EmployeeService.cs
         ?? AttendanceService.cs
         ?? LeaveService.cs
         ?? PayrollService.cs
         ?? PerformanceService.cs
         ?? NotificationService.cs
         ?? StorageService.cs
         ?? GeolocationService.cs
         ?? BiometricService.cs
   ?? Repositories/
      ?? Interfaces/
         ?? IEmployeeRepository.cs
         ?? IAttendanceRepository.cs
         ?? ILeaveRepository.cs
         ?? IPayrollRepository.cs
         ?? IPerformanceRepository.cs
      ?? Implementations/
         ?? EmployeeRepository.cs
         ?? AttendanceRepository.cs
         ?? LeaveRepository.cs
         ?? PayrollRepository.cs
         ?? PerformanceRepository.cs
?? Views/
   ?? Pages/
      ?? Auth/
         ?? LoginPage.xaml
         ?? LoginPage.xaml.cs
      ?? Dashboard/
         ?? DashboardPage.xaml
         ?? DashboardPage.xaml.cs
      ?? Employee/
         ?? EmployeeListPage.xaml
         ?? EmployeeListPage.xaml.cs
         ?? EmployeeDetailPage.xaml
         ?? EmployeeDetailPage.xaml.cs
         ?? EmployeeProfilePage.xaml
         ?? EmployeeProfilePage.xaml.cs
      ?? Attendance/
         ?? AttendancePage.xaml
         ?? AttendancePage.xaml.cs
         ?? CheckInPage.xaml
         ?? CheckInPage.xaml.cs
         ?? TimesheetPage.xaml
         ?? TimesheetPage.xaml.cs
      ?? Leave/
         ?? LeaveRequestPage.xaml
         ?? LeaveRequestPage.xaml.cs
         ?? LeaveBalancePage.xaml
         ?? LeaveBalancePage.xaml.cs
         ?? LeaveHistoryPage.xaml
         ?? LeaveHistoryPage.xaml.cs
      ?? Payroll/
         ?? PayrollPage.xaml
         ?? PayrollPage.xaml.cs
         ?? PayslipPage.xaml
         ?? PayslipPage.xaml.cs
      ?? Performance/
         ?? PerformancePage.xaml
         ?? PerformancePage.xaml.cs
         ?? GoalPage.xaml
         ?? GoalPage.xaml.cs
      ?? Settings/
         ?? SettingsPage.xaml
         ?? SettingsPage.xaml.cs
   ?? Controls/
      ?? Custom/
         ?? EmployeeCard.xaml
         ?? AttendanceChart.xaml
         ?? LeaveCalendar.xaml
         ?? PayrollSummary.xaml
   ?? Templates/
      ?? DataTemplates/
         ?? EmployeeTemplate.xaml
         ?? AttendanceTemplate.xaml
         ?? LeaveTemplate.xaml
?? Infrastructure/
   ?? Http/
      ?? HttpClientFactory.cs
      ?? HttpMessageHandler.cs
      ?? ApiException.cs
   ?? Security/
      ?? TokenManager.cs
      ?? BiometricAuthenticator.cs
      ?? SecureStorage.cs
   ?? Storage/
      ?? LocalStorage.cs
      ?? CacheManager.cs
      ?? OfflineSync.cs
   ?? Location/
      ?? LocationService.cs
      ?? GeofencingService.cs
   ?? Notifications/
      ?? PushNotificationService.cs
      ?? LocalNotificationService.cs
      ?? NotificationScheduler.cs
   ?? Logging/
      ?? Logger.cs
      ?? CrashReporter.cs
   ?? Analytics/
      ?? AnalyticsService.cs
      ?? UsageTracker.cs
?? Resources/
   ?? Styles/
      ?? Colors.xaml
      ?? Fonts.xaml
      ?? Styles.xaml
      ?? Themes/
         ?? LightTheme.xaml
         ?? DarkTheme.xaml
   ?? Images/
      ?? Icons/
      ?? Logos/
   ?? Localization/
      ?? Resources.resx
      ?? Resources.vi-VN.resx
      ?? Resources.en-US.resx
?? Tests/
   ?? Unit/
      ?? ViewModels/
         ?? EmployeeViewModelTests.cs
         ?? AttendanceViewModelTests.cs
         ?? LeaveViewModelTests.cs
         ?? PayrollViewModelTests.cs
      ?? Services/
         ?? AuthServiceTests.cs
         ?? EmployeeServiceTests.cs
         ?? AttendanceServiceTests.cs
      ?? Repositories/
         ?? EmployeeRepositoryTests.cs
         ?? AttendanceRepositoryTests.cs
   ?? Integration/
      ?? ApiIntegrationTests.cs
      ?? StorageIntegrationTests.cs
      ?? AuthIntegrationTests.cs
   ?? UI/
      ?? PageTests.cs
      ?? ControlTests.cs
?? Platforms/
   ?? Android/
      ?? Services/
         ?? AndroidBiometricService.cs
         ?? AndroidLocationService.cs
         ?? AndroidNotificationService.cs
      ?? Permissions/
         ?? AndroidPermissions.cs
      ?? MainActivity.cs
      ?? MainApplication.cs
   ?? iOS/
      ?? Services/
         ?? iOSBiometricService.cs
         ?? iOSLocationService.cs
         ?? iOSNotificationService.cs
      ?? Permissions/
         ?? iOSPermissions.cs
      ?? AppDelegate.cs
      ?? Program.cs
   ?? Windows/
      ?? Services/
         ?? WindowsBiometricService.cs
         ?? WindowsLocationService.cs
         ?? WindowsNotificationService.cs
      ?? Permissions/
         ?? WindowsPermissions.cs
      ?? App.xaml.cs
?? App.xaml
?? App.xaml.cs
?? AppShell.xaml
?? AppShell.xaml.cs
?? MauiProgram.cs
?? VanAn.HRApp.csproj
```

---

## **2. LU?NG X? LÝ LÝ T??NG**

### **2.1 Application Startup**

#### **Phase 1: MAUI Program Configuration**
```csharp
// MauiProgram.cs
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        
        // Register fonts
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
            });

        // Configure services
        ConfigureServices(builder.Services);

        return builder.Build();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core services
        services.AddSingleton<IConnectivity, ConnectivityImplementation>();
        services.AddSingleton<IGeolocation, GeolocationImplementation>();
        services.AddSingleton<ISecureStorage, SecureStorageImplementation>();
        
        // App services
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IEmployeeService, EmployeeService>();
        services.AddSingleton<IAttendanceService, AttendanceService>();
        services.AddSingleton<ILeaveService, LeaveService>();
        services.AddSingleton<IPayrollService, PayrollService>();
        services.AddSingleton<IPerformanceService, PerformanceService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IStorageService, StorageService>();
        services.AddSingleton<IBiometricService, BiometricService>();
        
        // Repository services
        services.AddSingleton<IEmployeeRepository, EmployeeRepository>();
        services.AddSingleton<IAttendanceRepository, AttendanceRepository>();
        services.AddSingleton<ILeaveRepository, LeaveRepository>();
        services.AddSingleton<IPayrollRepository, PayrollRepository>();
        services.AddSingleton<IPerformanceRepository, PerformanceRepository>();
        
        // Infrastructure services
        services.AddSingleton<IHttpClientFactory, HttpClientFactory>();
        services.AddSingleton<ITokenManager, TokenManager>();
        services.AddSingleton<ICacheManager, CacheManager>();
        services.AddSingleton<IOfflineSync, OfflineSync>();
        services.AddSingleton<ILocationService, LocationService>();
        services.AddSingleton<IPushNotificationService, PushNotificationService>();
        services.AddSingleton<ILogger, Logger>();
        services.AddSingleton<IAnalyticsService, AnalyticsService>();
        
        // ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<EmployeeListViewModel>();
        services.AddTransient<EmployeeDetailViewModel>();
        services.AddTransient<EmployeeProfileViewModel>();
        services.AddTransient<AttendanceViewModel>();
        services.AddTransient<CheckInViewModel>();
        services.AddTransient<TimesheetViewModel>();
        services.AddTransient<LeaveRequestViewModel>();
        services.AddTransient<LeaveBalanceViewModel>();
        services.AddTransient<LeaveHistoryViewModel>();
        services.AddTransient<PayrollViewModel>();
        services.AddTransient<PayslipViewModel>();
        services.AddTransient<PerformanceViewModel>();
        services.AddTransient<GoalViewModel>();
        services.AddTransient<FeedbackViewModel>();
        services.AddTransient<ProfileViewModel>();
        services.AddTransient<SettingsViewModel>();
    }
}
```

#### **Phase 2: Application Configuration**
```csharp
// App.xaml.cs
public partial class App : Application
{
    private readonly IAuthService _authService;
    private readonly INotificationService _notificationService;
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger _logger;

    public App(IAuthService authService, INotificationService notificationService, 
              IAnalyticsService analyticsService, ILogger logger)
    {
        InitializeComponent();
        
        _authService = authService;
        _notificationService = notificationService;
        _analyticsService = analyticsService;
        _logger = logger;
        
        // Configure theme
        ConfigureTheme();
        
        // Initialize services
        InitializeServices();
    }

    protected override async void OnStart()
    {
        try
        {
            _logger.LogInformation("Application starting");
            
            // Check authentication status
            var isAuthenticated = await _authService.IsAuthenticatedAsync();
            
            if (isAuthenticated)
            {
                // Navigate to dashboard
                MainPage = new AppShell();
                await _analyticsService.TrackEvent("app_start_authenticated");
            }
            else
            {
                // Navigate to login
                MainPage = new NavigationPage(new LoginPage());
                await _analyticsService.TrackEvent("app_start_unauthenticated");
            }
            
            // Initialize notifications
            await _notificationService.InitializeAsync();
            
            base.OnStart();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting application");
            await HandleStartupError(ex);
        }
    }

    protected override void OnSleep()
    {
        _logger.LogInformation("Application sleeping");
        _analyticsService.TrackEvent("app_sleep");
        base.OnSleep();
    }

    protected override void OnResume()
    {
        _logger.LogInformation("Application resuming");
        _analyticsService.TrackEvent("app_resume");
        base.OnResume();
    }

    private void ConfigureTheme()
    {
        // Apply theme based on user preferences
        var theme = Application.Current.UserAppTheme;
        // Theme configuration logic
    }

    private async void InitializeServices()
    {
        await _notificationService.RequestPermissionAsync();
        await _analyticsService.InitializeAsync();
    }

    private async Task HandleStartupError(Exception ex)
    {
        // Handle startup errors gracefully
        await MainPage.DisplayAlert("Error", "Failed to start application. Please try again.", "OK");
    }
}
```

---

### **2.2 Authentication Flow**

#### **Phase 1: Login ViewModel**
```csharp
// ViewModels/Auth/LoginViewModel.cs
public class LoginViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly IBiometricService _biometricService;
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger _logger;

    [ObservableProperty]
    private string _username;

    [ObservableProperty]
    private string _password;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _rememberMe;

    [ObservableProperty]
    private bool _biometricAvailable;

    [ObservableProperty]
    private string _biometricType;

    public LoginViewModel(IAuthService authService, IBiometricService biometricService,
                        IAnalyticsService analyticsService, ILogger logger)
    {
        _authService = authService;
        _biometricService = biometricService;
        _analyticsService = analyticsService;
        _logger = logger;
        
        Title = "Login";
        CheckBiometricAvailability();
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsLoading) return;
        
        try
        {
            IsLoading = true;
            
            // Validate input
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                await ShowErrorAsync("Please enter username and password");
                return;
            }

            // Authenticate
            var result = await _authService.LoginAsync(Username, Password, RememberMe);
            
            if (result.Success)
            {
                _analyticsService.TrackEvent("login_success", new Dictionary<string, string>
                {
                    ["method"] = "password"
                });
                
                await Shell.Current.GoToAsync("//dashboard");
            }
            else
            {
                await ShowErrorAsync(result.Message);
                _analyticsService.TrackEvent("login_failed", new Dictionary<string, string>
                {
                    ["reason"] = result.Message
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error");
            await ShowErrorAsync("Login failed. Please try again.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoginWithBiometricAsync()
    {
        if (IsLoading) return;
        
        try
        {
            IsLoading = true;
            
            var result = await _biometricService.AuthenticateAsync();
            
            if (result.Success)
            {
                _analyticsService.TrackEvent("login_success", new Dictionary<string, string>
                {
                    ["method"] = "biometric"
                });
                
                await Shell.Current.GoToAsync("//dashboard");
            }
            else
            {
                await ShowErrorAsync(result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Biometric login error");
            await ShowErrorAsync("Biometric authentication failed.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async void CheckBiometricAvailability()
    {
        try
        {
            var availability = await _biometricService.CheckAvailabilityAsync();
            BiometricAvailable = availability.IsAvailable;
            BiometricType = availability.Type.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking biometric availability");
        }
    }

    private async Task ShowErrorAsync(string message)
    {
        await Shell.Current.DisplayAlert("Error", message, "OK");
    }
}
```

#### **Phase 2: Authentication Service**
```csharp
// Services/Implementations/AuthService.cs
public class AuthService : IAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenManager _tokenManager;
    private readonly IStorageService _storageService;
    private readonly ILogger _logger;

    public AuthService(IHttpClientFactory httpClientFactory, ITokenManager tokenManager,
                      IStorageService storageService, ILogger logger)
    {
        _httpClientFactory = httpClientFactory;
        _tokenManager = tokenManager;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<AuthResult> LoginAsync(string username, string password, bool rememberMe)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            
            var loginRequest = new LoginRequest
            {
                Username = username,
                Password = password,
                RememberMe = rememberMe,
                DeviceInfo = await GetDeviceInfoAsync(),
                Location = await GetLocationAsync()
            };

            var response = await httpClient.PostAsJsonAsync(ApiEndpoints.Login, loginRequest);
            
            if (response.IsSuccessStatusCode)
            {
                var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
                
                // Store tokens
                await _tokenManager.StoreTokensAsync(authResponse.AccessToken, authResponse.RefreshToken);
                
                // Store user info
                await _storageService.SetAsync("user_info", authResponse.User);
                
                if (rememberMe)
                {
                    await _storageService.SetAsync("remembered_username", username);
                }
                
                return new AuthResult { Success = true };
            }
            else
            {
                var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                return new AuthResult { Success = false, Message = errorResponse.Message };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error");
            return new AuthResult { Success = false, Message = "Login failed. Please try again." };
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        try
        {
            var token = await _tokenManager.GetAccessTokenAsync();
            return !string.IsNullOrEmpty(token) && !await _tokenManager.IsTokenExpiredAsync(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking authentication status");
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            // Clear tokens
            await _tokenManager.ClearTokensAsync();
            
            // Clear stored user info
            await _storageService.RemoveAsync("user_info");
            
            _logger.LogInformation("User logged out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
        }
    }

    public async Task<UserInfo> GetCurrentUserAsync()
    {
        try
        {
            return await _storageService.GetAsync<UserInfo>("user_info");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
            return null;
        }
    }

    private async Task<DeviceInfo> GetDeviceInfoAsync()
    {
        return new DeviceInfo
        {
            DeviceId = DeviceInfo.Idiom.ToString(),
            Platform = DeviceInfo.Platform.ToString(),
            Version = DeviceInfo.Version.ToString(),
            Model = DeviceInfo.Model,
            Manufacturer = DeviceInfo.Manufacturer
        };
    }

    private async Task<LocationInfo> GetLocationAsync()
    {
        try
        {
            var location = await Geolocation.Default.GetLastKnownLocationAsync();
            
            return location != null ? new LocationInfo
            {
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                Accuracy = location.Accuracy
            } : null;
        }
        catch
        {
            return null;
        }
    }
}
```

---

### **2.3 Attendance Management**

#### **Phase 1: Check-in Flow**
```csharp
// ViewModels/Attendance/CheckInViewModel.cs
public class CheckInViewModel : BaseViewModel
{
    private readonly IAttendanceService _attendanceService;
    private readonly ILocationService _locationService;
    private readonly IBiometricService _biometricService;
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger _logger;

    [ObservableProperty]
    private LocationInfo _currentLocation;

    [ObservableProperty]
    private bool _isWithinGeofence;

    [ObservableProperty]
    private bool _canCheckIn;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private DateTime? _lastCheckIn;

    [ObservableProperty]
    private string _checkInStatus;

    public CheckInViewModel(IAttendanceService attendanceService, ILocationService locationService,
                           IBiometricService biometricService, IAnalyticsService analyticsService,
                           ILogger logger)
    {
        _attendanceService = attendanceService;
        _locationService = locationService;
        _biometricService = biometricService;
        _analyticsService = analyticsService;
        _logger = logger;
        
        Title = "Check In";
        LoadAttendanceStatus();
    }

    [RelayCommand]
    private async Task CheckInAsync()
    {
        if (IsLoading) return;
        
        try
        {
            IsLoading = true;
            
            // Validate location
            if (!IsWithinGeofence)
            {
                await ShowErrorAsync("You must be within the office premises to check in");
                return;
            }

            // Biometric verification
            var biometricResult = await _biometricService.AuthenticateAsync();
            if (!biometricResult.Success)
            {
                await ShowErrorAsync("Biometric verification failed");
                return;
            }

            // Check in
            var checkInRequest = new CheckInRequest
            {
                Location = CurrentLocation,
                Timestamp = DateTime.UtcNow,
                DeviceInfo = await GetDeviceInfoAsync(),
                BiometricHash = biometricResult.BiometricHash
            };

            var result = await _attendanceService.CheckInAsync(checkInRequest);
            
            if (result.Success)
            {
                LastCheckIn = result.Timestamp;
                CheckInStatus = "Checked In";
                CanCheckIn = false;
                
                _analyticsService.TrackEvent("check_in_success");
                
                await ShowSuccessAsync("Check in successful");
            }
            else
            {
                await ShowErrorAsync(result.Message);
                _analyticsService.TrackEvent("check_in_failed", new Dictionary<string, string>
                {
                    ["reason"] = result.Message
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Check in error");
            await ShowErrorAsync("Check in failed. Please try again.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshLocationAsync()
    {
        try
        {
            CurrentLocation = await _locationService.GetCurrentLocationAsync();
            IsWithinGeofence = await _locationService.IsWithinGeofenceAsync(CurrentLocation);
            CanCheckIn = IsWithinGeofence && (LastCheckIn == null || LastCheckIn.Value.Date < DateTime.UtcNow.Date);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing location");
        }
    }

    private async void LoadAttendanceStatus()
    {
        try
        {
            var status = await _attendanceService.GetAttendanceStatusAsync();
            LastCheckIn = status.LastCheckIn;
            CheckInStatus = status.Status;
            
            await RefreshLocationAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading attendance status");
        }
    }

    private async Task ShowErrorAsync(string message)
    {
        await Shell.Current.DisplayAlert("Error", message, "OK");
    }

    private async Task ShowSuccessAsync(string message)
    {
        await Shell.Current.DisplayAlert("Success", message, "OK");
    }
}
```

#### **Phase 2: Attendance Service**
```csharp
// Services/Implementations/AttendanceService.cs
public class AttendanceService : IAttendanceService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILocationService _locationService;
    private readonly IOfflineSync _offlineSync;
    private readonly ILogger _logger;

    public AttendanceService(IHttpClientFactory httpClientFactory, ILocationService locationService,
                            IOfflineSync offlineSync, ILogger logger)
    {
        _httpClientFactory = httpClientFactory;
        _locationService = locationService;
        _offlineSync = offlineSync;
        _logger = logger;
    }

    public async Task<CheckInResult> CheckInAsync(CheckInRequest request)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            
            var response = await httpClient.PostAsJsonAsync(ApiEndpoints.Attendance.CheckIn, request);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<CheckInResult>();
                
                // Sync with offline storage
                await _offlineSync.SyncAttendanceAsync(result);
                
                return result;
            }
            else
            {
                var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                
                // Store for offline sync
                await _offlineSync.QueueForSyncAsync(request);
                
                return new CheckInResult 
                { 
                    Success = false, 
                    Message = errorResponse.Message,
                    QueuedForSync = true
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Check in error");
            
            // Store for offline sync
            await _offlineSync.QueueForSyncAsync(request);
            
            return new CheckInResult 
            { 
                Success = false, 
                Message = "Network error. Check in queued for sync.",
                QueuedForSync = true
            };
        }
    }

    public async Task<AttendanceStatus> GetAttendanceStatusAsync()
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(ApiEndpoints.Attendance.Status);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<AttendanceStatus>();
            }
            else
            {
                // Return offline status
                return await _offlineSync.GetOfflineAttendanceStatusAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting attendance status");
            return await _offlineSync.GetOfflineAttendanceStatusAsync();
        }
    }

    public async Task<List<TimesheetEntry>> GetTimesheetAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync($"{ApiEndpoints.Attendance.Timesheet}?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<TimesheetEntry>>();
            }
            else
            {
                return await _offlineSync.GetOfflineTimesheetAsync(startDate, endDate);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting timesheet");
            return await _offlineSync.GetOfflineTimesheetAsync(startDate, endDate);
        }
    }

    public async Task<List<OvertimeRecord>> GetOvertimeRecordsAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync($"{ApiEndpoints.Attendance.Overtime}?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<OvertimeRecord>>();
            }
            else
            {
                return await _offlineSync.GetOfflineOvertimeRecordsAsync(startDate, endDate);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting overtime records");
            return await _offlineSync.GetOfflineOvertimeRecordsAsync(startDate, endDate);
        }
    }
}
```

---

### **2.4 Leave Management**

#### **Phase 1: Leave Request Flow**
```csharp
// ViewModels/Leave/LeaveRequestViewModel.cs
public class LeaveRequestViewModel : BaseViewModel
{
    private readonly ILeaveService _leaveService;
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger _logger;

    [ObservableProperty]
    private List<LeaveType> _leaveTypes;

    [ObservableProperty]
    private LeaveType _selectedLeaveType;

    [ObservableProperty]
    private DateTime _startDate;

    [ObservableProperty]
    private DateTime _endDate;

    [ObservableProperty]
    private string _reason;

    [ObservableProperty]
    private TimeSpan _duration;

    [ObservableProperty]
    private LeaveBalance _leaveBalance;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _canSubmit;

    public LeaveRequestViewModel(ILeaveService leaveService, IAnalyticsService analyticsService,
                                ILogger logger)
    {
        _leaveService = leaveService;
        _analyticsService = analyticsService;
        _logger = logger;
        
        Title = "Leave Request";
        StartDate = DateTime.Today;
        EndDate = DateTime.Today;
        
        LoadLeaveTypes();
        LoadLeaveBalance();
    }

    partial void OnSelectedLeaveTypeChanged(LeaveType value)
    {
        CalculateDuration();
        ValidateCanSubmit();
    }

    partial void OnStartDateChanged(DateTime value)
    {
        CalculateDuration();
        ValidateCanSubmit();
    }

    partial void OnEndDateChanged(DateTime value)
    {
        CalculateDuration();
        ValidateCanSubmit();
    }

    partial void OnReasonChanged(string value)
    {
        ValidateCanSubmit();
    }

    [RelayCommand]
    private async Task SubmitLeaveRequestAsync()
    {
        if (IsLoading) return;
        
        try
        {
            IsLoading = true;
            
            // Validate input
            if (!ValidateInput())
            {
                return;
            }

            var leaveRequest = new LeaveRequest
            {
                LeaveTypeId = SelectedLeaveType.Id,
                StartDate = StartDate,
                EndDate = EndDate,
                Reason = Reason,
                Duration = Duration,
                Status = LeaveStatus.Pending,
                RequestedAt = DateTime.UtcNow
            };

            var result = await _leaveService.SubmitLeaveRequestAsync(leaveRequest);
            
            if (result.Success)
            {
                _analyticsService.TrackEvent("leave_request_submitted", new Dictionary<string, string>
                {
                    ["leave_type"] = SelectedLeaveType.Name,
                    ["duration"] = Duration.TotalDays.ToString()
                });
                
                await ShowSuccessAsync("Leave request submitted successfully");
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await ShowErrorAsync(result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting leave request");
            await ShowErrorAsync("Failed to submit leave request. Please try again.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async void LoadLeaveTypes()
    {
        try
        {
            LeaveTypes = await _leaveService.GetLeaveTypesAsync();
            SelectedLeaveType = LeaveTypes?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading leave types");
        }
    }

    private async void LoadLeaveBalance()
    {
        try
        {
            LeaveBalance = await _leaveService.GetLeaveBalanceAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading leave balance");
        }
    }

    private void CalculateDuration()
    {
        if (SelectedLeaveType == null || StartDate > EndDate)
        {
            Duration = TimeSpan.Zero;
            return;
        }

        // Calculate business days
        var businessDays = 0;
        var current = StartDate;
        
        while (current <= EndDate)
        {
            if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
            {
                businessDays++;
            }
            current = current.AddDays(1);
        }

        Duration = TimeSpan.FromDays(businessDays);
    }

    private void ValidateCanSubmit()
    {
        CanSubmit = SelectedLeaveType != null && 
                   StartDate <= EndDate && 
                   !string.IsNullOrWhiteSpace(Reason) &&
                   Duration > TimeSpan.Zero &&
                   LeaveBalance?.AvailableDays >= Duration.TotalDays;
    }

    private bool ValidateInput()
    {
        if (SelectedLeaveType == null)
        {
            ShowErrorAsync("Please select a leave type");
            return false;
        }

        if (StartDate > EndDate)
        {
            ShowErrorAsync("Start date cannot be after end date");
            return false;
        }

        if (string.IsNullOrWhiteSpace(Reason))
        {
            ShowErrorAsync("Please provide a reason for your leave request");
            return false;
        }

        if (LeaveBalance?.AvailableDays < Duration.TotalDays)
        {
            ShowErrorAsync("Insufficient leave balance");
            return false;
        }

        return true;
    }

    private async Task ShowErrorAsync(string message)
    {
        await Shell.Current.DisplayAlert("Error", message, "OK");
    }

    private async Task ShowSuccessAsync(string message)
    {
        await Shell.Current.DisplayAlert("Success", message, "OK");
    }
}
```

---

### **2.5 Payroll Management**

#### **Phase 1: Payroll Dashboard**
```csharp
// ViewModels/Payroll/PayrollViewModel.cs
public class PayrollViewModel : BaseViewModel
{
    private readonly IPayrollService _payrollService;
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger _logger;

    [ObservableProperty]
    private List<PayrollRecord> _payrollRecords;

    [ObservableProperty]
    private PayrollRecord _currentPayroll;

    [ObservableProperty]
    private List<SalaryComponent> _salaryComponents;

    [ObservableProperty]
    private PayrollSummary _payrollSummary;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private DateTime _selectedMonth;

    [ObservableProperty]
    private bool _isRefreshing;

    public PayrollViewModel(IPayrollService payrollService, IAnalyticsService analyticsService,
                           ILogger logger)
    {
        _payrollService = payrollService;
        _analyticsService = analyticsService;
        _logger = logger;
        
        Title = "Payroll";
        SelectedMonth = DateTime.Now;
        
        LoadPayrollData();
    }

    [RelayCommand]
    private async Task RefreshPayrollAsync()
    {
        if (IsRefreshing) return;
        
        try
        {
            IsRefreshing = true;
            await LoadPayrollData();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task ViewPayslipAsync(PayrollRecord payroll)
    {
        try
        {
            await Shell.Current.GoToAsync($"{nameof(PayslipPage)}?payrollId={payroll.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to payslip");
        }
    }

    [RelayCommand]
    private async Task DownloadPayslipAsync(PayrollRecord payroll)
    {
        try
        {
            IsLoading = true;
            
            var result = await _payrollService.DownloadPayslipAsync(payroll.Id);
            
            if (result.Success)
            {
                // Save file to device
                await SaveFileAsync(result.FileName, result.Content);
                
                _analyticsService.TrackEvent("payslip_downloaded", new Dictionary<string, string>
                {
                    ["payroll_id"] = payroll.Id.ToString()
                });
                
                await ShowSuccessAsync("Payslip downloaded successfully");
            }
            else
            {
                await ShowErrorAsync(result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading payslip");
            await ShowErrorAsync("Failed to download payslip. Please try again.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async void LoadPayrollData()
    {
        try
        {
            // Load payroll records for the year
            PayrollRecords = await _payrollService.GetPayrollRecordsAsync(SelectedMonth.Year);
            
            // Get current month payroll
            CurrentPayroll = PayrollRecords?.FirstOrDefault(p => p.Month == SelectedMonth.Month && p.Year == SelectedMonth.Year);
            
            // Load salary components
            if (CurrentPayroll != null)
            {
                SalaryComponents = await _payrollService.GetSalaryComponentsAsync(CurrentPayroll.Id);
            }
            
            // Calculate payroll summary
            PayrollSummary = CalculatePayrollSummary();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading payroll data");
        }
    }

    private PayrollSummary CalculatePayrollSummary()
    {
        if (PayrollRecords == null || !PayrollRecords.Any())
        {
            return new PayrollSummary();
        }

        return new PayrollSummary
        {
            TotalGross = PayrollRecords.Sum(p => p.GrossSalary),
            TotalNet = PayrollRecords.Sum(p => p.NetSalary),
            TotalTax = PayrollRecords.Sum(p => p.TaxAmount),
            TotalDeductions = PayrollRecords.Sum(p => p.TotalDeductions),
            AverageSalary = PayrollRecords.Average(p => p.NetSalary),
            HighestSalary = PayrollRecords.Max(p => p.NetSalary),
            LowestSalary = PayrollRecords.Min(p => p.NetSalary)
        };
    }

    private async Task SaveFileAsync(string fileName, byte[] content)
    {
        // Implementation for saving file to device
        var filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);
        await File.WriteAllBytesAsync(filePath, content);
        
        // Share file if supported
        await Share.RequestAsync(new ShareFileRequest
        {
            Title = "Payslip",
            File = new ShareFile(filePath)
        });
    }

    private async Task ShowErrorAsync(string message)
    {
        await Shell.Current.DisplayAlert("Error", message, "OK");
    }

    private async Task ShowSuccessAsync(string message)
    {
        await Shell.Current.DisplayAlert("Success", message, "OK");
    }
}
```

---

## **3. INFRASTRUCTURE COMPONENTS**

### **3.1 HTTP Client Factory**
```csharp
// Infrastructure/Http/HttpClientFactory.cs
public class HttpClientFactory : IHttpClientFactory
{
    private readonly ITokenManager _tokenManager;
    private readonly ILogger _logger;

    public HttpClientFactory(ITokenManager tokenManager, ILogger logger)
    {
        _tokenManager = tokenManager;
        _logger = logger;
    }

    public HttpClient CreateClient()
    {
        var httpClient = new HttpClient();
        
        // Set base address
        httpClient.BaseAddress = new Uri(AppConstants.ApiBaseUrl);
        
        // Set default headers
        httpClient.DefaultRequestHeaders.Add("User-Agent", "VanAn.HRApp/1.0");
        
        // Add authentication handler
        httpClient = new AuthenticatedHttpClient(httpClient, _tokenManager, _logger);
        
        return httpClient;
    }
}

public class AuthenticatedHttpClient : HttpClient
{
    private readonly ITokenManager _tokenManager;
    private readonly ILogger _logger;

    public AuthenticatedHttpClient(HttpClient innerClient, ITokenManager tokenManager, ILogger logger)
        : base(innerClient)
    {
        _tokenManager = tokenManager;
        _logger = logger;
    }

    public override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Add authorization header
        var token = await _tokenManager.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        // Send request
        var response = await base.SendAsync(request, cancellationToken);

        // Handle unauthorized response
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Try to refresh token
            var refreshed = await _tokenManager.RefreshTokenAsync();
            if (refreshed)
            {
                // Retry request with new token
                token = await _tokenManager.GetAccessTokenAsync();
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                
                response = await base.SendAsync(request, cancellationToken);
            }
        }

        return response;
    }
}
```

### **3.2 Offline Sync Service**
```csharp
// Infrastructure/Storage/OfflineSync.cs
public class OfflineSync : IOfflineSync
{
    private readonly IStorageService _storageService;
    private readonly ILogger _logger;

    public OfflineSync(IStorageService storageService, ILogger logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    public async Task QueueForSyncAsync<T>(T item) where T : class
    {
        try
        {
            var queueKey = $"sync_queue_{typeof(T).Name}";
            var queue = await _storageService.GetAsync<List<T>>(queueKey) ?? new List<T>();
            
            queue.Add(item);
            await _storageService.SetAsync(queueKey, queue);
            
            _logger.LogInformation($"Item queued for sync: {typeof(T).Name}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error queuing item for sync: {typeof(T).Name}");
        }
    }

    public async Task SyncAttendanceAsync(CheckInResult result)
    {
        try
        {
            var attendanceKey = $"attendance_{result.Timestamp:yyyyMMdd}";
            await _storageService.SetAsync(attendanceKey, result);
            
            // Clear sync queue
            await ClearSyncQueueAsync<CheckInRequest>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing attendance");
        }
    }

    public async Task<List<CheckInRequest>> GetPendingSyncRequestsAsync()
    {
        try
        {
            return await _storageService.GetAsync<List<CheckInRequest>>("sync_queue_CheckInRequest") ?? new List<CheckInRequest>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending sync requests");
            return new List<CheckInRequest>();
        }
    }

    public async Task<bool> ProcessPendingSyncAsync()
    {
        try
        {
            var pendingRequests = await GetPendingSyncRequestsAsync();
            
            foreach (var request in pendingRequests)
            {
                // Process sync logic here
                // This would be called when network is available
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pending sync");
            return false;
        }
    }

    private async Task ClearSyncQueueAsync<T>()
    {
        var queueKey = $"sync_queue_{typeof(T).Name}";
        await _storageService.RemoveAsync(queueKey);
    }
}
```

---

## **4. SECURITY FEATURES**

### **4.1 Biometric Authentication**
```csharp
// Infrastructure/Security/BiometricAuthenticator.cs
public class BiometricAuthenticator : IBiometricService
{
    private readonly ILogger _logger;

    public BiometricAuthenticator(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<BiometricResult> AuthenticateAsync()
    {
        try
        {
            var authRequest = new AuthenticationRequest
            {
                Title = "HR App Authentication",
                Subtitle = "Use your fingerprint or face to authenticate",
                NegativeButtonText = "Cancel"
            };

            var result = await Biometric.Default.AuthenticateAsync(authRequest);
            
            return new BiometricResult
            {
                Success = result.Authenticated,
                Message = result.Authenticated ? "Authentication successful" : "Authentication failed",
                BiometricHash = result.Authenticated ? GenerateBiometricHash() : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Biometric authentication error");
            return new BiometricResult
            {
                Success = false,
                Message = "Biometric authentication not available"
            };
        }
    }

    public async Task<BiometricAvailability> CheckAvailabilityAsync()
    {
        try
        {
            var availability = await Biometric.Default.GetAvailabilityAsync();
            
            return new BiometricAvailability
            {
                IsAvailable = availability != BiometricAvailability.None,
                Type = availability switch
                {
                    BiometricAvailability.Fingerprint => BiometricType.Fingerprint,
                    BiometricAvailability.Face => BiometricType.Face,
                    BiometricAvailability.FingerprintAndFace => BiometricType.Both,
                    _ => BiometricType.None
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking biometric availability");
            return new BiometricAvailability
            {
                IsAvailable = false,
                Type = BiometricType.None
            };
        }
    }

    private string GenerateBiometricHash()
    {
        // Generate secure hash for biometric verification
        var data = $"{DateTime.UtcNow}_{DeviceInfo.Model}_{DeviceInfo.Id}";
        return Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(data)));
    }
}
```

---

## **5. TESTING STRATEGY**

### **5.1 Unit Tests**
```csharp
// Tests/Unit/ViewModels/EmployeeViewModelTests.cs
public class EmployeeViewModelTests
{
    private readonly Mock<IEmployeeService> _mockEmployeeService;
    private readonly Mock<IAnalyticsService> _mockAnalyticsService;
    private readonly Mock<ILogger> _mockLogger;
    private readonly EmployeeViewModel _viewModel;

    public EmployeeViewModelTests()
    {
        _mockEmployeeService = new Mock<IEmployeeService>();
        _mockAnalyticsService = new Mock<IAnalyticsService>();
        _mockLogger = new Mock<ILogger>();
        
        _viewModel = new EmployeeViewModel(_mockEmployeeService.Object, 
                                         _mockAnalyticsService.Object, 
                                         _mockLogger.Object);
    }

    [Fact]
    public async Task LoadEmployees_Should_Load_Employees()
    {
        // Arrange
        var employees = new List<Employee>
        {
            new Employee { Id = 1, Name = "John Doe", Email = "john@example.com" },
            new Employee { Id = 2, Name = "Jane Smith", Email = "jane@example.com" }
        };
        
        _mockEmployeeService.Setup(x => x.GetEmployeesAsync())
                           .ReturnsAsync(employees);

        // Act
        await _viewModel.LoadEmployeesCommand.ExecuteAsync(null);

        // Assert
        _viewModel.Employees.Should().HaveCount(2);
        _viewModel.Employees.Should().Contain(e => e.Name == "John Doe");
        _viewModel.IsLoading.Should().BeFalse();
        
        _mockEmployeeService.Verify(x => x.GetEmployeesAsync(), Times.Once);
    }

    [Fact]
    public async Task SearchEmployees_Should_Filter_Employees()
    {
        // Arrange
        var employees = new List<Employee>
        {
            new Employee { Id = 1, Name = "John Doe", Email = "john@example.com" },
            new Employee { Id = 2, Name = "Jane Smith", Email = "jane@example.com" }
        };
        
        _mockEmployeeService.Setup(x => x.GetEmployeesAsync())
                           .ReturnsAsync(employees);

        _viewModel.SearchText = "John";

        // Act
        await _viewModel.SearchEmployeesCommand.ExecuteAsync(null);

        // Assert
        _viewModel.FilteredEmployees.Should().HaveCount(1);
        _viewModel.FilteredEmployees.First().Name.Should().Be("John Doe");
    }
}
```

### **5.2 Integration Tests**
```csharp
// Tests/Integration/ApiIntegrationTests.cs
public class ApiIntegrationTests
{
    private readonly HttpClient _httpClient;
    private readonly IAuthService _authService;

    public ApiIntegrationTests()
    {
        _httpClient = new HttpClient();
        _authService = new AuthService(_httpClient, new Mock<ITokenManager>().Object,
                                      new Mock<IStorageService>().Object, 
                                      new Mock<ILogger>().Object);
    }

    [Fact]
    public async Task Login_Should_Authenticate_User()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Username = "test@example.com",
            Password = "password123"
        };

        // Act
        var result = await _authService.LoginAsync(loginRequest.Username, loginRequest.Password, false);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetEmployees_Should_Return_Employee_List()
    {
        // Arrange
        await AuthenticateAsync();
        var employeeService = new EmployeeService(_httpClient, new Mock<ILogger>().Object);

        // Act
        var employees = await employeeService.GetEmployeesAsync();

        // Assert
        employees.Should().NotBeNull();
        employees.Should().HaveCountGreaterThan(0);
    }

    private async Task AuthenticateAsync()
    {
        // Authenticate for API calls
        await _authService.LoginAsync("test@example.com", "password123", false);
    }
}
```

---

## **6. PERFORMANCE OPTIMIZATION**

### **6.1 Caching Strategy**
```csharp
// Infrastructure/Storage/CacheManager.cs
public class CacheManager : ICacheManager
{
    private readonly IMemoryCache _memoryCache;
    private readonly IStorageService _storageService;
    private readonly ILogger _logger;

    public CacheManager(IMemoryCache memoryCache, IStorageService storageService, ILogger logger)
    {
        _memoryCache = memoryCache;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<T> GetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null)
    {
        // Try memory cache first
        if (_memoryCache.TryGetValue(key, out T value))
        {
            return value;
        }

        // Try persistent cache
        var cachedValue = await _storageService.GetAsync<T>(key);
        if (cachedValue != null)
        {
            _memoryCache.Set(key, cachedValue, expiry ?? TimeSpan.FromMinutes(30));
            return cachedValue;
        }

        // Load from factory
        var result = await factory();
        
        // Store in both caches
        _memoryCache.Set(key, result, expiry ?? TimeSpan.FromMinutes(30));
        await _storageService.SetAsync(key, result);
        
        return result;
    }

    public async Task RemoveAsync(string key)
    {
        _memoryCache.Remove(key);
        await _storageService.RemoveAsync(key);
    }

    public async Task ClearAsync()
    {
        _memoryCache.Dispose();
        await _storageService.ClearAsync();
    }
}
```

### **6.2 Lazy Loading**
```csharp
// Core/ViewModels/BaseViewModel.cs
public abstract class BaseViewModel : ObservableObject
{
    protected readonly ICacheManager _cacheManager;
    protected readonly ILogger _logger;

    public BaseViewModel(ICacheManager cacheManager, ILogger logger)
    {
        _cacheManager = cacheManager;
        _logger = logger;
    }

    protected async Task<T> LazyLoadAsync<T>(string cacheKey, Func<Task<T>> loader, TimeSpan? expiry = null)
    {
        return await _cacheManager.GetAsync(cacheKey, loader, expiry);
    }

    protected async Task InvalidateCacheAsync(string cacheKey)
    {
        await _cacheManager.RemoveAsync(cacheKey);
    }
}
```

---

## **7. SUMMARY**

### **7.1 Key Features of Ideal HR App**
- **MVVM Architecture:** Proper separation of concerns
- **Authentication:** Biometric and password authentication
- **Attendance Management:** Check-in/out with geofencing
- **Leave Management:** Request and track leave
- **Payroll Management:** View payslips and salary details
- **Performance Management:** Reviews and goals
- **Offline Support:** Sync when online
- **Security:** Token-based authentication, biometric verification
- **Localization:** Multi-language support
- **Analytics:** Usage tracking and reporting

### **7.2 Technical Excellence**
- **Clean Architecture:** Proper layering and separation
- **Dependency Injection:** Flexible service configuration
- **Async/Await:** Non-blocking operations
- **Error Handling:** Robust exception management
- **Logging:** Comprehensive logging
- **Caching:** Performance optimization
- **Testing:** Unit and integration tests
- **Security:** Secure data storage and transmission

### **7.3 Business Value**
- **Employee Engagement:** Self-service HR features
- **Productivity:** Quick access to HR information
- **Compliance:** Proper attendance and leave tracking
- **Security:** Biometric authentication and data protection
- **Mobility:** Access HR services anywhere
- **Efficiency:** Automated processes and notifications

This ideal HR App provides a comprehensive, professional-grade mobile application that meets all HR management needs while maintaining excellent user experience, security, and performance.
