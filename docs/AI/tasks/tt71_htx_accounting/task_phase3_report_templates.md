# TASK CARD: Phase 3 — Template BCTC HTX (B01-HTX + B02-HTX + B09-HTX)

> **Master plan:** `docs/AI/plans/tt71-htx-accounting-master-plan.md`
> **Status:** ✅ COMPLETE 2026-10-02 (`9ef8286c` — pushed + CD Multi-VPS SUCCESS + RV markers PASS) · Q2 = CÓ (B09-HTX làm luôn)

## 1. OBJECTIVE

Tạo `Tt71Templates` (mirror `Tt99Templates.cs`) + nhánh service cho tenant HTX: B01-HTX, B02-HTX, B09-HTX.

## 2. CHANGES

| File | Change |
|---|---|
| `3_CoreHub/Services/Data/Tt71Templates.cs` (MỚI) | `BalanceSheetTt71` (B01-HTX) + `IncomeStatementTt71` (B02-HTX) + `NotesTt71` (B09-HTX — Q2=ngay) — theo cấu trúc `Tt99ReportTemplate`/`Tt99TemplateLine` |
| `3_CoreHub/Services/IncomeStatementService.cs` | Nhánh `standard == AccountingStandard.TT71_2024` → `GenerateWithTemplateAsync` với `Tt71Templates.IncomeStatementTt71` |
| `3_CoreHub/Services/BalanceSheetService.cs` | Nhánh TT71 → `Tt71Templates.BalanceSheetTt71` |
| `3_CoreHub/Services/CashFlowStatementService.cs` (nếu có) | TT 71 KHÔNG yêu cầu B03 — service không cần nhánh; UI ẩn tab (Phase 5) |
| `5_WebApps/ShopERP/Components/Pages/Accounting/CashFlowStatement.razor` | Ẩn khi tenant HTX (Phase 5 scope hoặc đây) |

## 3. CẤU TRÚC MẪU (PL IV — verified từ spec)

### B02-HTX (IncomeStatement) — Mã số đặc thù
```
01  Doanh thu hoạt động SXKD        01a bên ngoài (511) / 01b nội bộ (512)
02  Các khoản giảm trừ doanh thu    02a (521 ngoài) / 02b (521 nội bộ)
10  Doanh thu thuần SXKD (10=01-02) 10a/10b
11  Giá vốn hàng bán                11a (611) / 11b (612)
12  Chi phí quản lý kinh doanh      12a/12b (642 phân bổ)
20  KQ hoạt động SXKD (20=10-11-12) 20a/20b
31  Thu nhập khác (558)              32 Chi phí khác (658)
40  Lợi nhuận khác (40=31-32)
50  LN kế toán trước thuế (50=20+40)
51  Chi phí thuế TNDN (659)          60 LN sau thuế (60=50-51)
```

### B01-HTX (BalanceSheet)
- Tài sản: 110 Tiền · 120 ĐTTC · 130 Phải thu · 140 Hàng tồn kho · 150 TSCĐ (+ tài sản chung không chia) · 160 TSCĐ chung không chia?? · ... → **trích chi tiết từ `.devin/tt71_extracted.txt` khi implement** (mục "1. Báo cáo tình hình tài chính (Mẫu số B01 - HTX)")
- Nguồn vốn: 310 Phải trả người bán · 320 Người mua trả tiền trước · 330 Thuế · 340... · VCSH 400 (gồm quỹ chung không chia 442) · Tổng 500

### B09-HTX (Thuyết minh — Q2 = CÓ)
- I. Đặc điểm hoạt động của HTX (lĩnh vực/ngành nghề) · II. Kỳ kế toán, đơn vị tiền tệ · III. Chế độ kế toán áp dụng (nêu số hiệu TT 71) · IV. Thông tin bổ sung cho từng khoản mục B01-HTX (Tiền, ĐTTC, Phải thu, HTK, TSCĐ, TSCĐ chung không chia, Nợ phải trả, VCSH, Quỹ chung không chia...) · V. Thông tin bổ sung B02-HTX (doanh thu nội/ngoại, chi phí QLKD phân bổ...) · VI. Các chỉ tiêu ngoài bảng (001-008)
- Trích chi tiết cấu trúc từ `.devin/tt71_extracted.txt` (mục "3. Bản thuyết minh Báo cáo tài chính (Mẫu số B09 - HTX)") khi implement
- Có thể render dạng HTML/DOCX tĩnh (như IncomeStatement.razor export) — không cần service tính toán phức tạp (đa số là text + số từ B01/B02)

