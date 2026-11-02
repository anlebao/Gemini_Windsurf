# TASK CARD PH — VOUCHER FRAMEWORK G1 (registry + phiếu mẫu Tạm Ứng)

> Status: 📋 PLAN — Approved (2026-11-02) · SRS `docs/specs/SRS_ShopERP_VoucherFramework_v1.0.md` · Master plan `docs/AI/plans/voucher-framework-master-plan.md`
> Phạm vi G1: framework nền tảng + phiếu Tạm Ứng qua framework (KHÔNG migrate phiếu cũ)

---

## 1. MỤC TIÊU
1. `VoucherTypeRegistry` + `VoucherTypeDefinition` + `VoucherField` — khai báo phiếu (DI `AddVoucherType`)
2. Page generic `VoucherEntry.razor` `/vouchers/{code}` — schema form + ApplyParse generic + preview JE + guard + Lưu + in
3. `IVoucherPostingStrategy` + pipeline submit chung
4. Smart Entry: intent→registry (hết hardcode route); intents `advance-out`/`advance-settle`
5. Phiếu mẫu: **Tạm Ứng / Hoàn Ứng** (141) — reuse GetOpenKhoanNoAsync

## 2. FILES
- Tạo: `3_CoreHub/Services/Vouchers/VoucherTypeRegistry.cs` (+Definition/Field/`IVoucherPostingStrategy`) · `5_WebApps/ShopERP/Components/Pages/Accounting/VoucherEntry.razor` · `3_CoreHub/Services/Vouchers/Strategies/AdvanceStrategy.cs` · tests `VoucherFrameworkTests`
- Sửa: `Program.cs` (DI registry + strategies) · `VoucherTextParseService`/`LlmEnhance` (intents mới + alias) · SmartEntryBar/ApplyParse (route qua registry) · Sitemap/NavMenu
- KHÔNG: Domain, migration, entity mới (form thuần)

## 3. CHI TIẾT
- `VoucherField.Type`: Amount|Date|Party|Account|Select|Text|Item|Quantity (mở rộng được)
- ApplyParse generic: map `ParsedVoucher.SourceParse` → field (amount/date/doiTuong/paymentMethod/category...)
- Guard pipeline: dup-check 5' · period · balance-warn · preview JE · voucher number (VoucherNumberService) · in (PrintTemplate)
- Advance: tạm ứng Nợ 141/Có {cash} (đối tượng bắt buộc) · hoàn ứng Nợ {cash}/Có 141 + đối ứng tạm ứng mở (AllocatedToEntryId)

## 4. ACCEPTANCE (G1)
- [ ] Thêm phiếu Tạm Ứng chỉ qua registry+strategy (NFR1 — không sửa page khác)
- [ ] `/vouchers/advance` render schema form · Smart Entry "tạm ứng cho anh Nam 5 triệu" → route đúng
- [ ] Lưu tạm ứng → JE Nợ 141/Có 111 + voucher số · hoàn ứng → Nợ 111/Có 141 + đối ứng
- [ ] 5 phiếu cũ regression 0 đổi (full-matrix + llm-rv + multi-rv PASS)
- [ ] guard + build + Core.Tests PASS · CD + RV production (L1 markers, L2 route, L3 E2E)

## 5. QUYẾT ĐỊNH (chốt)
| # | Q | Đề xuất |
|---|---|---|
| D1 | G1-G3 tách đợt? | YES — G1 trước, G2 (xuất kho+thanh lý, cần Gate 5 cho FixedAsset) sau |
| D2 | Migrate phiếu cũ? | G3 tùy chọn, sau khi framework ổn định |
| D3 | Tạm ứng khấu trừ lương? | KHÔNG — chỉ theo dõi 141 + hoàn ứng |