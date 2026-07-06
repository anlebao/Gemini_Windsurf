# MASTER PLAN — Test System Improvement (E2E Hardening + Pyramid Rebalance)

> **Status:** 📋 PLANNING — awaiting user review
> **Created:** 2026-07-07 · **Branch:** `feature/test-system-improvement`
> **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT) · **JIT Planning** per wave
> **Prerequisite:** Order Lifecycle Stream complete (W-1→W5 + edge case tests)

---

## 0. PROBLEM STATEMENT

### Current State (verified 2026-07-07)

| Metric | Value | Target |
|--------|-------|--------|
| Dotnet tests | ~1,290 | — |
| Playwright E2E tests | 704 (22 files) | — |
| E2E ratio | **35%** of total | **20%** (pyramid) |
| `data-testid` in UI components | 115 (ShopERP 111, KhachLink **4**) | ≥200 |
| `data-testid` in E2E specs | 56 (but **13/21 files have 0**) | 100% golden path |
| Hard-coded `waitForTimeout` | **16 instances** (6 files + 3 page objects) | 0 |
| E2E tier filtering | smoke/e2e/load/chaos (no sub-tier) | Smoke/Golden/Full |
| Test data cleanup | `TestDataCleaner` (HTTP DELETE) — **cannot clean AccountingEntry** | Test tenant + isolation |
| CI E2E execution | All 704 tests, `--grep-invert @slow` | Tier-based per CI context |

### 5 Root Causes

1. **No E2E sub-tier** — 704 tests run as one monolithic batch; no way to run only "golden paths" on PR
2. **`data-testid` gap** — KhachLink has 4 (vs ShopERP 111); order-flow.spec.ts uses CSS selectors (`.feature-card`)
3. **Hard-coded waits** — 16 `waitForTimeout` cause flaky tests; SignalR/polling timing non-deterministic
4. **No test tenant isolation** — E2E creates real orders/customers on staging; AccountingEntry immutable = permanent garbage
5. **Missing W3 Payment E2E** — Order Lifecycle W-1→W5 merged but no E2E for payment confirm flow

---

## 1. WAVE STRUCTURE (6 waves, 2 sprints)

### Dependency Chain
```
W0 (Tier filtering) ──→ W2 (Wait cleanup) ──→ W4 (Payment E2E)
                    ↘                        ↗
W1 (data-testid audit) ──→ W3 (Test tenant) ──→ W5 (SignalR E2E)
```

- **W0 + W1** independent, can parallel
- **W2** depends W0 (tier tags needed to scope wait cleanup)
- **W3** depends W1 (test tenant needs data-testid for cleanup selectors)
- **W4** depends W2+W3 (new E2E needs fluent waits + test tenant)
- **W5** depends W4 (SignalR pattern builds on payment E2E)

### Sprint 1 (Foundation): W0 + W1 + W2 + W3
### Sprint 2 (Coverage): W4 + W5

---

## 2. WAVE SUMMARY

| Wave | Title | Gap Fixed | Est. Files | Est. Lines |
|------|-------|-----------|------------|------------|
| W0 | E2E Tier Filtering (Smoke/Golden/Full) | #1 No sub-tier | 4 | ~200 |
| W1 | `data-testid` Audit (KhachLink + E2E specs) | #2 Selector gap | ~15 | ~300 |
| W2 | Hard-coded Wait Cleanup | #3 Flaky waits | 9 | ~150 |
| W3 | Test Tenant + Accounting Cleanup Strategy | #4 Data isolation | 5 | ~250 |
| W4 | E2E for W3 Payment Confirm Flow | #5 Missing coverage | 2 | ~400 |
| W5 | SignalR E2E Pattern + Cross-system Timing | #5 Deep coverage | 2 | ~350 |

---

## 3. WAVE DETAILS (compact — full coding plan in task cards)

### W0: E2E Tier Filtering (Smoke / Golden / Full)

**Objective:** Split 704 E2E tests into 3 tiers with tag-based filtering.

**Tier definition:**
- **Tier 0 (Smoke, ~10 tests):** Health checks only — `@smoke` tag
- **Tier 1 (Golden Path, ~30 tests):** 3 critical flows — `@golden` tag
  - Order placement (KhachLink → checkout → tracking)
  - Payment confirm (webhook → admin confirm → accounting)
  - Kitchen complete → order Ready → dashboard update
