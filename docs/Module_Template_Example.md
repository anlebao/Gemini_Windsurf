# MODULE TEMPLATE EXAMPLE - HR Module

## 🏗️ FILE STRUCTURE

```
Modules/
├── HR/
│   ├── Components/
│   │   ├── EmployeeCard.razor
│   │   ├── EmployeeForm.razor
│   │   ├── ScheduleTable.razor
│   │   └── PayrollChart.razor
│   ├── Pages/
│   │   ├── Index.razor (Dashboard)
│   │   ├── Employees.razor
│   │   ├── EmployeeDetail.razor
│   │   ├── Schedule.razor
│   │   └── Payroll.razor
│   ├── Shared/
│   │   └── HRLayout.razor
│   ├── Services/
│   │   ├── IEmployeeService.cs
│   │   ├── EmployeeService.cs
│   │   ├── IScheduleService.cs
│   │   └── ScheduleService.cs
│   ├── Models/
│   │   ├── Employee.cs
│   │   ├── Schedule.cs
│   │   └── Payroll.cs
│   └── Styles/
│       └── hr-module.css
```

## 📄 EXAMPLE FILES

### 1. PROJECT FILE
```xml
<!-- Modules/HR/VanAn.HR.csproj -->
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\1_Shared\VanAn.Shared.csproj" />
    <ProjectReference Include="..\..\UI.Platform\VanAn.UI.Platform.csproj" />
    <ProjectReference Include="..\..\3_CoreHub\VanAn.CoreHub.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.Web" />
    <PackageReference Include="Microsoft.AspNetCore.Components.Authorization" />
  </ItemGroup>
</Project>
```

### 2. LAYOUT COMPONENT
```razor
@* Modules/HR/Shared/HRLayout.razor *@
@inject IThemeProvider ThemeProvider
@inject ITenantService TenantService
@inject IEmployeeService EmployeeService
@inject NavigationManager NavigationManager

<div class="hr-layout @ThemeProvider.CurrentTheme">
  <!-- Header -->
  <header class="hr-header">
    <div class="hr-header__content">
      <div class="hr-header__logo">
        <h1>HR Management</h1>
      </div>
      
      <div class="hr-header__user">
        <VanADropdown>
          <VanAButton Variant="ghost" Size="small">
            @TenantService.CurrentTenantName
            <i class="fas fa-chevron-down"></i>
          </VanAButton>
          
          <VanADropdownMenu>
            <VanADropdownItem OnClick="@SignOut">Sign Out</VanADropdownItem>
          </VanADropdownMenu>
        </VanADropdown>
      </div>
    </div>
  </header>

  <!-- Navigation -->
  <nav class="hr-navigation">
    <VanANavigation Items="@menuItems" CurrentPath="@NavigationManager.Uri" />
  </nav>

  <!-- Main Content -->
  <main class="hr-main">
    <div class="hr-main__content">
      @Body
    </div>
  </main>

  <!-- Footer -->
  <footer class="hr-footer">
    <VanAFooter />
  </footer>
</div>

@code {
  private List<NavigationItem> menuItems = new()
  {
    new() { Text = "Dashboard", Icon = "dashboard", Href = "/hr" },
    new() { Text = "Employees", Icon = "people", Href = "/hr/employees" },
    new() { Text = "Schedule", Icon = "calendar", Href = "/hr/schedule" },
    new() { Text = "Payroll", Icon = "payments", Href = "/hr/payroll" },
    new() { Text = "Reports", Icon = "assessment", Href = "/hr/reports" }
  };

  private async Task SignOut()
  {
    // Implementation for sign out
    NavigationManager.NavigateTo("/login");
  }
}
```

