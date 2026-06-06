# Hướng Dẫn Sử Dụng AI Context System

## Mục tiêu

Giúp AI làm việc hiệu quả qua nhiều phiên chat mà không mất context, không hallucinate, không sửa sai hướng.

---

## Tổng quan hệ thống 4 tầng (Updated ACS Architecture)

```
┌─────────────────────────────────────────────────────────────┐
│  Tầng 1 — Always-on Rules (.windsurfrules)                  │
│  Luôn inject vào mọi turn. Giữ ngắn < 500 tokens.          │
│  Bao gồm cả AI Context Gate Rules (ACS)                      │
├─────────────────────────────────────────────────────────────┤
│  Tầng 2 — State-Architecture Dual Layer                      │
│  • Session State (docs/AI/project_state.md)                 │
│    Đọc 1 lần đầu mỗi chat mới. Giữ < 60 dòng.              │
│  • Static Memory (docs/AI/architecture_memory.md)            │
│    Chỉ đọc khi cần kiến trúc tĩnh. Không đọc mặc định.      │
│  • Investigation Log (docs/AI/investigation_log.md)          │
│    Chỉ đọc khi tra cứu lỗi cũ. Append-only.                 │
├─────────────────────────────────────────────────────────────┤
│  Tầng 3 — Task Isolation Layer (docs/AI/tasks/)             │
│  • Task Cards với boundary rules                            │
│  • Relevant files giới hạn scope                             │
│  • Success criteria rõ ràng                                 │
├─────────────────────────────────────────────────────────────┤
│  Tầng 4 — On-demand Docs & Workflows                        │
│  • Roadmap, plan, fix plans (chỉ đọc khi cần)               │
│  • Triggered Workflows (.windsurf/workflows/)                │
└─────────────────────────────────────────────────────────────┘
```

---

## Tầng 1 — Always-on Rules (`.windsurf/rules/.windsurfrules`)

### Mục đích

Ghi các quy tắc **bất biến, áp dụng toàn bộ dự án**, cần AI tuân thủ trong mọi turn.

### Nguyên tắc viết

- Ngắn gọn, mỗi rule 1 dòng.
- Chỉ ghi những gì **thực sự không được vi phạm**.
- Không ghi lịch sử, không ghi trạng thái.
- Mục tiêu: < 500 tokens.

### Ví dụ rules nên có cho dự án này

```
- AccountingEntry is immutable. Corrections and reopening use Reversal Entry only. Never update or delete.
- Every repository query must filter by TenantId. No exceptions.
- New domain entities go in 1_Shared/Domain.cs only.
- UI: Use VanAButton, VanACard, VanAAlert, VanAModal, VanALayout. No custom HTML/CSS.
- Build gate: dotnet build VanAn.sln + guard-check.ps1 must pass after every change.
- Root Cause Confidence < 60%: investigate only, do not write code.
- Context Quality = Low: update project_state.md and start a new chat.
```

### Những gì KHÔNG nên ghi vào rules

- Trạng thái sprint hiện tại (để trong `project_state.md`).
- Lịch sử sửa lỗi (để trong fix plan docs).
- Roadmap (để trong `MVP plan Account M.md`).

---

## Tầng 2 — Session State (`docs/AI/project_state.md`)

### Mục đích

Là **nguồn sự thật duy nhất cho trạng thái làm việc hiện tại của AI**. Cho phép khôi phục context khi mở chat mới mà không cần đọc lại toàn bộ lịch sử hội thoại.

### Cấu trúc bắt buộc

```
1. Project Overview       — tổng quan cố định
2. Current Objective      — DUY NHẤT 1 mục tiêu
3. Current Status         — Completed / In Progress / Blocked
4. Root Cause Analysis    — Problem / Symptoms / Verified Facts / Assumptions /
                            Most Likely Root Cause / Rejected Hypotheses
5. Architecture Decisions — các quyết định kiến trúc đã chốt
6. Coding Plan            — roadmap hiện tại theo phase
7. Known Risks            — Risk / Impact / Mitigation
8. Important Files        — File Path / Purpose
9. Next Actions           — tối đa 5 hành động cụ thể
10. AI Health Check       — Understanding % / Root Cause Confidence % /
                            Unverified Assumptions / Context Quality / Recommended Action
```

### Quy tắc sử dụng

**Đọc:** Yêu cầu AI đọc file này ở đầu mỗi chat mới, không đọc lại nhiều lần trong cùng 1 chat.

**Cập nhật:** Cập nhật khi xảy ra một trong các sự kiện sau:
- Root cause được xác nhận.
- Test pass/fail thay đổi.
- Current Objective hoàn thành hoặc thay đổi.
- Architecture decision mới được chốt.
- Phát hiện risk mới quan trọng.
- Trước khi kết thúc phiên làm việc.
- Trước khi mở chat mới.

