# TASK CARD: HKD Book Fix - Wave 4 - Route `HKDBookService.GenerateS*BookAsync` through `IHKDBookGenerationService`

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Thay `new S*HKDTemplate()` + `ConvertToJournalEntries` (no-op calc) bằng `_hkdBookGenerationService.GenerateBookAsync(tenantId, period, templateCode)` (calc engine thật) — fix Issue 1 (NumericValues luôn rỗng)
- **Nghiệp vụ áp dụng:** Production routing fix — core fix của stream
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/hkd-fix-wave4-route-through-generation-service`
- **Estimated Sessions:** 1

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (IMPLEMENT phase — production method rewrite)
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 4 of 9
- **Dependency:** Wave 3 (IHKDBookGenerationService đã đăng ký DI), Wave 2 (JournalEntries có data)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/hkd_book_accounting_fix_master_plan.md` (READ)
- `3_CoreHub/Services/HKDBookService.cs` (UPDATE — constructor L13-20, 7 method GenerateS*BookAsync L460-654, ConvertToJournalEntries L718-751)
- `3_CoreHub/Services/Template/HKDBookGenerationService.cs` (READ — verify `GenerateBookAsync` signature)
- `3_CoreHub/Services/IHKDBookService.cs` (READ — verify interface không đổi)

### Boundary Rules (Nghiêm cấm)
- KHÔNG thay đổi `IHKDBookService` interface signature (backward compat)
- KHÔNG sửa `1_Shared/Domain/HKDTemplates.cs` (Domain — no-op CalculateAsync giữ nguyên, không dùng nữa)
- KHÔNG xóa `ConvertToJournalEntries` ngay — mark obsolete hoặc xóa sau khi grep confirm 0 usage
- KHÔNG thay đổi `RecordRevenueAsync`/`RecordExpenseAsync` (đã fix Wave 2)
- KHÔNG sửa `HKDBookGenerationService` logic

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Backward Compat:** `IHKDBookService.GenerateS*BookAsync` signature không đổi — caller không break
- [ ] **NumericValues Populated:** Sau fix, `book.NumericValues` phải có giá trị (verify ở Wave 6 test)
- [ ] **No Double Convert:** KHÔNG dùng `ConvertToJournalEntries` nữa — `HKDBookGenerationService` query DB trực tiếp
- [ ] **Template Code Match:** 7 templateCode phải đúng: "S1a_HKD", "S2a_HKD", "S2b_HKD", "S2c_HKD", "S2d_HKD", "S2e_HKD", "S3a_HKD"
- [ ] **Build Check:** `dotnet build VanAn.sln` Release pass

---

## 5. SUCCESS CRITERIA (ĐO LƯỢNG ĐƯỢC)
- [ ] **SC1:** `HKDBookService` constructor inject `IHKDBookGenerationService` (thêm param)
- [ ] **SC2:** 7 method `GenerateS*BookAsync` gọi `_hkdBookGenerationService.GenerateBookAsync(tenantId, period, "<code>")` thay vì `new S*HKDTemplate()`
- [ ] **SC3:** 0 `new S*HKDTemplate()` call còn lại trong `HKDBookService.cs` (grep)
- [ ] **SC4:** 0 `ConvertToJournalEntries` call còn lại trong 7 method (grep — method có thể giữ nhưng mark obsolete)
- [ ] **SC5:** `IHKDBookService` interface không đổi (backward compat)
- [ ] **SC6:** `dotnet build VanAn.sln` Release — 0 errors
- [ ] **SC7:** guard-check.ps1 PASSED
- [ ] **SC8:** (Verify ở Wave 6) `book.NumericValues` có giá trị sau `GenerateBookAsync`

