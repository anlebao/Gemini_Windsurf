# Sprint 0 Detailed Plan — Foundation: Domain + Migration + Anti-Fraud (v1.5 verified)

> **STATUS: COMPLETE 2026-07-26** — All 4 sessions (S1-S4) executed. 11 entities + 9 enums + 8 Order fields + 42 tests PASS + migration applied (local + VPS PG) + VPS deployed via CD #30201482750 + RV ALL 18 SC PASS. Merged to `main` (fast-forward `89e33480..f563e415`). See `task_cc_sprint0_foundation-2c5017.md` Section 11 for completion summary.
>
> **v1.5 VERIFICATION (2026-07-29):** Base code đối chiếu 100% pass — 11 entities confirmed (Domain.cs:3191-3716) + 11 EF configs + migration + 59 community tests + 39 architecture tests + guard-check ALL PASSED + fingerprint JS tồn tại. **GAP duy nhất:** fingerprint wire-up chưa hoàn thành (CC-S0-T3 Sprint 0.5 sẽ xử lý).

Kế hoạch chi tiết cho Sprint 0: TDD plan (25+ test cases — v1.2: tăng từ 22), coding plan (4 sessions — v1.2: tăng từ 3), 11 entity definitions (v1.2: tăng từ 9), EF configuration specs, migration steps, **device fingerprint JS + risk scoring service (v1.2 NEW)**.

> **v1.2 changes (incremental trên v1.1):**
> - Thêm 2 entity: `DeviceRegistration` (device fingerprint + token, max 3 per customer), `FraudFlag` (admin review queue).
> - Thêm `RiskScore` (int 0-100) + `RiskFactors` (JSON) + `HoldUntil` (DateTime?) trên `SalesReferral` + `AppInstallAttribution`.
> - Mở rộng `CommissionStatus` enum: +`Rejected=3`, +`Held=4`.
> - Mở rộng `AttributionStatus` enum: +`Rejected=3`, +`Held=4`.
> - Mở rộng `IdentityLevel` enum: +`DeviceVerified=4`.
> - Thêm 3 enums mới: `FraudEntityType`, `FraudFlagType`, `FraudFlagStatus`.
> - Device fingerprint JS (FingerprintJS v4, MIT, vendored self-host) trong `wwwroot/`.
> - RiskScoringService — compute deterministic RiskScore 0-100 từ 8 factors.
> - SMS OTP OPTIONAL (không bắt buộc). WebAuthn Passkey OPTIONAL (defer Sprint 7+).

---

## 1. ENTITY DEFINITIONS (Domain.cs) — v1.2: 11 entities

### 1.1 Enums (v1.2: 9 enums thay vì 6)

```csharp
public enum CommunityRoleType
{
    Shipper = 1,
    Salesman = 2
}

public enum DeliveryTaskStatus
{
    Assigned = 1,
    PickedUp = 2,
    OutForDelivery = 3,
    Delivered = 4,
    Failed = 5,
    Cancelled = 6
}

public enum WalletTransactionType
{
    CODCollection = 1,
    AdvancePayment = 2,
    Commission = 3,
    Withdrawal = 4,
    Settlement = 5,
    Reversal = 6 // v1.1 NEW — negating entry for wrong COD amount
}

public enum CommissionStatus
{
    Pending = 1,
    Paid = 2
}

public enum BonusStatus // v1.1 NEW
{
    None = 0,
    Pending = 1,
    Paid = 2
}

public enum AttributionStatus // v1.1 NEW, v1.2: + Rejected, Held
{
    Pending = 1,
    Paid = 2,
    Rejected = 3, // v1.2 NEW — RiskScore>=80 auto-reject hoặc admin
    Held = 4      // v1.2 NEW — RiskScore 60-79 hold 48h
}

public enum FraudEntityType // v1.2 NEW
{
    Customer = 1,
    Order = 2,
    SalesReferral = 3,
    AppInstallAttribution = 4,
    DeviceRegistration = 5
}

public enum FraudFlagType // v1.2 NEW
{
    SelfDeal = 1,              // salesman + customer cùng fingerprint
    AccountFarming = 2,        // 1 device nhiều accounts
    BotBehavior = 3,           // app-install <30s, >3 accounts/device/day
    WashTrading = 4,           // order → cancel → re-order
    SuspiciousFingerprint = 5, // fingerprint match blacklisted
    DeviceLimitExceeded = 6,   // >3 devices per customer
    HighRiskScore = 7          // RiskScore>=60 catch-all
}

public enum FraudFlagStatus // v1.2 NEW
{
    Pending = 1,
    Reviewed = 2,
    Confirmed = 3,
    Dismissed = 4
}
```

### 1.2 CommunityRole Entity

```csharp
public class CommunityRole : BaseEntity
{
    public Guid CustomerId { get; protected set; }
    public CommunityRoleType RoleType { get; protected set; }
    public DateTime ActivatedAt { get; protected set; }
    public Guid ActivatedBy { get; protected set; }
    public DateTime? DeactivatedAt { get; protected set; }
    public bool IsActive { get; protected set; } = true;
    public string? SalesmanCode { get; protected set; }

    protected CommunityRole() { }

    public CommunityRole(TenantId tenantId, Guid customerId, CommunityRoleType roleType, Guid activatedBy)
        : base(tenantId)
    {
        CustomerId = customerId;
        RoleType = roleType;
        ActivatedBy = activatedBy;
        ActivatedAt = DateTime.UtcNow;
        IsActive = true;
        if (roleType == CommunityRoleType.Salesman)
            SalesmanCode = GenerateSalesmanCode();
    }

    public void Deactivate()
    {
        IsActive = false;
        DeactivatedAt = DateTime.UtcNow;
        UpdateAudit();
    }

    private static string GenerateSalesmanCode()
    {
        // 6 chars, uppercase alphanumeric, exclude ambiguous chars (0, O, I, 1)
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
```

### 1.3 DeliveryTask Entity

```csharp
public class DeliveryTask : BaseEntity
{
    public Guid OrderId { get; protected set; }
    public Guid ShipperId { get; protected set; }
    public DeliveryTaskStatus Status { get; protected set; } = DeliveryTaskStatus.Assigned;
    public DateTime AssignedAt { get; protected set; }
    public DateTime? PickedUpAt { get; protected set; }
    public DateTime? OutForDeliveryAt { get; protected set; }
    public DateTime? DeliveredAt { get; protected set; }
    public DateTime? FailedAt { get; protected set; }
    public string? FailureReason { get; protected set; }
    public double ShopLat { get; protected set; }
    public double ShopLng { get; protected set; }
    public double? CustomerLat { get; protected set; }
    public double? CustomerLng { get; protected set; }

    protected DeliveryTask() { }

    public DeliveryTask(TenantId tenantId, Guid orderId, Guid shipperId, double shopLat, double shopLng, double? customerLat = null, double? customerLng = null)
        : base(tenantId)
    {
        OrderId = orderId;
        ShipperId = shipperId;
        Status = DeliveryTaskStatus.Assigned;
        AssignedAt = DateTime.UtcNow;
        ShopLat = shopLat;
        ShopLng = shopLng;
        CustomerLat = customerLat;
        CustomerLng = customerLng;
    }

    public void MarkPickedUp()
    {
        if (Status != DeliveryTaskStatus.Assigned)
            throw new InvalidOperationException($"Cannot transition from {Status} to PickedUp");
        Status = DeliveryTaskStatus.PickedUp;
        PickedUpAt = DateTime.UtcNow;
        UpdateAudit();
    }

    public void MarkOutForDelivery()
    {
        if (Status != DeliveryTaskStatus.PickedUp)
            throw new InvalidOperationException($"Cannot transition from {Status} to OutForDelivery");
        Status = DeliveryTaskStatus.OutForDelivery;
        OutForDeliveryAt = DateTime.UtcNow;
        UpdateAudit();
    }

    public void MarkDelivered()
    {
        if (Status != DeliveryTaskStatus.OutForDelivery)
            throw new InvalidOperationException($"Cannot transition from {Status} to Delivered");
        Status = DeliveryTaskStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
        UpdateAudit();
    }

    public void MarkFailed(string reason)
    {
        if (Status is DeliveryTaskStatus.Delivered or DeliveryTaskStatus.Cancelled)
            throw new InvalidOperationException($"Cannot transition from {Status} to Failed");
        Status = DeliveryTaskStatus.Failed;
        FailedAt = DateTime.UtcNow;
        FailureReason = reason ?? "Unknown";
        UpdateAudit();
    }

    public void Cancel()
    {
        if (Status is DeliveryTaskStatus.Delivered or DeliveryTaskStatus.Failed)
            throw new InvalidOperationException($"Cannot transition from {Status} to Cancelled");
        Status = DeliveryTaskStatus.Cancelled;
        UpdateAudit();
    }
}
```

