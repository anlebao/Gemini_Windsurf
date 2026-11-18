# FAQ & Xử lý sự cố — Account Trust & Recovery (ATRS)

Tài liệu tham khảo nhanh cho cả khách hàng và nhân viên. Cập nhật: 2026-11-18.

---

## A. Dành cho khách hàng

### A1. Tôi quên mật khẩu — dùng "Quên mật khẩu" hay "Khôi phục tài khoản"?
- **Quên mật khẩu đăng nhập** (tài khoản cửa hàng/ShopERP): dùng chức năng đặt lại mật khẩu của hệ thống đăng nhập.
- **Mất passkey / mã khôi phục / thiết bị**: dùng `/recovery` — quy trình xác minh bằng bằng chứng.

### A2. Tôi có thể có bao nhiêu passkey?
Không giới hạn cứng, nhưng nên giữ 2 thiết bị tin cậy (điện thoại + máy tính). Mỗi passkey gắn với một thiết bị.

### A3. Mã khôi phục có hết hạn không?
Không hết hạn, nhưng **mỗi mã chỉ dùng 1 lần**. Khi dùng hết, tạo bộ mới — bộ cũ bị thu hồi toàn bộ. Hãy tạo bộ mới ngay khi còn ít mã.

### A4. Tôi thấy thiết bị lạ trong danh sách — phải làm gì?
Vào `/security` → thu hồi thiết bị đó ngay (cần step-up bằng passkey). Sau đó vào `/recovery` chọn "nghi bị truy cập trái phép" nếu nghi có người đã đăng nhập.

### A5. Tại sao tôi không tạo được mã khôi phục / passkey?
- Tạo mã khôi phục yêu cầu **step-up** — hãy tạo passkey trước.
- Trình duyệt/thiết bị phải hỗ trợ WebAuthn (hầu hết thiết bị hiện đại đều hỗ trợ; trình duyệt cũ hoặc chế độ ẩn danh có thể không).

### A6. Yêu cầu khôi phục của tôi bị từ chối — tôi có thể thử lại?
Có. Đảm bảo cung cấp **bằng chứng khác/đầy đủ hơn** (đơn hàng gần nhất, thiết bị cũ, mốc thời gian). Mỗi ngày tối đa 3 yêu cầu — hãy chuẩn bị kỹ trước khi gửi.

### A7. Khiếu nại điểm của tôi được duyệt thì bao lâu thấy điểm về?
Sau khi nhân viên phê duyệt, hệ thống ghi điều chỉnh vào sổ cái điểm ngay lập tức — số dư cập nhật trong vòng vài phút. Nếu chưa thấy, kiểm tra lại trang khiếu nại hoặc liên hệ cửa hàng.

### A8. Tôi là khách vãng lai (không tài khoản) — có dùng được không?
**Chưa.** Passkey, mã khôi phục, khôi phục và khiếu nại trực tuyến hiện yêu cầu đăng nhập tài khoản. Khách vãng lai liên hệ cửa hàng trực tiếp.

### A9. Tài khoản tôi bị khóa — tôi có tự mở lại được không?
**Không.** Khi cửa hàng khóa tài khoản, hệ thống bảo mật đồng bộ trạng thái RESTRICTED/CLOSED — mọi yêu cầu khôi phục sẽ bị hạn chế và phải qua rà soát chặt. Liên hệ cửa hàng để được xem xét.

### A10. Thông báo của tôi ở đâu?
Trong KhachLink, mục **Thông báo** (`/notifications`). Hiện chưa có email/SMS cho sự kiện bảo mật — kiểm tra trong ứng dụng định kỳ.

---

## B. Dành cho nhân viên

### B1. Menu ATRS không hiện — tại sao?
- Role phải là **Owner** hoặc **SystemAdmin** (menu CRM & Loyalty).
- Quyền chi tiết (view/review/approve...) được cấp riêng trong `/admin/atrs-roles` — SystemAdmin có sẵn tất cả.

