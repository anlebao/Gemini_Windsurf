# Plan: Remove Shop Entity — Unify to Tenant (Option 1)

## Decision
Remove `Shop` entity entirely. `Tenant` = shop/company/HKD (single identity).
Preserve Store Finder + Campaign features by moving lat/lng to `TenantSettings`.

## Scope: 7 phases, ~40 files, 2 migrations (PG + SQLite), 1 data migration script

---

## Phase 1: Domain Layer — Extend TenantSettings + Remove Shop entity

### 1.1 Add Latitude/Longitude to TenantSettings
**File:** `1_Shared/Domain/Aggregates/TenantAggregate/TenantSettings.cs`
- Add `public double? Latitude { get; private set; }`
- Add `public double? Longitude { get; private set; }`
- Update constructor signature to accept lat/lng
- Add `WithCoordinates(double lat, double lng)` method

### 1.2 Update TenantConfiguration (EF Core)
**File:** `3_CoreHub/Infrastructure/Configurations/TenantConfiguration.cs`
- In `OwnsOne(e => e.Settings, ...)` block, add:
  - `settings.Property(s => s.Latitude).HasColumnName("Settings_Latitude");`
  - `settings.Property(s => s.Longitude).HasColumnName("Settings_Longitude");`

### 1.3 Remove Shop entity from Domain
**File:** `1_Shared/Domain.cs`
- Delete `Shop` class (lines 513-557)
- Delete `ShopId` record (line 434)
- Remove `SocialCampaign.ShopId` property (line 1384)
- Remove `SocialCampaign.Shop` navigation property (line 1395)
- Update `SocialCampaign` constructor — remove `shopId` parameter
- Rename `ShopConfig` record → `TenantConfig` (line 1331)
  - `ShopId` → `TenantId`
  - `ShopName` → `TenantName`
  - Keep all other fields (Address, Phone, Email, Latitude, Longitude, PrimaryColor, etc.)

### 1.4 Remove ShopIdConverter
**File:** `3_CoreHub/Infrastructure/ValueConverters/ShopIdConverter.cs`
- Delete file entirely

---

## Phase 2: CoreHub Infrastructure — Remove Shop config + DbSet

### 2.1 Remove ShopConfiguration
**File:** `3_CoreHub/Infrastructure/Configurations/ShopConfiguration.cs`
- Delete file entirely

### 2.2 Update SocialCampaignConfiguration
**File:** `3_CoreHub/Infrastructure/Configurations/SocialCampaignConfiguration.cs`
- Remove `ShopId` column config (lines 18-19)
- Remove `Shop` navigation config (lines 44-48)
- Remove index on `{TenantId, ShopId}` (line 51) — replace with index on `{TenantId}` only

### 2.3 Remove DbSet<Shop> from VanAnDbContext
**File:** `3_CoreHub/Infrastructure/VanAnDbContext.cs`
- Remove `public DbSet<Shop> Shops { get; set; }` (line 56)
- Remove `modelBuilder.ApplyConfiguration(new ShopConfiguration());` (line 224)

### 2.4 Remove DbSet<Shop> from IVanAnDbContext
**File:** `3_CoreHub/Infrastructure/IVanAnDbContext.cs`
- Remove `DbSet<Shop> Shops` (line 27)

### 2.5 Remove ShopRepository + ShopService
**Files to delete:**
- `3_CoreHub/Repositories/IShopRepository.cs`
- `3_CoreHub/Repositories/ShopRepository.cs` (if exists)
- `3_CoreHub/Services/ShopService.cs`
- `3_CoreHub/Services/IShopService.cs` (if exists)

### 2.6 Remove Shop DbSet from ShopERPDbContext
**File:** `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs`
- Remove `public DbSet<Shop> Shops { get; set; }` (line 31)
- Remove `modelBuilder.ApplyConfiguration(new ShopConfiguration());` (line 133)

---

## Phase 3: Gateway Controllers — Remove/Update Shop endpoints

### 3.1 Delete Gateway ShopsController
**File:** `2_Gateway/Controllers/ShopsController.cs`
- Delete file entirely (all endpoints will be replaced by tenant-based endpoints)

### 3.2 Add TenantStoreController (replaces Shop Store Finder)
**New file:** `2_Gateway/Controllers/TenantStoreController.cs`
- `GET /api/tenants/nearby?lat=&lng=&radiusKm=` — Store Finder, query Tenants with lat/lng
- `GET /api/tenants/search?name=` — search tenants by name
- `GET /api/tenants/{tenantId}/store-info` — returns tenant store info (name, address, phone, lat/lng)
- All `[AllowAnonymous]` (public Store Finder)
- Uses `IVanAnDbContext.Tenants` directly (no Shop entity)

