# Task Card: Phase 6 — Admin UI (Tenant Mgmt + Shop Instances + FeaturedProduct + Home.razor Catalog)

> **Master plan:** `gateway_router_multi_vps_master_plan.md`
> **Workflow:** `newfeaturebuild.md`
> **Phase:** 6 of 8
> **Depends on:** Phase 1 (ShopInstance entity), Phase 2 (ShopInstances API on Gateway)
> **Unlocks:** SysAdmin can manage ShopInstances + assign tenants + manage FeaturedProducts + Home.razor shows curated catalog

---

## 1. Use Case & Business Design

**Problem:** SysAdmin has no UI to:
1. Manage `ShopInstance` rows (create/edit/disable/health-check).
2. See which ShopERP instance hosts which tenant.
3. Select a ShopInstance when creating/onboarding a new tenant.
4. **NEW (Round 2 decision):** Home.razor currently shows full product catalog by browsing — requires multi-VPS routing for product fetch. Decision: replace with curated view showing only (a) products customer previously purchased + (b) sysadmin-featured products. This needs a new `FeaturedProduct` entity in PG + admin UI to manage it.

Current `TenantManagement.razor` (`/admin/tenants`) has 6 columns: Name, Loại hình, Trạng thái, Email, Ngày tạo, Hành động. No "ShopERP URL" column.

Current `Home.razor` fetches products via `GET /shoperp/api/products?shopId={shopId}` forwarded to a fixed ShopERP — breaks in multi-VPS.

**Goal:**
1. New page `/admin/shop-instances` for ShopInstance CRUD + health check trigger.
2. Update `TenantManagement.razor`:
   - Add "ShopERP Instance" column to tenant list (shows Label + BaseUrl).
   - Add ShopInstance selector in Create modal + Onboarding modal.
   - Add "Chuyển ShopERP" action for migrating tenant to another instance (out of scope for Phase 6 — document as future work; just add disabled button for now).
3. **NEW: New entity `FeaturedProduct`** in Domain + PG table — sysadmin-curated products for Home.razor.
4. **NEW: New page `/admin/featured-products`** — sysadmin can add/remove products to featured list (by ProductId + TenantId, with display label + optional image URL).
5. **NEW: New Gateway API `GET /api/catalog/recommended?customerId={id}`** — returns union of (a) products customer previously purchased (from PG OrderItems) + (b) FeaturedProduct rows. Queries PG directly (no ShopERP forward).
6. **NEW: Refactor `Home.razor`** — replace catalog browse with `GET /api/catalog/recommended?customerId={id}`. If customer not logged in (anonymous) → show only FeaturedProduct list.

**Out of scope:** Gateway API (Phase 2), Gateway router (Phase 3), actual data migration between ShopERP instances (future task card).

---

## 1.5. FeaturedProduct Design (NEW — Round 2 decision)

### Domain entity

