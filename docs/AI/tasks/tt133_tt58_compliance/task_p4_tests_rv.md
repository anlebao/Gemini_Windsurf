# TASK CARD P4 — TEST MATRIX + DEPLOY + RV (TT133 + TT58)

> Status: ✅ DONE — deployed `102ce865` (W1-W4) + regime-lock `615d85ac` — RV production L1-L3 PASS, 34/34 sweep (2026-10-30)
> Master plan: `docs/AI/plans/tt133-tt58-accounting-compliance-master-plan.md`

---

## 1. MỤC TIÊU

Khép regression cho toàn bộ ma trận chế độ kế toán (TT152 HKD · TT133 · TT99 · TT71 · TT58) sau khi thêm 2 regime mới, rồi deploy + RV production theo protocol 5 lớp.

## 2. TEST MATRIX

### 2.1 Core.Tests (xUnit — `6_Tests/VanAn.Core.Tests`)

| Suite | Nội dung | Số test |
|---|---|---|
| `Tt133TemplatesTests` | B01a mã số đầy đủ + công thức 200/300/400/500 · B02 mã 24 gộp/51 gộp · B03 direct+indirect subtotal | ~10 |
| `BalanceSheetServiceTests` (+TT133) | seed journal → B01a cân 200=500 · plug vào 417 · contra (*) âm ngoặc đơn | +4 |
| `IncomeStatementServiceTests` (+TT133) | công thức 30=20+21−22−24 · 60=50−51 | +3 |
| `CashFlowStatementServiceTests` (+TT133) | subtotal mã số TT133 | +2 |
| `Tt58TemplatesTests` | cấu trúc sổ S1/S2a/S2b/S2c/S2d/S3a/S3b/S4a + B01/B02-DNSN mã số | ~8 |
| `DnsnBookServiceTests` | danh mục sổ đúng 4 DnsnTaxMethod · S1 thuế theo ngành · S3b khấu trừ | ~8 |
| `AccountingStandardUiTests`/resolver | SuperSmall→TT58 · SME→TT133 · Large→TT99 · HTX→TT71 | +3 |
| Regression | toàn bộ suite hiện có (TT99/TT71/HKD/order/booking/công nợ) | giữ PASS |

### 2.2 ShopERP.Tests (bUnit)

- `FinancialReports` hub: render tab đúng per regime (TT133/TT58/TT99/TT71).
- `TrialBalance` label F01-DNN khi TT133.
- Trang sổ DNSN render cột đúng.

### 2.3 E2E (Gate 4 — chạy ở RV production, KHÔNG trong IMPLEMENT)

- Spec `6_Testing/e2e-tests/accounting-regime-forms.spec.ts`:
  - Tenant TT133 (Vạn An Trading a5b6c7d8 hoặc tenant test mới Type=SME): B01a-DNN render mã 110/200/300/500 · cân bằng.
  - Tenant SuperSmall (tạo/set Type + DnsnTaxMethod=VatPercent_TndnPercent): danh mục sổ chỉ S1-DNSN; mở sổ render dòng nhóm ngành + dòng thuế; B01-DNSN mã 200=500.
  - Excel export B01a-DNN → file tải về có header "Mẫu số B01a-DNN".

## 3. DEPLOY + RV

1. `guard-check.ps1` + `dotnet build VanAn.sln` + `dotnet test` (Core.Tests + ShopERP.Tests) — ALL PASS.
2. FAST PUSH (`git push --no-verify`) vì CI đã validate trong session.
3. CD Multi-VPS: `gh run list` polling (KHÔNG `gh run watch` — Known Error #11).
4. RV 5 lớp: L1 markers trong DLL (Tt133Templates/Tt58Templates/DnsnBookService) · L2 routes/health · L3 Playwright spec trên production · L4 flow · L5 manual user.

## 4. RISKS TRONG TEST

| Rủi ro | Mitigation |
|---|---|
| Spec TT133 từ nguồn web (không phải bản Bộ TC gốc trên tay) | Ghi nguồn trong comment; nếu user có docx TT133 gốc → đối chiếu lại mã số trước deploy |
| Tenant production chưa có Type=SuperSmall để test | Tạo tenant test hoặc set Type trên tenant test có sẵn (như cách test TT71) |
| TT58 sổ cần IndustrySector trên journal (giống HKD) | Null → bucket OtherBusiness (giống TT152) |

## 5. DoD

- [ ] Core.Tests + ShopERP.Tests ALL PASS (không fail mới)
- [ ] CD SUCCESS · RV L1-L3 PASS · E2E regime spec PASS production
- [ ] Master plan + task cards cập nhật trạng thái · project_state.md §2/§3/§4
