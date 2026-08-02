# TASK CARD: HKD Book Fix - Wave 5c - 2026 Regulatory Compliance Fix (CRITICAL — pháp lý)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Fix `HKDRevenueClassificationService` threshold + TNCN formulas cho tuân thủ Luật Thuế GTGT/TNCN sửa đổi 2025 + ND 117/2025 + Nghị quyết 198/2025/QH15 (áp dụng từ 01/01/2026). Bug pháp lý nghiêm trọng — sai = báo cáo sai thuế = phạt hành chính.
- **Nghiệp vụ áp dụng:** 2026 regulatory compliance cho HKD — resolves phản biện pháp lý (session 2026-07-03) phát hiện `HKDRevenueClassificationService` threshold SAI hoàn toàn vs luật 2026.
- **Status:** PENDING — Planning & Approval (v3 — Amendment 5c)
- **Branch:** `feature/hkd-fix-wave5c-2026-regulatory-compliance`
- **Estimated Sessions:** 1-2
- **Master plan link:** `docs/AI/tasks/hkd_book_accounting_fix_master_plan.md` Section 6.7 (Wave 5c)

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (IMPLEMENT phase — code change + tests)
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 5c of 12 (v3 — after Wave 5a + 5b, before Wave 6)
- **Dependency:** Wave 5a merged (account mapping + PIT fix). Wave 5b merged OR descoped (industry rates — W5c-T3 uses industryRate from W5b if executed; if descoped, use default rate + log technical debt).
- **Blocks:** Wave 6 (tests assert numeric values — cần correct thresholds + formulas)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc + sửa
- `docs/AI/project_state.md` (READ — context)
- `docs/AI/tasks/hkd_book_accounting_fix_master_plan.md` (READ — Section 6.7 + Cross-Wave Concerns 2026 Tax Rate Lookup Table)
- `3_CoreHub/Services/Orchestration/HKDRevenueClassificationService.cs` (READ + MODIFY — L12-14 thresholds, L52-76 warnings)
- `3_CoreHub/Services/Orchestration/IHKDRevenueClassificationService.cs` (READ — interface)
- `3_CoreHub/Services/Template/TemplateFactory.cs` (READ + MODIFY — S2aHKDTemplateImpl TNCN formula, nếu tính thuế ở template layer)
- `3_CoreHub/Services/HKDBookService.cs` (READ — verify where tax calculation happens)
- `1_Shared/Domain.cs` (READ — `HKDRevenueGroup` enum, `HKDRevenueClassification` static class, verify 4 groups definition)

### Files được phép tạo (tests)
- `6_Tests/VanAn.Core.Tests/Services/HKDRevenueClassificationServiceTests.cs` (NEW or existing — add 2026 threshold tests)
- `6_Tests/VanAn.Core.Tests/Services/HKDBookTaxCalculationTests.cs` (NEW — TNCN formula tests per group)

### Boundary Rules
- **KHÔNG sửa Domain layer** (`1_Shared/Domain.cs`) trừ khi có Tech Lead approval — `HKDRevenueGroup` enum, `HKDRevenueClassification` static class
- **KHÔNG sửa `AccountingEntry` immutability** — governance Hard Stop
- **KHÔNG thay đổi public API** của `IHKDRevenueClassificationService` trừ khi cần thêm method cho Nhóm 3/4 TNCN calculation
- **Legal review recommended** trước khi implement — confirm 2026 regulatory changes với bộ phận pháp lý/thuế

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)

### 4.1 2026 Regulatory Requirements (NGUỒN: Luật Thuế GTGT/TNCN sửa đổi 2025 + ND 117/2025 + Nghị quyết 198/2025/QH15)
- [ ] **Threshold chịu thuế:** 500M → **1 TỶ VND** từ 01/01/2026 (Luật Thuế GTGT/TNCN sửa đổi 2025)
- [ ] **4 revenue groups mới:**
  - Nhóm 1: ≤ **1 TỶ VND/năm** (không chịu thuế GTGT + TNCN)
  - Nhóm 2: **trên 1 tỷ đến dưới 3 tỷ đồng** (GTGT theo ngành nghề, TNCN theo doanh thu hoặc lợi nhuận)
  - Nhóm 3: **trên 3 tỷ đến dưới 50 tỷ đồng** (TNCN bắt buộc theo lợi nhuận)
  - Nhóm 4: **trên 50 tỷ đồng** (TNCN bắt buộc theo lợi nhuận)
