# Data Flow

> **Chi tiết luồng dữ liệu trong VanAn Ecosystem**
> Reference: ADR-001 (SQLite+NATS Offline First), ADR-002 (Multi-tenancy), ADR-003 (Accounting Immutability)

## Primary Request Flow (Customer → Database)

```
Customer Browser
       │
       ▼
KhachLink PWA (5002)
  - Blazor Server/WebAssembly
  - HttpClient calls
       │
       ▼ HTTPS
Gateway (5001)
  - YARP Reverse Proxy
  - Stateless forwarding
  - NO DbContext, NO business logic
       │
       ▼ HTTP forward
ShopERP (5003)
  - Blazor UI (staff/admin)
  - Controllers (Web API host)
  - IVanAnDbContext injection
       │
       ▼ EF Core 8.0.8
SQLite Database
  - Per-tenant or shared
  - Multi-tenancy via TenantId filter
```

**Example: Product Query**
```
KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite
     ↓                  ↓                  ↓
  HttpClient   ProductsController   ProductsController
                (forward)         (query IVanAnDbContext)
```

## Offline-First Sync Flow (ADR-001)

```
Station (local SQLite)
       │
       ▼ write
   Outbox Table
       │
       ▼ NATS publish
    NATS Bus
       │
       ▼ subscribe
   PostgreSQL (central)
       │
       ▼ broadcast
   Other Stations
```

**Outbox Pattern** (skill: `outbox-pattern-implementation`):
1. Local write → SQLite + Outbox entry (atomic)
2. Background worker reads Outbox → publishes to NATS
3. Central subscriber writes to PostgreSQL
4. Other stations subscribe for replication

## Multi-Tenancy Enforcement (ADR-002)

```
Every entity inherits BaseEntity + IMustHaveTenant
       │
       ▼
  TenantId (Guid, required)
       │
       ▼
EF Core Global Query Filter
  modelBuilder.Entity<T>().HasQueryFilter(e => e.TenantId == currentTenantId)
       │
       ▼
All queries auto-filtered by tenant
```

**Hard rule:** `TenantId` MUST NOT be removed or made nullable. Bypass = Hard Stop.

## Accounting Entry Flow (ADR-003)

```
Business Event (Order completed, Payment confirmed)
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
AccountingEntry (IMMUTABLE)
  - Append-only
  - NO update, NO delete
       │
       ▼ correction?
CreateReversalEntry(originalEntryId, reason)
       │
       ▼ append
Reversal AccountingEntry
  - References original
  - Negates amounts
  - Original remains unchanged
```

**Hard rule:** `AccountingEntry` MUST be 100% immutable. Corrections via Reversal Entry only. Bypass = Hard Stop.

## Order Workflow

```
Pending → Confirmed → Preparing → Ready → Completed
              ↓
          Cancelled
```

- **Inventory deducted at "Confirmed"** (not earlier)
- Kitchen status tracked separately via SignalR
- Payment webhook triggers accounting entry creation (after bank confirm)

## Real-Time Updates (SignalR)

```
ShopERP (5003)
  - Hub endpoints
       │
       ▼ SignalR
KhachLink (5002)
  - Receive order status updates
  - Receive kitchen notifications
  - Receive payment confirmations
```

## Current Known Issues (from project_state.md)

| Issue | Sprint | Status |
|---|---|---|
| TenantId hardcode fallback (no JWT claim → throw) | Sprint A (P0) | Pending |
| AccountCode not saved on manual entry (UI → API → DB) | Sprint A (P0) | Pending |
| Accounting entry creation timing (CreateOrder → PaymentWebhook) | Sprint A (P0) | Pending |
| Vendor/Category/Reference fields not wired to DB | Sprint B (P1) | Pending |
| Webhook notify Kitchen via SignalR after payment | Sprint B (P1) | Pending |
| Server-side duplicate detection for accounting entries | Sprint B (P1) | Pending |

Reference: `docs/AI/phase-next-order-accounting-improvements.md`

---

*Document Status: Active*
*Last Updated: 2026-06-18*
*Source: PROJECT_CONTEXT.md, project_state.md, ADR-001/002/003*
