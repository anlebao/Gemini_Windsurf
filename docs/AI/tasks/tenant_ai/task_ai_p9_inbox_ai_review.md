# Task AI-P9 — ShopInbox: Tab "AI trả lời khách" (Owner review AI conversations)

> **Status:** ✅ APPROVED 2026-11-07
> **Gate:** post-P1 owner visibility — SRS v1.1 §6.3 (conversations review) · NFR-8 · **main sln changes — additive**
> **Phụ thuộc:** P6b (admin API `GET /v1/admin/tenants/{t}/conversations` + `?sessionId=` — DONE) · `TenantAiApiClient` sẵn có
> **User duyệt design:** "ShopInbox thêm tab review AI conversations (read-only qua admin API P6b Get-AiConversations — đã có sẵn backend)" (2026-11-07)

## Objective

Chủ quán nhìn thấy khách đã hỏi AI những gì — ngay trong hộp thư quen thuộc, không cần ops script. Read-only: AI conversations chỉ review, không reply (reply đi qua channel human — P8 handoff).

## Hiện trạng (verified)

| Thành phần | File | Ghi chú |
|---|---|---|
| Owner inbox | `5_WebApps/ShopERP/Components/Pages/Community/ShopInbox.razor` | `[Authorize(Roles="Owner")]` · danh sách conversations `Shop/{tenantId}` + detail qua `RealtimeChatPanel` · `ShopInboxApiClient` lấy list/unread |
| AI admin client | `ShopERP/Services/TenantAiApiClient.cs` | signed ctx caller `TenantAi:CallerId` (scope có `admin`) · self-disable khi thiếu config · đã có `GetPendingDocumentsAsync`/`GetKnowledgeStatusAsync` |
| Backend | `vanan-ai` P6b | `GET /v1/admin/tenants/{t}/conversations` (paged summary) + `?sessionId=` drill — **đã deploy** |

## Deliverables

### 1. `TenantAiApiClient` — +2 methods (pattern hiện có, `AdminTimeout` 5s)

| Method | Endpoint |
|---|---|
| `GetAiConversationsAsync(Guid tenantId, int page, int size, CancellationToken)` | `GET v1/admin/tenants/{t}/conversations?page=&size=` → `TenantAiConversationList?` |
| `GetAiConversationMessagesAsync(Guid tenantId, string sessionId, CancellationToken)` | `GET v1/admin/tenants/{t}/conversations?sessionId=` → `TenantAiConversationDetail?` |

DTOs mới (ShopERP-side, khớp contracts P6b):
`TenantAiConversationSummary(string SessionId, string Channel, DateTime LastActiveAtUtc, int MessageCount, int RefusedCount, int DegradedCount)` · `TenantAiConversationList(int Page, int Size, int Total, List<...> Items)` · `TenantAiConversationMessage(string Role, string Content, string? Intent, double? Confidence, bool Refused, bool Degraded, DateTime CreatedAtUtc)` · `TenantAiConversationDetail(string SessionId, List<...> Messages)`.

### 2. `ShopInbox.razor` — tab UI

| Thành phần | Spec |
|---|---|
| Tab strip | "Tin nhắn khách" (hiện hữu) \| **"AI trả lời khách"** — render tab chỉ khi `TenantAiEnabled` flag ON (`IShopFeatureSettingsService.IsEnabledAsync`, pattern ShopFeatures) · flag off → tab ẩn, không gọi HTTP |
| List pane (tab AI) | Sessions desc theo `lastActiveAtUtc`: sessionId (truncate) · channel badge (`public`/`preview`) · messageCount · badges `refused:N`/`degraded:N` khi >0 · phân trang "Xem thêm" (+20) khi `total > loaded` |
| Detail pane | Click session → messages read-only: role bubble (user/assistant) · content · meta dưới assistant: `intent` chip · `confidence` % · badges refused/degraded · **không input gửi** — footer text "AI trả lời khách — chỉ xem, trả lời qua tab Tin nhắn khách" |
| States | loading spinner · service down/empty → "Chưa có hội thoại AI nào" / fail-safe alert (pattern `TenantAiApiClient` drop → null → empty state, KHÔNG exception) |

