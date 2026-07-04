# TASK CARD — VAS Wave 3: Account Code Map

> **Status:** NOT STARTED | INVESTIGATE → PLAN → IMPLEMENT
> **Prerequisite:** W2 merged (Domain records available) — ✅ MERGED `fef8097`
> **Branch:** `feature/vas-wave3-account-code-map`
> **Estimated sessions:** 1-2

## JIT Update (from master plan §4.1 — applied 2026-07-04 before INVESTIGATE)

W2 restructured BCTC records theo pháp luật VN. Downstream impacts applied to this card:

| # | Impact | Resolution in this card |
|---|--------|--------------------------|
| J1 | `AccountType` enum has **5 values, NO `Contra`** (`Asset, Liability, Equity, Revenue, Expense`) — verified `Domain.cs:2348` | W3-T2 seed spec updated: contra TK (214, 229) typed as `Type=Asset` + `IsNormalCredit=true` |
| J2 | `AccountChartEntry` record has `IsNormalCredit` flag (`Domain.cs:2409-2411`) — for contra TK 214/229 | W3-T1 interface + W3-T2 seed must populate `IsNormalCredit` per account |
| J3 | HKD→DN mapping table (D9) — **không đổi** | W3-T3 mapping table kept as-is (HKD internal keys → DN codes per standard) |
| J4 | `AccountChartEntry` is **in-memory record** in Domain (storage decision deferred to W3) | W3-T2 must decide: DB table `AccountCharts` (persisted, multi-tenant) vs in-memory dictionary (read-only seed). **Recommendation:** DB table — consistent with W1 seeder pattern, supports D9 conversion opening balance in W8. Needs migration. |

## Objective
Create AccountCode mapping table + 3 standards (TT 99/133/58) + **HKD→DN account mapping (D9)** + refactor hardcoded GetAccountName.

## Prerequisites (verify before code)
- [x] W2 merged (AccountChartEntry, AccountingStandard enum, AccountType enum available) — `fef8097`
- [x] Verify HKDBookService.GetAccountName method (hardcoded) — `HKDBookService.cs:743-746`, dictionary at `:28-47` (16 entries)
- [x] Grep GetAccountName usage — 2 call sites (`:260`, `:379`)
- [ ] Search web for TT 99/2025 phụ lục TK (R1) — pending INVESTIGATE

## Files to Create/Modify
| File | Action |
|------|--------|
| `3_CoreHub/Services/IAccountChartService.cs` | CREATE interface |
| `3_CoreHub/Services/AccountChartService.cs` | CREATE implementation |
| `3_CoreHub/Services/IHkdToEnterpriseAccountMapper.cs` | CREATE interface (D9) |
| `3_CoreHub/Services/HkdToEnterpriseAccountMapper.cs` | CREATE implementation (D9) |
| `3_CoreHub/Services/HKDBookService.cs` | MODIFY `_vietnameseAccounts` dict — fix F1-F6 label bugs (W3-T6, approved 2026-07-04) |
| `1_Shared/Domain.cs` | **NO CHANGE** — `AccountChartEntry` record already exists (W2) |
| `3_CoreHub/Infrastructure/Entities/AccountChartEntity.cs` | CREATE EF entity (DB table — J4 resolved) |
| `3_CoreHub/Infrastructure/Configurations/AccountChartConfiguration.cs` | CREATE EF config (auto-discovered via `ApplyConfigurationsFromAssembly`) |
| `3_CoreHub/Infrastructure/Migrations/<timestamp>_AddAccountCharts.cs` | CREATE migration |
| `3_CoreHub/Infrastructure/Seed/AccountChartSeeder.cs` | CREATE seeder (3 standards, TT 133 first per R3) |
| `5_WebApps/ShopERP/Program.cs` | ADD DI registration (IAccountChartService, IHkdToEnterpriseAccountMapper) |

## Detailed Task List

