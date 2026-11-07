# VAN AN ECOSYSTEM
# ENTERPRISE OPERATING MODEL & DIGITAL TWIN
## SOFTWARE REQUIREMENTS SPECIFICATION – SRS v2.0

**Document Type:** System Analysis & Documentation SRS
**Project:** Vạn An Ecosystem
**Version:** 2.1 (supersedes v2.0 — external review consolidation)
**Primary Executor:** Devin
**Language:** English for technical artifacts and Mermaid diagrams; Vietnamese may be used for business descriptions where appropriate.
**Implementation Principle:** Evidence-based, non-invasive, documentation-first.

## 0. CHANGELOG v1.0 → v2.0

| # | Change | Rationale |
|---|---|---|
| 0.1 | Strategic rationale added (§1.3): standardize operating model before scaling to ~3,000 HTX | Business context was implicit; needed to explain why this project precedes further module expansion |
| 0.2 | "Critical process" formally defined (§6.5) | AC-03/04/05/06/10 were unverifiable without it |
| 0.3 | Evidence reference format specified (§9.1): `path@commit` + optional `#L`, freshness rule | Line-number citations rot silently |
| 0.4 | Confidence scale defined: VERIFIED / INFERRED / ASSUMED (§5.2) | Field existed without a scale |
| 0.5 | ID scheme fixed: `ACTR-` for actors (was `ACT-`, collided with `ACTV-` Activity); added `RISK-`, `KPI-` (§11.1) | Registry list had 12 items but only 11 prefixes |
| 0.6 | Organization registration rule (§4.4): ORG entities get `ORG-` only, never dual-registered as `ACTR-` | Prevented traceability fork |
| 0.7 | HYBRID re-scoped to atomic activities (§5.1) | v1.0 example described a whole process, contradicting "exactly one classification" |
| 0.8 | KPI added to activity metadata (§5.2) | Aligns with Digital Twin vision — measure efficiency per activity |
| 0.9 | Vạn An AI (`vanan-ai` / Tenant Intelligence Platform) added: capability #21 (§2.2), software inventory (§7.1), AI execution boundary (§7.4), AI governance (§10.2), AI data rules (§8.4) | `9_TenantAI/` is an approved, in-build subsystem that the twin must model |
| 0.10 | Verified base-code inventory seeded (§2.3, §7.1) | Phase 0 starts from verified facts, not a blank page |
| 0.11 | MVP coverage slice defined (§3.1): critical flows ordered by priority | AC-01 "all inventoried" was unbounded |
| 0.12 | Phase gate added after Phase 4; TO-BE requires owner sign-off (§14, §16) | Recommendations are not approved business intent |
| 0.13 | Phase 0 deduplication mandate: twin is an index/traceability layer over existing docs (§14, §16) | Repo already has extensive documentation; duplication diverges |
| 0.14 | Twin maintenance triggers defined (§12.1) | Model must stay synchronized with code or it decays within weeks |
| 0.15 | Mermaid swimlane convention fixed (§6.2, §6.4) | Mermaid has no native swimlanes; consistency requires a rule |
| 0.16 | D-ID ↔ artifact mapping table added (§17) | §3 deliverables and §17 artifacts were unlinked |

## 0.1. CHANGELOG v2.0 → v2.1 (external review consolidation)

| # | Review item | Verdict | Change |
|---|---|---|---|
| 0.1.1 | HYBRID examples contradicted the atomic-activity rule ("detect → review" is two activities) | **ACCEPT** | §5.1 rewritten: HYBRID = single atomic step with inseparable human+software interaction; decompose-first rule; true examples (Smart Entry, live review console); anomaly/tax-report cases reclassified as AUTOMATED+MANUAL pairs |
| 0.1.2 | sequenceDiagram was wrongly "preferred" over swimlanes — different questions: sequence = interaction over time, swimlane = who does what | **ACCEPT** | §6.2 rewritten: swimlane flowchart (subgraph lanes) mandatory for critical business processes; sequenceDiagram reserved for technical interaction flows; critical flows may need both |
| 0.1.3 | "Verified" anchor table risks confirmation bias — label primes re-confirmation over independent verification | **ACCEPT** | §2.3 reframed as re-verification checklist captured at commit `64789992` (2026-11-07); per-component verification status (VERIFIED/DRIFTED/UNVERIFIED) + coverage status mandatory; anchors never substitute Phase 0 output |
| 0.1.4 | Maintenance triggers too rigid — every internal change would drag doc churn | **ACCEPT + linked to 0.1.5** | §12.1 split into Mandatory (business-visible behavior) vs Conditional (internal implementation → registry touch-up only). Automated check scope stated honestly: link integrity + coverage flags, NOT semantic drift |
| 0.1.5 | Registries in pure Markdown cannot be queried, validated or diffed at scale | **ACCEPT — strongest critique** | §11.3 added: registries stored as **YAML** (Git-diff friendly, supports comments) + JSON Schema per registry + validator script (ID format, required fields, referential integrity, enums, evidence format). Markdown = narrative, Mermaid = visuals, YAML = data |
| 0.1.6 | No distinction between standard process and per-HTX variants — blocks "HTX in a Box" | **ACCEPT — genuine gap** | §6.6 added: CORE / CONFIGURABLE / VARIANT process classes; VARIANT references parent CORE + declares deltas; CONFIGURABLE maps to existing `TenantFeatureSettings` mechanism |
| 0.1.7 | MVP order F1→F2→F3 optimized technical risk, not replication strategy | **ACCEPT with recorded caveat** | §3.1 reordered F4→F1→F2+F3→F6→F5→F7+. Caveat documented: F4 is least-implemented → expect highest ASSUMED/INFERRED share; that is discovery value, not failure |

---

# 1. PROJECT OBJECTIVE

## 1.1. Overall Objective

Build a comprehensive, traceable operating model of the entire Vạn An Ecosystem — the **Vạn An Operating Model** — as a version-controlled documentation layer maintained alongside the codebase.

The model must represent:

1. All business entities, stakeholders, actors and organizational roles.
2. All business activities and operational workflows.
3. The distinction between human-executed, software-executed and hybrid (human-in-the-loop) activities.
4. All software modules, services, integrations and infrastructure involved in each workflow.
5. Data creation, transformation, storage, synchronization and consumption.
6. Business rules, authorization, approvals, exceptions and control mechanisms.
7. The current operational state (AS-IS).
8. The intended future operational state (TO-BE).
9. Gaps between business requirements and existing software implementation (AUTOMATION GAP).
10. Opportunities to automate manual activities.

