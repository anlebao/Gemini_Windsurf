# VẠN AN ACCOUNTING SPRINT TRACKING PROTOCOL

## SCOPE
Áp dụng nghiêm ngặt khi người dùng gõ: `/audit-progress`, `audit-progress-sprint2`, hoặc các câu hỏi về trạng thái mạch Kế toán MVP.

## SOURCE OF TRUTH
- `docs/plan_MVP/sprint1-accounting-ui-detail-606838.md`
- `docs/plan_MVP/vanan-accounting-implementation-606838.md`

## PROTOCOL: /audit-progress
Khi người dùng gõ `/audit-progress`:
1. Đọc `Vanan_Sprint_State.md`
2. Quét workspace để xác minh các file thực sự tồn tại
3. Xuất bảng kiểm toán tiến độ:

| Task Name | Status | Verification: Code Exists? | Next Action |
|-----------|--------|---------------------------|-------------|

## PROTOCOL: audit-progress-sprint2
Khi người dùng gõ `audit-progress-sprint2`:
1. Đọc `docs/plan_MVP/sprint2-period-closing-audit-trail.md`
2. Quét workspace để xác minh các file thực sự tồn tại
3. Xuất bảng kiểm toán tiến độ Sprint 2:

| Task Name | Status | Verification: Code Exists? | Next Action |
|-----------|--------|---------------------------|-------------|

4. Báo cáo % hoàn thành tổng thể và trạng thái ngày hiện tại

## ISOLATION RULES
- File này chứa toàn bộ giao thức quét tiến độ kế toán
- Khi kết thúc Sprint 2 và chuyển sang Sprint 3, chỉ cần sửa hoặc xóa file cục bộ này
- Không động chạm vào xương sống của hệ thống quy tắc toàn cục (.windsurf/rules/.windsurfrules)
