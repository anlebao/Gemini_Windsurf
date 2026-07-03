# TASK CARD: HKD Book Fix - Wave 5a - Fix Account Mapping + PIT-on-Revenue (No Domain mod, no industry modeling)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** (1) Sửa `_vietnameseAccounts` dictionary sai (5 entry sai + 3 entry thiếu), (2) Sửa `PersonalIncomeTax` formula tính trên `VatAmount` thay vì `TotalRevenue`, (3) Sửa account `"521"`/`"512"` → `"5118"` cho doanh thu dịch vụ (Service layer + Domain layer)
- **Nghiệp vụ áp dụng:** TT 152/2025/TT-BTC compliance — fix 2 bug logic rõ ràng (PIT base + account mapping) KHÔNG cần modeling ngành nghề (industry-sector rates sang Wave 5b)
- **Status:** PENDING — Planning & Approval (v3 — split from old wave5)
- **Branch:** `feature/hkd-fix-wave5a-account-mapping-pit-fix`
- **Estimated Sessions:** 1
- **Master plan link:** `docs/AI/tasks/hkd_book_accounting_fix_master_plan.md` Section 6 (Wave 5a)

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (IMPLEMENT phase — service logic + 1 Domain account-number fix)
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 5a of 12 (v3 — after Wave 4, before Wave 5b/5c)
- **Dependency:** Wave 4 merged (GenerateS*BookAsync dùng IHKDBookGenerationService)
- **Blocks:** Wave 5b (industry rates), Wave 5c (2026 regulatory — uses PIT base from 5a)
- **Tech Lead approval required:** W5a-T4 (Domain modification — account number `"512"` → `"5118"`, no new field)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc + sửa
- `docs/AI/project_state.md` (READ — context)
- `docs/AI/tasks/hkd_book_accounting_fix_master_plan.md` (READ — Section 6 Wave 5a)
- `3_CoreHub/Services/HKDBookService.cs` (READ + MODIFY — `_vietnameseAccounts` L22-41)
- `3_CoreHub/Services/Template/TemplateFactory.cs` (READ + MODIFY — S2aHKDTemplateImpl L177-249, S2bHKDTemplateImpl L251-313)
- `1_Shared/Domain/HKDTemplates.cs` (READ + MODIFY W5a-T4 — **CẦN TECH LEAD APPROVAL** — sửa account `"512"` → `"5118"` trong S2b, L206-294)
- `6_Tests/VanAn.Core.Tests/Services/HKDBookServiceTests.cs` (READ + MODIFY — add 2 unit tests)

### Files được phép đọc (verify only)
- `3_CoreHub/Services/Orchestration/HKDRevenueClassificationService.cs` (READ — verify tax rate API for Wave 5b prep)
- `3_CoreHub/Services/IHKDTaxClassificationService.cs` (READ — verify interface)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa `AccountingEntry` immutability — governance Hard Stop
- KHÔNG sửa `HKDTemplates.cs` trừ W5a-T4 (account number fix — cần Tech Lead approval)
- KHÔNG thay đổi `IHKDBookService` interface
- KHÔNG cứng suất thuế mới — industry-specific rates sang Wave 5b (5a dùng default rate tạm thời)
- KHÔNG thêm dependency mới mà không verify package có sẵn
- KHÔNG thêm field mới vào Domain entity — W5a-T4 là account-number fix only

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)

### 4.1 Account Mapping Fix (W5a-T1)
Current `_vietnameseAccounts` (L22-41) có 5 entry SAI:
| Code | Hiện tại (SAI) | Sửa thành (ĐÚNG TT 200) |
|---|---|---|
| 211 | "Ngắn hạn vay ngân hàng" | "Tài sản cố định hữu hình" (211 = TSCĐ hữu hình, vay ngắn hạn là 311) |
| 811 | "Lợi nhuận gộp về bán hàng" | "Xác định kết quả kinh doanh" (811 = XĐKQKD) |
| 821 | "Chi phí tài chính" | "Chi phí thuế TNDN" (821 = CPT TNDN, CPT tài chính là 815) |
| 841 | "Lợi nhuận sau thuế" | **XÓA** (841 không tồn tại trong TT 200 — lợi nhuận sau thuế không có tài khoản riêng) |
| 521 | (used in S2b) | **XÓA khỏi S2b** — 521 = "Giảm trừ doanh thu", KHÔNG phải doanh thu dịch vụ |

Thêm 3 entry mới:
| Code | Label |
|---|---|
| 311 | "Vay ngắn hạn ngân hàng" |
| 333 | "Thuế và các khoản phải nộp nhà nước" |
| 5118 | "Doanh thu cung cấp dịch vụ" |