```csharp
// 1_Shared/Domain.cs — NEW value object + entity
public record FeaturedProductId(Guid Value)
{
    public static implicit operator Guid(FeaturedProductId id) => id.Value;
    public static implicit operator FeaturedProductId(Guid value) => new(value);
    public static FeaturedProductId FromGuid(Guid value) => new(value);
}

public class FeaturedProduct : BaseEntity
{
    public FeaturedProductId FeaturedProductId { get; protected set; } = new FeaturedProductId(Guid.NewGuid());
    public Guid ProductId { get; protected set; }      // Business reference to Product (in ShopERP SQLite, not PG FK)
    // TenantId inherited from BaseEntity (TenantId value object)
    public string DisplayName { get; protected set; } = string.Empty;   // Marketing name (may differ from Product.Name)
    public string? DisplayDescription { get; protected set; }  // Marketing description
    public string? ImageUrl { get; protected set; }     // Marketing image
    public decimal DisplayPrice { get; protected set; } // Display price (may differ from actual — show "from" price)
    public bool IsActive { get; protected set; } = true;
    public int SortOrder { get; protected set; }        // Display ordering
    public DateTime FeaturedAt { get; protected set; }  // When added to featured list

    protected FeaturedProduct() { }

    public FeaturedProduct(TenantId tenantId, Guid productId, string displayName, decimal displayPrice, string? displayDescription = null, string? imageUrl = null, int sortOrder = 0)
        : base(tenantId)
    {
        ProductId = productId;
        DisplayName = displayName;
        DisplayPrice = displayPrice;
        DisplayDescription = displayDescription;
        ImageUrl = imageUrl;
        SortOrder = sortOrder;
        FeaturedAt = DateTime.UtcNow;
    }

    public static FeaturedProduct Create(Guid id, TenantId tenantId, Guid productId, string displayName, decimal displayPrice, string? displayDescription = null, string? imageUrl = null, int sortOrder = 0)
    {
        var fp = new FeaturedProduct(tenantId, productId, displayName, displayPrice, displayDescription, imageUrl, sortOrder);
        // Single-Identity pattern: PK == business key
        fp.Id = id;
        fp.FeaturedProductId = new FeaturedProductId(id);
        return fp;
    }

    public void UpdateDisplayInfo(string displayName, decimal displayPrice, string? displayDescription, string? imageUrl, int sortOrder)
    {
        DisplayName = displayName;
        DisplayPrice = displayPrice;
        DisplayDescription = displayDescription;
        ImageUrl = imageUrl;
        SortOrder = sortOrder;
        UpdateAudit();
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdateAudit();
    }
}
```

### EF Configuration (PG only — `3_CoreHub/Infrastructure/`)

```csharp
// 3_CoreHub/Infrastructure/Configurations/FeaturedProductConfiguration.cs — NEW
public class FeaturedProductConfiguration : IEntityTypeConfiguration<FeaturedProduct>
{
    public void Configure(EntityTypeBuilder<FeaturedProduct> builder)
    {
        builder.ToTable("FeaturedProducts");
        builder.HasKey(e => e.Id);
        builder.Ignore(e => e.FeaturedProductId);      // Single-Identity: business key VO not mapped to column
        builder.Property(e => e.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.DisplayPrice).HasPrecision(18, 2);
        builder.Property(e => e.FeaturedAt).IsRequired();
        builder.HasIndex(e => new { e.ProductId, e.TenantId }).IsUnique();  // e.TenantId maps via TenantIdConverter from BaseEntity
    }
}
```

**Domain modification approval required:** adding `FeaturedProduct` + `FeaturedProductId` value object to `1_Shared/Domain.cs` is a Domain change.

### PG vs SQLite Product table semantics

| Table | Location | Purpose | Fields |
|---|---|---|---|
| `Products` (SQLite, ShopERP) | Per-tenant | **Operational** — full product data (price, VAT, recipe, COGS, stock) | All Product fields |
| `Products` (PG, Gateway) | Legacy | Currently synced from SQLite — **Phase 3 disables product sync to PG**. Table stays but not actively used. | All Product fields (stale) |
| `FeaturedProducts` (PG, Gateway) | NEW | **Marketing** — curated display info for Home.razor. No operational data. | DisplayName, DisplayPrice, ImageUrl, IsActive, SortOrder |

### Existing `CustomerRecommendationService` retirement

- `3_CoreHub/Services/CustomerRecommendationService.cs` currently queries `_dbContext.Products` in PG.
- After Phase 3 disables product sync, PG `Products` becomes stale → `CustomerRecommendationService` will return stale/wrong recommendations.
- **Decision:** Replace `CustomerRecommendationService` usage with new `CatalogController`.
- `ProductHttpService.GetRecommendedProductsAsync` (KhachLink) should be redirected to `GET /api/catalog/recommended` on Gateway (not `/shoperp/api/products/recommended`).
- Mark `CustomerRecommendationService` as `[Obsolete]` or delete after `CatalogController` is verified.

**Key distinction:** `FeaturedProducts` does NOT replace `Products` in PG. It's a separate marketing table. The actual ProductId + TenantId in FeaturedProduct reference the product in ShopERP SQLite (for when customer clicks "buy" → Scan.razor or checkout validates against ShopERP).

