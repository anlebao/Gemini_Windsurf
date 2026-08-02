# PLAN: Hoàn thành Loyalty Phase C gaps + Customer Segmentation & Promo Push + Sitemap update

> **Created:** 2026-07-27
> **Status:** DRAFT — awaiting user approval
> **Branch target:** `main`
> **Prerequisites:** Loyalty Phase A/B/C audit complete (this session)

---

## TỔNG QUAN

3 workstreams song song có thể chia session độc lập:

| WS | Mô tả | Scope | Est. sessions |
|----|-------|-------|---------------|
| **WS-1** | Hoàn thành 6 PARTIAL SC của Loyalty Phase C | Notification wire-up + Missions history + URL validation | 2 |
| **WS-2** | Customer List UI + Filter + Promo Push Campaign (Queue+Job) | New feature — CRM + marketing automation | 4 |
| **WS-3** | Sitemap update — thêm menu links cho WS-1/WS-2 outputs | NavMenu.razor + Sitemap.razor | 1 (cuối cùng, sau WS-1+WS-2) |

**Total: ~7 sessions**

---

## WS-1: HOÀN THÀNH LOYALTY PHASE C GAPS (6 PARTIAL SC)

### 1.1 Notification wire-up (HIGH — 5 items)

**Vấn đề:** ShopFeatureSettings có 5 notification toggles + UI config đầy đủ, nhưng service layer không đọc toggle và không gọi push. Toggle hiện tại là "trang trí" — không có tác dụng thực.

#### 1.1.1 Thêm 2 method thiếu trong PushNotificationService
**File:** `3_CoreHub/Services/PushNotificationService.cs`

```csharp
public async Task<int> SendRedemptionFulfilledNotificationAsync(
    Guid customerId, string voucherCode, string productName)
// Push: "Voucher {code} đã được xác nhận — {productName}. Đến quán để nhận hàng."

public async Task<int> SendRedemptionCancelledNotificationAsync(
    Guid customerId, string voucherCode, int pointsRefunded)
// Push: "Đổi điểm đã hủy — hoàn {pointsRefunded} điểm. Voucher {code} đã hết hiệu lực."
```

Pattern: copy `SendBirthdayNotificationAsync` (line 396) — same structure (load subscriptions → build payload → enqueue Outbox → publish NATS).

#### 1.1.2 Wire-up RedemptionService
**File:** `3_CoreHub/Services/RedemptionService.cs`

- Inject `IShopFeatureSettingsService` + `IPushNotificationService` (or `ILoyaltyNotificationService` nếu có wrapper)
- `FulfillAsync` (line 159-195): sau `record.MarkAsFulfilled` →
  ```csharp
  var settings = await _shopFeatureSettingsService.GetSettingsAsync(tenantId);
  if (settings.Notify_RedemptionFulfilled)
      await _pushNotificationService.SendRedemptionFulfilledNotificationAsync(
          record.CustomerId, voucher.VoucherCode, catalogItem?.ProductName ?? "");
  ```
- `CancelAsync` (line 197-233): sau refund points →
  ```csharp
  if (settings.Notify_RedemptionCancelled)
      await _pushNotificationService.SendRedemptionCancelledNotificationAsync(
          record.CustomerId, voucher?.VoucherCode ?? "", record.PointsSpent);
  ```

#### 1.1.3 Wire-up MissionService
**File:** `3_CoreHub/Services/MissionService.cs`

- Inject `IShopFeatureSettingsService` + `IPushNotificationService`
- `CompleteMissionAsync` (line 67-166): sau `AddPointsAsync` + tạo MissionCompletion →
  ```csharp
  var settings = await _shopFeatureSettingsService.GetSettingsAsync(tenantId);
  if (settings.Notify_MissionCompleted)
      await _pushNotificationService.SendLoyaltyPointsChangedNotificationAsync(
          customerId, mission.PointsReward, $"Hoàn thành nhiệm vụ: {mission.Title}");
  ```
  (`SendLoyaltyPointsChangedNotificationAsync` đã có sẵn — line 142)