The resulting model must become the central reference for understanding, maintaining, auditing and scaling the Vạn An Ecosystem.

## 1.2. Important Scope Clarification

This project is NOT a request to build another business application or replace existing software modules.

It is a request to create an evidence-based Enterprise Operating Model and Digital Twin documentation layer.

The Digital Twin in this SRS means a structured, traceable digital representation of business operations and software implementation.

It does NOT require a real-time simulation engine or a new runtime platform.

## 1.3. Strategic Rationale

Vạn An targets a multi-HTX ecosystem (on the order of thousands of cooperatives). Scaling manual operating know-how per HTX does not work; the operating model itself must become a deployable standard.

The twin serves six concrete purposes:

1. **Operational control** — know exactly who is responsible at every step.
2. **Automation targeting** — see which manual work can move to software.
3. **Cost control** — identify which steps require staff and which do not.
4. **Risk control** — find missing approvals, missing controls, fraud exposure.
5. **Software development** — trace every activity back to module, API, database and business rule.
6. **HTX replication** — turn the operating model into a standard kit deployable to new cooperatives without redesign.

This documentation should be built **before** further module expansion, so that growth is modeled rather than improvised.

---

# 2. SYSTEM CONTEXT

Devin must investigate the actual repository and identify all existing components.

The following are known business and technical concepts that must be investigated, not assumed to be fully implemented.

## 2.1. Business Ecosystem

Potential stakeholders:

- Vạn An Technology / technology and intellectual property owner.
- Vạn An Ecommerce / ecosystem business operator.
- HTX / cooperative organizations.
- HTX Hạt Nhân / local ecosystem representatives.
- Shop owners / merchants.
- Shop employees.
- Customers / buyers.
- Collaborators (CTV).
- Salespeople and delivery personnel.
- Suppliers and service providers.
- Investors and financial partners.
- Government and regulatory authorities.

Devin must identify additional actors discovered in the actual system.

## 2.2. Business Capabilities

Investigate the following capability groups:

1. Merchant onboarding and management.
2. HTX establishment, governance and membership.
3. ShopERP and business operations.
4. Accounting and tax-related operations (voucher framework, HKD books, period closing).
5. Customer management and CRM.
6. KhachLink and customer applications.
7. Local discovery and commerce.
8. Order management and transaction processing.
9. Booking and appointment management.
10. Sales and delivery collaboration.
11. Loyalty points and promotional benefits.
12. Vạn An service credits.
13. Supplier and procurement management.
14. Marketplace and service marketplace.
15. Notifications and communication.
16. Reporting and analytics.
17. SaaS subscription and service billing (metering "phí nền tảng").
18. User identity, permissions and security.
19. Data synchronization and infrastructure.
20. HTX financial and operational management.
21. **AI assistance and tenant intelligence** (`vanan-ai` — per-tenant advisory assistants; see §7.4).

These are investigation categories, not confirmation that all capabilities currently exist.

## 2.3. Anchored Starting Points (base-code claims — re-verification required)

**Capture:** commit `64789992`, 2026-11-07.

The following claims were verified against the repository at capture time. They are a **re-verification checklist, not survey results** — the "verified" label must not bias Phase 0 toward confirmation. Devin must independently verify each claim, assign each component its own verification status (`VERIFIED` / `DRIFTED` / `UNVERIFIED`) and coverage status (`NOT-SURVEYED` / `PARTIAL` / `COVERED`), and record any drift in the evidence register. A verified table row does NOT imply the whole component or its data flows are verified.

| Component | Repository path | Verified facts (starting evidence) |
|---|---|---|
| Solution root | `VanAn.sln` | Single solution for core ecosystem |
| Shared domain | `1_Shared/Domain.cs` | Single source of truth for domain entities; multi-tenant `BaseEntity`; single-identity pattern (`Id == BusinessKey.Value`) |
| Gateway | `2_Gateway/` | YARP reverse proxy + **Order Creator** (Option C, approved 2026-07-18); PostgreSQL is source of truth for Orders, Accounting, Tenants, ShopInstances, Users, FeaturedProducts |
| CoreHub | `3_CoreHub/` | Pure class library (no exe host); business services incl. voucher engine, `LlmEnhanceVoucherTextParseService` (Gemini, fail-closed), `SemanticSearch` interfaces |
| ShopERP | `5_WebApps/ShopERP` | Main Web API host; per-tenant SQLite; Products live here (NOT in Gateway PG); POS/kitchen display |
| KhachLink | `5_WebApps/KhachLink` | Customer-facing PWA; MUST NOT touch local DBs; HTTP via Gateway only |
| Metering | `8_Metering/vanan-metering` | Standalone service (own sln, own VPS): wallet/quota/deny gate, `X-Caller-Id`+`X-Metering-Key`, `Idempotency-Key`, `IMeteringClient` |
| Tenant AI | `9_TenantAI/vanan-ai` | Standalone service per `van_an_tenant_ai_srs_v1.md` v1.1: PostgreSQL + pgvector, Gemini LLM/embedding, tenant knowledge isolation, gated build (P1-G1…G5) |
| UI Platform | `UI.Platform/` | Shared component library; UI bypass is a Hard Stop violation |
| Mobile apps | `4_MobileApps/` | Investigate actual apps present |
| Accounting | `VanAn.Accounting/` | Investigate boundary vs `3_CoreHub` accounting services |
| Testing | `6_Testing/`, `6_Tests/` | Unit/integration/E2E (Playwright under `6_Testing/e2e-tests/`) |
| Messaging | NATS | Order delivery: subject `vanan.cloud.order.created.{shopInstanceId}`, routed by ShopInstanceId (multi-VPS) |
| Data stores | PostgreSQL + per-tenant SQLite + pgvector | PG = ecosystem source of truth; SQLite = per-tenant operational store (ShopERP); pgvector = AI knowledge (vanan-ai only) |
| Deployment | `nginx/`, `scripts/`, Dockerfiles | Multi-VPS via `ShopInstances` routing table; metering + vanan-ai on separate VPS |

**Key verified data flows (baseline for AS-IS):**

