# F6 — Loyalty Accrual & Redemption (AS-IS, X2)

> Activities → `activities.yaml` ACTV-501..505 · RULE-019 · `64789992`.

```mermaid
flowchart TB
    classDef human fill:#cfe3ff,stroke:#2b6cb0,color:#1a365d
    classDef auto fill:#c6f6d5,stroke:#2f855a,color:#22543d
    classDef error fill:#fed7d7,stroke:#c53030,color:#742a2a
    subgraph PLAT["SYS-002/003 Gateway+CoreHub (PG)"]
        direction TB
        ACC["ACTV-501 Accrue on order event<br/>(+outbox fallback)"]:::auto
        BUD["ACTV-505 Budget control"]:::auto
        CAT["RedemptionController +<br/>GlobalRedemptionCatalog"]:::auto
    end
    subgraph OPS["ACTR-014 Vạn An ops"]
        CFG["ACTV-503 Configure rules -><br/>loyalty.config.changed"]:::human
    end
    subgraph SHOP["SYS-004 ShopERP (SQLite)"]
        MIR["ACTV-502 Mirror balance<br/>(wildcard loyalty.changed.>)"]:::auto
    end
    subgraph CUST["ACTR-001 Customer"]
        RED["ACTV-504 Redeem via catalog"]:::human
    end
    ACC --> MIR
    CFG --> MIR
    RED --> BUD -->|allow| CAT
    BUD -->|over-budget| HOLD["hold/deny"]:::error
```

**Notes:** points accrue AUTOMATED từ order events; mirror wildcard sub (`loyalty.changed.>`) cho offline display; budget semantics INFERRED (GAP-015 — verify sau). SoT: PG wallet/loyalty; SQLite = mirror.