- [ ] **TNCN Nhóm 2:** `(Doanh thu - 1_000_000_000) × industryRate` (KHÔNG phải `Doanh thu × rate`) — hoặc `(Doanh thu - chi phí) × 15%` nếu xác định được chi phí
- [ ] **TNCN Nhóm 3:** `(Doanh thu - chi phí) × 17%` (bắt buộc theo lợi nhuận)
- [ ] **TNCN Nhóm 4:** `(Doanh thu - chi phí) × 20%` (bắt buộc theo lợi nhuận)
- [ ] **GTGT Nhóm 1:** 0 (exemption — không chịu thuế)
- [ ] **Thuế khoán BÃI BỎ** từ 01/01/2026 — tất cả kê khai + tự nộp (Nghị quyết 198/2025/QH15)
- [ ] **Lệ phí môn bài BÃI BỎ** từ 01/01/2026 (Điều 10, Nghị quyết 198/2025/QH15)
- [ ] **Khai thuế GTGT:** Theo quý (Nhóm 2/3/4)
- [ ] **Khai thuế TNCN:** Theo quý + quyết toán năm (31/1 năm sau)
- [ ] **Hóa đơn điện tử:** Bắt buộc nếu >1B (Nhóm 2), bắt buộc (Nhóm 3/4)

### 4.2 Suất thuế 4 nhóm ngành nghề (per ND 117/2025 — used by Nhóm 2 GTGT + TNCN-doanh-thu)
| Nhóm ngành nghề | GTGT | TNCN (tính trên doanh thu) |
|---|---|---|
| Phân phối, cung cấp hàng hóa | 1% | 0,5% |
| Sản xuất, vận tải, dịch vụ có gắn với hàng hóa, xây dựng có bao thầu NVL | 3% | 1,5% |
| Dịch vụ, xây dựng không bao thầu nguyên vật liệu | 5% | 2% |
| Hoạt động kinh doanh khác | 2% | 1% |

**KHÔNG có "2,5%"** — v2 plan có "2,5%" là FABRICATED, đã sửa trong v3.

### 4.3 Codebase evidence (đã verify)
- `HKDRevenueClassificationService` (L12-14): `Group1Threshold = 500_000_000m`, `Group2Threshold = 1_000_000_000m`, `Group3Threshold = 3_000_000_000m` — **SAI vs luật 2026** (phải là 1B / 3B / 50B)
- Service hiện tại chia 4 groups: ≤500M / 500M-1B / 1B-3B / >3B — **SAI hoàn toàn**
- `GetThresholdWarningsAsync` (L52-76) — warning messages + thresholds sai
- TNCN formula hiện tại (plan v2 Wave 5b): `PIT = TotalRevenue * industryRate` — **SAI** (Nhóm 2 phải trừ 1B trước)

