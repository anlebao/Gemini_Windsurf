# E2E Gap Backlog — Entry Point vs Test Coverage Audit
**Date:** 2026-06-18  
**Scope:** ShopERP (5003), KhachLink (5002), Accounting pages, Gateway (5001)  
**Source specs:** `6_Testing/e2e-tests/*.spec.ts`  
**Source routes:** `5_WebApps/ShopERP/Components/Pages/**`, `5_WebApps/KhachLink/Components/Pages/**`, `2_Gateway/Controllers/**`

---

## LEGEND
- 🔴 Critical — E2E sẽ fail 100% khi chạy
- 🟡 Warning — E2E có thể pass do fallback logic, nhưng không test đúng
- 🟢 OK — Match hoàn toàn

---

## SECTION 1: Route / Page Match

| # | E2E Spec | URL được test | Route thực tế | Status |
|---|---|---|---|---|
| 1 | `accounting-flow.spec.ts` | `/accounting` | `AccountingIndex.razor @page "/accounting"` | 🟢 OK |
| 2 | `accounting-flow.spec.ts` | `/accounting/revenue` | `RevenueEntry.razor @page "/accounting/revenue"` | 🟢 OK |
| 3 | `accounting-flow.spec.ts` | `/accounting/expenses` | `ExpenseEntry.razor @page "/accounting/expenses"` | 🟢 OK |
| 4 | `accounting-flow.spec.ts` | `/accounting/history` | `TransactionHistory.razor @page "/accounting/history"` | 🟢 OK |
| 5 | `accounting-flow.spec.ts` | `/accounting/balance` | `AccountBalance.razor @page "/accounting/balance"` | 🟢 OK |
| 6 | `balance-dashboard-flow.spec.ts` | `/accounting/balance` | `AccountBalance.razor @page "/accounting/balance"` | 🟢 OK |
| 7 | `period-closing-flow.spec.ts` | `/accounting/period-closing` | `PeriodClosing.razor @page "/accounting/period-closing"` | 🟢 OK |
| 8 | `audit-trail-flow.spec.ts` | `/admin/audit-trail` | `AuditTrail.razor @page "/admin/audit-trail"` | 🟢 OK |
| 9 | `order-flow.spec.ts` | `KHACHLINK_URL` = `http://localhost:5002/` | `Home.razor @page "/"` | 🟢 OK |
| 10 | `order-flow.spec.ts` | `/order-tracking/{orderId}` | **KHÔNG TỒN TẠI** trong KhachLink Pages | 🔴 GAP |
| 11 | `qr-payment.spec.ts` | `KHACHLINK_URL` + `#qrPaymentModal` | **KHÔNG có modal component** trong KhachLink | 🔴 GAP |

---

## SECTION 2: API Endpoint Match (E2E gọi trực tiếp)

| # | E2E Spec | Endpoint được gọi | Tồn tại? | Ghi chú |
|---|---|---|---|---|
| 12 | `order-flow.spec.ts` | `POST {COREHUB_URL}/api/orders` | 🔴 KHÔNG | CoreHub là Worker Host, KHÔNG có HTTP API. Endpoint này không tồn tại |
| 13 | `order-flow.spec.ts` | `PUT {COREHUB_URL}/api/orders/{id}/status` | 🔴 KHÔNG | Same — CoreHub không expose HTTP |
| 14 | `order-flow.spec.ts` | `GET {COREHUB_URL}/api/inventory/check` | 🔴 KHÔNG | Same — CoreHub không expose HTTP |
| 15 | `accounting-flow.spec.ts` | `POST {COREHUB_URL}/api/accounting/revenue` | 🔴 KHÔNG | Same — CoreHub không expose HTTP |
| 16 | `accounting-flow.spec.ts` | `POST {COREHUB_URL}/api/accounting/expense` | 🔴 KHÔNG | Same — CoreHub không expose HTTP |
| 17 | `qr-payment.spec.ts` | `POST {GATEWAY_URL}/api/v1/vietqr/generate` | 🟢 OK | `VietQrController` tồn tại ở Gateway |
| 18 | `qr-payment.spec.ts` | `POST {GATEWAY_URL}/api/v1/vietqr/validate-bank` | 🟢 OK | `VietQrController.ValidateBank` tồn tại |
| 19 | `qr-payment.spec.ts` | `GET {GATEWAY_URL}/api/v1/vietqr/supported-banks` | 🟢 OK | `VietQrController.GetSupportedBanks` tồn tại |

