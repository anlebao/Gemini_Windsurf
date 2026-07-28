# Project State Archive

> **Mục đích:** Lưu trữ các wave đã hoàn thành để giảm file size của project_state.md
> **Most Recent Archive:** 2026-07-24

---

## Archived 2026-07-24 (from project_state.md reduction)

### Phase 5 — KhachLink PWA Push Notification + Loyalty Auto-Push + Campaign Bulk Push + Click Tracking (COMPLETE 2026-07-24)

**17 Success Criteria all achieved:**
- SC1 VAPID verified · SC2 CampaignPushJob + migration · SC3 LoyaltyPointsChanged outbox · SC4 SendLoyaltyPointsChangedNotificationAsync · SC5 SendBulkNotificationAsync · SC6 CustomerSegmentationService · SC7 Customer.UpdateOrderStats · SC8 auto-push order status · SC9 Profile.razor toggle + unsubscribe · SC10 send-push + push/send endpoints · SC11 DELETE subscribe · SC12 CampaignsAdmin UI · SC13 build + guard-check PASS · SC14 RV VPS Android · SC15 PushNotificationDelivery + migration · SC16 POST /api/push/track + SW notificationclick beacon · SC17 CampaignsAdmin Sent/Clicked/CTR stats

**Session 1 (5.1-5.4) Implementation:**
- **5.1 Domain + EF + Migration:** CampaignPushJob + PushNotificationDelivery entities, Customer.UpdateOrderStats() method, EventTypes.LoyaltyPointsChanged, 2 EF configs, 2 migrations (PG + SQLite), 6 DbSets updated.
- **5.2 Loyalty outbox + auto-push:** LoyaltyRewardsService enqueues outbox + publishes NATS "loyalty.points.changed" on AddPoints/SubtractPoints. PushNotificationService.SendLoyaltyPointsChangedNotificationAsync. PushNotificationBackgroundService subscribes "loyalty.points.changed".
- **5.3 Order status auto-push SC8:** Already wired from Wave 9 — no code changes needed.
- **5.4 Customer segmentation + bulk push + stats:** CustomerSegmentCriteria record + ICustomerRepository.GetBySegmentAsync + CustomerRepository impl + CustomerSegmentationService + SendBulkNotificationAsync + UpdateOrderStats.

**Session 2 (5.5-5.9) Implementation:**
- **5.5 Gateway admin endpoints:** POST /api/campaigns/{id}/send-push, POST /api/push/send, POST /api/push/track, DELETE subscribe.
- **5.6 KhachLink Profile.razor toggle + full unsubscribe:** pwa.js subscribe/unsubscribe, PWAService, NotificationsController DELETE.
- **5.7 ShopERP Admin UI:** CampaignsAdmin.razor segment builder + CampaignPushJob history + Sent/Clicked/CTR stats.
- **5.8 Tests + Build + RV VPS:** All PASS.
- **5.9 Click tracking:** PushNotificationDelivery record on send + SW notificationclick beacon + POST /api/push/track update Status=Clicked.

### Loyalty L-A — Configurable Points Formula + Guard Fix (COMPLETE 2026-07-24)

**Commits:** `aae5fba2` (feat), `8b8f97bc` (docs).
- `LoyaltyPointsConfig` record (PointsRate=0.1, MinPointsPerOrder=10, MaxPointsPerOrder=null, AwardOnAllOrders=true) in `1_Shared/Domain.cs` (config DTO, NOT entity, no migration).
- Bound via `IOptions<LoyaltyPointsConfig>` from `appsettings.json` `LoyaltyPoints` section (Gateway + CoreHub + ShopERP).
- `OrderWorkflowService.HandleOrderCompletedAsync` updated: inject `IOptions<LoyaltyPointsConfig>`, replace hardcoded `10% + Math.Max(10, ...)` with `(int)(order.TotalAmount * config.PointsRate)` clamped to `[Min, Max]`.
- `AwardOnAllOrders` replaces TrackingCode guard (true = all orders get points, false = only orders with TrackingCode).
- `OrderWorkflowServiceTests` updated (orphaned file at `6_Tests/` root, not compiled by any project — noted, not fixed).
- Build 0 errors. guard-check ALL PASSED. CD success. VPS RV PASS.
- **Gap identified 2026-07-24:** config is appsettings.json-only, NO admin UI for owner. L-C WS-A will fix (extend ShopFeatureSettingsDto + ShopFeatures.razor).

### Loyalty L-B — Redemption System (COMPLETE 2026-07-24)

**Commits:** `8f6162a5` (feat + ACID fix + DDD fix), `88a74ab6` (nav + sitemap), `891869eb` (docs).
- 3 new entities in `1_Shared/Domain.cs`: `RedemptionCatalogItem` (admin-managed redeemable products: ProductName, Description, ImageUrl, PointsRequired, StockCount, ValidFrom/To, IsActive, VoucherExpiryDays, IsAvailable computed), `RedemptionRecord` (tracks customer redemption: CustomerId, CatalogItemId, VoucherId, PointsSpent, Status [Pending/Fulfilled/Cancelled/Expired], RedeemedAt, FulfilledAt, CancelledAt, Notes), `Voucher` (issued upon redemption: VoucherCode unique, QrCodeData PNG base64, ExpiresAt, Status [Active/Used/Expired]).
- 3 EF configs in `3_CoreHub/Infrastructure/Configurations/`: RedemptionCatalogItemConfiguration, RedemptionRecordConfiguration, VoucherConfiguration.
- DbSets added to IVanAnDbContext + VanAnDbContext + ShopERPDbContext.
- ShopERP SQLite migration `20260724042917_AddRedemptionSystem` (3 tables).
- `IRedemptionRepository` (3_CoreHub/Domain/Repositories) + `RedemptionRepository` (3_CoreHub/Infrastructure/Repositories) — catalog CRUD + records + vouchers + SaveChangesAsync.
- `IRedemptionService` (1_Shared/Services) + `RedemptionService` (3_CoreHub/Services):
  - `RedeemAsync(customerId, catalogItemId)`: verify catalog available → ACID transaction (IVanAnDbContext.BeginTransactionAsync) → SubtractPointsAsync (IdentityLevel gate, same DbContext → nested savepoint) → create RedemptionRecord (Pending) → create Voucher (with QR PNG via QRCoder) → link voucher to record → decrement stock → commit. If any step fails → rollback (atomic).
  - `FulfillAsync(voucherCode, notes)`: admin scan voucher code → mark Voucher.Used + Record.Fulfilled.
  - `CancelAsync(recordId, reason)`: cancel Pending record → refund points (AddPointsAsync) → expire voucher.
- DI registrations in ShopERP Program.cs.
- `RedemptionController` (ShopERP): admin CRUD catalog + fulfill + cancel + history + customer redeem + my vouchers/redemptions (X-Customer-Token auth).
- `RedemptionController` (Gateway): forwards customer-facing endpoints to ShopERP.
- `RedemptionCatalog.razor` (KhachLink `/rewards`): browse catalog + redeem button (disabled if insufficient points/unavailable) + voucher QR modal (code + QR PNG + expiry).
- `RedemptionCatalogAdmin.razor` (ShopERP `/admin/redemption-catalog`): catalog CRUD (ProductName, Description, ImageUrl, PointsRequired, StockCount, ValidTo, VoucherExpiryDays) + active toggle + delete.
- `RedemptionHistory.razor` (ShopERP `/admin/redemption-history`): fulfill voucher by code + notes + cancel pending record (refund) + recent records table (customer, points, status badge, date, voucher code).
- Nav links: AdminLayout sidebar (2 links) + NavMenu SystemAdmin section (2 links) + Sitemap card (2 links) + KhachLink header (gift icon `/rewards` + gem icon `/my-loyalty`).

**Code Review Fix (commit `8f6162a5`):**
- ACID: Wrapped RedeemAsync in single transaction via IVanAnDbContext.BeginTransactionAsync. SubtractPointsAsync uses same scoped DbContext → nested savepoint. If any step fails → rollback undoes points deduction + record + voucher (atomic). Fixes data inconsistency risk (previously each step committed independently).
- DDD: Removed BeginTransactionAsync from IRedemptionRepository (Domain layer). Domain interface must not reference EF Core types (VA-DDD-002 compliance). Transaction management moved to Service layer (allowed to depend on Infrastructure).
- Architecture test: RedemptionController added to Gateway [Authorize] exempt list (consistent with 8+ existing customer-facing controllers: LoyaltyController, CustomerOrdersController, etc. Auth enforced at ShopERP layer via CustomerTokenService.ValidateToken with IDataProtector — cryptographic, expiry check).