UI Platform components only (VanACard/VanAButton/badges — hard stop). CSS `.razor.css` theo style inbox hiện hữu.

### 3. DI/wiring

- `TenantAiApiClient` đã đăng ký (`Program.cs:875`) — inject `ITenantAiClient` + `IShopFeatureSettingsService` vào `ShopInbox.razor` (lazy call: chỉ gọi khi user mở tab AI, không load upfront).

## Tests bắt buộc (ShopERP.Tests)

- [ ] Client: mock HttpMessageHandler → `GetAiConversationsAsync` map đúng fields + page/size query · `GetAiConversationMessagesAsync` map messages · fail → null (không throw)
- [ ] Component/service-level: flag off → zero HTTP call (pattern `FlagOff_MakesZeroHttpCalls`)
- [ ] bUnit nếu có pattern sẵn trong ShopERP.Tests cho razor; không thì controller/client-level đủ + manual RV

## Acceptance

- [ ] Tests xanh · `dotnet build VanAn.sln` 0 err · ShopERP.Tests không regression · guard-check PASSED
- [ ] RV sau deploy: owner An Tâm mở ShopInbox → tab "AI trả lời khách" → thấy sessions pilot thật (22+ eval chats) → drill 1 session đọc được hội thoại · flag off → tab biến mất

## Không được làm

- Không cho owner reply vào AI session · không pipe AI messages vào `Shop/{tenantId}` conversation human
- Không thêm actions admin khác (approve docs/profile — scope P10 admin UI)
- Không đụng `RealtimeChatPanel`/MessagingHub

## Delivered

**Implement 2026-11-07 — build 0 err · ShopERP.Tests 14/14 TenantAi (9 cũ + 5 mới) · guard-check PASSED.**

- `TenantAiApiClient.cs`: +`GetAiConversationsAsync` (page/size, AdminTimeout 5s) + `GetAiConversationMessagesAsync` (sessionId drill) + 4 DTOs mirror contracts P6b. Fail-safe: HTTP lỗi/404/transport → `null` (KHÔNG throw); client disabled → zero HTTP.
- `ShopInbox.razor`: tab strip `nav nav-tabs` (pattern MembershipApplications) "Tin nhắn khách" | "AI trả lời khách" — render chỉ khi `IsEnabledAsync(tenantId, TenantAiKnowledgeSyncService.FlagName)` (= "TenantAiEnabled" ops toggle) ON; flag read lỗi → tab ẩn (fail-safe). Lazy: HTTP chỉ chạy khi owner mở tab AI lần đầu.
- List pane: sessions desc (server order), sessionId truncate 10, channel badge, messageCount, badges refused/degraded khi >0, "Xem thêm (+N)" pagination khi `total > loaded`.
- Detail pane: read-only bubbles user/assistant + meta (intent chip, confidence %, refused/degraded badges) + footer "chỉ xem, trả lời qua tab Tin nhắn khách" · states loading/empty/fail-safe.
- Header "Làm mới" giờ reload theo tab đang mở (`RefreshAsync`).
- Tests mới `TenantAiConversationClientTests` (5): map fields + `page&size` query + X-Tenant-Ctx header · detail map + `sessionId` query · 500 → null · 404 → null · disabled config → null + zero HTTP.
- bUnit component test: bỏ qua — `ShopInboxApiClient` là concrete class (không interface) nên không mock được trong bUnit; coverage ở client-level + RV thủ công (đúng fallback của card).

**Deploy + RV production 2026-11-07 (`06a8b993` — CD #42337045249 ALL SUCCESS):**
- L1: ShopERP.dll chứa `GetAiConversationsAsync`/`TenantAiConversationDetail` · image `06a8b993`.
- L2: `/community/messages` 302→Login · health 200.
- L3 production script `6_Testing/rv-p9-inbox-ai.cjs` (impersonate Owner An Tâm) **8/8 PASS:** tab hiện khi `TenantAiEnabled=true` (verified trong SQLite SystemSettings) · **20 sessions** pilot load được · badges refused/degraded · drill session → messages + footer "chỉ xem".
- Còn: L4/L5 user manual + flag-regression (tắt flag → tab ẩn — ops đã có quy trình SetToggle).
