# Hợp đồng Mua giúp — Bán dùm (Reseller Agreement) — VanAn ↔ Tenant

**Phiên bản:** 1.0 (Draft Template — Sprint 7)
**Ngày hiệu lực:** [Điền ngày]
**Lưu ý:** Đây là template draft, cần luật sư review + tùy chỉnh per-tenant trước khi ký. Mỗi tenant ký 1 bản riêng với cost price + fee rate cụ thể.

**Cơ sở pháp lý:**
- Luật Thương mại 2005 — Chương II Hợp đồng mua bán hàng hóa
- Luật Doanh nghiệp 2020
- Nghị định 31/2021/NĐ-CP (hướng dẫn Luật Thương mại — đại lý/bán hàng)
- Thông tư 39/2015/TT-BCT + Nghị định 98/2020/NĐ-CP (sàn TMĐT — Vạn An vận hành song song 2 mô hình)
- Nghị định 13/2023/NĐ-CP (bảo vệ dữ liệu cá nhân — cho customer data Vạn An thu thập)

---

## HỢP ĐỒNG MUA BÁN HÀNG HÓA SỐ: [___]/[NĂM]

- **Bên A (Mua — Reseller):** CÔNG TY [Vạn An] — MST: [___] — Địa chỉ: [___] — Đại diện: [___]
- **Bên B (Bán — Supplier/Tenant):** [TENANT LEGAL NAME] — MST: [___] — Địa chỉ: [___] — Đại diện: [___]

Hai bên đồng ý ký kết hợp đồng mua bán hàng hóa theo các điều khoản sau:

---

### Điều 1. Đối tượng hợp đồng

1.1. Bên B đồng ý bán cho Bên A các mặt hàng liệt kê tại **Phụ lục 1 — Danh mục sản phẩm & CostPrice** (đính kèm).

1.2. Bên A đồng ý mua lại các mặt hàng trên để bán lại cho khách hàng cuối qua nền tảng VanAn (Reseller mode).

1.3. Mô hình: "Mua giúp — Bán dùm". Bên A là bên bán chính thức với khách hàng cuối. Bên B là nhà cung cấp cho Bên A.

---

### Điều 2. CostPrice (Giá Vạn An mua)

2.1. CostPrice cho từng sản phẩm liệt kê tại Phụ lục 1, đã negotiate giữa 2 bên.

2.2. CostPrice được nhập vào hệ thống VanAn bởi admin Bên A qua `POST /api/admin/product-cost-price` (per TenantId + ProductId).

2.3. **Snapshot tại order creation:** CostPrice snapshot vào Order tại thời điểm customer đặt hàng. CostPrice update sau khi order tạo KHÔNG ảnh hưởng order đã tạo.

2.4. **Điều chỉnh CostPrice:**
- Bên B muốn tăng CostPrice → thông báo Bên A trước 7 ngày, negotiate lại.
- Bên A đồng ý → update qua admin API. Apply cho future orders only.
- Bên A không đồng ý → 2 bên renegotiate hoặc chấm dứt hợp đồng theo Điều 9.

2.5. CostPrice bao gồm [thuế VAT / chưa VAT] — ghi rõ trong Phụ lục 1.

---

### Điều 3. SellPrice (Giá Vạn An bán lại)

3.1. Bên A tự quyết định SellPrice (giá bán cho customer). SellPrice = CostPrice + Margin.

3.2. **Bên B KHÔNG có quyền can thiệp SellPrice** — Bên A là bên bán, chịu trách nhiệm thị trường.

3.3. SellPrice hiển thị trên KhachLink (customer-facing app). Snapshot vào Order tại creation.

---

### Điều 4. Margin phân chia (4 khoản)

4.1. Margin = SellPrice − CostPrice. Phân chia thành:

| Khoản | Tỷ lệ | Người nhận |
|---|---|---|
| PlatformFee | [PlatformFeeRate] × Margin (default 30%) | Bên A (PlatformWallet) |
| CommunityFund | [CommunityFundRate] × Margin (default 5%) | Quỹ cộng đồng |
| Commission | [CommissionRate] × Margin (if salesman referral) | Salesman CTV |
| Còn lại | Margin − PlatformFee − CommunityFund − Commission | Bên A net profit |

4.2. PlatformFeeRate + CommunityFundRate + CommissionRate ≤ 100% Margin (hệ thống validate).

4.3. **Điều chỉnh rate:** Bên A thông báo Bên B trước 7 ngày trước khi thay đổi PlatformFeeRate/CommunityFundRate. Apply cho future orders only.

4.4. DeliveryFee (phí giao hàng) tính riêng — không thuộc Margin. Default = [15.000 VND] per order, có thể per-order override.

---

### Điều 5. Thanh toán (Settlement)

5.1. **COD flow:** Customer trả tiền mặt cho shipper → Bên A thu qua shipper → Bên A thanh toán Bên B CostPrice qua wallet Settlement tx.

5.2. **Non-COD flow (Q5):** Customer thanh toán qua VietQR/card cho Bên A → Bên A thanh toán Bên B CostPrice qua wallet Settlement tx.

