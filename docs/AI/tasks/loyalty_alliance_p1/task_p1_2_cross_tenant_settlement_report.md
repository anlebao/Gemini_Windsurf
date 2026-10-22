# TASK CARD: P1.2 — CROSS-TENANT SETTLEMENT REPORT (read-only)

> Created: 2026-10-22 (audit `docs/specs/loyalty-alliance-audit.md` §6 Q4 — user chốt: member chịu redeem tại mình + report cho HTX settle ngoài hệ thống)
> Status: PLANNED
> Branch: `main`

## 1. GOAL & CONTEXT

- **Quyết định:** mỗi member tự chịu chi phí redeem tại shop mình (status quo). Để HTX settle thủ công định kỳ, cần **computed report** cho thấy ai "cho điểm nhiều hơn nhận về":
  - `EarnCredit[M]` = Σ points `EARN` có `TransactionTenantId = M` (khách tích ở M — M được hưởng doanh thu gốc)
  - `RedeemCost[M]` = Σ |points| `REDEEM` có `TransactionTenantId = M` (M chịu chi phí ưu đãi)
  - `NetSettlement[M]` = EarnCredit − RedeemCost → HTX dùng để chuyển khoản/hóa đơn **ngoài hệ thống**.
- **Không ledger mới, không auto-settlement** — report read-only từ `AllianceTransactions` (đã đủ `TransactionTenantId`/`SourceTenantId`/`Type`/`Points`/`TransactionAt`).

## 2. WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Skills (max 3):** `pattern-based-fixing` + `test-system-upgrade`

## 3. SCOPE

### 3.1 Gateway API
- [ ] `GET /api/v1/admin/loyalty/alliance/settlement-report?from=&to=` — `[Authorize(SystemAdmin)]`, aggregate query:
  ```sql
  SELECT "TransactionTenantId", "Type", SUM("Points")
  FROM "AllianceTransactions"
  WHERE "TransactionAt" BETWEEN @from AND @to
  GROUP BY "TransactionTenantId", "Type"
  ```
  → group ở C#: per tenant {EarnCredit, RedeemCost, AdjustNet, NetSettlement}. Join `Tenants` lấy tên member.
- [ ] Filter khoảng thời gian (default: tháng trước — chu kỳ settle hợp lý).

### 3.2 UI
- [ ] Section "Đối soát liên minh" trên `/admin/loyalty-reconcile` (trang sẵn có, SystemAdmin) — bảng member × {earn, redeem, net} + date-range picker + tổng cuối (Σ phải = 0 chênh lệch nội bộ → sanity check hiển thị).
- [ ] Gọi qua `AllianceWalletServiceHttpProxy`-style internal client hoặc admin API client có sẵn — KHÔNG inject `VanAnDbContext` vào page (ShopERP = SQLite mirror không có AllianceTransactions).

### 3.3 Tests
- [ ] Service-level: seed EARN/REDEEM nhiều tenant → aggregation đúng per tenant + khoảng ngày.
- [ ] REDEEM âm → RedeemCost dương (`Math.Abs`).
- [ ] Tenant không có tx → không xuất hiện (hoặc 0 — chọn 1, ghi rõ).
- [ ] bUnit page: render bảng + date filter call đúng.

## 4. DECISIONS (đã chốt)

| # | Quyết định |
|---|---|
| Q4 | Read-only computed report — settlement diễn ra ngoài hệ thống (hóa đơn/chuyển khoản giữa HTX và member) |
| — | `ADJUST` type cũng hiển thị (điều chỉnh tay ảnh hưởng đối soát — transparency) |

## 5. CONSTRAINTS

- Read-only — KHÔNG ghi gì vào PG.
- `guard-check.ps1` + build + tests MUST PASS.

## 6. DoD

- [ ] Report số liệu khớp `AllianceTransactions` (spot-check SQL)
- [ ] UI render + date filter hoạt động
- [ ] 0 writes, 0 entity mới
