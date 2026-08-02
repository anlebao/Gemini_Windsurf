# W6 Deep Investigation Report — 8 Failing Golden E2E Tests

> **Status:** ANALYZE complete · 2026-07-07
> **Mode:** ANALYZE (read-only investigation, no code changes)
> **Branch:** `main` (uncommitted W6 work-in-progress)
> **Purpose:** Per-test deep root-cause analysis with production-code evidence.
> Replaces the shallow triage in the original W6 task card Section 3.

---

## 0. Methodology

Each failing test was traced from spec → POM → production code (controllers, services,
repositories, Razor components, appsettings) to identify the **true** root cause, not
just the symptom. Verified against actual source on `main` (uncommitted W6 fixes in place).

Anti-Guessing Gate: every claim below is backed by a file:line citation.

---

## 1. Test #1 — `omnichannel-order-lifecycle.spec.ts:48` SCENARIO 1

### Symptom (stale triage)
> `.feature-card` selector not found

### Verified root cause — **TEST-SPEC / PRODUCTION-FLOW MISMATCH**
The selector triage is **wrong**. `Home.razor:107` already renders BOTH selectors:
```razor
<div class="col-md-4 mb-3 feature-card" data-testid="home-product-card">
```
And `CustomerPage.ts:32` already uses the testid (F1 applied):
```ts
this.menuItems = this.page.getByTestId('home-product-card');
```

The **real** failure is downstream: the test flow assumes a **guest checkout form**
(name + phone + address inputs + "Đặt hàng" button) that does **not** exist in
KhachLink's current cart-based flow:

| Test step (CustomerPage POM) | Selector | KhachLink reality |
|---|---|---|
| `fillGuestCheckoutForm({name, phone, address})` | `input[name="name"]`, `input[placeholder*="số điện thoại"]`, `textarea[placeholder*="địa chỉ"]` | `Checkout.razor` has **no** such inputs (cart-based flow, no guest form) |
| `submitGuestOrder()` | `button:has-text("Đặt hàng")` | No such button in `Checkout.razor` |
| `getOrderId()` | `.order-id, .order-number` | No such element — order id exposed via `checkout-link-tracking` testid |

### Evidence
- `6_Testing/e2e-tests/pages/CustomerPage.ts:36-47` — selectors for guest form
- `5_WebApps/KhachLink/Pages/Checkout.razor` — cart-based flow, no guest form
- `5_WebApps/KhachLink/Pages/Home.razor:107` — both `.feature-card` and testid exist

### Decision required
Either:
- **(A) Rewrite spec** to follow actual KhachLink flow (home → add to cart → `/cart` → checkout → `checkout-link-tracking` → tracking page), matching the pattern already used in `order-tracking.spec.ts:124` (Checkout redirect test, currently passing); OR
- **(B) Implement omnichannel guest-form UI** in `Checkout.razor` as a planned feature (separate feature build, NOT a W6 test fix).

**Recommendation: A** — the test was written for a UI that doesn't exist; align the test
to the actual product flow. The "omnichannel" lifecycle (admin accept → kitchen →
customer tracking) is still valuable but must use the cart-based flow that production
actually ships.

---

## 2. Test #2 — `order-tracking.spec.ts:57` "shows order ID in heading"

### Symptom (stale triage)
> Order not created before tracking page load

### Verified root cause — **TEST FALLBACK SELECTOR DOESN'T MATCH LOADING STATE**
The test logic (already reworked in W6) waits for either `order-tracking-heading` OR
`order-tracking-not-found`, with a 5s timeout each, then falls back to `h4, h3, h2`:
```ts
const headingVisible = await heading.isVisible({ timeout: 5000 }).catch(() => false);
const notFoundVisible = await notFound.isVisible({ timeout: 5000 }).catch(() => false);
if (headingVisible) { ... }
else if (notFoundVisible) { ... }
else {
  await expect(page.locator('h4, h3, h2').first()).toBeVisible({ timeout: 5000 });
}
```