### W3-T1: Create IAccountChartService + AccountChartService
```csharp
public interface IAccountChartService
{
    Task<string> GetAccountNameAsync(string accountCode, AccountingStandard standard);
    Task<List<AccountChartEntry>> GetAccountsByTypeAsync(AccountType type, AccountingStandard standard);
    Task<AccountType> GetAccountTypeAsync(string accountCode, AccountingStandard standard);
    Task<AccountChartEntry?> GetAccountAsync(string accountCode, AccountingStandard standard);
}
```
**Note (J2):** `AccountChartEntry` record carries `IsNormalCredit` flag — service must return it for contra TK handling (214/229). Caller (W4 services) uses `IsNormalCredit` to flip debit/credit sign for normal-balance reporting.

### W3-T2: Seed AccountChart data (3 standards)
- TT 99/2025: full TK 1xx-9xx (~71 accounts cấp 1) — **F8 (approved 2026-07-04):** include new TK 215 (Tài sản sinh học) + new TK 33x (Phải trả cổ tức, lợi nhuận — verify exact code during seed)
- TT 133/2016: rút gọn (~60 accounts) — **ưu tiên trước (R3)**
- TT 58/2026: siêu nhỏ (~30 accounts)
- **Storage decision (J4):** DB table `AccountCharts` (recommended) vs in-memory dictionary. If DB: create `AccountChartEntity` + `AccountChartConfiguration` + migration. If in-memory: static dictionary in `AccountChartService`.
- **Contra account handling (J1+J2):** TK 214 (Hao mòn TSCĐ) and TK 229 ( nếu có ) seeded with:
  - `Type = AccountType.Asset` (NOT a separate Contra type — enum has only 5 values)
  - `IsNormalCredit = true` (normal credit balance — depreciation accumulates on credit side)
  - All other Asset TK: `IsNormalCredit = false` (normal debit)
  - All Liability/Equity/Revenue TK: `IsNormalCredit = true` (normal credit)
  - All Expense TK: `IsNormalCredit = false` (normal debit)
- **Lưu ý đặc biệt (F9 — confirmed 2026-07-04):** Các tài khoản giảm trừ (như **TK 521 "Các khoản giảm trừ doanh thu"** — chiết khấu thương mại, hàng bán bị trả lại, giảm giá hàng bán) thuộc nhóm Revenue nhưng có **kết cấu Nợ bình thường** → `Type = AccountType.Revenue, IsNormalCredit = false`. Hệ thống báo cáo (W4) cần xử lý logic: nếu `IsNormalCredit == false` trong nhóm Revenue, giá trị được **trừ đi** thay vì cộng vào tổng doanh thu (Doanh thu thuần = 511 Credit − 521 Debit). Nếu để `IsNormalCredit = true` (nhầm), Net Revenue sẽ bị sai lệch hoàn toàn. **Verify:** `AccountChartService.GetAccountAsync("521", ...)` phải trả entry với `IsNormalCredit = false`.
- **Multi-tenancy:** AccountCharts is **reference data** (shared across tenants per standard) — NOT tenant-scoped. Seed once globally. `AccountChartEntity` does NOT implement `IMustHaveTenant` → no query filter (verified `VanAnDbContext.cs:230` only filters `IMustHaveTenant` entities; precedent: `PermissionGroup`, `AuditLog`).

### W3-T3: Add HKD→DN account mapping (D9 — for conversion opening balance)
Add mapping table for HKD internal synthetic accounts → DN chart of accounts:
```csharp
public interface IHkdToEnterpriseAccountMapper
{
    // Map HKD internal account → DN account code (per standard)
    string MapToEnterpriseAccount(string hkdAccountKey, AccountingStandard standard);
    // Get all mappings for a standard
    Dictionary<string, string> GetMappings(AccountingStandard standard);
}
```
**Mapping data (J3 — unchanged from original card):**
| HKD internal key | DN TT 133 | DN TT 99 | DN TT 58 | Ghi chú |
|------------------|-----------|----------|----------|---------|
| Revenue | 511 | 511 | 511 | Doanh thu |
| COGS | 632 | 632 | 632 | Giá vốn |
| Cash | 111 | 111 | 111 | Tiền mặt |
| CashBank | 112 | 112 | 112 | Tiền gửi NH |
| Inventory | 156 | 156 | 156 | Hàng hóa |
| Materials | 152 | 152 | 152 | Vật liệu |
| SellingExpense | 641 | 641 | 641 | CP bán hàng |
| AdminExpense | 642 | 642 | 642 | CP QLDN |
| TaxOutput | 3331 | 3331 | 3331 | Thuế GTGT đầu ra |
| TaxInput | 1331 | 1331 | 1331 | Thuế GTGT đầu vào |
| Payroll | 334 | 334 | 334 | Phải trả lương |
| FixedAsset | 211 | 211 | 211 | TSCĐ |
| Depreciation | 214 | 214 | 214 | KH TSCĐ (contra — IsNormalCredit=true in chart) |
| Equity | 411 | 411 | 411 | Vốn CSH |

