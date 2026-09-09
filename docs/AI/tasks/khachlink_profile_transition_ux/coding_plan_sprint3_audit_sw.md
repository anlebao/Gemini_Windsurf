# Coding Plan — Sprint 3: Audit Log + SW Version Bump + Security Monitoring Dashboard

> **Task card:** `docs/AI/tasks/khachlink_profile_transition_ux/task_card_sprint3_audit_sw.md`
> **Master plan:** `docs/AI/tasks/khachlink_profile_transition_ux/master_plan.md`
> **Prerequisite:** ✅ Sprint 2 COMPLETE + PUSHED + RV PASS (`61e4d4d4` on `main`) — WhatsNewBanner, CartPreservationModal, OnboardingTour, ProfileChangeDirection enum, UpdatedAt client model, nav IDs all live
> **Mode:** IMPLEMENT (enum additive + service inject + UI enhance + JS interop, no migration)
> **Branch target:** new branch `feature/khachlink-sprint3-audit-sw` from `main` @ `61e4d4d4`

---

## 0. Pre-Implementation Verification (RESOLVED open questions)

Các open questions trong task card đã verify qua codebase exploration (2026-09-09):

| # | Question | Resolution | Evidence |
|---|---|---|---|
| OQ1 | `IAuditTrailService` DI in Gateway? | ✅ **REGISTERED** — `AddScoped<IAuditTrailService, AuditTrailService>()` in Gateway `Program.cs` line 514, ShopERP `Program.cs` line 256, CoreHub `Program.cs` line 109. All 3 hosts register it. | `2_Gateway/Program.cs:514`, `5_WebApps/ShopERP/Program.cs:256`, `3_CoreHub/Program.cs:109` |
| OQ2 | `_tenantProvider.TenantId` for SystemAdmin? | ✅ **`Guid.Empty` (platform mode)** — `AuditTrailService.IsSystemAdminWithoutTenant()` (line 321-333) returns `true` when SystemAdmin + `!_tenantProvider.HasTenant`. `LogUpdateAsync` uses `new TenantId(_tenantProvider.TenantId)` — for SystemAdmin without tenant, this is `Guid.Empty`. `KhachLinkInstance.TenantId = Guid.Empty` → audit log tenant matches. ✅ | `3_CoreHub/Services/AuditTrailService.cs:321-333` |
| OQ3 | Audit query endpoint existing? | ✅ **EXISTS** — `AuditTrailController` at route `api/audit-trail` (NOT `api/v1/audit-logs`). Endpoints: `GET query`, `GET entity/{entityType}/{entityId}`, `GET recent`, `GET correlation/{correlationId}`. Authorized `Roles = "Admin"`. `GetEntityHistory` returns `IReadOnlyList<AuditLog>`. | `2_Gateway/Controllers/AuditTrailController.cs:72-96` |
| OQ4 | `KhachLinkInstanceDto.UpdatedAt`? | ✅ **EXISTS** — `KhachLinkInstanceDto.UpdatedAt` (DateTime) mapped from `BaseEntity.UpdatedAt` in `ToDto` (line 236). Client-side `ByDomainResponse.UpdatedAt` added in Sprint 2. | `2_Gateway/Controllers/KhachLinkInstanceController.cs:275,236` |
| OQ5 | AuditTrail.razor existing? | ✅ **EXISTS** — `/admin/audit-trail` page with filter panel, results table, detail modal, pagination. Uses UI Platform components (VanAButton, VanACard, VanAModal, VanAAlert, VanASpinner, VanASelect, VanAInput). `[Authorize(Policy = "SystemAdmin")]`. | `5_WebApps/ShopERP/Components/Pages/Admin/AuditTrail.razor` |
| OQ6 | Security event logging at source? | ✅ **IDENTIFIED** — `PlatformUserLoginService.LoginAsync` (line 19-41) returns `null` on: user not found, BCrypt verify fail, user inactive. No audit logging currently. Rate limiter `OnRejected` in Gateway `Program.cs` line 209-215 returns 429 but doesn't log. Both are inject points for security audit. | `3_CoreHub/Services/PlatformUserLoginService.cs:19-41`, `2_Gateway/Program.cs:209-215` |

**Additional findings:**
- `AuditLog` entity has `Reason` field — used by `ForPeriodClose`/`ForPeriodReopen`/`ForCorrection`/`ForReversal` but NOT by `ForCreate`/`ForUpdate`/`ForDelete` (set to null). Security events need `Reason` → new factory method `ForSecurityEvent` required.
- `AuditActionType` enum: 11 values (Create=1 ... PermissionChange=11). New values 12-15 are additive, stored as int, backward compatible.
- `AuditableEntityType` enum: 11 values (AccountingEntry=1 ... LoyaltyRewards=11). New values 12-13 are additive.
- `AuditTrail.razor` EntityType filter dropdown only lists 6 of 11 types — needs update to include new types.
- `KhachLinkInstanceService` constructor: `(IVanAnDbContext dbContext, ILogger<KhachLinkInstanceService>? logger = null)` — inject point for `IAuditTrailService`.
- `KhachLinkInstance.UpdateProfile(profile, navFlagsOverride)` calls `UpdateAudit()` (sets `UpdatedAt` = `DateTime.UtcNow`).
- Gateway rate limiter `OnRejected` delegate can resolve scoped services via `context.HttpContext.RequestServices`.
- `AdminLayout.razor` sidebar menu has "Audit Trail" at `/admin/audit-trail` (line 58) — no new nav link needed (enhance existing page).

---

## 0.5. EXPANDED SCOPE (user-approved 2026-09-09): Audit Toggle + Hybrid Async Persist

Scope expansion được user duyệt trong session review Sprint 3 — ngoài plan gốc:

### Design decisions (user-approved)
1. **Audit toggle — 4 flags** (1 master + 3 nhóm), SystemAdmin ON/OFF runtime, không restart:
   - `Audit_Enabled` (master, default **BẬT**) — OFF → không ghi bất kỳ audit log nào
   - `Audit_Accounting` (default **BẬT**) — AccountingEntry Create/Update/Delete + PeriodClose/Reopen + Correction/Reversal
   - `Audit_Security` (default **BẬT**) — FailedLogin + RateLimitHit + SuspiciousActivity + SecurityAlert
   - `Audit_KhachLink` (default **BẬT**) — KhachLinkInstance profile change
   - EntityType không thuộc 3 nhóm (Customer/Order/Product/...) → chỉ phụ thuộc master switch
2. **Hybrid async persist** (user chọn hybrid — kế toán sync):
   - **Accounting audit = SYNCHRONOUS** (`await` repository AddAsync) — bút toán kế toán phải có audit trail đảm bảo, không chấp nhận mất log khi crash (TT 152/2025 compliance)
   - **Security + KhachLink audit = ASYNC fire-and-forget** (enqueue `AuditLogQueue` → `AuditLogBackgroundWriter` batch INSERT) — không chặn luồng chính (login, 429, profile update)
3. **`IFeatureFlagService.IsEnabledAsync` thêm optional param** `defaultWhenMissing = false` — audit flags gọi với `true` (default ON), VALCN v2 flags giữ default OFF. Backward compatible với mọi caller hiện có.

### Hybrid persist matrix
| Log call | Toggle gate | Persist | Lý do |
|---|---|---|---|
| `LogCreateAsync/LogUpdateAsync/LogDeleteAsync` với `AccountingEntry` | master + `Audit_Accounting` | **SYNC** (await DB) | Toàn vẹn kế toán |
| `LogPeriodCloseAsync/LogPeriodReopenAsync` (EntityType=PeriodClosing) | master + `Audit_Accounting` | **SYNC** | Kế toán |
| `LogCorrectionAsync/LogReversalAsync` (EntityType=AccountingEntry) | master + `Audit_Accounting` | **SYNC** | Kế toán |
| `LogUpdateAsync` với `KhachLinkInstance` | master + `Audit_KhachLink` | **ASYNC** (queue) | Hiếm, không critical |
| `LogSecurityEventAsync` (EntityType=SecurityEvent) | master + `Audit_Security` | **ASYNC** (queue) | Best-effort, không chặn login/429 |
| Các EntityType khác | master only | **ASYNC** (queue) | Generic |

### Trade-offs (user chấp nhận)
- Security/KhachLink log có thể mất nếu process crash đột ngột (kill -9/OOM) — tối đa bằng queue capacity (1000) + batch chờ flush. Mitigation: bounded channel `DropOldest` + flush mỗi 5s hoặc đủ 100 items + residual flush khi graceful shutdown.
- Accounting audit KHÔNG mất — sync giữ nguyên. Nếu audit write fail → operation fail theo (hành vi hiện tại của `AccountingEntryService`, đúng cho compliance).
- `Log*Async` return type đổi `Task<AuditLog>` → `Task<AuditLog?>` — trả `null` khi toggle OFF. Callers hiện có discard return value (verified: `AccountingEntryService.cs:93`, Sprint 3 Step 4/5/6 đều discard) — source compatible.
- Query methods (`QueryAsync`/`GetEntityHistoryAsync`/`GetRecentAsync`/`GetByCorrelationIdAsync`) KHÔNG bị toggle — đọc audit luôn cho phép.
- Async audit rows xuất hiện trong DB trễ tối đa ~5s (flush interval) — RV Layer 4 phải chờ ~5s trước khi query.

---

## 1. Implementation Order (dependency-aware)

```
Step 1:  Add enum values to AuditLog.cs (AuditableEntityType + AuditActionType)  [Domain — additive]
Step 2:  Add ForSecurityEvent factory method to AuditLog entity                   [Domain — additive]
Step 1A: IFeatureFlagService + FeatureFlagService — defaultWhenMissing + 4 audit flags  [EXPANDED — toggle]
Step 3A: Create AuditLogQueue (bounded Channel<AuditLog>)                         [EXPANDED — async queue]
Step 3:  Add LogSecurityEventAsync + toggle gating + hybrid persist to IAuditTrailService + AuditTrailService  [Service — MODIFIED by expansion]
Step 3B: Create AuditLogBackgroundWriter + DI registration (Gateway + ShopERP)    [EXPANDED — async writer]
Step 4:  Inject IAuditTrailService into KhachLinkInstanceService, log UpdateAsync [Task 3.1 — audit profile change]
Step 5:  Log failed login in PlatformUserLoginService                             [Task 3.3 — security event at source]
Step 6:  Log rate limit hits in Gateway Program.cs OnRejected                     [Task 3.3 — security event at source]
Step 7:  Enhance AuditTrail.razor — summary cards + severity classification       [Task 3.4 — monitoring dashboard]
Step 7A: Audit toggles hiển thị trong admin feature-flag UI                       [EXPANDED — toggle UI, verify-only]
Step 8:  Create KhachLinkInstanceAudit.razor + link from KhachLinkInstances.razor [Task 3.1 — per-instance history]
Step 9:  SW version bump (onboarding-tour.js + KhachLinkLayout)                    [Task 3.2 — SW update trigger]
Step 10: Build + fix errors
Step 10A: Unit tests — toggle gating + queue/writer flush                          [EXPANDED — tests]
Step 11: E2E test additions (profile-transition.spec.ts)
Step 12: RV on production (timlathay.com + diemthuong2.khachvip.online)
```

**Why this order:** Steps 1-2 are Domain foundation (enum + factory). Step 1A phải chạy trước Step 3 (AuditTrailService cần `IsEnabledAsync` overload). Step 3A phải chạy trước Step 3 (hybrid persist cần queue). Step 3 modified (toggle + hybrid + LogSecurityEventAsync). Step 3B sau Step 3 (writer tiêu thụ những gì service enqueue). Step 4 depends on Step 1 (enum value). Steps 5-6 depend on Steps 2-3 (factory + service method). Step 7 depends on Steps 1-3 (enum values for classification). Step 7A phụ thuộc Step 1A (KnownFeatures đã có audit flags). Step 8 depends on Step 4 (audit logs exist to query). Step 9 is independent (client-side JS). Steps 10-12 are verification.

---

## 2. Step-by-Step Implementation

### Step 1: Add enum values to `AuditLog.cs` (Domain — additive)

#### File 1: `1_Shared/Domain/Audit/AuditLog.cs` — UPDATE

**1a. Add `AuditableEntityType` values** (after `LoyaltyRewards = 11`, line 38):

```csharp
public enum AuditableEntityType
{
    AccountingEntry = 1,
    PeriodClosing = 2,
    Customer = 3,
    Order = 4,
    Product = 5,
    Inventory = 6,
    Shop = 7,
    User = 8,
    Tenant = 9,
    SocialCampaign = 10,
    LoyaltyRewards = 11,
    // Sprint 3: platform-level + security entities
    KhachLinkInstance = 12,  // P1.2 — KhachLink profile change audit
    SecurityEvent = 13      // P3.3 — security incident (failed login, rate limit, suspicious activity)
}
```

