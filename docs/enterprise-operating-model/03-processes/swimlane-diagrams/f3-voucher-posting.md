# F3 — Voucher Posting (AS-IS, X1)

> Activities → `registries/activities.yaml` ACTV-307..312. Rules: RULE-010..012. `64789992`.

```mermaid
flowchart TB
    classDef human fill:#cfe3ff,stroke:#2b6cb0,color:#1a365d
    classDef auto fill:#c6f6d5,stroke:#2f855a,color:#22543d
    classDef ai fill:#c6f6d5,stroke:#2f855a,stroke-dasharray:5 5,color:#22543d
    classDef error fill:#fed7d7,stroke:#c53030,color:#742a2a

    subgraph ACCT["ACTR-006 ChiefAccountant (tenant)"]
        direction TB
        ENT["ACTV-307 Enter voucher<br/>(form / Smart Entry text)"]:::human
        REV["ACTV-312 Reversal entry<br/>khi sai (append-only)"]:::human
    end

    subgraph ENG["SYS-003 CoreHub"]
        direction TB
        LLM["ACTV-308 LLM parse enhancement<br/>(fail-closed)"]:::ai
        RES["ACTV-309 ResolveByIntent<br/>→ VoucherTypeDefinition"]:::auto
        STR["ACTV-310 Strategy.PostAsync<br/>→ journal entries"]:::auto
        NUM["ACTV-311 VoucherNumberService<br/>sequence"]:::auto
    end

    subgraph DB["SYS-013/014 Store"]
        JE[("AccountingEntry<br/>IMMUTABLE — DATA-009")]:::auto
    end

    ENT -->|Smart Entry| LLM --> RES
    ENT -->|form direct| RES
    LLM -.->|LLM fail| FP["deterministic parse only"]:::error
    RES -->|unresolved| MJ["manual journal path"]:::error
    RES --> STR --> NUM --> JE
    JE -.->|error found| REV --> JE
```

**Strategies (verified, `VoucherFramework.cs`):** Advance · StockIssue · AssetDisposal · Loan · Depreciation · Deposit + ManualJournalVoucherService. New voucher type = `AddVoucherType(def, strategy, intents)` — RULE-010 cấm page copy-paste.

**Immutability (CTRL-001/013, RULE-012):** AccountingEntry append-only; sửa sai = reversal entry trong cùng pattern — original không bao giờ đổi.

**Critical (§6.5: 1,4,6):** financial record creation + approval/audit + tax exposure.