**Lưu ý (D9):** HKD single-entry không có double-entry structure. Mapping là "best-effort" — opening balance migration (W8) sẽ cần manual review. Mapping table này chỉ cung cấp **account code translation**, không phải balance translation.

### W3-T4: ~~Refactor HKDBookService.GetAccountName~~ → **SKIP** (INVESTIGATE confirmed rec c)
- HKD uses TT 88/2021 + TT 152/2025 (single-entry, 7 simplified sổ — NOT a formal chart of accounts). `AccountingStandard` enum only has TT99/133/58 (D1 scope). Forcing HKD through `IAccountChartService` would mismatch standards.
- **Decision (approved 2026-07-04):** Keep HKD dictionary separate. Do NOT refactor `HKDBookService.GetAccountName` to call `IAccountChartService`.
- **Instead:** Fix the 6 label bugs in the existing HKD dictionary → see W3-T6.

### W3-T5: Build + guard pass

### W3-T6: Fix HKD dictionary label bugs (F1-F6 — approved 2026-07-04)
**Context:** TT 88/2021 is a single-entry system with 7 simplified sổ (NOT a formal chart of accounts). The HKD `_vietnameseAccounts` dictionary in `HKDBookService.cs:28-47` is an "Internal Synthetic Mapping" that borrows account codes from TT 133/TT 200 for display purposes. 6 label bugs found during INVESTIGATE:

