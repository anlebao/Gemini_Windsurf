# RESEARCH SNAPSHOT: Crawl-to-Onboard Tenant Pipeline

> **Snapshot taken:** 2026-08-25 @ commit `73f77f14` (main branch)
> **⚠️ WARNING:** Line numbers/method signatures sẽ stale sau mỗi commit. **Re-verify trước mỗi phase** bằng grep/read.
> **Purpose:** Codebase findings để hỗ trợ task card. Tách riêng khỏi master plan để refresh dễ.

---

## 1. Domain layer (`1_Shared/Domain/Aggregates/TenantAggregate/`)

### `TenantStatus.cs` (20 lines — verified 2026-08-25)
```csharp
public enum TenantStatus {
    Active = 1, Suspended = 2, Inactive = 3, Converted = 4
}
```
**Phase 1 action:** Add `Pending = 5` (NOT `=0` — correction H1).

### `TenantSettings.cs` (144 lines — verified 2026-08-25)
- **Class** (not record), private setters, parameterless ctor for EF.
- **Constructor:** 16 params: `contactEmail, contactPhone, address, logoUrl, taxCode, latitude, longitude, slug, socialLinksFb, socialLinksTiktok, brandStory, theme, commerceModeOverride, navColor, headerColor, footerColor`.
- ⚠️ **Missing in constructor:** `LegalForm`, `BusinessField`, `CharterCapital` (lines 45-47 — declared as properties but NOT in ctor params, NOT in With methods → set via reflection/EF only? Verify).
- **With methods: 12** (verified count — correction H3):
  1. `WithContactEmail` 2. `WithContactPhone` 3. `WithAddress` 4. `WithTaxCode` 5. `WithCoordinates` 6. `WithSlug` 7. `WithSocialLinks` 8. `WithBrandStory` 9. `WithTheme` 10. `WithCommerceModeOverride` 11. `WithStyleColors` 12. `WithLogoUrl`
- **Phase 1 action:** Add `CrawledPhone` to constructor (param 17) + all 12 With methods + preserve LegalForm/BusinessField/CharterCapital.

### `Tenant.cs` (339 lines — verified partial)
- 3 factories all set `Status=Active`.
- **`UpdateSlug(string? slug)` (line 180-199):** guard `if (Status == TenantStatus.Inactive) throw`. ⚠️ **Correction C4:** DO NOT tighten to `!= Active` — Suspended + Converted need update slug. Pending tenant bypass `UpdateSlug()` entirely — set slug via `CreateUnverified(..., pendingSlug)` factory param (Correction H2).
- Has `SetTenantId()` for self-reference, `AssignToShopInstance()`, `ChangeBusinessType()`.
- **Phase 1 action:** Add `CreateUnverified(TenantId id, string name, TenantSettings settings, string pendingSlug)` — sets `Status=Pending=5`, raises `TenantPendingEvent` (NOT `TenantCreatedEvent`).
- **Phase 1 action:** Add `Verify()` — guard `Status == Pending && PotentialDuplicateOf == null` (Correction H4), set `Status=Active`, raise `TenantVerifiedEvent`.
- **Phase 1 action:** Add `Guid? PotentialDuplicateOf` (NOT `TenantId?` — Correction C1) + `MarkPotentialDuplicateOf(Guid otherId)`.

### `TenantEvents.cs` (64 lines)
- 5 events exist. **Phase 1 action:** Add 4 new records:
  - `TenantPendingEvent(Guid TenantId, string TenantName, string? TaxCode, string? SourceUrl, DateTime OccurredAt)`
  - `TenantVerifiedEvent(Guid TenantId, Guid ApprovedByUserId, DateTime OccurredAt)`
  - `TenantClaimRequestedEvent(Guid TenantId, Guid ClaimRequestId, string ClaimantName, DateTime OccurredAt)`
  - `TenantClaimApprovedEvent(Guid TenantId, Guid ClaimRequestId, Guid OwnerUserId, Guid ApprovedByUserId, DateTime OccurredAt)`

---

## 2. EF Config (`3_CoreHub/Infrastructure/Configurations/TenantConfiguration.cs` — verified line 46-49)

```csharp
builder.Property(e => e.Status)
    .HasConversion<int>()
    .HasDefaultValue(TenantStatus.Active)  // line 48
    .IsRequired();
```
**Phase 2 action:** DO NOT change default (correction C2 reasoning). `CreateUnverified` sets `Status=Pending=5` explicitly → EF uses explicit value over default.

- `Settings.Slug` has unique index (line 65 — verify).
- `Settings.OwnsOne` flattens to `Settings_*` columns.
- **Phase 2 action:** Add `Settings_CrawledPhone` column (maxLength 50, nullable) + `Tenants.PotentialDuplicateOf` (Guid?, nullable, NO FK constraint — avoid cascade).

