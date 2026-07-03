# TASK CARD: HKD Book Fix - Wave 0 - Pre-flight Verification

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Verify baseline sạch + chốt data flow gap trước khi bắt đầu stream HKD Book Fix
- **Nghiệp vụ áp dụng:** Pre-flight cho stream HKD Book Accounting Report Fix (TT 152/2025/TT-BTC compliance)
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/hkd-fix-wave0-preflight`
- **Estimated Sessions:** 0.5

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE phase — verify only, no code change)
- **Execution Mode:** ANALYZE
- **Current Phase:** Wave 0 of 9
- **Dependency:** None (first wave)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/hkd_book_accounting_fix_master_plan.md` (READ)
- `3_CoreHub/Program.cs` (READ — verify DI registrations)
- `5_WebApps/ShopERP/Program.cs` (READ — verify DI registrations)
- `3_CoreHub/Services/HKDBookService.cs` (READ — verify write path)
- `3_CoreHub/Services/PreAggregation/SmartPreAggregationService.cs` (READ — verify query path)
- `3_CoreHub/Repositories/HKDBookRepository.cs` (READ — verify AddToBookAsync)
- `2_Gateway/Controllers/AccountingEntriesController.cs` (READ — verify no hkd-books endpoint)
- `5_WebApps/ShopERP/Components/Pages/Accounting/` (READ — verify no HKD book page)
- `docs/plan_MVP/HKD_BookAcc/*.docx` (READ — extract TT 152 layout spec)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa code — Wave 0 là verify only
- KHÔNG tạo file mới — chỉ đọc + ghi note
- KHÔNG chạy destructive command (drop DB, delete tables)

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Build Baseline:** `dotnet build VanAn.sln` Release phải pass (0 errors) — nếu fail, STOP báo user
- [ ] **DI Verification:** Grep `AddScoped<HKDBookGenerationService>` / `AddScoped<ScopedDataProvider>` / `AddScoped<SmartPreAggregationService>` / `AddScoped<ProductionFormulaEngine>` / `AddScoped<TemplateFactory>` (mới) → phải 0 matches (confirm chưa wire)
- [ ] **Endpoint Verification:** Grep `hkd-books` / `hkdbooks` / `GenerateS*BookAsync` trong Controllers → phải 0 matches (confirm chưa expose)
- [ ] **UI Verification:** Grep `S1a_HKD` / `S2a_HKD` / `S2b_HKD` / `S2c_HKD` / `S2d_HKD` / `S2e_HKD` / `S3a_HKD` trong `Components/Pages/` → phải 0 matches (confirm chưa render)
- [ ] **Data Verification:** Query `SELECT COUNT(*) FROM JournalEntries` trong DB dev → confirm rỗng hoặc có data (nếu có data, verify nguồn)

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `dotnet build VanAn.sln` Release pass (0 errors)
- [ ] **SC2:** 5 service calc engine confirmed chưa đăng ký DI (grep 0 matches)
- [ ] **SC3:** 0 endpoint expose `GenerateS*BookAsync` (grep 0 matches)
- [ ] **SC4:** 0 Razor page render S1a/S2a-S2e/S3a (grep 0 matches)
- [ ] **SC5:** `JournalEntries` table status confirmed (rỗng hoặc có nguồn)
- [ ] **SC6:** 7 mẫu docx TT 152 layout extracted (header + bảng + footer spec)
- [ ] **SC7:** Git status snapshot clean trước Wave 1

---

## 6. ACTIVE SKILLS (MAX 3)
- `build-error-analysis` — Verify build baseline
- `domain-integrity-validation` — Verify data flow gap

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 8
- **Verified Facts:**
  - Fact 1: `HKDBookService.GenerateS*BookAsync` dùng `new S*HKDTemplate()` (Domain) — `CalculateAsync` no-op
  - Fact 2: `Services/Template/` có calc engine thật nhưng không wire DI
  - Fact 3: `RecordRevenue/Expense` ghi `AccountingEntry`, không ghi `JournalEntry`
  - Fact 4: `SmartPreAggregationService` query `JournalEntries` (bảng rỗng)
  - Fact 5: `ConvertToJournalEntries` tạo in-memory, không persist
  - Fact 6: `HKDBookRepository.AddToBookAsync` tồn tại nhưng không ai gọi
  - Fact 7: `AccountingEntriesController` chỉ expose revenue/expense/profit summary
  - Fact 8: `Components/Pages/Accounting/` không có HKD book page
- **Assumptions:**
  - `JournalEntries` table rỗng trong DB dev (cần verify)
  - 7 mẫu docx có layout consistent (cần extract)
- **Open Questions:**
  - Q1: `IBookResultCache` đã đăng ký DI chưa? (Verify)
  - Q2: `IMemoryCache` đã đăng ký (`AddMemoryCache()`) chưa? (Verify)
- **Recommended Action:** PROCEED — verify only, risk none

---

## 8. REVERSE IMPACT ANALYSIS
| File verify | Reverse impact | Mitigation |
|---|---|---|
| `3_CoreHub/Program.cs` | None — read only | N/A |
| `HKDBookService.cs` | None — read only | N/A |
| `SmartPreAggregationService.cs` | None — read only | N/A |
| DB query | None — SELECT only | N/A |

---

## 9. TDD & TESTING STRATEGY
- **Unit tests:** N/A — Wave 0 là verify
- **Integration tests:** N/A
- **E2E tests:** N/A
- **Verification:** `dotnet build VanAn.sln` Release pass + grep checks + DB query

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: Sequential verify
1. Build baseline → 2. Grep DI → 3. Grep endpoint → 4. Grep UI → 5. DB query → 6. Extract docx layout → 7. Git snapshot

### Micro-phase breakdown

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Đọc master plan + audit report<br>- Chốt: 7 service chưa wire DI<br>- Chốt: 0 endpoint/UI<br>- Chốt: JournalEntries status<br>- Chốt: 7 mẫu docx layout spec | - Run `dotnet build VanAn.sln` Release<br>- Grep 5 service DI registrations<br>- Grep endpoint + UI<br>- Query DB (if accessible)<br>- Extract 7 docx layouts (script)<br>- Snapshot git status<br>- Update project_state.md |

### Rules
- 1 verify step tại 1 thời điểm
- Nếu build fail → STOP, báo user
- Nếu `JournalEntries` có data → verify nguồn trước khi tiếp tục

---

## 11. ESTIMATED EFFORT
- 0.5 session (verify only, không code)
- **BLOCKER:** None — risk thấp nhất trong 9 waves
- **PARALLEL:** Có thể làm cùng session với Wave 1 (cả 2 non-code/low-risk)