**Không cập nhật khi:** Chỉ đọc file, grep code, hoặc thảo luận mà chưa có kết quả cụ thể.

### Quy tắc AI Health Check

| Trạng thái | Hành động cho phép |
|---|---|
| Recommended Action = Continue Coding | Được viết code |
| Recommended Action = Investigate Further | Chỉ đọc file, grep, chạy test |
| Recommended Action = Generate New Plan | Lập lại plan, không code |
| Recommended Action = Start New Chat | Cập nhật state, mở chat mới |
| Root Cause Confidence < 60% | Chỉ điều tra, tuyệt đối không sửa code |
| Context Quality = Low | Cập nhật state, mở chat mới |

### Giữ file ngắn

- Mục tiêu: < 60 dòng (Updated ACS target).
- Khi quá dài, compact bằng cách gộp các Root Cause Analysis đã resolved vào investigation_log.md.
- Xóa các Next Actions đã hoàn thành.
- Di dời kiến trúc tĩnh sang architecture_memory.md.

### Prompt mẫu — Bắt đầu chat mới (Updated ACS)

```
Đọc docs/AI/project_state.md và docs/AI/tasks/task_XXXX.md.
Xác nhận ma trận Health Check và thực thi nghiêm ngặt theo boundary quy định.
Chỉ tiếp tục sau khi xác nhận xong.
Không code nếu Recommended Action không phải Continue hoặc Assumptions >= Verified Facts.
```

### Prompt mẫu — Cập nhật state

```
Cập nhật docs/AI/project_state.md với:
- Việc đã hoàn thành trong phiên này
- Test đã chạy và kết quả
- File đã sửa
- Root cause mới nếu có
- Next Actions tối đa 5 mục
- AI Health Check mới
Sau đó dừng lại, không làm gì thêm.
```

### Prompt mẫu — Cho phép code

```
Tôi xác nhận objective hiện tại.
Nếu Root Cause Confidence >= 60%, chuyển Recommended Action sang Continue Coding
và thực hiện bước tiếp theo theo plan. Tối đa 1-3 file mỗi lần.
```

---

## Tầng 3 — Task Isolation Layer (docs/AI/tasks/)

### Mục đích

Tạo "vòng kim cô" cho từng phiên làm việc nhỏ, ngăn chặn việc AI đọc lan man làm phình ngữ cảnh.

### Cấu trúc Task Card

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

### Nguyên tắc

- Mỗi task có Task Card riêng trong `docs/AI/tasks/task_*.md`
- Boundary rules ngăn AI đọc file ngoài scope
- Success criteria rõ ràng để biết khi nào task hoàn thành
- Task card được cập nhật khi task thay đổi

---

## Tầng 4 — On-demand Docs & Workflows

### Mục đích

Các tài liệu lớn chứa roadmap, architecture detail, fix history. **Không đọc mặc định mỗi chat.**

### Danh sách tài liệu chính của dự án này

| File | Đọc khi nào |
|---|---|
| `docs/AI/architecture_memory.md` | Cần kiến trúc tĩnh (Project Overview, Architecture Decisions, Important Files) |
| `docs/AI/investigation_log.md` | Cần tra cứu lỗi cũ đã resolved |
| `docs/plan_MVP/RoadMap/MVP plan Account M.md` | Cần xem roadmap sprint hoặc phase detail |
| `docs/plan_MVP/DETAIL_PLAN.md` | Cần xem architecture code pattern |
| `docs/SQLite_Configuration_Fix_Plan.md` | Debug SQLite/EF Core test issues |
| `docs/plan_MVP/HKD_BookAcc/*.docx` | Implement hoặc verify HKD book templates |
| `.github/workflows/pr-check.yml` | Debug CI build/test failures |

### Nguyên tắc

- Đọc đúng file cần thiết, không đọc cả thư mục.
- Sau khi đọc, extract thông tin cần thiết vào `project_state.md` (Verified Facts).
- Không để AI giữ toàn bộ nội dung 900+ dòng trong working context nếu không cần.

---

## Tầng 5 — Triggered Workflows (`.windsurf/workflows/`)

### Mục đích

Các quy trình lặp đi lặp lại, được định nghĩa sẵn, gọi bằng slash command.

### Danh sách workflows hiện có

| Slash Command | Dùng khi nào |
|---|---|
| `/Fix_Errors` | Có build errors cần fix theo pattern |
| `/Fix_Tests` | Có failing tests cần fix |
| `/newfeaturebuild` | Bắt đầu build feature mới (7 bước) |
| `/playwright_triage` | Triage Playwright test failures |
| `/playwright_fix` | Fix Playwright failures |
| `/playwright_validation` | Validate sau khi fix Playwright |
| `/review` | Review code changes trước khi commit |
| `/technical_debt_packaging` | Đóng gói technical debt sau bug fix |
| `/test-refactor-workflow` | Refactor tests |

