# HrApp - Comparison and Improvement Plan

**Date:** April 11, 2026  
**Module:** 4_MobileApps/HRApp  
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

### **1.2 HR Features Comparison**

| **Aspect** | **Realistic** | **Ideal** | **Gap Analysis** |
|------------|--------------|-----------|------------------|
| **Employee Management** | No HR features | Complete employee management | **High** - Need HR features |
| **Attendance Tracking** | No attendance system | Biometric attendance tracking | **High** - Need attendance system |
| **Leave Management** | No leave management | Comprehensive leave system | **High** - Need leave management |
| **Payroll Processing** | No payroll features | Full payroll processing | **High** - Need payroll system |
| **Performance Management** | No performance tracking | Performance evaluation system | **High** - Need performance system |

### **1.3 Technical Implementation Comparison**

| **Aspect** | **Realistic** | **Ideal** | **Gap Analysis** |
|------------|--------------|-----------|------------------|
| **API Integration** | No API integration | RESTful API integration | **High** - Need API integration |
| **Authentication** | No authentication | Biometric authentication | **High** - Need auth system |
| **Offline Support** | No offline support | Offline-first architecture | **Medium** - Need offline support |
| **Data Synchronization** | No sync mechanism | Real-time synchronization | **High** - Need sync system |
| **Security** | Basic security | Enterprise-grade security | **High** - Need security enhancement |

### **1.4 User Experience Comparison**

| **Aspect** | **Realistic** | **Ideal** | **Gap Analysis** |
|------------|--------------|-----------|------------------|
| **User Interface** | Template UI | Professional HR interface | **High** - Need UI redesign |
| **User Experience** | Basic UX | Intuitive HR workflow | **High** - Need UX enhancement |
| **Accessibility** | No accessibility | Full accessibility support | **Medium** - Need accessibility |
| **Localization** | No localization | Multi-language support | **Medium** - Need localization |
| **Performance** | Basic performance | Optimized performance | **Medium** - Need optimization |

---

## **2. PRIORITY ASSESSMENT**

### **2.1 Critical Issues (Priority 1)**
1. **No HR Features** - Complete lack of HR functionality
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
1. **No Advanced Features** - No advanced HR features
2. **No Analytics** - No HR analytics or reporting
3. **No Integration** - No third-party integrations
4. **No Customization** - No customizable features
5. **No Automation** - No workflow automation

---

## **3. IMPROVEMENT PLAN**

### **3.1 Phase 1: Foundation Architecture (Week 1-2)**

#### **Day 1-3: MVVM Architecture Implementation**
```csharp
// HRApp/ViewModels/BaseViewModel.cs
public abstract class BaseViewModel : ObservableObject
{
    protected readonly INavigationService _navigationService;
    protected readonly IDialogService _dialogService;
    protected readonly IApiService _apiService;
    protected readonly IAuthenticationService _authService;

    private bool _isBusy;
    private string _title;

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

    protected BaseViewModel(
        INavigationService navigationService,
        IDialogService dialogService,
        IApiService apiService,
        IAuthenticationService authService)
    {
        _navigationService = navigationService;
        _dialogService = dialogService;
        _apiService = apiService;
        _authService = authService;
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
}

// HRApp/ViewModels/DashboardViewModel.cs
public class DashboardViewModel : BaseViewModel
{
    private readonly IEmployeeService _employeeService;
    private readonly IAttendanceService _attendanceService;
    private readonly ILeaveService _leaveService;
    private readonly IPayrollService _payrollService;

    private ObservableCollection<DashboardItem> _dashboardItems;
    private EmployeeInfo _currentEmployee;
    private AttendanceSummary _attendanceSummary;
    private LeaveSummary _leaveSummary;
    private PayrollSummary _payrollSummary;

    public ObservableCollection<DashboardItem> DashboardItems
    {
        get => _dashboardItems;
        set => SetProperty(ref _dashboardItems, value);
    }

    public EmployeeInfo CurrentEmployee
    {
        get => _currentEmployee;
        set => SetProperty(ref _currentEmployee, value);
    }

    public AttendanceSummary AttendanceSummary
    {
        get => _attendanceSummary;
        set => SetProperty(ref _attendanceSummary, value);
    }

    public LeaveSummary LeaveSummary
    {
        get => _leaveSummary;
        set => SetProperty(ref _leaveSummary, value);
    }

    public PayrollSummary PayrollSummary
    {
        get => _payrollSummary;
        set => SetProperty(ref _payrollSummary, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand ViewProfileCommand { get; }
    public ICommand ClockInCommand { get; }
    public ICommand ClockOutCommand { get; }
    public ICommand RequestLeaveCommand { get; }
    public ICommand ViewPayslipCommand { get; }

    public DashboardViewModel(
        INavigationService navigationService,
        IDialogService dialogService,
        IApiService apiService,
        IAuthenticationService authService,
        IEmployeeService employeeService,
        IAttendanceService attendanceService,
        ILeaveService leaveService,
        IPayrollService payrollService)
        : base(navigationService, dialogService, apiService, authService)
    {
        _employeeService = employeeService;
        _attendanceService = attendanceService;
        _leaveService = leaveService;
        _payrollService = payrollService;

        RefreshCommand = new RelayCommand(async () => await LoadDashboardAsync());
        ViewProfileCommand = new RelayCommand(async () => await ViewProfileAsync());
        ClockInCommand = new RelayCommand(async () => await ClockInAsync(), () => CanClockIn());
        ClockOutCommand = new RelayCommand(async () => await ClockOutAsync(), () => CanClockOut());
        RequestLeaveCommand = new RelayCommand(async () => await RequestLeaveAsync());
        ViewPayslipCommand = new RelayCommand(async () => await ViewPayslipAsync());

        Title = "HR Dashboard";
    }

    public async Task InitializeAsync()
    {
        await LoadDashboardAsync();
    }

    private async Task LoadDashboardAsync()
    {
        await ExecuteAsync(async () =>
        {
            // Load current employee info
            var employeeId = await _authService.GetCurrentEmployeeIdAsync();
            CurrentEmployee = await _employeeService.GetEmployeeInfoAsync(employeeId);

            // Load dashboard data
            await Task.WhenAll(
                LoadAttendanceSummaryAsync(employeeId),
                LoadLeaveSummaryAsync(employeeId),
                LoadPayrollSummaryAsync(employeeId),
                LoadDashboardItemsAsync(employeeId)
            );
        }, "Loading dashboard...");
    }

    private async Task LoadAttendanceSummaryAsync(Guid employeeId)
    {
        var summary = await _attendanceService.GetAttendanceSummaryAsync(employeeId);
        AttendanceSummary = summary;
    }

    private async Task LoadLeaveSummaryAsync(Guid employeeId)
    {
        var summary = await _leaveService.GetLeaveSummaryAsync(employeeId);
        LeaveSummary = summary;
    }

    private async Task LoadPayrollSummaryAsync(Guid employeeId)
    {
        var summary = await _payrollService.GetPayrollSummaryAsync(employeeId);
        PayrollSummary = summary;
    }

    private async Task LoadDashboardItemsAsync(Guid employeeId)
    {
        var items = new List<DashboardItem>
        {
            new DashboardItem
            {
                Title = "Clock In/Out",
                Description = "Track your attendance",
                Icon = "clock",
                Color = "#4CAF50",
                Command = ClockInCommand
            },
            new DashboardItem
            {
                Title = "Leave Request",
                Description = "Request time off",
                Icon = "calendar",
                Color = "#2196F3",
                Command = RequestLeaveCommand
            },
            new DashboardItem
            {
                Title = "View Payslip",
                Description = "Check your salary",
                Icon = "money",
                Color = "#FF9800",
                Command = ViewPayslipCommand
            },
            new DashboardItem
            {
                Title = "My Profile",
                Description = "Update your information",
                Icon = "person",
                Color = "#9C27B0",
                Command = ViewProfileCommand
            }
        };

        DashboardItems = new ObservableCollection<DashboardItem>(items);
    }

    private async Task ViewProfileAsync()
    {
        await _navigationService.NavigateToAsync<ProfileViewModel>();
    }

    private async Task ClockInAsync()
    {
        await ExecuteAsync(async () =>
        {
            var employeeId = await _authService.GetCurrentEmployeeIdAsync();
            await _attendanceService.ClockInAsync(employeeId);
            
            await ShowSuccessAsync("Clocked in successfully!");
            await LoadAttendanceSummaryAsync(employeeId);
        }, "Clocking in...");
    }

    private async Task ClockOutAsync()
    {
        await ExecuteAsync(async () =>
        {
            var employeeId = await _authService.GetCurrentEmployeeIdAsync();
            await _attendanceService.ClockOutAsync(employeeId);
            
            await ShowSuccessAsync("Clocked out successfully!");
            await LoadAttendanceSummaryAsync(employeeId);
        }, "Clocking out...");
    }

    private async Task RequestLeaveAsync()
    {
        await _navigationService.NavigateToAsync<LeaveRequestViewModel>();
    }

    private async Task ViewPayslipAsync()
    {
        await _navigationService.NavigateToAsync<PayslipViewModel>();
    }

    private bool CanClockIn()
    {
        return AttendanceSummary?.CanClockIn == true;
    }

    private bool CanClockOut()
    {
        return AttendanceSummary?.CanClockOut == true;
    }
}

// HRApp/ViewModels/AttendanceViewModel.cs
public class AttendanceViewModel : BaseViewModel
{
    private readonly IAttendanceService _attendanceService;
    private readonly ILocationService _locationService;

    private ObservableCollection<AttendanceRecord> _attendanceRecords;
    private DateTime _selectedDate;
    private AttendanceRecord _todayAttendance;
    private bool _canClockIn;
    private bool _canClockOut;

    public ObservableCollection<AttendanceRecord> AttendanceRecords
    {
        get => _attendanceRecords;
        set => SetProperty(ref _attendanceRecords, value);
    }

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (SetProperty(ref _selectedDate, value))
            {
                _ = LoadAttendanceRecordsAsync();
            }
        }
    }

    public AttendanceRecord TodayAttendance
    {
        get => _todayAttendance;
        set => SetProperty(ref _todayAttendance, value);
    }

    public bool CanClockIn
    {
        get => _canClockIn;
        set => SetProperty(ref _canClockIn, value);
    }

    public bool CanClockOut
    {
        get => _canClockOut;
        set => SetProperty(ref _canClockOut, value);
    }

    public ICommand ClockInCommand { get; }
    public ICommand ClockOutCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ViewDetailsCommand { get; }

    public AttendanceViewModel(
        INavigationService navigationService,
        IDialogService dialogService,
        IApiService apiService,
        IAuthenticationService authService,
        IAttendanceService attendanceService,
        ILocationService locationService)
        : base(navigationService, dialogService, apiService, authService)
    {
        _attendanceService = attendanceService;
        _locationService = locationService;

        ClockInCommand = new RelayCommand(async () => await ClockInAsync(), () => CanClockIn);
        ClockOutCommand = new RelayCommand(async () => await ClockOutAsync(), () => CanClockOut);
        RefreshCommand = new RelayCommand(async () => await LoadAttendanceRecordsAsync());
        ViewDetailsCommand = new RelayCommand<AttendanceRecord>(async (record) => await ViewDetailsAsync(record));

        Title = "Attendance";
        SelectedDate = DateTime.Today;
    }

    public async Task InitializeAsync()
    {
        await LoadAttendanceRecordsAsync();
        await LoadTodayAttendanceAsync();
    }

    private async Task LoadAttendanceRecordsAsync()
    {
        await ExecuteAsync(async () =>
        {
            var employeeId = await _authService.GetCurrentEmployeeIdAsync();
            var records = await _attendanceService.GetAttendanceRecordsAsync(employeeId, SelectedDate);
            
            AttendanceRecords = new ObservableCollection<AttendanceRecord>(records);
        }, "Loading attendance records...");
    }

    private async Task LoadTodayAttendanceAsync()
    {
        await ExecuteAsync(async () =>
        {
            var employeeId = await _authService.GetCurrentEmployeeIdAsync();
            var todayAttendance = await _attendanceService.GetTodayAttendanceAsync(employeeId);
            
            TodayAttendance = todayAttendance;
            UpdateClockingStatus(todayAttendance);
        }, "Loading today's attendance...");
    }

    private async Task ClockInAsync()
    {
        await ExecuteAsync(async () =>
        {
            // Get current location
            var location = await _locationService.GetCurrentLocationAsync();
            
            // Verify location is within allowed area
            var isLocationValid = await _attendanceService.VerifyLocationAsync(location);
            if (!isLocationValid)
            {
                await _dialogService.ShowErrorAsync("You are not within the allowed clock-in area.");
                return;
            }

            // Clock in
            var employeeId = await _authService.GetCurrentEmployeeIdAsync();
            var attendanceRecord = await _attendanceService.ClockInAsync(employeeId, location);
            
            TodayAttendance = attendanceRecord;
            UpdateClockingStatus(attendanceRecord);
            
            await ShowSuccessAsync("Clocked in successfully!");
            await LoadAttendanceRecordsAsync();
        }, "Clocking in...");
    }

    private async Task ClockOutAsync()
    {
        await ExecuteAsync(async () =>
        {
            // Get current location
            var location = await _locationService.GetCurrentLocationAsync();
            
            // Verify location is within allowed area
            var isLocationValid = await _attendanceService.VerifyLocationAsync(location);
            if (!isLocationValid)
            {
                await _dialogService.ShowErrorAsync("You are not within the allowed clock-out area.");
                return;
            }

            // Clock out
            var employeeId = await _authService.GetCurrentEmployeeIdAsync();
            var attendanceRecord = await _attendanceService.ClockOutAsync(employeeId, location);
            
            TodayAttendance = attendanceRecord;
            UpdateClockingStatus(attendanceRecord);
            
            await ShowSuccessAsync("Clocked out successfully!");
            await LoadAttendanceRecordsAsync();
        }, "Clocking out...");
    }

    private async Task ViewDetailsAsync(AttendanceRecord record)
    {
        var parameters = new Dictionary<string, object>
        {
            { "attendanceRecord", record }
        };
        
        await _navigationService.NavigateToAsync<AttendanceDetailsViewModel>(parameters);
    }

    private void UpdateClockingStatus(AttendanceRecord todayAttendance)
    {
        if (todayAttendance == null)
        {
            CanClockIn = true;
            CanClockOut = false;
        }
        else if (todayAttendance.ClockInTime != null && todayAttendance.ClockOutTime == null)
        {
            CanClockIn = false;
            CanClockOut = true;
        }
        else
        {
            CanClockIn = false;
            CanClockOut = false;
        }
    }
}
```