#### 1.1.4 Wire-up BirthdayBonusJob toggle
**File:** `5_WebApps/ShopERP/Services/BirthdayBonusJob.cs`

- Inject `IShopFeatureSettingsService`
- Trước khi gọi `SendBirthdayNotificationAsync` (line 142) →
  ```csharp
  var settings = await _shopFeatureSettingsService.GetSettingsAsync(customer.TenantId);
  if (!settings.Notify_BirthdayBonus) continue; // skip push, still award points
  ```
  **Lưu ý:** Points vẫn award (business logic), chỉ skip push notification.

#### 1.1.5 Wire-up VoucherExpiryReminderJob toggle
**File:** `5_WebApps/ShopERP/Services/VoucherExpiryReminderJob.cs`

- Inject `IShopFeatureSettingsService`
- Trước khi gọi `SendVoucherExpiryReminderAsync` (line 145) →
  ```csharp
  var settings = await _shopFeatureSettingsService.GetSettingsAsync(voucher.TenantId);
  if (!settings.Notify_VoucherExpiringSoon) continue;
  ```
  **Lưu ý:** Job vẫn query vouchers (để log), chỉ skip push. Hoặc skip luôn query nếu toggle off cho tenant — optimize performance.

**Tests:** Update/create unit tests cho toggle on/off scenarios (5 test cases — 1 per toggle).

---

### 1.2 Missions.razor MissionCompletion history (MEDIUM — SC11/SC16)

**Vấn đề:** Backend API `GET /api/missions/my/completions` + `MissionService.GetCustomerCompletionsAsync` đã có, nhưng Missions.razor không gọi và không render history.

**File:** `5_WebApps/KhachLink/Pages/Missions.razor`

- Thêm section "Nhiệm vụ đã hoàn thành" ở cuối page (sau proof submit form)
- Gọi `GET /api/missions/my/completions` (qua Gateway forward)
- Render table/list: Mission Title | Points Awarded | Completed Date | Metadata preview (share URL)
- Pagination: load 20 items, "Xem thêm" button nếu >20
- Empty state: "Chưa hoàn thành nhiệm vụ nào" nếu list rỗng

**Layout reference:** Copy pattern từ `RedemptionCatalog.razor` voucher display section.

---

### 1.3 Social share URL validation (MEDIUM — SC9/SC10)

**Vấn đề:** Hiện chỉ check `host.Contains("facebook.com")` / `host.Contains("tiktok.com")` — user có thể submit bất kỳ URL nào trên 2 domain này (vd: trang chủ, profile, comment).

**File:** `5_WebApps/ShopERP/Controllers/CustomerProfileController.cs` (line 146-159)

Thay domain-only check bằng pattern validation:

```csharp
// Facebook: accept facebook.com/<user>/posts/<id>, fb.com/<user>/posts/<id>,
//           facebook.com/permalink.php?story_id=, facebook.com/share/v/
if (host.Contains("facebook.com") || host.Contains("fb.com"))
{
    bool validFb = uri.AbsolutePath.Contains("/posts/") 
                || uri.AbsolutePath.Contains("/permalink")
                || uri.Query.Contains("story_id=")
                || uri.AbsolutePath.Contains("/share/");
    if (!validFb)
        return BadRequest(new { error = "URL Facebook phải là link bài viết (posts/permalink), không phải trang chủ/profile." });
    missionType = MissionType.FacebookShare;
}
// TikTok: accept tiktok.com/@<user>/video/<id>, tiktok.com/<user>/video/<id>
else if (host.Contains("tiktok.com"))
{
    bool validTt = uri.AbsolutePath.Contains("/video/");
    if (!validTt)
        return BadRequest(new { error = "URL TikTok phải là link video (tiktok.com/@user/video/...), không phải trang chủ/profile." });
    missionType = MissionType.TikTokShare;
}
```

