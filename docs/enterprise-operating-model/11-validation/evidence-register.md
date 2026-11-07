# Evidence Register — Phase 0

> Captured at commit `64789992` (2026-11-07). Format per SRS §9.1.
> Status: VERIFIED · DRIFTED · UNVERIFIED. Coverage: NOT-SURVEYED · PARTIAL · COVERED.

## A. SRS §2.3 Anchor Re-Verification (12 claims)

| # | Claim | Status | Evidence | Coverage | Notes |
|---|---|---|---|---|---|
| 1 | `1_Shared/Domain.cs` = SSOT domain, multi-tenant BaseEntity, single-identity | **DRIFTED** | `1_Shared/Domain/` (57 files, subdirs Aggregates/Common/Events/Audit) + `1_Shared/Domain.cs` @64789992 | PARTIAL | Entities live in `1_Shared/Domain/` folder tree, not a single file. SSOT location = `1_Shared/` domain code; "Domain.cs single file" wording is outdated. BaseEntity/identity pattern not re-checked line-by-line. |
| 2 | Gateway = Option C order creator; PG SoT for Orders/Accounting/Tenants/ShopInstances/Users/FeaturedProducts | VERIFIED | `2_Gateway/Program.cs`@64789992; `2_Gateway/Controllers/PublicOrdersController.cs`@64789992; `3_CoreHub/Infrastructure/VanAnDbContext.cs`@64789992 (PG DbContext in CoreHub Infra, used by Gateway host) | PARTIAL | Entity-level PG scope not enumerated table-by-table. |
| 3 | CoreHub = pure class library (no exe) | **DRIFTED** | `3_CoreHub/VanAn.CoreHub.csproj`@64789992 → `<OutputType>Exe</OutputType>`; `3_CoreHub/Program.cs`@64789992 comment "Build: 2026-06-17 (OutputType=Exe fix)" — Generic Host (Microsoft.Extensions.Hosting), no Web SDK; hosts NatsSyncWorker/migrations | COVERED | Contradicts governance text "MUST remain pure Class Library". Likely intentional deviation 2026-06-17 — governance doc outdated, not code violation. **Owner confirm needed** (see unresolved-questions Q1). |
| 4 | ShopERP main Web API host; per-tenant SQLite; Products here not in Gateway PG | VERIFIED | `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs`@64789992; ShopERP has 34 controllers | PARTIAL | Products-in-SQLite-only inferred from Option C comments; not table-enumerated. |
| 5 | KhachLink = PWA, no local DB, HTTP via Gateway only | VERIFIED | `grep DbContext\|UseSqlite\|UseNpgsql` over `5_WebApps/KhachLink` → 0 matches @64789992 | PARTIAL | File-level scan only; WASM local storage usage not surveyed. |
| 6 | metering = standalone service, own sln/VPS, caller-key auth | VERIFIED | `8_Metering/vanan-metering/VanAn.Metering.sln`, `src/{Api,Contracts,Domain,Infra}`, `Dockerfile` @64789992 | PARTIAL | Auth headers/wallet semantics read from rules doc, not re-verified in code. |
| 7 | vanan-ai standalone, PG+pgvector, gated P1-G1..G5 | VERIFIED | `9_TenantAI/vanan-ai/src/{Api,Ai,Contracts,Domain}` + Dockerfile @64789992; master plan status (P0–P6 deployed) | PARTIAL | pgvector usage inferred from plan/compose; not code-verified this pass. |
| 8 | NATS `vanan.cloud.order.created.{shopInstanceId}` routed order delivery | VERIFIED | `5_WebApps/ShopERP/Services/OrderSyncSubscriber.cs:50-51`@64789992 (`created` + `status.changed` routed subjects); wildcard sub removed (comment L72-73) | COVERED | — |
| 9 | ShopInstances routing table, multi-VPS | VERIFIED | `1_Shared/Domain/ShopInstance.cs`@64789992; `2_Gateway/Services/ShopErpProductCatalogService.cs` routing usage | PARTIAL | Routing-table contents not enumerated. |
| 10 | Order status sync SQLite→PG reverse direction | VERIFIED | `3_CoreHub/Services/NatsSyncWorker.cs`@64789992 comment "subject prefix separates PG→SQLite (vanan.cloud.*) from SQLite→PG (vanan.shoperp.*)" | PARTIAL | Reverse subjects not enumerated this pass. |
| 11 | `GET /api/catalog/recommended` pure PG | VERIFIED | `2_Gateway/Controllers/CatalogController.cs:18,32`@64789992 — `[Route("api/catalog")]`, `[HttpGet("recommended")]`, union FeaturedProducts + purchase history | COVERED | Note: `ProductsController.cs:163` `/api/products/recommended` is a DIFFERENT endpoint — forwards to ShopERP by tenantId (multi-VPS). Both exist; don't conflate. |
| 12 | YARP product browse forwards → ShopERP | VERIFIED | `2_Gateway/appsettings.json:94-154`@64789992 ReverseProxy Routes/Clusters present | PARTIAL | Route-by-route forward matrix not enumerated. |

## B. Verified Facts Discovered (beyond anchors)

| Claim | Evidence |
|---|---|
| NATS subjects inventory (vanan.cloud.*): order.created.{si}, order.status.changed.{si}, order.payment.confirmed.{si}, tenant.verified, tenant.profile.updated, tenant.type.changed, loyalty.changed.{deviceId}, loyalty.config.changed, notification.created | `grep vanan\.cloud\.` — 78 matches @64789992 |
| Reverse prefix `vanan.shoperp.*` = SQLite→PG direction | `3_CoreHub/Services/NatsSyncWorker.cs`@64789992 |
| Hosted workers/subscribers: Gateway DataSyncSubscriber, EInvoiceSyncSubscriber, NotificationHubNotifier; ShopERP OrderSyncSubscriber, TenantSyncSubscriber, LoyaltySyncSubscriber, PaymentConfirmedSubscriber; CoreHub NatsSyncWorker, JournalPostingRetryWorker, EInvoiceWorker; 7_Tooling CrawlerCoordinator | file list @64789992 |
| Gateway controllers ≈80; ShopERP controllers ≈34 | `find …Controllers` counts @64789992 |
| 5_WebApps has **3 apps**: ShopERP, KhachLink, **Directory** (VanAn.Directory.csproj — SSR directory; not in original anchor list) | `ls 5_WebApps` @64789992 |
| `7_Tooling/VanAn.Crawler` exists with CrawlerCoordinator worker | file list @64789992 |
| docs/ contains ~766 .md files; 11 files contain mermaid blocks | `find/grep` @64789992 |

## C. Discrepancy Log (docs vs code)

| # | Discrepancy | Detail |
|---|---|---|
| D1 | Governance "CoreHub pure class library" vs `OutputType=Exe` + Program.cs | See A-3. Governance text outdated OR undocumented deviation. |
| D2 | Governance "Domain.cs single file" vs `1_Shared/Domain/` folder | See A-1. Location unchanged (`1_Shared`), granularity changed. |
| D3 | Anchor "Products live in ShopERP per-tenant SQLite" vs `/api/products/recommended` forwarding to ShopERP while `/api/catalog/recommended` is pure PG | Two different "recommended" endpoints — doc conflation risk. |

## D. Coverage Statement

Phase 0 coverage = **PARTIAL** overall: anchors verified at file-existence level; deep entity/API enumeration deferred to P3. No claim of full coverage.
