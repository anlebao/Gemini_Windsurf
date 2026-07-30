# Addendum — Fraud Vectors Reseller Model (Anti-Fraud Policy Addendum) — VanAn

**Phiên bản:** 1.0 (Draft — Sprint 7 Addendum)
**Ngày hiệu lực:** [Điền ngày]
**Lưu ý:** Đây là addendum cho `anti-fraud-policy.md` (Marketplace baseline). Áp dụng BỔ SUNG khi order.CommerceMode = Reseller. Không thay thế baseline — baseline vẫn áp dụng cho phần CTV fraud chung (SalesReferral, AppInstall, Device, Shipper baseline).

**Cơ sở pháp lý:**
- Luật Thương mại 2005 (hợp đồng mua bán — fraud trong thương mại)
- Bộ luật Hình sự 2015 (sửa đổi 2017) — tội lừa đảo chiếm đoạt tài sản (Điều 174)
- Nghị định 13/2023/NĐ-CP (bảo vệ dữ liệu — liên quan cost price data)

---

## 1. Mục đích addendum

Reseller mode thay đổi dòng tiền (Vạn An là trung gian, không phải tenant), tạo ra fraud vectors MỚI không có trong Marketplace:

1. **Cost price manipulation** — tenant inflate cost price.
2. **Margin manipulation** — salesman/tenant manipulate để tăng commission.
3. **Advance payment fraud** — tenant nhận advance nhưng không giao hàng.
4. **COD skimming (Reseller variant)** — shipper thu COD nhưng không nộp lại Vạn An.
5. **Community fund misappropriation** — SysAdmin lạm dụng quyền rút quỹ.
6. **External payment fraud (Q5)** — fake payment confirmation, chargeback fraud.
7. **Settlement fraud** — tenant nhận Settlement nhưng không giao hàng.

Addendum này bổ sung các fraud vectors mới + detection + response. Baseline `anti-fraud-policy.md` (3-strike, hold 48h, KYC, wallet reversal) vẫn áp dụng đầy đủ.

---

## 2. Fraud vectors mới (Reseller-specific)

### 2.1 Cost Price Manipulation (Tenant → Vạn An)

**Hành vi:**
- Tenant inflate CostPrice trong negotiate offline → Vạn An mua giá cao hơn thị trường.
- Tenant + Vạn An admin collude set CostPrice cao → Vạn An mất margin (admin nhận kickback).
- Tenant thay đổi CostPrice retroactively (không thể — snapshot bảo vệ, nhưng có thể thử update trước order tạo).

**Detection:**
- **Cost price benchmark:** Finance team periodically benchmark CostPrice vs market price (manual — Sprint 7 chưa auto).
- **Admin audit:** Audit ProductCostPrice update log — admin update CostPrice tăng > [20%] trong 30 ngày → flag review.
- **Margin anomaly:** Order có PlatformMargin < [5%] CostPrice → flag (margin quá mỏng, nghi cost price inflate).

**Response:**
- Confirm fraud → renegotiate CostPrice + reverse wallet tx (nếu đã settle).
- Admin collude → ban admin + legal action (Điều 174 BL Hình sự nếu đủ yếu tố).
- Tenant inflate → terminate `reseller-agreement.md` + claim bồi thường.

### 2.2 Margin Manipulation (Salesman / Tenant collude)

**Hành vi:**
- Salesman + tenant collude: tenant set CostPrice thấp → Margin cao → Commission (OnMargin) cao. Sau đó tenant + salesman chia lại kickback.
- Salesman tự tạo order qua QR của mình với product có CostPrice artificially thấp.

**Detection:**
- **Commission anomaly:** Commission > [50%] Margin → flag (commission rate quá cao so với margin).
- **Cost price outlier:** CostPrice cho product X < [70%] median CostPrice cùng category → flag.
- **Self-referral Reseller:** salesman + customer same device/IP (baseline §2.1 vẫn áp dụng) + Reseller mode → high risk.

**Response:**
- Confirm fraud → reverse Commission tx + strike salesman (baseline 3-strike).
- Tenant collude → terminate agreement + claim.
- Wallet reversal (baseline §8).

### 2.3 Advance Payment Fraud (Tenant → Vạn An)

