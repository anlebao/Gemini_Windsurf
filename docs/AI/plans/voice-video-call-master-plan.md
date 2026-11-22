# MASTER PLAN: VOICE VIDEO CALL MVP + STUN/P2P FIX + CALLTYPE + CONNECTIONTYPE

> Created: 2026-11-19 — extends `voice-service-master-plan.md` (voice đã ship P1–P7)
> Status: **APPROVED SCOPE (19/11/2026) — chờ IMPLEMENT**
> Mode: `newfeaturebuild.md` ANALYZE → IMPLEMENT
> Code: `10_Voice/vanan-voice` (service+SDK demo) · `UI.Platform` (js bridge + VoiceCallPanel) — KHÔNG đụng `1_Shared/Domain.cs`, Gateway, metering service

---

## 1. MỤC TIÊU

1. **Video call 1:1** trong cùng `VoiceCallPanel` chat hiện có (KhachLink + ShopERP) — tái dùng toàn bộ signaling/state machine/metering-gate hiện có.
2. **Fix STUN missing** — hiện `/v1/turn/credentials` chỉ trả `turn:`/`turns:` URLs, không STUN → client sau NAT không gather được srflx → **gần như 100% media qua TURN**. Fix để P2P ratio lên 60–80%.
3. **`ConnectionType` thật** — hiện `CallService.NotifyConnectedAsync` hardcode `"P2P"` → `TurnPercentage` metrics luôn = 0. Client report qua `getStats()`.
4. **`CallType` Audio|Video** — tách metering key `video.call`/`video.minute` để rate card riêng; lưu DB để analytics.

## 2. QUYẾT ĐỊNH ĐÃ CHỐT (user, 19/11/2026)

| # | Quyết định |
|---|---|
| D1 | Camera toggle **anytime** — re-offer qua path SendOffer/ReceiveOffer sẵn có; glare handling polite/impolite (callee rollback) |
| D2 | `CallType` làm luôn (đảo lại defer trước đó) — schema + contract + metering keys |
| D3 | Guest (chưa login) giữ gate "Đăng nhập để gọi" — KHÔNG mở guest video |
| D4 | Gộp STUN fix + ConnectionType thật vào cùng MVP (impact cao, effort nhỏ) |
| D5 | Metering keys `video.call`/`video.minute` — verify rate-card tồn tại trước khi chốt; fallback `voice.*` + callType metadata nếu metering strict-deny unknown keys |

## 3. HIỆN TRẠNG (verified 19/11/2026)

| Thành phần | Hiện trạng | Vấn đề |
|---|---|---|
| `TurnCredentialService.Issue()` | Trả `turn:udp`, `turn:tcp`, `turns:tls` — KHÔNG có stun | P2P ~0% cho user sau NAT |
| SDK `resolveIceServers()` | Dùng response làm iceServers duy nhất; `options.iceServers` không bao giờ set | Không STUN fallback |
| `CallService.cs:318` | `SetConnectionType("P2P")` hardcode | Metrics nói dối |
| `VoiceCall` entity | Không có CallType | — |
| `VoiceSchema.EnsureVoiceSchema` | CREATE IF NOT EXISTS, chưa có ALTER path | Cần idempotent ALTER |
| `NotifyConnected(Guid)` | Hub signature 1 tham số | Cần thêm connectionType optional |
| `voice-media.js` | `video:false`, `requestMic()` only | — |
| `voice-sdk.js` | Remote stream → hidden `<audio>`; không video element | — |
| `VoiceCallPanel` | Audio-only UI; `_idleLike`/`_active` states đầy đủ | Thêm video area + camera btn |
| Coturn compose | `network_mode: host`, UDP 3478 + relay 49152–65535, `--max-bps=1048576`, `--user-quota=12`, `--total-quota=1200` | STUN đã listen sẵn trên 3478; `max-bps` 1Mbps sẽ bóp video khi TURN-relay — **theo dõi sau MVP** |
| Deploy | `cd-multivps.yml` job `deploy-voice` → `VPS_VOICE_HOST`, env `.env.voice` | `TURN__TlsPort` cần verify coturn listen 5349 |

