# System Overview

> **High-level architecture của VanAn Ecosystem**
> Reference: ADR-001 (SQLite+NATS — partially superseded by Option C), ADR-002 (Multi-tenancy), ADR-003 (Accounting Immutability), ADR-004 (UI Platform)
> Option C (approved 2026-07-18): Gateway = Order Creator + Routed Async Delivery — `docs/AI/tasks/archive/gateway_infra/gateway_router_multi_vps_master_plan.md`

## Architecture Style

**Distributed modular monolith (multi-VPS)** với Clean Architecture layering:

```
Layers (inner → outer):
  Domain → Infrastructure → Services → API
  Dependencies point INWARD only (API → Services → Domain)
```

**Data ownership split (Option C):**
- **PostgreSQL trên Gateway** = source of truth cho platform data: Orders, Accounting, Tenants, ShopInstances, Users, FeaturedProducts, Loyalty…
- **SQLite per-tenant trên mỗi ShopERP** = business data: Products, Inventory, + order replica cho kitchen/POS display (không phải source)
- Orders flow **PG → NATS routed → SQLite**; status updates flow ngược lại **SQLite → PG**

## Topology

```
Customer Browser
       │ (PWA — Blazor WASM, nginx-served static)
       ▼ HTTPS
┌─────────────────────────────────────────────┐
│  Gateway VPS (5001) — vanan-gateway          │
│  • API host: ~77 controllers (Orders,        │
│    Accounting, Tenants, Loyalty, Booking…)   │
│  • EF Core → PostgreSQL (source of truth)    │
│  • SignalR hubs + Redis backplane            │
│  • YARP: forward catalog browse + static     │
│    assets → ShopERP                          │
└──────┬──────────────────┬────────────────────┘
       │ EF Core          │ NATS (routed)
       ▼                  │ vanan.cloud.order.created.{shopInstanceId}
┌──────────────┐          ▼
│ PostgreSQL   │   ┌──────────────────────────┐
│ (platform)   │   │  ShopERP VPS ×N (5003)    │
└──────────────┘   │  • Blazor Server UI + API │
                   │  • ShopERPDbContext (     │
                   │    SQLite per-tenant)     │
                   │  • VanAnDbContext (PG)    │
                   │  • Kitchen/POS display    │
                   └──────────────────────────┘

┌──────────────────────────┐   ┌──────────────────────────┐
│  Directory (timlathay)   │   │  Crawler (7_Tooling)     │
│  Blazor SSR + Interactive│   │  Worker: crawl → Gateway │
│  Server, Directory-       │   │  API (GDT, TrangVang…)   │
│  profile tenants          │   └──────────────────────────┘
└──────────────────────────┘
```

**Production:** 4 GCP VPS (`vanan-prod`): `vanan-gateway` · `vanan-shop-a` · `vanan-khachlink` · `vanan-khachlink-20260815-timlathay-com`. Multi-ShopERP supported qua `ShopInstances` routing table.

## Project Layout

| Project | Role | Constraints |
|---|---|---|
| `1_Shared` | Domain (Single Source of Truth) | Pure: NO EF Core, NO DbContext, NO DataAnnotations |
| `2_Gateway` | API Host + Order Creator + YARP forward | Option C — owns `VanAnDbContext` (PG). NOT "pure proxy" (rule rescinded 2026-07-18) |
| `3_CoreHub` | Business Logic + EF Infrastructure | MUST remain Class Library (.dll). NO `<OutputType>Exe</OutputType>` |
| `4_MobileApps` | MAUI skeletons (HR, Station) | **NOT in VanAn.sln** — not built |
| `5_WebApps/ShopERP` | Staff UI + per-tenant SQLite host | `ShopERPDbContext` (SQLite) + `VanAnDbContext` (PG) |
| `5_WebApps/KhachLink` | Customer PWA (Blazor WASM) | MUST NOT inject `IVanAnDbContext`. HTTP via Gateway only |
| `5_WebApps/Directory` | Directory-profile tenants | Blazor Web App (SSR + InteractiveServer) |
| `VanAn.Accounting` | Custom Roslyn analyzers (VA1001-VA1005) | Analyzer project, not runtime |
| `7_Tooling/VanAn.Crawler` | Business-data crawl worker | Publishes to Gateway API; deployed as `vanan-crawler` image |
| `UI.Platform` | Reusable UI Components | `VanAnButton`, `VanACard`, `VanAAlert`, `VanAForm`, `VanATable`, `VanAChart` |
| `6_Tests` | 6 test projects: Architecture, Core, E2E, Integration, Load, ShopERP | xUnit + NetArchTest + FluentAssertions + Playwright + bUnit |
| `6_Testing` | E2E specs + RV scripts | Playwright |

