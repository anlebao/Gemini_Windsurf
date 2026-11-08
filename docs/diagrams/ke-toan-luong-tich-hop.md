# Kế toán — Luồng chi tiết & giao tiếp module

> Chi tiết hóa sơ đồ #5 từ `van-hanh-he-thong.md`. Verified @476cd73f (2026-11-08).

## 1. Bản đồ tích hợp — ai ghi vào kế toán, ai đọc ra

```mermaid
flowchart LR
    classDef prod fill:#cfe3ff,stroke:#2b6cb0
    classDef core fill:#c6f6d5,stroke:#2f855a
    classDef cons fill:#f6e3c5,stroke:#b7791f
    classDef none fill:#e2e8f0,stroke:#718096

    subgraph SRC["Nguồn nghiệp vụ (producers)"]
        ORD["Order/POS paid<br/>OrderService"]:::prod
        VCH["Chứng từ (VoucherTypeRegistry)<br/>Smart Entry + form"]:::prod
        PR["Phiếu nhập mua<br/>PurchaseReceiptService"]:::prod
        MNL["Thu/chi tay + Công nợ<br/>ManualEntryJournalBridge"]:::prod
        RSV["Reseller order<br/>(3 booksets)"]:::prod
    end

    subgraph CORE["CORE kế toán (PG — IAccountingDbContext)"]
        AE[("AccountingEntry<br/>single-entry IMMUTABLE")]:::core
        JE[("JournalEntry<br/>double-entry")]:::core
        PC["PeriodClosingService<br/>close/reopen"]:::core
        RCN["AccountingReconcileService<br/>R1-R13 read-only"]:::core
    end

    subgraph OUT["Người dùng số liệu (consumers)"]
        BK["Sổ: HKD S1a-S3a ·<br/>Nhật ký · Sổ Cái · TB"]:::cons
        FS["BCTC B01/B02/B03/B09"]:::cons
        TAX["Thuế/e-invoice<br/>(3331/1331)"]:::cons
        FI["Fin-Intelligence<br/>advisory (read-only)"]:::cons
    end

    OUT2["NGOÀI sổ kế toán:<br/>wallet/community fund · metering ·<br/>stock movements (audit riêng)"]:::none

    ORD -->|order->entries, ref orderId| JE & AE
    RSV -->|idempotent suffix| JE & AE
    VCH -->|strategy.PostAsync -> voucher seq| JE
    PR -->|voucher + stock link| JE
    MNL -->|thu 5xx/chi 6xx<br/>công nợ KHÔNG tạo JE| AE & JE
    JE --> BK & FS
    AE --> HKDS["Sổ HKD TT152"]:::cons
    PC -->|kết chuyển 911<br/>reversal reopen| JE
    RCN -.->|đọc| JE & AE
    STOCK["StockMovement (SQLite)"]:::prod -.->|valuation snapshot -> R13| RCN
    MIRR["Order mirror (SQLite)"]:::prod -.->|R11 cross-store| RCN
    JE --> TAX
    JE -.->|đọc| FI
```

## 2. Write-path — nghiệp vụ thành bút toán

```mermaid
flowchart TB
    classDef auto fill:#c6f6d5,stroke:#2f855a
    classDef human fill:#cfe3ff,stroke:#2b6cb0
    classDef error fill:#fed7d7,stroke:#c53030

    A["Order paid"]:::auto --> B["GenerateAccountingEntriesAsync<br/>per-tenant regime<br/>511/3331/632..."]:::auto
    B -->|Marketplace| JE1["JE 1 bộ/order<br/>(R8b check)"]:::auto
    B -->|Reseller| JE3["3 booksets:<br/>Supplier+Reseller+Platform<br/>idempotent suffix"]:::auto
    C["Người dùng nhập phiếu"]:::human --> D{"VoucherTypeRegistry<br/>ResolveByIntent"}:::auto
    D -->|match| E["Strategy.PostAsync<br/>-> VoucherNumberService seq"]:::auto
    D -->|không| F["manual journal path"]:::error
    G["Thu/chi tay"]:::human --> H["ManualEntryJournalBridge<br/>thu: Nợ111/CóTK · chi: NợTK/Có111<br/>EntryDate=TransactionDate<br/>ref ManualEntry + idempotent"]:::auto
    H -.->|công nợ| I["KHÔNG tạo JE<br/>(theo dõi ngoài)"]:::error
    E & H --> JE
    JE1 & JE3 --> JE[("JournalEntry + AccountingEntry<br/>(append-only; sai -> REVERSAL)")]
```

