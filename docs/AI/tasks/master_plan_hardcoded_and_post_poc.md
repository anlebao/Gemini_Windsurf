# MASTER PLAN: Hardcoded Values (#3) + Post-PoC Gaps (#4)

> Created: 2026-08-13
> Source: API Testing & Codebase Scan findings (2026-08-13 session)

## TRIAGE MATRIX

| # | Item | Category | Actionable? | Sprint | Effort |
|---|---|---|---|---|---|
| 3a | Default Tenant ID in ProductReferralConfigService | Hardcode | YES | A | S |
| 3b | Default Tenant ID in SocialAuthController | Hardcode | YES | A | S |
| 3c | Default Tenant ID in CustomerIdentityController | Hardcode | YES | A | S |
| 3d | Default Tenant ID in PermissionGroupManagement | Hardcode | YES | A | S |
| 3e | Default Tenant ID in Domain.cs ShopConfig | Hardcode | LOW | A | M |
| 3f | Default Tenant ID in Program.cs / BirthdayBonus / VoucherExpiry | Config-override | NO (already config) | — | — |
| 3g | Localhost fallback URLs | Config-override | NO (already `?? config`) | — | — |
| 3h | SystemWalletIds (PlatformWallet, CommunityFund) | By-design | NO (system constants) | — | — |
| 4a | Settlement history page (`/admin/settlements`) | Post-PoC UI | YES | B | M |
| 4b | Tenant Settings page (`/admin/tenant-settings`) | Post-PoC UI | YES | B | M |
| 4c | Kitchen-initiated orders | Feature gap | FUTURE | C | L |
| 4d | Native app for background GPS | Post-PoC | OUT-OF-SCOPE | — | — |
| 4e | Native App Attestation (iOS/Android) | Post-PoC | OUT-OF-SCOPE | — | — |

## SPRINT A: Hardcoded Default Tenant ID Cleanup (4 files, Small)

**Goal:** Replace 4 truly-hardcoded `Guid.Parse("00000000-0000-0000-0000-000000000001")` with config-driven resolution.

**Pattern:** Use `IConfiguration["Seed:TenantId"]` with fallback (same as `Program.cs:665` already does).

**Files:**
1. `3_CoreHub/Services/ProductReferralConfigService.cs:52` — inject IConfiguration, read `Seed:TenantId`
2. `5_WebApps/ShopERP/Controllers/SocialAuthController.cs:136` — inject IConfiguration (already injected), read `Seed:TenantId`
3. `5_WebApps/ShopERP/Controllers/CustomerIdentityController.cs:265` — inject IConfiguration, read `Seed:TenantId`
4. `5_WebApps/ShopERP/Components/Pages/Admin/PermissionGroupManagement.razor.cs:146` — inject IConfiguration, read `Seed:TenantId`

**NOT changed:**
- `1_Shared/Domain.cs:2009` (ShopConfig) — record default, low risk, changing requires Domain modification approval
- `1_Shared/Domain/Common/SystemWalletIds.cs` — by-design system constants
- `Program.cs`, `BirthdayBonusJob.cs`, `VoucherExpiryReminderJob.cs` — already config-overridable

**Success Criteria:**
- [ ] 4 files use `IConfiguration["Seed:TenantId"]` with `Guid.TryParse` fallback
- [ ] `dotnet build` 0 errors
- [ ] guard-check.ps1 pass
- [ ] No behavior change in production (config already set)

---

## SPRINT B: Post-PoC UI — Settlement History + Tenant Settings (2 pages, Medium)

**Goal:** Implement 2 missing admin UI pages that currently require SystemAdmin API calls.

### B1: Settlement History Page (`/admin/settlements`)
- **Data source:** `WalletTransactions` where `Type == Settlement` (already exist in WalletService)
- **UI:** ShopERP Admin page, table of settlement transactions with filters (date range, order)
- **API:** Gateway already has `GET /api/community/wallet/transactions` (X-Customer-Token) — need admin variant or use existing ShopERP endpoints
- **No new domain entities** — purely UI + read API

### B2: Tenant Settings Page (`/admin/tenant-settings`)
- **Data source:** `Tenant.Settings` (TenantSettings value object — already exists with 15+ fields)
- **UI:** ShopERP Admin page, form to view/edit TenantSettings (ContactEmail, ContactPhone, Address, TaxCode, Latitude, Longitude, Slug, SocialLinks, BrandStory, Theme, NavColor, HeaderColor, FooterColor)
- **API:** Need `GET /api/admin/tenant-settings` + `PUT /api/admin/tenant-settings` (SystemAdmin or Owner)
- **No new domain entities** — TenantSettings already has `With*` immutable update methods

**Success Criteria:**
- [ ] Settlement history page shows real data from WalletTransactions
- [ ] Tenant Settings page shows + edits real TenantSettings
- [ ] UI Platform components (VanAnTable, VanAnCard, VanAnButton, VanAnForm)
- [ ] `dotnet build` 0 errors + guard-check pass
- [ ] E2E test for each page (Gate 4: UI layout change → E2E test)

---

## SPRINT C: Kitchen-Initiated Orders (FUTURE — not in this plan)

**Why deferred:** Requires Order entity modification (new creation source enum), OrderService new flow, Kitchen UI change. Scope too large for current sprint.

---

## OUT OF SCOPE

- **Native app background GPS** (4d) — requires iOS/Android native development
- **Native App Attestation** (4e) — requires iOS/Android native development
- **SystemWalletIds** (3h) — by-design system constants
- **Localhost fallback URLs** (3g) — already config-overridable, production config verified

---

## EXECUTION ORDER

1. **Sprint A first** (small, low risk, no UI, no Domain changes)
2. **Sprint B second** (medium, UI + read/write APIs, no Domain changes)
3. **Sprint C deferred** (future sprint, needs Domain modification approval)

## CONSTRAINTS

- NO Domain.cs modifications (all needed entities/value objects already exist)
- NO new migrations (no schema changes)
- UI Platform components mandatory (Gate 5)
- E2E tests required for UI pages (Gate 4)
