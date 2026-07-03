# TASK CARD: HKD Book Fix - Wave 5b - Industry-Sector Tax Rates per TT 152 (Conditional — needs W0-T10 + Tech Lead approval)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** (1) Thay default rate cứng (5% GTGT, 0.5% TNCN từ Wave 5a) bằng suất thuế theo ngành nghề per Luật 2025 + ND 117/2025, (2) Thêm khái niệm "nhóm ngành nghề" vào S2a template (mỗi ngành có Tổng cộng + GTGT + TNCN riêng), (3) Verify/create `HKDRevenueClassificationService` lookup table 4 nhóm ngành nghề
- **Nghiệp vụ áp dụng:** TT 152/2025/TT-BTC full compliance — suất thuế theo ngành nghề (KHÔNG cứng default rate)
- **Status:** PENDING — Planning & Approval (v3 — split from old wave5, CONDITIONAL)
- **Branch:** `feature/hkd-fix-wave5b-industry-sector-tax-rates`
- **Estimated Sessions:** 1-2
- **Master plan link:** `docs/AI/tasks/hkd_book_accounting_fix_master_plan.md` Section 6.5 (Wave 5b)

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (IMPLEMENT phase — service logic + possible Domain modification)
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 5b of 12 (v3 — after Wave 5a, before Wave 5c)
- **Dependency:** Wave 5a merged (PIT base + account mapping done), W0-T10 result known
- **Blocks:** Wave 5c (2026 regulatory — uses industry rates from 5b if executed; if descoped, 5c uses default rate)
- **Tech Lead approval required:** W5b-T0 (CONDITIONAL — add `IndustrySector` to `Tenant` IF W0-T10 finds field missing)

---

## 3. CONDITIONAL EXECUTION (3 paths)

| W0-T10 Result | Tech Lead Approval | Action |
|---|---|---|
| `Tenant.IndustrySector` exists | N/A | ✅ Proceed normally (skip W5b-T0) |
| Field missing | ✅ Approves Domain mod | Add `IndustrySector` field to `Tenant` (W5b-T0), then proceed |
| Field missing | ❌ Does NOT approve | **DESCOPE Wave 5b** — use single default rate from W5a, log technical debt in `docs/AI/technical_debt.md`, proceed to Wave 5c |

**If descope triggered:** skip this wave entirely, document in `docs/AI/technical_debt.md`, proceed to Wave 5c (5c uses default rate, logs technical debt for industry-specific rates).

---

## 4. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc + sửa
- `docs/AI/project_state.md` (READ — context)
- `docs/AI/tasks/hkd_book_accounting_fix_master_plan.md` (READ — Section 6.5 Wave 5b + Cross-Wave Concerns 2026 Tax Rate Lookup Table)
- `3_CoreHub/Services/Orchestration/HKDRevenueClassificationService.cs` (READ + MODIFY — verify/create lookup table 4 nhóm ngành nghề)
- `3_CoreHub/Services/IHKDTaxClassificationService.cs` (READ — verify interface API)
- `3_CoreHub/Services/HKDTaxClassificationService.cs` (READ — verify implementation)
- `3_CoreHub/Services/Template/TemplateFactory.cs` (READ + MODIFY — S2aHKDTemplateImpl L177-249, thay default rate bằng `HKDRevenueClassificationService.GetVatRate(industry)`)
- `6_Tests/VanAn.Core.Tests/Services/HKDBookServiceTests.cs` (READ + MODIFY — add 2 unit tests)

### Files được phép sửa (CONDITIONAL — W5b-T0 only)
- `1_Shared/Domain/Tenant.cs` (MODIFY W5b-T0 — **CẦN TECH LEAD APPROVAL** — add `IndustrySector` field IF W0-T10 finds missing)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa `AccountingEntry` immutability — governance Hard Stop
- KHÔNG sửa `HKDTemplates.cs` (Domain account-number fix đã làm trong Wave 5a)
- KHÔNG thay đổi `IHKDBookService` interface
- KHÔNG cứng suất thuế — MUST dùng `HKDRevenueClassificationService` lookup
- KHÔNG dùng "2,5%" (fabricated — không tồn tại trong luật)
- KHÔNG thêm dependency mới mà không verify package có sẵn
- W5b-T0 chỉ execute IF W0-T10 finds field missing AND Tech Lead approves

