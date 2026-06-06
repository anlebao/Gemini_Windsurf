# DETAIL PLAN: REFACTORING AI CONTEXT SYSTEM TO ACS VIA WINDSURF

## PHASE 1: KHỞI TẠO BỘ NHỚ KIẾN TRÚC TĨNH (STATIC MEMORY)

**Mục tiêu:** Di dời toàn bộ tri thức không đổi ra khỏi file trạng thái để AI không phải đọc lại mỗi turn.

### Bước 1.1: Tạo file Bộ nhớ Kiến trúc (architecture_memory.md)

**Hành động trên Windsurf:** Mở terminal hoặc dùng phím tắt tạo file mới tại đường dẫn `docs/AI/architecture_memory.md`.

**Nội dung nạp vào:** Cắt toàn bộ nội dung tĩnh từ file `project_state.md` hiện tại sang, bao gồm:
1. Project Overview (Tổng quan dự án)
2. Architecture Decisions (Quyết định kiến trúc: Immutable AccountingEntry, Reversal Entry, Multi-tenancy...)
3. Important Files (Danh mục file quan trọng)

**Tiêu chuẩn kiểm duyệt:** File này sau khi tạo xong sẽ đóng lại, tuyệt đối không ra lệnh cho Windsurf đọc mặc định ở đầu phiên chat mới.

### Bước 1.2: Tạo Nhật ký Điều tra (investigation_log.md)

**Hành động trên Windsurf:** Tạo file `docs/AI/investigation_log.md`.

**Nội dung nạp vào:** Di chuyển toàn bộ lịch sử phân tích lỗi (Root Cause Analysis) từ `project_state.md` sang. Cụ thể là:
- Vấn đề CI Unit Test với ShopERP.Tests DLL
- Vấn đề SQLite integration tests với lỗi no such table: Orders

**Tiêu chuẩn kiểm duyệt:** Định dạng theo cấu trúc Append-only (mỗi lỗi đã giải quyết gọn trong 1 block gồm: Issue, Evidence, Root Cause, Fix, Status).

---

## PHASE 2: THU GỌN VÀ ĐỊNH LƯỢNG HÓA FILE TRẠNG THÁI (DYNAMIC STATE)

**Mục tiêu:** Ép dung lượng `project_state.md` xuống dưới 60 dòng và cài đặt bộ metric cứng để kiểm soát độ "ngáo" của AI.

### Bước 2.1: Gọt giũa docs/AI/project_state.md

**Hành động trên Windsurf:** Mở file `project_state.md`, xóa bỏ hoàn toàn các phần đã di dời ở Phase 1.

**Thu gọn mục Completed:** Thay vì liệt kê chi tiết từng file sửa đổi ở các layer (Domain, Infrastructure, Application...), nén lại thành 1 dòng duy nhất:
```markdown
* Phase 2.9.4 Audit Trail: Toàn bộ các layer (Domain, Infra, App, Gateway, UI, E2E tests) đã implement xong và đã merge vào main
```

**Cấu trúc lại mục `AI Health Check`:** Thay thế các điểm số phần trăm tự phong (`Understanding Level: 95%`) bằng ma trận định lượng logic:
```markdown
## 10. AI Health Check Matrix
* Evidence Count: 3
* Verified Facts: 8
* Assumptions: 0
* Open Questions: 0
* Recommended Action: Continue — Create PR
```

### Bước 2.2: Thiết lập Luật Cứng tại .windsurfrules

**Hành động trên Windsurf:** Mở file `.windsurf/rules/.windsurfrules` (hoặc vị trí cấu hình rules của anh).

**Thêm quy tắc chặn dòng (Gate Rules):**
```plaintext
- Tuyệt đối nghiêm cấm sửa code nếu số lượng Assumptions >= Verified Facts hoặc số lượng Open Questions >= 3. Sẽ tự động chuyển sang chế độ điều tra (Investigate).
- Cấm đọc các file Roadmap hoặc Tài liệu kiến trúc trừ khi nhiệm vụ trong current_task.md yêu cầu đích danh.
```