**VPS RV 13/13 PASS (2026-07-24):**
1. 8 containers healthy (vanan-khachlink, vanan-shoperp, vanan-gateway, vanan-seq, vanan-certbot, vanan-nginx, vanan-postgres, vanan-nats).
2. KhachLink `/rewards` 200.
3. KhachLink `/my-loyalty` 200.
4. KhachLink header icons (gift + gem) in WASM bundle (2 matches via grep).
5. ShopERP `/admin/redemption-catalog` 200 (auth via sysadmin@vanan.vn, content "Redemption Catalog" verified).
6. ShopERP `/admin/redemption-history` 200 (content "Lịch sử đổi điểm" verified).
7. Sitemap has redemption links (HTML content check True/True/True).
8. NavMenu has redemption links (HTML content check True/True/True).
9. Gateway `GET /api/redemption/catalog/active` 200 (returns `[]`).
10. ShopERP `POST /api/redemption/catalog` 201 (created "Ca phe mien phi" 500pts).
11. ShopERP `POST /api/redemption/catalog` 201 (created "Tra sua" 1000pts stock=50).
12. Gateway `GET /api/redemption/catalog/active` 200 (returns 2 items after create).
13. ShopERP `GET /api/redemption/history` 200 (returns `[]`).
- Migration applied: "SQLite database migrated" log + queries run (RedemptionCatalogItems + RedemptionRecords tables exist).
- No errors in ShopERP logs (only EF Core SQL logging).

### Loyalty L-C Task Card Review (2026-07-24) — 3 gaps added to task card

User review of `docs/AI/tasks/loyalty_phase_c_task_based_awards_task_card.md` found 3 missing workstreams. Task card updated:

**WS-A — Owner config UI for loyalty formula (L-A gap fix):**
- `LoyaltyPointsConfig` currently appsettings.json-only — owner cannot self-edit.
- Fix: extend `ShopFeatureSettingsDto` + `ShopFeatureSettingsEntity` (per-tenant, DB-backed) with 4 new fields: Loyalty_PointsRate (decimal, 0.1), Loyalty_MinPointsPerOrder (int, 10), Loyalty_MaxPointsPerOrder (int? null), Loyalty_AwardOnAllOrders (bool, true).
- Update ShopFeatureSettingsService read/write + ShopFeatures.razor UI section "Công thức điểm thưởng".
- Update OrderWorkflowService to read from IShopFeatureSettingsService (per-tenant) with IOptions fallback (global default).
- Migration: add 4 columns to ShopFeatureSettings.

**WS-B — Customer mission tracking UI audit:**
- Existing: `/my-loyalty` (LoyaltyCard.razor) has PointBalance + tier badges + history list (+/− icons + reason + timestamp). `/profile` has name + tier + points + identity level + push toggle. `/rewards` (L-B) has catalog + redeem + QR. `/my-orders` has order history.
- Missing (added to task card): `/missions` page (SC11), Profile.razor birthday input (SC12), mission proof submit form for Facebook/TikTok share (SC15 NEW), MissionCompletion history in `/missions` page (SC16 NEW).

**WS-C — Notification rules for loyalty events:**
- Existing: PushNotificationService.SendLoyaltyPointsChangedNotificationAsync fires on every AddPoints/SubtractPoints via NATS + Outbox.
- Missing (added): 5 per-tenant toggles in ShopFeatureSettingsDto (Notify_MissionCompleted, Notify_BirthdayBonus, Notify_RedemptionFulfilled, Notify_RedemptionCancelled, Notify_VoucherExpiringSoon) + VoucherExpiryNotifyHours (int, 24).
- MissionService.CompleteMissionAsync → check Notify_MissionCompleted → push mission-specific reason.
- RedemptionService.FulfillAsync → check Notify_RedemptionFulfilled → new SendRedemptionFulfilledNotificationAsync.
- RedemptionService.CancelAsync → check Notify_RedemptionCancelled → push refund reason.
- NEW VoucherExpiryReminderJob (HostedService, daily) → query vouchers expiring within VoucherExpiryNotifyHours → push reminder.
- UI: ShopFeatures.razor new section "Thông báo điểm thưởng" with 5 toggles + expiry hours input.
- SC count: 14 original + 4 new (SC15-18) = 18 total.

### Featured Product Picker + Order Status Unification (COMPLETE + VPS VERIFIED 2026-07-23)

**Commit:** `17dab107`. 2 fixes trong cùng commit:

**Featured Product Picker (8 files):**
- `FeaturedProducts.razor`: Product picker dropdown (load từ `ShopERPDbContext.Products`, filter `TenantId + IsActive`); auto-fill snapshot (DisplayName=Product.Name, DisplayPrice=Product.Price, VatRate=Product.VatRate); lock Price+VAT (disabled); "Refresh from Product" button (edit mode); tenant selector ở đầu modal.
- Tenant dropdown change → reload product list. Product dropdown change → auto-fill snapshot.
- Eliminates auto-created stub products (Description='Synced from Gateway') in tenant owners' SQLite.

**Order Status Unification (7 files):**
- `OrderWorkflowService.cs`: Thêm "confirmed" vào normal flow state machine: `confirmed → [preparing, cancelled, completed]`.
- `IOrderService.cs` + `OrderService.cs`: Mark `UpdateOrderStatusAsync` `[Obsolete]` — redirect doc sang `OrderWorkflowService.TransitionStatusAsync`.
- `KitchenService.cs`: Inject `IOrderWorkflowService?`; delegate Ready transition sang `TransitionStatusAsync` (fallback direct mutation khi null — test scope).
- `Orders/Index.razor`: ConfirmOrder → `OrderWorkflowService.TransitionStatusAsync`.
- `OrdersController.cs` (ShopERP + Gateway): UpdateOrderStatus → delegate sang `OrderWorkflowService.TransitionStatusAsync`. Gateway `UpdateStatusRequest` thêm `Reason` field.

**Cleanup script:** `scripts/cleanup-featured-product-stubs.sql` — delete stubs (0 OrderItem refs) + deactivate stubs (with OrderItem refs).

**VPS Verification (2026-07-23 — DEFINITIVE, post-`17dab107` deploy):**
1. **Featured Product Picker UI** ✓ — Page render 200 + DLL verify 3 methods deployed + tạo featured product với ProductId thật 201.
2. **Refresh from Product** ✓ — PUT update price+VAT keep DisplayName 200.
3. **Order status flow** ✓ — `pending→confirmed→preparing→ready→completed` all 204, invalid `completed→preparing` rejected 404. ShopERP logs: Outbox event `OrderStatusChanged` published qua NATS `vanan.shoperp.order.status.changed`.
4. **Cleanup stub products** ✓ — 18→12 stubs (6 deleted + 12 deactivated, 0 active stubs remaining). Backup at `/tmp/vanan_shoperp_backup_.db`.

**Pre-existing issues (resolved 2026-07-23):**
- Issue 2 (ShopERP impersonation API 500) — FIXED + VPS VERIFIED. Refactor `AdminController.Impersonate` delegate tenant validation qua Gateway HTTP (`GET /api/v1/tenants/{id}`). VPS RV 3/3 PASS.
- Issue 1 (Gateway OrdersController reject SystemAdmin JWT) — AUTO-RESOLVED via Issue 2 fix (impersonated JWT has real tenant_id GUID).
- Issue 3 (ShopERP→PG status sync) — code đúng (DataSyncSubscriber subscribe `vanan.shoperp.>` + `case "order.status.changed"`), "không sync" là runtime cause (test order không có trong PG / NATS disconnect / tenantId mismatch).

---

## Archived Waves

**PREVIOUS OBJECTIVE (archived)**
**QuickSetup + Product Management — Phases 4–6**

**Status:** COMPLETED (2026-07-17) — merged to `main`

**Completed Actions:**
1. Phase 4: implemented the Owner-only `/products` management page with UI Platform grid, create/edit, lifecycle actions, image upload, navigation, and `CurrencyHelper`.
2. Phase 5: implemented product QR viewing plus single and selected-product batch printing.
3. Phase 6: added focused production E2E specs for product CRUD, QR/print, and QuickSetup flows.

**Key commits:** `a9766442` (Phase 4), `fdb25eb3` (Phase 5), `69a3642f` (Phase 6).