#### **Day 4-5: Service Layer Implementation**
```csharp
// HRApp/Services/EmployeeService.cs
public class EmployeeService : IEmployeeService
{
    private readonly IApiService _apiService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<EmployeeService> _logger;

    public EmployeeService(
        IApiService apiService,
        ICacheService cacheService,
        ILogger<EmployeeService> logger)
    {
        _apiService = apiService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<EmployeeInfo> GetEmployeeInfoAsync(Guid employeeId)
    {
        try
        {
            // Try to get from cache first
            var cacheKey = $"employee_{employeeId}";
            var cachedEmployee = await _cacheService.GetAsync<EmployeeInfo>(cacheKey);
            
            if (cachedEmployee != null)
            {
                return cachedEmployee;
            }

            // Get from API
            var employee = await _apiService.GetAsync<EmployeeInfo>($"/api/employees/{employeeId}");
            
            // Cache for 1 hour
            await _cacheService.SetAsync(cacheKey, employee, TimeSpan.FromHours(1));
            
            return employee;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting employee info for {EmployeeId}", employeeId);
            throw;
        }
    }

    public async Task<List<EmployeeInfo>> GetEmployeesAsync()
    {
        try
        {
            var employees = await _apiService.GetAsync<List<EmployeeInfo>>("/api/employees");
            return employees;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting employees list");
            throw;
        }
    }

    public async Task<EmployeeInfo> UpdateEmployeeInfoAsync(EmployeeInfo employeeInfo)
    {
        try
        {
            var updatedEmployee = await _apiService.PutAsync<EmployeeInfo>($"/api/employees/{employeeInfo.Id}", employeeInfo);
            
            // Update cache
            var cacheKey = $"employee_{employeeInfo.Id}";
            await _cacheService.SetAsync(cacheKey, updatedEmployee, TimeSpan.FromHours(1));
            
            return updatedEmployee;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating employee info for {EmployeeId}", employeeInfo.Id);
            throw;
        }
    }

    public async Task<byte[]> GetEmployeePhotoAsync(Guid employeeId)
    {
        try
        {
            var cacheKey = $"employee_photo_{employeeId}";
            var cachedPhoto = await _cacheService.GetAsync<byte[]>(cacheKey);
            
            if (cachedPhoto != null)
            {
                return cachedPhoto;
            }

            var photo = await _apiService.GetAsync<byte[]>($"/api/employees/{employeeId}/photo");
            
            // Cache for 24 hours
            await _cacheService.SetAsync(cacheKey, photo, TimeSpan.FromHours(24));
            
            return photo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting employee photo for {EmployeeId}", employeeId);
            throw;
        }
    }

    public async Task<bool> UpdateEmployeePhotoAsync(Guid employeeId, byte[] photoData)
    {
        try
        {
            var success = await _apiService.PostAsync<bool>($"/api/employees/{employeeId}/photo", photoData);
            
            if (success)
            {
                // Update cache
                var cacheKey = $"employee_photo_{employeeId}";
                await _cacheService.SetAsync(cacheKey, photoData, TimeSpan.FromHours(24));
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating employee photo for {EmployeeId}", employeeId);
            throw;
        }
    }
}

// HRApp/Services/AttendanceService.cs
public class AttendanceService : IAttendanceService
{
    private readonly IApiService _apiService;
    private readonly ILocationService _locationService;
    private readonly IBiometricService _biometricService;
    private readonly ILogger<AttendanceService> _logger;

    public AttendanceService(
        IApiService apiService,
        ILocationService locationService,
        IBiometricService biometricService,
        ILogger<AttendanceService> logger)
    {
        _apiService = apiService;
        _locationService = locationService;
        _biometricService = biometricService;
        _logger = logger;
    }

    public async Task<AttendanceRecord> ClockInAsync(Guid employeeId, Location location = null)
    {
        try
        {
            // Get current location if not provided
            location ??= await _locationService.GetCurrentLocationAsync();
            
            // Verify location
            var isLocationValid = await VerifyLocationAsync(location);
            if (!isLocationValid)
            {
                throw new InvalidOperationException("Invalid clock-in location");
            }

            // Get biometric verification
            var biometricData = await _biometricService.CaptureBiometricAsync();
            var isBiometricValid = await _biometricService.VerifyBiometricAsync(employeeId, biometricData);
            
            if (!isBiometricValid)
            {
                throw new InvalidOperationException("Biometric verification failed");
            }

            // Create clock-in request
            var request = new ClockInRequest
            {
                EmployeeId = employeeId,
                Location = location,
                BiometricData = biometricData,
                Timestamp = DateTime.UtcNow
            };

            // Send to API
            var attendanceRecord = await _apiService.PostAsync<AttendanceRecord>("/api/attendance/clockin", request);
            
            _logger.LogInformation("Employee {EmployeeId} clocked in at {Timestamp}", employeeId, request.Timestamp);
            
            return attendanceRecord;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clocking in employee {EmployeeId}", employeeId);
            throw;
        }
    }

    public async Task<AttendanceRecord> ClockOutAsync(Guid employeeId, Location location = null)
    {
        try
        {
            // Get current location if not provided
            location ??= await _locationService.GetCurrentLocationAsync();
            
            // Verify location
            var isLocationValid = await VerifyLocationAsync(location);
            if (!isLocationValid)
            {
                throw new InvalidOperationException("Invalid clock-out location");
            }

            // Get biometric verification
            var biometricData = await _biometricService.CaptureBiometricAsync();
            var isBiometricValid = await _biometricService.VerifyBiometricAsync(employeeId, biometricData);
            
            if (!isBiometricValid)
            {
                throw new InvalidOperationException("Biometric verification failed");
            }

            // Create clock-out request
            var request = new ClockOutRequest
            {
                EmployeeId = employeeId,
                Location = location,
                BiometricData = biometricData,
                Timestamp = DateTime.UtcNow
            };

            // Send to API
            var attendanceRecord = await _apiService.PostAsync<AttendanceRecord>("/api/attendance/clockout", request);
            
            _logger.LogInformation("Employee {EmployeeId} clocked out at {Timestamp}", employeeId, request.Timestamp);
            
            return attendanceRecord;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clocking out employee {EmployeeId}", employeeId);
            throw;
        }
    }

    public async Task<List<AttendanceRecord>> GetAttendanceRecordsAsync(Guid employeeId, DateTime date)
    {
        try
        {
            var startDate = date.Date;
            var endDate = date.Date.AddDays(1).AddTicks(-1);
            
            var records = await _apiService.GetAsync<List<AttendanceRecord>>(
                $"/api/attendance/employee/{employeeId}?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");
            
            return records;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting attendance records for employee {EmployeeId} on {Date}", employeeId, date);
            throw;
        }
    }

    public async Task<AttendanceRecord> GetTodayAttendanceAsync(Guid employeeId)
    {
        try
        {
            var today = DateTime.Today;
            var records = await GetAttendanceRecordsAsync(employeeId, today);
            
            return records.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting today's attendance for employee {EmployeeId}", employeeId);
            throw;
        }
    }

    public async Task<AttendanceSummary> GetAttendanceSummaryAsync(Guid employeeId)
    {
        try
        {
            var summary = await _apiService.GetAsync<AttendanceSummary>($"/api/attendance/summary/{employeeId}");
            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting attendance summary for employee {EmployeeId}", employeeId);
            throw;
        }
    }

    public async Task<bool> VerifyLocationAsync(Location location)
    {
        try
        {
            var isValid = await _apiService.PostAsync<bool>("/api/attendance/verify-location", location);
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying location {Latitude}, {Longitude}", location.Latitude, location.Longitude);
            return false;
        }
    }

    public async Task<List<AttendanceReport>> GetAttendanceReportsAsync(Guid employeeId, DateTime startDate, DateTime endDate)
    {
        try
        {
            var reports = await _apiService.GetAsync<List<AttendanceReport>>(
                $"/api/attendance/reports/{employeeId}?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");
            
            return reports;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting attendance reports for employee {EmployeeId}", employeeId);
            throw;
        }
    }
}

// HRApp/Services/LeaveService.cs
public class LeaveService : ILeaveService
{
    private readonly IApiService _apiService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<LeaveService> _logger;

    public LeaveService(
        IApiService apiService,
        ICacheService cacheService,
        ILogger<LeaveService> logger)
    {
        _apiService = apiService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<LeaveRequest> CreateLeaveRequestAsync(LeaveRequest request)
    {
        try
        {
            var createdRequest = await _apiService.PostAsync<LeaveRequest>("/api/leave/requests", request);
            
            _logger.LogInformation("Leave request {RequestId} created for employee {EmployeeId}", createdRequest.Id, request.EmployeeId);
            
            return createdRequest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating leave request for employee {EmployeeId}", request.EmployeeId);
            throw;
        }
    }

    public async Task<List<LeaveRequest>> GetLeaveRequestsAsync(Guid employeeId)
    {
        try
        {
            var requests = await _apiService.GetAsync<List<LeaveRequest>>($"/api/leave/requests/{employeeId}");
            return requests;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting leave requests for employee {EmployeeId}", employeeId);
            throw;
        }
    }

    public async Task<LeaveSummary> GetLeaveSummaryAsync(Guid employeeId)
    {
        try
        {
            var cacheKey = $"leave_summary_{employeeId}";
            var cachedSummary = await _cacheService.GetAsync<LeaveSummary>(cacheKey);
            
            if (cachedSummary != null)
            {
                return cachedSummary;
            }

            var summary = await _apiService.GetAsync<LeaveSummary>($"/api/leave/summary/{employeeId}");
            
            // Cache for 30 minutes
            await _cacheService.SetAsync(cacheKey, summary, TimeSpan.FromMinutes(30));
            
            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting leave summary for employee {EmployeeId}", employeeId);
            throw;
        }
    }

    public async Task<LeaveBalance> GetLeaveBalanceAsync(Guid employeeId)
    {
        try
        {
            var balance = await _apiService.GetAsync<LeaveBalance>($"/api/leave/balance/{employeeId}");
            return balance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting leave balance for employee {EmployeeId}", employeeId);
            throw;
        }
    }

    public async Task<bool> CancelLeaveRequestAsync(Guid requestId)
    {
        try
        {
            var success = await _apiService.DeleteAsync<bool>($"/api/leave/requests/{requestId}");
            
            if (success)
            {
                _logger.LogInformation("Leave request {RequestId} cancelled", requestId);
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling leave request {RequestId}", requestId);
            throw;
        }
    }

    public async Task<List<LeaveType>> GetLeaveTypesAsync()
    {
        try
        {
            var cacheKey = "leave_types";
            var cachedTypes = await _cacheService.GetAsync<List<LeaveType>>(cacheKey);
            
            if (cachedTypes != null)
            {
                return cachedTypes;
            }

            var types = await _apiService.GetAsync<List<LeaveType>>("/api/leave/types");
            
            // Cache for 24 hours
            await _cacheService.SetAsync(cacheKey, types, TimeSpan.FromHours(24));
            
            return types;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting leave types");
            throw;
        }
    }

    public async Task<List<Holiday>> GetHolidaysAsync(int year)
    {
        try
        {
            var cacheKey = $"holidays_{year}";
            var cachedHolidays = await _cacheService.GetAsync<List<Holiday>>(cacheKey);
            
            if (cachedHolidays != null)
            {
                return cachedHolidays;
            }

            var holidays = await _apiService.GetAsync<List<Holiday>>($"/api/leave/holidays/{year}");
            
            // Cache for 7 days
            await _cacheService.SetAsync(cacheKey, holidays, TimeSpan.FromDays(7));
            
            return holidays;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting holidays for year {Year}", year);
            throw;
        }
    }
}
```

