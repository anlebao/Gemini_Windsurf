# Hướng dẫn khách hàng — Bảo mật tài khoản & Khôi phục (ATRS)

Tài liệu này hướng dẫn người mua sử dụng các tính năng bảo mật tài khoản trên KhachLink: passkey, mã khôi phục, thiết bị, khôi phục tài khoản và khiếu nại điểm thưởng.

> **Lưu ý quan trọng:** Mức bảo vệ tài khoản **không phải là chứng minh danh tính pháp lý**. Passkey và mã khôi phục giúp bạn chứng minh quyền sở hữu tài khoản trong hệ thống, nhưng không thay thế giấy tờ tùy thân khi cần xác minh pháp lý.

---

## 1. Trung tâm bảo mật (`/security`)

Vào **Trang cá nhân → Bảo mật tài khoản** (hoặc đường dẫn `/security`). Trung tâm bảo mật gồm 3 phần:

### 1.1. Passkey (khóa truy cập sinh trắc học)

- Nhấn **"Tạo passkey"** → trình duyệt/thiết bị sẽ yêu cầu vân tay, Face ID hoặc PIN màn hình để đăng ký.
- Passkey gắn với tài khoản của bạn, không lưu trên server dưới dạng mật khẩu — an toàn hơn mật khẩu thường.
- Bạn có thể tạo nhiều passkey (ví dụ: một trên điện thoại, một trên máy tính).
- **Thu hồi passkey:** vào danh sách passkey → chọn passkey cần thu hồi. Một số thao tác nhạy cảm yêu cầu **xác thực lại (step-up)** bằng passkey trước khi thực hiện.

### 1.2. Thiết bị đã đăng nhập

- Danh sách thiết bị từng dùng passkey/đăng nhập, kèm loại thiết bị, nền tảng và thời điểm hoạt động gần nhất.
- **Thu hồi thiết bị:** chọn thiết bị → "Thu hồi". Sau khi thu hồi, thiết bị đó không còn dùng được passkey của bạn (cần step-up để xác nhận).
- Nếu mất điện thoại: vào trung tâm bảo mật **từ một thiết bị khác còn truy cập được** → thu hồi thiết bị đã mất.

### 1.3. Mã khôi phục (Recovery codes)

- Hệ thống cấp **10 mã khôi phục** dạng `XXXXX-XXXXX` khi bạn tạo bộ mã đầu tiên.
- **Mã chỉ hiển thị MỘT LẦN** — hãy chụp màn hình hoặc ghi lại và cất ở nơi an toàn (không lưu trên cùng thiết bị).
- Mỗi mã **chỉ dùng được một lần**. Khi hết mã, bạn có thể tạo bộ mới (sẽ thu hồi toàn bộ mã cũ).
- Tạo lại bộ mã là thao tác nhạy cảm → hệ thống yêu cầu step-up bằng passkey.
- **Tuyệt đối không** gửi mã khôi phục cho bất kỳ ai, kể cả nhân viên hỗ trợ.

### 1.4. Hồ sơ & bằng chứng (`/security/profile`)

- Cập nhật thông tin dùng làm bằng chứng khi khôi phục (tên, thông tin liên hệ đã xác minh... tùy cấu hình).
- Thiết lập tùy chọn thông báo bảo mật.

---

## 2. Hướng dẫn thiết lập (`/security/setup`)

Trang này hiển thị checklist thiết lập bảo mật:

1. Tạo passkey (khuyến nghị — nhanh và an toàn nhất).
2. Tạo bộ mã khôi phục và lưu lại offline.
3. Kiểm tra danh sách thiết bị, thu hồi thiết bị lạ.

Bạn **không bắt buộc** hoàn thành tất cả — nhưng tài khoản chỉ có passkey sẽ an toàn hơn; tài khoản chỉ có mã khôi phục vẫn dùng được nhưng cần giữ mã thật kỹ.

---

## 3. Khôi phục tài khoản (`/recovery`)

Dùng khi bạn **mất thiết bị**, **mất hết passkey/mật khẩu/mã khôi phục**, **nghi bị truy cập trái phép**, hoặc lý do khác.

### Điều kiện

- Bạn phải **đăng nhập được** vào KhachLink trên thiết bị hiện tại (phiên đăng nhập còn hiệu lực). Nếu đã mất toàn bộ phiên đăng nhập, liên hệ cửa hàng/nhân viên hỗ trợ để được hướng dẫn thủ tục thủ công.
- Mỗi tài khoản giới hạn **tối đa 3 yêu cầu khôi phục mỗi ngày** (yêu cầu thứ 4 sẽ bị từ chối — chống lạm dụng).

### Quy trình 4 bước