- Order creation: `KhachLink → Gateway (creates order in PG, client supplies ProductName + VatRate snapshot) → NATS vanan.cloud.order.created.{shopInstanceId} → ShopERP` (kitchen/POS replica).
- Order status: `ShopERP SQLite → PG` (reverse direction, for status updates).
- Metering: callers → `vanan-metering` quota/deny check → metered event ingest (`ai.chat.message`, etc.).
- Catalog browse: `KhachLink → GET /api/catalog/recommended` (pure PG) and YARP-forwarded browse to ShopERP.

Discrepancies between this table and live code are **expected and valuable** — record them in the evidence register as drift entries, never silently rewrite the anchor.

---

# 3. SCOPE OF WORK

The project consists of six deliverables.

| ID | Deliverable | Description |
|---|---|---|
| D01 | Ecosystem Inventory | Complete inventory of business entities, actors and software components |
| D02 | AS-IS Operating Model | Actual current workflows and responsibilities |
| D03 | TO-BE Operating Model | Target workflows and automation model (owner-approved) |
| D04 | System & Data Architecture | Software components, integrations and data flows |
| D05 | Gap Analysis | Manual work, missing functions, duplicated logic and risks |
| D06 | Digital Twin Documentation | Structured, version-controlled operating model with traceability |

All deliverables must be linked through stable identifiers. Artifact-level mapping is in §17.

## 3.1. MVP Coverage Slice (execution order)

AC-01 does not mean "document everything at once." Coverage proceeds in priority order; each phase gate states which slice items were covered.

Ordering follows the **replication strategy** (the twin exists to standardize operations for HTX expansion), not merely technical risk:

| Priority | Critical flow | Why |
|---|---|---|
| **F4** | Merchant onboarding → HTX formation → tenant provisioning → ShopInstance routing → feature activation | The replication envelope — everything else plugs into it |
| **F1** | Order lifecycle: KhachLink checkout → Gateway PG → NATS → ShopERP → status sync back | Transaction spine; best-documented flow (Option C) |
| **F2+F3** | Metering (quota/deny → wallet/top-up) + Voucher posting (intent → registry → strategy → journal entries) | Revenue control + financial integrity — paired: both gate money |
| **F6** | Loyalty/points accrual & redemption | Ecosystem customer loop; touches orders + accounting |
| **F5** | `vanan-ai` chat: storefront widget → signed tenant ctx → policy/RAG → grounded answer → metering | Newest subsystem; isolation rules must be captured early but rides on modeled foundations |
| **F7+** | Remaining capabilities per §2.2 in owner-prioritized order | Booking, CRM, marketplace, procurement, HTX governance |

**Recorded caveat:** F4 is likely the least-implemented slice — expect the highest share of `ASSUMED`/`INFERRED` items and a fat gap register. That is the discovery value of doing it first, not a modeling failure.

**Owner decision (2026-11-07):** F4 and F1 proceed as **parallel tracks** — two independent modeling workstreams merged at each phase gate. F2+F3 start only after at least one track clears its first gate review, to keep review load bounded.

---

# 4. REQUIREMENT: ACTOR AND ORGANIZATION MODEL

## 4.1. Actor Classification

Every actor must be classified into one of the following categories:

- HUMAN: An individual performing an activity.
- ORGANIZATION: A legal entity, cooperative or business organization.
- SOFTWARE: An application, service, module or automated agent.
- AI AGENT: A software actor whose output is probabilistic/advisory (e.g., `vanan-ai` assistants, LLM-backed parsers). Subtype of SOFTWARE; must carry the AI boundary attributes in §7.4.
- EXTERNAL SYSTEM: A third-party system or government platform.
- HYBRID: A combined human/software operational role, where necessary.

## 4.2. Actor Registry

Create a centralized actor registry.

Required attributes:

- Actor ID.
- Actor name.
- Actor type (incl. AI AGENT subtype).
- Organization (reference to `ORG-` ID).
- Business responsibilities.
- Authorized actions.
- Data access scope.
- Related workflows.
- Related software modules.
- Current status.
- Evidence source (format per §9.1).

## 4.3. Responsibility Mapping

Each business activity must identify:

- Responsible actor.
- Accountable actor, where applicable.
- Supporting actors.
- Approval authority.
- System executing or supporting the activity.

Use a RACI matrix where appropriate.

Do not assume that the organization operating a workflow is necessarily the organization legally responsible for the underlying transaction.

## 4.4. Organization Registration Rule

An ORGANIZATION-type actor is registered **only** in the Organization Registry with an `ORG-` ID. It is **not** duplicated in the Actor Registry.

The Actor Registry contains HUMAN, SOFTWARE, AI AGENT, EXTERNAL SYSTEM and HYBRID actors (`ACTR-`), each referencing its parent `ORG-` where applicable. This prevents the same entity existing under two IDs and breaking bidirectional traceability.

---

# 5. REQUIREMENT: HUMAN VS SOFTWARE EXECUTION

This is a mandatory core requirement.

## 5.1. Execution Classification

Classification applies to each **atomic activity**, not to a whole process. A process containing a customer action, a merchant confirmation and a software update is decomposed into three activities — it is not one HYBRID activity.

**Decompose-first rule:** before assigning HYBRID, attempt to split the work into separate MANUAL and AUTOMATED activities connected by a handoff or approval gate. HYBRID is correct only when splitting would misrepresent the work — i.e., human and software act in one continuous, inseparable interaction producing a single shared output. HYBRID should be rare; overusing it destroys automation-rate measurability.

Every activity must have exactly one primary execution classification:

### MANUAL

One human performs one action.

Examples:
- Salesperson visits a shop.
- Shop owner confirms an order.
- HTX representative verifies membership.
- Accountant reviews a generated tax report and approves it (the report *generation* is a separate AUTOMATED activity).
- Fraud analyst decides on a flagged transaction (the *detection* is a separate AUTOMATED activity).
- Operator approves AI knowledge `pending_review → approved` (the *submission/ingest* is a separate activity).
- Hạt Nhân supports a merchant.

### AUTOMATED

One system performs one action with no human intervention for that execution.

Examples:
- Calculate loyalty points.
- Synchronize data (NATS order delivery PG→SQLite).
- Generate transaction numbers.
- Detect an abnormal transaction and flag it for review.
- Generate a tax report for accountant review.
- Send automated notifications.
- AI retrieves tenant knowledge and drafts an answer within policy (generation alone is AUTOMATED).
- Execute configured business rules.

### HYBRID

