# TASK CARD: [SPRINT A] — AccountCode + Vendor/Category/Reference Field Wiring

## 1. GOAL & CONTEXT

- **Mục tiêu cốt lõi:** Wire các fields `AccountCode`, `Vendor`, `Category`, `Reference` từ API request DTO xuống `AccountingEntryService` và lưu vào DB — hiện tại các fields này bị drop hoàn toàn trước khi đến service.
- **Nghiệp vụ áp dụng:** Sổ sách kế toán HKD (TT 152/2025/TT-BTC) — mỗi bút toán thủ công phải ghi rõ mã tài khoản (511 doanh thu, 621 giá vốn, v.v.), nhà cung cấp, danh mục chi phí. Thiếu thông tin này làm sổ sách không hợp lệ.
- **Root cause đã verify (2026-06-20):**
  - `CreateRevenueEntryRequest` chỉ có `{TenantId, Year, Month, Amount, Currency, Description}` — **thiếu `AccountCode`**
  - `CreateExpenseEntryRequest` chỉ có `{TenantId, Year, Month, Amount, Currency, Description}` — **thiếu `Vendor`, `Category`, `Reference`**
  - `IAccountingService.CreateRevenueEntryAsync(tenantId, period, amount, description)` — 4 params, không có `accountCode`
  - `RevenueEntry.razor` đọc `accountCode` từ form, gọi `AccountingService.CreateRevenueEntryAsync()` — `accountCode` bị drop trước khi đến service
  - `ExpenseEntry.razor` đọc `vendor/category/reference` từ form — tất cả bị drop trước khi đến service
  - `AccountingEntryDto` (`1_Shared/DTOs/AccountingEntryDto.cs`) **đã có** `AccountCode`, `Vendor`, `Category`, `Reference` ✅

## 2. ACTIVE WORKFLOW ROUTING

- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** ANALYZE → IMPLEMENT (User approval required trước Domain change)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

**Files được phép đọc/sửa:**
| File | Action | Lý do |
|---|---|---|
| `docs/AI/project_state.md` | Read | Bắt buộc đầu phiên |
| `2_Gateway/Controllers/AccountingEntriesController.cs` | **Edit** | Thêm fields vào request DTOs |
| `3_CoreHub/Services/IAccountingService.cs` | **Edit** | Thêm `accountCode` param vào signatures |
| `3_CoreHub/Services/AccountingEntryService.cs` | **Edit** | Map fields xuống entry/DTO |
| `1_Shared/DTOs/AccountingEntryDto.cs` | Read only | Đã có fields — reference |
| `1_Shared/Domain.cs` | ⚠️ Chỉ đọc | Verify `AccountingEntry` entity — xem §4 Domain Constraint |
| `5_WebApps/ShopERP/Components/Pages/Accounting/RevenueEntry.razor` | Read only | Verify form field names đã đúng — không sửa UI |
| `5_WebApps/ShopERP/Components/Pages/Accounting/ExpenseEntry.razor` | Read only | Verify form field names — không sửa UI |

**Boundary Rules — Nghiêm cấm:**
- CẤM sửa `RevenueEntry.razor` / `ExpenseEntry.razor` (UI đã đọc đúng — không cần thay đổi)
- CẤM thêm field mới vào `AccountingEntry` Domain entity mà không có Tech Lead approval (xem §4)
- CẤM sửa `AccountingEntryDto` (đã đúng — không sửa)
- CẤM refactor `IHKDBookService`, `IReversalService` (ngoài scope)

## 4. DOMAIN CONSTRAINT & DECISION REQUIRED ⚠️

**Vấn đề:** `AccountingEntry` entity trong `1_Shared/Domain.cs` **không có `AccountCode` field**.
- Entity hiện có: `Amount`, `EntryType`, `VatRate`, `TransactionDate`, `Description`, `ReferenceId`, `ReferenceType`
- Entity **thiếu:** `AccountCode`, `Vendor`, `Category`, `Reference`

Đây là **Domain Modeling Defect (DMD-1)** — phải chọn 1 trong 2 hướng:

### Option Y — Domain Fix (Recommended, cần Tech Lead approval)
Thêm vào `AccountingEntry` constructor và entity:
```csharp
public string? AccountCode { get; }
public string? Vendor { get; }
public string? Category { get; }
public string? Reference { get; }
```
Cập nhật `CreateRevenue()` và `CreateExpense()` factory methods nhận thêm params.
**Pros:** Clean architecture — data tồn tại trong Domain, không gap.
**Cons:** Domain change — cần approval, cần EF Core migration.

### Option X — Workaround (Không cần approval, implement ngay)
- Lưu `AccountCode`, `Vendor`, `Category`, `Reference` **chỉ trong** `AccountingEntryDto` (response DTO).
- Trong `AccountingEntryService`: tạo entry Domain object như cũ (không có fields này), sau khi lưu DB → enrich DTO từ request data.
- Dùng `ReferenceType` field đã có trong entity để encode `AccountCode` (hack).
**Pros:** Không cần approval, implement ngay.
**Cons:** Domain và DB thiếu data — không thể query/filter theo AccountCode trong DB.

**→ STOP. Chờ User quyết định Option X hay Option Y trước khi IMPLEMENT A-1.**

## 5. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)

- [ ] **Domain Purity:** Không tự ý sửa Domain để fix Service issues — phải có approval.
- [ ] **Immutability:** `AccountingEntry` append-only — mọi thay đổi chỉ thêm field mới, không sửa existing factory methods.
- [ ] **Backward compat:** `IAccountingService.CreateRevenueEntryAsync()` signature thay đổi → mọi callers phải update (kiểm tra `OrderService.cs` line 105 cũng gọi method này).
- [ ] **Legal Standards:** TT 152/2025/TT-BTC — mã tài khoản (AccountCode) là bắt buộc cho sổ sách HKD hợp lệ.
- [ ] **Build Gate:** `dotnet build VanAn.sln --configuration Release` → 0 errors.

## 6. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)

### Task A-1: Wire AccountCode

- [ ] **SC1:** `CreateRevenueEntryRequest` (trong `AccountingEntriesController.cs`) có thêm `public string AccountCode { get; set; } = string.Empty;`
- [ ] **SC2:** `IAccountingService.CreateRevenueEntryAsync()` nhận thêm param `string accountCode`
- [ ] **SC3:** `AccountingEntryService.CreateRevenueEntryAsync()` lưu `accountCode` vào entry (Option Y: Domain field; Option X: DTO enrich)
- [ ] **SC4:** `OrderService.cs` — caller của `CreateRevenueEntryAsync()` vẫn build (update call site với `accountCode: "511"` default)

### Task A-2: Wire Vendor/Category/Reference

- [ ] **SC5:** `CreateExpenseEntryRequest` có thêm `public string? Vendor`, `string? Category`, `string? Reference`
- [ ] **SC6:** `IAccountingService.CreateExpenseEntryAsync()` nhận thêm 3 params optional: `string? vendor = null`, `string? category = null`, `string? reference = null`
- [ ] **SC7:** `AccountingEntryService.CreateExpenseEntryAsync()` map 3 fields xuống DTO (và Domain nếu Option Y)
- [ ] **SC8:** Existing callers của `CreateExpenseEntryAsync()` vẫn build (optional params nên backward compat)

### Build & Quality

- [ ] **SC9:** `dotnet build VanAn.sln --configuration Release` → 0 errors
- [ ] **SC10:** `guard-check.ps1` → PASS
- [ ] **SC11:** Không có compilation errors trong `OrderService.cs` (đang gọi `CreateRevenueEntryAsync` + `CreateExpenseEntryAsync`)

## 7. AI HEALTH CHECK MATRIX

**Evidence Count:** 6 verified facts, 0 assumptions

