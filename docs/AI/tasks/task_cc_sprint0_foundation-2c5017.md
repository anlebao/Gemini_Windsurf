# TASK CARD: Community Commerce — Sprint 0 — Foundation (v1.5 verified)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Tạo Domain entities mới (**11 entity — v1.2: tăng từ 9**): `CommunityRole`, `DeliveryTask`, `DeliveryTracking`, `Conversation`, `Message`, `SalesReferral` (v1.1: redesign composite, v1.2: +RiskScore fields), `WalletTransaction` (v1.1: + Reversal type), `ProductReferralConfig`, `AppInstallAttribution` (v1.2: + RiskScore/HoldUntil/DeviceRegistrationId), **`DeviceRegistration` (v1.2 NEW), `FraudFlag` (v1.2 NEW)** + bổ sung fields cho Order + EF Configuration + Migration (PG + SQLite) + **device fingerprint JS + risk scoring service (v1.2)**.
- **Nghiệp vụ áp dụng:** Nền tảng BLOCKING cho mọi sprint sau (S1-S6). Không có entities này thì không thể build API/UI cho shipper/salesman. **v1.2: Anti-fraud framework nền tảng cho UC-09/UC-12 risk scoring + Sprint 6 Fraud Review UI.**
- **Status:** COMPLETE 2026-07-26 (merged to main + VPS deployed + RV PASS) — **v1.5 VERIFIED 2026-07-29: base code đối chiếu 100% pass (11 entities + 11 EF configs + migration + 59 community tests + 39 architecture tests + guard-check ALL PASSED + fingerprint JS tồn tại)**
- **Branch:** `feature/community-sprint0-foundation` → merged to `main` (fast-forward `89e33480..f563e415`)
- **v1.5 VERIFICATION (2026-07-29):**
  - ✅ 11 entities confirmed in `1_Shared/Domain.cs` (lines 3191-3716): CommunityRole, DeliveryTask, DeliveryTracking, Conversation, Message, SalesReferral, WalletTransaction, ProductReferralConfig, AppInstallAttribution, DeviceRegistration, FraudFlag
  - ✅ `IdentityLevel.DeviceVerified=4` confirmed (Domain.cs:622)
  - ✅ 11 EF Configuration files confirmed in `3_CoreHub/Infrastructure/Configurations/`
  - ✅ Migration `20260726105331_CommunitySprint0.cs` confirmed
  - ✅ Fingerprint JS confirmed: `wwwroot/js/fingerprint.js` + `wwwroot/lib/fingerprintjs/fingerprint.js`
  - ✅ `dotnet build VanAn.sln` — 0 errors, 1120 warnings
  - ✅ Community tests: 59 passed, 0 failed
  - ✅ Architecture tests: 39 passed, 0 failed
  - ✅ `guard-check.ps1` ALL CHECKS PASSED (sau khi fix regex syntax error + exclude test files từ raw SQL scan)
  - ⚠️ **GAP (v1.5 NEW — CC-S0-T3):** Device fingerprint infrastructure có nhưng CHƯA wire-up vào production path (RV0-11 chỉ test JS load, không test end-to-end `collect()→POST→DeviceRegistration`). Sprint 0.5 (CC-S0-T3) sẽ wire-up.