- **Tier 2 (Full, ~700 tests):** Everything else — no tag needed

**CI execution matrix:**
| CI Context | Tiers run | Est. time |
|------------|-----------|-----------|
| PR check | Tier 0 + Tier 1 | ~2 min |
| Merge to main | Tier 0 + Tier 1 + Tier 2 | ~15 min |
| Nightly | All tiers × all browsers | ~45 min |

**Files:** `playwright.config.ts` (projects split), `env-config.ts` (tier config), 2 CI workflow YAMLs, task card
**Task card:** `docs/AI/tasks/test_w0_tier_filtering_task_card.md`

---

### W1: `data-testid` Audit

**Objective:** Add `data-testid` to all interactive elements in KhachLink + refactor E2E specs to use them.

**Scope:**
- KhachLink components: Home.razor, Cart.razor, Checkout.razor, OrderTracking.razor, RealTimeDashboard.razor (currently 4 `data-testid` → target ~40)
- E2E spec refactor: `order-flow.spec.ts`, `order-tracking.spec.ts`, `qr-payment-ui.spec.ts`, `omnichannel-order-lifecycle.spec.ts` (0 `data-testid` → use `getByTestId()`)

**Convention:** `data-testid="{component}-{element}-{action}"` (e.g., `data-testid="checkout-btn-submit"`)

**Files:** ~10 KhachLink `.razor` + ~5 E2E `.spec.ts` + task card
**Task card:** `docs/AI/tasks/test_w1_data_testid_audit_task_card.md`

---

### W2: Hard-coded Wait Cleanup

**Objective:** Replace all 16 `waitForTimeout`/`setTimeout` with fluent polling.

**Pattern:**
```typescript
// BAD: page.waitForTimeout(3000)
// GOOD:
await expect(page.locator('[data-testid="order-status"]'))
  .toHaveText('Ready', { timeout: 10000 });
// Or for API-driven state:
await page.waitForResponse(r => r.url().includes('/api/orders') && r.status() === 200);
```

**Files affected (9):** `voice-command.spec.ts` (4), `period-closing-flow.spec.ts` (1), `KitchenPage.ts` (3), `AdminPage.ts` (1), `strict-assert.ts` (1), + page objects
**Task card:** `docs/AI/tasks/test_w2_wait_cleanup_task_card.md`

---

### W3: Test Tenant + Accounting Cleanup Strategy

**Objective:** Isolate E2E test data from staging; solve AccountingEntry immutability.

**Approach (Option A — Dedicated Test Tenant):**
- Create fixed `TestTenantId` (Guid `00000000-0000-0000-0000-testtenant01`)
- E2E `global-setup.ts` authenticates as test tenant user
- `TestDataCleaner` enhanced: delete orders/customers for test tenant only
- **Accounting entries:** Accept as test tenant garbage (immutable by design) — OR add `E2E_TEST_MODE` flag to `OrderService.ConfirmPaymentAsync` that skips accounting entry creation (gated by env var, NOT Domain modification)

**Safety:** Test tenant data filtered out of production reports via `TenantId != TestTenantId` guard

**Files:** `test-data-cleaner.ts`, `global-setup.ts`, `env-config.ts`, `appsettings.Development.json` (test tenant config), task card
**Task card:** `docs/AI/tasks/test_w3_test_tenant_task_card.md`

---

### W4: E2E for W3 Payment Confirm Flow

**Objective:** Cover the 2 new payment flows from Order Lifecycle W3.

**2 E2E scenarios:**
- **E2E-04:** KhachLink "Tôi đã thanh toán" → `POST api/webhooks/payment` → ShopERP Orders/Detail `PaymentStatus=Paid` → SignalR broadcast
- **E2E-05:** Admin "Xác nhận đã nhận tiền" → accounting entries generated → `PaymentStatus=Paid`

**Prerequisites:** W1 (`data-testid` on payment buttons), W2 (fluent waits for SignalR), W3 (test tenant for cleanup)

**Files:** `payment-confirm-flow.spec.ts` (new, ~200 lines), `pages/PaymentPage.ts` (new), task card
**Task card:** `docs/AI/tasks/test_w4_payment_e2e_task_card.md`

---

### W5: SignalR E2E Pattern + Cross-system Timing

**Objective:** Verify real-time updates across SignalR + NATS + polling — the gap that unit tests cannot cover.

