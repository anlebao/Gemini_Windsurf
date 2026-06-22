# Coding Standards

> **Reference cho `.editorconfig`, `Directory.Build.props`, `.windsurfrules`, governance.md**
> Đây là bản tổng hợp. Source of truth vẫn là các file config gốc.

## C# Code Style

### Analyzer Configuration (`.editorconfig`)

| Setting | Severity | Ghi chú |
|---|---|---|
| `dotnet_analyzer_diagnostic.severity` | warning | Default |
| `CA1707` (no underscores in names) | none | Suppressed — allowed in tests |
| `CA1848` (virtual logger members) | none | Suppressed |
| `CA1716` (no reserved names) | none | Suppressed — low risk |
| `CA1805` (unnecessary default init) | none | Suppressed — low risk |
| `CA1069` (enum zero value) | none | Suppressed |
| `CA1008` (enum zero named "None") | none | Suppressed |
| `CA1031` (catch all) | none | Suppressed |
| `CA1002` (expose List<T>) | warning | |
| `CA2227` (collection setters) | warning | |
| `CA2007` (await ConfigureAwait) | suggestion | |
| `IDE0300/IDE0028/IDE0305` (collection expression) | none | Disabled — causes CS0173 on Linux CI (netstandard2.0) |
| `csharp_style_prefer_collection_expression` | false:none | Disabled for CI compatibility |

### Custom Roslyn Analyzers (errors)

| Analyzer | Severity | Meaning |
|---|---|---|
| `VA1001` | error | Domain layer purity violation |
| `VA1002` | error | Multi-tenancy violation |
| `VA1003` | error | AccountingEntry immutability violation |
| `VA1004` | error | Clean Architecture dependency direction |
| `VA1005` | error | UI Platform bypass |

Configured via `<WarningsAsErrors>VA1001;VA1002;VA1003;VA1004;VA1005</WarningsAsErrors>` in `Directory.Build.props`.

### Build Settings (`Directory.Build.props`)

| Setting | Value |
|---|---|
| `ManagePackageVersionsCentrally` | true |
| `CentralPackageTransitivePinningEnabled` | true |
| `GenerateAssemblyInfo` | true |
| `TreatWarningsAsErrors` | false |
| `AnalysisMode` | AllEnabledByDefault |
| `AssemblyVersion` | 1.0.0.0 |
| `MauiVersion` | 8.0.14 |

## Domain Layer Conventions

### Entity Pattern
```csharp
public class Order : BaseEntity, IMustHaveTenant
{
    // Protected setters only — enforce invariants
    public OrderId OrderId { get; protected set; }
    public OrderStatusId Status { get; protected set; }

    // Business methods change state + call UpdateAudit()
    public void UpdateStatus(OrderStatusId status)
    {
        Status = status;
        UpdateAudit();
    }
}
```

### Factory Method Pattern
```csharp
public static Order Create(Guid id, TenantId tenantId, List<OrderItem> items)
{
    // Validation
    // Create instance
    // Return
}
```

### Value Object Pattern
```csharp
public record TenantId(Guid Value)
{
    public static implicit operator Guid(TenantId tenantId) => tenantId.Value;
    public static implicit operator TenantId(Guid value) => new(value);
}
```

### Forbidden in Domain Layer
- EF Core (`Microsoft.EntityFrameworkCore.*`)
- `DbContext`
- DataAnnotations (`[Required]`, `[MaxLength]`, etc.)
- Business logic in controllers/gateway/hubs

## UI Platform Conventions (ADR-004)

### Component Hierarchy
```
Layer 1: Base (VanAnButton, VanAnCard, VanAnAlert, VanAnInput, VanAModal, VanASpinner)
Layer 2: Composite (VanAForm, VanATable, VanAChart, VanALayout, VanANavigation)
Layer 3: Module-Specific (EmployeeForm, CustomerCard, JournalEntry, etc.)
```

### Component Parameters (Pattern 9)
```razor
<!-- WRONG -->
<VanAnButton Variant="outline" Size="small" />
<VanAnAlert Variant="info" />

<!-- CORRECT -->
<VanAnButton Variant="ButtonVariant.Outline" Size="ButtonSize.Small" />
<VanAnAlert Variant="AlertVariant.Info" />
```

### Navigation (Pattern 10)
```razor
<!-- WRONG -->
@inject NavigationManager NavigationManager

<!-- CORRECT -->
@inherits VanAn.UI.Platform.Components.Base.BaseComponent
<!-- Navigation available through inheritance -->
```

### TagHelper Syntax (Pattern 11)
```razor
<!-- WRONG -->
<VanAnButton OnClick="() => Navigation.NavigateTo('/cart')" />

<!-- CORRECT -->
<VanAnButton OnClick="@(() => Navigation.NavigateTo('/cart'))" />
```

### Theme & Responsive
- Inject `IThemeProvider` + `ITenantService` for theming
- Mobile-first: Mobile (≤640px), Tablet (641-1024px), Desktop (≥1025px)
- CSS Grid + Flexbox for layout
- Use design tokens, NOT hardcoded values

## Testing Conventions

### Framework
- xUnit 2.9.0 (unit + architecture tests)
- FluentAssertions 6.12.0 (assertions)
- NetArchTest 1.3.2 (architecture tests)
- Playwright 1.50.0 (E2E)
- bunit 1.28.9 (Blazor component tests)

### Test Entity Builder (Pattern 1)
```csharp
// WRONG — direct entity creation
new Customer { FullName = "...", PhoneNumber = "..." }

// CORRECT — use builder
TestEntityBuilder.CreateCustomer(tenantId, "...", "...")
```

### Test Commands
```bash
dotnet build VanAn.sln
dotnet test 6_Tests/VanAn.Core.Tests/
dotnet test 6_Tests/VanAn.Architecture.Tests/
./guard-check.ps1
```

## Package Management

- Central Package Management enabled
- Two source files: `Directory.Build.props` + `Directory.Packages.props`
- NEVER declare `<PackageReference Version="...">` in `.csproj` (use `Version=""` to inherit from central)
- Exception: MAUI apps use `$(MauiVersion)` MSBuild property

## Git Conventions

### Branch Naming
- `feat/<scope>-<desc>` — new feature
- `fix/<scope>-<desc>` — bug fix
- `align-<phase>` — alignment/migration work

### Commit Prefixes (from git log)
- `fix(<scope>):` — bug fix
- `feat(<scope>):` — new feature
- `docs:` — documentation
- `refactor:` — code refactor

## Validation Requirements

Before any submission:
1. `guard-check.ps1` MUST PASS
2. `dotnet build VanAn.sln` MUST PASS
3. Architecture tests MUST PASS (`dotnet test 6_Tests/VanAn.Architecture.Tests/`)
4. No new analyzer warnings (VA1001-VA1005)

## Pattern Repository

Error patterns documented in: `docs/Implement/QuyTrinh/RULE_6_1_FullErrorInvestigation_Protocol.md`

Current state: 31 patterns (P1-P32, no P19), with:
- P3: OBSOLETE (2026-06-18)
- P6: MISLEADING (2026-06-18)
- P32: NEW — Sync-over-Async anti-pattern

---

*Document Status: Active*
*Last Updated: 2026-06-18*
*Source: .editorconfig, Directory.Build.props, Directory.Packages.props, .windsurfrules, governance.md, RULE_6_1_FullErrorInvestigation_Protocol.md*
