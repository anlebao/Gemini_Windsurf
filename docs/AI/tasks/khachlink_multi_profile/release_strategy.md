# RELEASE STRATEGY — KhachLink Multi-Profile (5 Loại KhachLink)

> **Created:** 2026-08-15
> **Principle:** Mỗi release = 1 branch → 1 PR → 1 merge → 1 deploy → giá trị nhìn thấy được
> **Conflict avoidance:** Sequential releases, mỗi release branch từ `main` mới nhất (sau release trước đã merge)
> **Reference:** `docs/AI/tasks/guard_qr_verify/release_strategy.md`

## RELEASE STATUS

| Release | Sprints | Branch | Status |
|---|---|---|---|
| **R1** — Multi-Profile Core + Type 1 + 4 + Multi-domain | 1-6 | `feature/khachlink-multi-profile-r1` | 🔵 IN PROGRESS |
| **R2** — Type 5 Reseller + Owner Role Assignment | 7 (expanded) | `feature/khachlink-multi-profile-r2` | ⏳ PENDING |
| **R2.2** — Reseller Accounting Fix | 7.2 | `feature/reseller-accounting-fix` | ⏳ PENDING — gap documented 2026-09-04 |
| **R3** — Type 2 Logistics + Type 3 JobMarket | 8-9 | `feature/khachlink-multi-profile-r3` | ⏳ PENDING |

---

## 1. BRANCH STRATEGY (No Conflict)

```
main (always-green)
 │
 ├── feature/khachlink-multi-profile-r1 (Sprint 1-6)
 │     ↓ PR → review → merge → deploy → RV
 │     └── main updated (R1 merged)
 │
 ├── feature/khachlink-multi-profile-r2 (Sprint 7 — from main after R1)
 │     ↓ PR → review → merge → deploy → RV
 │     └── main updated (R2 merged)
 │
 ├── feature/reseller-accounting-fix (Sprint 7.2 — from main after R2)
 │     ↓ PR → review → merge → deploy → RV
 │     └── main updated (R2.2 merged)
 │
 └── feature/khachlink-multi-profile-r3 (Sprint 8-9 — from main after R2.2)
       ↓ PR → review → merge → deploy → RV
       └── main updated (R3 merged — DONE)
```

**Rules:**
- **SEQUENTIAL** — không bao giờ 2 branch song song (tránh merge conflict)
- Mỗi release branch từ `main` SAU khi release trước đã merge
- Mỗi PR squash merge (1 commit per release trên main)
- Feature flag `KhachLink:MultiProfileEnabled` default OFF → merge an toàn, không ảnh hưởng production cho đến khi toggle ON

---

## 2. RELEASE PLAN (3 releases, mỗi release có giá trị rõ ràng)

### RELEASE R1 — "Multi-Profile Core + Type 1 + Type 4 + Multi-domain" (Sprints 1-6)

| | |
|---|---|
| **Sprints** | 1 (Domain+Infra) + 2 (Gateway API) + 3 (KhachLink Runtime) + 4 (Admin UI) + 5 (nginx+SSL) + 6 (Tests) |
| **Branch** | `feature/khachlink-multi-profile-r1` |
| **Feature flag** | `KhachLink:MultiProfileEnabled` = OFF (default), toggle ON cho test |

**Giá trị nhìn thấy được:**
- ✅ SystemAdmin tạo KhachLinkInstance với profile **Directory** (Type 1) hoặc **FullCommerce** (Type 4)
- ✅ Toggle 15 nav flags individually (override preset)
- ✅ Type 1 (Directory): chỉ Home + Stores + Profile hiện — danh bạ thuần
- ✅ Type 4 (FullCommerce): tất cả icons hiện — TMĐT đầy đủ cho 1 tenant
- ✅ **Multi-domain routing** — `shopA.khachvip.online` → render theo instance config
- ✅ Existing deployment (diemthuong2) → unchanged (flag OFF + seed instance FullCommerce)
- ✅ `OwnerTenantId` non-null → tenant context = owner tenant
- ✅ SSL SAN expand script hoạt động (thêm subdomain không phá cert cũ)

