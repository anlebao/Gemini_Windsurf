# Quy chế Mua giúp — Bán dùm (Reseller Model) — VanAn

**Phiên bản:** 1.0 (Draft — Sprint 7)
**Ngày hiệu lực:** [Điền ngày]
**Lưu ý:** Đây là bản draft, cần luật sư review trước khi publish. Áp dụng song song với `marketplace-policy.md` — mỗi tenant chọn 1 mode (Marketplace HOẶC Reseller) tại thời điểm tạo đơn. Past orders giữ snapshot mode tại creation.

**Cơ sở pháp lý:**
- Nghị định 13/2023/NĐ-CP (bảo vệ dữ liệu cá nhân)
- Thông tư 39/2015/TT-BCT + Nghị định 98/2020/NĐ-CP (sàn TMĐT)
- Luật Thương mại 2005 (chế độ đại lý/bán hàng — áp dụng cho "Mua giúp — Bán dùm")
- Luật Doanh nghiệp 2020 (Vạn An là bên mua-bán, không chỉ là trung gian sàn)
- Nghị định 31/2021/NĐ-CP (hướng dẫn Luật Thương mại — đại lý bán hàng)

---

## 1. Giới thiệu mô hình

VanAn vận hành 2 mô hình thương mại song song:

| Khía cạnh | Marketplace (xem `marketplace-policy.md`) | Reseller (bản quy chế này) |
|---|---|---|
| Vai trò Vạn An | Nền tảng giới thiệu + giao hàng | Bên mua từ tenant → bán lại cho customer |
| Ai định giá | Tenant tự định giá | Vạn An định giá bán (cost price từ tenant + margin) |
| Dòng tiền COD | Shipper thu hộ → shop nhận trực tiếp | Customer trả → Vạn An nhận → Vạn An phân phối |
| Advance payment | Shipper ứng cho shop | Vạn An ứng cho tenant (mua trước) |
| Settlement | Shipper ↔ Shop trực tiếp | Tất cả qua Vạn An wallet |
| Platform fee | Không có | Vạn An giữ margin → phân chia 4 khoản |
| Community fund | Không có | % margin vào quỹ cộng đồng |

**Nguyên tắc cốt lõi:** "Mua giúp — Bán dùm". Vạn An mua hàng hóa từ tenant rồi bán lại cho khách hàng cuối. Vạn An là **bên bán chính thức** trong giao dịch với customer (không chỉ là trung gian sàn như Marketplace mode).

---

## 2. Vai trò các bên (Reseller mode)

### 2.1 VanAn (Reseller)
- Mua hàng hóa từ tenant theo `reseller-agreement.md` (giá cost price đã negotiate).
- Định giá bán lại (SellPrice = CostPrice + Margin).
- Chịu trách nhiệm trước customer về chất lượng hàng hóa (vì là bên bán).
- Quản lý toàn bộ dòng tiền: thu COD/external payment → phân phối 4 khoản.
- Quản lý quỹ phát triển cộng đồng (xem `community-fund-policy.md`).

### 2.2 Tenant (Nhà cung cấp — Supplier)
- Cung cấp hàng hóa cho Vạn An theo `reseller-agreement.md`.
- Chịu trách nhiệm chất lượng sản phẩm nguồn (warranty với Vạn An).
- Nhận thanh toán từ Vạn An (Settlement wallet tx) — không giao dịch trực tiếp với customer.
- Negotiate cost price offline với Vạn An (admin nhập qua `POST /api/admin/product-cost-price`).

### 2.3 Customer (Buyer)
- Mua hàng từ Vạn An (không phải từ tenant).
- Thanh toán cho Vạn An (COD hoặc external payment — VietQR/card).
- Khiếu nại sản phẩm: liên hệ Vạn An trước (Vạn An là bên bán).

### 2.4 Shipper (CTV)
- Giao hàng cho Vạn An (thu COD hộ Vạn An, không phải tenant).
- Nhận DeliveryFee từ Vạn An (không phải từ tenant).
- Không tham gia advance payment (Vạn An ứng cho tenant, không phải shipper).

### 2.5 Salesman (CTV)
- Giới thiệu sản phẩm qua QR referral.
- Nhận hoa hồng từ Vạn An, tính trên **PlatformMargin** (không phải orderTotal).
- Hoa hồng hold 48h (anti-fraud — xem `anti-fraud-policy.md`).

---

## 3. Phân chia lợi nhuận (4 khoản)

Khi order hoàn tất (COD collected hoặc external payment confirmed), Vạn An phân chia margin thành 4 khoản:

```
COD collected (customer trả) = CostPrice + DeliveryFee + Commission + PlatformFee + CommunityFund
```

| Khoản | Người nhận | WalletTransactionType | Tính theo |
|---|---|---|---|
| CostPrice | Tenant | Settlement | Fix (negotiated) |
| DeliveryFee | Shipper | DeliveryFee | Fix (DefaultDeliveryFee hoặc per-order) |
| Commission | Salesman | Commission | PlatformMargin × CommissionRate (chỉ khi có referral) |
| PlatformFee | Vạn An (PlatformWallet) | PlatformFee | PlatformMargin × PlatformFeeRate |
| CommunityFund | Quỹ cộng đồng (CommunityFundWallet) | CommunityFund | PlatformMargin × CommunityFundRate |