### 4.4 Hardening Gates
- [ ] **Build:** `dotnet build VanAn.sln` Release — 0 errors
- [ ] **Guard:** guard-check.ps1 PASSED
- [ ] **Tests:** All new unit tests pass (5 tests: thresholds + 4 TNCN formulas + GTGT exemption)
- [ ] **Domain protection:** KHÔNG sửa `1_Shared/Domain.cs` (`HKDRevenueGroup` enum, `HKDRevenueClassification`) trừ khi Tech Lead approval. Nếu enum cần thêm `Group4` → báo Tech Lead.
- [ ] **Legal review:** Recommended trước khi implement — confirm 2026 regulatory changes

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `HKDRevenueClassificationService` thresholds: `Group1Threshold = 1_000_000_000m`, `Group2Threshold = 3_000_000_000m`, `Group3Threshold = 50_000_000_000m`, `Group4Threshold` (>50B) — per luật 2026
- [ ] **SC2:** `GetThresholdWarningsAsync` warning messages + thresholds updated per 4 nhóm mới (1B / 3B / 50B)
- [ ] **SC3:** TNCN Nhóm 2: `(Doanh thu - 1_000_000_000) × industryRate` (NOT `Doanh thu × rate`)
- [ ] **SC4:** TNCN Nhóm 3: `(Doanh thu - chi phí) × 17%`
- [ ] **SC5:** TNCN Nhóm 4: `(Doanh thu - chi phí) × 20%`
- [ ] **SC6:** GTGT Nhóm 1: 0 (exemption — revenue ≤ 1B)
- [ ] **SC7:** Thuế khoán + lệ phí môn bài abolished documented (UI labels, code comments)
- [ ] **SC8:** 5 unit test pass:
  - `RevenueGroup_ShouldUse2026Thresholds_1B_3B_50B`
  - `TNCN_Group2_ShouldSubtract1B_BeforeApplyingRate`
  - `TNCN_Group3_ShouldCalculateOnProfit_17Percent`
  - `TNCN_Group4_ShouldCalculateOnProfit_20Percent`
  - `GTGT_Group1_ShouldBeZero_WhenRevenueUnder1B`
- [ ] **SC9:** `dotnet build VanAn.sln` Release — 0 errors
- [ ] **SC10:** guard-check.ps1 PASSED

---

## 6. ACTIVE SKILLS (MAX 3)
- `dynamic-hkd-book-architecture` — HKD book architecture + tax calculation context
- `domain-integrity-validation` — Verify HKDRevenueGroup enum + HKDRevenueClassification static class
- `einvoice-integration` — 2026 e-invoice regulatory context (HĐĐT từ máy tính tiền)

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 4 verified facts from codebase + 1 legal source
- **Verified Facts:**
  - Fact 1: `HKDRevenueClassificationService` (L12-14) thresholds SAI: 500M / 1B / 3B (phải là 1B / 3B / 50B per luật 2026)
  - Fact 2: Service hiện tại chia 4 groups: ≤500M / 500M-1B / 1B-3B / >3B (SAI hoàn toàn vs luật 2026: ≤1B / 1B-3B / 3B-50B / >50B)
  - Fact 3: TNCN formula hiện tại (plan v2): `PIT = TotalRevenue * industryRate` (SAI — Nhóm 2 phải là `(Doanh thu - 1B) * industryRate`)
  - Fact 4: Plan v2 KHÔNG mention: thuế khoán abolished, lệ phí môn bài abolished, TNCN theo lợi nhuận (Nhóm 3/4)
  - Fact 5 (legal): Nguồn chính thức meinvoice.vn/MISA (14/04/2026) — trích Luật Thuế GTGT/TNCN sửa đổi 2025 + ND 117/2025 + Nghị quyết 198/2025/QH15 — confirm 4 nhóm doanh thu mới + TNCN formulas + threshold 1B
- **Assumptions:**
  - `HKDRevenueGroup` enum có 4 values (Group1-Group4) — cần verify (có thể cần thêm Group4 nếu chỉ có 3)
  - `HKDRevenueClassification.CalculateGroup` static method — cần verify logic threshold
  - Chi phí data có sẵn từ `AccountingEntries` (EntryType.Expense) cho TNCN Nhóm 3/4 formula
- **Open Questions:**
  - Q1: `HKDRevenueGroup` enum có Group4 không? (Verify — nếu không, cần Tech Lead approval để thêm)
  - Q2: TNCN calculation ở layer nào? (Service layer `HKDRevenueClassificationService` OR template layer `S2aHKDTemplateImpl`?)
  - Q3: Chi phí data có sẵn cho TNCN Nhóm 3/4 không? (Verify — cần `AccountingEntries` Expense sum)
- **Recommended Action:** PROCEED — but **legal review recommended** trước khi implement. Nếu Tech Lead không approve Domain mod ( thêm Group4) → workaround trong Service layer.

---

