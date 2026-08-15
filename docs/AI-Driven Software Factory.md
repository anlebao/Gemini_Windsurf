# ShopERP - AI-Driven Software Factory Roadmap (Devin & VanAn EcoSystem Edition)

> **Version:** 2.0 (Migration from Windsurf to Devin Engine)  
> **Date:** June 17, 2026  
> **Status:** Draft - Ready for Execution  
> **Target:** Biến ShopERP thành Nhà máy Phần mềm Tự trị (Software Factory) dưới sự điều phối của Devin Desktop/Cloud, tuân thủ nghiêm ngặt tiêu chuẩn kỹ thuật Vạn An và pháp luật Thuế/Kế toán Việt Nam 2026.

---

## I. KIẾN TRÚC QUẢN TRỊ DEVIN (.devinrules)

Để triệt tiêu hành vi "ném xúc xắc" (thử-sai mù quáng) của Devin trên hệ thống nghiệp vụ nhạy cảm, dự án thiết lập cấu hình chặn cứng (Hard Gates) sau:

*   **Chế độ thực thi mặc định:** `Step-by-Step` (Yêu cầu con người phê duyệt từng lệnh Terminal, File Modification và Git Push).
*   **Giới hạn vòng lặp lỗi:** Tối đa 3 lần thử-sai (Max 3 trial-and-error loops) khi gặp lỗi biên dịch hoặc test suite. Quá 3 lần, Agent phải dừng lại và báo cáo kiến trúc.
*   **Hàng rào bảo vệ Kế toán (Accounting Hard Stops):** 
    *   Cấm sửa đổi tính bất biến (Immutability) của Sổ cái Kế toán (AccountingEntry append-only).
    *   Cấm bypass tham số `TenantId` (Multi-tenancy bắt buộc ở mọi tầng).
    *   Tuyệt đối không sinh mã Nợ/Có (Double-entry) đối với đối tượng Tenant thuộc nhóm Hộ kinh doanh.

---

## II. MA TRẬN CẤU HÌNH HỆ THỐNG KẾ TOÁN (ÁP DỤNG TỪ 2026)

Hệ thống Core Hub (`3_CoreHub/`) và Giao diện (`5_WebApps/`) phải định tuyến luồng xử lý tự động theo ma trận thực tế sau:

| Đối tượng Tenant | Chế độ kế toán áp dụng từ 2026 | Ghi chú lập trình hệ thống |
| :--- | :--- | :--- |
| **Corporation Large** | **TT 99/2025/TT-BTC** | Mở cấu hình cho phép User tự chỉnh sửa danh mục Chart of Accounts (COA) cấp 1, 2. |
| **Corporation SME** | **TT 133/2016/TT-BTC** (Sửa đổi bởi **TT 46/2025/TT-BTC**) | Cho phép Tenant cấu hình lựa chọn chạy theo luồng SME hoặc nâng cấp lên luồng TT 99. |
| **Household Business** | **TT 152/2025/TT-BTC** | Áp dụng Single-Entry. Cập nhật lại biểu mẫu 5 loại sổ bắt buộc và logic lấy dữ liệu theo phương pháp kê khai thuế mới. |

---

## III. LỘ TRÌNH TRIỂN KHAI 5 GIAI ĐOẠN (RÚT GỌN NATIVE)

### Giai đoạn 1: Dịch chuyển Tài sản Trí tuệ (Legacy Windsurf Assets Migration)
*   **Thời gian:** Tuần 1  
*   **Mục tiêu:** Cứu vớt 35 assets cũ (12 workflows, 21 skills) từ Windsurf sang Devin để không mất ngữ cảnh dự án.
*   **Tác vụ chi tiết:**
    *   Ép Devin đọc toàn bộ `.devin/` cũ, tổng hợp logic để tự viết vào cấu trúc `.devinrules`.
    *   Tái cấu trúc thư mục tài liệu kiến trúc: `docs/decisions/` (Lưu 5 file ADRs nền tảng: SQLite+NATS Offline-First, Multi-Tenancy, Accounting Immutability, UI Platform, Playwright Isolation).
*   **Tiêu chí thành công:** File `.devinrules` cấu hình xong. Devin nhận diện được 4 Core Domains (Order, Inventory, Payment, Accounting).

### Giai đoạn 2: Đóng hòm CI/CD Pipeline & Guard Check (Automated Validation)
*   **Thời gian:** Tuần 2  
*   **Mục tiêu:** Thiết lập bộ lọc không cho code lỗi hoặc code vi phạm tiêu chuẩn kỹ thuật Vạn An lọt vào nhánh chính.
*   **Tác vụ chi tiết:**
    *   Cấu hình `.github/workflows/ci.yml` phối hợp với máy ảo đám mây của Devin.
    *   Tích hợp bộ script kiểm tra kỷ luật nghiêm ngặt `guard-check.ps1` trực tiếp vào Pipeline của GitHub, bắt buộc quét qua 3 lớp: Unit Tests, Architecture Tests (kiểm tra dependencies giữa các lớp), và Compliance Check (quét rò rỉ TenantId).
