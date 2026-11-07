# Glossary & ID Conventions — Vạn An Enterprise Operating Model

> SRS v2.1 §4.1, §5, §6.5, §6.6, §7.4, §11.1. Single reference for terms used across the twin.

## Execution types (per atomic activity — §5.1)

| Term | Definition |
|---|---|
| MANUAL | One human performs one action |
| AUTOMATED | One system performs one action, no human in that execution |
| HYBRID | ONE atomic step co-produced by inseparable human+software interaction (rare — decompose first) |

## Actor types (§4.1)

| Type | Meaning | ID |
|---|---|---|
| HUMAN | Individual performing an activity | `ACTR-` |
| ORGANIZATION | Legal entity/cooperative — **organizations register ONLY in `organizations.yaml` (`ORG-`), never dual-registered** (§4.4) | `ORG-` |
| SOFTWARE | Application, service, module, automated agent | `ACTR-` |
| AI AGENT | SOFTWARE subtype with probabilistic/advisory output — carries §7.4 boundary attributes | `ACTR-` (type=AI_AGENT) |
| EXTERNAL SYSTEM | Third-party or government platform | `ACTR-` |
| HYBRID | Combined human/software operational role | `ACTR-` |

## Confidence scale (§5.2)

| Level | Meaning |
|---|---|
| VERIFIED | Confirmed against executable code, schema, API surface or test |
| INFERRED | Strongly indicated by docs/naming, not directly verified in code |
| ASSUMED | Business expectation — requires owner confirmation |

## Critical process (§6.5) — ANY of:

1. Creates/modifies/reverses financial or accounting records.
2. Crosses a tenant or organization boundary.
3. Crosses a deployment boundary (multi-VPS, service-to-service).
4. Requires approval, produces audit evidence, or is reversible.
5. Gates revenue (metering deny/allow) or exposes tenant data to AI retrieval.
6. Failure creates regulatory, tax or data-loss exposure.

## Process variability (§6.6)

| Class | Meaning |
|---|---|
| CORE | Mandatory standard — identical for every HTX/tenant |
| CONFIGURABLE | Same process, behavior varied by configuration (e.g., TenantFeatureSettings) |
| VARIANT | Differs by business type/HTX model — MUST have `parentRef` to a CORE process, documents only deltas |

## ID scheme (§11.1) — stable, never renumbered; retired items get status RETIRED, never reuse

`ACTR` actor · `ORG` organization · `CAP` capability · `PROC` process · `ACTV` activity · `SYS` software component · `API` api/integration · `DATA` data entity · `RULE` business rule · `CTRL` control · `RISK` risk · `KPI` kpi · `GAP` gap

## Status values

`ACTIVE` (verified/current) · `PROPOSED` (TO-BE awaiting owner sign-off) · `RETIRED`

## Data terms

- **System of record (SoT):** authoritative store for a data item — Gateway PG (Orders/Accounting/Tenants/ShopInstances/Users/FeaturedProducts), ShopERP SQLite (Products/POS ops), metering DB (wallets/usage), vanan-ai PG (knowledge).
- **Replica/cache:** derived copy (e.g., ShopERP order replica for kitchen/POS).

## AI boundary (§7.4)

- **Advisory vs executing:** AI suggests into existing flows; it never creates orders/journal entries/business records.
- **Authoritative source:** structured tenant data beats vector-retrieved knowledge on conflict.
- **Tenant isolation:** retrieval scoped by signed tenant context.
- **Approval gate:** knowledge `pending_review → approved` before public retrieval.
- **Degrade:** metering deny/fail → configured degrade/refuse path.

## Evidence format (§9.1)

`path@commit-sha` (preferred) · `path#L<n>` (decays, note it) · `none:<reason>`. Entries record capture-commit; unverifiable → `STALE`.

## Strategic frame (owner directive 2026-11-07)

| Term | Meaning |
|---|---|
| **Cộng đồng kinh tế địa phương (VALCN)** | THE GOAL — local economic community: community commerce, community fund, shared local economy (CAP-023) |
| **HTX (Hợp tác xã)** | THE VEHICLE — legal wrapper/cooperative structure to organize the community economy (CAP-015); NOT the goal itself |
| **HTX in a Box** | replication kit: standard processes + tenant config to onboard an HTX as the vehicle |
