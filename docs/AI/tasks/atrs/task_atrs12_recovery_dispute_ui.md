# Task ATRS-12: Recovery & Dispute User UI — U09-U13

> **Master plan:** §4 · **Phụ thuộc:** ATRS-8 (recovery backend), ATRS-9 (dispute backend), ATRS-11 (security center shell) · **Mode:** IMPLEMENT sau duyệt
> **SRS coverage:** UI U09-U13, F03-F05, §9.1 status, §10-13, §14.2-14.3, §15 scenarios 9-15,23-24
> **Surface:** KhachLink (customer-facing) — entry từ U06 security center + dispute center.

## 1. OBJECTIVE

Wizard khôi phục tài khoản + theo dõi recovery + dispute center/submit/detail — responsive mobile-first, backend-authoritative mọi quyết định.

## 2. SCREENS

### U09 Account recovery wizard (4 steps theo UI SRS)
1. Describe access problem (lost device/damaged/passkey unavailable/recovery code unavailable/suspected unauthorized/other).
2. Choose available recovery methods — CHỈ hiển thị method account thật sự có (passkey khác, recovery code unused, approved method khác, "không dùng được cái nào").
3. Supporting info — prefill đã có; chỉ hỏi policy yêu cầu; attachments chỉ khi supported+cần+protected; nói rõ personal details không guarantee approval.
4. Review & submit — summary + privacy notice + checkbox + submit.
Rules: không re-enter data đã có; không tiết lộ evidence nào "đủ pass"; UI không tự tính outcome; high-risk → queue human; duplicate → show existing; không lockout vĩnh viễn.
States: step loading/validation/submitting/submitted/duplicate/service-unavailable.

### U10 Recovery request status
Reference + created + status + stage + last update + action-required + support instructions. Statuses map backend (Submitted/Under automated checks/Waiting for staff review/Additional info required/Approved/Rejected/Cancelled/Expired). Rejection reason an toàn (không lộ anti-fraud threshold). Actions: provide info/cancel(where allowed)/back.

### U11 Dispute center
Categories: điểm thưởng / ví-quyền lợi (chỉ khi authoritative wallet service tồn tại — AllianceWallet) / voucher-benefit / other ecosystem transaction. Open + recently resolved + link transaction history.

### U12 Submit dispute
Category + transaction selector (prefill từ authoritative service) + reason (backend-controlled list) + description + attachments optional policy-controlled + preferred contact nếu cần. Transaction details READ-ONLY từ source; không retype amount; review step trước submit; file type/size validate cả 2 phía.

### U13 Dispute detail
Reference/category/transaction ref/dates/status/user-visible timeline/user description+attachments/requests for info/final user-facing explanation. KHÔNG lộ internal notes/fraud signals/audit metadata. Withdraw nếu policy. Resolution chỉ reflect khi ledger confirm.

## 3. GLOBAL RULES

- Status hoàn toàn từ backend — không infer từ elapsed time.
- Idempotency: retry submit không tạo duplicate (gửi `Idempotency-Key`).
- Session expire giữa wizard → preserve safe draft (sessionStorage cho non-sensitive fields only — §12.1 cấm secrets).
- Offline/PWA: submit fail → retry an toàn; không queue nhạy cảm.
- Rejection/denial wording: an toàn, không lộ threshold.
- A11y + mobile-first + UI Platform components.

## 4. FILES (dự kiến)

- `5_WebApps/KhachLink/Pages/Recovery/*.razor`, `Pages/Disputes/*.razor` + services/DTOs
- Wizard component dùng chung (pattern step header/progress — UI §9 shared components)
- Transaction selector component consume ledger history API (ATRS-9)

## 5. TESTS

- Playwright §15 scenarios 9-15 + 23-24: recovery with credential, all-lost → review, duplicate handling, status tracking, provide-info, loyalty dispute linked real txn, no edit txn values, status changes, no-dup on retry, no secrets in console/URL/storage.
- Component tests: wizard step validation, method availability theo account state.

## 6. ACCEPTANCE

- [ ] UI §14.2 + §14.3 + §14.5 pass; F03/F04/F05 E2E pass
- [ ] 24 Playwright scenarios coverage (một phần staff scenarios ở ATRS-10/14)

## 7. RISKS

- Unauthenticated recovery entry (ATRS-8 D-mở) → UI phải handle "chưa login" state gracefully.
- Dispute "ví" — nếu user hiểu nhầm metering wallet → copy phải nói rõ điểm/quyền lợi (gap risk #4).
- Attachments R2 signed URL expiry → UX download lại sau expiry.
