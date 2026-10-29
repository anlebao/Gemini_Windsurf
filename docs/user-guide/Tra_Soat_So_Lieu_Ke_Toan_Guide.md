# Hướng dẫn sử dụng — Tra soát số liệu kế toán

> **Đối tượng:** SystemAdmin (quản trị hệ thống)
> **Đường dẫn:** `https://app2.khachvip.online/admin/reconcile`
> **Phiên bản:** v1.0 — deploy 2026-10-29 (SRS `van_an_tra_soat_so_lieu_ke_toan_srs_v1.md`)

---

## 1. Tính năng là gì?

**Tra soát số liệu kế toán** chạy **12 kiểm tra tự động (R1–R12)** trên dữ liệu kế toán của một tenant trong một kỳ (tháng), nhằm phát hiện sớm:

- Bút toán nhật ký **mất cân đối** Nợ/Có.
- Doanh thu/chi phí **lệch** giữa journal và số liệu trên màn hình.
- Tài sản ≠ Nợ phải trả + Vốn + Lãi (sai phương trình kế toán).
- Đơn hàng đã thu tiền nhưng **thiếu/trùng** bộ bút toán.
- Số liệu trên ShopERP (mirror SQLite) **lệch** với nguồn gốc PostgreSQL.
- Ngày nghiệp vụ (TransactionDate) bị hỏng (`01-01-0001`, `-infinity`).

**Nguyên tắc quan trọng:**

- ✅ **Chỉ đọc (read-only)** — không sửa, không tạo bút toán nào.
- ✅ **Chỉ gợi ý** — mỗi vấn đề kèm gợi ý khắc phục; việc sửa do kế toán/admin thực hiện thủ công.
- ✅ **Chỉ SystemAdmin** — Owner/kế toán tenant không thấy trang này.
- ✅ **Không lưu lịch sử** — mỗi lần chạy tính lại từ đầu; mọi lần chạy được ghi audit log (tenant, kỳ, người chạy, tóm tắt).

---

## 2. Truy cập trang

Có 3 cách vào:

1. **NavMenu:** đăng nhập SystemAdmin → menu trái → **Cấu hình & thiết lập** → **Tra soát số liệu kế toán**.
2. **Sitemap:** `/sitemap` → nhóm admin → card **🧮 Tra soát số liệu**.
3. **Trực tiếp:** gõ URL `https://app2.khachvip.online/admin/reconcile`.

> Nếu đăng nhập bằng tài khoản không phải SystemAdmin → trang trả về **403 Access Denied**.

---

## 3. Cách chạy tra soát

| Bước | Thao tác |
|---|---|
| 1 | Dropdown **Tenant** → chọn tenant cần kiểm tra (ví dụ: Cafe Tân Quy, Test HKD). |
| 2 | Ô **Kỳ (YYYY-MM)** → nhập tháng, ví dụ `2026-10`. |
| 3 | Bấm nút **Tra soát**. |
| 4 | Chờ 1–3 giây — hệ thống chạy tuần tự 13 mục kiểm tra. |
| 5 | Đọc kết quả (mục 4). Muốn chạy lại → bấm **Chạy lại**. |

> Mỗi check chạy độc lập — một check lỗi không làm hỏng các check còn lại.

---

## 4. Đọc kết quả

### 4.1 Bốn thẻ tóm tắt

| Thẻ | Ý nghĩa |
|---|---|
| **Tổng check** | Tổng số mục đã chạy (13: R1–R12 + các check thông tin). |
| **Pass** | Xanh lá — số check hoàn toàn đúng. |
| **Warning** | Vàng — có dấu hiệu bất thường cần xem xét (ví dụ: tài khoản 9xx/911, account malformed). |
| **Error** | Đỏ — sai dữ liệu rõ ràng cần xử lý. |

### 4.2 Bảng chi tiết từng check

Mỗi dòng: **Mã** · **Tên check** · **Mức** (badge màu) · **Số liệu** · **Chi tiết** (bấm mở rộng để xem danh sách ID/journal/account cụ thể).

Các mức:

| Badge | Ý nghĩa |
|---|---|
| 🟩 **Pass** | Đúng — không cần làm gì. |
| 🟦 **Info** | Thông tin tham khảo (ví dụ: tồn cọc 3387, check không áp dụng cho loại tenant). |
| 🟨 **Warning** | Nghi vấn — cần người xem xét quyết định (không phải lỗi chắc chắn). |
| 🟥 **Error** | Sai chắc chắn — cần hành động khắc phục. |

### 4.3 Danh sách gợi ý

Cuối trang (nếu có vấn đề) hiện khối **Gợi ý khắc phục** — mỗi gợi ý mô tả nguyên nhân + hướng xử lý thủ công. Hệ thống **không tự sửa** dữ liệu.

---

## 5. Ý nghĩa 13 mục kiểm tra