## 4. THIẾT KẾ CHI TIẾT

### 4.1 CallType (server)

- `VoiceCall.CallType` (`string`, `"audio"`|`"video"`, default `"audio"`) — property + `Create(..., string callType = "audio")`.
- `StartCallRequest` thêm `string? CallType` — hub/`CallService` whitelist: `null|""|"audio"`→audio; `"video"`→video; khác→HubException "Invalid callType".
- `IncomingCallDto` thêm `CallType` — callee UI biết trước khi accept.
- `VoiceCall.Create` call sites ×4 trong `CallService.StartCallAsync` truyền `req.CallType`.
- `VoiceDbContext`: `call.Property(e => e.CallType).HasMaxLength(8)`.
- `VoiceSchema`: idempotent `ALTER TABLE voice_calls ADD COLUMN CallType TEXT NULL` — guard bằng `PRAGMA table_info` (SQLite không có ADD COLUMN IF NOT EXISTS); default backfill `'audio'` khi NULL ở read path hoặc `UPDATE ... SET CallType='audio' WHERE CallType IS NULL` 1 lần sau ALTER.
- Metrics: `VoiceMetricsService` thêm `VideoCalls` count (`CallType == "video"`).

### 4.2 Metering theo CallType

- `MeteringGateAllowsAsync(tenantId, callerUserId, callType, now, ct)`: check `"{callType}.call"` + `"{callType}.minute"`.
- `IngestUsage`: event type = `"{callType}.call"` / `"{callType}.minute"`; MetadataJson thêm `"callType"`.
- **PRE-CHECK (Phase A0):** đọc rate-cards metering hiện có — nếu `video.*` chưa seed, quyết định: (a) seed rate card `video.*` = `voice.*` × hệ số (cần user/metering owner), hoặc (b) fallback keys `voice.*` + metadata callType. Ghi kết quả vào task card trước khi code CallService.

### 4.3 ConnectionType thật

- `voice-media.js`: `getConnectionType()` — `peer.getStats()`, tìm `candidate-pair` state `succeeded` → local/remote `candidateType`; `relay` ở bất kỳ đầu nào → `"TURN"`, ngược lại `"P2P"`. Trả `"UNKNOWN"` khi chưa connected.
- `voice-sdk.js`: sau media `connected` → `NotifyConnected` invoke kèm `{ callId, connectionType }` (signature mới).
- `VoiceHub.NotifyConnected(Guid callId, string? connectionType)` — backward compatible (param optional).
- `CallService.NotifyConnectedAsync(user, callId, connectionType)`: whitelist `"P2P"|"TURN"`, fallback `"P2P"`; thay hardcode.
- ICE-restart path (`onConnectionStateChange` reconnect) cũng nên update — `NotifyReconnected` giữ nguyên, ConnectionType set-once (`??=`) đã đúng.

### 4.4 STUN

- `TurnCredentials` record thêm `IReadOnlyList<string> StunUrls` (init property, không phá positional ctor — verify khi code).
- `TurnCredentialService.Issue`: `StunUrls = config["TURN:StunUrls"]` split `,` ; default `[$"stun:{host}:3478"]`. Endpoint `/v1/turn/credentials` trả thêm field `stunUrls`.
- SDK `resolveIceServers()`: trả mảng `[ {urls: stunUrls}, {urls: turnUrls, username, credential} ]`; khi TURN not configured → `[{urls: ['stun:stun.l.google.com:19302','stun:stun1.l.google.com:19302']}]`.
- Coturn: STUN đã native trên port 3478 (`--listening-port` mặc định) — verify trên VPS, KHÔNG cần config mới trừ khi disabled.

### 4.5 Video client (`UI.Platform/wwwroot/js`)

