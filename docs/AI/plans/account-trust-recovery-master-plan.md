# MASTER PLAN: VẠN AN ACCOUNT TRUST & RECOVERY SERVICE (ATRS) v1.0

> **Created:** 2026-11-15
> **Status:** ✅ **APPROVED 2026-11-15** — decisions D1-D10 + recovery-entry auth model CHỐT THEO BẢNG (user duyệt; chi tiết `atrs-decision-support.md`)
> **Progress 2026-11-17:** Wave 0-4 **CLOSED + deployed + RV prod PASS** (theo `atrs-execution-strategy.md`): Wave 1 skeleton recovery · Wave 2 WebAuthn + UI subset · Wave 3 dispute + RBAC subset · Wave 4 user UI full (U01-U13, U04 omit D7) + ATRS-6 evidence subset + recovery mine/cancel + D9 R2 attachments. **NEXT: Wave 5** = ATRS-10 full (A14/A18-full/A20-A22) + ATRS-13 notifications/observability → sau đó Wave 6 hardening+rollout (ATRS-14/15). Residuals Wave 3-4: `FlagExternalEvidencePending` job · staff MFA step-up · D6 authenticated Playwright.
> **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
> **SRS nguồn:**
> - `docs/requirements/VAN_AN_Account_Trust_and_Recovery_Service_v1.0.md` (service/backend)
> - `docs/requirements/VAN_AN_Account_Trust_UI_UX_SRS_v1.0 (1).md` (UI/UX — đề xuất rename bỏ "(1)")
> **Phase 0 audit:** `docs/requirements/BASE_CODE_GAP_ANALYSIS_ATRS.md` — **DONE 2026-11-15**
> **Liên quan bắt buộc:** `van-an-identity-tiered-trust-master-plan.md` (L0-L3, ID-P1, MFA-1, P4) · `notification-hub-master-plan.md` · `phi-nen-tang-metering-master-plan.md`

---

## 1. MỤC TIÊU

Xây dựng lớp **bằng chứng quyền kiểm soát tài khoản + khôi phục + tranh chấp** dùng chung toàn hệ sinh thái Vạn An — KHÔNG phải "Device ID thay OTP", KHÔNG phải IdP mới, KHÔNG phải ledger thứ hai.

4 ranh giới bất biến (SRS §20):
1. **Device Identity** — nhận diện + xác minh credential (tín hiệu rủi ro, không phải bằng chứng tuyệt đối).
2. **Personal Evidence** — thông tin khai báo/xác minh có nguồn gốc.
3. **Recovery & Dispute** — đánh giá đa bằng chứng + policy + quyết định có audit.
4. **Transaction Ledger** — `ILoyaltyPointLedgerService`/`IAllianceWalletService` vẫn là source of truth; ATRS chỉ tham chiếu.

## 2. ĐỊNH VỊ so với IDENTITY L0-L3 (BẮT BUỘC — tránh trùng lặp)

| Initiative | Phạm vi | Quan hệ với ATRS |
|---|---|---|
| Identity L0-L3 (ID-P1..P7) | Trust level của account (ai là ai) | **ATRS tiêu thụ** `IdentityLevel`/`UserTrustLevel`/`VerifiedChannel` làm input cho Risk Engine; KHÔNG tạo trust model song song |
| MFA-1 Admin TOTP | Step-up cho staff login | ATRS tái dùng `MfaService`/trusted-device pattern cho step-up primitive |
| P4 OTP (ID-P3) | Verify kênh liên hệ L2 | ATRS dùng verified channel làm recovery contact; phụ thuộc hardening `OtpService` (CSPRNG) |
| **ATRS** | Recovery workflow, device credential, dispute evidence, case management | **MỚI** — phần chưa ai cover |

**Nguyên tắc:** ATRS = "account recovery & dispute" layer. Trust/identity = đã có plan riêng. Ranh giới: ATRS đọc trust state, không định nghĩa lại.

## 3. OPEN DECISIONS — ✅ ĐÃ CHỐT 2026-11-15 (theo `atrs-decision-support.md`)

