# Master Plan: Loyalty Phase C + CRM + Promo Push — Audit Fix

Kế hoạch triển khai fix các deviation từ self-audit commit `f15d03f5` (feat(loyalty+crm): complete Loyalty Phase C gaps + customer segmentation + promo push campaigns). 4 phase (P0-P3) theo thứ tự ưu tiên: architectural/security → acceptance criteria → UX → cosmetic.

> **Audit source:** Self-audit report 2026-07-27 (8-phase protocol) — 32 requirements audited, 3 critical blockers + 5 partial implementations + 1 auth concern.
> **Original task card:** `docs/AI/tasks/plan_loyalty_completion_crm_promo_push.md`
> **Audited commit:** `f15d03f5` (32 files, 5885 insertions)
> **Build baseline:** 0 errors, 605 warnings (pre-existing CA)

---

## 0. EXECUTION RULES

### Session protocol
1. **TDD bắt buộc:** Test viết TRƯỚC code cho P1.A (cross-tenant query) + P1.B (15 missing tests). Stub/TODO cấm tính DONE.
2. **guard-check.ps1** chạy TRƯỚC khi ghi DONE mỗi phase.
3. **dotnet build VanAn.sln** 0 errors là gate bắt buộc mỗi phase.
4. **Phase isolation:** P0 xong → build pass → mới sang P1. P1 xong → tests pass → mới sang P2. Không skip phase.
5. **No scope creep:** Chỉ fix các deviation đã list. Không thêm feature mới, không redesign architecture.

### Branch protocol
```
main
  └─ fix/loyalty-crm-audit-fix  (single branch, 4 phases commit sequential)
```
- 1 branch cho toàn bộ 4 phase, commit per phase, merge vào main sau khi P3 + VPS RV pass
- Không merge nếu bất kỳ phase nào fail build hoặc test

### Hard rules (non-negotiable)
- **Domain layer PURE:** Không sửa `1_Shared/Domain.cs` trong audit fix (entities đã đúng per audit)
- **AccountingEntry immutable:** Không touch
- **Tenant isolation:** `IgnoreQueryFilters()` CHỈ dùng cho `GetAllCustomersAcrossTenantsAsync` (SystemAdmin-only endpoint). Owner KHÔNG được truy cập endpoint này.
- **UI Platform components only:** Không custom HTML/CSS cho UI mới
- **TDD:** P1.A + P1.B tests viết trước code
- **No new policy:** Dùng `OwnerOnly` có sẵn (`Program.cs:513` — `RequireRole(Owner, SystemAdmin)`). KHÔNG tạo policy mới `OwnerOrSystemAdmin`.
- **No layer bypass:** `IPromoCampaignService` phải ở `1_Shared/Services/` (contract layer), không ở `3_CoreHub/Services/`
- **Single-Identity Pattern:** Mọi entity dùng `BaseEntity.Id` trực tiếp (không business key VO) — đã đúng, không thay đổi
- **.NET 8.0.x:** Không upgrade SDK

---

## 1. PHASE 0 (P0) — Architectural & Security Fixes (BẮT BUỘC TRƯỚC)

**Estimated sessions:** 1
**Conflict risk:** LOW (chỉ thay attribute + move file)
**Goal:** Sửa 2 deviation có thể ảnh hưởng kiến trúc/bảo mật trước khi làm tiếp.

### Tasks
| # | Task ID | Task | Files | Type |
|---|---|---|---|---|
| 1 | AF-P0-T1 | Auth fix — Promo + Customer controllers | `5_WebApps/ShopERP/Controllers/CustomerController.cs`, `5_WebApps/ShopERP/Controllers/PromoCampaignController.cs` | `[Authorize]` → `[Authorize(Policy = "OwnerOnly")]` |
| 2 | AF-P0-T2 | Move `IPromoCampaignService` to correct layer | `3_CoreHub/Services/IPromoCampaignService.cs` → `1_Shared/Services/IPromoCampaignService.cs` | File move + namespace update + using references |