### **3.2 Phase 2: HR Features Implementation (Week 3-4)**

#### **Day 8-10: Leave Management System**
```csharp
// HRApp/ViewModels/LeaveRequestViewModel.cs
public class LeaveRequestViewModel : BaseViewModel
{
    private readonly ILeaveService _leaveService;
    private readonly IEmployeeService _employeeService;

    private ObservableCollection<LeaveType> _leaveTypes;
    private ObservableCollection<Holiday> _holidays;
    private LeaveRequest _leaveRequest;
    private LeaveBalance _leaveBalance;
    private DateTime _startDate;
    private DateTime _endDate;
    private LeaveType _selectedLeaveType;
    private string _reason;
    private string _attachmentPath;

    public ObservableCollection<LeaveType> LeaveTypes
    {
        get => _leaveTypes;
        set => SetProperty(ref _leaveTypes, value);
    }

    public ObservableCollection<Holiday> Holidays
    {
        get => _holidays;
        set => SetProperty(ref _holidays, value);
    }

    public LeaveRequest LeaveRequest
    {
        get => _leaveRequest;
        set => SetProperty(ref _leaveRequest, value);
    }

    public LeaveBalance LeaveBalance
    {
        get => _leaveBalance;
        set => SetProperty(ref _leaveBalance, value);
    }

    public DateTime StartDate
    {
        get => _startDate;
        set
        {
            if (SetProperty(ref _startDate, value))
            {
                _ = ValidateDatesAsync();
            }
        }
    }

    public DateTime EndDate
    {
        get => _endDate;
        set
        {
            if (SetProperty(ref _endDate, value))
            {
                _ = ValidateDatesAsync();
            }
        }
    }

    public LeaveType SelectedLeaveType
    {
        get => _selectedLeaveType;
        set
        {
            if (SetProperty(ref _selectedLeaveType, value))
            {
                _ = UpdateAvailableDaysAsync();
            }
        }
    }

    public string Reason
    {
        get => _reason;
        set => SetProperty(ref _reason, value);
    }

    public string AttachmentPath
    {
        get => _attachmentPath;
        set => SetProperty(ref _attachmentPath, value);
    }

    public ICommand SubmitCommand { get; }
    public ICommand AttachFileCommand { get; }
    public ICommand ViewCalendarCommand { get; }
    public ICommand ViewBalanceCommand { get; }

    public LeaveRequestViewModel(
        INavigationService navigationService,
        IDialogService dialogService,
        IApiService apiService,
        IAuthenticationService authService,
        ILeaveService leaveService,
        IEmployeeService employeeService)
        : base(navigationService, dialogService, apiService, authService)
    {
        _leaveService = leaveService;
        _employeeService = employeeService;

        SubmitCommand = new RelayCommand(async () => await SubmitLeaveRequestAsync(), () => CanSubmit());
        AttachFileCommand = new RelayCommand(async () => await AttachFileAsync());
        ViewCalendarCommand = new RelayCommand(async () => await ViewCalendarAsync());
        ViewBalanceCommand = new RelayCommand(async () => await ViewBalanceAsync());

        Title = "Leave Request";
        InitializeDates();
    }

    public async Task InitializeAsync()
    {
        await ExecuteAsync(async () =>
        {
            await Task.WhenAll(
                LoadLeaveTypesAsync(),
                LoadHolidaysAsync(),
                LoadLeaveBalanceAsync()
            );
        }, "Loading leave information...");
    }

    private void InitializeDates()
    {
        var today = DateTime.Today;
        StartDate = today;
        EndDate = today;
    }

    private async Task LoadLeaveTypesAsync()
    {
        var leaveTypes = await _leaveService.GetLeaveTypesAsync();
        LeaveTypes = new ObservableCollection<LeaveType>(leaveTypes);
        
        if (LeaveTypes.Any())
        {
            SelectedLeaveType = LeaveTypes.First();
        }
    }

    private async Task LoadHolidaysAsync()
    {
        var holidays = await _leaveService.GetHolidaysAsync(DateTime.Today.Year);
        Holidays = new ObservableCollection<Holiday>(holidays);
    }

    private async Task LoadLeaveBalanceAsync()
    {
        var employeeId = await _authService.GetCurrentEmployeeIdAsync();
        var balance = await _leaveService.GetLeaveBalanceAsync(employeeId);
        LeaveBalance = balance;
    }

    private async Task ValidateDatesAsync()
    {
        if (StartDate > EndDate)
        {
            await _dialogService.ShowErrorAsync("Start date cannot be after end date.");
            EndDate = StartDate;
            return;
        }

        // Check for weekends
        var weekends = GetWeekendDays(StartDate, EndDate);
        if (weekends.Any())
        {
            var confirmed = await _dialogService.ShowConfirmationAsync(
                $"The selected period includes {weekends.Count} weekend day(s). Do you want to continue?",
                "Weekend Days Detected");
            
            if (!confirmed)
            {
                return;
            }
        }

        // Check for holidays
        var holidayDays = GetHolidayDays(StartDate, EndDate);
        if (holidayDays.Any())
        {
            var confirmed = await _dialogService.ShowConfirmationAsync(
                $"The selected period includes {holidayDays.Count} holiday(s). Do you want to continue?",
                "Holidays Detected");
            
            if (!confirmed)
            {
                return;
            }
        }

        await UpdateAvailableDaysAsync();
    }

    private async Task UpdateAvailableDaysAsync()
    {
        if (SelectedLeaveType == null)
            return;

        var employeeId = await _authService.GetCurrentEmployeeIdAsync();
        var balance = await _leaveService.GetLeaveBalanceAsync(employeeId);
        
        var requestedDays = CalculateWorkingDays(StartDate, EndDate);
        var availableDays = balance.GetAvailableDays(SelectedLeaveType.Type);
        
        if (requestedDays > availableDays)
        {
            await _dialogService.ShowErrorAsync(
                $"Insufficient leave balance. Requested: {requestedDays} days, Available: {availableDays} days.");
        }
    }

    private async Task SubmitLeaveRequestAsync()
    {
        await ExecuteAsync(async () =>
        {
            var employeeId = await _authService.GetCurrentEmployeeIdAsync();
            
            var leaveRequest = new LeaveRequest
            {
                EmployeeId = employeeId,
                LeaveType = SelectedLeaveType.Type,
                StartDate = StartDate,
                EndDate = EndDate,
                Reason = Reason,
                Status = LeaveStatus.Pending,
                RequestedAt = DateTime.UtcNow,
                WorkingDays = CalculateWorkingDays(StartDate, EndDate)
            };

            // Handle attachment if provided
            if (!string.IsNullOrEmpty(AttachmentPath))
            {
                var attachmentData = await File.ReadAllBytesAsync(AttachmentPath);
                leaveRequest.Attachment = attachmentData;
                leaveRequest.AttachmentName = Path.GetFileName(AttachmentPath);
            }

            var createdRequest = await _leaveService.CreateLeaveRequestAsync(leaveRequest);
            
            await ShowSuccessAsync("Leave request submitted successfully!");
            
            // Navigate back to dashboard
            await _navigationService.GoBackAsync();
        }, "Submitting leave request...");
    }

    private async Task AttachFileAsync()
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, new[] { "public.image", "public.pdf" } },
                    { DevicePlatform.Android, new[] { "image/*", "application/pdf" } },
                    { DevicePlatform.WinUI, new[] { ".jpg", ".jpeg", ".png", ".pdf" } }
                })
            });

            if (result != null)
            {
                AttachmentPath = result.FullPath;
                await ShowSuccessAsync($"File attached: {result.FileName}");
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task ViewCalendarAsync()
    {
        var parameters = new Dictionary<string, object>
        {
            { "startDate", StartDate },
            { "endDate", EndDate },
            { "holidays", Holidays }
        };

        await _navigationService.NavigateToAsync<CalendarViewModel>(parameters);
    }

    private async Task ViewBalanceAsync()
    {
        await _navigationService.NavigateToAsync<LeaveBalanceViewModel>();
    }

    private bool CanSubmit()
    {
        return SelectedLeaveType != null &&
               !string.IsNullOrEmpty(Reason) &&
               StartDate <= EndDate &&
               IsBusy == false;
    }

    private int CalculateWorkingDays(DateTime startDate, DateTime endDate)
    {
        var workingDays = 0;
        var currentDate = startDate;

        while (currentDate <= endDate)
        {
            if (currentDate.DayOfWeek != DayOfWeek.Saturday && currentDate.DayOfWeek != DayOfWeek.Sunday)
            {
                // Check if it's not a holiday
                if (!IsHoliday(currentDate))
                {
                    workingDays++;
                }
            }
            currentDate = currentDate.AddDays(1);
        }

        return workingDays;
    }

    private List<DateTime> GetWeekendDays(DateTime startDate, DateTime endDate)
    {
        var weekendDays = new List<DateTime>();
        var currentDate = startDate;

        while (currentDate <= endDate)
        {
            if (currentDate.DayOfWeek == DayOfWeek.Saturday || currentDate.DayOfWeek == DayOfWeek.Sunday)
            {
                weekendDays.Add(currentDate);
            }
            currentDate = currentDate.AddDays(1);
        }

        return weekendDays;
    }

    private List<Holiday> GetHolidayDays(DateTime startDate, DateTime endDate)
    {
        return Holidays
            .Where(h => h.Date >= startDate.Date && h.Date <= endDate.Date)
            .ToList();
    }

    private bool IsHoliday(DateTime date)
    {
        return Holidays.Any(h => h.Date.Date == date.Date);
    }
}

// HRApp/ViewModels/LeaveBalanceViewModel.cs
public class LeaveBalanceViewModel : BaseViewModel
{
    private readonly ILeaveService _leaveService;

    private ObservableCollection<LeaveBalanceItem> _leaveBalances;
    private ObservableCollection<LeaveRequest> _recentRequests;
    private DateTime _selectedYear;

    public ObservableCollection<LeaveBalanceItem> LeaveBalances
    {
        get => _leaveBalances;
        set => SetProperty(ref _leaveBalances, value);
    }

    public ObservableCollection<LeaveRequest> RecentRequests
    {
        get => _recentRequests;
        set => SetProperty(ref _recentRequests, value);
    }

    public DateTime SelectedYear
    {
        get => _selectedYear;
        set
        {
            if (SetProperty(ref _selectedYear, value))
            {
                _ = LoadDataAsync();
            }
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand ViewRequestCommand { get; }
    public ICommand RequestLeaveCommand { get; }

    public LeaveBalanceViewModel(
        INavigationService navigationService,
        IDialogService dialogService,
        IApiService apiService,
        IAuthenticationService authService,
        ILeaveService leaveService)
        : base(navigationService, dialogService, apiService, authService)
    {
        _leaveService = leaveService;

        RefreshCommand = new RelayCommand(async () => await LoadDataAsync());
        ViewRequestCommand = new RelayCommand<LeaveRequest>(async (request) => await ViewRequestAsync(request));
        RequestLeaveCommand = new RelayCommand(async () => await RequestLeaveAsync());

        Title = "Leave Balance";
        SelectedYear = DateTime.Today;
    }

    public async Task InitializeAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        await ExecuteAsync(async () =>
        {
            var employeeId = await _authService.GetCurrentEmployeeIdAsync();
            
            await Task.WhenAll(
                LoadLeaveBalancesAsync(employeeId),
                LoadRecentRequestsAsync(employeeId)
            );
        }, "Loading leave balance...");
    }

    private async Task LoadLeaveBalancesAsync(Guid employeeId)
    {
        var balance = await _leaveService.GetLeaveBalanceAsync(employeeId);
        
        var balanceItems = new List<LeaveBalanceItem>
        {
            new LeaveBalanceItem
            {
                LeaveType = "Annual Leave",
                TotalDays = balance.AnnualLeaveTotal,
                UsedDays = balance.AnnualLeaveUsed,
                AvailableDays = balance.AnnualLeaveAvailable,
                Color = "#4CAF50"
            },
            new LeaveBalanceItem
            {
                LeaveType = "Sick Leave",
                TotalDays = balance.SickLeaveTotal,
                UsedDays = balance.SickLeaveUsed,
                AvailableDays = balance.SickLeaveAvailable,
                Color = "#2196F3"
            },
            new LeaveBalanceItem
            {
                LeaveType = "Personal Leave",
                TotalDays = balance.PersonalLeaveTotal,
                UsedDays = balance.PersonalLeaveUsed,
                AvailableDays = balance.PersonalLeaveAvailable,
                Color = "#FF9800"
            },
            new LeaveBalanceItem
            {
                LeaveType = "Maternity Leave",
                TotalDays = balance.MaternityLeaveTotal,
                UsedDays = balance.MaternityLeaveUsed,
                AvailableDays = balance.MaternityLeaveAvailable,
                Color = "#E91E63"
            }
        };

        LeaveBalances = new ObservableCollection<LeaveBalanceItem>(balanceItems);
    }

    private async Task LoadRecentRequestsAsync(Guid employeeId)
    {
        var requests = await _leaveService.GetLeaveRequestsAsync(employeeId);
        var recentRequests = requests
            .Where(r => r.RequestedAt.Year == SelectedYear.Year)
            .OrderByDescending(r => r.RequestedAt)
            .Take(10)
            .ToList();

        RecentRequests = new ObservableCollection<LeaveRequest>(recentRequests);
    }

    private async Task ViewRequestAsync(LeaveRequest request)
    {
        var parameters = new Dictionary<string, object>
        {
            { "leaveRequest", request }
        };

        await _navigationService.NavigateToAsync<LeaveRequestDetailsViewModel>(parameters);
    }

    private async Task RequestLeaveAsync()
    {
        await _navigationService.NavigateToAsync<LeaveRequestViewModel>();
    }
}
```

