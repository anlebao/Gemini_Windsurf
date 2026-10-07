# MASTER PLAN: KẾ TOÁN — NHẬP LIỆU & SỔ SÁCH ĐẦY ĐỦ (Import Excel · Số dư đầu kỳ · Ngày nghiệp vụ · Phiếu tay → Sổ HKD/BCTC)

> Created: 2026-10-07 (user directive: "các việc phải làm để 1 tenant bắt đầu dùng phần mềm kế toán, dữ liệu cũ nhập vào như thế nào — trước đây dùng Excel" → chốt: xử lý #1 #2 #3 #4)
> Status: **⏳ DRAFT — CHỜ USER REVIEW (chưa implement)**
> Branch: `main` · Priority: tiếp nối Thu chi & Công nợ (P1-P4 DONE, L5 manual chờ user)
> Nền tảng: module kế toán hiện tại + Công nợ MVP đã deploy (`bda6df1e`)

---

## 1. MỤC TIÊU

Đáp ứng nhu cầu thực tế: **tenant mới bắt đầu dùng phần mềm kế toán phải nhập được dữ liệu cũ (đang quản lý bằng Excel)** và **số liệu phải vào đầy đủ sổ sách/báo cáo**. Gồm 4 mục (user chốt):

| # | Mục | Nội dung | Trạng thái hiện tại (đã verify) |
|---|---|---|---|
| **#3** | **Ngày nghiệp vụ trong Lịch sử giao dịch + Số dư tài khoản** | Chuyển 2 màn đọc theo `TransactionDate` (ngày nghiệp vụ) thay vì `CreatedAt` (ngày nhập) | ❌ Lọc theo CreatedAt — phiếu nhập ngày cũ hiện sai kỳ ở 2 màn này |
| **#2** | **Số dư đầu kỳ (Opening Balance)** | Khai báo số dư từng TK (111/112/131/331/156/211/311/333/334/421...) trước mốc bắt đầu → vào báo cáo công nợ (Đầu kỳ) + BalanceSheet/B01 | ❌ Không có cơ chế; phiếu thu/chi không cho TK 111/112 |
| **#1** | **Import dữ liệu từ Excel** | Mẫu Excel (xlsx + CSV) + import hàng loạt (Ngày · Diễn giải · TK · Số tiền · Đối tượng · Loại phiếu) → tạo phiếu qua service hiện có | ❌ Không có — phải nhập tay từng phiếu |
| **#4** | **Phiếu nhập tay → Sổ HKD B01-B09 + Báo cáo tài chính (DN)** | Khi lập phiếu thu/chi (và số dư đầu kỳ) → tạo `JournalEntry` (pattern OrderService) → vào sổ HKD + báo cáo DN | ❌ B01-B09 + IncomeStatement/BalanceSheet đọc `JournalEntry` riêng — phiếu tay KHÔNG vào |

**Nguyên tắc:** kế thừa toàn bộ quyết định Công nợ (AccountingEntry immutable · EntryType Adjustment + CashBankBook cho công nợ [G6] · transactionDate user-nhập · period-closing guard · reversal) · KHÔNG phá báo cáo hiện có · multi-tenancy mọi query.

---

## 2. HIỆN TRẠNG (khảo sát 2026-10-07 — đã verify code)

| Thành phần | Hiện trạng | Kết luận |
|---|---|---|
| `IAccountingEntryRepository.GetByTenantAndDateRangeAsync` / `GetByTenantAndPeriodAsync` | Lọc + sort theo **CreatedAt** (repository, dòng 43-152) | #3: cần method mới lọc TransactionDate |
| `TransactionHistory.razor` (L207) | `GetEntriesByDateRangeAsync` → CreatedAt | #3: chuyển sang TransactionDate |
| `AccountBalance.razor` (L140-152) | `GetEntriesByTenantAndPeriodAsync` → CreatedAt | #3: chuyển sang TransactionDate |
| `CheckDuplicateEntryAsync` (AccountingEntryService L246) | `GetByTenantAndDateRangeAsync` 5' — **semantics đúng là CreatedAt** (chống double-click) | ⚠️ GIỮ NGUYÊN — không đổi |
| `GetTodayRevenueAsync`/`GetRevenueByDateRangeAsync` | Lọc EntryType.Revenue + CreatedAt | #3: cân nhắc chuyển TransactionDate (doanh thu theo ngày nghiệp vụ) |
| `JournalEntry` + `JournalService` | Có sẵn (Create/AddLine/ValidateAndSave — Nợ=Có) | #4: tái dùng |
| `OrderService.CreateRevenueEntryAsync` (L541) | Pattern: `Nợ 111/112 / Có 511(+3331)` + `_hkdBookRepository.AddToBookAsync(je, S2b_HKD)` | #4: copy pattern |
| `HKDBookGenerationService.GetJournalEntriesAsync` (L192) | Template B01-B09 đọc **mọi** JournalEntry theo EntryDate (không lọc book type) | #4: chỉ cần AddToBookAsync(S2b) là đủ cho cả bộ sổ |
| Phiếu thu/chi UI | Whitelist TK: thu 5xx/7xx (công nợ 131), chi 6xx (công nợ 331) — không có 111/112/156/211... | #2: cần màn khai báo đầu kỳ riêng |
| `EPPlus` | Đã có trong repo (VaIIeExportService) | #1: dùng cho xlsx |

---

## 3. QUYẾT ĐỊNH ĐỀ XUẤT (Q1-Q5 — **CHỜ USER REVIEW**)

| # | Câu hỏi | Đề xuất |
|---|---|---|
| **Q1** | Phiếu **công nợ (131/331)** có tạo JournalEntry để vào sổ cái HKD/báo cáo DN không? | **KHÔNG** — giữ [G6] (bán chịu không ghi 511; không có TK đối ứng chuẩn). Công nợ chỉ ở báo cáo công nợ (như hiện tại) |
| **Q2** | Số dư đầu kỳ: bắt buộc **Nợ = Có** hay tự bù chênh lệch? | **Bắt buộc Nợ = Có** khi lưu; cho phép nhập dòng **421 (Lợi nhuận chưa phân phối)** để bù — kế toán chuẩn; UI hiển thị tổng Nợ/Có + cảnh báo chênh lệch |
| **Q3** | Import Excel format? | **xlsx (EPPlus) + CSV** — cùng 1 bộ cột; tải mẫu từ UI |
| **Q4** | Import lỗi giữa chừng xử lý sao? | **Pre-validate toàn bộ (dry-run)** → hiện bảng lỗi theo dòng trước khi lưu; chỉ lưu khi 0 lỗi (append-only — tránh nửa chừng) |
| **Q5** | Phạm vi JournalEntry (#4): phiếu nào? | **Phiếu thu doanh thu (5xx/7xx) + phiếu chi phí (6xx) + số dư đầu kỳ** → JournalEntry. Công nợ ngoài (Q1). Reversal phiếu tay → tạo JournalEntry reversal tương ứng (đảo Nợ/Có) |

---

## 4. SCOPE (phases — session-per-phase, precedent Công nợ)

| Phase | Nội dung | Layer | Validation |
|---|---|---|---|
| **P1** (#3) | Repo: thêm `GetByTenantAndTransactionDateRangeAsync` (TransactionDate) + `GetByTenantAndTransactionDatePeriodAsync`; chuyển TransactionHistory + AccountBalance + (GetTodayRevenue/GetRevenueByDateRange nếu an toàn); **GIỮ duplicate-check CreatedAt**; tests regress | 3_CoreHub + ShopERP | guard + Core.Tests + ShopERP.Tests |
| **P2** (#4) | **JournalEntry cho phiếu tay**: hook vào `AccountingEntryService.CreateRevenue/ExpenseEntryAsync` + `CongNoService` (chỉ thu/chi 5xx/6xx) — tạo JournalEntry `Nợ 111 / Có 5xx` · `Nợ 6xx / Có 111` (pattern OrderService L541) + `AddToBookAsync(S2b_HKD)`; reversal phiếu tay → JournalEntry reversal; fail-safe (lỗi JournalEntry KHÔNG chặn phiếu — log + marker); tests [G6] (công nợ không tạo JE; B02 không gồm 131/331) | 3_CoreHub | guard + Core.Tests |
| **P3** (#2) | **Số dư đầu kỳ**: `IOpeningBalanceService` — khai báo theo kỳ bắt đầu (list TK + dư Nợ/Có + cân bằng Nợ=Có [+421]); tạo AccountingEntry (Adjustment) + JournalEntry "Số dư đầu kỳ" (Nợ TK/Có TK); UI `/accounting/opening-balance` (bảng khai báo + import Excel nếu sẵn) + menu; báo cáo công nợ Đầu kỳ tự đúng (TransactionDate < kỳ); tests | 3_CoreHub + ShopERP | guard + Core.Tests + ShopERP.Tests |
| **P4** (#1) | **Import Excel**: `IImportService` (đọc xlsx/CSV → validate từng dòng → dry-run báo lỗi → lưu qua IAccountingService/ICongNoService — giữ period guard + tenant filter + immutable); mẫu file + UI `/accounting/import` (upload + bảng kết quả/lỗi) + menu; tests (file mẫu, lỗi dòng, dry-run) | 3_CoreHub + ShopERP | guard + Core.Tests + ShopERP.Tests |
| **P5** | E2E spec `accounting-import.spec.ts` (khai báo đầu kỳ → import Excel → báo cáo công nợ/sổ HKD/BCTC thấy số liệu) + full test matrix | 6_Testing | guard + build + tests |
| **P6** | PUSH → CD Multi-VPS → RV L1-L5 (markers `OpeningBalanceService`/`ImportService` · flow: khai đầu kỳ + import thật + đối chiếu báo cáo) | production | RV protocol |

**Dependency:** P1 → P2 → P3 → P4 → P5 → P6 (P3 dùng JournalEntry của P2; P4 dùng services của P2/P3).

---

## 5. THIẾT KẾ CHI TIẾT

### 5.1 #3 — Ngày nghiệp vụ (P1)

- Repo `IAccountingEntryRepository` thêm 2 method (không sửa method cũ — tránh vỡ duplicate-check/OrderService):
  - `GetByTenantAndTransactionDateRangeAsync(tenantId, start, end)` — filter TransactionDate, sort TransactionDate+CreatedAt
  - `GetByTenantAndTransactionDatePeriodAsync(tenantId, period)` — filter TransactionDate ∈ kỳ
- Chuyển: `TransactionHistory` (date range) · `AccountBalance` (period) · `GetTodayRevenueAsync`/`GetRevenueByDateRangeAsync` (dashboard doanh thu theo ngày nghiệp vụ).
- **KHÔNG đổi** `CheckDuplicateEntryAsync` (CreatedAt = đúng semantics chống double-click 5').
- Tests: phiếu ngày cũ → Lịch sử giao dịch hiện ở kỳ nghiệp vụ; regress duplicate-check.

### 5.2 #4 — JournalEntry cho phiếu tay (P2)

| Phiếu | JournalEntry (pattern OrderService L541) |
|---|---|
| Thu doanh thu (5xx/7xx) | Nợ 111 / Có {accountCode} |
| Chi phí (6xx) | Nợ {accountCode} / Có 111 |
| Công nợ 131/331 | **KHÔNG tạo** (Q1 — G6) |
| Reversal phiếu tay | Đảo Nợ/Có của JE gốc (ReferenceType "Reversal", ReferenceId = JE gốc) |

- ReferenceType = `"ManualEntry"` (hoặc "Revenue"/"Expense"), ReferenceId = AccountingEntry.Id (truy vết).
- Persist: `IHKDBookRepository.AddToBookAsync(je, S2b_HKD)` (pattern OrderService L267; template B01-B09 đọc theo EntryDate — tự vào mọi sổ).
- EntryDate = TransactionDate (đúng kỳ nghiệp vụ — lesson W0-T6).
- **Fail-safe:** nếu tạo JE lỗi → log + vẫn lưu phiếu (kế toán đơn không chặn) + đánh dấu cần xử lý (ghi chú).
- Tests: phiếu thu/chi → JE tồn tại + cân bằng Nợ=Có + vào template B02/B01; công nợ KHÔNG tạo JE ([G6] regress); reversal đồng bộ; IncomeStatement/BalanceSheet gồm phiếu tay (regress PASS hiện có không đổi logic).

### 5.3 #2 — Số dư đầu kỳ (P3)

- `IOpeningBalanceService`:
  - `SaveOpeningBalancesAsync(tenantId, year, month, lines[])` — lines: (AccountCode, Debit, Credit); validate: TK hợp lệ (danh mục TK hiện có), không âm, **ΣDebit == ΣCredit** (Q2 — sai → lỗi; gợi ý nhập 421 để bù); period guard (kỳ trước mốc bắt đầu phải Open).
  - Tạo 1 AccountingEntry tổng (EntryType.Adjustment, description "Số dư đầu kỳ {MM/yyyy}", TransactionDate = ngày đầu kỳ − 1 ngày) **+ 1 JournalEntry "Số dư đầu kỳ"** (Nợ các TK dư Nợ / Có các TK dư Có — từng line) → BalanceSheet/B01 + công nợ Đầu kỳ (131/331 có số dư → báo cáo công nợ Đầu kỳ đúng, tuổi nợ tính từ ngày khai — ghi chú).
  - Idempotent: 1 kỳ khai 1 lần (chặn khai lại — chỉ cho sửa qua đảo + khai lại).
- UI `/accounting/opening-balance` (Owner): chọn kỳ bắt đầu → bảng TK (mã + tên + dư Nợ/Có) + tổng + nút Lưu (hoặc Import Excel — nếu P4 xong); NavMenu "Kế toán → Số dư đầu kỳ" + Sitemap + bUnit.
- Tests: khai đầu kỳ → BalanceSheet/B01 số dư đúng; công nợ Đầu kỳ đúng; chênh Nợ≠Có bị chặn; khai lại kỳ đã có bị chặn; isolation tenant.

### 5.4 #1 — Import Excel (P4)

- Mẫu (xlsx + csv): `Ngày | Loại phiếu | Tài khoản | Số tiền | Đối tượng | MST | Diễn giải | Số chứng từ`
  - Loại phiếu: `thu-doanh-thu · chi-phí · ghi-nhan-phai-thu · thu-tien-khach-tra-no · ghi-nhan-phai-tra · tra-tien-nguoi-ban` (khớp UI phiếu)
- `IImportService.ImportAsync(tenantId, stream, format)`:
  1. Parse + validate từng dòng (ngày hợp lệ, TK hợp lệ theo loại phiếu, tiền > 0, đối tượng bắt buộc cho công nợ, kỳ chưa đóng) → **dry-run: trả về bảng lỗi theo dòng (0 lỗi mới lưu — Q4)**.
  2. Lưu qua `IAccountingService`/`ICongNoService` (giữ nguyên immutable + guard + dấu +/−) — tự động sinh JournalEntry (P2).
- UI `/accounting/import` (Owner): tải mẫu + upload + bảng kết quả (số dòng OK/lỗi + chi tiết lỗi) + menu/Sitemap + bUnit.
- Tests: file mẫu import thành công; dòng lỗi (thiếu đối tượng/TK sai/kỳ đóng) bị liệt kê; dry-run không ghi gì; CSV + xlsx tương đương.

---

## 6. EXECUTION STRATEGY (session-per-phase — precedent Công nợ/Booking)

| Session | Phase | Deliverable | Validation |
|---|---|---|---|
| S1 | P1 (#3) | Repo TransactionDate + 2 màn + dashboard | guard + build + Core.Tests + ShopERP.Tests |
| S2 | P2 (#4) | JournalEntry phiếu tay + reversal sync + fail-safe | guard + build + Core.Tests |
| S3 | P3 (#2) | OpeningBalance service + UI + menu | guard + Core.Tests + ShopERP.Tests |
| S4 | P4 (#1) | ImportService + mẫu + UI | guard + Core.Tests + ShopERP.Tests |
| S5 | P5 | E2E spec + full matrix | guard + build + tests |
| S6 | P6 | PUSH + CD + RV L1-L5 | RV protocol |

**Test delta ước lượng:** Core.Tests ~2130 → ~2160-2190 · ShopERP.Tests ~163 → ~170-175 · E2E +4-6.

---

## 7. RỦI RO & MITIGATION

| # | Rủi ro | Mitigation |
|---|---|---|
| R1 | **#4 làm B02/báo cáo DN đổi số liệu** (phiếu tay giờ vào) — "regress" nhìn thấy | Đúng mục tiêu (không phải regress logic); chạy Core.Tests hồi quy toàn bộ + test chốt B02 gồm phiếu tay, công nợ KHÔNG vào ([G6]) |
| R2 | **Duplicate-check vỡ** khi đổi repo | KHÔNG sửa method cũ — thêm method mới; test duplicate 5' giữ PASS |
| R3 | **Import lưu nửa chừng** (append-only) | Dry-run bắt buộc (Q4) — 0 lỗi mới lưu |
| R4 | **Số dư đầu kỳ sai Nợ/Có** | Validate ΣNợ=ΣCó + bù 421 (Q2) + UI hiển thị tổng |
| R5 | **JournalEntry fail chặn phiếu** | Fail-safe: phiếu vẫn lưu, log + ghi chú (R2 Công nợ precedent) |
| R6 | **Tuổi nợ của số dư đầu kỳ 131 không đúng ngày gốc** | Ghi chú UI: khai sớm (ngày trước mốc) để tuổi nợ gần đúng; không có cách biết ngày gốc thật |
| R7 | **Regress Công nợ / Booking** | Chỉ thêm method/service — không sửa logic hiện có; full matrix + RV |

---

## 8. OUT OF SCOPE / DEFERRED

- Tự động định khoản phức tạp (thuế GTGT đầu ra/đầu vào theo hóa đơn — phiếu tay chỉ Nợ/Có 1-1 với 111)
- Phân hệ thuế (TNDN, GTGT khai thuế) — giữ nguyên
- Công nợ 131/331 vào sổ cái HKD/BCTC (Q1 — G6)
- Danh bạ đối tượng chuẩn hóa (vẫn chuỗi tên + MST)
- Import tự động phát hiện bảng Excel phức tạp (nhiều sheet/ký hiệu riêng) — mẫu chuẩn hoá

---

## 9. SUCCESS CRITERIA

- [ ] Lịch sử giao dịch + Số dư tài khoản hiển thị theo **ngày nghiệp vụ** (phiếu ngày cũ đúng kỳ) — duplicate-check giữ nguyên
- [ ] Phiếu thu/chi nhập tay → vào **Sổ HKD B01-B09 + Báo cáo tài chính (DN)** — công nợ KHÔNG vào ([G6] test)
- [ ] Khai báo **số dư đầu kỳ** (mọi TK, Nợ=Có [+421]) → BalanceSheet/B01 + công nợ Đầu kỳ đúng
- [ ] **Import Excel** (xlsx+CSV): mẫu chuẩn · dry-run báo lỗi theo dòng · 0 lỗi mới lưu · tạo đúng phiếu + JournalEntry
- [ ] Reversal phiếu tay đồng bộ cả JournalEntry
- [ ] guard + build + Core.Tests + ShopERP.Tests PASS mọi batch · E2E spec · Deploy + RV PASS → đóng
