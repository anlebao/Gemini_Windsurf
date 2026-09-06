# Sprint 7.2 — Reseller Accounting-Cashflow Alignment (R2.2)

> **Release:** R2.2 · **Branch:** `feature/reseller-accounting-fix` (from `main` after R2 merge)
> **Status:** DESIGN APPROVED 2026-09-05 (M2+ per user clarifications) — docs ready for review
> **Hard stops:** AccountingEntry immutable · Domain pure · Multi-tenancy every layer · Single-Identity Pattern · TT 152/2025/TT-BTC cash-basis

---

## 1. Problem Statement

`OrderService.GenerateAccountingEntriesAsync` (`3_CoreHub/Services/OrderService.cs:162`) has **NO `CommerceMode` branch** — all orders generate identical Revenue(511) + VAT(3331) + COGS(632) on `order.TenantId`'s books.

`WalletService.ConfirmCodResellerAsync` (`3_CoreHub/Services/WalletService.cs:223-336`) shows the actual Reseller cash flow:
- `order.TenantId` (SUPPLIER) receives only `order.CostPrice` via Wallet `Settlement`
- Customer pays `order.SellPrice` (= CostPrice + PlatformMargin)
- Margin split: PlatformFee + CommunityFund + Commission + VanAn net

**Hệ quả:** Supplier's books show inflated revenue (`sellPrice`) they never receive → violates TT 152 cash-basis principle (revenue = actual receipt).

---

## 2. Approved Design (M2+)

### 2.1 User clarifications (received 2026-09-05)

