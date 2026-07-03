# TASK CARD: HKD Book Fix - Wave 5 - Fix Account Mapping + Tax Formulas per TT 152

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** (1) Sửa `_vietnameseAccounts` dictionary sai, (2) Sửa công thức thuế cứng 5%/10% → suất thuế theo ngành nghề theo TT 152/2025/TT-BTC, (3) Sửa account number sai (521/512 → 5118), (4) Thêm khái niệm "nhóm ngành nghề" vào S2a template
- **Nghiệp vụ áp dụng:** TT 152 compliance — sai = báo cáo sai thuế
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/hkd-fix-wave5-fix-account-mapping-tax-formulas`
- **Estimated Sessions:** 2-3

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (IMPLEMENT phase — domain logic + possible Domain modification)
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 5 of 9
- **Dependency:** Wave 4 (routing — GenerateS*BookAsync dùng calc engine), **Tech Lead approval cho W5-T6** (Domain modification)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/hkd_book_accounting_fix_master_plan.md` (READ)
- `3_CoreHub/Services/HKDBookService.cs` (UPDATE — `_vietnameseAccounts` L22-41)
- `3_CoreHub/Services/Template/TemplateFactory.cs` (UPDATE — S2aHKDTemplateImpl L177-249, S2bHKDTemplateImpl L251-313)
- `3_CoreHub/Services/Orchestration/HKDRevenueClassificationService.cs` (READ + possibly UPDATE — verify tax rate mapping)
- `3_CoreHub/Services/IHKDTaxClassificationService.cs` (READ — verify API)
- `3_CoreHub/Services/HKDTaxClassificationService.cs` (READ — verify implementation)
- `1_Shared/Domain/HKDTemplates.cs` (UPDATE W5-T6 — **CẦN TECH LEAD APPROVAL** — sửa account "512" → "5118" trong S2b)
- `6_Tests/VanAn.Core.Tests/Services/HKDBookServiceTests.cs` (UPDATE — add 2 unit tests)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa `AccountingEntry` immutability
- KHÔNG sửa `HKDTemplates.cs` trừ W5-T6 (account number fix — cần Tech Lead approval)
- KHÔNG thay đổi `IHKDBookService` interface
- KHÔNG cứng suất thuế mới — phải dùng `HKDRevenueClassificationService` (lookup theo ngành nghề)
- KHÔNG thêm dependency mới mà không verify package có sẵn

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **TT 152 Compliance:** Suất thuế GTGT theo ngành nghề (1%; 1,5%; 2%; 2,5%; 3%; 5%); TNCN theo ngành nghề (0,5%; 1%; 1,5%; 2%)
- [ ] **TNCN Logic:** `PersonalIncomeTax = TotalRevenue * tncnRate` (không phải `VatAmount * 0.1`)
- [ ] **Account Mapping:** Sửa 5 entry sai + thêm entry mới (311, 333, 5118)
- [ ] **Domain Modification:** W5-T6 sửa `HKDTemplates.cs` — **HARD STOP** cho đến Tech Lead approval
- [ ] **Industry Sector:** S2a template phải phân nhóm ngành nghề (mỗi ngành có Tổng cộng + GTGT + TNCN riêng)
- [ ] **Build Check:** `dotnet build VanAn.sln` Release pass
- [ ] **Test Check:** `dotnet test` pass (2 unit test mới)

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `_vietnameseAccounts` sửa: 211→"Tài sản cố định hữu hình", 811→"Xác định kết quả kinh doanh", 821→"Chi phí thuế TNDN", xóa 841; thêm 311→"Vay ngắn hạn ngân hàng", 333→"Thuế và các khoản phải nộp nhà nước", 5118→"Doanh thu cung cấp dịch vụ"
- [ ] **SC2:** `S2aHKDTemplateImpl` dùng `HKDRevenueClassificationService.GetVatRate(industry)` thay `TotalRevenue * 0.05`
- [ ] **SC3:** `S2aHKDTemplateImpl.PersonalIncomeTax` formula = `TotalRevenue * tncnRate` (không phải `VatAmount * 0.1`)
- [ ] **SC4:** `S2bHKDTemplateImpl` account "521" → "5118" (doanh thu dịch vụ)
- [ ] **SC5:** `S2bHKDTemplate` (Domain) account "512" → "5118" — **sau Tech Lead approval**
- [ ] **SC6:** S2a template có nhóm ngành nghề (nếu tenant có data industry sector)
- [ ] **SC7:** Unit test `S2aBook_VatAmount_ShouldUseIndustryRate_NotHardcoded5Percent` pass
- [ ] **SC8:** Unit test `S2aBook_PersonalIncomeTax_ShouldCalculateOnRevenue_NotOnVat` pass
- [ ] **SC9:** `dotnet build VanAn.sln` Release — 0 errors
- [ ] **SC10:** `dotnet test` — all pass
- [ ] **SC11:** guard-check.ps1 PASSED

---

