# MASTER PLAN: ĐÓNG GAP TT 133 BCTC MẪU + TT 58/2026 DNSN

> Created: 2026-10-30 — user directive: "giải quyết dứt điểm đầy đủ, lập master plan và task card, đối chiếu 58_2026_TT-BTC_707080.docx"
> Source review: rà soát module kế toán 2026-10-30 (Gap #1: TT133 BCTC flat-list thay vì mẫu B01a/B02/B03/B09-DNN · Gap #2: TT 58/2026 chỉ có enum, resolver map sai sang TT133)
> Branch: `main`
> Workflow: `.devin/workflows/newfeaturebuild.md` (ANALYZE → IMPLEMENT)
> Status: **⏳ CHỜ USER CHỐT D1-D5 rồi IMPLEMENT tuần tự theo task cards**

---

## 1. MỤC TIÊU

Đóng dứt điểm 2 gap tuân thủ phát hiện trong review 2026-10-30:

| Gap | Nội dung | Hiện trạng |
|---|---|---|
| **#1** | TT 133/2016 (DN vừa & nhỏ): BCTC phải theo mẫu **B01a-DNN + B02-DNN + B09-DNN** (bắt buộc) · **B03-DNN** (khuyến khích) · **F01-DNN** (bảng cân đối TK, kèm khi nộp thuế) | ❌ `BalanceSheetService`/`IncomeStatementService` rơi về `GenerateFlatAsync` — danh sách TK phẳng, SAI mẫu |
| **#2** | TT 58/2026 (DN siêu nhỏ, hiệu lực 01/07/2026, thay TT 132/2018): hệ thống **sổ S1/S2a-d/S3a/S3b/S4a-d-DNSN** theo phương pháp thuế + BCTC **B01-DNSN + B02-DNSN** | ❌ Chỉ có enum `TT58_2026` + `Enterprise_SuperSmall`; resolver map sang TT133 (sai); không sổ/báo cáo nào |

Ngoài phạm vi (ghi nhận, không làm): B01b-DNN (mẫu tùy chọn thay B01a — defer) · B01-DNNKLT/B09-DNNKLT (không hoạt động liên tục — hiếm) · TT 24/2024 (đơn vị HCSN — ngoài phân khúc sản phẩm) · IFRS TT 118/2026.

---

## 2. BẰNG CHỨNG & SPEC ĐỐI CHIẾU

| Tài liệu | Nguồn | File spec |
|---|---|---|
| TT 58/2026/TT-BTC toàn văn | `docs/Accounting_Doc/58_2026_TT-BTC_707080.docx` — đã trích xuất text | `docs/AI/tasks/tt133_tt58_compliance/spec/tt58-2026-full-text.txt` |
| TT 133/2016/TT-BTC Phụ lục 2 (biểu mẫu BCTC) | docx chính thức tải về + trích xuất | `docs/AI/tasks/tt133_tt58_compliance/spec/tt133-pl2-bctc-forms.txt` |

### 2.1 TT 133 — hệ thống BCTC (Điều 71 Phụ lục 2)

- **Bắt buộc:** B01a-DNN + B02-DNN + B09-DNN · **Khuyến khích:** B03-DNN · **Nộp thuế:** thêm F01-DNN
- **B01a-DNN** mã số: TS 110 (Tiền&TĐT) · 120 ĐTTC (121-124) · 130 Phải thu (131-136) · 140 HTK (141-142) · 150 TSCĐ (151-152) · 160 BĐSĐT (161-162) · 170 XDCB · 180 TS khác (181-182) · **200 = 110+120+130+140+150+160+170+180** ‖ NV: 300 Nợ phải trả (311-320) · 400 VCSH (411-417) · **500 = 300+400**
- **B02-DNN** mã số: 01/02/10/11/20/21/22/23/24/30/31/32/40/50/51/60 — khác TT99: mã 24 "Chi phí quản lý kinh doanh" gộp 641+642; mã 51 "Chi phí thuế TNDN" gộp (không tách 821/822); không có mã 21 BĐSĐT.
- **B03-DNN**: direct 01-07→20, 21-25→30, 31-35→40, 50=20+30+40, 60/61/70 — cùng khung mã với TT99 B03-DN nhưng ít dòng hơn (mã 23/24/34/35 gộp hơn TT99).

### 2.2 TT 58/2026 — 4 trường hợp sổ theo phương pháp thuế (Điều 4→8)

| Trường hợp | Thuế GTGT | Thuế TNDN | Sổ bắt buộc |
|---|---|---|---|
| Điều 5 | % trên DT | % trên DT | **S1-DNSN** (doanh thu theo nhóm ngành nghề + dòng Tổng cộng/Thuế GTGT/Thuế TNDN) |
| Điều 6 | % trên DT | trên thu nhập | **S2a-DNSN** (doanh thu) + **S2b-DNSN** (DT/CP chi tiết) + **S2c-DNSN** (VL/DC/SP/HH) + **S2d-DNSN** (tiền) |
| Điều 7 | khấu trừ | % trên DT | **S3a-DNSN** (doanh thu) + **S3b-DNSN** (nghĩa vụ thuế GTGT) |
| Điều 8 | khấu trừ | trên thu nhập | S2b + S2c + S2d + S3b |

- Điều 9 (tùy chọn): S4a công nợ · S4b TSCĐ · S4c thuế khác · S4d VCSH.
- Điều 10 BCTC: **B01-DNSN** (TS 110/120/130/140/150 → 200 · NV 300=310+320 · 400=410+420+430 · 500=300+400) + **B02-DNSN** (01/02/03/10/20). KHÔNG B03, KHÔNG B09. BCTC chỉ bắt buộc nộp khi TNDN trên thu nhập; nộp % vẫn lập nhưng không bắt buộc nộp.
- Điều 2.2: HKD/cá nhân KD được CHỌN áp dụng TT 58.
- Điều 11: chuyển số dư từ TK TT 132 → sổ khi chuyển chế độ.
- TT 58 **không có hệ thống TK** — chế độ sổ (giống TT152 HKD), KHÔNG seed AccountChart cho TT58_2026 (xác nhận lại quyết định seeder hiện tại — đúng).

### 2.3 Kiến trúc hiện có tái dùng

- `Tt99ReportTemplate`/`Tt99TemplateLine` (Mã số → AccountCodes → hierarchy) — đã tái dùng cho `Tt71Templates`; TT133 + TT58 BCTC dùng cùng cơ chế.
- `GenericHKDBook` + `HKDBookTemplate` + `TemplateCalculationEngine` — sổ DNSN tương tự sổ HKD; S1/S2a≈S2a-HKD (nhóm ngành), S2b≈S2c-HKD (DT/CP), S2c≈S2d-HKD, S2d≈S2e-HKD (tiền).
- `IGeneralLedgerService` (NK chung/Sổ Cái/Sổ chi tiết) — nền dữ liệu JournalEntry cho mọi sổ.
- `AccountingStandardResolver` — cần sửa: `Enterprise_SuperSmall` → `TT58_2026`.
- `FinancialStatementNotesService` — đã switch theo standard (thêm nhánh TT133 B09-DNN + không áp dụng cho TT58).
- Xuất Excel: `LedgerExcelExportService` + `VaIIeExportService` (EPPlus) — tái dùng pattern Excel TT99.

---

## 3. PHẠM VI — PHASES + TASK CARDS

| Phase | Nội dung | Task card | Dep |
|---|---|---|---|
| **P1** | **TT133 report templates** — `Tt133Templates.cs` (B01a/B02/B03 direct+indirect) + wire vào BalanceSheet/IncomeStatement/CashFlow services + B09-DNN notes + F01 export-label | `task_p1_tt133_bctc_templates.md` | — |
| **P2** | **TT 58/2026 chế độ DNSN** — `DnsnTaxMethod` config trên tenant + 9 loại sổ DNSN (S1/S2a-d/S3a/S3b/S4a-d tùy chọn) + sổ generation + B01-DNSN/B02-DNSN + resolver fix | `task_p2_tt58_dnsn_regime.md` | P1 (dùng chung infra) |
| **P3** | **UI + xuất mẫu** — FinancialReports hub hiển thị đúng form theo regime (DNN: B01a/B02/B03/B09/F01 · DNSN: sổ + B01/B02) + Excel/In đúng mẫu + cập nhật checklist nội bộ (bỏ TT 132/2018, thêm TT 58/2026 + TT 152/2025) | `task_p3_ui_export_checklist.md` | P1+P2 |
| **P4** | **Tests + Deploy + RV** — Core.Tests template structure + bUnit UI + E2E spec Gate 4 + FAST PUSH + RV L1-L3 | `task_p4_tests_rv.md` | P1-P3 |

---

## 4. QUYẾT ĐỊNH USER — ✅ ĐÃ CHỐT (2026-10-30)

| # | Câu hỏi | ✅ QUYẾT ĐỊNH |
|---|---|---|
| **D1** | TT133 render form nào? | **Chỉ B01a-DNN** (bắt buộc chuẩn; B01b tùy chọn — defer roadmap) |
| **D2** | Sổ tùy chọn Điều 9 (S4a-d)? | **Chỉ S4a-DNSN (công nợ)** — tái dùng CongNoService; S4b/S4c/S4d defer |
| **D3** | Cấu hình trường hợp thuế TT58? | **`TenantSettings.DnsnTaxMethod`** (enum 4 giá trị — Domain change, Gate 5 APPROVED) |
| **D4** | HKD chọn áp dụng TT58 (Điều 2.2)? | **Cho phép** — `Tenant.AccountingStandard=TT58_2026` override; TT152 vẫn mặc định HKD |
| **D5** | Khi nào áp TT58? | Hiệu lực 01/07/2026 đã quá — áp ngay, không backdate |

---

## 5. THIẾT KẾ CHI TIẾT (tóm tắt)

### P1 — TT133 templates
- `3_CoreHub/Services/Data/Tt133Templates.cs`: `BalanceSheetTt133` (B01a-DNN mã 110-200/300-500 + công thức), `IncomeStatementTt133` (B02-DNN 01-60 + công thức khác TT99: 30=20+21-22-24, 50=30+40, 60=50-51), `CashFlowDirectTt133` + `CashFlowIndirectTt133`.
- Wire `BalanceSheetService`/`IncomeStatementService`: `TT133_2016` → template path (bỏ flat fallback cho TT133; giữ flat cho trường hợp lạ).
- `CashFlowStatementService`: thêm subtotal mã số cho TT133.
- `FinancialStatementNotesService`: nhánh TT133 → mục B09-DNN (kiểm chứng cấu trúc vs spec).
- `TrialBalance` UI → label/xuất "Bảng cân đối tài khoản (F01-DNN)" cho tenant TT133.
- W2 invariant giữ nguyên (200==500 / 10=01-02 ...). NetIncome plug vào Mã 417 (LNST chưa phân phối) — khác TT99 (Mã 420)!

### P2 — TT 58 DNSN
- Domain: `AccountingBookType += S1_DNSN, S2a_DNSN, S2b_DNSN, S2c_DNSN, S2d_DNSN, S3a_DNSN, S3b_DNSN, S4a_DNSN` (append-only) · `TenantSettings.DnsnTaxMethod` (enum `DnsnTaxMethod` — Domain change, cần Gate 5 approval D3).
- `Tt58Templates.cs`: template fields cho từng sổ (S1: doanh thu nhóm ngành + GTGT + TNDN theo tỷ lệ % ngành — tái dùng `IndustryVatRates`/`IndustryPitRates`; S3b: GTGT đầu vào/đầu ra khấu trừ).
- `DnsnBookService` (hoặc mở rộng `HKDBookGenerationService`): chọn danh mục sổ theo `DnsnTaxMethod`; B01-DNSN/B02-DNSN bằng `Tt99ReportTemplate` + service nhỏ (14 mã + 5 mã — map từ số dư sổ/journal: 110←111+112, 120←131+138+141+133, 130←15x, 140←211-214, 150←còn lại, 310←331+334+335+338+341, 320←333, 410←411, 420←421, 430←418).
- `AccountingStandardResolver`: `Enterprise_SuperSmall` → `TT58_2026`; `HTX`→TT71; `Enterprise_Large`→TT99; `Enterprise_SME`→TT133; HKD→TT152-implied (giữ caveat: HKD không render BCTC DNN).
- UI sổ DNSN: danh mục sổ động theo DnsnTaxMethod trên trang sổ.

### P3 — UI + xuất + checklist
- FinancialReports hub: TT133 → tab B01a/B02/B03/B09/F01; TT58 → tab sổ DNSN + B01/B02-DNSN; ẩn B03/B09 cho TT58.
- Excel export: thêm layout B01a-DNN/B02-DNN/F01-DNN (TT133) + B01-DNSN/B02-DNSN (TT58) theo đúng khung mẫu (header "Mẫu số …", footer ký).
- `checklist_kiem_soat_phan_mem_ke_toan_erp_UPDATED_2026.md`: cập nhật §0.1 (TT 58/2026 thay TT 132/2018 · TT 152/2025 HKD · TT 71/2024) + mục 1.3/3.1 đúng tên mẫu từng chế độ.

### P4 — Tests + RV
- Core.Tests: `Tt133TemplatesTests` (cấu trúc mã số + công thức + W2 invariant), `Tt58TemplatesTests`, `DnsnBookServiceTests` (4 trường hợp danh mục sổ), resolver test.
- ShopERP.Tests: hub render đúng form/regime.
- E2E (Gate 4): spec `accounting-regime-forms.spec.ts` — tenant TT133 ra B01a-DNN đúng mã số; tenant SuperSmall ra sổ S1/S2a + B01-DNSN.
- FAST PUSH → CD → RV L1-L3 (markers + routes + E2E production).

---

## 6. CHIẾN LƯỢC THỰC THI (user chốt 2026-10-30: an toàn · đầy đủ · nhanh)

**Nguyên tắc:** 100% code additive — file mới + nhánh `switch` mới; KHÔNG sửa nhánh TT99/TT71/TT152; mỗi phase = 1 commit riêng (rollback dễ).

| Wave | Nội dung | Commit | An toàn |
|---|---|---|---|
| **W1** | P1 TT133 — `Tt133Templates.cs` (mới) + nhánh TT133 trong 3 services + B09 verify + tests trước | 1 | Không đụng code đang chạy của regime khác |
| **W2** | P2 TT58 — Domain (Gate 5 đã duyệt D3: `DnsnTaxMethod` + enum `AccountingBookType` append) + `Tt58Templates` + `DnsnBookService` + resolver fix + migration settings nếu cần + tests | 1-2 | Append-only enum; resolver change 1 dòng có test |
| **W3** | P3 UI + export + checklist — regime-aware hub/menu + `RegimeReportExportService` + update checklist doc | 1 | UI-only + docs |
| **W4** | P4 — chạy full suite + FAST PUSH (1 lần) → CD → RV L1-L3 + E2E spec | — | 1 deploy duy nhất (CD ~12 phút) |

**Nhanh:** 1 phiên liên tục W1→W3, 1 push, 1 deploy, 1 RV.
**An toàn:** tests viết trước mỗi file service; W2 invariant (200=500 / 60=50−51) bắt buộc trong mọi template; chạy lại toàn bộ suite trước push (không chỉ test mới); Playwright tắt trong IMPLEMENT (Gate 3), chỉ bật ở RV.

## 7. RỦI RO

| # | Rủi ro | Mitigation |
|---|---|---|
| R1 | NetIncome plug Mã khác nhau (417 TT133 vs 420 TT99 vs 420 HTX) — nhầm mã | Tách hằng số per-standard trong service; test invariant từng form |
| R2 | TT133 không tách ngắn/dài hạn trong B01a — map 1 TK vào 1 mã | Mapping prefix như TT71 đã làm; số dư Có của TK tài sản → chỉ tiêu nợ phải trả tương ứng (ghi nhận limitation như TT71) |
| R3 | DnsnTaxMethod sai → danh mục sổ sai | Default VatPercent+TndnPercent (phổ biến nhất cho siêu nhỏ); hiển thị rõ trường hợp đang áp trên UI; đổi chỉ từ đầu năm TC (Điều 4.3) |
| R4 | Regress báo cáo TT99/TT71/HKD hiện có | Không sửa nhánh TT99/TT71 — chỉ thêm nhánh TT133/TT58; chạy lại toàn bộ Core.Tests/ShopERP.Tests |
| R5 | Spec TT133 trích từ docx web (chedo.ketoanthue.net) — cần đối chiếu bản Bộ TC | Task card P1 yêu cầu verify mã số với spec file trước khi code; mọi dòng template ghi nguồn |

---

## 7. SUCCESS CRITERIA (DoD)

- [ ] Tenant `Enterprise_SME` xuất B01a-DNN/B02-DNN/B03-DNN/B09-DNN đúng mã số + cân 200=500 · F01-DNN label đúng
- [ ] Tenant `Enterprise_SuperSmall` hiển thị danh mục sổ đúng trường hợp Đ5-Đ8 + sổ render + B01-DNSN/B02-DNSN đúng mã số + cân
- [ ] Resolver đúng: SuperSmall→TT58 · SME→TT133 · Large→TT99 · HTX→TT71
- [ ] Checklist nội bộ cập nhật (TT 58/2026, TT 152/2025 — bỏ TT 132/2018)
- [ ] guard + build + Core.Tests + ShopERP.Tests PASS · Deploy + RV L1-L3 PASS