| Question | Answer |
|---|---|
| Q4: Platform entries needed in R2.2? | **Cần luôn** |
| Ai xuất VAT cho customer? | **Reseller tenant** (có thể = Vạn An hoặc tenant khác) |
| M1 đủ không? | **Không — cần M2 (Platform entries) luôn** |
| VAT treatment | **Assume standard "mua-bán qua đại lý"** (no kế toán confirm yet) |
| Scope | **Fix accounting + thêm UI/report cho auditor** |
| Platform Tenant concept | **Vạn An có tenant ID riêng cho kế toán** |
| Platform entries tạo ở đâu | **PG (Gateway)** |
| Q1: `Order.OwnerTenantId` Domain mod? | **Approve** |
| Q3: Platform entries khi Reseller = Vạn An? | **Skip** (margin đã nằm trong Reseller's gross profit) |

### 2.2 Business model clarified

```
Supplier tenant (order.TenantId)         Reseller tenant (order.OwnerTenantId — NEW)
        │                                            │
        │ sells to Reseller at costPrice             │ sells to customer at sellPrice
        │ issues VAT invoice on costPrice            │ issues VAT invoice to customer on sellPrice
        ▼                                            ▼
   [Supplier books]                            [Reseller books]
   Revenue 511 = CostPrice                     Revenue 511 = SellPrice
   VAT 3331 = VAT(CostPrice)                   VAT 3331 = VAT(SellPrice)
   COGS 632 = ProductionCost                   COGS 632 = CostPrice
                                               VAT input 1331 = VAT(CostPrice) — khấu trừ

                        Platform (Vạn An) — only when Reseller ≠ Vạn An
                                    │
                                    │ receives PlatformFee + CommunityFundShare from margin
                                    ▼
                              [Platform books]
                              Revenue 511 = PlatformFeeAmount + CommunityFundShare

                          When Reseller = Vạn An:
                          → Skip Platform entries (margin = Reseller's gross profit
                            = SellPrice - CostPrice, already in Reseller books)
```

### 2.3 Accounting entries per Reseller order

**1. Supplier tenant's books** (`order.TenantId`):
| Account | Amount | Description | Reference |
|---|---|---|---|
| 511 (Revenue) | `order.CostPrice` | Doanh thu bán cho reseller | `#{orderId}-SUP-REV` |
| 3331 (VAT output) | `VAT(order.CostPrice)` | Thuế GTGT đầu ra | `#{orderId}-SUP-VAT` |
| 632 (COGS) | `Sum(Product.CostPrice × Qty)` | Giá vốn sản xuất (unchanged) | `#{orderId}-SUP-COGS` |

**2. Reseller tenant's books** (`order.OwnerTenantId` — NEW):
| Account | Amount | Description | Reference |
|---|---|---|---|
| 511 (Revenue) | `order.SellPrice` | Doanh thu bán cho customer | `#{orderId}-RES-REV` |
| 3331 (VAT output) | `VAT(order.SellPrice)` | Thuế GTGT đầu ra (hóa đơn cho customer) | `#{orderId}-RES-VAT` |
| 632 (COGS) | `order.CostPrice` | Giá mua từ supplier | `#{orderId}-RES-COGS` |
| 1331 (VAT input) | `VAT(order.CostPrice)` | Thuế GTGT đầu vào (khấu trừ) | `#{orderId}-RES-VATIN` |

**3. Platform (Vạn An) tenant's books** (`SystemSetting "PlatformAccountingTenantId"`):
- **Chỉ khi Reseller ≠ Vạn An** (avoid double-counting):
| Account | Amount | Description | Reference |
|---|---|---|---|
| 511 (Revenue) | `order.PlatformFeeAmount + CommunityFundShare` | Doanh thu dịch vụ đại lý | `#{orderId}-PLT-REV` |

- **Khi Reseller = Vạn An**: skip Platform entries entirely (margin đã nằm trong Reseller's gross profit = `SellPrice - CostPrice`)

### 2.4 VAT treatment assumptions (standard "mua-bán qua đại lý")

- Supplier output VAT = `VAT(CostPrice)` — supplier xuất hóa đơn cho Reseller
- Reseller output VAT = `VAT(SellPrice)` — Reseller xuất hóa đơn cho customer
- Reseller input VAT (khấu trừ) = `VAT(CostPrice)` — Reseller nhận hóa đơn từ Supplier
- Reseller net VAT payable = `VAT(SellPrice) − VAT(CostPrice)`
- Platform fee VAT (when Reseller ≠ Vạn An): assume same `VatRate` — TBD confirm kế toán

> ⚠️ **OPEN:** VAT rate cho Platform fee — assume `order.VatRate` hiện tại. Nếu kế toán yêu cầu rate khác (vd 10% cho dịch vụ), cần tách.

---

## 3. Implementation Scope

### 3.1 Domain mod (approved — small)

**File:** `1_Shared/Domain.cs` (Order entity)

```csharp
public Guid? OwnerTenantId { get; protected set; }  // R2.2: Reseller tenant (issues VAT to customer) — distinct from TenantId (supplier)
```

`SetResellerPricing` thêm 1 param `Guid? ownerTenantId`:
- Guard: `if (ownerTenantId == Guid.Empty) throw ...`
- Set: `OwnerTenantId = ownerTenantId`

**Single-Identity Pattern check:** `OwnerTenantId` là FK Guid (not value object) — references `BaseEntity.Id` của Tenant. ✅ Compliant.

**Domain purity:** Plain Guid? property, no EF attrs. ✅ Compliant.

### 3.2 EF config + migrations

| File | Change |
|---|---|
| `3_CoreHub/Infrastructure/Configurations/OrderConfiguration.cs` | `builder.Property(o => o.OwnerTenantId).HasColumnName("OwnerTenantId");` (nullable Guid) |
| PG migration (new) | `ALTER TABLE "Orders" ADD COLUMN "OwnerTenantId" uuid NULL;` |
| SQLite migration (new) | `ALTER TABLE Orders ADD COLUMN OwnerTenantId TEXT NULL;` (ShopERP also has Orders) |

### 3.3 Service — SnapshotCommerceModeAsync

**File:** `3_CoreHub/Services/OrderService.cs:850`

Lookup `KhachLinkInstance` by domain (from `command.SourceDomain` hoặc tenant's current KhachLinkInstance) → lấy `OwnerTenantId` → pass to `SetResellerPricing`.

```csharp
// Pseudo:
var kli = await _dbContext.KhachLinkInstances
    .IgnoreQueryFilters()
    .FirstOrDefaultAsync(k => k.CustomDomain == domain || k.OwnerTenantId == tenantId);
Guid? ownerTenantId = kli?.OwnerTenantId;
order.SetResellerPricing(totalCostPrice, sellPrice, platformMargin, deliveryFee,
    platformFeeRate, communityFundRate, ownerTenantId);
```

### 3.4 Service — GenerateAccountingEntriesAsync Reseller branch

**File:** `3_CoreHub/Services/OrderService.cs:162`

Add `if (order.CommerceMode == CommerceMode.Reseller)` branch:

```csharp
if (order.CommerceMode == CommerceMode.Reseller && order.OwnerTenantId.HasValue)
{
    var supplierTenantId = tenantId;
    var resellerTenantId = new TenantId(order.OwnerTenantId.Value);
    var costPrice = order.CostPrice ?? 0m;
    var sellPrice = order.SellPrice ?? 0m;

    // === 1. Supplier books ===
    await _accountingService.CreateRevenueEntryAsync(supplierTenantId, period,
        costPrice, $"Doanh thu bán cho reseller #{order.Id}",
        accountCode: "511", reference: $"{orderRef}-SUP-REV", industrySector: sector);
    // VAT on costPrice
    // COGS unchanged (production cost)

    // === 2. Reseller books ===
    await _accountingService.CreateRevenueEntryAsync(resellerTenantId, period,
        sellPrice, $"Doanh thu bán cho customer #{order.Id}",
        accountCode: "511", reference: $"{orderRef}-RES-REV", industrySector: sector);
    // VAT on sellPrice
    // COGS = costPrice
    // VAT input 1331 on costPrice

    // === 3. Platform books (skip if Reseller = Vạn An) ===
    var platformTenantIdStr = await _systemSettingService.GetAsync("PlatformAccountingTenantId");
    if (Guid.TryParse(platformTenantIdStr, out var platformGuid)
        && platformGuid != Guid.Empty
        && platformGuid != order.OwnerTenantId.Value)  // skip when Reseller = Vạn An
    {
        var platformTenantId = new TenantId(platformGuid);
        var platformIncome = (order.PlatformFeeAmount ?? 0m) + communityFundShare;
        if (platformIncome > 0)
        {
            await _accountingService.CreateRevenueEntryAsync(platformTenantId, period,
                platformIncome, $"Doanh thu dịch vụ đại lý #{order.Id}",
                accountCode: "511", reference: $"{orderRef}-PLT-REV", industrySector: sector);
        }
    }
    return;  // skip Marketplace path
}

// === Existing Marketplace path (unchanged) ===
```

### 3.5 SystemSetting seed

**Seed:** `"PlatformAccountingTenantId"` = Guid của Vạn An's tenant (1-time SysAdmin config via existing SystemSetting admin UI).

**Fallback:** If setting missing → log warning + skip Platform entries (degrade gracefully, don't fail order).

### 3.6 Auditor UI report

**New page:** `5_WebApps/ShopERP/Components/Pages/Admin/ResellerAccountingReconciliation.razor`
- Route: `/admin/reseller-accounting-reconciliation`
- Authorize: `SystemAdmin` policy (cross-tenant view)
- Columns per order: OrderId · Date · Supplier tenant · Reseller tenant · CostPrice · SellPrice · Margin · Supplier entries (511/3331/632) · Reseller entries (511/3331/632/1331) · Platform entries · Wallet transactions · VAT chain summary
- Filter: Date range · Supplier tenant · Reseller tenant
- Export: CSV (Excel export deferred)

**New API client:** `5_WebApps/ShopERP/Services/ResellerAccountingApiClient.cs` — GET `/api/admin/reseller-accounting/reconciliation?from=&to=&supplierTenantId=&resellerTenantId=`

**New Gateway endpoint:** `2_Gateway/Controllers/ResellerAccountingController.cs` — query AccountingEntry + WalletTransaction + Order join, return DTO.

### 3.7 Idempotency

- Mỗi entry có `reference` suffix: `#{orderId}-SUP-REV`, `#{orderId}-RES-REV`, `#{orderId}-PLT-REV`
- `PaymentConfirmedSubscriber` đã check `JournalEntry.Reference` trước khi gọi `GenerateAccountingEntriesAsync` — covers new references
- **Verification:** Nếu subscriber check chỉ theo `orderRef` gốc (không có suffix) → cần update check để cover suffix patterns. Implement phase sẽ verify.

---

## 4. Files to Change

| File | Change | Layer |
|---|---|---|
| `1_Shared/Domain.cs` | Add `Order.OwnerTenantId` (Guid?) + param to `SetResellerPricing` | Domain |
| `3_CoreHub/Infrastructure/Configurations/OrderConfiguration.cs` | Map `OwnerTenantId` column | Infrastructure |
| `3_CoreHub/Infrastructure/Migrations/{ts}_AddOrderOwnerTenantId.cs` (PG) | New migration | Infrastructure |
| `5_WebApps/ShopERP/Migrations/{ts}_AddOrderOwnerTenantId.cs` (SQLite) | New migration | Infrastructure |
| `3_CoreHub/Services/OrderService.cs` | `SnapshotCommerceModeAsync` lookup KLI + `GenerateAccountingEntriesAsync` Reseller branch | Service |
| `3_CoreHub/Services/ISystemSettingService.cs` (if exists) | Verify `GetAsync(key)` API | Service |
| `2_Gateway/Controllers/ResellerAccountingController.cs` (NEW) | Reconciliation query endpoint | API |
| `5_WebApps/ShopERP/Services/ResellerAccountingApiClient.cs` (NEW) | HTTP client for reconciliation | UI Service |
| `5_WebApps/ShopERP/Components/Pages/Admin/ResellerAccountingReconciliation.razor` (NEW) | Auditor report page | UI |
| `5_WebApps/ShopERP/Components/Layout/NavMenu.razor` | Add nav link under Admin → Kế Toán | UI |
| `6_Tests/...` | ~10-12 new tests | Tests |

**Total:** ~11 files (4 new, 7 modified) + 2 migrations + ~10-12 tests

---

## 5. Tests

### 5.1 Domain tests (2)
- `Order_SetResellerPricing_WithOwnerTenantId_SetsField`
- `Order_SetResellerPricing_WithEmptyOwnerTenantId_Throws`

### 5.2 Service tests (6)
- `GenerateAccountingEntriesAsync_Reseller_CreatesSupplierEntries` (Revenue=CostPrice, VAT=VAT(CostPrice), COGS=production)
- `GenerateAccountingEntriesAsync_Reseller_CreatesResellerEntries` (Revenue=SellPrice, VAT=VAT(SellPrice), COGS=CostPrice, VAT input 1331)
- `GenerateAccountingEntriesAsync_Reseller_CreatesPlatformEntries_WhenResellerNotVA` (Platform Revenue = PlatformFee + CommunityFund)
- `GenerateAccountingEntriesAsync_Reseller_SkipsPlatformEntries_WhenResellerIsVA` (no Platform entries)
- `GenerateAccountingEntriesAsync_Reseller_SkipsPlatformEntries_WhenSettingMissing` (degrade gracefully, log warning)
- `GenerateAccountingEntriesAsync_Marketplace_Unchanged` (regression — existing path intact)

### 5.3 Idempotency tests (2)
- `PaymentConfirmedSubscriber_DoesNotDuplicate_ResellerEntries` (call twice → same entries, no dupes)
- `GenerateAccountingEntriesAsync_Reseller_ReferenceSuffix` (verify `#{orderId}-SUP-REV` etc.)

### 5.4 Accounting = cashflow invariant (2)
- `ResellerOrder_AccountingSum_Equals_WalletSum` (sum of all AccountingEntry amounts = sum of all WalletTransaction amounts per order)
- `ResellerOrder_VATChain_Balanced` (Supplier output VAT + Reseller output VAT − Reseller input VAT = correct net VAT)

### 5.5 Auditor UI tests (bUnit, ~2)
- `ResellerAccountingReconciliation_Page_Renders_With_SystemAdmin`
- `ResellerAccountingReconciliation_Page_FilteredByDate`

---

## 6. Validation Criteria (Done Definition)

- [ ] `dotnet build VanAn.sln` — 0 errors
- [ ] `guard-check.ps1` — PASS
- [ ] All existing tests PASS (regression)
- [ ] All new R2.2 tests PASS (~10-12)
- [ ] Accounting = cashflow invariant test PASS
- [ ] VAT chain balanced test PASS
- [ ] Domain purity check (no EF attrs in Domain.cs)
- [ ] AccountingEntry immutability preserved (append-only)
- [ ] Single-Identity Pattern preserved (`OwnerTenantId` = Guid? FK, not value object)
- [ ] Multi-tenancy enforced (entries scoped to correct tenant)
- [ ] CI pass (all 4 workflows: main + accounting + pr-validation + e2e)
- [ ] CD deploy to 3 VPS
- [ ] RV Layer 1: API — create Reseller order → verify 3 booksets in PG
- [ ] RV Layer 5: DB — query AccountingEntry by `reference LIKE '%-SUP-REV'` etc.
- [ ] RV: Auditor UI page renders + filter works + export CSV

---

## 7. Rollback Plan

- Branch `feature/reseller-accounting-fix` — merge via PR, squash
- If issue: revert merge commit on `main` → CD auto-redeploy
- Database: `OwnerTenantId` column nullable — no data loss on revert (column stays, just unused)
- SystemSetting: `PlatformAccountingTenantId` row stays (harmless if unused)
- No data migration needed for rollback (no existing orders have `OwnerTenantId` — all are Marketplace)

---

## 8. Open Questions (resolve before/during implement)

| # | Question | Default if unresolved |
|---|---|---|
| O1 | VAT rate cho Platform fee — same `order.VatRate`? | Assume same `order.VatRate` — flag in code comment |
| O2 | Auditor export — CSV only, hay Excel (OpenXML)? | CSV only (Excel deferred) |
| O3 | `ISystemSettingService` đã tồn tại chưa? | Verify in implement phase — if not, use `IVanAnDbContext.SystemSettings` query directly |
| O4 | `KhachLinkInstance` lookup by domain — nào là reliable? | Use `command.SourceDomain` if present, else fallback by `tenantId` match `OwnerTenantId` |

---

## 9. References

- **Master plan:** `docs/AI/tasks/khachlink_multi_profile/master_plan.md`
- **Sprint 7 task card (R2):** `docs/AI/tasks/khachlink_multi_profile/sprint7_reseller_task_card.md`
- **Order entity:** `1_Shared/Domain.cs:1494` (Order class), `:1658` (SetResellerPricing)
- **Accounting generation:** `3_CoreHub/Services/OrderService.cs:162`
- **Wallet split:** `3_CoreHub/Services/WalletService.cs:223-336`
- **Accounting contract:** `3_CoreHub/Services/IAccountingService.cs`
- **SystemSetting config:** `3_CoreHub/Infrastructure/Configurations/SystemSettingConfiguration.cs`
- **KhachLinkInstance (OwnerTenantId source):** `1_Shared/Domain/Aggregates/KhachLinkAggregate/KhachLinkInstance.cs:39`
- **PaymentConfirmedSubscriber (idempotency):** `5_WebApps/ShopERP/Services/PaymentConfirmedSubscriber.cs`
- **Governance:** `.devin/rules/governance.md` (AccountingEntry immutable, Domain pure, Single-Identity)