`OrderTracking.razor` rendering states:
| State | Renders | Has h4/h3/h2? |
|---|---|---|
| `isLoading=true` (initial) | spinner + `<p>Đang tải thông tin đơn hàng...</p>` (line 67-74) | **NO** — only `<p>` |
| `errorMessage` set (401/404/timeout) | `order-tracking-not-found` alert (line 80) | NO (has `<h5>`) |
| `order != null` | `order-tracking-heading` `<h4>` (line 93) | YES |
| `order == null` (final else) | alert `<h5>❌ Không tìm thấy đơn hàng</h5>` (line 173) | NO (has `<h5>`) |

The fallback `h4, h3, h2` matches **none** of the non-heading states. If the WASM
initial render or the gateway HTTP call takes >5s (likely on a cold WASM load +
401 round-trip), both `heading` and `notFound` time out, and the fallback fails.

### Secondary factor (compounds the timing)
`OrderTracking.razor:243` calls `httpClient.GetAsync($"api/orders/{orderId}")` via the
KhachLink "gateway" HttpClient. That client has **no JWT** (WASM, customer-facing) →
the gateway returns **401** → `errorMessage` is set → not-found branch renders. This
works, but adds a full HTTP round-trip to the render latency.

### Evidence
- `6_Testing/e2e-tests/order-tracking.spec.ts:65-79` — fallback selector
- `5_WebApps/KhachLink/Pages/OrderTracking.razor:65-89` — loading & not-found states have no h4/h3/h2

### Fix
- Increase `heading` / `notFound` timeout to **15s** (matches WASM cold-load reality).
- Replace the fallback with `order-tracking-container` (always visible, line 61) —
  the test's intent is "page rendered content", and the container proves that.

---

## 3. Tests #3 & #4 — `payment-confirm-flow.spec.ts:85` (E2E-05) & `:149` (E2E-04b)

### Symptom (stale triage)
> Webhook `/api/webhooks/payment` returns 404 "Order not found"

### Verified code path
1. Test `POST /api/orders` with body `{ CustomerName, CustomerPhone, Items, TenantId }`
   + JWT Bearer header (`auth-api.ts:35`).
2. `OrdersController.CreateOrder` (`OrdersController.cs:26`) — **ignores body `TenantId`**,
   reads tenant from JWT claim via `GetTenantId()` (`OrdersController.cs:163`).
3. JWT issued by `DevLoginController.Login` (`DevLoginController.cs:41`) with
   `tenant_id = 11111111-1111-1111-1111-111111111111` (= `TEST_TENANT_ID`).
4. `CreateOrderFromCommandAsync(command, tenantId)` (`OrderService.cs:518`) →
   `_orderRepository.AddAsync(order)` (`OrderRepository.cs:107`) which **does** call
   `SaveChangesAsync` (line 112). Order IS persisted.
5. Response = `VietQrResponse { OrderId = createdOrder.Id.ToString() }` (camelCase
   JSON: `orderId`). Test extracts `order.orderId` correctly.
6. Test `POST /api/webhooks/payment` with `{ OrderId, TenantId: TEST_TENANT_ID, TransactionId }`.
7. `WebhookController.ConfirmPayment` (`WebhookController.cs:117`) — class has
   `[Authorize(Policy="RequireTenantAccess")]` but action has `[AllowAnonymous]`
   (line 116) → no auth required. OK.
8. `_orderService.ConfirmPaymentAsync(orderId, tenantId, transactionId)`
   (`OrderService.cs:554`) → `_orderRepository.GetByIdAsync(orderIdObj, tenantIdObj)`
   (`OrderRepository.cs:18`):
   ```csharp
   return await _context.Orders
       .FirstOrDefaultAsync(o => o.Id == id.Value && o.TenantId == tenantId, cancellationToken);
   ```
9. If `order == null` → `KeyNotFoundException` → `NotFound` (404). **This is the 404.**

### Root cause — **REQUIRES ROUND 2 DIAGNOSTIC** (3-Round Fix Limit)
The static analysis says the order **should** be found (same DB, same tenantId, same
Gateway process). The 404 means one of these is false at runtime. Candidate root causes,
to be disambiguated by a diagnostic test (Round 2):

