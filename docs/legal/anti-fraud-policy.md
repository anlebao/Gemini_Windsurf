# Chính sách Chống Gian lận (Anti-Fraud Policy) — VanAn Community

**Phiên bản:** 1.0 (Draft — v1.2 NEW)
**Ngày hiệu lực:** [Điền ngày]
**Lưu ý:** Đây là bản draft, cần luật sư review trước khi publish.

---

## 1. Mục đích
- Bảo vệ VanAn, shop owner, và CTV khỏi gian lận.
- Phát hiện và ngăn chặn fraud sớm.
- Minh bạch quy trình review và ban.

## 2. Loại fraud phát hiện

### 2.1 SalesReferral Fraud
- Salesman tự mua qua QR của mình để nhận hoa hồng.
- Wash trading — tạo đơn ảo để tăng hoa hồng.
- Same IP / same device cho salesman + buyer.

### 2.2 AppInstallAttribution Fraud
- Cài app nhiều lần trên cùng thiết bị để nhận bonus.
- Click spam — click ảo để claim attribution.

### 2.3 DeviceRegistration Fraud
- Tạo nhiều tài khoản trên cùng thiết bị.
- Device fingerprint trùng lặp.

### 2.4 Shipper Fraud
- Đánh dấu Delivered nhưng không giao hàng.
- Thu COD nhưng không nộp lại cho shop.

## 3. Phát hiện (Risk Scoring)
- Mỗi entity (SalesReferral, AppInstallAttribution, DeviceRegistration) có RiskScore 0-100.
- RiskScore tính từ risk factors: same IP, same device, same phone, rapid succession, etc.
- RiskScore ≥ 50 → tạo FraudFlag (Pending).

## 4. Review workflow
1. **FraudFlag Pending** → hiển thị trên `/admin/community/fraud-flags` (sort by RiskScore desc).
2. **Admin review**: xem detail (risk factors, related entity, customer history).
3. **Confirm** → reject entity + wallet reversal (if paid) + strike.
4. **Dismiss** → whitelist device (if applicable) + no strike.

## 5. 3-Strike Ban Rule (v1.2 NEW)
- **Strike 1**: Confirm FraudFlag → 1 strike.
- **Strike 2**: Confirm FraudFlag → 2 strikes.
- **Strike 3**: Confirm FraudFlag → **auto-ban** (Customer.IsActive = false).
- **Banned customer**: không thể đăng nhập, không thể nhận CTV role, không thể mua hàng.
- **Unban**: chỉ admin có thể unban (manual DB update — không có API tự động).

## 6. Hold 48h
- Hoa hồng Salesman: hold 48h trước khi payout.
- App install bonus: hold 48h trước khi payout.
- **Mục đích**: cho admin thời gian review FraudFlag trước khi tiền ra.

## 7. KYC Bank Account (Payout)
- CTV muốn rút tiền từ ví → phải submit KYC bank account.
- KYC gồm: tên chủ tài khoản, số tài khoản, ngân hàng, CCCD.
- VanAn verify KYC trước khi approve payout.
- **Mục đích**: chống money laundering, đảm bảo tiền đến đúng người.

## 8. Wallet Reversal
- Khi Confirm FraudFlag cho entity đã paid:
  - SalesReferral: reverse commission tx (create Reversal tx, negate amount).
  - AppInstallAttribution: reverse bonus tx.
- Reversal tx ghi rõ RelatedTransactionId để audit trail.

## 9. Device Whitelist (Dismiss)
- Khi Dismiss FraudFlag cho DeviceRegistration:
  - Device.IsVerified = true (whitelist).
  - Device sẽ không bị flag lại trừ khi có hành vi đáng ngờ mới.

## 10. Minh bạch
- CTV có thể xem FraudFlag của mình qua Profile page (`/api/community/my-fraud-flags`).
- Admin có thể xem full FraudFlag list + stats.
- Audit trail: mọi Confirm/Dismiss đều ghi ReviewedBy + ReviewedAt + ReviewNote.

## 11. Kháng cáo
- CTV bị ban có thể kháng cáo qua support@vanan.cloud.
- VanAn review trong 7 ngày.
- Nếu kháng cáo thành công: unban + xóa strikes.

## 12. Liên hệ
- Email: fraud@vanan.cloud
