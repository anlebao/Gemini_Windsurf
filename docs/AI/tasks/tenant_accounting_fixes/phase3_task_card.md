# TASK CARD — Phase 3: Bug 1 — SystemAdmin Edit Tenant BusinessType

> **Status:** 🟡 PLANNED — Domain modification APPROVED 2026-08-03
> **Prerequisite:** Domain approval (✅ APPROVED — guard: block nếu tenant đã có AccountingEntry)
> **Branch:** `feature/tenant-fix-phase3-edit-businesstype`
> **Estimated sessions:** 2 (1 Domain+Service+API, 1 UI+tests)
> **Mode:** IMPLEMENT
> **Domain modification:** ✅ YES (D1 approved — add `Tenant.ChangeBusinessType()` method)

## Objective
Cho phép SystemAdmin sửa loại hình (Công ty ↔ Hộ kinh doanh) cho tenant từ Edit Modal trong `/admin/tenants`.

**Business rules (APPROVED):**
- ✅ Cho phép đổi BusinessType nếu tenant CHƯA có AccountingEntry nào (data integrity — HKD và DN dùng chuẩn kế toán khác nhau)
- ❌ Block đổi BusinessType nếu tenant ĐÃ có AccountingEntry (throw `InvalidOperationException`)
- ❌ Block đổi nếu tenant Inactive hoặc Converted
- ❌ Block đổi sang HouseholdBusiness mà không có HKDGroup
- ❌ Block đổi sang Company mà có HKDGroup (must be null)
- ✅ Sync `TenantType` field cho feature flag routing (HKD → `TenantType.HKD`, Company → giữ `Type` hiện có hoặc set Enterprise_*)

## Prerequisites
- [ ] Phase 3 INVESTIGATE: verify `Tenant.cs` current state (no `ChangeBusinessType` method)
- [ ] Verify `AccountingEntry` query path (how to check if tenant has any entries)
- [ ] Verify `TenantManagementService.cs` current methods
- [ ] Verify `TenantsController.cs` current endpoints
- [ ] Verify `TenantApiClient.cs` current methods
- [ ] Verify `TenantManagement.razor` EditForm + HandleEditSubmit

## Files to Modify
| File | Changes |
|------|---------|
| `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs` | Add `ChangeBusinessType()` method + `TenantBusinessTypeChangedEvent` |
| `3_CoreHub/Services/TenantManagementService.cs` | Add `ChangeBusinessTypeAsync()` method (with AccountingEntry guard) |
| `3_CoreHub/Services/ITenantManagementService.cs` | Add interface method |
| `2_Gateway/Controllers/TenantsController.cs` | Add `[HttpPut("{tenantId:guid}/business-type")]` endpoint |
| `5_WebApps/ShopERP/Services/TenantApiClient.cs` | Add `ChangeBusinessTypeAsync()` client method |
| `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor` | Add BusinessType field to EditForm + UI + HandleEditSubmit logic |
| `6_Tests/VanAn.Core.Tests/Services/TenantChangeBusinessTypeTests.cs` (new) | Unit tests for domain method |
| `6_Tests/VanAn.Integration.Tests/TenantChangeBusinessTypeApiTests.cs` (new) | Integration tests for API endpoint |
| `6_Testing/e2e-tests/tenant-edit-businesstype.spec.ts` (new) | E2E test for UI flow |

## Detailed Task List

### P3-T1: Domain — Add `ChangeBusinessType()` method + event
**File:** `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs`

**Add after `SetTenantType()` method (line 232):**

```csharp
/// <summary>
/// Bug 1 fix (approved 2026-08-03): Change BusinessType for a tenant (SystemAdmin correction).
/// Use case: tenant created with wrong type, needs correction before any accounting data exists.
///
/// Guards:
/// - Cannot change if tenant is Inactive (archived).
/// - Cannot change if tenant is Converted (historical record, read-only).
/// - Cannot change to HouseholdBusiness without HKDGroup.
/// - Cannot change to Company with HKDGroup (must be null).
/// - Data integrity guard (enforced at service layer): block if tenant has ANY AccountingEntry.
///
/// Side effects:
/// - Syncs TenantType for feature flag routing: HKD → TenantType.HKD, Company → keep existing Type or null.
/// - Raises TenantBusinessTypeChangedEvent for audit trail.
/// </summary>
public void ChangeBusinessType(BusinessType newType, HKDGroup? hkdGroup, string reason)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(reason);
    if (Status == TenantStatus.Inactive)
        throw new InvalidOperationException("Cannot change business type of an inactive tenant.");
    if (Status == TenantStatus.Converted)
        throw new InvalidOperationException("Cannot change business type of a converted tenant.");
    if (newType == BusinessType.HouseholdBusiness && hkdGroup is null)
        throw new ArgumentException("HKDGroup is required when changing to HouseholdBusiness.", nameof(hkdGroup));
    if (newType == BusinessType.Company && hkdGroup is not null)
        throw new ArgumentException("HKDGroup must be null for Company tenants.", nameof(hkdGroup));

    BusinessType = newType;
    HKDGroup = hkdGroup;
    // Sync TenantType for feature flag routing (VasFeatureFlagService.CanAccessVasReportsAsync)
    if (newType == BusinessType.HouseholdBusiness)
    {
        Type = TenantType.HKD;
    }
    // For Company: keep existing Type (Enterprise_SME/Large/SuperSmall) or leave null
    // (SetTenantType can be called separately to classify Enterprise subtype)
    UpdateAudit();
    AddDomainEvent(new TenantBusinessTypeChangedEvent(Id.Value, newType, hkdGroup, reason, DateTime.UtcNow));
}
```