**Demo script:**
```
1. SystemAdmin login ShopERP → /admin/khachlink-instances → create Directory instance (danhba.khachvip.online)
2. DNS A record danhba.khachvip.online → gateway IP, run init-ssl-khachlink-instances.sh
3. Toggle flag ON → truy cập https://danhba.khachvip.online → chỉ Home + Stores + Profile hiện
4. Truy cập https://diemthuong2.khachvip.online → tất cả icons hiện (seed instance FullCommerce)
5. Toggle flag OFF → cả 2 domain render FullCommerce default (fallback)
```

**Không có trong R1:**
- ❌ Type 2 Logistics (R3)
- ❌ Type 3 JobMarket (R3)
- ❌ Type 5 Reseller (R2)

**Rollback:** Feature flag OFF → all KhachLink instances render FullCommerce default → existing behavior restored (< 1 min)

---

### RELEASE R2 — "Type 5 Reseller + Owner Role Assignment" (Sprint 7 — expanded 2026-09-04)

| | |
|---|---|
| **Sprints** | 7 (Reseller Profile + CommerceMode integration + Owner Role Assignment) |
| **Branch** | `feature/khachlink-multi-profile-r2` (from main after R1 merge) |
| **Feature flag** | ON (R1 đã RV) |

**Giá trị nhìn thấy được:**
- ✅ Profile `Reseller` preset (all nav flags true)
- ✅ SystemAdmin tạo Reseller instance (reseller.khachvip.online, owner=reseller tenant)
- ✅ KhachLink instance profile=Reseller + OwnerTenantId=reseller → tenant context = reseller
- ✅ Orders tạo trên reseller instance → snapshot `CommerceMode.Reseller` (existing flow via `TenantSettings.CommerceModeOverride` or `GlobalCommerceMode`)
- ✅ Tenant trung gian bán sản phẩm/dịch vụ cho tenant con — products = existing Product entity (chỉ khác text)
- ✅ **NEW: Owner tenant (Reseller owner) tự quản lý cộng tác viên — panel `/community/owner-panel` `[Authorize(Roles="Owner")]`**
- ✅ **NEW: Owner activate Salesman/Shipper cho customer thuộc tenant mình — Gateway `/api/v1/tenant-community/*` `[Authorize(Policy="RequireOwnerRole")]`**
- ✅ **NEW: IDOR guard — Owner tenant A KHÔNG activate cho customer tenant B**
- ✅ **NEW: SystemAdmin flow unchanged — existing `/api/admin/community/*` cross-tenant**

**Demo script:**
```
1. SystemAdmin → /admin/khachlink-instances → create Reseller instance (reseller.khachvip.online, owner=reseller tenant)
2. DNS + SSL expand
3. Truy cập https://reseller.khachvip.online → all icons + tenant context = reseller
4. Customer order → order snapshot CommerceMode.Reseller (verify in DB)
5. Owner tenant login ShopERP → /community/owner-panel → thấy eligible customers of own tenant
6. Owner activate Salesman cho customer A (thuộc tenant mình) → verify CommunityRole row in DB with TenantId=owner's tenant
7. Owner attempt activate cho customer B (different tenant) → 403 Forbidden (IDOR guard)
8. SystemAdmin login → /admin/community/admin-panel → cross-tenant view still works
```

**Không có trong R2:**
- ❌ Type 2 Logistics (R3)
- ❌ Type 3 JobMarket (R3)

**Rollback:** Disable Reseller option in admin UI (or flag OFF) → reseller instance falls back to FullCommerce

---

### RELEASE R2.2 — "Reseller Accounting Fix" (Sprint 7.2 — deferred from R2 2026-09-04)

| | |
|---|---|
| **Sprints** | 7.2 (Reseller Accounting-Cashflow Alignment) |
| **Branch** | `feature/reseller-accounting-fix` (from main after R2 merge) |
| **Feature flag** | ON (R2 đã RV) |

**Background (gap documented 2026-09-04):**
- `OrderService.GenerateAccountingEntriesAsync` (line 162-254) có **NO `CommerceMode` branch** — tất cả orders tạo identical Revenue(511) + VAT(3331) + COGS(632) trên sổ `order.TenantId`.
- `WalletService.ConfirmCodResellerAsync` (line 223-336) cho thấy Vạn An (Platform) là middleman: `order.TenantId` = SUPPLIER chỉ nhận `costPrice` qua Wallet Settlement, customer trả `sellPrice`. Margin do Vạn An giữ (PlatformFee + CommunityFund + Commission + VanAn net).
- **Hệ quả:** Supplier tenant's accounting shows inflated revenue (sellPrice) họ không thực nhận + gross margin họ không giữ → violates TT 152/2025/TT-BTC cash-basis (doanh thu ghi nhận theo thực thu).

