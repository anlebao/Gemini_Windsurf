

# ARCHIVED: task-sprint-a-accountcode-fields.md

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


# ARCHIVED: task-sprint-b-entry-timing.md

# TASK CARD: [SPRINT B] — Accounting Entry Timing Fix (CreateOrder → PaymentWebhook)

## 1. GOAL & CONTEXT

- **Mục tiêu cốt lõi:** Đảm bảo `AccountingEntry` (Revenue + COGS) chỉ được tạo **sau khi ngân hàng xác nhận thanh toán** — không phải khi order được tạo.
- **Nghiệp vụ áp dụng:** Nguyên tắc thực thu (cash-basis accounting) theo TT 152/2025/TT-BTC — doanh thu chỉ được ghi nhận khi tiền thực sự về tài khoản. Ghi nhận trước thanh toán là vi phạm kế toán nghiêm trọng.
- **Root cause đã verify (2026-06-20):**
  - `OrderService.CreateOrderFromCommandAsync()` line 80: `await GenerateAccountingEntriesAsync(newOrder, tenant);` — gọi ngay sau tạo order, trước bất kỳ payment confirmation
  - `WebhookController.cs` hiện chỉ xử lý **e-invoice webhook** (Viettel/MISA, extract `invoiceNo`) — không có payment webhook handler
  - Hệ quả: nếu khách quét QR nhưng không thanh toán (cancel, timeout) → `AccountingEntry` Revenue + COGS vẫn tồn tại trong sổ → báo cáo tài chính sai

## 2. ACTIVE WORKFLOW ROUTING

- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** ANALYZE → IMPLEMENT (User approval required cho architecture decision)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

**Files được phép đọc/sửa:**
| File | Action | Lý do |
|---|---|---|
| `docs/AI/project_state.md` | Read | Bắt buộc đầu phiên |
| `3_CoreHub/Services/OrderService.cs` | **Edit** | Remove unconditional `GenerateAccountingEntriesAsync` call (line 80) |
| `3_CoreHub/Services/IOrderService.cs` | **Edit** | Thêm `ConfirmPaymentAsync()` method |
| `2_Gateway/Controllers/WebhookController.cs` | **Edit** | Thêm `POST /api/webhooks/payment` endpoint |
| `3_CoreHub/Services/IAccountingService.cs` | Read only | Reference cho `GenerateAccountingEntriesAsync` |
| `2_Gateway/Hubs/OrderHub.cs` | Read only | Verify SignalR hub có thể notify sau payment confirm |
| `3_CoreHub/Services/KitchenService.cs` | Read only | Verify kitchen workflow (sau payment confirm, bếp cần biết) |
| `6_Tests/` | **Edit** | Unit tests cho timing logic |

**Boundary Rules — Nghiêm cấm:**
- CẤM sửa `AccountingEntry` entity (immutable — append-only)
- CẤM thêm `PaymentStatus` vào `AccountingEntry` (immutability violation)
- CẤM sửa `IHKDBookService` hay `IReversalService`
- CẤM sửa các Razor pages (KhachLink / ShopERP) trong sprint này
- `GenerateAccountingEntriesAsync` là private method trong `OrderService` — không expose qua interface

## 4. ARCHITECTURE DECISION REQUIRED ⚠️

Có **3 options** cho timing fix. User phải approve trước IMPLEMENT:

### Option A — Payment Webhook Endpoint (Recommended)
Thêm `POST /api/webhooks/payment` vào `WebhookController.cs`.
KhachLink sau khi nhận confirm từ VietQR/bank → call endpoint này.
Controller gọi `IOrderService.ConfirmPaymentAsync(orderId, tenantId)` → `GenerateAccountingEntriesAsync`.

```
KhachLink (nhận bank callback)
  ↓ POST /api/webhooks/payment { orderId, transactionId, confirmedAt }
WebhookController.ConfirmPayment()
  ↓
IOrderService.ConfirmPaymentAsync(orderId, tenantId)
  ↓
GenerateAccountingEntriesAsync(order, tenantId)
  ↓
AccountingEntry (Revenue + COGS) created ✅
```

**Pros:** Clean separation — payment webhook tách khỏi e-invoice webhook. Easy to test.
**Cons:** Cần KhachLink thêm logic gọi payment webhook sau QR payment confirm (ngoài scope Sprint B).
**Auth concern:** Payment callback từ bank là external → cần `[AllowAnonymous]` + signature verification.

### Option B — OrderService Internal State
Trong `OrderService`, không gọi `GenerateAccountingEntriesAsync` trong `CreateOrderFromCommandAsync`.
Thêm `ConfirmPaymentAsync(Guid orderId, TenantId tenantId)` vào `IOrderService`.
`OrdersController` gọi `ConfirmPaymentAsync` khi nhận confirm từ VietQR callback.

```
OrdersController.ConfirmPayment()  [POST /api/orders/{id}/confirm-payment]
  ↓
IOrderService.ConfirmPaymentAsync()
  ↓
GenerateAccountingEntriesAsync(order, tenantId)
```

**Pros:** Không cần thêm endpoint vào `WebhookController` — gọn hơn.
**Cons:** Payment confirm đặt vào `OrdersController` thay vì WebhookController — không đúng semantic.

### Option C — Deferred (Không recommend)
Giữ nguyên timing, thêm `AccountingEntry.Status = Pending` flag — chuyển `Posted` sau khi webhook xác nhận.
**Cons:** Vi phạm immutability của `AccountingEntry` — **HARD STOP**.

**→ STOP. Chờ User quyết định Option A hay Option B trước khi IMPLEMENT.**

## 5. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)

- [ ] **Immutability:** `AccountingEntry` vẫn immutable — không thêm status/mutable fields.
- [ ] **Domain Purity:** `ConfirmPaymentAsync` logic ở Service layer — không ở Controller.
- [ ] **Multi-tenancy:** Payment confirm phải truyền `TenantId` đúng — không hardcode.
- [ ] **Auth:** Payment webhook endpoint cần thiết kế auth rõ ràng: (a) bank callback = AllowAnonymous + HMAC verify, hoặc (b) internal call = RequireTenantAccess.
- [ ] **Idempotency:** `ConfirmPaymentAsync` phải idempotent — gọi 2 lần cho cùng `orderId` không tạo 2 bộ entries.
- [ ] **Legal:** TT 152/2025/TT-BTC — doanh thu ghi nhận theo thực thu.
- [ ] **Build Gate:** `dotnet build VanAn.sln --configuration Release` → 0 errors.

## 6. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)

### Task B-1a: Guard trong OrderService

- [ ] **SC1:** `OrderService.CreateOrderFromCommandAsync()` không còn gọi `GenerateAccountingEntriesAsync()` unconditionally
- [ ] **SC2:** Order được tạo thành công mà **không** tạo accounting entry (verify: sau `CreateOrder`, query `AccountingEntry` → empty cho orderId này)
- [ ] **SC3:** `OrderService.cs` line 80 và line 245 — `GenerateAccountingEntriesAsync` chỉ được gọi từ `ConfirmPaymentAsync`

### Task B-1b: Payment Webhook Endpoint (Option A) / Confirm Payment Endpoint (Option B)

- [ ] **SC4:** Endpoint tồn tại và nhận payload `{ orderId: Guid, transactionId: string, confirmedAt: DateTime }`
- [ ] **SC5:** Endpoint validate `orderId` tồn tại trong DB trước khi tiến hành
- [ ] **SC6:** Endpoint idempotent — gọi 2 lần với cùng `orderId` → 2nd call trả error/noop, không tạo duplicate entries

### Task B-1c: Wire Payment Confirm → Accounting Entry Generation

- [ ] **SC7:** `IOrderService` có method `ConfirmPaymentAsync(Guid orderId, TenantId tenantId, CancellationToken ct)`
- [ ] **SC8:** `OrderService.ConfirmPaymentAsync()` gọi `GenerateAccountingEntriesAsync(order, tenantId)`
- [ ] **SC9:** Sau `ConfirmPaymentAsync()` → `AccountingEntry` Revenue + COGS được tạo (verify qua unit test)
- [ ] **SC10:** `OrderService.ConfirmPaymentAsync()` cập nhật `Order.PaymentStatus = Paid` (nếu có field này) — hoặc log payment confirm event

### Unit Tests

- [ ] **SC11:** Unit test: `CreateOrderFromCommandAsync()` không tạo `AccountingEntry` → repo.GetEntriesByOrder() returns empty
- [ ] **SC12:** Unit test: `ConfirmPaymentAsync()` → `AccountingEntry` Revenue + COGS được tạo cho đúng `orderId`
- [ ] **SC13:** Unit test: `ConfirmPaymentAsync()` gọi 2 lần → idempotency — second call không tạo duplicate entry

### Build & Quality

- [ ] **SC14:** `dotnet build VanAn.sln --configuration Release` → 0 errors
- [ ] **SC15:** `guard-check.ps1` → PASS

## 7. AI HEALTH CHECK MATRIX

**Evidence Count:** 5 verified facts, 2 open questions

