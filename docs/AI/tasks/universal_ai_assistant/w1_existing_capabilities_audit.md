# W1 EXISTING-CAPABILITIES AUDIT — chống build trùng (2026-11-22)

> Mục đích: đối soát base code TRƯỚC khi implement W1, ghi lại phần đã có sẵn
> (tái dùng) vs phần build mới vs phần duplicate/rủi ro. Bài học từ 3 bug
> production của W1: DI thiếu, config thiếu, endpoint thừa.

## 1. ĐÃ CÓ SẴN — W1 tái dùng, KHÔNG rebuild

| Capability | Hiện có ở | W1 dùng lại bằng cách |
|---|---|---|
| Anonymous public AI chat API | `PublicTenantAiController` `/api/public/tenant-ai/*` | `/a/{slug}` page resolve slug→tenantId rồi gọi thẳng endpoints này qua `TenantAiPublicHttpService` |
| Chat UI widget | `UI.Platform/Components/Ai/AiChatWidget.razor` + `IAiChatClient` | Mount nguyên vẹn trên `Assistant.razor` |
| Deep-link mở chat | `Store.razor` `?chat=ai` → `OpenPanel` consume-once (W3-1 cũ) | **QR/link public giờ encode `/store/{slug}?chat=ai`** — không cần route mới |
| Human handoff | `RealtimeChatPanel` trong widget + guest device id | Wire sẵn trên `/a/` page |
| Product cards trong chat | TenantAI response → widget render | Tự có, deep-link "Thêm giỏ" về `/store/by-id/{tid}` |
| Kill-switch AI chat | `ShopFeatureSettingsEntity.AIChat_Enabled` | `PublicAssistantLinkController.IsAiChatEnabledAsync` check cùng flag |
| TenantAI persona/knowledge/publish/usage | `vanan-ai` + admin console `/admin/tenant-ai/*` | Link entity KHÔNG chứa config — trỏ về TenantAI |
| Metering AI | `ai.chat.message` Deny + `ai.chat.degraded` MeterOnly trong `MeteringServiceCodes` | Không đụng — gate sẵn trong forwarder |
| Rate limit + session cap | policy `tenant-ai-public` + cap 20 msg/h | `PublicAssistantLinkController` gắn `[EnableRateLimiting("tenant-ai-public")]` + copy pattern `TryAcquireSessionSlot` |
| QR PNG generation | `IQrCodeService`/`QrCodeService` (CoreHub) — đã dùng cho product QR, Guard ticket, Booking QR | Thêm overload generic `GenerateQRCode(string)`; **phải register DI trong Gateway** (bug #1) |
| Storefront URL derivation | `Tenant.Settings.Slug` + `KhachLinkInstances.CustomDomain` (`TenantSearchService`) | `AssistantLinkController.StorefrontSlugAsync` đọc `Settings.Slug` → `/store/{slug}?chat=ai` |
| QR-channel management UI precedent | `BookingQrChannels.razor` `/booking/qr-channels` (create/list/revoke/QR per channel + attribution) | `AiAssistantSettings.razor` theo cùng pattern |

## 2. BUILD MỚI THẬT SỰ trong W1

| Thành phần | Lý do không thể tái dùng |
|---|---|
| `AssistantLink` entity + migration | Chưa có entity "link phân phối AI" nào — BookingQrChannel là booking-specific |
| `AssistantLinkController` `/api/v1/assistant-link` | Owner CRUD + revoke/regenerate + `qr.png` — endpoint mới, auth `RequireTenantAccess` |
| `PublicAssistantLinkController` `/api/public/assistant-link/{slug}/resolve` | Slug→tenant resolution server-side — phần DUY NHẤT page `/a/` cần |
| `Assistant.razor` `/a/{slug}` | Hosted fallback cho tenant không storefront (hiện là fallback-only) |
| `AiAssistantSettings.razor` + `AssistantLinkApiClient` + DI | UI quản lý link/QR phía owner |
| Nav menu + sitemap entries | Đăng ký route mới |

## 3. DUPLICATE / DEAD CODE phát hiện sau deploy

| # | Vấn đề | Chi tiết | Đề xuất |
|---|---|---|---|
| D1 | **3/4 endpoint public của `PublicAssistantLinkController` là dead code** | `chat`, `widget-config`, `sessions/{sid}/messages` không được gọi — `Assistant.razor` chỉ gọi `/resolve`, sau đó chat đi qua `/api/public/tenant-ai/*` (comment trong code cũng ghi điều này). ~150 dòng forward logic trùng `PublicTenantAiController` | Xóa 3 endpoint, giữ `/resolve` — hoặc giữ lại nếu dự kiến `/a/` page gọi trực tiếp (hiện không) |
| D2 | `/a/{slug}` hosted page + toàn bộ public controller gần như thừa | User directive: tenant luôn có storefront → QR/link = `/store/{slug}?chat=ai`. `/a/` chỉ còn fallback khi thiếu `Settings.Slug` | Quyết định: giữ làm fallback (chi phí thấp) hay remove page + controller + entity. Nếu remove → migration drop `AssistantLinks` |
| D3 | `StorefrontSlugAsync` query tenant trực tiếp | Logic "tenant → storefront slug" thuộc `TenantSearchService` (đã có `GetBySlugAsync`, `GetStoreInfoAsync`) | Chấp nhận được (projection khác, read-only); nếu thêm surface mới cần slug → extract vào `ITenantSearchService` |
| D4 | Bài học quy trình | 3 bug prod: (a) service tồn tại nhưng quên register DI ở host mới; (b) config `AssistantLink:PublicBaseUrl` chưa set → URL tương đối; (c) build pass ≠ runtime — manual test bắt được | Checklist pre-deploy W2+: verify DI registration ở MỌI host inject service + verify config keys trong compose/env trước khi báo "done" |

## 4. Checklist cho W2+ (widget external embed)

Trước khi code, check lại các mục sau để không trùng:
- `AiChatWidget` là Blazor component trong KhachLink — external JS SDK là build MỚI thật (W2), không phải "tách nhỏ"
- Domain verification cho widget embed chưa có — cần entity/config mới (pattern tham khảo `KhachLinkInstances`)
- `assistantRef` resolution: hiện có 3 pattern song song — storefront-host, `/store/{slug}`, `/a/{slug}`. W2 phải unify theo D2 decision (assistantRef), không thêm pattern thứ 4
- QR service: dùng `IQrCodeService.GenerateQRCode` — KHÔNG tạo service QR mới; nhớ register DI ở host dùng nó