**Verified Facts (2026-06-20):**
- Fact 1: `CreateRevenueEntryRequest` — chỉ có `{TenantId, Year, Month, Amount, Currency, Description}` (đã đọc file)
- Fact 2: `CreateExpenseEntryRequest` — chỉ có `{TenantId, Year, Month, Amount, Currency, Description}` (đã đọc file)
- Fact 3: `IAccountingService.CreateRevenueEntryAsync()` signature — 4 params: `(TenantId, AccountingPeriod, decimal, string)` (đã đọc file)
- Fact 4: `AccountingEntry` entity không có `AccountCode`, `Vendor`, `Category`, `Reference` fields (đã đọc Domain.cs)
- Fact 5: `AccountingEntryDto` đã có `AccountCode`, `Vendor`, `Category`, `Reference` (đã đọc file)
- Fact 6: `RevenueEntry.razor` đọc `accountCode` từ form nhưng gọi service không truyền giá trị (đã đọc file)

**Open Questions:**
- Q1: **Option X hay Option Y cho DMD-1?** (User phải quyết định — xem §4) → **BLOCK: không implement A-1 cho đến khi Q1 resolve**
- Q2: Có EF Core migration cần thiết nếu chọn Option Y không? (Cần verify DbContext và migration history)

**Recommended Action:** INVESTIGATE Q1 + Q2 — chờ User approve trước IMPLEMENT.

## 8. REVERSE IMPACT ANALYSIS

| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `IAccountingService.CreateRevenueEntryAsync()` signature | `OrderService.cs:105` + `OrderService.cs:245` cũng gọi method này → compile error | Update call sites ngay trong cùng commit |
| `CreateExpenseEntryRequest` thêm fields | Backward compat tốt (optional params) — không break existing calls | Verify bằng build |
| Domain.cs thêm fields (Option Y) | EF Core cần migration | Tạo migration trong `3_CoreHub/Infrastructure/Migrations/` |
| `AccountingEntry` constructor thêm params (Option Y) | Tất cả `CreateRevenue()` / `CreateExpense()` call sites cần update | `OrderService.cs` cần update |

## 9. TDD & E2E TESTING STRATEGY

**Unit tests BẮT BUỘC (AccountingEntryService):**
- Test: `CreateRevenueEntry` với `accountCode = "511"` → entry có `AccountCode = "511"` trong response DTO
- Test: `CreateExpenseEntry` với `vendor = "Nhà CC ABC"`, `category = "materials"` → entry DTO có đúng values
- Test: Backward compat — `CreateRevenueEntry` không truyền `accountCode` → không crash (nếu optional)

**E2E (không bắt buộc Sprint A — UI đã đọc đúng):**
- Verify via existing `accounting-flow.spec.ts` — API smoke test `POST /api/accounting/revenue` không 4xx/5xx

## 10. JIT PLANNING + PURE EXECUTION

### Micro-phase breakdown Sprint A

| Micro-phase | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **MP-A1** | Đọc `IAccountingService.cs` + `AccountingEntryService.cs` + `OrderService.cs` caller lines → chốt: signature mới, call site update list | Sửa `IAccountingService.cs` signature + `AccountingEntryService.cs` impl + update `OrderService.cs` call sites |
| **MP-A2** | Đọc `AccountingEntriesController.cs` DTOs + `AccountingEntryService.CreateExpenseEntryAsync()` → chốt: 3 optional fields, mapping | Thêm fields vào `CreateExpenseEntryRequest` + update service impl |
| **MP-A3** | (Chỉ nếu Option Y) Đọc `Domain.cs` AccountingEntry constructor + existing migration → chốt: fields cần thêm, migration name | Thêm fields vào Domain entity + tạo EF migration |
| **MP-A4** | Đọc `AccountingEntriesController.cs` request handler → chốt: đủ fields pass xuống service | Cập nhật controller `CreateRevenueEntry` + `CreateExpenseEntry` actions |

### Rules
- JIT Planning: MAX 10 phút đọc per micro-phase
- Pure Execution: KHÔNG re-read — chỉ viết code theo plan
- Sau MP-A1: run `dotnet build` ngay → sửa compile errors trước khi sang MP-A2

---

**Implementation Date:** _(để trống — điền khi bắt đầu)_
**Branch:** `feat/sprint-a-accountcode-fields`
**Depends on:** `main` branch clean (Sprint trước merged)
**Status:** ⬜ AWAITING User decision Q1 (Option X vs Y)