| Mã | Check | Phát hiện | Mức khi bất thường |
|---|---|---|---|
| **R1** | Journal cân bằng | Bút toán có ΣNợ ≠ ΣCó | Error |
| **R2** | Trial balance kỳ | Tổng Nợ ≠ tổng Có toàn kỳ | Error |
| **R3** | Doanh thu/Chi phí | Journal 5xx-8xx lệch số liệu dashboard (đã gồm VAT 3331 + offset cọc) | Error |
| **R4** | Phương trình kế toán | Tài sản ≠ Nợ + Vốn + Lãi · account ngoài prefix 1-8 (9xx/911, malformed, rỗng) | Error / Warning (9xx, malformed) |
| **R5** | Sổ cái | Dư đầu → phát sinh → dư cuối các TK trọng yếu (tham khảo) | Info |
| **R6** | Nhật ký chung | Đếm + tổng bút toán kỳ (đối chiếu R2) | Pass/Error |
| **R7** | Sổ HKD | Tenant HKD: books S1a/S2b/S3a khớp doanh thu journal | Error (Info nếu tenant không HKD) |
| **R8a** | Cầu nối nguồn đơn | Journal ref `Order` còn tồn tại sau ngày chuyển đổi 2026-10-29 | Error |
| **R8b** | Bộ bút toán đơn | Mỗi đơn paid có đúng 1 bộ journal (2–3 bút toán) | Error |
| **R9** | Cọc 3387 | Cọc đã thu chưa đối trừ (residual ≠ 0) | Info |
| **R10** | AccountingEntry đơn | Đơn paid thiếu bút toán AccountingEntry | Error |
| **R11** | Mirror ShopERP | Đơn trên SQLite mirror lệch PG (sai tổng tiền/trạng thái) | Warning (Info nếu không gọi được mirror) |
| **R12** | Ngày nghiệp vụ | `TransactionDate` hỏng: `01-01-0001` / `-infinity` (quét toàn lịch sử, không chỉ kỳ chọn) | Warning |

**Ví dụ kết quả thực tế (production 2026-10):**

- Cafe Tân Quy: `10 Pass · 3 Info · 0 Warning · 0 Error` — dữ liệu sạch.
- Test HKD: `11 Pass · 2 Info · 0 Warning · 0 Error` — Info R9 báo còn tồn cọc 3387 = −20,000đ.

---

## 6. Lỗi thường gặp & cách xử lý

| Triệu chứng | Nguyên nhân | Xử lý |
|---|---|---|
| Trang 403 | Tài khoản không phải SystemAdmin | Đăng nhập sysadmin |
| Alert "Chọn tenant trước" | Chưa chọn tenant | Chọn tenant trong dropdown |
| Alert "Nhập kỳ dạng YYYY-MM" | Kỳ sai định dạng | Nhập vd `2026-10` (không `10-2026`) |
| API 400 "tenantId là bắt buộc" | Thiếu `tenantId` trên URL API | Truyền `?tenantId={guid}` |
| API 404 "Tenant không tồn tại" | GUID sai / tenant đã xóa | Kiểm tra lại tenant ID |
| R11 báo Info "không chạy được" | ShopERP mirror không liên lạc được / chưa cấu hình API key | Kiểm tra ShopERP VPS + `InternalLoyalty:ApiKey` 2 phía khớp nhau; các check khác vẫn chạy bình thường |
| R12 Warning | Entries có `TransactionDate` hỏng từ trước | Chạy backfill ngày theo gợi ý trong phần Chi tiết |

---

## 7. FAQ

**Hỏi:** Tra soát có sửa dữ liệu không?
**Đáp:** Không — hoàn toàn read-only. Chỉ đọc và gợi ý.

**Hỏi:** Có lưu lịch sử các lần chạy không?
**Đáp:** Không lưu report; chỉ ghi audit log (ai, tenant nào, kỳ nào, tóm tắt) trong Audit Trail.

**Hỏi:** Chạy bao lâu?
**Đáp:** 1–3 giây/tenant·kỳ (production đo ~1.6s cho 12+ journal, ~0.25s cho 34 journal).

**Hỏi:** Kỳ nào cũng chạy được?
**Đáp:** Có — chọn bất kỳ `YYYY-MM` nào; R12 luôn quét toàn lịch sử ngày hỏng.

**Hỏi:** R11 khác các check thế nào?
**Đáp:** R11 gọi HTTP nội bộ Gateway → ShopERP (SQLite mirror) so sánh đơn — là check duy nhất xuyên 2 store; lỗi mạng chỉ hạ xuống Info, không fail cả bộ.

**Hỏi:** Kế toán tenant xem được không?
**Đáp:** Không — chỉ SystemAdmin (chính sách G9). Kết quả cần chia sẻ → chụp màn hình hoặc xuất từ audit log.

---

*Tài liệu tham chiếu: SRS `docs/requirements/van_an_tra_soat_so_lieu_ke_toan_srs_v1.md` · task card `docs/AI/tasks/task_tra_soat_ke_toan.md` · script đối chiếu `6_Testing/rv-scripts/accounting-balance-rv.sh`.*