### Recommended catalog API

```
GET /api/catalog/recommended?customerId={id}&page=1&pageSize=20

Response:
{
  "products": [
    {
      "productId": "guid",
      "tenantId": "guid",
      "displayName": "Cà phê sữa đá",
      "displayPrice": 25000,
      "imageUrl": "...",
      "source": "Featured" | "History",
      "lastOrderedAt": "2026-07-10T..." // null for Featured
    }
  ],
  "totalCount": 15
}
```

**Logic:**
1. Query `FeaturedProducts` WHERE `IsActive = true` ORDER BY `SortOrder`.
2. If `customerId` provided and not empty: query `OrderItems` JOIN `Orders` WHERE `Order.CustomerId = {id}` AND `Order.PaymentStatus = 'Paid'` → DISTINCT `ProductId, TenantId` → get latest `OrderDate`.
3. Union both lists (Featured first, then History, deduplicate by ProductId+TenantId).
4. **No ShopERP HTTP call** — pure PG query. `DisplayPrice` is from `FeaturedProduct` (marketing price). Actual price validated at checkout (Phase 5 price validation).
5. **Anonymous users (no customerId):** return only Featured products.

### Home.razor refactor

- Remove existing product catalog browse (infinite scroll / tenant selector).
- Call `GET /api/catalog/recommended?customerId={id}` (customerId from localStorage if logged in; anonymous = only Featured).
- Display as card grid (VanAnCard per product) with "Scan QR để mua" button.
- **NEW (Round 2 decision): "Scan QR để mua" button → mở ngay cửa sổ scan QRCode (QRScanner component as modal)**
  - Không navigate sang page `/scan` — mở camera scan inline tại Home.razor.
  - Use existing `VanAn.KhachLink.Components.QRScanner` component inside `VanAnModal`.
  - **Blazor Interactivity requirement:** modal must set `rendermode` and handle `OnAfterRender` correctly to start/stop camera. Use `prerender: false` for the modal content to avoid hydration/camera errors (Category A/B gate).
  - Flow: khách bấm "Scan QR để mua" → camera mở → scan QR → parse `QRCodePayload` (UnitPrice, VatRate, ProductName from QR) → add to cart via `CartService` → show toast "Đã thêm {ProductName} vào giỏ" → đóng modal → customer tiếp tục browse hoặc bấm "Xem Giỏ Hàng".
  - **UX design:** Scan modal có nút "Đóng" + "Xem giỏ hàng" sau khi scan thành công.
- **Simplification:** Home.razor cards show DisplayName + DisplayPrice (marketing). Actual price comes from QR scan. Home.razor is "discovery", Scan modal is "ordering".

---

## 2. Reverse Impact Analysis

### UI Layer (`5_WebApps/ShopERP/Components/Pages/Admin/`)
- **NEW: `ShopInstances.razor`** — full CRUD page:
  - List all ShopInstances (table: Label, BaseUrl, MaxTenants, IsActive, HealthStatus, LastHealthCheck, TenantCount, Actions).
  - Create modal: Label, BaseUrl, MaxTenants, HealthCheckUrl.
  - Edit modal: Label, MaxTenants (BaseUrl immutable after creation — would break routing).
  - Activate/Deactivate buttons.
  - "Kiểm tra sức khoẻ" button → calls `POST /api/v1/shop-instances/{id}/health-check`.
  - Delete: blocked if TenantCount > 0 (show error alert).
  - All UI Platform components (`VanAnButton`, `VanAnCard`, `VanAnTable`, `VanAModal`, `VanAForm`, `VanAInput`, `VanAAlert`).
- **`TenantManagement.razor`** — UPDATE:
  - Add column "ShopERP Instance" in tenant list table (between "Email liên hệ" and "Ngày tạo").
  - Show `Label` + small text `BaseUrl` below.
  - If `ShopInstanceId` is null (legacy tenant not backfilled) → show red badge "Chưa gán".
  - Add `ShopInstance` dropdown in Create modal (required).
  - Add `ShopInstance` dropdown in Onboarding modal (required).
  - Add disabled "Chuyển ShopERP" button in actions column (tooltip: "Sắp ra mắt").