### Nguyên tắc

- Gọi workflow **sau khi** đã biết rõ vấn đề, không gọi để "khám phá".
- Ví dụ đúng: có log CI failure → gọi `/Fix_Errors`.
- Ví dụ sai: không biết lỗi gì → gọi `/Fix_Errors` để AI tự đoán.

---

## Vòng lặp làm việc chuẩn

```
┌─────────────────────────────────────────────────────────────┐
│                                                             │
│   READ STATE          VERIFY FACTS        UPDATE PLAN       │
│   Đọc project_state   Kiểm tra file/      Lập plan 2-5      │
│   đầu chat mới   →    build/test     →    bước nhỏ          │
│                        liên quan                            │
│        ↑                                       ↓            │
│                                                             │
│   UPDATE STATE        RUN TESTS          CODE SMALL STEP    │
│   Ghi lại kết quả  ←  Test đúng phạm ←   Sửa ít file nhất  │
│   vào project_state   vi thay đổi         có thể            │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## Phân biệt Fact và Assumption

Đây là quy tắc quan trọng nhất khi viết `project_state.md`.

### Fact

Thông tin đã được xác minh bằng một trong các cách:
- Đọc trực tiếp từ source file.
- Thấy trong output của `dotnet build` / `dotnet test`.
- User xác nhận trực tiếp.
- GitHub Actions log cho thấy rõ.

### Assumption

Thông tin chưa được xác minh bằng bằng chứng cụ thể:
- Roadmap ghi là completed nhưng chưa đọc source.
- Fix plan ghi là resolved nhưng chưa chạy test lại.
- AI suy luận từ pattern nhưng chưa kiểm tra code thực tế.

### Quy tắc ghi

```markdown
### Verified Facts
* User xác nhận Sprint 1 hoàn thành.              ← FACT
* pr-check.yml job build-verify chạy 3 lệnh...    ← FACT (đã đọc file)

### Assumptions
* Source code khớp với fix plan đã ghi.            ← ASSUMPTION (chưa đọc file)
* CI failure xảy ra ở bước build, không phải test. ← ASSUMPTION (chưa có log)
```

---

## Những lỗi phổ biến cần tránh

| Lỗi | Hậu quả | Cách tránh |
|---|---|---|
| Đọc toàn bộ roadmap 900+ dòng mỗi chat | Tốn 10K+ tokens, đầy context nhanh | Chỉ đọc `project_state.md`, đọc roadmap on-demand |
| Treat roadmap status = source code fact | AI code từ giả định sai | Luôn verify source trước khi code |
| Không cập nhật state sau phiên | Chat tiếp theo mất context | Cập nhật state trước khi kết thúc |
| Sửa code khi confidence < 60% | Fix sai root cause, tạo thêm lỗi | Investigate trước, code sau |
| Đưa tất cả rules vào `.windsurfrules` | Rules dài, tốn token mỗi turn | Chỉ ghi rules thực sự bất biến |
| Để `project_state.md` quá dài | Tốn nhiều token khi đọc | Compact file sau mỗi 3-5 phiên |
| Gọi workflow khi chưa biết rõ vấn đề | AI đoán mò, fix sai | Biết rõ lỗi trước khi gọi workflow |

---

## Ưu tiên khi các nguồn mâu thuẫn nhau

Khi roadmap nói "completed" nhưng build đang fail, ưu tiên theo thứ tự:

```
1. Build/test output hiện tại     ← cao nhất
2. Source code hiện tại
3. project_state.md
4. Fix plan docs
5. Roadmap/DETAIL_PLAN docs       ← thấp nhất
```

---

## Checklist trước khi mở chat mới

- [ ] `project_state.md` đã được cập nhật với kết quả phiên hiện tại.
- [ ] Current Objective phản ánh đúng mục tiêu tiếp theo.
- [ ] Các Assumptions quan trọng đã được ghi rõ.
- [ ] Next Actions tối đa 5 mục, cụ thể và đo lường được.
- [ ] AI Health Check đã được cập nhật.

---

## Checklist khi nhận được AI output

- [ ] AI có phân biệt Fact và Assumption không?
- [ ] Root Cause Confidence có >= 60% không trước khi code?
- [ ] Fix có minimal không, hay đang over-engineer?
- [ ] AI có đọc file liên quan trước khi đề xuất không?
- [ ] Build gate có được chạy sau khi sửa không?

---

*File này thuộc về: `docs/AI/AI_WORKFLOW_GUIDE.md`*
*Cập nhật khi: quy trình làm việc thay đổi, có công cụ/workflow mới, hoặc phát hiện anti-pattern mới.*
