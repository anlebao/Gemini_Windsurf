# Hướng Dẫn Sử Dụng ACS (Architecture Context System) Hiệu Quả

## Tổng quan

ACS là hệ thống quản lý context AI mới được thiết kế để giảm 70% token tiêu thụ ở turn đầu tiên và ngăn AI đọc lan man làm phình ngữ cảnh.

---

## Smart Router Map v8.1

ACS v8.1 tích hợp Smart Router Map để tự động định tuyến workflow dựa trên tín hiệu đầu vào.

### Auto-Routing Table

| Tín hiệu đầu vào (Trigger) | Mode | Workflow | Ghi chú |
|---|---|---|---|
| Yêu cầu tính năng mới / feature request | ANALYZE → IMPLEMENT | newfeaturebuild.md | 7-step workflow, Playwright disabled |
| Build lỗi (compile errors > 0) | FIX_ONLY | Fix_Errors.md | Pattern-based fixing, max 3 files/batch |
| Test lỗi (dotnet test fail, không phải Playwright) | FIX_ONLY_TESTS | Fix_Tests.md | Pattern-based, domain-safe |
| Playwright E2E test fail | TRIAGE_ONLY | playwright_triage.md | Cấm tự sửa, phải triage trước |
| Playwright fail đã classify (Selector/Timing/UI) | FIX_PLAYWRIGHT | playwright_fix.md | Restore existing behavior only |
| Playwright fail cần workaround kiến trúc | FIX_ARCHITECTURAL | playwright_fix_architectural.md | Technical debt tracking |
| Lỗi tương tác Blazor Server (event handler không trigger) | DEBUG | blazor_interactivity_debug.md | 5 Category (A, B, C, D, E) |
| Review code / security / architecture | REVIEW_ONLY | review.md | Không sửa code, chỉ báo cáo |

### Mode Isolation

- **ANALYZE:** inspect, assess, plan. NO code changes.
- **IMPLEMENT:** code, validate. NO scope expansion.
- **FIX_ONLY:** fix errors only. NO features, NO architecture redesign.
- **REVIEW_ONLY:** report only. NO code modifications.

---

## Skill Matrix v8.1

ACS v8.1 tự động chọn Active Skills (tối đa 3) dựa trên phân hệ đang làm việc.

### Skill Matrix by Subsystem

| Phân hệ | Skills (tối đa 3) | Khi nào kích hoạt |
|---|---|---|
| Kế toán/Thuế HKD | einvoice-integration, dynamic-hkd-book-architecture, domain-integrity-validation | Tính năng E-Invoice, sổ hộ kinh doanh |
| Tầng dữ liệu | outbox-pattern-implementation, sqlite-concurrency-analysis, nats-sqlite-deployment-validation | Outbox pattern, SQLite concurrency, NATS deployment |
| Tầng UI | accounting-ui-implementation, ui-platform-compliance-review, ui-platform-migration | UI kế toán, UI Platform compliance, migration |
| Order Workflow | order-workflow-unified, outbox-pattern-implementation, domain-integrity-validation | Order processing, workflow logic |
| Period Closing | period-closing-audit-trail, domain-integrity-validation | Kỳ kế toán, audit trail |
| Refactor | system-refactor-safety, domain-integrity-validation, test-system-upgrade | Refactor code, upgrade test system |
| Tests | test-system-upgrade, pattern-based-fixing, build-error-analysis | Fix test failures, upgrade test framework |
| Build Errors | build-error-analysis, pattern-based-fixing, domain-integrity-validation | Fix compile errors (Fix_Errors workflow) |
| Playwright | playwright_cost_optimizer, playwright_guard | Playwright E2E tests |

---

## Hardening Gates v8.1

ACS v8.1 áp dụng 6 chốt chặn kỹ thuật đặc quyền để đảm bảo chất lượng và an toàn.