- **NEW: `ShopInstances.razor.cs`** — code-behind (follow pattern of `UserManagement.razor.cs`).

### Service Layer (`5_WebApps/ShopERP/Services/`)
- **NEW: `ShopInstanceApiClient.cs`** — thin HTTP client calling Gateway `/api/v1/shop-instances`:
  - Pattern: same as `TenantOnboardingApiClient.cs` (mint SystemAdmin JWT, call Gateway).
  - Methods: `ListAsync`, `CreateAsync`, `UpdateAsync`, `SetActiveAsync`, `HealthCheckAsync`, `CountTenantsAsync`.
- **`TenantOnboardingApiClient.cs`** — UPDATE `OnboardTenantRequest` to include `ShopInstanceId` (per Phase 3 contract).

### Controllers (`5_WebApps/ShopERP/Controllers/`)
- **`TenantController.cs`** — REVIEW: may need endpoint to list tenants with their ShopInstance info (for TenantManagement page). Currently `TenantController` is minimal — likely needs `GET /api/tenants/with-shop-instance` returning tenant + ShopInstance Label/BaseUrl.
  - **Alternative:** TenantManagement page calls both `TenantManagementService` (for tenants) + `ShopInstanceApiClient` (for instances), joins in memory. Simpler. **Recommended.**

### DI Registration (`5_WebApps/ShopERP/Program.cs`)
- `builder.Services.AddScoped<ShopInstanceApiClient>();`
- `builder.Services.AddHttpClient<ShopInstanceApiClient>();` (configure BaseAddress = Gateway URL).

### NavMenu (`5_WebApps/ShopERP/Components/Layout/NavMenu.razor`)
- Add nav link "ShopERP Instances" under Admin section (visible to SystemAdmin only).
- Link to `/admin/shop-instances`.

### Sitemap (`5_WebApps/ShopERP/Components/Pages/Sitemap.razor`)
- Add entry for `/admin/shop-instances`.

### Domain Layer (NEW — FeaturedProduct)
- **`1_Shared/Domain.cs`** — ADD `FeaturedProduct` entity:
  - Inherits `BaseEntity`, follows Single-Identity pattern (constructor sets `Id = FeaturedProductId.Value`).
  - Fields: ProductId, TenantId, DisplayName, DisplayDescription?, ImageUrl?, DisplayPrice, IsActive, SortOrder, FeaturedAt.
  - **Domain Modification — requires user approval per governance IMPLEMENT rule.**
- **NEW: `FeaturedProductId` value object** (record with Guid Value + implicit conversions).

### Infrastructure (NEW — PG only)
- **NEW: `3_CoreHub/Infrastructure/Configurations/FeaturedProductConfiguration.cs`** — EF Core config (table `FeaturedProducts`, ignore business key VO, unique index on ProductId+TenantId).
- **`3_CoreHub/Infrastructure/VanAnDbContext.cs`** — ADD `DbSet<FeaturedProduct> FeaturedProducts`.
- **NEW: PG migration** `AddFeaturedProductsTable` — create table in PostgreSQL.
- **NOTE:** FeaturedProduct is **PG-only** — NOT added to ShopERPDbContext/SQLite. This is a Gateway marketing table.

### Gateway API (NEW — catalog endpoint)
- **NEW: `2_Gateway/Controllers/CatalogController.cs`**:
  - `GET /api/catalog/recommended?customerId={id}&page=1&pageSize=20`
  - Queries PG: FeaturedProducts (active) + OrderItems (customer history).
  - Returns union list with `source` field ("Featured" | "History").
  - [AllowAnonymous] — Home.razor is public-facing (customer may not be logged in).
  - If `customerId` empty → return only FeaturedProducts.