One atomic step whose output is co-produced by inseparable human+software interaction — a single session where neither side completes the output alone.

Examples:
- **Smart Entry voucher input:** operator types natural-language text; the LLM parses fields inline; the operator corrects and confirms in the same step — one interactive activity, one output (a draft voucher).
- **Assisted support resolution:** Hạt Nhân resolves a merchant issue in a live console where the system continuously surfaces data, scores and options as the human works — the resolution is co-produced.
- **Interactive AI-assisted data entry** where system validation/suggestion and human editing interleave within one submission step.

Contrast — NOT hybrid (two activities + handoff):
- "Software detects anomaly; human reviews" → AUTOMATED detect + MANUAL review.
- "Software generates report; accountant validates" → AUTOMATED generate + MANUAL approve.
- "AI suggests product; customer adds to cart" → AUTOMATED suggest + MANUAL purchase decision.

## 5.2. Execution Metadata

Every activity must contain:

- Activity ID.
- Activity name.
- Execution type.
- Human actor, if applicable.
- Software actor, if applicable.
- Trigger.
- Input.
- Processing steps.
- Business rules.
- Output.
- Approval requirement.
- Exception handling.
- Audit log requirement.
- Related module.
- Related API or service, if identifiable.
- Related database entities or tables, if identifiable.
- KPI, where measurable (link to `KPI-` registry entry).
- Source evidence (format per §9.1).
- Confidence level.

**Confidence scale:**

| Level | Meaning |
|---|---|
| VERIFIED | Confirmed against executable code, schema, API surface or test |
| INFERRED | Strongly indicated by docs/naming but not directly verified in code |
| ASSUMED | Business expectation; requires owner confirmation |

## 5.3. Mandatory Visualization

Use different visual styles for each execution type.

Required conventions (Mermaid `classDef` names are mandatory so diagrams stay consistent):

| Type | Color | classDef |
|---|---|---|
| Human activities | Blue | `human` |
| Automated software activities | Green | `auto` |
| Hybrid activities | Amber | `hybrid` |
| External systems or actors | Grey | `external` |
| Errors, exceptions, blocked | Red | `error` |
| AI agent activities | Green + dashed border | `ai` (`stroke-dasharray: 5 5`) |

These conventions must be consistent across all diagrams.

---

# 6. REQUIREMENT: BUSINESS PROCESS MODELING

## 6.1. Process Hierarchy

Build a hierarchical process model.

- Level 0 – Entire Vạn An Ecosystem.
- Level 1 – Major business domains.
- Level 2 – Business processes.
- Level 3 – Operational workflows.
- Level 4 – Individual activities and system actions.

Each process must have a unique ID and a defined relationship with its parent process.

## 6.2. Diagram Types — distinct questions, distinct tools

The three diagram families answer different questions and are NOT interchangeable:

| Diagram | Question it answers | Audience |
|---|---|---|
| Swimlane flowchart | **Who does what, and where does responsibility sit?** | Business owners, HTX operators, managers |
| Flowchart (no lanes) | What is the sequence of steps/decisions? | Everyone |
| Sequence diagram | How do actors/systems interact over time (calls, events, messages)? | Developers, architects |

**Rules:**

1. **Critical business processes (§6.5) MUST have a swimlane flowchart.** Mermaid has no native swimlanes — implement lanes as one `subgraph` per actor (lane title = `ACTR-ID + name`), nodes inside a subgraph belong to that lane, cross-lane edges are handoffs, each subgraph uses `direction TB`. If lane ordering renders poorly, simplify the diagram rather than switch types.
2. **Technical interaction flows use `sequenceDiagram`** — e.g., `KhachLink → Gateway → NATS → ShopERP`, `caller → vanan-metering`, `widget → vanan-ai`. These complement swimlanes; they never replace them for business documentation.
3. A critical flow may produce **both** artifacts: swimlane for responsibility, sequence for technical handoffs — linked by shared `ACTV-`/`API-` IDs.
4. Never mix conventions within one diagram.

Lanes represent only actors that actually participate (Customer, Merchant, Shop employee, CTV, Hạt Nhân, HTX, Vạn An Ecommerce, Vạn An Technology, Gateway, ShopERP, Metering, `vanan-ai`, External system). Do not create unnecessary lanes.

## 6.3. Flowchart Requirements

Every workflow must represent:

- Start and end events.
- Triggers.
- Human actions.
- Automated actions.
- Hybrid actions.
- Decisions and branching.
- Data inputs and outputs.
- Approval gates.
- Exceptions and error paths.
- External dependencies.
- Handoffs between actors.
- Responsible parties.

A happy-path-only diagram is insufficient.

## 6.4. Modeling Standards

Use Mermaid for version-controlled diagrams.

Preferred diagram types:

- `flowchart` for operational workflows (with lane subgraphs per §6.2).
- `sequenceDiagram` for technical interactions between systems and actors.
- `erDiagram` for data relationships.
- `classDiagram` where useful for domain structures.

If Mermaid cannot clearly represent a complex workflow, split it into linked diagrams rather than producing an unreadable diagram. All Mermaid blocks must be validated to render (AC-11).

## 6.5. Critical Process Definition

A process or workflow is **critical** if it meets ANY of:

1. Creates, modifies or reverses financial/accounting records (orders, vouchers, journal entries, wallet movements).
2. Crosses a tenant boundary or an organization boundary (multi-tenancy, HTX↔merchant↔Vạn An).
3. Crosses a deployment boundary (multi-VPS routing, service-to-service calls).
4. Requires approval, produces audit evidence, or can be reversed/corrected.
5. Gates revenue (metering deny/allow) or exposes tenant data to AI retrieval.
6. Failure creates regulatory, tax or data-loss exposure.

Critical processes get: swimlane diagram + exception paths + governance mapping (§10) + traceability to code (§7.3). Non-critical processes may use lighter documentation.

## 6.6. Process Variability Classes (HTX replication)

To serve the "HTX in a Box" goal, every process must be classified by variability — a single rigid TO-BE does not survive contact with hundreds of different cooperatives:

| Class | Meaning | Example |
|---|---|---|
| `CORE` | Mandatory standard — identical for every HTX/tenant | Order lifecycle spine; voucher posting rules; metering deny gate |
| `CONFIGURABLE` | Same process, behavior varied by configuration | Feature-flagged steps via `TenantFeatureSettings`/`IShopFeatureSettingsService`; notification channels; approval thresholds |
| `VARIANT` | Process differs by business type or HTX model | F&B kitchen flow vs spa booking flow; delivery-enabled vs pickup-only shops |

