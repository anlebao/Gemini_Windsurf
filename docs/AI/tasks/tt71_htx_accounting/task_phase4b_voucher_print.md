# TASK CARD: Phase 4b — Mẫu in chứng từ PL II (Phiếu thu 01-TT + Phiếu chi 02-TT)

> **Master plan:** `docs/AI/plans/tt71-htx-accounting-master-plan.md`
> **Status:** PENDING — Q5 = CÓ
> **Nguồn mẫu:** `docs/Accounting_Doc/phu-luc-II-Thong-tu-71.docx` (extract `.devin/tt71_pl2_extracted.txt` — mục 01-TT/02-TT)

## 1. OBJECTIVE

Xuất/print **Phiếu thu (Mẫu số 01-TT)** + **Phiếu chi (Mẫu số 02-TT)** theo đúng khuôn mẫu TT 71 từ dữ liệu entry đã lưu (phiếu thu/chi hiện tại).

## 2. CẤU TRÚC MẪU (verified từ docx)

### Mẫu số 01 - TT — PHIẾU THU
```
Quyển số: ...    Số: ...
Nợ: ...          Có: ...
Họ và tên người nộp tiền: ...
Địa chỉ: ...
Lý do nộp: ...
Số tiền: ... (Viết bằng chữ): ...
Kèm theo: ... Chứng từ gốc: ...
Chữ ký: GIÁM ĐỐC (đóng dấu) · KẾ TOÁN TRƯỞNG · NGƯỜI LẬP PHIẾU · THỦ QUỸ · NGƯỜI NỘP TIỀN
Đã nhận đủ số tiền (viết bằng chữ): ...
+ Tỷ giá ngoại tệ: ... + Số tiền quy đổi: ...
(Liên gửi ra ngoài phải đóng dấu)
```

### Mẫu số 02 - TT — PHIẾU CHI
```
Quyển số: ...    Số: ...
Nợ: ...          Có: ...
Họ và tên người nhận tiền: ...
Địa chỉ: ...
Lý do chi: ...
Số tiền: ... (Viết bằng chữ): ...
Kèm theo: ... Chứng từ gốc: ...
Chữ ký: GIÁM ĐỐC (đóng dấu) · KẾ TOÁN TRƯỞNG · THỦ QUỸ · NGƯỜI LẬP PHIẾU · NGƯỜI NHẬN TIỀN
Đã nhận đủ số tiền (viết bằng chữ): ...
+ Tỷ giá ngoại tệ: ... + Số tiền quy đổi: ...
(Liên gửi ra ngoài phải đóng dấu)
```

## 3. CHANGES

| File | Change |
|---|---|
| `5_WebApps/ShopERP/Components/Pages/Accounting/RevenueEntry.razor` | Sau khi lưu thành công → nút/liên kết "In Phiếu thu (01-TT)" → mở modal print (render mẫu 01-TT với dữ liệu entry vừa tạo) |
| `5_WebApps/ShopERP/Components/Pages/Accounting/ExpenseEntry.razor` | Tương tự — "In Phiếu chi (02-TT)" |
| (MỚI) `5_WebApps/ShopERP/Components/Accounting/Tt71ReceiptVoucher.razor` | Component render mẫu 01-TT (receipt) — nhận dữ liệu: số phiếu, ngày, Nợ/Có, người nộp, địa chỉ, lý do, số tiền + chữ, kèm theo, chữ ký |
| (MỚI) `5_WebApps/ShopERP/Components/Accounting/Tt71PaymentVoucher.razor` | Component render mẫu 02-TT (payment) — tương tự |
| (MỚI) `5_WebApps/ShopERP/Services/Accounting/VietnameseCurrencyText.cs` | Helper số → chữ tiền tệ ("Viết bằng chữ") — kiểm tra có sẵn chưa (OrderService/discount text đã từng có?) |
| `wwwroot/...` (in styles) | CSS in (A4 portrait, khổ giấy chứng từ) — dùng class hiện có của app, không custom layout phá UI Platform |

## 4. LƯU Ý

- Áp dụng cho **mọi tenant HTX** (theo chuẩn TT 71); tenant DN/HKD giữ print hiện tại (nếu có) hoặc ẩn
- Số phiếu: dùng `reference` (Số Chứng Từ) đã lưu — nếu trống, sinh `PT-{yyyyMMdd}-{seq}` / `PC-...`
- Dữ liệu Nợ/Có: từ entry đã lưu (AccountCode) — với tenant HTX account select đã giới hạn 511/512/558 (thu) và 642/658 (chi) từ Phase 4a
- Viết bằng chữ: kiểm tra helper có sẵn (`VanAn.CoreHub` hoặc `UI.Platform`) — nếu chưa có, viết helper mới (đơn giản: số → chữ VN, dùng trong component)

## 5. ACCEPTANCE

- [ ] Render Phiếu thu 01-TT đúng khuôn (header, Nợ/Có, người nộp, lý do, số tiền + chữ, 5 chữ ký)
- [ ] Render Phiếu chi 02-TT đúng khuôn
- [ ] In (browser print) ra A4 đẹp — dữ liệu entry thật
- [ ] ShopERP.Tests component render PASS

## 6. VERIFICATION

```powershell
dotnet build VanAn.sln
dotnet test 6_Tests\VanAn.ShopERP.Tests --filter "FullyQualifiedName~Voucher|FullyQualifiedName~RevenueEntry|FullyQualifiedName~ExpenseEntry"
```