---

## SECTION 3: DI / Runtime Crash Risk

| # | File | Issue | Status |
|---|---|---|---|
| 20 | `KhachLink/Components/Pages/VanAnDashboard.razor` | `@inject IDashboardService DashboardService` — service này đã bị REMOVE khỏi `KhachLink/Program.cs` (fix DI violation hôm nay). Khi navigate tới `/VanAnDashboard` sẽ crash runtime với `InvalidOperationException` | 🔴 Critical |
| 21 | `KhachLink/Program.cs` | `ISocialCampaignService` và `IOrderWorkflowService` đã bị remove. Nếu còn component nào inject chúng sẽ crash | 🟡 Warning — đã grep, hiện không có component nào inject, nhưng cần kiểm tra lại sau mỗi thay đổi |

---

## SECTION 4: Selector / UI Element Mismatch

| # | E2E Spec | Selector được dùng | Vấn đề |
|---|---|---|---|
| 22 | `order-flow.spec.ts` | `.feature-card`, `button:has-text("Đặt ngay")` | Chưa verify CSS class này có trong `KhachLink/Home.razor` thật sự không |
| 23 | `qr-payment.spec.ts` | `button:has-text("Thanh toán QR")`, `#qrPaymentModal`, `.qr-image` | KhachLink không có QR modal component — sẽ fail `waitForSelector` |
| 24 | `period-closing-flow.spec.ts` | `h1:has-text("Đóng Sổ Kỳ Kế Toán")` | Cần verify text trong `PeriodClosing.razor` có khớp chính xác không (case-sensitive) |
| 25 | `balance-dashboard-flow.spec.ts` | `.metrics-grid` | Cần verify `AccountBalance.razor` dùng class `.metrics-grid` hay `.vanan-metrics-card` (không khớp với selector trong `accounting-flow.spec.ts` là `.vanan-metrics-card`) |
| 26 | `balance-dashboard-flow.spec.ts` | `/login` + `#username`, `#password` | ShopERP dùng OpenID Connect redirect, KHÔNG có `/login` page trực tiếp với `#username`/`#password` |

---

## SECTION 5: Spec Coverage — Pages có nhưng chưa có E2E

| # | Page / Route | Spec file | Status |
|---|---|---|---|
| 27 | `KhachLink /VanAnDashboard` | Không có spec | 🟡 Untested |
| 28 | `ShopERP /` (Home) | Không có spec | 🟡 Untested |
| 29 | `ShopERP /admin/audit-trail` — Export Excel flow | `export-excel-flow.spec.ts` — cần verify | 🟡 Cần kiểm tra |
| 30 | `ShopERP` — Voice Command | `voice-command.spec.ts` — cần verify route | 🟡 Cần kiểm tra |
| 31 | `ShopERP` — i18n | `i18n.spec.ts` — cần verify | 🟡 Cần kiểm tra |

---

## SECTION 6: False-Positive Spec Pattern (E2E pass dù feature broken)

> **Root cause phát hiện 2026-06-18:** `accounting-flow.spec.ts` và nhiều spec khác dùng `reporter.pass()` thay cho `expect()` → test luôn xanh dù submit thực sự fail.

### 6.1 — Pattern A: `if (isVisible)` không có `expect` bắt buộc

```typescript
// HIỆN TẠI — sai: nếu button không hiển thị → silent skip, test vẫn pass
const submitButton = page.locator('button:has-text("Lưu")');
if (await submitButton.isVisible()) {
  await submitButton.click();
  const hasSuccess = await successAlert.isVisible();
  reporter.pass('...', { success: hasSuccess });  // chỉ log, không assert!
}

// ĐÚNG — phải có:
await expect(submitButton).toBeVisible();
await submitButton.click();
await expect(successAlert).toBeVisible();
```

**Ảnh hưởng:** Mọi test dùng pattern này đều là false-positive nếu UI không render đúng.

### 6.2 — Pattern B: `else { reporter.pass() }` bypass mọi lỗi

```typescript
// HIỆN TẠI — sai: nhánh else pass luôn kể cả khi API 500
} else {
  const response = await page.request.post(`${COREHUB_URL}/api/accounting/revenue`);
  if (response.status() === 200) {
    reporter.pass('OK');
  } else {
    reporter.pass('Form Check', { note: 'Submit button not found' }); // ← pass dù fail!
  }
}
```

