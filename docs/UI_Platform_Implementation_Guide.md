# UI PLATFORM IMPLEMENTATION GUIDE
## Frontend Layout Template cho tất cả modules

## 🎯 MỤC TIÊU

UI Platform sẽ trở thành **template chuẩn** cho frontend của tất cả modules:
- HR (Nhân sự)
- Loyalty (Khách hàng thân thiết) 
- Accounting (Kế toán)
- Warehouse (Kho hàng)

## 🚀 QUICK START CHO MODULE MỚI

### BƯỚC 1: ADD PROJECT REFERENCE
```xml
<!-- Trong module .csproj -->
<ItemGroup>
  <ProjectReference Include="..\..\UI.Platform\VanAn.UI.Platform.csproj" />
</ItemGroup>
```

### BƯỚC 2: CREATE MODULE LAYOUT
```razor
@* Modules/HR/Shared/HRLayout.razor *@
@inject IThemeProvider ThemeProvider
@inject ITenantService TenantService

<div class="hr-layout @ThemeProvider.CurrentTheme">
  <VanANavigation MenuItems="@HRMenuItems" />
  
  <main class="hr-main">
    <VanABreadcrumb Items="@breadcrumbs" />
    @Body
  </main>
  
  <VanAFooter />
</div>

@code {
  private List<MenuItem> HRMenuItems = new()
  {
    new() { Text = "Dashboard", Icon = "dashboard", Href = "/hr" },
    new() { Text = "Employees", Icon = "people", Href = "/hr/employees" },
    new() { Text = "Schedule", Icon = "calendar", Href = "/hr/schedule" }
  };
}
```

### BƯỚC 3: CREATE MODULE PAGE
```razor
@* Modules/HR/Pages/EmployeeList.razor *@
@page "/hr/employees"
@layout HRLayout

<VanACard Header="Employee Management">
  <VanATable 
    Data="@employees"
    Columns="@employeeColumns"
    Filterable="true"
    Sortable="true" />
</VanACard>

@code {
  private List<Employee> employees = new();
  private List<TableColumn> employeeColumns = new()
  {
    new() { Header = "ID", Property = "Id", Width = 1 },
    new() { Header = "Name", Property = "FullName", Width = 4 },
    new() { Header = "Department", Property = "Department", Width = 3 },
    new() { Header = "Actions", Template = CreateActionTemplate(), Width = 2 }
  };

  protected override async Task OnInitializedAsync()
  {
    employees = await EmployeeService.GetAllAsync();
  }

  private RenderFragment CreateActionTemplate() => __builder =>
  {
    <VanAButton Size="small" Variant="primary">Edit</VanAButton>
    <VanAButton Size="small" Variant="danger">Delete</VanAButton>
  };
}
```

## 🏗️ COMPONENT HIERARCHY

### BASE COMPONENTS (Luôn dùng)
```razor
<!-- Buttons -->
<VanAButton Variant="primary" Size="medium" OnClick="@Save">Save</VanAButton>
<VanAButton Variant="secondary" Size="small" OnClick="@Cancel">Cancel</VanAButton>

<!-- Cards -->
<VanACard Header="Card Title">
  <p>Card content here</p>
</VanACard>

<!-- Forms -->
<VanAForm Fields="@formFields" OnSubmit="@HandleSubmit" />

<!-- Alerts -->
<VanAAlert Type="success" Message="Operation completed successfully!" />
<VanAAlert Type="error" Message="An error occurred!" Dismissible="true" />
```

### COMPOSITE COMPONENTS (Phức hợp)
```razor
<!-- Data Tables -->
<VanATable 
  Data="@data"
  Columns="@columns"
  PageSize="10"
  Filterable="true"
  Sortable="true"
  OnRowClick="@HandleRowClick" />

<!-- Charts -->
<VanAChart Type="bar" Data="@chartData" Options="@chartOptions" />

<!-- Layouts -->
<VanALayout Type="sidebar" Sidebar="@sidebarContent">
  <main>@mainContent</main>
</VanALayout>
```

## 🎨 DESIGN TOKENS USAGE

### COLORS
```css
.custom-component {
  background-color: var(--color-primary-500);
  color: var(--color-neutral-50);
  border: 1px solid var(--color-neutral-200);
}
```

### SPACING
```css
.form-group {
  margin-bottom: var(--spacing-4);
  padding: var(--spacing-2);
}
```

### TYPOGRAPHY
```css
.page-title {
  font-size: var(--font-size-xl);
  font-weight: var(--font-weight-semibold);
}
```

## 📱 RESPONSIVE PATTERNS