**Lưu ý:** KHÔNG verify URL thật (Facebook/TikTok không có callback API — trust-based per task card Q1). Chỉ filter format sai + empty URL.

**Tests:** Unit test 5 cases per platform: valid post URL, valid permalink, invalid (homepage), invalid (profile), invalid (empty).

---

## WS-2: CUSTOMER LIST + FILTER + PROMO PUSH CAMPAIGN (NEW FEATURE)

### 2.1 Domain layer — extend CustomerSegmentCriteria + new PromoCampaign entity

#### 2.1.1 Extend CustomerSegmentCriteria
**File:** `3_CoreHub/Domain/Repositories/ICustomerRepository.cs` (line 9-16)

```csharp
public record CustomerSegmentCriteria(
    string? CustomerTier = null,
    IdentityLevel? MinIdentityLevel = null,
    decimal? MinTotalSpent = null,
    decimal? MaxTotalSpent = null,
    DateTime? LastOrderAfter = null,
    DateTime? LastOrderBefore = null,
    bool HasPushSubscription = false,
    // NEW — WS-2 filters:
    int? MinPointBalance = null,      // Filter by loyalty points range
    int? MaxPointBalance = null,
    int? BirthdayMonth = null,        // 1-12, null = no filter
    int? LastOrderWithinDays = null   // Convenience: LastOrderAfter = Now.AddDays(-N)
);
```

**Update `CustomerRepository.GetBySegmentAsync`** (line 104+) để handle 4 new fields:
- `MinPointBalance`/`MaxPointBalance`: JOIN LoyaltyRewards table (CustomerId → PointBalance)
- `BirthdayMonth`: `c.Birthday.HasValue && c.Birthday.Value.Month == criteria.BirthdayMonth`
- `LastOrderWithinDays`: convert to `LastOrderAfter = DateTime.UtcNow.AddDays(-N)` trước query

#### 2.1.2 New PromoCampaign entity
**File:** `1_Shared/Domain.cs`

```csharp
public class PromoCampaign : BaseEntity, IMustHaveTenant
{
    public string Title { get; protected set; } = string.Empty;
    public string Message { get; protected set; } = string.Empty;  // Push notification body
    public string? Url { get; protected set; }                     // Optional deep link
    public string Status { get; protected set; } = "Pending";      // Pending/Processing/Completed/Failed/Cancelled
    public int TotalRecipients { get; protected set; }             // Segment count at creation
    public int SentCount { get; protected set; }
    public int FailedCount { get; protected set; }
    public DateTime? StartedAt { get; protected set; }
    public DateTime? CompletedAt { get; protected set; }
    public string? SegmentSnapshotJson { get; protected set; }     // Criteria used (audit)
    public string? ErrorMessage { get; protected set; }
    
    // Factory + state transitions (MarkProcessing, MarkCompleted, IncrementSent, etc.)
}

public class PromoCampaignRecipient : BaseEntity, IMustHaveTenant
{
    public Guid PromoCampaignId { get; protected set; }
    public Guid CustomerId { get; protected set; }
    public string Status { get; protected set; } = "Pending";  // Pending/Sent/Failed
    public DateTime? SentAt { get; protected set; }
    public string? ErrorMessage { get; protected set; }
}
```

**Migration:** `5_WebApps/ShopERP/Migrations/20260727_AddPromoCampaign.cs` — tạo 2 tables (SQLite, tenant-scoped).

---

### 2.2 Service layer — PromoCampaignService + PromoCampaignJob

#### 2.2.1 PromoCampaignService
**Files (NEW):**
- `1_Shared/Services/IPromoCampaignService.cs` — contract
- `3_CoreHub/Services/PromoCampaignService.cs` — implementation

