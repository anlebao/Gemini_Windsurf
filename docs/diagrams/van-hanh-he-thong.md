# Vạn An — Sơ đồ vận hành hệ thống

> Sơ đồ minh họa cách hệ thống vận hành thực tế (không phải digital-twin registry).
> Mermaid — xem trên GitHub/VS Code, hoặc mở `van-hanh-he-thong.html` cùng thư mục.
> Verify tại `476cd73f` (2026-11-08).

## 1. Bối cảnh hệ sinh thái

```mermaid
flowchart LR
    classDef human fill:#cfe3ff,stroke:#2b6cb0
    classDef sys fill:#c6f6d5,stroke:#2f855a
    classDef ext fill:#e2e8f0,stroke:#718096
    KH["Khách hàng<br/>(KhachLink PWA)"]:::human
    OWN["Chủ shop<br/>Owner/Staff/Kho"]:::human
    SHP["Shipper /<br/>Salesman CTV"]:::human
    GRD["Bảo vệ giữ xe"]:::human
    ADM["Vạn An ops<br/>SystemAdmin"]:::human
    GW["Gateway :5001<br/>+ PostgreSQL<br/>(source of truth)"]:::sys
    SE["ShopERP :5003<br/>Blazor + SQLite<br/>per-tenant"]:::sys
    MT["vanan-metering :5050<br/>(internal)"]:::sys
    AI["vanan-ai :5060<br/>+ pgvector"]:::sys
    NQ(("NATS bus")):::sys
    EXT["VietQR · e-invoice ·<br/>Zalo/Telegram · R2"]:::ext
    KH -->|HTTPS| GW
    SHP -->|X-Customer-Token| GW
    OWN -->|JWT| SE
    GRD -->|JWT Guard| GW
    ADM -->|JWT SystemAdmin| GW
    GW <--> NQ <--> SE
    SE -->|X-Caller-Id+key| MT
    GW -->|signed ctx| AI
    GW & SE --> EXT
```

## 2. Xương sống đơn hàng (Option C — PG là SoT)

```mermaid
sequenceDiagram
    participant K as Khách (KhachLink)
    participant G as Gateway (Order creator)
    participant P as PostgreSQL
    participant N as NATS
    participant S as ShopERP (SQLite replica)
    participant POS as Bếp/POS
    K->>G: POST /api/public/orders/checkout<br/>(ProductName+VatRate snapshot)
    G->>P: OrderService.CreateOrder (resolve mode,<br/>snapshot rates, routingKey)
    G->>N: vanan.cloud.order.created.{shopInstanceId}
    N->>S: routed delivery (wildcard đã gỡ)
    S->>S: OrderSyncSubscriber -> replica + hiển thị
    POS->>S: xác nhận/bếp/hoàn tất
    S->>N: vanan.shoperp.order.status.changed
    N->>G: DataSyncSubscriber -> cập nhật PG
    G-->>K: GET /api/public/orders/{id} tracking
```

## 3. Giao hàng cộng đồng — VALCN shipper

```mermaid
flowchart TB
    classDef human fill:#cfe3ff,stroke:#2b6cb0
    classDef auto fill:#c6f6d5,stroke:#2f855a
    classDef error fill:#fed7d7,stroke:#c53030
    A["Shipper mở tab giao hàng"]:::human --> B["GET nearby-orders<br/>Haversine tới điểm giao<br/>(cross-tenant)"]:::auto
    B --> C["Chọn đơn -> accept<br/>(1 active DeliveryTask/đơn)"]:::human
    C --> D["DeliveryTask Assigned<br/>order -> delivering<br/>+ OrderStatusHistory"]:::auto
    D --> E["pickup -> delivering -> delivered"]:::human
    E -->|delivered| F["OrderWorkflowService<br/>-> completed + loyalty<br/>+ accounting + NATS"]:::auto
    E -->|failed| G["Failed(reason)"]:::error
    C -.->|đã có task / sai status| H["409"]:::error
    F --> I["Shipper nộp COD:<br/>Remittance -> shop/PlatformWallet"]:::auto
    I --> J["Reseller: split Settlement+<br/>DeliveryFee+PlatformFee+<br/>CommunityFund (snapshot)"]:::auto
```

## 4. Giữ xe — QR Parking

```mermaid
flowchart LR
    classDef human fill:#cfe3ff,stroke:#2b6cb0
    classDef auto fill:#c6f6d5,stroke:#2f855a
    classDef error fill:#fed7d7,stroke:#c53030
    IN["Xe vào: guard chụp<br/>biển số (+ảnh khách)"]:::human --> R2["Ảnh -> R2<br/>(presign/server-side)"]:::auto
    R2 --> ISS["Issue: VehicleSession(Issued)<br/>QR + mã 6 số + vé 58mm"]:::auto
    ISS --> CLM["Khách claim:<br/>QR {t,tn} | {sc,sid} | short code<br/>(X-Customer-Token)"]:::human
    CLM --> VS["Claimed<br/>(idempotent)"]:::auto
    VS --> OUT["Xe ra: guard scan<br/>-> GuardScanLog<br/>Match/Mismatch/Override"]:::human
    OUT -->|khớp| CO["CheckedOut"]:::auto
    OUT -->|lệch/nghi| FG["Flagged(reason)"]:::error
    ISS --> HUY["Void (hủy/hết hạn)"]:::auto
    CLM -.->|claimed rồi| X["409"]:::error
```