## 8. REVERSE IMPACT ANALYSIS
| File modify | Reverse impact | Mitigation |
|---|---|---|
| `HKDRevenueClassificationService.cs` (thresholds) | **HIGH** — affects toàn bộ HKD tax calculation, threshold warnings, revenue group classification | Unit tests cho 4 groups mới + integration test verify warning messages |
| `HKDRevenueClassificationService.cs` (warnings) | MEDIUM — warning messages hiển thị cho user | Update warning text per 2026 thresholds |
| `TemplateFactory.cs` (S2aHKDTemplateImpl TNCN formula) | HIGH — affects tax calculation trong S2a book report | Unit test TNCN formula per group |
| `HKDRevenueGroup` enum (if needs Group4) | **CRITICAL — Domain modification** → cần Tech Lead approval | If enum only has Group1-3 → STOP, báo Tech Lead. Workaround: dùng Group3 cho >3B (sai vs luật nhưng không crash) + log technical debt |
| UI labels (thuế khoán abolished) | LOW — label text only | Update labels, no logic change |

---

## 9. TDD & TESTING STRATEGY
- **Unit tests (NEW — 5 tests):**
  1. `RevenueGroup_ShouldUse2026Thresholds_1B_3B_50B` — assert Group1 ≤1B, Group2 1B-3B, Group3 3B-50B, Group4 >50B
  2. `TNCN_Group2_ShouldSubtract1B_BeforeApplyingRate` — revenue 2B, rate 0.5%, assert PIT = (2B - 1B) × 0.5% = 5M (NOT 2B × 0.5% = 10M)
  3. `TNCN_Group3_ShouldCalculateOnProfit_17Percent` — revenue 5B, chi phí 3B, assert PIT = (5B - 3B) × 17% = 340M
  4. `TNCN_Group4_ShouldCalculateOnProfit_20Percent` — revenue 60B, chi phí 40B, assert PIT = (60B - 40B) × 20% = 4B
  5. `GTGT_Group1_ShouldBeZero_WhenRevenueUnder1B` — revenue 800M, assert VatAmount = 0
- **Integration tests:** N/A (service layer test sufficient)
- **E2E tests:** N/A (Wave 8 handles UI)
- **Verification:** `dotnet build VanAn.sln` Release + `dotnet test` pass

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: Sequential fix → test → verify
1. Verify `HKDRevenueGroup` enum (Q1) → 2. Fix thresholds (T1) → 3. Fix warnings (T2) → 4. Add TNCN Nhóm 2 formula (T3) → 5. Add TNCN Nhóm 3 formula (T4) → 6. Add TNCN Nhóm 4 formula (T5) → 7. Add GTGT exemption Nhóm 1 (T6) → 8. Document abolished taxes (T7) → 9. Write 5 unit tests (T8-T12) → 10. Build + test pass (T13)

### Micro-phase breakdown

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** (T1-T2 — thresholds) | - Verify `HKDRevenueGroup` enum (có Group4?)<br>- Chốt: thresholds mới 1B / 3B / 50B / >50B<br>- Chốt: warning messages mới | - Modify `HKDRevenueClassificationService` L12-14 thresholds<br>- Modify `GetThresholdWarningsAsync` L52-76 warnings |
| **S1/S2** (T3-T6 — TNCN formulas) | - Chốt: TNCN calculation ở Service layer hay Template layer<br>- Chốt: chi phí data source (AccountingEntries Expense sum)<br>- Chốt: industryRate source (W5b lookup OR default) | - Add TNCN Nhóm 2: `(Doanh thu - 1B) × industryRate`<br>- Add TNCN Nhóm 3: `(Doanh thu - chi phí) × 17%`<br>- Add TNCN Nhóm 4: `(Doanh thu - chi phí) × 20%`<br>- Add GTGT exemption Nhóm 1: VatAmount = 0 |
| **S2** (T7 — documentation) | - Chốt: UI labels cần update (thuế khoán → kê khai, môn bài → abolished) | - Document thuế khoán + lệ phí môn bài abolished<br>- Update UI labels, code comments |
| **S2** (T8-T13 — tests + verify) | - Chốt: 5 unit test cases (thresholds + 4 TNCN + GTGT exemption) | - Write 5 unit tests<br>- Run `dotnet build VanAn.sln` Release<br>- Run `dotnet test`<br>- Run guard-check.ps1 |