### 1.4 DeliveryTracking Entity (append-only)

```csharp
public class DeliveryTracking : BaseEntity
{
    public Guid DeliveryTaskId { get; protected set; }
    public double Latitude { get; protected set; }
    public double Longitude { get; protected set; }
    public DateTime RecordedAt { get; protected set; }

    protected DeliveryTracking() { }

    public DeliveryTracking(TenantId tenantId, Guid deliveryTaskId, double lat, double lng)
        : base(tenantId)
    {
        DeliveryTaskId = deliveryTaskId;
        Latitude = lat;
        Longitude = lng;
        RecordedAt = DateTime.UtcNow;
    }
    // No update methods — append-only by design
}
```

### 1.5 Conversation + Message Entities

```csharp
public class Conversation : BaseEntity
{
    public Guid OrderId { get; protected set; }
    public Guid ShipperId { get; protected set; }
    public Guid CustomerId { get; protected set; }

    protected Conversation() { }

    public Conversation(TenantId tenantId, Guid orderId, Guid shipperId, Guid customerId)
        : base(tenantId)
    {
        OrderId = orderId;
        ShipperId = shipperId;
        CustomerId = customerId;
    }
}

public class Message : BaseEntity
{
    public Guid ConversationId { get; protected set; }
    public Guid SenderId { get; protected set; }
    public string Content { get; protected set; } = string.Empty;
    public DateTime SentAt { get; protected set; }
    public bool IsRead { get; protected set; }

    protected Message() { }

    public Message(TenantId tenantId, Guid conversationId, Guid senderId, string content)
        : base(tenantId)
    {
        ConversationId = conversationId;
        SenderId = senderId;
        Content = content;
        SentAt = DateTime.UtcNow;
        IsRead = false;
    }

    public void MarkAsRead()
    {
        IsRead = true;
        UpdateAudit();
    }
}
```

### 1.6 SalesReferral Entity (v1.1 — REDESIGN: composite code + per-product commission + app-install bonus)

```csharp
public class SalesReferral : BaseEntity, IMustHaveTenant
{
    public Guid SalesmanId { get; protected set; }
    public string SalesmanCode { get; protected set; } = string.Empty;
    public Guid ProductId { get; protected set; } // v1.1 NEW — product salesman chọn giới thiệu
    public string? ProductShortCode { get; protected set; } // v1.1 NEW — phần product của composite code
    public Guid? ReferredCustomerId { get; protected set; }
    public Guid? OrderId { get; protected set; }
    public decimal CommissionAmount { get; protected set; }
    public decimal CommissionRate { get; protected set; } // v1.1 NEW — snapshot rate tại thời điểm chốt đơn (audit)
    public CommissionStatus CommissionStatus { get; protected set; } = CommissionStatus.Pending;
    public decimal AppInstallBonusAmount { get; protected set; } = 0m; // v1.1 NEW
    public BonusStatus AppInstallBonusStatus { get; protected set; } = BonusStatus.None; // v1.1 NEW
    public Guid? AppInstallAttributionId { get; protected set; } // v1.1 NEW — link tới attribution nếu có
    public DateTime CreatedAt { get; protected set; }

    protected SalesReferral() { }

    public SalesReferral(TenantId tenantId, Guid salesmanId, string salesmanCode, Guid productId, string? productShortCode = null)
        : base(tenantId)
    {
        SalesmanId = salesmanId;
        SalesmanCode = salesmanCode;
        ProductId = productId;
        ProductShortCode = productShortCode;
        CreatedAt = DateTime.UtcNow;
    }

    public void AttachToOrder(Guid orderId, Guid customerId, decimal orderTotal, decimal commissionRate)
    {
        OrderId = orderId;
        ReferredCustomerId = customerId;
        CommissionRate = commissionRate; // snapshot từ ProductReferralConfig
        CommissionAmount = orderTotal * commissionRate;
        CommissionStatus = CommissionStatus.Pending;
        UpdateAudit();
    }

    public void MarkCommissionPaid()
    {
        CommissionStatus = CommissionStatus.Paid;
        UpdateAudit();
    }

    // v1.1 NEW — attach app-install bonus từ AppInstallAttribution
    public void AttachAppInstallBonus(Guid attributionId, decimal bonusAmount)
    {
        AppInstallAttributionId = attributionId;
        AppInstallBonusAmount = bonusAmount; // snapshot từ ProductReferralConfig.AppInstallBonus
        AppInstallBonusStatus = BonusStatus.Pending;
        UpdateAudit();
    }

    public void MarkAppInstallBonusPaid()
    {
        AppInstallBonusStatus = BonusStatus.Paid;
        UpdateAudit();
    }

    // v1.2 NEW — risk scoring + hold/reject
    public void SetRiskScore(int riskScore, string riskFactors)
    {
        RiskScore = riskScore;
        RiskFactors = riskFactors;
        if (riskScore >= 80)
        {
            CommissionStatus = CommissionStatus.Rejected;
        }
        else if (riskScore >= 60)
        {
            CommissionStatus = CommissionStatus.Held;
            HoldUntil = DateTime.UtcNow.AddHours(48);
        }
        UpdateAudit();
    }

    public void MarkRejected(string reason)
    {
        CommissionStatus = CommissionStatus.Rejected;
        UpdateAudit();
    }

    public void MarkHeld(DateTime holdUntil)
    {
        CommissionStatus = CommissionStatus.Held;
        HoldUntil = holdUntil;
        UpdateAudit();
    }

    public void ApproveAfterHold()
    {
        // Called sau cooling period (24h) hoặc admin review dismiss
        CommissionStatus = CommissionStatus.Pending; // ready for payout
        HoldUntil = null;
        UpdateAudit();
    }

    // v1.2 NEW fields
    public int RiskScore { get; protected set; } = 0;
    public string? RiskFactors { get; protected set; } // JSON
    public DateTime? HoldUntil { get; protected set; }
}
```

### 1.7 WalletTransaction Entity (immutable + Reversal pattern — v1.1)

