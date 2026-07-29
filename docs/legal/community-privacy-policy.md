# Chính sách Bảo mật — VanAn Community

**Phiên bản:** 1.0 (Draft)
**Ngày hiệu lực:** [Điền ngày]
**Lưu ý:** Đây là bản draft, cần luật sư review trước khi publish. Tuân thủ Nghị định 13/2023/NĐ-CP (bảo vệ dữ liệu cá nhân).

---

## 1. Dữ liệu thu thập

### 1.1 Dữ liệu cá nhân
- Họ tên, số điện thoại, email (từ social login hoặc OTP verify).
- Ngày sinh (tùy chọn, cho loyalty birthday reward).

### 1.2 Dữ liệu thiết bị (v1.2 NEW — Device Fingerprint)
- **Device fingerprint hash**: hash 64 chars từ thông tin thiết bị (user agent, platform, screen resolution, timezone).
- **Mục đích**: chống fraud — phát hiện thiết bị tạo nhiều tài khoản ảo.
- **Lưu trữ**: DeviceRegistration table, hash 1-way (không thể reverse).
- **Retention**: 24 tháng sau lần truy cập cuối.

### 1.3 Dữ liệu vị trí (Shipper)
- GPS ping khi shipper đang giao hàng (OutForDelivery).
- **Mục đích**: tracking đơn hàng real-time cho khách.
- **Lưu trữ**: LocationPing table, retention 90 ngày.

### 1.4 Dữ liệu giao dịch
- Đơn hàng, ví CTV, hoa hồng, COD.
- **Lưu trữ**: 5 năm (tuân thủ kế toán).

## 2. Đồng ý (Consent)

### 2.1 Device Fingerprint Consent (v1.2 NEW)
- Khi đăng nhập lần đầu, VanAn hiển thị consent dialog:
  > "VanAn thu thập device fingerprint để chống gian lận. Bạn đồng ý?"
- Nếu từ chối: tài khoản vẫn hoạt động nhưng không thể tham gia CTV (Shipper/Salesman).
- Có thể rút lại đồng ý bất cứ lúc nào → device fingerprint bị xóa.

### 2.2 Location Consent (Shipper)
- Khi kích hoạt Shipper role, hiển thị:
  > "VanAn thu thập vị trí GPS khi bạn đang giao hàng. Bạn đồng ý?"
- Nếu từ chối: không thể kích hoạt Shipper role.

## 3. Chia sẻ dữ liệu
- **Không bán** dữ liệu cá nhân cho bên thứ 3.
- Chia sẻ với shop owner (tenant): chỉ dữ liệu liên quan đơn hàng của shop đó.
- Chia sẻ với cơ quan chức năng khi có yêu cầu pháp lý.

## 4. Quyền của người dùng
- Xem dữ liệu cá nhân: qua Profile page.
- Yêu cầu xóa dữ liệu: liên hệ support@vanan.cloud (trừ dữ liệu kế toán bắt buộc lưu 5 năm).
- Rút lại consent device fingerprint / location.

## 5. Bảo mật
- Mã hóa TLS 1.3 cho tất cả traffic.
- Password hash bcrypt (cost 12).
- Device fingerprint hash SHA-256 1-way.
- JWT token cho admin, X-Customer-Token cho CTV (hết hạn 30 ngày).

## 6. Liên hệ
- Email: privacy@vanan.cloud
- Điện thoại: [Điền]