---

## 3. Services (`3_CoreHub/Services/`)

### `Onboarding/TenantOnboardingService.cs` (117 lines — verified full)
- Primary ctor: `(ITenantManagementService, IUserManagementService, IPermissionGroupService, IRoleAssignmentService, ILogger)`.
- `DefaultGroups` array: 4 groups (Quản lý, Thu ngân, Bếp, Kho).
- `OnboardAsync`: 5 steps — create tenant → assign ShopInstance → create owner user → assign Owner role → create 4 groups → assign owner to Quản lý.
- **Phase 3 action:** Add `OnboardUnverifiedAsync(CrawlListingDto, ct)` — only create Pending tenant + CrawlSource audit + duplicate check. NO user, NO groups.
- **Phase 3 action:** Add `VerifyAsync(Guid tenantId, VerifyTenantRequest, ct)` — call `tenant.Verify()`, then reuse steps 2-5 from `OnboardAsync` (create user + groups + unmask phone + update slug).

### `TenantManagementService.cs` (243 lines — not re-verified)
- `CreateTenantAsync` (creates Active + welcome email via `HandleTenantCreatedAsync`).
- `UpdateSlugAsync`, suspend/reactivate/deactivate, `ChangeBusinessTypeAsync`.

### `IUserManagementService.cs`
- `CreateUserAsync(tenantId, username, plainPassword, displayName, role, ct)`.

---

## 4. API — Gateway (`2_Gateway/Controllers/`)

### `TenantsController.cs` (329 lines — not re-verified, line refs may shift)
- `[Route("api/v1/tenants")]`, `[Authorize(Policy="SystemAdmin")]`.
- Endpoints: ListAll, Create, GetById, UpdateProfile, AssignShopInstance, UpdateSlug, ChangeBusinessType.

### `TenantStoreController.cs` (317 lines — not re-verified)
- `[Route("api/tenants")]`, `[AllowAnonymous]`.
- **`GetBySlug(string slug)`** does NOT filter by Status → returns any tenant with matching slug.
- `MapToStoreDto` returns `Phone = Settings.ContactPhone`.
- **Phase 4 action:** After load, check Status:
  - `Active` → full DTO (current behavior)
  - `Pending` → `Phone = MaskPhone(Settings.CrawledPhone)`, `Email = null`, add `IsPending = true`, `ClaimUrl = $"/store/{slug}/claim"` (Correction H6: NO separate `MaskedPhone` field — reuse `Phone`)
  - `Suspended/Inactive/Converted` → 404
- `GetNearby`, `Search` filter `Status == Active` — Pending not included (correct).

### Pattern #10 compliance
Gateway controllers using `StringContent`/`MediaTypeHeaderValue` must strip charset from `Request.ContentType`. Most new endpoints use `[FromBody]` → likely N/A, but audit.

---

## 5. API — KhachLink (`5_WebApps/KhachLink/`)

- **`Services/Http/TenantProfileHttpService.cs`** (82 lines): `GetBySlugAsync(slug)` → `api/tenants/by-slug/{slug}`. Returns `ShopDto`. Uses `JsonStringEnumConverter`. HttpClient named "gateway".
- **`Pages/Store.razor`** (line 7): `@page "/store/{Slug}"`. Renders hero, brand story, address, social, cart, products.
- **`Program.cs`** (line 56): `AddHttpClient("gateway", ...)` with `Gateway:BaseUrl`.
- **Hard stop:** KhachLink MUST NOT inject `IVanAnDbContext` — HTTP via Gateway only.
- **Phase 6 action:** Modify `Models/ShopDto.cs` — add `IsPending` + `ClaimUrl` (NO `MaskedPhone` — correction H6). Modify `Store.razor` — if Pending: hide commerce, show banner + Claim button. New `Pages/Claim.razor` + `ClaimHttpService`.

---

## 6. Outbox + NATS (`3_CoreHub/`)

