# Project State Archive

> **Mục đích:** Lưu trữ các wave đã hoàn thành để giảm file size của project_state.md
> **Archived Date:** 2026-06-24

---

## Archived Waves

**PREVIOUS OBJECTIVE (archived)**
**Wave 8–16 Production Hygiene + Wave 16 Production Hardening + Pre-Wave 17 Fixes**

**Status:** ✅ COMPLETED (2026-06-24 → 2026-06-28) — All merged to `main`

**Completed Actions:**
1. ✅ Wave 8: Upgrade Dashboard to Sitemap with Authentication (commit `d088739`)
2. ✅ Wave 9: Cleanup Orphan Controller — deleted `ShopERP/Controllers/CustomersController.cs`
3. ✅ Wave 10: Cleanup Duplicate Interfaces — deleted `ISocialCampaignService`/`ILoyaltyRewardsService` duplicates in ShopERP
4. ✅ Wave 11: Cleanup Invalid Framework Files — deleted `SocialCampaignManager.cshtml`, `KhachLink/wwwroot/index.html`
5. ✅ Wave 12: Fix API Authorization — `[Authorize(Policy="RequireTenantAccess")]` on Gateway + ShopERP endpoints
6. ✅ Wave 13: Replace Hardcoded Data — public `GET /api/products?shopId=` + `ProductHttpService` via Gateway
7. ✅ Wave 14: HMAC Request Signing — `HmacSigningMiddleware` + `ApiKey` entity + `IApiKeyManagementService`
8. ✅ Wave 15: KhachLink Page Cleanup + Blazor Web App routing (commit `26abd83`)
9. ✅ Wave 16: Production flow hardening — Campaign, Dashboard TenantId, VoiceCommand
10. ✅ Production fixes: resolved 502 errors (ShopERP stale volume + KhachLink 502)
11. ✅ Customer API Integration Tests Fix — 100% success rate (2026-06-24)
12. ✅ All integration tests: 144/144 PASS (2026-06-28)

**Archived:** 2026-06-28

---

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

---

## Archived 2026-07-08 (from project_state.md reduction)

### BUCKET A GUEST CHECKOUT FORM + POSTGRESQL MIGRATION FIX — COMPLETE ✅
**Commits:** `310f3da` + `8867dbc` on `main`

- **Guest checkout form:** `Order.SetCustomerInfo` + `CreateOrderCommand` + `OrderService` fix (CustomerId null) + `Checkout.razor` rewrite + `VanAInput @onchange→@oninput`
- **PostgreSQL migration:** `DesignTimeDbContextFactory` auto-detect provider + `PushSubscriptionConfiguration` `newid()→gen_random_uuid()` + regenerate `InitialCreate` with PG-native types + 36 tables
- **Tests:** Core.Tests 979/979 PASS, GuestCheckout 3/3 PASS, E2E qr-payment-ui 6/6 PASS

### E2E FIX — 4 PRE-EXISTING FAILURES RESOLVED ✅
**Commit:** `24718b8` on `main`

- Tests 1-3: selector bug (`#qrPaymentModal .modal-content` instead of `#qrPaymentModal`)
- Test 4: IdentityModel v7.1.2 — added `IssuerSigningKeyResolver` to Gateway JWT
- Domain defect: `Order.SetCustomerInfo()` missing — user-approved fix

### STREAM G: SAAS PRODUCTION HARDENING — COMPLETE ✅ (W0-W7, W8 pending)
**Master plan:** `docs/AI/tasks/saas_production_hardening_master_plan.md`

- **Sprint 1 (W0-W3):** Gateway Option B, secrets hardening, 9 legacy packages removed + SDK 8.0.422, CI restore. 1133/1133 PASS.
- **Sprint 2 (W4-W7):** UI test coverage (44 bUnit), period closing persist + auth hardening, e-invoice rewrite (Viettel 18 tests + MISA 18 tests), tech debt cleanup + Docker hardening. 1152/1152 PASS.
- **W6-T6 deferred:** Staging tests blocked by Viettel/MISA sandbox credentials.
- **W8 pending:** Final regression + `saas-production-v1.0` tag.

### STREAM F: VAS ENTERPRISE REPORTS — COMPLETE ✅ (W0-W9, 10 waves)
**Master plan:** `docs/AI/tasks/vas_enterprise_reports_master_plan.md`

- W0: Order→Accounting writer fix (9/18 issues)
- W1: Data audit + seed (31 journal entries, 5 account code fixes)
- W2: Domain records (3 enums, BCTC records, D9 HKD↔DN conversion)
- W3: Account code map (124 accounts: TT 133=51, TT 99=73, TT 58=0)
- W4: 4 report services (BS+IS+CF+TB, 25 tests)
- W5: 4 API endpoints
- W6: 5 Blazor UI pages (29 bUnit tests)
- W7: 29 numeric assertion tests
- W8: Feature flag + TenantType + conversion service (15 tests)
- W9: Regression (1114/1114 PASS, 45 regression tests)

### STREAM D: HKD BOOK ACCOUNTING FIX — COMPLETE ✅ (W0-W8, 12 waves)
- TT 152/2025 compliance + 2026 regulatory fix
- 7 HKD book templates (S1a, S2a-S2e, S3a) generate NumericValues
- DOCX/XLSX export, E2E + arch tests
- 7 pre-existing bugs fixed in Wave 7 (DI, circular dependency, unmapped Period, GUID parse, null logger, legacy overload)

### STREAM C: SHOPERP UI FIX — COMPLETE ✅ (W0-W6, 6 waves)
- 23 .razor files fixed, 14 dead pages → 0, 18 unstyled → 0, 3 broken layouts → 0
- UI Platform compliance, CSS isolation, AdminLayout, governance cleanup

### STREAM B: E2E TEST CLEANUP — COMPLETE ✅ (W0-W8, 8 waves)
- 7 anti-patterns fixed across 20 spec files
- 59 decorative `reporter.pass()` removed, auth patterns fixed, anti-schema tests deleted

### ORDER LIFECYCLE STREAM — COMPLETE ✅ (W-1→W5 + edge cases)
- Sync mechanism (Outbox+NATS), SignalR, Kitchen→Ready, Admin UI, Payment UI, polling, tests
- 8 edge case tests (idempotency, race condition, disconnected, partial completion, invalid payload)

### PLAYWRIGHT E2E GOLDEN TEST FIXES (W6) — COMPLETE ✅
**Commit:** `fd7b038` — 21/22 PASS (1 deferred → resolved by Bucket A)

- 5 buckets (A-E): test.skip, timeout, webhook tenant, Gateway status endpoint, VietQR validation
- 13 VietQrService unit tests, 14 modified files

### PRE-EXISTING DEFECTS (found, some addressed by Platform SystemAdmin plan)
1. **Blazor circuit crash** on `/`, `/sitemap`, `/admin/users` — `Authorization requires cascading parameter` — cascade timing issue
2. **DevLoginController role mismatch** — `/admin/users` requires "Owner" but `/dev/login/systemadmin` issues "SystemAdmin" → Platform SystemAdmin plan addresses this (policy updates)
3. **Dead code:** `CustomerPage.ts` loyalty methods unreferenced after Stream B Wave 4

### OLDER HISTORY (2026-07-02 and before)
- ShopConfig Refactor 3 phases, Tenant Onboarding 6 waves, Architecture Test Fixes, CI/CD Hotfix
- See git log for commit-level details
