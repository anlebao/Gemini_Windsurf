# TASK CARD — Phase 1: Bug 2A — Hide "Sổ HKD" Menu for Company Tenants

> **Status:** 🟡 PLANNED — ready to implement
> **Prerequisite:** None (independent phase)
> **Branch:** `feature/tenant-fix-phase1-hide-hkd-menu`
> **Estimated sessions:** 1
> **Mode:** IMPLEMENT
> **Domain modification:** ❌ NO (UI only)

## Objective
Ẩn menu "Sổ HKD (TT 152)" trong `AccountingLayout.razor` đối với tenant Company (Enterprise_*). Chỉ hiển thị cho tenant HKD (HouseholdBusiness).

**Business logic:**
- Tenant HKD → menu có "Sổ HKD", không có "Báo Cáo Tài Chính"
- Tenant Company → menu có "Báo Cáo Tài Chính", không có "Sổ HKD"
- SystemAdmin without tenant → redirect (đã handle ở line 58-67)

## Prerequisites
- [ ] Phase 1 INVESTIGATE: verify `AccountingLayout.razor` current state
- [ ] Confirm `CanAccessVasReportsAsync` returns `true` cho Enterprise_*, `false` cho HKD
- [ ] Confirm `_isEnterprise` field tồn tại (line 47)

## Files to Modify
| File | Changes |
|------|---------|
| `5_WebApps/ShopERP/Components/Pages/Accounting/AccountingLayout.razor` | Add `_isHkd` field + conditional menu item |
| `6_Testing/e2e-tests/hkd-menu-visibility.spec.ts` (new) | E2E test Gate 4 compliance |

## Detailed Task List

### P1-T1: Add `_isHkd` field + compute from `_isEnterprise`
**File:** `5_WebApps/ShopERP/Components/Pages/Accounting/AccountingLayout.razor`

**Current (line 47):**
```csharp
private bool _isEnterprise;
```

**New:**
```csharp
private bool _isEnterprise;
private bool _isHkd;
```

**Current (line 84-96 — VAS menu block):**
```csharp
// W8 feature flag: only show VAS Financial Reports menu for Enterprise tenants
if (TenantProvider.HasTenant)
{
    try
    {
        var tenantId = new TenantId(TenantProvider.TenantId);
        _isEnterprise = await FeatureFlagService.CanAccessVasReportsAsync(tenantId);
        if (_isEnterprise)
        {
            // Insert VAS menu before Đóng Sổ Kỳ
            AccountingMenuItems.Insert(AccountingMenuItems.Count - 1,
                new() { Title = "Báo Cáo Tài Chính", Icon = "bar-chart", Url = "/accounting/financial-reports" });
        }
    }
    catch
    {
        _isEnterprise = false;
    }
}
```

**New:**
```csharp
// W8 feature flag: only show VAS Financial Reports menu for Enterprise tenants
// Bug 2A fix: only show "Sổ HKD" menu for HKD tenants (mutually exclusive with VAS)
if (TenantProvider.HasTenant)
{
    try
    {
        var tenantId = new TenantId(TenantProvider.TenantId);
        _isEnterprise = await FeatureFlagService.CanAccessVasReportsAsync(tenantId);
        _isHkd = !_isEnterprise;  // HKD = không phải Enterprise
        if (_isEnterprise)
        {
            // Insert VAS menu before Đóng Sổ Kỳ
            AccountingMenuItems.Insert(AccountingMenuItems.Count - 1,
                new() { Title = "Báo Cáo Tài Chính", Icon = "bar-chart", Url = "/accounting/financial-reports" });
        }
    }
    catch
    {
        _isEnterprise = false;
        _isHkd = true;  // safe default: show HKD menu if flag check fails
    }
}
else
{
    // No tenant context (should not reach here — redirect handled above)
    _isHkd = true;  // default to HKD menu
}
```

### P1-T2: Make "Sổ HKD" menu item conditional
**File:** `5_WebApps/ShopERP/Components/Pages/Accounting/AccountingLayout.razor`

**Current (line 69-82 — base menu, "Sổ HKD" always visible):**
```csharp
AccountingMenuItems = new()
{
    new() { Title = "← Sitemap", Icon = "house-door", Url = "/sitemap" },
    new() { Title = "Dashboard", Icon = "dashboard", Url = "/accounting" },
    new() { Title = "Sản phẩm", Icon = "box-seam", Url = "/products" },
    new() { Title = "Nhập Doanh Thu", Icon = "plus-circle", Url = "/accounting/revenue" },
    new() { Title = "Nhập Chi Phí", Icon = "minus-circle", Url = "/accounting/expenses" },
    new() { Title = "Lịch Sử Giao Dịch", Icon = "history", Url = "/accounting/history" },
    new() { Title = "Số Dư Tài Khoản", Icon = "account-balance", Url = "/accounting/balance" },
    new() { Title = "Sổ HKD (TT 152)", Icon = "book", Url = "/accounting/hkd-books" },
    new() { Title = "Đóng Sổ Kỳ", Icon = "lock", Url = "/accounting/period-closing" },
    new() { Title = "Đăng xuất", Icon = "box-arrow-right", Url = "/Logout" }
};
```

**New approach:** Build base menu WITHOUT "Sổ HKD", then conditionally insert it for HKD tenants (pattern giống VAS menu insert).