**Verified Facts (2026-06-20):**
- Fact 1: `OrderService.cs:80` — `await GenerateAccountingEntriesAsync(newOrder, tenant);` gọi trong `CreateOrderFromCommandAsync()` (đã đọc file)
- Fact 2: `OrderService.cs:245` — cũng có `await GenerateAccountingEntriesAsync(savedOrder, tenant);` (second call site — cần check context)
- Fact 3: `WebhookController.cs` — chỉ có `ReceiveWebhook` handler cho e-invoice, không có payment webhook (đã đọc file)
- Fact 4: `IPeriodClosingService.GetPeriodStatusAsync()` đã tồn tại (có thể reuse pattern)
- Fact 5: `IOrderService` interface tồn tại — cần verify current method list trước khi thêm `ConfirmPaymentAsync`

**Open Questions:**
- Q1: **Option A hay Option B?** (User phải decide) → **BLOCK implement B-1b đến khi Q1 resolve**
- Q2: `OrderService.cs:245` — second call site ở context nào? (Cần đọc để biết có phải safe to remove không)
- Q3: Có `Order.PaymentStatus` field không? (Cần check Domain.cs — nếu có thì update khi confirm, nếu không thì chỉ trigger accounting)

**Recommended Action:** Resolve Q2/Q3 trong JIT Planning phase. Q1 phải có User approval.

## 8. REVERSE IMPACT ANALYSIS

| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| Remove `GenerateAccountingEntries` từ `CreateOrderFromCommandAsync` | Existing orders (đã create nhưng chưa pay) sẽ KHÔNG có accounting entry → **sổ sách bị lỗ hổng cho orders cũ** | Document: fix chỉ áp dụng cho orders mới; cần data migration script riêng cho orders cũ |
| `IOrderService` thêm method | Implementation `OrderService` phải implement → compile error nếu quên | Implement ngay trong cùng commit |
| `WebhookController` thêm endpoint | Route conflict nếu đặt tên trùng — check hiện có `/api/webhooks/{provider}` | Đặt route riêng: `/api/webhooks/payment` (không dùng `{provider}` wildcard) |
| `OrderHub.cs` notify sau payment (nếu Option A mở rộng) | KhachLink + ShopERP nhận event mới | Out of scope Sprint B — defer đến P1-3 |

## 9. TDD & E2E TESTING STRATEGY

**Unit tests BẮT BUỘC (TDD — viết test trước):**
```
Test 1: CreateOrder_DoesNotCreateAccountingEntry
  Arrange: mock IAccountingService
  Act: orderService.CreateOrderFromCommandAsync(command, tenantId)
  Assert: accountingService.CreateRevenueEntryAsync was NEVER called

Test 2: ConfirmPayment_CreatesAccountingEntries
  Arrange: existing order in repo, mock IAccountingService
  Act: orderService.ConfirmPaymentAsync(orderId, tenantId)
  Assert: accountingService.CreateRevenueEntryAsync was called ONCE
          accountingService.CreateExpenseEntryAsync was called ONCE

Test 3: ConfirmPayment_Idempotent
  Arrange: existing order, first confirm already done
  Act: orderService.ConfirmPaymentAsync(orderId, tenantId) twice
  Assert: No duplicate entries (second call is noop or exception)
```

**E2E (post-Sprint B):**
- `order-flow.spec.ts`: verify order creation không trigger accounting entry
- `accounting-flow.spec.ts`: verify POST /api/webhooks/payment → accounting entries exist

## 10. JIT PLANNING + PURE EXECUTION

### Micro-phase breakdown Sprint B

| Micro-phase | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **MP-B1** | Đọc `IOrderService.cs` + `OrderService.cs` lines 75-100 + 240-250 → chốt: method list hiện tại, second call site context, safe to remove | Write unit test `CreateOrder_DoesNotCreateAccountingEntry` (TDD first — failing test) |
| **MP-B2** | Đọc `Order` entity trong `Domain.cs` → chốt: có `PaymentStatus` field không, `ConfirmPaymentAsync` signature | Thêm `ConfirmPaymentAsync` vào `IOrderService` + implement trong `OrderService` (guard line 80, add method) |
| **MP-B3** | Đọc `WebhookController.cs` routes + auth attributes → chốt: route convention, auth strategy cho payment endpoint | Thêm `POST /api/webhooks/payment` action vào `WebhookController` |
| **MP-B4** | Run unit tests → verify MP-B1 test now passes | Run `dotnet build` + run unit tests |

### Rules
- JIT Planning: MAX 10 phút đọc per micro-phase
- TDD: viết test TRƯỚC implement (MP-B1 viết test → MP-B2 implement → test PASS)
- Domain modification: CẤM — dùng existing domain fields

---

**Implementation Date:** _(để trống — điền khi bắt đầu)_
**Branch:** `feat/sprint-b-entry-timing`
**Depends on:** Sprint A merged to `main` + User approval Q1 (Option A/B)
**Status:** ⬜ AWAITING User approval Q1 (Option A vs Option B)


# ARCHIVED: task-sprint-c-service-guards.md

# TASK CARD: [SPRINT C] — Service Layer Guards (Duplicate Detection + Period Closing + COGS)

## 1. GOAL & CONTEXT

- **Mục tiêu cốt lõi:** Thêm 2 server-side guards vào `AccountingEntryService`: (1) chặn duplicate entries trong 5 phút, (2) chặn entries vào kỳ đã đóng sổ. Task C-3 (COGS từ CostPrice) bị blocked do Domain Modeling Defect.
- **Nghiệp vụ áp dụng:**
  - **C-1:** Ngăn nhập kép do lỗi người dùng hoặc double-click (TT 152/2025 — sổ sách phải chính xác).
  - **C-2:** Kỳ đã đóng sổ là immutable về mặt kế toán — không thể thêm entry mới (vi phạm audit trail).
  - **C-3:** COGS tính 70% hardcode không phản ánh đúng giá vốn thực tế — cần `Product.CostPrice`.
- **Root cause đã verify (2026-06-20):**
  - `AccountingEntryService.CreateRevenue/ExpenseEntryAsync()` không có duplicate check — client-only (`_recentEntries` list trong Razor)
  - `AccountingEntryService` không inject `IPeriodClosingService` — không check period status trước khi tạo entry
  - `OrderService.cs:119`: `decimal cogsAmount = order.TotalPrice * 0.7m; // Assume 70% COGS for MVP`
  - `Product` entity trong `Domain.cs` không có `CostPrice` field → **Domain Modeling Defect DMD-2**

## 2. ACTIVE WORKFLOW ROUTING

- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** ANALYZE → IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

**Files được phép đọc/sửa:**

| File | Action | Lý do |
|---|---|---|
| `docs/AI/project_state.md` | Read | Bắt buộc đầu phiên |
| `3_CoreHub/Services/AccountingEntryService.cs` | **Edit** | Thêm duplicate check + period closing guard |
| `3_CoreHub/Services/IAccountingService.cs` | **Edit** | Có thể cần inject `IPeriodClosingService` qua constructor |
| `3_CoreHub/Services/IPeriodClosingService.cs` | Read only | Verify `GetPeriodStatusAsync()` signature |
| `3_CoreHub/Repositories/IAccountingEntryRepository.cs` | Read only | Verify có query method phù hợp cho duplicate check |
| `3_CoreHub/Services/OrderService.cs` | ⚠️ C-3 only (BLOCKED) | Line 119 — COGS hardcode — chỉ đọc nếu C-3 unblocked |
| `1_Shared/Domain.cs` | ⚠️ C-3 only (BLOCKED) | `Product` entity — chỉ sửa nếu C-3 approved |
| `6_Tests/` | **Edit** | Unit tests cho guards |

**Boundary Rules — Nghiêm cấm:**
- CẤM sửa `AccountingEntry` entity (immutable)
- CẤM thêm `CostPrice` vào `Product` entity mà không có Tech Lead approval (C-3 blocked)
- CẤM implement C-3 trước khi có approval
- CẤM sửa KhachLink hay ShopERP Razor pages trong sprint này
- CẤM thêm duplicate check vào Gateway Controller layer (business logic phải ở Service layer)

## 4. DOMAIN CONSTRAINT (C-3) ⚠️ BLOCKED

**Domain Modeling Defect DMD-2:** `Product` entity không có `CostPrice` field.

Để fix `OrderService.cs:119` (`order.TotalPrice * 0.7m`):
1. Cần thêm `public decimal CostPrice { get; protected set; }` vào `Product` entity
2. Cần EF Core migration
3. Cần update Product seeding/creation logic
4. Cần update `OrderService.GenerateAccountingEntriesAsync()` — `COGS = SUM(item.Quantity × product.CostPrice)`

**→ C-3 BLOCKED cho đến khi Tech Lead approve thêm `Product.CostPrice` vào `1_Shared/Domain.cs`.**

Sprint C sẽ implement **C-1 + C-2 only**. C-3 nhận status `BLOCKED` và sẽ tạo task card riêng sau khi domain change được approve.

## 5. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)