**3 E2E scenarios:**
- **E2E-06:** Order status change → SignalR broadcast → ShopERP dashboard auto-updates (no page reload)
- **E2E-07:** Kitchen complete all items → OrderStatus=Ready → KhachLink OrderTracking updates via polling
- **E2E-08:** SignalR disconnect → reconnect → dashboard shows latest state (not stale)

**SignalR-specific pattern:**
```typescript
// Verify Blazor SignalR connection state
const connState = await page.evaluate(() => (window as any).__blazorConnection?.state);
// Assert UI reflects server state after reconnect
await expect(page.locator('[data-testid="order-status"]')).toHaveText('Ready', { timeout: 15000 });
```

**Files:** `realtime-sync-flow.spec.ts` (new, ~250 lines), `pages/RealtimePage.ts` (new), task card
**Task card:** `docs/AI/tasks/test_w5_signalr_e2e_task_card.md`

---

## 4. EXECUTION RULES

### JIT Planning (per wave)
```
Phase 1 (INVESTIGATE): Verify current state (grep counts, file paths, CI config)
Phase 2 (PLAN): Read task card, confirm file:line changes
Phase 3 (IMPLEMENT): Code + verify + commit
```

### Session protocol
1. 1 wave per session
2. Start: read `project_state.md` + task card
3. End: `dotnet build` pass + `npx playwright test --list` pass + commit
4. Commit format: `[TEST-SYSTEM WAVE X] <short description>`

### Branch protocol
```
main ← feature/test-w0-tier-filtering
main ← feature/test-w1-data-testid-audit
main ← feature/test-w2-wait-cleanup
main ← feature/test-w3-test-tenant
main ← feature/test-w4-payment-e2e
main ← feature/test-w5-signalr-e2e
```

### Hard rules
- **KHÔNG sửa Domain.cs** — test system improvement only
- **KHÔNG sửa C# business logic** (trừ W3 test-mode flag nếu approved)
- **KHÔNG xóa E2E test có giá trị** — chỉ tag/reorganize
- **Mỗi wave phải pass `npx playwright test --list`** (TypeScript parse OK)
- **W3 test-mode flag:** Nếu thêm `E2E_TEST_MODE` vào `OrderService`, phải gate bằng env var + NOT modify Domain
- **AccountingEntry immutable:** Hard Stop — không bypass kể cả trong test mode

---

## 5. VERIFICATION CHECKLIST (final)

- [ ] Build 0 errors (`dotnet build VanAn.sln`)
- [ ] `npx playwright test --list` — 0 parse errors
- [ ] Tier 0 (Smoke) runs in <2 min
- [ ] Tier 1 (Golden Path) runs in <5 min
- [ ] 0 hard-coded `waitForTimeout` in golden path tests
- [ ] 100% golden path tests use `getByTestId()` (0 CSS selectors)
- [ ] KhachLink has ≥40 `data-testid` attributes
- [ ] Test tenant isolation: E2E data does not pollute production tenant
- [ ] E2E-04/05 (payment confirm) PASS
- [ ] E2E-06/07/08 (realtime sync) PASS
- [ ] CI PR check runs Tier 0 + Tier 1 only
- [ ] No regression in existing 1,290 dotnet tests

---

## 6. RISK REGISTER

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| SignalR E2E flaky (W5) | High | Medium | Use 15s timeout + retry 2x + trace on-first-retry |
| Test tenant config breaks staging | Low | High | Gate by env var `E2E_TEST_TENANT_ID`; default = empty (disabled) |
| `data-testid` refactor breaks existing tests | Medium | Low | Run full suite before/after; incremental per file |
| AccountingEntry garbage on test tenant | Medium | Low | Accept (immutable by design) OR W3 test-mode flag |
| CI time increase from new E2E | Low | Low | Tier filtering ensures only golden path runs on PR |

---

## 7. SUCCESS METRICS

| Metric | Before | After (target) |
|--------|--------|----------------|
| E2E ratio | 35% | ~22% |
| Hard-coded waits | 16 | 0 (golden path) |
| `data-testid` in KhachLink | 4 | ≥40 |
| E2E specs using `getByTestId` | 8/21 | 100% golden path |
| PR CI E2E time | ~15 min (all) | ~2 min (Tier 0+1) |
| Payment E2E coverage | 0 | 2 scenarios |
| Realtime E2E coverage | 0 | 3 scenarios |
