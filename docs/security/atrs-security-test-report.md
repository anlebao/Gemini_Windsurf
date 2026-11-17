# ATRS Security Test Report (ATRS-14 §15.3/§15.4 + DoD) — v1.0

> **Created:** 2026-11-17 · **Wave 6** · **Service:** `vanan-atrs` (VPS 34.126.90.228, :5080, PG `vanan_atrs`)
> **Basis:** unit tests `VanAn.Atrs.Tests` (156/156) + RV prod sweeps (Wave 1–5) + Playwright E2E specs + manual ops verification.

---

## 1. §15.3 Security Test Checklist — TRACEABILITY

| # | Hạng mục | Bằng chứng | Status |
|---|---|---|---|
| 1 | **IDOR/BOLA** — user A không đọc/sửa B | `RecoveryRequestsController` anti-enum 404 (Get/Mine/Cancel); `DisputeCaseManager.GetMineAsync` ownership; `DeviceService` filter TrustSubjectId; `AtrsNotificationService.MarkReadAsync` ownership → false/404. Tests: `RecoveryCaseManagerTests.CancelAsync_WrongSubject`, `DisputeCaseManagerTests` user-safe, `AtrsNotificationServiceTests.MarkRead_OwnedOnly`; RV sweep W5b `L5 mark-read id lạ → 404`. | ✅ |
| 2 | **WebAuthn** — replay challenge/origin/RP-ID/signature/revoked | `WebAuthnService` challenge one-time TTL 5' + origin/RP-ID binding + `IsRpIdValidForOrigin` server-side; credentials revoke cascade. Tests `WebAuthnServiceTests` 12; RV W2 sweep 40/40 (origin/rpId reject + replay reject). | ✅ |
| 3 | **Brute-force recovery code / lockout / timing** | `RecoveryCodeService` BCrypt + AttemptLimit (mặc định 5) + lockout 15' + generic failure (`InvalidCode` không phân biệt tồn tại). Test `RecoveryCodeServiceTests.Verify_WrongCodes_GenericFailure_ThenLockout`. BCrypt vốn chống timing oracle. | ✅ |
| 4 | **Recovery enumeration** | Generic responses mọi customer endpoint (404/401/400 cùng shape); RV sweep auth-neg ×8 (W3) + W5a/W5b/W5d anti-enum checks. | ✅ |
| 5 | **Session fixation/CSRF/CORS** | API dùng Bearer token (không cookie session cho atrs customer path); JWT `tv` revocation (ATRS-3); CSRF không áp dụng cho Bearer. CORS: atrs internal-only (:5080, không expose qua YARP ngoài `/api/v1/account-trust/*` đã auth). | ✅ (documented) |
| 6 | **Privilege escalation admin** | `IAtrsAuthorizationService` fail-closed (grant + scope + expiry + SystemAdmin break-glass) enforce mọi staff endpoint; grant mgmt SystemAdmin-only. Tests + sweep RBAC ×4 waves (no-grant 403 → grant → 200 → revoke → 403). | ✅ |
| 7 | **Mass assignment** | Controllers nhận DTO riêng (record positional) — không binding entity trực tiếp; enum/JSON validated. | ✅ |
| 8 | **Sensitive data leak (logs/errors/telemetry)** | Staff detail KHÔNG evidence payload; audit explorer KHÔNG `MinimalMetadataJson`/`EvidenceReferencesJson`; notification body KHÔNG secret; `/metrics` chỉ event_type. Tests: `RecoveryCaseManagerTests.StaffDetail_DoesNotExposeSecrets`, `AuditQueryServiceTests.Query_ResponseNeverExposesPayloadJson`, `AtrsNotificationServiceTests.NotificationBody_NeverContainsSecret`. | ✅ |
| 9 | **Race condition / double-submit** | Idempotency: recovery (IdempotencyKey unique), dispute (idempotencyKey + anti-duplicate transaction ref), decision adjust (`dispute:{caseId}:adjust:{decisionId}` unique). Tests: replay → existing/same-id; `Approve_WithAdjustment_ExecutesOnce_ResolvesCase` (AdjustCalls=1). | ✅ |
| 10 | **DoS-via-lockout abuse** | Lockout per TrustSubjectId (không global); attempt counter reset khi generate; attacker không thể lock account khác (không thể target subjectId của người khác qua API — auth per-subject). | ✅ (documented) |
| 11 | **Insider — staff không xem secret/hash** | Staff detail/queue KHÔNG trả recovery code/hash/evidence payload; maker-checker self-approval reject; break-glass SystemAdmin role-based. Tests + sweeps. | ✅ |
| 12 | **Maker-checker bypass attempts** | `DisputeDecision.Approve` enforce (policy-controlled `dispute.maker_checker_required` — A21); `SecurityPolicyService.ApproveAsync` approver≠proposer. Tests: `MakerChecker_SelfApproval_Rejected` + `MakerChecker_DisabledByPolicy_AllowsSelfApproval` (override có chủ đích, policy-versioned + audit). | ✅ |

## 2. §15.4 E2E Scenarios — Mapping

