# Actor Registry — Vạn An Ecosystem

> Data source: `registries/actors.yaml` (single source). Phase 1 · captured at `64789992`.
> Actor types per SRS §4.1: HUMAN · SOFTWARE · AI_AGENT · EXTERNAL · HYBRID.

## Summary (28 actors)

### HUMAN (16)

**Inside tenant shops** (ORG-005, roles from `UserRole.cs`@64789992 — VERIFIED):
`ACTR-002` Shop Owner · `ACTR-003` StoreKeeper · `ACTR-004` Staff · `ACTR-005` Masterchef · `ACTR-006` ChiefAccountant · `ACTR-007` DebtAccountant · `ACTR-008` Guard

**Ecosystem actors:** `ACTR-001` Customer · `ACTR-009` CTV/Collaborator · `ACTR-010` Salesperson (I) · `ACTR-011` Shipper (I) · `ACTR-012` Hạt Nhân rep (I) · `ACTR-013` HTX Official Member

**Platform staff:** `ACTR-014` Vạn An Ecommerce operator · `ACTR-015` Vạn An Technology operator

**External human:** `ACTR-016` Government/tax officer (I)

### SOFTWARE (5)
`ACTR-020` Gateway · `ACTR-021` ShopERP · `ACTR-022` Sync workers/subscribers · `ACTR-023` vanan-metering · `ACTR-024` Crawler

### AI_AGENT (2) — §7.4 boundary applies
`ACTR-030` vanan-ai tenant assistant (advisory, tenant-scoped, never creates orders) · `ACTR-031` SmartEntry LLM parser (fail-closed parse enhancement)

### EXTERNAL (5)
`ACTR-040` Gemini API · `ACTR-041` Payment provider · `ACTR-042` E-invoice provider · `ACTR-043` Zalo · `ACTR-044` Cloudinary/S3

## Discovery notes

- Shop roles come from `UserRole` enum — real code, not assumed (Owner/StoreKeeper/Guard/Staff/Masterchef/ChiefAccountant/DebtAccountant).
- HTX membership types exist in code: OfficialMember, LinkedCapitalMember, LinkedNonCapitalMember (`MembershipTypes.cs`) — actors for member variants deferred to F4 detail (P2a).
- `ApplicationStatus` (Draft→Submitted→NeedInfo→Approved/Rejected) + `IdentityVerificationLevel` (L1 OTP → L4 manual) found — verification escalation is a real modeled flow → feeds P2a.
- No HYBRID-type actor needed at P1 — human-in-the-loop appears at activity level (P2), not actor level.
- Open: crawler business role (Q5), per-HTX officer titles (owner input), Zalo integration status (I — SRS doc only, not code-verified).