```csharp
public class WalletTransaction : BaseEntity, IMustHaveTenant
{
    public Guid OwnerId { get; protected set; }
    public WalletTransactionType Type { get; protected set; }
    public decimal Amount { get; protected set; } // Reversal entry có Amount = -original (v1.1)
    public string Description { get; protected set; } = string.Empty;
    public Guid? RelatedOrderId { get; protected set; }
    public Guid? RelatedTransactionId { get; protected set; } // v1.1 NEW — Reversal entry reference original
    public decimal BalanceAfter { get; protected set; }
    public DateTime CreatedAt { get; protected set; }

    protected WalletTransaction() { }

    public WalletTransaction(TenantId tenantId, Guid ownerId, WalletTransactionType type, decimal amount, decimal balanceBefore, string description, Guid? relatedOrderId = null, Guid? relatedTransactionId = null)
        : base(tenantId)
    {
        OwnerId = ownerId;
        Type = type;
        Amount = amount;
        BalanceAfter = balanceBefore + amount; // Reversal: amount âm → BalanceAfter giảm
        Description = description;
        RelatedOrderId = relatedOrderId;
        RelatedTransactionId = relatedTransactionId; // v1.1 — set cho Reversal entry
        CreatedAt = DateTime.UtcNow;
    }
    // No update methods — immutable by design (like AccountingEntry)
    // Reversal: tạo entry mới Type=Reversal, Amount=-original.Amount, RelatedTransactionId=original.Id
}
```

### 1.8 Order Fields (additions to existing Order class — v1.1: + ReferralProductId)

```csharp
// Add to Order class:
public Guid? ShipperId { get; protected set; }
public Guid? SalesmanId { get; protected set; }
public string? ReferralCode { get; protected set; } // composite "{salesmanCode}|{productShortCode}" (v1.1)
public Guid? ReferralProductId { get; protected set; } // v1.1 NEW — product salesman chọn giới thiệu
public double? DeliveryLat { get; protected set; }
public double? DeliveryLng { get; protected set; }
public decimal? CodAmount { get; protected set; }
public DateTime? CodCollectedAt { get; protected set; }
```

### 1.9 Customer Fields — v1.1 REMOVED

~~`CurrentLat`, `CurrentLng`, `LocationUpdatedAt` + `UpdateLocation` method~~ — **Bỏ** (v1.1). Lý do: Shipper location đã có `DeliveryTracking` (append-only per DeliveryTask). Customer delivery location đã có `Order.DeliveryLat/Lng`. Customer "current location" không phục vụ UC nào trong spec → thêm field không dùng = tech debt.

### 1.10 ProductReferralConfig Entity (v1.1 NEW — per-product commission + app-install bonus)

```csharp
public class ProductReferralConfig : BaseEntity, IMustHaveTenant
{
    public Guid ProductId { get; protected set; } // unique (1 config per product)
    public string? ProductShortCode { get; protected set; } // 20 chars, unique within tenant
    public decimal CommissionRate { get; protected set; } // 2-5% (0.02m - 0.05m), do sysadmin set
    public decimal AppInstallBonus { get; protected set; } // bonus cố định khi customer cài app qua referral
    public bool IsActive { get; protected set; } = true;
    public DateTime CreatedAt { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }

    protected ProductReferralConfig() { }

    public ProductReferralConfig(TenantId tenantId, Guid productId, decimal commissionRate, decimal appInstallBonus, string? productShortCode = null)
        : base(tenantId)
    {
        if (commissionRate < 0.02m || commissionRate > 0.05m)
            throw new ArgumentOutOfRangeException(nameof(commissionRate), "CommissionRate must be between 0.02 and 0.05 (2-5%)");
        if (appInstallBonus < 0m)
            throw new ArgumentOutOfRangeException(nameof(appInstallBonus), "AppInstallBonus cannot be negative");
        ProductId = productId;
        CommissionRate = commissionRate;
        AppInstallBonus = appInstallBonus;
        ProductShortCode = productShortCode;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(decimal commissionRate, decimal appInstallBonus, string? productShortCode, bool isActive)
    {
        if (commissionRate < 0.02m || commissionRate > 0.05m)
            throw new ArgumentOutOfRangeException(nameof(commissionRate), "CommissionRate must be between 0.02 and 0.05 (2-5%)");
        if (appInstallBonus < 0m)
            throw new ArgumentOutOfRangeException(nameof(appInstallBonus), "AppInstallBonus cannot be negative");
        CommissionRate = commissionRate;
        AppInstallBonus = appInstallBonus;
        ProductShortCode = productShortCode;
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
        UpdateAudit();
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
        UpdateAudit();
    }
}
```

### 1.11 AppInstallAttribution Entity (v1.1 NEW — track app install cho salesman bonus)

```csharp
public class AppInstallAttribution : BaseEntity, IMustHaveTenant
{
    public Guid CustomerId { get; protected set; } // unique (1 customer 1 attribution)
    public Guid SalesmanId { get; protected set; }
    public Guid ProductId { get; protected set; } // product referral
    public Guid? SalesReferralId { get; protected set; } // link tới SalesReferral nếu có order sau đó
    public decimal BonusAmount { get; protected set; } // snapshot từ ProductReferralConfig.AppInstallBonus
    public AttributionStatus AttributionStatus { get; protected set; } = AttributionStatus.Pending;
    public DateTime InstalledAt { get; protected set; }
    public Guid? WalletTransactionId { get; protected set; } // WalletTransaction tạo cho salesman

    // v1.2 NEW — risk scoring
    public int RiskScore { get; protected set; } = 0;
    public string? RiskFactors { get; protected set; } // JSON
    public DateTime? HoldUntil { get; protected set; }
    public Guid? DeviceRegistrationId { get; protected set; } // v1.2 NEW — link tới device đã cài app

    protected AppInstallAttribution() { }

    public AppInstallAttribution(TenantId tenantId, Guid customerId, Guid salesmanId, Guid productId, decimal bonusAmount, Guid? deviceRegistrationId = null)
        : base(tenantId)
    {
        CustomerId = customerId;
        SalesmanId = salesmanId;
        ProductId = productId;
        BonusAmount = bonusAmount; // snapshot từ ProductReferralConfig.AppInstallBonus
        AttributionStatus = AttributionStatus.Pending;
        InstalledAt = DateTime.UtcNow;
        DeviceRegistrationId = deviceRegistrationId; // v1.2
    }

    public void MarkPaid(Guid walletTransactionId)
    {
        AttributionStatus = AttributionStatus.Paid;
        WalletTransactionId = walletTransactionId;
        UpdateAudit();
    }

    // v1.2 NEW — risk scoring + hold/reject
    public void SetRiskScore(int riskScore, string riskFactors)
    {
        RiskScore = riskScore;
        RiskFactors = riskFactors;
        if (riskScore >= 80)
        {
            AttributionStatus = AttributionStatus.Rejected;
        }
        else if (riskScore >= 60)
        {
            AttributionStatus = AttributionStatus.Held;
            HoldUntil = DateTime.UtcNow.AddHours(48);
        }
        UpdateAudit();
    }

    public void MarkRejected(string reason)
    {
        AttributionStatus = AttributionStatus.Rejected;
        UpdateAudit();
    }

    public void MarkHeld(DateTime holdUntil)
    {
        AttributionStatus = AttributionStatus.Held;
        HoldUntil = holdUntil;
        UpdateAudit();
    }

    public void ApproveAfterHold()
    {
        AttributionStatus = AttributionStatus.Pending; // ready for payout
        HoldUntil = null;
        UpdateAudit();
    }
}
```

### 1.12 DeviceRegistration Entity (v1.2 NEW — self-hosted device fingerprint + token)

