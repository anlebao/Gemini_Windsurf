# Task ATRS-14: Hardening — Security Tests, Privacy, Retention, Resilience

> **Master plan:** §4 · **Phụ thuộc:** tất cả cards trước · **Mode:** IMPLEMENT/FIX_ONLY theo findings
> **SRS coverage:** NFR-001..006, §11 privacy/legal, §15.3 security tests + §15.4 E2E 12 scenarios, §14 AC-008/009, §19 DoD phần test

## 1. OBJECTIVE

Hardening toàn diện trước release: security test suite, privacy artifacts, retention, backup/restore, load test — biến "build pass" thành "an toàn thật".

## 2. PHẠM VI

### 2.1 Security test suite (§15.3 — bắt buộc đủ)
- [ ] IDOR/BOLA: mọi endpoint `/account-trust/*` — user A không đọc/sửa B (profile, devices, credentials, recovery, disputes)
- [ ] WebAuthn: replay challenge, origin/RP ID mismatch, sai chữ ký, revoked credential dùng lại
- [ ] Brute-force secret challenge/recovery code (nếu có)/OTP — rate limit + lockout + không timing oracle
- [ ] Recovery enumeration: response không phân biệt account tồn tại/không
- [ ] Session fixation/hijacking; CSRF/CORS misconfig
- [ ] Privilege escalation admin endpoints; mass assignment
- [ ] Sensitive data leakage trong logs/errors/telemetry (grep + test)
- [ ] Race condition: revoke credential ∥ recovery đồng thời; double-submit idempotency
- [ ] DoS-via-lockout abuse
- [ ] Insider: staff L1 không xem secret/hash; maker-checker bypass attempts

### 2.2 E2E scenarios (§15.4 — 12 scenarios bắt buộc)
Mapping → Playwright specs (local + prod RV):
1. Đăng ký + profile + passkey (ATRS-11)
2. Login lại thiết bị đã đăng ký
3. Thêm thiết bị mới còn thiết bị cũ (§6.3)
4. Mất thiết bị còn recovery code
5. Mất cả → manual review
6. Attacker biết tên/ngày sinh/secret answer nhưng không credential → FAIL recovery
7. Báo mất + thu hồi device
8. Khiếu nại trừ điểm → adjust qua maker-checker
9. Khiếu nại ví/số dư
10. Staff thiếu quyền xem secret/duyệt recovery → deny
11. Maker-checker 2 người
12. ATRS down trong lúc ledger ghi → ledger không hỏng (NFR-003 fail-safe)

### 2.3 Privacy & compliance (§11 + UI §12)
- Data inventory table: mỗi field → mục đích, retention, pháp nhân xử lý
- Retention config: recovery/dispute cases + evidence (đề xuất 5 năm kế toán), security events (2 năm), personal evidence theo mục đích — **chờ PO chốt §18 Q7**
- DPIA note + consent withdraw flow test
- Legal review checklist trước release (không tự kết luận pháp lý)

### 2.4 Resilience & ops
- Backup/restore test cho ATRS DB
- Migration rollback/forward-fix plan
- ATRS down → chức năng đọc cơ bản không sập (NFR-003); sensitive actions fail-closed/step-up
- Idempotent retry verification (outbox/event duplicate delivery)
- Load test nhẹ: device list/events/case pagination p95 targets (NFR-004)

## 3. FILES

- `6_Tests/VanAn.Core.Tests` + `6_Tests/VanAn.Integration.Tests` + `6_Testing/e2e-tests` specs mới
- `docs/security/atrs-security-test-report.md` (deliverable §17.4.9)
- `docs/ops/atrs-runbook.md` draft (ATRS-15 hoàn thiện)

## 4. ACCEPTANCE

- [ ] §15.3 checklist 100% có test + pass
- [ ] §15.4 12/12 E2E pass (prod RV theo playwright.rules)
- [ ] AC-001..009 tổng hợp pass (traceability table trong report)
- [ ] Security test report + known limitations/residual risks (§17.4.12)

## 5. RISKS

- Playwright governance: chỉ chạy khi build pass + implementation complete; max scope theo rules.
- Một số security tests cần staging env (attacker scenarios) — dùng test tenant riêng, cleanup sau.
