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