**Methods:**
```csharp
Task<PromoCampaign> CreateCampaignAsync(
    string title, string message, string? url, CustomerSegmentCriteria criteria);
    // 1. Query segment count via ICustomerSegmentationService
    // 2. Create PromoCampaign (Pending) + PromoCampaignRecipient records (one per customer)
    // 3. Save SegmentSnapshotJson (criteria audit)
    // 4. Return campaign with TotalRecipients count

Task<PromoCampaign?> GetCampaignAsync(Guid campaignId);
Task<IReadOnlyList<PromoCampaign>> GetCampaignsAsync(int page, int pageSize);
Task<bool> CancelCampaignAsync(Guid campaignId);  // Only if Pending/Processing
Task<IReadOnlyList<Customer>> PreviewSegmentAsync(CustomerSegmentCriteria criteria);  // Dry-run filter
```

#### 2.2.2 PromoCampaignJob (HostedService — Outbox pattern)
**File (NEW):** `5_WebApps/ShopERP/Services/PromoCampaignJob.cs`

**Flow:**
1. Poll every 30s for PromoCampaigns with Status=Pending
2. Pick oldest Pending → mark Processing → record StartedAt
3. Load PromoCampaignRecipients where Status=Pending (batch 50)
4. For each recipient:
   - Call `PushNotificationService.SendPromoNotificationAsync(customerId, campaign.Message, campaign.Url)`
   - Mark recipient Sent/Failed + increment campaign SentCount/FailedCount
   - 100ms delay between sends (rate limit — avoid push provider ban)
5. When all recipients processed → mark Campaign Completed + record CompletedAt
6. On exception → mark Campaign Failed + ErrorMessage + leave recipients Pending for retry

**Register:** `5_WebApps/ShopERP/Program.cs` — `AddHostedService<PromoCampaignJob>()`

#### 2.2.3 SendPromoNotificationAsync
**File:** `3_CoreHub/Services/PushNotificationService.cs`

```csharp
public async Task<int> SendPromoNotificationAsync(
    Guid customerId, string message, string? url = null)
// Payload type: "promo" — title: "Khuyến mãi", body: message, data: { url }
```

---

### 2.3 API layer — Controllers

#### 2.3.1 ShopERP CustomerController (per-tenant CRM)
**File:** `5_WebApps/ShopERP/Controllers/CustomerController.cs` (NEW)

```
GET  /api/customers                    — list (paginated, tenant-scoped)
GET  /api/customers/segment            — preview filter result (dry-run)
POST /api/customers/export             — export CSV (optional)
```

#### 2.3.2 ShopERP PromoCampaignController (admin)
**File:** `5_WebApps/ShopERP/Controllers/PromoCampaignController.cs` (NEW)

```
GET  /api/promo-campaigns              — list campaigns (paginated)
GET  /api/promo-campaigns/{id}         — campaign detail + recipient status summary
POST /api/promo-campaigns              — create campaign (title, message, url, criteria)
POST /api/promo-campaigns/{id}/cancel  — cancel pending campaign
GET  /api/promo-campaigns/{id}/recipients — list recipients with status (paginated)
```

**Auth:** Cookie auth, `[Authorize(Policy = "OwnerOrSystemAdmin")]` (Owner = per-tenant, SystemAdmin = cross-tenant).

#### 2.3.3 Gateway forward (optional — for KhachLink staff view)
**File:** `2_Gateway/Controllers/PromoCampaignController.cs` (NEW — optional)

Forward `/api/promo-campaigns/**` to ShopERP (same pattern as RedemptionController forward). **Chỉ cần nếu KhachLink cần xem campaigns — mặc định admin-only trong ShopERP.**

---

### 2.4 UI layer — Customer List + Filter + Promo Push

#### 2.4.1 ShopERP CustomerList.razor (per-tenant CRM)
**File (NEW):** `5_WebApps/ShopERP/Components/Pages/Admin/CustomerList.razor`
**Route:** `/admin/customers`