- [ ] **Domain Purity:** Duplicate check và period guard là pure Service layer — không ở Controller, không ở Domain.
- [ ] **Immutability:** `AccountingEntry` append-only — guard chỉ throw exception TRƯỚC khi tạo entry, không modify entries đã tồn tại.
- [ ] **Performance:** Duplicate check query phải efficient — filter theo `TenantId + Amount + TransactionDate >= now.AddMinutes(-5)` có index.
- [ ] **Error clarity:** Exceptions phải có message rõ ràng để UI display đúng: `"Bút toán trùng lặp trong 5 phút vừa qua"`, `"Kỳ kế toán đã đóng sổ — không thể thêm bút toán mới"`.
- [ ] **Circular dependency:** `AccountingEntryService` inject `IPeriodClosingService` — verify không có circular DI (PeriodClosingService có inject IAccountingService không?).
- [ ] **Legal:** TT 152/2025/TT-BTC — kỳ đóng sổ là locked period, không cho phép thêm/sửa bút toán.
- [ ] **Build Gate:** `dotnet build VanAn.sln --configuration Release` → 0 errors.

## 6. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)

### Task C-1: Server-side Duplicate Detection

- [ ] **SC1:** `AccountingEntryService.CreateRevenueEntryAsync()` query: tìm entry có cùng `TenantId + Amount + AccountCode + TransactionDate.Date` trong vòng 5 phút trước → nếu found → throw `DuplicateEntryException` (hoặc `InvalidOperationException` với message rõ)
- [ ] **SC2:** `AccountingEntryService.CreateExpenseEntryAsync()` có cùng logic duplicate check
- [ ] **SC3:** Duplicate window là 5 phút (configurable via constant `DuplicateWindowMinutes = 5`)
- [ ] **SC4:** Unit test: tạo 2 entries giống nhau trong 5 phút → second call throws exception
- [ ] **SC5:** Unit test: tạo 2 entries giống nhau nhưng cách nhau > 5 phút → cả 2 entries đều được tạo thành công (không false-positive)
- [ ] **SC6:** Unit test: tạo 2 entries cùng `Amount` nhưng khác `AccountCode` → cả 2 thành công (khác account = không duplicate)

### Task C-2: Period Closing Guard

- [ ] **SC7:** `AccountingEntryService` inject `IPeriodClosingService` (constructor injection)
- [ ] **SC8:** `CreateRevenueEntryAsync()` gọi `IPeriodClosingService.GetPeriodStatusAsync(period, tenantId)` TRƯỚC khi tạo entry
- [ ] **SC9:** `GetPeriodStatusAsync()` trả `PeriodClosingStatus.Closed` → throw `InvalidOperationException("Kỳ kế toán {year}/{month} đã đóng sổ. Không thể thêm bút toán mới.")`
- [ ] **SC10:** `CreateExpenseEntryAsync()` có cùng period check
- [ ] **SC11:** Unit test: create entry vào kỳ `Closed` → exception với message đúng
- [ ] **SC12:** Unit test: create entry vào kỳ `Open` → entry được tạo thành công (không regression)
- [ ] **SC13:** Unit test: create entry vào kỳ `Open` sau khi một kỳ khác đã `Closed` → thành công (chỉ check period của entry, không phải period mới nhất)

### Task C-3 (BLOCKED)

- [ ] **SC14:** _(BLOCKED — Tech Lead approval required)_ `Product.CostPrice` thêm vào Domain.cs
- [ ] **SC15:** _(BLOCKED)_ `OrderService.GenerateAccountingEntriesAsync()` tính COGS từ `SUM(item.Quantity × product.CostPrice)` thay vì `order.TotalPrice * 0.7m`
- [ ] **SC16:** _(BLOCKED)_ Fallback: nếu `product.CostPrice == 0` → dùng `item.UnitPrice * 0.7m` (backward compat)

### Build & Quality

- [ ] **SC17:** `dotnet build VanAn.sln --configuration Release` → 0 errors
- [ ] **SC18:** `guard-check.ps1` → PASS
- [ ] **SC19:** `AccountingEntryService` không có circular DI (verify DI registration)

## 7. AI HEALTH CHECK MATRIX

**Evidence Count:** 5 verified facts, 2 open questions

**Verified Facts (2026-06-20):**
- Fact 1: `AccountingEntryService.CreateRevenueEntryAsync()` — không có duplicate check, không inject `IPeriodClosingService` (đã đọc file)
- Fact 2: `IPeriodClosingService` có `GetPeriodStatusAsync(period, tenantId, CancellationToken)` method (đã đọc file)
- Fact 3: `IAccountingEntryRepository` — cần verify có `GetByTenantAndDateRangeAsync()` cho duplicate query (đã thấy usage trong `GetTodayRevenueAsync`)
- Fact 4: `OrderService.cs:119` — `decimal cogsAmount = order.TotalPrice * 0.7m;` (đã đọc file)
- Fact 5: `Product` entity không có `CostPrice` field (đã đọc Domain.cs)

**Open Questions:**
- Q1: `PeriodClosingService` có inject `IAccountingService` không? (Cần check để avoid circular DI trước khi inject `IPeriodClosingService` vào `AccountingEntryService`)
- Q2: `IAccountingEntryRepository` có method nào trả entries theo `TenantId + Amount + DateRange` không, hay phải dùng `GetByTenantAndDateRangeAsync` rồi filter in-memory? (Performance implication)

**Recommended Action:** Resolve Q1/Q2 trong JIT Planning (đọc `PeriodClosingService.cs` + `IAccountingEntryRepository.cs`). Sau đó IMPLEMENT.

## 8. REVERSE IMPACT ANALYSIS

| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `AccountingEntryService` inject `IPeriodClosingService` | DI registration cần update trong `Program.cs` (Gateway + ShopERP) | Verify `IPeriodClosingService` đã registered — add if missing |
| Duplicate check: throw exception | Client code (Razor pages) cần handle exception gracefully | `RevenueEntry.razor` + `ExpenseEntry.razor` đã có try/catch — verify message display |
| Period closing guard: throw exception | Existing integration tests tạo entry vào past periods có thể fail | Review test data — đảm bảo test periods là Open |
| Circular DI risk | App crash at startup nếu circular | Check `PeriodClosingService` constructor trước khi inject |

## 9. TDD & E2E TESTING STRATEGY

**Unit tests BẮT BUỘC (TDD — viết test trước):**

```csharp
// C-1 Duplicate Detection Tests
[Fact]
async Task CreateRevenueEntry_DuplicateInWindow_ThrowsException()
{
    // Arrange: first entry created 2 min ago, same amount/accountCode
    // Act: create second entry with same amount/accountCode
    // Assert: throws DuplicateEntryException (or InvalidOperationException)
}

[Fact]
async Task CreateRevenueEntry_DuplicateOutsideWindow_Succeeds()
{
    // Arrange: first entry created 10 min ago
    // Act: create entry with same amount/accountCode
    // Assert: no exception — entry created
}

[Fact]
async Task CreateRevenueEntry_SameAmountDifferentAccountCode_Succeeds()
{
    // Arrange: entry with AccountCode "511" created 1 min ago
    // Act: create entry with AccountCode "515", same amount
    // Assert: no exception — different account = not duplicate
}

// C-2 Period Closing Guard Tests
[Fact]
async Task CreateRevenueEntry_ClosedPeriod_ThrowsInvalidOperation()
{
    // Arrange: period 2025/12 is Closed
    // Act: create entry for period 2025/12
    // Assert: throws InvalidOperationException with "đã đóng sổ" message
}

[Fact]
async Task CreateRevenueEntry_OpenPeriod_Succeeds()
{
    // Arrange: period 2026/06 is Open
    // Act: create entry for period 2026/06
    // Assert: no exception — entry created
}
```

**E2E (post-Sprint C):**
- Không bắt buộc — server-side guards verifiable via unit tests
- Optional: `accounting-flow.spec.ts` — verify API returns 400/422 for duplicate entry

## 10. JIT PLANNING + PURE EXECUTION

### Micro-phase breakdown Sprint C

| Micro-phase | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **MP-C1** | Đọc `IPeriodClosingService.cs` + `PeriodClosingService.cs` constructor → chốt: có circular DI không, `GetPeriodStatusAsync` return type | Write unit tests C-2 (TDD first — failing) |
| **MP-C2** | Đọc `IAccountingEntryRepository.cs` + `AccountingEntryService.GetTodayRevenueAsync()` → chốt: query method available, duplicate check query design | Write unit tests C-1 (TDD first — failing) |
| **MP-C3** | _(JIT đã done)_ | Implement C-2 period guard trong `AccountingEntryService` — inject `IPeriodClosingService`, add check trước create |
| **MP-C4** | _(JIT đã done)_ | Implement C-1 duplicate check trong `AccountingEntryService` — query + window logic |
| **MP-C5** | Đọc `Program.cs` (Gateway + ShopERP) → verify `IPeriodClosingService` registered | Fix DI registration nếu thiếu, run `dotnet build` + unit tests |

### Rules
- JIT Planning: MAX 10 phút đọc per micro-phase
- TDD: viết test trước mỗi implement step
- Resolve Q1 (circular DI) trong MP-C1 TRƯỚC khi implement bất cứ điều gì

