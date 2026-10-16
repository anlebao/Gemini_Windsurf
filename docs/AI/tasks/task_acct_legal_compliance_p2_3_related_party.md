# TASK CARD: P2.3 — GIAO DỊCH LIÊN KẾT (NĐ 255/2026) — KHẢO SÁT + MODULE TÙY CHỌN

> Created: 2026-10-16 (user directive master plan P0-P2)
> Master plan: `docs/AI/plans/accounting-legal-compliance-master-plan.md` (Phase P2.3)
> Branch: `main`
> Status: ✅ **Q4 CHỐT (2026-10-16): chỉ TÀI LIỆU khảo sát — module code chỉ khi có khách hàng có quan hệ liên kết**

---

## 1. GOAL & CONTEXT

- **Mục tiêu:** đóng lỗ hổng checklist **5.2** — theo dõi và kê khai **giao dịch liên kết** theo **Nghị định 255/2026/NĐ-CP** (thay NĐ 132/2020 + 20/2025; hiệu lực 01/07/2026) — dành cho doanh nghiệp có quan hệ liên kết (không áp dụng HKD/DNNVV thông thường).
- **Phát hiện review (2026-10-16):** không có bất kỳ module giao dịch liên kết nào (chỉ `CongNoService` 331 + `Vendor`).
- **Lưu ý:** phần lớn khách hàng hiện tại là HKD/DNNVV — nhu cầu thấp → phase này = **khảo sát + tài liệu**, code chỉ khi có yêu cầu.

## 2. ACTIVE WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` — Wave 5 (hoặc defer).
- **Execution Mode:** REVIEW_ONLY/IMPLEMENT tùy quyết định user.

## 3. SCOPE (khảo sát trước)

### 3.1 Khảo sát
- [ ] Tài liệu tóm tắt NĐ 255/2026: đối tượng áp dụng · xác định quan hệ liên kết · nghĩa vụ kê khai (tờ khai thông tin giao dịch liên kết — mẫu theo quy định) · hồ sơ xác định giá giao dịch liên kết · thời hạn nộp (cùng quyết toán TNDN).
- [ ] Đánh giá đối tượng khách hàng Vạn An (HKD/DNNVV/HTX — phần lớn ngoài phạm vi; DN lớn TT 99/2025 mới có thể thuộc diện).

### 3.2 Module (CHỈ khi user xác nhận)
- [ ] Entity theo dõi bên liên kết (tên, MST, loại quan hệ — theo tiêu chí NĐ 255/2026) + giao dịch liên kết (đối tượng, giá trị, hình thức).
- [ ] Báo cáo + tờ khai (mẫu) + hồ sơ xác định giá (tài liệu đính kèm).
- [ ] Multi-tenancy + migration + UI + tests + E2E.

## 4. DECISIONS (cần user)

- Có khách hàng thuộc diện giao dịch liên kết không? → CÓ: làm module; KHÔNG: chỉ tài liệu khảo sát (defer code).

## 5. CONSTRAINTS (governance)

- Nếu code: entity mới qua workflow chuẩn (Single-Identity) · không đụng AccountingEntry · Multi-tenancy · UI Platform 100% · guard + build + tests MUST PASS.

## 6. SUCCESS CRITERIA

- [ ] (Luôn) Tài liệu khảo sát NĐ 255/2026 + đánh giá áp dụng
- [ ] (Nếu duyệt) Module theo dõi + kê khai giao dịch liên kết + tests + deploy

## 7. DEFERRED

- Tích hợp tự động lấy tờ khai từ phần mềm thuế · xác định giá tự động (benchmark) — ngoài phạm vi