### Gate 1: Anti-Guessing Protocol
- **Quy tắc:** Nếu Assumptions >= Verified Facts → CẤM sửa code, chuyển sang Investigate
- **Hành động:** Đọc thêm file, chạy build/test, thu thập evidence
- **Mục tiêu:** Đạt được Assumptions < Verified Facts trước khi code

### Gate 2: Blazor Server Interactivity Hardening
Áp dụng triệt để 5 Category từ `blazor_interactivity_debug.md`:

- **Category A:** OnAfterRender missing → Force `prerender: false`
- **Category B:** Native form submit → ÉP inline `onsubmit="event.preventDefault(); return false;"` vĩnh viễn
- **Category C:** Event handler not called → Add IsInteractive flag + StateHasChanged
- **Category D:** DLL cache issue → Deep clean bin/obj, kill dotnet processes
- **Category E:** Hydration time-window gap → ÉP inline JS tĩnh đồng bộ (giống Category B)

### Gate 3: Playwright Isolation
- Cấm chạy Playwright diện rộng khi đang code (Steps 1-6 của newfeaturebuild)
- playwright_guard auto-activate trong IMPLEMENT mode
- Chỉ kích hoạt Playwright validation sau: build passes AND implementation complete

### Gate 4: UI Layout → E2E Test Requirement
- Quy tắc: Nếu tính năng mới có thay đổi UI layout (Razor pages, components, navigation, form, table, dialog...), bắt buộc phải viết E2E test tại `6_Testing/e2e-tests/`
- Timing: Viết E2E test spec sau Step 6 (Implementation), trước Step 7 (Review & Approval)
- Không được skip Step 7 approval nếu E2E spec chưa được viết và pass

### Gate 5: Domain Integrity Hardening
- AccountingEntry phải 100% immutable (append-only pattern, changes via Reversal Entry only)
- Domain layer phải pure: NO EF Core, NO DbContext, NO DataAnnotations
- Single Source of Truth: Tất cả domain entities CHỈ tồn tại trong 1_Shared/Domain.cs
- Multi-tenancy enforced ở mọi layer
- Không được sửa Domain để fix UI/Service issues

### Gate 6: ACS Health Check Matrix
Kiểm tra ma trận trong `docs/AI/project_state.md`:
- ✅ **Được sửa code:** Assumptions < Verified Facts VÀ Open Questions < 3
- ❌ **Cấm sửa code:** Assumptions >= Verified Facts HOẶC Open Questions >= 3
- Nếu bị cấm: chuyển sang chế độ điều tra (Investigate)

---

## Quy trình bắt đầu phiên chat mới

### Bước 1: Đọc file bắt buộc

```
Đọc docs/AI/project_state.md và docs/AI/tasks/task_XXXX.md.
```

**Quan trọng:**
- Luôn đọc cả 2 file này ở đầu phiên chat
- Không đọc `architecture_memory.md` hoặc `investigation_log.md` mặc định
- Không đọc roadmap trừ khi task card yêu cầu

### Bước 2: Xác nhận ma trận Health Check

Kiểm tra ma trận trong `project_state.md`:
```markdown
## 4. AI Health Check Matrix
* Evidence Count: [số]
* Verified Facts: [số]
* Assumptions: [số]
* Open Questions: [số]
* Recommended Action: [Continue / Investigate / Stop]
```

**Quy tắc Gate Rules:**
- ✅ **Được sửa code:** Assumptions < Verified Facts VÀ Open Questions < 3
- ❌ **Cấm sửa code:** Assumptions >= Verified Facts HOẶC Open Questions >= 3
- Nếu bị cấm: chuyển sang chế độ điều tra (Investigate)

### Bước 3: Thực thi theo boundary rules

Đọc boundary rules trong task card:
```markdown
## 3. Boundary Rules
* Không đọc lại các file source code cũ trừ khi có lỗi build/test phát sinh
* Không đọc file roadmap dài hạn
```

Tuân thủ nghiêm ngặt các quy tắc này.

---

## Khi nào đọc các file khác

