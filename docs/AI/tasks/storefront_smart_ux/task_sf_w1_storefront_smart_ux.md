# Task SF-W1 — Storefront Smart UX Đợt 1: AI quick-chips + Sticky nav + Skeleton + Unread badge

> **Status:** 🟡 DRAFT 2026-11-07 — chờ user review
> **Gate:** storefront UX polish — master plan `docs/AI/plans/storefront-smart-modern-master-plan.md` · **main sln changes — additive, zero backend**
> **Phụ thuộc:** P8 unified widget (DONE `06a8b993`) · `IRealtimeChatClient` đã DI trong KhachLink (RealtimeChatPanel dùng)
> **Scope:** `UI.Platform` (AiChatWidget params) + `5_WebApps/KhachLink` (Store.razor + StoreNav) — KHÔNG đụng Gateway/vanan-ai/ShopERP

## Objective

4 nâng cấp nhỏ gọn cho storefront: (A1) khách bấm chip theo section → AI trả lời ngay, không cần tự gõ; (B1) navbar sticky khi cuộn qua hero; (B2) skeleton shimmer + ảnh lazy/aspect chống CLS; (B6) badge "tin mới" trên bubble khi chủ quán nhắn.

## Hiện trạng (verified)

| Thành phần | File | Ghi chú |
|---|---|---|
| Widget | `UI.Platform/Components/Ai/AiChatWidget.razor` | P8: `_mode` Ai/Human · `HumanContent`/`OnDraftRequest` · `SendAsync(text)` internal |
| Panel human | `UI.Platform/Components/Realtime/RealtimeChatPanel.razor` | `GetHistoryAsync(subjectType, id, token, deviceId)` — trả `RealtimeChatHistoryResult.Messages` có `SenderId`/`SentAt` |
| Page | `5_WebApps/KhachLink/Pages/Store.razor` | inline `<style>` block (convention hiện tại) · sections có `id="booking"/"campaigns"/"products"/"location"/"shop-chat"` sẵn · `_customerDeviceId`/`_customerId`/`ChatIdentity` sẵn |
| Realtime client | `UI.Platform/Core/Interfaces/IRealtimeChatClient.cs` | đã DI-registered host-side (panel dùng) — inject thẳng vào Store.razor được |

## Deliverables

### 1. `AiChatWidget` — +3 params (additive, backward-compat)

| Param | Spec |
|---|---|
| `string? PendingQuestion` | Host set → widget **mở panel + auto-send** câu hỏi như user message (AI mode). Consume 1 lần (`_pendingQuestionSent`): fire trong `OnAfterRenderAsync` SAU KHI `_sessionId` đã có (guard `aiEnabled`). Human-mode-only mount (aiEnabled=false) → bỏ qua. |
| `int HumanUnreadCount` | >0 → badge đỏ trên `vanan-ai-bubble-btn` (`.vanan-ai-unread` CSS — absolute badge góc). 0/null → không badge. |
| `EventCallback OnHumanOpened` | Fire 1 lần khi `_mode` chuyển sang Human lần đầu mỗi lần mở (host dùng để mark-seen → reset count). Gọi trong `SwitchToHuman`/`EscalateAsync` + init human-mode khi `_open`. |

### 2. `Store.razor` — AI quick-chips (A1)

- State `_pendingQuestion` + method `AskAiAsync(string q)` → `_pendingQuestion = q` (widget consume → host reset null sau send — widget fire `OnPendingQuestionConsumed`? **Quyết định đơn giản hơn:** widget consume rồi raise `OnDraftRequest`? Không — thêm `EventCallback PendingQuestionChanged` (two-way `[Parameter]`+callback pattern Blazor chuẩn) → host `@bind-PendingQuestion` → widget tự reset null sau khi send. Gọn hơn.)
- Chips = `<button class="store-ai-chip">` inline, chỉ render khi `_features.AIChat_Enabled`:
  - Section products: "💡 Quán có món gì bán chạy?" · "🍽 Gợi ý combo cho 2 người?"
  - Section booking: "📅 Còn lịch trống chiều nay không?"
  - (Chips là `<button>` thuần — không phải component mới; style `.store-ai-chip` trong `<style>` sẵn của Store.razor.)
- Mount `AiChatWidget` thêm `@bind-PendingQuestion="_pendingQuestion"` + `HumanUnreadCount="_aiUnreadCount"` + `OnHumanOpened="OnHumanChatOpened"`.

### 3. `StoreNav` — sticky glass navbar (B1) — `5_WebApps/KhachLink/Components/StoreNav.razor`

