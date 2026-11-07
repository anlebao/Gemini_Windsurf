# Vạn An Ecosystem — Context Diagram (L0)

> Phase 1 output · captured at `64789992`. Actors/components referenced by registry ID.

```mermaid
flowchart TB
    classDef human fill:#cfe3ff,stroke:#2b6cb0,color:#1a365d
    classDef auto fill:#c6f6d5,stroke:#2f855a,color:#22543d
    classDef hybrid fill:#feebc8,stroke:#c05621,color:#7b341e
    classDef external fill:#e2e8f0,stroke:#718096,color:#2d3748
    classDef error fill:#fed7d7,stroke:#c53030,color:#742a2a
    classDef ai fill:#c6f6d5,stroke:#2f855a,stroke-dasharray:5 5,color:#22543d

    subgraph ECO["ORG-001/002 — Van An Platform (Technology + Ecommerce)"]
        GW["SYS-002 Gateway — order creator + YARP + PG SoT"]:::auto
        CH["SYS-003 CoreHub — business services + workers host"]:::auto
        MET["SYS-007 vanan-metering — wallet/quota/deny"]:::auto
        AI["SYS-008 vanan-ai — tenant AI assistant"]:::ai
        NATS["SYS-015 NATS — routed events"]:::auto
    end

    subgraph TENANT["ORG-005 Merchant tenant (per ShopInstance, multi-VPS)"]
        SE["SYS-004 ShopERP — POS/kitchen/accounting (SQLite)"]:::auto
        OWN["ACTR-002..008 — Owner & staff roles"]:::human
    end

    subgraph HTX["ORG-003/004 — HTX & Hat Nhan"]
        HN["ACTR-012 Hat Nhan rep"]:::human
        MEM["ACTR-013 HTX members"]:::human
    end

    CUST["ACTR-001 Customer — KhachLink PWA"]:::human
    CTV["ACTR-009 CTV / Collaborator"]:::human
    SALES["ACTR-010 Salesperson"]:::human
    DIR["SYS-006 Directory — SSR storefront dir"]:::auto

    EXT["ACTR-040..044 — Gemini · Payment · E-invoice · Zalo · CDN"]:::external
    GOV["ORG-007 Government / regulators"]:::external

    CUST -->|browse/order/chat| GW
    CUST --> DIR
    CTV --> GW
    SALES -->|onboard merchant| GW
    GW --> NATS
    NATS --> SE
    SE --> OWN
    GW --> CH
    GW --> MET
    GW --> AI
    AI --> EXT
    CH --> EXT
    HN -->|support/verify| OWN
    MEM --> SE
    SE -.->|status sync back| NATS
    GOV <-.->|e-invoice/tax| CH
```

## Reading guide

- **Option C data spine:** `Customer → Gateway (PG SoT) → NATS vanan.cloud.order.created.{shopInstanceId} → ShopERP` ; status sync returns via `vanan.shoperp.*`.
- **Tenant boundary:** each merchant = separate SQLite + ShopInstance routing entry; HTX = tenant class with HtxProfile + TT71 regime.
- **AI boundary (§7.4):** vanan-ai is advisory — suggests into existing flows; never creates orders/records; metered (`ai.chat.message`).
- Registry IDs on nodes are navigable: look up `SYS-004` in `registries/components.yaml`, `ACTR-002` in `registries/actors.yaml`.
