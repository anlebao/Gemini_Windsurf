# TASK CARD P1 — TT 133/2016 BCTC MẪU CHUẨN (B01a-DNN · B02-DNN · B03-DNN · B09-DNN · F01-DNN)

> Status: ✅ DONE — deployed `102ce865` (W1-W4) + regime-lock `615d85ac` — RV production L1-L3 PASS, 34/34 sweep (2026-10-30)
> Master plan: `docs/AI/plans/tt133-tt58-accounting-compliance-master-plan.md`
> Spec đối chiếu (BẮT BUỘC đọc trước khi code): `docs/AI/tasks/tt133_tt58_compliance/spec/tt133-pl2-bctc-forms.txt`
> Gap đóng: **#1** — TT133 BCTC đang render flat account list (SAI mẫu)

---

## 1. MỤC TIÊU

Tenant `Enterprise_SME` (TT133) xuất BCTC đúng mẫu TT 133/2016/TT-BTC Phụ lục 2:

| Mẫu | Bắt buộc | Trạng thái mục tiêu |
|---|---|---|
| B01a-DNN Báo cáo tình hình tài chính | Bắt buộc | Template + service wire |
| B02-DNN Báo cáo KQKD | Bắt buộc | Template + service wire |
| B09-DNN Thuyết minh BCTC | Bắt buộc | Verify + sửa `FinancialStatementNotesService` |
| B03-DNN Lưu chuyển tiền tệ (trực tiếp + gián tiếp) | Khuyến khích | Subtotal mã số trong `CashFlowStatementService` |
| F01-DNN Bảng cân đối tài khoản | Kèm khi nộp thuế | Label/export cho TrialBalance hiện có |
| B01b-DNN | Tùy chọn thay B01a | **DEFER** (ghi roadmap — D1) |
| B01/B09-DNNKLT | Không HĐ liên tục | **OUT OF SCOPE** |

## 2. SPEC MÃ SỐ (đã đối chiếu Phụ lục 2 — verify lại từ spec file khi implement)

### B01a-DNN — TÀI SẢN (map TK → Mã số)

| Mã | Chỉ tiêu | TK nguồn (chart TT133 hiện có) |
|---|---|---|
| 110 | Tiền và các khoản tương đương tiền | 111, 112 |
| 120 | Đầu tư tài chính | = 121+122+123+124 |
| 121 | Chứng khoán kinh doanh | 121 |
| 122 | Đầu tư nắm giữ đến ngày đáo hạn | 128 |
| 123 | Đầu tư góp vốn vào đơn vị khác | 228 |
| 124 | Dự phòng tổn thất ĐTTC (*) | 229 (phần đầu tư — ghi nhận limitation: không tách được → 0 hoặc gộp 136/142) |
| 130 | Các khoản phải thu | = 131+132+133+134+135+136 |
| 131 | Phải thu của khách hàng | 131 |
| 132 | Trả trước cho người bán | 331 (số dư Nợ — limitation: không tách bên → ghi chú) |
| 133 | Vốn kinh doanh ở đơn vị trực thuộc | — (0) |
| 134 | Phải thu khác | 136, 138, 141 |
| 135 | Tài sản thiếu chờ xử lý | — (0) |
| 136 | Dự phòng phải thu khó đòi (*) | 229 (*) |
| 140 | Hàng tồn kho | = 141+142 |
| 141 | Hàng tồn kho | 151, 152, 153, 154, 155, 156, 157 |
| 142 | Dự phòng giảm giá HTK (*) | 229 (phần HTK — limitation như trên) |
| 150 | Tài sản cố định | = 151−152 |
| 151 | Nguyên giá | 211 |
| 152 | Giá trị hao mòn lũy kế (*) | 214 (*) |
| 160 | Bất động sản đầu tư | = 161−162 |
| 161 | Nguyên giá | 217 |
| 162 | Giá trị hao mòn lũy kế (*) | 2147/hoặc 214 (*) |
| 170 | XDCB dở dang | 241 |
| 180 | Tài sản khác | = 181+182 |
| 181 | Thuế GTGT được khấu trừ | 133 |
| 182 | Tài sản khác | 242, 244 |
| **200** | **TỔNG TS = 110+120+130+140+150+160+170+180** | công thức |

### B01a-DNN — NGUỒN VỐN

| Mã | Chỉ tiêu | TK nguồn |
|---|---|---|
| 300 | Nợ phải trả | = 311+…+320 |
| 311 | Phải trả người bán | 331 |
| 312 | Người mua trả tiền trước | 131 (số dư Có — limitation → 0, ghi chú) |
| 313 | Thuế và các khoản phải nộp NN | 333 |
| 314 | Phải trả người lao động | 334 |
| 315 | Phải trả khác | 335, 336, 338 |
| 316 | Vay và nợ thuê tài chính | 341 |
| 317 | Phải trả nội bộ về VKD | 336 |
| 318 | Dự phòng phải trả | 352 |
| 319 | Quỹ khen thưởng, phúc lợi | 353 |
| 320 | Quỹ phát triển KH&CN | 356 |
| 400 | Vốn chủ sở hữu | = 411+…+417 |
| 411 | Vốn góp của chủ sở hữu | 411 |
| 412 | Thặng dư vốn cổ phần | — (chart TT133 chưa có 412 → thêm nếu cần, hoặc 0) |
| 413 | Vốn khác của chủ sở hữu | 418 |
| 414 | Cổ phiếu quỹ (*) | 419 (*) |
| 415 | Chênh lệch tỷ giá HĐ | 413 |
| 416 | Các quỹ thuộc VCSH | 414/418 |
| **417** | **LNST chưa phân phối (NetIncome plug vào đây)** | 421 |
| **500** | **TỔNG NV = 300+400** | công thức |