Rules:

- Every `VARIANT` process MUST reference its parent `CORE` process and document only its **deltas** — never fork the whole process tree.
- `CONFIGURABLE` variation points should map to actual configuration mechanisms in code where they exist (feature settings, tenant config) — cite them as evidence.
- A process may not be `VARIANT` without a registered `CORE` parent; orphan variants are a validator error.
- Registries store the class as a required `variability` field on each `PROC-` entry.

---

# 7. REQUIREMENT: SOFTWARE ARCHITECTURE MAPPING

## 7.1. Software Inventory

Discover all actual software components in the repository. §2.3 provides verified starting points — extend, don't rediscover.

Investigate, where present:

- Frontend applications (`5_WebApps/ShopERP`, `5_WebApps/KhachLink`, `4_MobileApps/`).
- Backend applications and API hosts.
- Business services (`3_CoreHub/` services, `VanAn.Accounting/`).
- Background workers and scheduled jobs (sync workers, outbox dispatchers).
- Message brokers (NATS subjects).
- Databases (Gateway PostgreSQL, per-tenant SQLite, `vanan-ai` PG+pgvector).
- Standalone services (`8_Metering/vanan-metering`, `9_TenantAI/vanan-ai`).
- Authentication and authorization.
- External integrations (Gemini API, payment providers, e-invoice).
- Deployment and infrastructure components (`nginx/`, Dockerfiles, `scripts/`).

Do not assume listed components are all separate deployable services — identify actual implementation boundaries. Example: CoreHub is a class library hosted inside ShopERP, not a standalone service; metering and vanan-ai ARE standalone.

## 7.2. Component Mapping

Each component must include:

- Component ID (`SYS-`).
- Name.
- Purpose.
- Business capability (`CAP-` refs).
- Repository path.
- Technology.
- Deployment boundary (in-process lib / hosted service / separate VPS).
- Dependencies.
- API interfaces (`API-` refs).
- Database access.
- Related workflows.
- Current implementation status (implemented / partial / planned).

## 7.3. Business-to-Code Traceability

For every important business activity, trace:

Business Activity
→ Software Module
→ Service / Handler
→ API / Event
→ Database Entity
→ Output / Result.

When a link cannot be verified, mark it as UNKNOWN or UNVERIFIED.

Never invent API names, database tables or service implementations.

## 7.4. AI Execution Boundary (`vanan-ai` / Tenant Intelligence Platform)

AI actors are modeled with additional mandatory attributes:

- **Advisory vs executing:** `vanan-ai` is an advisory layer — "AI là lớp giao tiếp thông minh, KHÔNG phải nguồn dữ liệu kinh doanh." AI NEVER creates orders, journal entries or business records; it suggests into existing flows (e.g., product suggestion → standard cart/checkout).
- **Authority source:** structured tenant data wins over vector-retrieved knowledge on conflict; the model must show which store is authoritative per data item.
- **Tenant isolation:** AI retrieval is scoped by signed tenant context; cross-tenant leakage is a modeled risk, not an implementation detail.
- **Approval gate:** tenant knowledge is `pending_review → approved`; only `approved` knowledge is retrievable publicly (HYBRID activity).
- **Degradation:** metering deny/fail → degrade to structured-only or refuse per configured policy — must appear as an exception path in diagrams.
- **Metering:** AI usage is a metered event (`ai.chat.message` serviceCode) — link AI activities to the metering flow (F2/F5).

Any future AI capability added to the ecosystem must be registered as an AI AGENT actor and carry this boundary section.

---

# 8. REQUIREMENT: DATA FLOW MODEL

## 8.1. Data Flow Mapping

For each important workflow, identify:

- Data origin.
- Data owner.
- Data creator.
- Data processor.
- Data recipient.
- Data storage location (and which store is the system of record).
- Data synchronization mechanism.
- Data retention or deletion mechanism, where known.
- Access control.
- External data transfers.

## 8.2. Data Classification

Classify data according to its business and security characteristics.

Examples:

- Merchant data.
- Customer data.
- Identity and account data.
- Orders and transactions.
- Accounting and tax data (immutable `AccountingEntry` — append-only, reversal-only).
- Loyalty points.
- Service credits.
- HTX membership.
- Financial records (wallets, top-ups, metering usage).
- Tenant AI knowledge (approved knowledge, embeddings, conversation logs — see §8.4).
- Operational logs.
- System configuration.

Identify sensitive personal data and financial information without exposing real production records in documentation.

## 8.3. Data Flow Diagrams

Create diagrams showing:

- Data creation.
- Data transformation.
- Data validation.
- Data synchronization.
- Data storage.
- Data retrieval.
- Data sharing.
- Data deletion or archival.

Distinguish between the system of record and replicated or cached data. Baseline: Gateway PG = system of record for Orders/Accounting/Tenants; ShopERP SQLite = operational replica for kitchen/POS; Products live only in ShopERP SQLite; AI knowledge lives only in `vanan-ai` PG.

## 8.4. AI Data Rules

- Tenant knowledge and embeddings are tenant-scoped data — classification, retention (e.g., conversation retention window) and approval state must be modeled.
- `Confidential`-classified knowledge must never reach public/anonymous retrieval contexts; model the `Approved Public Fact Extraction` path.
- No production secrets, tokens, or real personal data in documentation or example payloads (NFR-07).

---

# 9. REQUIREMENT: AS-IS AND TO-BE MODELS

## 9.1. AS-IS

The AS-IS model must describe actual current behavior.

Evidence hierarchy:

1. Executable code and actual configuration.
2. Database schema and migrations.
3. API definitions and integration implementations.
4. Automated tests and E2E tests.
5. Existing technical documentation.
6. Business specifications.
7. Assumptions requiring confirmation.

**Evidence reference format (mandatory):**

```
<repo-relative-path>@<commit-sha>          e.g. 2_Gateway/Controllers/OrdersController.cs@a1b2c3d
<repo-relative-path>#L<start>-<end>         when commit is impractical, with a note that line refs decay
```

Every evidence entry in the Evidence Register records the commit SHA of the repository at the time it was captured. If a claim cannot be re-verified at HEAD, it is flagged STALE in the register — not silently trusted.

