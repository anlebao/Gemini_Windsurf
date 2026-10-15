# MASTER PLAN — TRUNG TÂM THÔNG BÁO (OWNER + KHÁCH) + THỐNG KÊ CHAT ĐÃ ĐỌC/CHƯA ĐỌC

> **Date:** 2026-10-15 · **Status:** PLAN — chờ user duyệt (newfeaturebuild Step 4)
> **Objective:** (1) Tab/box "Thông báo" cho owner tenant: badge số chưa đọc + group theo loại/nhóm (trạng thái đơn hàng · điểm thưởng · khuyến mãi · quà tặng · hệ thống · bảo mật...). (2) Thống kê chat khách hàng (đã đọc/chưa đọc) để owner biết + xử lý. (3) Inbox thông báo in-app cho KHÁCH trên KhachLink (ngoài Web Push).
> **Mode:** ANALYZE (đã xong) → chờ duyệt → IMPLEMENT.

## 0. QUYẾT ĐỊNH USER (đã chốt 2026-10-15)
- **Q1 Chat stats:** hiển thị trên **hộp thư ShopERP** `/community/messages` — badge đỏ chưa đọc per conversation + tổng "X chưa đọc".
- **Q2 Nguồn thông báo MVP:** **bao gồm cả loyalty/promo/gift** — owner thấy mọi thông báo khách nhận (đơn mới/hủy · đặt lịch · cảnh báo tồn kho · claim · chat mới · khách đổi điểm/đạt ngưỡng · campaign chạy · quà sinh nhật/voucher hết hạn).
- **Q3 Inbox khách hàng:** **CÓ — làm luôn** inbox in-app trên KhachLink (lịch sử từ `PushNotificationDelivery` — mở rộng entity).


## 1. HIỆN TRẠNG (đã verify code)

### Thông báo — CHƯA có inbox phía owner
| Thành phần | Hiện trạng |
|---|---|
| `PushNotificationService` (CoreHub) | Web Push (VAPID) cho **khách hàng**: order status · loyalty points · campaign bulk |
| `PushNotificationBackgroundService` | NATS subscriber (`order.status.changed`, `loyalty.points.changed`) → push |
| `PushNotificationDelivery` (PG) | Vết delivery per-customer (Delivered/Clicked/Expired) — **click tracking chỉ, không phải inbox** |
| `NotificationsController` (ShopERP) | subscribe/unsubscribe/status/track-click (customer token) |
| `AlertCenter.razor` (ShopERP) | Cảnh báo tồn kho VA-IIE (ShiftAlert — STOCK_DROP/CASH_MISMATCH...) — modal riêng, **không nằm chung trung tâm thông báo** |
| EInvoice alerts | Module riêng (khác hub) |
| Menu/bell | **KHÔNG có** item "Thông báo" / bell / badge ở ShopERP |

### Chat (Realtime Platform) — có hạ tầng nhưng THIẾU read/unread stats
| Thành phần | Hiện trạng |
|---|---|
| `Conversation` + `ConversationParticipant` + `Message` (Domain) | Đã có `Message.IsRead` + `MarkAsRead()` |
| `RealtimeMessagingService` (CoreHub) | `MarkAsReadAsync` tồn tại **nhưng KHÔNG được gọi ở production** (không hub/controller nào wire) → IsRead luôn false |
| `RealtimeController` (Gateway) `GET /api/realtime/shop/conversations` | Trả list conversation (CustomerName/LastMessage/LastMessageAt/LastSenderId) — **KHÔNG có UnreadCount** |
| `ShopInbox.razor` (`/community/messages`) | List conversation **không badge chưa đọc** + RealtimeChatPanel |
| KhachLink `Store.razor` (`/store/{slug}` — tenant profile page) | ChatPanel customer ↔ shop (subject `Shop/{tenantId}`) |
| `RealtimeChatPanel` (UI.Platform) | Không gọi mark-read khi mở hội thoại |

