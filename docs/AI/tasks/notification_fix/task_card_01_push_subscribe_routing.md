# Task Card NF-1: Push subscribe endpoint unreachable → toàn bộ Web Push chết

> **Status:** ⚠️ CHẨN ĐOÁN SAI + ĐÃ GỠ BỎ FIX 2026-09-24 — Option A (YARP route) được thêm rồi **gỡ bỏ** vì redundant. Review lại base code: `2_Gateway/Controllers/NotificationsController.cs` (W17-T4, forward controller 4 endpoint) **đã tồn tại**, `MapControllers()` chạy trước `MapReverseProxy()` (`Program.cs:1704/1726`) → request không thể rơi vào fallback khachlink. Cơ chế chính thức hiện tại = **Gateway forward controller** (không cần YARP route). Nguyên nhân thật của toggle fail nghi vấn: binary Gateway prod stale (deploy trước W17-T4) + E16 + E18 — xem `master_plan.md` RC-1. Build + guard PASS sau khi gỡ route. **Còn:** RV production (subscribe → 200 + row `PushSubscriptions`) + verify `VAPID_PRIVATE_KEY` trên VPS.
> **Priority:** P0 — prerequisite cho mọi push notification (buyer + role fan-out TC-04)
> **Created:** 2026-09-24
> **Master plan:** `docs/AI/tasks/notification_fix/master_plan.md`
> **Effort:** 1-2h (Option A) / 3-4h (Option B) + RV

## Problem

Buyer bật "Thông báo đẩy" trong KhachLink Profile → luôn lỗi. `PushSubscriptions` table trong ShopERP SQLite luôn rỗng → `PushNotificationService.SendOrderStatusNotificationAsync` luôn log `"No active push subscriptions found for customer …"` → 0 push đến mọi thiết bị.

## Root cause (VERIFIED LẠI 2026-09-24 — chẩn đoán ban đầu SAI)

**Chẩn đoán ban đầu (SAI — đã verify lại):**

```
Profile.razor:499          POST /api/notifications/push/subscribe  (client "gateway" → api2.khachvip.online)
appsettings.json:88-171    YARP Routes — KHÔNG có route cho /api/notifications/*  (ĐÚNG — nhưng không quan trọng)
2_Gateway/                 KHÔNG có NotificationsController (grep 0 match PushSubscription)  (SAI)
```

**Sự thật trên base code (commit `2790e3d2`):**
- `2_Gateway/Controllers/NotificationsController.cs` **TỒN TẠI từ W17-T4** (`eea7c106`) — forward controller đủ 4 endpoint (`push/subscribe` POST+DELETE, `push/status` GET, `push/track` POST) qua `IHttpClientFactory.CreateClient("shoperp")`, `[AllowAnonymous]`. Grep chuỗi `PushSubscription` ra 0 match chỉ vì code dùng tên `Subscribe`/`GetPushStatus`/`TrackClick` — không có identifier `PushSubscription` nào trong file.
- `2_Gateway/Program.cs:1704` `MapControllers()` chạy **TRƯỚC** `MapReverseProxy()` (:1726), comment W4-Fix ghi rõ "controllers take priority over the YARP fallback catch-all route". Route MVC literal `api/notifications/push/subscribe` thắng precedence trước `{**catch-all}` → **không bao giờ có fallback khachlink 405**.
- Vì vậy: YARP `notifications-route` thêm trong Option A là **redundant** (không bao giờ được chọn) → **đã gỡ bỏ 2026-09-24**. Cơ chế duy nhất = Gateway forward controller.

**Nguyên nhân thật (nghi vấn, chưa verify được từ repo):** binary Gateway prod **stale** (deploy trước W17-T4 — lúc đó mới thực sự fallback khachlink) + E16 (subscription JSON shape) + E18 (VAPID key mismatch). Chẩn đoán ban đầu "verified file:line" là sai; kết quả RV 401 JSON sau fix trùng khớp hành vi forward controller vốn đã có sẵn.

## Solution

### KẾT LUẬN SAU VERIFY LẠI (2026-09-24): GIỮ OPTION B (forward controller) — KHÔNG cần YARP route

`2_Gateway/Controllers/NotificationsController.cs` (W17-T4) **đã tồn tại và hoạt động** — forward 4 endpoint qua `CreateClient("shoperp")` (đăng ký `Program.cs:799`), pattern giống `CampaignsController.SendCampaignPush` (forward body + auth header → `StatusCode((int)response.StatusCode, content)`). Route MVC literal thắng precedence YARP fallback nên request luôn tới đúng controller.

- Option A (YARP `notifications-route` → shoperp-cluster) đã thêm 2026-09-24 rồi **GỠ BỎ** — redundant, không bao giờ được chọn (MVC thắng precedence). Không gây hại nhưng gây nhầm lẫn bảo trì (2 cơ chế cho 1 endpoint).
- Forward `X-Customer-Token` header tự động (controller copy header vào request forward).
- `ResolveCustomerTenant` filter trên ShopERP controller resolve tenant từ token — hoạt động y như `/api/auth/*` hiện tại.
- ✅ Verified: `HmacSigning:ProtectedPaths` rỗng `[]` — middleware passive, không chặn.
- ⚠️ Rate limit nginx `zone=api burst=200` — OK cho push subscribe (low volume).
- ⚠️ Nếu prod vẫn 405/HTML khi deploy code hiện tại → Gateway binary stale (thiếu W17-T4) → redeploy, không phải thêm route.

### Phase C — VAPID key verify (RC-3, song song)

- [ ] SSH `vanan-shop-a`: `grep VAPID_PRIVATE_KEY .env.shoperp` — phải có giá trị thật (không rỗng, không placeholder). Nếu thiếu → generate cặp key mới theo `docs/AI/VAPID_KEY_GENERATION_GUIDE.md`, set `.env.shoperp` + GitHub secret `VAPID_PRIVATE_KEY` (CD preserve), `docker compose up -d` recreate.
- [ ] Verify `docker logs shoperp | grep "PushNotificationService initialized"` — log có `VAPID subject` thay vì throw.
- [ ] Dev placeholder `appsettings.Development.json:43` — ghi chú trong card, không đổi (dev-only).

## Verification (L1 trước RV)

- [ ] `curl -X POST https://api2.khachvip.online/api/notifications/push/subscribe` không token → **401 JSON** (không phải 405/HTML) = route đã tới ShopERP.
- [ ] `curl https://api2.khachvip.online/api/notifications/push/status` không token → **401** (trước: 405).

## Acceptance

- [ ] Customer thật bật toggle trên Profile → POST 200 + row mới trong `PushSubscriptions` (shop VPS SQLite)
- [ ] `GET push/status` trả `{enabled:true, count:≥1}`
- [ ] Toggle OFF → DELETE 200 + subscription soft-deleted
- [ ] Build `VanAn.sln` 0 errors · guard-check PASS (config-only change → build sanity đủ)
- [ ] RV: đổi status 1 đơn thật → log ShopERP `Push notifications sent: 1/1` (thay vì "No active push subscriptions")