5.3. **Thời gian settlement:**
- COD: Settlement tx tạo ngay khi shipper confirm COD collected (real-time).
- Non-COD: Settlement tx tạo ngay khi external payment confirmed.
- Payout thực tế (rút tiền từ wallet về bank account): theo Điều 6.

5.4. **Bằng chứng thanh toán:** WalletTransaction immutable (Reversal Entry pattern). Bên B có thể xem history qua ShopERP wallet UI.

5.5. **Hóa đơn:**
- Bên B xuất hóa đơn cho Bên A: CostPrice × quantity (VAT theo Phụ lục 1).
- Bên A xuất hóa đơn cho customer: SellPrice × quantity (VAT theo VatRate snapshot trong Order).

---

### Điều 6. Advance Payment (Vạn An ứng tiền)

6.1. Khi order confirmed, Bên A có thể ứng trước (advance) cho Bên B để Bên B chuẩn bị hàng.

6.2. **Advance amount:** thỏa thuận per-order (default = 0 — không advance, Bên A trả sau khi COD collected).

6.3. **Cơ chế:**
- Bên A wallet: AdvancePayment tx (−advanceAmount, PlatformWallet).
- Bên B wallet: Settlement tx (+advanceAmount, tenant).
- Shipper picks up from Bên B (không tham gia tài chính).

6.4. **Settlement final (sau COD):**
- Nếu CostPrice > advance: Bên A trả Bên B thêm (CostPrice − advance) trong COD flow.
- Nếu advance > CostPrice: Bên A thu lại (advance − CostPrice) — ghi rõ trong wallet tx.

6.5. **Rủi ro advance:** Bên A chịu rủi ro nếu Bên B không giao hàng sau khi nhận advance. Bên A có quyền:
- Hold advance cho Bên B nếu Bên B vi phạm SLA (Điều 7).
- Yêu cầu Bên B hoàn advance + bồi thường thiệt hại.

---

### Điều 7. SLA & Giao hàng

7.1. **SLA chuẩn bị hàng:** Bên B chuẩn bị hàng trong [___] giờ kể từ khi order confirmed (hoặc advance payment confirmed nếu có advance).

7.2. **SLA giao hàng:** Shipper (do Bên A điều phối) đến lấy hàng tại địa điểm Bên B trong [___] giờ sau khi Bên B confirm "sẵn sàng giao".

7.3. **Quality SLA:** Hàng hóa phải đạt chất lượng cam kết trong Phụ lục 1. Nếu customer khiếu nại chất lượng:
- Bên A refund customer trước.
- Bên A sub-claim Bên B theo warranty clause (Phụ lục 1).
- Bên B hoàn CostPrice cho Bên A (hoặc đổi hàng) trong [___] ngày.

7.4. **Out-of-stock:** Nếu Bên B không có hàng → thông báo Bên A trong [___] giờ. Bên A cancel order + refund customer. Bên B không bị phạt nếu thông báo đúng SLA.

7.5. **Penalty SLA violation:**
- Chuẩn bị hàng trễ: [___]% CostPrice per order (tối đa [___] VND).
- Không giao hàng sau advance: hoàn advance + bồi thường [___] VND.

---

### Điều 8. Trách nhiệm pháp lý

8.1. **Bên A (Vạn An — Reseller):**
- Là bên bán chính thức với customer → chịu trách nhiệm trước customer về chất lượng, giao hàng, refund.
- Xuất hóa đơn cho customer.
- Quản lý dòng tiền + phân chia 4 khoản.
- Quản lý quỹ cộng đồng theo `community-fund-policy.md`.

8.2. **Bên B (Tenant — Supplier):**
- Cung cấp hàng hợp pháp, không vi phạm pháp luật (hàng giả, hàng cấm, vi phạm IP).
- Xuất hóa đơn cho Bên A.
- Chịu trách nhiệm warranty với Bên A (sub-claim).
- Không giao dịch trực tiếp với customer trong Reseller mode (bypass Bên A = vi phạm hợp đồng).

8.3. **Bảo mật dữ liệu:** Bên A thu thập customer data theo `community-privacy-policy.md`. Bên B không tiếp xúc customer data (chỉ thấy order + product, không thấy customer PII).

---

### Điều 9. Chấm dứt hợp đồng

9.1. **Bên đơn phương chấm dứt:**
- Báo trước 30 ngày.
- Hoàn tất settlement tất cả pending orders (snapshot mode — orders đã tạo tiếp tục Reseller logic đến khi hoàn tất).

9.2. **Chấm dứt vì vi phạm:**
- Bên B vi phạm SLA 3 lần trong 30 ngày → Bên A có quyền chấm dứt.
- Bên B bypass Bên A bán trực tiếp customer → Bên A chấm dứt + phạt [___] VND.
- Bên A không thanh toán CostPrice quá [___] ngày → Bên B có quyền chấm dứt.

9.3. **Chấm dứt vì pháp lý:** Cơ quan nhà nước yêu cầu, thay đổi pháp luật khiến mô hình không khả thi.