- **v1.3 changes (incremental trên v1.2):**
  - **Community entities PG ONLY** — KHÔNG tạo trên ShopERP SQLite (cross-tenant nature, tránh 300K SQLite files phải migrate). Remove CC-S0-T3 SQLite migration task.
  - **Email/Password login DEFER Sprint 7+** — PoC auth = Social (Google + Facebook UI Sprint 1) + Device Fingerprint.
  - **Device Fingerprint Consent Dialog** — phải có trước khi collect fingerprint (GDPR/PDPA compliance).
  - **IdentityLevel.DeviceVerified=4 flagged as Domain Modification** — explicit trong task card.
  - **Vendor FingerprintJS + Leaflet (no CDN)** — consistent với zero-dependency rule.

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** COMPLETE (was ANALYZE → IMPLEMENT)
- **Current Phase:** Sprint 0 of 7 — COMPLETE 2026-07-26
- **Dependency:** None (first sprint — BLOCKING cho S1-S6)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/task_cc_sprint0_foundation.md` (READ — this task card)
- `docs/AI/tasks/sprint0_foundation_detailed_plan.md` (READ — detailed plan)

### Files cần CREATE (v1.2: 11 config files thay vì 9)
- `3_CoreHub/Infrastructure/Configurations/CommunityRoleConfiguration.cs`
- `3_CoreHub/Infrastructure/Configurations/DeliveryTaskConfiguration.cs`
- `3_CoreHub/Infrastructure/Configurations/DeliveryTrackingConfiguration.cs`
- `3_CoreHub/Infrastructure/Configurations/ConversationConfiguration.cs`
- `3_CoreHub/Infrastructure/Configurations/MessageConfiguration.cs`
- `3_CoreHub/Infrastructure/Configurations/SalesReferralConfiguration.cs`
- `3_CoreHub/Infrastructure/Configurations/WalletTransactionConfiguration.cs`
- `3_CoreHub/Infrastructure/Configurations/ProductReferralConfigConfiguration.cs`
- `3_CoreHub/Infrastructure/Configurations/AppInstallAttributionConfiguration.cs`
- `3_CoreHub/Infrastructure/Configurations/DeviceRegistrationConfiguration.cs` (v1.2 NEW)
- `3_CoreHub/Infrastructure/Configurations/FraudFlagConfiguration.cs` (v1.2 NEW)
- `6_Tests/VanAn.Core.Tests/CommunityRoleTests.cs`
- `6_Tests/VanAn.Core.Tests/DeliveryTaskTests.cs`
- `6_Tests/VanAn.Core.Tests/WalletTransactionTests.cs`
- `6_Tests/VanAn.Core.Tests/OrderCommunityFieldsTests.cs`
- `6_Tests/VanAn.Core.Tests/ProductReferralConfigTests.cs`
- `6_Tests/VanAn.Core.Tests/AppInstallAttributionTests.cs`
- `6_Tests/VanAn.Core.Tests/SalesReferralTests.cs`
- `6_Tests/VanAn.Core.Tests/DeviceRegistrationTests.cs` (v1.2 NEW)
- `6_Tests/VanAn.Core.Tests/FraudFlagTests.cs` (v1.2 NEW)
- `6_Tests/VanAn.Core.Tests/RiskScoringServiceTests.cs` (v1.2 NEW — risk score calculation logic)
- `6_Tests/VanAn.Architecture.Tests/WalletTransactionImmutabilityTests.cs`
- `5_WebApps/KhachLink/wwwroot/js/fingerprint.js` (v1.2 NEW — FingerprintJS v4 MIT, self-host)
- `5_WebApps/KhachLink/wwwroot/lib/fingerprintjs/fingerprint.js` (v1.2 NEW — vendored FingerprintJS library)

### Files cần MODIFY
- `1_Shared/Domain.cs` — thêm 11 entity mới + 9 enum mới + fields cho Order (v1.2: +RiskScore/HoldUntil trên SalesReferral/AppInstallAttribution, +IdentityLevel.DeviceVerified=4)
- `3_CoreHub/Infrastructure/IVanAnDbContext.cs` — thêm 11 DbSet
- `3_CoreHub/Infrastructure/VanAnDbContext.cs` — thêm 11 DbSet
- `3_CoreHub/Infrastructure/Migrations/` — `dotnet ef migrations add CommunitySprint0` (PG)
- `5_WebApps/ShopERP/Infrastructure/` — SQLite migration
- `3_CoreHub/Infrastructure/Configurations/OrderConfiguration.cs` — thêm config cho 8 new fields
- `3_CoreHub/Services/RiskScoringService.cs` (v1.2 NEW) — IRiskScoringService + impl compute RiskScore 0-100 từ 8 factors
- `3_CoreHub/Services/IWalletService.cs` (v1.4 NEW — moved from Sprint 5) — base atomic CreateTransactionAsync (HR-SCALE-3)
- `3_CoreHub/Services/WalletService.cs` (v1.4 NEW — base impl only, Sprint 5 extends với COD/Advance/Settlement/Reverse)

### Files READ ONLY (investigate patterns)
- `1_Shared/Domain/Common.cs` — BaseEntity, IMustHaveTenant, AggregateRoot patterns
- `3_CoreHub/Infrastructure/Configurations/CustomerConfiguration.cs` — EF config pattern reference
- `3_CoreHub/Infrastructure/Configurations/OrderConfiguration.cs` — Order config + OwnsOne pattern
- `5_WebApps/ShopERP/Controllers/SocialAuthController.cs` — Google auth ĐÃ TỒN TẠI (v1.1: verify only, KHÔNG build mới)
- `5_WebApps/ShopERP/Controllers/CustomerIdentityController.cs` — OTP auth đã tồn tại (verify only)
- `1_Shared/Domain.cs:1467-1478` — `Order.Create` reference impl cho Single-Identity Pattern (Id = OrderId.Value sync)

### Boundary Rules (v1.2 updated)
- KHÔNG sửa existing entity logic — chỉ THÊM fields vào Order (backward compatible, nullable). v1.1: KHÔNG thêm fields vào Customer. **v1.2: THÊM fields vào SalesReferral + AppInstallAttribution (RiskScore, RiskFactors, HoldUntil) + mở rộng IdentityLevel enum (+DeviceVerified=4).**
- KHÔNG tạo API endpoints trong Sprint 0 — chỉ Domain + EF + Migration + **device fingerprint JS + risk scoring service (v1.2)**
- KHÔNG tạo UI — Sprint 0 không có UI work
- KHÔNG phá OTP login hiện tại — regression test bắt buộc (**OPTIONAL trong v1.2 — SMS không bắt buộc**)
- KHÔNG build social login — ĐÃ CÓ (`SocialAuthController.cs`, Tiered Auth P1 PASS). Chỉ verify existing.
- **v1.2 NEW:** KHÔNG build SMS gateway integration — SMS OPTIONAL. Device fingerprint + behavioral + risk scoring thay thế.
- **v1.2 NEW:** KHÔNG build WebAuthn Passkey — OPTIONAL, defer Sprint 7+ (post-PoC).
- `WalletTransaction` phải immutable (no update/delete methods) + Reversal pattern — giống AccountingEntry pattern
- Community entities thừa kế `BaseEntity` (có TenantId) — cross-tenant trên Gateway PG, tenant-scoped trên ShopERP SQLite
- **Single-Identity Pattern (v1.1):** Tất cả 11 entity mới dùng `BaseEntity.Id` trực tiếp (KHÔNG có business key VO kiểu `CommunityRoleId`). Constructor public, EF config `HasKey(e => e.Id)`.
- **Domain Modification (v1.2 + v1.3 explicit flag):** Việc thêm 11 entity + 8 fields vào Order + **mở rộng 3 enum (IdentityLevel +DeviceVerified=4, CommissionStatus +Rejected=3 +Held=4, AttributionStatus +Rejected=3 +Held=4)** là Domain Modification approved as Community Commerce feature plan. Chỉ thực hiện trong Sprint 0 Domain Phase.
  - **IdentityLevel (v1.3 explicit):** Hiện có 4 values (Guest=0, Social=1, Verified=2, Full=3) tại Domain.cs:615-621. Add `DeviceVerified=4`. **Verified by codebase 2026-07-26.**
  - **CommissionStatus (v1.2 NEW):** Hiện có 2 values (Pending=1, Paid=2). Add `Rejected=3`, `Held=4`.
  - **AttributionStatus (v1.2 NEW):** Hiện có 2 values (Pending=1, Paid=2). Add `Rejected=3`, `Held=4`.
- **v1.2 NEW — DeviceRegistration max 3 per Customer:** Enforce tại application layer (count active before insert, throw if exceed). Device 4+ → create with IsActive=false + FraudFlag.
- **v1.2 NEW — RiskScore deterministic:** RiskScoringService tính score từ 8 factors (sameFingerprint, sameIP24h, customerAgeDays<7, deviceFirstSeen<24h, ordersFromDeviceToday>3, referralBonusAmount>50K, appInstallTime<30s, blacklistedFingerprint). Deterministic — cùng input luôn ra cùng score.

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES) — v1.2
- [ ] **Domain Protection:** Entities mới vào `1_Shared/Domain.cs` (Single Source of Truth). No EF Core attributes trong Domain.
- [ ] **Single-Identity (v1.1):** Tất cả 11 entity mới dùng `BaseEntity.Id` trực tiếp (không business key VO). Constructor public, EF config `HasKey(e => e.Id)`.
- [ ] **WalletTransaction Immutable + Reversal (v1.1):** Append-only, no Update/Delete methods. `BalanceAfter` tính khi tạo. Reversal entry: `Type=Reversal`, `Amount=-original`, `RelatedTransactionId=original.Id`.
- [ ] **Architecture test:** `WalletTransaction_Immutable_NoPublicSetter` + `WalletTransaction_NoUpdateMethod` PASS trong VanAn.Architecture.Tests.
- [ ] **Backward Compatible:** Fields mới trên Order là nullable — không break existing data. v1.1: KHÔNG thêm fields vào Customer. **v1.2: Thêm fields vào SalesReferral + AppInstallAttribution (RiskScore, RiskFactors, HoldUntil) — nullable, backward compatible.**
- [ ] **.NET 8.0.x:** CI/CD dùng .NET 8. Kiểm tra Directory.Packages.props nếu thêm package mới.
- [ ] **EF Auto-Discovery:** `ApplyConfigurationsFromAssembly` trong `OnModelCreating` — config mới tự động được pick up.
- [ ] **Multi-tenancy:** Community entities implement `IMustHaveTenant` (qua BaseEntity). Cross-tenant trên Gateway PG (không query filter), tenant-scoped trên ShopERP SQLite.
- [ ] **Per-product config (v1.1):** `ProductReferralConfig` lưu `CommissionRate` (2-5%) + `AppInstallBonus` per product — KHÔNG hardcode.
- [ ] **Composite referral (v1.1):** `SalesReferral` có `ProductId` + `ProductShortCode` — mã salesman gộp với mã product.
- [ ] **v1.2 NEW — Zero external dependency:** KHÔNG phụ thuộc SMS gateway, Zalo OA, WhatsApp, Kafka, Synadia, RDS. Self-host toàn bộ.
- [ ] **v1.2 NEW — Device fingerprint self-host:** FingerprintJS v4 (MIT) vendored trong `wwwroot/lib/fingerprintjs/`. KHÔNG dùng CDN (tránh dependency external).
- [ ] **v1.2 NEW — RiskScore deterministic:** RiskScoringService tính score từ 8 factors, deterministic, có unit test verify.
- [ ] **v1.2 NEW — DeviceRegistration max 3 per Customer:** Application-layer enforce (count active before insert).
- [ ] **v1.2 NEW — SMS OTP OPTIONAL:** KHÔNG bắt buộc. Device fingerprint + behavioral + risk scoring thay thế.
- [ ] **v1.2 NEW — WebAuthn OPTIONAL:** Defer Sprint 7+. KHÔNG implement trong Sprint 0.

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC) — v1.2
- [x] **SC1:** 11 entity mới tồn tại trong `Domain.cs` với đúng fields, constructor, state machine methods (v1.2: tăng từ 9 — +DeviceRegistration, +FraudFlag) — PASS (pre-push CI, DLL deployed)
- [x] **SC2:** 9 enum mới (v1.2: tăng từ 6): `CommunityRoleType`, `DeliveryTaskStatus`, `WalletTransactionType` (+ Reversal=6), `CommissionStatus` (+ Rejected=3, Held=4 v1.2), `BonusStatus`, `AttributionStatus` (+ Rejected=3, Held=4 v1.2), `FraudEntityType` (v1.2 NEW), `FraudFlagType` (v1.2 NEW), `FraudFlagStatus` (v1.2 NEW) — PASS (pre-push CI, DLL deployed)
- [x] **SC3:** Order có 8 fields mới: `ShipperId`, `SalesmanId`, `ReferralCode`, `ReferralProductId`, `DeliveryLat`, `DeliveryLng`, `CodAmount`, `CodCollectedAt` (tất cả nullable) — PASS (VPS PG: 8/8 columns verified)
- [x] **SC4 (v1.2 NEW):** `SalesReferral` có 3 fields mới: `RiskScore` (int, 0-100), `RiskFactors` (string JSON), `HoldUntil` (DateTime?) — nullable, default 0/null — PASS (VPS PG: 3/3 columns verified)
- [x] **SC5 (v1.2 NEW):** `AppInstallAttribution` có 4 fields mới: `RiskScore`, `RiskFactors`, `HoldUntil`, `DeviceRegistrationId` (Guid?, FK → DeviceRegistration.Id) — PASS (VPS PG: 4/4 columns verified)
- [x] **SC6 (v1.2 NEW):** `IdentityLevel` enum có 5 values: Guest=0, Social=1, Verified=2, Full=3, **DeviceVerified=4 (NEW)** — PASS (pre-push CI, DLL deployed)
- [x] **SC7:** 11 EF Configuration files tồn tại (v1.2: tăng từ 9), `ApplyConfigurationsFromAssembly` pick up tự động — PASS (pre-push CI, DLL deployed)
- [x] **SC8:** `IVanAnDbContext` + `VanAnDbContext` có 11 DbSet mới (v1.2: tăng từ 9) — PASS (pre-push CI, DLL deployed)
- [x] **SC9:** Migration apply thành công (local PG + VPS PG) — `20260726105331_CommunitySprint0` recorded in `__EFMigrationsHistory` — PASS (VPS PG verified). **Note (F6 fix 2026-07-26):** Migration scope thực tế rộng hơn tên gọi — ngoài 11 community tables + 8 Order cols, migration còn chứa 3 Loyalty-B tables (RedemptionCatalogItems, RedemptionRecords, Vouchers) + 5 Loyalty-C/PWA cols trên Customers (Birthday, FacebookShareCount, OtpVerifiedAt, PWAInstalledAt, TikTokShareCount) + 10 Loyalty-C cols trên ShopFeatureSettings (Loyalty_*, Notify_*). Đây là các schema changes chưa được migrate từ các sprint trước (Loyalty L-B/L-C) được gộp chung vào migration CommunitySprint0. Không rename migration file (đã applied trên VPS PG).
- [x] **SC10:** Unit tests ≥25 cases pass (v1.2: tăng từ 22 — +DeviceRegistration 4 cases, +FraudFlag 3 cases, +RiskScoring 5 cases) — PASS (pre-push CI: 1009 tests total, 42 community-specific)
- [x] **SC11:** `dotnet build VanAn.sln` 0 errors — PASS (pre-push CI: 0 errors, 1 warning)
- [x] **SC12:** `guard-check.ps1` ALL CHECKS PASSED — PASS (local verify 2026-07-26)
- [x] **SC13:** Architecture tests pass — PASS (pre-push CI: 39 arch tests total). **Note (F7 fix 2026-07-26):** Sprint 0 chỉ ADD 2 immutability tests (`WalletTransaction_Immutable_NoPublicSetter` + `WalletTransaction_NoUpdateMethod` trong `WalletTransactionImmutabilityTests.cs`). 37 arch tests còn lại là PRE-EXISTING (VA-DDD-002 Domain layer dependency check, etc.) — không phải Sprint 0 deliverable. SC13 phrasing gốc gây hiểu lầm quy công lao pre-existing tests cho Sprint 0.
- [x] **SC14:** OTP login regression test pass (**OPTIONAL trong v1.2 — SMS không bắt buộc**) — N/A (deferred per v1.2)
- [x] **SC15:** Social login (Google) regression test pass — verify existing `SocialAuthController` vẫn hoạt động — PASS (VPS: `/api/auth/google/login` → 302, `/api/auth/google/callback` → 302, no 500/404)
- [x] **SC16 (v1.2 NEW):** Device fingerprint JS (FingerprintJS, self-host) load thành công — `wwwroot/js/fingerprint.js` + `wwwroot/lib/fingerprintjs/fingerprint.js` tồn tại — PASS (VPS KhachLink: both served, contains 'FingerprintJS' + 'window.fingerprint.collect'). **Note (F1 fix 2026-07-26):** Sprint 0 originally vendored a STUB placeholder (FNV-1a hash, not real fingerprinting). F1 fix replaced stub with real FingerprintJS v5.2.0 (UMD build, ~37KB). **License correction:** Task card specified "v4 (MIT)" but FingerprintJS v4 is actually BUSL-1.1 (Business Source License — restricts production use). v5+ is properly MIT licensed. Upgraded to v5.2.0 (MIT). API compatible: `FingerprintJS.load()` + `agent.get()` → `{ visitorId, components }`.
- [x] **SC17 (v1.2 NEW):** RiskScoringService compute deterministic score — 8 factors, unit test verify cùng input → cùng output — PASS (pre-push CI: 6 RiskScoringServiceTests + 7 risk score entity tests)
- [x] **SC18 (v1.2 NEW):** DeviceRegistration entity có max 3 active per Customer constraint — unit test verify throw khi insert device 4th — PASS (pre-push CI: 4 DeviceRegistrationTests). **Note (F3 fix 2026-07-26):** Sprint 0 SC18 claimed max-3 enforcement but NO production code existed — only 4 entity tests (Create/Touch/Deactivate/Verify), none tested the max-3 constraint. F3 fix added `IDeviceRegistrationService` + `DeviceRegistrationService` (application-layer enforce: count active before insert, device 4+ → IsActive=false + FraudFlag) + 6 service tests (first/third/fourth/fifth device, deactivated device, independent customer counts). DI registered in Gateway Program.cs.

**Implementation Date:** 2026-07-26
**Branch:** `feature/community-sprint0-foundation` → merged to `main` (fast-forward `89e33480..f563e415`)
**Commit:** `e1a75bbf` (feat) + `f563e415` (docs/state)
**VPS Deployment:** 2026-07-26 12:11 UTC (CD run #30201482750, all 3 jobs PASS in 5m13s)
**VPS RV:** 2026-07-26 12:15 UTC — ALL 18 SC PASS (40/40 effective checks; 4 script false positives explained)

---

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Verify entity creation, state machine, immutability
- `system-refactor-safety` — Safe addition of entities without breaking existing
- `build-error-analysis` — Fix EF Core configuration/migration errors

---

## 7. AI HEALTH CHECK MATRIX (INITIAL) — v1.1
- **Evidence Count:** 14
- **Verified Facts:**
  - Fact 1: `BaseEntity` trong `1_Shared/Domain/Common.cs:75` — có Id, TenantId, CreatedAt, UpdatedAt, IsDeleted
  - Fact 2: `IMustHaveTenant` trong `1_Shared/Domain/Common.cs:55` — yêu cầu TenantId
  - Fact 3: `ApplyConfigurationsFromAssembly` trong `VanAnDbContext.cs:174` — auto-discovery EF configs
  - Fact 4: `CustomerConfiguration.cs` — pattern: Ignore business key, HasConversion cho TenantId, HasQueryFilter
  - Fact 5: `OrderConfiguration.cs` — pattern: OwnsOne cho value objects, HasKey(Id), Ignore(OrderId)
  - Fact 6: Google auth ĐÃ TỒN TẠI: `SocialAuthController.cs` — GoogleLogin + GoogleCallback (v1.1: không build mới)
  - Fact 7: OTP auth đã tồn tại: `CustomerIdentityController.cs` — SendOtp + VerifyOtp
  - Fact 8: `IVanAnDbContext.cs` — interface pattern cho DbSet declarations
  - Fact 9: `VanAnDbContext.cs:135-148` — Ignore list cho value objects (OrderId, CustomerId, etc.)
  - Fact 10: CI/CD dùng .NET 8.0.x (`ci.yml:19`)
  - Fact 11: `Customer` entity có `IdentityLevel` enum (Guest=0, Social=1, Verified=2, Full=3)
  - Fact 12: `Order` entity có `OrderType` (string field, KHÔNG phải enum — values "DINEIN", "TAKEAWAY", "DELIVERY") + `DeliveryAddress` (string)
  - Fact 13 (v1.1 NEW): `Order.Create` (Domain.cs:1467-1478) — reference impl Single-Identity Pattern (Id = OrderId.Value sync)
  - Fact 14 (v1.1 NEW): `OrderStatuses.Default[]` (Domain.cs:458-508) CHỈ có 6 trạng thái — KHÔNG có "delivering" (cần CC-S1-T0 ở Sprint 1)
- **Assumptions:**
  - SQLite migration sẽ cần riêng (ShopERP dùng SQLite, Gateway dùng PG)
  - Community entities cross-tenant trên Gateway PG (không tenant query filter)
  - 9 entity mới dùng `BaseEntity.Id` trực tiếp (không business key VO) — đơn giản, tránh dual-identity bug
- **Open Questions:**
  - Q1: Community entities có cần tenant query filter trên ShopERP SQLite không? (Likely yes — shipper trong 1 tenant)
  - Q2: `SalesmanCode` generate algorithm — random 6 chars + uniqueness check?
  - Q3: `WalletTransaction` có cần `AccountingEntry` linkage không? (Likely no cho PoC)
  - Q4 (v1.1 NEW): `ProductReferralConfig.ProductShortCode` — generate auto hay sysadmin set manual? (Likely sysadmin set, fallback ProductId)
- **Recommended Action:** PROCEED — Assumptions (3) < Verified Facts (14), Open Questions (4) = 4 (borderline, resolve Q4 trong JIT Planning — default sysadmin set manual)

---

## 8. REVERSE IMPACT ANALYSIS — v1.1
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `1_Shared/Domain.cs` | Thêm 9 entities + 8 Order fields — không sửa existing | Backward compatible, nullable fields. Domain Modification approved. |
| `IVanAnDbContext.cs` | Thêm 9 DbSet — interface expansion | Default implementation trong VanAnDbContext |
| `VanAnDbContext.cs` | Thêm 9 DbSet | Auto-discovery pick up configs |
| `OrderConfiguration.cs` | Thêm config cho 8 new fields (v1.1: + ReferralProductId) | Nullable columns, no break |
| ~~`CustomerConfiguration.cs`~~ | ~~Thêm config cho new fields~~ (v1.1: bỏ — không thêm Customer fields) | — |

---

## 9. TDD & E2E TESTING STRATEGY — v1.1
- **Unit Tests (Domain) — ≥15 cases:**
  - `CommunityRoleTests.cs` — entity creation, SalesmanCode generation, activate/deactivate (4 cases)
  - `DeliveryTaskTests.cs` — state machine transitions, invalid transitions throw (6 cases)
  - `WalletTransactionTests.cs` — immutability, BalanceAfter calculation, Reversal entry (v1.1: +Reversal) (3 cases)
  - `OrderCommunityFieldsTests.cs` — 8 new nullable fields exist, default null (v1.1: +ReferralProductId) (1 case)
  - `SalesReferralTests.cs` (v1.1 NEW) — composite code, AttachToOrder với per-product commission, AttachAppInstallBonus (2 cases)
  - `ProductReferralConfigTests.cs` (v1.1 NEW) — CommissionRate 2-5%, AppInstallBonus, ProductShortCode (1 case)
  - `AppInstallAttributionTests.cs` (v1.1 NEW) — unique per Customer, snapshot BonusAmount (1 case)
- **Architecture Tests (v1.1 NEW):**
  - `WalletTransactionImmutabilityTests.cs` — `WalletTransaction_Immutable_NoPublicSetter` + `WalletTransaction_NoUpdateMethod` (reflection check)
- **Integration Tests:**
  - Migration apply test (PG + SQLite)
  - DbSet CRUD smoke test
- **E2E tests:** None cho Sprint 0 (no UI/API)
- **Test boundary:** Unit tests + architecture tests + build verification

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES) — v1.2: 4 sessions

### Chiến lược: JIT Planning + Pure Execution
Mỗi session: Phase 1 (chốt exact code) → user approve → Phase 2 (viết code). Không re-explore.

### Micro-phase breakdown cho Sprint 0 (v1.2: 4 sessions thay vì 3)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Entity definitions: 11 entities (v1.2: +DeviceRegistration, +FraudFlag), 9 enums (v1.2: +3 fraud enums), RiskScore fields trên SalesReferral/AppInstallAttribution, IdentityLevel.DeviceVerified=4, Single-Identity Pattern | `Domain.cs` — 11 entities + 9 enums + 8 Order fields + SalesReferral/AppInstallAttribution RiskScore fields + 9 unit test files (25+ cases) |
| **S2** | EF Configuration: 11 table names, columns, indexes, unique constraints (DeviceRegistration.DeviceToken unique, FraudFlag.Status+CreatedAt index, DeviceRegistration.CustomerId+IsActive index, FingerprintHash index) | 11 Configuration files + IVanAnDbContext + VanAnDbContext updates + architecture test file + RiskScoringService skeleton |
| **S3** | Risk scoring service: 8 factors deterministic logic, RiskScoringService unit tests (5 cases), device fingerprint JS integration | RiskScoringService full impl + 5 risk scoring unit tests + FingerprintJS vendored + `wwwroot/js/fingerprint.js` interop |
| **S4** | Migration: PG + SQLite migration script (11 tables), regression test (OTP + Google login existing), final build | `dotnet ef migrations add CommunitySprint0` (PG + SQLite) + apply + verify + regression tests + guard-check + build |

### Rules
- TDD: test TRƯỚC code trong Session S1
- Mỗi session kết thúc: `dotnet build` pass
- Session S4 kết thúc: migration apply thành công + regression pass + tất cả SC pass + VPS ready
- v1.2: Session S3 NEW (risk scoring + device fingerprint JS) — tách khỏi S2 để tránh session quá lớn

---

## 11. COMPLETION SUMMARY

**Status: COMPLETE 2026-07-26** (merged to main + VPS deployed + RV PASS)

### Deliverables
1. **Domain (S1):** 11 entities + 9 enums + 8 Order fields + IdentityLevel.DeviceVerified=4 added to `1_Shared/Domain.cs`. Single-Identity Pattern applied (all entities use `BaseEntity.Id` directly).
2. **Unit Tests (S1):** 40 community test cases in `6_Tests/VanAn.Core.Tests/Community/` (11 files) + 2 architecture tests in `VanAn.Architecture.Tests/WalletTransactionImmutabilityTests.cs`. ALL 42 PASS.
3. **EF Configurations (S2):** 11 new config files in `3_CoreHub/Infrastructure/Configurations/`. `OrderConfiguration` modified (+8 fields +3 indexes). 11 DbSets added to `IVanAnDbContext` + `VanAnDbContext` + `ShopERPDbContext`.
4. **Services (S3):** `IRiskScoringService` + `RiskScoringService` (deterministic 8-factor scoring, cap 100). `IWalletService` + `WalletService` (v1.4 base atomic CreateTransactionAsync — HR-SCALE-3 SELECT FOR UPDATE on PG). DI registered in `2_Gateway/Program.cs`.
5. **FingerprintJS (S3):** `5_WebApps/KhachLink/wwwroot/js/fingerprint.js` (JS interop wrapper) + `5_WebApps/KhachLink/wwwroot/lib/fingerprintjs/fingerprint.js` (vendored stub — replace with real FingerprintJS v4 MIT before production).
6. **Migration (S4):** `20260726105331_CommunitySprint0` generated + applied to local PG + VPS PG. 11 new Community tables + 8 Order columns + 7 RiskScore/HoldUntil/DeviceRegistrationId columns verified.
7. **Guard Check (S4):** `guard-check.ps1` ALL CHECKS PASSED.

### CI/CD
- Pre-push CI: build PASS (265s) + 1009 unit tests PASS + 39 architecture tests PASS.
- CD run #30201482750: Build & Push (3m42s) + Pre-Deploy Validation (10s) + Deploy to VPS (1m21s) — ALL PASS.
- VPS RV: 7 containers healthy, 3 DLLs deployed 2026-07-26 12:1x, migration recorded, 11 tables + 8 Order columns + 7 extra columns verified on PG, FingerprintJS served, Google login regression PASS.

### Known Follow-ups
- Replace FingerprintJS stub with real FingerprintJS v4 (MIT) before production deployment.
- Sprint 1 (Nearby Orders) requires Domain Modification #2: `OrderStatuses.Default[]` + "delivering" status + OrderWorkflowService transitions.

---

## 12. ESTIMATED EFFORT — v1.3
- 3 sessions theo JIT Planning (v1.3: giảm từ 4 — bỏ CC-S0-T3 SQLite migration, community entities PG ONLY)
- **BLOCKER:** None (greenfield additions, không phụ thuộc external services)
