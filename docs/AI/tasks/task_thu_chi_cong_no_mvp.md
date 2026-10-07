# TASK CARD: THU CHI & CÔNG NỢ (PHẢI THU / PHẢI TRẢ) MVP

> Created: 2026-10-06 (user directive: "chỉ cần thu chi, báo cáo công nợ, phải thu phải trả, theo dõi phải trả phải thu" + chốt Q1-Q5)
> Source SRS: `docs/requirements/van_an_thu_chi_cong_no_srs_v1.md` (v1.1 — user chốt Q1-Q5 2026-10-06)
> Master plan: `docs/AI/plans/thu-chi-cong-no-master-plan.md` (ACTIVE — chờ approve)
> Branch: `main`
> Status: **✅ APPROVED (2026-10-06, user) — ✅ Session 1: P1 Services DONE (2026-10-07 — Core.Tests 2130 PASS · guard ALL PASSED) — ✅ Session 2: P2 UI DONE (2026-10-07 — ShopERP.Tests 161 PASS · guard ALL PASSED) — ✅ Session 3: P3 E2E + hardening DONE (2026-10-07 — guard ALL PASSED · Core.Tests 2130 · ShopERP.Tests 161 · Architecture PASS · build 0 errors) — ⏳ Session 4: P4 Deploy + RV (kế tiếp)**

---

## 1. GOAL & CONTEXT

- **Mục tiêu:** mở rộng module kế toán hiện tại đáp ứng 4 nhu cầu: (1) thu chi (đã có) · (2) phải thu (khách nợ) · (3) phải trả (nợ người bán) · (4) theo dõi từng đối tượng (phát sinh → đã thu/trả → còn lại) + **tuổi nợ** (Q4) + **truy ngược lịch sử thanh toán** (Q5) + **tra MST → tên công ty** (Q3).
- **Nguyên tắc (SRS §4 G1-G13 — kịch bản đơn giản nhất):** KHÔNG entity/bảng/migration mới — tận dụng `AccountingEntry` (immutable) + `AccountCode 131/331` + `Vendor` + `ReferenceType` · nhập tay qua phiếu (không đụng Order) · bán chịu không ghi 511 · sửa sai = reversal.
- **Đã xác minh code (2026-10-06):** `AccountingEntryService` server **không chặn** 131/331 + Amount âm (chỉ UI whitelist 5xx/7xx + 6xx) · `BusinessInfoApiClient` + Gateway `GET /api/v1/business-info/{mst}` **đã có** (phiếu thu/chi đang dùng) · `AccountingEntry.Vendor/ReferenceType/ReferenceId` sẵn sàng.
- **Điều kiện trigger — ĐÃ THỎA:** SRS v1.1 hoàn tất + Q1-Q5 user chốt (2026-10-06).

## 2. ACTIVE WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT — 7 step)
- **Execution Mode:** IMPLEMENT (user approved 2026-10-06) — Session 1 P1 Services
- **Skills (max 3):** `domain-integrity-validation` (AccountingEntry immutable — KHÔNG đụng Domain.cs) + `pattern-based-fixing` (regress báo cáo) + `test-system-upgrade` (test matrix)
- **Session strategy (session-per-phase — precedent Booking/VA-IIE):**
  - Session 1 (P1): Services core + FIFO aging + tests → guard/build/Core.Tests PASS → commit ✅ DONE 2026-10-07
  - Session 2 (P2): UI phiếu thu/chi 3 loại + 2 trang + menu + bUnit → guard/ShopERP.Tests PASS → commit ✅ DONE 2026-10-07
  - Session 3 (P3): E2E spec + full test matrix → guard/build/tests PASS ✅ DONE 2026-10-07
  - Session 4 (P4): PUSH → CD Multi-VPS (KHÔNG migration) → RV L1-L5 → đóng ⏳ KẾ TIẾP

## 3. SCOPE (4 phases per master plan §4)

### Phase 1 — Services core + tests (task `task_thu_chi_cong_no_phase1_services.md`) ⏳

