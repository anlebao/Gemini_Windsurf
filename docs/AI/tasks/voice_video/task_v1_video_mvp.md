# TASK CARD: Voice Video Call MVP (V1)

> Master plan: `docs/AI/plans/voice-video-call-master-plan.md`
> Parent: `docs/AI/plans/voice-service-master-plan.md` (voice P1–P7 đã ship)
> Mode: `newfeaturebuild.md` — ANALYZE ✅ → IMPLEMENT ✅ A+B+C+D CODE DONE → chờ push/deploy + RV
> Created: 2026-11-19 · Owner: Devin session

## OBJECTIVE

Nâng cấp voice call 1:1 hiện có → **video call** trong `VoiceCallPanel`, kèm 3 fix nền:
STUN missing (P2P ~0% → mục tiêu 60–80%), `ConnectionType` thật (metrics đang nói dối),
`CallType` Audio|Video tách metering (`video.call`/`video.minute`).

## SCOPE LOCK (user decisions 19/11/2026)

- [x] Video 1:1 only — KHÔNG group/SFU/recording/screen-share
- [x] Camera toggle anytime (re-offer + polite/impolite glare rule)
- [x] `CallType` làm luôn: DB column + contract + metering keys riêng
- [x] Guest giữ gate login — không mở guest video
- [x] STUN + ConnectionType gộp chung MVP
- [x] Feature flag: dùng chung `Voice_Call_Enabled` (không thêm flag mới)

## PRE-FLIGHT CHECKLIST (Phase A0 — DONE 22/11/2026)

- [x] **A0.1 Metering rate cards:** `video.*` CHƯA seed — NHƯNG unknown/unpriced serviceCode → `deny=false` luôn (`WalletService` "Unpriced / MeterOnly → deny=false always"). **Quyết định: dùng `video.call`/`video.minute` trực tiếp** — usage vẫn được record, rate card seed sau sẽ bắt đầu enforce. Không cần fallback.
- [x] **A0.2 `/v1/turn/credentials`:** trả `TurnCredentials` record — thêm `StunUrls` init property = additive JSON field, client cũ ignore. An toàn.
- [x] **A0.3 `VoiceCallCoordinator`:** `CallInfoJson` pass-through sẵn (PublishCallState(state, infoJson)) — SDK nhét `callType` vào state info JSON là xong, không cần plumbing mới.
- [x] **A0.4 Header call button:** `RealtimeChatPanel.razor` có `voice-call-header-btn` → `StartVoiceCallAsync()` → `_voicePanel.StartCallAsync()`. **Phase C:** `StartCallAsync(callType)` overload + nút video riêng ở header.
- [x] **A0.4b Global video surface:** `VoicePresencePanel` (incoming overlay + mini callbar) cũng cần video hosts + camera toggle — thêm vào Phase C checklist.
- [x] **A0.5 Coturn prod:** compose KHÔNG có `--tls-listening-port`/cert → `turns:` 5349 không listen (R7 confirmed — non-blocking, browser fallback udp/tcp 3478). STUN ngầm có trên UDP 3478.
- [x] **A0.6 `MeteringClient.CheckAsync`:** arbitrary serviceCode string → GET `v1/wallets/{t}/check?serviceCode=...` — keys `video.*` truyền được ngay.

## IMPLEMENTATION CHECKLIST

### Phase A — Voice service (`10_Voice/vanan-voice`) ✅ DONE 22/11/2026 — 98/98 tests

**Tests first (TDD):**
- [x] `TurnCredentialServiceTests` +2: StunUrls default + config csv override; assert STUN không lẫn trong TURN urls.
- [x] `CallServiceIntegrationTests` +7: video persist+incoming calltype, default audio, invalid calltype fail-fast (no row + CallFailed), metering video keys qua MockHttp (assert `"serviceCode":"video.call"` + metadata), NotifyConnected TURN/P2P/garbage→P2P.
- [x] `VoiceMetricsServiceTests` +1: VideoCalls count.

