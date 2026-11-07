# Data Flow Catalog — P3

> 10 fields per critical flow (SRS §8.1). Registry: `registries/data-entities.yaml`. `64789992`.

## Flow F1 — Order lifecycle

| Field | Value |
|---|---|
| Origin | Customer checkout (KhachLink) — ProductName+VatRate snapshot (RULE-001) |
| Owner | Tenant (merchant) legally †; platform stores |
| Creator | Gateway order creator (ACTV-203) |
| Processor | Gateway → NATS → ShopERP POS/kitchen |
| Recipient | Shop staff/owner, kitchen, customer (tracking), loyalty engine |
| Storage | **SoT: PG `Order` (DATA-004)**; replica: SQLite order replica (DATA-005) |
| Sync | `vanan.cloud.order.created.{si}` → replica; status back via `status.changed` + `vanan.shoperp.*` |
| Retention | not evidenced — open question |
| Access | tenant-scoped (CTRL-002); public tracking by order id |
| External | payment confirm event; loyalty.changed to wallet |

## Flow F4 — Tenant provisioning

| Field | Value |
|---|---|
| Origin | Membership application (owner + sales) |
| Owner | Vạn An Ecommerce (ops) †; tenant owns business data |
| Creator | TenantManagementService (ACTV-107) |
| Processor | Gateway/CoreHub → NATS → ShopERP mirror |
| Recipient | ShopERP tenant DB, KhachLink instance binding |
| Storage | **SoT: PG Tenant (DATA-001), ShopInstance (DATA-002), application+HtxProfile (DATA-012)**; mirror: SQLite tenant row (DATA-003) |
| Sync | `tenant.verified/profile.updated/type.changed` |
| Retention | suspend/deactivate lifecycle (ACTV-115); deletion not evidenced |
| Access | admin ops + tenant self-service |
| External | KYC docs (OCR), domain registrar |

## Flow — AI knowledge (component-level §7.4; deep model at X3)

| Field | Value |
|---|---|
| Origin | Tenant-submitted docs + product sync (ShopERP) |
| Owner | **Tenant** (knowledge); Vạn An (infrastructure) |
| Creator | Ingest API + operator tooling |
| Processor | approval gate (`pending_review→approved`, CTRL-004) → chunk/embed → pgvector |
| Recipient | tenant's own customers via widget — **tenant isolation: signed ctx (CTRL-005)** |
| Storage | **SoT: vanan-ai PG (DATA-011)** — never replicated to business stores |
| Sync | manual/auto sync from ShopERP products |
| Retention | 90-day conversation retention (per AI SRS) |
| Access | Public↔Confidential classes; Confidential → extraction path only |
| External | Gemini API (embedding+chat) |

## Key SoT table

| Data | SoT | Replicas |
|---|---|---|
| Order | PG (DATA-004) | SQLite replica (DATA-005) |
| Products | SQLite per-tenant (DATA-006) | none to PG (Option C) |
| Tenant/ShopInstance | PG (DATA-001/002) | SQLite mirror (DATA-003) |
| AccountingEntry | PG (DATA-009) | — |
| Wallet/usage | metering DB (DATA-010) | — |
| AI knowledge | vanan-ai PG (DATA-011) | — |