> **Session P1 PREP (làm ở session MỚI — base `d56be1c9` = plan + task card approved):**
> Đọc trước: `docs/AI/project_state.md` (mục 2/3/4 — Thu chi & Công nợ) + SRS v1.1 `docs/requirements/van_an_thu_chi_cong_no_srs_v1.md` (§5.1-5.2, §6, giả định G1-G13) + master plan §5.1-5.2 + `3_CoreHub/Services/AccountingEntryService.cs` (CreateRevenue/ExpenseEntryAsync + CheckDuplicateEntryAsync — pattern phiếu) + `IAccountingService.cs` + `HKDBookService.cs` (phiếu → sổ HKD B01-B09).
> **PHÁT HIỆN KIẾN TRÚC (2026-10-06 — đã verify):** (1) phiếu thu/chi (`AccountingEntry`) nuôi **sổ HKD** (`HKDBookService.RecordRevenueAsync/RecordExpenseAsync` → B01-B09) — KHÔNG phải IncomeStatement/TrialBalance (các báo cáo DN này đọc **JournalEntry** riêng). → entry công nợ 131/331 sẽ **tự xuất hiện trong sổ nhật ký + sổ cái HKD** (đúng bản chất kế toán — sổ cái TK 131/331 là nơi theo dõi); [G6] = verify B02 (kết quả KD HKD) lọc theo AccountCode 5xx/6xx → 131/331 không lọt (dự kiến tự nhiên đúng — cần test chốt). (2) `AccountingEntryService` server **không chặn** 131/331 + Amount âm (chỉ UI whitelist 5xx/7xx + 6xx). (3) `AccountingEntry` có `Vendor` + `ReferenceType`/`ReferenceId` sẵn sàng.
> **Open items P1 (quyết định tại P1):** (1) EntryType cho phiếu công nợ — nếu HKD sổ cái/B02 lọc theo AccountCode thì EntryType Revenue/Expense hay Adjustment không ảnh hưởng; chọn theo pattern gần nhất (RecordRevenueAsync) + test chốt [G6]; (2) `CheckDuplicateEntryAsync` (cùng amount+account+type trong 5') — phiếu công nợ trùng số tiền hợp lệ (2 khách cùng 5tr) → cân nhắc bỏ qua duplicate-check cho ReferenceType công nợ hoặc reference-aware; (3) FIFO tuổi nợ — tie-break `TransactionDate` + `CreatedAt`; (4) transactionDate truyền ngày user nhập (lesson phiếu thu — không dùng UtcNow).
>
> **✅ P1 DECISIONS (2026-10-07 — Session 1, ghi nhận để P2/P3/P4 dùng):**
> 1. **EntryType = `Adjustment` + BookType = `CashBankBook`** cho phiếu công nợ — qua factory **additive** `AccountingEntry.CreateDebt(...)` (P1.2). Lý do [G6] đã verify: `CreateRevenue` KHÔNG nhận vendor (đối tượng bắt buộc); `CreateExpense` buộc Expense semantics → lọt `GetExpenseTotalAsync`; RevenueBook/Expense lọt `GetRevenueTotalAsync`/`GetTodayRevenueAsync` (lọc theo BookType/EntryType). Adjustment+CashBankBook trung tính ở mọi nơi (PeriodClosing chỉ tách Revenue/Expense; HKDTaxReporting CashBankBook → null). Factory thuần additive — giữ nguyên immutability, KHÔNG cột/bảng/migration.
> 2. **Bỏ qua duplicate-check 5'** cho phiếu công nợ (nhập tay chủ ý như bút toán sổ cái; 2 khách cùng 5tr hợp lệ — `CongNoService` gọi `repository.AddAsync` trực tiếp, không qua `AccountingEntryService`). **Giữ period-closing guard** (kỳ đóng → chặn).
> 3. **FIFO tie-break** `TransactionDate` + `CreatedAt`; **cặp (gốc + đảo) net = 0 bị loại khỏi stream** (đảo = hủy bút toán — scenario C; reversal-of-reversal net ≠ 0 giữ nguyên); reversal entry resolve vendor về entry gốc (`CreateReversal` không copy Vendor).
> 4. **transactionDate user nhập** → period tự suy ra từ transactionDate (không param period riêng — tránh lệch kỳ); **tuổi nợ tính tại cuối kỳ báo cáo** (không phải hôm nay — deterministic + đúng mockup FR-7).
> 5. `ReferenceType` (RECEIVABLE/PAYABLE...) KHÔNG dùng được — domain field không settable qua factory (KHÔNG đụng Domain.cs thêm). Phân biệt loại phiếu bằng **AccountCode 131/331 + dấu Amount (+ = ghi nợ, − = thu/trả nợ)** — đủ cho FIFO + báo cáo. `Reference` string để chừa chứng từ.
> 6. **[G6] verified trong code (P1.1):** IncomeStatement (DN/HTX) + TrialBalance + HKD B01-B09 đọc **JournalEntry** riêng (KHÔNG phải AccountingEntry) → entry công nợ KHÔNG BAO GIỜ lọt (PREP note cũ nói "phiếu thu nuôi sổ HKD qua RecordRevenueAsync" — thực tế `RecordRevenueAsync` là dead code, HKD books đọc JournalEntries do OrderService/Seeder tạo). Kênh rò rỉ duy nhất = GetRevenueTotalAsync/GetExpenseTotalAsync (BookType) + GetTodayRevenueAsync (EntryType) → đã chặn bởi decision #1.
> **Tái dùng:** `IAccountingEntryRepository` (HKDBookService pattern) · filter TenantId (lesson a21f97f2) · `VanAnDbContextTestFactory` global filter → dùng `IgnoreQueryFilters()` đúng chỗ (lesson P3 booking) · reversal pattern (`AccountingEntry` ReversalEntries).
> **Validation cuối session:** guard-check + build VanAn.sln + Core.Tests + ShopERP.Tests MUST PASS → commit (KHÔNG push trừ khi user yêu cầu).

- [x] **P1.1 Xác minh nền [DONE 2026-10-07]:** IncomeStatement/TrialBalance/B01-B09 đọc **JournalEntry** riêng (KHÔNG phải AccountingEntry) → entry 131/331 KHÔNG lọt doanh thu/chi phí [G6] ✓ (chi tiết: P1 decisions #6). Kênh rò rỉ duy nhất = revenue/expense totals theo BookType/EntryType → chốt EntryType=Adjustment + BookType=CashBankBook (decision #1). Duplicate-check: bỏ qua cho phiếu công nợ (decision #2 — 2 khách cùng số tiền hợp lệ).
- [x] **P1.2 `ICongNoService`/`CongNoService` [DONE 2026-10-07]:** `3_CoreHub/Services/CongNo/` (namespace `VanAn.CoreHub.Services.CongNo` — precedent Booking):
  - `CreateReceivableAsync(tenantId, amount, doiTuong, description?, transactionDate, isPayment, mst?, reference?)` — 131 dương (bán chịu) / âm (thu nợ) — diễn giải mặc định `"Bán chịu — {đối tượng} (MST xxx)"` / `"Thu tiền khách trả nợ — {đối tượng}"` [G11]
  - `CreatePayableAsync(...)` — 331 tương tự (`"Mua chịu — ..."` / `"Trả tiền người bán — ..."`)
  - `GetCongNoReportAsync(tenantId, year, month)` → `CongNoReportDto` (2 khối 131/331; mỗi đối tượng: Đầu kỳ · PS tăng · Đã thu/trả · Cuối kỳ · **Tuổi nợ <30/30-60/60-90/>90 — FIFO động** [G12] tại cuối kỳ)
  - `GetDoiTuongLedgerAsync(tenantId, accountCode, vendor, year, month)` — sổ cộng dồn (Ngày · Diễn giải · Tăng · Giảm · Số dư + đầu kỳ/cuối kỳ; entry đảo hiển thị kèm cặp gốc — audit-friendly)
  - `GetKhoanNoPaymentsAsync(tenantId, khoanNoEntryId)` — lịch sử thanh toán 1 khoản (FIFO động, mỗi lần + số dư còn lại) [FR-8.1]; null nếu khoản không tồn tại/đã đảo
  - Mọi query filter TenantId (lesson a21f97f2) · repo mới `IAccountingEntryRepository.GetByTenantAndAccountCodesAsync` (lọc theo TransactionDate — không CreatedAt — đúng ngày nghiệp vụ) · DI trong `3_CoreHub/Program.cs`
- [x] **P1.3 Reversal [DONE 2026-10-07]:** tái dùng cơ chế reversal hiện có (`AccountingEntry.CreateReversal`) — cặp gốc+đảo net=0 loại khỏi stream FIFO/báo cáo → số dư tự khớp [G10] (test scenario C PASS)
- [x] **P1.4 Tests (Core.Tests) [DONE 2026-10-07 — +19, Core.Tests 2130 PASS]:** kịch bản A (bán chịu 5tr + thu 2tr → cuối kỳ 3tr; aging 30-60: 3tr) · B (mua chịu/trả nợ → 331 cuối kỳ 2tr) · C (reversal phiếu nợ + reversal phiếu thu) · D (FIFO tuổi nợ: 2 khoản 01/07 + 15/08, trả 20/09 → >90: 3tr · 30-60: 3tr) · D2 (lịch sử thanh toán khoản: 1 lần trả 2tr → còn 3tr; khoản đã đảo → null) · isolation tenant · [G6] (Adjustment+CashBankBook; không lọt revenue total) · validations (amount ≤ 0, đối tượng trống, kỳ đóng) · P1 decisions #2/#3 (2 phiếu cùng tiền đều tạo được; tie-break CreatedAt)

### Phase 2 — UI (task `task_thu_chi_cong_no_phase2_ui.md`) ✅ DONE 2026-10-07

- [x] **P2.1 Phiếu thu** (`RevenueEntry.razor`): dropdown "Loại phiếu" {Thu doanh thu · Ghi nhận phải thu · Thu tiền khách trả nợ} → loại công nợ: hiện ô **Đối tượng (bắt buộc)** + ô **MST + nút "Tra cứu"** (reuse `BusinessInfoApiClient` — điền tên vào Đối tượng [G11]) + whitelist account 131 + diễn giải gợi ý kèm MST → gọi `ICongNoService.CreateReceivableAsync` (isPayment = loại "Thu tiền khách trả nợ") — bỏ duplicate-check client cho phiếu công nợ (P1 decision #2)
- [x] **P2.2 Phiếu chi** (`ExpenseEntry.razor`): tương tự — {Chi phí · Ghi nhận phải trả · Trả tiền người bán} → 331 → `CreatePayableAsync`; ẩn vendor/category khi loại công nợ
- [x] **P2.3 Trang `/accounting/cong-no`** (`CongNoReport.razor` — UI Platform 100%): bộ lọc tháng (năm/tháng + nút Xem) + bảng 2 khối 131/331 (mockup SRS FR-7: Đầu kỳ · PS tăng · Đã thu/trả · Cuối kỳ · Tuổi nợ 4 nhóm + dòng TỔNG) + chips lọc "Tất cả/Còn nợ/Hết nợ" + **In** (vananPrintBill) / **Xuất Excel** (CSV `vanAn.downloadFile` — pattern TransactionHistory) + click đối tượng → sổ chi tiết (`/accounting/cong-no/{loai}/{doiTuong}?year=&month=`)
- [x] **P2.4 Trang `/accounting/cong-no/{loai}/{doiTuong}`** (`CongNoLedger.razor`): sổ chi tiết cộng dồn (FR-8: Ngày · Diễn giải · Tăng · Giảm · Số dư + đầu kỳ/cuối kỳ; badge "đảo" cho reversal) + nút **Lịch sử** trên khoản nợ → modal "Lịch sử thanh toán khoản nợ" (FR-8.1 — FIFO động, số dư còn lại sau mỗi lần) + nút **Đảo bút toán** mỗi dòng [G10] (modal lý do → `IReversalService`)
- [x] **P2.5 NavMenu "Kế toán → Công Nợ"** (Owner — `ShopErpMenuService`) + Sitemap (`link-accounting-cong-no`) + AccountingIndex quick action + **bUnit +19** (phiếu thu/chi 3 loại render + đối tượng bắt buộc + tra MST điền tên [G11] + báo cáo render/chips/link + sổ render + modal lịch sử + đảo bút toán) → **ShopERP.Tests 161 PASS**
>
> **Ghi chú kiến trúc P2 (bài học):** UI.Platform `DynamicFormFields` KHÔNG có `_Imports.razor` → Razor compiler của UI.Platform xuất `@bind` như attribute LITERAL (đã chứng minh qua generated g.cs: `AddMarkupContent("<input @bind=...>")` — chỉ khi thêm `_Imports.razor` vào UI.Platform thì `@bind` mới biên dịch thành `CreateBinder`). Các trang hiện tại bù bằng JS interop đọc DOM lúc submit ("DOM là source of truth"). → P2 đặt **Loại phiếu/Đối tượng/MST là native controls trong trang host** (ShopERP biên dịch @bind chuẩn — bUnit test được), DynamicFormFields chỉ giữ date/amount/account/description/reference. KHÔNG sửa UI.Platform (ngoài scope — tránh regress mọi form).

### Phase 3 — E2E + hardening (task `task_thu_chi_cong_no_phase3_e2e.md`) ✅ DONE 2026-10-07

- [x] **P3.1 E2E spec `cong-no.spec.ts`** (Gate 4 — `6_Testing/e2e-tests/`): 6 tests self-gating (precedent va-iie-forecast + accounting-entry-flow — `isTierEnabled('e2e')` + dev login):
  - Render: phiếu thu công nợ (3 loại phiếu + Đối tượng bắt buộc + TK 131) · báo cáo 2 khối + chips · sổ chi tiết · Sitemap `link-accounting-cong-no`
  - **Full flow:** phiếu thu "Ghi nhận phải thu" (đối tượng + số tiền duy nhất timestamp) → báo cáo công nợ (dòng + số dư cuối kỳ vi-VN) → click → sổ chi tiết → nút Lịch sử → modal "Lịch sử thanh toán khoản nợ" (chưa thanh toán) → **cleanup bằng đảo bút toán** (G10 — immutable, không xóa; assert dòng "Reversal of:" xuất hiện + badge "đảo")
  - Tra MST [G11] tolerant: điền tên công ty HOẶC lỗi thân thiện (404/429/502 — R6, network phụ thuộc)
  - **KHÔNG chạy** trong session này (Playwright DISABLED khi IMPLEMENT — chạy ở P4 RV production hoặc user window)
- [x] **P3.2 Full test matrix:** guard-check ALL PASSED (windsurf + architecture + Roslyn + Release build 1 warning + fast test gate) · `dotnet build VanAn.sln` 0 errors · **Core.Tests 2130 PASS** · **ShopERP.Tests 161 PASS** · Architecture.Tests PASS → commit (KHÔNG push)

### Phase 4 — Deploy + RV (task `task_thu_chi_cong_no_phase4_rv.md`) ⏳

> **Sweep-first (lesson governance #27 — KHÔNG fix case-by-case):** viết RV sweep script `cong-no-rv-sweep.mjs` (phiếu công nợ + tra MST + báo cáo + tuổi nợ FIFO + sổ + lịch sử thanh toán + reversal + isolation) chạy 1 lần thu toàn bộ FAIL → phân tích theo cụm (cascade/cùng class/kỳ vọng sai) → fix nhóm → 1 deploy → re-sweep toàn bộ. E2E spec chạy sau khi sweep sạch.

- [ ] **P4.1 PUSH** (FAST PUSH `env -u GH_TOKEN -u GITHUB_TOKEN git push --no-verify` — pattern #11) + verify sha (`git ls-remote`/gh api — keyring valid)
- [ ] **P4.2 CD Multi-VPS** (KHÔNG migration — không entity mới) — poll `gh run list --json status,conclusion`
- [ ] **P4.3 RV L1/L2:** markers (ShopERP.dll `CongNoService`/`CongNo` + Gateway dll nếu đổi) · `/health` 200 · route probe
- [ ] **P4.4 RV L3/L4:** E2E production `cong-no.spec.ts` + flow thật (phiếu thu công nợ trên tenant demo → báo cáo → tuổi nợ → lịch sử thanh toán) · L5 manual (user)
- [ ] **P4.5 ĐÓNG:** project_state + master plan + task card → thông báo user

## 4. USER DECISIONS — ĐÃ CHỐT (2026-10-06, SRS v1.1 §10)

| # | Câu hỏi | Quyết định |
|---|---|---|
| Q1 | Bán chịu ghi đồng thời doanh thu? | KHÔNG — phiếu công nợ chỉ 131; ghi doanh thu khi thu tiền |
| Q2 | Tự động công nợ từ đơn hàng? | KHÔNG — nhập tay qua phiếu |
| Q3 | Đối tượng nhập thế nào? | Tên tự nhập + tùy chọn tra MST → tự điền tên công ty (reuse có sẵn) |
| Q4 | Tuổi nợ? | CÓ — <30/30-60/60-90/>90 ngày, FIFO động |
| Q5 | Truy ngược lịch sử thanh toán nợ? | CÓ — từng khoản: các lần thu/trả + còn lại |

## 5. CONSTRAINTS (governance)

- **AccountingEntry 100% immutable** — KHÔNG đụng Domain.cs (feature này chỉ Services + UI + đọc) — mọi sửa sai qua reversal
- KHÔNG thêm entity/bảng/migration — tận dụng AccountCode 131/331 + Vendor + ReferenceType
- Multi-tenancy mọi query (TenantId filter — lesson a21f97f2)
- KHÔNG sửa logic IncomeStatement/BalanceSheet/TrialBalance/CashFlow (chỉ verify không regress + service đọc mới)
- UI Platform 100% (Gate 5) · E2E spec cho UI mới (Gate 4) · Playwright DISABLED trong IMPLEMENT
- `guard-check.ps1` + `dotnet build VanAn.sln` + Core.Tests + ShopERP.Tests MUST PASS trước commit
- Tra MST: reuse `BusinessInfoApiClient` — KHÔNG gọi API ngoài mới; xử lý 404/429/502 thân thiện (pattern có sẵn)
- Secrets: KHÔNG commit token/secret

## 6. SUCCESS CRITERIA (SRS §9 DoD)

- [ ] Phiếu thu/chi 3 loại + đối tượng bắt buộc + tra MST điền tên
- [ ] AccountingEntry 131/331 + Vendor + ReferenceType đúng dấu — KHÔNG entity/migration mới
- [ ] IncomeStatement/BalanceSheet không regress (test hồi quy PASS)
- [ ] Báo cáo công nợ: đầu kỳ/phát sinh/cuối kỳ + tuổi nợ FIFO đúng (kịch bản A/B/C/D)
- [ ] Sổ chi tiết cộng dồn + truy ngược lịch sử thanh toán + đảo bút toán
- [ ] Menu "Công nợ" + Sitemap + bUnit + E2E spec
- [ ] guard-check + build + tests PASS mọi batch · Deploy + RV production PASS → đóng

## 7. DEFERRED (ghi nợ — SRS §3.2 [G13])

- Tự động công nợ từ Order/POS/checkout · Danh bạ khách/cung cấp · Hạn thanh toán/quá hạn/lãi · Hợp đồng/hóa đơn/đối chiếu · Ngoại tệ/công nợ nội bộ/tạm ứng 141/138 · Tự động đối trừ với đơn hàng