---

## 5. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)

### 5.1 Suất thuế 4 nhóm ngành nghề (per Luật 2025 + ND 117/2025 — Amendment 5b)
| Nhóm ngành nghề | GTGT | TNCN (tính trên doanh thu — Nhóm 2) |
|---|---|---|
| Phân phối, cung cấp hàng hóa | **1%** | **0,5%** |
| Sản xuất, vận tải, dịch vụ có gắn với hàng hóa, xây dựng có bao thầu NVL | **3%** | **1,5%** |
| Dịch vụ, xây dựng không bao thầu nguyên vật liệu | **5%** | **2%** |
| Hoạt động kinh doanh khác | **2%** | **1%** |

**KHÔNG có "2,5%"** — v2 plan có "2,5%" là FABRICATED, đã sửa trong v3.

### 5.2 Industry-Sector Modeling (W5b-T4)
S2a template phải phân nhóm ngành nghề — mỗi ngành có:
- `Tổng cộng (n)` — total revenue cho ngành đó
- `Thuế GTGT (n)` — GTGT cho ngành (revenue × industry GTGT rate)
- `Thuế TNCN (n)` — TNCN cho ngành (revenue × industry TNCN rate)

### 5.3 Hardening Gates
- [ ] **Build:** `dotnet build VanAn.sln` Release — 0 errors
- [ ] **Guard:** guard-check.ps1 PASSED
- [ ] **Tests:** 2 unit test pass (industry rate + industry grouping)
- [ ] **Domain protection:** W5b-T0 chỉ execute IF W0-T10 finds field missing AND Tech Lead approves
- [ ] **No "2,5%":** Lookup table MUST dùng đúng 4 nhóm trên

---

## 6. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC) — only if executed
- [ ] **SC1:** `HKDRevenueClassificationService` có lookup table 4 nhóm ngành nghề (1%/0.5%, 3%/1.5%, 5%/2%, 2%/1%)
- [ ] **SC2:** `S2aHKDTemplateImpl` dùng `HKDRevenueClassificationService.GetVatRate(industry)` thay `TotalRevenue * defaultRate`
- [ ] **SC3:** S2a template có nhóm ngành nghề (mỗi ngành có Tổng cộng + GTGT + TNCN riêng)
- [ ] **SC4:** Unit test `S2aBook_VatAmount_ShouldUseIndustryRate_NotHardcoded5Percent` pass
- [ ] **SC5:** Unit test `S2aBook_IndustryGrouping_ShouldSeparateBySector` pass
- [ ] **SC6:** `dotnet build VanAn.sln` Release — 0 errors
- [ ] **SC7:** `dotnet test` — all pass
- [ ] **SC8:** guard-check.ps1 PASSED

---

## 7. ACTIVE SKILLS (MAX 3)
- `einvoice-integration` — Reference pattern cho tax rate lookup
- `dynamic-hkd-book-architecture` — HKD book domain knowledge + industry sector modeling
- `domain-integrity-validation` — Verify tax rate lookup + industry grouping correctness

---

## 8. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 4 verified facts from codebase + 1 legal source
- **Verified Facts:**
  - Fact 1: `HKDRevenueClassificationService` (Orchestration/) tồn tại — cần verify API có `GetVatRate(industry)` / `GetTncnRate(industry)`
  - Fact 2: `IHKDTaxClassificationService` (Services/) tồn tại — cần verify interface
  - Fact 3: `S2aHKDTemplateImpl` (L177-249) hiện dùng default rate từ Wave 5a (5% GTGT, 0.5% TNCN) — cần thay bằng industry rates
  - Fact 4: W0-T10 sẽ confirm `Tenant.IndustrySector` field existence (determines if W5b-T0 needed)
  - Fact 5 (legal): Nguồn chính thức — Luật 2025 + ND 117/2025 confirm 4 nhóm ngành nghề (1%/0.5%, 3%/1.5%, 5%/2%, 2%/1%)
