# F2 — Metering "Phí nền tảng" (AS-IS, X1)

> Activities → `registries/activities.yaml` ACTV-301..306. Rules: RULE-007..009. Controls: CTRL-003/011/012. `64789992`.

```mermaid
flowchart TB
    classDef human fill:#cfe3ff,stroke:#2b6cb0,color:#1a365d
    classDef auto fill:#c6f6d5,stroke:#2f855a,color:#22543d
    classDef error fill:#fed7d7,stroke:#c53030,color:#742a2a

    subgraph CALLER["Caller (SYS-002/004/006/008 ×4 clients)"]
        direction TB
        CHK["ACTV-301 Check quota<br/>(X-Caller-Id + X-Metering-Key)"]:::auto
        OP["Execute metered op"]:::auto
        ING["ACTV-305 Ingest usage event<br/>+ Idempotency-Key"]:::auto
    end

    subgraph MET["SYS-007 vanan-metering"]
        direction TB
        DENY["ACTV-302 Deny gate<br/>(Enforcement=Deny && balance<req)"]:::auto
        DED["applied=min(req,bal)<br/>excess→UnpaidCredits"]:::auto
        ALR["ACTV-306 Alerts 50/10/0 per CycleKey"]:::auto
    end

    subgraph OWNER["ACTR-002 Shop Owner"]
        direction TB
        TOP["ACTV-303 Request top-up<br/>TOPUP-XXXX pending"]:::human
    end

    subgraph OPS["ACTR-014 Vạn An ops"]
        direction TB
        CNF["ACTV-304 Confirm → grant<br/>+ cycle reset (idempotent)"]:::human
    end

    CHK -->|allow| OP --> ING --> DED --> ALR
    CHK -->|deny| DENY --> BLK["Op blocked + wallet banner"]:::error
    BLK --> TOP --> CNF --> CHK
    ING -->|replay same key| DED
```

**Exception paths:** check fail → caller policy (fail-open default; vanan-ai degrade structured-only — GAP-014 by design) · replay idempotency → event cũ (CTRL-011) · double-confirm → unique partial index blocks (RULE-008) · Pending>72h → lazy Expired · unpriced serviceCode → no deduct.

**Critical points (§6.5: 1,4,5,6):** deduct trong CÙNG tx với event (RULE-009); ledger append-only (CTRL-012); deny gate là revenue gate.