| # | Code | Current (WRONG) | Correct (TT 133/200) | Fix |
|---|------|-----------------|----------------------|-----|
| F1 | 641 | "Chi phí quản lý doanh nghiệp" | "Chi phí bán hàng" | Swap label with 642 |
| F2 | 642 | "Chi phí bán hàng" | "Chi phí quản lý doanh nghiệp" | Swap label with 641 |
| F3 | 811 | "Xác định kết quả kinh doanh" | "Chi phí khác" | Fix label (911 = XĐKQ) |
| F4 | 822 | "Thu nhập khác" | 711 = "Thu nhập khác" | Change key 822→711 (822 doesn't exist) |
| F5 | 831 | "Lợi nhuận trước thuế" | (remove — fabricated, not a real TK) | Remove entry |
| F6 | 311 | "Vay ngắn hạn ngân hàng" | "Vay và nợ thuê tài chính" | Fix label per TT 133 |

**Add missing accounts:**
- 911 = "Xác định kết quả kinh doanh" (was missing — 811 was wrongly labeled as this)

**Verified safe:** Grep confirmed 641/642/811/822/831 appear ONLY in the dictionary definition (lines 41-46), NOT in any entry creation logic. No code depends on the labels or the 822/831 keys. Fix is display-only.

**Note:** F6 revised during INVESTIGATE — 311 is NOT obsolete for HKD context (TT 88/2021 HKD can have loans). Only the label was inaccurate. 311 exists in TT 133 as "Vay và nợ thuê tài chính".

## Verification
- [x] W3-AC1: `GetAccountNameAsync("511", TT133_2016)` returns "Doanh thu bán hàng và cung cấp dịch vụ"
- [x] W3-AC2: `GetAccountTypeAsync("511", TT133_2016)` returns `Revenue`
- [x] W3-AC3: `GetAccountAsync("214", TT133_2016)` returns entry with `Type=Asset, IsNormalCredit=true` (J1+J2)
- [x] W3-AC4: `GetAccountAsync("521", TT99_2025)` returns entry with `Type=Revenue, IsNormalCredit=false` (F9 contra-revenue — TT 99 ONLY, NOT TT 133)
- [x] W3-AC4b: `GetAccountAsync("521", TT133_2016)` returns null (521 removed in TT 133)
- [x] W3-AC5: `GetAccountNameAsync("999", TT133_2016)` returns fallback "Tài khoản 999"
- [x] W3-MP1: `MapToEnterpriseAccount("Revenue", TT133_2016)` returns "511" (Theory ×2 standards)
- [x] W3-MP2: `MapToEnterpriseAccount("Depreciation", TT133_2016)` returns "214" (Theory ×2 standards)
- [x] W3-MP3: `MapToEnterpriseAccount("UnknownKey", ...)` throws KeyNotFoundException
- [x] W3-SE1: SeedAsync creates TT133=49, TT99=73, TT58=0, total=122
- [x] W3-SE2: CleanupAsync + SeedAsync is idempotent
- [x] W3-SE3: TT 133 seeded first (R3 priority)
- [x] W3-SE4: No duplicate AccountCode per Standard
- [x] **W3-T6 (F1-F6):** HKD dict — `_vietnameseAccounts["641"]` = "Chi phí bán hàng", `["642"]` = "Chi phí quản lý doanh nghiệp", `["811"]` = "Chi phí khác", `["711"]` = "Thu nhập khác", `["911"]` = "Xác định kết quả kinh doanh", `["311"]` = "Vay và nợ thuê tài chính", no key "822" or "831"
- [x] Build pass + guard pass
- [x] Arch tests pass (no Domain layer violation — `AccountChartEntry` already in Domain from W2)

## Review Findings (2026-07-04 — applied in FIX plan)

### CRITICAL findings (now fixed)
- **C1:** Seeder not called from startup → FIX-2: Added CleanupAsync + SeedAsync call in Program.cs startup scope
- **C2:** `AccountChartService` depended on `VanAnDbContext` (not registered in ShopERP DI) → FIX-1: Changed to `IVanAnDbContext`
- **C3:** `IVanAnDbContext` + `ShopERPDbContext` missing `AccountCharts` DbSet → FIX-1: Added DbSet to both
- **C4:** TT 133 had 4 WRONG accounts (311 removed, 213 is sub-account, 641 is sub-account, 521 removed) → FIX-3: Removed + added 18 missing
- **C5:** TT 58/2026 has NO chart of accounts ("bỏ hoàn toàn HTTK, thay bằng sổ đơn giản hóa") → FIX-5: Removed TT 58 from seeder

### F8 update
- **Original:** "include new TK 215 + new TK 33x (verify exact code during seed)"
- **Verified:** TK 332 "Phải trả cổ tức, lợi nhuận" (NEW in TT 99, split from 338 — source: ketoan.vn)
- **Applied:** FIX-4 added TK 332 to TT 99

### F9 clarification
- TK 521 exists in **TT 99 only** (NOT TT 133 — removed). W4 Net Revenue = 511 Credit − 521 Debit applies to TT 99 reports.
- TT 133 discounts go directly to 511 (per W1 seeder note #3).

### Account counts (final)
- TT 133: 49 accounts (47 level-1 + 2 level-2: 3331, 1331)
- TT 99: 73 accounts (71 level-1 + 2 level-2: 3331, 1331)
- TT 58: 0 accounts (no chart of accounts — FIX-5)
- **Total: 122 accounts across 2 standards**

## Rollback
- Git revert
- GetAccountName fallback to hardcoded if needed

## Open Questions
- Q1: DB table hay in-memory? — **RESOLVED (J4): DB table** `AccountCharts` (reference data, no tenant scope)
- Q2: TT 99 phụ lục TK? — **RESOLVED (INVESTIGATE):** 71 accounts cấp 1 found, key changes documented (112 rename, 215 new, 161/417/441/461/466/611/631 removed)
- Q3: Có cần migration? — **RESOLVED: Yes** (new `AccountCharts` table)
- Q4: HKD→DN mapping keys đủ không? — **RESOLVED:** Mapping uses semantic keys (Revenue, COGS...), HKD dict uses codes — different purposes, don't merge
- Q5: HKD standard not in enum? — **RESOLVED (rec c):** Keep HKD dict separate, do NOT refactor `HKDBookService.GetAccountName`. Fix label bugs instead (W3-T6)