**Layout:**
- Filter bar (top): 4 filter controls + "Lọc" button + "Xóa lọc" button
  1. **Điểm thưởng range:** 2 number inputs (min/max) — placeholder "Từ" / "Đến"
  2. **Lần mua gần nhất:** 1 number input + dropdown unit (5/10/30/60/90 days) — hoặc free text "trong ... ngày"
  3. **Sinh nhật trong tháng:** 1 month picker (default = current month) — `<input type="month">`
  4. **Doanh số range:** 2 number inputs (min/max VND) — placeholder "Từ" / "Đến"
- Results table: Name | Phone | Tier | Points | Total Spent | Last Order Date | Birthday | Identity Level | Push Subscribed
- Pagination: 20/page
- Row action: "Gửi khuyến mãi" button → opens promo compose modal
- Bulk action: checkbox select + "Gửi khuyến mãi cho N khách đã chọn" → opens promo compose modal (pre-filled segment = selected IDs)

#### 2.4.2 ShopERP PromoPushComposer.razor (modal component)
**File (NEW):** `5_WebApps/ShopERP/Components/Pages/Admin/PromoPushComposer.razor`

**Modal layout:**
- Title: "Gửi thông báo khuyến mãi"
- Recipients summary: "Sẽ gửi cho N khách hàng" (count from filter or selected)
- Form fields:
  - Tiêu đề (text input, max 100 chars)
  - Nội dung thông báo (textarea, max 500 chars)
  - Link đích (optional URL input — deep link to KhachLink page)
- Buttons: "Hủy" | "Tạo chiến dịch" (creates PromoCampaign → job processes async)
- After create: show success toast "Đã tạo chiến dịch '{title}' — đang xử lý. Xem tiến độ tại /admin/promo-campaigns"

#### 2.4.3 ShopERP PromoCampaignList.razor (campaign history + tracking)
**File (NEW):** `5_WebApps/ShopERP/Components/Pages/Admin/PromoCampaignList.razor`
**Route:** `/admin/promo-campaigns`

**Layout:**
- Table: Title | Message (truncated) | Recipients | Sent | Failed | Status | Created | Completed | Actions
- Status badge: Pending (gray) | Processing (blue) | Completed (green) | Failed (red) | Cancelled (gray)
- Row action: "Chi tiết" → expand/collapse recipient list (paginated) OR link to detail page
- Row action: "Hủy" (only if Pending/Processing) → confirm dialog → cancel
- Progress bar for Processing campaigns: SentCount/TotalRecipients

#### 2.4.4 Gateway cross-tenant view (SystemAdmin)
**File (NEW):** `5_WebApps/ShopERP/Components/Pages/Admin/CustomerListGlobal.razor`
**Route:** `/admin/customers-global`

**Same as CustomerList.razor but:**
- No tenant filter (query all tenants)
- Add "Tenant" column
- `[Authorize(Policy = "SystemAdmin")]`
- Calls different endpoint (cross-tenant query — needs new `GetAllCustomersAcrossTenantsAsync` in CustomerRepository with `IgnoreQueryFilters`)

**Lưu ý:** Cross-tenant query bypass global TenantId filter — cần `IgnoreQueryFilters()` + manual tenant scoping. Reference: `ResolveCustomerTenantAttribute` pattern (Bug 1 fix).

---

## WS-3: SITEMAP UPDATE (SAU WS-1 + WS-2)

### 3.1 NavMenu.razor — thêm menu links
**File:** `5_WebApps/ShopERP/Components/Layout/NavMenu.razor`

Thêm vào section SystemAdmin (line 162-207):

```razor
@* CRM — Customer management + promo push *@
<div class="nav-item px-3">
    <NavLink class="nav-link" href="admin/customers">
        <span class="bi bi-people-fill-nav-menu" aria-hidden="true"></span> Khách hàng (CRM)
    </NavLink>
</div>
<div class="nav-item px-3">
    <NavLink class="nav-link" href="admin/customers-global">
        <span class="bi bi-globe-nav-menu" aria-hidden="true"></span> Khách hàng (Cross-tenant)
    </NavLink>
</div>
<div class="nav-item px-3">
    <NavLink class="nav-link" href="admin/promo-campaigns">
        <span class="bi bi-megaphone-fill-nav-menu" aria-hidden="true"></span> Chiến dịch khuyến mãi
    </NavLink>
</div>
```