#### **Day 11-12: Payroll Management**
```csharp
// HRApp/ViewModels/PayrollViewModel.cs
public class PayrollViewModel : BaseViewModel
{
    private readonly IPayrollService _payrollService;
    private readonly IEmployeeService _employeeService;

    private ObservableCollection<Payslip> _payslips;
    private Payslip _selectedPayslip;
    private PayrollSummary _payrollSummary;
    private DateTime _selectedMonth;

    public ObservableCollection<Payslip> Payslips
    {
        get => _payslips;
        set => SetProperty(ref _payslips, value);
    }

    public Payslip SelectedPayslip
    {
        get => _selectedPayslip;
        set
        {
            if (SetProperty(ref _selectedPayslip, value))
            {
                _ = LoadPayslipDetailsAsync(value);
            }
        }
    }

    public PayrollSummary PayrollSummary
    {
        get => _payrollSummary;
        set => SetProperty(ref _payrollSummary, value);
    }

    public DateTime SelectedMonth
    {
        get => _selectedMonth;
        set
        {
            if (SetProperty(ref _selectedMonth, value))
            {
                _ = LoadPayslipsAsync();
            }
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand DownloadPayslipCommand { get; }
    public ICommand ViewDetailsCommand { get; }
    public ICommand ViewSummaryCommand { get; }

    public PayrollViewModel(
        INavigationService navigationService,
        IDialogService dialogService,
        IApiService apiService,
        IAuthenticationService authService,
        IPayrollService payrollService,
        IEmployeeService employeeService)
        : base(navigationService, dialogService, apiService, authService)
    {
        _payrollService = payrollService;
        _employeeService = employeeService;

        RefreshCommand = new RelayCommand(async () => await LoadPayslipsAsync());
        DownloadPayslipCommand = new RelayCommand<Payslip>(async (payslip) => await DownloadPayslipAsync(payslip));
        ViewDetailsCommand = new RelayCommand<Payslip>(async (payslip) => await ViewDetailsAsync(payslip));
        ViewSummaryCommand = new RelayCommand(async () => await ViewSummaryAsync());

        Title = "Payroll";
        SelectedMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    }

    public async Task InitializeAsync()
    {
        await LoadPayslipsAsync();
        await LoadPayrollSummaryAsync();
    }

    private async Task LoadPayslipsAsync()
    {
        await ExecuteAsync(async () =>
        {
            var employeeId = await _authService.GetCurrentEmployeeIdAsync();
            var payslips = await _payrollService.GetPayslipsAsync(employeeId, SelectedMonth.Year);
            
            var monthPayslips = payslips
                .Where(p => p.PayDate.Month == SelectedMonth.Month && p.PayDate.Year == SelectedMonth.Year)
                .OrderByDescending(p => p.PayDate)
                .ToList();

            Payslips = new ObservableCollection<Payslip>(monthPayslips);
        }, "Loading payslips...");
    }

    private async Task LoadPayrollSummaryAsync()
    {
        await ExecuteAsync(async () =>
        {
            var employeeId = await _authService.GetCurrentEmployeeIdAsync();
            var summary = await _payrollService.GetPayrollSummaryAsync(employeeId);
            PayrollSummary = summary;
        }, "Loading payroll summary...");
    }

    private async Task LoadPayslipDetailsAsync(Payslip payslip)
    {
        if (payslip == null)
            return;

        await ExecuteAsync(async () =>
        {
            var detailedPayslip = await _payrollService.GetPayslipDetailsAsync(payslip.Id);
            
            // Update the payslip in the collection
            var index = Payslips.IndexOf(payslip);
            if (index >= 0)
            {
                Payslips[index] = detailedPayslip;
            }
        }, "Loading payslip details...");
    }

    private async Task DownloadPayslipAsync(Payslip payslip)
    {
        await ExecuteAsync(async () =>
        {
            var pdfData = await _payrollService.DownloadPayslipAsync(payslip.Id);
            
            // Save to device
            var fileName = $"Payslip_{payslip.PayDate:yyyy-MM-dd}.pdf";
            var filePath = await SaveFileAsync(fileName, pdfData);
            
            await ShowSuccessAsync($"Payslip downloaded to {filePath}");
        }, "Downloading payslip...");
    }

    private async Task ViewDetailsAsync(Payslip payslip)
    {
        var parameters = new Dictionary<string, object>
        {
            { "payslip", payslip }
        };

        await _navigationService.NavigateToAsync<PayslipDetailsViewModel>(parameters);
    }

    private async Task ViewSummaryAsync()
    {
        await _navigationService.NavigateToAsync<PayrollSummaryViewModel>();
    }

    private async Task<string> SaveFileAsync(string fileName, byte[] data)
    {
        try
        {
            // Get downloads folder
            var downloadsPath = Path.Combine(FileSystem.AppDataDirectory, "Downloads");
            
            // Create directory if it doesn't exist
            Directory.CreateDirectory(downloadsPath);
            
            // Save file
            var filePath = Path.Combine(downloadsPath, fileName);
            await File.WriteAllBytesAsync(filePath, data);
            
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving file {FileName}", fileName);
            throw;
        }
    }
}

// HRApp/ViewModels/PayslipDetailsViewModel.cs
public class PayslipDetailsViewModel : BaseViewModel
{
    private readonly IPayrollService _payrollService;

    private Payslip _payslip;
    private ObservableCollection<PayrollItem> _earnings;
    private ObservableCollection<PayrollItem> _deductions;
    private PayrollSummary _summary;

    public Payslip Payslip
    {
        get => _payslip;
        set => SetProperty(ref _payslip, value);
    }

    public ObservableCollection<PayrollItem> Earnings
    {
        get => _earnings;
        set => SetProperty(ref _earnings, value);
    }

    public ObservableCollection<PayrollItem> Deductions
    {
        get => _deductions;
        set => SetProperty(ref _deductions, value);
    }

    public PayrollSummary Summary
    {
        get => _summary;
        set => SetProperty(ref _summary, value);
    }

    public ICommand ShareCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand DownloadCommand { get; }

    public PayslipDetailsViewModel(
        INavigationService navigationService,
        IDialogService dialogService,
        IApiService apiService,
        IAuthenticationService authService,
        IPayrollService payrollService)
        : base(navigationService, dialogService, apiService, authService)
    {
        _payrollService = payrollService;

        ShareCommand = new RelayCommand(async () => await SharePayslipAsync());
        PrintCommand = new RelayCommand(async () => await PrintPayslipAsync());
        DownloadCommand = new RelayCommand(async () => await DownloadPayslipAsync());

        Title = "Payslip Details";
    }

    public override async Task InitializeAsync(object parameter)
    {
        if (parameter is Payslip payslip)
        {
            Payslip = payslip;
            await LoadPayslipDetailsAsync(payslip.Id);
        }
    }

    private async Task LoadPayslipDetailsAsync(Guid payslipId)
    {
        await ExecuteAsync(async () =>
        {
            var detailedPayslip = await _payrollService.GetPayslipDetailsAsync(payslipId);
            
            Payslip = detailedPayslip;
            Earnings = new ObservableCollection<PayrollItem>(detailedPayslip.Earnings);
            Deductions = new ObservableCollection<PayrollItem>(detailedPayslip.Deductions);
            Summary = detailedPayslip.Summary;
        }, "Loading payslip details...");
    }

    private async Task SharePayslipAsync()
    {
        await ExecuteAsync(async () =>
        {
            var pdfData = await _payrollService.DownloadPayslipAsync(Payslip.Id);
            var fileName = $"Payslip_{Payslip.PayDate:yyyy-MM-dd}.pdf";
            
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Share Payslip",
                File = new ShareFile(fileName, pdfData)
            });
        }, "Sharing payslip...");
    }

    private async Task PrintPayslipAsync()
    {
        await ExecuteAsync(async () =>
        {
            var pdfData = await _payrollService.DownloadPayslipAsync(Payslip.Id);
            
            // Print the PDF
            await PrintPdfAsync(pdfData);
            
            await ShowSuccessAsync("Payslip sent to printer!");
        }, "Printing payslip...");
    }

    private async Task DownloadPayslipAsync()
    {
        await ExecuteAsync(async () =>
        {
            var pdfData = await _payrollService.DownloadPayslipAsync(Payslip.Id);
            
            var fileName = $"Payslip_{Payslip.PayDate:yyyy-MM-dd}.pdf";
            var filePath = await SaveFileAsync(fileName, pdfData);
            
            await ShowSuccessAsync($"Payslip downloaded to {filePath}");
        }, "Downloading payslip...");
    }

    private async Task PrintPdfAsync(byte[] pdfData)
    {
        // Implementation for printing PDF
        // This would depend on the platform and available printing APIs
        await Task.CompletedTask;
    }

    private async Task<string> SaveFileAsync(string fileName, byte[] data)
    {
        try
        {
            var downloadsPath = Path.Combine(FileSystem.AppDataDirectory, "Downloads");
            Directory.CreateDirectory(downloadsPath);
            
            var filePath = Path.Combine(downloadsPath, fileName);
            await File.WriteAllBytesAsync(filePath, data);
            
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving file {FileName}", fileName);
            throw;
        }
    }
}
```