## 4. LƯU Ý

- B02-HTX cần tách nội bộ/ngoài bộ — template line hiện tại (`Tt99TemplateLine`) chỉ có ReportItemCode/label/level/AccountCodes — cần kiểm tra: tách 01a/01b bằng AccountCodes khác nhau (511 vs 512) là ĐỦ (không cần đổi struct) ✅
- 12a/12b (CP QLKD phân bổ) — dữ liệu không có sẵn dấu hiệu nội/ngoại bộ → Phase 3 tính 12 (tổng 642), 12a/12b hiển thị rỗng/0 (note trong tài liệu) hoặc chờ mở rộng dữ liệu
- 11b (612 chi phí nội bộ) — chỉ khi HTX có giao dịch nội bộ

## 5. ACCEPTANCE

- [x] B01-HTX + B02-HTX + B09-HTX render đúng mã số/nhãn TT 71 (service + template; UI display hoàn chỉnh Phase 5)
- [x] HTX không sinh B03 (CashFlowStatement.razor chặn khi `TenantType.HTX` — VanAAlert "TT 71 KHÔNG yêu cầu B03")
- [x] Test template: `Tt71TemplatesTests` (mới — cấu trúc B01/B02 + B09 render qua NotesService) + IncomeStatement T3 + BalanceSheet T4

## 6. KẾT QUẢ (2026-10-02)

- `3_CoreHub/Services/Data/Tt71Templates.cs` (mới): `BalanceSheetTt71` (B01-HTX) + `IncomeStatementTt71` (B02-HTX) — trích trực tiếp từ `spec/tt71-pl-i-ii-iv.txt` (PL IV Mục I.1/I.2); reuse `Tt99ReportTemplate/Tt99TemplateLine` (generic)
- `IncomeStatementService.GenerateTt71Async` — B02-HTX formulas; **sign convention `!IsNormalCredit`** (TT71) → 521 giảm trừ trình bày DƯƠNG, 10 = 01 − 02 đúng; TT99 path giữ nguyên (negate Expense only) qua `ComputeIncomeDirectLinesAsync` dùng chung
- `BalanceSheetService.GenerateTt71Async` — B01-HTX totals 150/160/200/300/400/500 + NetIncome plug Mã 420 + W2 invariant (200 == 500) + classification 110-200/300-380/400-500; 137 "Trong đó" không cộng tổng; `ComputeBalanceSheetDirectLinesAsync` dùng chung
- `FinancialStatementNotesService.GenerateTt71NotesAsync` — B09-HTX I-VI theo PL IV Mục I.3
- `CashFlowStatement.razor` — chặn B03 khi tenant HTX
- Tests: `Tt71TemplatesTests` (mới) + T3 + T4 — **Core.Tests 1872 PASS (+5) · guard ALL PASSED · build 0 errors · CD SUCCESS · RV markers PASS**
- Ghi chú thiết kế (đã note trong template + tests): 12a/12b (CP QLKD phân bổ) = 0 → 20a+20b ≠ 20 khi 12 > 0 (20 giữ tổng 642); Mã 170 bỏ 333 (không tách bên — tránh double-count với Mã 330); Mã 320 (131 credit detail) = 0

## 7. VERIFICATION

```powershell
dotnet build VanAn.sln
dotnet test 6_Tests\VanAn.Core.Tests --filter "FullyQualifiedName~Tt71|FullyQualifiedName~IncomeStatement|FullyQualifiedName~BalanceSheet"
```