**Giá trị nhìn thấy được:**
- ✅ `GenerateAccountingEntriesAsync` có `CommerceMode.Reseller` branch riêng (separate from Marketplace)
- ✅ Supplier tenant's Revenue(511) = `costPrice` (actual cash received via Settlement), NOT `sellPrice`
- ✅ Supplier tenant's COGS(632) = production cost (unchanged — actual cost of goods sold)
- ✅ Platform tenant's books: PlatformFee + CommunityFund + VanAn net margin = income entries
- ✅ Commission + DeliveryFee entries if applicable (salesman/shipper accounting)
- ✅ Accounting = cashflow invariant: sum of all AccountingEntry amounts per order = sum of all WalletTransaction amounts per order
- ✅ TT 152/2025/TT-BTC cash-basis compliance verified

**Demo script:**
```
1. Tạo Reseller order + confirm COD
2. Verify supplier tenant's books: Revenue(511) = costPrice, COGS(632) = production cost, gross margin = 0 (correct — supplier doesn't keep margin)
3. Verify Platform tenant's books: PlatformFee + CommunityFund + VanAn net = total margin
4. Verify commission entry on salesman's tenant (if applicable)
5. Verify delivery fee entry on shipper's tenant (if applicable)
6. Sum all AccountingEntry amounts = Sum all WalletTransaction amounts (invariant)
```

**Không có trong R2.2:**
- ❌ Type 2 Logistics (R3)
- ❌ Type 3 JobMarket (R3)
- ❌ Marketplace accounting changes (only Reseller branch added)

**Rollback:** `git revert <R2.2-commit>` → Reseller orders generate Marketplace-style accounting (current buggy behavior) — no data corruption, only accounting-cashflow mismatch returns

---

### RELEASE R3 — "Type 2 Logistics + Type 3 JobMarket" (Sprints 8-9)

| | |
|---|---|
| **Sprints** | 8 (Logistics) + 9 (JobMarket + /jobs page) |
| **Branch** | `feature/khachlink-multi-profile-r3` (from main after R2.2 merge) |
| **Feature flag** | ON |

**Giá trị nhìn thấy được:**
- ✅ Profile `Logistics` preset (hide commerce, show community) — Type 2
- ✅ Profile `JobMarket` preset (hide commerce, show /jobs) — Type 3
- ✅ `/jobs.razor` page — wrapper reuse `/stores` component + filter products by keyword (job/việc/dịch vụ/service)
- ✅ SystemAdmin tạo Logistics + JobMarket instance
- ✅ Logistics instance: shipper/shop owner community tabs hiện (role AND flag — both must be true)
- ✅ JobMarket instance: `/jobs` page list products có text "job/việc/dịch vụ" trong name

**Demo script:**
```
1. SystemAdmin → create Logistics instance (ship.khachvip.online) → truy cập → Home + Stores + Community + Profile
2. SystemAdmin → create JobMarket instance (vieclam.khachvip.online) → truy cập → Home + Stores + Jobs + Profile
3. /jobs page → list products có text "job/việc/dịch vụ" trong name
4. Login as shipper on ship.khachvip.online → community tabs hiện (role AND flag)
```

**Rollback:** Disable Logistics/JobMarket options in admin UI (or flag OFF)

---

## 3. CONFLICT AVOIDANCE STRATEGY

### 3.1 Sequential branching
```
R1 branch: feature/khachlink-multi-profile-r1 (from main @ latest)
  → merge to main → main @ <R1-commit>

R2 branch: feature/khachlink-multi-profile-r2 (from main @ <R1-commit>)
  → merge to main → main @ <R2-commit>

R3 branch: feature/khachlink-multi-profile-r3 (from main @ <R2-commit>)
  → merge to main → main @ <R3-commit>
```
Không bao giờ branch song song → 0 merge conflict.

### 3.2 File ownership per release

