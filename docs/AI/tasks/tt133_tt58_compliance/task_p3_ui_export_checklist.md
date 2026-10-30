# TASK CARD P3 — UI REGIME-AWARE + XUẤT ĐÚNG MẪU + CHECKLIST PHÁP LÝ

> Status: ✅ DONE — deployed `102ce865` (W1-W4) + regime-lock `615d85ac` — RV production L1-L3 PASS, 34/34 sweep (2026-10-30)
> Master plan: `docs/AI/plans/tt133-tt58-accounting-compliance-master-plan.md`

---

## 1. MỤC TIÊU

1. UI báo cáo/sổ hiển thị **đúng danh mục biểu mẫu theo chế độ** của tenant (TT133 → B01a/B02/B03/B09/F01-DNN · TT58 → sổ DNSN + B01/B02-DNSN · ẩn form không áp dụng).
2. Xuất **Excel/In đúng khung mẫu** (header "Mẫu số …", cột mã số, footer ký: Người lập biểu / Kế toán trưởng / Người đại diện pháp luật) cho B01a-DNN, B02-DNN, F01-DNN, B01-DNSN, B02-DNSN.
3. Cập nhật checklist pháp lý nội bộ: bỏ TT 132/2018, thêm TT 58/2026 + TT 152/2025 + TT 71/2024 đúng mục.

## 2. PHẠM VI

### 2.1 UI regime routing (`FinancialReports.razor` + hub/sổ)

| Standard | Tab hiển thị |
|---|---|
| TT99_2025 | B01-DN · B02-DN · B03-DN (direct+indirect) · B09-DN — giữ nguyên |
| TT133_2016 | **B01a-DNN** · B02-DNN · B03-DNN · B09-DNN · **F01-DNN** (TrialBalance label) |
| TT71_2024 | B01-HTX · B02-HTX · B09-HTX (ẩn B03) — giữ nguyên |
| TT58_2026 | Sổ DNSN (theo DnsnTaxMethod) · B01-DNSN · B02-DNSN — ẨN B03/B09/F01/CashFlow |
| HKD (Type=HKD) | Sổ HKD S1a-S3a — giữ nguyên (ghi nhận caveat resolver TT133 cho BCTC) |

- Ẩn tab: dùng `AccountingStandardResolver.Resolve(tenant.Type)` + `Tenant.AccountingStandard` override (HKD chọn TT58 — D4).
- `ShopErpMenuService`: menu "Báo Cáo Tài Chính" + "Sổ kế toán" regime-aware (DN siêu nhỏ thấy sổ DNSN).
- Select chuẩn trên trang báo cáo: giới hạn option theo tenant Type (không cho SME chọn TT99 nếu chưa khai báo — vẫn cho phép chọn TT99 khi DN nhỏ chọn áp dụng TT133→TT99? TT58 Điều 4.3: siêu nhỏ được chọn DNNVV — cho phép override `Tenant.AccountingStandard=TT133_2016`).

### 2.2 Xuất Excel/In đúng mẫu (EPPlus — pattern `LedgerExcelExportService`)

- `IRegimeReportExportService` (ShopERP): `ExportBctcAsync(tenant, form, period)` → workbook:
  - Header: Đơn vị/Địa chỉ trái · "Mẫu số B01a-DNN (Ban hành kèm theo Thông tư số 133/2016/TT-BTC…)" phải — text đúng thông tư từng form.
  - Bảng: CHỈ TIÊU · Mã số · Thuyết minh · Số cuối năm · Số đầu năm (B01) / Năm nay · Năm trước (B02).
  - Footer: NGƯỜI LẬP BIỂU (Ký, họ tên) · KẾ TOÁN TRƯỞNG · NGƯỜI ĐẠI DIỆN THEO PHÁP LUẬT (đóng dấu).
  - Forms: B01a-DNN, B02-DNN, B03-DNN, F01-DNN (TT133) · B01-DNSN, B02-DNSN (TT58) · sổ DNSN S1-S4a (khung sổ đơn giản theo spec §2.3 card P2).
- In: nút "In báo cáo" dùng `vananPrintBill` pattern hiện có — khung in giống Excel.
- Số âm chỉ tiêu (*) → ngoặc đơn `(…)` đúng ghi chú mẫu.

### 2.3 Checklist nội bộ — `docs/Accounting_Doc/checklist_kiem_soat_phan_mem_ke_toan_erp_UPDATED_2026.md`

- §0.1: **BỎ "Chế độ kế toán DN siêu nhỏ — TT 132/2018"** → thay "TT 58/2026/TT-BTC (hiệu lực 01/07/2026, thay TT 132/2018)".
- Thêm dòng: TT 152/2025 (HKD — hiệu lực 01/01/2026) · TT 71/2024 (HTX).
- Mục 1.3: cập nhật "DN siêu nhỏ: TT 132/2018" → TT 58/2026; thêm HKD: TT 152/2025.
- Mục 3.1: liệt kê đúng mẫu từng chế độ (DNN: B01a/B02/B09 bắt buộc + B03 khuyến khích + F01 · DNSN: B01/B02 · HKD: sổ S1a-S3a · HTX: B01/B02/B09-HTX).
- Ghi chú P1.5 task card `task_acct_legal_compliance_p1_5_regime_checklist_quyche.md` — merge phần checklist vào đây (phần Quy chế PMKT vẫn riêng).

## 3. FILES

- Sửa: `FinancialReports.razor` · `ShopErpMenuService.cs` · `TrialBalance.razor` (F01 label — phối hợp P1) · trang sổ
- Tạo: `5_WebApps/ShopERP/Services/RegimeReportExportService.cs` (+ interface)
- Sửa: `docs/Accounting_Doc/checklist_kiem_soat_phan_mem_ke_toan_erp_UPDATED_2026.md`

## 4. ACCEPTANCE CRITERIA

- [ ] Tenant TT133: hub chỉ hiển thị B01a/B02/B03/B09/F01 — không có tab TT99
- [ ] Tenant TT58: hub hiển thị sổ DNSN + B01/B02-DNSN — ẩn CashFlow/B09
- [ ] Excel B01a-DNN mở được, đúng header mẫu + mã số + footer ký + số âm ngoặc đơn
- [ ] Checklist: không còn "TT 132/2018"; có TT 58/2026 + TT 152/2025 + TT 71/2024
- [ ] bUnit: render đúng tab theo regime (3-4 tests)

## 5. TEST CASES

- T1: hub tenant TT133 → đúng 5 tab
- T2: hub tenant TT58 → sổ + 2 BCTC, ẩn B03/B09
- T3: export B01a-DNN — sheet name/header/mã số/footer
- T4: override HKD→TT58 (D4) → hiển thị DNSN