### Nguồn sự kiện tiềm năng cho từng nhóm
- **Trạng thái đơn hàng:** `OrderWorkflowService`/`OrderService` (đơn mới, hủy, thanh toán, đặt lịch mới — booking queue)
- **Điểm thưởng:** `LoyaltyPointLedgerService`/`RedemptionService` (khách đổi điểm, đạt ngưỡng)
- **Khuyến mãi:** `PromoCampaignJob` (chạy campaign)
- **Quà tặng:** `BirthdayBonusJob` · `VoucherExpiryReminderJob`
- **Hệ thống:** `AlertCenter`/`ShiftAlertService` (tồn kho) · e-invoice lỗi · đóng kỳ kế toán · import Excel xong
- **Bảo mật:** `TenantClaimService` (hàng đợi Claim) · login thiết bị mới · thay đổi quyền
- **Chat:** tin nhắn mới từ khách (cũng là nguồn "chat stats")

## 2. GIẢI PHÁP ĐỀ XUẤT

### A. Entity & Service chung (1 pipeline, 2 đầu ra)
**`TenantNotification` (mới — PG, owner):** `TenantId` · `Category` (enum append-only: `OrderStatus, Loyalty, Promotion, Gift, System, Security, Chat, Booking`) · `Title` · `Body` · `ActionUrl` · `ReferenceType`/`ReferenceId` · `Severity` (Info/Warning/Critical) · `IsRead`+`ReadAt` · `CreatedAt`. Append-only (chỉ đổi IsRead/ReadAt).

**`PushNotificationDelivery` (mở rộng — PG, khách):** += `Title` · `Body` · `Category` (cùng enum) · `IsRead`+`ReadAt` · `MarkAsRead()` — vừa là vết web push vừa là bản ghi inbox in-app của khách (migration PG).

**`INotificationService` (CoreHub — fail-safe, idempotent theo ReferenceType+Id):** `CreateForOwnerAsync(tenantId, category, title, body, actionUrl, ref...)` → TenantNotification · `CreateForCustomersAsync(customerIds, category, title, body, actionUrl, ref...)` → PushNotificationDelivery (+ kích hoạt web push qua PushNotificationService — thay payload cũ) · `ListForOwnerAsync` (paged/filter category/read) · `UnreadCountForOwnerAsync` · `ListForCustomerAsync` (token-scoped) · `UnreadCountForCustomerAsync` · `MarkReadAsync` (owner/customer) · `MarkAllReadAsync`.

**Nguồn ghi (hook fail-safe — không bao giờ làm hỏng luồng nghiệp vụ):**
| Sự kiện | Owner (TenantNotification) | Khách (PushNotificationDelivery) |
|---|---|---|
| Đơn mới/hủy/hoàn tất · thanh toán (`OrderWorkflowService`/`OrderService`) | ✅ OrderStatus | ✅ (web push hiện có) |
| Booking mới (queue) | ✅ Booking | — |
| Khách đổi điểm/đạt ngưỡng (`RedemptionService`/`LoyaltyPointLedgerService`) | ✅ Loyalty | ✅ |
| Campaign chạy (`PromoCampaignJob`) | ✅ Promotion | ✅ (bulk hiện có) |
| Quà sinh nhật/voucher hết hạn (`BirthdayBonusJob`/`VoucherExpiryReminderJob`) | ✅ Gift | ✅ |
| Cảnh báo tồn kho (`ShiftAlertService`/AlertNotifierDispatcher) | ✅ System | — |
| Claim mới (`TenantClaimService`) | ✅ Security | — |
| Tin chat mới từ khách (shop inbox) | ✅ Chat | — |

