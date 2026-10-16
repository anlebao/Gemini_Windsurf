# TASK CARD: P1.5 — CẬP NHẬT CHECKLIST CHẾ ĐỘ KẾ TOÁN (TT 152/2025, TT 58/2026) + QUY CHẾ SỬ DỤNG PMKT

> Created: 2026-10-16 (user directive master plan P0-P2)
> Master plan: `docs/AI/plans/accounting-legal-compliance-master-plan.md` (Phase P1.5)
> Branch: `main`
> Status: 📋 PLANNED

---

## 1. GOAL & CONTEXT

- **Mục tiêu:** (1) cập nhật **checklist kiểm soát** (`C:\Users\lebao\Downloads\checklist_kiem_soat_phan_mem_ke_toan_erp_UPDATED_2026.md`) bổ sung 2 chế độ kế toán mà code ĐÃ hỗ trợ nhưng checklist CHƯA có: **TT 152/2025/TT-BTC (HKD)** và **TT 58/2026**; (2) ban hành **Quy chế sử dụng phần mềm kế toán** (checklist 1.5 — yêu cầu TT 103/2005) dưới dạng tài liệu chuẩn trong repo.
- **Phát hiện review (2026-10-16):** code hỗ trợ `AccountingStandard { TT99_2025, TT133_2016, TT58_2026, TT71_2024 }` (`Domain.cs:4208`) + HKD theo TT 152/2025 (7 loại sổ — `Domain.cs:57`) — **checklist hiện chỉ ghi TT 99/2025, TT 133/2016, TT 132/2018** → thiếu TT 152/2025 (HKD) + TT 58/2026.
- **Đã xác minh code:** `ShopErpMenuService.cs:198-204` ("Sổ HKD (TT 152)") · `AccountChartSeeder.cs` (seed tài khoản) · `docs/user-guide/Accounting_Guide.md` (tài liệu kế toán hiện có).

## 2. ACTIVE WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` — Wave 4 (docs).
- **Execution Mode:** IMPLEMENT (docs) — hạng mục nhẹ, không code nghiệp vụ (trừ khi xác minh TT 58/2026 cần cấu hình).

## 3. SCOPE

### 3.1 Cập nhật checklist (file Downloads + bản copy trong repo nếu có)
- [ ] Bảng §0.1 + §1.3: thêm **TT 152/2025/TT-BTC** (chế độ kế toán HKD — 7 loại sổ, S2a/S2b, cash-basis) + **TT 58/2026** (xác minh phạm vi: DNNVV/siêu nhỏ?) với ngày hiệu lực.
- [ ] Mục mới **1.5 Quy chế sử dụng phần mềm kế toán** + mục 3.3 lưu trữ: nhấn mạnh thời hạn 5/10 năm.
- [ ] Cập nhật dẫn chiếu pháp lý section 2 (HĐĐT) — đã đúng NĐ 254/2026/TT 91/2026.

### 3.2 Quy chế sử dụng phần mềm kế toán (template docs)
- [ ] Tạo `docs/legal/Quy_che_su_dung_phan_mem_ke_toan_TEMPLATE.md`: nội dung theo TT 103/2005 — quản lý máy chủ · quản lý dữ liệu · kiểm soát nhập liệu từ ngoài · sao lưu định kỳ (2 nơi độc lập) · phân quyền máy nhập/xử lý · phân định chức năng bộ máy kế toán · trách nhiệm Kế toán trưởng/người đại diện (Điều 28.2 TT 99/2025).
- [ ] (Tùy chọn) Màn hình hiển thị/đường dẫn tải quy chế trong Admin (nếu user yêu cầu).

### 3.3 Xác minh TT 58/2026 (khảo sát — không code trừ khi cần)
- [ ] Tra cứu văn bản TT 58/2026/TT-BTC (số hiệu/ngày ban hành/phạm vi — DNNVV hay siêu nhỏ) → ghi chú vào checklist + master plan; nếu là chế độ mới cần cấu hình tài khoản → tách task riêng.

## 4. DECISIONS

- Mặc định: tài liệu chuẩn `docs/legal/`; không code UI trừ khi user yêu cầu hiển thị quy chế trong sản phẩm.

## 5. CONSTRAINTS (governance)

- Không đụng Domain.cs · không migration (chỉ docs + xác minh) · nếu TT 58/2026 cần hỗ trợ chế độ kế toán mới → Gate 5/approval riêng.

## 6. SUCCESS CRITERIA (DoD)

- [ ] Checklist §0.1/§1.3 bổ sung TT 152/2025 + TT 58/2026 (sau xác minh)
- [ ] Quy chế sử dụng PMKT template hoàn tất trong `docs/legal/`
- [ ] (nếu có) ghi chú TT 58/2026 phạm vi áp dụng
- [ ] Đóng task sau review docs

## 7. DEFERRED

- Hiển thị quy chế trong UI Admin · cấu hình hệ thống tài khoản cho TT 58/2026 (nếu chưa có)