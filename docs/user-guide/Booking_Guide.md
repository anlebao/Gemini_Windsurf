# HƯỚNG DẪN SỬ DỤNG ĐẶT LỊCH HẸN (APPOINTMENT BOOKING) — VẠN AN ECOSYSTEM

> **Phiên bản:** MVP v1.1 — cập nhật 2026-10-06
> **Áp dụng:** Appointment Booking & Staff Scheduling MVP (SRS v1.1) — dành cho mô hình dịch vụ theo lịch hẹn: Spa / Hair Salon / Massage / Karaoke / Clinic.
> **Phạm vi:** Khách hàng đặt lịch qua KhachLink (4 màn hình + trang theo dõi) · Chủ shop/quản lý xử lý lịch hẹn qua ShopERP (8 trang) · Hoa hồng cho cộng tác viên giới thiệu (QR).
> **Luồng tổng thể:** QR → chọn dịch vụ → chọn giờ & nhân viên → ghi chú & cọc → xác nhận → shop duyệt → gán nhân viên → đón khách → hoàn tất → tạo Đơn hàng → tính hoa hồng (nếu có).

---

## MỤC LỤC

1. [Tổng quan & vai trò](#1-tổng-quan--vai-trò)
2. [Chuẩn bị — Bật tính năng & tạo mã QR](#2-chuẩn-bị--bật-tính-năng--tạo-mã-qr)
3. [Shop Owner / StoreKeeper — Quản lý lịch hẹn (ShopERP)](#3-shop-owner--storekeeper--quản-lý-lịch-hẹn-shoperp)
4. [Nhân viên & lịch làm việc](#4-nhân-viên--lịch-làm-việc)
5. [Đặt cọc & hoàn cọc](#5-đặt-cọc--hoàn-cọc)
6. [Hoa hồng cộng tác viên](#6-hoa-hồng-cộng-tác-viên)
7. [Customer — Khách hàng đặt lịch (KhachLink)](#7-customer--khách-hàng-đặt-lịch-khachlink)
8. [Bảng tra cứu nhanh trạng thái](#8-bảng-tra-cứu-nhanh-trạng-thái)
9. [Câu hỏi thường gặp (FAQ)](#9-câu-hỏi-thường-gặp-faq)

---

## 1. TỔNG QUAN & VAI TRÒ

| Vai trò | Mô tả | Nền tảng truy cập | Phạm vi |
|---|---|---|---|
| **Shop Owner / StoreKeeper** | Chủ shop / quản lý — duyệt lịch, gán nhân viên, xử lý cọc, theo dõi hoa hồng | ShopERP — menu **"Đặt lịch hẹn"** (`/booking/*`) | **Shop của mình** (per-tenant) |
| **Customer** | Khách hàng cuối — đặt lịch không cần đăng ký | KhachLink PWA — link `/booking/{mã QR}` | Chính lịch hẹn của mình |
| **Collaborator (Salesman)** | Cộng tác viên giới thiệu qua mã QR riêng → hưởng hoa hồng khi lịch hoàn tất | Nhận hoa hồng qua ví điện tử (Wallet) | Lịch do QR của mình giới thiệu |

**Đặc điểm chính:**

- **Không cần đăng ký tài khoản** — khách đặt lịch nặc danh (zero-friction), hệ thống nhận diện qua thiết bị.
- **Chống đặt trùng giờ** — một nhân viên chỉ nhận 1 lịch trong cùng khung giờ; 2 khách đặt cùng lúc → 1 người được, người kia nhận thông báo "khung giờ vừa có người đặt".
- **Chỉ shop đã kích hoạt mới nhận đặt lịch** (feature-flag từng shop) — F&B/không dịch vụ không bị ảnh hưởng.
- **Hoàn tất lịch → tự động tạo Đơn hàng** — doanh thu về đúng luồng kế toán hiện có.
- **Hoa hồng chỉ tính khi đủ điều kiện**: lịch HOÀN TẤT + đã thu đủ cọc (nếu có) + QR còn hiệu lực.

---

## 2. CHUẨN BỊ — BẬT TÍNH NĂNG & TẠO MÃ QR

### 2.1 Kích hoạt tính năng đặt lịch

Tính năng được kích hoạt **theo từng shop** (mặc định TẮT). Khi kích hoạt, shop chọn **chính sách đặt cọc**:

| Chính sách cọc | Ý nghĩa |
|---|---|
| Không cọc | Khách không phải đặt cọc khi xác nhận lịch |
| Cọc cố định (VNĐ) | Khách đặt cọc số tiền cố định (vd 100.000đ) |
| Cọc theo phần trăm | Cọc = % tổng giá trị dịch vụ |

> **Chủ shop cần làm:** liên hệ quản trị Vạn An (hoặc qua API tenant — `PUT /api/tenant/booking/config`) để bật tính năng + chọn chính sách cọc + nhập nội dung chính sách hủy (vd: "Hủy trước 2 giờ").

### 2.2 Tạo mã QR đặt lịch

Vào ShopERP → **Đặt lịch hẹn → Mã QR** (`/booking/qr-channels`):

1. Nhấn **"Tạo mã QR"**.
2. Chọn **cộng tác viên giới thiệu** (nếu có — để tính hoa hồng), hoặc để trống (khách vào thẳng, không ghi nhận hoa hồng).
3. Nhấn tạo → hệ thống sinh **mã QR + link đặt lịch** (vd `https://.../booking/4F3C1A2B-...`).
4. **Lưu lại link ngay** — mã QR chỉ hiển thị **ĐÚNG MỘT LẦN** (bảo mật §7.2); sau khi đóng màn hình không xem lại được.
5. In/dán mã QR ở quầy, bàn, hoặc gửi link cho khách / cộng tác viên.

**Thu hồi mã QR:** nếu mã bị lộ hoặc hết chiến dịch → nhấn **"Thu hồi"** → mã cũ không nhận đặt lịch nữa (lịch đã đặt trước đó vẫn giữ nguyên).

> 💡 **Mẹo:** tạo 1 mã QR riêng cho từng cộng tác viên → biết chính xác ai giới thiệu lịch nào → hoa hồng đúng người.

---

## 3. SHOP OWNER / STOREKEEPER — QUẢN LÝ LỊCH HẸN (SHOPERP)

Vào ShopERP → menu **"Đặt lịch hẹn"** (8 trang).

### 3.1 Hàng đợi đặt lịch (`/booking/queue`)

Trang chính mỗi ngày — danh sách lịch hẹn theo trạng thái:

- **Bộ lọc nhanh** theo trạng thái: Tất cả · Chờ xác nhận · Đã xác nhận · Đã phân công · Đã đến · Đang phục vụ · Hoàn tất · Đã hủy.
- Mỗi lịch hiển thị: tên dịch vụ, khung giờ, mã lịch, khách (mã thiết bị / ghi chú), badge cọc.

**Thao tác theo trạng thái:**

| Trạng thái lịch | Nút thao tác | Ý nghĩa |
|---|---|---|
| **Chờ xác nhận** | ✓ Xác nhận · ✕ Từ chối · Vắng mặt · Hủy | Duyệt lịch khách mới đặt |
| **Đã xác nhận** | 👤 Phân công · Vắng mặt · Hủy | Chọn nhân viên phục vụ |
| **Đã phân công** | ✅ Check-in · 👤 Đổi nhân viên · Hủy | Khách đã đến quầy |
| **Đã đến** | ▶ Bắt đầu | Bắt đầu phục vụ |
| **Đang phục vụ** | 💵 Hoàn tất | Nhập tổng tiền thực tế → tạo Đơn hàng |
| **Hoàn tất** | — (hiển thị 🧾 Đơn hàng) | Đã tạo đơn + tính hoa hồng (nếu đủ điều kiện) |

**Phân công / đổi nhân viên:** nhấn **👤 Phân công** → modal hiển thị **danh sách nhân viên phù hợp** (đúng kỹ năng dịch vụ + rảnh khung giờ) với badge **"Sẵn sàng"** hoặc lý do bận (vd "Đã có lịch 14:00"). Chỉ chọn nhân viên **Sẵn sàng** — hệ thống kiểm tra lại xung đột ngay khi lưu.

**Hoàn tất:** nhập **tổng tiền thực tế** (mặc định = giá dịch vụ) → xác nhận → hệ thống **tự động tạo Đơn hàng** (theo dõi ở trang Đơn hàng như đơn bán thường) và **tính hoa hồng** (nếu đủ điều kiện).

### 3.2 Xem ai rảnh (`/booking/availability`)

Bảng **nhân viên × khung giờ** trong ngày: ✓ = rảnh, ✗ = bận kèm lý do (ngoài giờ làm việc / nghỉ / đã có lịch / không có kỹ năng). Dùng để chủ động gán lịch hoặc tư vấn khách.

### 3.3 Lịch theo ngày (`/booking/calendar`)

Xem lịch hẹn theo ngày (◀ ▶ chuyển ngày): bảng **giờ · khách · dịch vụ · nhân viên · trạng thái** — dùng cho quầy lễ tân điều phối.

---

## 4. NHÂN VIÊN & LỊCH LÀM VIỆC

### 4.1 Quản lý nhân viên (`/booking/staff`)

- **Thêm nhân viên**: tên + vai trò (vd "KTV", "Thợ cắt tóc") → lưu.
- **Gán kỹ năng**: nhấn "Kỹ năng" → tích chọn **dịch vụ** nhân viên được phục vụ (vd KTV Lan: Massage 60' + 90'; KTV Hoa: chỉ Massage 60').
  - Nhân viên KHÔNG có kỹ năng phù hợp → **không xuất hiện** trong danh sách phân công cho dịch vụ đó.
- **Vô hiệu hóa**: bỏ tick "Đang hoạt động" → không nhận lịch mới (lịch cũ giữ nguyên).

### 4.2 Lịch làm việc (`/booking/schedules`)

- **Lịch tuần (recurring)**: 7 ngày × giờ bắt đầu/kết thúc + **giờ nghỉ giữa ca** (vd 12:00–13:00). Khách không đặt được lịch trùng giờ nghỉ.
- **Lịch đặc biệt (override) theo ngày cụ thể** — 4 loại:
  - **Đi làm** (ngoài lịch tuần, vd tăng ca)
  - **Nghỉ phép** — nhân viên nghỉ ngày đó (không nhận lịch)
  - **Không có mặt** (bận việc khác)
  - **Nghỉ giữa ca** (nghỉ thêm 1 khung giờ riêng)

> 💡 Mẹo: nghỉ phép nên khai báo sớm → khách xem giờ trống sẽ **không thấy** giờ của nhân viên nghỉ (hệ thống tự loại).

---

## 5. ĐẶT CỌC & HOÀN CỌC

Trang **Đặt cọc** (`/booking/deposits`) — danh sách lịch có yêu cầu cọc + trạng thái thanh toán.

### 5.1 Nhận cọc

1. Khách đặt lịch → lịch có yêu cầu cọc hiển thị badge **"Cọc 100.000đ — Chưa thu"**.
2. Khách chuyển khoản / trả tiền mặt tại quầy → nhấn **"Đã nhận cọc"** → hệ thống ghi nhận **Đã thu cọc** (trạng thái thanh toán của lịch chuyển sang **Đã thanh toán**).
3. Trạng thái cọc đủ là **điều kiện bắt buộc** để tính hoa hồng cho cộng tác viên (xem mục 6).

### 5.2 Hoàn cọc

Khách hủy lịch hợp lệ theo chính sách (vd hủy trước 2 giờ) → nhấn **"Hoàn cọc"** → hệ thống ghi nhận hoàn tiền + **tự động đảo hoa hồng** (nếu hoa hồng đã tính cho lịch này).

> **Lưu ý:** cọc không thu → lịch hoàn tất vẫn **không tính hoa hồng** (điều kiện "đã thu cọc" chưa đủ).

---

## 6. HOA HỒNG CỘNG TÁC VIÊN

Trang **Hoa hồng** (`/booking/commission`) — sổ cái hoa hồng hợp nhất (booking + đơn hàng).

### 6.1 Điều kiện được hưởng hoa hồng (đủ TẤT CẢ)

1. Lịch do **QR của cộng tác viên** giới thiệu (khách vào qua link QR có gắn tên).
2. Lịch **HOÀN TẤT** (đã tạo đơn hàng).
3. **Đã thu đủ cọc** (nếu lịch có yêu cầu cọc).
4. Mã QR **còn hiệu lực** tại thời điểm khách đặt.

### 6.2 Xem & thanh toán

- Lọc theo cộng tác viên → bảng sổ cái: giá trị đơn, **hoa hồng (gross)** → **thuế TNCN khấu trừ** (theo quy định NĐ 253/2026 — mốc 5.000.000đ/lần) → **số thực nhận (net)**.
- Lịch **chưa đủ điều kiện** → không xuất hiện (không tính).
- Nhấn **"Thanh toán"** cho lịch đã đủ điều kiện (trạng thái **Đã tích lũy**) → tiền hoa hồng chuyển vào **ví điện tử của cộng tác viên** (trạng thái **Đã thanh toán**).

### 6.3 Hủy lịch sau khi đã tính hoa hồng

Hoàn cọc / hủy lịch đã hoàn tất → hệ thống **tự động tạo bút toán đảo (reversal)** — hoa hồng được hoàn lại đúng giá trị, ví cộng tác viên được trừ tương ứng (không mất tiền của shop).

---

## 7. CUSTOMER — KHÁCH HÀNG ĐẶT LỊCH (KHACHLINK)

Khách quét mã QR (hoặc mở link đặt lịch) → **4 màn hình đặt lịch** trên điện thoại, **không cần đăng ký tài khoản**:

### Màn 1 — Chọn dịch vụ
- Hiển thị tên shop + danh mục (chips) + **thẻ dịch vụ** (tên, thời lượng, giá) + **dịch vụ phụ thêm** (vd nước uống, phụ thu phòng VIP).
- Chọn dịch vụ (có thể chọn kèm dịch vụ phụ) → nhấn **"Tiếp tục"** (thanh tổng tiền hiển thị cố định ở đáy màn hình).

### Màn 2 — Chọn thời gian & nhân viên
- **Chọn ngày** (7 ngày tới) → danh sách **khung giờ trống** (nút lớn, dễ bấm).
- Lọc **"Bất kỳ ai" / chọn nhân viên cụ thể** — chỉ hiển thị giờ nhân viên đó rảnh.
- Nếu chọn nhân viên cụ thể → lịch **được gán ngay** cho nhân viên đó.

### Màn 3 — Ghi chú & đặt cọc
- **Thẻ ghi chú nhanh**: Phòng riêng / Lần đầu đến / KTV nữ / Cần chuẩn bị trước (chạm để chọn).
- 🎙 **Ghi chú bằng giọng nói**: chạm micro, nói, hệ thống chuyển thành chữ (có thể sửa lại; không lưu âm thanh).
- **Đặt cọc** (nếu shop yêu cầu): hiển thị số tiền cọc cố định theo chính sách shop (không tự chọn số khác).

### Màn 4 — Xác nhận
- Kiểm tra lại: dịch vụ · thời gian · nhân viên · ghi chú · tổng tiền · số cọc · **chính sách hủy** của shop.
- Nhấn **"XÁC NHẬN ĐẶT LỊCH"** → chờ hệ thống lưu thành công (bấm lại không tạo trùng).

### Trang theo dõi lịch (Status)
- Sau khi đặt → trang theo dõi tự cập nhật: **Đã gửi → Shop đang xử lý → Đã xác nhận → Đã phân công** (tự làm mới mỗi 5 giây, sau 2 phút giãn ra 15 giây).
- **Mã lịch hẹn** hiển thị (giữ lại để đối chiếu với shop).
- Nút **"Làm mới"** để xem ngay; nút **"Hủy lịch"** (chỉ khi chưa phục vụ — theo chính sách hủy của shop).
- Trạng thái kết thúc (Hoàn tất / Đã hủy / Từ chối / Vắng mặt) → tự dừng cập nhật.

---

## 8. BẢNG TRA CỨU NHANH TRẠNG THÁI

| Trạng thái | Ý nghĩa | Ai chuyển | Chuyển tiếp tới |
|---|---|---|---|
| **Chờ xác nhận** | Khách vừa đặt | Hệ thống (khách) | Shop xác nhận / từ chối / hủy |
| **Đã xác nhận** | Shop duyệt lịch | Shop | Phân công nhân viên / hủy / vắng mặt |
| **Đã phân công** | Đã chọn nhân viên | Shop | Check-in / đổi nhân viên / hủy / vắng mặt |
| **Đã đến** | Khách có mặt tại quầy | Shop | Bắt đầu phục vụ |
| **Đang phục vụ** | Đang làm dịch vụ | Shop | Hoàn tất |
| **Hoàn tất** | Xong → tạo Đơn hàng + tính hoa hồng | Shop | *(kết thúc)* |
| **Đã hủy / Từ chối / Vắng mặt** | Kết thúc không phục vụ | Khách / Shop | *(kết thúc)* |

---

## 9. CÂU HỎI THƯỜNG GẶP (FAQ)

**Q1: Khách đặt trùng giờ cùng nhân viên?**
Hệ thống chặn — 1 nhân viên chỉ nhận 1 lịch/khung giờ; khách đặt sau nhận thông báo "Khung giờ này vừa có người đặt. Vui lòng chọn khung giờ khác."

**Q2: Lịch đã gán nhân viên mà khách muốn hủy?**
Được — khách hủy từ trang theo dõi hoặc shop hủy từ Hàng đợi (trước khi khách đến; sau khi check-in không hủy được, chỉ hoàn tất hoặc vắng mặt).

**Q3: Lịch hoàn tất nhưng chưa thấy Đơn hàng?**
Đơn hàng được tạo tự động ngay khi hoàn tất (kiểm tra trang Đơn hàng, mã đơn gắn với mã lịch). Nếu chưa thấy, kiểm tra trạng thái cọc — hoặc bấm Hoàn tất lại lần nữa (hệ thống không tạo trùng).

**Q4: Vì sao lịch hoàn tất nhưng không có hoa hồng?**
Chưa đủ điều kiện: QR không gắn cộng tác viên · cọc chưa thu đủ · hoặc mã QR bị thu hồi trước khi khách đặt. Xem mục 6.1.

**Q5: Nhân viên nghỉ nhưng khách vẫn đặt được?**
Khai báo nghỉ tại **Lịch làm việc → Thêm lịch đặc biệt → Nghỉ phép** cho đúng ngày → hệ thống tự ẩn giờ của nhân viên đó.

**Q6: Khách đặt lịch có cần tài khoản?**
Không — đặt nặc danh qua thiết bị (mã lịch dùng để đối chiếu).

**Q7: Có thể đổi nhân viên sau khi đã phân công?**
Có — Hàng đợi → nút **"Đổi nhân viên"** → chọn nhân viên Sẵn sàng (hệ thống kiểm tra xung đột ngay).

**Q8: Hoa hồng bị trừ thuế?**
Có — khấu trừ thuế TNCN theo quy định hiện hành (NĐ 253/2026, mốc 5.000.000đ/lần); sổ cái hiển thị rõ gross → thuế → net.

---

> **Hỗ trợ:** Liên hệ quản trị Vạn An để kích hoạt tính năng đặt lịch, cấu hình cọc/chính sách, hoặc báo lỗi. Tài liệu kỹ thuật: `docs/AI/plans/booking-rv-fix-plan.md` · `docs/requirements/van_an_appointment_booking_srs_v1.1_mvp (1).md`.
