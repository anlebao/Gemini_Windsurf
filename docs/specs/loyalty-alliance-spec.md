# Loyalty Alliance System — Specification v1.0

## 1. Overview

Hệ thống loyalty points hiện tại hoạt động ở chế độ **Silo**: điểm tích lũy tại tenant A chỉ đổi được tại tenant A. Spec này định nghĩa cơ chế **Alliance**: khách hàng có 1 ví điểm chung dùng được ở mọi tenant trong liên minh.

### Nguyên tắc cốt lõi
- **Giá trị điểm đồng nhất toàn hệ thống**: 1 điểm = 1 điểm ở mọi tenant, không có conversion rate
- **Mode switchable**: SystemAdmin chuyển giữa Silo và Alliance
- **Per-tenant override**: Cấu hình tenant cấp thấp hơn ghi đè cấu hình toàn cục
- **Chỉ SystemAdmin được phép thay đổi mode**

---

## 2. Mode Configuration

### 2.1. Loyalty Mode

| Mode | Mô tả |
|------|-------|
| **Silo** | Điểm tích lũy tại tenant X chỉ đổi được tại tenant X (hành vi hiện tại) |
| **Alliance** | Điểm tích lũy tại bất kỳ tenant nào đều đổi được ở mọi tenant trong liên minh |

### 2.2. Configuration Hierarchy

```
Priority (cao → thấp):
  1. Tenant-level setting (per-tenant override)
  2. Global setting (system-wide default)
```

**Logic resolve mode cho tenant T:**
```
if (TenantT.LoyaltyMode != null)
    effectiveMode = TenantT.LoyaltyMode    // tenant override
else
    effectiveMode = GlobalSetting.LoyaltyMode  // global default
```

### 2.3. Quyền thay đổi

| Role | Global setting | Per-tenant setting |
|------|---------------|-------------------|
| SystemAdmin | ✅ Được phép | ✅ Được phép |
| Tenant Owner | ❌ | ❌ |
| Tenant Admin | ❌ | ❌ |

---

## 3. Data Model

### 3.1. New Entities (PostgreSQL — Gateway)

```csharp
// Global loyalty config — single row
public class LoyaltyGlobalConfig : BaseEntity  // NOT tenant-scoped
{
    public LoyaltyMode Mode { get; protected set; } = LoyaltyMode.Silo;
    public int PointsRate { get; protected set; } = 1;  // 1 VND/1000 = 1 point (uniform)
    public int MinPointsPerOrder { get; protected set; } = 10;
    public int MaxPointsPerOrder { get; protected set; } = 30;
    public int MaxWalletPoints { get; protected set; } = 100000;  // Q5: configurable cap
    public DateTime? LastChangedAt { get; protected set; }
    public string? LastChangedBy { get; protected set; }  // SystemAdmin email

    // Business methods
    public void UpdateMode(LoyaltyMode mode, string changedBy) { ... }
    public void UpdateLimits(int maxPointsPerOrder, int maxWalletPoints, string changedBy) { ... }
}

// Per-tenant loyalty override
public class LoyaltyTenantConfig : BaseEntity, IMustHaveTenant  // tenant-scoped
{
    public LoyaltyMode? Mode { get; protected set; }  // null = inherit global
    public bool IsAllianceMember { get; protected set; } = false;
    public int? MaxWalletPoints { get; protected set; }  // null = inherit global (Q5)
    public DateTime? LastChangedAt { get; protected set; }
    public string? LastChangedBy { get; protected set; }

    // Business methods
    public void SetMode(LoyaltyMode? mode, string changedBy) { ... }
    public void SetAllianceMembership(bool isMember, string changedBy) { ... }
    public void SetMaxWalletPoints(int? max, string changedBy) { ... }
}

// Cross-tenant wallet — 1 per customer device identity
public class AllianceWallet : BaseEntity  // NOT tenant-scoped
{
    public Guid CustomerDeviceId { get; protected set; }  // cross-tenant identity
    public string? PhoneNumber { get; protected set; }   // for lookup
    public int TotalPointBalance { get; protected set; }
    public DateTime LastEarnAt { get; protected set; }
    public DateTime LastRedeemAt { get; protected set; }

    // Business methods
    public void AddPoints(int points, Guid tenantId, string reason) { ... }
    public void DeductPoints(int points, Guid tenantId, string reason) { ... }
}

// Transaction log — every earn/redeem across tenants
public class AllianceTransaction : BaseEntity  // NOT tenant-scoped
{
    public Guid WalletId { get; protected set; }
    public Guid TenantId { get; protected set; }        // where transaction occurred
    public TransactionType Type { get; protected set; }  // EARN, REDEEM, ADJUST
    public int Points { get; protected set; }            // positive for EARN, negative for REDEEM
    public int BalanceAfter { get; protected set; }
    public string Reason { get; protected set; } = string.Empty;
    public Guid? SourceOrderId { get; protected set; }   // for EARN from order completion
    public string? VoucherCode { get; protected set; }    // for REDEEM
    public Guid? RefundTenantId { get; protected set; }   // Q4: tenant where refund goes (tenant of redeem)
    public DateTime TransactionAt { get; protected set; }
}

// Enums
public enum LoyaltyMode { Silo = 0, Alliance = 1 }
public enum TransactionType { EARN = 0, REDEEM = 1, ADJUST = 2 }
```

