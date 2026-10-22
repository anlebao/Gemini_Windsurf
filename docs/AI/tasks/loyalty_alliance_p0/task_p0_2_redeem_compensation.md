# TASK CARD: P0.2 — REDEEM ORPHAN DEDUCTION (voucher fail → điểm mất không chứng từ)

> Created: 2026-10-22 (audit Risk R2, SRS TC-10)
> Status: IMPLEMENTED 2026-10-22 — compensation + LoyaltyReconcileCase + admin view; tests pass
> Branch: `main`
> Depends: P0.1 (lock trước, tránh compensation đua với deduct khác)

## 1. GOAL & CONTEXT

- **Defect:** `RedemptionService.RedeemAsync` Alliance branch: deduct PG wallet **trước** → tạo `RedemptionRecord`+`Voucher` ở SQLite **sau**. Nếu bước 2 fail (SQLite lỗi, crash, network drop giữa chừng) → PG đã trừ điểm, không voucher, khách mất điểm vô hình. Code tự ghi nhận: *"PG deduction NOT refunded (manual reconcile needed)"* (`RedemptionService.cs:172-175`).
- **SRS TC-10:** "giao dịch được hoàn tất hoặc giải phóng điểm đúng một lần; không mất điểm và không cấp ưu đãi hai lần".
- **Hai hướng sửa:**
  - (a) **Compensation:** voucher-creation fail → auto `RefundAsync` với idempotency-key `compensate:{voucherCode}`.
  - (b) **Pending-first saga:** tạo `RedemptionRecord` status Pending trước → deduct → confirm record. Fail giữa chừng → record Pending mồ côi, job dọn + refund.
- **Đánh giá:** (a) đơn giản hơn nhiều, phủ được hầu hết case; (b) đúng distributed-transaction hơn nhưng thêm state machine + cleanup job. Đề xuất **(a) + failure-log row** — nếu cả compensation fail thì ghi `RedemptionReconcileRecord` để đối soát thủ công (thay vì chỉ log text).

## 2. WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Skills:** `pattern-based-fixing` + `domain-integrity-validation`

## 3. SCOPE

### 3.1 Compensation (`RedemptionService.cs` Alliance branch :143-177)
- [ ] Trong `catch` sau local-tx fail: gọi `_allianceWalletService.RefundAsync(deviceGuid, tenantId, points, "Compensation: voucher creation failed", voucherCode, idempotencyKey: $"compensate:{voucherCode}", customerId)`.
- [ ] Compensation fail nữa → persist `LoyaltyReconcileCase` (bảng nhẹ, PG): `{VoucherCode, CustomerDeviceId, TenantId, Points, FailedAt, Reason, ResolvedAt?}` → báo cho ops xử lý. KHÔNG chỉ log.
- [ ] Message trả khách: vẫn "liên hệ hỗ trợ" nhưng kèm mã voucherCode để tra cứu.

### 3.2 Entity nhẹ `LoyaltyReconcileCase` (PG)
- [ ] Domain entity mới (Domain Phase — cần approval): fields trên + `CaseStatus { Open, Resolved }`. Append-only updates qua status transition.
- [ ] Admin view `/admin/loyalty-reconcile` (SystemAdmin): list Open cases + nút Resolve (ghi note + ResolvedAt). UI Platform component.

### 3.3 Symmetric gap — deduct-success + voucher-exists-nhưng-record-fail
- [ ] Audit thêm: `CancelAsync` refund path cũng nên dùng cùng idempotency-key scheme (`refund:{recordId}` đã có — verify).

### 3.4 Tests
- [ ] Voucher-create throw → compensation refund gọi đúng, balance về lại, tx ADJUST có voucherCode.
- [ ] Compensation fail → `LoyaltyReconcileCase` Open row tồn tại.
- [ ] Idempotent: retry redeem cùng voucherCode → không trừ 2 lần (đã có) + không refund 2 lần.

## 4. DECISIONS

| # | Câu hỏi | Đề xuất |
|---|---|---|
| Q1 | (a) compensation vs (b) pending-saga? | (a) + reconcile case — scope nhỏ, cover đủ; saga để phase sau nếu case thực tế nhiều |
| Q2 | Auto-resolve reconcile case? | Không — ops duyệt tay, audit trail |
| Q3 | `LoyaltyReconcileCase` entity mới OK? | Cần Domain approval (rule: Domain change needs approval) |

## 5. CONSTRAINTS

- Compensation dùng ĐÚNG `RefundAsync` (đã idempotent) — không viết deduct-path mới.
- Không chặn redeem hiện tại — fix là fail-safe, không đổi happy path.
- `LoyaltyReconcileCase` theo Single-Identity + multi-tenancy (tenant-scoped theo redeeming tenant).

## 6. DoD

- [ ] Voucher fail → điểm hoàn tự động (test prove)
- [ ] Worst-case → reconcile case visible ở admin, không mất vết
- [ ] Tests PASS, không regression redeem/CancelAsync
