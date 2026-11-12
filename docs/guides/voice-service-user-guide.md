# HƯỚNG DẪN SỬ DỤNG — DỊCH VỤ GỌI THOẠI (VANAN VOICE)

> **Phiên bản:** 1.0 — 2026-11-12
> **Phạm vi:** SystemAdmin · Shop Owner (ShopERP) · Buyer/Khách hàng (KhachLink PWA)
> **Kiến trúc:** voice = module độc lập (`vanan-voice`, voice.khachvip.online) — gọi thoại **ngữ cảnh chat** (chat-first), signaling qua SignalR, media P2P + TURN fallback, phí qua vanan-metering.

---

## MỤC LỤC

1. [Tổng quan nhanh](#1-tổng-quan-nhanh)
2. [Dành cho SystemAdmin](#2-dành-cho-systemadmin)
3. [Dành cho Shop Owner](#3-dành-cho-shop-owner)
4. [Dành cho Buyer / Khách hàng](#4-dành-cho-buyer--khách-hàng)
5. [Bảng lỗi & cách xử lý](#5-bảng-lỗi--cách-xử-lý)
6. [Câu hỏi thường gặp (FAQ)](#6-câu-hỏi-thường-gặp-faq)

---

## 1. Tổng quan nhanh

- Gọi thoại **chỉ xuất hiện trong khung chat** với một người đối thoại cụ thể (khách ↔ shop). Không gọi tự do trừ khi SystemAdmin bật "Gọi tự do" cho tenant đó.
- Cuộc gọi **không bao giờ bị cắt giữa chừng** vì hết phí — phí trừ **sau khi kết thúc** (MeterOnly ở giai đoạn pilot).
- Kết nối ưu tiên **P2P** (trực tiếp máy-người-dùng); nếu mạng chặn P2P (NAT đối xứng/4G chặt) hệ thống tự chuyển **TURN relay**.
- Trên di động: trình duyệt cần quyền **micro**; iOS Safari hỗ trợ nhưng nên dùng bản PWA đã cài.

| Đối tượng | Vào đâu | Nút gọi nằm ở |
|---|---|---|
| Buyer | KhachLink → Chat với shop | Trong khung chat |
| Shop Owner | ShopERP → Hộp thư cộng đồng (ShopInbox) | Trong khung chat |
| SystemAdmin | ShopERP → `/admin/voice` + `/admin/metering/rate-cards` | — |

---

## 2. Dành cho SystemAdmin

### 2.1. Bật/tắt voice cho từng tenant — `/admin/voice`

**Đường vào:** ShopERP (app2) → đăng nhập SystemAdmin → menu **Voice** (`/admin/voice`).

Mỗi tenant (Active) có 2 công tắc:

| Công tắc | Ý nghĩa | Mặc định |
|---|---|---|
| **VoiceFeatureEnabled** | Kill-switch tổng. TẮT → mọi cuộc gọi của tenant bị chặn ngay lập tức (`FEATURE_DISABLED`), kể cả đang gọi dở. | Bật |
| **VoiceOpenCalling** | Cho phép gọi **không cần ngữ cảnh chat đã xác minh** (gọi tự do theo `TargetUserId`). Tắt → chỉ gọi được từ trong hội thoại chat. | Tắt |

**Cách dùng:**
1. Tìm tenant trong bảng (có ô tìm kiếm/lọc theo tên).
2. Gạt 2 toggle → bấm **Lưu**.
3. Toast xác nhận: *"Đã lưu cấu hình voice cho '<tên>' (gọi thoại: bật/tắt, gọi tự do: bật/tắt)"*.
4. Cấu hình **có hiệu lực sau tối đa ~30 giây** (voice service cache 30s) — không cần restart.

> ⚠️ Tenant **chưa từng cấu hình** = mặc định (voice BẬT, gọi tự do TẮT). Dòng hiển thị nhãn "Mặc định".

### 2.2. Thiết lập mức phí — `/admin/metering/rate-cards`

Voice tính phí qua 2 rate card (giống mọi dịch vụ metering):

| Rate card | Đơn vị | Ghi chú |
|---|---|---|
| `voice.call` | 1 lần gọi | Hiện pilot = **0đ** |
| `voice.minute` | phút (làm tròn lên) | Hiện pilot = **1.000đ/phút**, `Enforcement=MeterOnly` |

**Cách chỉnh phí:** vào `/admin/metering/rate-cards` → tạo/sửa rate card `voice.call` / `voice.minute` → chọn `Enforcement`:
- `MeterOnly`: chỉ ghi nhận usage, **không bao giờ chặn cuộc gọi** (dùng cho pilot).
- `Enforce`: tenant hết ví → cuộc gọi mới bị `METERING_DENIED` (call đang chạy vẫn không bị cắt — SRS §14).

### 2.3. Giám sát

- `GET https://voice.khachvip.online/v1/metrics/summary` — tổng call, tỉ lệ connected/missed/failed, `turnRelayed` + `turnPercentage`, `meteringDenied`, `featureDisabled`, `avgDurationSeconds`. Dùng số liệu này cho báo cáo pilot & quyết định Gate 1 (P2P ≥85%).
- Tenant voice kill-switch test nhanh: tắt `VoiceFeatureEnabled` của 1 tenant → gọi thử phải nhận "Tính năng gọi thoại đang tạm ngừng" → bật lại.

---

## 3. Dành cho Shop Owner

### 3.1. Bật gọi thoại cho cửa hàng (bắt buộc, làm 1 lần)

Nút "Gọi thoại" **không hiện mặc định** — chủ shop phải tự bật trong cài đặt:

1. ShopERP → **Cài đặt → Tính năng cửa hàng** (`/settings/shop-features`).
2. Bật toggle **"Bật gọi thoại trong chat (P4)**" → **Lưu**.
3. F5 lại trang — nút 📞 "Gọi thoại" xuất hiện trong mọi khung chat, **và khách cũng thấy nút** (cờ này là public config, KhachLink đọc cùng giá trị).

> Ngoài ra SystemAdmin phải đảm bảo `VoiceFeatureEnabled` = Bật ở `/admin/voice` (kill-switch nền tảng). Hai lớp độc lập: tắt cái nào cũng mất nút/chặn cuộc gọi.

### 3.2. Gọi cho khách / nghe máy

1. Mở **ShopERP** → **Hộp thư cộng đồng (ShopInbox)** → chọn hội thoại với khách.
2. Trong khung chat, góc trên có nút **📞 "Gọi thoại"** (chỉ hiện khi tính năng bật cho tenant).
3. Bấm → trình duyệt xin quyền **micro** → **Allow**. Trạng thái chuyển *"Đang gọi..."*.
4. Khách nghe máy → 2 bên vào trạng thái **"Đang gọi"**: có nút **Tắt mic/Bật mic** và **Kết thúc**.

**Khi khách gọi đến:** khung chat hiện **"Cuộc gọi đến..."** với 2 nút **Nghe** (xanh) / **Từ chối** (đỏ). Chuông reo tối đa **30 giây** — không nghe → tự chuyển "cuộc gọi nhỡ".

### 3.3. Trạng thái & ý nghĩa

| Hiển thị | Ý nghĩa |
|---|---|
| Đang gọi... | Đã bấm gọi, đang chờ đối phương nghe |
| Cuộc gọi đến... | Có người gọi đến — bấm Nghe/Từ chối |
| Đang kết nối... | Đã accept, đang bắt tay WebRTC |
| Đang gọi | Đã thông — nói chuyện được |
| Đang nối lại... | Mất kết nối tạm thời, tự thử lại (tối đa 2 lần) |
| Bạn có cuộc gọi nhỡ. | Có cuộc gọi đến không nghe — chỉ hiện 1 lần |

### 3.4. Lưu ý vận hành

- **Micro bắt buộc:** nếu từ chối quyền mic, cuộc gọi không có tiếng — kiểm tra icon ổ khóa trên thanh địa chỉ → cho phép Microphone → tải lại trang.
- **Mạng yếu:** cuộc gọi tự chuyển TURN relay; nếu vẫn rớt, hệ thống báo kết thúc — gọi lại bình thường.
- **Nhiều thiết bị:** nếu owner mở ShopInbox trên 2 máy, cả 2 cùng reo; máy nào bấm Nghe trước sẽ nhận cuộc gọi (máy kia tự hủy trạng thái reo).
- **Cước phí:** trừ vào ví phí nền tảng của tenant **sau khi cúp** — báo cáo xem tại `/wallet/phi-nen-tang`.

---

## 4. Dành cho Buyer / Khách hàng

### 4.1. Gọi cho shop

1. Mở **KhachLink** (PWA/web) → vào **Chat** với shop bạn đang trao đổi.
2. Bấm nút **📞 "Gọi thoại"** trong khung chat → cho phép micro → chờ shop nghe máy.
3. Khi shop bấm Nghe → trạng thái "Đang gọi" → nói chuyện bình thường.

> Nút gọi **chỉ hiện trong hội thoại chat đã tồn tại** — bạn phải nhắn tin với shop trước. Không thể gọi shop chưa từng chat (trừ khi shop bật "gọi tự do" — hiếm).

### 4.2. Khi shop gọi đến

- Trong khung chat hiện **"Cuộc gọi đến..."** + nút **Nghe** / **Từ chối**.
- Không nghe trong 30 giây → cuộc gọi kết thúc, hiện nhắc **"Bạn có cuộc gọi nhỡ."**

### 4.3. Lỗi thường gặp phía khách

| Thông báo | Nghĩa | Cách xử lý |
|---|---|---|
| "Tính năng gọi thoại đang tạm ngừng." | Shop đã tắt voice (kill-switch) hoặc hệ thống đang bảo trì | Nhắn tin bình thường, thử lại sau |
| "Không gọi được: ..." | Lỗi kết nối/mạng | Kiểm tra mạng, tải lại trang, thử lại |
| Không có tiếng 2 chiều | Quyền mic bị chặn | Bấm icon ổ khóa → Microphone → Allow → gọi lại |
| Cuộc gọi vào "Đang nối lại..." mãi | Mạng rất yếu | Chuyển Wi-Fi/4G khác hoặc gọi lại |

> Khách **không thấy** ví phí nền tảng — mọi thông báo phí đều trung tính ("tính năng tạm ngừng"), không lộ thông tin ví của shop.

---

## 5. Bảng lỗi & cách xử lý

| Triệu chứng | Nguyên nhân thường gặp | Xử lý |
|---|---|---|
| Không thấy nút "Gọi thoại" (cả 2 phía) | Shop chưa bật `Voice_Call_Enabled` trong Cài đặt → Tính năng cửa hàng (mặc định TẮT), hoặc SystemAdmin tắt `VoiceFeatureEnabled` ở `/admin/voice`, hoặc đang ở màn hình không phải chat | Owner bật toggle "Bật gọi thoại trong chat (P4)" → Lưu → F5; SystemAdmin kiểm tra `/admin/voice`; phải gọi từ trong chat |
| "Tính năng gọi thoại đang tạm ngừng." | Kill-switch OFF hoặc METERING_DENIED (ShopERP hiện kèm link nạp phí) | Bật lại toggle; nạp ví tại `/wallet/phi-nen-tang` |
| "Bạn có cuộc gọi nhỡ." | Cuộc gọi đến không nghe sau 30s | Gọi lại ngay trong cùng khung chat |
| "Đang gọi..." mãi không vào "Đang gọi" | Đối phương chưa accept / mạng đối phương yếu | Chờ hoặc Hủy; kiểm tra mạng 2 bên |
| Hai bên đều "Đang nối lại..." | Rớt kết nối P2P + TURN | Hệ thống tự thử lại ≤2 lần; nếu thất bại → Kết thúc & gọi lại |
| Tiếng rè/đứt quãng | Mạng yếu, nhiều tab/app dùng mic | Đóng tab thừa, chuyển mạng ổn định hơn |
| Cuộc gọi cũ vẫn "treo" sau khi đóng app | App bị kill giữa call | Mở lại → hệ thống tự reconcile (tối đa vài phút); gọi mới vẫn bình thường |

---

## 6. Câu hỏi thường gặp (FAQ)

**Q: Gọi thoại có tốn phí không?**
A: Có — tính theo `voice.minute` (phút, làm tròn lên) + `voice.call` qua ví phí nền tảng của tenant. Pilot hiện MeterOnly (chỉ ghi nhận). Cuộc gọi **không bị cắt** vì hết phí.

**Q: Khách có cần cài app không?**
A: Không bắt buộc — dùng web/PWA bình thường. Cài PWA giúp ổn định hơn trên điện thoại (đặc biệt iOS Safari).

**Q: Gọi được không cần chat trước không?**
A: Mặc định KHÔNG — phải có hội thoại chat đã xác minh (chống spam/lạm dụng). SystemAdmin có thể bật "Gọi tự do" per-tenant khi cần.

**Q: Cuộc gọi có qua server Vạn An không?**
A: Chỉ phần **báo hiệu** (ai gọi ai, trạng thái) qua server. **Tiếng nói đi trực tiếp P2P** giữa 2 máy; nếu không được mới qua TURN relay. Server không nghe/lưu nội dung cuộc gọi.

**Q: Tắt voice có ảnh hưởng chat không?**
A: Không — kill-switch chỉ chặn cuộc gọi, nhắn tin/đặt hàng/chat hoạt động bình thường.

**Q: Owner dùng 2 điện thoại cùng lúc thì sao?**
A: Cả 2 đều nhận cuộc gọi đến; máy nào bấm Nghe trước thắng, máy kia tự hủy. Mỗi user chỉ có **1 cuộc gọi active** tại một thời điểm.

**Q: Muốn nghe lại cuộc gọi nhỡ?**
A: Hệ thống **không ghi âm** cuộc gọi (privacy). Chỉ có trạng thái "cuộc gọi nhỡ" + thời gian — hãy gọi lại.

---

## Phụ lục — Tham chiếu kỹ thuật (cho dev/ops)

- Voice service: `10_Voice/vanan-voice/` · endpoint `https://voice.khachvip.online` · hub `/hubs/voice` · health `/v1/health` · metrics `/v1/metrics/summary`
- Admin UI: ShopERP `/admin/voice` (Gateway `/api/admin/voice-config`) · metering `/admin/metering/rate-cards`
- Task cards: `docs/AI/tasks/voice_service/` (p0–p7) · master plan `docs/AI/plans/voice-service-master-plan.md` · SRS `docs/requirements/B. SRS – Vạn An Voice Service v1.1.md`
- RV scripts: `6_Testing/rv-scripts/voice-rv-sweep.mjs` + `voice-call-sim.mjs`
