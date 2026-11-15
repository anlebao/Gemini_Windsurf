# Task ATRS-0: Phase 0 Discovery — Codebase Audit & Gap Analysis

> **Master plan:** `docs/AI/plans/account-trust-recovery-master-plan.md`
> **Status:** ✅ **DONE 2026-11-15** — không implement, chỉ audit.

## Deliverable

`docs/requirements/BASE_CODE_GAP_ANALYSIS_ATRS.md` — audit read-only toàn bộ subsystem liên quan.

## Kết quả chính

- ~40% nền tái dùng được: `DeviceRegistration`, `ILoyaltyPointLedgerService`/`AdjustPointsAsync`, `AuditLog` append-only, `INotificationService`, outbox/NATS, `IdentityLevel`/`UserTrustLevel`/`VerifiedChannel`, MFA TOTP fields, `LoyaltyReconcileCase` precedent.
- 10 open decisions D1-D10 → đưa vào master plan §3 + card ATRS-1.
- WebAuthn = 0; token revocation = 0; `RecoveryRequest`/`DisputeCase` = 0.

## Gate

Phase 0 hoàn tất đúng SRS §17.1 — được phép sang ANALYZE→IMPLEMENT sau khi ATRS-1 chốt decisions.