**Hành vi:**
- Tenant nhận AdvancePayment nhưng không chuẩn bị hàng.
- Tenant nhận advance + cancel order → giữ advance.
- Tenant nhận advance + giao hàng kém chất lượng (để customer reject → giữ advance).

**Detection:**
- **Advance-to-delivery ratio:** Tenant có advance > [50%] orders nhưng delivery success < [80%] → flag.
- **Advance cancel rate:** Tenant cancel order sau advance > [10%] → flag.
- **Advance amount anomaly:** Advance amount > CostPrice (advance không nên vượt cost) → block (system validate).

**Response:**
- Confirm fraud → reverse AdvancePayment tx + terminate agreement.
- Tenant không hoàn advance → legal action (Điều 174 BL Hình sự — lừa đảo chiếm đoạt tài sản).
- Hold future advance cho tenant (manual flag — tenant phải settlement COD trước, không advance).

### 2.4 COD Skimming (Reseller Variant — Shipper → Vạn An)

**Hành vi:**
- Shipper thu COD customer nhưng tap "Delivered failed" → giữ tiền.
- Shipper thu COD nhưng không confirm (hold tiền) → Vạn An không phân phối được.
- Shipper thu COD + cancel order → giữ tiền.

**Detection:**
- **COD-to-confirm delay:** Shipper thu COD (customer report) nhưng không confirm trong [24h] → flag.
- **Failed delivery anomaly:** Shipper có "Delivered failed" rate > [15%] nhưng customer feedback "đã nhận hàng" → flag.
- **COD amount mismatch:** Shipper confirm COD amount < SellPrice + DeliveryFee (snapshot) → block + flag.

**Response:**
- Confirm fraud → reverse CODCollection tx + strike shipper (baseline 3-strike).
- Shipper không nộp COD → legal action + ban.
- Hold 48h (baseline §6) áp dụng cho DeliveryFee + Commission (không payout ngay).

### 2.5 Community Fund Misappropriation (SysAdmin → Vạn An/Cộng đồng)

**Hành vi:**
- SysAdmin approve spend cho bản thân / người liên quan (conflict of interest — `community-fund-policy.md` §5.6).
- SysAdmin approve spend với reason vague ("chi chung", "vận hành") → rút quỹ sai mục đích.
- SysAdmin approve spend cho recipient không tồn tại (ghost recipient).
- SysAdmin + recipient collude inflate amount.

**Detection:**
- **Self-approval:** ApprovedBy == Recipient (string match) → block (system validate).
- **Reason keyword blocklist:** "chi chung", "vận hành", "khác" (vague) → require manual review (Sprint 7 chưa auto, manual).
- **Recipient vetting:** Spend > [5.000.000 VND] → Finance vet recipient (manual — `community-fund-policy.md` §5.3).
- **Spend frequency:** SysAdmin approve > [3] spends trong 24h → flag review.
- **Spend amount anomaly:** Spend > [20%] current balance trong 1 tx → flag.

**Response:**
- Confirm fraud → reverse CommunityFundSpend tx + ban SysAdmin + legal action.
- Whistleblow channel: legal@vanan.cloud (anonymous — `community-fund-policy.md` §8.2).
- External audit recommend (post-Sprint 7 tech debt).

### 2.6 External Payment Fraud (Q5 — Non-COD)

**Hành vi:**
- Fake payment confirmation: webhook giả mạo confirm payment chưa nhận.
- Chargeback fraud: customer thanh toán → nhận hàng → chargeback (claim không nhận).
- Stolen card: customer dùng thẻ ăn cắp → Vạn An chargeback + mất hàng.
- VietQR manipulation: fake QR redirect → customer chuyển tiền sai tài khoản.

**Detection:**
- **Webhook signature verify:** Payment gateway webhook phải có HMAC signature (system validate — Sprint 7 cần implement).
- **Chargeback monitor:** Customer có chargeback > [1] trong 90 ngày → flag + hold future non-COD.
- **Velocity check:** Customer non-COD payment > [5] orders trong 1h → flag (card testing).
- **IP geolocation:** Customer IP geolocation ≠ delivery address → flag.

**Response:**
- Fake webhook → block + ban source IP + legal action.
- Chargeback fraud → reverse Settlement + Commission + DeliveryFee (hold shipper fee) + ban customer.
- Stolen card → cooperate with payment gateway + police report.
- VietQR manipulation → verify QR signature + whitelist Vạn An official QR.

