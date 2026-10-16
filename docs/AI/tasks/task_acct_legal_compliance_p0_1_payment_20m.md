# TASK CARD: P0.1 — KIỂM SOÁT THANH TOÁN ≥ 20 TRIỆU KHÔNG DÙNG TIỀN MẶT

> Created: 2026-10-16 (user directive master plan P0-P2)
> Master plan: `docs/AI/plans/accounting-legal-compliance-master-plan.md` (Phase P0.1)
> Branch: `main`
> Status: ✅ **Q1 CHỐT (2026-10-16) · ✅ W1 DONE + DEPLOYED + RV L1-L3 PASS (2026-10-16 — commits `8584edd8`+`19c7a642`+`5731ed31` · CD #38922403257 + #38932459737 SUCCESS · RV: POS 77tr cash modal cảnh báo + lý do bắt buộc + audit Order reason (fix async queue `5731ed31`) + báo cáo rà soát hiển thị đơn)**

---

## 1. GOAL & CONTEXT

- **Mục tiêu:** đóng lỗ hổng checklist **2.3** — tự động cảnh báo/khóa khấu trừ GTGT đầu vào & chi phí TNDN khi hóa đơn/chứng từ thanh toán từ **20.000.000 VNĐ** trở lên chọn hình thức *Tiền mặt*; theo dõi **gộp nhiều hóa đơn < 20 triệu cùng ngày cùng 01 nhà cung cấp**; cung cấp **Báo cáo rà soát cuối kỳ** phục vụ thanh tra.
- **Cơ sở pháp lý:** TT 219/2013/TT-BTC Điều 15 khoản 2 điểm b (GTGT) · TT 96/2015/TT-BTC Điều 4 (TNDN) · hướng dẫn TCT gộp hóa đơn cùng ngày.
- **Phát hiện review (2026-10-16):** `PaymentMethodConstants.cs` (CASH/VIETQR/CREDIT_CARD/COD/EXTERNAL → 111/112) **chỉ map tài khoản** — KHÔNG có bất kỳ kiểm soát ngưỡng 20 triệu nào. ❌ Thiếu hoàn toàn.
- **Đã xác minh code:** `Order.PaymentMethod` (Domain.cs:2154) · `POS/Payment.razor` (chọn phương thức thanh toán) · `Checkout.razor` (KhachLink) · phiếu chi `ExpenseEntry.razor`/`ManualEntryJournalBridge.cs` (mua hàng) · `WalletService` (COD/EXTERNAL).

## 2. ACTIVE WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT) — Wave 1 cùng P0.2.
- **Execution Mode:** IMPLEMENT (sau khi user chốt Q1).
- **Skills (max 3):** `domain-integrity-validation` (KHÔNG đụng Domain.cs) + `pattern-based-fixing` + `test-system-upgrade`.

## 3. SCOPE

### 3.1 Config (PG — không entity kế toán)
- [ ] Feature flag `Payment_20M_Control_Enabled` (default ON) + threshold config per-tenant `PaymentCashThreshold` (default `20_000_000`) — tái dùng cơ chế `ShopFeatureSettings`/`FeatureFlagService` hiện có.

### 3.2 Services (`3_CoreHub/Services/Payment/`)
- [ ] `IPaymentComplianceService`:
  - `EvaluateAsync(tenantId, amount, paymentMethod, vendor?, date)` → `PaymentComplianceResult { Severity: None|Warning|RequiresReason, ReasonRequired }` — trigger khi `PaymentMethod ∈ {CASH, COD}` và `amount ≥ threshold`.
  - `CheckSameDayAggregationAsync(tenantId, vendor, date)` — tổng hóa đơn/chứng từ **cùng ngày, cùng nhà cung cấp** ≥ ngưỡng → cảnh báo (đọc từ bảng chứng từ mua/phiếu chi theo `TransactionDate` + `Vendor`).
  - `GetCashPaymentReviewAsync(tenantId, year, month)` → `CashPaymentReviewDto` — **Báo cáo rà soát cuối kỳ**: danh sách chứng từ ≥ 20 triệu thanh toán tiền mặt + tổng + ghi chú lý do (phục vụ thanh tra).
- [ ] Hook: `OrderService` (create/payment path — POS + KhachLink checkout) + `ExpenseEntry`/`ManualEntryJournalBridge` (phiếu chi mua hàng) + P0.3 e-invoice issue — gọi EvaluateAsync, trả warning về UI.
- [ ] Audit: ghi `AuditLog` khi có xác nhận vượt ngưỡng (reason vào diễn giải/`Reason`) — tái dùng `AuditTrailService`.
- [ ] DI trong `3_CoreHub/Program.cs`; multi-tenancy filter mọi query (lesson a21f97f2).

### 3.3 UI (ShopERP — UI Platform 100%)
- [ ] `POS/Payment.razor` + `ExpenseEntry.razor` (loại chi phí/mua hàng): khi chọn tiền mặt ≥ ngưỡng → **modal cảnh báo** (theo Q1: yêu cầu xác nhận + ô lý do bắt buộc HOẶC chặn) — UI Platform component.
- [ ] Trang mới `/accounting/cash-payment-review` (Owner + ChiefAccountant): báo cáo rà soát cuối kỳ + In/Excel (pattern `vanAn.downloadFile`/EPPlus) + Sitemap + NavMenu.

### 3.4 Tests
- [ ] Core.Tests: ngưỡng 20M (19.999.999 → pass; 20.000.000 → warning; 21tr CASH → warning; VIETQR → pass) · gộp cùng ngày cùng NCC (2 hóa đơn 12tr + 12tr cùng ngày → warning) · khác NCC/khác ngày → pass · báo cáo rà soát đúng tổng · isolation tenant.
- [ ] bUnit: modal cảnh báo render + xác nhận lý do; trang báo cáo render.
- [ ] E2E spec (Gate 4 — chạy ở RV): phiếu chi 21tr tiền mặt → modal → báo cáo rà soát.

## 4. DECISIONS — ✅ Q1 CHỐT (2026-10-16)

| # | Câu hỏi | ✅ QUYẾT ĐỊNH |
|---|---|---|
| Q1 | Chặn cứng hay cảnh báo + xác nhận lý do? | ✅ **Cảnh báo mạnh + bắt buộc ghi lý do** (modal xác nhận, lý do lưu vào diễn giải + audit — không chặn cứng, tránh kẹt nghiệp vụ); báo cáo rà soát cuối kỳ là nguồn đối chiếu cho thanh tra |

## 5. CONSTRAINTS (governance)

- KHÔNG đụng Domain.cs (chỉ Services + UI + config) · KHÔNG entity/bảng kế toán mới (báo cáo tính động từ dữ liệu hiện có) · Multi-tenancy mọi query · UI Platform 100% (Gate 5) · E2E spec cho UI mới (Gate 4) · Playwright DISABLED trong IMPLEMENT · `guard-check.ps1` + `dotnet build VanAn.sln` + Core.Tests + ShopERP.Tests MUST PASS trước commit · Secrets: KHÔNG commit token.

## 6. SUCCESS CRITERIA (DoD)

- [ ] Cảnh báo/khóa khi chứng từ ≥ 20 triệu chọn tiền mặt (theo Q1) tại POS + phiếu chi + e-invoice
- [ ] Theo dõi gộp hóa đơn cùng ngày cùng NCC
- [ ] Báo cáo rà soát cuối kỳ + In/Excel
- [ ] Audit log xác nhận vượt ngưỡng (có lý do)
- [ ] guard + build + tests PASS · Deploy + RV production PASS → đóng

## 7. DEFERRED

- Tự động đối chiếu chứng từ thanh toán ngân hàng (sao kê) với hóa đơn · khấu trừ tự động (chỉ cảnh báo/ghi nhận) · ngưỡng theo nhóm ngành (hiện cố định 20 triệu theo quy định)