### 3. DASHBOARD PAGE
```razor
@* Modules/HR/Pages/Index.razor *@
@page "/hr"
@layout HRLayout

<div class="hr-dashboard">
  <!-- Stats Cards -->
  <div class="hr-dashboard__stats">
    <VanACard CssClass="stat-card">
      <Body>
        <div class="stat-content">
          <h3>@totalEmployees</h3>
          <p>Total Employees</p>
        </div>
        <div class="stat-icon">
          <i class="fas fa-users"></i>
        </div>
      </Body>
    </VanACard>

    <VanACard CssClass="stat-card">
      <Body>
        <div class="stat-content">
          <h3>@activeEmployees</h3>
          <p>Active Employees</p>
        </div>
        <div class="stat-icon">
          <i class="fas fa-user-check"></i>
        </div>
      </Body>
    </VanACard>

    <VanACard CssClass="stat-card">
      <Body>
        <div class="stat-content">
          <h3>@newHiresThisMonth</h3>
          <p>New Hires This Month</p>
        </div>
        <div class="stat-icon">
          <i class="fas fa-user-plus"></i>
        </div>
      </Body>
    </VanACard>
  </div>

  <!-- Charts Section -->
  <div class="hr-dashboard__charts">
    <div class="chart-container">
      <VanACard Header="Employee Distribution">
        <Body>
          <VanAChart Type="pie" Data="@departmentData" Options="@chartOptions" />
        </Body>
      </VanACard>
    </div>

    <div class="chart-container">
      <VanACard Header="Hiring Trend">
        <Body>
          <VanAChart Type="line" Data="@hiringTrendData" Options="@chartOptions" />
        </Body>
      </VanACard>
    </div>
  </div>

  <!-- Recent Activities -->
  <div class="hr-dashboard__activities">
    <VanACard Header="Recent Activities">
      <Body>
        <VanATable 
          Data="@recentActivities"
          Columns="@activityColumns"
          PageSize="5"
          Filterable="false" />
      </Body>
    </VanACard>
  </div>
</div>

@code {
  private int totalEmployees = 0;
  private int activeEmployees = 0;
  private int newHiresThisMonth = 0;
  private List<ChartData> departmentData = new();
  private List<ChartData> hiringTrendData = new();
  private List<Activity> recentActivities = new();
  private List<TableColumn> activityColumns = new();

  protected override async Task OnInitializedAsync()
  {
    await LoadDashboardData();
  }

  private async Task LoadDashboardData()
  {
    try
    {
      var stats = await EmployeeService.GetDashboardStatsAsync();
      totalEmployees = stats.TotalEmployees;
      activeEmployees = stats.ActiveEmployees;
      newHiresThisMonth = stats.NewHiresThisMonth;

      departmentData = stats.DepartmentDistribution;
      hiringTrendData = stats.HiringTrend;
      recentActivities = stats.RecentActivities;

      activityColumns = new()
      {
        new() { Header = "Employee", Property = "EmployeeName", Width = 3 },
        new() { Header = "Action", Property = "Action", Width = 3 },
        new() { Header = "Date", Property = "Date", Width = 2 },
        new() { Header = "By", Property = "PerformedBy", Width = 3 }
      };
    }
    catch (Exception ex)
    {
      // Handle error
      Console.WriteLine($"Error loading dashboard data: {ex.Message}");
    }
  }

  private ChartOptions chartOptions = new()
  {
    Responsive = true,
    MaintainAspectRatio = false,
    Plugins = new ChartPlugins
    {
      Legend = new ChartLegend { Position = "bottom" }
    }
  };
}
```