**Ảnh hưởng:** Kể cả khi submit button không tồn tại VÀ API call fail → test vẫn xanh.

### 6.3 — Pattern C: `global-setup.ts` login sẽ fail vì ShopERP dùng OpenID Connect

```typescript
// global-setup.ts line 19-27 — sai:
await page.goto(`${config.SHOPERP_URL}/login`);
await page.fill('#username', 'admin@vanan.vn');   // ← trang /login không tồn tại
await page.fill('#password', 'VanAn@2026');
await page.click('button[type="submit"]');
await page.waitForURL(`${config.SHOPERP_URL}/`);
// → waitForURL sẽ timeout vì không bao giờ redirect về /
```

**Ảnh hưởng:** `auth/admin.json` không bao giờ được tạo đúng → `_tenantId = Guid.Empty` trong mọi Blazor page → `HandleSubmit` trả lỗi *"Không xác định được Tenant ID"* → đây là **root cause chính** của manual test fail.

### 6.4 — Danh sách spec bị ảnh hưởng

| Spec file | Pattern A | Pattern B | Pattern C (auth) | False-positive? |
|---|---|---|---|---|
| `accounting-flow.spec.ts` | ✅ | ✅ | ✅ | 🔴 **100%** |
| `order-flow.spec.ts` | ✅ | ✅ | ✅ | 🔴 **100%** |
| `audit-trail-flow.spec.ts` | ✅ | ✅ | ✅ | 🔴 **100%** |
| `period-closing-flow.spec.ts` | ✅ | — | ✅ | 🔴 **cao** |
| `balance-dashboard-flow.spec.ts` | — | — | ✅ | 🟡 auth fail |
| `qr-payment.spec.ts` | — | — | — | 🟢 có `expect()` thật |

---

## TASK LIST (ưu tiên)

### 🔴 P0 — Phải fix trước khi chạy E2E

| Task ID | Task | File | Action |
|---|---|---|---|
| T-01 | Fix `VanAnDashboard.razor` inject `IDashboardService` đã bị remove | `KhachLink/Components/Pages/VanAnDashboard.razor` | Remove `@inject IDashboardService` + replace bằng HTTP call qua Gateway hoặc comment out trang |
| T-02 | Tạo trang `/order-tracking/{orderId}` trên KhachLink | `KhachLink/Components/Pages/OrderTracking.razor` | Tạo mới `@page "/order-tracking/{OrderId}"` với UI hiển thị status |
| T-03 | Tạo QR Payment modal trên KhachLink | `KhachLink/Components/QrPaymentModal.razor` | Tạo component với `id="qrPaymentModal"`, `.qr-image`, gọi Gateway `/api/v1/vietqr/generate` |
| T-04 | Sửa E2E `order-flow.spec.ts` — đổi `COREHUB_URL` → `GATEWAY_URL` | `6_Testing/e2e-tests/order-flow.spec.ts` | CoreHub không có HTTP. Orders API nằm ở Gateway `/api/orders` |
| T-05 | Sửa E2E `accounting-flow.spec.ts` — đổi `COREHUB_URL` → `GATEWAY_URL` | `6_Testing/e2e-tests/accounting-flow.spec.ts` | Gateway cần có `/api/accounting/revenue` và `/api/accounting/expense` |
| T-16 | Fix `global-setup.ts` — thay login form bằng auth bypass cho test env | `6_Testing/global-setup.ts` | Dùng `storageState` với JWT mock hoặc thêm `/dev/login` endpoint trên ShopERP chỉ cho `ASPNETCORE_ENVIRONMENT=Development` |

### 🟡 P1 — Fix để E2E có ý nghĩa thực sự (chống false-positive)