---

## 11. C-3 UNBLOCK PATH (Sau khi Tech Lead approve)

Khi Tech Lead approve `Product.CostPrice`:

1. **Tạo task card riêng:** `task-sprint-c3-cogs-costprice.md`
2. **Domain change:**
   - Thêm `public decimal CostPrice { get; protected set; }` vào `Product` entity trong `Domain.cs`
   - Thêm `CostPrice` vào `Product()` constructor
3. **EF Core migration:** `Add-Migration AddProductCostPrice` trong `3_CoreHub`
4. **OrderService fix:**
   ```csharp
   // Thay dòng 119:
   decimal cogsAmount = order.Items.Sum(i => i.Quantity * (product?.CostPrice ?? i.UnitPrice * 0.7m));
   ```
5. **Product seeding:** Update dev seed data với `CostPrice` cho test products

---

**Implementation Date:** _(để trống — điền khi bắt đầu)_
**Branch:** `feat/sprint-c-service-guards`
**Depends on:** Sprint B merged to `main`
**Status:** ⬜ C-1/C-2 READY (resolve Q1 trong JIT Planning) | 🚫 C-3 BLOCKED (awaiting Domain approval)


# ARCHIVED: task-fix1-accounting-controller-tests.md

# TASK CARD: [FIX-1] — AccountingEntriesControllerTests JWT Claim Migration Fix

**Created:** 2026-06-19
**Priority:** P1 — Test suite integrity (9 failing tests blocking CI confidence)
**Effort:** LOW (~30 min, 1 file, no production code change)
**Branch:** `fix/test-jwt-tenantid-accounting-controller`

---

## 1. GOAL & CONTEXT

- **Mục tiêu cốt lõi:** Sửa 9/10 test trong `AccountingEntriesControllerTests.cs` đang fail do test dùng `X-Tenant-Id` HTTP header đã bị deprecated; và sửa 1 test assert message sai. Không thay đổi production code.
- **Nghiệp vụ áp dụng:** Wave 1 Phase 2 (Security) đã migrate `AccountingEntriesController` từ header-based TenantId sang JWT claim `tenant_id`. Tests không được cập nhật theo, dẫn đến `UnauthorizedObjectResult` trên tất cả requests vì `GetTenantIdFromClaim()` không tìm thấy claim trong `DefaultHttpContext`.

### Root Cause (đã verify)

```
Controller (production — đúng):
  GetTenantIdFromClaim() → reads User.FindFirst("tenant_id") from ClaimsPrincipal
  → Guid.Empty nếu không có claim → return Unauthorized({ error = "Tenant ID required in JWT claim" })

Tests (sai — chưa update):
  _controller.ControllerContext.HttpContext.Request.Headers["X-Tenant-Id"] = tenantId
  → DefaultHttpContext.User là anonymous, không có "tenant_id" claim
  → GetTenantIdFromClaim() = Guid.Empty → tất cả return Unauthorized
```

### Failing tests (9 tests đều return `UnauthorizedObjectResult`)

| Test | Expected | Actual |
|---|---|---|
| `CreateRevenueEntry_ShouldReturnCreated_WhenValidRequest` | `CreatedAtActionResult` | `UnauthorizedObjectResult` |
| `CreateExpenseEntry_ShouldReturnCreated_WhenValidRequest` | `CreatedAtActionResult` | `UnauthorizedObjectResult` |
| `GetEntryById_ShouldReturnOk_WhenEntryExists` | `OkObjectResult` | `UnauthorizedObjectResult` |
| `GetEntryById_ShouldReturnNotFound_WhenEntryDoesNotExist` | `NotFoundResult` | `UnauthorizedObjectResult` |
| `CreateReversalEntry_ShouldReturnCreated_WhenValidRequest` | `CreatedAtActionResult` | `UnauthorizedObjectResult` |
| `CreateReversalEntry_ShouldReturnBadRequest_WhenEntryCannotBeReversed` | `BadRequestObjectResult` | `UnauthorizedObjectResult` |
| `GetRevenueSummary_ShouldReturnSummary_WhenValidRequest` | `OkObjectResult` | `UnauthorizedObjectResult` |
| `GetProfitSummary_ShouldReturnSummary_WhenValidRequest` | `OkObjectResult` | `UnauthorizedObjectResult` |
| `GetEntryById_ShouldReturnUnauthorized_WhenTenantIdMissing` | message `"Tenant ID required"` | `{ error = "Tenant ID required in JWT claim" }` |

---

## 2. ACTIVE WORKFLOW ROUTING

- **Target Workflow:** `.devin/workflows/Fix_Errors.md`
- **Execution Mode:** FIX_ONLY

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

**Files được phép đọc/sửa:**

| File | Action | Lý do |
|---|---|---|
| `docs/AI/project_state.md` | Read | Bắt buộc đầu phiên |
| `6_Tests/VanAn.Core.Tests/Accounting/AccountingEntriesControllerTests.cs` | **Edit** | Fix JWT claim setup thay cho X-Tenant-Id header |
| `2_Gateway/Controllers/AccountingEntriesController.cs` | Read only | Verify `GetTenantIdFromClaim()` signature + message text |

**Boundary Rules — Nghiêm cấm:**
- CẤM sửa `AccountingEntriesController.cs` (production code đúng, chỉ test sai)
- CẤM sửa Domain layer
- CẤM thêm feature mới
- CẤM sửa bất kỳ file nào ngoài `AccountingEntriesControllerTests.cs`

---

## 4. TECHNICAL CONSTRAINTS

- [ ] **No production code change:** Chỉ sửa file test — controller logic đúng, test sai.
- [ ] **JWT Claim name:** Controller dùng dual-read: `"tenant_id"` (primary) hoặc `"TenantId"` (legacy). Test helper phải set `"tenant_id"`.
- [ ] **ClaimsIdentity setup:** Dùng `System.Security.Claims.ClaimsIdentity` + `ClaimsPrincipal` — không dùng mock auth middleware.
- [ ] **DefaultHttpContext:** `ControllerContext.HttpContext = new DefaultHttpContext()` → gán thêm `.User = new ClaimsPrincipal(identity)`.
- [ ] **Message assertion:** 1 test assert đúng message `"Tenant ID required in JWT claim"` (không phải `"Tenant ID required"`).

---

## 5. IMPLEMENTATION PLAN

### Step 1 — Thêm `using` statement

```csharp
using System.Security.Claims;
```

### Step 2 — Thêm private helper method vào test class

```csharp
/// <summary>
/// Sets up JWT tenant_id claim on the controller's HttpContext.
/// Replaces deprecated X-Tenant-Id header approach (Wave 1 Phase 2 migration).
/// </summary>
private void SetTenantClaim(Guid tenantId)
{
    var claims = new[] { new Claim("tenant_id", tenantId.ToString()) };
    var identity = new ClaimsIdentity(claims, "TestAuth");
    _controller.ControllerContext.HttpContext = new DefaultHttpContext
    {
        User = new ClaimsPrincipal(identity)
    };
}
```

### Step 3 — Thay thế header setup trong 8 test cases

Mỗi test có đoạn:
```csharp
// Old (stale — không hoạt động)
_controller.ControllerContext.HttpContext = new DefaultHttpContext();
_controller.ControllerContext.HttpContext.Request.Headers["X-Tenant-Id"] = request.TenantId.ToString();
```

Thay bằng:
```csharp
// New (correct — JWT claim)
SetTenantClaim(request.TenantId);
```

Các tests dùng `tenantId` thay vì `request.TenantId`:
```csharp
// Old
_controller.ControllerContext.HttpContext.Request.Headers["X-Tenant-Id"] = tenantId.ToString();
// New
SetTenantClaim(tenantId);
```

### Step 4 — Fix message assertion trong 1 test

Test: `GetEntryById_ShouldReturnUnauthorized_WhenTenantIdMissing`

```csharp
// Old (stale message)
Assert.Equal("Tenant ID required", unauthorizedResult.Value);

// New (match controller's actual message)
// Controller returns: new { error = "Tenant ID required in JWT claim" }
// Test phải dùng anonymous object hoặc dynamic để match
var value = unauthorizedResult.Value;
Assert.NotNull(value);
// Verify the error property matches
```

> **Note:** Controller trả `new { error = "..." }` — anonymous object. Test hiện assert `unauthorizedResult.Value` là string đơn giản. Cần đổi sang dynamic read hoặc serialize-compare.

---

## 6. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)