## 5. Kế toán — 2 sổ song song theo regime

```mermaid
flowchart TB
    classDef auto fill:#c6f6d5,stroke:#2f855a
    classDef human fill:#cfe3ff,stroke:#2b6cb0
    classDef error fill:#fed7d7,stroke:#c53030
    EVT["Nghiệp vụ: đơn/phiếu thu-chi/<br/>nhập mua/tạm ứng/TS"]:::auto
    EVT --> AE["AccountingEntry<br/>single-entry, IMMUTABLE<br/>(sổ HKD TT152)"]:::auto
    EVT --> JE["JournalEntry<br/>double-entry<br/>(BCTC TT133/99/71)"]:::auto
    JE --> REP["Báo cáo: Nhật ký/Sổ Cái/<br/>TrialBalance/B01-B03+B09"]:::auto
    AE --> HKD["Sổ HKD S1a-S3a<br/>(template engine)"]:::auto
    REP --> CLS["Khóa kỳ: validate<br/>(reconcile Error->chặn)<br/>+ kết chuyển 911 (accrual)<br/>+ MarkClosed + audit"]:::auto
    CLS -->|sai sót| RO["Reopen: 3 roles + reason<br/>-> reversal closing JEs"]:::human
    AE -->|sai sót| RV["Reversal entry<br/>(không sửa gốc)"]:::auto
    RGN["Regime-lock theo TenantType:<br/>HTX->TT71 · lớn->TT99 · vừa->TT133<br/>siêu nhỏ->TT58(+133) · HKD->TT152"]:::human
    RGN -.-> AE & JE
    REC["Tra soát R1-R13 (SystemAdmin)<br/>read-only, fail-open"]:::human --> CLS
```

## 6. Tồn kho — voucher PG ↔ stock SQLite

```mermaid
flowchart LR
    classDef auto fill:#c6f6d5,stroke:#2f855a
    classDef human fill:#cfe3ff,stroke:#2b6cb0
    classDef error fill:#fed7d7,stroke:#c53030
    PR["Phiếu nhập mua (PG)<br/>post"]:::auto --> SM["StockMovement<br/>PurchaseIn (SQLite)<br/>idempotent (rcpt,line,type)"]:::auto
    SM --> CUR["Ingredient.CurrentStock<br/>(snapshot)"]:::auto
    MAN["Nhập/xuất tay"]:::human -->|out > tồn| E1["chặn"]:::error
    MAN --> SM
    CNT["Kiểm kê theo ca<br/>(Draft/Submitted mới sửa)"]:::human --> AL["Đóng ca -> AlertEngine<br/>10 rules -> ShiftAlert"]:::auto
    ORD["Đơn -> recipe -><br/>định lượng NVL"]:::auto -->|thiếu| E2["cannot fulfill"]:::error
    CUR --> VAL["Σ(stock x price)<br/>-> reconcile R13"]:::auto
```

## 7. Phí nền tảng — metering deny gate

```mermaid
sequenceDiagram
    participant C as Caller (ShopERP/Gateway)
    participant M as vanan-metering :5050
    participant A as Admin (Vạn An)
    C->>M: GET /v1/check (X-Caller-Id + key)
    alt quota còn
        M-->>C: allow (+ % còn lại)
    else còn <=50% warn / <=10% critical
        M-->>C: allow + alert flag (poll)
    else hết quota
        M-->>C: DENY
    end
    A->>M: POST /v1/topups (VietQR thủ công)
    M->>M: wallet append-only + rate-card head
```

## 8. Triển khai — multi-VPS

```mermaid
flowchart TB
    classDef sys fill:#c6f6d5,stroke:#2f855a
    subgraph GCP["GCP vanan-prod"]
        GW2["vanan-gateway<br/>asia-southeast1-a<br/>(nginx+Gateway+PG+NATS)"]:::sys
        SA["vanan-shop-a<br/>asia-southeast1-b<br/>(ShopERP)"]:::sys
        MT2["vanan-metering<br/>asia-southeast1-b<br/>internal-only :5050"]:::sys
        KL["vanan-khachlink<br/>asia-southeast1-c<br/>(nginx static PWA)"]:::sys
        KL2["vanan-khachlink-timlathay<br/>asia-southeast1-c"]:::sys
    end
    KL & KL2 -->|HTTPS| GW2
    GW2 -->|NATS routed| SA
    SA -->|internal IP| MT2
    USERS["Khách / Owner / Shipper / Admin"] -->|domain| KL & KL2
```