```csharp
public class DeviceRegistration : BaseEntity, IMustHaveTenant
{
    public Guid CustomerId { get; protected set; }
    public string DeviceToken { get; protected set; } = string.Empty; // 64 chars, server-signed UUIDv7+HMAC
    public string FingerprintHash { get; protected set; } = string.Empty; // 64 chars SHA256
    public string FingerprintSignals { get; protected set; } = string.Empty; // JSON raw signals
    public DateTime FirstSeenAt { get; protected set; }
    public DateTime LastSeenAt { get; protected set; }
    public bool IsActive { get; protected set; } = true;
    public bool IsVerified { get; protected set; } = false; // admin review passed
    public string UserAgent { get; protected set; } = string.Empty; // 500 chars
    public string Platform { get; protected set; } = string.Empty; // 50 chars
    public string IpAddress { get; protected set; } = string.Empty; // 50 chars
    public int RiskScore { get; protected set; } = 0; // device-level risk

    protected DeviceRegistration() { }

    public DeviceRegistration(TenantId tenantId, Guid customerId, string deviceToken, string fingerprintHash, string fingerprintSignals, string userAgent, string platform, string ipAddress)
        : base(tenantId)
    {
        CustomerId = customerId;
        DeviceToken = deviceToken;
        FingerprintHash = fingerprintHash;
        FingerprintSignals = fingerprintSignals;
        UserAgent = userAgent;
        Platform = platform;
        IpAddress = ipAddress;
        FirstSeenAt = DateTime.UtcNow;
        LastSeenAt = DateTime.UtcNow;
        IsActive = true;
        IsVerified = false;
    }

    public void Touch(DateTime lastSeenAt, string ipAddress)
    {
        LastSeenAt = lastSeenAt;
        IpAddress = ipAddress;
        UpdateAudit();
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdateAudit();
    }

    public void Verify()
    {
        IsVerified = true;
        UpdateAudit();
    }

    public void UpdateRiskScore(int score)
    {
        RiskScore = score;
        UpdateAudit();
    }
}
```

**Constraints (application-layer):**
- Max 3 active DeviceRegistration per Customer — count active before insert, throw `DeviceLimitExceededException` if 4th
- Device 4+ → create with `IsActive=false` + create FraudFlag(FlagType=DeviceLimitExceeded)
- Unique index on `DeviceToken` (1 token = 1 device)
- Index on `(CustomerId, IsActive)` (query active devices per customer)
- Index on `FingerprintHash` (query: ai khác dùng fingerprint này? — anti-fraud check)

### 1.13 FraudFlag Entity (v1.2 NEW — admin review queue)

```csharp
public class FraudFlag : BaseEntity, IMustHaveTenant
{
    public FraudEntityType EntityType { get; protected set; }
    public Guid EntityId { get; protected set; }
    public Guid? CustomerId { get; protected set; } // customer liên quan (nullable — có thể flag device)
    public FraudFlagType FlagType { get; protected set; }
    public int RiskScore { get; protected set; } // snapshot tại thời điểm flag
    public string RiskFactors { get; protected set; } = string.Empty; // JSON chi tiết factors
    public string Description { get; protected set; } = string.Empty; // 500 chars human-readable
    public FraudFlagStatus Status { get; protected set; } = FraudFlagStatus.Pending;
    public Guid? ReviewedBy { get; protected set; } // admin user Id
    public DateTime? ReviewedAt { get; protected set; }
    public string? ReviewNote { get; protected set; } // 500 chars
    public DateTime CreatedAt { get; protected set; }

    protected FraudFlag() { }

    public FraudFlag(TenantId tenantId, FraudEntityType entityType, Guid entityId, Guid? customerId, FraudFlagType flagType, int riskScore, string riskFactors, string description)
        : base(tenantId)
    {
        EntityType = entityType;
        EntityId = entityId;
        CustomerId = customerId;
        FlagType = flagType;
        RiskScore = riskScore;
        RiskFactors = riskFactors;
        Description = description;
        Status = FraudFlagStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public void Confirm(Guid reviewedBy, string note)
    {
        Status = FraudFlagStatus.Confirmed;
        ReviewedBy = reviewedBy;
        ReviewedAt = DateTime.UtcNow;
        ReviewNote = note;
        UpdateAudit();
    }

    public void Dismiss(Guid reviewedBy, string note)
    {
        Status = FraudFlagStatus.Dismissed;
        ReviewedBy = reviewedBy;
        ReviewedAt = DateTime.UtcNow;
        ReviewNote = note;
        UpdateAudit();
    }

    public void MarkReviewed(Guid reviewedBy, string note)
    {
        Status = FraudFlagStatus.Reviewed;
        ReviewedBy = reviewedBy;
        ReviewedAt = DateTime.UtcNow;
        ReviewNote = note;
        UpdateAudit();
    }
}
```

**Indexes:**
- Index on `(Status, CreatedAt)` — admin dashboard query pending flags sort by date
- Index on `(EntityType, EntityId)` — query flags per entity
- Index on `CustomerId` — query flags per customer (3-strike check)

### 1.14 IdentityLevel expansion (v1.2 NEW)

```csharp
// Modify existing IdentityLevel enum:
public enum IdentityLevel
{
    Guest = 0,
    Social = 1,
    Verified = 2,    // SMS OTP verified
    Full = 3,
    DeviceVerified = 4 // v1.2 NEW — device fingerprint + behavioral check passed (KHÔNG cần SMS)
}
```

**Lý do:** Customer không muốn verify SĐT vẫn có thể dùng community features nếu device fingerprint + behavioral pass. `DeviceVerified` tương đương `Verified` cho community role activation (UC-02).

---

## 2. TDD PLAN (25+ TEST CASES — v1.2: tăng từ 22)

### File: `6_Tests/VanAn.Core.Tests/CommunityRoleTests.cs`

| # | Test Name | What It Verifies |
|---|---|---|
| 1 | `CommunityRole_Create_Shipper_ValidFields` | RoleType=Shipper, IsActive=true, SalesmanCode=null |
| 2 | `CommunityRole_Create_Salesman_GeneratesCode` | SalesmanCode 6 chars, not null, alphanumeric |
| 3 | `CommunityRole_Deactivate_SetsDeactivatedAt` | IsActive=false, DeactivatedAt not null |
| 4 | `CommunityRole_SalesmanCode_Unique_AcrossInstances` | Two Salesman roles have different codes |

### File: `6_Tests/VanAn.Core.Tests/DeliveryTaskTests.cs`

| # | Test Name | What It Verifies |
|---|---|---|
| 5 | `DeliveryTask_Create_Status_Assigned` | Status=Assigned, AssignedAt set |
| 6 | `DeliveryTask_Transition_AssignedToPickedUp` | Status=PickedUp, PickedUpAt set |
| 7 | `DeliveryTask_Transition_PickedUpToOutForDelivery` | Status=OutForDelivery |
| 8 | `DeliveryTask_Transition_OutForDeliveryToDelivered` | Status=Delivered, DeliveredAt set |
| 9 | `DeliveryTask_Transition_InvalidThrows` | Delivered→PickedUp throws InvalidOperationException |
| 10 | `DeliveryTask_MarkFailed_WithReason` | Status=Failed, FailureReason set |

### File: `6_Tests/VanAn.Core.Tests/WalletTransactionTests.cs`

| # | Test Name | What It Verifies |
|---|---|---|
| 11 | `WalletTransaction_Create_BalanceAfterCorrect` | BalanceAfter = balanceBefore + amount |
| 12 | `WalletTransaction_Immutable_NoUpdateMethod` | Verify no public update methods exist (reflection check) |
| 13 | `WalletTransaction_Reversal_CreatesNegatingEntry` (v1.1 NEW) | Reversal entry: Type=Reversal, Amount=-original, RelatedTransactionId=original.Id, BalanceAfter = balanceBefore + (-original.Amount) |

### File: `6_Tests/VanAn.Core.Tests/OrderCommunityFieldsTests.cs`