## Hard Stops (Architectural)

1. **3_CoreHub** MUST remain pure Class Library — NO `<OutputType>Exe</OutputType>`
2. **Gateway = Order Creator + Routed Async Delivery (Option C)** — Gateway PG is source of truth for Orders + Accounting + Tenants + ShopInstances + Users + FeaturedProducts. YARP forwards only select traffic (static assets, KhachLink PWA, product catalog browse). Prior "pure proxy" rule rescinded.
3. **KhachLink** MUST NOT inject `IVanAnDbContext` or query local DBs — HTTP via Gateway only
4. **NO new API-host .csproj** (e.g., `VanAn.CoreHub.Api`) — existing hosts: Gateway (platform API), ShopERP (staff/tenant ops), Directory (public directory)
5. **Domain layer** MUST remain pure — NO EF Core, NO DbContext, NO DataAnnotations
6. **AccountingEntry** MUST be 100% immutable — append-only, corrections via Reversal Entry only
7. **Multi-tenancy** MUST be enforced at every layer — `TenantId` mandatory on all tenant-scoped entities
8. **Single-Identity Pattern** — `BaseEntity.Id` (PK) is the ONLY identity; business key VOs are `builder.Ignore()`-d, never mapped to columns

## Data Flow

See `DataFlow.md` — Option C: PG create → NATS routed → SQLite replica (orders), SQLite → PG (status).

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | .NET 8, EF Core 8.0.8, Serilog 4.2.0 |
| Database | PostgreSQL 15 (Gateway platform, Npgsql 8.0.8) + SQLite per-tenant (ShopERP business) |
| Event Bus | NATS 2.10 (`NATS.Client` 1.1.6) — routed subjects `vanan.cloud.*` |
| Real-time | SignalR 8.0.8 + StackExchangeRedis backplane 8.0.30 |
| Reverse Proxy | YARP 2.3.0 (select routes only — not full forwarding) |
| Frontend | Blazor Server (ShopERP), Blazor WASM (KhachLink PWA), Blazor SSR+InteractiveServer (Directory), MAUI 8.0.14 (skeletons, not in sln) |
| UI Components | UI.Platform (custom, Bootstrap-based) |
| Supporting | UUIDNext (UUIDv7 ids), AWSSDK.S3 (R2 image storage), EPPlus (Excel export), Swashbuckle, DataProtection |
| Testing | xUnit 2.9.0, FluentAssertions 6.12.0, Playwright 1.50.0, NetArchTest 1.3.2, bUnit |
| Code Analysis | `VanAn.Accounting` Roslyn analyzers VA1001-VA1005 (enforced as errors), Microsoft.CodeAnalysis 4.8.0 |
| Observability | Seq (centralized logs), `/health` endpoints |

## Package Management

- **Central Package Management** enabled (`ManagePackageVersionsCentrally=true`)
- Two sources: `Directory.Build.props` (EF/Microsoft/MAUI) + `Directory.Packages.props` (NATS/Testing/Extensions)
- `<WarningsAsErrors>VA1001;VA1002;VA1003;VA1004;VA1005</WarningsAsErrors>` — custom analyzers enforced as errors

## Deployment

- Docker + GHCR images (`ghcr.io/anlebao/vanan-{gateway,shoperp,khachlink,crawler}`), nginx reverse proxy + certbot SSL
- **CD: `.github/workflows/cd-multivps.yml`** — push to `main` → deploy all VPS + smoke tests (impact-analysis + ensure_image re-tag pattern)
- Compose per VPS: `docker-compose.gateway.yml` · `.shoperp.yml` · `.khachlink.yml` · `.directory.yml` · `.infra.yml` (postgres+nats+seq) · `docker-compose.prod.yml` (legacy single-VPS reference)
- Legacy `cd.yml` (`oracle-prod`) — SSH broken since 2026-08-06, do not use
- Health endpoints: `/health` on each service

---

*Document Status: Active*
*Last Updated: 2026-10-19*
*Source: VanAn.sln, *.csproj, Program.cs (Gateway/ShopERP/Directory), docker-compose.*.yml, governance.md (Option C), project_state.md*