### architecture_memory.md
**Đọc khi:**
- Cần xem Project Overview (tổng quan dự án)
- Cần xem Architecture Decisions (quyết định kiến trúc)
- Cần xem Important Files (danh mục file quan trọng)

**Không đọc khi:**
- Đầu phiên chat mới
- Task không yêu cầu kiến trúc tĩnh

### investigation_log.md
**Đọc khi:**
- Cần tra cứu lỗi cũ đã resolved
- Debug lỗi tương tự đã gặp trước đó

**Không đọc khi:**
- Đầu phiên chat mới
- Task không liên quan đến lỗi cũ

### Roadmap / Plan files
**Đọc khi:**
- Task card yêu cầu đích danh
- Cần xem chi tiết phase/sprint

**Không đọc khi:**
- Đầu phiên chat mới
- Chỉ cần overview

---

## Cập nhật project_state.md

### Khi cần cập nhật

Cập nhật khi xảy ra:
- Root cause được xác nhận
- Test pass/fail thay đổi
- Current Objective hoàn thành hoặc thay đổi
- Architecture decision mới được chốt
- Phát hiện risk mới quan trọng
- Trước khi kết thúc phiên làm việc

### Cách cập nhật

```markdown
Cập nhật docs/AI/project_state.md với:
- Việc đã hoàn thành trong phiên này
- Test đã chạy và kết quả
- File đã sửa
- Root cause mới nếu có
- Next Actions tối đa 5 mục
- AI Health Check Matrix mới
```

### Giữ file ngắn

- Mục tiêu: < 60 dòng
- Compact bằng cách di dời kiến trúc tĩnh sang `architecture_memory.md`
- Di dời Root Cause Analysis đã resolved sang `investigation_log.md`
- Xóa Next Actions đã hoàn thành

---

## Tạo Task Card mới

### Khi cần tạo task card

Khi bắt đầu task mới hoặc phase mới:
1. Tạo file `docs/AI/tasks/task_[tên].md`
2. Điền theo template:

```markdown
# Task Card: [Tên Task]

## 1. Goal
* [Mục tiêu cụ thể - 1-3 dòng]

## 2. Relevant Files
* [Danh sách file liên quan - tối đa 5-10 file]

## 3. Boundary Rules
* [Quy tắc giới hạn scope - không đọc file ngoài scope]
* [Không đọc roadmap trừ khi cần thiết]

## 4. Success Criteria
* [Tiêu chí thành công - cụ thể và đo lường được]
```

### Ví dụ task card

```markdown
# Task Card: Sprint 3 - Phase 5 E-Invoice Multi-Provider Integration

## 1. Goal
* Bắt đầu Sprint 3 với Phase 5 E-Invoice Multi-Provider Integration
* Implement domain models, provider interfaces, circuit breaker và outbox pattern

## 2. Relevant Files
* docs/AI/project_state.md
* docs/plan_MVP/RoadMap/MVP plan Account M.md
* 1_Shared/Domain.cs (sẽ thêm E-Invoice domain models)
* 3_CoreHub/Services/EInvoice/ (thư mục mới)

## 3. Boundary Rules
* Không đọc lại source code của Audit Trail hay Period Closing trừ khi có lỗi build/test
* Không đọc file roadmap dài hạn trừ khi cần xác định chi tiết E-Invoice requirements
* Chỉ tập trung vào E-Invoice domain models, provider interfaces, circuit breaker, outbox pattern

## 4. Success Criteria
* Domain models cho E-Invoice được tạo
* Provider interfaces được định nghĩa
* Circuit breaker pattern được implement
* Outbox pattern được implement
* Unit tests cho E-Invoice components pass
```

---

## Quy trình làm việc hiệu quả

### Vòng lặp chuẩn