- **NEW: `2_Gateway/Controllers/FeaturedProductsController.cs`**:
  - CRUD: `GET /api/v1/featured-products`, `POST`, `PUT`, `DELETE`.
  - [Authorize(SystemAdmin)] — sysadmin only.
  - Body: `{ productId, tenantId, displayName, displayPrice, imageUrl?, sortOrder, isActive }`.

### KhachLink UI (NEW — Home.razor refactor)
- **`Home.razor`** — REWRITE:
  - Remove existing catalog browse + tenant selector + infinite scroll.
  - Call `GET /api/catalog/recommended?customerId={id}` (customerId from localStorage).
  - Display card grid (VanAnCard): DisplayName, DisplayPrice, ImageUrl, "Đặt hàng" button.
  - "Đặt hàng" → show info "Vui lòng quét QR tại quán để đặt hàng" OR if product has QR → link to scan.
  - Sections: "Gợi ý cho bạn" (History) + "Sản phẩm nổi bật" (Featured).
  - All UI Platform components.

### Admin UI (NEW — FeaturedProducts page)
- **NEW: `5_WebApps/ShopERP/Components/Pages/Admin/FeaturedProducts.razor`**:
  - List all FeaturedProducts (table: DisplayName, Tenant, DisplayPrice, IsActive, SortOrder, Actions).
  - Create modal: ProductId (GUID input or search), TenantId (dropdown), DisplayName, DisplayPrice, ImageUrl, SortOrder.
  - Edit modal: DisplayName, DisplayPrice, ImageUrl, SortOrder, IsActive toggle.
  - Delete: confirm dialog.
  - All UI Platform components.
- **NEW: `FeaturedProducts.razor.cs`** — code-behind.
- **NEW: `5_WebApps/ShopERP/Services/FeaturedProductApiClient.cs`** — thin HTTP client calling Gateway `/api/v1/featured-products`.

### NavMenu + Sitemap (NEW)
- Add "Sản phẩm nổi bật" nav link under Admin → `/admin/featured-products` (SystemAdmin only).

### Tests
- **Manual UI test** (Phase 6 gate): create ShopInstance, create tenant with ShopInstance selected, verify tenant list shows correct instance.
- **Manual UI test (NEW):** add FeaturedProduct → open Home.razor → verify it appears in "Sản phẩm nổi bật" section.
- **NEW unit test: `6_Tests/VanAn.Core.Tests/FeaturedProductTests.cs`** — entity creation, Single-Identity pattern, IsActive toggle.
- **NO Playwright** (governance).
- **Optional unit test:** `ShopInstanceApiClientTests.cs` with mocked HttpClient (low value — thin client). Skip unless time permits.

### TDD Plan
1. Write failing test for `FeaturedProduct.Create` (Single-Identity pattern).
2. Add `FeaturedProduct` entity to Domain.cs → test passes.
3. Add EF config + migration → build.
4. Create `FeaturedProductsController` (Gateway) → build.
5. Create `CatalogController` (Gateway) → build.
6. Create `FeaturedProductApiClient` + `FeaturedProducts.razor` (ShopERP admin) → build.
7. Refactor `Home.razor` (KhachLink) → build.
8. Create `ShopInstanceApiClient.cs` + `ShopInstances.razor` page.
9. Update `TenantManagement.razor` (add column + dropdowns).
10. Update `NavMenu.razor` + `Sitemap.razor`.
11. Manual UI test.

---

## 3. Detailed Coding Plan

### Namespace Strategy
- `VanAn.Shared.Domain` (FeaturedProduct entity)
- `VanAn.ShopERP.Services` (ShopInstanceApiClient, FeaturedProductApiClient)
- `VanAn.ShopERP.Components.Pages.Admin` (ShopInstances.razor, FeaturedProducts.razor)
- `VanAn.ShopERP.Components.Layout` (NavMenu.razor)
- `VanAn.Gateway.Controllers` (CatalogController, FeaturedProductsController)
- `VanAn.KhachLink.Pages` (Home.razor refactor)