### Rules
- 1 fix step tại 1 thời điểm
- **Nếu `HKDRevenueGroup` enum chỉ có Group1-3 → STOP, báo Tech Lead** (cần thêm Group4 — Domain modification)
- **Nếu Tech Lead không approve Domain mod → workaround:** dùng Group3 cho >3B (sai vs luật nhưng không crash) + log technical debt + báo user
- **Legal review recommended** trước khi implement — confirm 2026 regulatory changes
- **W5c KHÔNG skip** — CRITICAL pháp lý, fix-forward thay vì revert nếu fail

---

## 11. 2026 REGULATORY REFERENCE (legal source)

### Nguồn chính thức
- **meinvoice.vn/MISA** (14/04/2026) — "Cách tính thuế hộ kinh doanh 2026: Hướng dẫn chi tiết và ví dụ thực tế"
- **Luật Thuế GTGT sửa đổi 2025** — threshold 1B, 4 nhóm doanh thu
- **Luật Thuế TNCN 2025** — TNCN formulas per nhóm, method lợi nhuận
- **Nghị định 117/2025/NĐ-CP** — suất thuế 4 nhóm ngành nghề
- **Nghị quyết 198/2025/QH15** — thuế khoán abolished, lệ phí môn bài abolished

### 4 nhóm doanh thu 2026
| Nhóm | Doanh thu/năm | GTGT | TNCN |
|---|---|---|---|
| 1 | ≤ 1 tỷ | Không chịu thuế | Không chịu thuế |
| 2 | trên 1 tỷ - dưới 3 tỷ | Doanh thu × tỷ lệ GTGT (theo ngành nghề) | `(Doanh thu - 1 tỷ) × tỷ lệ TNCN` OR `(Doanh thu - chi phí) × 15%` |
| 3 | trên 3 tỷ - dưới 50 tỷ | Doanh thu × tỷ lệ GTGT (theo ngành nghề) | `(Doanh thu - chi phí) × 17%` (bắt buộc lợi nhuận) |
| 4 | trên 50 tỷ | Doanh thu × tỷ lệ GTGT (theo ngành nghề) | `(Doanh thu - chi phí) × 20%` (bắt buộc lợi nhuận) |

### Suất thuế 4 nhóm ngành nghề (ND 117/2025)
| Nhóm ngành nghề | GTGT | TNCN (Nhóm 2, tính trên doanh thu) |
|---|---|---|
| Phân phối, cung cấp hàng hóa | 1% | 0,5% |
| Sản xuất, vận tải, dịch vụ có gắn với hàng hóa, xây dựng có bao thầu NVL | 3% | 1,5% |
| Dịch vụ, xây dựng không bao thầu nguyên vật liệu | 5% | 2% |
| Hoạt động kinh doanh khác | 2% | 1% |

### Ví dụ thực tế (từ meinvoice.vn)
Anh A, cửa hàng bán lẻ đồ điện tử (Phân phối), doanh thu 2 tỷ, chi phí 1.5 tỷ:
- **TH1 (TNCN theo doanh thu):** GTGT = 2B × 1% = 20M; TNCN = (2B - 1B) × 0.5% = 5M; Tổng = 25M
- **TH2 (TNCN theo lợi nhuận):** GTGT = 2B × 1% = 20M; TNCN = (2B - 1B) × 15% = 150M; Tổng = 175M

---

## 12. ESTIMATED EFFORT
- 1-2 sessions (code change + 5 unit tests)
- **BLOCKER:** Wave 6 — tests assert numeric values, cần correct thresholds + formulas
- **CRITICAL:** Pháp lý — sai = báo cáo sai thuế = phạt hành chính
- **NOT RECOMMENDED FOR REVERT:** Nếu fail, fix-forward thay vì revert (revert = quay lại threshold 500M sai = vẫn sai thuế)
- **Legal review recommended** trước khi implement
- **PARALLEL:** Không — Wave 5c phải hoàn thành trước Wave 6