**1b. Add `AuditActionType` values** (after `PermissionChange = 11`, line 21):

```csharp
public enum AuditActionType
{
    Create = 1,
    Update = 2,
    Delete = 3,
    PeriodClose = 4,
    PeriodReopen = 5,
    Correction = 6,
    Reversal = 7,
    Export = 8,
    Login = 9,
    Logout = 10,
    PermissionChange = 11,
    // Sprint 3: security event actions
    SecurityAlert = 12,       // P3.3 — generic security alert (manual or system-detected)
    FailedLogin = 13,         // P3.3 — failed login attempt (bad credentials, inactive user)
    SuspiciousActivity = 14,  // P3.3 — suspicious pattern detected (repeated failures, anomaly)
    RateLimitHit = 15         // P3.3 — rate limit triggered (potential brute-force / abuse)
}
```

**Notes:**
- Additive only — values 12-15 appended, existing 1-11 unchanged. Enum stored as int in PG, backward compatible.
- No migration needed — existing rows have int values 1-11, new rows can have 12-15.
- `KhachLinkInstance = 12` for profile change audit (Task 3.1).
- `SecurityEvent = 13` for security incidents that don't map to a specific business entity (Task 3.3).
- `SecurityAlert`, `FailedLogin`, `SuspiciousActivity`, `RateLimitHit` are new action types for security event logging.

**Governance check:** ✅ Domain PURE — only adding enum values (additive), no entity modification, no `AccountingEntry`/`BaseEntity` changes. User approved "Add audit enum values" decision.

---

### Step 2: Add `ForSecurityEvent` factory method to `AuditLog` entity (Domain — additive)

#### File 1 (cont.): `1_Shared/Domain/Audit/AuditLog.cs` — UPDATE

**Add new factory method** after `ForReversal` (after line 288, before closing `}`):

```csharp
    /// <summary>
    /// Sprint 3 P3.3: Factory method for security event audit log.
    /// Used for failed login, rate limit hits, suspicious activity, security alerts.
    /// EntityType = SecurityEvent, EntityId = Guid.Empty (synthetic — no specific entity).
    /// Description goes into Reason field (human-readable incident description).
    /// </summary>
    public static AuditLog ForSecurityEvent(
        TenantId tenantId,
        AuditActionType actionType,
        string description,
        string userId,
        string? userName = null,
        string? correlationId = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        return new AuditLog
        {
            TenantId = tenantId,
            Action = actionType,
            EntityType = AuditableEntityType.SecurityEvent,
            EntityId = Guid.Empty,  // Synthetic — security events are not tied to a specific entity
            OldValues = null,
            NewValues = null,
            Reason = description,
            UserId = userId,
            UserName = userName,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow
        };
    }
```

**Notes:**
- Additive — new factory method, existing factory methods unchanged.
- `EntityType = SecurityEvent` (new enum value from Step 1).
- `EntityId = Guid.Empty` — security events are system-level, not tied to a specific entity row.
- `Reason = description` — human-readable incident description (e.g., "Failed login for username 'admin' from IP 1.2.3.4").
- Follows existing factory method pattern (private constructor + public static factory).

**Governance check:** ✅ Domain additive — new factory method only, no existing method modified. `AuditLog` remains immutable (private constructor, factory-only creation, append-only via repository).

---

### Step 1A: `IFeatureFlagService` + `FeatureFlagService` — defaultWhenMissing overload + 4 audit flags (EXPANDED — toggle)

#### File: `3_CoreHub/Services/IFeatureFlagService.cs` — UPDATE

**Thêm optional param `defaultWhenMissing` vào `IsEnabledAsync`** (signature change backward-compatible — callers hiện có không truyền param → default `false`, giữ nguyên behavior VALCN v2):

```csharp
    /// <summary>
    /// Check if a feature is enabled. Default when no SystemSetting row exists:
    /// VALCN v2 flags → false (disabled); Sprint 3 audit flags → gọi với defaultWhenMissing: true (audit ON mặc định).
    /// </summary>
    Task<bool> IsEnabledAsync(string featureName, bool defaultWhenMissing = false, CancellationToken ct = default);
```

#### File: `3_CoreHub/Services/FeatureFlagService.cs` — UPDATE

**1A-a. `KnownFeatures` thêm field `Default` + 4 audit flags:**

```csharp
    // Known features — used by GetAllAsync to return full list even if no SystemSetting row exists
    // Sprint 3 EXPANDED: thêm Default field — audit flags default ON (true), VALCN flags default OFF (false)
    private static readonly (string Name, string Display, string Desc, string Phase, bool Default)[] KnownFeatures =
    [
        ("ValcnV2_PlatformFee", "Platform Fee (Marketplace)", "Tính PlatformFeeAmount trên Marketplace orders (Phase 2)", "Phase 2", false),
        ("ValcnV2_LoyaltyBudget", "Loyalty Budget Cap", "Check budget trước AddPoints + reset jobs (Phase 3)", "Phase 3", false),
        ("ValcnV2_RefundReversal", "Refund Reversal (UC-06)", "4-step reversal on order cancel (Phase 4)", "Phase 4", false),
        // Sprint 3 EXPANDED: audit toggles — default ON
        ("Audit_Enabled", "Audit Logging (Master)", "Bật/tắt TOÀN BỘ audit logging (mặc định: BẬT)", "Sprint 3", true),
        ("Audit_Accounting", "Audit — Kế toán", "Bút toán + đóng/mở kỳ + điều chỉnh/hoàn逆转 (ghi đồng bộ)", "Sprint 3", true),
        ("Audit_Security", "Audit — Bảo mật", "Đăng nhập thất bại + rate limit + đáng ngờ (ghi async)", "Sprint 3", true),
        ("Audit_KhachLink", "Audit — KhachLink", "Thay đổi profile KhachLink instance (ghi async)", "Sprint 3", true),
    ];
```

**1A-b. `IsEnabledAsync` honor `defaultWhenMissing`:**

```csharp
    public async Task<bool> IsEnabledAsync(string featureName, bool defaultWhenMissing = false, CancellationToken ct = default)
    {
        string cacheKey = $"feat_flag_{featureName}";
        if (_cache.TryGetValue(cacheKey, out bool cached))
            return cached;

        string settingKey = $"Features:Enable{featureName}";
        string? value = null;
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IVanAnDbContext>();
            var setting = await dbContext.SystemSettings
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == settingKey, ct);
            value = setting?.Value;
        }

        // Sprint 3 EXPANDED: no setting row → dùng defaultWhenMissing (audit ON, VALCN OFF)
        bool enabled = value != null ? value == "true" : defaultWhenMissing;
        _cache.Set(cacheKey, enabled, CacheTtl);
        return enabled;
    }
```

**1A-c. `GetAllAsync` respect per-feature Default** (tránh UI hiển thị "OFF" trong khi runtime default ON):

```csharp
        return KnownFeatures.Select(f =>
        {
            var v = settings.GetValueOrDefault($"Features:Enable{f.Name}");
            return new FeatureFlagDto(f.Name, f.Display, f.Desc, f.Phase, v != null ? v == "true" : f.Default);
        }).ToList();
```

**Notes:**
- Optional param thay vì overload — zero-diff cho callers hiện có (compile không đổi).
- Audit flags default ON: `Features:EnableAudit_*` chưa có row trong DB → runtime trả true. Admin tắt → `SetEnabledAsync` tạo row `"false"`.
- Cache 30s giữ nguyên — toggle có hiệu lực tối đa 30s sau khi đổi.
- **VERIFY at implementation:** `IFeatureFlagService` đã register trong DI Gateway + ShopERP chưa (VALCN v2 đang dùng → có lẽ đã có). Nếu thiếu → add `AddSingleton<IFeatureFlagService, FeatureFlagService>()`.

**Governance check:** ✅ Service layer only — không đụng Domain. `SystemSetting` aggregate dùng nguyên状 (key-value runtime toggle pattern sẵn có).

---

### Step 3A: Create `AuditLogQueue` (EXPANDED — async queue)

#### File: `3_CoreHub/Services/AuditLogQueue.cs` — NEW

```csharp
using System.Threading.Channels;
using VanAn.Shared.Domain.Audit;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Sprint 3 EXPANDED: Bounded in-memory queue cho async audit logging.
/// Security + KhachLink audit logs enqueue ở đây (fire-and-forget) — không chặn luồng chính.
/// Accounting audit KHÔNG qua queue — AuditTrailService ghi sync trực tiếp (bảo toàn kế toán).
/// FullMode = DropOldest: khi đầy (capacity 1000) log cũ nhất bị drop — app không bao giờ block.
/// </summary>
public class AuditLogQueue
{
    private readonly Channel<AuditLog> _channel = Channel.CreateBounded<AuditLog>(
        new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true  // chỉ AuditLogBackgroundWriter đọc
        });

    /// <summary>Non-blocking enqueue. Trả false nếu channel đầy và log bị drop (best-effort).</summary>
    public bool TryEnqueue(AuditLog log) => _channel.Writer.TryWrite(log);

    /// <summary>Dequeue 1 log nếu có (không block). Trả false nếu queue rỗng.</summary>
    public bool TryDequeue(out AuditLog log) => _channel.Reader.TryRead(out log!);

    /// <summary>Số log đang chờ trong queue (monitoring/diagnostics).</summary>
    public int Count => _channel.Reader.CanCount ? _channel.Reader.Count : 0;
}
```

**Notes:**
- `Channel<T>` thread-safe by design — nhiều request enqueue song song, 1 background writer đọc.
- `DropOldest` — quyết định có chủ đích: audit security best-effort, thà drop log cũ nhất còn hơn block request login/429.
- Registered as **Singleton** (Step 3B).
- Không cần interface — class cụ thể inject trực tiếp (như `IMemoryCache` pattern của FeatureFlagService dùng scope factory).

**Governance check:** ✅ Service layer only. `AuditLog` immutable — queue chỉ defer thời điểm ghi, không modify entity.

---

### Step 3: Add `LogSecurityEventAsync` + toggle gating + hybrid persist to `IAuditTrailService` + `AuditTrailService` (MODIFIED by expansion)

#### File 2: `3_CoreHub/Services/IAuditTrailService.cs` — UPDATE

**3-a. Return type đổi `Task<AuditLog>` → `Task<AuditLog?>` cho TẤT CẢ Log* methods** (trả `null` khi toggle OFF — callers hiện có discard return value, source compatible):

```csharp
    Task<AuditLog?> LogCreateAsync(...);        // signature params giữ nguyên
    Task<AuditLog?> LogUpdateAsync(...);
    Task<AuditLog?> LogDeleteAsync(...);
    Task<AuditLog?> LogPeriodCloseAsync(...);
    Task<AuditLog?> LogPeriodReopenAsync(...);
    Task<AuditLog?> LogCorrectionAsync(...);
    Task<AuditLog?> LogReversalAsync(...);
```