| # | Test Name | What It Verifies |
|---|---|---|
| 14 | `Order_NewFields_DefaultNull` (v1.1: +ReferralProductId) | ShipperId, SalesmanId, ReferralCode, ReferralProductId, DeliveryLat, DeliveryLng, CodAmount, CodCollectedAt all null |

### File: `6_Tests/VanAn.Core.Tests/SalesReferralTests.cs` (v1.1 NEW)

| # | Test Name | What It Verifies |
|---|---|---|
| 15 | `SalesReferral_AttachToOrder_CommissionFromProductConfig` (v1.1 NEW) | CommissionAmount = orderTotal * commissionRate (per-product, KHÔNG hardcode) |
| 16 | `SalesReferral_AttachAppInstallBonus_SetsBonusAmount` (v1.1 NEW) | AppInstallBonusAmount set, AppInstallBonusStatus=Pending |

### File: `6_Tests/VanAn.Core.Tests/ProductReferralConfigTests.cs` (v1.1 NEW)

| # | Test Name | What It Verifies |
|---|---|---|
| 17 | `ProductReferralConfig_Create_ValidFields` (v1.1 NEW) | CommissionRate 2-5%, AppInstallBonus, ProductShortCode, IsActive=true |
| 18 | `ProductReferralConfig_Create_InvalidRate_Throws` (v1.1 NEW) | CommissionRate < 0.02 or > 0.05 throws ArgumentOutOfRangeException |

### File: `6_Tests/VanAn.Core.Tests/AppInstallAttributionTests.cs` (v1.1 NEW)

| # | Test Name | What It Verifies |
|---|---|---|
| 19 | `AppInstallAttribution_Create_UniquePerCustomer` (v1.1 NEW) | 1 customer 1 attribution (unique constraint — verify via EF config + test) |
| 20 | `AppInstallAttribution_MarkPaid_SetsWalletTransactionId` (v1.1 NEW) | AttributionStatus=Paid, WalletTransactionId set |

### File: `6_Tests/VanAn.Architecture.Tests/WalletTransactionImmutabilityTests.cs` (v1.1 NEW)

| # | Test Name | What It Verifies |
|---|---|---|
| 21 | `WalletTransaction_Immutable_NoPublicSetter` (v1.1 NEW) | Reflection: all mutable fields have protected setter (no public setter) |
| 22 | `WalletTransaction_NoUpdateMethod` (v1.1 NEW) | Reflection: no public method named "Update*" on WalletTransaction |

**Total: 22 test cases from v1.1 + v1.2 additions below**

### File: `6_Tests/VanAn.Core.Tests/DeviceRegistrationTests.cs` (v1.2 NEW)

| # | Test Name | What It Verifies |
|---|---|---|
| 23 | `DeviceRegistration_Create_ValidFields` (v1.2 NEW) | DeviceToken 64 chars, FingerprintHash 64 chars, IsActive=true, IsVerified=false |
| 24 | `DeviceRegistration_Touch_UpdatesLastSeenAndIp` (v1.2 NEW) | LastSeenAt + IpAddress update |
| 25 | `DeviceRegistration_Deactivate_SetsIsActiveFalse` (v1.2 NEW) | IsActive=false after Deactivate() |
| 26 | `DeviceRegistration_Verify_SetsIsVerifiedTrue` (v1.2 NEW) | IsVerified=true after Verify() |

### File: `6_Tests/VanAn.Core.Tests/FraudFlagTests.cs` (v1.2 NEW)

| # | Test Name | What It Verifies |
|---|---|---|
| 27 | `FraudFlag_Create_Status_Pending` (v1.2 NEW) | Status=Pending, CreatedAt set |
| 28 | `FraudFlag_Confirm_SetsStatusConfirmed` (v1.2 NEW) | Status=Confirmed, ReviewedBy+ReviewedAt+ReviewNote set |
| 29 | `FraudFlag_Dismiss_SetsStatusDismissed` (v1.2 NEW) | Status=Dismissed, ReviewedBy+ReviewedAt+ReviewNote set |

### File: `6_Tests/VanAn.Core.Tests/RiskScoringServiceTests.cs` (v1.2 NEW)

| # | Test Name | What It Verifies |
|---|---|---|
| 30 | `RiskScore_AllFactorsZero_Returns0` (v1.2 NEW) | No risk factors → score 0 |
| 31 | `RiskScore_SameFingerprint_Adds50` (v1.2 NEW) | salesmanFingerprint==customerFingerprint → +50 |
| 32 | `RiskScore_SameFingerprintPlusNewCustomer_Returns80` (v1.2 NEW) | +50 + +30 (customerAgeDays<7) = 80 → auto-reject |
| 33 | `RiskScore_BotInstall_Adds40` (v1.2 NEW) | appInstallTime<30s → +40 |
| 34 | `RiskScore_BlacklistedFingerprint_Adds60` (v1.2 NEW) | blacklistedFingerprint → +60 |
| 35 | `RiskScore_Deterministic_SameInputSameOutput` (v1.2 NEW) | Verify deterministic — same input always produces same score |

### File: `6_Tests/VanAn.Core.Tests/SalesReferralRiskScoreTests.cs` (v1.2 NEW — risk on entity)

| # | Test Name | What It Verifies |
|---|---|---|
| 36 | `SalesReferral_SetRiskScore_60_SetsHeld` (v1.2 NEW) | RiskScore=60 → CommissionStatus=Held, HoldUntil=now+48h |
| 37 | `SalesReferral_SetRiskScore_80_SetsRejected` (v1.2 NEW) | RiskScore=80 → CommissionStatus=Rejected |
| 38 | `SalesReferral_SetRiskScore_30_StaysPending` (v1.2 NEW) | RiskScore=30 → CommissionStatus=Pending (no hold) |
| 39 | `SalesReferral_ApproveAfterHold_ClearsHold` (v1.2 NEW) | ApproveAfterHold() → CommissionStatus=Pending, HoldUntil=null |

### File: `6_Tests/VanAn.Core.Tests/AppInstallAttributionRiskScoreTests.cs` (v1.2 NEW)

| # | Test Name | What It Verifies |
|---|---|---|
| 40 | `AppInstallAttribution_SetRiskScore_60_SetsHeld` (v1.2 NEW) | RiskScore=60 → AttributionStatus=Held, HoldUntil=now+48h |
| 41 | `AppInstallAttribution_SetRiskScore_80_SetsRejected` (v1.2 NEW) | RiskScore=80 → AttributionStatus=Rejected |
| 42 | `AppInstallAttribution_SetRiskScore_30_StaysPending` (v1.2 NEW) | RiskScore=30 → AttributionStatus=Pending |

**Total: 42 test cases (≥25 minimum met — v1.2)**

---

## 3. EF CONFIGURATION SPECS

### 3.1 CommunityRoleConfiguration.cs
```csharp
public class CommunityRoleConfiguration : IEntityTypeConfiguration<CommunityRole>
{
    public void Configure(EntityTypeBuilder<CommunityRole> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CustomerId).IsRequired();
        builder.Property(e => e.RoleType).HasConversion<int>().IsRequired();
        builder.Property(e => e.ActivatedBy).IsRequired();
        builder.Property(e => e.ActivatedAt).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();
        builder.Property(e => e.SalesmanCode).HasMaxLength(10);
        builder.HasIndex(e => e.SalesmanCode).IsUnique(); // Only non-null values unique
        builder.HasIndex(e => new { e.CustomerId, e.RoleType, e.IsActive });
        builder.Property(e => e.TenantId).IsRequired();
    }
}
```