Thêm vào section Owner (line 68-107) — per-tenant CRM:
```razor
<div class="nav-item px-3">
    <NavLink class="nav-link" href="admin/customers">
        <span class="bi bi-people-fill-nav-menu" aria-hidden="true"></span> Khách hàng (CRM)
    </NavLink>
</div>
<div class="nav-item px-3">
    <NavLink class="nav-link" href="admin/promo-campaigns">
        <span class="bi bi-megaphone-fill-nav-menu" aria-hidden="true"></span> Chiến dịch khuyến mãi
    </NavLink>
</div>
```

### 3.2 Sitemap.razor — thêm card mới
**File:** `5_WebApps/ShopERP/Components/Pages/Sitemap.razor`

Thêm 2 card mới:

**Card 1 — CRM & Khách hàng (Owner + SystemAdmin):**
```razor
<VanACard Header="👥 CRM & Khách Hàng" Hoverable="true" data-testid="card-crm">
    <div class="sitemap-links">
        <a href="/admin/customers" class="sitemap-link" data-testid="link-customers">
            <span class="sitemap-link-icon">👥</span>
            <span>Danh sách khách hàng + Lọc segment</span>
        </a>
        <a href="/admin/promo-campaigns" class="sitemap-link" data-testid="link-promo-campaigns">
            <span class="sitemap-link-icon">📢</span>
            <span>Chiến dịch khuyến mãi (Push)</span>
        </a>
    </div>
</VanACard>
```

**Card 2 — Cross-tenant (SystemAdmin only):**
```razor
<AuthorizeView Roles="SystemAdmin">
    <Authorized>
        <VanACard Header="🌍 Cross-Tenant" Hoverable="true" data-testid="card-cross-tenant">
            <div class="sitemap-links">
                <a href="/admin/customers-global" class="sitemap-link" data-testid="link-customers-global">
                    <span class="sitemap-link-icon">🌍</span>
                    <span>Tất cả khách hàng (Cross-tenant)</span>
                </a>
            </div>
        </VanACard>
    </Authorized>
</AuthorizeView>
```

---

## THỨ TỰ THỰC HIỆN

| Session | Workstream | Tasks | Output |
|---------|-----------|-------|--------|
| S1 | WS-1.1 | Notification wire-up (5 items) + 2 new PushNotificationService methods + tests | Toggle hoạt động thật |
| S2 | WS-1.2 + WS-1.3 | Missions.razor history section + social share URL validation + tests | Phase C 18/18 COMPLETE |
| S3 | WS-2.1 + WS-2.2 | Domain (PromoCampaign entity + extend CustomerSegmentCriteria) + Service (PromoCampaignService + PromoCampaignJob) + migration | Backend ready |
| S4 | WS-2.3 | Controllers (CustomerController + PromoCampaignController) + Gateway forward | API ready |
| S5 | WS-2.4 | UI (CustomerList.razor + PromoPushComposer modal + PromoCampaignList.razor + CustomerListGlobal.razor) | Frontend ready |
| S6 | WS-3 | Sitemap + NavMenu update + build + RV | Menu links live |
| S7 | Full | Build + test suite + guard-check + VPS RV | Deploy |

---

## RISKS & MITIGATIONS

| Risk | Mitigation |
|------|------------|
| Cross-tenant query (CustomerListGlobal) bypass TenantId filter | Use `IgnoreQueryFilters()` + manual tenant scoping — reference `ResolveCustomerTenantAttribute` pattern |
| PromoCampaignJob rate limit — push provider ban | 100ms delay between sends + batch 50 recipients per cycle |
| Bulk push spam — owner gửi 1000 thông báo cùng lúc | TotalRecipients count hiển thị trước confirm + rate limit trong job |
| CustomerSegmentCriteria JOIN LoyaltyRewards — performance | Index on LoyaltyRewards.CustomerId (đã có) — verify |
| PromoCampaignRecipient table grow large | Auto-purge recipients > 90 days after campaign completed (future tech debt) |