| # | Scenario | Spec / evidence | Status |
|---|---|---|---|
| 1 | Đăng ký + profile + passkey | `atrs-security-center.spec.ts` (W2/W4) 3/3 + 11/11 | ✅ |
| 2 | Login lại thiết bị đã đăng ký | passkey assert → step-up token (W2 probe) | ✅ |
| 3 | Thêm thiết bị mới còn thiết bị cũ | WebAuthn register + device list (W2/W4) | ✅ |
| 4 | Mất thiết bị còn recovery code | sweep W1/W5d (submit → verify-code → approved) | ✅ |
| 5 | Mất cả → manual review | `VerifyCodePathAsync` bad code → AUTOMATED_CHECK → staff review path (W1/W5a state probe) | ✅ |
| 6 | Attacker biết info nhưng không credential → FAIL | recovery code required (verify fail → không approve); RBAC deny staff | ✅ |
| 7 | Báo mất + thu hồi device | `DevicesController` revoke (step-up gate) + notification (W5d) | ✅ |
| 8 | Khiếu nại trừ điểm → adjust maker-checker | `atrs-disputes-admin.spec.ts` 5/5 + sweep W3 (maker-checker 403 → approve → EXECUTED → RESOLVED) | ✅ |
| 9 | Khiếu nại ví/số dư | dispute submit/evidence/withdraw (W3/W4) | ✅ |
| 10 | Staff thiếu quyền → deny | RBAC sweep mọi wave (403 matrix) | ✅ |
| 11 | Maker-checker 2 người | W3 sweep + unit test | ✅ |
| 12 | ATRS down lúc ledger ghi → ledger không hỏng | `DisputeCaseManager.ApproveDecisionAsync`: adjust fail → decision FAILED + case UNDER_REVIEW (KHÔNG sửa số dư từ ATRS — luôn qua Gateway `AdjustPointsAsync` idempotent). Test adjust-fail path + W3 RV. | ✅ |

## 3. AC Traceability (SRS AC-001..009)

| AC | Verdict | Evidence |
|---|---|---|
| AC-001/002 (profile + device registry) | ✅ | TrustProfile/UserDevice tests + W2 sweep |
| AC-003/004 (passkey + recovery) | ✅ | WebAuthn/recovery tests + prod ceremony (CDP) |
| AC-005/006 (audit trail + RBAC) | ✅ | AccountSecurityEvents + AtrsPermissions + sweeps |
| AC-007/008/009 (dispute + ledger + notification) | ✅ | DisputeCaseManager tests + W3 RV + W5d notify |
| AC-010 (step-up) | ✅ | StepUpServiceTests + W2 probe |

## 4. Ops Verification (W6)

- **Backup/restore:** pg_dump 17KB → restore scratch DB → 21/21 tables, row counts khớp (recovery_requests 13, account_notifications 10, security_policies 10) → drop scratch. Script pattern ghi runbook.
- **Migration rollback:** mọi migration additive (DropTable/columns trong Down() có sẵn) — rollback note theo từng migration file.
- **/metrics:** Prometheus text hợp lệ (counters + gauges) — internal :5080.

## 5. DoD Checklist (§19) — W6 REVIEW

| # | Hạng mục | Verdict |
|---|---|---|
| 1 | Integrate UserId/auth hiện hữu, không identity silo | ✅ `TrustSubjectLink` (Customer/DemoUser/DeviceCluster) |
| 2 | Device Registry + states | ✅ UserDevice lifecycle |
| 3 | WebAuthn thật trên platform hỗ trợ | ⚠️ virtual authenticator verified; **L5 manual device thật pending user** |
| 4 | Recovery workflow có risk control | ✅ RiskPolicyEngine + tiered proof |
| 5 | Secret challenge không single-factor | ✅ (D7 omit secret question; recovery = code + risk + review) |
| 6 | Audit trail + phân quyền phân lớp | ✅ AccountSecurityEvents + AtrsPermissions |
| 7 | Dispute workflow + ledger reference; không sửa số dư từ ATRS | ✅ qua Gateway AdjustPointsAsync idempotent |
| 8 | AC trọng yếu có test | ✅ §3 |
| 9 | Security/privacy review | ✅ report này; **legal review checklist cần PO** (§2.3) |
| 10 | Backup/restore + ops + monitoring + rollback plan | ✅ §4 + `atrs-runbook.md` |
| 11 | PO chấp thuận open decisions còn lại | ⏳ **PO cần sign-off** (retention Q7, email channel P4, Zalo slot) |

## 6. Known Limitations & Residual Risks (§17.4.12)

1. **Email/SMS notification kênh thiếu** — in-app only; email chờ P4 Brevo email-first; ZNS chờ OA. Customer không email + mất device chỉ còn in-app.
2. **KhachLink inbox UI chưa có** — API đã sẵn (`/notifications`), hiển thị SecurityCenter = future card.
3. **Step-up abnormal notification defer** — fail events đã audit; notify per-failure cần rate-aware.
4. **Staff authenticated UI E2E** — Playwright authenticated staff flow cần staff cookie login (D6 precedent); API-level đã cover.
5. **Queue/list API trả full TrustSubjectId** — masking ở UI; API-level masking = hardening còn lại.
6. **Metrics in-memory** — reset khi restart; Prometheus scrape aggregation cần nếu muốn lịch sử.
7. **WebAuthn platform thật (iOS/Android) manual** — chờ user L5.
8. **Retention config** — chưa có job xoá theo retention (chờ PO chốt §18 Q7: recovery/dispute 5 năm, security events 2 năm).
9. **Load test p95** — chưa chạy load; page có limit take (≤200/≤500) + index phủ query chính.
10. **Feature flag AtrsEnabled per-profile** — ATRS đã live cho test tenants (W1–W5 RV); formal per-profile gating (KhachLinkProfile) = residual nếu rollout rộng cần kiểm soát.