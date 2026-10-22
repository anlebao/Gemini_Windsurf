# TASK CARD: P0.3 — AWARD-CHỈ-Ở-COMPLETED + REFUND REVERSAL SAFETY NET

> Created: 2026-10-22 (audit Risk R5, SRS FR-09/TC-03) · Updated 2026-10-22 (user decision D-Q1)
> Status: IMPLEMENTED 2026-10-22 — delivered-award removed, flag default ON; tests pass
> Branch: `main`

## 0. QUYẾT ĐỊNH ĐÃ CHỐT (user 2026-10-22)

**D-Q1: Chỉ ghi nhận điểm khi đơn `completed`** — không award ở `delivered`.
→ Loại bỏ gốc edge case "khách đã tiêu điểm rồi đơn bị hoàn": `completed` là terminal trong builtin flow (`OrderFlowService.cs:145` — không allowed-next).
→ Reversal giữ lại làm **safety net** cho: (a) tenant tự cấu hình flow `completed→cancelled` (flow là per-tenant configurable — `OrderFlowProfile`), (b) refund thủ công ngoài status transition, (c) backfill dữ liệu cũ.

**Trade-off đã biết:** `#99-2` delivered-award tồn tại vì chủ shop quên bấm "completed" → khách không thấy điểm. User chấp nhận: điểm chỉ hiện khi completed (đổi lấy không cần reversal happy-path). **Không implement** "award-pending ở delivered" — giữ đơn giản.

## 1. GOAL & CONTEXT

- **Defect A:** award chạy ở `delivered` (`OrderWorkflowService.cs:141-150`) → khách có thể tiêu điểm trước khi đơn completed → hủy đơn = vấn đề point-debt.
- **Defect B:** `ValcnV2_RefundReversal` default OFF → đơn cancel không đảo điểm đã award = farm-điểm leak (vẫn áp dụng cho đơn cancel TRƯỚC completed, vì award ở delivered đã chạy; sau khi gỡ delivered-award thì chỉ còn edge case flow-custom + refund thủ công).
- **Code reversal ĐÃ TỒN TẠI:** `LoyaltyPointLedgerService.RevertOrderAsync` + `RefundOrchestrationService` 4-step. Việc: gỡ delivered-award + bật flag + verify.

## 2. WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Skills:** `pattern-based-fixing` + `test-system-upgrade`

## 3. SCOPE

### 3.1 Gỡ delivered-award (D-Q1)
- [ ] `OrderWorkflowService.cs:141-150`: bỏ branch `newStatus=="delivered"` → `ProcessLoyaltyPointsAsync` — chỉ award ở `completed` (qua `HandleOrderCompletedAsync`).
- [ ] Double-award guard giữ nguyên (`LoyaltyIssuanceRecord` + silo-history fallback) — vẫn cần vì delivered→completed transition cũ có thể đã award.
- [ ] **Không** đụng legacy fallback path — chỉ gỡ trigger.

### 3.2 Verify + bật `ValcnV2_RefundReversal` (safety net)
- [ ] Audit `RefundOrchestrationService.OrchestrateReversalAsync`: 4 steps idempotent + isolated (1 step fail không làm mất các step khác?)
- [ ] Partial refund / hoàn một phần: hiện có chưa, hay chỉ full-cancel? (SRS FR-09 đòi cả — nếu thiếu thì scope riêng)
- [ ] `completed→cancelled` flow-custom tenant: reversal phải bắt được (issuance record tồn tại bất kể status).
- [ ] Backfill đơn cũ: report `orders cancelled ∧ LoyaltyIssuanceRecord !IsReversed` → user duyệt → batch revert có giám sát.
- [ ] Bật flag → RV trên staging → production.

### 3.3 Tests
- [ ] Award KHÔNG chạy ở delivered; chạy đúng 1 lần ở completed (delivered→completed không double).
- [ ] Cancel đơn đã-award-ở-delivered (dữ liệu cũ) → reversal đảo đúng.
- [ ] Flow-custom `completed→cancelled` → reversal chạy.
- [ ] Alliance + Silo cả 2 nhánh; flag OFF → backward compat.

## 4. DECISIONS

| # | Câu hỏi | Quyết định |
|---|---|---|
| Q1 | Khách tiêu điểm rồi đơn bị hoàn? | ✅ **CHỐT — không xảy ra trong builtin flow** (award chỉ ở completed=terminal). Reversal chỉ là safety net cho flow-custom/backfill: trừ tối đa balance, phần thiếu bỏ (hành vi hiện tại "points retained" — chấp nhận cho edge case) |
| Q2 | Partial refund đảo một phần điểm? | Để task riêng nếu audit phát hiện thiếu (scope creep risk) |
| Q3 | Backfill đơn cũ | Report → user duyệt → batch (đã nêu §3.2) |

## 5. CONSTRAINTS

- KHÔNG sửa `LoyaltyIssuanceRecord`/`AllianceTransaction` đã ghi — reversal là tx mới (append-only).
- `HandleOrderCancelledAsync` reversal fail không được fail cancel (đã đúng — giữ nguyên safe-fail).
- Backfill chạy 1 lần, có report trước, user duyệt.

## 6. DoD

- [ ] Cancel đơn đã award → điểm đảo đúng cả Silo lẫn Alliance
- [ ] Không re-reversal, không âm giả tạo
- [ ] Backfill report + batch (nếu duyệt) chạy xong, có log
- [ ] Flag bật trên staging → RV pass → production
