# Project State Archive

> **Mục đích:** Lưu trữ các wave đã hoàn thành để giảm file size của project_state.md
> **Archived Date:** 2026-06-24

---

## Archived Waves

**PREVIOUS OBJECTIVE (archived)**
**Production Hygiene — Wave 7: Production Hardening**

**Status:** ✅ COMPLETED — Branch `feature/wave7-prod-hardening`, merged to base for Wave 8

**Completed Actions:**
1. ✅ W7-T1 through W7-T5: Production hardening tasks (see PRODUCTION_HYGIENE_master_plan.md for details)

**Archived:** 2026-06-24

---

**PREVIOUS OBJECTIVE (archived)**
**Security Compliance — Wave 6: User Aggregate + RBAC Management**

**Status:** ✅ COMPLETED — Branch `feature/wave6-user-rbac-mgmt`, merged to `main` (commit `2599c1b`)

**Completed Actions:**
1. ✅ W6-T1: `1_Shared/Domain/Aggregates/UserAggregate/DemoUser.cs` (AggregateRoot lifecycle: Create, Deactivate, Reactivate, ChangePassword, AssignRole, UpdateProfile)
2. ✅ W6-T2: `UserRole.cs`, `UserTenant.cs`, `PermissionGroup.cs`, `UserPermissionGroup.cs`, `UserEvents.cs`
3. ✅ W6-T3: Legacy `DemoUser`, `UserTenant`, `UserRole` in `Domain.cs` marked `[Obsolete]`
4. ✅ W6-T4: `IUserManagementService` + `UserManagementService` (Create/List/Get/Update/Deactivate/Reactivate/ChangePassword)
5. ✅ W6-T5: `IRoleAssignmentService` + `RoleAssignmentService` (assign/revoke roles, group membership, effective roles)
6. ✅ W6-T6: `IPermissionGroupService` + `PermissionGroupService` (create/update/list groups, add/remove roles)
7. ✅ W6-T7: `UserController` in `ShopERP/Controllers/` — tenant-scoped CRUD endpoints
8. ✅ W6-T8: `PermissionGroupController` in `ShopERP/Controllers/` — group CRUD endpoints
9. ✅ W6-T9: `UserCreatedEvent` handler dispatches welcome email via `INotificationService`
10. ✅ W6-T10: `UserManagement.razor` at `/admin/users`
11. ✅ W6-T11: `PermissionGroupManagement.razor` at `/admin/permission-groups` + NavMenu entries
12. ✅ W6-T12: `UserDomainTests` (7 cases) + `UserManagementServiceTests` (9) + `RoleAssignmentServiceTests` (6) + `PermissionGroupServiceTests` (7) = 29/29 PASS

**Archived:** 2026-06-24

---

**PREVIOUS OBJECTIVE (archived)**
**Security Compliance — Wave 5: Domain Refactor (God File Split) + Tenant Rich Domain Model + Tenant CRUD**

**Status:** ✅ COMPLETED — Branch `feature/wave5-tenant-mgmt`, merged into `feature/wave6-user-rbac-mgmt` base

**Completed Actions:**
1. ✅ W5-T1: `AggregateRoot` base + `IDomainEvent` interface added to `Common.cs`
2. ✅ W5-T2: `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs` (Rich Domain) + `TenantStatus.cs` + `TenantSettings.cs`
3. ✅ W5-T3: `TenantAggregate/TenantEvents.cs` — `TenantCreatedEvent`, `TenantSuspendedEvent`, `TenantDeactivatedEvent`
4. ✅ W5-T4: `record Tenant` in `Domain.cs` marked `[Obsolete]`; `TenantConfiguration.cs` updated; `IVanAnDbContext` + `VanAnDbContext.Tenants` now typed to new aggregate; integration tests migrated
5. ✅ W5-T5: `ITenantManagementService` + `TenantManagementService` (Create/List/Get/Update/Suspend/Reactivate/Deactivate)
6. ✅ W5-T6: `TenantController` in `ShopERP/Controllers/` — 7 endpoints; `SystemAdmin` policy added to Gateway + ShopERP
7. ✅ W5-T7: `TenantCreatedEvent` handler dispatches welcome email via `INotificationService`
8. ✅ W5-T8: `3_CoreHub/EmailTemplates/TenantWelcomeEmail.html` (Vietnamese template)
9. ✅ W5-T9: `TenantManagement.razor` at `/admin/tenants` — list/create/suspend/reactivate/deactivate; NavMenu entry for `SystemAdmin`
10. ✅ W5-T10: `TenantDomainTests` (13 cases) + `TenantManagementServiceTests` (10 cases)