**Code:**
- [x] `VoiceContracts.cs`: `StartCallRequest.CallType` (optional 5th), `IncomingCallDto.CallType` (optional 6th).
- [x] `VoiceHub.cs`: **learned — SignalR KHÔNG bind optional hub params** (1-arg invoke lỗi "Failed to invoke ... due to an error"). Fix: giữ `NotifyConnected(Guid)` + thêm `NotifyConnectedWithType(Guid, string?)` — JS SDK sẽ gọi method mới, client cũ tương thích.
- [x] `VoiceCall.cs`: `CallTypes` static class (Audio/Video + `Normalize` — null→audio, unknown→null reject); `VoiceCall.CallType` + `Create(..., callType)`.
- [x] `VoiceDbContext.cs`: `HasMaxLength(8)`.
- [x] `VoiceSchema.cs`: `ColumnExists` (PRAGMA table_info, metering pattern) + conditional ALTER + backfill `CallType='audio'`.
- [x] `CallService.cs`: `Normalize` fail-fast `INVALID_CALLTYPE` (no row, CallFailed); ×4 `VoiceCall.Create` + `IncomingCallDto` truyền callType; `NotifyConnectedAsync(…, string? connectionType)` whitelist TURN/P2P → P2P; metering keys `{prefix}.call/.minute` (`video` khi CallType=video, `voice` khi audio) ở cả gate lẫn `IngestUsage` + metadata `callType`.
- [x] `TurnCredentialService.cs`: `StunUrls` init property (additive JSON) — default `stun:{host}:3478`, override `TURN:StunUrls` csv.
- [x] `VoiceMetricsService.cs`: +`VideoCalls` count.

**Gate A:** `dotnet build` 0 err + `dotnet test` **98/98 pass** ✅

### Phase B — Client JS (`UI.Platform/wwwroot/js`) ✅ DONE 22/11/2026

- [x] `voice-media.js`: `requestMedia(video)` (giữ `requestMic` alias), `setCameraEnabled` (add/removeTrack + stop), `getLocalStream()`, `hasVideoTrack()`, `getConnectionType()` (getStats → relay→"TURN" else "P2P"), `getSignalingState()`; `createAnswer` rollback cho polite glare.
- [x] `voice-sdk.js`: `callType`/`cameraOn` state; `startCall` gửi callType (context.callType); `onIncoming` đọc `d.callType`; `setState` merge `{callType}` vào mọi info JSON (→ Coordinator pass-through); `toggleCamera()` + re-offer SendOffer khi peer tồn tại; glare: impolite caller ignore remote offer, polite callee rollback; remote `<video muted>` vào `#voice-remote-video-host` + local PiP vào `#voice-local-video-host`; `attachVideoElements()` cho host div render muộn; `onVideoChange(localOn,remoteOn)`; **NotifyConnectedWithType** (SignalR không bind optional params — bug đã fix Phase A); **STUN merge**: `[{urls:stunUrls},{urls:turnUrls,creds}]`, fallback `stun:stun.l.google.com` khi TURN off/lỗi.
- [x] `voice-chat.js`: `startCall(...,callType)`; `toggleCamera`/`getCallType`/`isCameraOn`/`attachVideoElements` (optional-chained cho SDK cũ); `onVideoChange` → `OnVoiceVideoChange` JSInvokable.
- [x] Backward compat: audio call path giữ `video:false` (requestMedia(false) = MIC_CONSTRAINTS cũ).

**Gate B:** `node --check` 3 files PASS · `dotnet build UI.Platform` 0 err (warnings pre-existing) ✅

### Phase C — Blazor UI ✅ DONE 22/11/2026

- [x] `VoiceCallPanel.razor.cs`: `_callType` (parse infoJson qua `Coordinator.ParseCallType`, reset về audio khi idle), `_cameraOn`/`_remoteVideoOn`; `OnVoiceVideoChange(bool,bool)` → publish coordinator; `ToggleCameraAsync`; `StartCallAsync(callType)`; delegated `SyncVideoFromCoordinator`; `_pendingVideoAttach` + `OnAfterRenderAsync` → `attachVideoElements`.
- [x] `VoiceCallPanel.razor`: video host divs khi `!_idleLike && (callType==video || stream live)` — `_showVideo` cover cả audio→video upgrade path; camera button cạnh mute; title/icon "Cuộc gọi video" phân biệt.
- [x] `VoiceCallPanel.razor.css`: remote 16:9 cover, local PiP 88px góc dưới-phải `scaleX(-1)`, placeholder "Đang chờ video...".
- [x] **`VoicePresencePanel.razor(.cs/.css)`**: global video overlay (remote + PiP, loại trừ chat surface qua `ChatSurfaceActive` — cùng host ids, mutual exclusion by design); camera toggle trong callbar; `OnVoiceVideoChange` → `PublishVideoState`; VanAnButton OnClick giữ nguyên.
- [x] `VoiceCallCoordinator`: `CallType`, `VideoLocalOn`, `VideoRemoteOn`, `PublishVideoState`, `ParseCallType`; `Reset()` dọn sạch.
- [x] `RealtimeChatPanel` header: nút `bi-camera-video` → `StartCallAsync("video")`, cùng `VoiceCallReady` gate.
- [x] Guest path: `VoiceCallReady` requires identity → cả 2 nút disabled + title "Đăng nhập để gọi...".

