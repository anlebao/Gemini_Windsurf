# Hướng dẫn nhân viên — Vận hành Account Trust & Recovery (ATRS)

Tài liệu này hướng dẫn nhân viên ShopERP sử dụng các trang quản trị ATRS: dashboard, hàng chờ khôi phục, hàng chờ khiếu nại, nhật ký bảo mật, phân quyền và chính sách bảo mật.

> **Nguyên tắc cốt lõi:** Mọi quyết định bảo mật đều **do backend (vanan-atrs) thực thi** — giao diện chỉ hiển thị và gửi yêu cầu. Nhân viên không thể sửa dữ liệu trực tiếp, mọi hành động đều ghi audit log.

---

## 1. Bảng điều hành (`/admin/atrs`)

Tổng quan tình hình bảo mật tài khoản, gồm các widget:

| Widget | Ý nghĩa |
|---|---|
| Mới trong 24h | Yêu cầu khôi phục mới tạo |
| Chờ duyệt | Đang chờ nhân viên xử lý |
| Đã chuyển cấp | Yêu cầu đã escalate |
| Chờ khách hàng | Khách cần bổ sung thông tin |
| Khiếu nại mở | Số khiếu nại đang xử lý |
| Quá hạn SLA | Yêu cầu vượt thời gian cam kết (mặc định 24h) |

Nhấn vào widget để đi tới danh sách tương ứng.

---

## 2. Hàng chờ khôi phục (`/admin/recovery`)

Danh sách yêu cầu khôi phục tài khoản của khách hàng.

### Bộ lọc

- **Trạng thái:** SUBMITTED / PENDING_REVIEW / PENDING_USER_ACTION / ESCALATED / APPROVED / REJECTED / COMPLETED...
- **Loại yêu cầu:** mất thiết bị / mất thông tin / nghi truy cập trái phép...
- **Người phụ trách:** lọc theo nhân viên được assign hoặc chưa assign.

### Thao tác trên từng yêu cầu (trang chi tiết `/admin/recovery/{id}`)

| Thao tác | Quyền cần | Mô tả |
|---|---|---|
| **Gán cho tôi** | `case.assign` | Nhận xử lý yêu cầu |
| **Thêm ghi chú** | `recovery.review` | Ghi chú nội bộ (khách không thấy) |
| **Yêu cầu thêm thông tin** | `recovery.review` | Chuyển trạng thái chờ khách bổ sung |
| **Escalate** | `recovery.review` | Chuyển cấp quản lý cao hơn |
| **Trả về review** | `recovery.review` | Đưa yêu cầu đã escalate về hàng chờ thường |
| **Khuyến nghị duyệt/từ chối** | `recovery.review` | Đề xuất quyết định (ghi vào timeline) |
| **Duyệt** | `recovery.approve` | Phê duyệt yêu cầu |
| **Từ chối** | `recovery.approve` | Từ chối — **bắt buộc nhập lý do** |

### Nguyên tắc khi review

- **Không bao giờ** yêu cầu khách gửi mã khôi phục, mật khẩu hay thông tin nhạy cảm qua chat.
- Kiểm tra **risk decision + policy version** trên màn hình trước khi quyết định.
- Mọi hành động đều ghi audit (ai làm, khi nào, lý do).
- Khi yêu cầu ở trạng thái không hợp lệ, hệ thống trả lỗi — đọc kỹ thông báo trước khi thử lại.

---

## 3. Hàng chờ khiếu nại (`/admin/disputes`)

Quy trình xử lý khiếu nại điểm thưởng/giao dịch theo mô hình **maker-checker**:

1. **Điều tra** (`dispute.investigate`): xem bằng chứng khách gửi, lịch sử giao dịch, assign cho nhân viên.
2. **Đề xuất điều chỉnh** (`dispute.propose_adjustment`): investigator đề xuất số điểm điều chỉnh — tạo `DisputeDecision` ở trạng thái chờ duyệt.
3. **Phê duyệt** (`dispute.approve_adjustment`): approver **phải là người khác** với proposer (maker-checker — tự duyệt sẽ bị hệ thống từ chối). Sau khi duyệt, hệ thống tự động ghi điều chỉnh vào sổ cái điểm qua Gateway — **không sửa số dư trực tiếp**.
4. Case chỉ chuyển **RESOLVED** sau khi sổ cái xác nhận. Nếu ghi sổ cái lỗi, decision chuyển FAILED và case quay về UNDER_REVIEW để đề xuất lại.

