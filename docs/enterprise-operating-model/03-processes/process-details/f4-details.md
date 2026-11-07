# F4 Process Details — Track A

> Metadata lives in `registries/activities.yaml` (18-field per SRS §5.2). This doc adds narrative context + evidence notes. Captured at `64789992`.

## PROC-101 Merchant application & approval (CORE)

Real state machine in code: `ApplicationStatus` Draft→Submitted→NeedInfo→Approved|Rejected (`MembershipTypes.cs`). Controllers: `MembershipApplicationsController`, `MembershipAdminController`, `OcrOnboardingController`, `OnboardingController`.

**Execution split (this workflow):** MANUAL ACTV-101/102/104/106 + AUTOMATED ACTV-103/105 + downstream AUTO — no HYBRID (decompose-first: review decision = MANUAL even though system presents the app).

## PROC-104 KYC escalation (CONFIGURABLE)

`IdentityVerificationLevel`: L1 OTP (AUTOMATED) → L2 personal info → L3 KYC → L4 manual (MANUAL, Hạt Nhân per membership SRS — I). Escalation is config-driven per tenant type.

## PROC-102 Tenant provisioning (CORE)

Verified mechanics (`TenantManagementService.cs`): `CreateTenantAsync` → `AssignShopInstanceAsync` (MaxTenants capacity check → `InvalidOperationException` when full) → publish `vanan.cloud.tenant.verified` → `TenantSyncSubscriber` upserts SQLite mirror Pending→Active. Suspend/Reactivate/Deactivate + `SetAccountingStandard` + `ChangeBusinessType` verified. KhachLink/domain binding via `KhachLinkInstanceController`/`DomainRegistrarController` (I).

## PROC-103 HTX activation (CONFIGURABLE)

`MarkAsHtx()` idempotent + audited; `GetOrCreateAsync` hook in same tx; startup backfill in Gateway; publishes `vanan.cloud.tenant.type.changed` → SQLite mirror. `AccountingStandardResolver` picks TT71 for HTX (RULE-004).

## Human-side gaps (feed P4)

- Sales acquisition + contract steps doc-thin → `unresolved-questions.md` Q3.
- Approval authority for tenant onboarding (ACTV-104) assigned to Vạn An Ecommerce operator — actor confidence I; owner confirm Q4.