### B2. Khách nói không nhận được thông báo khôi phục.
Kiểm tra `/admin/atrs-audit` xem sự kiện `recovery.*` đã phát chưa. Lưu ý: chỉ có **thông báo trong ứng dụng** — chưa email/SMS (tính năng đang chờ tích hợp).

### B3. Tôi không thấy quyền "Duyệt" trên yêu cầu khôi phục.
Có thể bạn chỉ có `recovery.review` mà chưa có `recovery.approve`. Hoặc yêu cầu đang ở trạng thái không cho phép duyệt (vd: chưa qua bước review). Kiểm tra trạng thái trên trang chi tiết.

### B4. Khiếu nại: tôi đề xuất điều chỉnh nhưng không ai duyệt được.
Maker-checker: **approver phải khác proposer**. Nếu bạn vừa propose vừa là approver duy nhất, cần người khác có `dispute.approve_adjustment` duyệt — hoặc SystemAdmin.

### B5. Dispute decision bị FAILED.
Sổ cái điểm (qua Gateway) từ chối — kiểm tra log Gateway, đảm bảo customer tồn tại và số điểm hợp lệ. Sau khi sửa nguyên nhân, tạo decision mới (không retry decision cũ — idempotency key đã dùng).

### B6. Dashboard báo "Quá hạn SLA" tăng đột biến.
Kiểm tra `/admin/recovery` lọc trạng thái — có thể nhiều yêu cầu SUBMITTED cũ chưa xử lý. SLA mặc định 24h, có thể điều chỉnh trong `/admin/atrs-policy` (key `recovery.sla_hours`) — thay đổi cần maker-checker.

### B7. Làm sao biết một yêu cầu khôi phục là thật hay giả?
Không có điểm "chắc chắn" — đánh giá qua: risk decision (ALLOWED/STEP_UP/REVIEW), bằng chứng khách cung cấp, lịch sử thiết bị, tính nhất quán thông tin. Khi nghi ngờ, chọn **escalate** thay vì approve/reject vội.

### B8. Thu hồi quyền của nhân viên nghỉ việc.
`/admin/atrs-roles` → tìm nhân viên → thu hồi từng grant. Nếu nhân viên là SystemAdmin (break-glass), quyền role-based không thu hồi qua trang này — cần đổi role ở ShopERP User Management.

---

## C. Dành cho quản trị hệ thống

### C1. ATRS service down — ảnh hưởng gì?
- Khách không khôi phục/khai báo được; nhân viên không xử lý được.
- Gateway/ShopERP **không crash** — các tích hợp là best-effort.
- Kiểm tra: `curl http://<atrs-vps>:5080/health` → 200 là OK.

### C2. Backup/restore atrs database.
Xem `docs/ops/atrs-runbook.md` mục "Database restore" — đã verify pg_dump/restore trên prod.

### C3. Metrics/monitoring.
- Endpoint `GET /metrics` (Prometheus) trên atrs:5080 — counters recovery/dispute/notifications + gauges pending/SLA.
- Log container `vanan-atrs-atrs-1` — mọi staff action + internal sync đều có log.

### C4. Bảo mật endpoint nội bộ.
`POST /api/v1/internal/account-status` yêu cầu `X-Internal-Api-Key` (chia sẻ Gateway↔ATRS qua GH secret `ATRS_INTERNAL_API_KEY`). Không expose ra ngoài — chỉ Gateway gọi.

---

## D. Giới hạn đã biết (documented)

| Hạng mục | Trạng thái |
|---|---|
| Email/SMS notification | Chưa — chờ tích hợp Brevo (P4) |
| Guest (không tài khoản) security UI | API sẵn, UI chưa — khách vãng lai dùng kênh trực tiếp |
| Staff MFA/step-up | Chưa — nằm trong lộ trình MFA-1 |
| Delay giao dịch nhạy cảm sau recovery | Chưa wire — risk engine + manual review bù một phần |
| U04 secret questions | Cố ý OMIT (D7) — không triển khai |