```csharp
// Build base menu (shared — HKD + Enterprise)
// Bug 2A fix: "Sổ HKD" removed from base — inserted conditionally below for HKD tenants only
AccountingMenuItems = new()
{
    new() { Title = "← Sitemap", Icon = "house-door", Url = "/sitemap" },
    new() { Title = "Dashboard", Icon = "dashboard", Url = "/accounting" },
    new() { Title = "Sản phẩm", Icon = "box-seam", Url = "/products" },
    new() { Title = "Nhập Doanh Thu", Icon = "plus-circle", Url = "/accounting/revenue" },
    new() { Title = "Nhập Chi Phí", Icon = "minus-circle", Url = "/accounting/expenses" },
    new() { Title = "Lịch Sử Giao Dịch", Icon = "history", Url = "/accounting/history" },
    new() { Title = "Số Dư Tài Khoản", Icon = "account-balance", Url = "/accounting/balance" },
    new() { Title = "Đóng Sổ Kỳ", Icon = "lock", Url = "/accounting/period-closing" },
    new() { Title = "Đăng xuất", Icon = "box-arrow-right", Url = "/Logout" }
};
```

Then trong block `if (TenantProvider.HasTenant)` (sau khi compute `_isHkd`):
```csharp
if (_isHkd)
{
    // Insert "Sổ HKD" before "Đóng Sổ Kỳ" (index = Count - 2, before last 2 items)
    AccountingMenuItems.Insert(AccountingMenuItems.Count - 2,
        new() { Title = "Sổ HKD (TT 152)", Icon = "book", Url = "/accounting/hkd-books" });
}
```

**Order trong menu cuối (HKD tenant):**
1. ← Sitemap
2. Dashboard
3. Sản phẩm
4. Nhập Doanh Thu
5. Nhập Chi Phí
6. Lịch Sử Giao Dịch
7. Số Dư Tài Khoản
8. **Sổ HKD (TT 152)** ← chỉ HKD
9. Đóng Sổ Kỳ
10. Đăng xuất

**Order trong menu cuối (Company tenant):**
1. ← Sitemap
2. Dashboard
3. Sản phẩm
4. Nhập Doanh Thu
5. Nhập Chi Phí
6. Lịch Sử Giao Dịch
7. Số Dư Tài Khoản
8. **Báo Cáo Tài Chính** ← chỉ Company (insert ở Count-1, trước Đăng xuất)

Wait — cần check thứ tự insert. VAS menu insert ở `Count - 1` (trước Đăng xuất). HKD menu insert ở `Count - 2` (trước Đóng Sổ Kỳ). Vì 2 menu mutually exclusive, chỉ 1 trong 2 insert chạy → order OK.

**Revised:** Cả 2 insert cùng vị trí `Count - 2` (trước Đóng Sổ Kỳ) để UX consistent:

```csharp
if (_isEnterprise)
{
    AccountingMenuItems.Insert(AccountingMenuItems.Count - 2,
        new() { Title = "Báo Cáo Tài Chính", Icon = "bar-chart", Url = "/accounting/financial-reports" });
}
else if (_isHkd)
{
    AccountingMenuItems.Insert(AccountingMenuItems.Count - 2,
        new() { Title = "Sổ HKD (TT 152)", Icon = "book", Url = "/accounting/hkd-books" });
}
```

### P1-T3: E2E test (Gate 4 compliance)
**File mới:** `6_Testing/e2e-tests/hkd-menu-visibility.spec.ts`

```typescript
import { test, expect } from '@playwright/test';

test.describe('Bug 2A — Accounting menu visibility by tenant type', () => {
  test('HKD tenant sees "Sổ HKD" menu, not "Báo Cáo Tài Chính"', async ({ page }) => {
    // Login as HKD tenant owner
    // Navigate to /accounting
    // Assert: menu contains "Sổ HKD (TT 152)"
    // Assert: menu does NOT contain "Báo Cáo Tài Chính"
  });

  test('Company tenant sees "Báo Cáo Tài Chính" menu, not "Sổ HKD"', async ({ page }) => {
    // Login as Company tenant owner
    // Navigate to /accounting
    // Assert: menu contains "Báo Cáo Tài Chính"
    // Assert: menu does NOT contain "Sổ HKD (TT 152)"
  });
});
```

**Note:** Cần INVESTIGATE existing E2E test pattern (login flow, tenant setup) trước khi viết test. Tham khảo `6_Testing/e2e-tests/hkd-books.spec.ts` (đã tồn tại).

### P1-T4: Build + guard + tests
- `dotnet build VanAn.sln` Release — 0 errors
- `guard-check.ps1` — ALL CHECKS PASSED
- E2E test pass (nếu local infra có Playwright)
- Commit: `[TENANT-FIX P1] hide Sổ HKD menu for Company tenants`

## Verification
- [ ] `_isHkd` field added + computed from `_isEnterprise`
- [ ] "Sổ HKD" menu item conditional (only HKD tenants)
- [ ] "Báo Cáo Tài Chính" menu item conditional (only Enterprise tenants — đã có, không đổi)
- [ ] E2E test covers both HKD + Company scenarios
- [ ] Build 0 errors
- [ ] Guard pass
- [ ] Commit on feature branch

## Rollback
- Git revert commit
- Menu "Sổ HKD" sẽ lại always visible (pre-fix state)

## Impact Assessment
- **User-facing:** Tenant Company không còn thấy link "Sổ HKD" (sai feature cho Company)
- **Performance:** Không đổi (1 boolean check thêm)
- **Security:** Không đổi (menu visibility, không phải authorization)
- **Data:** Không đổi (UI only)