---

## DOMAIN MODIFICATION APPROVAL REQUIRED

| Modification | Type | Justification |
|-------------|------|---------------|
| Extend `CustomerSegmentCriteria` record (4 new fields) | Modify existing record | Add filter dimensions for CRM — additive, no breaking change |
| New `PromoCampaign` entity | Add new entity | Marketing automation feature — approved by user request |
| New `PromoCampaignRecipient` entity | Add new entity | Tracking per-recipient delivery status — required for Queue+Job pattern |

**Per governance:** Domain modifications require user approval. **Awaiting approval before WS-2.1.**

---

## FILES SUMMARY

### WS-1 (Phase C gaps):
- MODIFY: `3_CoreHub/Services/PushNotificationService.cs` (+2 methods)
- MODIFY: `3_CoreHub/Services/RedemptionService.cs` (notification wire-up)
- MODIFY: `3_CoreHub/Services/MissionService.cs` (notification wire-up)
- MODIFY: `5_WebApps/ShopERP/Services/BirthdayBonusJob.cs` (toggle check)
- MODIFY: `5_WebApps/ShopERP/Services/VoucherExpiryReminderJob.cs` (toggle check)
- MODIFY: `5_WebApps/KhachLink/Pages/Missions.razor` (history section)
- MODIFY: `5_WebApps/ShopERP/Controllers/CustomerProfileController.cs` (URL validation)
- MODIFY: `6_Tests/` (unit tests cho toggle + URL validation)

### WS-2 (Customer + Promo):
- MODIFY: `1_Shared/Domain.cs` (+PromoCampaign + PromoCampaignRecipient entities)
- MODIFY: `3_CoreHub/Domain/Repositories/ICustomerRepository.cs` (extend CustomerSegmentCriteria)
- MODIFY: `3_CoreHub/Infrastructure/Repositories/CustomerRepository.cs` (4 new filter fields)
- NEW: `3_CoreHub/Infrastructure/Configurations/PromoCampaignConfiguration.cs`
- NEW: `3_CoreHub/Infrastructure/Configurations/PromoCampaignRecipientConfiguration.cs`
- NEW: `1_Shared/Services/IPromoCampaignService.cs`
- NEW: `3_CoreHub/Services/PromoCampaignService.cs`
- NEW: `5_WebApps/ShopERP/Services/PromoCampaignJob.cs`
- NEW: `5_WebApps/ShopERP/Controllers/CustomerController.cs`
- NEW: `5_WebApps/ShopERP/Controllers/PromoCampaignController.cs`
- NEW: `5_WebApps/ShopERP/Components/Pages/Admin/CustomerList.razor`
- NEW: `5_WebApps/ShopERP/Components/Pages/Admin/CustomerListGlobal.razor`
- NEW: `5_WebApps/ShopERP/Components/Pages/Admin/PromoPushComposer.razor`
- NEW: `5_WebApps/ShopERP/Components/Pages/Admin/PromoCampaignList.razor`
- NEW: `5_WebApps/ShopERP/Migrations/20260727_AddPromoCampaign.cs`
- MODIFY: `5_WebApps/ShopERP/Program.cs` (register PromoCampaignJob + services)
- MODIFY: `3_CoreHub/Services/PushNotificationService.cs` (+SendPromoNotificationAsync)

### WS-3 (Sitemap):
- MODIFY: `5_WebApps/ShopERP/Components/Layout/NavMenu.razor`
- MODIFY: `5_WebApps/ShopERP/Components/Pages/Sitemap.razor`

**Total: ~25 files (10 new, 15 modify)**