### 4.2 PIT Formula Fix (W5a-T2)
Current `S2aHKDTemplateImpl` (L177-249):
- `VatAmount = TotalRevenue * 0.05` (cứng 5% — industry rates sang Wave 5b)
- `PersonalIncomeTax = VatAmount * 0.1` (**SAI** — PIT phải tính trên Revenue, không phải VatAmount)

Fix:
- `VatAmount = TotalRevenue * defaultRate` (defaultRate = 0.05m tạm thời — Wave 5b sẽ thay bằng `HKDRevenueClassificationService.GetVatRate(industry)`)
- `PersonalIncomeTax = TotalRevenue * pitDefaultRate` (pitDefaultRate = 0.005m tạm thời — Wave 5b sẽ thay bằng industry rate)

### 4.3 S2b Account Fix (W5a-T3 + W5a-T4)
- `S2bHKDTemplateImpl` (L251-313): account `"521"` → `"5118"` (Service layer — W5a-T3, no approval needed)
- `S2bHKDTemplate` (Domain `HKDTemplates.cs` L206-294): account `"512"` → `"5118"` (Domain layer — W5a-T4, **CẦN TECH LEAD APPROVAL**)

### 4.4 Hardening Gates
- [ ] **Build:** `dotnet build VanAn.sln` Release — 0 errors
- [ ] **Guard:** guard-check.ps1 PASSED
- [ ] **Tests:** 2 unit test pass (PIT base + account mapping)
- [ ] **Domain protection:** W5a-T4 là account-number fix only (KHÔNG thêm field) — cần Tech Lead approval
- [ ] **AccountingEntry immutability:** KHÔNG modify

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `_vietnameseAccounts` sửa: 211→"Tài sản cố định hữu hình", 811→"Xác định kết quả kinh doanh", 821→"Chi phí thuế TNDN", xóa 841; thêm 311→"Vay ngắn hạn ngân hàng", 333→"Thuế và các khoản phải nộp nhà nước", 5118→"Doanh thu cung cấp dịch vụ"
- [ ] **SC2:** `S2aHKDTemplateImpl.PersonalIncomeTax` formula = `TotalRevenue * pitDefaultRate` (không phải `VatAmount * 0.1`)
- [ ] **SC3:** `S2bHKDTemplateImpl` account `"521"` → `"5118"` (Service layer)
- [ ] **SC4:** `S2bHKDTemplate` (Domain) account `"512"` → `"5118"` — **sau Tech Lead approval** (W5a-T4)
- [ ] **SC5:** Unit test `S2aBook_PersonalIncomeTax_ShouldCalculateOnRevenue_NotOnVat` pass
- [ ] **SC6:** Unit test `S2bBook_ServiceRevenue_ShouldUseAccount5118_Not521` pass
- [ ] **SC7:** `dotnet build VanAn.sln` Release — 0 errors
- [ ] **SC8:** `dotnet test` — all pass
- [ ] **SC9:** guard-check.ps1 PASSED

---

## 6. ACTIVE SKILLS (MAX 3)
- `einvoice-integration` — Reference pattern cho tax rate lookup (prep for Wave 5b)
- `dynamic-hkd-book-architecture` — HKD book domain knowledge
- `domain-integrity-validation` — Verify account mapping + PIT formula correctness

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 6 verified facts from codebase
- **Verified Facts:**
  - Fact 1: `_vietnameseAccounts` (L22-41) có 5 entry sai: 211, 811, 821, 841, 521
  - Fact 2: `S2aHKDTemplateImpl` (L177-249) cứng `VatAmount = TotalRevenue * 0.05` + `PersonalIncomeTax = VatAmount * 0.1` (PIT base SAI)
  - Fact 3: `S2bHKDTemplateImpl` (L251-313) dùng account "521" cho "Doanh thu dịch vụ" — sai (521=Giảm trừ doanh thu)
  - Fact 4: `S2bHKDTemplate` (Domain HKDTemplates.cs L206-294) dùng account "512" — sai (512 không tồn tại TT 200; dịch vụ là 5118)
  - Fact 5: `HKDRevenueClassificationService` (Orchestration/) tồn tại — cần verify API cho Wave 5b prep
  - Fact 6: `IHKDTaxClassificationService` (Services/) tồn tại — cần verify interface
- **Assumptions:**
  - Default rate 5% GTGT + 0.5% TNCN acceptable tạm thời (Wave 5b sẽ thay bằng industry rates)
  - Tech Lead sẽ approve W5a-T4 (account-number fix only, no new field — low risk)