**Gate C:** `dotnet build` UI.Platform + ShopERP + KhachLink 0 err ✅ · `guard-check.ps1` ALL PASSED (build + analyzers + fast test gate) ✅

### Phase D — Wrap-up ✅ DONE 22/11/2026

- [x] Sync `10_Voice/vanan-voice/sdk/js/voice-media.js` + `voice-sdk.js` — byte-identical copy từ UI.Platform (source of truth); `node --check` pass.
- [x] **`6_Testing/rv-scripts/voice-video-rv.mjs` MỚI** — user directive "kế thừa sweep test đợt voice mới nhất": clone pattern `voice-presence-rv.mjs` (2 contexts, fake media, presence path, chat đóng → global UI). 18 checks: V0 stunUrls (AC6) · V1-V2 startCall video + callType · V3-V4 incoming overlay + callbar · V5-V7 video overlay + local PiP + remote live (AC1) · V8-V10 camera toggle OFF→remote off→ON→remote back (AC2) · V11-V12 end cleanup · V13 metrics videoCalls+1 (AC4) · V14-V16 audio regression (AC3).
- [x] Update task card + `project_state.md` (status + next actions + maintenance log).
- [ ] **CÒN NGOÀI CODE:** push → CD deploy-voice → chạy `voice-video-rv.mjs` (cần VOICE_CUSTOMER_TOKEN dev-token window + staff creds).

## ACCEPTANCE CRITERIA

- [x] AC1: Gọi video 1:1 KhachLink↔ShopERP thành công — remote thấy video, local PiP mirror. **RV V5-V7 PASS**
- [x] AC2: Toggle camera giữa call (cả 2 phía) — video bật/tắt không rớt call. **RV V8-V10 — 1 bug bắt được + fixed**
- [x] AC3: Audio call hiện tại không regression (UI + flow y hệt). **RV V14-V16 PASS**
- [x] AC4: `voice_calls.CallType` đúng; metering nhận `video.*` events. **RV V13 PASS** (metrics videoCalls +1)
- [ ] AC5: `ConnectionType` = "TURN" khi relay, "P2P" khi direct — client report implemented; `turnRelayed` field verify khi có call qua TURN thật (RV env P2P).
- [x] AC6: STUN urls xuất hiện trong `/v1/turn/credentials` response. **RV V0 PASS** (`stun:voice.khachvip.online:3478`)
- [ ] AC7: Guest vẫn bị chặn gọi (cả audio lẫn video) — chưa RV (cần guest browser profile).
- [x] AC8: `dotnet test` voice + `dotnet build` + `guard-check.ps1` pass.

### RV findings (2026-11-22, `voice-video-rv.mjs` → 24/25 → fix → re-run)

- **V10 FAIL → FIXED (replaceTrack):** camera toggle OFF dùng `removeTrack` → remote receiver track `ended`; toggle ON lại (`addTrack` + re-offer) **Chrome không re-fire `ontrack`** → remote video không quay lại. **Fix đúng chuẩn:** `videoSender.replaceTrack(null)` tắt (remote track muted, KHÔNG ended — không cần renegotiation) / `replaceTrack(track)` bật; giữ `videoSender` ref qua toggles; remote `emitVideoState` check `live && !muted` + hook `t.onmute/onunmute`. RV checks V7/V9/V10 cập nhật muted-aware.

## DEFERRED (explicit — không làm trong task này)

- Group call/SFU · guest video · recording/screenshare · `Video_Call_Enabled` flag riêng · multi-TURN geo · Redis VoiceStateStore · coturn `--max-bps` raise cho video · TURN TLS 5349 verify+enable.

## KNOWN RESIDUAL RISKS

- R5: video qua TURN bị cap 1Mbps → choppy; chấp nhận MVP, the STUN fix giảm tần suất.
- R7: `turns:` URL có thể trỏ port chưa listen → browser thử udp/tcp trước nên không block; verify khi deploy.