**3-b. Add new method** after `GetByCorrelationIdAsync` (after line 111, before closing `}`) — có `ipAddress` + `userAgent` structured fields (fix review issue #4):

```csharp
        /// <summary>
        /// Sprint 3 P3.3: Log a security event (failed login, rate limit hit, suspicious activity).
        /// Uses AuditActionType.SecurityAlert / FailedLogin / SuspiciousActivity / RateLimitHit.
        /// EntityType = SecurityEvent, EntityId = Guid.Empty.
        /// EXPANDED: persisted ASYNC via AuditLogQueue (fire-and-forget) — không chặn login/429.
        /// </summary>
        Task<AuditLog?> LogSecurityEventAsync(
            AuditActionType actionType,
            string description,
            string? correlationId = null,
            string? ipAddress = null,
            string? userAgent = null,
            CancellationToken cancellationToken = default);
```

#### File 3: `3_CoreHub/Services/AuditTrailService.cs` — UPDATE

**3-c. Constructor inject thêm `IFeatureFlagService` + `AuditLogQueue`:**

```csharp
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly ITenantProvider _tenantProvider;
        private readonly ILogger<AuditTrailService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IFeatureFlagService _featureFlag;   // NEW — Sprint 3 toggle
        private readonly AuditLogQueue _auditQueue;          // NEW — Sprint 3 async queue

        public AuditTrailService(
            IAuditLogRepository auditLogRepository,
            ITenantProvider tenantProvider,
            ILogger<AuditTrailService> logger,
            IHttpContextAccessor httpContextAccessor,
            IFeatureFlagService featureFlag,
            AuditLogQueue auditQueue)
        {
            _auditLogRepository = auditLogRepository;
            _tenantProvider = tenantProvider;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _featureFlag = featureFlag;
            _auditQueue = auditQueue;
        }
```

**3-d. Add toggle gate + hybrid persist helpers** (trước `#region Helper Methods - User Context`):

```csharp
        #region Sprint 3 EXPANDED — Toggle Gating + Hybrid Persist

        /// <summary>
        /// Sprint 3 EXPANDED: Audit toggle gate — master switch + group switch theo EntityType.
        /// Audit_Enabled (master, default ON) → Audit_Accounting / Audit_Security / Audit_KhachLink (default ON).
        /// EntityType ngoài 3 nhóm → chỉ phụ thuộc master.
        /// </summary>
        private async Task<bool> IsAuditEnabledAsync(AuditableEntityType entityType, CancellationToken ct)
        {
            if (!await _featureFlag.IsEnabledAsync("Audit_Enabled", defaultWhenMissing: true, ct))
                return false;

            var groupFlag = entityType switch
            {
                AuditableEntityType.AccountingEntry or AuditableEntityType.PeriodClosing => "Audit_Accounting",
                AuditableEntityType.KhachLinkInstance => "Audit_KhachLink",
                AuditableEntityType.SecurityEvent => "Audit_Security",
                _ => null
            };
            if (groupFlag == null)
                return true;
            return await _featureFlag.IsEnabledAsync(groupFlag, defaultWhenMissing: true, ct);
        }

        /// <summary>
        /// Sprint 3 EXPANDED: Hybrid persist — accounting = SYNC (await DB, toàn vẹn kế toán);
        /// security/khachlink/khác = ASYNC (enqueue, không chặn luồng chính).
        /// </summary>
        private async Task<AuditLog?> PersistAsync(AuditLog auditLog, AuditableEntityType entityType, CancellationToken ct)
        {
            var isAccounting = entityType is AuditableEntityType.AccountingEntry
                or AuditableEntityType.PeriodClosing;
            if (isAccounting)
            {
                return await _auditLogRepository.AddAsync(auditLog);  // SYNC — accounting integrity
            }
            _auditQueue.TryEnqueue(auditLog);  // ASYNC fire-and-forget
            return auditLog;
        }

        #endregion
```

**3-e. Mỗi Log* method thêm gate + đổi persist** (pattern chung — ví dụ `LogCreateAsync`):

```csharp
        public async Task<AuditLog?> LogCreateAsync(
            AuditableEntityType entityType,
            Guid entityId,
            string newValues,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            // Sprint 3 EXPANDED: toggle gate — trả null khi OFF (không build, không ghi)
            if (!await IsAuditEnabledAsync(entityType, cancellationToken))
                return null;

            var tenantId = new TenantId(_tenantProvider.TenantId);
            var userId = GetCurrentUserId();
            var userName = GetCurrentUserName();

            _logger.LogDebug(
                "Logging CREATE for {EntityType} {EntityId} by user {UserId}",
                entityType, entityId, userId);

            var auditLog = AuditLog.ForCreate(
                tenantId, entityType, entityId, newValues, userId, userName, correlationId);

            // Sprint 3 EXPANDED: hybrid persist (AccountingEntry → sync; khác → async queue)
            return await PersistAsync(auditLog, entityType, cancellationToken);
        }
```

Apply cùng pattern cho `LogUpdateAsync`/`LogDeleteAsync` (generic theo `entityType`). Riêng `LogPeriodCloseAsync`/`LogPeriodReopenAsync` (EntityType=PeriodClosing) + `LogCorrectionAsync`/`LogReversalAsync` (EntityType=AccountingEntry) — gate check theo EntityType tương ứng, persist qua `PersistAsync` (tự rơi vào sync path).

**3-f. Add `LogSecurityEventAsync` implementation** (after `GetByCorrelationIdAsync`, trước `#region Sprint 3 EXPANDED`):

```csharp
        public async Task<AuditLog?> LogSecurityEventAsync(
            AuditActionType actionType,
            string description,
            string? correlationId = null,
            string? ipAddress = null,
            string? userAgent = null,
            CancellationToken cancellationToken = default)
        {
            // Sprint 3 EXPANDED: toggle gate — SecurityEvent → master + Audit_Security
            if (!await IsAuditEnabledAsync(AuditableEntityType.SecurityEvent, cancellationToken))
                return null;

            var tenantId = new TenantId(_tenantProvider.TenantId);
            var userId = GetCurrentUserId();
            var userName = GetCurrentUserName();

            _logger.LogWarning(
                "SECURITY EVENT: {ActionType} — {Description} by user {UserId}",
                actionType, description, userId);

            var auditLog = AuditLog.ForSecurityEvent(
                tenantId,
                actionType,
                description,
                userId,
                userName,
                correlationId,
                ipAddress,
                userAgent);

            // Security → ASYNC fire-and-forget (PersistAsync: SecurityEvent không phải accounting → enqueue)
            return await PersistAsync(auditLog, AuditableEntityType.SecurityEvent, cancellationToken);
        }
```

**Notes:**
- Uses `AuditLog.ForSecurityEvent` factory (Step 2) — giờ truyền đủ `ipAddress` + `userAgent` (structured fields, fix review issue #4).
- `_tenantProvider.TenantId` — for anonymous requests (failed login, rate limit), this is `Guid.Empty` (no tenant context). For authenticated security events, it's the user's tenant.
- `GetCurrentUserId()` returns `"system"` when no HTTP context user (anonymous request). For failed login, the user is not authenticated → userId = "system". To capture the attempted username, include it in `description`.
- `LogWarning` level — security events are always logged at Warning (not Debug like normal audit).
- **Persist ASYNC** — `LogSecurityEventAsync` return ngay sau khi enqueue (không await DB). Audit row xuất hiện trong DB trễ tối đa ~5s (flush interval của `AuditLogBackgroundWriter`).
- Query methods KHÔNG đổi — `QueryAsync`/`GetEntityHistoryAsync`/`GetRecentAsync`/`GetByCorrelationIdAsync` đọc DB trực tiếp, không bị toggle.
- **VERIFY at implementation:** grep mọi caller của `Log*Async` — nếu caller nào DÙNG return value (không discard) → thêm null-check. Hiện verified: `AccountingEntryService.cs:93` discard.

---

### Step 3B: Create `AuditLogBackgroundWriter` + DI registration (EXPANDED — async writer)

#### File: `3_CoreHub/Services/AuditLogBackgroundWriter.cs` — NEW

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.Shared.Domain.Audit;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Sprint 3 EXPANDED: Background writer — drain AuditLogQueue mỗi 5s (hoặc khi đủ 100/batch),
/// batch INSERT qua IAuditLogRepository (scoped, resolve qua IServiceScopeFactory).
/// Graceful shutdown: flush residual batch sau khi stoppingToken cancel.
/// Best-effort: batch fail (DB lỗi) → log error + drop (không re-enqueue — tránh vòng lặp khi DB down).
/// </summary>
public class AuditLogBackgroundWriter : BackgroundService
{
    private readonly AuditLogQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditLogBackgroundWriter> _logger;
    private const int BatchSize = 100;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);

    public AuditLogBackgroundWriter(
        AuditLogQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<AuditLogBackgroundWriter> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(FlushInterval);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await FlushAsync();
                await timer.WaitForNextTickAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown — flush residual bên dưới
        }

        // Residual flush — không truyền stoppingToken (đã cancel)
        await FlushAsync();
    }

    private async Task FlushAsync()
    {
        while (true)
        {
            var batch = new List<AuditLog>();
            while (_queue.TryDequeue(out var log))
            {
                batch.Add(log);
                if (batch.Count >= BatchSize) break;
            }
            if (batch.Count == 0) return;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
                foreach (var log in batch)
                {
                    await repository.AddAsync(log);
                }
                _logger.LogDebug("AuditLogBackgroundWriter: flushed {Count} audit logs", batch.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "AuditLogBackgroundWriter: flush failed — {Count} audit logs dropped (best-effort)",
                    batch.Count);
            }

            if (batch.Count < BatchSize) return;  // queue đã rỗng
        }
    }
}
```

#### DI registration — `2_Gateway/Program.cs` + `5_WebApps/ShopERP/Program.cs` — UPDATE

Thêm vào cả 2 hosts (sau chỗ `AddScoped<IAuditTrailService, AuditTrailService>()` — Gateway line ~514, ShopERP line ~256):

```csharp
// Sprint 3 EXPANDED: async audit queue + background writer
builder.Services.AddSingleton<AuditLogQueue>();
builder.Services.AddHostedService<AuditLogBackgroundWriter>();
```

**Notes:**
- `BackgroundService` = singleton → KHÔNG inject scoped `IAuditLogRepository` trực tiếp. Dùng `IServiceScopeFactory` tạo scope mỗi flush (pattern sẵn có trong `FeatureFlagService.cs:42-50`).
- Flush: mỗi 5s HOẶC khi batch đủ 100 — volume thấp (security logs rate-limited) → 5s đủ.
- Batch fail → drop + LogError. KHÔNG re-enqueue — nếu DB down, re-enqueue tạo vòng lặp vô hạn + queue đầy. Accounting audit không ảnh hưởng (sync path riêng).
- Graceful shutdown: `ExecuteAsync` catch `OperationCanceledException` → residual flush với `CancellationToken.None`.
- **CRITICAL:** Gateway `OnRejected` resolve `IAuditTrailService` qua `RequestServices` — nếu `AuditLogQueue`/`IFeatureFlagService` chưa register trong Gateway → `GetService` trả null → audit silently skipped. Registration ở Gateway là BẮT BUỘC.
- **VERIFY at implementation:** (a) `IFeatureFlagService` đã register ở cả 2 hosts chưa (VALCN đang dùng → có lẽ có). (b) OQ1 nhắc "CoreHub Program.cs line 109" — governance nói 3_CoreHub là pure Class Library. Verify file này là gì (test host? legacy?). Nếu là host thật → register queue ở đó luôn.

**Governance check:** ✅ Service/Infrastructure layer — không đụng Domain. `AuditLog` immutable — writer chỉ INSERT, không update/delete.

---

### Step 4: Inject `IAuditTrailService` into `KhachLinkInstanceService`, log `UpdateAsync` (Task 3.1)

#### File 4: `3_CoreHub/Services/KhachLinkInstanceService.cs` — UPDATE

**4a. Add `using` + inject `IAuditTrailService`** (top of file + constructor):

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;
using VanAn.Shared.Domain.Audit;  // NEW — AuditableEntityType, AuditActionType

namespace VanAn.CoreHub.Services
{
    public class KhachLinkInstanceService : IKhachLinkInstanceService
    {
        private readonly IVanAnDbContext _dbContext;
        private readonly IAuditTrailService _auditTrailService;  // NEW
        private readonly ILogger<KhachLinkInstanceService>? _logger;

        public KhachLinkInstanceService(
            IVanAnDbContext dbContext,
            IAuditTrailService auditTrailService,  // NEW
            ILogger<KhachLinkInstanceService>? logger = null)
        {
            _dbContext = dbContext;
            _auditTrailService = auditTrailService;
            _logger = logger;
        }
```

**4b. Capture old values + log audit in `UpdateAsync`** (replace existing `UpdateAsync` body, lines 97-141):

