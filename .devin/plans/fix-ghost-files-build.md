# PLAN: Fix Ghost Files — Full Solution Release Build

**Branch:** `align-consumer-phase4`  
**Goal:** Đưa `dotnet build VanAn.sln --configuration Release` về 0 errors  
**Status:** IN PROGRESS  
**Created:** 2026-06-14  

---

## ROOT CAUSE (đã xác nhận qua investigation)

**Pattern:** AI agents (Windsurf, Devin) tạo Controllers/Services/Pages tham chiếu tới
class chưa tồn tại, commit file tham chiếu, nhưng KHÔNG commit file được tham chiếu.

**Evidence:**
- Commit `e4904a9` tạo `HKDElectronicInvoiceController.cs` — không có `DTOs/` đi kèm
- Commit `fb5bb85` sửa `Home.razor`, `CartService.cs` — không có `ProductDto.cs`
- `.gitignore` không chặn DTOs — xác nhận 100% là Ghost File, không phải gitignore trap
- `dotnet build Debug` local có thể pass vì partial compilation, CI Release quét toàn solution

---

## 7-STEP EXECUTION PLAN

### STEP 1 — `2_Gateway/DTOs/InvoiceItemDto.cs`
**Why:** `HKDElectronicInvoiceController.cs` dùng `InvoiceItemDto` ở 2 chỗ:
- Line 99-107: positional constructor `(ItemCode, ItemName, Unit, Quantity, UnitPrice, VatRate, Amount, VatAmount)`
- Line 174: `List<InvoiceItemDto>? Items` trong `CreateInvoiceRequest`

**Contract chính xác (từ line 99-107):**
```csharp
namespace VanAn.Gateway.DTOs;

/// <summary>
/// Line item DTO for e-invoice request and response.
/// Positional order matches InvoiceItem domain entity mapping in HKDElectronicInvoiceController.
/// </summary>
public record InvoiceItemDto(
    string ItemCode,
    string ItemName,
    string? Unit,
    decimal Quantity,
    decimal UnitPrice,
    decimal VatRate,
    decimal Amount,
    decimal VatAmount
);
```

**Also needed in same file or separate:** `InvoiceDto`, `InvoiceStatusDto` — check controller for usage.

---

### STEP 2 — `2_Gateway/Services/IMstLookupService.cs` + `MstLookupService.cs`
**Why:** `2_Gateway/Program.cs` line 56:
```csharp
builder.Services.AddScoped<IMstLookupService, MstLookupService>();
// Uses "VietQR" HttpClient: BaseAddress = https://api.vietqr.io/v2/
// Comment: "Business Lookup Proxy for KhachLink"
```

**IMstLookupService contract:**
```csharp
namespace VanAn.Gateway.Services;

public interface IMstLookupService
{
    /// <summary>Lookup business info by MST (tax code) via VietQR API.</summary>
    Task<BusinessLookupResult?> LookupByTaxCodeAsync(string taxCode, CancellationToken ct = default);
}

public record BusinessLookupResult(
    string TaxCode,
    string BusinessName,
    string? Address,
    string? Status
);
```

**MstLookupService — stub with mock data (safe for CI):**
```csharp
namespace VanAn.Gateway.Services;

public class MstLookupService(
    IHttpClientFactory httpClientFactory,
    ILogger<MstLookupService> logger) : IMstLookupService
{
    public async Task<BusinessLookupResult?> LookupByTaxCodeAsync(string taxCode, CancellationToken ct = default)
    {
        // TODO: Sprint 4 - Implement actual VietQR API call
        // GET https://api.vietqr.io/v2/business/{taxCode}
        logger.LogInformation("MstLookupService: lookup taxCode={TaxCode} (stub)", taxCode);
        await Task.CompletedTask;
        return new BusinessLookupResult(taxCode, "Chưa tra cứu", null, "stub");
    }
}
```

---

### STEP 3 — `5_WebApps/KhachLink/Models/ProductDto.cs`
**Why:** `Home.razor` line 123, 138, 152 + `CartService.cs` line 56 + `CartState.cs` line 13

**Contract chính xác (từ inline initialization Home.razor line 140):**
```
ProductId, Name, Description, Price, Category, IsActive, VatRate
```

```csharp
namespace VanAn.KhachLink.Models;

public class ProductDto
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? Category { get; set; }
    public bool IsActive { get; set; }
    public decimal VatRate { get; set; }
}
```

---

### STEP 4 — `5_WebApps/ShopERP/Infrastructure/ValueConverters/InvoiceItemIdConverter.cs`
**Why:** `ShopERPDbContext.cs` line 97-98:
```csharp
configurationBuilder.Properties<InvoiceItemId>()
    .HaveConversion<InvoiceItemIdConverter>();
```

**Pattern từ `ElectronicInvoiceIdConverter` (đã tồn tại — dùng làm template chính xác):**
```csharp
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Infrastructure;

/// <summary>
/// EF Core ValueConverter: InvoiceItemId (strong-typed) ↔ Guid (DB column).
/// </summary>
public class InvoiceItemIdConverter()
    : ValueConverter<InvoiceItemId, Guid>(
        id => id.Value,
        value => new InvoiceItemId(value));
```

---

### STEP 5 — Namespace alignment check (`_Imports.razor`)
**Why:** Blazor pages cần `@using` để resolve `ProductDto` không cần full-qualify.

