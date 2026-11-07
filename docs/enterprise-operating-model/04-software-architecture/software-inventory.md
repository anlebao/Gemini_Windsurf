# Software Inventory — P3

> Data source: `registries/components.yaml` (16 SYS). Enrichment at `64789992`.
> Boundary classes: IN_PROCESS · HOSTED · STANDALONE_VPS · EXTERNAL.

## Deployable units

| Unit | SYS | Deploys as | DB touch |
|---|---|---|---|
| Gateway | SYS-002 | ASP.NET host, main VPS | PG (SoT) via VanAnDbContext |
| CoreHub | SYS-003 | Library + generic Exe host (workers, migrations) — drift D1 | PG |
| ShopERP | SYS-004 | ASP.NET host per ShopInstance VPS | SQLite per-tenant |
| KhachLink | SYS-005 | Blazor WASM PWA | none (HTTP only — verified 0 DbContext refs) |
| Directory | SYS-006 | SSR host | via Gateway APIs |
| vanan-metering | SYS-007 | standalone sln, own VPS | own DB (wallets/usage) |
| vanan-ai | SYS-008 | standalone sln, own VPS | PG + pgvector |
| NATS | SYS-015 | external broker | — |
| Crawler | SYS-010 | tooling worker | TBD (Q5) |
| Mobile apps | SYS-016 | 4_MobileApps | NOT-SURVEYED |

## In-process libraries

UI.Platform (SYS-011) · VanAn.Accounting (SYS-012 — boundary vs CoreHub accounting TBD) · VanAn.sln core/1_Shared (SYS-001) · Sync layer (SYS-009, hosted inside Gateway/ShopERP processes).

## Notable structure (verified)

- **PG DbContext lives in CoreHub Infrastructure** (`3_CoreHub/Infrastructure/VanAnDbContext.cs`) — consumed by Gateway host; CoreHub also runs standalone host for `NatsSyncWorker`, `JournalPostingRetryWorker`, `EInvoiceWorker`.
- **4 MeteringApiClient copies** exist: `2_Gateway`, `5_WebApps/Directory`, `5_WebApps/ShopERP`, `9_TenantAI` — same pattern duplicated per caller (drift-dedup candidate for P4, not a bug).
- Controllers: Gateway ≈80, ShopERP ≈34 — API surface is large; `apis.yaml` currently catalogs the flow-relevant subset (P3 scope), full enumeration is open-ended → drift detector (X0) will cover breadth.