| File | R1 | R2 | R3 |
|---|---|---|---|
| `1_Shared/Domain/Aggregates/KhachLinkAggregate/*` | ✏️ CREATE | — | — |
| `3_CoreHub/Infrastructure/Configurations/KhachLinkInstanceConfiguration.cs` | ✏️ CREATE | — | — |
| `3_CoreHub/Infrastructure/VanAnDbContext.cs` | ✏️ ADD DbSet + exclusion | — | — |
| `3_CoreHub/Migrations/*` | ✏️ CREATE migration | — | — |
| `3_CoreHub/Repositories/KhachLinkInstanceRepository.cs` | ✏️ CREATE | — | — |
| `3_CoreHub/Services/KhachLinkInstanceService.cs` | ✏️ CREATE | — | — |
| `2_Gateway/Controllers/KhachLinkInstanceController.cs` | ✏️ CREATE | — | — |
| `2_Gateway/DTOs/KhachLinkInstanceDto.cs` | ✏️ CREATE | — | — |
| `2_Gateway/Program.cs` | ✏️ DI register | — | — |
| `2_Gateway/appsettings.json` | ✏️ Feature flag | — | — |
| `5_WebApps/KhachLink/Services/Http/KhachLinkInstanceHttpService.cs` | ✏️ CREATE | — | — |
| `5_WebApps/KhachLink/Models/KhachLinkInstanceConfig.cs` | ✏️ CREATE | — | — |
| `5_WebApps/KhachLink/Components/Layout/NavMenu.razor` | ✏️ REFACTOR (15 items → flag-driven) | — | — |
| `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor` | ✏️ REFACTOR (fetch+cascade) | — | — |
| `5_WebApps/KhachLink/Program.cs` | ✏️ DI register | — | — |
| `5_WebApps/KhachLink/Pages/Jobs.razor` | — | — | ✏️ CREATE (R3 Sprint 9) |
| `5_WebApps/ShopERP/Pages/Admin/KhachLinkInstances.razor` | ✏️ CREATE | ✏️ Enable Reseller dropdown | ✏️ Enable Logistics+JobMarket dropdown |
| `5_WebApps/ShopERP/Services/ApiClients/KhachLinkInstanceApiClient.cs` | ✏️ CREATE | — | — |
| `5_WebApps/ShopERP/Program.cs` | ✏️ DI register | ✏️ REGISTER `TenantCommunityAdminApiClient` | — |
| `3_CoreHub/Services/ICommunityAdminService.cs` | — | ✏️ ADD tenant-scoped overloads | — |
| `3_CoreHub/Services/CommunityAdminService.cs` | — | ✏️ IMPLEMENT tenant-scoped + IDOR guard | — |
| `2_Gateway/Controllers/TenantCommunityAdminController.cs` | — | ✏️ CREATE (Owner endpoints) | — |
| `5_WebApps/ShopERP/Components/Pages/Community/OwnerPanel.razor` | — | ✏️ CREATE (Owner panel) | — |
| `5_WebApps/ShopERP/Services/ApiClients/TenantCommunityAdminApiClient.cs` | — | ✏️ CREATE | — |
| `5_WebApps/ShopERP/Components/Layout/NavMenu.razor` | — | ✏️ ADD Owner-only "Cộng tác viên" link | — |
| `3_CoreHub/Services/OrderService.cs` | — | — (R2.2) | ✏️ ADD `CommerceMode.Reseller` branch in `GenerateAccountingEntriesAsync` |
| `3_CoreHub/Services/IAccountingService.cs` | — | — (R2.2) | ✏️ ADD Platform income entry factory (PlatformFee, CommunityFund, VanAn net) |
| `3_CoreHub/Services/AccountingEntryService.cs` | — | — (R2.2) | ✏️ IMPLEMENT Platform income entries |
| `6_Tests/VanAn.Core.Tests/Accounting/ResellerAccountingTests.cs` | — | — (R2.2) | ✏️ NEW — accounting = cashflow invariant tests |
| `nginx/templates/vanan.multivps.conf.template` | ✏️ ADD wildcard server block | — | — |
| `scripts/init-ssl-khachlink-instances.sh` | ✏️ CREATE | — | — |
| `6_Tests/VanAn.Core.Tests/KhachLink/*` | ✏️ CREATE | ✏️ ADD Reseller tests | ✏️ ADD Logistics+JobMarket tests |
| `6_Tests/VanAn.Integration.Tests/KhachLink/*` | ✏️ CREATE | ✏️ ADD Reseller tests | ✏️ ADD Logistics+JobMarket tests |
| `KhachLinkNavFlags.ForProfile()` | ✏️ FullCommerce + Directory | ✏️ ADD Reseller | ✏️ ADD Logistics + JobMarket |

