# Task Card: Voice Presence — "login = sẵn sàng nhận cuộc gọi"

> **Status:** 📋 DRAFT — chờ approve
> **Depends on:** chat_ui_unification P0-P2 (DONE), VOICE SERVICE P4/S-series (DONE)
> **Scope:** `UI.Platform/Components/Realtime/VoiceCallPanel.*` · `UI.Platform/wwwroot/js/voice-chat.js` · `5_WebApps/ShopERP/Components/Layout/*` · `5_WebApps/KhachLink` layout · `10_Voice` PermissionService (nhỏ) · E2E spec mới

## 1. Objective

Hiện tại "online để nhận call" = "đang mở đúng khung chat có VoiceCallPanel mount":
- **Staff**: chỉ nhận được call khi ShopInbox mở + đã chọn conversation (`ShopInbox.razor:131`)
- **Customer**: chỉ khi human chat mode đang mở (`AiChatWidget.razor:66`)

→ Staff ở Orders/POS/đóng inbox = customer gọi nhận `OFFLINE` ngay (`CallService.cs:166-174`), không đổ chuông.

Mục tiêu: **presence = phiên đăng nhập**, không phải màn hình chat. Staff có toggle "Sẵn sàng nhận cuộc gọi" global trên NavMenu; customer auto-presence khi login. Incoming call hiện overlay toàn cục mọi trang.

## 2. Verified current state

| Thành phần | Hiện trạng |
|---|---|
| Presence | `VoiceStateStore.ConnectionsOf(userId)` — đếm voice SignalR connections sống (`VoiceHub.OnConnectedAsync` → `store.AddConnection`) |
| Voice JS | `window.vananVoiceChat` **singleton** (`voice-chat.js:16` — `let voice=null`, initialize ghi đè) → tối đa 1 initializer/page |
| Staff mount | `VoiceCallPanel` trong `RealtimeChatPanel` trong ShopInbox thread-selected only |
| Customer mount | `VoiceCallPanel` trong `RealtimeChatPanel` trong AiChatWidget HumanContent (lazy, chỉ Human mode) |
| IncomingCall | `CallService.cs:181` broadcast tới MỌI connection của target — presence-only connection nhận được sẵn, không cần server đổi |
| Call routing | `PermissionService` → `ResolveCounterpartCandidatesAsync` (SQL `Voice:ContextVerifySql:chat:counterpart`) → pick đầu tiên `ConnectionsOf>0 && !HasActiveCall` |
| Call identity | `IncomingCallDto` có `CallerDisplayName` + `VerifiedContextLabel` → UI phân biệt được ai gọi |
| Giới hạn | FR-007: 1 user = 1 active call; caller busy/callee busy/collision/offline/metering deny đều terminal |

## 3. Requirements

### P0 — Presence foundation (lớn nhất)

- [ ] **R1. `VoicePresencePanel`** (component mới, UI.Platform): param `PresenceOnly=true`-style — init `vananVoiceChat` với token, KHÔNG render call button, KHÔNG cần SubjectType/SubjectId. Nhận `OnVoiceIncoming` → đẩy call info lên host qua EventCallback. Reuse voice-sdk/voice-chat.js nguyên vẹn.
- [ ] **R2. JS singleton guard** — `voice-chat.js` expose `isInitialized()`/owner-tag; `VoiceCallPanel.OnInitializedAsync` skip init khi presence đã init (tránh steal callbacks). Chat panel `startCall` vẫn gọi `vananVoiceChat.startCall` bình thường (JS function độc lập với component nào init).
- [ ] **R3. State bridge** — scoped C# service `VoiceCallCoordinator` (per-circuit): presence panel đăng ký làm owner; chat `VoiceCallPanel` forward `OnVoiceStateChange`/`OnVoiceIncoming` tới coordinator → chat panel header button + call card vẫn hoạt động khi call start từ chat. Ngược lại presence panel ẩn incoming UI khi chat đang active call (tránh double overlay).
- [ ] **R4. ShopERP NavMenu toggle** "Sẵn sàng nhận cuộc gọi" (icon `bi-telephone` + chấm xanh khi ON): persist `localStorage vanan_voice_presence=1`; ON → mount/keep `VoicePresencePanel` (StaffToken); OFF → dispose (voice connection drop → customer thấy OFFLINE trung thực). Mount trong `MainLayout` khi authenticated + `Voice_Call_Enabled`.
- [ ] **R5. KhachLink auto-presence** — mount `VoicePresencePanel` ở storefront layout khi `CustomerToken` tồn tại + flag ON. Không toggle (customer là phía gọi chính).

### P1 — Incoming call UX toàn cục