### **3.3 Phase 3: Advanced Features (Week 5-6)**

#### **Day 13-15: Biometric Authentication**
```csharp
// HRApp/Services/BiometricService.cs
public class BiometricService : IBiometricService
{
    private readonly ILogger<BiometricService> _logger;
    private readonly IStorageService _storageService;

    public BiometricService(
        ILogger<BiometricService> logger,
        IStorageService storageService)
    {
        _logger = logger;
        _storageService = storageService;
    }

    public async Task<bool> IsBiometricAvailableAsync()
    {
        try
        {
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                return await BiometricAuthenticator.IsAvailableAsync();
            }
            else if (DeviceInfo.Platform == DevicePlatform.iOS)
            {
                return await BiometricAuthenticator.IsAvailableAsync();
            }
            else
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking biometric availability");
            return false;
        }
    }

    public async Task<BiometricResult> AuthenticateAsync(string reason)
    {
        try
        {
            var available = await IsBiometricAvailableAsync();
            if (!available)
            {
                return new BiometricResult
                {
                    Success = false,
                    Error = "Biometric authentication not available"
                };
            }

            var request = new AuthenticationRequest(reason);
            var result = await BiometricAuthenticator.AuthenticateAsync(request);

            return new BiometricResult
            {
                Success = result.Status == AuthenticationStatus.Success,
                Error = result.Status == AuthenticationStatus.Failed ? "Authentication failed" : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during biometric authentication");
            return new BiometricResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<BiometricData> CaptureBiometricAsync()
    {
        try
        {
            var available = await IsBiometricAvailableAsync();
            if (!available)
            {
                throw new InvalidOperationException("Biometric authentication not available");
            }

            // Capture biometric data for verification
            var result = await BiometricAuthenticator.CaptureAsync();
            
            return new BiometricData
            {
                Data = result.Data,
                Type = result.Type,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing biometric data");
            throw;
        }
    }

    public async Task<bool> VerifyBiometricAsync(Guid employeeId, BiometricData biometricData)
    {
        try
        {
            // Get stored biometric template for employee
            var storedTemplate = await _storageService.GetAsync<BiometricTemplate>($"biometric_{employeeId}");
            
            if (storedTemplate == null)
            {
                _logger.LogWarning("No biometric template found for employee {EmployeeId}", employeeId);
                return false;
            }

            // Verify biometric data against stored template
            var isValid = await VerifyBiometricMatchAsync(biometricData, storedTemplate);
            
            _logger.LogInformation("Biometric verification for employee {EmployeeId}: {Result}", employeeId, isValid);
            
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying biometric for employee {EmployeeId}", employeeId);
            return false;
        }
    }

    public async Task<bool> RegisterBiometricAsync(Guid employeeId)
    {
        try
        {
            var available = await IsBiometricAvailableAsync();
            if (!available)
            {
                throw new InvalidOperationException("Biometric authentication not available");
            }

            // Capture biometric data multiple times for enrollment
            var captures = new List<BiometricData>();
            
            for (int i = 0; i < 3; i++)
            {
                var capture = await CaptureBiometricAsync();
                captures.Add(capture);
                
                if (i < 2)
                {
                    // Wait a moment between captures
                    await Task.Delay(1000);
                }
            }

            // Create biometric template from captures
            var template = await CreateBiometricTemplateAsync(captures);
            
            // Store template
            await _storageService.SetAsync($"biometric_{employeeId}", template);
            
            _logger.LogInformation("Biometric registration completed for employee {EmployeeId}", employeeId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering biometric for employee {EmployeeId}", employeeId);
            return false;
        }
    }

    private async Task<bool> VerifyBiometricMatchAsync(BiometricData capturedData, BiometricTemplate storedTemplate)
    {
        // Implementation of biometric matching algorithm
        // This would use platform-specific APIs for biometric verification
        
        // For demonstration, we'll use a simple hash comparison
        var capturedHash = ComputeHash(capturedData.Data);
        var storedHash = storedTemplate.Hash;
        
        // In a real implementation, this would use sophisticated biometric matching
        return capturedHash == storedHash;
    }

    private async Task<BiometricTemplate> CreateBiometricTemplateAsync(List<BiometricData> captures)
    {
        // Combine multiple captures to create a robust template
        var combinedData = CombineBiometricData(captures);
        var hash = ComputeHash(combinedData);
        
        return new BiometricTemplate
        {
            EmployeeId = captures.First().EmployeeId,
            Hash = hash,
            CreatedAt = DateTime.UtcNow,
            Type = captures.First().Type
        };
    }

    private byte[] CombineBiometricData(List<BiometricData> captures)
    {
        // Implementation to combine multiple biometric captures
        // This would use sophisticated algorithms in a real implementation
        
        var combined = new List<byte>();
        
        foreach (var capture in captures)
        {
            combined.AddRange(capture.Data);
        }
        
        return combined.ToArray();
    }

    private string ComputeHash(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(data);
        return Convert.ToBase64String(hashBytes);
    }
}

// HRApp/Services/LocationService.cs
public class LocationService : ILocationService
{
    private readonly ILogger<LocationService> _logger;
    private readonly IGeolocation _geolocation;
    private readonly IPermissionService _permissionService;

    public LocationService(
        ILogger<LocationService> logger,
        IGeolocation geolocation,
        IPermissionService permissionService)
    {
        _logger = logger;
        _geolocation = geolocation;
        _permissionService = permissionService;
    }

    public async Task<Location> GetCurrentLocationAsync()
    {
        try
        {
            // Check location permission
            var hasPermission = await _permissionService.CheckPermissionAsync<LocationPermission>();
            if (!hasPermission)
            {
                // Request permission
                var granted = await _permissionService.RequestPermissionAsync<LocationPermission>();
                if (!granted)
                {
                    throw new UnauthorizedAccessException("Location permission denied");
                }
            }

            // Get current location
            var location = await _geolocation.GetLastKnownLocationAsync();
            
            if (location == null)
            {
                // Request current location
                location = await _geolocation.GetLocationAsync(new GeolocationRequest
                {
                    DesiredAccuracy = GeolocationAccuracy.High,
                    Timeout = TimeSpan.FromSeconds(30)
                });
            }

            return new Location
            {
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                Altitude = location.Altitude ?? 0,
                Accuracy = location.Accuracy,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current location");
            throw;
        }
    }

    public async Task<bool> IsLocationEnabledAsync()
    {
        try
        {
            var location = await _geolocation.GetLastKnownLocationAsync();
            return location != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking location status");
            return false;
        }
    }

    public async Task<Location> GetLocationWithTimeoutAsync(TimeSpan timeout)
    {
        try
        {
            var cts = new CancellationTokenSource(timeout);
            
            var location = await _geolocation.GetLocationAsync(new GeolocationRequest
            {
                DesiredAccuracy = GeolocationAccuracy.High,
                Timeout = timeout
            }, cts.Token);

            return new Location
            {
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                Altitude = location.Altitude ?? 0,
                Accuracy = location.Accuracy,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting location with timeout");
            throw;
        }
    }

    public async Task<double> CalculateDistanceAsync(Location location1, Location location2)
    {
        try
        {
            var coord1 = new Location(location1.Latitude, location1.Longitude);
            var coord2 = new Location(location2.Latitude, location2.Longitude);
            
            var distance = Location.CalculateDistance(coord1, coord2, DistanceUnits.Kilometers);
            
            return distance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating distance");
            throw;
        }
    }

    public async Task<bool> IsWithinGeofenceAsync(Location currentLocation, Geofence geofence)
    {
        try
        {
            var distance = await CalculateDistanceAsync(currentLocation, geofence.Center);
            return distance <= geofence.Radius;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking geofence");
            return false;
        }
    }
}
```

