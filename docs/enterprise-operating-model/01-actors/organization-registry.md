# Organization Registry — Vạn An Ecosystem

> Data source: `registries/organizations.yaml` (single source — do not restate fields here).
> SRS §4.4: organizations register ONLY here (`ORG-`), never dual-registered as `ACTR-`.
> Phase 1 · captured at `64789992`.

## Structure

| ORG | Organization | Type | Note |
|---|---|---|---|
| ORG-001 | Vạn An Technology | TECH_PROVIDER | IP + platform software owner |
| ORG-002 | Vạn An Ecommerce | PLATFORM_OPERATOR | Business operator — onboarding, ops, billing |
| ORG-003 | HTX (collective) | COOPERATIVE | `TenantType.HTX=5`, HtxProfile, TT71 regime |
| ORG-004 | HTX Hạt Nhân | COOPERATIVE | Local nucleus — child of ORG-003 |
| ORG-005 | Merchants (collective) | LEGAL | Per-tenant shops — HKD/Enterprise types |
| ORG-006 | Suppliers | EXTERNAL | — |
| ORG-007 | Government & regulators | EXTERNAL | Tax authority (e-invoice path exists in code) |
| ORG-008 | Payment providers | EXTERNAL | VietQR/bank rails |
| ORG-009 | LLM provider (Gemini) | EXTERNAL | SmartEntry + vanan-ai |
| ORG-010 | Media/CDN (Cloudinary, S3) | EXTERNAL | — |
| ORG-011 | Investors & partners | EXTERNAL | ASSUMED — owner confirm |

## Key boundary notes (SRS §4.3 / §10)

- **Legal responsibility ≠ workflow participation.** Vạn An Ecommerce operating onboarding does NOT make it the legally responsible party for merchant transactions — that boundary is a business-owner question (see `11-validation/unresolved-questions.md` Q4).
- HTX and merchants are *collective* registry entries: they represent classes of orgs (each real HTX/shop is a tenant instance of that class). Per-tenant instances do NOT get separate ORG- IDs.
- ORG-004 Hạt Nhân is modeled as a child org of HTX per membership SRS — INFERRED confidence, business owner confirm.
