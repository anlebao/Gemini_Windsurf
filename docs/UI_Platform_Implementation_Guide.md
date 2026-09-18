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

## 📒 HKD BOOK ACCOUNTING MODULE (Wave 8 — TT 152/2025/TT-BTC)

Reference implementation of a TT 152-compliant accounting report module in ShopERP.

### Pages

| Route | File | Purpose |
|-------|------|---------|
| `/accounting/hkd-books` | `5_WebApps/ShopERP/Components/Pages/Accounting/HKDBooks.razor` | List of 7 HKD book templates (S1a, S2a-S2e, S3a) |
| `/accounting/hkd-books/{templateCode}` | `HKDBookDetail.razor` | Detail view + DOCX/XLSX export buttons |

### Services

| Service | File | Purpose |
|---------|------|---------|
| `IHKDBookGenerationService` | `3_CoreHub/Services/Template/HKDBookGenerationService.cs` | Generates `GenericHKDBook` via `TemplateFactory` + formula engine. Depends on `IVanAnDbContext` (not concrete `VanAnDbContext`) so it can be injected directly into ShopERP. |
| `HKDBookExportService` | `5_WebApps/ShopERP/Services/HKDBookExportService.cs` | DOCX export via `DocumentFormat.OpenXml`, XLSX export via `EPPlus`. Returns `(byte[], contentType, fileName)`. |

### DI Wiring (ShopERP `Program.cs`)

```csharp
builder.Services.AddScoped<IVanAnDbContext, VanAnDbContext>();
builder.Services.AddScoped<IHKDBookGenerationService, HKDBookGenerationService>();
builder.Services.AddScoped<HKDBookExportService>();
// + TemplateFactory, TemplateCalculationEngine, SmartPreAggregationService, etc.
```

### UI Platform Components Used

- `VanACard` — page sections (header, table, footer)
- `VanAButton` — export DOCX / XLSX actions
- `VanAAlert` — empty/error state
- Scoped CSS via `HKDBookDetail.razor.css` (design tokens only, no custom HTML)

### Regression Prevention

| Mechanism | File | Guards Against |
|-----------|------|----------------|
| Architecture test (SC6) | `6_Tests/VanAn.Architecture.Tests/HKDBookTemplateArchitectureTests.cs` | Issue 1: `HKDBookTemplate` subclass with no-op `CalculateAsync` → `NumericValues` empty |
| Encoding lint (SC7) | `scripts/check-encoding.ps1` | Issue 7: UTF-8 mojibake in `.cs`/`.razor` files |
| E2E test | `6_Testing/e2e-tests/hkd-books.spec.ts` | UI regression: list/detail/export buttons render |

### Export Library Dependencies

| Library | Version | Purpose | Package |
|---------|---------|---------|---------|
| `EPPlus` | 7.6.1 | XLSX export | `Directory.Packages.props` |
| `DocumentFormat.OpenXml` | 3.0.1 | DOCX export | `Directory.Packages.props` |

---

## 💬 REALTIME PLATFORM (P4-P6 — 2026-09-18)

Chat 2 chiều realtime + live location **tái sử dụng** — gắn vào module mới KHÔNG viết component/JS mới.
Task card: `docs/AI/tasks/realtime_platform/task_card_01_realtime_chat_gps_reusable.md`.

### Quick start (5 bước — consumer mới)

```csharp
// 1. Host registration (KhachLink/ShopERP/Directory đều đã gọi rồi)
builder.Services.AddRealtimePlatform();   // endpoint provider + adapters + named HttpClient

// 2. Server: implement authorizer cho subject type của module
public class TicketRealtimeAuthorizer(...) : IRealtimeParticipantAuthorizer
{
    // CanAccessAsync(type, subjectId, userId, tenantId, ct):
    //   - Customer/guest: thành viên conversation (initiator hoặc participant row)
    //   - Staff: tenant của caller (JWT tenant_id claim) == subjectId nếu subject là tenant
}
// DI (Gateway Program.cs, keyed theo subject):
builder.Services.AddKeyedScoped<IRealtimeParticipantAuthorizer, TicketRealtimeAuthorizer>(RealtimeSubjectType.Ticket);

// 3. Tạo conversation ở domain event (idempotent)
await _messaging.EnsureConversationAsync(tenantId, RealtimeSubjectType.Ticket, ticket.Id,
    customerId, staffId, RealtimeParticipantRole.Customer, RealtimeParticipantRole.Staff);

// 4. Ghi vị trí khi cần (trackerId server-stamp — client không gửi được)
await _liveLocation.RecordPingAsync(tenantId, RealtimeSubjectType.Ticket, ticket.Id, null, lat, lng);
```