### **3.4 Phase 4: UI/UX Enhancement (Week 7-8)**

#### **Day 18-20: Advanced UI Components**
```xml
<!-- HRApp/Pages/DashboardPage.xaml -->
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:HRApp.ViewModels"
             xmlns:controls="clr-namespace:HRApp.Controls"
             x:DataType="vm:DashboardViewModel"
             x:Class="HRApp.Pages.DashboardPage">

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
                </Grid.ColumnDefinitions>

                <StackLayout Grid.Column="0" Spacing="5">
                    <Label Text="Welcome back," TextColor="White" FontSize="14"/>
                    <Label Text="{Binding CurrentEmployee.FullName}" 
                           TextColor="White" FontSize="18" FontAttributes="Bold"/>
                </StackLayout>

                <Frame Grid.Column="1" BackgroundColor="White" CornerRadius="25" Padding="8" HasShadow="True">
                    <Image Source="{Binding CurrentEmployee.PhotoUrl}" 
                           WidthRequest="40" HeightRequest="40" Aspect="AspectFill"/>
                </Frame>
            </Grid>
        </Frame>

        <!-- Main Content -->
        <ScrollView Grid.Row="1">
            <StackLayout Padding="20" Spacing="20">

                <!-- Quick Stats -->
                <Frame BackgroundColor="White" CornerRadius="15" Padding="15" HasShadow="True">
                    <Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>

                        <StackLayout Grid.Column="0" HorizontalOptions="Center" Spacing="5">
                            <Label Text="{Binding AttendanceSummary.ThisMonthDays}" 
                                   TextColor="{StaticResource PrimaryColor}" 
                                   FontSize="24" FontAttributes="Bold" HorizontalOptions="Center"/>
                            <Label Text="Days Worked" TextColor="Gray" FontSize="12" HorizontalOptions="Center"/>
                        </StackLayout>

                        <StackLayout Grid.Column="1" HorizontalOptions="Center" Spacing="5">
                            <Label Text="{Binding LeaveSummary.AvailableDays}" 
                                   TextColor="{StaticResource AccentColor}" 
                                   FontSize="24" FontAttributes="Bold" HorizontalOptions="Center"/>
                            <Label Text="Leave Days" TextColor="Gray" FontSize="12" HorizontalOptions="Center"/>
                        </StackLayout>

                        <StackLayout Grid.Column="2" HorizontalOptions="Center" Spacing="5">
                            <Label Text="{Binding PayrollSummary.CurrentSalary, StringFormat='{0:C0}'}" 
                                   TextColor="{StaticResource SuccessColor}" 
                                   FontSize="24" FontAttributes="Bold" HorizontalOptions="Center"/>
                            <Label Text="Salary" TextColor="Gray" FontSize="12" HorizontalOptions="Center"/>
                        </StackLayout>
                    </Grid>
                </Frame>

                <!-- Clock In/Out Card -->
                <Frame BackgroundColor="White" CornerRadius="15" Padding="15" HasShadow="True">
                    <StackLayout Spacing="10">
                        <Label Text="Time Tracking" FontSize="16" FontAttributes="Bold"/>
                        
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>

                            <Button Grid.Column="0" 
                                    Text="Clock In" 
                                    Command="{Binding ClockInCommand}"
                                    BackgroundColor="{StaticResource SuccessColor}"
                                    TextColor="White"
                                    CornerRadius="10"
                                    Margin="5"
                                    IsEnabled="{Binding CanClockIn}"/>

                            <Button Grid.Column="1" 
                                    Text="Clock Out" 
                                    Command="{Binding ClockOutCommand}"
                                    BackgroundColor="{StaticResource DangerColor}"
                                    TextColor="White"
                                    CornerRadius="10"
                                    Margin="5"
                                    IsEnabled="{Binding CanClockOut}"/>
                        </Grid>

                        <StackLayout BindableLayout.ItemsSource="{Binding TodayAttendance}">
                            <BindableLayout.ItemTemplate>
                                <DataTemplate>
                                    <Frame BackgroundColor="{StaticResource LightBackgroundColor}" 
                                           CornerRadius="10" Padding="10" Margin="0,5">
                                        <StackLayout>
                                            <Label Text="{Binding Status}" FontAttributes="Bold"/>
                                            <Label Text="{Binding ClockInTime, StringFormat='In: {0:HH:mm}'}" FontSize="12"/>
                                            <Label Text="{Binding ClockOutTime, StringFormat='Out: {0:HH:mm}'}" FontSize="12"/>
                                        </StackLayout>
                                    </Frame>
                                </DataTemplate>
                            </BindableLayout.ItemTemplate>
                        </StackLayout>
                    </StackLayout>
                </Frame>

                <!-- Quick Actions -->
                <Label Text="Quick Actions" FontSize="16" FontAttributes="Bold"/>
                
                <CollectionView ItemsSource="{Binding DashboardItems}">
                    <CollectionView.ItemTemplate>
                        <DataTemplate>
                            <Frame BackgroundColor="White" CornerRadius="15" Padding="15" HasShadow="True" Margin="0,5">
                                <Grid>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="Auto"/>
                                        <ColumnDefinition Width="*"/>
                                        <ColumnDefinition Width="Auto"/>
                                    </Grid.ColumnDefinitions>

                                    <Frame Grid.Column="0" 
                                           BackgroundColor="{Binding Color}" 
                                           CornerRadius="25" 
                                           WidthRequest="50" HeightRequest="50"
                                           Padding="10">
                                        <Label Text="{Binding Icon}" 
                                               TextColor="White" 
                                               FontSize="20" 
                                               HorizontalOptions="Center" 
                                               VerticalOptions="Center"/>
                                    </Frame>

                                    <StackLayout Grid.Column="1" VerticalOptions="Center" Margin="15,0">
                                        <Label Text="{Binding Title}" FontAttributes="Bold"/>
                                        <Label Text="{Binding Description}" TextColor="Gray" FontSize="12"/>
                                    </StackLayout>

                                    <Image Grid.Column="2" 
                                           Source="arrow_right.png" 
                                           WidthRequest="20" HeightRequest="20"
                                           VerticalOptions="Center"/>
                                </Grid>

                                <Frame.GestureRecognizers>
                                    <TapGestureRecognizer Command="{Binding Command}"/>
                                </Frame.GestureRecognizers>
                            </Frame>
                        </DataTemplate>
                    </CollectionView.ItemTemplate>
                </CollectionView>

            </StackLayout>
        </ScrollView>

        <!-- Bottom Navigation -->
        <Frame Grid.Row="2" BackgroundColor="White" Padding="10" HasShadow="True">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>

                <StackLayout Grid.Column="0" HorizontalOptions="Center" Spacing="5">
                    <Image Source="home.png" WidthRequest="24" HeightRequest="24"/>
                    <Label Text="Home" FontSize="10" TextColor="{StaticResource PrimaryColor}"/>
                </StackLayout>

                <StackLayout Grid.Column="1" HorizontalOptions="Center" Spacing="5">
                    <Image Source="attendance.png" WidthRequest="24" HeightRequest="24"/>
                    <Label Text="Attendance" FontSize="10" TextColor="Gray"/>
                </StackLayout>

                <StackLayout Grid.Column="2" HorizontalOptions="Center" Spacing="5">
                    <Image Source="leave.png" WidthRequest="24" HeightRequest="24"/>
                    <Label Text="Leave" FontSize="10" TextColor="Gray"/>
                </StackLayout>

                <StackLayout Grid.Column="3" HorizontalOptions="Center" Spacing="5">
                    <Image Source="payroll.png" WidthRequest="24" HeightRequest="24"/>
                    <Label Text="Payroll" FontSize="10" TextColor="Gray"/>
                </StackLayout>
            </Grid>
        </Frame>
    </Grid>
</ContentPage>

<!-- HRApp/Controls/CustomProgressBar.xaml -->
<ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="HRApp.Controls.CustomProgressBar">

    <Frame BackgroundColor="{StaticResource LightBackgroundColor}" CornerRadius="10" Padding="5">
        <Grid>
            <ProgressBar x:Name="ProgressBar" 
                         Progress="0" 
                         ProgressColor="{StaticResource PrimaryColor}"
                         HeightRequest="8"/>
            <Label x:Name="ProgressLabel" 
                   Text="0%" 
                   HorizontalOptions="Center" 
                   VerticalOptions="Center"
                   FontSize="10"
                   TextColor="White"/>
        </Grid>
    </Frame>
</ContentView>

<!-- HRApp/Controls/CustomProgressBar.xaml.cs -->
public partial class CustomProgressBar : ContentView
{
    public static readonly BindableProperty ProgressProperty =
        BindableProperty.Create(nameof(Progress), typeof(double), typeof(CustomProgressBar), 0.0,
            propertyChanged: OnProgressPropertyChanged);

    public static readonly BindableProperty MaxValueProperty =
        BindableProperty.Create(nameof(MaxValue), typeof(double), typeof(CustomProgressBar), 100.0);

    public static readonly BindableProperty ShowPercentageProperty =
        BindableProperty.Create(nameof(ShowPercentage), typeof(bool), typeof(CustomProgressBar), true);

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public double MaxValue
    {
        get => (double)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    public bool ShowPercentage
    {
        get => (bool)GetValue(ShowPercentageProperty);
        set => SetValue(ShowPercentageProperty, value);
    }

    public CustomProgressBar()
    {
        InitializeComponent();
    }

    private static void OnProgressPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (CustomProgressBar)bindable;
        control.UpdateProgress();
    }

    private void UpdateProgress()
    {
        var progress = Progress / MaxValue;
        ProgressBar.Progress = progress;
        
        if (ShowPercentage)
        {
            ProgressLabel.Text = $"{progress * 100:F0}%";
            ProgressLabel.IsVisible = true;
        }
        else
        {
            ProgressLabel.IsVisible = false;
        }

        // Change color based on progress
        if (progress >= 0.8)
        {
            ProgressBar.ProgressColor = Colors.Green;
        }
        else if (progress >= 0.5)
        {
            ProgressBar.ProgressColor = Colors.Orange;
        }
        else
        {
            ProgressBar.ProgressColor = Colors.Red;
        }
    }
}
```