**Archived:** 2026-07-17

---

**PREVIOUS OBJECTIVE (archived)**
**Single-Identity Refactor (Hướng A) — all affected entities + VPS crash fix**

**Status:** COMPLETED + VPS VERIFIED (2026-07-17) — merged to `main`

**Completed Actions:**
1. Extended the single-identity pattern to `Product`, `Customer`, `OrderItem`, `Ingredient`, and `Recipe`: constructors synchronize `BaseEntity.Id` with the business-key value object.
2. Ignored the five business-key value objects in EF Core and migrated SQLite/PostgreSQL schemas to remove their duplicate columns.
3. Replaced persisted-entity `.BusinessKey.Value` reads and filters with `Id`.
4. Fixed the ShopERP production 502 by checking seed products by `Id`; removed migration exception swallowing so startup fails fast.
5. Removed the duplicate PostgreSQL product and corrected its `OrderItem` reference; deployed manually after reclaiming VPS Docker disk space.
6. Verified all production containers plus `khachvip.online/`, `/health`, and `diemthuong.khachvip.online/` return HTTP 200.

**Key commits:** `b8584a8a` through `e70c91a7`.

**Archived:** 2026-07-17

---

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

---

## Archived 2026-07-15 (from project_state.md reduction)

### TIERED AUTH PHASE 1-3 + PRODUCTION DEPLOY (2026-07-12 → 2026-07-13)

**Tiered Auth Master Plan + 7 Task Cards (2026-07-12):**
- Created `tiered_auth_loyalty_master_plan.md` (7 phases, dependency graph, cost analysis — 96% saving)
- 7 task cards: phase0_domain, phase1_google_oauth, phase2_verification_gate, phase3_khachlink_social_ui, phase4_facebook_oauth, phase5_zalo_zns, phase6_e2e_tests
- Strategy: Social Login (free) → Zalo ZNS OTP (300đ) → eSMS fallback (1.000-1.200đ)

**Phase 1 — Google OAuth (2026-07-13):**
- `ISocialAuthService` + `GoogleAuthService` (OAuth code exchange + ID token verification) + `SocialAuthController` + DI + YARP route
- Google token endpoint snake_case JSON fix (`[JsonPropertyName]` on `GoogleTokenResponse`)
- Production wiring: `appsettings.Production.json` env var placeholders + `docker-compose.prod.yml` env vars
- Dev secret rotation: scrubbed plain-text secret from `appsettings.Development.json` → `dotnet user-secrets`
- Test fix: `AllShopErpControllers_MustHaveAuthCoverage` — added `HasClassLevelAllowAnonymous` skip
- Commit `b4c6aeb`

**Phase 2 — Verification Gate (2026-07-13):**
- `LoyaltyRewardsService.SubtractPointsAsync` — throws `IdentityLevelNotSufficientException` khi `IdentityLevel < Verified`
- Gate chỉ cho redeem, KHÔNG cho earn. Bug fix: `catch (IdentityLevelNotSufficientException) { rollback; throw; }` trước generic catch
- 3 API endpoints: `POST /api/loyalty/redeem`, `POST /api/customer-identity/upgrade/send-otp`, `POST /api/customer-identity/upgrade/verify-otp`
- 6 TDD tests in `LoyaltyRewardsServiceVerificationGateTests.cs`

**Phase 3 — KhachLink UI (2026-07-13):**
- `SocialAuthHttpService.cs` (HTTP client cho upgrade + redeem)
- `Login.razor` — Google login button + OAuth callback handler
- `IdentityUpgradeModal.razor` — 3-step OTP upgrade flow (Intro → OtpSent → Success)
- `Profile.razor` — IdentityLevel badge + upgrade prompt
- `LoyaltyCard.razor` — redeem section + 403 → show upgrade modal
- Commits `06d08d1e`, `f419d149`

**Production Deploy + Online RV (2026-07-13):**
- 7 CD runs to fix: missing `Directory.Packages.props` COPY, stale GHA cache, missing sentinel env vars, `[controller]` token route mismatch
- Final deploy: local build + SCP to VPS. PostgreSQL schema reset.
- **Online RV 14/14 PASS** on `khachvip.online`
- Commits: `a9cf334b`, `c7dd67bf`, `40392310`, `10e83f8f`, `1a9bbed4`, `23b8ef24`, `11cf6af6`, `4bd66bc1`

### KHACHLINK FULL FLOW WAVES 0-4 (2026-07-11 → 2026-07-12)

