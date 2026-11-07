# F4 — Merchant Onboarding & Tenant Provisioning (AS-IS, Track A)

> Swimlane per SRS §6.2. Activities → `registries/activities.yaml` ACTV-101..115.
> Coverage: PARTIAL — human steps INFERRED (await Q3/Q4); system steps VERIFIED at `64789992`.

```mermaid
flowchart TB
    classDef human fill:#cfe3ff,stroke:#2b6cb0,color:#1a365d
    classDef auto fill:#c6f6d5,stroke:#2f855a,color:#22543d
    classDef hybrid fill:#feebc8,stroke:#c05621,color:#7b341e
    classDef external fill:#e2e8f0,stroke:#718096,color:#2d3748
    classDef error fill:#fed7d7,stroke:#c53030,color:#742a2a
    classDef ai fill:#c6f6d5,stroke:#2f855a,stroke-dasharray:5 5,color:#22543d

    subgraph SALES["ACTR-010 Salesperson"]
        direction TB
        V["ACTV-101 Field visit / contact"]:::human
    end

    subgraph OWNER["ACTR-002 Shop Owner"]
        direction TB
        SUB["ACTV-102 Submit application"]:::human
        DOCS["Provide docs on NeedInfo"]:::human
        FEAT["ACTV-112 Configure feature settings"]:::human
    end

    subgraph SYS["ACTR-020 Gateway + SYS-003 CoreHub"]
        direction TB
        OCR["ACTV-103 OCR extract (optional)"]:::auto
        OTP["ACTV-105 KYC L1 OTP verify"]:::auto
        CT["ACTV-107 Create tenant (PG)"]:::auto
        ASG["ACTV-108 Assign ShopInstance<br/>+ capacity check"]:::auto
        BIND["ACTV-111 Bind KhachLink/domain"]:::auto
        HTX["ACTV-113 MarkAsHtx + publish<br/>tenant.type.changed"]:::auto
    end

    subgraph OPS["ACTR-014 Vạn An Ecommerce operator"]
        direction TB
        REV["ACTV-104 Review app<br/>Approved|NeedInfo|Rejected"]:::human
    end

    subgraph HN["ACTR-012 Hạt Nhân"]
        direction TB
        KYC4["ACTV-106 KYC L4 manual verify"]:::human
    end

    subgraph SYNC["SYS-009/015 NATS + ShopERP"]
        direction TB
        EVT["ACTV-109 publish tenant.verified"]:::auto
        MIR["ACTV-110 SQLite mirror<br/>Pending→Active"]:::auto
    end

    V --> SUB --> OCR --> OTP --> REV
    REV -->|NeedInfo| DOCS --> REV
    REV -->|Rejected| RJ["Application closed"]:::error
    REV -->|Approved| CT
    OTP -.->|insufficient| KYC4 -.-> REV
    CT --> ASG --> BIND --> EVT --> MIR --> FEAT
    ASG -->|instance full/inactive| FULL["InvalidOperation →<br/>rebalance ShopInstance"]:::error
    BIND --> HTX
    HTX --> EVT
```

## Exception paths (SRS §6.3)

| Trigger | Path |
|---|---|
| Application Rejected | terminal — owner notified (mechanism TBD, INFERRED) |
| NeedInfo | loops back to owner for documents → re-review |
| ShopInstance full/inactive | `InvalidOperationException` — operator rebalances/assigns other instance (RULE-003) |
| NATS down at tenant.verified | outbox retry (pattern exists in codebase, confidence V) |
| OCR failure | manual entry fallback |
| Suspend/deactivate | ACTV-115 — blocks ops, reversible via reactivate |

## Variants (§6.6)

- Business-type differences (F&B vs spa vs retail) mostly = CONFIGURABLE via `TenantFeatureSettings` (ACTV-112), not process forks.
- `TenantType` (HKD vs Enterprise vs HTX) → VARIANT at accounting-regime level (RULE-004), not onboarding level.

## Open items (INFERRED — need owner, Q3/Q4)

- Who performs application review today — Ecommerce ops vs delegated HTX? (ACTV-104 marked ACTR-014, confidence V on workflow, I on actor)
- Is there a contract/signing step between approval and provisioning (not visible in code)?
- Hạt Nhân role at L4 verification — per membership SRS, not code-verified.