- [ ] **SC1:** `AccountingEntriesControllerTests` — 10/10 tests PASS (hiện 1/10)
- [ ] **SC2:** `CreateRevenueEntry_ShouldReturnCreated_WhenValidRequest` → `CreatedAtActionResult` ✅
- [ ] **SC3:** `CreateExpenseEntry_ShouldReturnCreated_WhenValidRequest` → `CreatedAtActionResult` ✅
- [ ] **SC4:** `GetEntryById_ShouldReturnOk_WhenEntryExists` → `OkObjectResult` ✅
- [ ] **SC5:** `GetEntryById_ShouldReturnNotFound_WhenEntryDoesNotExist` → `NotFoundResult` ✅
- [ ] **SC6:** `CreateReversalEntry_ShouldReturnCreated_WhenValidRequest` → `CreatedAtActionResult` ✅
- [ ] **SC7:** `CreateReversalEntry_ShouldReturnBadRequest_WhenEntryCannotBeReversed` → `BadRequestObjectResult` ✅
- [ ] **SC8:** `GetRevenueSummary_ShouldReturnSummary_WhenValidRequest` → `OkObjectResult` ✅
- [ ] **SC9:** `GetProfitSummary_ShouldReturnSummary_WhenValidRequest` → `OkObjectResult` ✅
- [ ] **SC10:** `GetEntryById_ShouldReturnUnauthorized_WhenTenantIdMissing` → asserts correct message ✅
- [ ] **SC11:** Không có production code nào bị thay đổi (verify `git diff --stat` chỉ có test file)
- [ ] **SC12:** `dotnet build VanAn.sln --configuration Release` → 0 errors
- [ ] **SC13:** `guard-check.ps1` → EXIT 0

---

## 7. AI HEALTH CHECK MATRIX

**Evidence Count:** 5 verified facts, 0 open questions

**Verified Facts:**
- Fact 1: `GetTenantIdFromClaim()` reads `User.FindFirst("tenant_id")` — confirmed by reading controller line 336 (2026-06-19)
- Fact 2: Controller error message là `new { error = "Tenant ID required in JWT claim" }` — confirmed line 52
- Fact 3: Tests set `Request.Headers["X-Tenant-Id"]` — confirmed by reading test file lines 81, 167 (2026-06-19)
- Fact 4: `DefaultHttpContext.User` là anonymous `ClaimsPrincipal` — không có claims → `GetTenantIdFromClaim()` = `Guid.Empty`
- Fact 5: Wave 1 Phase 2 committed at `c4d6acc` đã migrate controller sang JWT claim — tests không update theo

**Open Questions:** None

**Recommended Action:** IMPLEMENT — tất cả facts đủ, không có assumptions.

---

## 8. REVERSE IMPACT ANALYSIS

| Thay đổi | Impact | Mitigation |
|---|---|---|
| Thêm `SetTenantClaim()` helper | Chỉ trong test class, không ảnh hưởng production | N/A |
| Thay `Headers["X-Tenant-Id"]` → `SetTenantClaim()` | Test coverage chính xác hơn (đúng auth flow) | Verify tất cả 10 tests pass |
| Fix message assertion | Align test với actual contract | N/A |

**Không có reverse impact lên production code.**


# ARCHIVED: task-fix2-webhook-null-guard.md

# TASK CARD: [FIX-2] — WebhookService Null callbackData Guard

**Created:** 2026-06-19
**Priority:** P2 — Test suite integrity + defensive production behavior
**Effort:** TRIVIAL (~5 min, 1 line in production code, 0 test changes)
**Branch:** `fix/webhook-null-callbackdata-guard`

---

## 1. GOAL & CONTEXT

- **Mục tiêu cốt lõi:** Thêm `ArgumentNullException` guard cho `callbackData = null` trong `WebhookService.ProcessWebhookAsync()`. Sửa 1 failing test `ProcessWebhookAsync_NullCallbackData_ShouldThrowArgumentNullException`.
- **Nghiệp vụ áp dụng:** Webhook từ nhà cung cấp hóa đơn điện tử (Viettel/MISA) — null payload phải bị reject rõ ràng để tránh silent no-op trong audit trail.

### Root Cause (đã verify)

```
Test expects: ArgumentNullException khi callbackData = null
Thực tế:      ProcessWebhookAsync() không có null-guard cho callbackData
              → null chạy vào ParseWebhookPayload()
              → string.IsNullOrWhiteSpace(null) = true → return (null, null)
              → processingSucceeded = true → KHÔNG throw exception
```

**Signature hiện tại:**
```csharp
public async Task ProcessWebhookAsync(
    string providerId,
    string providerInvoiceNumber,
    string callbackData,          // non-nullable string — nhưng không guard null
    CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(providerId)) throw ...     // ✅ has guard
    if (string.IsNullOrWhiteSpace(providerInvoiceNumber)) throw ...  // ✅ has guard
    // ❌ NO guard for callbackData
```

**Failing test:**
```
VanAn.Core.Tests.Services.WebhookServiceTests
  .ProcessWebhookAsync_NullCallbackData_ShouldThrowArgumentNullException
  
Assert.Throws() Failure: No exception was thrown
Expected: typeof(System.ArgumentNullException)
```

### Why this is the correct production behavior

- `null` callbackData từ external provider = malformed/broken webhook request
- Silent no-op (current behavior) tạo false audit trail entry ("processed" but payload was null)
- Explicit `ArgumentNullException` = fail-fast, caller biết payload invalid
- Consistent với pattern của `providerId` và `providerInvoiceNumber` guards đã có

---

## 2. ACTIVE WORKFLOW ROUTING

- **Target Workflow:** `.devin/workflows/Fix_Errors.md`
- **Execution Mode:** FIX_ONLY

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

**Files được phép đọc/sửa:**

| File | Action | Lý do |
|---|---|---|
| `docs/AI/project_state.md` | Read | Bắt buộc đầu phiên |
| `3_CoreHub/Services/Orchestration/WebhookService.cs` | **Edit** | Thêm null-guard cho `callbackData` |
| `6_Tests/VanAn.Core.Tests/Services/WebhookServiceTests.cs` | Read only | Verify test không cần thay đổi |

**Boundary Rules — Nghiêm cấm:**
- CẤM sửa `WebhookServiceTests.cs` (test đúng, production code thiếu guard)
- CẤM thay đổi signature của `ProcessWebhookAsync`
- CẤM sửa bất kỳ interface `IWebhookService` nào
- CẤM thêm logic xử lý ngoài null-guard

---

## 4. TECHNICAL CONSTRAINTS

- [ ] **Minimal change:** 1 dòng thêm vào đầu `ProcessWebhookAsync`, trước các guards hiện có.
- [ ] **Exception type:** Phải là `ArgumentNullException` (không phải `ArgumentException`) — test assert đúng type.
- [ ] **Vị trí:** Thêm SAU `if (string.IsNullOrWhiteSpace(providerInvoiceNumber))` hoặc TRƯỚC — miễn là trước `BuildKey()` và `ParseWebhookPayload()`.
- [ ] **Không thay đổi empty string behavior:** `callbackData = ""` hiện xử lý thành công (test `ProcessWebhookAsync_EmptyCallbackData_ShouldProcessWithoutError` PASS) — chỉ reject `null`.

---

## 5. IMPLEMENTATION PLAN

### Thay đổi duy nhất — `WebhookService.cs`

Thêm 1 dòng null-guard sau các guard hiện có:

```csharp
public async Task ProcessWebhookAsync(
    string providerId,
    string providerInvoiceNumber,
    string callbackData,
    System.Threading.CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(providerId))
        throw new ArgumentException("ProviderId is required.", nameof(providerId));
    if (string.IsNullOrWhiteSpace(providerInvoiceNumber))
        throw new ArgumentException("ProviderInvoiceNumber is required.", nameof(providerInvoiceNumber));

    // ADD THIS LINE:
    if (callbackData is null)
        throw new ArgumentNullException(nameof(callbackData), "CallbackData cannot be null.");

    var key = BuildKey(providerId, providerInvoiceNumber);
    // ... rest unchanged
```

---

## 6. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)

- [ ] **SC1:** `ProcessWebhookAsync_NullCallbackData_ShouldThrowArgumentNullException` → PASS ✅
- [ ] **SC2:** `ProcessWebhookAsync_EmptyCallbackData_ShouldProcessWithoutError` vẫn PASS (không regression) ✅
- [ ] **SC3:** Tất cả 18/18 `WebhookServiceTests` PASS ✅
- [ ] **SC4:** Chỉ 1 dòng thêm vào `WebhookService.cs` (verify bằng `git diff`)
- [ ] **SC5:** `dotnet build VanAn.sln --configuration Release` → 0 errors
- [ ] **SC6:** `guard-check.ps1` → EXIT 0

---

## 7. AI HEALTH CHECK MATRIX

**Evidence Count:** 4 verified facts, 0 open questions

**Verified Facts:**
- Fact 1: `ProcessWebhookAsync` không có null-guard cho `callbackData` — confirmed by reading `WebhookService.cs` lines 44-53 (2026-06-19)
- Fact 2: `ParseWebhookPayload` xử lý null silently qua `IsNullOrWhiteSpace` check — confirmed lines 150-156
- Fact 3: Test asserts `ArgumentNullException` — confirmed reading `WebhookServiceTests.cs` line 315 (2026-06-19)
- Fact 4: Empty string test (`ProcessWebhookAsync_EmptyCallbackData`) PASS — null guard không ảnh hưởng

**Open Questions:** None

**Recommended Action:** IMPLEMENT — trivial 1-line fix, zero risk.

---

