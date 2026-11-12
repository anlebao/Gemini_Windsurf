# Voice Call Button — Investigation & Findings (2026-11-12)

**Status:** ✅ APPROVED + IMPLEMENTED (2026-11-12) — user duyệt toàn bộ khuyến nghị: S3 + S4-A (chat header icon) + S5 Option 1 (guest disabled + tooltip). Đã implement: `voice-sdk.js` (hubState + `onConnectionStateChange` + `getConnectionState`, guard `startCall` khi chưa Connected — sync cả 2 bản `10_Voice/sdk` + `UI.Platform/wwwroot`) · `voice-chat.js` bridge `OnVoiceConnectionState` · `VoiceCallPanel` (`_hubState`, nút "Đang kết nối..." disabled tới khi Connected, không ghi đè init error, `CallButtonTitle` tooltip) · `RealtimeChatPanel` header icon `voice-call-header-btn` (mirror hub/call state qua `HubStateChanged`/`CallStateChanged` callbacks + `@ref`). Verify: voice tests 85/85 · node 18/18 · VanAn.sln build 0 err · guard-check PASS. **Còn lại:** commit + push + CD deploy + RV call thật 2 phía (bước 4 §4).
**Phạm vi:** nút "Gọi thoại" trong chat (KhachLink buyer + ShopERP owner inbox).
**Tenant test:** An Tâm (`e2d91cdf-d3c4-4399-b96e-e772a7a41630`), storefront `diemthuong2.khachvip.online`.

---

## 1. Lỗi quan sát được

Sau deploy `e35cd45c` (fix render nút), nút "Gọi thoại" đã HIỆN trên cả 2 phía.
Nhưng bấm gọi phía **buyer** báo:

```
Không gọi được: Cannot send data if the connection is not in the 'Connected' State.
  at K.send (signalr.min.js) ... at q.invoke ...
  at Object.startCall (voice-sdk.js:298)
  at Object.startCall (voice-chat.js:37)
```

Nghĩa là: `connection.invoke('StartCall')` chạy khi SignalR hub connection **chưa/chẳng bao giờ Connected**.

---

## 2. Root cause (đã trace code + config prod)

### RC-A — ĐÃ FIX (`e35cd45c`, đã deploy)

| # | Bug | Fix |
|---|-----|-----|
| A1 | `RealtimeChatPanel` render `<VoiceCallPanel>` nhưng không truyền `Enabled=` → default `false` → `@if(Enabled)` giấu toàn bộ UI → **không nút ở cả 2 phía** | `Enabled="true"` |
| A2 | `ShopFeatureSettingsHttpService.IsEnabledAsync` thiếu case `Voice_Call_Enabled` → buyer flag luôn false | thêm case |
| A3 | Buyer lookup flag dùng tenant `000...001` (Vạn An Cafe) thay vì tenant của shop | `ChatPanel.TenantId` param; OrderTracking/DeliveryTracking truyền tenant thật; Store.razor dùng `_features.Voice_Call_Enabled` |

### RC-B — MỚI (lỗi hiện tại): voice hub connection không bao giờ Connected phía customer

`voice-sdk.js initialize()` gọi `await connection.start()` tới `https://voice.khachvip.online/hubs/voice`.
Hub `VoiceHub.OnConnectedAsync` resolve identity qua chuỗi validator; identity = null → `HubException` → `start()` fail → connection ở trạng thái Disconnected → mọi `invoke` sau đó ném đúng error trên.

Customer path gãy ở **2 lớp độc lập** (cả 2 đều phải fix):

**B1 — `Voice:ShopErpBaseUrl` chưa được deploy.**
`CustomerTokenValidator` return null ngay nếu config rỗng. `.env.voice` sinh trong `cd-multivps.yml` (job deploy-voice) **không có** `Voice__ShopErpBaseUrl` → customer token validator bị vô hiệu hoàn toàn trên production.

**B2 — Token transport mismatch.**
- SDK gửi token qua SignalR `accessTokenFactory` → query param **`access_token`**.
- `CustomerTokenValidator` chỉ đọc **`customerToken`** query / **`X-Customer-Token`** header.
- `StaffJwtValidator` đọc `access_token` + `Authorization: Bearer` → staff JWT OK.
- Customer token không phải JWT → StaffJwtValidator fail → rơi xuống CustomerTokenValidator → không thấy credential → null.
- Cùng mismatch ảnh hưởng `GET /v1/turn/credentials` và `/v1/missed-calls/{userId}` — cả 2 cũng resolve identity qua cùng chain và client fetch dùng `Authorization: Bearer <customerToken>`.

**Hệ quả UX phụ:** `VoiceCallPanel.OnInitializedAsync` catch lỗi init → set `_error`, nhưng `_error` sau đó bị `StartCallAsync` ghi đè bằng "Không gọi được: ..." → user không thấy root error ("Unauthorized" / connect fail) mà chỉ thấy lỗi invoke muộn.

### RC-C — Rủi ro còn lại (chưa verify)

