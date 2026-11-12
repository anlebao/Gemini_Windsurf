# Voice Call Button — Investigation & Findings (2026-11-12)

**Status:** REVIEW — chờ duyệt solution trước khi implement/commit.
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

### S3 — UX: chỉ enable nút khi sẵn sàng kết nối (yêu cầu mới)
Hiện `_canCall = Token != null && UserId != Empty && state == "idle"` — **không hề biết hub đã connect chưa**. Đề xuất:
- `voice-sdk.js`: expose trạng thái connection (`getConnectionState()` → 'connecting'|'connected'|'disconnected'|'reconnecting') + callback `onConnectionStateChange` (SignalR `onreconnecting/onreconnected/onclose`).
- `voice-chat.js` + `VoiceCallPanel`: thêm state `connecting` — nút hiển thị "Đang kết nối..." disabled cho tới khi hub `Connected`; nếu connect fail → hiện lỗi rõ ("Không kết nối được máy chủ cuộc gọi") thay vì nút sáng nhưng bấm lỗi.
- `VoiceCallPanel`: không để `_error` init bị ghi đè — ưu tiên hiển thị lỗi kết nối.

### S4 — Vị trí nút gọi nổi bật (yêu cầu mới)
Đề xuất 2 vị trí (chọn 1 hoặc cả 2):
- **A. Chat header** — icon điện thoại nhỏ ngay cạnh tiêu đề conversation trong `RealtimeChatPanel` (top-right). Nhìn thấy ngay khi mở chat, không phải scroll xuống cuối.
- **B. Floating action button** — nút tròn nổi góc chat panel (chỉ khi `VoiceCallEnabled` + idle). Phù hợp mobile.
- Giữ nguyên panel dưới cùng cho incoming/active states (overlay đã có).
- Lưu ý UI Platform rule: dùng `VanAnButton`/tokens, không custom HTML/CSS bypass.

### S5 — Policy guest call (quyết định cần user chốt)
- Option 1 (khuyến nghị pilot): guest không gọi được — nút ẩn/disabled + tooltip "Đăng nhập để gọi thoại". Đơn giản, an toàn.
- Option 2: voice service hỗ trợ guest identity qua `X-Customer-Device-Id` → cần validator mới + server mapping device→customer — scope lớn hơn, để phase sau.

### S6 — CORS mở rộng (khi rollout P6)
Thêm origin cho mọi tenant domain pilot vào `Voice__CorsOrigins` (hiện hardcode 2 domain trong workflow — cân nhắc chuyển sang biến/secrets khi số tenant tăng).

---

## 4. Việc cần làm tiếp theo (đề xuất thứ tự)

1. Review + duyệt doc này → quyết định S3/S4/S5.
2. Chạy lại `dotnet test` voice suite (đang dở) + build voice solution.
3. Commit S1+S2 (+S3 nếu duyệt) → fast push → CD deploy voice + webapps.
4. RV: buyer login → mở chat An Tâm → nút hiện → bấm gọi → kỳ vọng `Calling` state → callee thấy incoming → accept → CONNECTED.
5. Verify thêm `/v1/turn/credentials` trả 200 cho customer token (trước fix chắc chắn 401).