## 3. Chu kỳ kỳ kế toán — close/reconcile/reopen

```mermaid
sequenceDiagram
    participant O as Owner/Kế toán
    participant V as ValidatePeriod
    participant R as Reconcile R1-R13
    participant C as PeriodClosingService
    participant DB as PG (JE + status)
    participant S as ShopERP (SQLite)
    O->>V: close period
    V->>DB: entries exist? pending Amount=0?
    V->>R: chạy checklist
    R->>S: HTTP mirror-orders (R11) + inventory/valuation (R13)
    S-->>R: DTOs (fail-open -> skip Info)
    R-->>V: checks (Error->errors / Warning->warnings / crash->warning)
    alt errors
        V-->>O: chặn đóng kỳ
    else pass
        O->>C: ClosePeriodAsync
        C->>DB: closing JEs 911 (TT133/99/71 only, idempotent)
        C->>DB: MarkClosed(user) + audit
    end
    O->>C: Reopen (Owner/ChiefAccountant/SystemAdmin + reason)
    C->>DB: Reopening -> reverse closing JEs -> Open + audit
```

## 4. Ranh giới module — cái GÌ đi vào sổ, cái gì KHÔNG

```mermaid
flowchart TB
    classDef in fill:#c6f6d5,stroke:#2f855a
    classDef out fill:#fed7d7,stroke:#c53030
    classDef xref fill:#e2e8f0,stroke:#718096

    IN["VÀO SỔ (ghi AE/JE):<br/>· order paid -> entries<br/>· voucher strategies -> JE<br/>· thu/chi -> AE+bridge JE<br/>· nhập mua -> voucher+JE<br/>· reseller -> 3 booksets<br/>· kết chuyển 911 / reversal"]:::in

    OUT["KHÔNG VÀO SỔ:<br/>· công nợ (phải thu/trả — track ngoài, chỉ phiếu thu/chi mới ghi)<br/>· wallet/community fund (sổ riêng PG)<br/>· stock movements (audit SQLite — chỉ snapshot vào R13)<br/>· metering quota/topup (dịch vụ nền tảng)<br/>· loyalty points"]:::out

    X["CROSS-BOUNDARY:<br/>· StockMovement <-> PurchaseReceipt (PG): link Id, không FK<br/>· Order replica SQLite -> mirror DTO cho R11<br/>· Ingredient stock -> valuation DTO cho R13<br/>· Tenant.Type -> regime-lock resolve"]:::xref
```

## Ghi chú giao tiếp chính

- **Order → kế toán:** `OrderService.GenerateAccountingEntriesAsync` — 1 journal/paid order (R8b); đường journal trực tiếp `ref 'Order'` đã bỏ từ **2026-10-29** (R8a bridge) — mọi ghi mới đi qua bút toán chuẩn.
- **Voucher framework:** mọi chứng từ mới = `VoucherTypeDefinition` + `IVoucherPostingStrategy` — Smart Entry resolve theo intent, không hardcode route.
- **Công nợ:** phải thu/trả **không** tạo JE — chỉ theo dõi 131/331 qua entries + phiếu thu/chi.
- **Immutable:** `AccountingEntry` append-only — mọi sửa sai = reversal entry; `JournalEntry` cũng chỉ chỉnh qua reversal/close-reopen.
- **Regime-lock:** `AccountingStandardRules` quyết định sổ nào áp dụng (HKD→TT152 sổ riêng, không BCTC trừ khi elect TT58).
- **Kho ↔ GL:** stock không ghi bút toán — chỉ snapshot `Σ(stock×price)` nuôi check R13; nếu cần bút toán nhập kho → qua phiếu nhập mua.
- **Reconcile:** SystemAdmin-only, read-only, audit-log mỗi lần chạy; fail-open (R11/R13 skip Info khi provider lỗi).
