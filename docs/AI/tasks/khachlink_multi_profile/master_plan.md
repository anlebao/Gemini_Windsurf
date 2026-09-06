# MASTER PLAN: KhachLink Multi-Profile (5 Loại KhachLink)

> **Created:** 2026-08-15
> **Last Updated:** 2026-09-04 (Sprint 7 EXPANDED — added Owner-assign-role tasks per user request. R1 base unchanged.)
> **Source:** User request — 5 loại KhachLink (Directory / Logistics / JobMarket / FullCommerce / Reseller)
> **Branch:** `main` (R1 merged — `5047ed8c` Merge + `b3af97a1` enable workflow)
> **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT, 7 steps)
> **Domain modification:** YES — approved by user 2026-08-15 (`KhachLinkInstance` aggregate + `KhachLinkNavFlags` VO + `KhachLinkProfile` enum)
> **Plan file:** `C:\Users\lebao\.devin\plans\plan-69592e1cef008788.md`

## Release Status

| Release | Sprints | Branch | Status |
|---|---|---|---|
| **R1** — Multi-Profile Core + Type 1 + 4 + Multi-domain | 1-6 | `main` (merged `5047ed8c` + enabled `b3af97a1`) | ✅ COMPLETE + MERGED + ENABLED |
| **R2** — Type 5 Reseller + Owner Role Assignment | 7 (expanded) | `feature/khachlink-multi-profile-r2` | ⏳ PENDING |
| **R2.2** — Reseller Accounting-Cashflow Alignment | 7.2 | `feature/reseller-accounting-fix` (from main after R2 merge) | ⏳ PENDING — M2+ design approved 2026-09-05, docs ready for review |
| **R3** — Type 2 Logistics + Type 3 JobMarket | 8-9 | `feature/khachlink-multi-profile-r3` | ⏳ PENDING |

> **2026-09-04 Sprint 7 expansion:** Original Sprint 7 covered only Reseller preset + UI enable + CommerceMode verify + tests. User-identified gap: Reseller owner (tenant owner) cần toàn quyền chỉ định KhachLink customer làm Salesman/Shipper. Investigation confirm: existing `CommunityAdminService` chỉ cho phép **SystemAdmin** activate roles (`[Authorize(Policy="SystemAdmin")]` on `CommunityAdminController` + `AdminPanel.razor`). R2 expanded to add TenantOwner-scoped role assignment flow. Reuses existing `RequireOwnerRole` policy (`tenant_id` claim + `Owner` role). Customer entity `: BaseEntity, IMustHaveTenant` — có TenantId sẵn.