**Add Domain Event (in same file or `Events/` folder — check existing event pattern):**

```csharp
/// <summary>
/// Bug 1 fix: Raised when SystemAdmin changes tenant BusinessType (audit trail).
/// </summary>
public record TenantBusinessTypeChangedEvent(
    Guid TenantId,
    BusinessType NewBusinessType,
    HKDGroup? NewHkdGroup,
    string Reason,
    DateTime ChangedAt) : IDomainEvent;
```

**Note:** INVESTIGATE existing event pattern — `TenantCreatedEvent`, `TenantSuspendedEvent` etc. để confirm `IDomainEvent` interface + namespace.

### P3-T2: Service — Add `ChangeBusinessTypeAsync()` with AccountingEntry guard
**File:** `3_CoreHub/Services/ITenantManagementService.cs`

**Add to interface:**
```csharp
/// <summary>
/// Bug 1 fix: Change tenant BusinessType (SystemAdmin correction).
/// Guard: throws InvalidOperationException if tenant has ANY AccountingEntry.
/// </summary>
Task ChangeBusinessTypeAsync(TenantId tenantId, BusinessType newType, HKDGroup? hkdGroup, string reason, CancellationToken ct = default);
```

**File:** `3_CoreHub/Services/TenantManagementService.cs`

**Add implementation:**
```csharp
public async Task ChangeBusinessTypeAsync(TenantId tenantId, BusinessType newType, HKDGroup? hkdGroup, string reason, CancellationToken ct = default)
{
    // 1. Load tenant
    var tenant = await _dbContext.Tenants
        .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
        ?? throw new ArgumentException($"Tenant {tenantId.Value} not found.");

    // 2. DATA INTEGRITY GUARD (approved 2026-08-03): block if tenant has ANY AccountingEntry
    bool hasAccountingData = await _dbContext.AccountingEntries
        .AsNoTracking()
        .AnyAsync(e => e.TenantId == tenantId.Value, ct);
    if (hasAccountingData)
    {
        throw new InvalidOperationException(
            "Không thể đổi loại hình tenant: tenant đã có dữ liệu kế toán (AccountingEntry). " +
            "HKD và DN dùng chuẩn kế toán khác nhau (TT 152 vs TT 99/133/58). " +
            "Nếu cần chuyển đổi, hãy dùng tính năng HKD→DN Conversion (D9).");
    }

    // 3. Call domain method (validates status + HKDGroup consistency)
    tenant.ChangeBusinessType(newType, hkdGroup, reason);

    // 4. Save
    await _dbContext.SaveChangesAsync(ct);
    _logger.LogInformation("Tenant {TenantId} BusinessType changed to {NewType} (reason: {Reason})",
        tenantId.Value, newType, reason);
}
```