**Archived:** 2026-06-24

---

**PREVIOUS OBJECTIVE (archived)**
**Security Compliance — Wave 4: RBAC Enforcement at Blazor UI Layer**

**Status:** ✅ COMPLETED — Branch `feature/wave4-rbac-ui`, merged to `main` (commit `5a6b441`)

**Completed Actions:**
1. ✅ W4-T1 through W4-T6: AuthorizeRouteView, policy-gated pages, NavMenu role gates, AccessDenied.razor, role-based login redirect, E2E tests

**Archived:** 2026-06-24

---

**PREVIOUS OBJECTIVE (archived)**
**Security Compliance — Wave 3: Report Export (Excel with EPPlus)**

**Status:** ✅ COMPLETED — Branch `feature/wave3-report-export`, merged to `main` (PR #42 merged wave 3)

**Completed Actions:**
1. ✅ W3-T1 through W3-T8: EPPlus export, ReportController, E2E tests, unit tests

**Archived:** 2026-06-24

---

**PREVIOUS OBJECTIVE (archived)**
**Security Compliance — Wave 2: Data Protection (Field-level Encryption)**

**Status:** ✅ COMPLETED — Branch `feature/wave2-data-protection`, merged to `main`

**Completed Actions:**
1. ✅ W2-T1: `AddDataProtection()` registered in `3_CoreHub/Program.cs` + `5_WebApps/ShopERP/Program.cs`, keys persisted to `./keys/`
2. ✅ W2-T2: `EncryptedStringConverter` — EF Core ValueConverter using `IDataProtector`
3. ✅ W2-T3: `EncryptedStringConverter` applied to `CustomerConfiguration.cs` — PhoneNumber, Email
4. ✅ W2-T4: `EncryptedStringConverter` applied to `LeadConfiguration.cs` + `FacebookLeadConfiguration.cs` — PhoneNumber, Email
5. ✅ W2-T5: EF Core Migration created — columns resized to `HasMaxLength(500)` for encrypted values
6. ✅ W2-T6: Data migration script — existing plain-text PII encrypted in dev DB
7. ✅ W2-T7: Integration tests — `CustomerEncryptionTests` 6+ cases PASS
8. ✅ W2-T8: `appsettings.Production.json` updated — `DataProtection:KeyDirectory`, `DataProtection:ApplicationName`

**Archived:** 2026-06-24

---

**PREVIOUS OBJECTIVE (archived)**
**Security Compliance — Wave 1: Notification Integration (Brevo Email + ESMS SMS)**

**Status:** ✅ COMPLETED — Branch `feature/wave1-notifications`, PR #39 merged to main

**Completed Actions:**
1. ✅ W1-T1: HttpClient used directly (no SDK — Brevo REST v3 + ESMS v4)
2. ✅ W1-T2: BrevoEmailService — IEmailService implementation, HTML support, error handling
3. ✅ W1-T3: EsmsNotificationService — ISmsService implementation, Unicode, 1 retry
4. ✅ W1-T4: CompositeNotificationService — INotificationService delegates to IEmailService + ISmsService
5. ✅ W1-T5: appsettings.Production.json + appsettings.Development.json with __REPLACE__ placeholders
6. ✅ W1-T6: 11/11 unit tests PASS (BrevoEmailServiceTests 5 + EsmsServiceTests 6)

**Archived:** 2026-06-24

---

**PREVIOUS OBJECTIVE (archived)**
**Security Compliance — Wave 0: JWT Authentication Foundation**

**Status:** ✅ COMPLETED (2026-06-23) — PR #38 merged to main

**Problem:** Plain-text password in Login.cshtml.cs, no JWT Bearer on Gateway, no BCrypt password hashing

**Solution:** Stateless JWT (HS256, 8h), BCrypt work factor 12, dual-scheme auth (Cookie+JwtBearer)

**Completed Actions:**
1. ✅ W0-T1: JwtBearer 8.0.8 + BCrypt.Net-Next 4.0.3 added to Central Package Management
2. ✅ W0-T2: IJwtTokenService + JwtTokenService created in 3_CoreHub/Services/
3. ✅ W0-T3: Login.cshtml.cs migrated to BCrypt.Verify + JWT cookie issue
4. ✅ W0-T4: AddJwtBearer added to Gateway/Program.cs (Cookie default + JwtBearer secondary)
5. ✅ W0-T5: ShopERP seed data: 5 DemoUsers with BCrypt hash work factor 12
6. ✅ W0-T6: 9 unit tests — JwtTokenServiceTests (6) + LoginPasswordTests (3) = 9/9 PASS
7. ✅ W0-T7: DevLoginController returns JWT token in response for E2E Bearer tests
8. ✅ W0-T8: CI fixes: ITenantProvider mock in ComponentTestBase (26/26 ShopERP tests) + flaky TamperedSignature test

**Archived:** 2026-06-24

---

**PREVIOUS OBJECTIVE (archived)**
**Fix Integration Tests: Value Object Mapping (EF Core Configuration)**

**Status:** ✅ COMPLETED (2026-06-15)

**Problem:** 89 integration tests failing due to EF Core mapping errors for strongly-typed ID value objects (ProductId, IngredientId, LeadId, etc.)

**Solution:** Created 14 dedicated IEntityTypeConfiguration<T> files with proper HasConversion for all value objects

**Entities Fixed (14 total):**
- ElectronicInvoice, Order, Customer (Batch 0)
- Product, Ingredient, Recipe, Inventory (Batch 1)
- Lead, FacebookLead (Batch 2)
- OrderItem (Batch 3)
- Shop, DemoUser, SocialCampaign, LoyaltyRewards (Batch 4)

**Pattern Applied:**
```csharp
builder.Property(e => e.ValueObjectId)
    .HasConversion(id => id.Value, value => new TypeName(value))
    .IsRequired();
```

**Final Architecture Flow:**
```
KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite Database
     ↓                  ↓                  ↓
  HttpClient   ProductsController   ProductsController
                (forward)         (query IVanAnDbContext)
```

**Completed Actions:**
1. ✅ Rolled back QrMenu.razor to use Gateway API (HttpClient) instead of IVanAnDbContext
2. ✅ Removed seed data from KhachLink Program.cs
3. ✅ Removed seed data from CoreHub Program.cs (Class Library)
4. ✅ Created ProductsController in ShopERP with IVanAnDbContext injection
5. ✅ Added seed data (5 products) to ShopERP Program.cs with TenantId: 00000000-0000-0000-0000-000000000001
6. ✅ Created Gateway ProductsController to forward requests to ShopERP via HttpClient
7. ✅ Fixed ShopERP DI issues (IAuditTrailService, IAuditLogRepository, ITenantProvider)
8. ✅ All services running: ShopERP (5003), Gateway (5001), KhachLink (5002)
9. ✅ API verification: curl http://localhost:5001/api/products?tenantId=... returns 200 OK with 5 products
10. ✅ Architecture tests: 7/7 PASS
11. ✅ Playwright E2E tests: 15 passed, 2 skipped

**Key Files Modified:**
- `5_WebApps/ShopERP/Controllers/ProductsController.cs` - API endpoint with IVanAnDbContext
- `5_WebApps/ShopERP/Program.cs` - Seed data + DI registrations
- `5_WebApps/ShopERP/Services/TenantProvider.cs` - Local implementation
- `2_Gateway/Controllers/ProductsController.cs` - HttpClient forward to ShopERP
- `5_WebApps/KhachLink/Pages/QrMenu.razor` - HttpClient API calls

**Archived:** 2026-06-24