**Action:** Kiểm tra `5_WebApps/KhachLink/_Imports.razor` — thêm nếu thiếu:
```razor
@using VanAn.KhachLink.Models
```

---

### STEP 6 — Build Release full solution
```powershell
dotnet build VanAn.sln --configuration Release --no-restore 2>&1 |
    Select-String "error CS" | Select-String -NotMatch "warning"
# Expected: no output
```

Nếu còn lỗi: investigate và fix trước khi commit.

---

### STEP 7 — Commit + Push
**Scope:** 1 commit duy nhất gom toàn bộ fixes từ session này.

```
fix(ci): resolve all pre-existing ghost file build errors blocking Release build

Root cause: AI agents created controller/service/page references without
creating the referenced DTOs, Services, and Converters (ghost files).
Build Debug local masked the issue; CI Release exposed all missing types.

Changes in this commit:
- 3_CoreHub/Infrastructure/VanAnDbContext.cs
  * Fix: BaseVanAnDbContext -> DbContext (phantom base class)
  * Fix: type-safe null-guarded ApplyMultiTenancyFilters (EF.Property)
- 3_CoreHub/Program.cs: remove dead using, restore VanAn.CoreHub.Agents
- 3_CoreHub/Agents/FeatureDeveloperExecutor.cs (stub, Phase 6)
- 3_CoreHub/Agents/BuildFixerExecutor.cs (stub, Phase 6)
- 3_CoreHub/Services/BatchInvoiceProcessor.cs (stub, Sprint 4)
- 3_CoreHub/Services/Orchestration/WebhookDtos.cs (Viettel + MISA)
- 3_CoreHub/Infrastructure/ProjectMemory/CleanupResult.cs
- 3_CoreHub/Infrastructure/ProjectMemory/ProjectMemoryHealthCheck.cs
- 3_CoreHub/Infrastructure/ProjectMemory/ProjectMemoryCleanupOptions.cs
- 3_CoreHub/Infrastructure/ProjectMemory/ProjectMemoryCleanupService.cs
- 2_Gateway/DTOs/InvoiceItemDto.cs
- 2_Gateway/Services/IMstLookupService.cs
- 2_Gateway/Services/MstLookupService.cs
- 5_WebApps/KhachLink/Models/ProductDto.cs
- 5_WebApps/ShopERP/Infrastructure/ValueConverters/InvoiceItemIdConverter.cs
- VanAn.Accounting/VanAn.Accounting.Analyzers/AnalyzerReleases.Unshipped.md
- .devin/skills/ci-build-debug/ (reusable CI debug skill)
- .devin/plans/fix-ghost-files-build.md (this file)
```

---

## RISK REGISTER

| Step | Rủi ro | Mitigation |
|------|--------|-----------|
| STEP 1 | `InvoiceDto`/`InvoiceStatusDto` cũng missing | Kiểm tra controller lần 2 trước khi tạo |
| STEP 2 | `MstLookupService` có thể được dùng bởi Controllers | Tìm tất cả usages trước khi finalize interface |
| STEP 4 | EF ValueConverter type mismatch runtime | Copy exact pattern từ `ElectronicInvoiceIdConverter` |
| STEP 5 | `_Imports.razor` không tồn tại | Tạo mới nếu cần |
| STEP 6 | Còn lỗi mới phát sinh | Xử lý tại chỗ trước khi commit |

---

## SESSION STATE SNAPSHOT

### Uncommitted changes (đã làm trong session):
| File | Status |
|------|--------|
| `3_CoreHub/Infrastructure/VanAnDbContext.cs` | Modified — fixed |
| `3_CoreHub/Program.cs` | Modified — fixed |
| `3_CoreHub/Infrastructure/ProjectMemory/IProjectMemoryService.cs` | Modified — cleaned |
| `3_CoreHub/Infrastructure/ProjectMemory/ProjectMemoryService.cs` | Modified — cleaned |
| `3_CoreHub/Agents/FeatureDeveloperExecutor.cs` | New stub |
| `3_CoreHub/Agents/BuildFixerExecutor.cs` | New stub |
| `3_CoreHub/Services/BatchInvoiceProcessor.cs` | New stub |
| `3_CoreHub/Services/Orchestration/WebhookDtos.cs` | New stub |
| `3_CoreHub/Infrastructure/ProjectMemory/CleanupResult.cs` | New |
| `3_CoreHub/Infrastructure/ProjectMemory/ProjectMemoryHealthCheck.cs` | New stub |
| `3_CoreHub/Infrastructure/ProjectMemory/ProjectMemoryCleanupOptions.cs` | New stub |
| `3_CoreHub/Infrastructure/ProjectMemory/ProjectMemoryCleanupService.cs` | New stub |
| `VanAn.Accounting/VanAn.Accounting.Analyzers/AnalyzerReleases.Unshipped.md` | New |
| `.devin/skills/ci-build-debug/` | New skill |

### Still needed (Steps 1-5 above):
- `2_Gateway/DTOs/InvoiceItemDto.cs`
- `2_Gateway/Services/IMstLookupService.cs`
- `2_Gateway/Services/MstLookupService.cs`
- `5_WebApps/KhachLink/Models/ProductDto.cs`
- `5_WebApps/ShopERP/Infrastructure/ValueConverters/InvoiceItemIdConverter.cs`