### 4. EMPLOYEE LIST PAGE
```razor
@* Modules/HR/Pages/Employees.razor *@
@page "/hr/employees"
@layout HRLayout

<div class="hr-employees">
  <!-- Header Section -->
  <div class="hr-employees__header">
    <div class="hr-employees__title">
      <h2>Employee Management</h2>
      <p>Manage your organization's employees</p>
    </div>
    
    <div class="hr-employees__actions">
      <VanAButton Variant="primary" OnClick="@ShowAddEmployee">
        <i class="fas fa-plus"></i>
        Add Employee
      </VanAButton>
    </div>
  </div>

  <!-- Filters Section -->
  <div class="hr-employees__filters">
    <VanACard CssClass="filter-card">
      <Body>
        <div class="filter-row">
          <VanAInput 
            Placeholder="Search employees..." 
            Value="@searchTerm"
            OnValueChanged="@OnSearchChanged" />
          
          <VanASelect 
            Placeholder="Department" 
            Options="@departmentOptions"
            Value="@selectedDepartment"
            OnValueChanged="@OnDepartmentChanged" />
          
          <VanASelect 
            Placeholder="Status" 
            Options="@statusOptions"
            Value="@selectedStatus"
            OnValueChanged="@OnStatusChanged" />
          
          <VanAButton Variant="secondary" OnClick="@ResetFilters">
            <i class="fas fa-refresh"></i>
            Reset
          </VanAButton>
        </div>
      </Body>
    </VanACard>
  </div>

  <!-- Employee Table -->
  <div class="hr-employees__table">
    <VanACard>
      <Body>
        <VanATable 
          Data="@filteredEmployees"
          Columns="@employeeColumns"
          PageSize="10"
          Filterable="true"
          Sortable="true"
          OnRowClick="@OnEmployeeClick"
          Loading="@isLoading" />
      </Body>
    </VanACard>
  </div>
</div>

<!-- Add/Edit Employee Modal -->
<VanAModal IsVisible="@showEmployeeModal" OnClose="@CloseEmployeeModal">
  <Header>
    <h3>@(editingEmployee?.Id == null ? "Add Employee" : "Edit Employee")</h3>
  </Header>
  
  <Body>
    <EmployeeForm 
      Employee="@editingEmployee"
      OnSave="@OnEmployeeSave"
      OnCancel="@CloseEmployeeModal" />
  </Body>
</VanAModal>

@code {
  private List<Employee> employees = new();
  private List<Employee> filteredEmployees = new();
  private Employee? editingEmployee;
  private bool showEmployeeModal = false;
  private bool isLoading = true;
  
  private string searchTerm = "";
  private string selectedDepartment = "";
  private string selectedStatus = "";
  
  private List<SelectOption> departmentOptions = new();
  private List<SelectOption> statusOptions = new();
  private List<TableColumn> employeeColumns = new();

  protected override async Task OnInitializedAsync()
  {
    await LoadEmployees();
    SetupFilters();
    SetupTableColumns();
  }

  private async Task LoadEmployees()
  {
    isLoading = true;
    try
    {
      employees = await EmployeeService.GetAllAsync();
      filteredEmployees = employees;
    }
    catch (Exception ex)
    {
      // Handle error
      Console.WriteLine($"Error loading employees: {ex.Message}");
    }
    finally
    {
      isLoading = false;
    }
  }

  private void SetupFilters()
  {
    departmentOptions = new()
    {
      new() { Value = "", Text = "All Departments" },
      new() { Value = "IT", Text = "IT" },
      new() { Value = "HR", Text = "HR" },
      new() { Value = "Finance", Text = "Finance" },
      new() { Value = "Operations", Text = "Operations" }
    };

    statusOptions = new()
    {
      new() { Value = "", Text = "All Status" },
      new() { Value = "Active", Text = "Active" },
      new() { Value = "Inactive", Text = "Inactive" },
      new() { Value = "OnLeave", Text = "On Leave" }
    };
  }

  private void SetupTableColumns()
  {
    employeeColumns = new()
    {
      new() { Header = "ID", Property = "Id", Width = 1 },
      new() { Header = "Name", Property = "FullName", Width = 3 },
      new() { Header = "Email", Property = "Email", Width = 3 },
      new() { Header = "Department", Property = "Department", Width = 2 },
      new() { Header = "Status", Property = "Status", Width = 2 },
      new() { Header = "Actions", Template = CreateActionTemplate(), Width = 2 }
    };
  }

  private RenderFragment CreateActionTemplate() => __builder =>
  {
    <div class="table-actions">
      <VanAButton Size="small" Variant="ghost" OnClick="@() => EditEmployee(context)">
        <i class="fas fa-edit"></i>
      </VanAButton>
      <VanAButton Size="small" Variant="ghost" OnClick="@() => DeleteEmployee(context)">
        <i class="fas fa-trash"></i>
      </VanAButton>
    </div>
  };

  private void ShowAddEmployee()
  {
    editingEmployee = new Employee();
    showEmployeeModal = true;
  }

  private void EditEmployee(Employee employee)
  {
    editingEmployee = employee;
    showEmployeeModal = true;
  }

  private async Task DeleteEmployee(Employee employee)
  {
    // Show confirmation dialog
    var confirmed = await ShowConfirmDialog("Are you sure you want to delete this employee?");
    if (confirmed)
    {
      try
      {
        await EmployeeService.DeleteAsync(employee.Id);
        await LoadEmployees();
        ShowSuccessMessage("Employee deleted successfully");
      }
      catch (Exception ex)
      {
        ShowErrorMessage($"Error deleting employee: {ex.Message}");
      }
    }
  }

  private async Task OnEmployeeSave(Employee employee)
  {
    try
    {
      if (employee.Id == null)
      {
        await EmployeeService.CreateAsync(employee);
        ShowSuccessMessage("Employee created successfully");
      }
      else
      {
        await EmployeeService.UpdateAsync(employee);
        ShowSuccessMessage("Employee updated successfully");
      }
      
      await LoadEmployees();
      CloseEmployeeModal();
    }
    catch (Exception ex)
    {
      ShowErrorMessage($"Error saving employee: {ex.Message}");
    }
  }

  private void CloseEmployeeModal()
  {
    showEmployeeModal = false;
    editingEmployee = null;
  }

  private void OnSearchChanged(string value)
  {
    searchTerm = value;
    ApplyFilters();
  }

  private void OnDepartmentChanged(string value)
  {
    selectedDepartment = value;
    ApplyFilters();
  }

  private void OnStatusChanged(string value)
  {
    selectedStatus = value;
    ApplyFilters();
  }

  private void ApplyFilters()
  {
    filteredEmployees = employees.Where(e =>
      (string.IsNullOrEmpty(searchTerm) || 
       e.FullName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
       e.Email.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) &&
      (string.IsNullOrEmpty(selectedDepartment) || e.Department == selectedDepartment) &&
      (string.IsNullOrEmpty(selectedStatus) || e.Status == selectedStatus)
    ).ToList();
  }

  private void ResetFilters()
  {
    searchTerm = "";
    selectedDepartment = "";
    selectedStatus = "";
    ApplyFilters();
  }

  private void OnEmployeeClick(Employee employee)
  {
    NavigationManager.NavigateTo($"/hr/employees/{employee.Id}");
  }
}
```

