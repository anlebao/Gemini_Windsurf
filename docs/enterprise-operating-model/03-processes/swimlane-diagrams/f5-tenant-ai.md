# F5 — Vạn An AI (`vanan-ai`) Deep Model (AS-IS, X3)

> Activities → `activities.yaml` ACTV-401..410 · Rules RULE-013..018 · Controls CTRL-004/005/014/015.
> Boundary §7.4 + AC-13: advisory-only, tenant-isolated, metered, approved-knowledge-only. `64789992`.

```mermaid
flowchart TB
    classDef human fill:#cfe3ff,stroke:#2b6cb0,color:#1a365d
    classDef auto fill:#c6f6d5,stroke:#2f855a,color:#22543d
    classDef ai fill:#c6f6d5,stroke:#2f855a,stroke-dasharray:5 5,color:#22543d
    classDef error fill:#fed7d7,stroke:#c53030,color:#742a2a

    subgraph CUST["ACTR-001 Customer"]
        direction TB
        ASK["ACTV-401 Ask via AiChatWidget"]:::human
        CART["ACTV-407 Suggestion -> cart<br/>(AI never orders, RULE-017)"]:::human
    end
    subgraph GW["ACTR-020 Gateway"]
        SIGN["ACTV-402 Sign X-Tenant-Ctx<br/>(HMAC, RULE-013)"]:::auto
    end
    subgraph AI["ACTR-030 vanan-ai (SYS-008)"]
        direction TB
        MET["ACTV-403 Metering check<br/>(RULE-014 degrade)"]:::auto
        FLT["ACTV-404 Intent + policy +<br/>permission filter (CTRL-014)"]:::ai
        RET["ACTV-405 SQL+RAG retrieval<br/>(RULE-016, CTRL-015)"]:::ai
        ANS["ACTV-406 LLM draft +<br/>Answer Validator"]:::ai
        SES["ACTV-410 Session persist<br/>(90d, cap 20/h)"]:::auto
    end
    subgraph OPS["ACTR-015 Vạn An operator"]
        APPR["ACTV-409 Approve knowledge<br/>pending_review→approved (CTRL-004)"]:::human
    end
    subgraph ING["Ingest"]
        SYNC["ACTV-408 products:batch /<br/>documents -> pending_review"]:::auto
    end

    ASK --> SIGN --> MET --> FLT --> RET --> ANS --> CART
    MET -->|deny/fail| DEG["degrade structured-only<br/>ai.chat.degraded MeterOnly"]:::error
    RET -.->|Confidential class| NORE["extraction path only"]:::error
    SYNC --> APPR --> RET
    ANS --> SES
```

## AI boundary summary (AC-13 per activity)

| Activity | Advisory/Executing | Authoritative source | Isolation | Metering |
|---|---|---|---|---|
| ACTV-403 check | executing (gate) | metering service | caller+ctx | ai.chat.check |
| ACTV-404/405/406 | advisory generation | structured > vector (RULE-016) | signed ctx + approved-only | ai.chat.message |
| ACTV-407 cart | human executes | order flow F1 | — | — |
| ACTV-409 approval | human authority | knowledge registry | tenant scope | — |

## Governance (§10.2)

- Knowledge owner = **tenant**; infra owner = Vạn An; accountability for AI output follows whoever turns suggestion → action (RULE-017 keeps AI out of record creation).
- Quota = governance control (cost boundary), not just tech limit — degrade is deliberate policy (RULE-014), documented GAP-014.
- Verified endpoints: `/v1/chat`, `/v1/chat/sessions/{sid}/messages`, `/v1/config/widget`, `/v1/knowledge/*`, `/v1/admin/knowledge/{pending,approve}`, `/v1/auth/*` (`Program.cs`@64789992).
