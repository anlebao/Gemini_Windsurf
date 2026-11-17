# ATRS Data Model (ERD overview) — vanan-atrs

> **Created:** 2026-11-17 (ATRS-15 §17.4 deliverable) · PG database `vanan_atrs` (snake_case, EFCore.NamingConventions)
> Đầy đủ schema: migrations trong `11_Atrs/vanan-atrs/src/VanAn.Atrs.Infra/Migrations/` (21 tables).

## Entities & relationships

```
TrustSubject (canonical identity, account-level)
├── TrustSubjectLink (1..*: Customer | DemoUser | DeviceCluster + TenantId)
├── AccountTrustProfile (1:1 — status, assurance, xmin concurrency)
├── AccountSecurityEvent (0..* — append-only audit, sanitized payloads)
├── ConsentRecord (0..*)
├── RecoveryCodeSet (0..*) ── RecoveryCode (1..*)
├── RecoveryRequest (0..*) ── RecoveryCaseNote (1..*)
├── UserDevice (0..*) ── DeviceCredential (1..*)
├── WebAuthnChallenge (0..*)
├── DisputeCase (0..*) ── DisputeEvidenceItem (1..*) · DisputeDecision (1..*) · EvidenceAttachment (0..*)
├── PersonalEvidenceItem (0..*)
└── AccountNotification (0..* — ATRS-13 in-app inbox)

StaffPermissionGrant (cross-tenant registry — DemoUserId ↔ permission ↔ scope/expiry)
SecurityPolicy (versioned policy: EFFECTIVE/PROPOSED/REJECTED/SUPERSEDED — tự là audit trail)
```

## Key columns / rules

| Table | Ghi chú |
|---|---|
| `trust_subjects` | PrimaryContactHash (64), unique `(trust_subject_id, identity_type, identity_id)` trên links |
| `account_trust_profiles` | unique subject; `xmin` = concurrency token (system column) |
| `account_security_events` | append-only, KHÔNG update/delete; index (subject, occurred) + (event_type, occurred) + correlation |
| `recovery_requests` | state machine 9 status; unique idempotency_key; index (subject, status); assigned_reviewer_id/assigned_at (W5a) |
| `recovery_case_notes` | append-only internal notes (KHÔNG lộ user) |
| `dispute_cases` | unique idempotency; index (subject,status) + (status,submitted) + tenant |
| `dispute_decisions` | append-only maker-checker (proposed_by/approved_by); unique idempotency |
| `staff_permission_grants` | fail-closed scope (scope_tenant_id NULL = mọi tenant); IsRevoked/ExpiresAt |
| `security_policies` | unique (policy_key, version); index (policy_key, status) |
| `account_notifications` | append-only; index (subject, created); ReadAt |

## Migration inventory (rollback = Down())

1. `20261115124416_InitialAtrsSchema`
2. `20261115131733_AddRecoveryCodesAndRequests`
3. `20261116023237_FixRowVersionXmin` + `20261116024122_FixRowVersionUint`
4. `20261116054254_AddWebAuthnDevices`
5. `20261116115723_DisputeCases_StaffGrants`
6. `20261117011345_AddPersonalEvidenceAndAttachments`
7. `20261117052601_AddRecoveryStaffOps` (W5a)
8. `20261117085410_AddSecurityPolicies` (W5c)
9. `20261117093229_AddAccountNotifications` (W5d)

Tất cả additive (CreateTable/AddColumn) — rollback an toàn từng bước.