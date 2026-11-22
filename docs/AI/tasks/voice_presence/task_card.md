# Task Card: Voice Presence — "login = sẵn sàng nhận cuộc gọi"

> **Status:** 🟡 Phase A+B DONE — 16/16 full RV (`67088f40`). Phase C IMPLEMENTED — R9 round-robin routing (server) + R10 busy/offline chat-suggestion UX + R12 presence badge đỏ/xanh/xám; R11 verify-only (đã có sẵn). Đang chờ guard → deploy → RV.
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

- [x] **R1. `VoicePresencePanel`** (component mới, UI.Platform) — implement qua `VoiceCallCoordinator` thay EventCallback (bus pattern, cover cả 2 chiều). Init `vananVoiceChat` khi chưa có owner, ngược lại observer. KHÔNG render call button.
- [x] **R2. JS singleton guard** — `voice-chat.js` expose `isInitialized()` + `_initInFlight` guard (chặn race init đồng thời); `VoiceCallPanel` delegated mode: skip init/dispose JS, replay coordinator state, vẫn fetch missed calls.
- [x] **R3. State bridge** — `VoiceCallCoordinator` scoped per-circuit: owner publish hub/call/incoming; chat panel delegated mirror + `ChatSurfaceActive` suppress global UI (tránh double overlay).
- [x] **R4. ShopERP toggle** "Sẵn sàng nhận cuộc gọi" — `VoicePresenceHost` mount trong `UserHeader` (cover cả 4 layouts, thay NavMenu — cùng hiệu quả, ít file đụng hơn). Icon `bi-telephone` + chấm xanh; persist `localStorage vanan_voice_presence`; ON → mount `VoicePresencePanel` (staff JWT qua `MintOwnerTokenAsync`); OFF → dispose → connection drop → customer thấy OFFLINE. Tự ẩn khi flag `Voice_Call_Enabled` OFF hoặc static-SSR page.
- [x] **R5. KhachLink auto-presence** — `VoicePresencePanel` mount trong `Store.razor` khi `_customerToken` + `_customerId` + `_features.Voice_Call_Enabled`; mount TRƯỚC `AiChatWidget` để làm JS owner → chat panel delegated. Không toggle (đúng design).

### P1 — Incoming call UX toàn cục

- [x] **R6. Global incoming-call overlay** — `voice-presence-incoming` trong `VoicePresencePanel`: icon + `IncomingCallerName` + `IncomingContextLabel` + accept/reject (`voice-presence-accept`/`-reject`); hiện khi `CallState==ringing` && `!ChatSurfaceActive`.
- [x] **R7. In-call mini bar toàn cục** — `voice-presence-callbar`: state label + timer mm:ss (PeriodicTimer) + mute/end; hiện khi call active && `!ChatSurfaceActive`.
- [x] **R8. Chat↔presence handoff** — delegated mode: chat `VoiceCallPanel` mirror coordinator → card trong chat vẫn render + `CallStateChanged`/`HubStateChanged` events vẫn bắn cho header button; global UI tự ẩn khi `ChatSurfaceActive`.

### P2 — Multi-customer routing + edge cases

- [x] **R9. Load-aware counterpart pick** — `PermissionService` + `VoiceStateStore.NextRotationIndex`: round-robin theo `contextReferenceId` trong nhóm reachable+idle (FR-007 = 1 call/user nên "load" = phân phối đều, không phải least-loaded). 3 unit test mới: rotate B→C→B, busy candidate bị skip, 0 reachable → fallback candidates[0].
- [x] **R9b. Test harness fix** — `CallServiceHarness` thiếu `IVoiceDisplayNameResolver` (regression từ Phase A+B, 28 test fail) → thêm `StubDisplayNameResolver` → 88/88 voice tests pass.
- [x] **R10. Busy UX** — `VoiceCallPanel` terminal messages: OFFLINE → "…không trực tuyến — bạn có thể để lại tin nhắn, chúng tôi sẽ chuyển ngay khi họ online."; BUSY → "…đang bận — bạn có thể để lại tin nhắn." Caller đã ở trong chat thread nên không cần deep-link.
- [x] **R11. Multi-device dedupe** — VERIFY-ONLY: server `AcceptCall` broadcast `CallAccepted` tới mọi connection (`CallService.AcceptCallAsync`), first-accept-wins `CallServiceIntegrationTests.First_accept_wins_other_devices_get_cleanup_event` PASS, SDK `answeredElsewhere → Idle` cleanup. Không cần code mới.
- [x] **R12. Presence-aware badge** — `VoicePresenceHost` subscribe `VoiceCallCoordinator.Changed` → toggle button `is-calling` (đỏ nhấp + pulse ring) khi `IsCallActive(CallState)`; xám=tắt / xanh=sẵn sàng / đỏ nhấp=đang gọi. `VoiceCallCoordinator.IsCallActive` = single source (panel delegate). Customer online-dot: defer (nice-to-have).

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

