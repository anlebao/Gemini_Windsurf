# TASK CARD: P1.3 — ADMIN ADJUST POINTS FORM (khiếu nại — MVP)

> Created: 2026-10-22 (audit `docs/specs/loyalty-alliance-audit.md` §6 Q6 — user chốt: SystemAdmin adjust trực tiếp + audit, không workflow phê duyệt)
> Status: PLANNED
> Branch: `main`

## 1. GOAL & CONTEXT

- **Quyết định:** khiếu nại điểm → SystemAdmin điều chỉnh trực tiếp, đầy đủ audit (FR-13 MVP). Workflow phê duyệt defer — chỉ thêm khi HTX cần role riêng.
- **Reuse có sẵn:** internal API `POST /api/internal/loyalty/points/add|deduct` (Reason + IdempotencyKey + CustomerId) + `AuditTrail` + trang `/admin/loyalty-reconcile` (SystemAdmin, ShopERP). `ADJUST` ledger type đã tồn tại.
- **Thiếu duy nhất:** UI form + endpoint internal chọn đúng `ADJUST` type (add/deduct hiện ghi type gì — verify khi implement; nếu chưa có ADJUST path thì truyền qua Reason prefix hoặc thêm param nhỏ).

## 2. WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Skills (max 3):** `pattern-based-fixing` + `test-system-upgrade`

## 3. SCOPE

### 3.1 Form trên `/admin/loyalty-reconcile` (`5_WebApps/ShopERP/Components/Pages/Admin/LoyaltyReconcile.razor`)
- [ ] Card "Điều chỉnh điểm (ADJUST)": input `CustomerDeviceId` (guid) + `TenantId` (dropdown member — reuse tenant list đã có trên trang) + `Points` (số, cho phép âm) + `Reason` dropdown {Complaint, VoucherFailManual, DataFix, Other} + `Note` bắt buộc (chi tiết khiếu nại — audit).
- [ ] Submit → `IdempotencyKey = $"adjust:{Guid.NewGuid()}"` sinh client-side → call internal `points/add` (points>0) hoặc `points/deduct` (|points|) với `Reason = $"{reasonCode}:{note}"`.
- [ ] Confirm modal trước submit (ghi đè số dư — hành động nhạy cảm, pattern `PeriodClosing.razor` cảnh báo).
- [ ] Kết quả: hiển thị balance mới + link audit trail; lỗi (insufficient/wallet missing) → VanAAlert message thân thiện.

### 3.2 API gap check (verify khi implement)
- [ ] Nếu `points/add|deduct` chưa ghi `AllianceTransactionType.ADJUST` → thêm optional `TxType` trong request DTO, default giữ nguyên behavior (backward compat). ADJUST tx vẫn phải qua atomic-update path `b0fad8ee`.
- [ ] Idempotent mặc định qua key sinh client-side (retry safe).

### 3.3 Tests
- [ ] bUnit: form render + validate (deviceId rỗng/note rỗng → disable submit) + submit gọi đúng endpoint.
- [ ] Service-level (Core.Tests): `TxType=ADJUST` → ledger row type ADJUST (nếu thêm param).
- [ ] Deduct vượt balance → error trả UI, không ghi row.

## 4. DECISIONS (đã chốt)

| # | Quyết định |
|---|---|
| Q6 | Không workflow phê duyệt — SystemAdmin là đủ MVP; mọi adjust đều có `AuditTrail` + `Reason` bắt buộc + ledger ADJUST append-only (đủ truy vết LEG-05) |
| — | IdempotencyKey sinh client-side per-submit (không reuse) — tránh dedup 2 lần adjust cùng nội dung hợp lệ |

## 5. CONSTRAINTS

- Internal API giữ `[InternalApiKey]` — không mở endpoint mới cho user thường.
- `guard-check.ps1` + build + tests MUST PASS.

## 6. DoD

- [ ] Admin điều chỉnh được điểm ± với lý do + audit đầy đủ
- [ ] Ledger ghi `ADJUST` (hoặc Reason traceable) — truy vết được khiếu nại
- [ ] Idempotent, lỗi balance hiển thị rõ