---

## **4. IMPLEMENTATION PLAN**

### **4.1 Week 1-2: Foundation Architecture**
- [ ] Implement MVVM architecture
- [ ] Setup dependency injection
- [ ] Create base view models
- [ ] Implement navigation service
- [ ] Setup API integration
- [ ] Add authentication service

### **4.2 Week 3-4: HR Features Implementation**
- [ ] Implement attendance tracking
- [ ] Add leave management system
- [ ] Create payroll management
- [ ] Setup employee profiles
- [ ] Add biometric authentication
- [ ] Implement location services

### **4.3 Week 5-6: Advanced Features**
- [ ] Add offline support
- [ ] Implement data synchronization
- [ ] Create notification system
- [ ] Add reporting features
- [ ] Setup analytics
- [ ] Implement security features

### **4.4 Week 7-8: UI/UX Enhancement**
- [ ] Design professional UI
- [ ] Create custom controls
- [ ] Add animations
- [ ] Implement accessibility
- [ ] Add localization
- [ ] Optimize performance

---

## **5. SUCCESS METRICS**

### **5.1 User Experience Metrics**
- **App Rating:** >4.5 stars
- **User Engagement:** >80% daily active users
- **Task Completion:** >90% task completion rate
- **User Satisfaction:** >85% satisfaction score