- `OutboxMessage.Create(eventType, eventData, tenantId, routingKey?, correlationId?)` → status `Pending` → `NatsSyncWorker` flushes → `INatsEventPublisher.PublishAsync(subject, byte[], ct)`.
- Pattern: domain event → save aggregate → dispatch handler → write outbox row → NATS publisher.
- ⚠️ **Open O4 RESOLVED (Option A approved 2026-08-25):** Active tenant MUST sync sang ShopERP SQLite qua NATS. Evidence (verified 2026-08-25): `Program.cs:151-153` inject `ShopERPDbContext` (SQLite) vào `IVanAnDbContext`. `ProductsController.cs:146` query `_dbContext.Tenants.ToListAsync()` (NO try/catch — fail nếu thiếu tenant). `UserManagement.razor.cs:45` query `DbContext.Tenants.ToListAsync()` (try/catch graceful). `TenantController.cs:37` `tenantService.CreateTenantAsync` — local SQLite CRUD. **`AdminController.cs:25-26`** comment nói "ShopERP SQLite has no Tenants table" — **OUTDATED/CONTRADICTORY** với `ShopERPDbContext.cs:55` `DbSet<Tenant> Tenants`. **Data integrity constraint (user-raised 2026-08-25):** nếu tenant có 2 ID khác nhau ở PG vs SQLite → order/accounting split → số liệu kế toán sai. **Decision (Option A):** `TenantVerifiedEvent` + `TenantProfileUpdatedEvent` → outbox → NATS `vanan.cloud.tenant.verified`/`tenant.profile.updated` → NEW `TenantSyncSubscriber` (ShopERP) upsert SQLite row với cùng `Guid` tenantId. Pending KHÔNG sync. Follow `OrderSyncSubscriber` pattern. **C2 SQLite migration vẫn cần** cho schema consistency.

---

## 7. Migrations

- **CoreHub PG latest:** `20260821063126_AddBusinessProfile.cs` — pattern: `CreateTable` + `CreateIndex`, `uuid` for Guid, `timestamp without time zone` for DateTime, `text` for string, `numeric(18,2)` for decimal, `boolean` for bool.
- **⚠️ Correction C2:** `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs:55` có `DbSet<Tenant> Tenants` (SQLite mirror). Legacy plan said "NO ShopERP migration" — **SAI**. Phase 2 phải tạo migration song song ở `5_WebApps/ShopERP.Migrations/` cho 2 cột `Tenants.PotentialDuplicateOf` + `Tenants.Settings_CrawledPhone` (or confirm ShopERP never queries these fields + disable model check — riskier).
- `TenantClaimRequests` + `CrawlSources` tables: PG-only (Gateway source of truth per Option C) — KHÔNG mirror sang SQLite.

---

## 8. Solution structure (`VanAn.sln`)

- Projects: 1_Shared, 2_Gateway, 3_CoreHub, 5_WebApps (KhachLink/ShopERP/Directory), 6_Tests (Core.Tests, ShopERP.Tests, Architecture.Tests, Integration.Tests, Load.Tests, E2E.Tests).
- **No `7_Tooling` folder yet** — Phase 5 tạo + add to .sln.
- `Directory.Packages.props` centralized versioning — Phase 5 add `AngleSharp` (≥7 days old stable version).
- ⚠️ Verify CoreHub.csproj `OutputType` — legacy plan claim "Exe (existing violation)". If still Exe → flag tech debt, not our fix.

---

## 9. Test patterns (`6_Tests/`)

- `VanAn.Core.Tests/Services/TenantDomainTests.cs`: pure domain unit, `FluentAssertions`, `[Fact(DisplayName="W5-D1: ...")]`.
- `VanAn.Core.Tests/Services/TenantManagementServiceTests.cs`: `VanAnDbContextTestFactory.Create()` (SQLite in-memory), Moq `INotificationService` + `IShopInstanceService`.
- `VanAn.Core.Tests/Services/Onboarding/TenantOnboardingServiceTests.cs`: Moq 4 deps, `NullLogger<T>.Instance`.
- `VanAn.Architecture.Tests/ArchitectureRulesTests.cs`: file-content checks — Phase 5 update if crawler .csproj added (whitelist `7_Tooling`).
- ⚠️ **Open O3:** `VanAnDbContextTestFactory` cần update cho 2 DbSet mới (`TenantClaimRequests`, `CrawlSources`) — Phase 8.

---

## 10. VERIFY-BEFORE-IMPLEMENT CHECKLIST

Trước mỗi phase, re-verify các line ref trên:
- [ ] Phase 1: `TenantStatus.cs`, `TenantSettings.cs` (12 With + ctor 16 params), `Tenant.cs:180-199` (UpdateSlug guard)
- [ ] Phase 2: `TenantConfiguration.cs:46-49` (Status default), `ShopERPDbContext.cs:55` (DbSet<Tenant>)
- [ ] Phase 3: `TenantOnboardingService.cs` (ctor deps + DefaultGroups + OnboardAsync 5 steps)
- [ ] Phase 4: `TenantStoreController.cs` GetBySlug + MapToStoreDto (line refs likely shifted)
- [ ] Phase 5: `Directory.Packages.props` (AngleSharp version), Gateway API key auth (`HmacApiKeyLookupAdapter.cs`)
- [ ] Phase 6: `TenantProfileHttpService.cs`, `Store.razor`, `Models/ShopDto.cs`, image upload service (Cloudinary?)
- [ ] Phase 7: `TenantManagement.razor` (line ~1003 likely shifted)
- [ ] Phase 8: `VanAnDbContextTestFactory`, test patterns