### P0-T1: Auth fix chi tiết
**Vấn đề:** `CustomerController` + `PromoCampaignController` dùng `[Authorize]` trơn → bất kỳ user authenticated nào (incl. Staff, StoreKeeper, Guard) có thể gọi Promo API (tạo/hủy campaign, list customers).
**Fix:**
```csharp
// CustomerController.cs line 15
[Authorize]  →  [Authorize(Policy = "OwnerOnly")]

// PromoCampaignController.cs line 15
[Authorize]  →  [Authorize(Policy = "OwnerOnly")]
```
**Policy `OwnerOnly` đã tồn tại** trong `5_WebApps/ShopERP/Program.cs:513`:
```csharp
.AddPolicy("OwnerOnly", policy => policy.RequireRole(UserRole.Owner.ToString(), "SystemAdmin"))
```
**Không tạo policy mới.** `CustomerListGlobal.razor` đã dùng `[Authorize(Policy = "SystemAdmin")]` — đã đúng, không cần sửa.

### P0-T2: Layer move chi tiết
**Vấn đề:** `IPromoCampaignService.cs` ở `3_CoreHub/Services/` — sai layer. Convention: contract ở `1_Shared/Services/` (xem `IMissionService.cs`, `IRedemptionService.cs` đã ở đúng layer).
**Fix:**
1. Move file `3_CoreHub/Services/IPromoCampaignService.cs` → `1_Shared/Services/IPromoCampaignService.cs`
2. Update namespace: `VanAn.CoreHub.Services` → `VanAn.Shared.Services`
3. Update `using` references trong:
   - `3_CoreHub/Services/PromoCampaignService.cs`
   - `5_WebApps/ShopERP/Controllers/PromoCampaignController.cs`
   - `5_WebApps/ShopERP/Components/Pages/Admin/CustomerList.razor`
   - `5_WebApps/ShopERP/Components/Pages/Admin/PromoCampaignList.razor`
   - `5_WebApps/ShopERP/Components/Pages/Admin/CustomerListGlobal.razor`
   - `5_WebApps/ShopERP/Program.cs` (line 365)
4. Build pass → verify no other references missed

### Entry criteria
- [ ] `dotnet build VanAn.sln` pass trên main (baseline 0 errors)
- [ ] `guard-check.ps1` pass
- [ ] Self-audit report đã được user review + approve

### Exit criteria — ALL PASSED
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` ALL CHECKS PASSED
- [ ] `CustomerController` + `PromoCampaignController` có `[Authorize(Policy = "OwnerOnly")]`
- [ ] `IPromoCampaignService.cs` tồn tại tại `1_Shared/Services/IPromoCampaignService.cs`
- [ ] Không còn reference nào đến `VanAn.CoreHub.Services.IPromoCampaignService`
- [ ] **Smoke test:** Staff account gọi `POST /api/promo-campaigns` → 403 Forbidden (was 200)

---

## 2. PHASE 1 (P1) — Hoàn thành Acceptance Criteria (TDD)

**Estimated sessions:** 3 (P1.A + P1.B + P1.C)
**Conflict risk:** MEDIUM (P1.A thay đổi repository + rewrite UI)
**Goal:** Sửa 3 critical blockers + 1 partial implementation để đạt acceptance criteria của task card gốc.

### Tasks
| # | Task ID | Task | Files | Type |
|---|---|---|---|---|
| 1 | AF-P1-T1 | CustomerListGlobal full-stack (cross-tenant CUSTOMER list) | Repo + Service + Controller + Blazor | Rewrite (D1/D2 fix) |
| 2 | AF-P1-T2 | 15 missing unit tests + P1.A tests | `6_Tests/` | TDD (D3 fix) |
| 3 | AF-P1-T3 | Missions pagination | API + Controller + UI | Partial fix (D4) |

### P1-T1: CustomerListGlobal full-stack (TDD)

**Vấn đề:** `CustomerListGlobal.razor` hiện show cross-tenant CAMPAIGNS (với "coming soon" note cho customer list). Task card yêu cầu cross-tenant CUSTOMER list với Tenant column + filter bar.

**Thứ tự TDD (bắt buộc):**

#### Step 1: Test TRƯỚC (failing test)
**File:** `6_Tests/VanAn.Core.Tests/Repositories/CustomerRepositoryCrossTenantTests.cs` (NEW)
```csharp
[Fact]
public async Task GetAllCustomersAcrossTenantsAsync_ReturnsCustomersFromMultipleTenants()
{
    // Seed 2 tenants với customers
    // Act: gọi GetAllCustomersAcrossTenantsAsync (chưa implement → fail)
    // Assert: returns customers từ cả 2 tenants, không filter theo TenantId
}