⚠️ **KHÁC TT99:** tổng TS = mã **200** (không phải 280); tổng NV = **500** (không phải 440); NetIncome plug = **417** (không phải 420). Sign convention: normal-credit → negate (như TT99/TT71); dòng (*) trình bày âm trong ngoặc đơn.

### B02-DNN (map TK)

| Mã | Chỉ tiêu | TK / công thức |
|---|---|---|
| 01 | Doanh thu bán hàng và CC DV | 511 |
| 02 | Các khoản giảm trừ DT | — (TT133 không có 521 → 0) |
| 10 | Doanh thu thuần | = 01−02 |
| 11 | Giá vốn hàng bán | 632 (TT133 có 632) |
| 20 | Lợi nhuận gộp | = 10−11 |
| 21 | Doanh thu hoạt động tài chính | 515 |
| 22 | Chi phí tài chính | 635 |
| 23 | — Trong đó: chi phí lãi vay | (chi tiết 635 — nếu không tách → 0, ghi chú) |
| 24 | Chi phí quản lý kinh doanh | 642 (TT133 gộp 641+642 → chỉ dùng 642) |
| 30 | LN thuần từ HĐKD | = 20+21−22−24 |
| 31 | Thu nhập khác | 711 |
| 32 | Chi phí khác | 811 |
| 40 | Lợi nhuận khác | = 31−32 |
| 50 | Tổng LN kế toán trước thuế | = 30+40 |
| 51 | Chi phí thuế TNDN | 821 (gộp — không tách 821/822) |
| 60 | LN sau thuế TNDN | = 50−51 |

### B03-DNN (mã số — direct + indirect)

- Direct: 01-07 → 20 · 21-25 → 30 · 31-35 → 40 · 50=20+30+40 · 60 đầu kỳ · 61 tỷ giá · 70=50+60+61 (xem spec file — mã giống khung TT99 nhưng ít dòng).
- Indirect: mã 01 LN trước thuế + điều chỉnh 03-17 → 20; investing/financing giống direct.

## 3. THIẾT KẾ

1. **`3_CoreHub/Services/Data/Tt133Templates.cs`** (mới — pattern `Tt71Templates.cs`): `BalanceSheetTt133` ("B01a-DNN") · `IncomeStatementTt133` ("B02-DNN") · `CashFlowDirectTt133` ("B03-DNN-Direct") · `CashFlowIndirectTt133` ("B03-DNN-Indirect-Adjustments"). Reuse `Tt99ReportTemplate`/`Tt99TemplateLine`, `AccountingStandard.TT133_2016`.
2. **`BalanceSheetService`**: thêm nhánh `standard == TT133_2016` → `GenerateTt133Async` — tái dùng `ComputeBalanceSheetDirectLinesAsync` + công thức tổng theo B01a + NetIncome plug **Mã 417** + invariant `200 == 500`.
3. **`IncomeStatementService`**: nhánh TT133 → `GenerateTt133Async` — công thức B02-DNN (30=20+21−22−24 · 40=31−32 · 50=30+40 · 60=50−51).
4. **`CashFlowStatementService`**: nhánh `standard == TT133_2016` — thêm subtotal mã (20/30/40/50/60/61/70) như đã làm cho TT99; direct + indirect.
5. **`FinancialStatementNotesService`**: kiểm chứng nhánh TT133 render đúng mục B09-DNN; sửa nếu lệch; giữ hiển thị tên chuẩn "Thông tư 133/2016/TT-BTC".
6. **F01-DNN**: trang TrialBalance — khi standard=TT133 hiển thị nhãn "BẢNG CÂN ĐỐI TÀI KHOẢN (Mẫu số F01-DNN)" + export tên file đúng; cấu trúc giữ nguyên (đã đủ cột số dư đầu/phát sinh/cuối).
7. UI standard select: giữ `AccountingStandardResolver` (SME→TT133); KHÔNG sửa resolver ở phase này (P2 mới thêm TT58).

## 4. FILES TẠO/SỬA

- Tạo: `3_CoreHub/Services/Data/Tt133Templates.cs`
- Sửa: `BalanceSheetService.cs`, `IncomeStatementService.cs`, `CashFlowStatementService.cs`, `FinancialStatementNotesService.cs` (nếu cần), `TrialBalance.razor` (label F01)
- Tests: `6_Tests/VanAn.Core.Tests/Services/Tt133TemplatesTests.cs` + bổ sung `BalanceSheetServiceTests`/`IncomeStatementServiceTests`/`CashFlowStatementServiceTests` nhánh TT133

## 5. ACCEPTANCE CRITERIA

- [ ] B01a-DNN render đúng mã số 110→200/300→500, cân 200=500 (invariant throw nếu lệch)
- [ ] B02-DNN đúng mã + công thức (đặc biệt mã 24 gộp, mã 51 gộp)
- [ ] B03-DNN có subtotal mã số cả 2 phương pháp
- [ ] B09-DNN thuyết minh hiển thị TT133
- [ ] F01-DNN label trên TrialBalance cho tenant TT133
- [ ] Không regress TT99/TT71/HKD — toàn bộ test cũ PASS
- [ ] Mọi dòng `Tt99TemplateLine` được verify với spec file trước khi commit (ghi chú nguồn trong comment như Tt71/Tt99Templates)

## 6. TEST CASES (tối thiểu)

- T1: B01a cấu trúc mã số đầy đủ (đếm + spot check 110/141/200/311/417/500)
- T2: W2 invariant — seed journal → 200=500
- T3: NetIncome plug vào 417 (không phải 420)
- T4: B02 công thức 30/40/50/60 đúng khi có 515/635/711/811/821
- T5: B03 direct + indirect subtotal
- T6: tenant HKD/TT99/TT71 không regress (resolver + form routing)