1. **Chọn vấn đề:** mất thiết bị / không còn thông tin đăng nhập / nghi bị truy cập trái phép / khác.
2. **Cung cấp bằng chứng:** tùy loại yêu cầu, hệ thống có thể yêu cầu mã khôi phục, thông tin đơn hàng gần nhất, hoặc thông tin liên hệ đã xác minh.
3. **Gửi yêu cầu:** hệ thống tạo mã tham chiếu — lưu lại mã này để tra cứu.
4. **Theo dõi & xác minh:** vào `/recovery/{mã}` để xem trạng thái; khi yêu cầu ở trạng thái chờ, bạn nhập **mã khôi phục** để xác minh danh tính.

### Kết quả có thể xảy ra

| Kết quả | Ý nghĩa |
|---|---|
| **APPROVED** | Yêu cầu được duyệt — làm theo hướng dẫn trên màn hình (đổi thông tin, thu hồi phiên cũ...). |
| **PENDING_REVIEW** | Chuyển nhân viên rà soát thủ công — thời gian xử lý tùy mức độ phức tạp. |
| **NEEDS_INFO** | Cần bổ sung thông tin — kiểm tra thông báo và phản hồi. |
| **REJECTED** | Bằng chứng không đủ — có thể gửi yêu cầu mới với bằng chứng khác. |
| **ESCALATED** | Chuyển cấp quản lý cao hơn. |

> **Lưu ý:** Mọi hành động khôi phục đều được ghi nhật ký bảo mật (ai làm, khi nào, kết quả) — minh bạch và chống gian lận.

---

## 4. Khiếu nại điểm thưởng / giao dịch (`/disputes`)

Dùng khi bạn phát hiện **sai lệch điểm thưởng, ví, hoặc giao dịch**.

### 4.1. Danh sách khiếu nại (`/disputes`)

- Xem tất cả khiếu nại đã gửi, trạng thái và tiến trình xử lý.

### 4.2. Tạo khiếu nại (`/disputes/new`)

1. Chọn **giao dịch/giao dịch điểm** liên quan (hệ thống liệt kê lịch sử giao dịch của bạn tại cửa hàng).
2. Mô tả vấn đề, chọn loại khiếu nại.
3. Đính kèm **bằng chứng** (ảnh chụp, file) nếu có — hệ thống lưu dấu vân tay (hash) file để chống sửa đổi.
4. Xác nhận chính sách bảo mật → gửi.

> Khiếu nại gửi thành công sẽ **không thể sửa nội dung** — hãy kiểm tra kỹ trước khi gửi.

### 4.3. Chi tiết khiếu nại (`/disputes/{id}`)

- Xem timeline xử lý, trạng thái, quyết định của nhân viên.
- Có thể **rút khiếu nại** khi còn ở trạng thái cho phép.
- Nếu khiếu nại được chấp nhận điều chỉnh điểm, hệ thống thực hiện qua sổ cái điểm chính thức — **không sửa số dư trực tiếp**, đảm bảo minh bạch.

---

## 5. Thông báo

- Thông báo liên quan bảo mật (khôi phục, khiếu nại, thay đổi thiết bị) hiển thị **trong ứng dụng** tại mục Thông báo.
- Hiện tại hệ thống **chưa gửi email/SMS** cho các sự kiện này — hãy kiểm tra thông báo trong ứng dụng định kỳ.

---

## 6. Câu hỏi thường gặp

**Q: Tôi mất điện thoại, làm sao thu hồi thiết bị?**
A: Đăng nhập KhachLink trên thiết bị khác → `/security` → danh sách thiết bị → thu hồi thiết bị đã mất. Sau đó tạo passkey mới trên thiết bị hiện tại.

**Q: Tôi mất cả mã khôi phục lẫn passkey?**
A: Vào `/recovery` → chọn "Tôi không còn mật khẩu/passkey/mã khôi phục" → làm theo hướng dẫn cung cấp bằng chứng → hệ thống chuyển nhân viên rà soát thủ công.

**Q: Nhân viên có xem được mã khôi phục/passkey của tôi không?**
A: **Không.** Mã khôi phục lưu dạng hash một chiều; passkey là khóa mật mã — nhân viên không bao giờ thấy được.

**Q: Tôi mua hàng không cần tài khoản (khách vãng lai) thì sao?**
A: Tính năng bảo mật nâng cao (passkey, khôi phục, khiếu nại) hiện yêu cầu đăng nhập tài khoản. Khách vãng lai liên hệ cửa hàng trực tiếp khi cần hỗ trợ.

**Q: Tài khoản của tôi bị khóa thì sao?**
A: Khi cửa hàng khóa/vô hiệu hóa tài khoản, hệ thống bảo mật tự động đồng bộ trạng thái — các yêu cầu khôi phục sẽ bị hạn chế và phải qua rà soát chặt hơn.

**Q: Tôi nghi tài khoản bị hack?**
A: Vào `/recovery` → chọn "Tôi nghi tài khoản bị truy cập trái phép" → cung cấp bằng chứng → sau khi được duyệt, hệ thống sẽ thu hồi phiên/thiết bị đáng ngờ.
