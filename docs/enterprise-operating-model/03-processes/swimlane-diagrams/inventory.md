# Inventory — Stock · Count · Intelligence (AS-IS, D2 deep-dive)

> Activities → `activities.yaml` ACTV-641..650 · PROC-161..165 · RULE-033..036 · CTRL-026..028 · RISK-017..019 · `f447a1a8`.
> Store split: PurchaseReceipt = PG voucher · stock entities = **SQLITE_TENANT** (DATA-016) — link by Id only, no FK (RULE-036).

```mermaid
flowchart TB
    classDef human fill:#cfe3ff,stroke:#2b6cb0,color:#1a365d
    classDef auto fill:#c6f6d5,stroke:#2f855a,color:#22543d
    classDef error fill:#fed7d7,stroke:#c53030,color:#742a2a

    subgraph STF["ACTR-003 StoreKeeper / ACTR-002 Owner"]
        direction TB
        MAN["ACTV-643 Manual in/out"]:::human
        CNT["ACTV-644 Shift count<br/>(Draft/Submitted only)"]:::human
        RCP["ACTV-650 Ingredient +<br/>recipe management"]:::human
    end

    subgraph SHOP["SYS-004 ShopERP host (SQLite — stock writes ONLY here)"]
        direction TB
        RCV["ACTV-641 Receipt post -><br/>PurchaseIn + snapshot"]:::auto
        RTN["ACTV-642 Return -><br/>PurchaseReturn"]:::auto
        ALT["ACTV-645 Shift-close<br/>alert engine (10 rules)"]:::auto
        RPT["ACTV-647 Nhap-Xuat-Ton<br/>report (baseline rebuild)"]:::auto
        VAL["ACTV-648 Valuation snapshot<br/>-> reconcile R13"]:::auto
        FRC["ACTV-649 Forecast +<br/>variance analysis"]:::auto
    end

    subgraph CORE["SYS-003 CoreHub (PG voucher)"]
        DED["ACTV-646 Order deduction check<br/>(recipe -> ingredient)"]:::auto
        PR[("PurchaseReceipt voucher")]:::auto
    end

    subgraph DB["SYS-014 SQLite (DATA-016)"]
        MOV[("StockMovement — audit SoT")]:::auto
        ING[("Ingredient.CurrentStock = snapshot")]:::auto
        ALR[("ShiftAlert")]:::auto
    end

    PR -->|optional dep — ShopERP only, RULE-033| RCV
    PR -->|return| RTN
    RCV --> MOV
    RTN --> MOV
    MAN --> MOV
    MOV --> ING
    CNT --> ALT --> ALR
    DED -.->|short -> cannot fulfill| ERR["order blocked"]:::error
    RPT --> MOV
    VAL --> ING
    FRC --> ALT
    MAN -.->|out > stock| ERR2["rejected"]:::error
    PR -.->|host w/o dep — RISK-017| ERR3["stock silently skipped"]:::error
```

**Voucher link (RULE-033/036):** receipt post on PG → `ApplyReceiptStockAsync` (PurchaseIn + `CurrentStock` += + `PricePerUnit` = latest + `StockAppliedAt`); return → `ApplyReturnStockAsync` (negative allowed — consumed-before-return = RISK-018). Idempotent per (receiptId, lineId, type) — CTRL-026.

**Stock semantics (RULE-035):** `StockMovement` = audit SoT; `CurrentStock` snapshot only. Report baseline = `CurrentStock − Σmovements` → opening correct even for pre-table stock. Manual-out > stock → rejected; return may go negative.

**Count & alerts (RULE-034, CTRL-027):** `InventoryCount` editable only while shift Draft/Submitted (blocked after Closed, NFR-7); shift close → AlertEngine 10 rules (ING_VARIANCE_HIGH, STOCK_LOW…) → ShiftAlert in-app (+optional Telegram/Zalo — unseen risk RISK-019).

**Exceptions:** stock apply silently skipped on non-ShopERP hosts (RISK-017) · insufficient ingredient → order cannot fulfill · out > stock → InvalidOperationException · count edit after Closed → blocked · provider/HTTP fail → R13 skips Info.