### MOBILE FIRST
```razor
<div class="employee-grid">
  @foreach (var employee in employees)
  {
    <div class="employee-card">
      <VanACard Header="@employee.FullName">
        <div class="employee-info">
          <span class="department">@employee.Department</span>
          <VanAButton Size="small" OnClick="@() => EditEmployee(employee)">Edit</VanAButton>
        </div>
      </VanACard>
    </div>
  }
</div>

<style>
.employee-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: var(--spacing-4);
}

@media (min-width: 768px) {
  .employee-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (min-width: 1024px) {
  .employee-grid {
    grid-template-columns: repeat(3, 1fr);
  }
}
</style>
```

## 🔧 MODULE CUSTOMIZATION

### CUSTOM COLORS
```css
/* Modules/HR/Styles/hr-module.css */
.hr-layout {
  --color-module-primary: #2563eb;
  --color-module-secondary: #64748b;
}

.hr-layout .vanan-button--primary {
  background-color: var(--color-module-primary);
}
```

### CUSTOM COMPONENTS
```razor
@* Modules/HR/Components/EmployeeCard.razor *@
<VanACard CssClass="employee-card @GetStatusClass()">
  <Header>
    <div class="employee-header">
      <h3>@Employee.FullName</h3>
      <VanABadge Type="@GetStatusBadgeType()">@Employee.Status</VanABadge>
    </div>
  </Header>
  
  <Body>
    <div class="employee-details">
      <p><strong>Department:</strong> @Employee.Department</p>
      <p><strong>Email:</strong> @Employee.Email</p>
      <p><strong>Phone:</strong> @Employee.Phone</p>
    </div>
  </Body>
  
  <Footer>
    <div class="employee-actions">
      <VanAButton Size="small" OnClick="@OnEdit">Edit</VanAButton>
      <VanAButton Size="small" Variant="danger" OnClick="@OnDelete">Delete</VanAButton>
    </div>
  </Footer>
</VanACard>

@code {
  [Parameter]
  public Employee Employee { get; set; } = new();
  
  [Parameter]
  public EventCallback<Employee> OnEdit { get; set; }
  
  [Parameter]
  public EventCallback<Employee> OnDelete { get; set; }
  
  private string GetStatusClass()
  {
    return Employee.Status switch
    {
      EmployeeStatus.Active => "employee-active",
      EmployeeStatus.Inactive => "employee-inactive",
      _ => "employee-unknown"
    };
  }
  
  private string GetStatusBadgeType()
  {
    return Employee.Status switch
    {
      EmployeeStatus.Active => "success",
      EmployeeStatus.Inactive => "warning",
      _ => "neutral"
    };
  }
}
```

## 🎯 BEST PRACTICES CHECKLIST

### ✅ LUÔN LÀM
- [ ] Sử dụng UI Platform components thay vì custom HTML
- [ ] Follow design tokens cho colors, spacing, typography
- [ ] Implement responsive design mobile-first
- [ ] Add proper accessibility attributes
- [ ] Use semantic HTML structure
- [ ] Implement proper error handling

### ❌ TRÁNH
- [ ] Custom CSS khi có component sẵn
- [ ] Hardcoded values cho colors/spacing
- [ ] Skip accessibility testing
- [ ] Ignore mobile responsiveness
- [ ] Create duplicate components

## 📋 MODULE STRUCTURE TEMPLATE

```
Modules/
├── HR/
│   ├── Components/
│   │   ├── EmployeeCard.razor
│   │   ├── ScheduleTable.razor
│   │   └── PayrollChart.razor
│   ├── Pages/
│   │   ├── Index.razor (Dashboard)
│   │   ├── Employees.razor
│   │   ├── Schedule.razor
│   │   └── Payroll.razor
│   ├── Shared/
│   │   └── HRLayout.razor
│   ├── Services/
│   │   └── HREmployeeService.cs
│   └── Styles/
│       └── hr-module.css
├── Loyalty/
├── Accounting/
└── Warehouse/
```

## 🔄 CONTINUOUS IMPROVEMENT

### MONTHLY REVIEWS
1. **Component Usage Analytics**
   - Track which components are used most
   - Identify unused components
   - Find opportunities for new components

2. **Performance Monitoring**
   - Page load times
   - Component render times
   - Memory usage

3. **User Feedback**
   - Developer satisfaction surveys
   - User experience feedback
   - Accessibility testing results

### QUARTERLY UPDATES
1. **New Component Additions**
   - Based on usage patterns
   - Industry best practices
   - User requests

2. **Design Token Updates**
   - Color palette refinements
   - Spacing adjustments
   - Typography improvements

3. **Performance Optimizations**
   - Component lazy loading
   - Bundle size reduction
   - Render optimization

---
**Created**: 3/5/2026
**Next Review**: 3/6/2026
**Owner**: UI Platform Team
