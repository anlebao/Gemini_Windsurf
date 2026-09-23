# Master Plan — Issue #185 "loyalty point error" (2026-09-23)

**Created:** 2026-09-23
**Status:** DECISIONS APPROVED 2026-09-23 → IMPLEMENTING
**Decisions:** (1) Card 03 sync = Outbox `ShopFeatureSettingsChanged` → NATS → Gateway PG upsert ✓ duyệt · (2) Card 02 = KHÔNG đổi Domain — dùng per-tenant `Loyalty_PointsRate` (decimal, đã hỗ trợ 0.01%) ✓ · (3) Card 04 = chỉ SystemAdmin set budget, owner xem **read-only** trong shop-features ✓
**Branch target:** `main`
**Source:** https://github.com/anlebao/Gemini_Windsurf/issues/185 (6 mục, app2.khachvip.online + luồng KhachLink)

## Summary

| # | Mục issue | Verify kết quả | Root cause (verified file:line) | Loại |
|---|---|---|---|---|
| 1 | `/loyalty/dashboard` — "lỗi không kết nối" | ✅ **BUG xác nhận** | `LoyaltyDashboard.razor:8` inject raw `HttpClient` (page duy nhất) → default client không `BaseAddress` → `GetAsync("/api/loyalty/dashboard")` throw `InvalidOperationException` → catch → "Lỗi kết nối". Endpoint backend OK. Cùng pattern hỏng đã gặp: `Impersonate.cshtml.cs` "Replaces the broken HttpClient-based flow" | Bug — FIX |
| 2 | Tỷ lệ tích điểm (%) phải cho nhập 0.01 | ✅ **Limitation xác nhận** | `Domain.cs:2310` — `LoyaltyGlobalConfig.PointsRate` là `int` percent, validate 0–100 (`Domain.cs:2349`); UI `LoyaltyConfigAdmin.razor:62-65` `int.TryParse` + `Step="1"` | Domain change — cần duyệt |
| 3 | Tắt loyalty vẫn hiển thị điểm trong luồng mua hàng | ✅ **BUG xác nhận — 2 lớp** | (a) **Config drift PG/SQLite:** `ShopFeatures.razor:542` save qua `SettingsService` → ShopERP **SQLite**; luồng order chạy Gateway đọc **PG** `ShopFeatureSettings` (estimate `Gateway/LoyaltyController.cs:86-87`, banner `PublicOrdersController.cs:522-532`, award `OrderWorkflowService.cs:564`). **Không có subscriber/sync nào** cho ShopFeatureSettings → OFF không bao giờ tới Gateway. (b) Client gaps: `Checkout.razor:835` `_showLoyaltySignupModal = true` không check `_loyaltyEnabled`; `OrderTracking.razor:100` banner condition thiếu `_loyaltyEnabled` (đã load line 381 nhưng không dùng) | Bug — FIX (a cần duyệt hướng sync) |
| 4 | Không tìm thấy chỗ set ngân sách điểm / tenant | ✅ **Feature TỒN TẠI** — discoverability | Budget caps có sẵn `/admin/loyalty-config` → card "Ghi đè theo tenant" → chọn tenant → "Ngân sách điểm (Budget Caps)" (`LoyaltyConfigAdmin.razor:151-206`, Batch 3 deployed 2026-09-21). Vấn đề: ẩn sau dropdown + page `SystemAdmin`-only → Owner không bao giờ thấy | UX — quyết định hướng expose |
| 5 | Chỉ đơn "giao hàng" mới show địa chỉ + map + bắt buộc SĐT | ⚠️ **Partial — gap thật ở SĐT** | Map/GPS đã `DELIVERY`-only (`Checkout.razor:383`) ✓ · Address luôn hiển thị (chỉ *required* khi DELIVERY, line 359) · **SĐT chỉ required khi `payment==transfer` (`Checkout.razor:618-630`) — DELIVERY+cash đặt được không cần SĐT người nhận** | Bug/UX — FIX |
| 6 | `/community/owner-panel` mặc định show danh sách salesman + shipper | ❌ **Chưa implement — feature mới** | `OwnerPanel.razor` hiện list "khách hàng đủ điều kiện" (Verified + ≥1000 điểm), role chỉ là cột "Vai trò hiện tại". Không có view mặc định cho CTV đang active | Feature mới |

## Fix plan (đề xuất thứ tự)

### Batch 1 — Fix nhanh, độc lập (không đụng Domain/architecture)
1. **Card 01** — `/loyalty/dashboard`: bỏ `@inject HttpClient` → gọi service/DB trực tiếp trong component (Blazor Server không cần HTTP hop). E2E spec mới (Gate 4).
2. **Card 05** — Checkout delivery fields: SĐT bắt buộc khi `DELIVERY` (mọi payment); ẩn address field khi không DELIVERY. E2E spec (Gate 4).
3. **Card 03-client** — Gate UI phía KhachLink: `_showLoyaltySignupModal` chỉ khi `_loyaltyEnabled`; tracking banner condition += `_loyaltyEnabled`.

### Batch 2 — Settings sync (architecture, cần duyệt hướng)
4. **Card 03-sync** — ShopFeatureSettings SQLite→PG: đề xuất Outbox event `ShopFeatureSettingsChanged` (ShopERP publish khi save) → NATS → Gateway subscriber upsert PG. Đóng drift cho TẤT CẢ flag, không chỉ loyalty. **Cần duyệt hướng trước khi implement.**

### Batch 3 — Domain + features (mỗi cái cần duyệt)
5. **Card 02** — `PointsRate` int→decimal (Domain + migration + API + UI). **Hard stop: Domain modification — cần approval.**
6. **Card 04** — Budget caps discoverability: expose cho Owner (đề xuất: link/hint từ `/settings/shop-features` + endpoint owner-scoped, hoặc chỉ menu rename).
7. **Card 06** — OwnerPanel default: tab "Cộng tác viên hiện tại" (Salesman+Shipper active) làm view mặc định.

## Hard stop checks
- **Card 02** đụng `Domain.cs` → IMPLEMENT mode + user approval bắt buộc.
- **Card 03-sync** thêm event + subscriber cross-VPS → cần duyệt hướng (Outbox event vs Gateway write API vs đọc qua HTTP).
- **Card 04** nếu expose budget cho Owner → authz endpoint mới → cần duyệt scope.
- Gate 4: mọi UI layout change (card 01, 03-client, 05, 06) → E2E spec tại `6_Testing/e2e-tests/`.

## Acceptance criteria
- [ ] Build full sln 0 errors · guard-check.ps1 PASS · Core.Tests PASS sau mỗi batch
- [ ] #1: `/loyalty/dashboard` load được 4 stat cards trên app2 (RV)
- [ ] #2: nhập 0.01% lưu được + estimate/award dùng đúng rate
- [ ] #3: tắt loyalty → estimate=0, không banner, không modal signup, không award (RV E2E)
- [ ] #4: Owner/SysAdmin tìm được budget caps (theo hướng duyệt)
- [ ] #5: DELIVERY+cash bắt nhập SĐT; TAKEAWAY/DINEIN không thấy address/map
- [ ] #6: owner-panel mở ra thấy danh sách CTV hiện tại

## Task cards
- `task_card_01_loyalty_dashboard_connection.md` — #1
- `task_card_02_pointsrate_decimal.md` — #2
- `task_card_03_loyalty_off_still_shows.md` — #3
- `task_card_04_budget_caps_discoverability.md` — #4
- `task_card_05_delivery_only_fields.md` — #5
- `task_card_06_owner_panel_default_roles.md` — #6