`voice-media.js`:
- `requestMedia(video)` thay/đổi `requestMic()`: `getUserMedia({audio:{...}, video: video?{width:{ideal:640},height:{ideal:480},frameRate:{ideal:24}}:false})`; giữ `requestMic()` = `requestMedia(false)` cho backward compat.
- `setCameraEnabled(on)`: nếu `on` và chưa có video track → `getUserMedia({video})` add track vào peer (negotiationneeded → re-offer); nếu `off` → `track.stop()` + `replaceTrack(null)` hoặc removeTrack+re-offer. Chọn **removeTrack + re-offer** (đơn giản, giải phóng camera đèn).
- `getLocalStream()`, `getConnectionType()` exports.

`voice-sdk.js`:
- State thêm `callType` (`'audio'|'video'`), `cameraOn`.
- `startCall(targetUserId, context, callType='audio')` → gửi `callType` trong StartCallRequest; `prepareMedia(video = callType==='video')`.
- `onIncoming(d)` đọc `d.callType` → `acceptCall()` gọi `prepareMedia(video)` tương ứng. `acceptCall` signature giữ nguyên (đọc state), hoặc `acceptCall(callType)` — chọn đọc từ incoming dto (server authoritative hơn).
- Camera toggle: `toggleCamera()` → `media.setCameraEnabled(!cameraOn)` → sau khi tracks đổi, `createOffer(false)` + `SendOffer` (reuse renegotiation path — glare: nếu `signalingState !== 'stable'` khi nhận offer → polite rollback `setRemoteDescription({type:'rollback'})` rồi apply; caller=impolite).
- Remote video: tạo `<video id="vanan-voice-remote-video" autoplay playsInline playsinline>` append vào `#voice-remote-video-host` (nếu host div chưa có → fallback ẩn như audio element). Reuse gesture-unlock pattern cho `video.play()`.
- Local PiP: `<video id="vanan-voice-local-video" muted playsInline>` vào `#voice-local-video-host`, `srcObject = localStream`, `mirror` CSS.
- Callback mới `onVideoChange(localOn, remoteOn)` → bridge `OnVoiceVideoChange`.
- `onTrack`: remote stream có thể chứa audio+video — `<audio>` element vẫn nhận cả stream (chỉ phát audio tracks); `<video>` nhận video tracks. OK.

`voice-chat.js`:
- `startCall(targetUserId, subjectType, subjectId, peerId, callType)` — truyền `callType` vào context hoặc arg riêng → `voice.startCall(..., {contextType:'chat', contextReferenceId, callType})`.
- `toggleCamera()`, expose `getCallType()`.
- `initialize` options thêm `onVideoChange` → `OnVoiceVideoChange(localOn, remoteOn)` JSInvokable.

### 4.6 Blazor `VoiceCallPanel` + `VoicePresencePanel` (reconcile 22/11)

> **Commit mới `fbc9a694`/`8a08d38e`:** Voice Presence Phase C đã ship — global call UI (`VoicePresencePanel` incoming overlay + mini callbar, `VoiceCallCoordinator` bus, `VoicePresenceHost` badge). Video call được accept **ngoài chat** (chat widget đóng) phải có video surface ở global UI nữa — không chỉ `VoiceCallPanel`.

- `.razor.cs`: `_callType` (từ state info JSON — `callType` field trong `onStateChange`/`OnVoiceIncoming` info → coordinator `CallInfoJson` pass-through, không cần plumbing mới), `_cameraOn`, `_remoteVideoOn`, `_localVideoOn`; `OnVoiceVideoChange` JSInvokable; `ToggleCameraAsync()`.
- `.razor`: khi `_active && _callType==="video"` → render `<div id="voice-remote-video-host">` + `<div id="voice-local-video-host">` (class CSS mới); nút camera cạnh mute (`bi-camera-video`/`bi-camera-video-off`); incoming video call → accept button label/icon đổi (vd "Nghe & xem").
- `.razor.css`: video tile layout — remote 100% area card, local PiP góc phải-dưới ~120px, `transform: scaleX(-1)` mirror.
- **`VoicePresencePanel`** (global): incoming overlay + mini callbar thêm video hosts + camera toggle — cùng JS singleton nên chỉ cần render host divs + button; state đọc từ `Coordinator.CallInfoJson`/`CallState`.
- `VoiceCallCoordinator`: verify `CallInfoJson` đã chứa `callType` (nếu SDK nhét vào info) — nếu chưa, thêm parse/forward; `IsCallActive` đã có sẵn.
- Header call button (RealtimeChatPanel/S3) — **A0.4 verify**: nếu nút gọi ngoài panel tồn tại → thêm video-call entry (`StartCallAsync(callType:"video")`).

