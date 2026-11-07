# Business Capability Map — Vạn An Ecosystem

> Data source: `registries/capabilities.yaml`. SRS §2.2 (21 groups) + 1 discovered (CAP-022 KYC).
> Phase 1 · captured at `64789992`.

## Map by L1 domain (PROC-)

| L1 Domain | Capabilities | Coverage |
|---|---|---|
| PROC-010 Membership & Onboarding | CAP-001 merchant onboarding · CAP-002 HTX governance · CAP-022 KYC verification | F4 track |
| PROC-020 Commerce & Orders | CAP-007 local discovery · CAP-008 orders · CAP-009 booking · CAP-010 sales/delivery collab · CAP-012 credits · CAP-014 marketplace (ASSUMED) | F1 track |
| PROC-030 Finance & Accounting | CAP-004 accounting/tax · CAP-013 procurement · CAP-017 metering/billing | X1 |
| PROC-040 Customer Engagement | CAP-005 CRM · CAP-011 loyalty · CAP-015 notifications | X2 |
| PROC-050 AI & Intelligence | CAP-021 tenant AI | X3 |
| PROC-060 Platform Ops & Governance | CAP-003 ShopERP ops · CAP-016 reporting · CAP-018 identity/security · CAP-019 sync infra · CAP-020 HTX ops · CAP-006 customer apps | backbone |

## Confidence notes

- **ASSUMED:** CAP-014 marketplace — no code evidence this pass; candidate for gap register.
- **INFERRED:** CAP-005 CRM (partial), CAP-010 delivery, CAP-012 credits, CAP-013 procurement, CAP-020 HTX ops — need owner priority at expansion (Q6).
- All others have code-level evidence in `capabilities.yaml`.
- CAP-022 added: `MembershipTypes.cs` shows real verification levels (L1–L4) — a capability not in the original 21 but present in code.