### 5. EMPLOYEE FORM COMPONENT
```razor
@* Modules/HR/Components/EmployeeForm.razor *@
<VanAForm OnSubmit="@HandleSubmit" CssClass="employee-form">
  <div class="form-grid">
    <div class="form-group">
      <VanAInput 
        Label="First Name"
        Placeholder="Enter first name"
        Value="@Employee.FirstName"
        OnValueChanged="@((value) => Employee.FirstName = value)"
        Required="true" />
    </div>

    <div class="form-group">
      <VanAInput 
        Label="Last Name"
        Placeholder="Enter last name"
        Value="@Employee.LastName"
        OnValueChanged="@((value) => Employee.LastName = value)"
        Required="true" />
    </div>

    <div class="form-group">
      <VanAInput 
        Label="Email"
        Type="email"
        Placeholder="Enter email address"
        Value="@Employee.Email"
        OnValueChanged="@((value) => Employee.Email = value)"
        Required="true" />
    </div>

    <div class="form-group">
      <VanAInput 
        Label="Phone"
        Type="tel"
        Placeholder="Enter phone number"
        Value="@Employee.Phone"
        OnValueChanged="@((value) => Employee.Phone = value)" />
    </div>

    <div class="form-group">
      <VanASelect 
        Label="Department"
        Placeholder="Select department"
        Options="@departmentOptions"
        Value="@Employee.Department"
        OnValueChanged="@((value) => Employee.Department = value)"
        Required="true" />
    </div>

    <div class="form-group">
      <VanASelect 
        Label="Status"
        Placeholder="Select status"
        Options="@statusOptions"
        Value="@Employee.Status"
        OnValueChanged="@((value) => Employee.Status = value)"
        Required="true" />
    </div>

    <div class="form-group full-width">
      <VanAInput 
        Label="Address"
        Type="textarea"
        Placeholder="Enter address"
        Value="@Employee.Address"
        OnValueChanged="@((value) => Employee.Address = value)" />
    </div>
  </div>

  <div class="form-actions">
    <VanAButton Type="submit" Variant="primary">
      @(Employee.Id == null ? "Create Employee" : "Update Employee")
    </VanAButton>
    <VanAButton Variant="secondary" OnClick="@OnCancel">Cancel</VanAButton>
  </div>
</VanAForm>

@code {
  [Parameter]
  public Employee Employee { get; set; } = new();

  [Parameter]
  public EventCallback<Employee> OnSave { get; set; }

  [Parameter]
  public EventCallback OnCancel { get; set; }

  private List<SelectOption> departmentOptions = new();
  private List<SelectOption> statusOptions = new();

  protected override void OnInitialized()
  {
    SetupOptions();
  }

  private void SetupOptions()
  {
    departmentOptions = new()
    {
      new() { Value = "IT", Text = "IT" },
      new() { Value = "HR", Text = "HR" },
      new() { Value = "Finance", Text = "Finance" },
      new() { Value = "Operations", Text = "Operations" }
    };

    statusOptions = new()
    {
      new() { Value = "Active", Text = "Active" },
      new() { Value = "Inactive", Text = "Inactive" },
      new() { Value = "OnLeave", Text = "On Leave" }
    };
  }

  private async Task HandleSubmit()
  {
    await OnSave.InvokeAsync(Employee);
  }
}
```