> **2026-09-04 R2.2 — Reseller Accounting Fix (DEFERRED from R2):** Investigation discover accounting-cashflow mismatch in Reseller mode. `OrderService.GenerateAccountingEntriesAsync` (line 162-254) has **NO `CommerceMode` branch** — all orders generate identical Revenue(511) + VAT(3331) + COGS(632) on `order.TenantId`'s books. But `WalletService.ConfirmCodResellerAsync` (line 223-336) shows Vạn An (Platform) is the middleman: order.TenantId = SUPPLIER receives only `costPrice` via Wallet Settlement, while customer pays `sellPrice`. Margin (sellPrice - costPrice) is split by Vạn An into PlatformFee + CommunityFund + Commission + VanAn net profit — but NONE of this margin split is reflected in `AccountingEntry`/`JournalEntry`. Hệ quả: supplier tenant's accounting shows inflated revenue (sellPrice) they never receive + gross margin they don't keep → violates TT 152/2025/TT-BTC cash-basis principle (revenue recognition by actual receipt). **Deferred to separate release** to keep Sprint 7 scope manageable + avoid touching immutable AccountingEntry patterns in same sprint as feature work.
>
> **2026-09-05 R2.2 — M2+ DESIGN APPROVED (refined after deeper investigation + user clarifications):**
> - **User clarifications received 2026-09-05:**
>   - Q4: Platform (Vạn An) accounting entries — **CẦN LUÔN** trong R2.2 (not deferred)
>   - Ai xuất hóa đơn VAT cho customer? **Reseller tenant** (có thể = Vạn An hoặc tenant khác)
>   - M1 có đủ? **Không, cần M2 (Platform entries) luôn**
>   - VAT treatment: **assume standard "mua-bán qua đại lý"** (no kế toán confirm yet)
>   - Scope R2.2: **fix accounting + thêm UI/report cho auditor**
>   - Platform Tenant concept: **Vạn An có tenant ID riêng cho kế toán**
>   - Platform entries tạo ở **PG (Gateway)**
> - **Approved M2+ design (3 tenant booksets per Reseller order):**
>   - **Supplier tenant books** (`order.TenantId`): Revenue 511 = `order.CostPrice`, VAT 3331 = VAT trên `CostPrice`, COGS 632 = `Sum(Product.CostPrice × Qty)` (unchanged — production cost)
>   - **Reseller tenant books** (`order.OwnerTenantId` — NEW Domain field): Revenue 511 = `order.SellPrice`, VAT 3331 = VAT trên `SellPrice`, COGS 632 = `order.CostPrice`, VAT input 1331 = VAT trên `CostPrice` (khấu trừ)
>   - **Platform (Vạn An) tenant books** (`SystemSetting "PlatformAccountingTenantId"`): Revenue 511 = `PlatformFeeAmount + CommunityFundShare` — **chỉ khi Reseller ≠ Vạn An** (skip when Reseller = Vạn An, margin đã nằm trong Reseller's gross profit)
> - **Domain mod approved (small):** Add `Order.OwnerTenantId` (Guid?) + param to `SetResellerPricing`. Set during `SnapshotCommerceModeAsync` (lookup `KhachLinkInstance` by domain).
> - **Auditor UI report:** New ShopERP Admin page `/admin/reseller-accounting-reconciliation` — shows per Reseller order: supplier/reseller/platform entries + wallet transactions + VAT chain + margin split. Filter by date range + tenant. CSV/Excel export.
> - **Idempotency:** Reference suffix `#{orderId}-SUP`, `#{orderId}-RES`, `#{orderId}-PLT` — existing `PaymentConfirmedSubscriber` JournalEntry.Reference check covers new entries.
> - **VAT assumptions (standard "mua-bán qua đại lý"):** Supplier output VAT = VAT(costPrice); Reseller output VAT = VAT(sellPrice); Reseller input VAT (khấu trừ) = VAT(costPrice); Reseller net VAT payable = VAT(sellPrice) − VAT(costPrice).
> - **Trade-offs:** ~10 files touched, 2 migrations (PG + SQLite), 1 SystemSetting seed, Domain mod (1 nullable field). Larger than M1 (1 file) nhưng user approve M2+ vì cần Platform entries + auditor UI.

---

## 1. BUSINESS CONTEXT (5 Loại KhachLink)

| # | Tên | Profile enum | Mô tả | Owner | Release |
|---|---|---|---|---|---|
| 1 | Danh bạ | `Directory` | Shop/Doanh nghiệp/Sản phẩm directory — ẩn cart, rewards, redeem | Platform (null) | R1 |
| 2 | Logistics | `Logistics` | Sàn shipper — nhận đơn, push đơn. Reuse Community Commerce (Sprint 4-7 cũ) | Platform/tenant | R3 |
| 3 | Sàn việc | `JobMarket` | Sàn công việc/dịch vụ — list "job" như Product (name + price, chỉ khác text) | Platform/tenant | R3 |
| 4 | Full commerce | `FullCommerce` | KhachLink của 1 tenant cụ thể — domain riêng, đầy đủ chức năng TMĐT | 1 tenant (OwnerTenantId) | R1 |
| 5 | Reseller | `Reseller` | Tenant trung gian — bán sản phẩm/dịch vụ cho tenant con. Order level dùng `CommerceMode.Reseller` (existing) | 1 reseller tenant | R2 |

**Đơn giản hóa quan trọng (user-confirmed 2026-08-15):**
- Type 3 & 5 KHÔNG cần entity/model mới — sản phẩm/dịch vụ = existing `Product` entity (chỉ khác text content)
- Type 2 = reuse Community Commerce (Sprint 4-7 cũ)
- Type 5 order level = existing `CommerceMode.Reseller` (đã có sẵn)
- Toàn bộ feature build = Phase 1 (R1) + 2 small releases (R2, R3)

---

## 2. ARCHITECTURE DECISIONS

| Decision | Rationale |
|---|---|
| **Entity `KhachLinkInstance` riêng** (không gắn vào Tenant) | Type 1 danh bạ không thuộc tenant nào → `OwnerTenantId = null`. 1 tenant có thể có nhiều instance. Platform instances không bị trói buộc tenant lifecycle. |
| **Follow `ShopInstance` pattern** | Platform-level routing entity, `BaseEntity` (not AggregateRoot), `TenantId = Guid.Empty` sentinel, excluded from multi-tenancy query filter. Reference: `1_Shared/Domain/ShopInstance.cs` |
| **Single deployment + multi-domain routing** | Cùng 1 KhachLink container, nginx wildcard `*.khachvip.online`, runtime fetch config qua `GET /api/v1/khachlink-instances/by-domain/{domain}`. KHÔNG tạo 5 Docker image riêng. |
| **SSL: SAN expand pattern** | Dùng cùng pattern `scripts/init-ssl-multivps.sh` — `certbot --expand -d <subdomain>`. KHÔNG dùng wildcard cert (tránh DNS challenge phức tạp). |
| **Feature flag `KhachLink:MultiProfileEnabled` default OFF** | Zero regression — existing deployment unchanged. Giống Guard QR pattern. Toggle ON cho test. |
| **NavMenu refactor: hardcoded → flag-driven** | 15 nav items wrap trong `@if (_navFlags.ShowXxx)`. Default `KhachLinkNavFlags` = all true (FullCommerce) → existing behavior preserved. |
| **`ShowJobs` = Option B** | Route `/jobs.razor` wrapper reuse `/stores` component + filter products by keyword (job/việc/dịch vụ/service). 1 Razor page nhỏ, không entity build. |
| **Bỏ Sprint 0 (ANALYZE)** | Đã analyze đầy đủ trong session design — NavMenu, TenantSettings, ShopConfigHttpService, nginx config đã đọc. Vào thẳng Sprint 1. |

---

## 3. DOMAIN MODEL

### KhachLinkProfile (Enum)
```csharp
public enum KhachLinkProfile
{
    FullCommerce = 0,   // Type 4 — default, all features on
    Directory = 1,      // Type 1 — directory only
    Logistics = 2,      // Type 2 — community commerce focus (R3)
    JobMarket = 3,      // Type 3 — job/service marketplace (R3)
    Reseller = 4        // Type 5 — tenant trung gian (R2)
}
```

### KhachLinkNavFlags (Value Object — Owned Entity)
```
15 boolean flags:
ShowHome, ShowCart, ShowOrders, ShowLoyaltyHistory, ShowMissions,
ShowRewards, ShowAllianceWallet, ShowStores, ShowCampaigns, ShowScan,
ShowQrClaim, ShowCommunity, ShowJobs, ShowProfile, ShowStaffDashboard

Factory: ForProfile(KhachLinkProfile) → preset
- FullCommerce: all true
- Directory: ShowHome/Stores/Profile true, rest false
- Logistics: ShowHome/Stores/Profile/Community true, rest false (R3)
- JobMarket: ShowHome/Stores/Profile/Jobs true, rest false (R3)
- Reseller: all true (R2)
```

### KhachLinkInstance (Entity — follows ShopInstance pattern)
```
- Id (PK, Guid)           ← Single-Identity pattern (no business key VO)
- TenantId (Guid)         ← always Guid.Empty (platform sentinel, excluded from multi-tenancy filter)
- Label (string, 200)     ← human-readable
- Profile (KhachLinkProfile enum, stored as int)
- CustomDomain (string, 255, unique) ← "shopA.khachvip.online"
- OwnerTenantId (Guid?)   ← null = platform-level; non-null = tenant-owned (Type 4, 5)
- NavFlags (owned KhachLinkNavFlags) ← 15 flattened bool columns
- IsActive (bool, default true)
- CreatedAt, UpdatedAt, IsDeleted (from BaseEntity)

Methods:
- Create(label, profile, customDomain, ownerTenantId?, navFlagsOverride?) → new instance
- UpdateProfile(profile, navFlagsOverride?) → update profile + reset nav flags to preset
- UpdateNavFlags(flags) → override individual flags
- Activate() / Deactivate()
```

**NOT AggregateRoot** — no domain events (routing config entity, like ShopInstance).
**NOT `KhachLinkInstanceId` VO** — Single-Identity Pattern, `Id = PK only`.

---

## 4. API CONTRACT (Gateway — new)

| Endpoint | Method | Auth | Input | Output |
|---|---|---|---|---|
| `/api/v1/khachlink-instances` | GET | SystemAdmin | — | `KhachLinkInstanceDto[]` |
| `/api/v1/khachlink-instances/{id}` | GET | SystemAdmin | — | `KhachLinkInstanceDto` |
| `/api/v1/khachlink-instances/by-domain/{domain}` | GET | AllowAnonymous | — | `KhachLinkInstanceDto` (404 if not found OR flag OFF) |
| `/api/v1/khachlink-instances` | POST | SystemAdmin | `CreateKhachLinkInstanceRequest` | `KhachLinkInstanceDto` (201) |
| `/api/v1/khachlink-instances/{id}` | PUT | SystemAdmin | `UpdateKhachLinkInstanceRequest` | `KhachLinkInstanceDto` |
| `/api/v1/khachlink-instances/{id}` | DELETE | SystemAdmin | — | 204 (soft delete = Deactivate) |

---

## 5. SPRINT BREAKDOWN

### RELEASE R1 — "Multi-Profile Core + Type 1 + 4 + Multi-domain" (Sprints 1-6)

| Sprint | Goal | Task Card |
|---|---|---|
| 1 — Domain + Infrastructure | KhachLinkProfile enum + KhachLinkNavFlags VO + KhachLinkInstance entity + EF config + migration + seed | `sprint1_domain_infra_task_card.md` |
| 2 — Gateway API | Repository + Service + DTOs + Controller (6 endpoints) + DI register + feature flag check | `sprint2_gateway_api_task_card.md` |
| 3 — KhachLink Runtime | InstanceHttpService + KhachLinkLayout refactor (fetch+cascade) + NavMenu refactor (15 items → flag-driven) + header icons | `sprint3_khachlink_runtime_task_card.md` |
| 4 — SystemAdmin UI | ShopERP /admin/khachlink-instances page + KhachLinkInstanceApiClient + NavMenu link | `sprint4_admin_ui_task_card.md` |
| 5 — nginx + SSL | Wildcard server block + init-ssl-khachlink-instances.sh script + deployment guide update | `sprint5_nginx_ssl_task_card.md` |
| 6 — R1 Tests | Domain unit + service integration + API integration tests | `sprint6_r1_tests_task_card.md` |

### RELEASE R2 — "Type 5 Reseller + Owner Role Assignment" (Sprint 7 — expanded 2026-09-04)

| Sprint | Goal | Task Card |
|---|---|---|
| 7 — Reseller Profile + Owner Role Assignment | `ForProfile(Reseller)` preset + SystemAdmin UI enable + CommerceMode.Reseller integration verify + **TenantOwner-scoped role activation endpoints + Owner panel UI + tests** | `sprint7_reseller_task_card.md` |

### RELEASE R2.2 — "Reseller Accounting-Cashflow Alignment" (Sprint 7.2 — M2+ approved 2026-09-05)

| Sprint | Goal | Task Card |
|---|---|---|
| 7.2 — Reseller Accounting-Cashflow Alignment (M2+) | 3 tenant booksets per Reseller order (Supplier + Reseller + Platform-when-not-VA) + `Order.OwnerTenantId` Domain mod + `PlatformAccountingTenantId` SystemSetting seed + Auditor UI report + idempotency via reference suffix + ~10-12 tests | `sprint7_2_reseller_accounting_fix_task_card.md` |

### RELEASE R3 — "Type 2 Logistics + Type 3 JobMarket" (Sprints 8-9)

| Sprint | Goal | Task Card |
|---|---|---|
| 8 — Logistics Profile | `ForProfile(Logistics)` preset + SystemAdmin UI enable + Community Commerce verify + tests | `sprint8_logistics_task_card.md` |
| 9 — JobMarket + /jobs | `ForProfile(JobMarket)` preset + `/jobs.razor` page (reuse /stores + filter) + SystemAdmin UI enable + tests | `sprint9_jobmarket_task_card.md` |

---

## 6. EXECUTION ORDER

```
R1 (Sprints 1-6, sequential):
  Sprint 1 (Domain+Infra) → build pass → approval gate
    ↓
  Sprint 2 (Gateway API) → build pass → approval gate
    ↓
  Sprint 3 (KhachLink Runtime) → build pass → approval gate
    ↓
  Sprint 4 (Admin UI) → build pass → approval gate
    ↓
  Sprint 5 (nginx+SSL) → approval gate
    ↓
  Sprint 6 (Tests) → CI pass → R1 complete → merge → deploy → RV
    ↓
R2 (Sprint 7 — branch from main after R1 merge):
  Sprint 7 (Reseller + Owner Role Assignment) → build pass → merge → deploy → RV
    ↓
R2.2 (Sprint 7.2 — branch from main after R2 merge):
  Sprint 7.2 (Reseller Accounting Fix) → build pass → merge → deploy → RV
    ↓
R3 (Sprints 8-9 — branch from main after R2.2 merge):
  Sprint 8 (Logistics) → build pass
  Sprint 9 (JobMarket+/jobs) → build pass → merge → deploy → RV
    ↓
DONE
```

**Total: ~10 sprints across 4 releases**

---

## 7. CONSTRAINTS & COMPLIANCE

- **Domain PURE:** `KhachLinkInstance` trong `1_Shared/Domain/`, no EF Core attrs
- **Single-Identity:** No business key VO — `Id = PK only` (follows ShopInstance pattern)
- **Multi-tenancy:** `TenantId = Guid.Empty` (platform sentinel) — excluded from multi-tenancy query filter (added to exclusion list in `ApplyMultiTenancyFilters`)
- **Option C compliance:** KhachLink HTTP-only — `KhachLinkInstanceHttpService` calls Gateway API, no direct DbContext
- **UI Platform:** Admin page dùng UI Platform components (Gate 5)
- **Feature flag default OFF:** Zero regression cho existing deployment
- **AccountingEntry:** Not touched — immutable
- **Pattern #10 (Gateway charset):** If controller forwards content, strip charset from `Request.ContentType`
- **SSL SAN limit:** 100 SAN/cert (Let's Encrypt) — enough for MVP

---

## 8. ROLLBACK PLAN

- **R1:** Feature flag OFF → all KhachLink instances render FullCommerce default (NavMenu `_navFlags = new()` all true) → existing behavior restored
- **R2:** Disable Reseller option in admin UI (or flag OFF)
- **R3:** Disable Logistics/JobMarket options (or flag OFF)
- **Any:** `git revert <release-commit>` on main

---

## 9. SUCCESS CRITERIA (Definition of Done)

### R1
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` ALL PASSED
- [ ] CI pipeline ALL PASS (existing + new tests)
- [ ] SystemAdmin tạo KhachLinkInstance với profile Directory/FullCommerce
- [ ] Toggle 15 nav flags individually
- [ ] Type 1 (Directory): chỉ Home + Stores + Profile hiện
- [ ] Type 4 (FullCommerce): tất cả icons hiện (giống existing)
- [ ] Multi-domain: `shopA.khachvip.online` → render theo instance config
- [ ] Existing deployment (diemthuong2) → unchanged (flag OFF + seed instance)
- [ ] `OwnerTenantId` non-null → tenant context = owner tenant
- [ ] `OwnerTenantId` null → tenant context = LastInteractionService (existing)
- [ ] SSL SAN expand script hoạt động
- [ ] Deploy to VPS + RV pass

### R2
- [ ] Profile `Reseller` preset (all true)
- [ ] Reseller instance + OwnerTenantId=reseller → `CommerceMode.Reseller` áp dụng cho order
- [ ] SystemAdmin UI tạo reseller instance
- [ ] **Owner tenant có panel "Quản lý cộng tác viên" (`/community/owner-panel`) — `[Authorize(Roles="Owner")]`**
- [ ] **Owner thấy danh sách eligible customers của chính tenant mình (filter by JWT `tenant_id`)**
- [ ] **Owner activate Salesman/Shipper cho customer thuộc tenant mình — Gateway endpoint `[Authorize(Policy="RequireOwnerRole")]`**
- [ ] **IDOR guard: Owner tenant A KHÔNG activate cho customer tenant B (service verify `customer.TenantId == JWT tenant_id`)**
- [ ] **SystemAdmin vẫn truy cập được existing `/api/admin/community/*` (cross-tenant, unchanged)**
- [ ] Tests PASS · deploy + RV

### R3
- [ ] Profile `Logistics` preset (hide commerce, show community)
- [ ] Profile `JobMarket` preset (hide commerce, show /jobs)
- [ ] `/jobs.razor` page wrapper reuse `/stores` + filter
- [ ] SystemAdmin UI tạo Logistics + JobMarket instance
- [ ] Tests PASS · deploy + RV

### R2.2 — Reseller Accounting-Cashflow Alignment (M2+ approved 2026-09-05)
- [ ] **Domain mod:** `Order.OwnerTenantId` (Guid?) + param to `SetResellerPricing` (set during SnapshotCommerceModeAsync)
- [ ] **EF config + migrations:** `OrderConfiguration` map `OwnerTenantId` (PG migration + SQLite migration)
- [ ] **Service:** `SnapshotCommerceModeAsync` lookup `KhachLinkInstance` by domain, pass `OwnerTenantId` to `SetResellerPricing`
- [ ] **Service:** `GenerateAccountingEntriesAsync` Reseller branch (3 tenant booksets)
  - [ ] Supplier books (`order.TenantId`): Revenue 511 = `order.CostPrice`, VAT 3331 = VAT(CostPrice), COGS 632 = `Sum(Product.CostPrice × Qty)`
  - [ ] Reseller books (`order.OwnerTenantId`): Revenue 511 = `order.SellPrice`, VAT 3331 = VAT(SellPrice), COGS 632 = `order.CostPrice`, VAT input 1331 = VAT(CostPrice)
  - [ ] Platform books (`PlatformAccountingTenantId` SystemSetting) — only when Reseller ≠ Vạn An: Revenue 511 = `PlatformFeeAmount + CommunityFundShare`
- [ ] **SystemSetting seed:** `PlatformAccountingTenantId` = Vạn An's tenant Guid (1-time SysAdmin config)
- [ ] **Auditor UI:** `/admin/reseller-accounting-reconciliation` page — per-order supplier/reseller/platform entries + wallet transactions + VAT chain + margin split. Filter by date + tenant. CSV/Excel export
- [ ] **Idempotency:** Reference suffix `#{orderId}-SUP`/`-RES`/`-PLT` — `PaymentConfirmedSubscriber` covers via existing JournalEntry.Reference check
- [ ] Accounting = cashflow invariant test: sum of all AccountingEntry amounts per order = sum of all WalletTransaction amounts per order
- [ ] TT 152/2025/TT-BTC cash-basis compliance verified (cash-basis: revenue = actual cash received)
- [ ] VAT chain verified: Supplier output VAT(costPrice) → Reseller input VAT(costPrice) khấu trừ → Reseller output VAT(sellPrice)
- [ ] Domain purity preserved (no EF Core attrs in Domain — `Order.OwnerTenantId` is plain Guid? property)
- [ ] AccountingEntry immutability preserved (HARD STOP — append-only, no update/delete)
- [ ] Single-Identity Pattern preserved (`OwnerTenantId` is FK Guid, not value object — references `BaseEntity.Id`)
- [ ] Tests PASS (~10-12 new: Reseller accounting + auditor report)
- [ ] Build + guard + CI pass · deploy + RV (5 layers)

---

## 10. REFERENCES

- **Plan file:** `C:\Users\lebao\.devin\plans\plan-69592e1cef008788.md`
- **Release strategy:** `docs/AI/tasks/khachlink_multi_profile/release_strategy.md`
- **Reference entity:** `1_Shared/Domain/ShopInstance.cs` (platform-level routing entity pattern)
- **Reference EF config:** `3_CoreHub/Infrastructure/Configurations/ShopInstanceConfiguration.cs`
- **Reference DbContext:** `3_CoreHub/Infrastructure/VanAnDbContext.cs` (DbSet + exclusion list + Ignore list)
- **Existing NavMenu:** `5_WebApps/KhachLink/Components/Layout/NavMenu.razor` (340 lines, hardcoded)
- **Existing KhachLinkLayout:** `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor` (194 lines, header icons hardcoded)
- **Existing ShopConfigHttpService:** `5_WebApps/KhachLink/Services/Http/ShopConfigHttpService.cs` (HTTP fetch + fallback pattern)
- **Existing CommerceMode:** `1_Shared/Domain/Aggregates/OrderAggregate/CommerceMode.cs` (Marketplace/Reseller/Inherit)
- **Existing nginx:** `nginx/templates/vanan.multivps.conf.template` (470 lines, per-subdomain server blocks)
- **Existing SSL script:** `scripts/init-ssl-multivps.sh` (SAN expand pattern)
- **Guard QR reference:** `docs/AI/tasks/guard_qr_verify/` (master_plan + release_strategy + 7 task cards)
- **Deployment guide:** `docs/operations/Multi_VPS_Deployment_Guide.md`