**Master Plan + Wave 0 (2026-07-11):**
- 3 subagents verified codebase: 11 tech debt items (TD-KL-01..14)
- Master plan `khachlink_full_flow_master_plan.md` (5 waves, 43 tasks)
- Wave 0: Module Toggle Infrastructure — 6 toggles + Shop Settings UI + API + KhachLink HTTP service
- 13 files (7 new + 6 modified), EF migration, 2 runtime issues fixed (missing migration, LINQ Pattern #1)
- RV1-RV12 PASS. Merge `8edea1b`. Live RV Protocol added to all Wave 1-4 task cards.

**Wave 2 (2026-07-11):**
- Payment Flow + Kitchen UI + Polling 3s. 12 files (1 new + 11 modified)
- Pre-existing bug fix: `GetOrderByIdForPublicTrackingAsync` — `IgnoreQueryFilters` for anonymous endpoint
- RV1-RV10 PASS. Merge `49c1911`.

**Wave 3 (2026-07-12):**
- Voice Note STT-only + TTS Kitchen + QR Table Number. 9 files (1 new + 8 modified)
- `tts-reader.js` (Web Speech API), `QRCodePayload.TableNumber`, Domain `[Obsolete]` on audio blobs
- RV1-RV12 PASS. Merge `a1b2c3d`.

**Wave 4 — Configurable Polling Interval (2026-07-12):**
- `PollingIntervalSeconds` (default 15, range 5-120, `Math.Clamp`). 8 files modified + 1 new test file
- EF migration `AddPollingIntervalSeconds`. E2E 8/8 PASS (26.3s). Merge to main.

### ACCOUNTING POSTGRESQL ONLINE — 3 WAVES (2026-07-09 → 2026-07-10)

**Master Plan + Debt Audit (2026-07-09):**
- ADR-001 violation since 2026-06-03 (commit `957ac95`): accounting on SQLite instead of PostgreSQL
- 10 services + 3 repos affected. Roslyn Analyzers: 9 dead. Debt Tier 4 recorded.
- User chose Option B (split interface, compile-time safety) over Option A (throw stubs)

**Wave 1 — Interface Split (2026-07-09):**
- `IAccountingDbContext` (6 DbSets), removed from `IVanAnDbContext` (19 business-only)
- `VanAnDbContext` implements both, `ShopERPDbContext` business-only
- 11 SWAP + 3 DUAL-INJECT files. DI: `VanAnDbContext` UseNpgsql + `IAccountingDbContext` registered
- Commit `9d589bd`. Branch `feature/accounting-pg-wave1-interface-split`.

**Wave 2 — Residual (2026-07-10):**
- `ConnectionStrings__AccountingConnection` env var to 3 compose files
- Uses `${POSTGRES_DB:-VanAnCoreHub}`. `.env.example` updated.

**Wave 3 — Architecture Tests + Verify (2026-07-10):**
- 4 Architecture Tests: Rule J (accounting services inject IAccountingDbContext), K (ShopERPDbContext no accounting DbSets), L (docker-compose AccountingConnection), M (ShopERP UseNpgsql)
- Fixed Rule C (ShopERP exempt). Fixed 6 integration test factories.
- 1223/1223 PASS (Release). Guard-check ALL PASSED.

**Docs Sync + Tier 5 Debt (2026-07-09):**
- User rejected "Option C graceful degradation" for Edge mode (7 points)
- Approved simpler: env var to 3 compose files, no code changes
- Tier 5 debt: true offline Edge accounting via Gateway HTTP API. Commit `ebda286`.

### DOCKER CONFIG FIX + DEPLOYMENT MODES (2026-07-09)
- Port swap fix (gateway=5001, shoperp=5003, khachlink=5002)
- ShopERP 500 crash: SQLite volume stale → `DesignTimeDbContextFactory` + `MigrateAsync`
- KhachLink 500 crash: missing `Gateway__BaseUrl` → added env var
- Dual Deployment Modes (SaaS + Edge) recorded in Section 5a
- Commits `9b2d209`, `b9ed4a2`

### ENTRY POINT CHECK + FIXES (2026-07-10 → 2026-07-11)
- Full stack local Debug boot: Docker + PostgreSQL + NATS + Gateway + ShopERP + KhachLink
- 150+ routes extracted from 45 controllers. 57 entry points tested.
- 4 error groups fixed: VAS 500s (TenantType null + Forbid misuse), Gateway JWT scheme, SystemAdmin impersonation endpoint
- `SystemAdmin 500s fixed: EInvoice DI block + tenant seeding with self-reference
- Tests: Arch 38/38, Core 983/984, Integration 201/201

### PLATFORM SYSTEMADMIN (2026-07-08)
- **Planning:** 2 role systems investigated (`UserRole` tenant-scoped vs `PlatformRole` cross-tenant). User chose pattern 2 lớp. Commit `792cc3f`.
- **Implement:** T1-T9: PlatformUser entity, PlatformUserConfiguration, 3 DbContext DbSet, EF Migration, PlatformUserLoginService (BCrypt + JWT), PlatformUserLoginController, DI + 3 policy updates + seed. Commit `dde219e`.
- **Review + F1-F5 Fix:** 5 deviations fixed (AllowAnonymous, idempotent test, unit tests, config password, AuditTrail role). EDR-1..EDR-8. Access Matrix master plan. 1174/1174 PASS.

### SDK 8.0.422 + TRIAGE + BUCKET A (2026-07-07)
- 14 commits: SDK to system path (CVEs patched), 5 pre-existing issues triaged, qr-payment-ui 6/6 PASS, guest checkout + PostgreSQL migration, 21/22 golden tests PASS

---

**PREVIOUS OBJECTIVE � KhachLink Theme Customization � COMPLETE (2026-07-22)**

Feature cho ph�p SysAdmin ch?n 1 trong 5 theme (Classic, Modern, Teen, Lady, Premium) cho m?i tenant. Theme persist v�o PostgreSQL, truy?n qua API d?n KhachLink, render cho c? KhachLink pages (Home, Cart, Checkout) v� Store profile page (/store/{slug}).

### Implementation (4 phases, 12 files modified, 1 migration created)

**Phase 1 � Domain + EF + Migration:**
- `TenantSettings.cs`: Th�m `ThemeType Theme` property + `WithTheme()` method + update 8 `With*` methods truy?n Theme
- `TenantConfiguration.cs`: Map `Settings_Theme` column (int, default 0=Classic)
- Migration `20260722141255_AddTenantTheme`: `ALTER TABLE Tenants ADD COLUMN Settings_Theme integer NOT NULL DEFAULT 0`

**Phase 2 � Service + Gateway API:**
- `ITenantManagementService.cs`: `UpdateTenantProfileRequest` th�m `ThemeType? Theme` (nullable = preserve existing)
- `TenantManagementService.cs`: `UpdateProfileAsync` apply `request.Theme ?? existingSettings?.Theme ?? Classic`
- `TenantsController.cs`: `TenantDto` + `UpdateTenantProfileApiRequest` th�m Theme
- `TenantStoreController.cs`: `TenantStoreDto` th�m Theme (anonymous endpoint cho KhachLink)
- `TenantApiClient.cs` (ShopERP): `TenantApiDto` + `UpdateTenantProfileApiRequest` th�m Theme

**Phase 3 � ShopERP Admin UI:**
- `TenantManagement.razor`: Edit modal th�m dropdown 5 theme (vanan-select) v?i m� t? ti?ng Vi?t. `EditForm` class + `OpenEditModal` + `HandleEditSubmit` th�m Theme field.

**Phase 4 � KhachLink render theme:**
- `ShopDto.cs`: Th�m `ThemeType Theme` property
- `ShopConfigHttpService.cs`: `BuildShopConfigFromShop` set `ActiveTheme = shop.Theme`
- `Store.razor`: Wrap content trong `.store-page theme-@GetThemeClass()`, thay hardcoded gradient `#ff9966?#ff5e62` b?ng CSS variables (`--store-hero-gradient`, `--store-accent-gradient`, `--store-accent-color`). 5 theme class blocks define gradient per theme.

**Build:** `dotnet build VanAn.sln` 0 errors. Unit tests `TenantManagementServiceTests` 10/10 PASS.

**Status: COMPLETE. Build pass, unit tests pass. CD deployed. RV 6/6 PASS on live VPS.**

### Runtime Verification (6/6 PASS, live VPS `diemthuong.khachvip.online`, 2026-07-22)

| # | Test | Result | Evidence |
|---|------|--------|----------|
| RV1 | KhachLink app loads after deploy | PASS | HTTP 200, content 6905 bytes |
| RV2 | Gateway store-info returns Theme field | PASS | `"theme":0` in JSON response |
| RV3 | Admin tenants API returns Theme field | PASS | All tenants have `"theme":0` (Classic) |
| RV4 | Theme round-trip: Teen(2) ? Classic(0) | PASS | Set Teen ? `theme:2`, reset Classic ? `theme:0` |
| RV5 | Admin API shows updated theme | PASS | Coffee An An `theme:2` after update |
| RV6 | KhachLink app stable after theme changes | PASS | HTTP 200, no crash |

### Post-deploy fix (commit `ab1bc9f7`)

**Bug:** EF Core `HasDefaultValue(ThemeType.Classic)` treated `0` (Classic) as sentinel � when theme value equals default (0), EF Core skipped `Settings_Theme` in UPDATE SQL, leaving old value in DB. Made it impossible to reset theme to Classic after changing it.

**Fix:** Removed `.HasDefaultValue(ThemeType.Classic)` from `TenantConfiguration.cs`. DB column keeps `DEFAULT 0` from migration for INSERTs. For UPDATEs, EF Core now always includes `Settings_Theme` regardless of value.

**Also fixed (commit `517ddd66`):** `ThemeType?` (nullable) in request DTOs caused System.Text.Json to deserialize `"theme":0` as `null` (0 is default enum value). Changed to non-nullable `ThemeType` (default Classic) in all 3 request DTOs.

---

**PREVIOUS OBJECTIVE � KhachLink PWA � SRI Hotfix + Full RT Verification � COMPLETE (2026-07-22)**

SRI integrity mismatch hotfix deployed + full RT (runtime) test suite executed against live site `https://diemthuong.khachvip.online`. All 10 RT tests PASS. Covers Phase 1 (WASM), Phase 2 (SW caching), Phase 2b (online guard), Phase 3 SC5-SC8 (offline API fallback), SRI hotfix.

### SRI Hotfix (commit `0bb404e9`, 2 files)
- **Root cause:** After deploys, browser blocked `VanAn.KhachLink.wasm` + `VanAn.Shared.wasm` with "Failed to find a valid digest in the integrity attribute" � stale cached wasm (old build) served with fresh `blazor.boot.json` (new integrity hashes).
- **Fix `service-worker.js`:** WASM/DLL fetch handler cache-first ? network-first + cache fallback. Added `activate` event to delete stale caches from old SW versions. Cache version `v11-phase3` ? `v12-sri-fix`.
- **Fix `nginx.conf`:** `/_framework/` cache header `immutable, max-age=31536000` ? `no-cache, must-revalidate` (wasm filenames NOT content-hashed).

### RT Test Results (10/10 PASS, live site, 2026-07-22)
Test spec: `6_Testing/e2e-tests/khachlink-pwa-offline-rt.spec.ts` | Config: `6_Testing/playwright-rt.config.ts`

| # | Test ID | Phase | Result | Time |
|---|---------|-------|--------|------|
| 1 | RT-SRI-01 | SRI+P1 | PASS � App loads, no SRI integrity errors, Blazor error UI not visible | 11.9s |
| 2 | RT-SRI-02 | SRI+P1 | PASS � VanAn.KhachLink.wasm + VanAn.Shared.wasm both 200 (not blocked) | 10.8s |
| 3 | RT-SW-01 | P2 | PASS � Service worker registered, state=activated, scriptURL=service-worker.js | 8.1s |
| 4 | RT-SW-02 | P2 | PASS � WASM cache populated, old caches (v10-batched, v11-phase3) deleted | 13.3s |
| 5 | RT-SC5 | P3 | PASS � Offline Store Finder: page loads from cache, content visible | 16.3s |
| 6 | RT-SC6 | P3 | PASS � Offline Home: page loads from cache, content visible | 16.5s |
| 7 | RT-SC7 | P3 | PASS � Offline Order Tracking: WASM renders from cache | 16.0s |
| 8 | RT-SC8 | P3 | PASS � Offline Order History: page loads from cache, content visible | 16.3s |
| 9 | RT-ONLINE-01 | P2b | PASS � navigator.onLine=false when offline, app renders for browsing | 12.1s |
| 10 | RT-SEC-01 | P3 | PASS � Auth endpoints NOT in dynamic cache (no cross-user leak risk) | 26.0s |

**CD:** GitHub Actions CD run `29901024876` � Build & Push Images SUCCESS, Pre-Deploy Validation SUCCESS, Deploy to VPS SUCCESS.

**Status: COMPLETE. Pushed, CD deployed, RT verified 10/10 PASS.**

---

**PREVIOUS OBJECTIVE � KhachLink PWA Phase 3 � Offline API Fallback Hardening � COMPLETE (2026-07-22)**

Phase 3 of `docs/AI/tasks/khachlink_pwa_offline_master_plan.md`. Hardens the service worker's offline API fallback: whitelist-based cache patterns, stale-while-revalidate for catalog/campaigns, 24h cache expiration. Fixes dead-code `dynamicCachePatterns` (was declared but never used in Phase 2 � fetch handler cached ALL `/api/*` GETs including auth endpoints).

### Phase 3 changes (1 file: `5_WebApps/KhachLink/wwwroot/service-worker.js`)

**SC1 � dynamicCachePatterns now actually used (whitelist):**
- Was dead code in Phase 2 � fetch handler used `startsWith('/api/')` (cached ALL API GETs including `/api/customers/me`, `/api/loyalty/my` ? cross-user cache leak risk on shared devices)
- Now whitelist-based: only 9 endpoint prefixes are cacheable
- Corrected endpoints (was wrong in task card + Phase 2):
  - `/api/tenants/search`, `/api/tenants/nearby`, `/api/tenants/by-slug/`, `/api/tenants/` (covers `/{id}/store-info`, `/{id}/feature-settings`)
  - `/api/catalog/` (`/api/catalog/recommended`)
  - `/api/campaigns/` (`/by-tenant/{id}`, `/{trackingCode}`, `/{id}`)
  - `/api/products/` (`/recommended`, `/grouped-by-tenant`, `/{id}/qr`)
  - `/api/public/orders/` (OrderTracking � was incorrectly listed as `/api/orders/{id}` in task card)
  - `/api/customerorders` (OrderHistory � was incorrectly listed as `/api/orders/history` in task card)
- Removed dead `/api/menu` pattern (endpoint does not exist in Gateway)
- Auth endpoints (`/api/customers/me`, `/api/loyalty/my`, `/api/customer-identity/me`) intentionally EXCLUDED

**SC2 � Stale-while-revalidate for catalog/campaigns:**
- `swrPatterns = ['/api/catalog/', '/api/campaigns/']`
- Fresh cache (< 24h): return immediately, NO background fetch (zero network hit)
- Expired cache: return stale immediately + background fetch to refresh (true SWR)
- No cache: wait for network

**SC3 � 24h cache expiration:**
- `CACHE_EXPIRY_MS = 24 * 60 * 60 * 1000` (24 hours)
- `stampResponse()` adds `x-sw-cached-at` header (ms since epoch) to cached responses
- `isExpired()` checks timestamp on retrieval
- Network-first path: any cache hit wins offline (stale > blank)
- SWR path: fresh cache skips network entirely; expired cache triggers bg refresh

**Cache version bumped:** `v10-batched` ? `v11-phase3` (forces SW update to clear old cache entries that lack `x-sw-cached-at` header)

**Build:** `dotnet build VanAn.sln` 0 errors, 0 warnings. guard-check.ps1 PASS (Windsurf Guard, Architecture Guard, Roslyn Analyzers, fast test gate).

**Status: COMPLETE. Pushed, CI PASS, CD deployed, RT 10/10 PASS.**

---

**PREVIOUS OBJECTIVE � KhachLink PWA Phase 2b � Price Validation + navigator.onLine Guard + Phase 4 Descope � COMPLETE (2026-07-22)**

Architecture review of offline checkout strategy concluded that Phase 4 (offline write queue / IndexedDB + Background Sync) creates unacceptable risks for financial integrity. Phase 4 is **DESCOPED**. Checkout is now **online-only** with `navigator.onLine` guard. Price validation gap (Gateway trusted client-sent prices) fixed with Tier 0+1 validation.

### Phase 2b � Price validation + online guard (commit `51b7e624`, 2 files)

**Tier 0 � Sanity checks (Gateway `PublicOrdersController.checkout`, 0ms):**
- Reject 400 if `UnitPrice <= 0`, `Quantity <= 0`, `VatRate < 0` or `> 1.0`
- Returns specific error per item (product name + invalid value)
- Catches client bugs, DevTools manipulation, corrupted cache

**Tier 1 � FeaturedProducts cross-check (Gateway, ~5ms):**
- Query `FeaturedProducts` from Gateway PG (local � does NOT call ShopERP, no latency, no coupling)
- Compare client `UnitPrice` vs `FeaturedProduct.DisplayPrice` with 5% tolerance
- If mismatch > 5% ? reject 400 "gi� d� thay d?i, vui l�ng t?i l?i trang"
- QR-scanned products (not in FeaturedProducts) skip Tier 1 � QR price is system-generated, trustworthy

**navigator.onLine guard (KhachLink `Checkout.razor`):**
- Check `navigator.onLine` before submit via JS interop
- If offline ? show error "Khong co ket noi mang. Vui long kiem tra 4G/Wifi de gui don hang"
- Financial transactions = online real-time only

**Tier 2 � Async reconciliation (DEFERRED):** ShopERP-side price comparison via NATS reply. Not needed for MVP � Tier 0+1 covers Featured products (most common checkout path).

### Phase 4 � Offline write queue DESCOPE (2026-07-22)

**Decision:** Phase 4 (offline write queue / IndexedDB + Background Sync for checkout POST) is **DESCOPED** from the master plan.

**Rationale (from architecture review):**
1. **Financial integrity:** Offline checkout creates "ghost orders" � order timestamp, price, and inventory state are ambiguous when replayed later. Gateway is order creator (Option C) and must validate in real-time.
2. **Price validation:** Tier 0+1 price validation requires Gateway PG access � cannot run offline.
3. **Inventory overselling:** Without real-time inventory check, offline orders can cause overbooking. Gateway has no inventory table (products live in ShopERP SQLite per-tenant).
4. **Token expiry:** Background Sync replay may fire after auth token expires ? 401 ? order stuck in queue silently.
5. **F&B UX:** Customer-facing PWA for food ordering � "order saved, will send later" is confusing for time-sensitive F&B orders. Better UX: clear error "no connection, check 4G/Wifi".

**What this means for offline capability:**
- **Offline READ works:** catalog browse, store finder, order history, campaign view � all cached by service worker (Phase 2+3).
- **Offline WRITE blocked:** checkout, order creation � requires real-time Gateway validation. `navigator.onLine` guard + clear error message.
- **iOS Safari:** no Background Sync API needed (was a risk in original plan � now moot).

**Revised master plan effort:** 6-9 sessions remaining (Phase 3 + 5 + 6). Phase 4 descope saves 3-4 sessions.

**Status: COMPLETE. Pushed to main, CI PASSED (build 128s, unit 969/0, KhachLink Startup 6/4skip/0, Architecture 37/37). CD deployed to VPS.**

### Next: Phase 3 (Offline API Fallback Hardening)
Per master plan, Phase 3 hardens the offline API fallback � updates `dynamicCachePatterns` to current Option C endpoints (already done in Phase 2), adds stale-while-revalidate for catalog/tenants, and returns meaningful offline JSON responses. See `docs/AI/tasks/khachlink_pwa_phase3_offline_api_task_card.md`.

---

**PREVIOUS OBJECTIVE � KhachLink PWA Phase 2 � Service Worker DLL Caching + Post-Deploy Hotfixes � COMPLETE (2026-07-22)**

Phase 2 of `docs/AI/tasks/khachlink_pwa_offline_master_plan.md`. Updates `service-worker.js` to cache Blazor WASM DLLs + `.wasm` runtime for true offline support. Commit `ec15bc01` pushed, CD PASSED, VPS RV PASS. Then 3 post-deploy hotfixes for runtime issues discovered via browser testing.

### Phase 2 main (commit `ec15bc01`, 1 file: `service-worker.js`)
- Added `WASM_CACHE` (`vanan-wasm-v9-wasm`) for `_framework/*` assets
- `importScripts('/service-worker-assets.js')` loads SDK-generated manifest with hashes + URLs for all `_framework/*.wasm/.dll/.js` assets
- Install event: precaches all WASM assets from manifest (best-effort, per-URL catch)
- `blazor.boot.json`: network-first + cache fallback (detect new versions online, fall back to cached version offline)
- `_framework/*` (DLLs, `.wasm`, `.wasm.br`, `.wasm.gz`): cache-first (immutable, hashed filenames)
- Navigation: network-first ? cached `index.html` ? offline shell (3-tier fallback)
- `dynamicCachePatterns` updated to Option C endpoints (`/api/tenants`, `/api/catalog`, `/api/campaigns`, `/api/products`, `/api/orders`, `/api/menu`)
- Cache version bumped `v8-offline-shell` ? `v9-wasm` (forces SW update)
- Added `/index.html` + `/js/*.js` to `staticUrlsToCache` (needed for WASM)
- Skip cross-origin requests (CDN scripts like html5-qrcode, jsQR)

### Post-deploy hotfixes (3 commits, 2026-07-22)
Browser testing after Phase 2 deploy revealed 3 runtime issues:

1. **Rate limit 503 + SRI integrity fail** (commit `0186723f`, 2 files): SW install event fired 80 concurrent `cache.add()` for `/_framework/*` ? front proxy nginx rate limiter (`burst=20`) blocked 60/80 with 503 ? SRI integrity check fail ? Blazor boot crash. Fix: (a) `service-worker.js` � batch SW precache into chunks of 5 (sequential per batch) instead of 80 concurrent `Promise.allSettled`, cache version `v9-wasm` ? `v10-batched`; (b) `nginx/templates/vanan.conf.template` � move `limit_req zone=web burst=20 nodelay` from server block into `location /` + `location /_blazor` blocks, so `location /_framework/` is exempt from rate limiting (immutable hashed assets don't need rate protection).

2. **CannotResolveService AuthenticationStateProvider** (commit `dabc3698`, 2 files): Phase 1 WASM conversion removed server-side Blazor infrastructure which provided a default `AuthenticationStateProvider`. `UI.Platform.TenantService` requires `AuthenticationStateProvider` via constructor injection, but KhachLink `Program.cs` never registered one ? `CannotResolveService` at render time. Fix: new `Services/AnonymousAuthenticationStateProvider.cs` � stub returning anonymous `ClaimsPrincipal` (no TenantId claim). KhachLink is customer-facing PWA with no server auth; tenant context comes from `LastInteractionService` (localStorage via QR scan). `TenantService.GetCurrentTenantId()` returns `Guid.Empty` ? callers (Home/Cart/Layout) already handle this fallback. Registered in `Program.cs`.

3. **NullabilityInfoContext_NotSupported** (commit `b8a94413`, 1 file): Blazor WASM SDK disables `NullabilityInfoContext` feature switch by default. When `System.Text.Json`'s `DefaultJsonTypeInfoResolver` tries to read nullable annotations via reflection (`NullabilityInfoContext.Create`), it throws ? crashes all HTTP JSON deserialization (CatalogHttpService, OrderWorkflowHttpService, ProductHttpService, SocialCampaignHttpService, etc.). Fix: `<NullabilityInfoContextSupport>true</NullabilityInfoContextSupport>` MSBuild property in `VanAn.KhachLink.csproj`. Reference: [dotnet/runtime#118333](https://github.com/dotnet/runtime/issues/118333).

### VPS RV (2026-07-22) � 9/9 PASS
- `vanan-khachlink` container **healthy** (nginx serving static files, deployed at 04:28 UTC)
- Service worker updated to `v10-batched` with batched install (5/batch)
- 80 concurrent `/_framework/Microsoft.AspNetCore.SignalR.Client.Core.wasm` requests ? **80� 200, 0� 503** (was 20� 503 before fix)
- Homepage HTTP 200, Blazor WASM boot HTML served
- Catalog API (`api.khachvip.online/api/catalog/recommended`) returns valid JSON `{"products":[...]}`
- nginx config confirmed: `location /_framework/` block exists, no `limit_req`
- 4 key WASM assets accessible: `blazor.boot.json`, `blazor.webassembly.js`, `SignalR.Client.Core.wasm`, `VanAn.KhachLink.wasm` � all 200
- `_framework/` = 19.5MB (well under 50MB iOS Safari limit)

### Offline behavior after Phase 2 + 2b
- App loads from cache (WASM DLLs cached) ? UI events fire, navigation works
- API GETs hit cache fallback (read-only)
- **Checkout = online-only** (navigator.onLine guard + Tier 0+1 price validation at Gateway)
- If WASM not yet cached (first visit offline): offline shell shown

**Status: COMPLETE. Pushed to main, CD PASSED, VPS RV 9/9 PASS.**

---

**PREVIOUS OBJECTIVE � KhachLink PWA Phase 1 � Blazor Server ? WebAssembly Conversion � COMPLETE (2026-07-21)**

Phase 1 of `docs/AI/tasks/khachlink_pwa_offline_master_plan.md`. Converts KhachLink from Blazor Server to Blazor WebAssembly so the PWA can work offline (UI events run client-side, no WebSocket required). Commit `b642662b` pushed, CI PASSED.

### Architecture changes
- `VanAn.KhachLink.csproj`: SDK `Microsoft.NET.Sdk.Web` ? `Microsoft.NET.Sdk.BlazorWebAssembly`
- `Program.cs`: `WebApplication.CreateBuilder` ? `WebAssemblyHostBuilder.CreateDefault` + removed `AddInteractiveServerComponents` (WASM interactive by default)
- `App.razor`: `blazor.web.js` ? `blazor.webassembly.js`
- Removed `@rendermode InteractiveServer` from all 13 Pages + PWAInstallPrompt.razor
- Removed `Serilog.AspNetCore` (server-only, pulls `Microsoft.AspNetCore.App` FrameworkReference incompatible with `browser-wasm` RuntimeIdentifier)
- Removed `Microsoft.EntityFrameworkCore.Sqlite` from `VanAn.Shared.csproj` (unused)

### Contract extraction (Option 2 � user-approved)
- Moved 3 contract files `3_CoreHub/Services/` ? `1_Shared/Services/`: `IOrderWorkflowService.cs`, `ISocialCampaignService.cs`, `IShopFeatureSettingsService.cs` (includes `ShopFeatureSettingsDto` + `PriceValidationResult`). Namespace `VanAn.CoreHub.Services` ? `VanAn.Shared.Services`.
- Added `using VanAn.Shared.Services;` to ~20 files in CoreHub, Gateway, ShopERP, Tests
- Updated fully-qualified DI registrations in `Gateway/Program.cs` + `ShopERP/Program.cs`
- Added `IInventoryService` alias in `OrderService.cs` to disambiguate (exists in both `CoreHub.Interfaces` + `Shared.Services`)
- Removed `VanAn.CoreHub` ProjectReference from `KhachLink.csproj` (KhachLink uses only Shared contracts + HTTP services)

### Dead code cleanup (files that referenced CoreHub directly)
- Deleted `DashboardHttpService.cs`, `OfflineOrderService.cs` + `.ts`, `EnhancedCartService.cs` + `.ts`, `SyncConflictResolver.cs`, `ConflictResolutionService.cs` + `.ts` (all dead � not registered in DI)
- Deleted `Campaign.cshtml` + `Campaign.cshtml.cs` (legacy MVC Razor Page � incompatible with WASM), replaced by `Campaign.razor` Blazor component at `/c/{trackingCode}`
- Deleted 6 dead test files (tests for deleted dead code): `RetryStrategyTests`, `TimeBasedBugTests`, `UIStateMachineTests`, `FinancialSafetyTests`, `ProductionDataTests`, `SyncConflictResolverTests`

### Deployment changes
- `Dockerfile`: dotnet runtime ? `nginx:alpine` serving static files
- `nginx.conf`: SPA routing (`try_files` ? `index.html`), gzip, cache headers for `_framework/` (immutable), no-cache for `service-worker.js` + `blazor.boot.json`
- `docker-compose.prod.yml`: removed ASPNETCORE env vars + Serilog config, memory limit 512m ? 256m
- `wwwroot/appsettings.json`: Gateway BaseUrl for WASM config loading

### Test impact
- Unit tests: **984 passed / 0 failed** (33 dead tests removed from 6 deleted files)
- KhachLink Startup: **6 passed / 4 skipped / 0 failed** (4 server-startup tests skipped � `WebApplicationFactory` can't boot WASM, marked Skip with reason, rewrite planned for Phase 6)
- Build: `dotnet build VanAn.sln` ? **0 errors**

**Status: COMPLETE. Pushed to main, CI PASSED. Awaiting CD deploy + VPS RV.**

### Next: Phase 2 (Service Worker DLL Caching)
Per master plan, Phase 2 updates `service-worker.js` to cache Blazor WASM DLLs (`_framework/*.dll`) for true offline support. See `docs/AI/tasks/khachlink_pwa_phase2_sw_dll_caching_task_card.md`.

---

**PREVIOUS OBJECTIVE � KhachLink /stores Search Button Fix � COMPLETE (2026-07-21)**

User reported search button on `https://diemthuong.khachvip.online/stores` not clickable. Root cause: the magnifier-glass icon in the search box was a decorative `<span class="input-group-text">` � NOT a button, so clicking it did nothing. Search was only triggered via `@oninput` debounce (300ms after typing) with no dedicated search button or Enter-key handler.

**Fix (1 file):** `5_WebApps/KhachLink/Pages/StoreFinder.razor`
- Converted search icon `<span>` ? `<button type="button" @onclick="LoadStores">` � now clickable.
- Added `@onkeyup="OnSearchKeyUp"` on the input � pressing **Enter** triggers immediate search (cancels running debounce).
- Added `OnSearchKeyUp(KeyboardEventArgs e)` method.
- Added `.btn-search-icon` CSS (cursor pointer, hover, no outline) to preserve input-group look.

**Verification:** `dotnet build VanAn.KhachLink.csproj` ? Build succeeded, 0 errors, 11 pre-existing warnings (unrelated). Ready for commit + push to trigger CD deploy.

**Status: COMPLETE. Awaiting CD deploy after push.**

---

**PREVIOUS OBJECTIVE � Post-Shop-Removal Runtime Verification + Tenant.Id LINQ Bug Fix � COMPLETE (2026-07-21)**

Shop entity removal (previous session, 221 files) deployed to VPS via CD. This session performed comprehensive runtime verification (RV) and fixed a regression batch.

### A. Tenant.Id Value Object LINQ Translation Bug (Known Error Pattern #8 � NEW)
After Shop removal, `TenantStoreController` (new replacement for `ShopsController`) failed on `/api/tenants/{tenantId}/store-info` with HTTP 500. Root cause: `Tenant.Id` is a `TenantId` value object with `HasConversion` � three failing patterns discovered across 3 controllers:
1. `EF.Property<Guid>(t, "Id") == guid` ? IConvertible cast error (Pattern #1 variant)
2. `t.Id.Value == guid` in `Where` ? LINQ translation fails
3. `guidList.Contains(t.Id)` with `List<Guid>` ? type mismatch

**Fix (1 commit, 3 files):** Construct `TenantId` value object before comparison. `t.Id == new TenantId(tenantId)`. For `Contains`, convert collection: `tenantIds.Select(id => new TenantId(id)).ToList()`.
- `TenantStoreController.GetStoreInfo` � fixed
- `PublicOrdersController.checkout` � fixed (preventive, was working but pattern risky)
- `CatalogController.recommended` � fixed (preventive)

**Commits:** `20697063` (initial TenantStore fix), `e876cf53` (batch fix all 3 controllers + Pattern #8 added to governance.md).

### B. RV Results on VPS (2026-07-21)
- All 5 VanAn containers healthy (gateway, shoperp, khachlink, postgres, nats)
- DB schema verified: `Shops` table dropped, `SocialCampaigns.ShopId` dropped, `Tenants.Settings_Latitude/Longitude` added
- 3 tenants in DB (coordinates null � expected, no migration data on this VPS)
- All tenant-based endpoints PASS:
  - `GET /api/tenants/{id}/store-info` (valid): 200 ?
  - `GET /api/tenants/{id}/store-info` (invalid): 404 ?
  - `GET /api/tenants/nearby`: 200 ?
  - `GET /api/tenants/search`: 200 ?
  - `GET /api/catalog/recommended`: 200 ?
  - `GET /health`: 200 ?
- No errors in gateway logs after fix deployed

### C. Governance Update
Added Known Error Pattern #8 to `.devin/rules/governance.md` � `Tenant.Id` value object LINQ translation. Reference implementations: `TenantManagementService.GetTenantByIdAsync`, `SocialCampaignRepository.GetActiveByTenantIdValueAsync`.

**Status: COMPLETE. All deployed to VPS. RV 6/6 PASS for tenant-based endpoints.**

---

**PREVIOUS OBJECTIVE � Shop Entity Removal � COMPLETE (2026-07-21)**

Removed `Shop` entity from system (221 files). `Tenant` is now single identity for all business operations (aligns with TT 152/2025/TT-BTC � each HKD = separate legal entity). `Latitude/Longitude` migrated to `TenantSettings`. `ShopsController` replaced by `TenantStoreController`. All migrations applied (PostgreSQL + SQLite). See Section 6 + `docs/AI/tasks/` for details.

---

**PREVIOUS OBJECTIVE � KhachLink Home Page Personalization + Campaigns/Shops CRUD Admin UI � COMPLETE (2026-07-20)**

Two features delivered this session:

### A. Dynamic Home Page Content (replaces static Hero + Stats)
- **LastInteractionService** � tracks `lastTenantId` in localStorage via JS interop. `RecordInteractionAsync(tenantId)` called from `Scan.razor` (QR scan add-to-cart, both fast + legacy paths) + `Home.razor AddFeaturedToCart` (Featured product add).
- **Home.razor** � Hero section replaced with Campaign section (shows active campaigns for last-interaction tenant, fallback empty state with "Qu�t QR Ngay" CTA for new users). Stats section replaced with StoreFinder section (shows shop info: name, address, phone, Google Maps link). Auto-refresh when customer adds product from different tenant.
- **Backend:** `GET /api/campaigns/by-tenant/{tenantId}` (Gateway, AllowAnonymous) + `GET /api/shops/by-tenant/{tenantId}` (Gateway, AllowAnonymous, pre-existing).
- **Commits:** `e292166c` (initial), `c8765aeb` (TenantId VO fix), `6b9cf88d` (SaveChangesAsync fix), `4e6cbafd` (ShopId FK fix), `f79c5f46` (by-tenant service method + PUT DTO), `a83b797c` (IgnoreQueryFilters).

### B. Campaigns + Shops CRUD Admin UI (SystemAdmin only)
- **Backend:** Gateway `CampaignsController` � added POST create + fixed auth on PUT/DELETE (`[AllowAnonymous]` ? `[Authorize(Policy="SystemAdmin")]`). Gateway `ShopsController` � added POST/PUT/DELETE forward to ShopERP with SystemAdmin auth + Authorization header forwarding.
- **Admin UI:** Two new ShopERP Blazor pages � `/admin/campaigns` (CampaignsAdmin.razor: list + create/edit modal with Tenant + Shop dropdowns + delete) + `/admin/shops` (ShopsAdmin.razor: list + create/edit modal with Tenant dropdown + lat/lng coordinates + delete). Both `@attribute [Authorize(Policy="SystemAdmin")]`.
- **Commits:** `2725e28d` (admin UI + backend), `4e6cbafd` (FK fix + shop dropdown), `f79c5f46` (PUT DTO), `a83b797c` (IgnoreQueryFilters).

### RV Test Results (2026-07-20)
**Campaigns CRUD � ALL PASS ?** (tested via curl on VPS with SystemAdmin JWT):
| Test | HTTP | Result |
|---|---|---|
| POST no token | 302 | Redirect login ? |
| POST create | 201 | Campaign persisted to PG ? |
| GET all | 200 | Contains new campaign ? |
| GET by-tenant (Home endpoint) | 200 | Contains new campaign ? |
| PUT update | 200 | Contains "Updated" ? |
| DELETE | 200 | Soft-delete (IsActive=false) ? |

**Shops CRUD via Gateway � Known Limitation ??** POST returns login HTML because ShopERP uses cookie auth (OIDC), not JWT. Admin UI `ShopsAdmin.razor` uses `DbContext` directly (in-process, cookie auth) � works correctly. Gateway shops write forwarding is secondary; admin UI is primary interface.

### Bugs Found & Fixed During RV
1. `CreateCampaignAsync` missing `SaveChangesAsync` � campaigns never persisted (commit `6b9cf88d`)
2. FK violation `FK_SocialCampaigns_Shops_ShopId` � `Guid.Empty` ShopId (commit `4e6cbafd`)
3. `GET by-tenant` used `GetCampaignsByShopAsync` (queries ShopId not TenantId) (commit `f79c5f46`)
4. PUT 400 � `[FromBody] SocialCampaign` has protected setters ? use `UpdateCampaignRequest` DTO (commit `f79c5f46`)
5. PUT 404 � `GetByIdAsync` didn't use `IgnoreQueryFilters` for SystemAdmin cross-tenant (commit `a83b797c`)
6. `GetActiveByTenantIdValueAsync` used `c.TenantId.Value == tenantId` (can't translate) ? use `c.TenantId == new TenantId(tenantId)` per Known Error Pattern #1 (commit `c8765aeb`)

**Status: COMPLETE. All deployed to VPS. RV 6/6 PASS for Campaigns CRUD.**

---

**PREVIOUS OBJECTIVE � Multi-VPS Checkout Architecture (Option C) � ALL 8 PHASES COMPLETE (2026-07-20)**

Multi-VPS Checkout Option C master plan � Phases 1, 2, 3, 3.5, 4, 5, 3.6, 6, 7 all complete. See Section 6 History Log + `docs/Architecture/ADR001-Station-Architecture.md` v3 addendum + `docs/AI/tasks/tech_debt_multi_vps_checkout.md`. NEXT: Phase 8 (Multi-VPS E2E Validation � Playwright).

**Archived (2026-07-17):** QuickSetup + Product Management Phases 4�6 and the Single-Identity Refactor (Hu?ng A). See `docs/AI/project_state_archive.md`.

---

## Archived 2026-07-26 (from project_state.md reduction â€” 627 â†’ ~170 lines)

### Previous Objective â€” Community Commerce Doc v1.4 Hybrid Central + Edge Architecture (COMPLETE 2026-07-26)

Spec Section 7C: Hybrid Central + Edge diagram, 11 bottlenecks, 10 short-term + 8 long-term solutions, 9 corrections, 8 refactor techniques, cuá»‘n chiáº¿u strategy, evolution roadmap, 12 hard rules HR-SCALE-1 to HR-SCALE-12.

Master plan Section 12 (Cost): PoC $50 â†’ 10M $135K. SMS 58% cost driver. VN-optimized @ 1M: $0.009/user/mo. Break-even ~1M users.

Master plan Section 13 (Sprint 7+ Edge Migration): 15 tasks. Entry: >100K users. Exit: 4 edge gateways + PostGIS + SignalR 100K+ + cost â‰¤$10K @ 1M.

Master plan Section 14 (Hard Rules): 12 rules. Apply from Sprint 0: HR-SCALE-1 (/api/v1/), 2 (ACL), 5 (SalesmanCode prefix), 11 (migration rehearsal).

### Previous Objective â€” Community Commerce Doc v1.3 Review Fixes (COMPLETE 2026-07-26)

9 BLOCKING + 7 HIGH/MEDIUM items resolved (doc-only):
- A1: Email/password DEFER Sprint 7+. PoC auth = Social + Fingerprint.
- A2: Community entities PG ONLY. Sprint 0 reduced 4â†’3 sessions.
- A3: ChatHub/LocationHub auth via X-Customer-Token query string.
- A4: "delivering" status = Domain Modification (CC-S1-T0).
- A5: IdentityLevel.DeviceVerified=4 = Domain Modification.
- A6-A8: UI Spec Addendum Section 7B (8 pages).
- A9: Deployment Plan Section 11.
- B1-B4: Scan.razor/pwa.js/Checkout.razor modify existing. GoogleMaps KEEP. ProductShortCode in PG. PostGIS defer.
- C1-C3: VPS planning + backup. Monitoring. Legal gate. SW cache update.

### Previous Objective â€” Community Commerce Doc v1.2 Self-Hosted Anti-Fraud (COMPLETE 2026-07-26)

7 files updated: 5-layer anti-fraud (fingerprint + token + behavioral + risk scoring + attestation). +DeviceRegistration +FraudFlag entities. UC-01/09/12 risk scoring. Zero external dependency.

### Previous Objective â€” Community Commerce Doc v1.1 Baseline Fixes (COMPLETE 2026-07-25)

6 files updated: A1-A4 baseline fixes, UC-08/09/10 composite referral + per-product commission, UC-12 app-install attribution, B1-B4 entity redesign, Sprint 0 drop social login, Sprint 4 redesign.

### Previous Objective â€” Phase 5 Push Notification + Loyalty L-A/L-B/L-C (COMPLETE 2026-07-24)

Phase 5: 17 SC. CampaignPushJob + PushNotificationDelivery. LoyaltyPointsChanged outbox + NATS. CustomerSegmentationService. Click tracking. CampaignsAdmin UI.

Loyalty L-A: Configurable points formula. Commits aae5fba2 + 8b8f97bc.

Loyalty L-B: Redemption system. 3 entities + RedemptionService (ACID) + controllers + KhachLink /rewards + ShopERP admin. RV 13/13. Commits 8f6162a5 + 88a74ab6 + 891869eb.

Loyalty L-C: 3 workstreams (per-tenant config UI, gamification 5 mission types, notification rules + 2 HostedServices). RV 57/57. Commit 146a6eed. Rebrand commit 89e33480.

### Previous Objective â€” Featured Product Picker + Order Status Unification (COMPLETE 2026-07-23)

Commit 17dab107. Product picker dropdown. Order status unified via OrderWorkflowService. RV 4/4.

### Previous Objective â€” KhachLink Font Fix + Order Tracking Freeze Fix (COMPLETE 2026-07-23)

Font: 6 static files double-encoding repaired (d9e2728f). Freeze: IAsyncDisposable + CTS cancel + backoff (7fc7ca27).

### Previous Objective â€” KhachLink Theme + PWA Phases 1-3 (COMPLETE 2026-07-22)

Theme: 5 themes per tenant. PWA Phase 1: Blazor Server â†’ WASM. Phase 2: SW DLL caching. Phase 2b: Price validation. Phase 3: Offline API fallback. Phase 4 DESCOPE.

### Previous Objective â€” Multi-VPS Checkout Option C (ALL 8 PHASES COMPLETE 2026-07-20)

Phases 1-7 complete. ShopInstance + Tenant FK. Gateway Order Creator + NATS routed. Accounting consolidation. KhachLink multi-tenant cart. Admin UI. Governance. See ADR-001 v3.

### Full History Log (archived 2026-07-26)

See git log for full commit history. Key milestones:
- [2026-07-26] Sprint 0 COMPLETE. 11 entities + 42 tests + migration. Merged + deployed. RV 18/18.
- [2026-07-26] Doc v1.4-v1.1 COMPLETE. 4 doc-only sessions.
- [2026-07-24] Loyalty L-C COMPLETE. RV 57/57.
- [2026-07-24] Loyalty L-B COMPLETE. RV 13/13.
- [2026-07-24] Loyalty L-A + Phase 5 Push COMPLETE.
- [2026-07-23] Product Picker + Order Status. RV 4/4.
- [2026-07-23] Font Fix + Freeze Fix.
- [2026-07-22] Theme + PWA Phases 1-3.
- [2026-07-21] PWA Phase 1 (Server â†’ WASM).
- [2026-07-20] Multi-VPS Option C Phases 1-7 COMPLETE.
- [2026-07-18] Multi-tenant bug fix + Quick-Setup real.
- [2026-07-17] Single-Identity Refactor + VPS verified.
- [2026-07-16] UUIDv7 Refactor + Data Sync Hardening.
- [2026-07-15] Order Sync Track E1 COMPLETE.
- [2026-07-14] KhachLink E2E VPS PASS + UI/UX fix batch.
- [2026-07-13] Tiered Auth P1-P3 RV COMPLETE. 14/14.
- [2026-07-12] KhachLink Wave 3+4.
- [2026-07-11] KhachLink Wave 0+2.
- [2026-07-09-10] Accounting PostgreSQL Online. 3 waves. 1223/1223.
- Older: See earlier archive sections.

### Full Maintenance Log (archived 2026-07-26)

See git log and earlier archive sections for full maintenance log entries 2026-07-14 through 2026-07-26.