```razor
@* 5. UI — dùng component UI Platform, KHÔNG tự viết chat/map *@
<RealtimeChatPanel SubjectType="Ticket"          @* "Order" | "Shop" | ... (enum name) *@
                   SubjectId="ticket.Id"
                   CurrentUserId="_userId"        @* customer id / guest device id / staff id *@
                   CustomerToken="_customerToken" @* HOẶC *@
                   CustomerDeviceId="_deviceId"   @* HOẶC *@
                   StaffToken="_staffJwt" />      @* staff (ShopERP mint Owner JWT) *@

<VanAnMap MapElementId="ticket-map" Height="350px" ShopLat="..." ShopLng="..." />  @* map tĩnh *@
```

### Checklist module mới
- [ ] `AddRealtimePlatform()` đã gọi trong host Program.cs
- [ ] Authorizer implemented + keyed DI đăng ký (subject chưa đăng ký → **default deny**)
- [ ] `RealtimeSubjectType` tái dùng (thêm enum value cần Domain approval)
- [ ] Conversation ensure gọi ở đúng domain event (idempotent — unique index `(TenantId, SubjectType, SubjectId)`)
- [ ] Mọi query cross-tenant có `IgnoreQueryFilters()` + comment lý do
- [ ] UI dùng `RealtimeChatPanel`/`VanAnMap` — không bypass UI Platform (Gate)
- [ ] Tests: 2 user khác tenant không thấy conversation của nhau

### Identity (server tự resolve — client chỉ gửi credential)
| Credential | Vị trí | Identity |
|---|---|---|
| `X-Customer-Token` / `customerToken` (query) | customer login | CustomerId |
| `X-Customer-Device-Id` / `customerDeviceId` (query) | guest (localStorage `customer_device_id`) | device guid (chính là ParticipantId/SenderId/TrackerId) |
| `Authorization: Bearer` / `access_token` (query) | staff JWT (ShopERP mint, có `tenant_id` claim) | staff userId + TenantId |

### Endpoints (Gateway `/api/realtime/*`)
- `GET  /conversations/{subjectType}/{subjectId}` — history (Shop: visitor tự tạo conversation create-only; Order: legacy ensure)
- `POST /conversations/messages` — gửi tin (server push `ReceiveMessage` vào group `msg_{type}_{id}`)
- `GET  /shop/conversations` — inbox chủ shop (staff-only, last-message preview)
- `POST /location/ping` — ghi toạ độ (reject (0,0)) · `GET /location/{type}/{id}/latest` — ping mới nhất
- Hubs: `/hubs/messaging` (group `msg_{type}_{id}`) · `/hubs/tracking` (group `loc_{type}_{id}`)

### Static assets (F4)
Leaflet + `realtime.js` served tại `_content/VanAn.UI.Platform/...` trên MỌI host đã reference RCL —
không copy JS vào app. Chỉ cần thêm vào layout: `lib/leaflet/leaflet.{css,js}` + `js/realtime.js`.

### E2E
- `6_Testing/e2e-tests/realtime-shop-chat.spec.ts` — Shop chat (API auth + guest create/send + store page UI)
- `6_Testing/e2e-tests/realtime-tracking.spec.ts` — generic location surface (auth + validation + migrated page)

---
**Created**: 3/5/2026
**Next Review**: 3/6/2026
**Owner**: UI Platform Team
**Wave 8 Update**: 2026-07-04 — HKD Book accounting module added
**Realtime Update**: 2026-09-18 — Realtime Platform P4-P6 (chat + live location reusable, reuse recipe 5 bước)