### 3.3 Update CampaignsController — remove ShopId
**File:** `2_Gateway/Controllers/CampaignsController.cs`
- Remove `ShopId` from `CreateCampaignRequest` DTO (line 267)
- Remove `ShopId` from `UpdateCampaignRequest` DTO
- Remove ShopId handling in create/update methods (lines 187, 228)
- Remove ShopId from log messages (line 110)

### 3.4 Update PublicOrdersController — remove campaign.ShopId
**File:** `2_Gateway/Controllers/PublicOrdersController.cs`
- Line 66: `campaign.ShopId ?? Guid.Empty` → remove, use `tenantId` from campaign
- Line 69-72: Remove ShopId from log messages

### 3.5 Update ShopConfigController → TenantConfigController
**File:** `2_Gateway/Controllers/ShopConfigController.cs`
- Rename to `TenantConfigController`
- Route: `api/tenantconfig` (was `api/v1/shopconfig`)
- Replace `ShopConfig` with `TenantConfig`
- `ShopId` → `TenantId` throughout

### 3.6 Update KitchenController — ShopId → TenantId
**File:** `2_Gateway/Controllers/KitchenController.cs`
- Line 43: `shop_{update.ShopId}` → `tenant_{update.TenantId}`

### 3.7 Update ProductsController — shopId → tenantId
**File:** `2_Gateway/Controllers/ProductsController.cs`
- Line 103: rename `shopId` parameter to `tenantId`
- Line 107: `ResolveShopErpClientAsync(shopId)` → `ResolveShopErpClientAsync(tenantId)`
- Line 124: Update log message

### 3.8 Update DashboardController + OnboardingController
**Files:** `2_Gateway/Controllers/DashboardController.cs`, `2_Gateway/Controllers/OnboardingController.cs`
- Replace `ShopId` with `TenantId` in DTOs and log messages

---

## Phase 4: ShopERP — Remove Shop CRUD + Update Campaigns admin

### 4.1 Delete ShopERP ShopsController
**File:** `5_WebApps/ShopERP/Controllers/ShopsController.cs`
- Delete file entirely

### 4.2 Add TenantStoreController (ShopERP side)
**New file:** `5_WebApps/ShopERP/Controllers/TenantStoreController.cs`
- `GET /api/tenant-store/{tenantId}` — returns tenant store info from SQLite
- `GET /api/tenant-store/nearby?lat=&lng=&radiusKm=` — nearby tenants
- Uses `ShopERPDbContext.Tenants` (with Settings owned entity)

### 4.3 Delete ShopApiClient
**File:** `5_WebApps/ShopERP/Services/ShopApiClient.cs`
- Delete file entirely

### 4.4 Delete ShopsAdmin.razor
**File:** `5_WebApps/ShopERP/Components/Pages/Admin/ShopsAdmin.razor`
- Delete file entirely

### 4.5 Update CampaignsAdmin.razor — remove Shop dropdown
**File:** `5_WebApps/ShopERP/Components/Pages/Admin/CampaignsAdmin.razor`
- Remove `@inject ShopApiClient ShopApi` (line 13)
- Remove Shop dropdown from create/edit modal (lines 134-149)
- Remove `_shops` list + `LoadShopsForTenant` method (lines 199, 264-280)
- Remove `ShopId` from `_form` class (line 390)
- Remove Shop badge from list (lines 79-81)
- Remove `ShopId = item.ShopId` from edit mapping (line 300)
- Remove `ShopId = _form.ShopId` from create payload (line 350)

### 4.6 Update ShopSettingsController → TenantSettingsController
**File:** `5_WebApps/ShopERP/Controllers/ShopSettingsController.cs`
- Rename route: `api/shop/settings` → `api/tenant/settings`
- Rename class: `ShopSettingsController` → `TenantSettingsController`
- Logic unchanged (already tenant-scoped)

### 4.7 Update Program.cs — remove Shop service registration
**File:** `5_WebApps/ShopERP/Program.cs`
- Remove `services.AddScoped<IShopService, ShopService>();` (line 427)
- Remove `ShopApiClient` registration
- Update `ShopFeatureSettings` → `TenantFeatureSettings` references (optional, can defer)

### 4.8 Update NavMenu.razor — remove Shops admin link
**File:** `5_WebApps/ShopERP/Components/Layout/NavMenu.razor`
- Remove `/admin/shops` nav link