If documentation conflicts with code, record the discrepancy (in the evidence register AND the gap register).

## 9.2. TO-BE

The TO-BE model describes the intended operating model.

It must identify:

- Activities to retain as manual.
- Activities to automate.
- Activities requiring human approval.
- New business controls.
- New software capabilities.
- Required integrations.
- Required data.
- Changes in responsibilities.

TO-BE must not be presented as already implemented. TO-BE proposals drafted by Devin are **recommendations**; they enter the model as `PROPOSED` and become `APPROVED` only after business-owner sign-off at the Phase 4 gate.

## 9.3. Automation Gap Analysis

Create a structured comparison:

| Activity | AS-IS | TO-BE | Gap |
|---|---|---|---|
| Merchant onboarding | Verify from evidence | Partially automated | Identify missing steps |
| Order processing | Verify from evidence | Automated where appropriate | Identify missing capabilities |
| Loyalty calculation | Verify from evidence | Automated | Identify missing rules |
| Accounting validation | Verify from evidence | Hybrid | Identify approval controls |
| HTX reporting | Verify from evidence | Automated reporting + human review | Identify missing functions |
| AI tenant advisory | Verify from evidence | Hybrid (AI drafts, human/system validates) | Identify knowledge gaps + policy gaps |

The examples above are investigation targets, not assertions about current implementation.

Classify each gap:

- Missing business process.
- Missing software feature.
- Manual operation that can be automated.
- Missing integration.
- Duplicate functionality.
- Inconsistent business rule.
- Missing audit or approval.
- Data inconsistency.
- Security or permission gap.
- AI boundary gap (advisory acting as executing, unapproved knowledge leak, missing metering).
- Unknown / needs business clarification.

The assessment must not assume that every manual activity should be automated.

---

# 10. REQUIREMENT: GOVERNANCE AND CONTROL MODEL

Map governance across the ecosystem.

Investigate:

1. User identity and authentication.
2. Role-based permissions.
3. Organization and tenant boundaries (multi-tenancy enforced at every layer).
4. Transaction approval.
5. Financial authorization.
6. Data access.
7. Audit trails.
8. Exception handling.
9. System monitoring.
10. Operational accountability.

For each critical process, identify:

- Who initiates it?
- Who executes it?
- Who approves it?
- Who receives the result?
- Who is legally or contractually responsible?
- Who can reverse or correct it?
- What audit evidence is retained?

Pay special attention to the boundary between the merchant, HTX, Vạn An Ecommerce, Vạn An Technology and external service providers.

Do not assign legal responsibility solely from software permissions or workflow participation.

## 10.1. Known Control Mechanisms (verified anchors)