- `position: sticky; top: 0` render NGAY SAU `</section>` hero → tự stick khi cuộn qua, **không cần JS**.
- Nội dung: logo nhỏ (`_store.LogoUrl` hoặc icon shop) + tên quán + anchor chips (`#booking`/`#campaigns`/`#products`/`#location`/`#shop-chat` — chỉ render chip cho section đang bật theo `_features.*`) + CTA `tel:` nếu có `_store.Phone`.
- Glass: `backdrop-filter: blur(12px)` + nền `rgba(255,255,255,0.82)` + border-bottom; z-index dưới widget (1040 < 1050).
- Scoped CSS `StoreNav.razor.css`; mobile: chips scroll ngang (`overflow-x:auto`, hide scrollbar).

### 4. Skeleton shimmer + image hygiene (B2) — Store.razor `<style>` block

- `.skeleton` base class: `linear-gradient` shimmer `background-position` animation; tôn trọng `@media (prefers-reduced-motion: reduce)` → tắt animation.
- Campaign section: `_campaignLoading` → render 3 `.skeleton-card` (grid giữ layout) thay spinner.
- Campaign `<img>`: thêm `loading="lazy"` + `aspect-ratio: 16/9` (chống CLS khi media load chậm).
- Products/`VibeShowcase`: KHÔNG đụng component — chỉ bọc skeleton nếu `_products.Count==0 && !loaded` ở page level (giữ scope nhỏ).

### 5. Unread badge (B6) — Store.razor (zero backend)

- Inject `IRealtimeChatClient` (đã có DI).
- `_aiUnreadCount`: computed bằng poll nhẹ:
  - `OnInitializedAsync` sau khi có `_store` + `ChatIdentity` → `RefreshUnreadAsync()` + `Timer` 45s (dispose trong `Dispose()`).
  - Poll: `GetHistoryAsync("Shop", _store.Id, _customerToken, _customerDeviceId, take: 20)` → đếm `m.SenderId != ChatIdentity.Value && m.SentAt > lastSeen` (lastSeen = localStorage `ai_human_last_seen_{_store.Id}`; thiếu → count = số tin không-phải-của-mình trong 20 tin gần nhất, cap hiển thị "9+").
  - Dừng poll khi `_mode` human mở (flag `_humanOpen` — host không biết mode; **đơn giản: không dừng — count reset qua OnHumanOpened**).
- `OnHumanChatOpened()` → ghi `lastSeen=DateTime.UtcNow` vào localStorage + `_aiUnreadCount=0`.
- Giới hạn ghi vào card: localStorage → không sync cross-device; guest-vs-login cùng logic vì `SenderId` so với `ChatIdentity`.

## Ranh giới (HARD)

- Không backend/API mới — không migration, không endpoint.
- Không đụng vanan-ai, Gateway, MessagingHub, ShopInbox.
- Chips KHÔNG auto-fire khi page load — chỉ khi khách bấm (anti-spam metering).
- Skeleton/navbar: CSS thuần, không JS layout library.
- UI Platform components cho phần tử mới (chips/nav dùng markup+scoped css theo convention page hiện tại — Store.razor đã là raw `<style>`/`<button>`).

## Tests bắt buộc (Core.Tests bUnit — pattern `AiChatWidgetTests`/`RealtimeChatPanelDraftTests` hiện có)

- [ ] `PendingQuestion` set → panel mở + `SendChatAsync` gọi đúng text · chỉ 1 lần (set lại không resend)
- [ ] `PendingQuestion` khi `aiEnabled=false` → không send (human-only mode giữ nguyên)
- [ ] `@bind-PendingQuestion` → widget reset null sau send (callback invoked)
- [ ] `HumanUnreadCount=3` → badge render "3" · =0 → không badge
- [ ] Switch human → `OnHumanOpened` fired · `HumanUnreadCount` host reset (composite check optional)
- [ ] Regression: mount không params mới → behavior P8 nguyên vẹn

## Acceptance

- [ ] Tests xanh · `dotnet build VanAn.sln` 0 err · Core.Tests + ShopERP.Tests không regression · guard-check PASSED
- [ ] E2E spec update `ai-chat-widget.spec.ts` +1 case: quick-chip visible + click → user message xuất hiện (Gate 4)
- [ ] RV: storefront An Tâm — scroll qua hero → nav stick · chips bấm → AI answer · skeleton trong lúc load · badge khi có tin chủ quán mới

## Không được làm

- Không auto-open widget / auto-send khi page load
- Không đụng VibeShowcase internals, không đổi theme system (W3)
- Không thêm tracking/analytics mới
- Không làm cross-device unread sync (ghi rõ giới hạn localStorage)

## Delivered

_(điền sau implement)_
