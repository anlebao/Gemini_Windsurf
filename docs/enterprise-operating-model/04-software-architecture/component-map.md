# Component Map — P3

> Registry: `registries/components.yaml`. Deployment boundaries shown as subgraphs. `64789992`.

```mermaid
flowchart TB
    classDef auto fill:#c6f6d5,stroke:#2f855a,color:#22543d
    classDef external fill:#e2e8f0,stroke:#718096,color:#2d3748
    classDef ai fill:#c6f6d5,stroke:#2f855a,stroke-dasharray:5 5,color:#22543d

    subgraph CLIENT["Client tier"]
        KL["SYS-005 KhachLink PWA"]:::auto
        DIR["SYS-006 Directory SSR"]:::auto
        MOB["SYS-016 Mobile apps (PARTIAL)"]:::auto
    end

    subgraph MAINVPS["Main VPS"]
        GW["SYS-002 Gateway — YARP + order creator + API host"]:::auto
        CH["SYS-003 CoreHub — services + worker host"]:::auto
        PG[("SYS-013 PostgreSQL — SoT")]:::auto
    end

    subgraph TENANTVPS["Tenant VPS ×N (per ShopInstance)"]
        SE["SYS-004 ShopERP — POS/kitchen/accounting"]:::auto
        SQ[("SYS-014 SQLite — products + replicas")]:::auto
    end

    subgraph SVC["Standalone services (own VPS)"]
        MET["SYS-007 vanan-metering"]:::auto
        AI["SYS-008 vanan-ai"]:::ai
        AIDB[("PG + pgvector")]:::auto
    end

    NATS["SYS-015 NATS broker"]:::external
    EXT["External: Gemini · Payment · E-invoice · Zalo · CDN"]:::external

    KL -->|HTTP| GW
    DIR -->|HTTP| GW
    MOB -->|HTTP| GW
    GW -->|EF Core| PG
    GW --> CH --> PG
    GW -->|YARP API-113| SE
    GW -->|publish vanan.cloud.*| NATS
    NATS -->|routed {shopInstanceId}| SE
    SE -->|EF Core| SQ
    SE -->|publish vanan.shoperp.*| NATS
    KL & DIR & SE -->|MeteringApiClient ×4| MET
    KL -->|GET /api/public/tenant-ai/*| GW
    GW -->|signed X-Tenant-Ctx API-111| AI
    AI --> AIDB
    AI --> EXT
    CH --> EXT
    SE -->|X-Internal-Api-Key API-109| GW
```

## Boundary facts

- `SYS-002/003/013` co-locate main VPS; `SYS-004/014` replicate per tenant VPS — multi-VPS routing via `ShopInstance` (DATA-002).
- `SYS-007/008` are the only true standalone deployables besides apps — everything else is in-process or hosted app.
- AI boundary (§7.4): `SYS-008` advisory-only; its DB is SoT **only** for AI knowledge (DATA-011), never for business records.