```
┌─────────────────────────────────────────────────────────────┐
│                                                             │
│   READ STATE          VERIFY FACTS        UPDATE PLAN       │
│   Đọc project_state   Kiểm tra file/      Lập plan 2-5      │
│   + task card    →    build/test     →    bước nhỏ          │
│                        liên quan                            │
│        ↑                                       ↓            │
│                                                             │
│   UPDATE STATE        RUN TESTS          CODE SMALL STEP    │
│   Ghi lại kết quả  ←  Test đúng phạm ←   Sửa ít file nhất  │
│   vào project_state   vi thay đổi         có thể            │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Các bước chi tiết

1. **Đọc state + task card**
   - Đọc `docs/AI/project_state.md`
   - Đọc `docs/AI/tasks/task_XXXX.md`
   - Xác nhận Health Check Matrix

2. **Verify facts**
   - Đọc file liên quan từ task card
   - Chạy build/test nếu cần
   - Thu thập evidence

3. **Update plan**
   - Lập plan 2-5 bước nhỏ
   - Mỗi bước tối đa 1-3 file
   - Focus minimal changes

4. **Code small step**
   - Sửa ít file nhất có thể
   - Tuân thủ boundary rules
   - Không đọc file ngoài scope

5. **Run tests**
   - Test đúng phạm vi thay đổi
   - Chạy `guard-check.ps1` nếu cần
   - Verify kết quả

6. **Update state**
   - Cập nhật `project_state.md`
   - Cập nhật Health Check Matrix
   - Dừng lại, không làm gì thêm

---

## Tránh lỗi phổ biến

### Lỗi 1: Đọc quá nhiều file đầu phiên

**Sai:**
```
Đọc docs/AI/project_state.md
Đọc docs/AI/architecture_memory.md
Đọc docs/AI/investigation_log.md
Đọc docs/plan_MVP/RoadMap/MVP plan Account M.md
Đọc docs/plan_MVP/DETAIL_PLAN.md
```

**Đúng:**
```
Đọc docs/AI/project_state.md và docs/AI/tasks/task_XXXX.md.
```

### Lỗi 2: Sửa code khi Assumptions >= Verified Facts

**Sai:**
- Assumptions: 5, Verified Facts: 3
- Vẫn sửa code vì "nhanh hơn"

**Đúng:**
- Assumptions: 5, Verified Facts: 3
- Chuyển sang chế độ điều tra (Investigate)
- Thu thập thêm evidence trước khi code

### Lỗi 3: Để project_state.md quá dài

**Sai:**
- project_state.md 200+ dòng
- Chứa cả kiến trúc tĩnh
- Chứa cả lịch sử lỗi

**Đúng:**
- project_state.md < 60 dòng
- Kiến trúc tĩnh sang architecture_memory.md
- Lịch sử lỗi sang investigation_log.md

### Lỗi 4: Không tuân thủ boundary rules

**Sai:**
- Task card nói "không đọc roadmap"
- Vẫn đọc roadmap vì "tò mò"

**Đúng:**
- Tuân thủ nghiêm ngặt boundary rules
- Chỉ đọc file trong Relevant Files
- Chỉ đọc roadmap khi task card yêu cầu

---

## Checklist trước khi code

- [ ] Đã đọc `docs/AI/project_state.md`
- [ ] Đã đọc `docs/AI/tasks/task_XXXX.md`
- [ ] Xác nhận Health Check Matrix: Assumptions < Verified Facts
- [ ] Xác nhận Health Check Matrix: Open Questions < 3
- [ ] Đã hiểu boundary rules trong task card
- [ ] Chỉ đọc file trong Relevant Files
- [ ] Plan tối đa 2-5 bước nhỏ
- [ ] Mỗi bước tối đa 1-3 file

---

## Checklist sau khi code

- [ ] Build pass
- [ ] Tests pass
- [ ] Cập nhật `docs/AI/project_state.md`
- [ ] Cập nhật Health Check Matrix
- [ ] Dừng lại, không làm gì thêm

---

## Ví dụ thực tế

### Ví dụ 1: Bắt đầu Sprint 3

```
User: Bắt đầu Sprint 3 - Phase 5 E-Invoice