| # | Quyết định | Đã chốt | Card ảnh hưởng |
|---|---|---|---|
| D1 | **Canonical identity** | **`TrustSubjectId`** (UUID mới) + `TrustSubjectLink` map `CustomerId`(per-tenant)/`DemoUserId`/DeviceId-cluster; lookup `PrimaryContactHash` (HMAC, index-only, KHÔNG PK) | ATRS-1, ATRS-2 |
| D2 | **Deployment** | **Service riêng `vanan-atrs`** (pattern `10_Voice`: sln riêng, Dockerfile, port ~5080, **PG riêng** — trust registry cross-tenant) + contracts trong `1_Shared` | ATRS-1, tất cả |
| D3 | **Token revocation** | **`TokenVersion` claim** (JWT `tv` + customer token payload) + cache check TTL 60s + write-through khi bump; **1 helper chung `TokenVersionValidator` trong `1_Shared`** (mọi validator: Gateway/voice/tenantai) | ATRS-3 |
| D4 | **DisputeCase** | Bảng mới `DisputeCases` tổng quát; `LoyaltyReconcileCase` giữ nguyên (system-error), map thành dispute category system-discrepancy | ATRS-9 |
| D5 | **Staff RBAC** | Permission table mới `AtrsPermissions` (8 nhóm quyền UI §4.2) | ATRS-10 |
| D6 | **WebAuthn lib** | `Fido2NetLib` (MIT, stable — verify version ≥7 ngày khi implement) | ATRS-5 |
| D7 | **U04 secret question** | **OMIT v1.0** — đánh dấu "not implemented, documented" trong SRS | ATRS-6 |
| D8 | **Security events** | Bảng riêng `AccountSecurityEvents` (schema FR-009) + AuditLog giữ cho entity CRUD; chung `CorrelationId` khi cần trace | ATRS-2 |
| D9 | **Attachment storage** | R2 object storage + signed URL + type/size validation | ATRS-9 |
| D10 | **Scope v1.0** | **Customer-only** recovery/dispute; staff = MFA-1 + runbook reset (viết runbook TRƯỚC khi bật tính năng) | ATRS-8, ATRS-10 |

**Recovery-entry auth model (đã chốt):** tiered proof — ① còn recovery code → verify code · ② không code → verified-contact OTP (email-first Brevo, P4) + captcha + risk engine · ③ không gì → **manual review mặc định R3 + maker-checker**. Bắt buộc: anti-enumeration (generic response), rate limit + lockout progressive, captcha ở entry, log `AccountSecurityEvents`. Cần verify tỷ lệ customer có email (ATRS-13).

## 4. PHASES + TASK CARDS

| Card | Phase | Nội dung | Phụ thuộc | SRS coverage chính |
|---|---|---|---|---|
| `task_atrs0_discovery` | P0 | ✅ DONE — gap analysis | — | SRS §17.1, UI §16 P0 |
| `task_atrs1_decisions_srs_revision` | P1 | Chốt D1-D10 → ADR + sửa 2 SRS (canonical identity, deployment, status mapping, tenancy, U04 omit) | P0 | SRS §18, UI §18 |
| `task_atrs2_foundation_schema` | P2 | Domain entities + EF + migration: `AccountTrustProfile`, `TrustSubject` mapping, extend `DeviceRegistration`, `AccountSecurityEvents`, `ConsentRecords`, tenancy model | ATRS-1 (D1,D2,D8) | FR-001, FR-002, FR-003(một phần), FR-009, §7, NFR-002 |
| `task_atrs3_token_revocation_stepup` | P2 | `tokenVersion` claim + revocation check + step-up token primitive (dùng lại MFA trusted-device pattern) | ATRS-1 (D3) | FR-011 (revoke), FR-010 step-up, NFR-001 |
| `task_atrs4_recovery_codes` | P3 | Recovery codes: generate/hash/one-time/revoke/regenerate + UI hiển thị 1 lần | ATRS-2 | FR-008, U05, AC |
| `task_atrs5_webauthn_passkey` | P3 | WebAuthn registration/assertion (Fido2NetLib), `DeviceCredentials`, challenge one-time, revocation | ATRS-3 (step-up), ATRS-4 | FR-004, FR-005, U03, AC-003 |
| `task_atrs6_evidence_secret_challenge` | P3 | Personal Evidence metadata layer (map VerifiedChannel/IdentityLevel, không copy PII) + Consent records + **SecretChallenge CHỈ nếu PO giữ U04** | ATRS-2 | FR-006, FR-007(cond), U02, U04(cond) |
| `task_atrs7_risk_policy_engine` | P4 | Deterministic rules engine, R0-R4, reason codes, policy version, config ngưỡng | ATRS-2, ATRS-3 | FR-010, §9.1-9.3 |
| `task_atrs8_recovery_workflow` | P4 | RecoveryRequest state machine, idempotency, rate limit, manual review queue backend, post-recovery restrictions + session revoke, notifications | ATRS-3, ATRS-7 | FR-011, §6.3-6.4, U09/U10 backend, F03/F04 backend |
| `task_atrs9_dispute_workflow` | P5 | DisputeCase/EvidenceItems/Decisions, ledger reference integration, maker-checker → `AdjustPointsAsync`/`RefundAsync`, attachment R2, transaction history API | ATRS-2, ATRS-7, ATRS-10 (RBAC) | FR-012, FR-013, FR-014, FR-015(một phần), U11-U13 backend, F05/F07 backend |
| `task_atrs10_staff_rbac_admin` | P5 | `AtrsPermissions` + 8 role groups + staff screens A14-A16 (queue/review), A20 (audit explorer), A21 (policy), A22 (roles) | ATRS-8, ATRS-9 | FR-014, §13, UI §4, A14-A22 |
| `task_atrs11_user_security_ui` | P5 | U01 onboarding, U02 profile, U03 passkey, U05 recovery codes, U06 security center, U07/U08 device registry/detail | ATRS-4,5,6 + UI platform | UI U01-U08, F01-F02 |
| `task_atrs12_recovery_dispute_ui` | P6 | U09 recovery wizard, U10 status, U11 dispute center, U12 submit, U13 detail | ATRS-8,9,11 | UI U09-U13, F03-F05 |
| `task_atrs13_notifications_observability` | P6 | Security notification templates (email/in-app), alert rules, metrics dashboard, correlation ID | ATRS-8,9 | FR-011 notify, NFR-006, §9.3 |
| `task_atrs14_hardening_privacy` | P7 | Security test suite §15.3, privacy review artifacts, retention config, backup/restore test, load test | tất cả | NFR-001..005, §11, §15 |
| `task_atrs15_rollout_rv` | P7 | Feature flag rollout theo app, runbook recovery/dispute, ops training, DoD review | ATRS-14 | §16 P5, §19 DoD |