### Implementation Steps
**Step 1 — FeaturedProduct entity (1 modified file):**
- Add `FeaturedProduct` class + `FeaturedProductId` VO to `1_Shared/Domain.cs`.
- Follow Single-Identity pattern (constructor sets Id = FeaturedProductId.Value).
- Build → 0 errors.

**Step 2 — EF config + migration (2 new files + 1 modified):**
- `FeaturedProductConfiguration.cs` — table `FeaturedProducts`, unique index.
- `VanAnDbContext.cs` — add `DbSet<FeaturedProduct> FeaturedProducts`.
- PG migration `AddFeaturedProductsTable`.
- Build → 0 errors.

**Step 3 — Gateway API (2 new files):**
- `CatalogController.cs` — `GET /api/catalog/recommended` (public, queries PG).
- `FeaturedProductsController.cs` — CRUD (SystemAdmin only).
- Build → 0 errors.

**Step 4 — FeaturedProduct admin UI (2 new + 1 new service):**
- `FeaturedProductApiClient.cs` — thin HTTP client.
- `FeaturedProducts.razor` + `.razor.cs` — admin CRUD page.
- Register in Program.cs.
- Build → 0 errors.

**Step 5 — Home.razor refactor (1 modified file):**
- Replace catalog browse with `GET /api/catalog/recommended` call.
- Display card grid (VanAnCard).
- Build → 0 errors.

**Step 6 — ShopInstanceApiClient (1 new file):**
- Copy pattern from `TenantOnboardingApiClient.cs`.
- Methods: `ListAsync`, `CreateAsync`, `UpdateAsync`, `SetActiveAsync`, `HealthCheckAsync`.
- Build → 0 errors.

**Step 7 — DI registration (1 modified file):**
- `Program.cs`: register `ShopInstanceApiClient` + `FeaturedProductApiClient` + HttpClients.
- Build → 0 errors.

**Step 8 — ShopInstances.razor page (1 new file + 1 code-behind):**
- Follow `TenantManagement.razor` structure (header, list table, create modal, edit modal, alerts).
- All UI Platform components.
- Build → 0 errors.

**Step 9 — Update TenantManagement.razor (1 modified file):**
- Add column "ShopERP Instance" in list table.
- Inject `ShopInstanceApiClient`, load instances in `OnInitializedAsync`.
- Add dropdown in Create modal + Onboarding modal.
- Update `_createForm` + `_onboardingForm` to include `ShopInstanceId`.
- Update `HandleCreateSubmit` + `HandleOnboardingSubmit` to pass `ShopInstanceId`.
- Build → 0 errors.

**Step 10 — NavMenu + Sitemap (2 modified files):**
- Add nav link "ShopERP Instances" → `/admin/shop-instances` (SystemAdmin only).
- Add nav link "Sản phẩm nổi bật" → `/admin/featured-products` (SystemAdmin only).
- Add sitemap entries.
- Build → 0 errors.

**Step 11 — Manual UI test:**
- Login as SystemAdmin.
- Navigate to `/admin/shop-instances` → create instance "VPS Local" with `BaseUrl = http://shoperp:5003`.
- Navigate to `/admin/tenants` → verify existing tenants show "VPS Local" in new column (backfilled from Phase 1).
- Click "+ Tạo Tenant + Onboarding" → verify ShopInstance dropdown shows "VPS Local".
- Create new tenant → verify row appears with correct instance label.
- Navigate to `/admin/featured-products` → add a featured product → verify it appears in Home.razor.

**Step 12 — Full regression:**
- `dotnet build VanAn.sln` — 0 errors.
- `guard-check.ps1` PASS.

### Active Skills
- `accounting-ui-implementation` (admin UI)
- `ui-platform-compliance-review` (VanAn components)
- `ui-platform-migration` (TenantManagement.razor modification — must stay UI Platform compliant)
- `domain-integrity-validation` (FeaturedProduct entity — Single-Identity pattern)

---

## 4. Validation Gates

