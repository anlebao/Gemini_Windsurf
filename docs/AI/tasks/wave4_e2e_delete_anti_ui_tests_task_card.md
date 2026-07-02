# TASK CARD: E2E Cleanup - Wave 4 - Delete Anti-UI Tests (Pattern G2)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Xóa 2 test scenarios trong `omnichannel-order-lifecycle.spec.ts` dùng selectors UI không tồn tại (loyalty points, offline indicator)
- **Nghiệp vụ áp dụng:** E2E test integrity — test phải match UI thật
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/e2e-cleanup-wave4-delete-anti-ui-tests`
- **Estimated Sessions:** 1

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 4 of 8
- **Dependency:** Wave 3 merged

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/e2e_test_cleanup_master_plan.md` (READ)
- `5_WebApps/KhachLink/Components/PWA/PWAInstallPrompt.razor` (READ — confirm `offline-indicator` là PWA install, không phải offline status)
- `5_WebApps/KhachLink/Pages/OrderTracking.razor` (READ — confirm không có loyalty)
- `6_Testing/e2e-tests/omnichannel-order-lifecycle.spec.ts` (UPDATE — xóa SCENARIO 2 + 3)
- `6_Testing/e2e-tests/utils/test-data-cleaner.ts` (UPDATE — xóa `generateLoyaltyCustomerData` nếu không còn dùng)
- `6_Testing/e2e-tests/pages/CustomerPage.ts` (READ — confirm loyalty selectors không có thật)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa code C# — UI không có loyalty là fact, test sai
- KHÔNG fix test bằng cách tạo UI loyalty — out of scope (feature mới)
- KHÔNG xóa `SCENARIO 1: First-Time Guest Omnichannel Order Flow` — có thể pass
- KHÔNG xóa `CustomerPage.ts` methods khác (chỉ đọc để confirm)

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Selector Verification:** `loyalty-points`, `points-balance`, `applyLoyaltyPoints` — 0 match trong KhachLink (grep confirmed)
- [ ] **Offline Indicator:** `offline-indicator`, `network-status` — chỉ trong `PWAInstallPrompt.razor` (PWA install prompt, không phải offline status)
- [ ] **setOffline + goto:** `context.setOffline(true)` rồi `page.goto()` — goto sẽ fail vì không có network
- [ ] **Parse Check:** `npx playwright test --list` pass sau xóa

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `SCENARIO 2: Returning Loyalty Customer Flow` đã xóa khỏi `omnichannel-order-lifecycle.spec.ts`
- [ ] **SC2:** `SCENARIO 3: Network Interruption / Edge Offline Resiliency` đã xóa
- [ ] **SC3:** `SCENARIO 1: First-Time Guest Omnichannel Order Flow` còn lại nguyên vẹn
- [ ] **SC4:** `TestDataGenerator.generateLoyaltyCustomerData` đã xóa nếu không còn reference
- [ ] **SC5:** 0 reference đến `loyaltyPointsDisplay`, `applyLoyaltyPoints`, `simulatePaymentSuccess` trong test files (trừ CustomerPage.ts — giữ method nếu SCENARIO 1 dùng)
- [ ] **SC6:** `npx playwright test --list` pass

---

## 6. ACTIVE SKILLS (MAX 3)
- `pattern-based-fixing` — Xóa test scenario theo anti-UI pattern
- `build-error-analysis` — Fix TS parse error nếu có

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 4
- **Verified Facts:**
  - Fact 1: `loyalty-points`, `points-balance` — 0 match trong `5_WebApps/KhachLink` (grep confirmed)
  - Fact 2: `offline-indicator`, `network-status` — chỉ trong `PWAInstallPrompt.razor` (9 matches, PWA install context)
  - Fact 3: `OrderTracking.razor` tồn tại (L1, L53, L54) — không có loyalty fields
  - Fact 4: `CustomerPage.ts` L44 `loyaltyPointsDisplay = page.locator('.loyalty-points, .points-balance')` — selector không match UI thật
- **Assumptions:**
  - SCENARIO 1 dùng `customerPage.addFirstItemToCart`, `proceedToCheckout`, `fillGuestCheckoutForm` — selectors thật, có thể pass
  - `generateLoyaltyCustomerData` chỉ dùng trong SCENARIO 2 — xóa được
- **Open Questions:**
  - Q1: `CustomerPage.ts` methods `getLoyaltyPoints`, `applyLoyaltyPoints`, `simulatePaymentSuccess` — xóa hay giữ? (Recommend: giữ — có thể dùng cho feature loyalty tương lai, không break)
- **Recommended Action:** PROCEED — xóa 2 scenarios

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `omnichannel-order-lifecycle.spec.ts` | Giảm từ 3 scenarios → 1 scenario | Positive — xóa test vô giá trị |
| `test-data-cleaner.ts` | Mất `generateLoyaltyCustomerData` | OK — không còn reference |

---

## 9. TDD & TESTING STRATEGY
- **Parse check:** `npx playwright test --list` sau xóa
- **Runtime check:** Skip
- **Verification:** `npx playwright test --list` pass

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược: Delete-Only
1. Đọc `omnichannel-order-lifecycle.spec.ts` → identify SCENARIO 2 + 3 line ranges
2. Xóa SCENARIO 2 block (từ `test('SCENARIO 2...` đến `});` đóng)
3. Xóa SCENARIO 3 block
4. Verify `npx playwright test --list` pass
5. Check `test-data-cleaner.ts` — xóa `generateLoyaltyCustomerData` nếu không còn reference

### Micro-phase breakdown cho Wave 4

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Confirm line ranges SCENARIO 2 + 3<br>- Confirm `generateLoyaltyCustomerData` chỉ dùng trong SCENARIO 2<br>- Chốt: giữ SCENARIO 1 + CustomerPage.ts methods | - Xóa SCENARIO 2 + 3 trong omnichannel<br>- Xóa `generateLoyaltyCustomerData` trong test-data-cleaner.ts<br>- Run `npx playwright test --list`<br>- Commit |

### Rules
- Xóa toàn bộ `test(...)` block — từ `test('SCENARIO ...` đến `});` đóng
- KHÔNG xóa `import` statements nếu SCENARIO 1 còn dùng
- Verify parse sau xóa

---

## 11. ESTIMATED EFFORT
- 0.5 session (2 scenarios xóa + 1 utility method)
- **BLOCKER:** None