**Ví dụ:**
- CostPrice = 80.000 VND
- SellPrice = 100.000 VND (Margin = 20.000 VND)
- DeliveryFee = 15.000 VND
- PlatformFeeRate = 30% → PlatformFee = 6.000 VND
- CommunityFundRate = 5% → CommunityFund = 1.000 VND
- CommissionRate = 5% (OnMargin) → Commission = 1.000 VND (nếu có salesman)
- COD collected = 80.000 + 15.000 + 1.000 + 6.000 + 1.000 = 103.000 VND (= SellPrice + DeliveryFee + Commission)

**Bất biến tài chính (financial invariant):** Tổng tất cả wallet tx amounts = COD collected (COD flow) hoặc = external payment amount (non-COD flow). Hệ thống tự verify sau mỗi confirm.

---

## 4. Dòng tiền

### 4.1 COD Flow (Reseller)
1. Shipper delivers → customer trả tiền mặt (COD = SellPrice + DeliveryFee).
2. Shipper tap "Đã thu COD" → `POST /api/community/wallet/confirm-cod`.
3. Vạn An wallet system tạo 6 transactions (5-split + CODCollection):
   - CODCollection (+codAmount, shipper) — shipper thu hộ
   - Settlement (+costPrice, tenant) — Vạn An trả tenant giá vốn
   - DeliveryFee (+deliveryFee, shipper) — Vạn An trả shipper
   - Commission (+commission, salesman) — Vạn An trả salesman (if referral)
   - PlatformFee (+platformFee, PlatformWallet) — Vạn An giữ
   - CommunityFund (+communityFund, CommunityFundWallet) — quỹ cộng đồng
4. Order.CodCollectedAt = now.

### 4.2 Advance Payment Flow (Reseller)
1. Order confirmed → Vạn An cần mua hàng từ tenant.
2. Vạn An ứng tiền cho tenant:
   - AdvancePayment (-advanceAmount, PlatformWallet) — Vạn An ứng
   - Settlement (+advanceAmount, tenant) — tenant nhận
3. Shipper picks up from tenant (không tham gia tài chính).
4. Shipper delivers → COD flow (§4.1).
5. Settlement final:
   - Nếu costPrice > advance: Vạn An trả tenant thêm (costPrice - advance) trong COD flow.
   - Nếu advance > costPrice: Vạn An thu lại (advance - costPrice).

### 4.3 Non-COD Flow (Q5 — External Payment)
1. Customer thanh toán qua VietQR/card trước khi giao hàng.
2. `POST /api/community/confirm-external-payment` (SystemAdmin hoặc payment gateway webhook).
3. Vạn An wallet system tạo 5-split (không có CODCollection):
   - ExternalPayment (+paymentAmount, PlatformWallet) — Vạn An nhận
   - Settlement (+costPrice, tenant)
   - DeliveryFee (+deliveryFee, shipper) — trả sau khi giao thành công
   - Commission (+commission, salesman)
   - PlatformFee (+platformFee, PlatformWallet)
   - CommunityFund (+communityFund, CommunityFundWallet)
4. Shipper giao hàng (không thu tiền — đã thanh toán trước).

**Lưu ý:** Non-COD flow, DeliveryFee vẫn trả shipper sau khi giao thành công (không phải ngay lúc confirm payment) — chống shipper nhận phí nhưng không giao.

---

## 5. Định giá

### 5.1 CostPrice (Vạn An mua từ tenant)
- Negotiate offline giữa Vạn An và tenant.
- Vạn An admin nhập qua `POST /api/admin/product-cost-price` (per tenant, per product).
- Unique index trên (TenantId, ProductId).
- Snapshot vào Order tại creation time — không thay đổi khi cost price update sau.

### 5.2 SellPrice (Vạn An bán cho customer)
- SellPrice = CostPrice + Margin.
- Margin = CostPrice × DefaultPlatformFeeRate (global, hoặc per-product override).
- Hiển thị trên KhachLink (mode-aware price display).
- Snapshot vào Order tại creation time.

### 5.3 Margin phân chia
- PlatformFee = Margin × PlatformFeeRate (default 30%).
- CommunityFund = Margin × CommunityFundRate (default 5%).
- Commission = Margin × CommissionRate (salesman, if referral — OnMargin base).
- Còn lại (Margin - PlatformFee - CommunityFund - Commission) = Vạn An net profit (giữ trong PlatformWallet).

**Lưu ý:** PlatformFeeRate + CommunityFundRate + CommissionRate ≤ 100% margin. Hệ thống validate khi admin set rate.

---

## 6. Toggle mechanism

