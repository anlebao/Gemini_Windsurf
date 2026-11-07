# Task AI-P8 — Unified Chat Widget: AI-first + Human Handoff (1 bubble, 2 modes)

> **Status:** ✅ APPROVED 2026-11-07
> **Gate:** post-P1 storefront UX — SRS v1.1 §6.1 · FR-10 · NFR-3 · **main sln changes — additive**
> **Phụ thuộc:** P4 (AiChatWidget + RealtimeChatPanel đã deploy) · P7 pilot (không block — parallel được)
> **User duyệt design:** "unified widget 1 bubble — mặc định AI mode, nút 'Chat với chủ quán' chuyển sang human mode (mount RealtimeChatPanel inline) + auto-escalate khi refuse" (2026-11-07)

## Objective

Gộp 2 điểm chat trên storefront thành **1 floating bubble duy nhất**: khách chat AI trước (metered, grounded); khi AI refuse / khách chủ động bấm → chuyển sang chat chủ quán (MessagingHub sẵn có, free). Không merge backend — chỉ chuyển giao UX.

## Hiện trạng (verified 2026-11-07)

| Thành phần | File | Ghi chú |
|---|---|---|
| AI bubble | `UI.Platform/Components/Ai/AiChatWidget.razor` | floating bubble + panel · `TenantId` · `OnAddToCart` · config `/v1/config/widget` (self-hide khi `aiEnabled=false`) · mount `Store.razor:302` sau flag `_features.AIChat_Enabled` |
| Human chat section | `UI.Platform/Components/Realtime/RealtimeChatPanel.razor` | **inline page section** (không phải bubble) · mount `Store.razor:271` `SubjectType="Shop"` · params: `SubjectId:Guid`, `CurrentUserId:Guid`, `CustomerToken`, `CustomerDeviceId`, `StaffToken`, `ScrollContainerId` · SignalR `/hubs/messaging` + HTTP send |
| Owner inbox | `ShopERP/Components/Pages/Community/ShopInbox.razor` | subject `Shop/{tenantId}` — không đổi ở card này (P9 lo phần review AI) |

**Vấn đề:** khách có 2 entry chat riêng (section "Trò Chuyện Với Cửa Hàng" + bubble AI); AI refuse = dead-end, không có đường tới chủ quán.

## Deliverables

### 1. `AiChatWidget` — thêm human-handoff surface (additive, UI.Platform)

| Thay đổi | Spec |
|---|---|
| `[Parameter] public RenderFragment? HumanContent` | Nội dung render khi `_mode == Human` — host (Store.razor) truyền `<RealtimeChatPanel .../>` đã config sẵn identity. Chỉ mount khi human mode (Blazor lazy — SignalR connection chỉ mở khi switch, đúng tiết kiệm) |
| `[Parameter] public bool HumanChatAvailable { get; set; }` | false → giữ nguyên behavior hiện tại (backward-compat, widget dùng nơi khác vẫn chạy) |
| Mode state `_mode` | `Ai` (default) / `Human` · header bubble đổi title: "trợ lý quán" ↔ "Chủ quán" + nút back "← Trợ lý AI" |
| Nút header "💬 Chat với chủ quán" | Hiển thị khi `HumanChatAvailable && HumanContent != null` → `_mode = Human` |
| **Auto-escalate CTA** | Assistant message `Refused=true` → render chip "Hỏi chủ quán trực tiếp" ngay dưới message → click → `_mode = Human` + `RaiseDraftRequested(câu hỏi user của turn đó)` |
| `[Parameter] public EventCallback<string?> OnDraftRequest` | Bắn khi escalate từ refuse CTA, payload = user text của turn bị refuse (tối đa 500 chars) — host dùng để prefill human input |

### 2. `RealtimeChatPanel` — `[Parameter] public string? InitialDraft` (additive nhỏ)

- `OnInitializedAsync`/`OnParametersSet`: `InitialDraft` non-empty → prefill `_inputText = InitialDraft` **1 lần** (flag `_draftApplied` — không ghi đè text user đang gõ khi re-render).
- Null/empty → behavior giữ nguyên 100%.

### 3. `Store.razor` — gộp mount

```razor
@if (_features.AIChat_Enabled)
{
    <AiChatWidget TenantId="_store.Id"
                  OnAddToCart="AddAiProductToCartAsync"
                  HumanChatAvailable="@_features.ShopChat_Enabled"   @* flag section chat hiện hữu *@
                  OnDraftRequest="OnAiEscalateDraft">
        <HumanContent>
            <RealtimeChatPanel SubjectType="Shop" SubjectId="_store.Id"
                               CurrentUserId="ChatIdentity.Value"
                               CustomerToken="@_customerToken"
                               CustomerDeviceId="_customerDeviceId"
                               ScrollContainerId="ai-widget-human-scroll"
                               InitialDraft="@_humanDraft" />
        </HumanContent>
    </AiChatWidget>
}
```

- `OnAiEscalateDraft(string? q)` → `_humanDraft = q` (set trước khi HumanContent mount → prefill đúng).
- Section "Trò Chuyện Với Cửa Hàng" (`Store.razor:265-276`) **giữ nguyên** — page section phục vụ flow khác (scroll context); bubble là entry nhanh. Nếu sau RV thấy trùng → quyết remove sau, không trong card này.
- Flag khi AI off nhưng ShopChat on: bubble vẫn hiện ở Human mode (AI mode ẩn theo `aiEnabled` logic hiện có — widget tự xử lý: `aiEnabled=false && HumanChatAvailable` → bubble mở thẳng human).

