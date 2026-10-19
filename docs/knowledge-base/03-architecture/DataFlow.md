# Data Flow

> **Chi tiết luồng dữ liệu trong VanAn Ecosystem**
> Reference: ADR-001 (SQLite+NATS — partially superseded by Option C), ADR-002 (Multi-tenancy), ADR-003 (Accounting Immutability)
> Option C (2026-07-18): Gateway = Order Creator + Routed Async Delivery — `docs/AI/tasks/archive/gateway_infra/gateway_router_multi_vps_master_plan.md`

## Primary Order Flow (Customer → Kitchen) — Option C

```
Customer Browser / KhachLink PWA (5002, Blazor WASM)
       │
       ▼ HTTPS — checkout gửi ProductName + VatRate snapshot
Gateway (5001) — ORDER CREATOR
  - OrdersController: tạo Order trực tiếp trong PostgreSQL
  - KHÔNG query Products (snapshot do client cung cấp)
  - Route theo ShopInstanceId (ShopInstances routing table)
       │
       ▼ NATS publish
vanan.cloud.order.created.{shopInstanceId}
       │
       ▼ subscribe (mỗi ShopERP chỉ nhận subject của mình)
ShopERP (5003) — per-tenant SQLite
  - Order replica → kitchen/POS display
  - KHÔNG phải source of truth cho orders
```

**Catalog browse (khác order flow):**
```
KhachLink → Gateway YARP forward → ShopERP → SQLite (Products)
Exception: GET /api/catalog/recommended → pure PG query (FeaturedProducts)
```

## Sync Directions (post Option-C)

| Direction | Data | Mechanism |
|---|---|---|
| PG → SQLite | New orders (routed by ShopInstanceId) | `vanan.cloud.order.created.{shopInstanceId}` |
| SQLite → PG | Order status updates (kitchen/POS ops) | NATS status sync (DataSyncSubscriber) |
| SQLite → PG | Payment confirmed | `vanan.cloud.order.payment.confirmed.{shop}` |
| PG → stations | Notifications, loyalty changes | `vanan.cloud.notification.created`, `vanan.cloud.loyalty.changed.*` |
| Both | Tenant profile/type changes | `vanan.cloud.tenant.profile.updated`, `vanan.cloud.tenant.type.changed` |
| ~~SQLite → PG~~ | Product sync | **DISABLED** (Phase 3 — products live in ShopERP only) |

**Outbox pattern** (skill: `outbox-pattern-implementation`) vẫn áp dụng cho các sync cần durability — local write + outbox entry atomic, worker publish NATS.

## Multi-Tenancy Enforcement (ADR-002)

```
Tenant-scoped entity: BaseEntity + TenantId (required)
       │
       ▼
VanAnDbContext (PG, Gateway + ShopERP)
  - Global query filter: e.TenantId == CurrentTenantIdValue
  - TenantId stored as TEXT via converter (NEVER EF.Property<Guid>)
       │
       ▼
ShopERPDbContext (SQLite, ShopERP)
  - Chỉ có IsDeleted/outbox filters — KHÔNG có tenant global filter
  - Services/pages PHẢI filter TenantId tường minh
    (production bug a21f97f2 — cross-tenant inventory leak)
```

**Exceptions (platform-level, `TenantId = Guid.Empty` sentinel):** `SystemSetting`, `LoyaltyGlobalConfig`, `AllianceWallet`, `KhachLinkInstance`, `ShopInstance`… — query bằng `IgnoreQueryFilters` + predicate tường minh.

**Hard rule:** `TenantId` MUST NOT be removed or made nullable trên tenant-scoped entities. Bypass = Hard Stop.

## Accounting Entry Flow (ADR-003)

```
Business Event (Order completed, Payment confirmed, Manual entry)
       │
       ▼
AccountingService.CreateEntry(...)
       │
       ▼ validate
  - TenantId present
  - Period not closed
  - Double-entry balanced (Debit == Credit)
       │
       ▼ append
AccountingEntry (IMMUTABLE) — PostgreSQL
  - Append-only, NO update, NO delete
  - TransactionDate mapped (ngày nghiệp vụ user nhập)
       │
       ▼ correction?
CreateReversalEntry(originalEntryId, reason)
       │
       ▼ append
Reversal AccountingEntry — references original, negates amounts
```

**Hard rule:** `AccountingEntry` MUST be 100% immutable. Corrections via Reversal Entry only. Bypass = Hard Stop.

## Order Workflow — Configurable (issue #195, 2026-10-19)

Built-in default chain (fallback khi không có profile):
```
pending → confirmed → preparing → ready → completed
     └──────────────→ cancelled
(delivering cũng là status hợp lệ)
```

- `OrderFlowProfile` + `OrderFlowStep`: flow configurable theo scope — precedence `tenant+source → tenant → system+source → system → built-in`
- `Order.SourceType`: nhận diện loại đơn (POS/booking) — booking dùng reduced flow
- `OrderStatusHistory`: bảng audit/timeline cho mọi transition
- `OrderStatusId` = string value object (`"pending"`, `"confirmed"`…), không phải fixed enum
- `OrderWorkflowService` validate transition động theo `AllowedNext`; admin config qua `/admin/order-flow` (Gateway `OrderFlowController` + `AdminOrderFlowController`)
- Kitchen status realtime qua SignalR `KitchenHub`

## Real-Time Updates (SignalR)

Hubs chủ yếu host trên **Gateway (5001)** + Redis backplane:

| Hub | Endpoint | Purpose |
|---|---|---|
| `OrderHub` | `/orderHub` | Order status (Gateway + ShopERP cũng map) |
| `KitchenHub` | `/kitchenhub` | Kitchen display updates |
| `LocationHub` | `/hubs/location` | Location tracking |
| `ChatHub` | `/hubs/chat` | Customer chat |
| `MessagingHub` | `/hubs/messaging` | Messaging |
| `TrackingHub` | `/hubs/tracking` | Delivery tracking |
| `NotificationsHub` | `/hubs/notifications` | Push notifications (new order, alerts…) |

ShopERP (5003) map `/orderHub` cho staff UI.

## Current State

Known issues/roadmap KHÔNG track ở file này — xem `docs/AI/project_state.md` (single source of truth cho objective hiện tại).

---

*Document Status: Active*
*Last Updated: 2026-10-19*
*Source: Program.cs (Gateway/ShopERP), VanAnDbContext, ShopERPDbContext, DataSyncSubscriber, OrderFlow configs, governance.md (Option C), ADR-001/002/003*
