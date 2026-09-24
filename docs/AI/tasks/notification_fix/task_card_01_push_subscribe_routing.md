# Task Card NF-1: Push subscribe endpoint unreachable → toàn bộ Web Push chết

> **Status:** ✅ IMPLEMENTED 2026-09-24 — Option A (YARP route). `notifications-route` (`/api/notifications/{**catch-all}` → `shoperp-cluster`, Order=100) đã thêm vào `2_Gateway/appsettings.json`. HMAC verify không chặn (`ProtectedPaths: []`). Build + guard PASS. **Còn:** RV production (subscribe → 200 + row `PushSubscriptions`) + verify `VAPID_PRIVATE_KEY` trên VPS.
> **Priority:** P0 — prerequisite cho mọi push notification (buyer + role fan-out TC-04)
> **Created:** 2026-09-24
> **Master plan:** `docs/AI/tasks/notification_fix/master_plan.md`
> **Effort:** 1-2h (Option A) / 3-4h (Option B) + RV

## Problem

Buyer bật "Thông báo đẩy" trong KhachLink Profile → luôn lỗi. `PushSubscriptions` table trong ShopERP SQLite luôn rỗng → `PushNotificationService.SendOrderStatusNotificationAsync` luôn log `"No active push subscriptions found for customer …"` → 0 push đến mọi thiết bị.

## Root cause (VERIFIED — file:line)

```
Profile.razor:499          POST /api/notifications/push/subscribe  (client "gateway" → api2.khachvip.online)
appsettings.json:88-171    YARP Routes — KHÔNG có route cho /api/notifications/*
                           chỉ có: /api/auth/* → shoperp, /api/platform/* → shoperp,
                           fallback {**catch-all} → khachlink-cluster
2_Gateway/                 KHÔNG có NotificationsController (grep 0 match PushSubscription)
```

Chain lỗi: `POST /api/notifications/push/subscribe` → Gateway `MapControllers()` không match → `MapReverseProxy()` → `fallback-route` → `khachlink-cluster` (`http://${KHACHLINK_REMOTE_HOST}:80/`) → KhachLink nginx `location /` → `try_files … /index.html` → **405 Not Allowed / HTML** (không phải JSON).

`NotificationsController` (ShopERP, `5_WebApps/ShopERP/Controllers/NotificationsController.cs`) chứa 4 endpoint đúng (`push/subscribe` POST+DELETE, `push/status` GET, `push/track` POST) nhưng **không bao giờ được gọi** — dead endpoint.

Hệ quả phụ: `GET /api/notifications/push/status` cũng 405 → `_pushEnabled` luôn false → toggle không bao giờ "dính".

## Solution

### Option A (đề xuất) — thêm YARP route → shoperp-cluster

`2_Gateway/appsettings.json` → `ReverseProxy.Routes` thêm (pattern giống `social-auth-route`):

```json
"notifications-route": {
  "ClusterId": "shoperp-cluster",
  "Match": { "Path": "/api/notifications/{**catch-all}" },
  "Order": 100,
  "Transforms": [ { "PathPattern": "/api/notifications/{**catch-all}" } ]
}
```

- Forward `X-Customer-Token` header tự động (YARP proxy headers mặc định giữ).
- `ResolveCustomerTenant` filter trên controller resolve tenant từ token — hoạt động y như `/api/auth/*` hiện tại.
- ✅ Verified 2026-09-24: `HmacSigning:ProtectedPaths` rỗng `[]` cả appsettings.json lẫn Production — middleware passive, không chặn.
- ⚠️ Rate limit nginx `zone=api burst=200` — OK cho push subscribe (low volume).

### Option B — Gateway proxy controller (nếu cần logic ở Gateway)

Tạo `2_Gateway/Controllers/NotificationsController.cs` forward 4 endpoint qua `CreateClient("shoperp")` (đã đăng ký `Program.cs:799`), pattern giống `CampaignsController.SendCampaignPush` (`CampaignsController.cs:271-314` — forward body + auth header → `StatusCode((int)response.StatusCode, content)`).

Nhược điểm: duplicate controller, thêm hop. Chỉ chọn nếu Option A bị chặn bởi middleware.

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