**Thứ tự đề xuất:** `ATRS-1 → ATRS-2 → ATRS-3 → {ATRS-4 ∥ ATRS-6} → ATRS-5 → ATRS-7 → ATRS-8 → ATRS-9 → {ATRS-10 ∥ ATRS-11} → ATRS-12 → ATRS-13 → ATRS-14 → ATRS-15`

**Song song với identity initiative:** ATRS-2..4 nên chạy **sau hoặc cùng wave** ID-P1/P4 (đụng `Customer`/`DemoUser` schema + phone lookup). Không block MFA-1.

## 5. SRS COVERAGE MATRIX (không bỏ sót)

### Service SRS

| Req | Nội dung | Card |
|---|---|---|
| FR-001 | Bind 1 TrustProfile/UserId, không tạo user mới, sync trạng thái account | ATRS-2 |
| FR-002 | Tạo profile PENDING_SETUP, partial completion, privacy notice version | ATRS-2, ATRS-6 |
| FR-003 | Device register: DeviceId random, states, history, không fingerprint-only, không IMEI | ATRS-2 |
| FR-004 | WebAuthn/passkey: challenge one-time, verify bằng lib, nhiều credential, revoke, synced-passkey wording | ATRS-5 |
| FR-005 | PWA fallback: Installation ID + signed cookie, không cấp quyền cao chỉ bằng nó | ATRS-5 |
| FR-006 | Personal evidence fields + metadata (source/verificationStatus/privacyNoticeVersion) | ATRS-6 |
| FR-007 | Secret challenge (conditional — D7 đề xuất omit) | ATRS-6 |
| FR-008 | Recovery codes: hash, one-time, regenerate step-up | ATRS-4 |
| FR-009 | Activity evidence events + schema | ATRS-2 (table), ATRS-8/9/13 (emit) |
| FR-010 | Risk & step-up policy deterministic, R-levels, reason codes | ATRS-3, ATRS-7 |
| FR-011 | Recovery request: states, idempotency, rate limit, notify, old-device preference, post-recovery delay, revoke sessions | ATRS-3, ATRS-8 |
| FR-012 | Dispute case: types, evidence items, decisions, maker-checker, export | ATRS-9 |
| FR-013 | Loyalty/wallet evidence integration, ledger là source, dispute→adjustment request | ATRS-9 |
| FR-014 | Admin/staff review RBAC, maker-checker, audit mọi access | ATRS-10 |
| FR-015 | User transparency: devices, last-seen, lost/revoke, case status, security notifications, consent withdraw | ATRS-11, ATRS-12, ATRS-13 |
| §6.1-6.5 | Flows: onboarding, login registered device, new device + old, lost device recovery, dispute | ATRS-8/9/11/12 |
| §7 | Data model 8 bảng | ATRS-2 (foundation), ATRS-4/5/6/8/9 (các bảng chuyên) |
| §8 + §8.1 | API contract + security (token, ownership, CSRF, CORS, rate limit, idempotency, correlation) | Tất cả backend cards; convention theo `InternalApiKey`/JWT hiện có |
| §9 | Evidence classification + R0-R4 + anti-abuse | ATRS-7, ATRS-8 |
| NFR-001..006 | Security/Privacy/Availability/Performance/Scalability/Observability | ATRS-14 + rải trong cards |
| §11 | Privacy/legal: consent, retention, DPIA, controller/processor | ATRS-1, ATRS-6, ATRS-14 |
| §12 | Ecosystem integration + per-client scope | ATRS-1, ATRS-10 |
| §13 | Phân quyền đề xuất (bảng roles) | ATRS-10 |
| §14 | AC-001..009 | Map trong từng card + tổng hợp ATRS-15 |
| §15 | Test plan unit/integration/security/E2E 12 scenarios | Rải trong cards + ATRS-14 |
| §17 | Devin rules + 12 deliverables | ATRS-1 (gap analysis ✓, ADR), mỗi card cập nhật project_state |
| §18 | 10 open decisions PO | §3 D1-D10 (bổ sung Q1-Q10 chi tiết trong card ATRS-1) |
| §19 | DoD | ATRS-15 |