## 8. REVERSE IMPACT ANALYSIS

| Thay đổi | Impact | Mitigation |
|---|---|---|
| `if (callbackData is null) throw ArgumentNullException` | Callers passing `null` sẽ nhận exception thay vì silent no-op | Tất cả known callers dùng `string` không null — search confirms không có `null` call site |
| `WebhookController.ReceiveWebhook` (caller) | `[FromBody] string callbackData` → ASP.NET sẽ bind empty string nếu body rỗng, không bind null | Không bị ảnh hưởng |

**Verify call sites trước khi implement:**
```
grep -r "ProcessWebhookAsync" 2_Gateway/ 3_CoreHub/ --include="*.cs"
```
Expected: chỉ `WebhookController.cs` gọi service — `callbackData` từ `[FromBody]` không null khi ASP.NET bind.


# ARCHIVED: master-implementation-plan.md

# MASTER IMPLEMENTATION PLAN — Wave-by-Wave Execution

**Created:** 2026-06-18  
**Last Updated:** 2026-06-18  
**Current Status:** Wave 0 ✅ COMPLETED → Wave 1 READY TO START  
**Branch strategy:** Multiple feature branches, merge to `main` (align-consumer-phase4) between waves  
**Execution principle:** Sequential waves, separate sessions per wave, JIT Planning + Pure Execution

---

## 0. EXECUTION RULES

### Session protocol
1. **Mỗi wave = 1+ sessions** (không rigid 1:1 — wave lớn có thể 2-3 sessions, wave nhỏ có thể 1 session)
2. **Session bắt đầu:** Load context (`load-context` skill) → đọc master plan này → đọc task card của wave
3. **Session kết thúc khi:** Wave SC pass HOẶC context đầy (whichever first)
4. **Sau mỗi session:** Update `project_state.md` (Section 4 + 10 + 11) + commit
5. **Giữa các wave:** Verify `dotnet build VanAn.sln --configuration Release` + `guard-check.ps1` pass trước khi sang wave kế

### Branch protocol (UPDATED 2026-06-18)
```
main (align-consumer-phase4) ← Wave 0 merged ✅
  └── fix/tenantid-remediation (Wave 1 + Wave 3) — NEXT
  └── fix/einvoice-cleanup (Wave 2 + Wave 4) — AFTER Wave 1
```
- ~~Wave 0: trên branch `fix/shoperp-audit-trail-di`~~ → **MERGED to main**
- Wave 1+3: branch mới `fix/tenantid-remediation` từ `main` (tạo ngay bây giờ)
- Wave 2+4: branch mới `fix/einvoice-cleanup` từ `main` (sau Wave 1 merged)
- Wave 5: branch riêng theo task

### Hard rules (không violate)
- CẤM chạy 2 wave song song trên cùng 1 branch (conflict risk)
- CẤM sang wave kế nếu wave hiện tại chưa merge + build pass
- CẤM skip `project_state.md` update sau mỗi session
- CẤM mở wave mới nếu Open Questions của wave đó chưa resolve

---

## 1. WAVE 0 — Quick Wins, Isolated ✅ COMPLETED

**Branch:** `fix/shoperp-audit-trail-di` (merged to `main` via commit `1cccd4c`)
**Completed:** 2026-06-18
**Sessions:** 1 session

### Tasks
| # | Task ID | Task | Files | Task card | Status |
|---|---|---|---|---|---|
| 1 | P0-3 | Fix `VanAnDashboard.razor` DI crash | `VanAnDashboard.razor` (1 file) | `task-p0-3-dashboard-di-crash.md` | ✅ DONE |
| 2 | P0-7 | EInvoice test coverage — write missing tests | `EInvoiceOrchestratorTests.cs` (9 new tests), `Core.Tests/WebhookServiceTests.cs` (rewritten) | task_sprint3b_provider_integration.md §5 | ✅ DONE |

### Entry criteria (Wave 0)
- [x] Branch `fix/shoperp-audit-trail-di` active
- [x] EInvoice review audit committed (3e25c00)

### Exit criteria (Wave 0) — ALL PASSED
- [x] P0-3: Dashboard navigate không crash
- [x] P0-7: CircuitBreakerTests verified existing, HTTP mock tests verified, EInvoiceOrchestratorTests CreateInvoiceAsync flow (6 tests), WebhookServiceTests rewritten (18 tests)
- [x] `dotnet build VanAn.sln --configuration Release` → 0 errors
- [x] `guard-check.ps1` → PASS
- [x] `project_state.md` updated + committed
- [x] Merge to `main` → **COMPLETED**

### Why first
- 0 dependency on TenantId work
- 0 file overlap with Wave 1-4
- Test coverage (P0-7) tạo safety net trước khi sửa production code ở Wave 1
- Dashboard crash (P0-3) là production risk, fix nhanh

---

## 2. WAVE 1 — TenantId Foundation (✅ COMPLETED)

**Branch:** `fix/tenantid-remediation` (created from `main` — Wave 0 merged ✅)
**Completed:** 2026-06-18
**Sessions:** 2 sessions (Phase 1 = 1 session, Phase 2 = 1 session)
**Conflict risk:** HIGH (TenantProvider.cs, Gateway controllers, VanAnDbContext)

### Tasks (sequential — Phase 2 re-touches Phase 1 files)
| # | Task ID | Task | Depends on | Task card | Status |
|---|---|---|---|---|---|
| 3 | P0-1a | TenantId Phase 1 — stop bleeding | — | task-tenantid-phase1-stop-bleeding.md | ✅ DONE |
| 4 | P0-1b | TenantId Phase 2 — tenant foundation | Phase 1 merged | task-tenantid-phase2-tenant-foundation.md | ✅ DONE |

### Entry criteria
- [x] Wave 0 merged to `main`
- [x] Branch `fix/tenantid-remediation` created from `main`
- [x] Phase 1 Open Questions resolved (Q1/Q2 đã resolve 2026-06-18 per card)
- [x] Phase 1 implemented and committed

### Exit criteria Phase 1 — ✅ COMPLETED
- [x] SC1-SC10 pass (per task card)
- [x] Build + arch tests pass (11/11 PASS)
- [x] Commit + ready to continue Phase 2

### Exit criteria Phase 2 — ✅ COMPLETED
- [x] SC1-SC8, SC10 pass (per task card)
- [x] UserTenant entity + configuration + Login DB lookup + claim `tenant_id` + `[Authorize(Policy="RequireTenantAccess")]` trên tất cả Gateway controllers
- [x] Build + arch tests pass (11/11 PASS)
- [x] `project_state.md` updated + committed
- [ ] Merge to `main` — **READY TO MERGE**

### Why here
- Phase 2 SC4 chuẩn hóa claim name `TenantId`→`tenant_id` re-touch `TenantProvider.cs` (Phase 1 vừa fix) → phải sequential
- Phase 2 thêm auth policy lên TẤT CẢ Gateway controllers bao gồm `WebhookController` → phải trước EInvoice cleanup (Wave 2 fix WebhookController)
- TenantId là root cause của 3 backlogs (P0-1 + E2E auth T-20 + manual test fail §9) → fix sớm unblock nhiều thứ

---

## 3. WAVE 2 — EInvoice API Layer (sau Wave 1, new branch)

**Branch:** `fix/einvoice-cleanup` (tạo từ `main` sau Wave 1 merged)
**Estimated sessions:** 2 (Phase A = 1 session, Phase B = 1 session)
**Conflict risk:** MEDIUM (WebhookController — nhưng Phase 2 đã merged nên không conflict)

### Tasks (sequential — Phase B cần Phase A cleanup trước)
| # | Task ID | Task | Depends on | Task card |
|---|---|---|---|---|
| 5 | P0-6a | EInvoice cleanup Phase A — dead code | Phase 2 merged (WebhookController auth) | task-einvoice-deadcode-cleanup.md Phase A |
| 6 | P0-6b | EInvoice cleanup Phase B — controller | Phase A + Phase 2 tenant pattern | task-einvoice-deadcode-cleanup.md Phase B |

### Entry criteria
- [ ] Wave 1 merged to `main`
- [ ] Branch `fix/einvoice-cleanup` created from `main`
- [ ] Open Questions resolved: Q1 (controller location — Gateway vs ShopERP), Q2 (route convention plural vs singular)

### Exit criteria Phase A
- [ ] SC1-SC4 pass: DELETE/rewrite `EInvoiceE2ETests.cs`, fix `WebhookController` route + body shape
- [ ] Build pass

### Exit criteria Phase B
- [ ] SC5-SC7 pass: HKDElectronicInvoiceController tạo lại + DTOs đầy đủ + DI wiring
- [ ] Build + guard-check pass
- [ ] `project_state.md` updated + committed
- [ ] Merge to `main`

### Why here (not earlier)
- Controller mới cần JWT claim tenant pattern (từ Phase 1+2)
- WebhookController fix route/body phải sau Phase 2 (Phase 2 thêm auth policy lên WebhookController)
- Nếu làm trước Phase 2 → conflict + phải retrofit tenant pattern

---