### 3.2 DeliveryTaskConfiguration.cs
```csharp
public class DeliveryTaskConfiguration : IEntityTypeConfiguration<DeliveryTask>
{
    public void Configure(EntityTypeBuilder<DeliveryTask> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.OrderId).IsRequired();
        builder.Property(e => e.ShipperId).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.AssignedAt).IsRequired();
        builder.Property(e => e.ShopLat).IsRequired();
        builder.Property(e => e.ShopLng).IsRequired();
        builder.Property(e => e.FailureReason).HasMaxLength(500);
        builder.HasIndex(e => e.OrderId);
        builder.HasIndex(e => e.ShipperId);
        builder.HasIndex(e => new { e.OrderId, e.Status }); // For "active task per order" check
        builder.Property(e => e.TenantId).IsRequired();
    }
}
```

### 3.3 DeliveryTrackingConfiguration.cs
```csharp
public class DeliveryTrackingConfiguration : IEntityTypeConfiguration<DeliveryTracking>
{
    public void Configure(EntityTypeBuilder<DeliveryTracking> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.DeliveryTaskId).IsRequired();
        builder.Property(e => e.Latitude).IsRequired();
        builder.Property(e => e.Longitude).IsRequired();
        builder.Property(e => e.RecordedAt).IsRequired();
        builder.HasIndex(e => new { e.DeliveryTaskId, e.RecordedAt });
        builder.Property(e => e.TenantId).IsRequired();
    }
}
```

### 3.4 ConversationConfiguration.cs + MessageConfiguration.cs
```csharp
// Conversation
builder.HasKey(e => e.Id);
builder.Property(e => e.OrderId).IsRequired();
builder.Property(e => e.ShipperId).IsRequired();
builder.Property(e => e.CustomerId).IsRequired();
builder.HasIndex(e => e.OrderId).IsUnique(); // 1 conversation per order

// Message
builder.HasKey(e => e.Id);
builder.Property(e => e.ConversationId).IsRequired();
builder.Property(e => e.SenderId).IsRequired();
builder.Property(e => e.Content).IsRequired().HasMaxLength(2000);
builder.Property(e => e.SentAt).IsRequired();
builder.HasIndex(e => e.ConversationId);
```

### 3.5 SalesReferralConfiguration.cs (v1.1 — redesign)
```csharp
builder.HasKey(e => e.Id);
builder.Property(e => e.SalesmanId).IsRequired();
builder.Property(e => e.SalesmanCode).IsRequired().HasMaxLength(10);
builder.Property(e => e.ProductId).IsRequired(); // v1.1 NEW
builder.Property(e => e.ProductShortCode).HasMaxLength(20); // v1.1 NEW
builder.Property(e => e.CommissionAmount).HasPrecision(18, 2);
builder.Property(e => e.CommissionRate).HasPrecision(18, 4); // v1.1 NEW — snapshot rate
builder.Property(e => e.CommissionStatus).HasConversion<int>();
builder.Property(e => e.AppInstallBonusAmount).HasPrecision(18, 2); // v1.1 NEW
builder.Property(e => e.AppInstallBonusStatus).HasConversion<int>(); // v1.1 NEW
builder.HasIndex(e => e.SalesmanCode);
builder.HasIndex(e => e.OrderId);
builder.HasIndex(e => e.ProductId); // v1.1 NEW — query referrals per product
```

### 3.6 WalletTransactionConfiguration.cs (v1.1 — + RelatedTransactionId)
```csharp
builder.HasKey(e => e.Id);
builder.Property(e => e.OwnerId).IsRequired();
builder.Property(e => e.Type).HasConversion<int>().IsRequired();
builder.Property(e => e.Amount).HasPrecision(18, 2).IsRequired();
builder.Property(e => e.BalanceAfter).HasPrecision(18, 2).IsRequired();
builder.Property(e => e.Description).IsRequired().HasMaxLength(500);
builder.Property(e => e.RelatedTransactionId); // v1.1 NEW — Reversal entry reference
builder.HasIndex(e => e.OwnerId);
builder.HasIndex(e => e.RelatedOrderId);
builder.HasIndex(e => e.RelatedTransactionId); // v1.1 NEW — query reversal for original
```

### 3.7 ProductReferralConfigConfiguration.cs (v1.1 NEW)
```csharp
public class ProductReferralConfigConfiguration : IEntityTypeConfiguration<ProductReferralConfig>
{
    public void Configure(EntityTypeBuilder<ProductReferralConfig> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ProductId).IsRequired();
        builder.Property(e => e.ProductShortCode).HasMaxLength(20);
        builder.Property(e => e.CommissionRate).HasPrecision(18, 4).IsRequired();
        builder.Property(e => e.AppInstallBonus).HasPrecision(18, 2).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();
        builder.Property(e => e.TenantId).IsRequired();
        builder.HasIndex(e => e.ProductId).IsUnique(); // 1 config per product
        builder.HasIndex(e => new { e.TenantId, e.ProductShortCode }).IsUnique()
              .HasFilter("\"ProductShortCode\" IS NOT NULL"); // unique short code within tenant (filtered)
    }
}
```

### 3.8 AppInstallAttributionConfiguration.cs (v1.1 NEW)
```csharp
public class AppInstallAttributionConfiguration : IEntityTypeConfiguration<AppInstallAttribution>
{
    public void Configure(EntityTypeBuilder<AppInstallAttribution> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CustomerId).IsRequired();
        builder.Property(e => e.SalesmanId).IsRequired();
        builder.Property(e => e.ProductId).IsRequired();
        builder.Property(e => e.BonusAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(e => e.AttributionStatus).HasConversion<int>().IsRequired();
        builder.Property(e => e.InstalledAt).IsRequired();
        builder.Property(e => e.TenantId).IsRequired();
        builder.HasIndex(e => e.CustomerId).IsUnique(); // 1 customer 1 attribution (UC-12 AC-12.2)
        builder.HasIndex(e => e.SalesmanId); // query bonus per salesman
        builder.HasIndex(e => e.ProductId);
    }
}
```

### 3.9 Order Field Additions (in existing OrderConfiguration.cs — v1.1: + ReferralProductId)
```csharp
// In OrderConfiguration.cs — add:
builder.Property(o => o.ShipperId);
builder.Property(o => o.SalesmanId);
builder.Property(o => o.ReferralCode).HasMaxLength(30); // composite "{salesmanCode}|{productShortCode}" (v1.1: tăng từ 20)
builder.Property(o => o.ReferralProductId); // v1.1 NEW
builder.Property(o => o.DeliveryLat);
builder.Property(o => o.DeliveryLng);
builder.Property(o => o.CodAmount).HasPrecision(18, 2);
builder.Property(o => o.CodCollectedAt);
builder.HasIndex(o => o.ShipperId);
builder.HasIndex(o => o.SalesmanId);
builder.HasIndex(o => o.ReferralProductId); // v1.1 NEW
```

### 3.10 VanAnDbContext OnModelCreating — Ignore list additions
```csharp
// Add to existing Ignore list (line ~135-148):
// No new value objects to ignore — all 9 entities use BaseEntity.Id directly (Single-Identity Pattern, v1.1)
```

### 3.11 IVanAnDbContext + VanAnDbContext — DbSet additions (v1.2: 11 DbSet thay vì 9)
```csharp
// Add to IVanAnDbContext:
DbSet<CommunityRole> CommunityRoles { get; }
DbSet<DeliveryTask> DeliveryTasks { get; }
DbSet<DeliveryTracking> DeliveryTrackings { get; }
DbSet<Conversation> Conversations { get; }
DbSet<Message> Messages { get; }
DbSet<SalesReferral> SalesReferrals { get; }
DbSet<WalletTransaction> WalletTransactions { get; }
DbSet<ProductReferralConfig> ProductReferralConfigs { get; } // v1.1 NEW
DbSet<AppInstallAttribution> AppInstallAttributions { get; } // v1.1 NEW
DbSet<DeviceRegistration> DeviceRegistrations { get; } // v1.2 NEW
DbSet<FraudFlag> FraudFlags { get; } // v1.2 NEW

// Add to VanAnDbContext (same properties with { get; set; })
```

