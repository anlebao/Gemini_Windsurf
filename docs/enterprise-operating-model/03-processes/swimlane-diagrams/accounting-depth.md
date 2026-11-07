# Accounting Depth — Close · Statements · Reconcile (AS-IS, D3 deep-dive)

> Activities → `activities.yaml` ACTV-629..640 · PROC-171/172/173/174 · RULE-029..032 · CTRL-023..025 · RISK-014..016 · `58d3d3d6`.
> Stores: accounting = PG via `IAccountingDbContext` (both `AccountingEntry` single-entry DATA-009 and `JournalEntry` double-entry DATA-015 coexist by regime).

```mermaid
flowchart TB
    classDef human fill:#cfe3ff,stroke:#2b6cb0,color:#1a365d
    classDef auto fill:#c6f6d5,stroke:#2f855a,color:#22543d
    classDef error fill:#fed7d7,stroke:#c53030,color:#742a2a

    subgraph OWN["ACTR-002 Owner / ACTR-006 ChiefAccountant"]
        direction TB
        STD["ACTV-630 Set accounting standard<br/>(write-path validated)"]:::human
        CLS["ACTV-637 Close period"]:::human
        ROP["ACTV-638 Reopen period<br/>(3 approver roles + reason)"]:::human
    end

    subgraph ADM["ACTR-015 SystemAdmin"]
        REC["ACTV-639 Run reconcile<br/>R1-R13 (read-only)"]:::human
    end

    subgraph ENG["SYS-003/004 CoreHub + ShopERP"]
        direction TB
        RES["ACTV-629 Resolve effective standard<br/>(regime-lock RULE-029)"]:::auto
        HKD["ACTV-631 HKD books S1a-S3a<br/>(TT152, template engine)"]:::auto
        TB["ACTV-632 Trial balance"]:::auto
        GL["ACTV-633 Journal + So Cai"]:::auto
        FS["ACTV-634 B01/B02/B03 + B09"]:::auto
        VAL["ACTV-635 Validate pre-close<br/>(errors block, warnings pass)"]:::auto
        KET["ACTV-636 Closing JEs 911<br/>(accrual-only, idempotent)"]:::auto
        MIR["ACTV-640 Mirror + inventory<br/>providers (fail-open)"]:::auto
    end

    subgraph DB["SYS-013 PG"]
        JE[("JournalEntry · DATA-015")]:::auto
        AE[("AccountingEntry · DATA-009 IMMUTABLE")]:::auto
    end

    RES --> STD
    RES -->|HKD (no BCTC unless TT58)| HKD
    RES -->|TT133/99/71| TB --> GL --> FS
    REC -->|error checks feed| VAL
    MIR --> REC
    VAL -->|errors| BLK["close blocked"]:::error
    VAL -->|pass| KET --> CLS
    CLS --> JE
    ROP -->|reverses closing JEs| JE
    ROP -.->|reversal-only, RULE-031| AE
```

**Regime-lock (RULE-029, CTRL-023):** `AccountingStandardRules` (1_Shared, dùng chung UI+write-path) — HTX→TT71 hard · DN lớn→TT99 · DN vừa→TT133 · DN siêu nhỏ→TT58 default + TT133 option · HKD→`AllowedStandards=[]` (sổ TT152 riêng, KHÔNG BCTC) cho tới khi bầu chọn TT58 · legacy `Type=null` → all 4 (RISK-014).

**Period close (CTRL-024):** `ValidatePeriodAsync` — no entries → error · pending `Amount=0` → block · reconcile Error checks → errors, Warning → pass-with-note · reconcile crash → warning-only fail-safe. `ClosePeriodAsync` → closing JEs (RULE-030: accrual regimes TT133/TT99/TT71 only; HKD+DNSN skip; idempotent) → `MarkClosed` persisted → `LogPeriodCloseAsync` audit.

**Reopen (RULE-031):** roles `Owner|ChiefAccountant|SystemAdmin` + mandatory reason → status Closed→Reopening → reverse closing JEs → MarkReopened → audit.

**Reconcile (RULE-032):** R1 journal cân bằng · R2 trial balance · R3 rev/exp đồng bộ · R4 balance identity · R5 sổ cái · R6 nhật ký · R7 HKD books · R8 single-source bridge (R8a legacy `ref 'Order'` cutoff 2026-10-29 / R8b exactly-1 journal per paid order) · R9 deposit 3387 · R10 order→entries · R11 cross-store PG↔SQLite · R12 TransactionDate · R13 inventory↔GL. Read-only + per-check fail-open; SystemAdmin-only; audit summary logged (CTRL-025). R11/R13 silent-skip risk = RISK-016.