**INVESTIGATE before coding:**
- Verify `_dbContext.AccountingEntries` DbSet name (grep `AccountingEntries` in `IVanAnDbContext`)
- Verify `AccountingEntry.TenantId` property name + type (Guid, not TenantId VO — Pattern #8)
- Verify `_dbContext.Tenants` query pattern (existing methods use `t.Id == tenantId`)

### P3-T3: Gateway API — Add endpoint
**File:** `2_Gateway/Controllers/TenantsController.cs`

**Add after `UpdateProfile` endpoint (line 64-87):**

```csharp
/// <summary>
/// Bug 1 fix: Change tenant BusinessType (SystemAdmin correction).
/// Guard: returns 409 Conflict if tenant has accounting data.
/// </summary>
[HttpPut("{tenantId:guid}/business-type")]
public async Task<ActionResult> ChangeBusinessType(Guid tenantId, [FromBody] ChangeBusinessTypeApiRequest request, CancellationToken ct)
{
    try
    {
        await _tenantService.ChangeBusinessTypeAsync(
            new TenantId(tenantId),
            request.BusinessType,
            request.HkdGroup,
            request.Reason,
            ct);
        return NoContent();
    }
    catch (InvalidOperationException ex)
    {
        return Conflict(new { error = ex.Message });
    }
    catch (ArgumentException ex)
    {
        return BadRequest(new { error = ex.Message });
    }
}
```

**Add DTO (in same file or nearby DTOs file):**
```csharp
public sealed class ChangeBusinessTypeApiRequest
{
    public BusinessType BusinessType { get; init; }
    public HKDGroup? HkdGroup { get; init; }
    public string Reason { get; init; } = string.Empty;
}
```

### P3-T4: ShopERP TenantApiClient — Add client method
**File:** `5_WebApps/ShopERP/Services/TenantApiClient.cs`

**Add method:**
```csharp
/// <summary>
/// Bug 1 fix: Change tenant BusinessType via Gateway API.
/// </summary>
public async Task ChangeBusinessTypeAsync(Guid tenantId, BusinessType newType, HKDGroup? hkdGroup, string reason, CancellationToken ct = default)
{
    var request = new { BusinessType = newType, HkdGroup = hkdGroup, Reason = reason };
    var response = await _httpClient.PutAsJsonAsync($"/api/tenants/{tenantId}/business-type", request, ct);
    if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
    {
        var content = await response.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException($"Không thể đổi loại hình: {content}");
    }
    response.EnsureSuccessStatusCode();
}
```

### P3-T5: UI — Add BusinessType field to Edit Modal
**File:** `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor`

**Step 5a: Add fields to `EditForm` class (line 1034-1049):**

```csharp
private sealed class EditForm
{
    public string Name { get; set; } = string.Empty;
    public BusinessType BusinessType { get; set; }  // NEW
    public HKDGroup? HKDGroup { get; set; }  // NEW
    public string? ChangeReason { get; set; }  // NEW
    public string? ContactEmail { get; set; }
    // ... existing fields unchanged
}
```

**Step 5b: Populate in `OpenEditModal` (line 786-808):**

```csharp
private void OpenEditModal(TenantApiDto t)
{
    _selectedTenant = t;
    _editForm = new EditForm
    {
        Name = t.Name,
        BusinessType = t.BusinessType,  // NEW
        HKDGroup = t.HKDGroup,  // NEW (need to add HKDGroup to TenantApiDto if not present)
        ContactEmail = t.ContactEmail,
        // ... existing fields unchanged
    };
    _showEditModal = true;
    _mapInitialized = false;
}
```

**INVESTIGATE:** Verify `TenantApiDto` has `BusinessType` + `HKDGroup` fields (grep `TenantApiDto`).

**Step 5c: Add UI to Edit modal (after "Tên tenant" field, line 383):**

```razor
<div class="form-group">
    <label for="edit-businesstype">Loại hình *</label>
    <select id="edit-businesstype" class="vanan-select" @bind="_editForm.BusinessType" aria-label="Loại hình doanh nghiệp">
        <option value="@BusinessType.Company">Công ty</option>
        <option value="@BusinessType.HouseholdBusiness">Hộ kinh doanh</option>
    </select>
    @if (_editForm.BusinessType == BusinessType.HouseholdBusiness)
    {
        <label for="edit-hkdgroup" class="mt-2">Nhóm HKD *</label>
        <select id="edit-hkdgroup" class="vanan-select" @bind="_editForm.HKDGroup" aria-label="Nhóm hộ kinh doanh">
            <option value="@HKDGroup.Group1">Nhóm 1</option>
            <option value="@HKDGroup.Group2">Nhóm 2</option>
            <option value="@HKDGroup.Group3">Nhóm 3</option>
        </select>
    }
</div>
@if (_editForm.BusinessType != _selectedTenant?.BusinessType)
{
    <div class="form-group">
        <label for="edit-reason">Lý do đổi loại hình *</label>
        <input id="edit-reason" class="vanan-input" @bind="_editForm.ChangeReason"
               placeholder="vd: Tạo nhầm loại hình, cần sửa lại" required />
        <small class="text-muted">⚠️ Không thể đổi nếu tenant đã có dữ liệu kế toán (AccountingEntry).</small>
    </div>
}
```

**Step 5d: Add logic to `HandleEditSubmit` (line 895-951):**

After `UpdateProfileAsync` call (line 926), before slug update (line 928):

```csharp
// Bug 1 fix: Change BusinessType if changed (requires reason + no accounting data)
if (_editForm.BusinessType != _selectedTenant.BusinessType)
{
    if (string.IsNullOrWhiteSpace(_editForm.ChangeReason))
    {
        ShowAlert("error", "Vui lòng nhập lý do đổi loại hình.");
        return;
    }
    if (_editForm.BusinessType == BusinessType.HouseholdBusiness && !_editForm.HKDGroup.HasValue)
    {
        ShowAlert("error", "Vui lòng chọn Nhóm HKD khi đổi sang Hộ kinh doanh.");
        return;
    }
    try
    {
        await TenantApi.ChangeBusinessTypeAsync(
            _selectedTenant.Id,
            _editForm.BusinessType,
            _editForm.BusinessType == BusinessType.HouseholdBusiness ? _editForm.HKDGroup : null,
            _editForm.ChangeReason);
    }
    catch (InvalidOperationException ex)
    {
        ShowAlert("error", ex.Message);
        return;
    }
}
```

### P3-T6: Unit tests — Domain method
**File mới:** `6_Tests/VanAn.Core.Tests/Services/TenantChangeBusinessTypeTests.cs`

```csharp
public class TenantChangeBusinessTypeTests
{
    [Fact]
    public void ChangeBusinessType_HKD_to_Company_Succeeds_When_No_AccountingData()
    {
        // Arrange: create HKD tenant
        var tenant = Tenant.CreateHouseholdBusiness(TenantId.New(), "Test HKD", HKDGroup.Group1);
        // Act
        tenant.ChangeBusinessType(BusinessType.Company, null, "Tạo nhầm");
        // Assert
        Assert.Equal(BusinessType.Company, tenant.BusinessType);
        Assert.Null(tenant.HKDGroup);
    }

    [Fact]
    public void ChangeBusinessType_Company_to_HKD_Succeeds_With_HKDGroup()
    {
        // Arrange
        var tenant = Tenant.CreateCompany(TenantId.New(), "Test Company");
        // Act
        tenant.ChangeBusinessType(BusinessType.HouseholdBusiness, HKDGroup.Group2, "Sửa loại hình");
        // Assert
        Assert.Equal(BusinessType.HouseholdBusiness, tenant.BusinessType);
        Assert.Equal(HKDGroup.Group2, tenant.HKDGroup);
        Assert.Equal(TenantType.HKD, tenant.Type);
    }

    [Fact]
    public void ChangeBusinessType_Throws_When_Inactive()
    {
        var tenant = Tenant.CreateCompany(TenantId.New(), "Test");
        tenant.Deactivate("test");
        Assert.Throws<InvalidOperationException>(() =>
            tenant.ChangeBusinessType(BusinessType.HouseholdBusiness, HKDGroup.Group1, "test"));
    }

    [Fact]
    public void ChangeBusinessType_Throws_When_Converted()
    {
        var tenant = Tenant.CreateHouseholdBusiness(TenantId.New(), "Test", HKDGroup.Group1);
        var successorId = TenantId.New();
        tenant.MarkConvertedTo(successorId);
        Assert.Throws<InvalidOperationException>(() =>
            tenant.ChangeBusinessType(BusinessType.Company, null, "test"));
    }

    [Fact]
    public void ChangeBusinessType_Throws_When_HKDGroup_Null_For_HouseholdBusiness()
    {
        var tenant = Tenant.CreateCompany(TenantId.New(), "Test");
        Assert.Throws<ArgumentException>(() =>
            tenant.ChangeBusinessType(BusinessType.HouseholdBusiness, null, "test"));
    }

    [Fact]
    public void ChangeBusinessType_Throws_When_HKDGroup_NotNull_For_Company()
    {
        var tenant = Tenant.CreateHouseholdBusiness(TenantId.New(), "Test", HKDGroup.Group1);
        Assert.Throws<ArgumentException>(() =>
            tenant.ChangeBusinessType(BusinessType.Company, HKDGroup.Group1, "test"));
    }

    [Fact]
    public void ChangeBusinessType_Throws_When_Reason_Empty()
    {
        var tenant = Tenant.CreateCompany(TenantId.New(), "Test");
        Assert.Throws<ArgumentException>(() =>
            tenant.ChangeBusinessType(BusinessType.HouseholdBusiness, HKDGroup.Group1, ""));
    }

    [Fact]
    public void ChangeBusinessType_Raises_DomainEvent()
    {
        var tenant = Tenant.CreateCompany(TenantId.New(), "Test");
        tenant.ClearDomainEvents(); // clear TenantCreatedEvent
        tenant.ChangeBusinessType(BusinessType.HouseholdBusiness, HKDGroup.Group1, "test");
        Assert.Contains(tenant.DomainEvents, e => e is TenantBusinessTypeChangedEvent);
    }
}
```

### P3-T7: Integration tests — API endpoint
**File mới:** `6_Tests/VanAn.Integration.Tests/TenantChangeBusinessTypeApiTests.cs`

```csharp
public class TenantChangeBusinessTypeApiTests
{
    [Fact]
    public async Task ChangeBusinessType_Returns_204_When_No_AccountingData()
    {
        // Create tenant, call endpoint, assert 204
    }

    [Fact]
    public async Task ChangeBusinessType_Returns_409_When_Has_AccountingData()
    {
        // Create tenant + AccountingEntry, call endpoint, assert 409 Conflict
    }

    [Fact]
    public async Task ChangeBusinessType_Returns_400_When_Reason_Empty()
    {
        // Assert 400 Bad Request
    }
}
```

### P3-T8: E2E test — UI flow
**File mới:** `6_Testing/e2e-tests/tenant-edit-businesstype.spec.ts`

```typescript
test.describe('Bug 1 — SystemAdmin edit tenant BusinessType', () => {
  test('changes BusinessType successfully for tenant without accounting data', async ({ page }) => {
    // Login as SystemAdmin
    // Navigate to /admin/tenants
    // Click "Sửa" on a tenant without accounting data
    // Change BusinessType + enter reason
    // Click "Lưu"
    // Assert: success alert, tenant list updated
  });

  test('blocks BusinessType change for tenant with accounting data', async ({ page }) => {
    // Login as SystemAdmin
    // Click "Sửa" on a tenant WITH accounting data
    // Change BusinessType + enter reason
    // Click "Lưu"
    // Assert: error alert "Không thể đổi loại hình"
  });
});
```

### P3-T9: Build + guard + tests
- `dotnet build VanAn.sln` Release — 0 errors
- `guard-check.ps1` — ALL CHECKS PASSED
- Unit tests pass (8+ tests)
- Integration tests pass (3+ tests)
- E2E tests pass (2+ tests)
- Commit: `[TENANT-FIX P3] SystemAdmin can edit tenant BusinessType (with accounting data guard)`

## Verification
- [ ] `Tenant.ChangeBusinessType()` domain method added with all guards
- [ ] `TenantBusinessTypeChangedEvent` raised on change
- [ ] `TenantManagementService.ChangeBusinessTypeAsync()` checks AccountingEntry before calling domain method
- [ ] Gateway endpoint `[HttpPut("{tenantId:guid}/business-type")]` returns 204/409/400 appropriately
- [ ] `TenantApiClient.ChangeBusinessTypeAsync()` handles 409 Conflict with error message
- [ ] Edit Modal has BusinessType + HKDGroup + ChangeReason fields
- [ ] `HandleEditSubmit` calls ChangeBusinessType API when BusinessType changed
- [ ] Unit tests: 8+ tests covering all guards + event
- [ ] Integration tests: 3+ tests covering 204/409/400
- [ ] E2E tests: 2+ tests covering success + block scenarios
- [ ] Build 0 errors
- [ ] Guard pass
- [ ] Commit on feature branch

## Rollback
- Git revert commit
- `ChangeBusinessType` method removed from Domain
- Edit Modal trở lại không có field BusinessType (pre-fix state)
- **No migration needed** (BusinessType + HKDGroup đã là column hiện có) → không cần migration rollback

## Impact Assessment
- **User-facing:** SystemAdmin có thể sửa loại hình tenant (feature mới, có guard)
- **Domain:** Add 1 method + 1 event (approved D1) — không break existing
- **Data integrity:** Guard block đổi khi có AccountingEntry — bảo vệ consistency
- **Performance:** 1 query `AnyAsync(AccountingEntries)` thêm khi đổi — chỉ khi user đổi type
- **Security:** Endpoint trong `[Authorize(Policy = "SystemAdmin")]` (existing controller policy)
- **Audit:** `TenantBusinessTypeChangedEvent` ghi lại reason + timestamp

## Domain Modification Audit (Gate 5 compliance)
- [x] Change is part of approved feature plan (D1 approved 2026-08-03)
- [x] Domain Phase is active (Phase 3 = Domain modification phase)
- [x] User approval granted (2026-08-03: "block nếu tenant đã có AccountingEntry")
- [x] AccountingEntry remains immutable (không động AccountingEntry)
- [x] Single Source of Truth: method added to `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs`
- [x] No EF Core / DbContext in Domain layer (method là pure domain logic)