## 4. WAVE 3 — TenantId Completion (parallel với Wave 2, trên branch tenantid)

**Branch:** `fix/tenantid-remediation` (sau Phase 2 merged, tiếp tục trên branch này hoặc tạo branch mới từ main)
**Estimated sessions:** 2 (Phase 3 = 1 session, Phase 4 = 1 session)
**Conflict risk:** LOW (KhachLink + Accounting Razor pages — không đụng Gateway/EInvoice)

### Tasks (sequential — Phase 4 cần Phase 3)
| # | Task ID | Task | Depends on | Task card |
|---|---|---|---|---|
| 7 | P0-1c | TenantId Phase 3 — KhachLink tenant | Phase 2 merged | task-tenantid-phase3-khachlink-tenant.md |
| 8 | P0-1d | TenantId Phase 4 — cleanup & unification | Phase 2 + Phase 3 merged | task-tenantid-phase4-cleanup.md |

### Entry criteria
- [ ] Wave 1 (Phase 2) merged to `main`
- [ ] Branch từ `main` (có thể dùng lại `fix/tenantid-remediation` hoặc tạo mới)

### Exit criteria Phase 3
- [ ] SC1-SC8 pass: KhachLink resolve tenant từ shop URL, SignalR auth, remove demo data, OfflineOrderService tenant from context
- [ ] Build + guard-check + arch tests (VA-KHACHLINK-004) pass

### Exit criteria Phase 4
- [ ] SC1-SC10 pass: 0 hardcoded fallbacks, 6 Razor pages dùng ITenantProvider, 0 manual FindFirst
- [ ] Build + guard-check + arch tests + all existing tests pass (no regression)
- [ ] `project_state.md` updated + committed
- [ ] Merge to `main`

### Why parallel with Wave 2
- Phase 3+4 đụng KhachLink + Accounting Razor pages
- KHÔNG đụng Gateway controllers hay EInvoice files
- → Không conflict với Wave 2, có thể làm song song

---

## 5. WAVE 4 — EInvoice UI + E2E (sau Wave 2 + Wave 3)

**Branch:** `fix/einvoice-cleanup` (tiếp tục) hoặc branch mới từ `main`
**Estimated sessions:** 2 (Phase C = 1 session, Phase D = 1 session)
**Conflict risk:** LOW (new files only)

### Tasks (sequential — Phase D cần Phase C)
| # | Task ID | Task | Depends on | Task card |
|---|---|---|---|---|
| 9 | P0-6c | EInvoice cleanup Phase C — 6 Razor pages | Phase B controller + Phase 2 auth | task-einvoice-deadcode-cleanup.md Phase C |
| 10 | P0-6d | EInvoice cleanup Phase D — 3 Playwright specs | Phase C pages + Phase 2 E2E auth | task-einvoice-deadcode-cleanup.md Phase D |

### Entry criteria
- [ ] Wave 2 merged (controller exists)
- [ ] Wave 3 merged (auth pattern stable)
- [ ] Branch from `main`

### Exit criteria Phase C
- [ ] SC8-SC10 pass: 6 Razor pages với VanAn components, mobile-first responsive
- [ ] Build pass

### Exit criteria Phase D
- [ ] SC11-SC13 pass: 3 Playwright specs test real UI flow, re-enable E2E in CI
- [ ] `project_state.md` updated + committed
- [ ] Merge to `main`

### Why here
- UI pages cần controller endpoint (Phase B) + auth policy (Phase 2)
- Playwright specs cần pages (Phase C) + E2E auth setup (Phase 2 dev login endpoint)

---

## 6. WAVE 5 — Remaining Backlog (sau tất cả)

**Branch:** Theo task cụ thể
**Estimated sessions:** Variable
**Conflict risk:** LOW (dependent trên tất cả waves trước)

### Tasks (priority order, có thể parallel)
| # | Task ID | Task | Depends on |
|---|---|---|---|
| 11 | P0-2 | E2E false-positive specs (T-17/18/19/21) | — (isolated) |
| 12 | P0-4 | AccountCode not saved | Phase 2 (tenant pattern) |
| 13 | P0-5 | Entry timing (CreateOrder→PaymentWebhook) | Phase 2 |
| 14 | P1-1 | E2E auth global-setup | Phase 2 (dev login) |
| 15 | P1-2 to P1-5 | Various | various |
| 16 | P2-1 to P2-5, P3-1 to P3-3 | Various | various |

---

## 7. FILE CONFLICT MATRIX (tại sao thứ tự này)

| File zone | Wave 0 | Wave 1 | Wave 2 | Wave 3 | Wave 4 | Conflict mitigation |
|---|---|---|---|---|---|---|
| `TenantProvider.cs` | — | ✅ Phase 1+2 | — | — | — | Sequential Phase 1→2 |
| Gateway controllers | — | ✅ Phase 1+2 | ✅ Phase A+B | ✅ Phase 3 (Orders) | — | Wave 1 trước Wave 2 |
| `VanAnDbContext.cs` | — | ✅ Phase 1+2 | — | — | — | Isolated trong Wave 1 |
| `Program.cs` (Gateway) | — | ✅ Phase 2 | ✅ Phase B DI | — | — | Wave 1 trước Wave 2 |
| Accounting Razor pages | — | ✅ Phase 2 auth | — | ✅ Phase 4 refactor | — | Wave 1 trước Wave 3 |
| `WebhookController.cs` | — | ✅ Phase 2 auth | ✅ Phase A fix | — | — | Wave 1 trước Wave 2 |
| KhachLink pages/hubs | — | — | — | ✅ Phase 3 | — | Isolated trong Wave 3 |
| EInvoice Razor pages | — | — | — | — | ✅ Phase C (new) | New files, no conflict |
| Test files | ✅ Wave 0 | ✅ Phase 2 tests | ✅ Phase A tests | ✅ Phase 3-4 tests | ✅ Phase D specs | Separate files |
| CI workflows | — | — | — | — | ✅ Phase D re-enable | Isolated |

---

## 8. VISUAL TIMELINE

```
Week 1:  [Wave 0: tests + dashboard] ──→ merge
              │
Week 2:  [Wave 1: TenantId Phase 1→2] ──→ merge
              │
Week 3:  ┌────[Wave 2: EInvoice Phase A→B] ──→ merge
         │         │
         └────[Wave 3: TenantId Phase 3→4] ──→ merge (parallel)
                   │
Week 4:  [Wave 4: EInvoice Phase C→D] ──→ merge
                   │
Week 5+: [Wave 5: remaining backlog]
```

---

## 9. SESSION CHECKLIST (cho mỗi session)

### Before session start
- [ ] `load-context` skill → đọc `project_state.md`
- [ ] Đọc master plan này → xác định wave hiện tại
- [ ] Đọc task card của wave hiện tại
- [ ] Verify branch đúng
- [ ] Verify wave trước đã merged (git log)

### During session
- [ ] JIT Planning: đọc boundary files 1 lần, chốt file cần sửa/tạo
- [ ] Pure Execution: viết code, không re-explore
- [ ] Run build + tests sau mỗi micro-phase

### Before session end
- [ ] Wave SC pass HOẶC context gần đầy
- [ ] `dotnet build VanAn.sln --configuration Release` → 0 errors
- [ ] `guard-check.ps1` → PASS
- [ ] Update `project_state.md` (Section 4 + 10 + 11)
- [ ] Commit với message format: `<type>(<wave>): <description>`
- [ ] Nếu wave hoàn tất: merge to `main` + verify build trên `main`

---

## 10. ROLLBACK PLAN

Nếu wave fail/conflict không resolve:
1. **STOP** — không cố fix tiếp
2. `git stash` uncommitted changes
3. `git checkout main` — về baseline sạch
4. Document failure trong `project_state.md` Section 7 (Known Risks)
5. Tạo task card mới cho retry với approach khác
6. Không sang wave kế cho đến khi wave hiện tại resolve

---

## REFERENCES
- TenantId cards: `task-tenantid-phase1-stop-bleeding.md`, `task-tenantid-phase2-tenant-foundation.md`, `task-tenantid-phase3-khachlink-tenant.md`, `task-tenantid-phase4-cleanup.md`
- EInvoice cards: `task_sprint3_einvoice.md` (SUPERSEDED), `task_sprint3b_provider_integration.md`, `task-einvoice-deadcode-cleanup.md`
- Guard check: `task-upgrade-guard-check.md`
- Consolidated backlog: `project_state.md` Section 4


# ARCHIVED: task-tenantid-phase2-tenant-foundation.md