- **Assumptions:**
  - `HKDRevenueClassificationService` có thể extend để thêm lookup table 4 nhóm ngành nghề
  - Tenant có data industry sector (W0-T10 sẽ confirm)
  - S2a template có thể thêm nhóm ngành nghề (TemplateField structure supports grouping)
- **Open Questions:**
  - Q1: `HKDRevenueClassificationService` API chính xác là gì? (W5b-T1 verify)
  - Q2: `Tenant.IndustrySector` exists? (W0-T10 — determines W5b-T0)
  - Q3: S2a template structure có support grouping by industry? (W5b-T4 verify)
- **Recommended Action:** PROCEED only if W0-T10 confirms field exists OR Tech Lead approves W5b-T0. Else DESCOPE.

---

## 9. REVERSE IMPACT ANALYSIS
| File modify | Reverse impact | Mitigation |
|---|---|---|
| `HKDRevenueClassificationService` (lookup table) | Thêm mapping suất thuế theo ngành nghề | Unit test W5b-T5 verify |
| `S2aHKDTemplateImpl` (industry rates) | VatAmount/TNCN tính đúng theo ngành nghề | Unit test W5b-T5 verify |
| `S2aHKDTemplateImpl` (industry grouping) | S2a book có nhóm ngành nghề riêng | Unit test W5b-T6 verify |
| `Tenant.cs` (W5b-T0, CONDITIONAL) | **HIGH RISK** — Domain modification (add field) | **Tech Lead approval required** — only if W0-T10 finds missing |

---

## 10. TDD & TESTING STRATEGY
- **Unit tests (NEW — 2 tests):**
  1. `S2aBook_VatAmount_ShouldUseIndustryRate_NotHardcoded5Percent`
     - Arrange: Tenant với ngành nghề "Phân phối" (1% GTGT), seed JournalEntries account 511 Credit 1000
     - Act: `GenerateS2aBookAsync`
     - Assert: `NumericValues["VatAmount"] == 10m` (1000 * 0.01), KHÔNG phải `50m` (1000 * 0.05 — default rate)
  2. `S2aBook_IndustryGrouping_ShouldSeparateBySector`
     - Arrange: Tenant với 2 ngành nghề (Phân phối 1% + Dịch vụ 5%), seed JournalEntries: account 511 Credit 600 (Phân phối) + account 5118 Credit 400 (Dịch vụ)
     - Act: `GenerateS2aBookAsync`
     - Assert: 2 group totals — Group "Phân phối" VatAmount = 6m (600 * 0.01), Group "Dịch vụ" VatAmount = 20m (400 * 0.05)
- **Integration tests:** N/A (Wave 7)
- **E2E tests:** N/A (Wave 8)
- **Verification:** `dotnet build VanAn.sln` Release + `dotnet test` pass

---

## 11. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: Verify API → Create lookup → Fix template → Add grouping → Tests
1. Verify `HKDRevenueClassificationService` API (W5b-T1) → 2. Create lookup table 4 nhóm (W5b-T2) → 3. Fix S2a template use industry rate (W5b-T3) → 4. Add industry grouping to S2a (W5b-T4) → 5. Add 2 unit tests (W5b-T5, W5b-T6) → 6. Build + test (W5b-T7)

### Micro-phase breakdown

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** (W5b-T1-T2) | - Đọc `HKDRevenueClassificationService` + `IHKDTaxClassificationService` API<br>- Chốt: lookup table 4 nhóm ngành nghề (1%/0.5%, 3%/1.5%, 5%/2%, 2%/1%)<br>- Chốt: W5b-T0 có cần không (W0-T10 result) | - Verify API (W5b-T1)<br>- Create/update lookup table (W5b-T2)<br>- If W5b-T0 needed + approved: add `IndustrySector` to `Tenant` |
| **S1/S2** (W5b-T3-T4) | - Chốt: S2a template structure cho industry grouping<br>- Chốt: cách query revenue per industry | - Fix `S2aHKDTemplateImpl` use `GetVatRate(industry)` (W5b-T3)<br>- Add industry grouping to S2a template (W5b-T4) |
| **S2** (W5b-T5-T7) | - Chốt: 2 unit test cases | - Add 2 unit tests (W5b-T5, W5b-T6)<br>- Run `dotnet build` + `dotnet test` (W5b-T7)<br>- Run guard-check.ps1 |