### 3.12 DeviceRegistrationConfiguration.cs (v1.2 NEW)
```csharp
public class DeviceRegistrationConfiguration : IEntityTypeConfiguration<DeviceRegistration>
{
    public void Configure(EntityTypeBuilder<DeviceRegistration> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CustomerId).IsRequired();
        builder.Property(e => e.DeviceToken).IsRequired().HasMaxLength(64);
        builder.Property(e => e.FingerprintHash).IsRequired().HasMaxLength(64);
        builder.Property(e => e.FingerprintSignals).IsRequired(); // JSON
        builder.Property(e => e.UserAgent).HasMaxLength(500);
        builder.Property(e => e.Platform).HasMaxLength(50);
        builder.Property(e => e.IpAddress).HasMaxLength(50);
        builder.Property(e => e.TenantId).IsRequired();
        builder.HasIndex(e => e.DeviceToken).IsUnique(); // 1 token = 1 device
        builder.HasIndex(e => new { e.CustomerId, e.IsActive }); // query active devices per customer
        builder.HasIndex(e => e.FingerprintHash); // anti-fraud check: ai khác dùng fingerprint này?
    }
}
```

### 3.13 FraudFlagConfiguration.cs (v1.2 NEW)
```csharp
public class FraudFlagConfiguration : IEntityTypeConfiguration<FraudFlag>
{
    public void Configure(EntityTypeBuilder<FraudFlag> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EntityType).HasConversion<int>().IsRequired();
        builder.Property(e => e.EntityId).IsRequired();
        builder.Property(e => e.FlagType).HasConversion<int>().IsRequired();
        builder.Property(e => e.RiskFactors).IsRequired(); // JSON
        builder.Property(e => e.Description).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.ReviewNote).HasMaxLength(500);
        builder.Property(e => e.TenantId).IsRequired();
        builder.HasIndex(e => new { e.Status, e.CreatedAt }); // admin dashboard pending flags sort by date
        builder.HasIndex(e => new { e.EntityType, e.EntityId }); // query flags per entity
        builder.HasIndex(e => e.CustomerId); // 3-strike check
    }
}
```

### 3.14 SalesReferral/AppInstallAttribution Field Additions (v1.2 NEW — modify existing configs)
```csharp
// In SalesReferralConfiguration.cs (v1.1) — add:
builder.Property(e => e.RiskScore).HasDefaultValue(0);
builder.Property(e => e.RiskFactors); // JSON, nullable
builder.Property(e => e.HoldUntil);
builder.HasIndex(e => e.CommissionStatus); // query Held/Pending/Rejected

// In AppInstallAttributionConfiguration.cs (v1.1) — add:
builder.Property(e => e.RiskScore).HasDefaultValue(0);
builder.Property(e => e.RiskFactors); // JSON, nullable
builder.Property(e => e.HoldUntil);
builder.Property(e => e.DeviceRegistrationId);
builder.HasIndex(e => e.AttributionStatus); // query Held/Pending/Rejected
```

---

## 4. CODING PLAN — SESSION BREAKDOWN (v1.2: 4 sessions thay vì 3)

### Session S1: Domain Entities + Unit Tests (TDD)

**JIT Planning output:**
- Exact entity code (Section 1 above — 11 entities, 9 enums — v1.2: +DeviceRegistration, +FraudFlag, +3 fraud enums)
- Exact test code (Section 2 above — 42 test cases — v1.2: +20 cases)
- File: `1_Shared/Domain.cs` — append after existing entities
- Files: 11 test files in `6_Tests/VanAn.Core.Tests/` + 1 architecture test file

**Pure Execution:**
1. Write test files FIRST (TDD) — 11 unit test files + 1 architecture test file
2. Add 9 enums to `Domain.cs` (v1.2: +3 fraud enums)
3. Add 11 entity classes to `Domain.cs` (v1.2: +DeviceRegistration, +FraudFlag)
4. Add 8 fields to `Order` class (v1.1: +ReferralProductId, bỏ Customer fields)
5. Add RiskScore/RiskFactors/HoldUntil fields + SetRiskScore/MarkHeld/MarkRejected/ApproveAfterHold methods to SalesReferral + AppInstallAttribution (v1.2 NEW)
6. Modify IdentityLevel enum: add DeviceVerified=4 (v1.2 NEW)
7. `dotnet build` — fix compile errors
8. `dotnet test 6_Tests/VanAn.Core.Tests/` — all 42 tests pass (v1.2)
9. `dotnet test 6_Tests/VanAn.Architecture.Tests/` — WalletTransactionImmutabilityTests PASS

**End of S1:** Domain entities exist, tests pass, build green.

### Session S2: EF Configuration + DbContext

**JIT Planning output:**
- Exact EF config code (Section 3 above — 11 config files + 2 modifications — v1.2: +DeviceRegistration, +FraudFlag)
- Files: 11 new config files + 3 existing config files to modify (Order, SalesReferral, AppInstallAttribution)
- IVanAnDbContext + VanAnDbContext changes (11 DbSet)

**Pure Execution:**
1. Create 11 EF Configuration files (v1.2: +DeviceRegistration, +FraudFlag)
2. Modify `OrderConfiguration.cs` — add 8 new field configs
3. Modify `SalesReferralConfiguration.cs` — add RiskScore/RiskFactors/HoldUntil + index CommissionStatus (v1.2 NEW)
4. Modify `AppInstallAttributionConfiguration.cs` — add RiskScore/RiskFactors/HoldUntil/DeviceRegistrationId + index AttributionStatus (v1.2 NEW)
5. Add 11 DbSet to `IVanAnDbContext.cs`
6. Add 11 DbSet to `VanAnDbContext.cs`
7. `dotnet build` — fix errors
8. Verify `ApplyConfigurationsFromAssembly` picks up new configs

**End of S2:** EF configs wired, build green.

### Session S3: RiskScoringService + Device Fingerprint JS + IWalletService base (v1.4 NEW — moved from Sprint 5)

**JIT Planning output:**
- RiskScoringService interface + implementation (8 factors deterministic)
- FingerprintJS v4 (MIT) vendored library + JS interop wrapper
- **v1.4 NEW: IWalletService.CreateTransactionAsync base method** (atomic BalanceAfter — HR-SCALE-3) — moved from Sprint 5 vì Sprint 4 CoolingPeriodJob cần tạo WalletTransaction
- 5 risk scoring unit tests (cases 30-35) + 3 wallet base unit tests (v1.4 NEW)

**Pure Execution:**
1. Create `3_CoreHub/Services/IRiskScoringService.cs` + `RiskScoringService.cs` (deterministic 8-factor scoring)
2. Write 5 RiskScoringServiceTests (cases 30-35)
3. Vendor FingerprintJS v4 (MIT) into `5_WebApps/KhachLink/wwwroot/lib/fingerprintjs/fingerprint.js`
4. Create `5_WebApps/KhachLink/wwwroot/js/fingerprint.js` — JS interop wrapper (collect signals, hash, return)
5. **v1.4 NEW: Create `3_CoreHub/Services/IWalletService.cs`** with base method:
   ```csharp
   public interface IWalletService
   {
       // v1.4: Base atomic method — dùng bởi Sprint 4 CoolingPeriodJob + Sprint 5 full WalletService
       Task<WalletTransaction> CreateTransactionAsync(Guid ownerId, WalletTransactionType type, decimal amount, string description, Guid? relatedOrderId = null, Guid? relatedTransactionId = null);
       Task<decimal> GetBalanceAsync(Guid ownerId);
   }
   ```
