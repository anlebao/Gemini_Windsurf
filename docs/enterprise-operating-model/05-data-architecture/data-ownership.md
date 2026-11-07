# Data Ownership & Classification — P3

> SRS §8.2. † = legal ownership needs owner confirmation (Q4) — not inferred from software.

| Class | Examples | Owner | Access | Sensitivity |
|---|---|---|---|---|
| Tenant/identity | Tenant (DATA-001), users, roles | ORG-001 platform † + tenant | tenant-scoped + ops | identity |
| Infra/routing | ShopInstance (DATA-002) | ORG-001 | ops only | internal |
| Orders/transactions | DATA-004/005 | tenant † | tenant + customer(own) | financial-adjacent |
| Products/catalog | DATA-006 | tenant | tenant + public browse | business |
| Accounting/tax | AccountingEntry DATA-009 — **immutable (CTRL-001)** | tenant † | accountant roles; audit | **financial — regulated** |
| Onboarding/KYC | DATA-007/012 | platform † | ops + owner | **personal data — sensitive** |
| Wallets/usage | DATA-010 | platform (metering) | tenant view + ops | financial |
| Loyalty points | loyalty balances | platform+tenant † | customer + tenant | soft-financial |
| AI knowledge | DATA-011 | **tenant** (approved content) | tenant-scoped retrieval; Public/Confidential classes | tenant-confidential |
| Operational logs | sync/audit logs | platform | ops | internal |
| System config | feature settings, rate cards | platform+tenant | admin | internal |

## Rules

- No real production records in docs (NFR-07). Classifications above are schema-level.
- Sensitive classes (KYC docs, accounting, wallets): access notes must stay at role level, never list real PII.
- AI knowledge is the only data class owned by tenant but processed through an external LLM — data-flow diagrams must show the Gemini leg (§8.4).