### 6.1 Global toggle
- SystemAdmin set `GlobalCommerceMode` qua `POST /api/admin/commerce-mode/global`.
- Runtime toggle (không restart) — qua SystemSetting entity.
- Default = Marketplace (zero regression cho existing tenants).

### 6.2 Tenant override
- SystemAdmin set per-tenant `TenantSettings.CommerceModeOverride`:
  - `Inherit` (-1): dùng global (default)
  - `Marketplace` (0): ép Marketplace cho tenant này
  - `Reseller` (1): ép Reseller cho tenant này
- Qua `POST /api/admin/commerce-mode/tenant/{tenantId}`.

### 6.3 Order snapshot
- Mỗi Order snapshot `CommerceMode` tại creation time.
- Toggle affect **future orders only** — past orders giữ mode cũ.
- Lý do: kế toán/đối soát ổn định, không thay đổi financial logic của orders đã chốt.

---

## 7. Trách nhiệm pháp lý

### 7.1 Vạn An là bên bán (Reseller mode)
- Hóa đơn: Vạn An xuất hóa đơn cho customer (VAT theo VatRate snapshot trong Order).
- Warranty: Vạn An chịu trách nhiệm warranty với customer (sub-claim lại tenant theo `reseller-agreement.md`).
- Tranh chấp: customer khiếu nại Vạn An (không phải tenant).

### 7.2 Tenant là nhà cung cấp
- Hóa đơn: Tenant xuất hóa đơn cho Vạn An (cost price × quantity).
- Warranty: Tenant chịu trách nhiệm warranty với Vạn An (theo agreement).
- Không giao dịch trực tiếp với customer trong Reseller mode.

### 7.3 Kế toán
- Vạn An ghi nhận: doanh thu (SellPrice), giá vốn (CostPrice), chi phí bán (DeliveryFee + Commission), quỹ (CommunityFund).
- Tenant ghi nhận: doanh thu bán hàng cho Vạn An (CostPrice).
- AccountingEntry immutable (Reversal Entry pattern) — xem governance Domain rules.

---

## 8. Quỹ phát triển cộng đồng (Community Fund)

- Nguồn: CommunityFund = Margin × CommunityFundRate (mỗi order Reseller).
- Quản trị: xem `community-fund-policy.md` (guardrail cho SysAdmin rút tiền).
- Audit trail: CommunityFundSpendRecord (amount, reason, recipient, approvedBy, spentAt).
- Minh bạch: customer + CTV có thể xem balance + history qua API.

---

## 9. Khiếu nại và giải quyết

### 9.1 Customer khiếu nại sản phẩm
- Liên hệ Vạn An support (Vạn An là bên bán).
- Vạn An sub-claim lại tenant theo `reseller-agreement.md`.
- Refund: Vạn An refund customer trước, rồi claim tenant.

### 9.2 Tenant khiếu nại Vạn An
- CostPrice dispute: renegotiate offline, update qua admin API.
- Settlement delay: liên hệ Vạn An finance.
- Advance payment dispute: xem `reseller-agreement.md` clause về advance.

### 9.3 CTV khiếu nại
- Shipper: DeliveryFee không nhận → liên hệ Vạn An (không phải tenant).
- Salesman: Commission tính sai → kiểm tra CommissionBase (OnMargin cho Reseller).
- Xem `community-terms-of-service.md` §6.

---

## 10. Cấm

- Tenant raise cost price sau khi order tạo (snapshot bảo vệ).
- Vạn An thay đổi PlatformFeeRate/CommunityFundRate retroactively cho past orders.
- Shipper thu COD nhưng không confirm (xem `anti-fraud-policy.md` §2.4 + addendum).
- Salesman manipulate margin để tăng commission (xem `anti-fraud-policy-reseller-addendum.md`).
- SysAdmin rút community fund ngoài quy trình `community-fund-policy.md`.
- Tenant bán trực tiếp cho customer trong Reseller mode (bypass Vạn An).

---

## 11. Thay đổi quy chế

- Vạn An thông báo trước 7 ngày (giống Marketplace).
- Toggle mode không coi là "thay đổi quy chế" (đã có snapshot protection).
- Thay đổi PlatformFeeRate/CommunityFundRate = thay đổi quy chế → thông báo 7 ngày.

---

## 12. Liên hệ

- Support: support@vanan.cloud
- Finance (settlement/advance): finance@vanan.cloud
- Community fund: community@vanan.cloud
- Legal: legal@vanan.cloud

---

## 13. Tài liệu liên quan

- `marketplace-policy.md` — Quy chế Marketplace mode (alternative mode)
- `reseller-agreement.md` — Hợp đồng B2B template Vạn An ↔ Tenant
- `community-fund-policy.md` — Quản trị quỹ cộng đồng
- `community-terms-of-service.md` — Điều khoản CTV (áp dụng cả 2 mode)
- `community-privacy-policy.md` — Chính sách bảo mật (áp dụng cả 2 mode)
- `anti-fraud-policy.md` — Chính sách chống gian lận (Marketplace baseline)
- `anti-fraud-policy-reseller-addendum.md` — Fraud vectors Reseller (addendum)
