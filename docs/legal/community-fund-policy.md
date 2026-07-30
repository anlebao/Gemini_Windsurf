# Chính sách Quản trị Quỹ Phát triển Cộng đồng (Community Fund Policy) — VanAn

**Phiên bản:** 1.0 (Draft — Sprint 7 / Q3)
**Ngày hiệu lực:** [Điền ngày]
**Lưu ý:** Đây là bản draft, cần luật sư + kế toán review trước khi publish. Quỹ có dòng tiền thực + quyền rút của SysAdmin → cần guardrail pháp lý chặt chẽ.

**Cơ sở pháp lý:**
- Luật Doanh nghiệp 2020 (quỹ nội bộ doanh nghiệp)
- Luật Kế toán 2015 (ghi nhận quỹ, audit trail)
- Nghị định 13/2023/NĐ-CP (bảo vệ dữ liệu cá nhân — recipient data)
- Thông tư 200/2014/TT-BTC (chế độ kế toán doanh nghiệp — quỹ nội bộ)

---

## 1. Mục đích quỹ

1.1. **Quỹ phát triển cộng đồng (Community Fund)** là quỹ nội bộ Vạn An, tài trợ các hoạt động vì lợi ích cộng đồng người dùng VanAn (customers, CTV, tenants).

1.2. **Mục đích sử dụng:**
- Tài trợ sự kiện cộng đồng (offline meetup, workshop, charity).
- Hỗ trợ CTV/tenant gặp khó khăn (thiên tai, dịch bệnh).
- Đầu tư phát triển hạ tầng cộng đồng (feature cộng đồng, tool open-source).
- Đóng góp tổ chức phi chính phủ / quỹ từ thiện (đối tác chiến lược).
- Khác (cần SysAdmin approve + ghi rõ reason).

1.3. **KHÔNG dùng cho:**
- Chi phí vận hành Vạn An (lương, server, marketing) — đây là PlatformFee, không phải CommunityFund.
- Phân chia lợi nhuận cho cổ đông.
- Cho vay, đầu tư tài chính sinh lời.
- Mục đích cá nhân SysAdmin hoặc người liên quan.

---

## 2. Nguồn quỹ

2.1. **Nguồn chính:** Mỗi order Reseller mode hoàn tất (COD collected hoặc external payment confirmed), hệ thống tự động chuyển `CommunityFund = PlatformMargin × CommunityFundRate` vào CommunityFundWallet.

2.2. **Default rate:** CommunityFundRate = 5% Margin (configurable qua `POST /api/admin/commerce-mode/global`).

2.3. **Nguồn bổ sung (tùy chọn):**
- Donation tự nguyện từ tenant/customer (cần API riêng — chưa có trong Sprint 7).
- Round-up donation khi customer checkout (cần UI — chưa có trong Sprint 7).

2.4. **Marketplace mode:** KHÔNG đóng góp quỹ (Marketplace không có margin → không có CommunityFund tx).

---

## 3. Quản trị quỹ

### 3.1 Vai trò SystemAdmin
- **Approve spend:** Mỗi lần rút quỹ cần 1 SystemAdmin approve (ApprovedBy field trong CommunityFundSpendRecord).
- **Set rate:** SystemAdmin set CommunityFundRate (global) qua admin API.
- **View balance + history:** SystemAdmin xem full qua `/api/admin/community-fund/balance` + `/api/admin/community-fund/history`.

### 3.2 Vai trò Vạn An Finance Team
- **Reconcile:** Đối soát wallet balance vs bank account monthly.
- **Audit:** Review CommunityFundSpendRecord quarterly.
- **Report:** Báo cáo công khai balance + spend history cho cộng đồng (transparency — xem §6).

### 3.3 Vai trò Cộng đồng (customers + CTV + tenants)
- **View:** Xem balance + spend history qua API public (read-only).
- **Suggest:** Đề xuất mục sử dụng quỹ qua support@vanan.cloud.
- **Whistleblow:** Báo cáo nghi ngờ lạm dụng quỹ qua legal@vanan.cloud (anonymous option).

---

## 4. Quy trình rút quỹ (Spend Workflow)

### 4.1 Initiate
- SystemAdmin initiate spend qua `POST /api/admin/community-fund/spend`:
  - Amount (decimal, > 0)
  - Reason (string, 500 chars — ghi rõ mục đích)
  - Recipient (string, 200 chars — người nhận/đối tác)
  - WalletTransactionId (auto-created — CommunityFundSpend type)

### 4.2 Validate (system-side)
- Amount ≤ current CommunityFundWallet balance (else reject).
- Reason không rỗng, ≥ 10 chars.
- Recipient không rỗng.
- ApprovedBy = SystemAdmin userId (từ JWT).

### 4.3 Execute
- Hệ thống tạo 2 records:
  - `WalletTransaction(CommunityFundSpend, -amount, CommunityFundWallet)` — wallet tx (immutable).
  - `CommunityFundSpendRecord(amount, reason, recipient, approvedBy, spentAt, walletTransactionId)` — audit record.
- Balance giảm real-time.

### 4.4 Reconcile
- Finance team reconcile spend record vs bank transfer receipt monthly.
- Mismatch → escalate legal@vanan.cloud.

---

## 5. Guardrail (Anti-Abuse)

### 5.1 Single-approver limit
- Spend ≤ [10.000.000 VND]: 1 SystemAdmin approve (current Sprint 7 scope).
- Spend > [10.000.000 VND]: **RECOMMENDED** 2 SystemAdmin approve (dual-control) — chưa implement trong Sprint 7, ghi tech debt.

