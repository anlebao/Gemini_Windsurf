# VẠN AN VOICE — Reusable Client SDK (P2)

SDK gọi thoại P2P dùng chung cho mọi host app (KhachLink, ShopERP, TimLaThay, Booking...).
Attachable by contract: 3 file tĩnh + 1 lời gọi `initialize` — host không đụng internals.

## Thành phần

| File | Vai trò |
|---|---|
| `js/vendor/signalr.min.js` | `@microsoft/signalr` 8.x browser bundle (vendored — không phụ thuộc CDN) |
| `js/voice-media.js` | WebRTC wrapper: mic (echoCancellation/noiseSuppression/autoGainControl), mono Opus cap 128 kbps, RTCPeerConnection, ICE restart |
| `js/voice-sdk.js` | `window.vananVoice` — orchestrator: state machine client, SignalR `/hubs/voice`, callId UUIDv7, ICE restart bounded (≤2) |
| `demo/` | Demo 2 tab (caller/callee) — chạy local để kiểm thử P2P thật |

## Tích hợp (host app)

```html
<script src=".../js/vendor/signalr.min.js"></script>
<script src=".../js/voice-media.js"></script>
<script src=".../js/voice-sdk.js"></script>
```

```js
const voice = window.vananVoice;
await voice.initialize({
    hubUrl: 'https://voice.khachvip.online/hubs/voice',
    accessTokenProvider: () => staffJwtOrCustomerToken,   // token Vạn An (SRS O2)
    turnCredentialsUrl: 'https://voice.khachvip.online/v1/turn/credentials', // P3: TURN REST, fail-open → STUN-only khi lỗi
    // iceServers: [{ urls: 'stun:...' }],   // fallback khi không set turnCredentialsUrl
    onStateChange: (state, info) => { /* cập nhật UI */ },
    onIncoming: (call) => { /* hiện nút Nghe/Từ chối */ },
    onLocalStream: (stream) => { localAudio.srcObject = stream; },
    onRemoteStream: (stream) => { remoteAudio.srcObject = stream; },
    onError: (msg) => { /* thông báo tiếng Việt */ }
});

await voice.startCall(targetUserId, { contextType: 'chat', contextReferenceId: conversationId });
await voice.acceptCall();   // callee — chỉ gọi sau user action
voice.mute(); voice.unmute();
await voice.endCall();
voice.getCallState();       // { state, callId, muted, iceRestartCount, isCaller }
```

States: `idle → calling → ringing → accepted → negotiating → connected ↔ reconnecting → ended|rejected|cancelled|missed|busy|offline|collision|failed` (mirror server SRS §5; client provisional đến khi server xác nhận).

## Demo local (P2P thật giữa 2 tab)

1. Chạy voice service với `Jwt__Secret` (≥32 ký tự):
   ```
   cd 10_Voice/vanan-voice/src/VanAn.Voice.Api
   Jwt__Secret="<secret≥32>" Voice__DbPath=data/demo.db dotnet run
   ```
2. Mint 2 token (HS256, claims `sub` + `tenant_id` — cùng secret):
   ```
   node sdk/demo/mint-token.mjs <userIdA> <tenantId> <secret>   # tab caller
   node sdk/demo/mint-token.mjs <userIdB> <tenantId> <secret>   # tab callee
   ```
3. Mở 2 tab:
   - Tab 1: `sdk/demo/index.html?role=caller&token=<jwtA>&target=<userIdB>`
   - Tab 2: `sdk/demo/index.html?role=callee&token=<jwtB>`
   - Tab 1 bấm **Gọi** → Tab 2 bấm **Nghe** → audio 2 chiều (máy tính cần mic).

## Kiểm thử

- Node unit tests (state machine client, callId UUIDv7, mic chỉ sau user action, ICE restart bounded, multi-device cleanup): `node --test 6_Testing/unit-tests/voice-sdk.test.mjs` — 12/12.
- E2E WebRTC thật (2 browser, fake media): `6_Testing/e2e-tests/voice-p2p.spec.ts` — chạy ở validation phase (Playwright chỉ chạy sau khi implement xong).