```csharp
        public async Task<bool> UpdateAsync(
            Guid id,
            KhachLinkProfile profile,
            KhachLinkNavFlags navFlags,
            string? theme = null,
            string? logoUrl = null,
            string? navColor = null,
            string? headerColor = null,
            string? footerColor = null,
            CancellationToken ct = default)
        {
            if (navFlags is null)
                throw new ArgumentNullException(nameof(navFlags));

            var instance = await _dbContext.KhachLinkInstances
                .FirstOrDefaultAsync(i => i.Id == id, ct);
            if (instance is null)
                return false;

            // Sprint 3 P1.2: Capture old values for audit log BEFORE update.
            var oldProfile = instance.Profile;
            var oldNavFlagsJson = JsonSerializer.Serialize(new
            {
                instance.NavFlags.ShowHome, instance.NavFlags.ShowCart, instance.NavFlags.ShowOrders,
                instance.NavFlags.ShowLoyaltyHistory, instance.NavFlags.ShowMissions, instance.NavFlags.ShowRewards,
                instance.NavFlags.ShowAllianceWallet, instance.NavFlags.ShowStores, instance.NavFlags.ShowCampaigns,
                instance.NavFlags.ShowScan, instance.NavFlags.ShowQrClaim, instance.NavFlags.ShowCommunity,
                instance.NavFlags.ShowJobs, instance.NavFlags.ShowProfile, instance.NavFlags.ShowStaffDashboard
            });
            var oldStyleJson = JsonSerializer.Serialize(new
            {
                instance.Theme, instance.LogoUrl, instance.NavColor, instance.HeaderColor, instance.FooterColor
            });

            instance.UpdateProfile(profile, navFlags);
            instance.UpdateStyle(theme, logoUrl, navColor, headerColor, footerColor);

            // Sprint 3 P1.2: Capture new values for audit log AFTER update.
            var newNavFlagsJson = JsonSerializer.Serialize(new
            {
                instance.NavFlags.ShowHome, instance.NavFlags.ShowCart, instance.NavFlags.ShowOrders,
                instance.NavFlags.ShowLoyaltyHistory, instance.NavFlags.ShowMissions, instance.NavFlags.ShowRewards,
                instance.NavFlags.ShowAllianceWallet, instance.NavFlags.ShowStores, instance.NavFlags.ShowCampaigns,
                instance.NavFlags.ShowScan, instance.NavFlags.ShowQrClaim, instance.NavFlags.ShowCommunity,
                instance.NavFlags.ShowJobs, instance.NavFlags.ShowProfile, instance.NavFlags.ShowStaffDashboard
            });
            var newStyleJson = JsonSerializer.Serialize(new
            {
                instance.Theme, instance.LogoUrl, instance.NavColor, instance.HeaderColor, instance.FooterColor
            });

            // #136: Force EF Core to persist owned type changes on PostgreSQL.
            if (_dbContext is DbContext dbContext
                && dbContext.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
            {
                var rootEntry = dbContext.Entry(instance);
                rootEntry.State = EntityState.Modified;
                var navFlagsEntry = rootEntry.Reference(nameof(KhachLinkInstance.NavFlags)).TargetEntry;
                if (navFlagsEntry != null)
                {
                    navFlagsEntry.State = EntityState.Modified;
                }
            }
            await _dbContext.SaveChangesAsync(ct);

            // Sprint 3 P1.2: Log audit trail (after successful save — only log if DB write succeeded).
            try
            {
                var oldValues = JsonSerializer.Serialize(new
                {
                    Profile = oldProfile.ToString(),
                    NavFlags = oldNavFlagsJson,
                    Style = oldStyleJson
                });
                var newValues = JsonSerializer.Serialize(new
                {
                    Profile = profile.ToString(),
                    NavFlags = newNavFlagsJson,
                    Style = newStyleJson
                });

                await _auditTrailService.LogUpdateAsync(
                    AuditableEntityType.KhachLinkInstance,
                    id,
                    oldValues,
                    newValues,
                    correlationId: id.ToString(),
                    ct);
            }
            catch (Exception ex)
            {
                // Audit logging is best-effort — don't fail the update if audit fails.
                _logger?.LogWarning(ex, "Failed to log audit trail for KhachLinkInstance {Id} update", id);
            }

            _logger?.LogInformation("Updated KhachLinkInstance {Id} profile={Profile}", instance.Id, profile);
            return true;
        }
```

**Notes:**
- Old values captured BEFORE `UpdateProfile` (profile + navFlags + style snapshot).
- New values captured AFTER `UpdateProfile` (reflects the new state).
- Audit log AFTER `SaveChangesAsync` — only log if DB write succeeded (don't log failed updates).
- `correlationId: id.ToString()` — instance ID as correlation (trace all audit events for this instance).
- `AuditableEntityType.KhachLinkInstance` (new enum value from Step 1).
- Audit logging wrapped in try/catch — best-effort, don't fail the update if audit fails.
- `KhachLinkInstance.TenantId = Guid.Empty` → `AuditTrailService.LogUpdateAsync` uses `_tenantProvider.TenantId` which is `Guid.Empty` for SystemAdmin (OQ2 verified). ✅
- Controller (`KhachLinkInstanceController.Update`) — **NO CHANGE needed**. Service self-logs. Controller just calls `_instanceService.UpdateAsync`.

**Governance check:** ✅ Service layer change only — no Domain modification (enum already added in Step 1). `AuditLog` immutable (append-only via `LogUpdateAsync`). Multi-tenancy: `Guid.Empty` platform sentinel.

---

### Step 5: Log failed login in `PlatformUserLoginService` (Task 3.3 — security event at source)

#### File 5: `3_CoreHub/Services/PlatformUserLoginService.cs` — UPDATE

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.Shared.Domain.Audit;  // NEW — AuditActionType
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Services;

public class PlatformUserLoginService : IPlatformUserLoginService
{
    private readonly IVanAnDbContext _db;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAuditTrailService _auditTrailService;  // NEW
    private readonly IHttpContextAccessor _httpContextAccessor;  // NEW — for IP capture

    public PlatformUserLoginService(
        IVanAnDbContext db,
        IJwtTokenService jwtTokenService,
        IAuditTrailService auditTrailService,  // NEW
        IHttpContextAccessor httpContextAccessor)  // NEW
    {
        _db = db;
        _jwtTokenService = jwtTokenService;
        _auditTrailService = auditTrailService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<PlatformLoginResult?> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var user = await _db.PlatformUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Username == username, ct);

        var clientIp = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = _httpContextAccessor.HttpContext?.Request?.Headers?.UserAgent.ToString() ?? "unknown";

        if (user == null)
        {
            // Sprint 3 P3.3: Log failed login — user not found
            await LogSecurityEventAsync(
                AuditActionType.FailedLogin,
                $"Failed login: username '{username}' not found from IP {clientIp}",
                clientIp, userAgent, ct);
            return null;
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            // Sprint 3 P3.3: Log failed login — wrong password
            await LogSecurityEventAsync(
                AuditActionType.FailedLogin,
                $"Failed login: wrong password for '{username}' from IP {clientIp}",
                clientIp, userAgent, ct);
            return null;
        }

        if (!user.IsActive)
        {
            // Sprint 3 P3.3: Log failed login — inactive user
            await LogSecurityEventAsync(
                AuditActionType.FailedLogin,
                $"Failed login: inactive user '{username}' from IP {clientIp}",
                clientIp, userAgent, ct);
            return null;
        }

        var token = _jwtTokenService.GenerateToken(
            userId: user.Id,
            email: user.Email ?? user.Username,
            role: PlatformRole.SystemAdmin.ToString(),
            tenantId: Guid.Empty);

        return new PlatformLoginResult(user.Id, user.Email ?? user.Username, PlatformRole.SystemAdmin.ToString(), token);
    }

    /// <summary>Sprint 3 P3.3: Best-effort security event logging for failed logins.</summary>
    private async Task LogSecurityEventAsync(
        AuditActionType actionType,
        string description,
        string clientIp,
        string userAgent,
        CancellationToken ct)
    {
        try
        {
            await _auditTrailService.LogSecurityEventAsync(
                actionType,
                description,
                correlationId: clientIp,  // IP as correlation — track repeated attempts from same IP
                ipAddress: clientIp,      // Sprint 3 EXPANDED — structured IP field
                userAgent: userAgent,     // Sprint 3 EXPANDED — structured UserAgent field
                ct);
        }
        catch
        {
            // Best-effort — don't fail login flow if audit fails
        }
    }
}
```

**Notes:**
- Inject `IAuditTrailService` + `IHttpContextAccessor` (both already registered in DI).
- `IHttpContextAccessor` — capture client IP + User-Agent for security context. Already registered in Gateway/ShopERP DI (used by `AuditTrailService`).
- 3 failure paths logged: user not found, wrong password, inactive user. Each with distinct description.
- `correlationId: clientIp` — IP as correlation ID lets dashboard group repeated failures from same IP (brute-force detection).
- Best-effort logging — try/catch wraps audit call, login flow never fails due to audit error.
- `LogSecurityEventAsync` (Step 3) uses `GetCurrentUserId()` which returns `"system"` for anonymous requests (failed login = not authenticated). The attempted username is in the description.

**Verify DI:** `IHttpContextAccessor` registered? — Check Gateway/ShopERP `Program.cs`. `AuditTrailService` already injects it (line 24, 30-35), so it's registered. ✅

---

### Step 6: Log rate limit hits in Gateway `Program.cs` `OnRejected` (Task 3.3 — security event at source)

#### File 6: `2_Gateway/Program.cs` — UPDATE

**Modify `OnRejected` callback** (line 209-215). The rate limiter has 3 `AddRateLimiter` calls — the last one (line 181-216) has the `OnRejected`. Update it:

```csharp
                // Return 429 (Too Many Requests) instead of default 503 for rate-limited audit requests.
                options.OnRejected = async (context, cancellationToken) =>
                {
                    // Sprint 3 P3.3: Log rate limit hit as security event (potential brute-force / abuse)
                    try
                    {
                        var auditService = context.HttpContext.RequestServices
                            .GetService<VanAn.CoreHub.Services.IAuditTrailService>();
                        if (auditService != null)
                        {
                            var clientIp = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                            var userAgent = context.HttpContext.Request.Headers.UserAgent.ToString();
                            var endpoint = context.HttpContext.Request.Path.Value ?? "unknown";
                            var policy = context.Lease.GetAllTags().FirstOrDefault() ?? "unknown";

                            await auditService.LogSecurityEventAsync(
                                VanAn.Shared.Domain.Audit.AuditActionType.RateLimitHit,
                                $"Rate limit hit: policy '{policy}' on '{endpoint}' from IP {clientIp}",
                                correlationId: clientIp,
                                ipAddress: clientIp,    // Sprint 3 EXPANDED — structured IP field
                                userAgent: userAgent,   // Sprint 3 EXPANDED — structured UserAgent field
                                cancellationToken);
                        }
                    }
                    catch
                    {
                        // Best-effort — don't block 429 response if audit fails
                    }

                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.HttpContext.Response.ContentType = "application/json";
                    await context.HttpContext.Response.WriteAsync(
                        """{"message":"Quá giới hạn yêu cầu. Vui lòng thử lại sau 1 giờ."}""", cancellationToken);
                };
```

**Notes:**
- `context.HttpContext.RequestServices.GetService<IAuditTrailService>()` — resolve scoped service from HttpContext (rate limiter runs in request pipeline, scoped services available).
- `context.Lease.GetAllTags()` — rate limiter metadata (policy name). Verify API — `RateLimitLease.GetAllTags()` returns metadata tags. If API differs, use `context.Lease.Metadata` or hardcode "rate-limited".
- `correlationId: clientIp` — same IP correlation as failed login (dashboard can group failed login + rate limit from same IP = brute-force pattern).
- Best-effort — try/catch wraps audit call, 429 response never blocked by audit failure.
- **VERIFY:** `IAuditTrailService` resolvable from `HttpContext.RequestServices` in rate limiter context. Gateway registers it as Scoped (line 514). Rate limiter runs after DI scope creation → scoped services available. ✅

**Alternative if `GetAllTags()` API not available:**
```csharp
var policy = "rate-limited";  // Fallback — all rate limit hits use same label
```

---

### Step 7: Enhance `AuditTrail.razor` — summary cards + severity classification (Task 3.4 — monitoring dashboard)

#### File 7: `5_WebApps/ShopERP/Components/Pages/Admin/AuditTrail.razor` — UPDATE

**7a. Add severity model + classification logic** (in `@code` block, after `FilterModel` class):

```csharp
    // Sprint 3 P3.4: Incident severity classification — presentation-only, computed from Action + EntityType.
    // No Domain change — classification lives in UI layer (dashboard presentation logic).
    public enum IncidentSeverity
    {
        Critical,  // Nghiêm trọng — hack, tấn công mạng, gian lận
        High,      // Cao — potential security issue
        Medium,    // Trung — significant business change
        Low,       // Thấp — routine operation
        Info       // Thông tin — login/logout, export
    }

    private static IncidentSeverity GetSeverity(AuditLog log) => (log.Action, log.EntityType) switch
    {
        // Critical: security incidents + financial fraud
        (AuditActionType.SuspiciousActivity, _) => IncidentSeverity.Critical,
        (AuditActionType.Delete, AuditableEntityType.AccountingEntry) => IncidentSeverity.Critical,
        (AuditActionType.Delete, AuditableEntityType.Tenant) => IncidentSeverity.Critical,
        (AuditActionType.RateLimitHit, _) => IncidentSeverity.Critical,
        // High: potential security issues
        (AuditActionType.FailedLogin, _) => IncidentSeverity.High,
        (AuditActionType.SecurityAlert, _) => IncidentSeverity.High,
        (AuditActionType.PermissionChange, _) => IncidentSeverity.High,
        (AuditActionType.Delete, AuditableEntityType.User) => IncidentSeverity.High,
        (AuditActionType.Delete, AuditableEntityType.KhachLinkInstance) => IncidentSeverity.High,
        // Medium: significant business changes
        (AuditActionType.Correction, _) => IncidentSeverity.Medium,
        (AuditActionType.Reversal, _) => IncidentSeverity.Medium,
        (AuditActionType.PeriodClose, _) => IncidentSeverity.Medium,
        (AuditActionType.PeriodReopen, _) => IncidentSeverity.Medium,
        (AuditActionType.Update, AuditableEntityType.AccountingEntry) => IncidentSeverity.Medium,
        (AuditActionType.Update, AuditableEntityType.KhachLinkInstance) => IncidentSeverity.Medium,
        (AuditActionType.Update, AuditableEntityType.Tenant) => IncidentSeverity.Medium,
        (AuditActionType.Create, AuditableEntityType.Tenant) => IncidentSeverity.Medium,
        // Info: routine
        (AuditActionType.Login, _) => IncidentSeverity.Info,
        (AuditActionType.Logout, _) => IncidentSeverity.Info,
        (AuditActionType.Export, _) => IncidentSeverity.Info,
        // Low: default
        _ => IncidentSeverity.Low
    };

    private static string GetSeverityDisplay(IncidentSeverity s) => s switch
    {
        IncidentSeverity.Critical => "🔴 Nghiêm trọng",
        IncidentSeverity.High => "🟠 Cao",
        IncidentSeverity.Medium => "🟡 Trung bình",
        IncidentSeverity.Low => "🔵 Thấp",
        IncidentSeverity.Info => "ℹ️ Thông tin",
        _ => s.ToString()
    };

    private static string GetSeverityClass(IncidentSeverity s) => s switch
    {
        IncidentSeverity.Critical => "severity-critical",
        IncidentSeverity.High => "severity-high",
        IncidentSeverity.Medium => "severity-medium",
        IncidentSeverity.Low => "severity-low",
        IncidentSeverity.Info => "severity-info",
        _ => "severity-low"
    };