### 2.7 Settlement Fraud (Tenant → Vạn An)

**Hành vi:**
- Tenant nhận Settlement (CostPrice) nhưng không giao hàng cho shipper.
- Tenant nhận Settlement + giao hàng kém chất lượng (customer reject) → giữ Settlement.
- Tenant nhận Settlement + cancel order sau khi nhận → không hoàn.

**Detection:**
- **Settlement-to-pickup delay:** Tenant nhận Settlement nhưng shipper không pickup trong [48h] → flag.
- **Customer reject rate:** Tenant có customer reject (quality) > [10%] → flag review warranty.
- **Settlement reversal rate:** Tenant có reversal Settlement tx > [5%] → flag.

**Response:**
- Confirm fraud → reverse Settlement tx + terminate agreement + legal action.
- Tenant không hoàn Settlement → claim bồi thường theo `reseller-agreement.md` §9.
- Hold future Settlement cho tenant (manual flag — tenant phải delivery trước, settlement sau).

---

## 3. Risk Scoring (Reseller extension)

Baseline RiskScore (0-100) áp dụng cho SalesReferral, AppInstallAttribution, DeviceRegistration. Reseller extension bổ sung:

| Entity | RiskScore source |
|---|---|
| Order (Reseller) | Margin %, CostPrice outlier, advance ratio, salesman self-referral |
| ProductCostPrice | Update frequency, % change, admin update pattern |
| CommunityFundSpendRecord | Amount vs balance, reason keyword, recipient vetting, self-approval |
| WalletTransaction (Reseller) | Settlement-to-pickup delay, COD-to-confirm delay, chargeback |

**Threshold:** RiskScore ≥ 50 → tạo FraudFlag (Pending) → admin review (baseline §4 workflow).

---

## 4. Hold 48h (Reseller extension)

Baseline hold 48h cho Salesman Commission + AppInstall bonus. Reseller extension:

| Tx type | Hold? | Lý do |
|---|---|---|
| Settlement (tenant) | No | Tenant cần tiền mua hàng — hold sẽ block supply chain |
| DeliveryFee (shipper) | **Yes 48h** | Cho admin thời gian review COD skimming |
| Commission (salesman, OnMargin) | **Yes 48h** | Baseline + Reseller margin manipulation risk |
| PlatformFee (Vạn An) | No | Internal wallet, không payout external |
| CommunityFund (quỹ) | No | Internal wallet, không payout external |
| CommunityFundSpend (rút quỹ) | **Yes 24h** | Cho whistleblow window trước khi tiền ra bank |
| ExternalPayment (confirm) | No | Real-time (cần webhook signature verify) |

---

## 5. KYC (Reseller extension)

Baseline KYC cho CTV payout. Reseller extension:

| Role | KYC requirement |
|---|---|
| Tenant (payout Settlement) | **NEW:** Tenant phải submit business KYC (MST, giấy phép KD, CCCD đại diện pháp luật) trước khi receive Settlement payout. |
| SysAdmin (approve CommunityFundSpend) | **NEW:** SysAdmin phải submit personal KYC (CCCD + bank account) trước khi được quyền approve spend. |
| Customer (non-COD payment) | Recommend KYC cho non-COD > [5.000.000 VND] per order — chưa mandatory trong Sprint 7. |

---

## 6. Wallet Reversal (Reseller extension)

Baseline reversal cho SalesReferral + AppInstall. Reseller extension:

| Fraud type | Reversal action |
|---|---|
| Cost price manipulation | Reverse Settlement tx (negate CostPrice) + renegotiate |
| Margin manipulation | Reverse Commission tx (negate) + strike salesman |
| Advance payment fraud | Reverse AdvancePayment tx (negate) + claim tenant |
| COD skimming | Reverse CODCollection tx (negate) + strike shipper |
| Community fund misappropriation | Reverse CommunityFundSpend tx (negate) + ban admin |
| External payment fraud | Reverse Settlement + Commission + DeliveryFee + ban customer |
| Settlement fraud | Reverse Settlement tx (negate) + terminate agreement |

**Reversal tx ghi rõ RelatedTransactionId** (baseline §8) — audit trail đầy đủ.