---

## PHASE 3: TRIỂN KHAI LAYER CÔ LẬP NHIỆM VỤ (TASK ISOLATION)

**Mục tiêu:** Tạo ra "vòng kim cô" cho từng phiên làm việc nhỏ, ngăn chặn việc AI đọc lan man làm phình ngữ cảnh.

### Bước 3.1: Thiết lập thư mục và mẫu Task Card

**Hành động trên Windsurf:**
- Tạo folder `docs/AI/tasks/`
- Tạo file nhiệm vụ hiện tại: Tạo file `docs/AI/tasks/task_next_phase.md` phục vụ cho phase tiếp theo

**Nội dung file Task Card:**
```markdown
# Task Card: [Tên Task]

## 1. Goal
* [Mục tiêu cụ thể]

## 2. Relevant Files
* [Danh sách file liên quan]

## 3. Boundary Rules
* [Quy tắc giới hạn scope - không đọc file ngoài scope]

## 4. Success Criteria
* [Tiêu chí thành công]
```

---

## PHASE 4: ĐỒNG BỘ VÀ CẬP NHẬT TÀI LIỆU HƯỚNG DẪN CHÍNH

**Mục tiêu:** Khóa quy trình ACS vào tài liệu tổng để đảm bảo tính nhất quán cho các phiên chat sau.

### Bước 4.1: Cập nhật docs/AI/AI_WORKFLOW_GUIDE.md

**Hành động trên Windsurf:** Sửa đổi tài liệu `AI_WORKFLOW_GUIDE.md` để cập nhật kiến trúc 4 tầng mới (bổ sung bộ đôi State-Architecture ở Tầng 2 và bổ sung Task Card ở Tầng 3) theo đúng giải pháp ACS đã thống nhất.

**Ghi nhận rõ quy trình bắt đầu một chat mới:** Thay vì prompt bắt AI đọc cả đống file nền tảng, prompt chuẩn hóa mới sẽ là:
```plaintext
"Đọc docs/AI/project_state.md và docs/AI/tasks/task_XXXX.md. Xác nhận ma trận Health Check và thực thi nghiêm ngặt theo boundary quy định."
```

---

## PHASE 5: ĐÁNH GIÁ HIỆU QUẢ TIẾT KIỆM TOKEN (BENCHMARK)

**Hành động thực tế:** Sau khi hoàn thành việc chia tách file, anh mở một chat mới hoàn toàn trên Windsurf.

**Thực thi lệnh Prompt đầu tiên:**
> *"Đọc docs/AI/project_state.md và docs/AI/tasks/task_next_phase.md. Hãy cho biết hành động tiếp theo theo đúng ma trận Health Check."*

**Kiểm tra:** Kiểm tra số lượng token tiêu hao ở turn đầu tiên. Mục tiêu là tổng số lượng token nạp vào cho turn đầu tiên phải **giảm ít nhất 70%** so với việc nạp file `project_state.md` cồng kềnh cũ kết hợp với việc đọc roadmap.

---

## Kế hoạch hành động ngay lập tức (Next Action):

1. Sửa encoding file plan-toi-uu-context-token.md (UTF-8, format markdown) ✅
2. Cập nhật task card obsolete (thay task_verify_audit_trail bằng task mới cho phase tiếp theo)
3. Thêm Next Action cụ thể vào cuối file plan ✅
4. Bắt đầu Phase 1: Tạo architecture_memory.md
5. Bắt đầu Phase 1.2: Tạo investigation_log.md
6. Bắt đầu Phase 2: Gọt giũa project_state.md
7. Bắt đầu Phase 2.2: Cập nhật .windsurfrules
8. Bắt đầu Phase 3: Tạo docs/AI/tasks/ và task card mới
9. Bắt đầu Phase 4: Cập nhật AI_WORKFLOW_GUIDE.md
10. Bắt đầu Phase 5: Benchmark token usage