[Fact]
public async Task GetAllCustomersAcrossTenantsAsync_BypassesGlobalTenantFilter()
{
    // Verify IgnoreQueryFilters được apply
    // Assert: returns customers kể cả khi ITenantProvider.TenantId = Guid.Empty
}
```

**File:** `6_Tests/VanAn.Integration.Tests/PromoCampaignControllerAuthTests.cs` (NEW)
```csharp
[Fact]
public async Task GetAllCustomersGlobal_StaffRole_Returns403()
{
    // Login as Staff → GET /api/customers/global → 403
}

[Fact]
public async Task GetAllCustomersGlobal_SystemAdmin_Returns200()
{
    // Login as SystemAdmin → GET /api/customers/global → 200 + customers từ multiple tenants
}
```

#### Step 2: Repository
**File:** `3_CoreHub/Domain/Repositories/ICustomerRepository.cs` (MODIFY)
```csharp
/// <summary>
/// AF-P1: Get ALL active customers across ALL tenants (SystemAdmin only).
/// Bypasses global TenantId query filter via IgnoreQueryFilters().
/// DO NOT expose to Owner role — only SystemAdmin endpoint.
/// </summary>
Task<IReadOnlyList<Customer>> GetAllCustomersAcrossTenantsAsync();
```

**File:** `3_CoreHub/Infrastructure/Repositories/CustomerRepository.cs` (MODIFY)
```csharp
public async Task<IReadOnlyList<Customer>> GetAllCustomersAcrossTenantsAsync()
{
    return await _context.Customers
        .IgnoreQueryFilters()  // Bypass global TenantId filter — SystemAdmin only
        .Where(c => !c.IsDeleted && c.IsActive)
        .OrderBy(c => c.TenantId)  // Group by tenant for UI
        .ThenBy(c => c.FullName)
        .ToListAsync();
}
```

#### Step 3: Controller
**File:** `5_WebApps/ShopERP/Controllers/CustomerController.cs` (MODIFY)
```csharp
[HttpGet("global")]
[Authorize(Policy = "SystemAdmin")]  // SystemAdmin only — NOT OwnerOnly
public async Task<IActionResult> ListGlobal([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
{
    var all = await _customerRepository.GetAllCustomersAcrossTenantsAsync();
    // Paginate + enrich with TenantId + loyalty points
    // Return DTO with TenantId field
}
```

#### Step 4: Blazor rewrite
**File:** `5_WebApps/ShopERP/Components/Pages/Admin/CustomerListGlobal.razor` (REWRITE)
- **Xóa:** Campaign overview table + "coming soon" note (lines 27-75 hiện tại)
- **Thêm:**
  - Filter bar (giống CustomerList nhưng không có tenant filter — vì SystemAdmin thấy tất cả)
    - Points range (min/max)
    - Last order within N days
    - Birthday month
    - Total spent range (min/max VND)
  - Results table: **Tenant** | Name | Phone | Tier | Points | Total Spent | Last Order | Birthday | Identity Level | Push Subscribed
  - Pagination 20/page
  - Empty state: "Không có khách hàng nào thỏa bộ lọc."
- **Giữ:** `[Authorize(Policy = "SystemAdmin")]` (đã đúng), route `/admin/customers-global`

### P1-T2: 15 missing unit tests + P1.A tests

**Vấn đề:** Task card yêu cầu 15 tests (5 toggle + 10 URL validation) nhưng KHÔNG có test nào được viết. Governance yêu cầu TDD.

#### 5 Toggle tests (WS-1.1)
**File:** `6_Tests/VanAn.Core.Tests/Services/NotificationToggleTests.cs` (NEW)

| # | Test | Toggle | Expected behavior |
|---|---|---|---|
| 1 | `Notify_RedemptionFulfilled_On_SendsPush` | true | `SendRedemptionFulfilledNotificationAsync` called |
| 2 | `Notify_RedemptionFulfilled_Off_SkipsPush` | false | `SendRedemptionFulfilledNotificationAsync` NOT called, fulfillment still succeeds |
| 3 | `Notify_MissionCompleted_On_SendsPush` | true | `SendLoyaltyPointsChangedNotificationAsync` called with mission reason |
| 4 | `Notify_BirthdayBonus_Off_StillAwardsPoints` | false | Points awarded, `SendBirthdayNotificationAsync` NOT called |
| 5 | `Notify_VoucherExpiringSoon_Off_SkipsPush` | false | `SendVoucherExpiryReminderAsync` NOT called, job still queries + logs |

#### 10 URL validation tests (WS-1.3)
**File:** `6_Tests/VanAn.Integration.Tests/CustomerProfileShareUrlValidationTests.cs` (NEW)

| # | Platform | URL | Expected |
|---|---|---|---|
| 1 | Facebook | `facebook.com/user/posts/123` | 200 OK (mission triggered) |
| 2 | Facebook | `facebook.com/permalink.php?story_id=123` | 200 OK |
| 3 | Facebook | `facebook.com` (homepage) | 400 BadRequest |
| 4 | Facebook | `facebook.com/user` (profile) | 400 BadRequest |
| 5 | Facebook | empty string | 400 BadRequest |
| 6 | TikTok | `tiktok.com/@user/video/123` | 200 OK |
| 7 | TikTok | `tiktok.com/user/video/123` | 200 OK |
| 8 | TikTok | `tiktok.com` (homepage) | 400 BadRequest |
| 9 | TikTok | `tiktok.com/@user` (profile) | 400 BadRequest |
| 10 | TikTok | empty string | 400 BadRequest |

#### P1.A tests (cross-tenant isolation)
- `GetAllCustomersAcrossTenantsAsync_ReturnsCustomersFromMultipleTenants`
- `GetAllCustomersAcrossTenantsAsync_BypassesGlobalTenantFilter`
- `GetAllCustomersGlobal_StaffRole_Returns403`
- `GetAllCustomersGlobal_SystemAdmin_Returns200`

**Total P1-T2: 19 tests** (15 missing + 4 P1.A)

### P1-T3: Missions pagination

**Vấn đề:** `Missions.razor` history section load ALL completions trong 1 call. Task card yêu cầu 20/page + "Xem thêm" button.

#### API
**File:** `5_WebApps/ShopERP/Controllers/MissionsController.cs` (MODIFY)
```csharp
[HttpGet("my/completions")]
public async Task<IActionResult> GetMyCompletions(
    [FromHeader(Name = "X-Customer-Token")] string? token,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
{
    // Paginate completions
    // Return { items, total, page, pageSize, hasMore }
}
```

#### Service
**File:** `3_CoreHub/Services/MissionService.cs` (MODIFY)
- Add `GetCustomerCompletionsPagedAsync(Guid customerId, int page, int pageSize)`

#### UI
**File:** `5_WebApps/KhachLink/Pages/Missions.razor` (MODIFY)
- Load 20 items ban đầu
- "Xem thêm" button nếu `hasMore = true` → load page 2, append to list
- State: `_completionsPage`, `_completionsHasMore`

### Entry criteria (P1)
- [ ] P0 exit criteria ALL PASSED
- [ ] Build pass trên branch `fix/loyalty-crm-audit-fix`

### Exit criteria (P1) — ALL PASSED
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` ALL CHECKS PASSED
- [ ] **P1-T1:** `GetAllCustomersAcrossTenantsAsync` tồn tại + returns multi-tenant customers + `IgnoreQueryFilters` verified
- [ ] **P1-T1:** `CustomerListGlobal.razor` render customer table (NOT campaigns) + Tenant column + filter bar
- [ ] **P1-T1:** Staff role gọi `/api/customers/global` → 403; SystemAdmin → 200
- [ ] **P1-T2:** 19 tests PASS (5 toggle + 10 URL + 4 cross-tenant)
- [ ] **P1-T3:** `GET /api/missions/my/completions?page=2&pageSize=20` returns paginated + `hasMore`
- [ ] **P1-T3:** Missions.razor "Xem thêm" button works (loads page 2, appends)
- [ ] **VPS RV:** Deploy → SystemAdmin login → `/admin/customers-global` shows customers from multiple tenants

---

## 3. PHASE 2 (P2) — UX Completions

**Estimated sessions:** 1-2
**Conflict risk:** LOW (chỉ thêm UI elements)
**Goal:** Hoàn thành các UX requirement của task card mà Devin đã skip.

### Tasks
| # | Task ID | Task | Files | Type |
|---|---|---|---|---|
| 1 | AF-P2-T1 | CustomerList: per-row "Gửi khuyến mãi" button | `CustomerList.razor` | Add row action |
| 2 | AF-P2-T2 | CustomerList: checkbox bulk select + "Gửi khuyến mãi cho N khách" | `CustomerList.razor` | Add bulk action |
| 3 | AF-P2-T3 | PromoCampaignList: progress bar (SentCount/TotalRecipients) for Processing | `PromoCampaignList.razor` | Add visual |
| 4 | AF-P2-T4 | PromoCampaignList: "Chi tiết" expand/collapse recipient list | `PromoCampaignList.razor` | Add expand |
| 5 | AF-P2-T5 | CustomerList: add "Push Subscribed" column | `CustomerList.razor` + `CustomerController.cs` | Add column |

### P2-T1: Per-row "Gửi khuyến mãi" button
- Mỗi row trong results table có button "Gửi" → mở promo modal với segment = single customer ID
- Cần extend `CustomerSegmentCriteria` hoặc tạo new param `SelectedCustomerIds` trong `CreateCampaignRequest`
- PromoCampaignService.CreateCampaignAsync cần hỗ trợ recipient list từ selected IDs (không chỉ segment criteria)

### P2-T2: Bulk select
- Checkbox column trong table
- "Select all" checkbox ở header
- Button "Gửi khuyến mãi cho N khách đã chọn" (N = số checkbox checked)
- Modal pre-filled với selected customer IDs

### P2-T3: Progress bar
- Trong PromoCampaignList, row có Status=Processing → render progress bar
- `width = (SentCount / TotalRecipients) * 100%`
- Auto-refresh mỗi 5s khi có campaign Processing (JS interop hoặc SignalR)

### P2-T4: Detail expand
- "Chi tiết" button per row → expand/collapse inline
- Load recipients via `GET /api/promo-campaigns/{id}/recipients?page=1`
- Show: CustomerName | Status | SentAt | ErrorMessage

### P2-T5: Push Subscribed column
- `CustomerController.MapCustomerDto` thêm `HasPushSubscription` field
- Query: check if customer has active PushSubscription
- UI: icon ✓/✗ trong column

### Entry criteria (P2)
- [ ] P1 exit criteria ALL PASSED
- [ ] 19 tests PASS

### Exit criteria (P2) — ALL PASSED
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` ALL CHECKS PASSED
- [ ] Per-row "Gửi khuyến mãi" button works (creates campaign with 1 recipient)
- [ ] Bulk select + "Gửi cho N khách" works (creates campaign with N recipients)
- [ ] Progress bar renders for Processing campaigns
- [ ] "Chi tiết" expand shows recipient list
- [ ] "Push Subscribed" column shows correct status
- [ ] **VPS RV:** Owner login → `/admin/customers` → select 3 customers → "Gửi khuyến mãi" → campaign created with 3 recipients

---

## 4. PHASE 3 (P3) — Cosmetic

**Estimated sessions:** 1
**Conflict risk:** VERY LOW (file structure only)
**Goal:** Tuân thủ file structure spec của task card (functionally equivalent, cosmetic only).

### Tasks
| # | Task ID | Task | Files | Type |
|---|---|---|---|---|
| 1 | AF-P3-T1 | Tách `PromoPushComposer.razor` thành file riêng | `5_WebApps/ShopERP/Components/Pages/Admin/PromoPushComposer.razor` (NEW) | Extract component |
| 2 | AF-P3-T2 | Tách `PromoCampaignRecipientConfiguration.cs` thành file riêng | `3_CoreHub/Infrastructure/Configurations/PromoCampaignRecipientConfiguration.cs` (NEW) | Extract class |
| 3 | AF-P3-T3 | `POST /api/customers/export` CSV (optional) | `CustomerController.cs` | Add endpoint |

### P3-T1: Extract PromoPushComposer
- Move modal markup từ `CustomerList.razor:133-178` vào `PromoPushComposer.razor` (NEW)
- `CustomerList.razor` reference component: `<PromoPushComposer Show="@_showPromoModal" OnClose="ClosePromoModal" ... />`
- Parameters: `Show`, `RecipientCount`, `OnSubmit`, `OnClose`

### P3-T2: Extract PromoCampaignRecipientConfiguration
- Move class `PromoCampaignRecipientConfiguration` từ `PromoCampaignConfiguration.cs:48-73` vào file riêng
- Same namespace, same logic

### P3-T3: CSV Export (optional per task card)
- `POST /api/customers/export` with segment criteria
- Returns CSV: Name, Phone, Tier, Points, TotalSpent, LastOrder, Birthday, IdentityLevel
- `Content-Type: text/csv`, `Content-Disposition: attachment; filename=customers.csv`

### Entry criteria (P3)
- [ ] P2 exit criteria ALL PASSED

### Exit criteria (P3) — ALL PASSED
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` ALL CHECKS PASSED
- [ ] `PromoPushComposer.razor` tồn tại như file riêng
- [ ] `PromoCampaignRecipientConfiguration.cs` tồn tại như file riêng
- [ ] CSV export endpoint returns valid CSV (if implemented)
- [ ] **Final VPS RV:** Deploy → all 4 phases verified on VPS

---

## 5. VPS RUNTIME VERIFICATION (Final — sau P3)

| # | Test | Command | Expected | Phase verified |
|---|---|---|---|---|
| RV-AF-1 | Build pass | `dotnet build VanAn.sln` | 0 errors | All |
| RV-AF-2 | guard-check | `./guard-check.ps1` | ALL PASSED | All |
| RV-AF-3 | Staff blocked from Promo API | Login as Staff → `POST /api/promo-campaigns` | 403 Forbidden | P0-T1 |
| RV-AF-4 | Owner can create campaign | Login as Owner → `POST /api/promo-campaigns` | 200 OK | P0-T1 |
| RV-AF-5 | IPromoCampaignService in 1_Shared | `Get-ChildItem 1_Shared/Services/IPromoCampaignService.cs` | File exists | P0-T2 |
| RV-AF-6 | Cross-tenant customer list | SystemAdmin → `/admin/customers-global` | Shows customers from multiple tenants | P1-T1 |
| RV-AF-7 | Staff blocked from global | Staff → `/admin/customers-global` | 403 | P1-T1 |
| RV-AF-8 | 19 unit tests pass | `dotnet test --filter "Category=AuditFix"` | 19/19 PASS | P1-T2 |
| RV-AF-9 | Missions pagination | `GET /api/missions/my/completions?page=2&pageSize=20` | 200 + hasMore | P1-T3 |
| RV-AF-10 | Per-row promo send | Owner → select 1 customer → "Gửi" | Campaign with 1 recipient | P2-T1 |
| RV-AF-11 | Bulk promo send | Owner → select 3 → "Gửi cho 3 khách" | Campaign with 3 recipients | P2-T2 |
| RV-AF-12 | Progress bar | Create campaign → `/admin/promo-campaigns` while Processing | Progress bar visible | P2-T3 |
| RV-AF-13 | Detail expand | Click "Chi tiết" on campaign row | Recipient list shown | P2-T4 |
| RV-AF-14 | Push Subscribed column | `/admin/customers` table | Column present with ✓/✗ | P2-T5 |
| RV-AF-15 | PromoPushComposer separate file | `Get-ChildItem PromoPushComposer.razor` | File exists | P3-T1 |
| RV-AF-16 | Recipient config separate file | `Get-ChildItem PromoCampaignRecipientConfiguration.cs` | File exists | P3-T2 |

---

## 6. DEVIATION TRACKING (from self-audit)

| ID | Deviation | Phase fix | Status |
|---|---|---|---|
| D1 | CustomerListGlobal shows campaigns not customers | P1-T1 | Pending |
| D2 | "Coming soon" note in CustomerListGlobal | P1-T1 | Pending |
| D3 | 15 missing unit tests | P1-T2 | Pending |
| D4 | Missions history no pagination | P1-T3 | Pending |
| D5 | PromoPushComposer inlined | P3-T1 | Pending |
| D6 | IPromoCampaignService wrong layer | P0-T2 | Pending |
| D7 | PromoCampaignRecipientConfiguration combined file | P3-T2 | Pending |
| D8 | SendPromoNotificationAsync extra title param | — | Accepted (additive, no fix needed) |
| D9 | No per-row/bulk promo send | P2-T1, P2-T2 | Pending |
| D10 | Missing Push Subscribed column | P2-T5 | Pending |
| D11 | No campaign detail expand + progress bar | P2-T3, P2-T4 | Pending |
| D12 | Auth policy not applied | P0-T1 | Pending |
| D13 | CSV export not implemented | P3-T3 | Pending (optional) |

---

## 7. RISKS & MITIGATIONS

| Risk | Mitigation |
|---|---|
| `IgnoreQueryFilters()` bypass leaks tenant data to Owner | Endpoint `[Authorize(Policy = "SystemAdmin")]` — Owner gets 403. Test RV-AF-7 verifies. |
| Moving `IPromoCampaignService` breaks references | Grep all `using VanAn.CoreHub.Services` references before move. Build gate after P0-T2. |
| Per-row promo send needs new API contract (selected IDs not segment) | Extend `CreateCampaignRequest` with optional `SelectedCustomerIds: List<Guid>`. If present, use IDs directly; if absent, use segment criteria. |
| Progress bar auto-refresh causes perf issue | Poll every 5s ONLY when campaign status = Processing. Stop polling when Completed/Failed. |
| 19 tests take long to write | Split: P1-T2 done in 1 session. Toggle tests use mock IShopFeatureSettingsService. URL tests use WebApplicationFactory. |
| Bulk select with 1000+ customers | Rate limit already in PromoCampaignJob (100ms delay, batch 50). UI shows confirm dialog with N count before create. |

---

## 8. FILES SUMMARY

### P0 (2 files):
- MODIFY: `5_WebApps/ShopERP/Controllers/CustomerController.cs` (auth attribute)
- MODIFY: `5_WebApps/ShopERP/Controllers/PromoCampaignController.cs` (auth attribute)
- MOVE: `3_CoreHub/Services/IPromoCampaignService.cs` → `1_Shared/Services/IPromoCampaignService.cs`
- MODIFY: 6 files (using references update)

### P1 (8 files):
- NEW: `6_Tests/VanAn.Core.Tests/Repositories/CustomerRepositoryCrossTenantTests.cs`
- NEW: `6_Tests/VanAn.Core.Tests/Services/NotificationToggleTests.cs`
- NEW: `6_Tests/VanAn.Integration.Tests/CustomerProfileShareUrlValidationTests.cs`
- NEW: `6_Tests/VanAn.Integration.Tests/PromoCampaignControllerAuthTests.cs`
- MODIFY: `3_CoreHub/Domain/Repositories/ICustomerRepository.cs` (+method)
- MODIFY: `3_CoreHub/Infrastructure/Repositories/CustomerRepository.cs` (+method)
- MODIFY: `5_WebApps/ShopERP/Controllers/CustomerController.cs` (+global endpoint)
- MODIFY: `5_WebApps/ShopERP/Controllers/MissionsController.cs` (pagination)
- MODIFY: `3_CoreHub/Services/MissionService.cs` (paged method)
- REWRITE: `5_WebApps/KhachLink/Pages/Missions.razor` (pagination UI)
- REWRITE: `5_WebApps/ShopERP/Components/Pages/Admin/CustomerListGlobal.razor` (customer list)

### P2 (3 files):
- MODIFY: `5_WebApps/ShopERP/Components/Pages/Admin/CustomerList.razor` (row action + bulk + column)
- MODIFY: `5_WebApps/ShopERP/Components/Pages/Admin/PromoCampaignList.razor` (progress + expand)
- MODIFY: `5_WebApps/ShopERP/Controllers/CustomerController.cs` (HasPushSubscription field)

### P3 (3 files):
- NEW: `5_WebApps/ShopERP/Components/Pages/Admin/PromoPushComposer.razor`
- NEW: `3_CoreHub/Infrastructure/Configurations/PromoCampaignRecipientConfiguration.cs`
- MODIFY: `5_WebApps/ShopERP/Components/Pages/Admin/CustomerList.razor` (use extracted component)
- MODIFY: `3_CoreHub/Infrastructure/Configurations/PromoCampaignConfiguration.cs` (remove extracted class)
- MODIFY: `5_WebApps/ShopERP/Controllers/CustomerController.cs` (CSV export — optional)

**Total: ~20 files (5 new, 15 modify/rewrite/move)**

---

## 9. THỨ TỰ THỰC HIỆN

| Session | Phase | Tasks | Output | Gate |
|---|---|---|---|---|
| S1 | P0 | T1 (auth) + T2 (layer move) | Security + architecture fixed | Build pass + smoke test 403 |
| S2 | P1-T1 | Test → Repo → Service → Controller → Blazor | Cross-tenant customer list working | Tests pass + RV-AF-6,7 |
| S3 | P1-T2 | 19 tests (5 toggle + 10 URL + 4 cross-tenant) | TDD violation fixed | 19/19 tests pass |
| S4 | P1-T3 | API + Controller + UI pagination | Missions history paginated | RV-AF-9 |
| S5 | P2 | T1-T5 (row action, bulk, progress, expand, column) | UX complete | RV-AF-10 to 14 |
| S6 | P3 | T1-T3 (file extract + CSV) | Cosmetic done | RV-AF-15, 16 |
| S7 | Final | Build + guard-check + VPS RV + merge | Deploy | All 16 RV pass |

**Total: ~7 sessions**

---

## 10. DOMAIN MODIFICATION APPROVAL

| Modification | Type | Justification | Phase |
|---|---|---|---|
| None | — | Audit fix KHÔNG sửa Domain layer (entities đã đúng per audit) | — |

**Per governance:** Domain modifications require user approval. **Audit fix không yêu cầu domain change** — tất cả entities (`PromoCampaign`, `PromoCampaignRecipient`, `CustomerSegmentCriteria`) đã đúng theo audit.

---

**Plan status:** APPROVED 2026-07-27 (user confirmed via ask_user_question: OwnerOnly policy, all 15+ tests, CustomerListGlobal with filter bar)

**Next action:** Exit Ask mode → implement P0 (S1).