---

## 7. 3-Strike Ban (Reseller extension)

Baseline 3-strike cho Customer. Reseller extension:

| Role | Strike source | Ban action |
|---|---|---|
| Salesman | Margin manipulation, self-referral Reseller | Ban CTV role (baseline) |
| Shipper | COD skimming, fake delivery | Ban CTV role (baseline) |
| Tenant | Cost price manipulation, advance fraud, settlement fraud | **NEW:** Ban tenant (IsActive=false) + toggle Marketplace (không Reseller) |
| SysAdmin | Community fund misappropriation, self-approval | **NEW:** Ban admin role + legal action (no 3-strike — 1 strike = ban vì trust breach) |

**Tenant ban:** 3 strikes trong 90 ngày → ban tenant + auto-toggle Marketplace (tenant vẫn bán trực tiếp được, nhưng không được Reseller).

**SysAdmin ban:** 1 strike (misappropriation) → ban + legal action. Không cho cơ hội 2nd — vì trust breach với cộng đồng.

---

## 8. Minh bạch (Reseller extension)

Baseline: CTV xem FraudFlag của mình qua Profile. Reseller extension:

| Stakeholder | View quyền |
|---|---|
| Customer | Xem own order Reseller breakdown (cost price hidden, chỉ thấy SellPrice + DeliveryFee) |
| Tenant | Xem own Settlement + Advance tx + own FraudFlag (if any) |
| Salesman | Xem own Commission tx (OnMargin breakdown) + own FraudFlag |
| Shipper | Xem own DeliveryFee + CODCollection tx + own FraudFlag |
| SysAdmin | Xem full FraudFlag list + CommunityFundSpend audit |
| Public | Xem CommunityFund balance + anonymized spend history (`community-fund-policy.md` §6) |

---

## 9. Kháng cáo (Reseller extension)

Baseline: CTV kháng cáo ban qua support@vanan.cloud. Reseller extension:

| Role | Kháng cáo channel |
|---|---|
| Tenant | legal@vanan.cloud (commercial dispute — `reseller-agreement.md` §12) |
| SysAdmin (banned) | legal@vanan.cloud (employment/legal — không unban qua support) |
| Customer (non-COD ban) | support@vanan.cloud (baseline) |

---

## 10. Liên hệ

- Fraud team: fraud@vanan.cloud (baseline)
- Legal (Reseller dispute): legal@vanan.cloud
- Finance (cost price benchmark): finance@vanan.cloud
- Community fund whistleblow: legal@vanan.cloud (anonymous — `community-fund-policy.md` §8.2)

---

## 11. Sprint 7 scope note

**Implemented trong Sprint 7:**
- Fraud vectors documentation (§2).
- Risk scoring extension (§3) — manual flag, không auto-detect.
- Hold 48h DeliveryFee + Commission (§4).
- KYC tenant + SysAdmin (§5) — manual process.
- Wallet reversal Reseller (§6) — system support.
- 3-strike tenant + 1-strike SysAdmin (§7) — manual admin action.

**Tech debt (post-Sprint 7):**
- Auto-detect cost price outlier (§2.1) — cần benchmark data source.
- Auto-detect margin manipulation (§2.2) — cần ML/stat model.
- Auto-detect advance fraud (§2.3) — cần tenant behavior baseline.
- Webhook signature verify (§2.6) — cần payment gateway integration.
- Reason keyword blocklist auto (§2.5) — cần NLP.
- Recipient auto-vetting (§2.5) — cần third-party KYC API.
- Dual-control spend (§2.5) — `community-fund-policy.md` §5.1.
- External audit mandatory (§2.5) — `community-fund-policy.md` §5.5.

---

## 12. Tài liệu liên quan

- `anti-fraud-policy.md` — Baseline (Marketplace + CTV chung) — áp dụng đầy đủ
- `reseller-policy.md` — Quy chế Reseller
- `reseller-agreement.md` — Hợp đồng B2B (Dispute resolution §12)
- `community-fund-policy.md` — Quản trị quỹ (Misappropriation guardrail §5)
- `community-terms-of-service.md` — Điều khoản CTV (áp dụng cả 2 mode)
- `community-privacy-policy.md` — Bảo mật (áp dụng cả 2 mode)