- **Guest (chưa login):** `CustomerToken` rỗng → `_canCall=false` → nút disabled (by design hiện tại). Cần quyết định policy: bắt đăng nhập để gọi, hay hỗ trợ guest qua device-id (server chưa có identity channel cho guest).
- **CORS:** `Voice__CorsOrigins` chỉ có `diemthuong2` + `app2`. Mọi tenant domain khác (vd storefront An Tâm nếu khác domain) sẽ bị browser chặn ngay từ negotiate.
- **Multi-VPS customer validation:** `Voice:ShopErpBaseUrl` đề xuất `https://api2.khachvip.online` (Gateway `/api/customer-identity/me` → forward ShopERP). Nếu customer thuộc ShopInstance khác gateway này trỏ tới, validation có thể fail — cần verify với tenant pilot.
- **Staff side chưa test lại** sau `Enabled` fix — về lý thuyết StaffToken (JWT) đã đi đúng `access_token`.

---

## 3. Proposed solutions

### S1 — Server: CustomerTokenValidator chấp nhận fallback channels (B2)
**Đã viết sẵn trong working tree (CHƯA commit):**
- `10_Voice/.../Auth/CustomerTokenValidator.cs`: `ReadAnyCredential` thử lần lượt `customerToken` query → `X-Customer-Token` header → `access_token` query → `Authorization: Bearer`. An toàn vì StaffJwtValidator chạy trước — staff JWT hợp lệ không bao giờ tới đây; staff JWT hết hạn forward sang ShopERP `/me` → 401 → null.
- `tests/.../CustomerTokenValidatorTests.cs`: 6 tests (access_token query, Bearer, customerToken query, missing config, no credential, ShopERP 401). Test run bị interrupt — cần chạy lại.

### S2 — Deploy: thêm `Voice__ShopErpBaseUrl` (B1)
**Đã sửa sẵn trong working tree (CHƯA commit):**
- `.github/workflows/cd-multivps.yml` job deploy-voice: `"Voice__ShopErpBaseUrl=https://api2.khachvip.online"`.

### S3 — UX: chỉ enable nút khi sẵn sàng kết nối (yêu cầu mới) — ✅ DONE
- `voice-sdk.js`: `hubState` ('disconnected'|'connecting'|'connected'|'reconnecting') + `onConnectionStateChange` callback (SignalR `onreconnecting/onreconnected/onclose`) + `getConnectionState()`; `startCall` guard `hubState !== 'connected'` → throw "Mất kết nối máy chủ cuộc gọi."
- `voice-chat.js`: forward `OnVoiceConnectionState` → .NET + export `getConnectionState`.
- `VoiceCallPanel`: `_hubState` + `_canCall` yêu cầu `connected`; nút hiển thị "Đang kết nối..." disabled; disconnect giữa session → "Mất kết nối máy chủ cuộc gọi — tải lại trang để thử lại." (`_error ??=` giữ root-cause init error); `StartCallAsync` public + guard sớm không ghi đè `_error`.

### S4 — Vị trí nút gọi nổi bật (yêu cầu mới) — ✅ DONE (chọn A: chat header)
- Icon điện thoại nhỏ `voice-call-header-btn` trong header `RealtimeChatPanel` (top-right, cạnh nút refresh) — hiện cả KhachLink (Store/ChatPanel shim/OrderTracking/DeliveryTracking) lẫn ShopERP ShopInbox vì cùng 1 component.
- Disabled tới khi `hubState==connected` + `callState==idle` + có identity; tooltip phân biệt: "Đăng nhập để gọi thoại" / "Đang kết nối máy chủ cuộc gọi..." / "Không kết nối được máy chủ cuộc gọi" / "Đang trong cuộc gọi".
- Click → `_voicePanel.StartCallAsync()` qua `@ref` (single SDK instance); state sync qua `HubStateChanged`/`CallStateChanged` EventCallback.
- Panel dưới cùng giữ nguyên cho incoming/active states. Dùng `VanAnButton` + Bootstrap icons — không custom HTML/CSS bypass.

### S5 — Policy guest call — ✅ DONE (Option 1)
Guest (chỉ `CustomerDeviceId`, không token) → nút disabled + `title="Đăng nhập để gọi thoại"` ở cả header lẫn panel. Guest identity channel (`X-Customer-Device-Id`) để phase sau nếu cần.

### S6 — CORS mở rộng (khi rollout P6)
Thêm origin cho mọi tenant domain pilot vào `Voice__CorsOrigins` (hiện hardcode 2 domain trong workflow — cân nhắc chuyển sang biến/secrets khi số tenant tăng).

---

## 4. Việc cần làm tiếp theo (đề xuất thứ tự)

1. Review + duyệt doc này → quyết định S3/S4/S5.
2. Chạy lại `dotnet test` voice suite (đang dở) + build voice solution.
3. Commit S1+S2 (+S3 nếu duyệt) → fast push → CD deploy voice + webapps.
4. RV: buyer login → mở chat An Tâm → nút hiện → bấm gọi → kỳ vọng `Calling` state → callee thấy incoming → accept → CONNECTED.
5. Verify thêm `/v1/turn/credentials` trả 200 cho customer token (trước fix chắc chắn 401).