### UI/UX SRS

| Req | Card |
|---|---|
| U01-U08 (onboarding, profile, passkey, secret challenge, recovery codes, security center, device registry/detail) | ATRS-11 |
| U09-U13 (recovery wizard, status, dispute center/submit/detail) | ATRS-12 |
| A14-A22 (dashboard, recovery queue/review, dispute queue/investigation/decision, audit explorer, policy, roles) | ATRS-10 |
| F01-F07 flows | Backend cards + UI cards tương ứng |
| Status vocabulary mapping (§9.1) | ATRS-1 (bảng mapping duy nhất trong SRS revision) |
| Global interaction (§10: responsive, forms, states, notifications, a11y) | Rải trong ATRS-11/12 + UI.Platform conventions |
| §11 data/API expectations + no fabricated data | Tất cả UI cards |
| §12 security/privacy 15 items | ATRS-14 + rải |
| §13 error/edge cases (24 items) | Rải trong cards + ATRS-14 test matrix |
| §14 acceptance criteria | ATRS-15 tổng hợp |
| §15 Playwright 24 scenarios | ATRS-11/12 (per-screen) + ATRS-14 (full sweep) |
| §16 phases | Map 1-1 sang ATRS-11/12/10 |
| §17 Devin instructions + gap table + prohibited shortcuts | Tuân thủ trong mọi card |
| §18 open decisions 12 items | Gộp vào §3 D1-D10 + card ATRS-1 checklist |

## 6. RỦI RO LỚN NHẤT

1. **Identity fragmentation** (D1) — nếu chốt sai canonical key, toàn bộ schema sai. → ATRS-1 phải xong trước mọi schema card.
2. **Token revocation** (D3) — không có thì recovery "đá" attacker không được; JWT 8h + customer token 30d là cửa sổ tấn công lớn.
3. **Phone PII encrypted → lookup in-memory** — recovery-by-contact cần HMAC lookup key (card ATRS-2 hoặc ID-P3).
4. **Scope creep**: SRS rất lớn (22 màn hình + 15 FR). Cards được cắt theo phase; U04 omit giúp giảm ~1 màn hình + 1 module.
5. **Đụng identity initiative đang chạy** — coordination bắt buộc, không song song sửa `Customer`/`DemoUser`.

## 7. SUCCESS CRITERIA (tổng)

1. Một khách mất điện thoại → recovery được qua passkey/recovery-code thiết bị khác, hoặc manual review có audit đầy đủ.
2. Dispute trừ điểm → điều chỉnh qua `AdjustPointsAsync` có maker-checker + resolution reference, ledger không bị sửa trực tiếp.
3. Không màn hình nào tuyên bố "Device ID = đã xác minh danh tính".
4. Mọi FR-001..015 có test tương ứng; AC-001..009 pass; Playwright scenarios §15 (24) pass trên prod RV.
5. `guard-check.ps1` + `dotnet build VanAn.sln` PASS mọi card.

---

*Plan created 2026-11-15 — chờ review. Không code nào được viết; mọi card ở trạng thái DRAFT chờ duyệt.*