### B. Gateway API + Realtime
- **Owner (tenant JWT):** `GET /api/v1/tenant/notifications?category=&page=&unreadOnly=` · `GET .../unread-count` · `POST .../{id}/read` · `POST .../read-all`.
- **Khách (customer token):** `GET /api/v1/customer/notifications` · `GET .../unread-count` · `POST .../{id}/read` · `POST .../read-all`.
- **`NotificationsHub` (SignalR — Gateway):** push `NotificationCreated` + `UnreadCountChanged` tới group `Shop/{tenantId}` (owner) và `Customer/{customerId}` (khách — KhachLink WASM đã có SignalR client từ chat).
- **Chat:** hub `MessagingHub` += `MarkConversationMessagesRead(conversationId, readerId)` (shop/staff) — `RealtimeChatPanel` (staff) gọi khi mở hội thoại; `GET /api/realtime/shop/conversations` += `UnreadCount` per conversation + `TotalUnread`.

### C. UI
- **ShopERP:** bell + badge (UserHeader — mọi layout, realtime qua hub + poll fallback) · trang `/notifications` (tabs group category + đọc/chưa đọc + "Đánh dấu đã đọc"/"Đọc tất cả" + deep-link) + NavMenu "Thông báo" · `ShopInbox.razor` badge đỏ chưa đọc per conversation + header "X chưa đọc" (Q1).
- **KhachLink (WASM):** bell + badge (Layout/Header) · trang `Notifications.razor` (group category, mark read/all, deep-link) — đọc API customer + realtime qua hub.

## 3. PHASES (IMPLEMENT sau khi duyệt)
- **P1 Domain (Gate 5):** `TenantNotification` + `NotificationCategory` (append-only) + `TenantNotificationSeverity` + mở rộng `PushNotificationDelivery` (Title/Body/Category/IsRead/ReadAt + `MarkAsRead`).
- **P2 CoreHub:** `INotificationService` (owner + customer) + DI + hooks fail-safe (order/booking/loyalty/promo/gift/alert/claim/chat) + migration PG.
- **P3 Gateway:** API owner + customer + `NotificationsHub` + chat mark-read (hub) + `UnreadCount` trong shop conversations.
- **P4 ShopERP:** bell+badge (UserHeader) · trang `/notifications` + NavMenu · ShopInbox badge chưa đọc (Q1) · client API.
- **P4b KhachLink:** bell+badge (header) · trang Notifications (WASM) + client API + realtime hub.
- **P5 Tests:** Domain guards · service (create/list/unread/mark/idempotent) · controller auth (owner/customer) · hub · bUnit bell/page/inbox badge · KhachLink page tests.
- **P6 Validation + Deploy + RV:** guard-check + build + Core.Tests + ShopERP.Tests → FAST PUSH → CD → RV L1-L4 (markers · migration applied · health · badge realtime owner + khách · chat read/unread production).

## 4. RÀNG BUỘC / RISK
- Domain PURE (entity record-style, không EF trong Domain) · `AccountingEntry` không đụng.
- Notifications append-only — không delete (chỉ IsRead); retention không giới hạn MVP.
- Hook ghi notification fail-safe (try/catch + không blocking luồng nghiệp vụ — pattern `AlertNotifierDispatcher`).
- Multi-tenancy: mọi query filter TenantId; API tenant JWT / customer token-scoped.
- Chat IsRead là cờ chung 2 phía (customer↔shop) — đủ cho MVP; per-participant watermark nếu cần multi-member (ngoài scope).
- Không migration SQLite (notification PG-only; ShopERP đọc qua API — pattern ShopInboxApiClient).
- Migration PG: `AddTenantNotifications` + `AddPushNotificationDeliveryInboxColumns` (backfill Title/Body cho delivery cũ = null — UI hiển thị fallback).

## 5. CÂU HỎI ĐÃ CHỐT (2026-10-15)
- Q1 Chat stats → hộp thư ShopERP (badge per conversation + tổng).
- Q2 Nguồn thông báo → gồm cả loyalty/promo/gift (owner thấy mọi thông báo khách nhận).
- Q3 Inbox khách → CÓ, làm luôn (KhachLink in-app, lịch sử từ PushNotificationDelivery mở rộng).
