# F1 — Order Lifecycle AS-IS (Track B, Option C)

> Swimlane per SRS §6.2. Activities → `registries/activities.yaml` ACTV-201..212.
> Confidence: VERIFIED for system steps at `64789992`; INFERRED for staff UI actions.

```mermaid
flowchart TB
    classDef human fill:#cfe3ff,stroke:#2b6cb0,color:#1a365d
    classDef auto fill:#c6f6d5,stroke:#2f855a,color:#22543d
    classDef hybrid fill:#feebc8,stroke:#c05621,color:#7b341e
    classDef external fill:#e2e8f0,stroke:#718096,color:#2d3748
    classDef error fill:#fed7d7,stroke:#c53030,color:#742a2a
    classDef ai fill:#c6f6d5,stroke:#2f855a,stroke-dasharray:5 5,color:#22543d

    subgraph CUST["ACTR-001 Customer (KhachLink)"]
        direction TB
        BR["ACTV-201 Browse + cart"]:::human
        CO["ACTV-202 Submit checkout<br/>(snapshot ProductName+VatRate)"]:::human
        TRK["ACTV-210 Track status"]:::human
    end

    subgraph GW["ACTR-020 Gateway + PG"]
        direction TB
        CRT["ACTV-203 Validate +<br/>create order in PG"]:::auto
        RTE["ACTV-204 Resolve ShopInstanceId<br/>+ publish order.created"]:::auto
        PAY["ACTV-212 payment.confirmed event"]:::auto
    end

    subgraph NATS["SYS-015 NATS"]
        direction TB
        MSG["vanan.cloud.order.created.{si}"]:::auto
        STS["vanan.cloud.order.status.changed.{si}<br/>↔ vanan.shoperp.* back-sync"]:::auto
    end

    subgraph SHOP["SYS-004 ShopERP (tenant SQLite)"]
        direction TB
        REC["ACTV-205 Persist order replica"]:::auto
        NTF["ACTV-206 Notify POS/kitchen"]:::auto
        CONF["ACTV-207 Staff confirms"]:::human
        KIT["ACTV-208 Kitchen prepares"]:::human
    end

    subgraph ENG["SYS-003 CoreHub"]
        direction TB
        LOY["ACTV-211 Loyalty accrual<br/>loyalty.changed.{deviceId}"]:::auto
    end

    BR --> CO --> CRT --> RTE --> MSG --> REC --> NTF --> CONF --> KIT
    KIT --> STS
    STS --> TRK
    PAY --> STS
    CONF -->|reject| CAN["Cancelled path"]:::error
    MSG -->|no ShopInstance route| RF["Routing fail"]:::error
    CRT --> LOY
```

## Sequence view (technical — complements swimlane, SRS §6.2 rule 3)

```mermaid
sequenceDiagram
    participant C as KhachLink (ACTR-001)
    participant GW as Gateway API-103 (ACTR-020)
    participant PG as PostgreSQL (SYS-013)
    participant N as NATS (SYS-015)
    participant SE as ShopERP (ACTR-021)
    participant SQ as SQLite (SYS-014)

    C->>GW: POST /api/public/orders/checkout (snapshot ProductName+VatRate)
    GW->>PG: CreateOrderFromCommandAsync → Order (SoT, DATA-004)
    GW->>N: publish vanan.cloud.order.created.{shopInstanceId}
    N->>SE: deliver (routed by ShopInstanceId — RULE-002)
    SE->>SQ: upsert order replica (DATA-005)
    SE-->>SE: POS/kitchen display
    SE->>N: status changes → vanan.cloud.order.status.changed.{si}
    SE->>N: back-sync → vanan.shoperp.* → PG
    C->>GW: GET /api/public/orders/{id} → status
```

## Exception paths (SRS §6.3)

| Trigger | Path |
|---|---|
| Invalid checkout payload | 4xx — no order created |
| Missing ShopInstance route | order stays PG-only — routing fail (error, needs monitoring) |
| NATS down | outbox/retry pattern (VERIFIED pattern in code) |
| Duplicate event delivery | idempotent upsert in subscriber |
| Order rejected by shop | cancelled status → sync back |
| Payment confirmed later | separate event `payment.confirmed.{si}` |

## Boundaries

- **SoT:** Order lives in PG (DATA-004); ShopERP copy is replica for display (DATA-005). Status transitions originate at ShopERP → sync back (verified direction comments).
- **RULE-001:** Gateway never queries Products at checkout — client snapshot is authoritative for the order record.
