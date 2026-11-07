# System Context & Repository Inventory — Phase 0

> Captured at commit `64789992` (2026-11-07). Every claim links to `11-validation/evidence-register.md`.
> Coverage: PARTIAL — inventory-level; deep enumeration deferred to P3.

## 1. Applications & Deployables

| Component | Path | Boundary | Status | Evidence |
|---|---|---|---|---|
| Gateway (YARP + order creator + API host) | `2_Gateway/` (~80 controllers) | Hosted service, VPS | IMPLEMENTED | ER-A2 |
| ShopERP (Web API + Blazor, per-tenant SQLite) | `5_WebApps/ShopERP/` (~34 controllers) | Hosted, multi-VPS per ShopInstance | IMPLEMENTED | ER-A4 |
| KhachLink (customer PWA, no DB) | `5_WebApps/KhachLink/` | Hosted PWA | IMPLEMENTED | ER-A5 |
| Directory (SSR storefront directory) | `5_WebApps/Directory/` | Hosted | IMPLEMENTED | ER-B |
| CoreHub (business services + **generic host**: NatsSyncWorker, migrations) | `3_CoreHub/` | Library + Exe host (drift D1) | IMPLEMENTED | ER-A3 |
| vanan-metering (platform fee) | `8_Metering/vanan-metering/` | Standalone sln + own VPS | IMPLEMENTED | ER-A6 |
| vanan-ai (tenant intelligence) | `9_TenantAI/vanan-ai/` | Standalone sln + own VPS | IMPLEMENTED (piloting) | ER-A7 |
| Crawler tooling | `7_Tooling/VanAn.Crawler/` | Tooling worker | IMPLEMENTED | ER-B |
| UI Platform (component lib) | `UI.Platform/` | In-process library | IMPLEMENTED | ER-B |
| VanAn.Accounting | `VanAn.Accounting/` | Library — boundary vs CoreHub accounting TBD (Q6) | PARTIAL-survey | — |
| Mobile apps | `4_MobileApps/` | — | NOT-SURVEYED | — |
| Shared domain | `1_Shared/` (Domain/ 57 files) | Library | IMPLEMENTED | ER-A1 |

## 2. Data Stores (baseline SoT)

| Store | Holds | Role |
|---|---|---|
| Gateway PostgreSQL (`VanAnDbContext`, in `3_CoreHub/Infrastructure`) | Orders, Accounting, Tenants, ShopInstances, Users, FeaturedProducts | System of record |
| ShopERP per-tenant SQLite (`ShopERPDbContext`) | Products, POS/kitchen replica of orders, tenant mirrors | Operational store + replica |
| vanan-metering DB | Wallets, top-ups, usage, rate cards | SoT (metering domain) |
| vanan-ai PG + pgvector | Tenant knowledge, embeddings, sessions | SoT (AI knowledge) |

## 3. Messaging (NATS)

- **PG→SQLite direction:** `vanan.cloud.*` — order.created.{shopInstanceId}, order.status.changed.{si}, order.payment.confirmed.{si}, tenant.verified/profile.updated/type.changed, loyalty.changed.{deviceId}, loyalty.config.changed, notification.created
- **SQLite→PG direction:** `vanan.shoperp.*` — order status + tenant sync (subjects not enumerated — P3)
- Routing key = `ShopInstanceId` (multi-VPS; wildcard subs removed)

## 4. Workers / Subscribers

Gateway: DataSyncSubscriber, EInvoiceSyncSubscriber, NotificationHubNotifier · ShopERP: OrderSyncSubscriber, TenantSyncSubscriber, LoyaltySyncSubscriber, PaymentConfirmedSubscriber · CoreHub: NatsSyncWorker, JournalPostingRetryWorker, EInvoiceWorker · Tooling: CrawlerCoordinator.

## 5. External Integrations (seeds)

Gemini API (LLM: SmartEntry parse, vanan-ai) · payment providers (VietQR top-up — verify) · e-invoice (EInvoiceWorker/Subscriber + providers) · government platforms (HTX/tax reporting — not confirmed in code this pass).

## 6. Implemented vs Planned (inventory level)

- IMPLEMENTED: order lifecycle (Option C), metering, voucher framework, accounting engine (TT133/TT71/TT58), loyalty alliance, booking, tenant provisioning basics, vanan-ai pilot stack.
- PARTIAL/UNCERTAIN: marketplace, procurement, subscription billing beyond metering, HTX financial mgmt — confirm at P3/X4.
- PLANNED: per SRS §2.2 capability groups not yet evidenced → ASSUMED until surveyed.

## 7. Critical Process Classification (SRS §6.5)

| Flow | Criteria hit | Critical? |
|---|---|---|
| F4 onboarding/HTX/tenant provisioning | 2 (org boundary), 3 (multi-VPS routing) | YES |
| F1 order lifecycle | 1 (financial record), 3, 4 (status audit) | YES |
| F2 metering deny/wallet | 5 (revenue gate), 1 | YES |
| F3 voucher posting | 1, 4 (approval/audit), 6 (tax) | YES |
| F5 AI chat | 5 (tenant data to AI), 3 | YES |
| F6 loyalty | 1 (financial-adjacent points), 4 | YES |
| F7+ others | varies | triage at expansion |

## 8. Coverage Map (execution order — SRS §3.1 + owner decision)

`F4 ∥ F1` parallel → `F2+F3` → `F6` → `F5` → `F7+`. Backbone: P1 actors/capabilities → P3 traceability → P4 governance/gap → P5 twin package. Gates G0–G5 per master plan.

## 9. Phase-0 Outputs (G0 evidence)

- This inventory · `11-validation/evidence-register.md` (12 anchors + drift D1–D3) · `00-overview/existing-docs-index.md` (dedup) · `11-validation/unresolved-questions.md` (Q1–Q7) · scaffold from P0b (registries+schemas+validator, 0 errors).

**G0 ask:** approve coverage map + confirm Q1/Q2 governance-text updates + answer Q3/Q4 business narrative → proceed P1.