AI: Đọc docs/AI/project_state.md và docs/AI/tasks/task_sprint3_einvoice.md.

Health Check Matrix:
- Evidence Count: 0
- Verified Facts: 5
- Assumptions: 0
- Open Questions: 0
- Recommended Action: Continue — Start Sprint 3

Boundary Rules:
- Không đọc lại source code của Audit Trail hay Period Closing
- Không đọc file roadmap dài hạn
- Chỉ tập trung vào E-Invoice domain models, provider interfaces, circuit breaker, outbox pattern

Hành động tiếp theo:
1. Đọc docs/plan_MVP/RoadMap/MVP plan Account M.md để xác định chi tiết E-Invoice requirements
2. Implement E-Invoice domain models trong 1_Shared/Domain.cs
3. Tạo provider interfaces trong 3_CoreHub/Services/EInvoice/
```

### Ví dụ 2: Debug lỗi build

```
User: Build lỗi CS1061 - property not found

AI: Đọc docs/AI/project_state.md và docs/AI/tasks/task_XXXX.md.

Health Check Matrix:
- Evidence Count: 1 (build error log)
- Verified Facts: 1
- Assumptions: 0
- Open Questions: 1 (property nào bị missing?)
- Recommended Action: Investigate Further

Assumptions (0) < Verified Facts (1) ✅
Open Questions (1) < 3 ✅

Hành động tiếp theo:
1. Đọc file có lỗi để xác định property nào bị missing
2. Inspect Domain entity trong 1_Shared/Domain/
3. Verify property name và type
4. Fix mapping hoặc báo Domain Modeling Defect nếu cần
```

---

## Kết quả dự kiến

Với ACS:
- **Token giảm 70%:** Từ ~10K tokens xuống ~3K tokens ở turn đầu tiên
- **Context sạch:** Không đọc lan man, chỉ đọc file cần thiết
- **Gate rules:** Ngăn sửa code khi chưa đủ evidence
- **Task isolation:** Mỗi task có scope rõ ràng

---

## File tham khảo

### ACS Core Files
- `docs/AI/plan-toi-uu-context-token.md` - Plan chi tiết refactoring
- `docs/AI/AI_WORKFLOW_GUIDE.md` - Hướng dẫn workflow tổng quát
- `docs/AI/architecture_memory.md` - Kiến trúc tĩnh
- `docs/AI/investigation_log.md` - Lịch sử lỗi
- `docs/AI/project_state.md` - Trạng thái hiện tại
- `docs/AI/tasks/task_*.md` - Task cards

### Smart Router Map v8.1
- `.windsurfrules` - Smart Router Map v8.1 (gốc dự án) - **FILE CHÍNH**
- `.windsurf/rules/.windsurfrules` - File cũ (đã deprecated)
- `.windsurf/rules/sprint_tracking.rules.md` - Sprint tracking protocol

### Workflow Files
- `.windsurf/workflows/newfeaturebuild.md` - 7-step build new feature
- `.windsurf/workflows/Fix_Errors.md` - Fix build errors
- `.windsurf/workflows/Fix_Tests.md` - Fix failing C# tests
- `.windsurf/workflows/playwright_triage.md` - Triage Playwright failures
- `.windsurf/workflows/playwright_fix.md` - Fix classified Playwright failures
- `.windsurf/workflows/playwright_fix_architectural.md` - Fix architectural Playwright failures
- `.windsurf/workflows/blazor_interactivity_debug.md` - Debug Blazor Server interactivity (5 Category A-E)
- `.windsurf/workflows/review.md` - Code review workflow

### Skill Files
- `.windsurf/skills/technical_debt_management.md` - Debt classification & remediation
- `.windsurf/skills/playwright_cost_optimizer.md` - Deterministic cost tiers
- `.windsurf/skills/playwright_guard.md` - Browser isolation during IMPLEMENT mode