### 6. MODULE STYLES
```css
/* Modules/HR/Styles/hr-module.css */
.hr-layout {
  --color-module-primary: #2563eb;
  --color-module-secondary: #64748b;
  --color-module-accent: #f59e0b;
}

/* Dashboard Styles */
.hr-dashboard {
  display: grid;
  gap: var(--spacing-6);
}

.hr-dashboard__stats {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
  gap: var(--spacing-4);
}

.stat-card {
  background: linear-gradient(135deg, var(--color-module-primary), var(--color-module-secondary));
  color: white;
}

.stat-card .vanan-card__body {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.stat-content h3 {
  font-size: var(--font-size-2xl);
  font-weight: var(--font-weight-bold);
  margin: 0;
}

.stat-content p {
  margin: 0;
  opacity: 0.9;
}

.stat-icon {
  font-size: var(--font-size-3xl);
  opacity: 0.8;
}

.hr-dashboard__charts {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(400px, 1fr));
  gap: var(--spacing-4);
}

.chart-container {
  min-height: 300px;
}

/* Employee List Styles */
.hr-employees__header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: var(--spacing-6);
}

.hr-employees__title h2 {
  margin: 0 0 var(--spacing-2) 0;
  color: var(--color-neutral-900);
}

.hr-employees__title p {
  margin: 0;
  color: var(--color-neutral-600);
}

.hr-employees__filters {
  margin-bottom: var(--spacing-4);
}

.filter-row {
  display: grid;
  grid-template-columns: 2fr 1fr 1fr auto;
  gap: var(--spacing-3);
  align-items: end;
}

/* Form Styles */
.employee-form {
  display: grid;
  gap: var(--spacing-6);
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: var(--spacing-4);
}

.form-group.full-width {
  grid-column: 1 / -1;
}

.form-actions {
  display: flex;
  gap: var(--spacing-3);
  justify-content: flex-end;
}

/* Responsive Design */
@media (max-width: 768px) {
  .hr-dashboard__stats {
    grid-template-columns: 1fr;
  }
  
  .hr-dashboard__charts {
    grid-template-columns: 1fr;
  }
  
  .hr-employees__header {
    flex-direction: column;
    align-items: flex-start;
    gap: var(--spacing-3);
  }
  
  .filter-row {
    grid-template-columns: 1fr;
  }
  
  .form-grid {
    grid-template-columns: 1fr;
  }
  
  .form-actions {
    flex-direction: column;
  }
}
```

## 🎯 KEY TAKEAWAYS

### 1. CONSISTENT STRUCTURE
- Every module follows the same folder structure
- Layout components are module-specific
- Services and models are properly organized

### 2. UI PLATFORM INTEGRATION
- All UI elements use UI Platform components
- Design tokens are used consistently
- Responsive design is built-in

### 3. BEST PRACTICES
- Proper error handling
- Loading states
- Accessibility features
- Performance optimization

### 4. MAINTAINABILITY
- Clear separation of concerns
- Reusable components
- Proper dependency injection
- Comprehensive styling

---
**This template can be copied and adapted for any new module (Loyalty, Accounting, Warehouse, etc.)**