### **5.2 Technical Metrics**
- **App Performance:** <3 seconds startup time
- **Crash Rate:** <0.5% crash rate
- **Battery Usage:** <10% battery consumption
- **Memory Usage:** <100MB memory usage

### **5.3 Business Metrics**
- **Attendance Accuracy:** >99% accuracy
- **Leave Processing:** <24 hours processing time
- **Payroll Accuracy:** 100% accuracy
- **Biometric Success:** >95% success rate

---

## **6. RISK MITIGATION**

### **6.1 Technical Risks**
1. **Biometric Compatibility** - Platform-specific implementations
2. **Location Accuracy** - Multiple location sources
3. **Offline Sync** - Robust synchronization
4. **Performance** - Optimization strategies

### **6.2 Business Risks**
1. **User Adoption** - Intuitive design
2. **Data Security** - Enterprise-grade security
3. **Compliance** - Labor law compliance
4. **Integration** - API compatibility

---

## **7. NEXT STEPS**

### **7.1 Immediate Actions (This Week)**
1. **Setup Architecture** - MVVM foundation
2. **Implement Services** - Core HR services
3. **Create UI** - Professional interface
4. **Add Authentication** - Secure login system

### **7.2 Short-term Goals (2 Weeks)**
1. **Complete Core Features** - Attendance, leave, payroll
2. **Add Biometrics** - Secure authentication
3. **Implement Offline** - Offline capability
4. **Add Reporting** - HR analytics

### **7.3 Long-term Goals (2 Months)**
1. **Advanced Features** - AI-powered insights
2. **Integration** - Third-party integrations
3. **Optimization** - Performance tuning
4. **Deployment** - App store deployment

---

## **8. SUMMARY**

### **8.1 Current State**
- **Basic MAUI template** with no HR features
- **No API integration** or backend connectivity
- **No authentication** or security measures
- **Template UI** only
- **No data management** or persistence

### **8.2 Target State**
- **Professional HR app** with comprehensive features
- **Full API integration** with real-time sync
- **Biometric authentication** and location tracking
- **Modern UI/UX** with intuitive workflows
- **Offline support** and data synchronization

### **8.3 Implementation Strategy**
- **8-week phased approach** with clear milestones
- **User-centric** design philosophy
- **Security-first** implementation
- **Performance-optimized** development

**Status:** HrApp module needs complete transformation from template to professional HR application with advanced features.
