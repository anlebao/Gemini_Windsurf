# Task Card 185-3: Tắt "chương trình điểm thưởng" vẫn hiển thị điểm trong luồng mua hàng

> **Status:** DONE 2026-09-23 — commit 8a47f40a — Phase A (client gates) + Phase B (Outbox→NATS→PG upsert) implemented
> **Priority:** P1 — toggle không có tác dụng = user trust issue
> **Created:** 2026-09-23
> **Master plan:** `docs/AI/tasks/issue_185_loyalty/master_plan.md`
> **Effort:** 1h (client gates) + 3-4h (settings sync)

## Problem

Tắt `Loyalty_Program_Enabled` ở `/settings/shop-features` nhưng khách vẫn thấy điểm thưởng trong toàn bộ luồng order → checkout → payment → delivery.

## Root cause (VERIFIED — file:line)

### Lớp A — Config drift PG vs SQLite (root cause chính)
```
ShopFeatures.razor:542   SettingsService.UpdateSettingsAsync(tenantId, settings)
  → ShopFeatureSettingsService ghi vào IVanAnDbContext = ShopERPDbContext = SQLite (ShopERP)
```
Nhưng toàn bộ luồng KhachLink đọc flag trên **Gateway → PG `ShopFeatureSettings`**:
- `Gateway/LoyaltyController.cs:86-87` — estimate endpoint (`loyaltyEnabled`)
- `Gateway/PublicOrdersController.cs:522-532` — `ResolveLoyaltyEnabledAsync` (banner toggle)
- `CoreHub/OrderWorkflowService.cs:560-566` — award gate (`Skipped award … Loyalty_Program_Enabled=false`) — chạy trên Gateway context cho đơn Gateway-created

**Không có subscriber/sync nào cho ShopFeatureSettings** (grep `ShopFeature` trong `*Sync*.cs` = 0). PG không có row → `GetSettingsAsync` trả default `Loyalty_Program_Enabled=true` (DTO default `IShopFeatureSettingsService.cs:12`). → Toggle OFF "không bao giờ tới" Gateway.

Đã ghi nhận drift trong project_state (Batch 1 RV): "tenant 1 hóa ra Loyalty_Program_Enabled=false, config drift PG vs SQLite ghi nhận".

### Lớp B — Client gaps (độc lập, fix được ngay)
- `Checkout.razor:835-838` — `_showLoyaltySignupModal = true` cho anonymous user **không check `_loyaltyEnabled`** → modal "nâng cấp để nhận điểm" vẫn hiện sau khi đặt hàng dù loyalty OFF.
- `OrderTracking.razor:100` — banner condition `(_orderStatus completed/delivered) && _pointsAwarded > 0` **không có `_loyaltyEnabled`** (đã load ở line 381 nhưng không dùng). Thường an toàn vì backend trả `PointsAwarded=null` khi disabled — nhưng nếu award đã xảy ra trước khi tắt, banner vẫn hiện (edge case — có thể chấp nhận, vì điểm THẬT đã tích).

## Solution

### Phase A — Client gates (Batch 1, không cần duyệt)
- [ ] A1: `Checkout.razor` — `_showLoyaltySignupModal = true` chỉ khi `_loyaltyEnabled` (flag đã load từ estimate ở first render).
- [ ] A2: `OrderTracking.razor:100` — thêm `&& _loyaltyEnabled` vào banner condition (defensive, khớp intent).

### Phase B — Settings sync SQLite→PG (Batch 2 — **APPROVED: B1**)
- **B1 (đã duyệt 2026-09-23) — Outbox event `ShopFeatureSettingsChanged`:** `ShopFeatureSettingsService.UpdateSettingsAsync` (khi chạy trong ShopERP) enqueue outbox → NATS subject `vanan.cloud.featuresettings.changed.{tenantId}` → Gateway subscriber upsert row PG `ShopFeatureSettings`. Đóng drift cho TẤT CẢ flags (không chỉ loyalty — VAT_Display, Kitchen_Workflow, Community thresholds… cũng đang drift).
- ~~B2~~ ~~B3~~ — rejected.

Lưu ý direction: ShopERP là chỗ duy nhất owner edit → chỉ cần 1 chiều SQLite→PG. Tenant chưa từng save → PG default `true` (giữ fail-open hiện tại).

### Phase C — Tests + E2E
- [ ] C1: Unit test subscriber — event → upsert PG (insert mới + update row có sẵn, idempotent replay).
- [ ] C2: E2E spec — tắt loyalty → checkout không estimate banner, không signup modal; bật lại → hiện lại.
- [ ] C3: RV production — tenant test: OFF → `GET /api/loyalty/estimate` trả `loyaltyEnabled:false, points:0` → đơn complete → không award → tracking không banner.

## Acceptance
- [ ] Tắt toggle → toàn bộ luồng mua hàng không còn chữ "điểm thưởng" (order → checkout → payment → delivery)
- [ ] Bật lại → điểm quay lại đúng
