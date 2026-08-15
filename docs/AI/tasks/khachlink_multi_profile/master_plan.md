# MASTER PLAN: KhachLink Multi-Profile (5 Loại KhachLink)

> **Created:** 2026-08-15
> **Last Updated:** 2026-08-15 (R1 COMPLETE — all 6 sprints merged to `main` via `5047ed8c` + enabled via `b3af97a1`. timlathay.com live as Directory type)
> **Source:** User request — 5 loại KhachLink (Directory / Logistics / JobMarket / FullCommerce / Reseller)
> **Branch:** `main` (R1 merged — `5047ed8c` Merge + `b3af97a1` enable workflow)
> **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT, 7 steps)
> **Domain modification:** YES — approved by user 2026-08-15 (`KhachLinkInstance` aggregate + `KhachLinkNavFlags` VO + `KhachLinkProfile` enum)
> **Plan file:** `C:\Users\lebao\.devin\plans\plan-69592e1cef008788.md`

## Release Status

| Release | Sprints | Branch | Status |
|---|---|---|---|
| **R1** — Multi-Profile Core + Type 1 + 4 + Multi-domain | 1-6 | `main` (merged `5047ed8c` + enabled `b3af97a1`) | ✅ COMPLETE + MERGED + ENABLED |
| **R2** — Type 5 Reseller | 7 | `feature/khachlink-multi-profile-r2` | ⏳ PENDING |
| **R3** — Type 2 Logistics + Type 3 JobMarket | 8-9 | `feature/khachlink-multi-profile-r3` | ⏳ PENDING |

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

### RELEASE R2 — "Type 5 Reseller" (Sprint 7)

| Sprint | Goal | Task Card |
|---|---|---|
| 7 — Reseller Profile | `ForProfile(Reseller)` preset + SystemAdmin UI enable + CommerceMode.Reseller integration verify + tests | `sprint7_reseller_task_card.md` |

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
  Sprint 7 (Reseller) → build pass → merge → deploy → RV
    ↓
R3 (Sprints 8-9 — branch from main after R2 merge):
  Sprint 8 (Logistics) → build pass
  Sprint 9 (JobMarket+/jobs) → build pass → merge → deploy → RV
    ↓
DONE
```

**Total: ~9 sprints across 3 releases**

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
- [ ] Tests PASS · deploy + RV

### R3
- [ ] Profile `Logistics` preset (hide commerce, show community)
- [ ] Profile `JobMarket` preset (hide commerce, show /jobs)
- [ ] `/jobs.razor` page wrapper reuse `/stores` + filter
- [ ] SystemAdmin UI tạo Logistics + JobMarket instance
- [ ] Tests PASS · deploy + RV

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