# TASK CARD: [TENANTID REMEDIATION] - [PHASE 2] - TENANT FOUNDATION

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Xây dựng User-Tenant mapping thực sự — user thuộc tenant nào, login lấy tenant từ DB thay vì hardcode, enforce `RequireTenantAccess` policy trên mọi endpoint/page tenant-scoped.
- **Nghiệp vụ áp dụng:** Multi-tenancy production — mỗi user chỉ thấy data của tenant mình thuộc về.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** ANALYZE → IMPLEMENT (cần Phase 1 merged trước khi bắt đầu)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `1_Shared/Domain.cs` — thêm `UserTenant` entity (modeling defect confirmed: thiếu User-Tenant relationship)
  - `3_CoreHub/Infrastructure/VanAnDbContext.cs` — thêm `DbSet<UserTenant>`
  - `3_CoreHub/Infrastructure/Configurations/UserTenantConfiguration.cs` — mới
  - `5_WebApps/ShopERP/Pages/Login.cshtml.cs` — lookup tenant từ DB thay vì hardcode
  - `5_WebApps/ShopERP/Services/TenantProvider.cs` — chuẩn hóa claim name
  - `5_WebApps/ShopERP/Program.cs` — enforce `RequireTenantAccess` policy
  - `2_Gateway/Program.cs` — register ITenantProvider cho Gateway
  - `2_Gateway/Controllers/*.cs` — apply `[Authorize(Policy = "RequireTenantAccess")]`
  - `5_WebApps/ShopERP/Components/Pages/Accounting/*.razor` — apply `[Authorize(Policy = "RequireTenantAccess")]`
- **Boundary Rules (Nghiêm cấm):**
  - CẤM sửa AccountingEntry immutability
  - CẤM tạo Tenant management UI trong Phase 2 (để Phase riêng)
  - CẤM refactor KhachLink tenant context (để Phase 3)
  - Domain modification CHỈ cho `UserTenant` entity — phải có Tech Lead approval

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Domain Purity:** `UserTenant` entity phải pure — no EF Core, no DataAnnotations.
- [ ] **Multi-tenancy HARDENING:** `UserTenant` là cross-tenant entity (user có thể thuộc nhiều tenant) — không apply query filter trên chính nó.
- [ ] **Auth enforcement:** Mọi controller/page tenant-scoped MUST có `[Authorize(Policy = "RequireTenantAccess")]`.
- [ ] **Claim standardization:** Tất cả claim name → `"tenant_id"` (OIDC standard snake_case) — cập nhật nhất quán toàn codebase.
- [ ] **Legal Standards:** TT 152/2025/TT-BTC — mỗi HKD phải có dữ liệu cách ly. User-Tenant mapping là cơ sở pháp lý cho việc phân định dữ liệu.

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [x] **SC1:** `UserTenant` entity tồn tại trong Domain.cs với fields: `UserId`, `TenantId`, `Role`, `AssignedAt`, `IsActive`.
- [x] **SC2:** `UserTenantConfiguration.cs` tồn tại với proper EF mapping + value conversion.
- [x] **SC3:** `Login.cshtml.cs` — lookup `UserTenant` từ DB, set claim `"tenant_id"` với real tenant GUID.
- [x] **SC4:** Tất cả claim name trong codebase → `"tenant_id"` (snake_case) — `HttpContextTenantProvider`, `OrdersController`, `AccountingEntriesController`, `ProviderController`.
- [x] **SC5:** `[Authorize(Policy = "RequireTenantAccess")]` áp dụng trên:
  - Tất cả Gateway controllers (Orders, AccountingEntries, Provider, Webhook — trừ health/public)
  - WebhookController.ReceiveWebhook có `[AllowAnonymous]` (external provider callbacks)
- [x] **SC6:** `RequireTenantAccess` policy updated: `RequireClaim("tenant_id")` (snake_case).
- [x] **SC7:** Gateway có `ITenantProvider` registered (JWT claim-based).
- [x] **SC8:** `dotnet build VanAn.sln` — 0 errors.
- [ ] **SC9:** `guard-check.ps1` — PASS (script error - skipped).
- [x] **SC10:** Architecture tests — PASS (11/11).
- [ ] **SC11:** Integration test: user A (tenant 1) không thấy data của user B (tenant 2) — TODO Wave 3.
- [ ] **SC12:** Security test: request không có `tenant_id` claim → 401/403 — TODO E2E tests.

**Implementation Date:** 2026-06-18
**Branch:** `fix/tenantid-remediation`

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — verify UserTenant modeling
- `system-refactor-safety` — refactor auth layer
- `outbox-pattern-implementation` — (nếu cần event cho tenant assignment)

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5 architectural gaps đã verify
- **Verified Facts:**
  - Fact 1: Không có User-Tenant mapping — `DemoUser` không có TenantId field
  - Fact 2: `Login.cshtml.cs:54` hardcode tất cả user → `00000000-0000-0000-0000-000000000001`
  - Fact 3: `RequireTenantAccess` policy defined nhưng ZERO usages
  - Fact 4: `Tenant` entity tồn tại (record, Domain.cs:156) nhưng không có relationship với User
  - Fact 5: `DbSet<Tenant> Tenants` tồn tại nhưng không có seed data production
- **Assumptions:**
  - User có thể thuộc 1 hoặc nhiều tenant (multi-tenant membership) — cần User confirm
  - Role trong UserTenant có thể khác role trong JWT hiện tại — cần clarify
- **Open Questions:**
  - Q1: User thuộc 1 tenant hay nhiều tenant? (single vs multi-tenant membership)
  - Q2: Nếu multi-tenant, user chọn tenant nào khi login? (tenant switcher UI?)
  - Q3: Tenant admin (Owner) có quyền tạo user mới và gán tenant không?
- **Recommended Action:** **Investigate** — Open Questions = 3, cần User làm rõ trước khi implement.

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| Domain.cs thêm `UserTenant` | Architecture tests cần update (nếu check entity count) | Verify NetArchTest rules |
| Login.cshtml.cs lookup DB | Login chậm hơn (1 DB query) | Acceptable — cache tenant trong cookie |
| Claim name `"TenantId"` → `"tenant_id"` | Break mọi existing JWT tokens | ⚠️ Cần migration plan — rotate tokens hoặc dual-read tạm |
| `RequireTenantAccess` enforcement | Pages không có auth → 403 | ⚠️ Cần audit tất cả pages, thêm `[AllowAnonymous]` cho public pages |
| Gateway ITenantProvider | Gateway cần IHttpContextAccessor | Verify Gateway Program.cs |

## 9. TDD & E2E TESTING STRATEGY
- **TDD BẮT BUỘC (new feature — UserTenant entity):**
  - Viết test cho `UserTenant` entity trước (creation, validation, multi-tenant membership)
  - Implement entity → test PASS
  - Viết test cho Login tenant lookup trước → implement → PASS
  - Viết test cho `RequireTenantAccess` policy enforcement → implement → PASS
- **E2E Playwright test BẮT BUỘC (auth flow thay đổi):**
  - Login flow thay đổi (lookup DB thay vì hardcode) → E2E auth spec phải update
  - `global-setup.ts` cần rewrite (per e2e-gap-backlog T-16)
  - Spec files: `accounting-flow.spec.ts`, `order-flow.spec.ts`, `audit-trail-flow.spec.ts`, `period-closing-flow.spec.ts`, `balance-dashboard-flow.spec.ts`
  - Test case: user A (tenant 1) login → chỉ thấy tenant 1 data, không thấy tenant 2
  - Test case: request không có `tenant_id` claim → 403
- **Test boundary:**
  - Unit tests: `UserTenant` entity, `LoginModel` tenant lookup, claim name consistency
  - Integration tests: `RequireTenantAccess` policy, multi-tenant data isolation
  - E2E tests: login → tenant-scoped page → verify data isolation

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Mỗi Session chạy 2 Micro-phases LIÊN TỤC trong 1 phiên:

```
[Session N]
  ├── Phase 1: JIT Planning
  │     Đọc boundary files 1 lần duy nhất → chốt: file cần sửa/tạo,
  │     tên test case, method signature, cấu trúc hàm.
  │     KHÔNG đọc ngoài boundary. KHÔNG giải thích dài.
  └── Phase 2: Pure Execution
        Bám chặt Phase 1 → viết thẳng.
        Token chỉ chi cho output code, không suy luận/re-explore.
```

### Micro-phase breakdown cho Phase 2 (Tenant Foundation)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Đọc Domain.cs Tenant entity → chốt: `UserTenant` fields, factory method, test names | Write `UserTenant` entity + `UserTenantConfiguration` + unit tests |
| **S2** | Đọc Login.cshtml.cs + TenantProvider → chốt: DB lookup signature, claim name | Refactor Login → DB lookup + update claim name toàn codebase |
| **S3** | Đọc Program.cs + all controllers/pages → chốt: policy placement list | Apply `RequireTenantAccess` policy + Gateway ITenantProvider |
| **S4** | Đọc E2E specs → chốt: auth setup changes | Update E2E auth + integration tests + verify |

### Rules
- JIT Planning: MAX 15 phút đọc, chốt output bằng text ngắn
- Pure Execution: KHÔNG re-read, chỉ viết code theo plan
- Domain modification (UserTenant) cần Tech Lead approval trong JIT Planning S1

## 11. ESTIMATED EFFORT
- 3-5 ngày (1 ngày Domain+EF + 1 ngày Login refactor + 1 ngày policy enforcement + 1 ngày test + 1 ngày buffer)
- 4 sessions (S1-S4) theo JIT Planning
- **BLOCKER:** Cần User trả lời 3 Open Questions trước khi bắt đầu.
