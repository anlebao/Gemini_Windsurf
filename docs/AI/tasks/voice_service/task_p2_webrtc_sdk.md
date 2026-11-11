# Task P2 — WEBRTC P2P SDK: Voice Service (`vanan-voice`)

> **Status:** DONE — 11/11/2026 · **node unit tests 12/12 pass** · dotnet 65/65 pass · E2E spec sẵn sàng (environment chặn WebRTC ICE — phân loại Infrastructure, chạy trên máy có UDP/loopback bình thường)
> **Created:** 2026-11-11 · **Phase:** P2 — theo `docs/AI/plans/voice-service-master-plan.md` (chạy SONG SONG với P3)
> **Tài liệu gốc:** SRS v1.1 §9 (client SDK), §4 FR-004/009/012

---

## KẾT QUẢ (11/11/2026)

- **SDK**: `10_Voice/vanan-voice/sdk/` — 3 file tĩnh attachable by contract:
  - `js/vendor/signalr.min.js` — @microsoft/signalr 8.0.7 vendored (không CDN).
  - `js/voice-media.js` — `window.vananVoiceMedia`: mic (echoCancellation/noiseSuppression/autoGainControl), mono Opus cap 128 kbps, RTCPeerConnection, ICE restart, `debug()` hook.
  - `js/voice-sdk.js` — `window.vananVoice`: state machine client (14 state, provisional đến khi server confirm), callId **UUIDv7**, mic CHỈ sau user action (startCall/acceptCall), ICE restart ≤2, multi-device cleanup (first-accept-wins), timeout từ server (`timeoutConfig`), callbacks onStateChange/onIncoming/onLocalStream/onRemoteStream/onError. Hỗ trợ inject `connection`/`media` (test/transport tùy chỉnh).
- **Demo**: `sdk/demo/` (index.html + app.js 2 tab caller/callee + mint-token.mjs HS256) + `sdk/README.md` hướng dẫn tích hợp.
- **Node unit tests 12/12** (`6_Testing/unit-tests/voice-sdk.test.mjs` — fake connection + fake media): UUIDv7, StartCall context, caller/callee full flow, mic-only-on-action, multi-device cleanup, ring timeout, offline, mute, **ICE restart bounded (2)**, EndCall, reject-start-while-active, dispose.
- **E2E spec** (`6_Testing/e2e-tests/voice-p2p.spec.ts` + `playwright-voice-p2p.config.ts` + `scripts/voice-demo-server.mjs`): 2 tab P2P thật (fake media device), tự chạy voice API (dotnet run webServer) + demo static server, JWT minted in-spec, bật open-calling qua admin endpoint. **1/2 test PASS (mic-not-requested); test P2P bị chặn bởi môi trường** — xem dưới.

## Bug thật E2E bắt được (đã fix)

| # | Bug | Fix |
|---|---|---|
| E2E-1 | Client không bao giờ gửi `NotifyConnected` khi media connected → server kẹt NEGOTIATING mãi | `onConnectionStateChange`: Negotiating + media 'connected' → invoke NotifyConnected (server xác nhận CallConnected) |
| E2E-2 | SDK gửi `RTCIceCandidate` object nhưng server DTO `IceCandidateDto.Candidate` là **string** → deserialize fail → ICE không relay | `relayIceCandidate()`: gửi `candidate.candidate` (string), bỏ end-of-candidates |
| E2E-3 | Voice API thiếu CORS → demo cross-origin (5080→5070) bị chặn | Thêm `Voice:CorsOrigins` (comma-separated) + policy AllowCredentials |
| E2E-4 | E2E quên bật open-calling (default OFF đúng SRS) | Spec bật qua admin endpoint PUT `/v1/tenants/{t}/voice-config` (trước khi test) |

## Chặn môi trường (phân loại Infrastructure — không phải bug code)

- Sandbox hiện tại **chặn UDP/loopback cho browser process**: kể cả loopback RTCPeerConnection thuần trong 1 page (không qua SDK) cũng kẹt `connectionState='new'` sau setLocalDescription → ICE agent không bao giờ chạy. Đã thử: headless/headed, `--allow-loopback-in-peer-connection`, `--disable-features=WebRtcHideLocalIpsWithMdns` — không đổi.
- **Spec đã sẵn sàng** — chạy trên máy có mạng bình thường: `npx playwright test --config=playwright-voice-p2p.config.ts`.
- Ledger ghi nhận: `6_Testing/reports/playwright-ledger.md` (2026-11-11).

## Acceptance (SRS §9)

1. ✅ Node unit tests xác minh: mic không request trước user action; callId UUIDv7; state machine; timeout; ICE restart ≤2; multi-device.
2. 🔶 2 browser P2P thật + audio: spec sẵn sàng, chờ môi trường WebRTC ICE hoạt động.
3. ✅ SDK không phụ thuộc host app (3 file tĩnh + inject options; không import internals host).
4. ✅ Demo 2 tab + README tích hợp.

## Output

- SDK + demo + 12/12 node tests + E2E spec/config/helper + ledger.
- Sẵn sàng P4 (tích hợp chat-first: `RealtimeChatPanel.razor` thêm nút call → `window.vananVoice` qua JS interop).