```

**7b. Add summary cards state + load method** (in `@code` block, after `LoadData` method):

```csharp
    // Sprint 3 P3.4: Summary cards data
    private List<AuditLog> _recentLogs = new();
    private int _criticalCount;
    private int _highCount;
    private int _failedLoginCount;
    private int _rateLimitCount;
    private int _totalLast24h;

    private async Task LoadSummaryCards()
    {
        try
        {
            // Get recent 200 logs for summary computation (covers ~24h depending on volume)
            _recentLogs = (await AuditTrailService.GetRecentAsync(200)).ToList();

            _criticalCount = _recentLogs.Count(l => GetSeverity(l) == IncidentSeverity.Critical);
            _highCount = _recentLogs.Count(l => GetSeverity(l) == IncidentSeverity.High);
            _failedLoginCount = _recentLogs.Count(l => l.Action == AuditActionType.FailedLogin);
            _rateLimitCount = _recentLogs.Count(l => l.Action == AuditActionType.RateLimitHit);

            var cutoff = DateTime.UtcNow.AddHours(-24);
            _totalLast24h = _recentLogs.Count(l => l.Timestamp >= cutoff);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading audit summary cards");
        }
    }
```

**7c. Update `OnInitializedAsync` + `RefreshData`** to load summary cards:

```csharp
    protected override async Task OnInitializedAsync()
    {
        await LoadData();
        await LoadSummaryCards();
    }

    private async Task RefreshData()
    {
        await LoadData();
        await LoadSummaryCards();
    }
```

**7d. Add summary cards section** in markup (after `<header>` block, before `<section class="filter-section">`):

```razor
    <!-- Sprint 3 P3.4: Summary cards — incident monitoring dashboard -->
    <section class="summary-cards-section">
        <div class="summary-grid">
            <VanACard>
                <div class="summary-card summary-total">
                    <div class="summary-icon">📊</div>
                    <div class="summary-value">@_totalLast24h</div>
                    <div class="summary-label">Sự kiện (24h)</div>
                </div>
            </VanACard>
            <VanACard>
                <div class="summary-card summary-critical">
                    <div class="summary-icon">🔴</div>
                    <div class="summary-value">@_criticalCount</div>
                    <div class="summary-label">Nghiêm trọng</div>
                </div>
            </VanACard>
            <VanACard>
                <div class="summary-card summary-high">
                    <div class="summary-icon">🟠</div>
                    <div class="summary-value">@_highCount</div>
                    <div class="summary-label">Cao</div>
                </div>
            </VanACard>
            <VanACard>
                <div class="summary-card summary-failed-login">
                    <div class="summary-icon">🚫</div>
                    <div class="summary-value">@_failedLoginCount</div>
                    <div class="summary-label">Đăng nhập thất bại</div>
                </div>
            </VanACard>
            <VanACard>
                <div class="summary-card summary-rate-limit">
                    <div class="summary-icon">⚡</div>
                    <div class="summary-value">@_rateLimitCount</div>
                    <div class="summary-label">Rate limit</div>
                </div>
            </VanACard>
        </div>
    </section>
```

**7e. Add severity column to results table** (in `<thead>`, after `<th>Hành động</th>`):

```razor
                            <tr>
                                <th>Thời gian</th>
                                <th>Hành động</th>
                                <th>Mức độ</th>  <!-- NEW -->
                                <th>Entity</th>
                                <th>User</th>
                                <th>Lý do</th>
                                <th>Chi tiết</th>
                            </tr>
```

**7f. Add severity cell to table rows** (in `<tbody>`, after action cell):

```razor
                                    <td class="severity">
                                        <span class="severity-badge @GetSeverityClass(GetSeverity(log))">
                                            @GetSeverityDisplay(GetSeverity(log))
                                        </span>
                                    </td>
```

**7g. Add severity filter to filter panel** (in `.filter-grid`, after EntityType filter):

```razor
                <div class="filter-group">
                    <label>Mức độ</label>
                    <select class="vanan-select" @bind="Filter.Severity">
                        <option value="">Tất cả</option>
                        <option value="Critical">🔴 Nghiêm trọng</option>
                        <option value="High">🟠 Cao</option>
                        <option value="Medium">🟡 Trung bình</option>
                        <option value="Low">🔵 Thấp</option>
                        <option value="Info">ℹ️ Thông tin</option>
                    </select>
                </div>
```

**7h. Apply severity filter in `LoadData`** (after `AuditLogs = result.Items.ToList()`):

```csharp
            // Sprint 3 P3.4: Client-side severity filter (computed from Action+EntityType)
            if (Filter.Severity != null && !string.IsNullOrEmpty(Filter.Severity))
            {
                var severityFilter = Enum.Parse<IncidentSeverity>(Filter.Severity);
                AuditLogs = AuditLogs.Where(l => GetSeverity(l) == severityFilter).ToList();
            }
```

**7i. Update EntityType filter dropdown** to include new types (add after `LoyaltyRewards`):

```razor
                        <option value="@AuditableEntityType.KhachLinkInstance">KhachLink Instance</option>
                        <option value="@AuditableEntityType.SecurityEvent">Sự cố bảo mật</option>
```

**7j. Update Action filter dropdown** to include new action types (add after `PermissionChange`):

```razor
                        <option value="@AuditActionType.SecurityAlert">Cảnh báo bảo mật</option>
                        <option value="@AuditActionType.FailedLogin">Đăng nhập thất bại</option>
                        <option value="@AuditActionType.SuspiciousActivity">Hoạt động đáng ngờ</option>
                        <option value="@AuditActionType.RateLimitHit">Rate limit</option>
```

**7k. Update `FilterModel`** to include Severity:

```csharp
    public class FilterModel
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public AuditActionType? Action { get; set; }
        public AuditableEntityType? EntityType { get; set; }
        public string? Severity { get; set; }  // NEW — client-side filter
        public string? UserId { get; set; }
        public string? SearchTerm { get; set; }
    }