### 3.2. Existing Entities — Impact

| Entity | Thay đổi | Ghi chú |
|--------|---------|---------|
| `LoyaltyRewards` (SQLite) | **Không xóa** — vẫn dùng trong Silo mode | Trong Alliance mode, `PointBalance` được sync từ `AllianceWallet` |
| `RedemptionCatalogItem` (SQLite) | **Không đổi** | Quà vẫn do từng tenant quản lý |
| `RedemptionRecord` (SQLite) | **Không đổi** | Record vẫn tenant-scoped |
| `Voucher` (SQLite) | **Không đổi** | Voucher vẫn tenant-scoped |
| `Customer` (SQLite) | **Không đổi** | Customer vẫn tenant-scoped |

### 3.3. DB Placement

| Entity | Database | Lý do |
|--------|----------|-------|
| `LoyaltyGlobalConfig` | PostgreSQL (Gateway) | System-wide, cross-tenant |
| `LoyaltyTenantConfig` | PostgreSQL (Gateway) | Tenant-level config, cross-tenant query |
| `AllianceWallet` | PostgreSQL (Gateway) | Cross-tenant wallet, cần truy cập từ mọi ShopERP |
| `AllianceTransaction` | PostgreSQL (Gateway) | Audit log, cross-tenant |
| `LoyaltyRewards` (existing) | SQLite (ShopERP) | Giữ cho Silo mode, sync trong Alliance mode |
| `RedemptionCatalogItem` (existing) | SQLite (ShopERP) | Per-tenant catalog, không đổi |
| `RedemptionRecord` (existing) | SQLite (ShopERP) | Per-tenant record, không đổi |
| `Voucher` (existing) | SQLite (ShopERP) | Per-tenant voucher, không đổi |

---

## 4. Flow

### 4.1. EARN — Order Completed

```
Order completed at Tenant A
  │
  ├─ Silo mode (tenant A):
  │    └─ LoyaltyRewardsService.AddPointsAsync(customerId, points)
  │         → Update SQLite LoyaltyRewards.PointBalance (existing flow, no change)
  │
  └─ Alliance mode (tenant A):
       └─ AllianceWalletService.AddPointsAsync(customerDeviceId, tenantA, points)
            → INSERT AllianceTransaction(EARN, tenantA, +points)
            → UPDATE AllianceWallet.TotalPointBalance += points
            → Sync to SQLite LoyaltyRewards.PointBalance (for local display)
            → Publish NATS event: LoyaltyPointsChanged (wallet balance)
```

### 4.2. REDEEM — Customer Redeems Gift

```
Customer redeems catalog item at Tenant B
  │
  ├─ Silo mode (tenant B):
  │    └─ Existing flow: RedemptionService.RedeemAsync(customerId, catalogItemId)
  │         → Check SQLite LoyaltyRewards.PointBalance >= pointsRequired
  │         → Deduct from SQLite LoyaltyRewards
  │         → Create RedemptionRecord + Voucher in SQLite
  │
  └─ Alliance mode (tenant B):
       └─ AllianceWalletService.RedeemAsync(customerDeviceId, tenantB, catalogItemId, pointsRequired)
            → Check tenant B IsAllianceMember == true (Q2: opt-out toàn phần)
            → Check AllianceWallet.TotalPointBalance >= pointsRequired
            → INSERT AllianceTransaction(REDEEM, tenantB, -pointsRequired, RefundTenantId=tenantB)
            → UPDATE AllianceWallet.TotalPointBalance -= pointsRequired
            → Create RedemptionRecord + Voucher in Tenant B's SQLite (tenant-scoped)
            → Sync to SQLite LoyaltyRewards.PointBalance (for local display)
            → Publish NATS event: LoyaltyPointsChanged (wallet balance)
```