### 4. CSS/layout

- `.razor.css` cho human-mode panel trong bubble: `RealtimeChatPanel` là section component → bọc container `.ai-widget-human { height: 420px; overflow: hidden }` + scroll container id riêng (`ai-widget-human-scroll` — không trùng `shop-chat-scroll` của section).

## Ranh giới (giữ nguyên — HARD)

- **Không merge backend:** AI turns → vanan-ai PG (metering + 90d retention); human messages → MessagingHub store (free). Hai history tách — không pipe chéo.
- AI **không tự post** vào human channel — escalation là hành động của khách (bấm nút / bấm chip).
- Không động: SignalR hub, MessagingHub, vanan-ai service, ShopInbox (P9).
- UI Platform components only (hard stop).

## Tests bắt buộc (Core.Tests bUnit + unit)

- [ ] Widget: `HumanChatAvailable` → nút "Chat với chủ quán" render → click → `_mode=Human`, `HumanContent` mount, header "Chủ quán" + back button
- [ ] Back → AI mode, lịch sử AI giữ nguyên (state không reset)
- [ ] Refused assistant message → chip "Hỏi chủ quán trực tiếp" → click → `OnDraftRequest` bắn đúng user text + `_mode=Human`
- [ ] `HumanChatAvailable=false` → không nút/chip nào xuất hiện (regression behavior cũ)
- [ ] `aiEnabled=false` + human available → bubble mở human mode trực tiếp
- [ ] `RealtimeChatPanel`: `InitialDraft` → input prefill 1 lần · re-render không ghi đè · null → không đổi
- [ ] Store-level smoke: escalate flow draft truyền đúng (bUnit composite hoặc unit OnAiEscalateDraft)

## Acceptance

- [ ] Tests xanh · `dotnet build VanAn.sln` 0 err · Core.Tests + ShopERP.Tests không regression · guard-check PASSED
- [ ] RV (sau deploy): storefront An Tâm — hỏi câu refuse → chip hiện → bấm → human mode + draft prefill → gửi → tin nhắn tới ShopInbox owner · AI mode back → lịch sử còn
- [ ] E2E spec update `ai-chat-widget.spec.ts` thêm 1 case handoff (Gate 4)

## Không được làm

- Không đẩy AI messages vào MessagingHub / không cho owner reply vào AI session
- Không auto-send câu hỏi vào human channel (chỉ prefill — khách tự bấm gửi)
- Không remove section "Trò Chuyện Với Cửa Hàng" trong card này

## Delivered

**Implement 2026-11-07 — build 0 err · Core.Tests 19/19 (7 cũ + 8 widget handoff + 4 draft panel) · guard-check PASSED.**

- `IAiChatClient.cs`: `AiSessionMessage` + `bool Refused = false` (additive — history payload không có field → false, backward compatible; vanan-ai `SessionMessageDto` giữ nguyên, khi nào service thêm `refused` thì client tự bind).
- `AiChatWidget.razor`: `WidgetMode {Ai, Human}` · `HumanContent`/`HumanChatAvailable`/`OnDraftRequest` params · header "Chat với chủ quán" ↔ back "← Trợ lý AI" (back chỉ hiện khi `aiEnabled=true`) · chip "Hỏi chủ quán trực tiếp" trên assistant msg `Refused` → `EscalateAsync` (draft = user text turn đó, truncate 500) · `ShowWidget` = config-resolved && (aiEnabled || handoff) → aiEnabled=false/down + human available = bubble mở thẳng human.
- `RealtimeChatPanel.razor`: `InitialDraft` (prefill 1 lần qua `_draftApplied` trong `OnParametersSet`) + `HeaderTitle` (default giữ nguyên — additive ngoài card spec, cần để panel trong bubble hiện "Chủ quán" thay "Chat với khách hàng").
- `Store.razor`: unified mount — **deviation: `HumanChatAvailable="@ChatIdentity.HasValue"`** thay `_features.ShopChat_Enabled` (flag đó không tồn tại; gate thật của section chat hiện hữu là ChatIdentity — đúng ý card "flag section chat hiện hữu") · `OnAiEscalateDraft` → `_humanDraft` → `InitialDraft` + `ScrollContainerId="ai-widget-human-scroll"`.
- `AiChatWidget.razor.css`: `.ai-widget-human` (420px, ::deep flex layout — inner message list flex-fill thay inline 400px cap) + `.vanan-ai-escalate`.
- Quyết định implement: fallback transport-error "Trợ lý AI tạm nghỉ" cũng đánh `Refused=true` (dead-end → mở đường tới chủ quán, đúng intent auto-escalate).
- Tests mới: 8 trong `AiChatWidgetTests` (button switch/back/history giữ/refuse chip + draft/truncate 500/human off regression/aiEnabled=false human mode/AI down null-config/composite RealtimeChatPanel prefill) + 4 trong `RealtimeChatPanelDraftTests` (prefill 1 lần/no-overwrite/null/HeaderTitle).
- E2E `ai-chat-widget.spec.ts` +2 case (Gate 4): handoff button/back flow · refuse→chip→human+draft prefill (`AI_CHAT_TEST_REFUSE_QUESTION` env, self-skip nếu AI không refuse).