| Gate | Command | Expected |
|---|---|---|
| Build | `dotnet build VanAn.sln` | 0 errors |
| Unit: FeaturedProduct | `dotnet test --filter FeaturedProduct` | Entity creation + Single-Identity pass |
| Manual UI: ShopInstances page | Create + list + health-check | All work |
| Manual UI: TenantManagement | New column shows + dropdown works | All work |
| Manual UI: FeaturedProducts page | Add + list + edit + delete | All work |
| Manual UI: Home.razor | Featured products appear + history section | All work |
| Guard check | `./guard-check.ps1` | PASS |

---

## 5. Deliverables

- Modified: `1_Shared/Domain.cs` (FeaturedProduct entity + FeaturedProductId VO)
- New: `3_CoreHub/Infrastructure/Configurations/FeaturedProductConfiguration.cs`
- Modified: `3_CoreHub/Infrastructure/VanAnDbContext.cs` (add DbSet)
- New: PG migration `AddFeaturedProductsTable`
- New: `2_Gateway/Controllers/CatalogController.cs`
- New: `2_Gateway/Controllers/FeaturedProductsController.cs`
- New: `5_WebApps/ShopERP/Services/ShopInstanceApiClient.cs`
- New: `5_WebApps/ShopERP/Services/FeaturedProductApiClient.cs`
- New: `5_WebApps/ShopERP/Components/Pages/Admin/ShopInstances.razor` + `.razor.cs`
- New: `5_WebApps/ShopERP/Components/Pages/Admin/FeaturedProducts.razor` + `.razor.cs`
- Modified: `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor` (column + dropdowns)
- Modified: `5_WebApps/ShopERP/Services/TenantOnboardingApiClient.cs` (add ShopInstanceId to request)
- Modified: `5_WebApps/ShopERP/Components/Layout/NavMenu.razor` (2 new nav links)
- Modified: `5_WebApps/ShopERP/Components/Pages/Sitemap.razor` (2 new sitemap entries)
- Modified: `5_WebApps/ShopERP/Program.cs` (DI)
- Modified: `5_WebApps/KhachLink/Components/Pages/Home.razor` (refactor to recommended catalog + embed Scan modal on "Scan QR để mua" button)
- Modified: `5_WebApps/KhachLink/Services/Http/ProductHttpService.cs` (redirect `GetRecommendedProductsAsync` to `/api/catalog/recommended`)
- Modified/Obsolete: `3_CoreHub/Services/CustomerRecommendationService.cs` (retire after CatalogController verified)
- New: `6_Tests/VanAn.Core.Tests/FeaturedProductTests.cs`

---

## 6. Approval Gate

**Domain modification (NEW):** Adding `FeaturedProduct` + `FeaturedProductId` value object to `1_Shared/Domain.cs` requires user approval per governance IMPLEMENT rule. This is a new entity (not modifying existing `Product`), follows Single-Identity pattern.

**Note:** UI Platform compliance is a Hard Stop rule. All new UI must use `VanAn*` components. Reviewer must verify before marking phase complete.

**Dependencies:** This phase reuses the `OrderItems` + `Orders` PG schema and the Outbox routing key mechanism from Phase 3. Do not start Phase 6 before Phase 3 routing is merged.

---

## 7. COMPLETION SUMMARY

**Phase 6 COMPLETE** — commit `<HASH>` on `main`.

### Files created
| File | Purpose |
|------|---------|
| _TBD_ | _TBD_ |

### Files modified
| File | Change |
|------|--------|
| _TBD_ | _TBD_ |

### Issues fixed during implementation
- _TBD_

### Verification

#### Static Verification (compile-time)
- **Build:** _TBD_
- **Unit tests:** _TBD_
- **guard-check.ps1:** _TBD_

#### Live Runtime Verification (boot + HTTP + UI)
> **Lesson learned (Wave 0):** Build + Architecture Tests + guard-check PASS ≠ runtime works.
> Live runtime verification is MANDATORY for all phases.

| # | Test | Status | Evidence |
|---|------|--------|----------|
| RV1 | _TBD_ | _TBD_ | _TBD_ |