| Task ID | Task | File | Action |
|---|---|---|---|
| T-06 | Fix `/login` flow trong `balance-dashboard-flow.spec.ts` | `6_Testing/e2e-tests/balance-dashboard-flow.spec.ts` | Dùng `storageState` từ `global-setup` thay vì fill form trực tiếp |
| T-07 | Thêm `/api/accounting/revenue` và `/api/accounting/expense` vào Gateway | `2_Gateway/Controllers/AccountingController.cs` | Tạo proxy controller chuyển tiếp tới `IAccountingService` |
| T-08 | Verify `POST /api/orders` route prefix trên Gateway | `2_Gateway/Controllers/OrdersController.cs` | Kiểm tra route là `/api/orders` hay `/api/v1/orders` |
| T-09 | Đồng nhất CSS class `.metrics-grid` vs `.vanan-metrics-card` | `ShopERP/Components/Pages/Accounting/AccountBalance.razor` | Thêm alias class hoặc sửa spec |
| T-10 | Verify text `h1` trong `PeriodClosing.razor` khớp spec | `ShopERP/Components/Pages/Accounting/PeriodClosing.razor` | Check exact string `"Đóng Sổ Kỳ Kế Toán"` |
| T-17 | Refactor `accounting-flow.spec.ts` — thay `reporter.pass()` bằng `expect()` thật | `6_Testing/e2e-tests/accounting-flow.spec.ts` | Mỗi submit test phải có `expect(successAlert).toBeVisible()` sau click |
| T-18 | Refactor `order-flow.spec.ts` — xóa else-bypass pattern | `6_Testing/e2e-tests/order-flow.spec.ts` | Xóa `else { reporter.pass('...', { note: '...' }) }` — thay bằng `expect` cứng |
| T-19 | Refactor `audit-trail-flow.spec.ts` — thay `reporter.pass()` bằng `expect()` thật | `6_Testing/e2e-tests/audit-trail-flow.spec.ts` | Same pattern fix |
| T-20 | Thêm test env `TenantId` claim vào ShopERP dev auth | `ShopERP` | Dev login endpoint phải trả JWT có `TenantId` claim để `_tenantId != Guid.Empty` |

### 🟢 P2 — Backlog / Nice-to-have

| Task ID | Task | Action |
|---|---|---|
| T-11 | Viết E2E cho `KhachLink /VanAnDashboard` | Cần fix T-01 trước |
| T-12 | Verify `export-excel-flow.spec.ts` routes và selectors | Check spec file + ShopERP pages |
| T-13 | Verify `voice-command.spec.ts` routes và selectors | Check spec file + Gateway VoiceCommandController |
| T-14 | Verify `i18n.spec.ts` | Check spec file coverage |
| T-15 | Viết E2E cho `ShopERP /` (Home page) | Hiện không có coverage |
| T-21 | Refactor `period-closing-flow.spec.ts` — thêm `expect()` sau mỗi step | `6_Testing/e2e-tests/period-closing-flow.spec.ts` | Tăng độ tin cậy |

---

## IMPLEMENTATION GUIDE

### Thứ tự implement để unblock E2E nhanh nhất

```
Phase 1 — Auth foundation (T-16, T-20)
  └─ Fix global-setup.ts + dev login endpoint
  └─ Không có auth → mọi Blazor submit đều fail với "Không xác định TenantId"

Phase 2 — Missing pages (T-01, T-02, T-03)
  └─ VanAnDashboard DI fix
  └─ OrderTracking page
  └─ QR Payment modal

Phase 3 — API routing (T-04, T-05, T-07, T-08)
  └─ Sửa spec URL COREHUB → GATEWAY
  └─ Thêm accounting endpoints vào Gateway nếu thiếu

Phase 4 — Spec quality (T-17, T-18, T-19)
  └─ Thay reporter.pass() bằng expect() thật
  └─ Xóa else-bypass pattern
```

### Definition of Done cho mỗi task
1. Build pass: `dotnet build VanAn.sln` — 0 errors
2. Spec có ít nhất 1 `expect()` bắt buộc (không phải `reporter.pass()` only)
3. `auth/admin.json` được tạo đúng bởi `global-setup.ts`
4. Chạy spec đơn: `npx playwright test <spec-file> --project=e2e-tests` — pass thật

---

## SUMMARY

| Category | Count |
|---|---|
| 🟢 Routes OK | 9 |
| 🔴 Missing pages/endpoints | 7 (T-01 đến T-05, T-16) |
| 🔴 False-positive specs | 4 specs (T-17, T-18, T-19, T-21) |
| 🟡 Selector/auth mismatch | 5 (T-06, T-09, T-10, T-20) |
| 🟡 Untested pages | 5 |
| **Total gaps** | **21** |

**Blockers cho CI E2E pass (phải fix theo thứ tự):**
1. T-16 — Auth (global-setup)
2. T-20 — TenantId claim
3. T-01 — DI crash
4. T-04, T-05 — URL sai
5. T-17, T-18, T-19 — False-positive specs