> Nếu ledger điều chỉnh thất bại liên tục, kiểm tra kết nối Gateway/Alliance và tra log — không tự ý sửa database.

---

## 4. Nhật ký bảo mật (`/admin/atrs-audit`)

Tra cứu mọi sự kiện bảo mật (read-only — không sửa/xóa được):

- **Bộ lọc:** loại sự kiện, kết quả, loại actor (khách/nhân viên/hệ thống), ứng dụng nguồn, đối tượng, khoảng thời gian.
- Dữ liệu hiển thị **đã loại bỏ payload nhạy cảm** — bạn thấy ai làm gì, khi nào, kết quả ra sao; không thấy nội dung bằng chứng hay mã bí mật.
- Phân trang server-side — dùng bộ lọc thay vì cuộn thủ công khi dữ liệu lớn.

---

## 5. Phân quyền ATRS (`/admin/atrs-roles`)

**Chỉ SystemAdmin** mới cấp/thu hồi quyền.

### Các nhóm quyền

| Quyền | Tác dụng |
|---|---|
| `recovery.view` | Xem danh sách/yêu cầu khôi phục |
| `recovery.review` | Xử lý: assign, ghi chú, yêu cầu info, escalate, khuyến nghị |
| `recovery.approve` | Duyệt/từ chối yêu cầu |
| `dispute.view` | Xem khiếu nại |
| `dispute.investigate` | Điều tra, assign, transition |
| `dispute.propose_adjustment` | Đề xuất điều chỉnh điểm |
| `dispute.approve_adjustment` | Duyệt điều chỉnh (maker-checker) |
| `case.assign` | Gán case cho nhân viên |
| `audit.read` | Xem nhật ký bảo mật |
| `policy.manage` | Đề xuất/sửa chính sách bảo mật |
| `policy.approve` | Duyệt chính sách (maker-checker) |

### Cấp quyền

1. Chọn nhân viên từ danh sách.
2. Chọn quyền + phạm vi (tenant) + thời hạn (nếu cần).
3. Xác nhận — mọi thay đổi đều được ghi log.

> **Lưu ý:** Tài khoản **SystemAdmin** có toàn quyền (break-glass) và không thể bị thu hồi quyền qua trang này — đây là cơ chế khẩn cấp có chủ đích.

---

## 6. Chính sách bảo mật (`/admin/atrs-policy`)

Quản lý các tham số bảo mật **có version + maker-checker**:

- **Đọc:** xem mọi phiên bản policy của từng key (recovery SLA hours, dispute maker-checker...).
- **Đề xuất** (`policy.manage`): tạo version mới — version đầu tiên của key mới tự có hiệu lực; thay đổi trên key đã có sẽ ở trạng thái PROPOSED chờ duyệt.
- **Duyệt/Từ chối** (`policy.approve`): approver phải khác proposer — tự duyệt bị từ chối.

> Chỉ sửa policy khi hiểu rõ tác động: ví dụ tăng `recovery.sla_hours` sẽ làm giảm số cảnh báo quá hạn SLA trên dashboard.

---

## 7. Xử lý tình huống thường gặp

| Tình huống | Cách xử lý |
|---|---|
| Khách báo không nhận thông báo | Kiểm tra `/admin/atrs-audit` xem sự kiện đã phát chưa; hiện chỉ có thông báo trong ứng dụng (chưa email/SMS) |
| Yêu cầu khôi phục kẹt PENDING_USER_ACTION | Khách chưa nhập mã — nhắc khách kiểm tra thông báo/trang trạng thái |
| Dispute decision FAILED | Kiểm tra log Gateway/ledger; đề xuất decision mới (key idempotency mới) |
| Không thấy menu ATRS | Kiểm tra role (Owner/SystemAdmin) + quyền grant trong `/admin/atrs-roles` |
| Nghi tài khoản bị compromise | Ưu tiên `recovery.review` → approve → hệ thống tự thu hồi token/thiết bị cũ |
