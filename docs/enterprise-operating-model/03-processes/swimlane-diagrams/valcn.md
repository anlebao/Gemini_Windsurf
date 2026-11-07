# VALCN — Community Commerce (AS-IS, D1 deep-dive)

> Activities → `activities.yaml` ACTV-610..621 (detail) · coverage anchors ACTV-606..608 · RULE-020..025 · CTRL-016..020 · RISK-007..011 · `69496de4`.

```mermaid
flowchart TB
    classDef human fill:#cfe3ff,stroke:#2b6cb0,color:#1a365d
    classDef auto fill:#c6f6d5,stroke:#2f855a,color:#22543d
    classDef error fill:#fed7d7,stroke:#c53030,color:#742a2a

    subgraph OPS["ACTR-014 Vạn An ops (SystemAdmin)"]
        direction TB
        CFG["ACTV-611 Configure commerce mode<br/>(global rates + tenant override)"]:::human
        SPD["ACTV-617 Fund spend +<br/>immutable audit record"]:::human
        ROLE["ACTV-620 Activate/deactivate<br/>Shipper/Salesman role"]:::human
    end

    subgraph ENG["SYS-002/003 Gateway + CoreHub (PG — cross-tenant)"]
        direction TB
        RES["ACTV-610 Resolve mode +<br/>snapshot rates on Order"]:::auto
        ELG["ACTV-619 Eligibility evaluation<br/>(per-tenant thresholds)"]:::auto
        ACC["ACTV-613 Accept -> DeliveryTask<br/>+ order delivering"]:::auto
        TRN["ACTV-614 Transitions<br/>PickedUp->OFD->Delivered/Failed"]:::auto
        GPS["ACTV-615 GPS ping +<br/>LocationHub push"]:::auto
        SPL["ACTV-616 Payment split -><br/>wallet legs + 3-bookset JE"]:::auto
        REF["ACTV-621 Referral attribution<br/>+ commission"]:::auto
    end

    subgraph SHP["ACTR-011 Shipper / ACTR-010 Salesman"]
        direction TB
        BRW["ACTV-612 Nearby orders<br/>(Haversine)"]:::human
        RMT["ACTV-618 COD remittance<br/>(shipper -> shop/platform)"]:::human
    end

    subgraph DB["SYS-013 PG store"]
        WAL[("Wallet txs + DATA-013<br/>community entities")]:::auto
    end

    CFG -->|SystemSetting/override| RES
    RES -->|Reseller order paid| SPL --> WAL
    ROLE --> ELG
    BRW --> ACC --> TRN
    TRN -->|Delivered| SPL
    ACC -->|concurrency guard CTRL-020| ACC
    TRN --> GPS
    TRN -->|orphan task if delegate null — RISK-011| ERR1["warn-log only"]:::error
    SPL -->|balance > spend — RULE-024| SPD --> WAL
    RMT --> WAL
    REF -->|risk score high| ERR2["Rejected/Held"]:::error
```

**Modes (RULE-020):** Marketplace (tenant sells direct) vs Reseller ("Mua giúp — Bán dùm": Vạn An buys from supplier, resells; margin splits into PlatformFee + CommunityFund on snapshot rates). Per-tenant `CommerceModeOverride` wins over global; `Inherit` falls back.

**Fund (RULE-024, CTRL-017):** CommunityFund wallet is system-wide (`TenantId = Guid.Empty`); balance = Σ CommunityFund tx − Σ CommunityFundSpend. Spend blocked when `amount > balance`; every spend → immutable `CommunityFundSpendRecord` (audit). Single-approver = RISK-008.

**Delivery (RULE-022, CTRL-020):** exactly one active DeliveryTask per order (pre-check + unique constraint). Delivered delegates to `OrderWorkflowService` → completed + loyalty + accounting + outbox NATS. Orphan path = warn-log only (RISK-011).

**Cross-tenant by design (RISK-007):** all community services use `IgnoreQueryFilters` — distance/eligibility span tenants. Gates: X-Customer-Token + role checks (CTRL-016).

**Eligibility (RULE-023):** per-tenant thresholds via `ShopFeatureSettings` (R2.1, default 1000 pts + IdentityLevel≥Verified); list uses the more permissive of Shipper/Salesman thresholds. PII never projected (CTRL-018).

**Exceptions:** eligibility fail → reject unless admin `bypassEligibility` · accept on wrong status/taken order → 409 · invalid transition → 409 · overspend → block · referral high-risk → Rejected/Held (FraudReview) · Reseller external payment amount ≠ SellPrice+DeliveryFee → reject.
