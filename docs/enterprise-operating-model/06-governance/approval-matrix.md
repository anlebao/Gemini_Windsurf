# Approval Matrix — Critical Processes

> SRS §10: who approves what. † = legal accountability needs owner confirm (Q4). `64789992`.

| Process | Gate | Approver | Evidence |
|---|---|---|---|
| Membership application | Submitted→Approved | Vạn An Ecommerce op (ACTR-014) † | `ApplicationStatus` + `MembershipAdminController` |
| KYC L4 verification | L3→L4 pass | Hạt Nhân / delegated reviewer † | `IdentityVerificationLevel` |
| Tenant ShopInstance assign | capacity gate | automated (RULE-003) — exception → ops | `AssignShopInstanceAsync` |
| Tenant suspend/reactivate | status change | platform ops † | `TenantManagementService` |
| MarkAsHtx | HTX marker | platform ops / backfill † | `HtxProfileService` |
| Order confirm/reject | merchant gate | Shop Owner (ACTR-002) | ShopERP flow (I) |
| Voucher posting | per voucher type | ChiefAccountant † (per type) | voucher framework (X1 detail) |
| Metering deny | quota gate | automated (CTRL-003) | metering service |
| AI knowledge publish | pending_review→approved | tenant owner + operator tooling (CTRL-004) | AI SRS |
| AI degrade/refuse | quota/policy | automated policy | AI SRS |

## Boundary notes

- Merchant-side approvals (order confirm, voucher posting) never leave the tenant — platform ops has no approval role inside tenant business records (except accounting admin functions TBD).
- Platform-side approvals (application, KYC L4, suspend, MarkAsHtx, AI knowledge) are cross-tenant ops — audit trail required (see audit-controls.md).
- Legal approver for HTX admission (HTX legal act vs platform flag) — **Q4 owner confirm**, not inferred from MarkAsHtx.
