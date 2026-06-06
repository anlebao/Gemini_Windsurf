# TASK CARD: [SPRINT 3] - [PHASE 5] - E-Invoice Multi-Provider Integration

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Implement E-Invoice multi-provider integration với domain models, provider interfaces, circuit breaker và outbox pattern cho hệ thống kế toán HKD.
- **Nghiệp vụ áp dụng:** Hóa đơn điện tử theo Nghị định 70/2025/NĐ-CP và Thông tư 32/2025/TT-BTC.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** .windsurf/workflows/newfeaturebuild.md
- **Execution Mode:** ANALYZE -> IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `docs/plan_MVP/RoadMap/MVP plan Account M.md` (chứa Design Reference & UI Reference)
  - `1_Shared/Domain.cs` (sẽ thêm E-Invoice domain models)
  - `3_CoreHub/Services/EInvoice/` (thư mục mới cho E-Invoice services)
- **Boundary Rules (Nghiêm cấm):**
  - CẤM đọc lại source code của Audit Trail hay Period Closing trừ khi có lỗi build/test phát sinh.
  - CẤM đọc file roadmap dài hạn trừ khi cần xác định chi tiết E-Invoice requirements.
  - CẤM chỉnh sửa Domain Layer ngoại trừ file `1_Shared/Domain.cs` khi có modeling defect công nhận.

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Domain Purity:** Domain entity không chứa EF Core, DbContext, DataAnnotations.
- [ ] **Immutability:** `AccountingEntry` phải 100% append-only, thay đổi qua Reversal Entry.
- [ ] **UI Compliance:** 100% sử dụng linh kiện chuẩn từ `UI.Platform`. Cấm viết custom HTML/CSS.
- [ ] **Legal Standards:** Nghị định 123/2020/NĐ-CP và Thông tư 78/2021/TT-BTC về hóa đơn điện tử.

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] Domain models cho E-Invoice được tạo trong `1_Shared/Domain.cs`
- [ ] Provider interfaces được định nghĩa trong `3_CoreHub/Services/EInvoice/`
- [ ] Circuit breaker pattern được implement
- [ ] Outbox pattern được implement
- [ ] Unit tests cho E-Invoice components pass
- [ ] Chạy `guard-check.ps1` đạt kết quả 0 errors

## 6. ACTIVE SKILLS (MAX 3)
- einvoice-integration
- ui-platform-compliance-review
- domain-integrity-validation

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 0
- **Verified Facts:**
  - Fact 1: File `1_Shared/Domain.cs` hiện chưa có Entity EInvoice
  - Fact 2: Thư mục `3_CoreHub/Services/EInvoice/` chưa tồn tại
  - Fact 3: RoadMap đã xác định Sprint 3 - Phase 5 với Design Reference và UI Reference
  - Fact 4: Circuit breaker pattern và outbox pattern đã được thiết kế trong plan
  - Fact 5: Legal requirements theo Nghị định 123/2020/NĐ-CP đã được xác định
- **Assumptions:** 0
- **Open Questions:** 0
- **Recommended Action:** Continue — Start Sprint 3