## 6. ACTIVE SKILLS (MAX 3)
- `einvoice-integration` — Reference pattern cho tax rate lookup
- `dynamic-hkd-book-architecture` — HKD book domain knowledge
- `domain-integrity-validation` — Verify account mapping + tax formula correctness

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 6
- **Verified Facts:**
  - Fact 1: `_vietnameseAccounts` (L22-41) có 5 entry sai: 211, 811, 821, 841, 521
  - Fact 2: `S2aHKDTemplateImpl` (L177-249) cứng `VatAmount = TotalRevenue * 0.05` + `PersonalIncomeTax = VatAmount * 0.1`
  - Fact 3: `HKDRevenueClassificationService` (Orchestration/) tồn tại — cần verify API có `GetVatRate(industry)` / `GetTncnRate(industry)`
  - Fact 4: `IHKDTaxClassificationService` (Services/) tồn tại — cần verify interface
  - Fact 5: `S2bHKDTemplateImpl` (L251-313) dùng account "521" cho "Doanh thu dịch vụ" — sai (521=Giảm trừ doanh thu)
  - Fact 6: `S2bHKDTemplate` (Domain HKDTemplates.cs L206-294) dùng account "512" — sai (512 không tồn tại TT 200; dịch vụ là 5118)
- **Assumptions:**
  - `HKDRevenueClassificationService` có API trả suất thuế theo ngành nghề (verify)
  - Tenant có data industry sector (cần verify Tenant entity có property này)
  - TT 152 suất thuế: GTGT 1%-5%, TNCN 0,5%-2% (theo TT 152/2025/TT-BTC)
- **Open Questions:**
  - Q1: `HKDRevenueClassificationService` API chính xác là gì? (READ verify)
  - Q2: Tenant entity có property `IndustrySector` / `BusinessType` không? (READ Tenant.cs)
  - Q3: W5-T6 (Domain modification) — Tech Lead approval status? (BLOCK nếu chưa approval)
- **Recommended Action:** PROCEED với W5-T1 đến W5-T5 — STOP ở W5-T6 cho Tech Lead approval

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `HKDBookService._vietnameseAccounts` | Tên tài khoản hiển thị đúng trong General Ledger + Trial Balance | No mitigation — fix only |
| `S2aHKDTemplateImpl` formula | VatAmount/PIT tính đúng theo ngành nghề | Test verify (W5-T8, W5-T9) |
| `S2bHKDTemplateImpl` account "521"→"5118" | SUM_ACCOUNT query đúng tài khoản | Test verify |
| `HKDTemplates.cs` (Domain, W5-T6) | **HIGH RISK** — Domain modification | **Tech Lead approval required** — STOP nếu chưa |
| `HKDRevenueClassificationService` (có thể update) | Thêm lookup table nếu chưa có | Verify API trước |

---

## 9. TDD & TESTING STRATEGY
- **Unit tests:** 2 test mới (tax rate + PIT logic)
- **Integration tests:** N/A (Wave 7)
- **E2E tests:** N/A
- **Verification:** `dotnet build` + `dotnet test` pass

### Test specs
**Test 1: `S2aBook_VatAmount_ShouldUseIndustryRate_NotHardcoded5Percent`**
- Arrange: Tenant với ngành nghề 1% GTGT, seed JournalEntries account 511 Credit 1000
- Act: `GenerateS2aBookAsync`
- Assert: `NumericValues["VatAmount"] == 10m` (1000 * 0.01), không phải 50m (1000 * 0.05)

**Test 2: `S2aBook_PersonalIncomeTax_ShouldCalculateOnRevenue_NotOnVat`**
- Arrange: Tenant với TNCN rate 1%, seed Revenue 1000
- Act: `GenerateS2aBookAsync`
- Assert: `NumericValues["PersonalIncomeTax"] == 10m` (1000 * 0.01), không phải 1m (10 * 0.1)

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: Sequential fix theo dependency
1. Fix `_vietnameseAccounts` (mechanical) → 2. Verify `HKDRevenueClassificationService` API → 3. Fix S2a formula → 4. Fix S2b account → 5. (STOP) Tech Lead approval cho W5-T6 → 6. Fix Domain S2b → 7. Add tests

### Micro-phase breakdown

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Đọc `HKDRevenueClassificationService` + `IHKDTaxClassificationService` API<br>- Đọc `Tenant.cs` (verify IndustrySector property)<br>- Chốt: lookup table suất thuế (nếu service chưa có)<br>- Chốt: W5-T6 Domain mod — request Tech Lead approval | - Fix `_vietnameseAccounts` (W5-T1)<br>- Fix `S2aHKDTemplateImpl` formula (W5-T4)<br>- Fix `S2bHKDTemplateImpl` account (W5-T5)<br>- Add 2 unit tests (W5-T8, W5-T9)<br>- Run `dotnet build` + `dotnet test`<br>- Commit (skip W5-T6 nếu chưa approval) |
| **S2** (sau approval) | - Tech Lead approval cho W5-T6<br>- Chốt: account "512" → "5118" trong Domain S2b | - Fix `HKDTemplates.cs` S2b account (W5-T6)<br>- Run `dotnet build` + `dotnet test`<br>- Commit |

### Rules
- KHÔNG làm W5-T6 trước Tech Lead approval (HARD STOP)
- 1 fix tại 1 thời điểm — build + test verify sau mỗi cái
- Nếu `HKDRevenueClassificationService` chưa có API → tạo lookup table (W5-T3)
- Verify Tenant có IndustrySector data — nếu không, fallback default rate

---

## 11. ESTIMATED EFFORT
- 2-3 sessions (account mapping + tax formula + industry sector modeling + Domain mod + tests)
- **BLOCKER:** W5-T6 cần Tech Lead approval (Domain modification)
- **CRITICAL:** Compliance pháp lý — sai suất thuế = báo cáo sai thuế