## 7. Verification log

- Build KhachLink + ShopERP: 0 errors · `VanAn.Core.Tests` Realtime/AiChatWidget/ChatDateFormatter filter: **128/128** · `VoiceCallCoordinatorTests` 6/6 + `VoicePresencePanelTests` 6/6 (bUnit: overlay caller name, ChatSurfaceActive suppression, callbar, idle).
- Guard-check v7.3: ALL PASSED (4927 warnings pre-existing).
- Commit `b9eae8e5` → CD `44913784981` **SUCCESS**.
- RV static: `voice-chat.js` deployed chứa `isInitialized`/`_initInFlight`; store page 200. Spec `voice-presence.spec.ts` + config `playwright-rv-voice-presence.config.ts` self-gating — **full overlay RV cần customer session login thật** (guest → panel không mount → skip); ShopERP toggle RV cần staff login.
- Review fixes trước commit: JS `_initInFlight` race guard; delegated panel vẫn `LoadMissedCallsAsync`; `VoiceCallPanel` catch fallback delegated khi thua race init.

### Full call-flow RV (2026-11-21/22) — 16/16 PASS ✅

Sweep-first: `voice-presence-preflight.mjs` **5/5** → `voice-presence-rv.mjs` **16/16** trên production (An Tâm `e2d91cdf`, 2 browser contexts: customer auto-presence + staff toggle ON, call THẬT qua voice.khachvip.online — GUM/TURN/RTCPeerConnection đều chạy, callbar hiện cả 2 phía, end → dọn UI, toggle OFF → `isInitialized=false`).

Fixes trong RV loop:
- **S9 caller name**: `CallService.CallerDisplayName` stub trả GUID → `DisplayNameResolver` query Gateway PG (`Customers.FullName` → fallback GUID, fail-open) + `Voice:DisplayNameSql` config + staff token kèm DisplayName. Overlay hiện "RV Test Customer".
- **S10/S11 accept/end không chạy**: root cause = raw `<button @onclick>` do `VoicePresencePanel` render trực tiếp **không được Blazor dispatch** trong island `InteractiveServerRenderMode(prerender:false)` — chứng minh bằng jsCalls counter=0 qua cả Playwright click lẫn DOM `.click()`, `[RV-DIAG]` server log xác nhận handler không fire, trong khi toggle parent cùng island hoạt động. Fix: swap sang `<VanAnButton OnClick>` (button do child component render — cùng pattern `VoiceCallPanel` đã verify prod) → dispatch OK, `[RV-DIAG]` confirm `AcceptCallAsync invoked owner=True ringing → JS ok`, `EndCallAsync connected → JS ok`. Kèm exception logging thay `catch{}` nuốt lỗi.
- **Script**: đóng customer widget trước call (global UI path), check C5 sớm (tránh ring timeout), staff hub retry, tiered fallback probes (hit-test/jsCalls/state log) giữ lại làm diagnostic.

## 8. Explicit non-goals

- Không đổi VoiceHub protocol/CallStateMachine/metering gate
- Không call queue/waiting room (defer — cần state machine mới)
- Không group call / call transfer
- Không push notification khi app closed (PWA push = workstream riêng)
- Customer toggle "sẵn sàng" — auto-presence là đủ cho v1