**R1→R2 conflict risk:** `KhachLinkNavFlags.ForProfile()` (R2 adds Reseller case). Giải pháp: R2 branch từ main mới nhất (đã có R1) → chỉ thêm 1 case, không sửa existing → 0 conflict.

**R2→R2.2 conflict risk:** None. R2.2 touches `OrderService.GenerateAccountingEntriesAsync` + accounting tests — no overlap with R2 files (NavFlags, CommunityAdminService, OwnerPanel, TenantCommunityAdminController).

**R2.2→R3 conflict risk:** None. R3 touches `KhachLinkNavFlags.ForProfile()` (Logistics + JobMarket cases) + `KhachLinkInstances.razor` (enable dropdown) + `Jobs.razor`. No overlap with R2.2 accounting files. R3 branch từ main sau R2.2 → 0 conflict.

### 3.3 Feature flag isolation
```json
// appsettings.json (R1)
"KhachLink": {
  "MultiProfileEnabled": false  // OFF — existing behavior unchanged
}
```
- R1 merge: flag OFF → production unchanged → **safe merge**
- R1 RV pass → toggle ON → multi-profile active
- R2 merge: flag đã ON → Reseller profile available in admin UI
- R3 merge: flag ON → Logistics + JobMarket profiles available

---

## 4. VALIDATION GATES (per release)

| Gate | R1 | R2 | R3 |
|---|---|---|---|
| `dotnet build` 0 errors | ✅ | ✅ | ✅ |
| `guard-check.ps1` ALL PASS | ✅ | ✅ | ✅ |
| CI pipeline pass | ✅ | ✅ | ✅ |
| Unit tests (new) | ✅ | ✅ | ✅ |
| Integration tests (new) | ✅ | ✅ | ✅ |
| Manual demo flow | ✅ | ✅ | ✅ |
| Deploy to VPS | ✅ | ✅ | ✅ |
| RV on VPS | ✅ | ✅ | ✅ |
| Feature flag toggle test | ✅ ON/OFF | — | — |

---

## 5. ROLLBACK PLAN (per release)

| Release | Rollback method | Time |
|---|---|---|
| R1 | Feature flag OFF → all instances render FullCommerce default | < 1 min |
| R2 | Disable Reseller option in admin UI (or flag OFF) | < 1 min |
| R2.2 | `git revert <R2.2-commit>` — accounting-cashflow mismatch returns but no data corruption | < 10 min |
| R3 | Disable Logistics/JobMarket options (or flag OFF) | < 1 min |
| Any | `git revert <release-commit>` on main | < 10 min |

---

## 6. ESTIMATED TIMELINE

| Release | Sprints | Cumulative | Giá trị |
|---|---|---|---|
| R1 — Multi-Profile Core + Type 1 + 4 + Multi-domain | 6 | 6 | SystemAdmin tạo Directory/FullCommerce instance, multi-domain routing |
| R2 — Type 5 Reseller + Owner Role Assignment | 1 (expanded) | 7 | Tenant trung gian bán lại, order Reseller mode, Owner tự quản lý cộng tác viên |
| R2.2 — Reseller Accounting Fix | 1 | 8 | Accounting = cashflow invariant, TT 152 cash-basis compliance |
| R3 — Type 2 + 3 | 2 | 10 | Sàn shipper + sàn việc (jobs = Product filter) |

**Total: ~10 sprints across 4 releases**

---

## 7. APPROVAL GATES

```
Sprint 1 build pass → user approve → Sprint 2
Sprint 2 build pass → user approve → Sprint 3
Sprint 3 build pass → user approve → Sprint 4
Sprint 4 build pass → user approve → Sprint 5
Sprint 5 → user approve → Sprint 6
R1 complete (Sprint 6 CI pass) → user approve → merge → deploy → RV
  ↓
R2 (Sprint 7) → build pass → user approve → merge → deploy → RV
  ↓
R2.2 (Sprint 7.2) → build pass → user approve → merge → deploy → RV
  ↓
R3 (Sprint 8-9) → build pass → user approve → merge → deploy → RV
  ↓
DONE
```