### 5.2 Reason documentation
- Reason field bắt buộc ≥ 10 chars, ghi rõ mục đích + người thụ hưởng.
- "Chi chung", "Vận hành" → reject (system validate keyword blocklist — chưa implement, manual review).

### 5.3 Recipient vetting
- Recipient phải là tổ chức/person có thể verify (tên + MST/CCCD nếu tổ chức).
- Spend > [5.000.000 VND] → Finance team vet recipient trước khi approve (manual process).

### 5.4 Audit trail immutability
- CommunityFundSpendRecord immutable (snapshot pattern — không update, chỉ create).
- WalletTransaction immutable (Reversal Entry pattern — nếu cần reverse, tạo tx ngược dấu).
- Audit log: ApprovedBy + SpentAt + WalletTransactionId — không xóa.

### 5.5 Transparency report
- Quarterly: Vạn An publish balance + spend history (anonymized recipient) cho cộng đồng.
- Annual: external audit (kế toán độc lập) — recommend, chưa mandatory trong Sprint 7.

### 5.6 Conflict of interest
- SystemAdmin approve spend cho bản thân / người liên quan → CẤM.
- Detect: ApprovedBy ≠ Recipient (string match + manual review).
- Violation → unban SysAdmin + reverse spend + legal action.

---

## 6. Minh bạch (Transparency)

### 6.1 Public API
- `GET /api/community/community-fund/balance` — current balance (no auth, public).
- `GET /api/community/community-fund/history` — spend history (anonymized recipient, no auth, public).
- `GET /api/admin/community-fund/history` — full history with recipient PII (SystemAdmin JWT).

### 6.2 Report cadence
- **Real-time:** API public (balance + anonymized history).
- **Monthly:** internal Finance report (full).
- **Quarterly:** public report (publish trên vanan.cloud/community-fund).
- **Annual:** external audit (recommend).

### 6.3 Anonymization
- Public history: recipient field anonymize (vd: "Tổ chức X" → "Tổ chức từ thiện #1").
- Admin history: full recipient (SystemAdmin JWT).
- Reason: full public (không anonymize).

---

## 7. Kế toán

### 7.1 Ghi nhận
- **Quỹ tích lũy:** mỗi CommunityFund tx (Reseller order hoàn tất) → ghi nhận "Quỹ phát triển cộng đồng" (TK 412 hoặc tương đương theo TT 200).
- **Quỹ chi:** mỗi CommunityFundSpend tx → ghi nhận chi quỹ (TK 665 hoặc tương đương).
- **Audit trail:** CommunityFundSpendRecord + WalletTransaction + Reversal Entry (nếu có).

### 7.2 Reconciliation
- Wallet balance (system) = Bank account balance (reality) — monthly reconcile.
- Mismatch > [1.000.000 VND] → escalate Finance + Legal.

### 7.3 Tax
- CommunityFund tx (income): Vạn An ghi nhận doanh thu (vì là phần margin Vạn An giữ) → chịu thuế TNDN.
- CommunityFundSpend (expense): ghi nhận chi phí — deductibility theo Luật Thuế TNDN (cần kế toán review per case).

---

## 8. Kháng cáo & Whistleblow

### 8.1 Kháng cáo spend decision
- Cộng đồng không đồng ý 1 spend → email legal@vanan.cloud trong 30 ngày.
- Legal team review → nếu vi phạm §5 → reverse spend + discipline SysAdmin.

### 8.2 Whistleblow (anonymous)
- Email legal@vanan.cloud (subject: "[WHISTLEBLOW] Community Fund").
- Anonymous option: không require sender identity.
- Vạn An protect whistleblower (không retaliate).

### 8.3 Escalation
- Violation §5.6 (conflict of interest) → legal action + báo cơ quan chức năng nếu cần.

---

## 9. Thay đổi chính sách

- Vạn An thông báo trước 14 ngày trước khi thay đổi (lâu hơn Marketplace 7 ngày — vì liên quan dòng tiền cộng đồng).
- Thay đổi CommunityFundRate = thay đổi chính sách → 14 ngày.
- Thay đổi guardrail (§5) → 14 ngày + community consultation (recommend).

---

## 10. Liên hệ

- Community fund team: community@vanan.cloud
- Finance (reconcile): finance@vanan.cloud
- Legal (whistleblow): legal@vanan.cloud
- Audit: audit@vanan.cloud

---

## 11. Sprint 7 scope note

- **Implemented:** Single-approver spend (§4), audit trail (§5.4), public balance API (§6.1).
- **Tech debt (post-Sprint 7):**
  - Dual-control spend > 10M VND (§5.1).
  - Reason keyword blocklist (§5.2).
  - Recipient auto-vetting (§5.3).
  - External audit mandatory (§5.5).
  - Donation API (§2.3).
  - Round-up donation UI (§2.3).
  - Public quarterly report automation (§6.2).

---

## 12. Tài liệu liên quan

- `reseller-policy.md` — Quy chế Reseller (quỹ là 1 trong 4 khoản phân chia)
- `reseller-agreement.md` — Hợp đồng B2B (Phụ lục 2 — CommunityFundRate)
- `anti-fraud-policy-reseller-addendum.md` — Fraud vectors liên quan quỹ
- `community-privacy-policy.md` — Bảo mật recipient data