### 4.3. CANCEL/REFUND — Voucher Cancelled (Q4)

```
Voucher VAN-XXX cancelled at Tenant B (where redeem occurred)
  │
  └─ Alliance mode:
       └─ AllianceWalletService.RefundAsync(voucherCode, tenantB, pointsRefund)
            → INSERT AllianceTransaction(ADJUST, tenantB, +pointsRefund, "Voucher cancelled")
            → UPDATE AllianceWallet.TotalPointBalance += pointsRefund
            → Update LoyaltyRewards.PointBalance at Tenant B's SQLite (Q4: refund về tenant redeem)
            → Publish NATS event: LoyaltyPointsChanged (wallet balance)
```

### 4.4. Mode Switch — SystemAdmin Changes Mode

```
SystemAdmin sets Tenant A to Alliance mode
  │
  ├─ If switching Silo → Alliance:
  │    └─ Migration step: consolidate existing LoyaltyRewards into AllianceWallet
  │         → Find all customers in Tenant A by CustomerDeviceId
  │         → For each: create/merge AllianceWallet, sum PointBalance across tenants
  │         → INSERT AllianceTransaction(ADJUST, tenantA, +sum, "Mode migration")
  │
  └─ If switching Alliance → Silo (Q1: chia theo nguồn):
       └─ Split step: distribute AllianceWallet.TotalPointBalance back to per-tenant SQLite
            → Query AllianceTransaction history for this wallet
            → Calculate net EARN per-tenant: SUM(EARN) - SUM(REDEEM at that tenant)
            → Distribute TotalPointBalance proportionally by net EARN per-tenant
            → For each tenant: update LoyaltyRewards.PointBalance with allocated share
            → INSERT AllianceTransaction(ADJUST, each-tenant, ±adjustment, "Mode migration split")
            → Freeze AllianceWallet (IsActive = false, balance = 0)
            → Edge case: if a tenant's net EARN <= 0, no points allocated to that tenant
```

---

## 5. API

### 5.1. SystemAdmin — Global Config

```
GET    /api/platform/loyalty/config
       → { mode: "Silo", pointsRate: 1, minPointsPerOrder: 10, maxPointsPerOrder: 30, maxWalletPoints: 100000 }

PUT    /api/platform/loyalty/config
       Body: { mode: "Alliance", pointsRate: 1, minPointsPerOrder: 10, maxPointsPerOrder: 30, maxWalletPoints: 100000 }
       → 200 OK or 403 (non-SystemAdmin)
```

### 5.2. SystemAdmin — Per-Tenant Config

```
GET    /api/platform/loyalty/tenant/{tenantId}/config
       → { mode: null, isAllianceMember: false, maxWalletPoints: null }  // null = inherit global

PUT    /api/platform/loyalty/tenant/{tenantId}/config
       Body: { mode: "Alliance", isAllianceMember: true, maxWalletPoints: 50000 }
       → 200 OK or 403 (non-SystemAdmin)
```

### 5.3. Customer — Wallet Balance (Alliance mode)

```
GET    /api/loyalty/wallet
       Header: X-Customer-Token
       → {
           totalPointBalance: 150,
           breakdown: [
             { tenantId: "A", tenantName: "Coffee An An", points: 80 },
             { tenantId: "B", tenantName: "Tra Sua XYZ", points: 70 }
           ],
           recentTransactions: [
             { type: "EARN", tenantName: "Coffee An An", points: 30, reason: "Order completed", timestamp: "..." },
             { type: "REDEEM", tenantName: "Tra Sua XYZ", points: -50, reason: "Voucher VAN-XXX", timestamp: "..." }
           ]
         }
```

### 5.4. Customer — Redeem Cross-Tenant (Alliance mode)

```
POST   /api/redemption/redeem
       Header: X-Customer-Token
       Body: { catalogItemId: "guid", tenantId: "target-tenant-guid" }
       → {
           success: true,
           pointsSpent: 50,
           newPointBalance: 100,   // AllianceWallet.TotalPointBalance
           voucherCode: "VAN-XXX",
           redeemAtTenant: "Tra Sua XYZ"
         }
```

---

## 6. Effective Mode Resolution

```csharp
public async Task<LoyaltyMode> GetEffectiveModeAsync(Guid tenantId)
{
    // 1. Check tenant override
    var tenantConfig = await _tenantConfigRepo.GetByTenantIdAsync(tenantId);
    if (tenantConfig?.Mode != null)
        return tenantConfig.Mode.Value;

    // 2. Fall back to global
    var globalConfig = await _globalConfigRepo.GetSingletonAsync();
    return globalConfig.Mode;
}
```

