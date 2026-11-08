# Điểm thưởng liên minh — Loyalty Alliance

> Chi tiết luồng accrual/sync/redemption/budget. Verified @476cd73f (2026-11-08).
> SoT: PG (AllianceWallet + LoyaltyRewards + ledger). SQLite = mirror cho display.

## 1. Tổng quan — 2 chế độ Silo vs Alliance

```mermaid
flowchart TB
    classDef auto fill:#c6f6d5,stroke:#2f855a
    classDef human fill:#cfe3ff,stroke:#2b6cb0
    classDef ext fill:#e2e8f0,stroke:#718096

    CFG["LoyaltyModeResolver<br/>Silo=0 / Alliance=1<br/>per tenant"]:::auto
    CFG -->|Silo| S1["Điểm riêng từng shop<br/>(không cộng dồn liên minh)"]:::auto
    CFG -->|Alliance| S2["AllianceWallet chung<br/>điểm tích xuyên tenant"]:::auto
    GC["LoyaltyGlobalConfig<br/>+ LoyaltyTenantConfig"]:::auto -.-> CFG
    S2 --> AW[("AllianceWallet (PG)<br/>số dư điểm SoT")]
    S1 --> LR[("LoyaltyRewards (PG)<br/>per-tenant rewards")]
```

## 2. Accrual — đơn hàng sinh điểm

```mermaid
flowchart LR
    classDef auto fill:#c6f6d5,stroke:#2f855a
    classDef error fill:#fed7d7,stroke:#c53030
    classDef ext fill:#e2e8f0,stroke:#718096

    DONE["Order -> completed<br/>(POS / shipper delivered /<br/>booking)"]:::auto
    DONE --> CALC["LoyaltyPointsCalculator<br/>points theo rule tenant"]:::auto
    CALC --> BUD{"LoyaltyBudgetService<br/>(flag ValcnV2_LoyaltyBudget)"}:::auto
    BUD -->|cap hit| CAP["adjusted points<br/>PerOrderRateCap/<br/>Monthly/DailyBudget/<br/>PerCustomerDailyLimit"]:::auto
    BUD -->|hết ngân sách| ZERO["0 điểm — bỏ reward"]:::error
    CAP & BUD -->|pass| ADD["AllianceWallet.AddPoints<br/>+ LoyaltyIssuanceRecord<br/>+ ledger append"]:::auto
    ADD --> REC["RecordIssuanceAsync<br/>(counter atomic++)"]:::auto
    ADD --> PUB["LoyaltyBalanceSyncPublisher<br/>+ outbox fallback"]:::auto
```

## 3. Sync PG -> SQLite (display mirror)

```mermaid
sequenceDiagram
    participant P as AllianceWallet (PG)
    participant PB as LoyaltyBalanceSyncPublisher
    participant N as NATS
    participant O as Outbox (fallback)
    participant S as LoyaltySyncSubscriber (ShopERP)
    participant L as Mirror (SQLite)
    P->>PB: AddPoints/Redeem xong
    PB->>N: vanan.cloud.loyalty.changed.{deviceId}
    PB->>O: enqueue (retry path)
    O->>N: replay on restart
    N->>S: routed by deviceId
    S->>L: upsert mirror balance
    Note over S: wildcard 'loyalty.changed.>'<br/>cho offline display
    GC2["LoyaltyConfigController"]->>N: vanan.cloud.loyalty.config.changed
    N->>S: re-resolve mode/rules
```

## 4. Redemption + budget reset + jobs

```mermaid
flowchart TB
    classDef human fill:#cfe3ff,stroke:#2b6cb0
    classDef auto fill:#c6f6d5,stroke:#2f855a
    classDef error fill:#fed7d7,stroke:#c53030
    classDef ext fill:#e2e8f0,stroke:#718096

    CUST["Khách mở catalog<br/>(KhachLink)"]:::human --> CAT["GlobalRedemptionCatalog<br/>items + cost points"]:::auto
    CAT --> RED["Redeem -> RedemptionRecord<br/>trừ điểm wallet"]:::auto
    RED -->|đủ điểm| OK["voucher/quà -><br/>dùng tại shop liên minh"]:::auto
    RED -->|thiếu| E1["reject"]:::error
    D1["LoyaltyBudgetDailyResetJob<br/>00:00 UTC"]:::ext --> RST1["PointsIssuedToday=0<br/>all tenants"]:::auto
    M1["LoyaltyBudgetMonthlyResetJob<br/>ngày 1 hàng tháng"]:::ext --> RST2["PointsIssuedThisMonth=0"]:::auto
    FRAUD["FraudReview: RiskScore>=80<br/>Rejected · 60-79 Held 48h"]:::error -.->|commission/attribution giữ| RED
```

## Ghi chú giao tiếp chính

- **Trigger accrual:** order `completed` (từ POS hoặc shipper `DeliveryTask` → `OrderWorkflowService`) — points tính theo rule tenant, KHÔNG ghi trực tiếp khi order tạo.
- **Budget (VALCN v2 P3, flag OFF mặc định):** `LoyaltyBudgetService` check 4 caps trước `AddPoints` → adjusted (có thể thấp hơn hoặc 0); `RecordIssuanceAsync` atomic counter; reversal path (Phase 4) decrement. `RecordIssuance` fail chỉ warn — counter có thể lệch nhẹ (by design).
- **Sync:** publish `loyalty.changed.{deviceId}` routed + **outbox fallback** khi publish fail; ShopERP mirror subscribe wildcard `loyalty.changed.>`; config đổi → `loyalty.config.changed` broadcast.
- **Idempotency:** `LoyaltyIssuanceRecord` chống double-issue (orderId tracking); outbox replay không cộng trùng.
- **Reconcile:** `LoyaltyReconcileCase` entity tồn tại cho tra soát điểm (drift backlog).
- **Commission link:** salesman commission/attribution qua `FraudReview` — Rejected (score≥80) / Held (60-79, 48h) trước khi chi.