### Rules
- KHÔNG làm W5b-T0 trước Tech Lead approval (HARD STOP — Domain modification, IF needed)
- 1 fix tại 1 thời điểm — build + test verify sau mỗi cái
- Nếu W0-T10 finds field missing AND Tech Lead KHÔNG approve → **DESCOPE** (skip entire wave, log technical debt, proceed to Wave 5c)
- KHÔNG dùng "2,5%" — fabricated, không tồn tại trong luật

---

## 12. ESTIMATED EFFORT
- 1-2 sessions (if executed) / 0 sessions (if descoped)
- **BLOCKER:** Wave 5c (2026 regulatory — uses industry rates if 5b executed; if descoped, 5c uses default rate + logs technical debt)
- **CONDITIONAL:** W0-T10 result + Tech Lead approval determines execution
- **Tech Lead approval required:** W5b-T0 (CONDITIONAL — only if W0-T10 finds `Tenant.IndustrySector` missing)

---

## 13. Tenant.IndustrySector Status (from Wave 0 T10 — propagated 2026-07-03)

- **Field exists:** **NO** — `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs` has:
  - `Id` (TenantId), `Name` (string), `BusinessType` (enum), `HKDGroup?` (enum?), `Status` (TenantStatus), `Settings` (TenantSettings)
  - **NO `IndustrySector`, NO `BusinessSector`, NO `NganhNghe` field**
- **DECISION: Wave 5b CONDITIONAL — needs Tech Lead approval for W5b-T0 (add `IndustrySector` to `Tenant`) OR descope**
  - Option A: Add `IndustrySector` enum + field to `Tenant` (Domain modification — needs Tech Lead approval per governance Hard Stop)
  - Option B: **DESCOPE Wave 5b** — use default tax rate (e.g. 5%/2% per ND 117/2025 nhóm 3), log as technical debt, proceed to Wave 5c
- **Wave 5b action:** Before starting Wave 5b, confirm with Tech Lead:
  - If approved → execute W5b-T0 (add `IndustrySector` to `Tenant`) + W5b-T1-T7
  - If not approved OR descoped → skip Wave 5b, log technical debt in project_state.md, proceed to Wave 5c with default rate
- **Note:** Wave 5c (2026 Regulatory Compliance Fix) is MANDATORY and does NOT depend on Wave 5b. If 5b descoped, 5c uses default rate per ND 117/2025.
- **PARALLEL:** Không — Wave 5b phải hoàn thành (hoặc descope) trước Wave 5c

---

## 13. Tenant.IndustrySector Status (from Wave 0 T10 — propagated 2026-07-03)

- Field exists: **NO** — `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs` has:
  - `Id`, `Name`, `BusinessType`, `HKDGroup?`, `Status`, `Settings` (TenantSettings)
  - **NO `IndustrySector`, NO `BusinessSector`, NO `NganhNghe` field**
- **DECISION: Wave 5b CONDITIONAL — Tech Lead approval needed for W5b-T0 (add `IndustrySector` to `Tenant`)**
  - Options:
    - (a) Add `IndustrySector` enum + field to `Tenant` (Domain modification — needs Tech Lead approval per governance), OR
    - (b) **DESCOPE Wave 5b** — use single default tax rate from Wave 5a, log as technical debt in `docs/AI/technical_debt.md`, proceed to Wave 5c
  - Wave 5b **cannot proceed** without this decision
- **Wave 5b implication:**
  - Section 3 conditional table: **"Field missing" row is the active path** — either Tech Lead approves W5b-T0 OR descope
  - If descoped: Wave 5c uses default rate (from Wave 5a) + logs technical debt for industry-specific rates
  - **ACTION REQUIRED before Wave 5b:** User/Tech Lead must decide (a) approve Domain modification OR (b) descope Wave 5b