9.4. **Hậu quả chấm dứt:**
- Past orders (snapshot Reseller) tiếp tục hoàn tất theo Reseller logic.
- Future orders: tenant tự động chuyển về Marketplace mode (nếu tenant vẫn active trên sàn) hoặc offboard.
- Wallet balance: payout đầy đủ trong [___] ngày.

---

### Điều 10. Toggle mode

10.1. Bên A có quyền toggle tenant sang Marketplace mode (tenant bán trực tiếp) qua `POST /api/admin/commerce-mode/tenant/{tenantId}`.

10.2. Toggle affect **future orders only** — past orders giữ Reseller snapshot.

10.3. Toggle không coi là chấm dứt hợp đồng — chỉ chuyển mô hình thương mại.

10.4. Nếu Bên B muốn quay lại Reseller → báo Bên A, renegotiate Phụ lục 1 (CostPrice có thể đã thay đổi).

---

### Điều 11. Bảo mật & Dữ liệu

11.1. Bên B không tiếp xúc customer PII (chỉ thấy order + product info).

11.2. Bên A xử lý customer data theo `community-privacy-policy.md` + Nghị định 13/2023/NĐ-CP.

11.3. Wallet tx data: 2 bên đều thấy (Bên B thấy Settlement + Advance tx của mình, không thấy tx của bên khác).

---

### Điều 12. Giải quyết tranh chấp

12.1. 2 bên ưu tiên thương lượng.

12.2. Không thương lượng được → giải quyết tại [Trọng tài thương mại VN / Tòa án có thẩm quyền].

12.3. Tranh chấp wallet tx: dựa vào audit trail (WalletTransaction immutable + Reversal Entry).

---

### Điều 13. Thời hạn hợp đồng

13.1. Hợp đồng có thời hạn [___] năm, tự động gia hạn [___] năm nếu không bên nào báo chấm dứt.

13.2. Bắt đầu: [Điền ngày]. Kết thúc: [Điền ngày].

---

### Điều 14. Điều khoản chung

14.1. Hợp đồng này + các Phụ lục là một thể thống nhất.

14.2. Sửa đổi hợp đồng: bằng văn bản, 2 bên ký xác nhận.

14.3. Bổ sung Phụ lục mới (sản phẩm mới): 2 bên ký Phụ lục addendum.

14.4. Hợp đồng lập thành [___] bản, mỗi bên giữ [___] bản có giá trị pháp lý như nhau.

---

## PHỤ LỤC 1 — Danh mục sản phẩm & CostPrice

| STT | Tên sản phẩm | ProductId (GUID) | CostPrice (VND) | VAT | Warranty | Ghi chú |
|---|---|---|---|---|---|---|
| 1 | [Tên] | [GUID] | [___] | [__%] | [___] | |
| 2 | ... | | | | | |

**Lưu ý kỹ thuật:** ProductId phải khớp với Product entity trong ShopERP SQLite (per-tenant). CostPrice lưu trong Gateway PG (ProductCostPrice entity, unique index TenantId + ProductId).

---

## PHỤ LỤC 2 — Fee Rates (per-tenant)

| Rate | Giá trị | Ghi chú |
|---|---|---|
| PlatformFeeRate | [30%] | % Margin Bên A giữ |
| CommunityFundRate | [5%] | % Margin vào quỹ cộng đồng |
| DefaultDeliveryFee | [15.000 VND] | Phí giao hàng per order |
| CommissionRate (salesman) | [5%] | % Margin cho salesman (OnMargin base) |

**Lưu ý:** Rate có thể global (SystemSetting) hoặc per-tenant override. Per-tenant override ghi rõ trong Phụ lục này.

---

## PHỤ LỤC 3 — SLA chi tiết

| SLA | Thời gian | Penalty |
|---|---|---|
| Chuẩn bị hàng | [___] giờ | [___] |
| Shipper đến lấy | [___] giờ sau ready | (Bên A chịu) |
| Warranty response | [___] ngày | [___] |
| Out-of-stock notify | [___] giờ | Không phạt nếu đúng SLA |
| Settlement payout | [___] ngày | [___] |

---

**ĐẠI DIỆN BÊN A**                              **ĐẠI DIỆN BÊN B**

(Ký, ghi rõ họ tên, đóng dấu)                  (Ký, ghi rõ họ tên, đóng dấu)

________________________                       ________________________

**Ngày ký:** [___]/[___]/[___]

---

## Tài liệu liên quan

- `reseller-policy.md` — Quy chế Reseller (public-facing)
- `community-fund-policy.md` — Quản trị quỹ cộng đồng
- `community-terms-of-service.md` — Điều khoản CTV (áp dụng cả 2 mode)
- `community-privacy-policy.md` — Chính sách bảo mật (áp dụng cả 2 mode)
- `anti-fraud-policy.md` + `anti-fraud-policy-reseller-addendum.md` — Anti-fraud
- `marketplace-policy.md` — Quy chế Marketplace (alternative mode)