*   **Tiêu chí thành công:** Devin không thể tự ý merge Pull Request nếu GitHub Actions báo đỏ.

### Giai đoạn 3: Phát triển Module Hóa đơn Điện tử & Thuế 2026 (Feature Delivery)
*   **Thời gian:** Tuần 3 - 4  
*   **Mục tiêu:** Giao việc tự trị cho Devin xử lý các tính năng Core theo quy định Thuế mới.
*   **Tác vụ chi tiết:**
    *   Kích hoạt Devin Native GitHub để đọc Issue và tự rẽ nhánh (branch).
    *   Triển khai Module ký số và đẩy dữ liệu Hóa đơn điện tử khởi tạo từ máy tính tiền (Nghị định 123 & Thông tư 78) theo định dạng XML chuẩn của Tổng cục Thuế.
    *   Lập trình lớp `AccountingServiceFactory` để định tuyến tự động giữa TT 99, TT 46 và TT 152 dựa trên `tenant_type`.
*   **Tiêu chí thành công:** Xuất thành công file XML hóa đơn máy tính tiền thời gian thực từ POS khi checkout, truyền không đồng bộ qua NATS Broker.

### Giai đoạn 4: Thiết lập Bộ nhớ Dự án (Project Memory & Auto-Documentation)
*   **Thời gian:** Tuần 5 - 6  
*   **Mục tiêu:** Triệt tiêu bệnh "mất trí nhớ ngắn hạn" của AI. Lưu lịch sử quyết định để Agent thế hệ sau kế thừa.
*   **Tác vụ chi tiết:**
    *   Cấu hình cơ sở dữ liệu lưu trữ lịch sử Agent (`agent_history`, `tasks`, `decisions`) trên PostgreSQL.
    *   Ép Devin tự động cập nhật tài liệu `Changelog.md`, API Docs và Domain Docs mỗi khi có sự thay đổi mã nguồn được con người chấp thuận.
*   **Tiêu chí thành công:** Khi truy vấn "Tại sao chúng ta chọn hạch toán Single-entry cho TT 152?", Agent truy xuất chính xác dữ liệu lịch sử dự án để trả lời.

### Giai đoạn 5: Vận hành Nhà máy Phần mềm Tự trị (Software Factory Orchestration)
*   **Thời gian:** Dài hạn (Ongoing)  
*   **Mục tiêu:** Tối ưu hóa luồng làm việc end-to-end, giảm thiểu sự can thiệp của con người tại các tác vụ lặp đi lặp lại.
*   **Tác vụ chi tiết:**
    *   Kích hoạt toàn diện cơ chế Multi-Agent phối hợp ngầm trên Cloud của Devin: PO Agent (Phân tích Requirement thành User Story) $\rightarrow$ Architect Agent (Thiết kế Schema và ADR) $\rightarrow$ Dev Agent (Gõ code song song các module Kho, Đơn hàng) $\rightarrow$ QA Agent (Chạy Playwright E2E kiểm thử).
*   **Tiêu chí thành công:** Lập trình viên Vạn An đóng vai trò là Người phê duyệt tối cao (Reviewer) tại các cổng phê duyệt chiến lược, AI tự động vận hành các bước còn lại.

---

## IV. ĐẦU TƯ ƯU TIÊN VÀ CHI PHÍ (ROI TRONG KỶ NGUYÊN DEVIN)

| Mức độ ưu tiên | Giai đoạn | Giá trị chiến lược | Kiểm soát của Con người | ROI |
| :--- | :--- | :--- | :--- | :--- |
| **1** | Phase 1: KB Migration | 🔥🔥🔥🔥🔥 (Tối cao) | 100% (Giám sát chặt) | Rất cao |
| **2** | Phase 2: CI/CD Guard | 🔥🔥🔥🔥 (Cực cao) | 80% (Hệ thống tự chấm) | Cao |
| **3** | Phase 3: Tax/Invoice Feature | 🔥🔥🔥 (Trung bình) | 50% (Thả xích sandbox) | Khá |
| **4** | Phase 4: Project Memory | 🔥🔥🔥 (Trung bình) | 20% (AI tự động hóa) | Trung bình |

---

## V. CÁC BƯỚC HÀNH ĐỘNG KHẨN CẤP (IMMEDIATE NEXT STEPS)

- [ ] **Bước 1:** Khởi động Devin Desktop, tạo file `.devinrules` tại thư mục gốc với nội dung ở Mục I.
- [ ] **Bước 2:** Di dời thư mục `.devin/` cũ vào thư mục tạm `docs/legacy-windsurf-assets/`.
- [ ] **Bước 3:** Ra lệnh cho Devin: *"Đọc toàn bộ file cũ trong `docs/legacy-windsurf-assets/`. Biên dịch và chuyển đổi toàn bộ quy tắc, kỹ năng cũ thành cấu trúc thực thi mới của mày, đảm bảo tuyệt đối tuân thủ ma trận kế toán 2026 tại Mục II."*