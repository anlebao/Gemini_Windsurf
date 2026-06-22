# System Overview

> **High-level architecture của VanAn Ecosystem**
> Reference: ADR-001 (SQLite+NATS), ADR-002 (Multi-tenancy), ADR-003 (Accounting Immutability), ADR-004 (UI Platform)

## Architecture Style

**Modular Monolith + Offline-First** với Clean Architecture layering:

```
Layers (inner → outer):
  Domain → Infrastructure → Services → API
  Dependencies point INWARD only (API → Services → Domain)
```

## Topology

```
                    ┌──────────────────────────┐
                    │       KhachLink (5002)    │
                    │   Customer-facing PWA     │
                    │   (Blazor + HttpClient)   │
                    └────────────┬─────────────┘
                                 │ HTTP
                    ┌────────────▼─────────────┐
                    │      Gateway (5001)       │
                    │   YARP Reverse Proxy      │
                    │   (stateless, no DbContext)│
                    └────────────┬─────────────┘
                                 │ HTTP forward
                    ┌────────────▼─────────────┐
                    │     ShopERP (5003)        │
                    │   Main Web API Host       │
                    │   (Blazor + Controllers   │
                    │    + IVanAnDbContext)     │
                    └────────────┬─────────────┘
                                 │ EF Core
                    ┌────────────▼─────────────┐
                    │      SQLite Database      │
                    │   (per-tenant or shared)  │
                    └──────────────────────────┘
```

## Project Layout

| Project | Role | Constraints |
|---|---|---|
| `1_Shared` | Domain (Single Source of Truth) | Pure: NO EF Core, NO DbContext, NO DataAnnotations |
| `2_Gateway` | Reverse Proxy (YARP) | NO DbContext, NO business logic, NO services |
| `3_CoreHub` | Business Logic + EF Infrastructure | MUST remain Class Library (.dll). NO `<OutputType>Exe</OutputType>` |
| `4_MobileApps` | MAUI Apps (HR, Station) | Mobile clients only |
| `5_WebApps/ShopERP` | Staff UI + Main Web API Host | Injects `IVanAnDbContext` |
| `5_WebApps/KhachLink` | Customer PWA | MUST NOT inject `IVanAnDbContext`. HTTP via Gateway only |
| `UI.Platform` | Reusable UI Components | `VanAnButton`, `VanACard`, `VanAAlert`, `VanAForm`, `VanATable`, `VanAChart` |
| `6_Tests` | Unit + Architecture Tests | xUnit + NetArchTest + FluentAssertions |
| `6_Testing` | E2E Tests | Playwright |

## Hard Stops (Architectural)

1. **3_CoreHub** MUST remain pure Class Library — NO `<OutputType>Exe</OutputType>`
2. **Gateway** MUST remain pure stateless Reverse Proxy — NO DbContext/EF Core
3. **KhachLink** MUST NOT inject `IVanAnDbContext` or query local DBs — HTTP via Gateway only
4. **NO new .csproj files** (e.g., `VanAn.CoreHub.Api`) — use `5_WebApps/ShopERP` as main Web API Host
5. **Domain layer** MUST remain pure — NO EF Core, NO DbContext, NO DataAnnotations
6. **AccountingEntry** MUST be 100% immutable — append-only, corrections via Reversal Entry only
7. **Multi-tenancy** MUST be enforced at every layer — `TenantId` mandatory on all entities

## Data Flow

See `DataFlow.md` for detailed flow (SQLite → Outbox → NATS → PostgreSQL).

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | .NET 8, EF Core 8.0.8, Serilog 4.2.0 |
| Database | SQLite (local/test), PostgreSQL (production) |
| Event Bus | NATS 1.1.6 |
| Real-time | SignalR 8.0.8 |
| Reverse Proxy | YARP 2.3.0 |
| Frontend | Blazor Server/WebAssembly, MAUI 8.0.14 |
| UI Components | UI.Platform (custom, Bootstrap-based) |
| Testing | xUnit 2.9.0, FluentAssertions 6.12.0, Playwright 1.50.0, NetArchTest 1.3.2 |
| Code Analysis | Microsoft.CodeAnalysis 4.8.0, custom Roslyn analyzers VA1001-VA1005 |

## Package Management

- **Central Package Management** enabled (`ManagePackageVersionsCentrally=true`)
- Two sources: `Directory.Build.props` (EF/Microsoft/MAUI) + `Directory.Packages.props` (NATS/Testing/Extensions)
- `<WarningsAsErrors>VA1001;VA1002;VA1003;VA1004;VA1005</WarningsAsErrors>` — custom analyzers enforced as errors

## Deployment

- Docker-based with Nginx reverse proxy + SSL
- CD pipeline: PR #29-#33 merged (smart entrypoint, HTTP-only until SSL cert exists)
- Health endpoints: `/health` on each service

---

*Document Status: Active*
*Last Updated: 2026-06-18*
*Source: PROJECT_CONTEXT.md, Directory.Build.props, Directory.Packages.props, .editorconfig, project_state.md*