### 4.7 Demo SDK parity — **ĐÃ DRIFT, sync BẮT BUỘC**

- `10_Voice/vanan-voice/sdk/js/voice-sdk.js` hiện THIẾU 2 fix mới (`AbortSignal.timeout(5000)` TURN fetch, `acceptCall` console.warn) — bản demo đã stale so với `UI.Platform/wwwroot/js`.
- Quy tắc 1 chiều: **UI.Platform `wwwroot/js` là source of truth**; Phase D copy `voice-media.js` + `voice-sdk.js` đã sửa sang `sdk/js/` (demo không có `voice-chat.js` — giữ nguyên cấu trúc).

## 5. ROADMAP ↔ PHASES

| Phase | Nội dung | Gate |
|---|---|---|
| **A0** | Verify rate-cards metering (`video.*` tồn tại?) + đọc `Program.cs` endpoint turn + check `VoiceCallCoordinator` | Quyết định metering keys |
| **A** | Voice service: contracts → domain → schema ALTER → CallService → TurnCredentialService → metrics → **tests trước (TDD)** → `dotnet test` + build | test pass |
| **B** | Client JS: voice-media → voice-sdk → voice-chat | build UI.Platform |
| **C** | VoiceCallPanel razor/cs/css + check header call button + coordinator route | build + guard-check |
| **D** | Sync demo sdk/js, update task card status, báo cáo | — |

## 6. RỦI RO

| # | Rủi ro | Mitigation |
|---|---|---|
| R1 | `video.*` rate card chưa seed → metering deny-all video calls | A0 verify trước; fallback `voice.*` keys + metadata |
| R2 | ALTER TABLE trên prod `voice.db` (container volume `/opt/vanan-voice/data`) | Idempotent guard `PRAGMA table_info`; test trên copy DB; deploy = restart container |
| R3 | Glare khi 2 bên cùng toggle camera | Polite/impolite rule; worst case renegotiation fail → end-call retry |
| R4 | Video autoplay policy (như bug audio 2026-11-13) | Reuse gesture-unlock cho `<video>` |
| R5 | TURN `--max-bps=1048576` bóp video khi relay | Documented — phase sau: nâng cap hoặc per-callType; P2P-first giảm thiểu |
| R6 | Bandwidth TURN khi scale | Đã phân tích: voice VPS chết đầu tiên; STUN fix giảm TURN ratio; multi-TURN node = phase rollout |
| R7 | `turns:` TLS chưa verify coturn listen 5349 | Check `TURN__TlsPort` env + coturn config trên VPS trước deploy |
| R8 | WASM cold-start: video element trong hidden tab | playsInline + autoplay muted patterns đã proven |

## 7. KHÔNG LÀM (explicit out-of-scope)

- Group video call / SFU / LiveKit — mesh 1:1 only.
- Guest video call — vẫn yêu cầu login.
- Recording, screen share, virtual background.
- Metering rate card pricing change (chỉ tách keys; giá do metering owner quyết).
- Đổi `Voice_Call_Enabled` flag semantics — video dùng chung flag (có thể thêm `Video_Call_Enabled` sau nếu cần gate riêng — đề xuất defer).
- Multi-TURN geo nodes, Redis state store — phase scale sau.

## 8. TASK CARDS

- `docs/AI/tasks/voice_video/task_v1_video_mvp.md` — task card chi tiết với checklist + acceptance criteria.