```

**7l. Add CSS for summary cards + severity badges** (in existing `<style>` block or add new):

```css
    /* Sprint 3 P3.4: Summary cards */
    .audit-trail-page .summary-cards-section { margin-bottom: 1.5rem; }
    .audit-trail-page .summary-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
        gap: 1rem;
    }
    .audit-trail-page .summary-card {
        text-align: center;
        padding: 0.5rem;
    }
    .audit-trail-page .summary-icon { font-size: 2rem; margin-bottom: 0.5rem; }
    .audit-trail-page .summary-value {
        font-size: 2rem;
        font-weight: 700;
        line-height: 1;
    }
    .audit-trail-page .summary-label {
        font-size: 0.85rem;
        color: var(--color-neutral-600, #666);
        margin-top: 0.25rem;
    }
    .audit-trail-page .summary-critical .summary-value { color: #dc3545; }
    .audit-trail-page .summary-high .summary-value { color: #fd7e14; }
    .audit-trail-page .summary-failed-login .summary-value { color: #6f42c1; }
    .audit-trail-page .summary-rate-limit .summary-value { color: #e83e8c; }

    /* Sprint 3 P3.4: Severity badges */
    .audit-trail-page .severity-badge {
        display: inline-block;
        padding: 2px 8px;
        border-radius: 4px;
        font-size: 0.8rem;
        font-weight: 500;
        white-space: nowrap;
    }
    .audit-trail-page .severity-critical {
        background: #f8d7da;
        color: #721c24;
        border: 1px solid #f5c6cb;
    }
    .audit-trail-page .severity-high {
        background: #fff3cd;
        color: #856404;
        border: 1px solid #ffeaa7;
    }
    .audit-trail-page .severity-medium {
        background: #d1ecf1;
        color: #0c5460;
        border: 1px solid #bee5eb;
    }
    .audit-trail-page .severity-low {
        background: #e2e3e5;
        color: #383d41;
        border: 1px solid #d6d8db;
    }
    .audit-trail-page .severity-info {
        background: #f8f9fa;
        color: #495057;
        border: 1px solid #dee2e6;
    }
```

**Notes:**
- Severity classification is **presentation-only** — computed from `AuditActionType` + `AuditableEntityType` in the UI layer. No Domain change for classification itself (enum values from Step 1 enable the classification).
- Summary cards use `GetRecentAsync(200)` — separate data load from the filtered table. Covers ~24h depending on volume.
- Severity filter is **client-side** — applied after `QueryAsync` returns. For large datasets, this only filters the current page. Acceptable for MVP (admin dashboard, not high-volume).
- `IncidentSeverity` enum + `GetSeverity` switch expression — maps (Action, EntityType) pairs to severity levels.
- Critical = hack/attack/fraud: `SuspiciousActivity`, `Delete on AccountingEntry` (fraud), `Delete on Tenant` (destructive), `RateLimitHit` (brute-force).
- High = potential security: `FailedLogin`, `SecurityAlert`, `PermissionChange`, `Delete on User/KhachLinkInstance`.
- UI Platform compliance: uses `VanACard` for summary cards, existing `vanan-table` + `vanan-select` classes. Scoped CSS for severity badges (app-specific, no UI Platform equivalent).

---

### Step 7A: Audit toggles hiển thị trong admin feature-flag UI (EXPANDED — verify-only)

#### File: `5_WebApps/ShopERP/Components/Pages/Admin/ValcnFeatures.razor` (VERIFY exact path) — LIKELY NO CHANGE

**Logic:** `IFeatureFlagService` comment ghi "Admin UI: /admin/valcn-features (SystemAdmin role)". Nếu page này render danh sách từ `GetAllAsync()` (iterate results) → 4 audit flags tự xuất hiện sau Step 1A thêm vào `KnownFeatures` — **ZERO UI change**.

**Verify:**
1. Grep page render `/admin/valcn-features` — tìm file Razor thực sự (có thể tên khác).
2. Nếu page iterate `GetAllAsync()` → no change. ✅
3. Nếu page HARDCODE list flag names (không iterate) → thêm 4 audit flags vào markup (label tiếng Việt từ `KnownFeatures`).

**Notes:**
- Toggle semantics cho audit: default ON (hiển thị đúng nhờ `GetAllAsync` fix ở Step 1A-c).
- Admin tắt/bật qua `SetEnabledAsync` có sẵn — cache 30s → hiệu lực tối đa 30s sau.
- Nếu cần UX tốt hơn (nhóm "Audit" riêng, badge "default ON") → defer, không cần MVP.

---

### Step 8: Create `KhachLinkInstanceAudit.razor` + link from `KhachLinkInstances.razor` (Task 3.1 — per-instance history)

#### File 8: `5_WebApps/ShopERP/Components/Pages/Admin/KhachLinkInstanceAudit.razor` — NEW

```razor
@page "/admin/khachlink-instances/{InstanceId:guid}/audit"
@rendermode InteractiveServer
@layout AdminLayout
@using VanAn.UI.Platform.Components
@using VanAn.UI.Platform.Components.Composite
@using VanAn.Shared.Domain.Audit
@using VanAn.CoreHub.Services
@using VanAn.ShopERP.Services
@inject IAuditTrailService AuditTrailService
@inject KhachLinkInstanceApiClient InstanceApi
@inject IThemeProvider ThemeProvider
@inject ILogger<KhachLinkInstanceAudit> Logger
@inject NavigationManager NavigationManager

@attribute [Authorize(Policy = "SystemAdmin")]

<div class="khachlink-audit-page @ThemeProvider.CurrentTheme">
    <header class="page-header">
        <h1>📝 Lịch sử thay đổi — @(_instanceLabel ?? "KhachLink Instance")</h1>
        <div class="header-actions">
            <VanAButton Variant="secondary" Size="small" OnClick="RefreshData">🔄 Refresh</VanAButton>
            <VanAButton Variant="outline" Size="small" OnClick="GoBack">← Quay lại</VanAButton>
        </div>
    </header>

    @if (!string.IsNullOrEmpty(_alertMessage))
    {
        <VanAAlert Type="@_alertType" Message="@_alertMessage" OnClose="() => _alertMessage = string.Empty" />
    }

    @if (_isLoading)
    {
        <VanACard>
            <div class="loading-state">
                <VanASpinner Size="large" />
                <p>Đang tải lịch sử...</p>
            </div>
        </VanACard>
    }
    else if (_auditLogs == null || !_auditLogs.Any())
    {
        <VanACard>
            <div class="empty-state">
                <div class="empty-icon">📭</div>
                <p>Chưa có lịch sử thay đổi nào cho instance này.</p>
            </div>
        </VanACard>
    }
    else
    {
        <VanACard Header="@($"Lịch sử thay đổi ({_auditLogs.Count})")">
            <div class="table-responsive">
                <table class="vanan-table audit-history-table">
                    <thead>
                        <tr>
                            <th>Thời gian</th>
                            <th>Người đổi</th>
                            <th>Profile cũ</th>
                            <th>Profile mới</th>
                            <th>Nav Flags thay đổi</th>
                            <th>Chi tiết</th>
                        </tr>
                    </thead>
                    <tbody>
                        @foreach (var log in _auditLogs)
                        {
                            <tr>
                                <td class="timestamp">@log.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")</td>
                                <td class="user">@(log.UserName ?? log.UserId ?? "System")</td>
                                <td class="old-profile">@ExtractProfile(log.OldValues)</td>
                                <td class="new-profile">@ExtractProfile(log.NewValues)</td>
                                <td class="nav-flags-diff">
                                    @GetNavFlagsDiffSummary(log.OldValues, log.NewValues)
                                </td>
                                <td>
                                    <VanAButton Variant="ghost" Size="small" OnClick="() => ShowDetails(log)">
                                        👁️ Chi tiết
                                    </VanAButton>
                                </td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        </VanACard>
    }
</div>

@if (_selectedLog != null)
{
    <VanAModal Title="Chi tiết thay đổi" IsVisible="true" OnClose="CloseDetails" Size="large">
        <div class="audit-detail">
            <div class="detail-row">
                <label>Thời gian:</label>
                <span>@_selectedLog.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")</span>
            </div>
            <div class="detail-row">
                <label>Người đổi:</label>
                <span>@(_selectedLog.UserName ?? _selectedLog.UserId ?? "System")</span>
            </div>
            @if (!string.IsNullOrEmpty(_selectedLog.OldValues))
            {
                <div class="detail-section">
                    <label>Giá trị cũ:</label>
                    <pre class="json-preview">@FormatJson(_selectedLog.OldValues)</pre>
                </div>
            }
            @if (!string.IsNullOrEmpty(_selectedLog.NewValues))
            {
                <div class="detail-section">
                    <label>Giá trị mới:</label>
                    <pre class="json-preview">@FormatJson(_selectedLog.NewValues)</pre>
                </div>
            }
        </div>
    </VanAModal>
}

<style>
    .khachlink-audit-page .page-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 1rem;
    }
    .khachlink-audit-page .header-actions { display: flex; gap: 0.5rem; }
    .khachlink-audit-page .json-preview {
        background: var(--color-neutral-100, #f5f5f5);
        padding: 0.75rem;
        border-radius: 4px;
        font-size: 0.85rem;
        max-height: 300px;
        overflow: auto;
        white-space: pre-wrap;
    }
</style>

@code {
    [Parameter]
    public Guid InstanceId { get; set; }

    private List<AuditLog> _auditLogs = new();
    private string? _instanceLabel;
    private bool _isLoading = true;
    private string _alertMessage = string.Empty;
    private string _alertType = "info";
    private AuditLog? _selectedLog;

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        _isLoading = true;
        try
        {
            // Fetch instance label for header
            var instances = await InstanceApi.ListAsync();
            var instance = instances.FirstOrDefault(i => i.Id == InstanceId);
            _instanceLabel = instance?.Label;

            // Fetch audit history for this instance
            _auditLogs = (await AuditTrailService.GetEntityHistoryAsync(
                AuditableEntityType.KhachLinkInstance,
                InstanceId,
                maxResults: 100)).ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading audit history for KhachLinkInstance {Id}", InstanceId);
            _alertMessage = $"Lỗi tải dữ liệu: {ex.Message}";
            _alertType = "danger";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task RefreshData()
    {
        await LoadData();
    }

    private void GoBack()
    {
        NavigationManager.NavigateTo("/admin/khachlink-instances");
    }

    private void ShowDetails(AuditLog log)
    {
        _selectedLog = log;
        StateHasChanged();
    }

    private void CloseDetails()
    {
        _selectedLog = null;
        StateHasChanged();
    }

    /// <summary>Extract Profile value from JSON audit values.</summary>
    private static string ExtractProfile(string? jsonValues)
    {
        if (string.IsNullOrEmpty(jsonValues))
            return "—";
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(jsonValues);
            if (doc.RootElement.TryGetProperty("Profile", out var profile))
                return profile.GetString() ?? "—";
        }
        catch { }
        return "—";
    }

    /// <summary>Summarize nav flags changes between old and new values.</summary>
    private static string GetNavFlagsDiffSummary(string? oldJson, string? newJson)
    {
        if (string.IsNullOrEmpty(oldJson) || string.IsNullOrEmpty(newJson))
            return "—";
        try
        {
            using var oldDoc = System.Text.Json.JsonDocument.Parse(oldJson);
            using var newDoc = System.Text.Json.JsonDocument.Parse(newJson);
            if (!oldDoc.RootElement.TryGetProperty("NavFlags", out var oldNav) ||
                !newDoc.RootElement.TryGetProperty("NavFlags", out var newNav))
                return "—";

            // NavFlags is a nested JSON string — parse it
            var oldFlags = System.Text.Json.JsonDocument.Parse(oldNav.GetString() ?? "{}").RootElement;
            var newFlags = System.Text.Json.JsonDocument.Parse(newNav.GetString() ?? "{}").RootElement;

            int added = 0, removed = 0;
            foreach (var prop in oldFlags.EnumerateObject())
            {
                if (newFlags.TryGetProperty(prop.Name, out var newVal))
                {
                    if (prop.Value.GetBoolean() && !newVal.GetBoolean()) removed++;
                    if (!prop.Value.GetBoolean() && newVal.GetBoolean()) added++;
                }
            }
            if (added == 0 && removed == 0) return "Không đổi";
            var parts = new List<string>();
            if (added > 0) parts.Add($"+{added}");
            if (removed > 0) parts.Add($"-{removed}");
            return string.Join(" ", parts);
        }
        catch { }
        return "—";
    }

    /// <summary>Pretty-print JSON for detail modal.</summary>
    private static string FormatJson(string? json)
    {
        if (string.IsNullOrEmpty(json)) return "";
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return System.Text.Json.JsonSerializer.Serialize(doc, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch { return json; }
    }
}
```

**Notes:**
- Route `/admin/khachlink-instances/{InstanceId:guid}/audit` — sub-route of instances page.
- `IAuditTrailService.GetEntityHistoryAsync(AuditableEntityType.KhachLinkInstance, InstanceId)` — queries audit logs for this specific instance.
- `KhachLinkInstanceApiClient.ListAsync()` — fetch instance label for header (reuse existing API client).
- `ExtractProfile` + `GetNavFlagsDiffSummary` — parse JSON OldValues/NewValues to display profile + nav flags diff.
- NavFlags is double-serialized (JSON string within JSON) because `KhachLinkInstanceService.UpdateAsync` serializes NavFlags as a nested JSON string. Parse twice.
- UI Platform: `VanACard`, `VanAButton`, `VanAAlert`, `VanAModal`, `VanASpinner` — same components as `AuditTrail.razor`.

#### File 9: `5_WebApps/ShopERP/Components/Pages/Admin/KhachLinkInstances.razor` — UPDATE

**Add "Lịch sử" button** in action cell (after "Sửa" button, line 87-88):

```razor
                            <td class="action-cell">
                                <VanAButton Variant="info" Size="small" OnClick="() => OpenEditModal(item)">
                                    Sửa
                                </VanAButton>
                                <VanAButton Variant="secondary" Size="small" OnClick="() => ViewAuditHistory(item)">
                                    📝 Lịch sử
                                </VanAButton>
                                @if (item.IsActive)
                                {
                                    <VanAButton Variant="warning" Size="small" OnClick="() => Deactivate(item)">
                                        Vô hiệu hoá
                                    </VanAButton>
                                }
                                else
                                {
                                    <VanAButton Variant="success" Size="small" OnClick="() => Activate(item)">
                                        Kích hoạt
                                    </VanAButton>
                                }
                            </td>
```

**Add `ViewAuditHistory` method** in `@code` block (after `Activate` method):

```csharp
    /// <summary>Sprint 3 P1.2: Navigate to per-instance audit history page.</summary>
    private void ViewAuditHistory(KhachLinkInstanceDto item)
    {
        NavigationManager.NavigateTo($"/admin/khachlink-instances/{item.Id}/audit");
    }
```

**Add `@inject NavigationManager NavigationManager`** at top of file (after line 13):

```razor
@inject NavigationManager NavigationManager
```

---

### Step 9: SW version bump via UpdatedAt cache buster (Task 3.2)

#### File 10: `5_WebApps/KhachLink/wwwroot/js/onboarding-tour.js` — UPDATE

**Add `vananTriggerSWUpdate` function** (at end of file):

```javascript
// Sprint 3 P5.1: Trigger Service Worker update check when profile change detected.
// Forces SW to check for updates (new cached nav config) — does NOT reload page.
// Nav update from StateHasChanged() is immediate; SW bump is for next PWA launch.
window.vananTriggerSWUpdate = function() {
    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.getRegistration().then(function(reg) {
            if (reg) {
                reg.update();
                console.log('[VanAn SW] Update check triggered (profile change detected)');
            }
        }).catch(function(err) {
            console.warn('[VanAn SW] Update check failed:', err);
        });
    }
};
```

#### File 11: `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor` — UPDATE

**9a. Add SW trigger call in profile-change detection** (in `OnInitializedAsync`, after `_profileChangeDirection` is set, inside the `if (profileChanged && dismissed != "true")` block from Sprint 2):

```csharp
                    if (profileChanged && dismissed != "true")
                    {
                        _profileChangeDirection = ProfileChangeDirectionHelper.Compute(lastSeenProfile, instanceConfig.Profile);
                        _showWhatsNewBanner = _profileChangeDirection != ProfileChangeDirection.None;

                        // Sprint 3 P5.1: Trigger SW update when profile change detected.
                        // SW checks for new version → next PWA launch gets fresh nav config.
                        if (_showWhatsNewBanner)
                        {
                            try
                            {
                                await JSRuntime.InvokeVoidAsync("vananTriggerSWUpdate");
                            }
                            catch { /* non-critical — SW update is enhancement */ }
                        }
                    }
```

**Notes:**
- Sprint 2 already added profile-change detection in `KhachLinkLayout.OnInitializedAsync` (compares `instanceConfig.UpdatedAt` vs localStorage `last_seen_profile_at`).
- Sprint 3 adds `vananTriggerSWUpdate()` call when profile change detected — triggers SW `reg.update()` (checks for new SW version).
- SW update is **non-blocking** — doesn't reload page. Nav update from `StateHasChanged()` is immediate (Blazor re-renders with new NavFlags). SW bump is for PWA install cache (next launch).
- `KhachLinkInstanceHttpService` — **NO CHANGE needed**. Sprint 2 already added `UpdatedAt` to client model + cache key `_v3`. The SW trigger uses the existing detection logic.
- `KhachLinkInstanceController` — **NO CHANGE needed**. `UpdatedAt` already in DTO (line 275).

**Governance check:** ✅ KhachLink HTTP-only — SW update is client-side JS interop only. No DbContext injection, no HTTP call. ✅

---

### Step 10: Build + fix errors

```powershell
dotnet build VanAn.sln
```

**Expected errors + fixes:**

| Error | Cause | Fix |
|---|---|---|
| `IAuditTrailService` not found in `KhachLinkInstanceService` | Missing `using VanAn.CoreHub.Services` (if different namespace) | Add `using` — `IAuditTrailService` is in `VanAn.CoreHub.Services` (same namespace as `KhachLinkInstanceService`). ✅ No `using` needed. |
| `IAuditTrailService` not found in `PlatformUserLoginService` | Missing `using` | Add `using VanAn.CoreHub.Services;` — `PlatformUserLoginService` is in `VanAn.CoreHub.Services` namespace. ✅ Same namespace. |
| `IHttpContextAccessor` not found in `PlatformUserLoginService` | Missing `using Microsoft.AspNetCore.Http;` | Add `using Microsoft.AspNetCore.Http;` (already in `AuditTrailService`). |
| `AuditActionType.RateLimitHit` not found | Enum value not added | Verify Step 1 — `RateLimitHit = 15` added to `AuditActionType`. |
| `AuditableEntityType.SecurityEvent` not found | Enum value not added | Verify Step 1 — `SecurityEvent = 13` added to `AuditableEntityType`. |
| `GetService<IAuditTrailService>` not found in `Program.cs` | Missing `using` | Add `using VanAn.CoreHub.Services;` + `using VanAn.Shared.Domain.Audit;` at top of `Program.cs` (or use fully-qualified names as in Step 6 snippet). |
| `context.Lease.GetAllTags()` not found | Rate limiter API differs | Use fallback: `var policy = "rate-limited";` (hardcode). Verify `RateLimitLease` API in .NET 8. |
| `KhachLinkInstanceApiClient` not found in `KhachLinkInstanceAudit.razor` | Missing `@using VanAn.ShopERP.Services` | Add `@using VanAn.ShopERP.Services;` (already in snippet). |
| `IncidentSeverity` not found in `AuditTrail.razor` | Nested enum in `@code` block | Verify enum is inside `@code` block, not outside. Blazor renders `@code` as a partial class. |

**Guard:** Run `guard-check.ps1` if pre-commit hook exists.

---

### Step 10A: Unit tests — toggle gating + queue/writer flush (EXPANDED)

#### File: `6_Testing/VanAn.Core.Tests/Audit/AuditToggleAndQueueTests.cs` — NEW (VERIFY test project structure + naming convention trước khi tạo)

**Test cases (stub `IFeatureFlagService` + `IAuditLogRepository` + real `AuditLogQueue`):**

```csharp
// 1. Toggle gating
[Fact] LogCreateAsync_MasterOff_ReturnsNull_NoRepositoryCall      // Audit_Enabled=false → null, repo.AddAsync never called
[Fact] LogCreateAsync_AccountingGroupOff_AccountingSuppressed     // Audit_Accounting=false + AccountingEntry → null
[Fact] LogCreateAsync_AccountingGroupOff_KhachLinkStillLogged     // Audit_Accounting=false + KhachLinkInstance → logged (group khác)
[Fact] LogCreateAsync_NoSetting_DefaultsOn_LogWritten             // flag service stub: mọi flag default true → log ghi
[Fact] LogSecurityEventAsync_SecurityGroupOff_ReturnsNull         // Audit_Security=false → null

// 2. Hybrid persist
[Fact] LogCreateAsync_AccountingEntry_WritesSyncDirect            // AccountingEntry → repo.AddAsync called, queue.Count == 0
[Fact] LogCreateAsync_KhachLinkInstance_EnqueuesAsync             // KhachLinkInstance → queue.Count == 1, repo.AddAsync never called
[Fact] LogSecurityEventAsync_EnqueuesAsync                        // SecurityEvent → queue.Count == 1
[Fact] LogPeriodCloseAsync_WritesSyncDirect                      // PeriodClosing → sync path

// 3. Queue + writer
[Fact] AuditLogQueue_TryEnqueueDequeue_Roundtrip                 // enqueue N → dequeue N đúng thứ tự
[Fact] AuditLogBackgroundWriter_FlushAsync_WritesBatchToRepo      // enqueue 3 → FlushAsync → repo.AddAsync × 3, queue rỗng
[Fact] AuditLogBackgroundWriter_FlushAsync_EmptyQueue_NoCall     // queue rỗng → repo không bị call
```

**Notes:**
- Stub pattern: xem `6_Testing/VanAn.Core.Tests/Community/StubShopFeatureSettingsService.cs` precedent + cách tests hiện có mock `IAuditLogRepository`.
- `AuditLogBackgroundWriter.FlushAsync` private → test qua `ExecuteAsync` với short `stoppingToken` cancel, hoặc reflection, hoặc extract `FlushAsync` thành `internal` + `InternalsVisibleTo`. Chọn cách đơn giản nhất theo convention tests hiện có.
- Toggle default-true semantics: stub `IFeatureFlagService.IsEnabledAsync(name, default, ct)` trả `default` param khi "no setting" — test đúng semantics default ON.

---

### Step 11: E2E test additions

#### File 12: `6_Testing/e2e-tests/profile-transition.spec.ts` — UPDATE

**Add Sprint 3 test cases** (append after Sprint 2 block):

```typescript
test.describe('Sprint 3 — Audit Log + SW Version Bump (P1.2 + P5.1)', () => {
  // P5.1: SW update trigger — vananTriggerSWUpdate function exists
  test('SW update: vananTriggerSWUpdate function is defined', async ({ page }) => {
    await page.goto(KHACHLINK_URL, { waitUntil: 'networkidle' });

    // Verify the JS function is loaded (onboarding-tour.js includes it)
    const exists = await page.evaluate(() => typeof (window as any).vananTriggerSWUpdate === 'function');
    expect(exists).toBe(true);
  });

  // P5.1: SW update triggers on profile change detection
  test('SW update: triggers when profile change detected (stale last_seen_profile_at)', async ({ page }) => {
    // Simulate stale lastSeenProfileAt → profile change detected → SW update triggered
    await page.addInitScript(() => {
      localStorage.setItem('last_seen_profile_at', '2020-01-01T00:00:00');
      localStorage.setItem('last_seen_profile', 'Directory');
      localStorage.removeItem('whats_new_dismissed');
    });

    // Intercept console.log to capture SW update trigger message
    const swUpdateLogs: string[] = [];
    page.on('console', msg => {
      if (msg.text().includes('[VanAn SW]')) swUpdateLogs.push(msg.text());
    });

    await page.goto(KHACHLINK_URL, { waitUntil: 'networkidle' });
    await page.waitForTimeout(3000);  // Allow time for profile detection + SW trigger

    // If profile actually changed (UpdatedAt > 2020-01-01), SW update should trigger
    // Note: only triggers if instance.UpdatedAt > stale timestamp
    const banner = page.locator('.whats-new-banner');
    if (await banner.count() > 0) {
      // Banner shows → profile change detected → SW update should have triggered
      expect(swUpdateLogs.some(l => l.includes('Update check triggered'))).toBe(true);
    }
  });

  // P1.2: Audit log — verify AuditTrail API endpoint exists (admin-only)
  // NOTE: This test verifies the API endpoint is reachable, not the audit log content
  // (which requires admin JWT + actual profile change). Manual RV covers full flow.
  // FIX (review issue #1): /api/audit-trail sống ở GATEWAY (5001), KHÔNG phải KhachLink (5002).
  // Dùng GATEWAY_URL — verify public Gateway URL +/nginx /api/ proxy config trước khi chạy.
  test('Audit API: entity history endpoint exists (GET /api/audit-trail/entity/{type}/{id})', async ({ request }) => {
    // Without admin auth, should return 401 (endpoint exists but requires auth)
    const response = await request.get(
      `${GATEWAY_URL.replace(/\/$/, '')}/api/audit-trail/entity/12/00000000-0000-0000-0000-000000000000`
    );
    // 401 = endpoint exists but requires auth (expected without JWT)
    // 404 = endpoint not found (FAIL)
    expect([401, 403]).toContain(response.status());
  });
});
```

**Notes:**
- E2E tests can't verify audit log content (requires SystemAdmin JWT + actual profile change). Manual RV covers full flow.
- SW update trigger test: verifies `vananTriggerSWUpdate` function exists + triggers on profile change (via console log capture).
- Audit API test: verifies endpoint exists (returns 401/403 without auth, not 404).
- `entity/12/{id}` — `12` = `AuditableEntityType.KhachLinkInstance` (int value).
- **FIX (review issue #1):** test gốc dùng `KHACHLINK_URL` — SAI. `/api/audit-trail` ở Gateway (5001). Thêm constant `GATEWAY_URL` vào spec (verify public URL / nginx proxy trước khi chạy). Nếu nginx trên khachlink domain có proxy `/api/` → Gateway → có thể giữ KHACHLINK_URL — VERIFY.
- **RESOLVED (review issues #3 + #7):** `GetEntityHistoryAsync(entityType, entityId, maxResults = 100)` — signature có `maxResults` ✅ (verified `AuditTrailService.cs:250-254`). `GetRecentAsync(count = 50)` ✅ (verified line 263-270). Code Step 7b/8 dùng đúng.

---

### Step 12: RV on production (timlathay.com + diemthuong2.khachvip.online)

Follow `.devin/rules/runtime-verification.md` 5-layer protocol:

**Layer 1 — API checks:**
- `GET /api/audit-trail/entity/12/{instanceId}` with SystemAdmin JWT → returns audit history for KhachLinkInstance
- `GET /api/audit-trail/recent?count=10` → verify new action types (FailedLogin, RateLimitHit) appear if triggered

**Layer 2 — Static assets:**
- `GET /js/onboarding-tour.js` → verify `vananTriggerSWUpdate` function present
- No new CSS files (enhanced existing AuditTrail.razor scoped styles)

**Layer 3 — Playwright runtime:**
- `npx playwright test profile-transition` → Sprint 3 tests PASS

**Layer 4 — UI flow:**
- Admin: đổi profile Directory → FullCommerce on diemthuong2 → verify audit log row inserted (query `GET /api/audit-trail/entity/12/{diemthuong2InstanceId}`) — **EXPANDED: KhachLink audit giờ ASYNC — chờ ~5-10s trước khi query (flush interval 5s)**
- Admin: vào `/admin/audit-trail` → verify summary cards show counts + severity badges in table
- Admin: vào `/admin/khachlink-instances` → click "Lịch sử" → verify audit history page shows profile change
- Failed login: attempt bad password on `/api/platform/login` → verify FailedLogin audit log created — **EXPANDED: ASYNC — chờ ~5-10s + verify `IpAddress` structured field populated (không chỉ trong Reason)**
- Rate limit: trigger auth rate limit (6+ login attempts/min) → verify RateLimitHit audit log created — **EXPANDED: ASYNC — chờ ~5-10s**
- **EXPANDED — Toggle RV:** (a) vào `/admin/valcn-features` → verify 4 audit toggles hiển thị, default ON; (b) tắt `Audit_KhachLink` → đổi profile → chờ 10s → verify KHÔNG có audit row mới; (c) bật lại → đổi profile → verify row xuất hiện (chờ 5-10s + cache 30s); (d) tắt `Audit_Security` → failed login → verify không có row; (e) bật lại → verify row
- **EXPANDED — Accounting sync RV:** tạo bút toán kế toán mới (app2.khachvip.online) → verify audit row xuất hiện NGAY (sync path — không có delay 5s)

**Layer 5 — Manual browser:**
- Open KhachLink on diemthuong2 → change profile → reopen → verify SW update triggered (console log) + nav updated

**STOP at first failure** — don't proceed to next layer if current fails.

---

## 3. Governance Checklist

- [x] **DOMAIN MOD (enum additive only):** `AuditableEntityType` +12,+13; `AuditActionType` +12,+13,+14,+15. Additive, không renumber. Enum stored as int, backward compatible. ✅
- [x] **DOMAIN MOD (factory additive):** `AuditLog.ForSecurityEvent` — new factory method, existing factories unchanged. `AuditLog` remains immutable (private constructor, factory-only). ✅
- [x] **AuditLog immutable** — append-only, reuse `AuditTrailService.LogUpdateAsync` + new `LogSecurityEventAsync`. No update/delete. ✅
- [x] **Domain PURE** — only enum values + factory method added. No EF Core, no DbContext, no DataAnnotations in Domain. ✅
- [x] **UI Platform** — `AuditTrail.razor` + `KhachLinkInstanceAudit.razor` use VanAn.UI.Platform components (VanACard, VanAButton, VanAModal, VanAAlert, VanASpinner). ✅
- [x] **KhachLink HTTP-only** — SW update client-side JS interop only. No DbContext injection. ✅
- [x] **No new .csproj** — all changes in existing projects. ✅
- [x] **No migration** — enum stored as int, new values 12-15 backward compatible. ✅
- [x] **Multi-tenancy** — `KhachLinkInstance.TenantId = Guid.Empty` (platform sentinel). Security events use `_tenantProvider.TenantId` (Guid.Empty for anonymous). ✅
- [x] **Playwright isolation** — E2E only after build pass + implementation complete. ✅
- [x] **Severity classification = presentation-only** — `IncidentSeverity` enum + `GetSeverity` switch live in `AuditTrail.razor` `@code` block (UI layer). No Domain change for classification. ✅
- [x] **EXPANDED — Audit toggle:** `IFeatureFlagService` optional param `defaultWhenMissing` (backward compatible) + 4 audit flags default ON qua `SystemSetting` pattern sẵn có. Service layer only — không đụng Domain. ✅
- [x] **EXPANDED — Hybrid async:** Accounting audit SYNC (giữ toàn vẹn kế toán TT 152/2025) + Security/KhachLink audit ASYNC qua `AuditLogQueue` (bounded 1000, DropOldest) + `AuditLogBackgroundWriter` (BackgroundService, IServiceScopeFactory pattern). `AuditLog` immutable — queue chỉ defer write, không modify entity. ✅
- [x] **EXPANDED — Return type `Task<AuditLog>` → `Task<AuditLog?>`:** interface signature change nhưng source-compatible (callers hiện có discard return value — verified). Grep lại mọi caller trước khi build. ✅

---

## 4. Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | Enum addition phá backward compatibility | Additive only — values 12-15 appended. Enum stored as int, existing 1-11 unchanged. No migration. |
| R2 | `IAuditTrailService` không resolve trong rate limiter `OnRejected` | `HttpContext.RequestServices.GetService<IAuditTrailService>()` — scoped service resolvable in request pipeline. Best-effort try/catch — 429 response not blocked. |
| R3 | `IHttpContextAccessor` không registered trong `PlatformUserLoginService` DI | `AuditTrailService` already injects it → registered in all 3 hosts. Verify DI registration. |
| R4 | `context.Lease.GetAllTags()` API không tồn tại | Fallback: hardcode `var policy = "rate-limited";`. Verify `RateLimitLease` API in .NET 8. |
| R5 | Audit log JSON serialize NavFlags quá lớn | NavFlags = 15 bools → JSON ~200 chars. Style = 5 fields → ~100 chars. Total OldValues/NewValues ~400 chars each. Acceptable. |
| R6 | Summary cards `GetRecentAsync(200)` không cover 24h nếu volume cao | MVP acceptable — admin dashboard, not real-time monitoring. Future: add server-side count endpoint. |
| R7 | Severity classification sai cho action type mới | `GetSeverity` switch covers all new action types (FailedLogin=High, RateLimitHit=Critical, SuspiciousActivity=Critical, SecurityAlert=High). Default = Low. |
| R8 | `KhachLinkInstanceAudit.razor` NavFlags double-serialized parse fail | `GetNavFlagsDiffSummary` wraps in try/catch → returns "—" on parse error. Defensive parsing. |
| R9 | SW update không trigger trên iOS Safari | iOS PWA SW update limited. Fallback: nav update từ `StateHasChanged()` đủ cho current session. SW bump cho next launch. (Same as Sprint 3 task card R5.) |
| R10 | Failed login audit spam nếu brute-force attack | Rate limiter (5 req/min/IP) caps login attempts. Each failed login logs 1 audit row. 5 rows/min max per IP. Acceptable — audit log is append-only, designed for this. |
| RE1 | **EXPANDED:** Async queue mất Security/KhachLink log khi process crash đột ngột (kill -9/OOM) | User chấp nhận hybrid: Accounting audit SYNC (không mất). Mitigation async path: bounded 1000 + DropOldest + flush 5s/100 items + residual flush graceful shutdown. Tối đa mất = queue capacity + batch chờ. |
| RE2 | **EXPANDED:** Default-true audit flags phá VALCN default-false semantics | Optional param `defaultWhenMissing = false` — callers VALCN hiện có không đổi. Chỉ audit call sites truyền `true`. `GetAllAsync` cập nhật theo `Default` per feature — UI hiển thị đúng. |
| RE3 | **EXPANDED:** `AuditTrailService` ctor thêm 2 dependencies → DI resolve fail nếu thiếu registration | `AuditLogQueue` (Singleton) + `IFeatureFlagService` phải register ở Gateway + ShopERP TRƯỚC khi build. Gateway `OnRejected` dùng `GetService` (không throw) → thiếu registration = audit silently skipped — phải verify bằng integration test / RV. |
| RE4 | **EXPANDED:** BackgroundWriter drop batch khi DB lỗi | LogError + drop (không re-enqueue — tránh vòng lặp khi DB down). Best-effort semantics cho security/KhachLink. Accounting không ảnh hưởng (sync path riêng). |
| RE5 | **EXPANDED:** Async audit row trễ ~5s — admin query ngay không thấy | RV Layer 4 note: chờ 5-10s trước khi query. Dashboard `GetRecentAsync` hiển thị khi flush xong. Acceptable MVP. |
| RE6 | **EXPANDED:** `Task<AuditLog?>` breaking cho caller dùng return value | Verified: `AccountingEntryService.cs:93` + Sprint 3 Steps 4/5/6 discard. VERIFY at implementation: grep `.LogCreateAsync(`, `.LogUpdateAsync(`... toàn repo — nếu caller nào dùng return → thêm null-check. |

---

## 5. Files Summary

| # | File | Action | Step | Status |
|---|---|---|---|---|
| 1 | `1_Shared/Domain/Audit/AuditLog.cs` | UPDATE (add enum values + ForSecurityEvent factory) | 1, 2 | ⏳ |
| 2 | `3_CoreHub/Services/IAuditTrailService.cs` | UPDATE (LogSecurityEventAsync + `Task<AuditLog?>` return types) | 3 | ⏳ |
| 3 | `3_CoreHub/Services/AuditTrailService.cs` | UPDATE (toggle gate + hybrid persist + LogSecurityEventAsync impl) | 3 | ⏳ |
| 4 | `3_CoreHub/Services/KhachLinkInstanceService.cs` | UPDATE (inject IAuditTrailService, log UpdateAsync) | 4 | ⏳ |
| 5 | `3_CoreHub/Services/PlatformUserLoginService.cs` | UPDATE (inject IAuditTrailService + IHttpContextAccessor, log failed login) | 5 | ⏳ |
| 6 | `2_Gateway/Program.cs` | UPDATE (OnRejected audit + register AuditLogQueue + BackgroundWriter) | 6, 3B | ⏳ |
| 7 | `5_WebApps/ShopERP/Components/Pages/Admin/AuditTrail.razor` | UPDATE (summary cards + severity classification + filters) | 7 | ⏳ |
| 8 | `5_WebApps/ShopERP/Components/Pages/Admin/KhachLinkInstanceAudit.razor` | NEW (per-instance audit history) | 8 | ⏳ |
| 9 | `5_WebApps/ShopERP/Components/Pages/Admin/KhachLinkInstances.razor` | UPDATE (add "Lịch sử" button + ViewAuditHistory) | 8 | ⏳ |
| 10 | `5_WebApps/KhachLink/wwwroot/js/onboarding-tour.js` | UPDATE (add vananTriggerSWUpdate) | 9 | ⏳ |
| 11 | `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor` | UPDATE (call vananTriggerSWUpdate on profile change) | 9 | ⏳ |
| 12 | `6_Testing/e2e-tests/profile-transition.spec.ts` | UPDATE (Sprint 3 test cases) | 11 | ⏳ |
| 13 | `3_CoreHub/Services/IFeatureFlagService.cs` | UPDATE (optional `defaultWhenMissing` param) | 1A | ⏳ |
| 14 | `3_CoreHub/Services/FeatureFlagService.cs` | UPDATE (defaultWhenMissing + 4 audit flags + Default field) | 1A | ⏳ |
| 15 | `3_CoreHub/Services/AuditLogQueue.cs` | NEW (bounded Channel, DropOldest, capacity 1000) | 3A | ⏳ |
| 16 | `3_CoreHub/Services/AuditLogBackgroundWriter.cs` | NEW (BackgroundService, flush 5s/100, residual flush) | 3B | ⏳ |
| 17 | `5_WebApps/ShopERP/Program.cs` | UPDATE (register AuditLogQueue + BackgroundWriter) | 3B | ⏳ |
| 18 | `5_WebApps/ShopERP/Components/Pages/Admin/ValcnFeatures.razor` (VERIFY path) | VERIFY (likely no change — page iterate GetAllAsync) | 7A | ⏳ |
| 19 | `6_Testing/VanAn.Core.Tests/Audit/AuditToggleAndQueueTests.cs` (VERIFY structure) | NEW (toggle gating + hybrid persist + queue/writer tests) | 10A | ⏳ |

**Total:** 14 modified + 4 new + 1 verify = 19 files (gốc 12 + mở rộng 7)

---

## 6. Open Questions (resolve during implementation)

1. **`IHttpContextAccessor` DI in `PlatformUserLoginService`:** Verify `IHttpContextAccessor` registered in DI. `AuditTrailService` already injects it → should be registered. Verify lúc implement.
2. **`RateLimitLease.GetAllTags()` API:** Verify method exists in .NET 8 `RateLimiter`. If not → use fallback `var policy = "rate-limited";`. Verify lúc implement.
3. **`KhachLinkInstanceApiClient` in `KhachLinkInstanceAudit.razor`:** Verify `KhachLinkInstanceApiClient` is injectable in Blazor Server page (it's used in `KhachLinkInstances.razor` → should work). Verify lúc implement.
4. **Audit log query for `KhachLinkInstance` from ShopERP:** `IAuditTrailService.GetEntityHistoryAsync` queries via `IAuditLogRepository.GetByEntityAsync` — verify this returns cross-tenant results for SystemAdmin (not tenant-filtered). `GetEntityHistoryAsync` doesn't check `IsSystemAdminWithoutTenant` (only `QueryAsync` does). Verify `IAuditLogRepository.GetByEntityAsync` is cross-tenant. Verify lúc implement.
5. **EXPANDED — `IFeatureFlagService` DI registration:** Verify registered trong Gateway + ShopERP (VALCN v2 đang dùng → có lẽ có). Nếu thiếu → add `AddSingleton<IFeatureFlagService, FeatureFlagService>()` ở cả 2 hosts. Verify lúc implement.
6. **EXPANDED — "CoreHub Program.cs line 109" (OQ1 evidence):** Governance nói 3_CoreHub là pure Class Library (no Exe). Verify file này là gì — test host? legacy? Nếu là host chạy thật → register `AuditLogQueue` + `AuditLogBackgroundWriter` ở đó luôn. Verify lúc implement.
7. **EXPANDED — Callers dùng return value của `Log*Async`:** Grep toàn repo `.LogCreateAsync(`, `.LogUpdateAsync(`, `.LogDeleteAsync(`, `.LogPeriodCloseAsync(`, `.LogPeriodReopenAsync(`, `.LogCorrectionAsync(`, `.LogReversalAsync(` — nếu caller nào GÁN result (không discard) → thêm null-handling. Hiện verified `AccountingEntryService.cs:93` discard. Verify lúc implement.
8. **EXPANDED — Admin feature-flag page path:** Verify page render `/admin/valcn-features` tên file thật + có iterate `GetAllAsync()` hay hardcode flag list. Verify lúc implement (Step 7A).
9. **EXPANDED — Test project structure:** Verify `6_Testing/VanAn.Core.Tests` convention (folder structure, naming, stub pattern — xem `StubShopFeatureSettingsService.cs` precedent) trước khi tạo `AuditToggleAndQueueTests.cs`. Verify lúc implement (Step 10A).

---

## 7. Related

- Master plan: `docs/AI/tasks/khachlink_profile_transition_ux/master_plan.md`
- Top-level card: `docs/AI/tasks/khachlink_profile_transition_ux/task_card.md`
- Sprint 1 (prerequisite): `docs/AI/tasks/khachlink_profile_transition_ux/task_card_sprint1_guardrail_foundation.md`
- Sprint 2 (prerequisite): `docs/AI/tasks/khachlink_profile_transition_ux/task_card_sprint2_transition_messaging.md`
- Sprint 2 coding plan: `docs/AI/tasks/khachlink_profile_transition_ux/coding_plan_sprint2_transition_messaging.md`
- AuditLog entity: `1_Shared/Domain/Audit/AuditLog.cs`
- AuditTrailService: `3_CoreHub/Services/AuditTrailService.cs`
- IAuditTrailService: `3_CoreHub/Services/IAuditTrailService.cs`
- AuditTrailController: `2_Gateway/Controllers/AuditTrailController.cs`
- AuditTrail.razor (existing): `5_WebApps/ShopERP/Components/Pages/Admin/AuditTrail.razor`
- KhachLinkInstanceService: `3_CoreHub/Services/KhachLinkInstanceService.cs`
- KhachLinkInstanceController: `2_Gateway/Controllers/KhachLinkInstanceController.cs`
- PlatformUserLoginService: `3_CoreHub/Services/PlatformUserLoginService.cs`
- Gateway rate limiter: `2_Gateway/Program.cs` lines 98-216
- AccountingEntryService (precedent — inject IAuditTrailService): `3_CoreHub/Services/AccountingEntryService.cs`
