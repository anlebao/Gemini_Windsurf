# MASTER PLAN — VOUCHER FRAMEWORK (registry-driven)

> ✅ Approved (2026-11-02) · SRS: `docs/specs/SRS_ShopERP_VoucherFramework_v1.0.md`
> Mục tiêu: thêm phiếu chứng từ mới = registry + strategy, không phình hệ thống

---

## GIAI ĐOẠN

### G1 — Framework + phiếu mẫu Tạm Ứng (nền tảng)
- `VoucherTypeRegistry` + `VoucherTypeDefinition` + `VoucherField` (DI, `AddVoucherType`)
- Page generic `VoucherEntry.razor` `/vouchers/{code}`: render schema form, ApplyParse generic (SourceParse), preview JE, guard (dup/period/balance-warn), voucher number, in
- `IVoucherPostingStrategy` + pipeline submit chung
- Smart Entry: `intent → registry` (bỏ hardcode route); intents `advance-out`/`advance-settle`
- **Phiếu mẫu: Tạm Ứng** (Nợ 141/Có 111 · hoàn ứng Nợ 111/Có 141 — reuse GetOpenKhoanNoAsync)
- Migrations: KHÔNG (form thuần)
- Tests: registry resolution · strategy tạm ứng · apply generic map · guard chung

### G2 — Phiếu Xuất Kho + Thanh Lý TSCĐ (qua framework)
- `stock-out` (02-VT): SX Nợ 154/Có 152 · bán Nợ 632/Có 152 + StockMovement (bình quân gia quyền)
- `asset-disposal` (01-TSCĐ): bảng `FixedAssets` tối thiểu (mã/tên/nguyên giá/hao mòn 214/ngày) — **Gate 5 (Domain entity mới)** → migration PG
- JE thanh lý: Nợ 214 + Nợ 811/Có 711 + Nợ 111,112 / Có 211
- Đóng ví dụ Smart Entry gốc "thanh lý TSCĐ 100tr" (hiện map Revenue — sai)

### G3 — Migrate dần 5 phiếu cũ (TÙY CHỌN, không bắt buộc)
- Thu/Chi/Chuyển tiền/Nhập mua/CTGS → registry (từng phiếu, sau khi framework ổn định)
- Không đụng trong G1-G2 — đảm bảo zero-regression cho phiếu đang chạy production

### DEFER (ghi nhận, không làm)
- Bảng khấu hao định kỳ (02-TSCĐ) · Tín dụng nội bộ 132/332 (vay thành viên) · FIFO theo lô

---

## RỦI RO & KIỂM SOÁT
| Rủi ro | Giảm thiểu |
|---|---|
| Framework over-engineer | Chỉ UI/route/parse; business = strategy C# nhỏ, không config |
| Vỡ phiếu cũ | G1-G2 chỉ THÊM phiếu mới; phiếu cũ bất biến; migrate G3 tùy chọn |
| Registry thiếu field phiếu tương lai | VoucherFieldType mở rộng được (Item/Quantity/Party...) — additive |
| Multi-voucher lệch intent | intent→registry dictionary + alias VN (đã có ResolveIntent) |

## ACCEPTANCE CHUNG
- [ ] Thêm phiếu mới demo (tạm ứng) chỉ qua registry+strategy, không sửa file khác (NFR1)
- [ ] 5 phiếu cũ regression 0 đổi (E2E full-matrix + llm-rv + multi-rv vẫn PASS)
- [ ] guard + build + Core.Tests PASS mỗi giai đoạn · CD + RV production