**Mọi flow loyalty (EARN, REDEEM) đều gọi `GetEffectiveModeAsync` trước để quyết định route:**
- `Silo` → existing `LoyaltyRewardsService` (SQLite)
- `Alliance` → new `AllianceWalletService` (PostgreSQL)

---

## 7. NATS Events

| Event | Subject | Payload | Mục đích |
|-------|---------|---------|---------|
| `LoyaltyPointsChanged` | `vanan.cloud.loyalty.changed.{customerDeviceId}` | `{ walletId, totalBalance, change, tenantId, type }` | Sync wallet balance to all ShopERP instances for local display |

ShopERP subscriber nhận event → update local `LoyaltyRewards.PointBalance` để UI hiển thị đúng即使在 Alliance mode.

---

## 8. Migration Plan

### Phase 1: Domain + Infrastructure
- Add entities: `LoyaltyGlobalConfig`, `LoyaltyTenantConfig`, `AllianceWallet`, `AllianceTransaction`
- Add EF configurations + migration (PostgreSQL)
- Add `LoyaltyMode` enum to Domain

### Phase 2: Services
- `AllianceWalletService` — manage wallet, transactions
- `LoyaltyModeResolver` — resolve effective mode per tenant
- Modify `OrderWorkflowService.ProcessLoyaltyPointsAsync` — branch by mode
- Modify `RedemptionService.RedeemAsync` — branch by mode

### Phase 3: API
- SystemAdmin endpoints: global config, per-tenant config
- Customer wallet endpoint
- Modify redeem endpoint to accept `tenantId` parameter

### Phase 4: Mode Switch Migration
- Silo → Alliance: consolidate wallets
- Alliance → Silo: split wallets back

### Phase 5: UI
- SystemAdmin: loyalty config panel (global + per-tenant)
- Customer: wallet view with breakdown by tenant
- Customer: cross-tenant redeem UI

### Phase 6: Testing
- Unit tests: mode resolution, wallet operations, cross-tenant redeem
- Integration tests: mode switch migration, NATS sync
- E2E tests: earn at tenant A → redeem at tenant B

---

## 9. Resolved Decisions

| # | Question | Decision | Chi tiết |
|---|----------|----------|----------|
| 1 | Khi switch Alliance → Silo, điểm chia theo tenant nào? | **Chia theo nguồn (xuất xứ)** | Dựa trên `AllianceTransaction` history: tính tổng EARN per-tenant, chia `TotalPointBalance` theo tỷ lệ đóng góp. Vd: tenant A đóng 80đ, tenant B đóng 70đ → 80/70. Nếu 1 tenant đã REDEEM hết phần của mình, phần còn lại về tenant còn điểm. |
| 2 | Tenant từ chối alliance thì phạm vi ảnh hưởng? | **Opt-out toàn phần** | `IsAllianceMember = false` → tenant đó hoàn toàn Silo: không nhận điểm ngoài, điểm tích tại đó cũng không cho đổi ở tenant khác. Coi như Silo hoàn toàn cho tenant đó bất kể global mode. |
| 3 | Tenant Admin có thấy cross-tenant history? | **Chỉ thấy tại tenant** | Tenant Admin chỉ thấy transaction xảy ra tại tenant mình (EARN + REDEEM). Không thấy giao dịch ở tenant khác. SystemAdmin thấy all. |
| 4 | Refund points khi hủy voucher cross-tenant? | **Refund về tenant nơi redeem** | Points hoàn về `LoyaltyRewards` tại tenant nơi voucher được redeem (không hoàn về AllianceWallet). Lý do: tenant đó đã "trả quà" cho khách, khi hủy thì điểm quay về tenant đó. |
| 5 | Giới hạn số điểm tối đa trong wallet? | **Có giới hạn configurable** | SystemAdmin set `MaxWalletPoints` ở global config. Per-tenant override được qua `LoyaltyTenantConfig.MaxWalletPoints`. Khi đạt limit, EARN mới bị từ chối + thông báo khách. Default: 100,000 điểm. |

---

## 10. Non-Goals (Out of Scope)

- Conversion rate giữa các tenant (giá trị điểm đồng nhất, không cần conversion)
- Cross-tenant redemption catalog (catalog vẫn per-tenant, chỉ điểm là cross-tenant)
- Customer identity unification (vẫn dùng DeviceId/phone để match cross-tenant, không tạo global customer entity)
- Loyalty tier calculation cross-tenant (tier vẫn per-tenant based on local spend)