- [ ] **R6. Global incoming-call overlay** — component dùng chung (UI.Platform): toast/card góc phải-dưới hoặc top banner: avatar + "{CallerDisplayName} gọi đến" + context label (tên shop/khách) + accept/reject ≥48px + ring animation; auto-dismiss theo ring timeout; `prefers-reduced-motion` safe. Hiện trên MỌI trang khi có `IncomingCall`.
- [ ] **R7. In-call mini bar toàn cục** — khi đang connected và user navigate trang khác → thanh call nhỏ (avatar + timer mm:ss + mute/end) fixed bottom — tái dùng pattern `ai-mini-callbar` P1 chat_ui. KHÔNG block navigation.
- [ ] **R8. Chat↔presence handoff** — accept call từ global overlay khi chat panel không mở: call vẫn chạy (WebRTC theo callId, không cần context phía callee); nếu user mở chat giữa call → chat panel mirror state qua coordinator.

### P2 — Multi-customer routing + edge cases

- [ ] **R9. Load-aware counterpart pick** — `PermissionService`: trong candidates reachable+not-busy, ưu tiên staff có ít active call nhất (hoặc round-robin); hiện tại pick đầu tiên theo SQL order. Server change nhỏ, cần unit test.
- [ ] **R10. Busy UX** — caller nhận `BUSY`/`OFFLINE` → message rõ + gợi ý "Để lại tin nhắn cho chủ quán" (deep-link `?chat=human` hoặc mở chat widget sẵn). Phân biệt: OFFLINE = không ai sẵn sàng → gợi ý chat; BUSY = có người nhưng đang gọi → tương tự.
- [ ] **R11. Multi-device dedupe** — cùng staff login 2 máy: `IncomingCall` tới cả 2 (đã vậy); accept first-wins đã có (`CallService.cs:197`). UI: máy thua nhận `CallAccepted` → auto-dismiss overlay. Verify + test.
- [ ] **R12. Presence-aware badge** — NavMenu icon hiện trạng thái: xám (chưa bật/tắt), xanh (sẵn sàng), đỏ nhấp (đang gọi). Customer side: optional "Đang online" dot trên store header (nice-to-have).

## 4. Trả lời câu hỏi multi-customer (design decision đã chốt)

- **Phân biệt ai gọi:** `IncomingCallDto` đã chứa `CallerDisplayName` + `VerifiedContextLabel` (context `chat:Shop:{tenant}:{customerId}` resolve ra tên khách). Overlay R6 hiển thị tên → staff biết ai.
- **Nhận hết đồng thời:** FR-007 giới hạn **1 active call/user** — N customer cùng lúc cần ≥N staff bật toggle. Routing (R9) chia đều sang staff rảnh. Hết staff → caller `BUSY` + R10 gợi ý chat. **Call queue/waiting room KHÔNG nằm trong scope** — feature riêng nếu cần (giữ customer "đang chờ" + auto-connect khi staff rảnh = thay đổi state machine, defer).
- **Owner gọi lại customer:** vẫn qua context per-customer thread như hiện tại — presence chỉ thêm khả năng *nhận*, không đổi *gọi*.

## 5. Execution strategy — đủ yêu cầu, nhanh

```
Phase A (P0): JS guard + VoicePresencePanel + Coordinator + 2 layout mounts + NavMenu toggle
  → verify: staff toggle ON ở bất kỳ trang nào → customer gọi → đổ chuông + accept được
Phase B (P1): global overlay + in-call mini bar + handoff
  → verify: incoming hiện khi đang ở Orders/POS; accept → call chạy; navigate → mini bar theo
Phase C (P2): load-aware routing + busy UX + badges
  → verify: 2 customer gọi song song → 2 staff khác nhau nhận (nếu ≥2 staff online)
```

Mỗi phase 1 commit; E2E spec `voice-presence.spec.ts` flag-gated (cần tenant Voice_Call_Enabled + 2 identity test: 1 staff token + 1 customer token — tái dùng dev-token RV window).

## 6. Risks

| Risk | Mitigation |
|---|---|
| JS singleton race: chat VoiceCallPanel init sau presence → steal callbacks | R2 guard + coordinator; test mount order cả 2 chiều |
| Double incoming UI (presence overlay + chat card) | R3 suppress rule: chat active call → presence ẩn; chat idle → presence hiện |
| Staff toggle ON nhưng quên → réo máy khi bận | Toggle explicit + badge state; default OFF; document trong NavMenu tooltip |
| Presence connection tốn resource khi idle | 1 SignalR conn/user/session — rẻ; disconnect on logout/pagehide (SDK đã xử lý OnDisconnected) |
| Metering: presence không gọi → không tốn phí (gate chỉ chạy khi StartCall) | Verified `CallService` — không đổi |
| Blazor Server circuit: layout component sống suốt session — dispose khi logout | `IAsyncDisposable` + check auth state change |

## 7. Explicit non-goals

- Không đổi VoiceHub protocol/CallStateMachine/metering gate
- Không call queue/waiting room (defer — cần state machine mới)
- Không group call / call transfer
- Không push notification khi app closed (PWA push = workstream riêng)
- Customer toggle "sẵn sàng" — auto-presence là đủ cho v1
