# TASK CARD: [SPRINT X] - [PHASE Y] - [TASK NAME]

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** [Mô tả ngắn gọn kết quả cuối cùng cần đạt được trong 1-2 câu]
- **Nghiệp vụ áp dụng:** [Ví dụ: Kế toán HKD theo TT 152/2025/TT-BTC / Hóa đơn điện tử máy tính tiền]

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** [Tên file workflow bắt buộc, ví dụ: .windsurf/workflows/newfeaturebuild.md]
- **Execution Mode:** [ANALYZE -> IMPLEMENT / FIX_ONLY / REVIEW_ONLY]

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - [Đường dẫn file 1]
  - [Đường dẫn file 2]
- **Boundary Rules (Nghiêm cấm):**
  - CẤM đọc lại các module không liên quan để tránh phình context window.
  - CẤM chỉnh sửa Domain Layer ngoại trừ file `1_Shared/Domain.cs` khi có modeling defect công nhận.

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Domain Purity:** Domain entity không chứa EF Core, DbContext, DataAnnotations.
- [ ] **Immutability:** [Nếu liên quan kế toán] `AccountingEntry` phải 100% append-only, thay đổi qua Reversal Entry.
- [ ] **UI Compliance:** 100% sử dụng linh kiện chuẩn từ `UI.Platform`. Cấm viết custom HTML/CSS.
- [ ] **Legal Standards:** [Ghi rõ thông tư/luật định Việt Nam cập nhật đến 2026 áp dụng cho task này].

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] [Tiêu chí 1: Ví dụ - Định nghĩa xong Struct XML Hóa đơn theo chuẩn TCT]
- [ ] [Tiêu chí 2: Ví dụ - Viết bổ sung Integration Test và chạy pass]
- [ ] [Tiêu chí 3: Ví dụ - Chạy `guard-check.ps1` đạt kết quả 0 errors]

## 6. ACTIVE SKILLS (MAX 3)
- [Skill 1]
- [Skill 2]

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** [Số lượng log lỗi hoặc bằng chứng trực quan từ compile/test]
- **Verified Facts:**
  - Fact 1: [Ghi rõ sự thật 1, ví dụ: File Domain.cs hiện chưa có Entity EInvoice]
  - Fact 2: [Ghi rõ sự thật 2]
- **Assumptions:** [Các giả định chưa được chứng thực thông qua đọc code/chạy lệnh]
- **Open Questions:** [Các câu hỏi cần User làm rõ trước khi code]
- **Recommended Action:** [Continue / Investigate (Nếu Assumptions >= Facts hoặc Open Questions >= 3)]