- **Open Questions:**
  - Q1: `HKDRevenueClassificationService` API chính xác là gì? (READ verify — prep for Wave 5b)
  - Q2: W5a-T4 (Domain modification) — Tech Lead approval status? (BLOCK nếu chưa approval)
- **Recommended Action:** PROCEED với W5a-T1 đến W5a-T3 + W5a-T5/T6 — STOP ở W5a-T4 cho Tech Lead approval

---

## 8. REVERSE IMPACT ANALYSIS
| File modify | Reverse impact | Mitigation |
|---|---|---|
| `HKDBookService._vietnameseAccounts` (L22-41) | Tên tài khoản hiển thị đúng trong General Ledger + Trial Balance | No mitigation — fix only |
| `S2aHKDTemplateImpl` PIT formula (L177-249) | PIT tính đúng trên Revenue | Unit test W5a-T5 verify |
| `S2bHKDTemplateImpl` account "521"→"5118" (L251-313) | SUM_ACCOUNT query đúng tài khoản | Unit test W5a-T6 verify |
| `HKDTemplates.cs` S2b account "512"→"5118" (L206-294) | **HIGH RISK** — Domain modification | **Tech Lead approval required** — STOP nếu chưa |

---

## 9. TDD & TESTING STRATEGY
- **Unit tests (NEW — 2 tests):**
  1. `S2aBook_PersonalIncomeTax_ShouldCalculateOnRevenue_NotOnVat`
     - Arrange: Tenant + seed Revenue 1000 (JournalEntries account 511 Credit 1000)
     - Act: `GenerateS2aBookAsync`
     - Assert: `NumericValues["PersonalIncomeTax"] == 5m` (1000 * 0.005), KHÔNG phải `0.5m` (10 * 0.1 — old formula on VatAmount)
  2. `S2bBook_ServiceRevenue_ShouldUseAccount5118_Not521`
     - Arrange: Tenant + seed JournalEntries account 5118 Credit 500
     - Act: `GenerateS2bBookAsync`
     - Assert: `NumericValues["ServiceRevenue"] == 500m` (query account 5118), KHÔNG phải `0m` (query account 521 — không có data)
- **Integration tests:** N/A (Wave 7)
- **E2E tests:** N/A (Wave 8)
- **Verification:** `dotnet build VanAn.sln` Release + `dotnet test` pass

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: Sequential fix theo dependency
1. Fix `_vietnameseAccounts` (mechanical) → 2. Fix S2a PIT formula → 3. Fix S2b Service account → 4. (STOP) Tech Lead approval cho W5a-T4 → 5. Fix Domain S2b account → 6. Add 2 unit tests → 7. Build + test

### Micro-phase breakdown

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** (W5a-T1-T3 + T5/T6) | - Đọc `HKDRevenueClassificationService` API (prep Wave 5b)<br>- Chốt: default rate tạm thời (5% GTGT, 0.5% TNCN)<br>- Chốt: W5a-T4 Domain mod — request Tech Lead approval | - Fix `_vietnameseAccounts` (W5a-T1)<br>- Fix `S2aHKDTemplateImpl` PIT formula (W5a-T2)<br>- Fix `S2bHKDTemplateImpl` account "521"→"5118" (W5a-T3)<br>- Add 2 unit tests (W5a-T5, W5a-T6)<br>- Run `dotnet build` + `dotnet test`<br>- Commit (skip W5a-T4 nếu chưa approval) |
| **S2** (sau approval — W5a-T4) | - Tech Lead approval cho W5a-T4<br>- Chốt: account "512" → "5118" trong Domain S2b | - Fix `HKDTemplates.cs` S2b account (W5a-T4)<br>- Run `dotnet build` + `dotnet test`<br>- Commit |

### Rules
- KHÔNG làm W5a-T4 trước Tech Lead approval (HARD STOP — Domain modification)
- 1 fix tại 1 thời điểm — build + test verify sau mỗi cái
- Nếu Tech Lead KHÔNG approve W5a-T4 → fix Service layer only (W5a-T3), log Domain fix as technical debt, proceed to Wave 5b/5c
- Default rate tạm thời (5% GTGT, 0.5% TNCN) — Wave 5b sẽ thay bằng industry rates

---

## 11. ESTIMATED EFFORT
- 1 session (Service layer fixes + tests) + 0.5 session (Domain fix sau approval)
- **BLOCKER:** Wave 5b (industry rates cần PIT base correct từ 5a), Wave 5c (2026 regulatory cần PIT formula base)
- **Tech Lead approval required:** W5a-T4 (Domain account-number fix — no new field)
- **PARALLEL:** Không — Wave 5a phải hoàn thành trước Wave 5b/5c