- `AccountingEntry` immutability: append-only; corrections via reversal entries only.
- Multi-tenancy: `BaseEntity.TenantId`; filtering by direct property comparison (never `EF.Property<Guid>` on TenantId — Known Error Pattern #1).
- Metering deny gate: quota exhaustion blocks metered operations (caller fail-open client; `vanan-ai` uses stricter degrade policy).
- AI knowledge approval: `pending_review → approved` gate before public retrieval.
- `DevToken__Secret`: dev-auth endpoint disabled by default; inline env only during RV windows.

## 10.2. AI Governance

- Who owns/approves each tenant's knowledge base? (tenant = knowledge owner; Vạn An = infrastructure owner — model the approval actor per tenant).
- Who is accountable for an incorrect AI answer? Model as: AI output is advisory; accountability follows the actor who turns a suggestion into a business action.
- AI quota/deny behavior is a governance control (cost boundary), not just a technical limit.

---

# 11. REQUIREMENT: DIGITAL TWIN REGISTRY

Create structured registries that can be maintained as the system evolves.

Required registries:

1. Actor Registry.
2. Organization Registry.
3. Business Capability Registry.
4. Process Registry.
5. Activity Registry.
6. Software Component Registry.
7. API and Integration Registry.
8. Data Entity Registry.
9. Business Rule Registry.
10. Risk and Control Registry.
11. KPI Registry.
12. Gap and Improvement Registry.

## 11.1. Stable Identifiers

| Prefix | Entity |
|---|---|
| `ACTR-xxx` | Actor (non-organization) |
| `ORG-xxx` | Organization |
| `CAP-xxx` | Capability |
| `PROC-xxx` | Process |
| `ACTV-xxx` | Activity |
| `SYS-xxx` | Software component |
| `API-xxx` | API / integration |
| `DATA-xxx` | Data entity |
| `RULE-xxx` | Business rule |
| `CTRL-xxx` | Control |
| `RISK-xxx` | Risk |
| `KPI-xxx` | KPI |
| `GAP-xxx` | Gap / improvement |

Identifiers must not change simply because a name or diagram changes. Retired items are marked `RETIRED`, never renumbered or reused.

## 11.2. Traceability

The documentation must support navigation in both directions:

Business Requirement → Process → Activity → Software → API → Database.

And:

Software Component → Business Capability → Process → Activity → Responsible Actor.

## 11.3. Machine-Readable Registry Format

Markdown alone cannot answer queries ("which processes use this module?", "which activities lack an owner?", "what is the MANUAL/AUTOMATED ratio?"). The twin therefore uses three formats with distinct roles:

| Format | Role |
|---|---|
| **YAML** | Registry data — one file per registry, Git-diff friendly, supports comments |
| **JSON Schema** | One schema per registry defining required fields, ID patterns, enums and reference rules |
| **Markdown + Mermaid** | Human narrative, explanation and visualization — references registry IDs, never duplicates registry data |

**Layout:** `docs/enterprise-operating-model/registries/{actors,organizations,capabilities,processes,activities,components,apis,data-entities,rules,controls,kpis,gaps}.yaml` + `registries/schema/*.schema.json`.

**Validator** (`scripts/validate-twin.ps1` or equivalent — no new platform required): checks, as CI or pre-push step:

1. ID format and uniqueness per registry.
2. Required fields present (per schema).
3. Referential integrity — every referenced ID exists; orphaned VARIANT processes, dangling `ORG-`/`PROC-`/`SYS-` links are errors.
4. Enum values valid (execution type, confidence, variability class, status).
5. Evidence references match the §9.1 format.
6. Coverage flags — activities without owner or without software link are *warnings*, not errors (they are legitimate findings).

Scope honesty: the validator checks **structural integrity** (links, fields, formats). It cannot verify that a diagram semantically matches code — that remains a human/RV responsibility.

---

# 12. REQUIREMENT: DOCUMENTATION STRUCTURE

Create a dedicated documentation directory without disrupting the existing application architecture.

Recommended structure (sits alongside existing `docs/` subtrees; see §16 rule — twin indexes existing docs, it does not absorb them):

```
docs/enterprise-operating-model/

    00-overview/
        ecosystem-overview.md
        system-context.md
        glossary.md

    01-actors/
        actor-registry.md
        organization-registry.md
        responsibility-matrix.md

    02-business-capabilities/
        capability-map.md

    03-processes/
        process-catalog.md
        swimlane-diagrams/
        process-details/

    04-software-architecture/
        software-inventory.md
        component-map.md
        integration-map.md

    05-data-architecture/
        data-flow-catalog.md
        data-ownership.md
        data-entity-map.md

    06-governance/
        authorization-map.md
        approval-matrix.md
        audit-controls.md

    07-as-is/
        current-operating-model.md

    08-to-be/
        target-operating-model.md

    09-gap-analysis/
        automation-gap-register.md
        risk-register.md
        improvement-roadmap.md

    10-digital-twin/
        digital-twin-schema.md
        traceability-matrix.md
        model-index.md

    registries/                    # machine-readable twin data (§11.3)
        *.yaml
        schema/*.schema.json

    11-validation/
        evidence-register.md
        unresolved-questions.md
        validation-report.md
```

Adjust this structure to fit the actual repository conventions if necessary.

## 12.1. Twin Maintenance Triggers

The twin is a living layer — but maintenance must be proportionate, or the model becomes a burden that gets abandoned. Two tiers:

### Mandatory update (same change batch or linked follow-up task)

Business-visible behavior changes:

| Repo change | Required twin update |
|---|---|
| New/changed business rule or transaction flow | RULE + affected ACTV/PROC + diagrams |
| New voucher type (`VoucherTypeRegistry.AddVoucherType`) | Activity + RULE + CTRL entries; process diagram |
| Permission/role/responsibility change | Authorization map; approval matrix; RACI |
| Tenant-boundary or source-of-truth change | Data ownership; governance; relevant flows |
| New/changed integration behavior (NATS subject, sync direction, external API contract) | Integration map; data-flow diagrams |
| New actor-facing workflow or approval gate | Process catalog; swimlane; RACI |
| AI boundary change (new intent, knowledge class, policy, advisory→executing shift) | AI boundary attributes; CTRL/RISK entries |

### Conditional update (registry touch-up only — no diagram churn)

Internal implementation changes that don't alter business behavior:

| Repo change | Required twin update |
|---|---|
| New internal API endpoint | `apis.yaml` entry if referenced by a modeled activity; otherwise defer |
| Refactor/rename within a module | Update evidence refs only if the component is already referenced |
| New entity/table with no process impact | `data-entities.yaml` row; no diagram work |
| Internal service/helper added | SYS entry only if it crosses a documented boundary |

A change touching neither tier needs no twin update. The registry validator (§11.3) should run in CI/pre-push to catch broken links when referenced artifacts move — it checks link integrity and coverage flags, not semantic equivalence between diagrams and code.

---

# 13. NON-FUNCTIONAL REQUIREMENTS

## NFR-01: Accuracy

Every AS-IS technical statement must have an evidence reference in the §9.1 format.

## NFR-02: Traceability

Every major business process must link to its activities, actors and software components.

## NFR-03: Maintainability

All diagrams must be editable text files, preferably Mermaid.

## NFR-04: Version Control

All documentation must be maintained in Git.

## NFR-05: Readability

Diagrams must be split by business domain and process level. Avoid giant unreadable diagrams — never force the whole ecosystem into a single flowchart; use linked multi-level diagrams.

## NFR-06: Consistency

Execution classifications, identifiers, `classDef` color conventions and visual styles must be consistent.

## NFR-07: Security

Never include credentials, access tokens, real personal data or production secrets.

## NFR-08: Extensibility

The model must support future HTX expansion, additional tenants, software modules and integrations without redesigning the entire documentation structure.

## NFR-09: Evidence

Every unknown, assumption and unverified implementation must be explicitly identified with a confidence level (§5.2).

## NFR-10: AI Boundary Accuracy

Every statement about AI behavior must distinguish what the AI advises from what it executes, and must name the authoritative data source for each claim the AI can make.

---

# 14. IMPLEMENTATION PLAN

Execute the project in controlled phases. Each phase gate is a STOP for review.

## PHASE 0 – REPOSITORY DISCOVERY

Tasks:

- Inspect repository structure; confirm/extend the §2.3 verified anchors.
- Identify all applications and projects.
- Inspect solution files and dependencies.
- Identify databases and migrations.
- Identify APIs, workers and scheduled jobs.
- Review existing SRS, architecture documents and business specifications.
- **Inventory existing documentation (`docs/`) and record which docs the twin will index vs. supersede — deduplication mandate.**
- Identify existing Mermaid diagrams.
- Identify implemented versus planned modules.

Output:

- Repository inventory.
- Initial software component inventory.
- Existing-docs index (link map).
- Evidence register.
- Unresolved questions.

Do not modify application code.

**GATE 0:** discovery report + proposed coverage map (ordered per §3.1).

## PHASE 1 – ECOSYSTEM AND ACTOR MODEL

Tasks:

- Identify business entities.
- Identify organizational boundaries.
- Identify all actor categories (incl. AI AGENT actors).
- Map responsibilities.
- Identify external systems.

Output:

- Ecosystem context diagram.
- Actor registry.
- Organization registry.
- Responsibility matrix.

*(Gate review folded into Gate 2 unless scope is large.)*

## PHASE 2 – AS-IS BUSINESS PROCESS MODEL

Tasks:

- Identify major business capabilities.
- Build the process hierarchy.
- Map operational workflows (§3.1 order: F1→F2→F3 first).
- Identify human and automated activities.
- Map approvals and exception paths.

Output:

- Capability map.
- Process catalog.
- Swimlane diagrams.
- Human/software execution matrix.

**GATE 2:** complete process inventory + representative diagrams for business-owner validation.

## PHASE 3 – SOFTWARE AND DATA TRACEABILITY

Tasks:

- Map business activities to code.
- Identify services, handlers and APIs.
- Map database entities.
- Map integrations and synchronization.
- Identify source-of-truth boundaries (PG vs SQLite vs pgvector).

Output:

- Software architecture map.
- Data flow diagrams.
- Business-to-code traceability matrix.

**GATE 3:** software and data traceability results.

## PHASE 4 – TO-BE AND GAP ANALYSIS

Tasks:

- Compare actual workflows with intended business objectives.
- Identify manual work.
- Evaluate automation opportunities.
- Identify missing controls and integration requirements.
- Separate confirmed gaps from assumptions.
- **Draft TO-BE as PROPOSED items only.**

Output:

- TO-BE process diagrams (PROPOSED).
- Automation gap register.
- Risk register.
- Prioritized improvement roadmap.

**GATE 4:** business-owner sign-off on TO-BE items and gap priorities. Unapproved items remain PROPOSED.

## PHASE 5 – DIGITAL TWIN DOCUMENTATION

Tasks:

- Standardize identifiers.
- Create structured registries.
- Establish cross-references.
- Validate diagram consistency.
- Establish documentation maintenance rules (§12.1).

Output:

- Digital Twin documentation layer.
- Master index.
- Traceability matrix.
- Final validation report.

**GATE 5:** complete documentation package + acceptance report.

---

# 15. ACCEPTANCE CRITERIA

The project is complete only when the following conditions are met.

### AC-01: Ecosystem Coverage

All applications, business domains, organizations and actors within the declared coverage scope are inventoried. Coverage is claimed per §3.1 slice, not as an absolute. Unknown or inaccessible areas are explicitly reported.

### AC-02: Execution Classification

Every modeled activity is classified as MANUAL, AUTOMATED or HYBRID.

### AC-03: Swimlane Coverage

Every critical (§6.5) end-to-end workflow has a swimlane flowchart showing human and software responsibilities (§6.2).

### AC-04: Exception Coverage

Critical workflows include alternative paths, errors, approval gates and recovery actions.

### AC-05: Software Traceability

Every verified software activity links to actual code, API, service or database evidence where applicable.

### AC-06: Data Traceability

Critical data flows identify their origin, processor, system-of-record storage and destination.

### AC-07: AS-IS Integrity

No planned feature is incorrectly presented as an implemented feature.

### AC-08: TO-BE Separation

Future-state proposals are clearly separated from verified current-state behavior, and PROPOSED vs APPROVED status is explicit.

### AC-09: Automation Gap

All identified manual activities have an automation assessment. The assessment must not assume that every manual activity should be automated.

### AC-10: Governance

Critical processes identify execution responsibility, approval authority and control requirements.

### AC-11: Documentation Quality

All Mermaid diagrams render successfully, all internal references resolve, and the registry validator (§11.3) passes with zero errors.

### AC-12: No Regression

Existing application code, database schemas, APIs and runtime behavior remain unchanged during this documentation project.

### AC-13: AI Boundary

All AI activities identify advisory-vs-executing role, authoritative data source, tenant isolation scope and metering link.

### AC-14: Final Validation

Provide a report listing:

- Completed deliverables.
- Incomplete deliverables.
- Unverified areas.
- Business questions requiring owner confirmation.
- Technical gaps.
- Recommended next steps.

---

# 16. STRICT EXECUTION RULES FOR DEVIN

1. Begin with repository discovery. Do not immediately generate diagrams based on assumptions.
2. Do not modify application code, database schemas, APIs or production configuration.
3. Do not create stubs or placeholder implementations to make diagrams appear complete.
4. Do not infer that a module exists merely because it is mentioned in a business document.
5. Do not infer legal or organizational responsibility from software implementation alone.
6. Clearly distinguish verified facts, business requirements, assumptions and recommendations (confidence scale §5.2).
7. Preserve existing documentation and repository conventions. The twin **indexes** existing docs; it does not duplicate or absorb them.
8. Do not remove or overwrite existing documents without explicit authorization.
9. If business information is insufficient, record the question rather than inventing an answer.
10. Work in small, reviewable phases.
11. Validate Mermaid syntax and cross-references.
12. Provide evidence for every significant technical conclusion in the §9.1 format.
13. Treat §2.3 anchors as a **re-verification checklist** — independently verify each claim at Phase 0, assign per-component verification and coverage status, and record drift rather than rewriting anchors silently. The table never substitutes for survey output.
14. AI actors get the §7.4 boundary treatment — no exceptions.
15. Registry data lives in YAML (§11.3); Markdown must reference registry IDs, never restate registry facts that could drift.

## PHASE GATES

Gates after Phase 0, 2, 3, 4 and 5 (§14). Approval of a phase is never permission to modify application code.

---

# 17. FINAL DELIVERABLE

Devin must deliver the following artifacts, each mapped to a deliverable ID:

| # | Artifact | Deliverable |
|---|---|---|
| 1 | Vạn An Ecosystem Context Diagram | D01 |
| 2 | Business Capability Map | D01 |
| 3 | Organization and Actor Map | D01 |
| 4 | Human vs Software Execution Map | D02 |
| 5 | AS-IS Enterprise Operating Model | D02 |
| 6 | TO-BE Enterprise Operating Model | D03 |
| 7 | Swimlane diagrams for critical business processes | D02/D03 |
| 8 | Software Architecture Diagram | D04 |
| 9 | Data Flow Architecture Diagram | D04 |
| 10 | Governance and Responsibility Model | D02/D05 |
| 11 | Automation Gap Register | D05 |
| 12 | Business-to-Code Traceability Matrix | D04/D06 |
| 13 | Digital Twin Registry (12 registries, §11) | D06 |
| 14 | Prioritized Improvement Roadmap | D05 |
| 15 | Final Validation and Coverage Report | D06 |

The final model must allow a business owner, technical architect, developer or HTX operator to answer:

- Who performs each activity?
- What triggers the activity?
- Is it manual, automated or hybrid?
- Which software module supports or executes it?
- Is it advisory (AI) or executing?
- What data is created or modified, and where is the system of record?
- Who is responsible and who approves?
- What happens when something goes wrong?
- Is the functionality actually implemented?
- What should be automated next?

**DEFINITION OF DONE**

The Vạn An Enterprise Operating Model is accepted when the relationship between business operations, human responsibilities, software execution, data flows and governance can be traced end-to-end using version-controlled documentation supported by repository evidence.

No claim of 100% coverage is permitted unless the discovery scope and coverage evidence justify it.