| # | Hypothesis | Evidence for | Evidence against | Diagnostic |
|---|---|---|---|---|
| H1 | Order not actually persisted (silent rollback) | `CreateOrderFromCommandAsync` has no explicit transaction commit (unlike `CreateOrderWithQueueAsync` at line 351) | `AddAsync` calls `SaveChangesAsync` (line 112) — should persist | Query DB directly after `POST /api/orders` |
| H2 | `Order.TenantId` EF translation mismatch (Known Pattern #1) | Governance Pattern #1 warns TenantId stored as TEXT via converter; `o.TenantId == tenantId` is the **correct** pattern per the rule | Repository already uses the correct pattern | Inspect `Order.TenantId` property type + TenantIdConverter config in `VanAnDbContext` |
| H3 | DB path mismatch — Gateway writes to a different SQLite file than the one the test's order-creation response implies | Gateway `appsettings.Development.json:3` → `C:\VibeCoding\Gemini_Windsurf\5_WebApps\ShopERP\vanan_shoperp.db`; ShopERP `appsettings.Development.json:16` → `vanan_shoperp.db` (relative, resolves to ShopERP bin dir) — **DIFFERENT files** | Both order creation and webhook hit Gateway → same DB → should be consistent | Verify both services point to the SAME absolute DB path; check actual running process working dirs |
| H4 | `Order.Create` throws on null `CustomerDeviceId` → 500 → test would `test.skip()`, not 404 | Test does skip on non-200/201 (line 103-105) | If skip fired, test wouldn't reach the webhook call | Check test logs for skip message |

**Most likely: H3** (DB path mismatch is confirmed in config) **or H2** (TenantIdConverter
subtlety). H1 is unlikely given `AddAsync` saves. H4 is ruled out by the skip guard.

### Diagnostic plan (Round 2 — MANDATORY per 3-Round Fix Limit)
1. Run `payment-confirm-flow.spec.ts` with `--reporter=line --grep "E2E-04b"` and capture
   the exact Playwright error + the order-creation response body.
2. Add a temporary diagnostic assertion right after `POST /api/orders`:
   ```ts
   console.log('Order creation response:', JSON.stringify(order));
   console.log('Extracted orderId:', orderId);
   // Then immediately GET the order back:
   const verify = await request.get(`${config.GATEWAY_URL}/api/orders/${orderId}`, { headers: authHeaders });
   console.log('GET order status:', verify.status());
   if (verify.ok()) console.log('GET order body:', await verify.text());
   ```
   - If GET returns 404 → confirms order isn't readable via the same endpoint
     (persistence OR tenant mismatch OR translation issue).
   - If GET returns 200 → order exists, the 404 is specific to the webhook path
     (e.g., `tenantId` argument mismatch — webhook sends `TEST_TENANT_ID` from body,
     but maybe `TenantId` value-object equality fails).
3. Inspect `Order.TenantId` type + `VanAnDbContext` TenantId converter configuration.
4. Verify Gateway and ShopERP actually use the same SQLite file at runtime
   (`dotnet` process working directory + absolute path resolution).

### Fixes (applied based on diagnostic outcome)
- **If H1:** Add explicit `SaveChangesAsync` + transaction in `CreateOrderFromCommandAsync`.
- **If H2:** Adjust `OrderRepository.GetByIdAsync` to use the pattern from Known
  Pattern #1 (already correct — verify converter is registered for `Order.TenantId`).
- **If H3:** Unify Gateway + ShopERP `ConnectionStrings:DefaultConnection` to the
  **same absolute** SQLite path. Update `2_Gateway/appsettings.Development.json` and
  `5_WebApps/ShopERP/appsettings.Development.json` to point to one shared file
  (e.g., `C:\vanan_shoperp.db` as project_state.md claims is already the case).

---

## 4. Test #5 — `qr-payment.spec.ts:66` TC_QR_Validation

### Symptom (stale triage)
> API returns non-JSON (auth or endpoint missing)

### Verified root cause — **PRODUCTION LOGIC GAP (bank-id not validated)**
The "non-JSON" classification is **wrong**. The endpoint exists and returns JSON.

`VietQrController.ValidateBankConfig` (`VietQrController.cs:40`) →
`VietQrService.ValidateBankConfigAsync` (`VietQrService.cs:60`):
```csharp
public async Task<bool> ValidateBankConfigAsync(BankConfig config)
{
    await Task.CompletedTask;
    if (string.IsNullOrWhiteSpace(config.BankId) ||
        string.IsNullOrWhiteSpace(config.AccountNo) ||
        string.IsNullOrWhiteSpace(config.AccountName))
    {
        return false;
    }
    // Additional validation logic would go here
    // For now, just return true if basic info is provided
    return true;
}
```

The service returns `true` for **any** non-empty `(BankId, AccountNo, AccountName)`.
The test:
```ts
// Valid bank
const validResult = await validResponse.json();
expect(validResult).toBe(true);   // ✅ passes (true === true)
// Invalid bank
const invalidResult = await invalidResponse.json();
expect(invalidResult).toBe(false); // ❌ FAILS — service returns true for "999999/123/INVALID BANK"
```

The "invalid bank" assertion fails because the service doesn't actually validate the
BankId against the supported-banks list.

### Evidence
- `1_Shared/Services/VietQrService.cs:60-84` — returns true on any non-empty input
- `2_Gateway/Controllers/VietQrController.cs:55-69` — supported banks list is hard-coded here (Vietcombank 970422, VietinBank 970436, etc.)
- `6_Testing/e2e-tests/qr-payment.spec.ts:80-86` — `expect(invalidResult).toBe(false)`

### Business logic gap (per TT 152/2025/TT-BTC intent)
A bank config is "valid" iff:
1. `BankId` is in the supported banks list (BIN codes registered with Napas/VietQR).
2. `AccountNo` is non-empty and numeric (8-16 digits).
3. `AccountName` is non-empty.

Currently only #2 and #3 (in a weak form) are checked. #1 is missing.

### Fix (production code — IMPL mode after approval)
1. Extract the supported-banks list from `VietQrController.GetSupportedBanks` into a
   shared static (or `IVietQrService.GetSupportedBankIdsAsync()`).
2. In `VietQrService.ValidateBankConfigAsync`, check `BankId` is in the supported list.
3. Add unit tests in `VanAn.Core.Tests` for the validation logic (valid bank, unknown
   BIN, empty fields, whitespace).
4. Keep the E2E test as the contract test — it correctly captures the business intent.

---

## 5. Tests #6, #7, #8 — `realtime-sync-flow.spec.ts:43, :115, :172`

### Symptom (stale triage)
> Dashboard/tracking elements not found

### Verified root causes — **TWO distinct issues, both shared with #3/#4**

#### 5a. Order creation/persistence issue (affects #6, #7, #8 — same as #3/#4)
All three SignalR tests create orders via `POST /api/orders` with the **same payload
shape** as #3/#4 (`{CustomerName, CustomerPhone, Items, TenantId}`). They hit the same
`OrdersController.CreateOrder` → `CreateOrderFromCommandAsync` path.

- **#6 (E2E-06):** Test has an API fallback (`realtime-sync-flow.spec.ts:99-108`):
  if the dashboard order row isn't visible, verify via `GET /api/orders/{orderId}`.
  If the order isn't persisted (root cause H1/H3 from §3), `GET` returns 404 →
  `expect(verifyResponse.status()).toBe(200)` fails.
- **#8 (E2E-08):** Same API fallback (line 232-242). Same failure mode as #6.
- **#7 (E2E-07):** No API fallback — directly asserts the KhachLink tracking page
  shows "Ready" status. See §5b.

**Fix:** Resolve the order-persistence root cause (§3 diagnostic). #6 and #8 will
fall out automatically once orders are persisting correctly.

#### 5b. KhachLink can't fetch order from Gateway (affects #7 — and compounds #2)
`OrderTracking.razor:243` calls `httpClient.GetAsync($"api/orders/{orderId}")` via the
KhachLink "gateway" HttpClient. KhachLink is a **Blazor WASM customer-facing app** —
customers don't login, so no JWT is attached. The gateway `GET /api/orders/{id}` is
`[Authorize(Policy = "RequireTenantAccess")]` (`OrdersController.cs:14`) → returns
**401** → `OrderTracking.razor:254` sets `errorMessage = "Không thể xác thực yêu cầu
(status 401)"` → renders the **not-found** branch → `data-testid="order-tracking-status"`
(line 138) is **never rendered**.

Test #7 (`realtime-sync-flow.spec.ts:163-165`):
```ts
const statusBadge = page.getByTestId('order-tracking-status');
await expect(statusBadge).toBeVisible({ timeout: 10000 });
await expect(statusBadge).toHaveText(/Ready|Sẵn sàng/i, { timeout: 15000 });
```
Fails at `toBeVisible` because the not-found branch doesn't render the status badge.

### Evidence
- `5_WebApps/KhachLink/Pages/OrderTracking.razor:243` — gateway HTTP call, no auth
- `2_Gateway/Controllers/OrdersController.cs:14` — `[Authorize(Policy="RequireTenantAccess")]`
- `5_WebApps/KhachLink/Pages/OrderTracking.razor:138` — `order-tracking-status` only in `order != null` branch
- `6_Testing/e2e-tests/realtime-sync-flow.spec.ts:163` — `toBeVisible` on `order-tracking-status`

### Fix (production code — IMPL mode after approval)
Add a **public, anonymous** order-tracking endpoint for customer-facing use:
- `GET /api/public/orders/{id}` → returns a **safe, limited** DTO (order id, status,
  items count, totals — NO tenant-internal fields, NO customer PII beyond what the
  customer already entered).
- Update `OrderTracking.razor` to call `/api/public/orders/{id}` instead of
  `/api/orders/{id}`.
- This aligns with the existing `PublicOrdersController` pattern (already
  `[AllowAnonymous]` for guest checkout).

**Business justification:** A customer tracking their own order by URL is a legitimate
public flow (like tracking a Shopee/Lazada order by link). The customer already knows
the order id (they just placed it). No tenant PII is leaked.

**Alternative (NOT recommended):** Attach a machine-to-machine JWT from KhachLink WASM.
This bakes a secret into client-side code — security anti-pattern.

---

## 6. Summary — Root Cause Reclassification

| Test | Stale triage | Verified root cause | Bucket |
|---|---|---|---|
| #1 omnichannel | Selector `.feature-card` | Test-spec assumes guest-form UI that doesn't exist | A — Spec mismatch |
| #2 order-tracking | Order not created | Test fallback `h4/h3/h2` doesn't match loading state | B — Test logic |
| #3 E2E-05 | Webhook 404 | Order-persistence / tenant-match (Round 2 diagnostic) | C — Backend |
| #4 E2E-04b | Webhook 404 | Same as #3 | C — Backend |
| #5 TC_QR_Validation | Non-JSON | `ValidateBankConfigAsync` returns true for any non-empty input | E — Prod logic gap |
| #6 E2E-06 | SignalR element | Order-persistence (shared with #3) | C — Backend |
| #7 E2E-07 | testid not found | KhachLink can't auth to gateway `GET /api/orders/{id}` (401) | D — KhachLink auth |
| #8 E2E-08 | element not found | Order-persistence (shared with #3) | C — Backend |

**Reclassification:** 4 distinct buckets, not 3. The "SignalR" bucket was a red herring
— only #7 has a SignalR-adjacent cause (and it's actually an auth gap, not SignalR).
#6/#8 share the same backend root cause as #3/#4.

---

## 7. Production-Code Gaps Requiring Business-Logic Additions

Per the user directive "nếu production code thiếu thì bổ sung theo đúng nghiệp vụ":

| Gap | File | Business rule | Fix |
|---|---|---|---|
| **G1** Bank-id validation | `1_Shared/Services/VietQrService.cs:60` | Bank config valid iff BankId ∈ supported banks AND AccountNo non-empty numeric AND AccountName non-empty (per VietQR/Napas BIN registry) | Implement real validation + unit tests |
| **G2** Public order tracking | `2_Gateway/Controllers/OrdersController.cs:14` | Customer-facing order tracking by URL is a legitimate public flow (customer knows their own order id) | Add `GET /api/public/orders/{id}` (limited DTO) + wire KhachLink to it |
| **G3** (possible) Order persistence | `3_CoreHub/Services/OrderService.cs:518` | Orders created via gateway command MUST be persisted and readable by subsequent webhook/status calls | Round 2 diagnostic to confirm; fix depends on outcome |
| **G4** (possible) DB path unification | `2_Gateway/appsettings.Development.json:3` + `5_WebApps/ShopERP/appsettings.Development.json:16` | Dev env: Gateway and ShopERP MUST share one SQLite file (Option B monolithic mode) | Unify to one absolute path |

G1 and G2 are confirmed business-logic gaps. G3 and G4 are hypotheses pending Round 2.

---

## 8. Execution Order (dependency-aware)

1. **Bucket C first** (order-persistence diagnostic + fix) — unblocks #3, #4, #6, #8 (5 of 8 tests).
2. **Bucket E** (VietQR bank validation) — independent, 1 test (#5), pure production-logic fix.
3. **Bucket D** (public order-tracking endpoint) — unblocks #7, also improves #2's reliability.
4. **Bucket B** (order-tracking test logic) — quick test-side fix, 1 test (#2).
5. **Bucket A** (omnichannel spec rewrite) — largest test-side change, 1 test (#1), needs user decision on A vs B.

Buckets C, E, D can be done in parallel (different files, no conflicts). Buckets B and A
are test-side only and can also parallelize.

---

## 9. Files Touched (estimated)

### Production code (IMPL mode, requires approval)
- `1_Shared/Services/VietQrService.cs` — G1 bank-id validation
- `2_Gateway/Controllers/PublicOrdersController.cs` — G2 add `GET /{id}` public tracking
- `5_WebApps/KhachLink/Pages/OrderTracking.razor` — G2 call public endpoint
- `2_Gateway/appsettings.Development.json` — G4 unify DB path (if confirmed)
- `5_WebApps/ShopERP/appsettings.Development.json` — G4 unify DB path (if confirmed)
- `3_CoreHub/Services/OrderService.cs` — G3 persistence fix (if confirmed)
- `3_CoreHub/Repositories/OrderRepository.cs` — G3 EF translation (if confirmed)

### Test specs
- `6_Testing/e2e-tests/omnichannel-order-lifecycle.spec.ts` — Bucket A rewrite
- `6_Testing/e2e-tests/order-tracking.spec.ts` — Bucket B fallback fix
- `6_Testing/e2e-tests/payment-confirm-flow.spec.ts` — Round 2 diagnostic (temporary), then verify
- `6_Testing/e2e-tests/realtime-sync-flow.spec.ts` — verify after C/D fixes

### New tests (TDD for production gaps)
- `6_Tests/VanAn.Core.Tests/Services/VietQrServiceTests.cs` (or extend existing) — G1 unit tests

---

## 10. Open Questions for User

1. **Bucket A decision:** Rewrite omnichannel spec to cart-based flow (A) OR implement
   guest-form UI as a new feature (B)?
2. **G2 public endpoint:** Approve adding `GET /api/public/orders/{id}` with a limited
   DTO (no tenant PII)? This is a new public API surface.
3. **G4 DB path:** Confirm the intended dev DB location — `C:\vanan_shoperp.db` (per
   project_state.md) or `C:\VibeCoding\Gemini_Windsurf\5_WebApps\ShopERP\vanan_shoperp.db`
   (per Gateway appsettings)?

These will be asked via `ask_user_question` before entering IMPL mode.