---

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Verify routing không break data flow
- `system-refactor-safety` — Refactor 7 method an toàn
- `pattern-based-fixing` — Apply cùng pattern cho 7 method

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: `HKDBookService` constructor (L13-20) hiện inject `IAccountingEntryRepository`, `IHKDBookRepository`, `ILogger<HKDBookService>` — cần thêm `IHKDBookGenerationService`
  - Fact 2: 7 method `GenerateS*BookAsync` (L460-654) cùng pattern: `new S*HKDTemplate()` + `GetByPeriodAsync` + `ConvertToJournalEntries` + `template.CreateBookAsync`
  - Fact 3: `HKDBookGenerationService.GenerateBookAsync(TenantId, AccountingPeriod, string templateCode)` — signature confirmed (HKDBookGenerationService.cs L28-31)
  - Fact 4: `HKDBookGenerationService` query DB trực tiếp qua `VanAnDbContext.JournalEntries` (L180-188) — không cần `ConvertToJournalEntries`
  - Fact 5: `IHKDBookService` interface (IHKDBookService.cs) có 7 method `GenerateS*BookAsync` — signature không đổi
- **Assumptions:**
  - `HKDBookGenerationService.GenerateBookAsync` return `GenericHKDBook` với `NumericValues` populated (verify ở Wave 6)
  - Caller của `GenerateS*BookAsync` (nếu có) không break — return type same
- **Open Questions:**
  - Q1: Có caller nào đang gọi `GenerateS*BookAsync` không? (Grep — likely 0, chỉ test gọi)
  - Q2: `ConvertToJournalEntries` có còn dùng ở đâu không sau khi rewrite? (Grep — nếu 0, mark obsolete)
- **Recommended Action:** PROCEED — risk medium, thay đổi 7 method production nhưng backward compat

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `HKDBookService` constructor | Thêm 1 dep — caller phải pass IHKDBookGenerationService | DI auto-resolve (Wave 3 đã register) |
| 7 method `GenerateS*BookAsync` | Return `GenericHKDBook` với NumericValues populated (trước rỗng) | Đây là fix — caller hưởng lợi |
| `ConvertToJournalEntries` (mark obsolete) | 0 usage sau rewrite | Mark `[Obsolete]` hoặc xóa nếu grep confirm |
| Test `HKDBookServiceTests` | Mock setup cần thêm `IHKDBookGenerationService` | Update test constructor (Wave 6) |

---

## 9. TDD & TESTING STRATEGY
- **Unit tests:** Update ở Wave 6 (retrofit numeric assertions)
- **Integration tests:** N/A (Wave 7)
- **E2E tests:** N/A
- **Verification:** `dotnet build VanAn.sln` Release pass + grep verify

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: Pattern-based rewrite 7 method
1. Inject `IHKDBookGenerationService` vào constructor
2. Rewrite `GenerateS1aBookAsync` — replace body với `_hkdBookGenerationService.GenerateBookAsync(tenantId, period, "S1a_HKD")`
3. Rewrite 6 method còn lại (S2a-S2e, S3a) — cùng pattern, đổi templateCode
4. Grep `ConvertToJournalEntries` — mark obsolete hoặc xóa
5. Build verify

### Micro-phase breakdown

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Đọc `HKDBookGenerationService.GenerateBookAsync` signature<br>- Grep caller của `GenerateS*BookAsync` (verify 0 production caller, chỉ test)<br>- Chốt: mark `ConvertToJournalExpressions` obsolete hay xóa | - Inject `IHKDBookGenerationService` vào `HKDBookService` constructor<br>- Rewrite 7 method `GenerateS*BookAsync` (pattern-based)<br>- Mark/xóa `ConvertToJournalEntries`<br>- Run `dotnet build VanAn.sln` Release<br>- Grep verify 0 `new S*HKDTemplate()`<br>- Commit |

### Rules
- 1 method tại 1 thời điểm — rewrite, build verify, rồi sang method tiếp
- KHÔNG thay đổi try/catch error handling structure (giữ log error pattern)
- Verify templateCode đúng (S1a_HKD, S2a_HKD, ... — không phải S1a-HKD)

---

## 11. ESTIMATED EFFORT
- 1 session (7 method rewrite + constructor inject + grep verify)
- **BLOCKER:** Wave 3 phải merged trước (IHKDBookGenerationService resolvable)
- **CRITICAL:** Đây là wave fix Issue 1 cốt lõi — NumericValues sẽ có số liệu