6. **v1.4 NEW: Create `3_CoreHub/Services/WalletService.cs`** base impl:
   ```csharp
   public class WalletService : IWalletService
   {
       // v1.4: HR-SCALE-3 atomic BalanceAfter — SELECT FOR UPDATE pattern
       public async Task<WalletTransaction> CreateTransactionAsync(...)
       {
           using var tx = await _dbContext.BeginTransactionAsync();
           // Lock last transaction row for this owner
           var lastTx = await _dbContext.WalletTransactions
               .FromSqlRaw("SELECT * FROM \"WalletTransactions\" WHERE \"OwnerId\" = {0} ORDER BY \"CreatedAt\" DESC LIMIT 1 FOR UPDATE", ownerId)
               .FirstOrDefaultAsync();
           var balanceBefore = lastTx?.BalanceAfter ?? 0m;
           var wt = new WalletTransaction(tenantId, ownerId, type, amount, balanceBefore, description, relatedOrderId, relatedTransactionId);
           _dbContext.WalletTransactions.Add(wt);
           await _dbContext.SaveChangesAsync();
           await tx.CommitAsync();
           return wt;
       }
   }
   ```
7. **v1.4 NEW: Write 3 WalletService base unit tests:**
   - `WalletService_CreateTransaction_BalanceAfterCorrect` — balanceBefore + amount
   - `WalletService_CreateTransaction_Concurrent_NoRace` — 2 concurrent creates → 2 different BalanceAfter (atomic)
   - `WalletService_GetBalance_NoTransactions_ReturnsZero` — empty wallet
8. Register IRiskScoringService + IWalletService in DI (Gateway + CoreHub)
9. `dotnet build` — fix errors
10. Run 5 RiskScoringServiceTests + 3 WalletServiceTests — pass
11. Verify `curl https://localhost:5002/js/fingerprint.js` returns JS content (local)

**End of S3:** Risk scoring service + device fingerprint JS + **WalletService base (atomic)** ready. Sprint 4 CoolingPeriodJob có thể dùng IWalletService.CreateTransactionAsync.

### Session S4: Migration + Regression + Final

**JIT Planning output:**
- Migration command
- Expected new tables + columns (11 tables + 8 Order columns + 3 SalesReferral + 4 AppInstallAttribution new columns — v1.2: +2 tables + 7 columns)
- SQLite compatibility check
- Regression test targets (OTP + Google login existing — OPTIONAL v1.2)

**Pure Execution:**
1. `dotnet ef migrations add CommunitySprint0 --project 3_CoreHub/Infrastructure --startup-project 2_Gateway`
2. Review generated migration — verify 11 new tables + 8 Order columns + 3 SalesReferral + 4 AppInstallAttribution new columns
3. `dotnet ef database update --project 3_CoreHub/Infrastructure --startup-project 2_Gateway`
4. Verify 11 tables exist in PG
5. If ShopERP SQLite needs migration: `dotnet ef migrations add CommunitySprint0 --project 5_WebApps/ShopERP/Infrastructure --startup-project 5_WebApps/ShopERP`
6. Apply SQLite migration
7. Run existing `CustomerIdentityController` tests — verify OTP login still works (regression, OPTIONAL v1.2)
8. Run existing `SocialAuthController` tests — verify Google login still works
9. Run `guard-check.ps1` — ALL PASSED
10. Run `dotnet test 6_Tests/VanAn.Architecture.Tests/` — no dependency violations + WalletTransactionImmutabilityTests PASS
11. Run full `dotnet build VanAn.sln --configuration Release` — 0 errors
12. Run all unit tests — 0 failures (42 cases v1.2)
13. Update `project_state.md` — Sprint 0 COMPLETE

**End of S4:** Migrations applied, regression pass, all SC pass, Sprint 0 ready for VPS verification.

---

## 5. VPS VERIFICATION SCRIPT (Sprint 0 — v1.1: bỏ Google login, +2 tables)

```powershell
# scripts/verify-sprint0.ps1
param([string]$Domain)

$results = @()

# RV0-1: Gateway health
$resp = Invoke-WebRequest -Uri "https://$Domain/api/health" -UseBasicParsing -ErrorAction SilentlyContinue
$results += [PSCustomObject]@{ Test="RV0-1 Gateway health"; Status=$(if ($resp.StatusCode -eq 200) {"PASS"} else {"FAIL"}); Expected="200"; Actual="$($resp.StatusCode)" }

# RV0-2: OTP send
$resp = Invoke-WebRequest -Uri "https://$Domain/api/customer-identity/otp/send" -Method POST -Body '{"phoneNumber":"0901234567"}' -ContentType "application/json" -UseBasicParsing -ErrorAction SilentlyContinue
$results += [PSCustomObject]@{ Test="RV0-2 OTP send"; Status=$(if ($resp.StatusCode -eq 200) {"PASS"} else {"FAIL"}); Expected="200"; Actual="$($resp.StatusCode)" }

# RV0-3 (v1.1: REMOVED — Google login đã verify trong Tiered Auth P1, không cần re-verify)

# RV0-4: DB migration — CommunityRoles table exists
# (SSH command — run manually or via GitHub Actions)
# docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c '\dt CommunityRoles'

# RV0-5 (v1.1 NEW): DB migration — ProductReferralConfigs table exists
# docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c '\dt ProductReferralConfigs'

# RV0-6 (v1.1 NEW): DB migration — AppInstallAttributions table exists
# docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c '\dt AppInstallAttributions'

# RV0-7 (v1.1 NEW): DB migration SQLite — all 9 community tables exist
# docker exec vanan-shoperp sqlite3 /data/shoperp.db ".tables" | grep -E "DeliveryTask|ProductReferralConfig|AppInstallAttribution|SalesReferral|WalletTransaction"

# Summary
$failed = $results | Where-Object { $_.Status -ne "PASS" }
if ($failed) { Write-Host "VERIFICATION FAILED" -ForegroundColor Red; $failed | Format-Table; exit 1 }
else { Write-Host "SPRINT 0 VPS VERIFICATION ALL PASSED" -ForegroundColor Green; $results | Format-Table }
```

---

## 6. RISKS (Sprint 0 specific — v1.1)

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| EF migration conflict với existing migrations | LOW | HIGH | `dotnet ef migrations add` trên branch mới, clean DB |
| ~~PII encryption cho Customer new fields~~ (v1.1: bỏ — không thêm Customer fields) | — | — | — |
| Architecture test fail (dependency direction) | LOW | HIGH | Entities trong Domain.cs, configs trong Infrastructure — đúng layer |
| SQLite migration incompatibility | MEDIUM | MEDIUM | Test SQLite migration riêng, PG-specific features avoided |
| `SalesmanCode` uniqueness race | LOW | LOW | DB unique index + retry logic (Sprint 4) |
| `ProductReferralConfig.CommissionRate` validation (v1.1 NEW) | LOW | MEDIUM | Domain constructor throw nếu rate < 0.02 hoặc > 0.05 |
| `AppInstallAttribution.CustomerId` unique constraint (v1.1 NEW) | LOW | HIGH | DB unique index + application check trước insert (catch race condition) |
| Single-Identity Pattern violation (v1.1 NEW) | LOW | HIGH | Tất cả entity dùng `BaseEntity.Id` trực tiếp — không business key VO → không dual-identity bug |