### 4.9 Update TenantManagement.razor — add lat/lng fields
**File:** `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor`
- Add Latitude/Longitude input fields to tenant edit form
- These map to `TenantSettings.Latitude/Longitude`

---

## Phase 5: KhachLink — Update Shop references

### 5.1 Update Home.razor — Store Finder endpoint
**File:** `5_WebApps/KhachLink/Pages/Home.razor`
- Line 466: `/api/shops/by-tenant/{tenantId}` → `/api/tenants/{tenantId}/store-info`
- `StoreInfoDto` — keep as is (Id, Name, Address, Phone, Latitude, Longitude)
- Line 18: `ShopConfigHttpService` → `TenantConfigHttpService`
- Line 346: `ShopConfig` → `TenantConfig`
- Line 506: `ShopConfigService.GetShopConfigFromProductsAsync` → `TenantConfigService.GetTenantConfigFromProductsAsync`

### 5.2 Update Scan.razor — legacy QR path
**File:** `5_WebApps/KhachLink/Pages/Scan.razor`
- Line 271: `qrPayload.ShopId` → fallback to `qrPayload.TenantId` (legacy QR codes that only have ShopId will break — acceptable, they're old)
- Better: `qrPayload.TenantId != Guid.Empty ? qrPayload.TenantId : qrPayload.ShopId` (backward compat)

### 5.3 Update Campaign.cshtml.cs — remove ShopId
**File:** `5_WebApps/KhachLink/Pages/Campaign.cshtml.cs`
- Line 21: Remove `[FromQuery(Name = "shopId")] public Guid? ShopId`
- Line 40-41: `ShopId ?? Campaign.ShopId ?? Guid.Empty` → use `Campaign.TenantId.Value`
- `Products = await _productService.GetProductsAsync(Campaign.TenantId.Value)`

### 5.4 Update SocialCampaignHttpService — remove by-shop endpoint
**File:** `5_WebApps/KhachLink/Services/Http/SocialCampaignHttpService.cs`
- Remove `GetCampaignsByShopAsync(Guid shopId)` method (line 43)
- Keep `GetCampaignsByTenantAsync(Guid tenantId)` (line 53)

### 5.5 Rename ShopConfigHttpService → TenantConfigHttpService
**File:** `5_WebApps/KhachLink/Services/Http/ShopConfigHttpService.cs`
- Rename file + class
- Update endpoint: `shoperp/api/shops/by-tenant/{tenantId}` → `api/tenants/{tenantId}/store-info`
- `ShopConfig` → `TenantConfig` return type

### 5.6 Update ShopDto → TenantStoreDto
**File:** `5_WebApps/KhachLink/Models/ShopDto.cs`
- Rename to `TenantStoreDto` or delete if unused after Home.razor update

### 5.7 Update OfflineOrderDto.ShopId → TenantId
**File:** `5_WebApps/KhachLink/Models/OfflineOrderDto.cs`
- Line 15: `public string ShopId` → `public string TenantId`
- Line 64: `new TenantId(Guid.Parse(ShopId))` → `new TenantId(Guid.Parse(TenantId))`
- Line 81: `ShopId = order.TenantId.Value.ToString()` → `TenantId = order.TenantId.Value.ToString()`

### 5.8 Update OfflineOrderService + SyncConflictResolver
**Files:**
- `5_WebApps/KhachLink/Services/OfflineOrderService.cs`
- `5_WebApps/KhachLink/Services/SyncConflictResolver.cs`
- `5_WebApps/KhachLink/Services/ConflictResolutionService.cs`
- Replace `offlineOrder.ShopId` → `offlineOrder.TenantId`
- Replace `order.ShopId` → `order.TenantId`

### 5.9 Update DynamicThemeProvider.razor
**File:** `5_WebApps/KhachLink/Components/DynamicThemeProvider.razor`
- Line 98: `public Guid ShopId` → `public Guid TenantId`
- Line 120: `/api/v1/shopconfig/shops/{ShopId}/config` → `/api/tenantconfig/{TenantId}/config`
- Line 133: `ShopId = ShopId` → `TenantId = TenantId`

### 5.10 Update RealTimeDashboard.razor
**File:** `5_WebApps/KhachLink/Components/Dashboard/RealTimeDashboard.razor`
- Line 524: `public Guid ShopId` → `public Guid TenantId`
- Update all references

### 5.11 Update Program.cs
**File:** `5_WebApps/KhachLink/Program.cs`
- Line 69: `ShopConfigHttpService` → `TenantConfigHttpService`
- Line 109: `ShopFeatureSettingsHttpService` → keep (or rename to `TenantFeatureSettingsHttpService`)

---

## Phase 6: Shared DTOs — Update ShopId references

### 6.1 QRCodePayload — keep ShopId for backward compat
**File:** `1_Shared/DTOs/QRCodePayload.cs`
- Keep `ShopId` field (legacy QR codes still in circulation)
- Add `[Obsolete("Use TenantId instead. ShopId kept for legacy QR backward compat.")]`
- Update constructors: `shopId` parameter → accept but store in both ShopId and TenantId

### 6.2 Kitchen DTOs — ShopId → TenantId
**Files:**
- `1_Shared/DTOs/KitchenStatusUpdateDto.cs` — `ShopId` → `TenantId`
- `1_Shared/DTOs/KitchenEvents.cs` — `ShopId` → `TenantId`
- `1_Shared/DTOs/KitchenAnalyticsDto.cs` — `ShopId` → `TenantId`

### 6.3 Update KitchenService
**File:** `3_CoreHub/Services/KitchenService.cs` (if exists)
- `GetGroupedKitchenItemsAsync(Guid shopId)` → `GetGroupedKitchenItemsAsync(Guid tenantId)`
- Remove `TenantId tenantId = new(shopId)` conversion

### 6.4 Update Kitchen Display.razor
**File:** `5_WebApps/ShopERP/Components/Pages/Kitchen/Display.razor`
- Update ShopId → TenantId references

---

## Phase 7: Migrations + Data Migration

### 7.1 PG migration — Remove Shop + Add Tenant lat/lng
**New file:** `3_CoreHub/Infrastructure/Migrations/<timestamp>_RemoveShopEntity.cs`
- Drop FK `FK_SocialCampaigns_Shops_ShopId`
- Drop column `SocialCampaigns.ShopId`
- Drop table `Shops`
- Add columns `Tenants.Settings_Latitude` (double), `Tenants.Settings_Longitude` (double)

### 7.2 SQLite migration — Same
**New file:** `5_WebApps/ShopERP/Migrations/<timestamp>_RemoveShopEntity.cs`
- Same as PG migration

### 7.3 Data migration script (VPS)
**Script:** `scripts/migrate_shop_to_tenant.sh`
- Before running migration:
  1. For each Shop row: UPDATE Tenants SET Settings_Latitude = shop.Latitude, Settings_Longitude = shop.Longitude WHERE Tenants.Id = shop.TenantId
  2. UPDATE SocialCampaigns SET ShopId = NULL (all campaigns become tenant-wide)
- Then run EF migration to drop tables/columns

---

## Phase 8: Cleanup + Build + Test

### 8.1 Delete test file
**File:** `6_Tests/ShopServiceMultiTenancyTests.cs`
- Delete entirely (tests ShopService which no longer exists)

### 8.2 Update Architecture tests
**File:** `6_Tests/VanAn.Architecture.Tests/AuthorizationEnforcementTests.cs`
- Remove Shop-related test cases (lines 86-93)

### 8.3 Build + fix errors
- `dotnet build VanAn.sln -c Release`
- Fix any remaining Shop references (grep for "Shop" excluding "ShopInstance" and "Shopping")

### 8.4 Run tests
- `dotnet test 6_Tests/VanAn.Core.Tests/`
- `dotnet test 6_Tests/VanAn.Architecture.Tests/`

### 8.5 Commit + push + RV on VPS
- Run data migration script on VPS PostgreSQL
- Run data migration script on VPS SQLite
- Deploy via CD
- Verify:
  - `/accounting/history` works
  - `/orders` works
  - KhachLink Home page shows store info (from Tenant, not Shop)
  - Campaign page works (no ShopId)
  - Store Finder works (if implemented)

---

## Risk Assessment

| Risk | Mitigation |
|---|---|
| Existing QR codes with ShopId break | Keep ShopId field in QRCodePayload, fallback to TenantId |
| Store Finder lat/lng lost | Migrate to TenantSettings before dropping Shops table |
| Campaigns with ShopId lose targeting | Set all to null (tenant-wide) — acceptable per user decision |
| Kitchen SignalR groups break | Update group name from `shop_{id}` to `tenant_{id}` — both sides update together |
| OfflineOrderDto.ShopId breaks offline sync | Rename to TenantId — old offline data has ShopId as string, will need migration |

## Estimated effort
- ~40 files modified/deleted
- 2 EF migrations (PG + SQLite)
- 1 data migration script
- 1 new controller (TenantStoreController)
- Build + test + deploy cycle
