# Task ATRS-8: Recovery Workflow Backend (FR-011, §6.3-6.4)

> **Master plan:** §4 · **Phụ thuộc:** ATRS-3 (revoke/step-up), ATRS-4 (recovery codes), ATRS-5 (passkey), ATRS-7 (risk engine) · **Mode:** IMPLEMENT sau duyệt
> **SRS coverage:** FR-011, §6.3, §6.4, U09/U10 backend, F03/F04 backend, §9.3

## 1. OBJECTIVE

RecoveryRequest state machine đầy đủ: submit → automated checks → (user action | staff review) → approve/reject → post-recovery (revoke credentials/sessions, register new, delay sensitive actions) — với audit + notify + idempotency.

## 2. THIẾT KẾ

### 2.1 `RecoveryRequests` (§7.6)
Fields: `Id, TrustSubjectId, RequestType(LOST_DEVICE/NO_CREDENTIAL/COMPROMISED/OTHER), Status, NewDeviceId?, EvidenceSummary(JSON metadata only), RiskDecision, PolicyVersion, ReviewerId?, DecisionReasonCode?, CreatedAt, CompletedAt` + `IdempotencyKey` unique.
- States (FR-011): `SUBMITTED → AUTOMATED_CHECK → PENDING_USER_ACTION | PENDING_REVIEW → APPROVED | REJECTED | CANCELLED | EXPIRED | COMPLETED`.
- `COMPLETED` = sau khi user đăng ký credential mới thành công (tách approve ≠ completed — user phải re-enroll).

### 2.2 Flow xử lý
1. `POST /recovery-requests` — auth: cho phép anonymous-ish? **Quyết định:** user mất hết credential → không có token → endpoint phải verify bằng recovery code/passkey-on-new-device HOẶC tạo request ở trạng thái unauthenticated với proof tối thiểu (SĐT/email đã verify + captcha). **→ Cần quyết định trong card (D-mở): recovery entry auth model.**
2. Idempotency key chống duplicate; rate limit per subject+contact.
3. Automated check: recovery code valid? passkey on accessible device? old device still active? → risk engine (ATRS-7) quyết định ALLOW/step-up/REVIEW/DENY.
4. Old device còn dùng được → ưu tiên approve từ old device + step-up (§6.3, FR-011).
5. Manual review → queue cho staff (ATRS-10 consume; backend expose internal endpoints).
6. Approve → `COMPLETED` path: revoke credentials/sessions cũ (ATRS-3 `RevokeAllTokensAsync` + `DeviceCredential` revoke + `DeviceRegistration` LOST/REVOKED), bắt user đăng ký passkey/recovery-code mới, áp delay/hạn chế giao dịch nhạy cảm theo policy (config, notify rõ).
7. Notify: kênh đã đăng ký (email/in-app — ATRS-13); không chứa bí mật.

### 2.3 Edge cases bắt buộc (UI §13 + FR-011)
- Duplicate active request → trả existing.
- Cancel bởi người tạo khi còn an toàn.
- Expiry config; session-expired giữa flow.
- "Không khóa toàn bộ tài khoản máy móc" — chỉ TEMPORARY_RESTRICT hành động nhạy cảm.

## 3. FILES (dự kiến)

- `RecoveryRequest` entity + state machine (domain methods: Submit/AiCheck/RequestInfo/Approve/Reject/Cancel/Expire/Complete)
- `RecoveryCaseManager` service + controller (user endpoints) + internal staff endpoints
- Integrations: RiskPolicyEngine, StepUpService, token revocation, DeviceRegistration, WebAuthn/recovery-code verify, NotificationService

## 4. TESTS

- [ ] State machine: mọi transition hợp lệ + invalid transitions reject
- [ ] Idempotent submit (cùng key → same request)
- [ ] Rate limit + anti-enumeration (response không lộ account tồn tại)
- [ ] Approve → tokens revoked + credentials revoked + new enrollment required
- [ ] Post-recovery restriction: sensitive action bị chặn trong delay window
- [ ] Old-device-available path → step-up approve nhanh hơn manual
- [ ] Audit events đầy đủ mỗi transition

## 5. ACCEPTANCE

- [ ] E2E backend: mất thiết bị còn recovery code → recover; mất hết → manual review → approve → re-enroll
- [ ] AC-006 pass; security tests recovery-enumeration pass

## 6. RISKS

- **Unauthenticated recovery entry** = attack surface lớn nhất — phải có captcha + strict rate limit + risk engine chặn sớm; manual review mặc định cho R3.
- Revoke "theo khả năng hệ thống" — JWT stateless đã giải ở ATRS-3; realtime hub sessions cần disconnect (check `StaffJwtValidator`/Hub auth).
- Delay window có thể bị lạm dụng DoS — config + notify.
