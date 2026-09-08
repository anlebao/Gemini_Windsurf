# KhachLink Profile Transition UX — Master Plan

**Created:** 2026-09-08
**Status:** ANALYZE COMPLETE — awaiting IMPLEMENT session
**Branch target:** `main` (new branch `feature/khachlink-profile-transition-ux` from `main` @ `bfb97afd`)
**Source:** User request — review trải nghiệm khách hàng khi chuyển đổi profile KhachLink (Directory → Reseller → FullCommerce) trên app điện thoại.

## Problem

### Hiện trạng (verify 2026-09-08)

KhachLink là PWA per-domain. Mỗi domain = 1 `KhachLinkInstance` = 1 `KhachLinkProfile` (Directory/Reseller/FullCommerce/Logistics/JobMarket). **Khách hàng KHÔNG tự chuyển profile** — SystemAdmin cấu hình ở backend qua `/admin/khachlink-instances` (ShopERP). Khi admin đổi profile, NavFlags cập nhật trong PWA khách hàng sau tối đa 1 phút (cache TTL `KhachLinkInstanceHttpService`).

Hệ thống hiện chỉ ẩn/hiện nav items (`@if (_navFlags.ShowXxx)`) mà **không có bất kỳ layer giao tiếp nào** với khách hàng về sự thay đổi. 10 điểm nghẽn UX đã phát hiện trong review:

| # | Điểm nghẽn | Mức độ |
|---|---|---|
| 1 | Không có transition messaging — nav items xuất hiện/biến mất thầm lặng | 🔴 |
| 2 | Directory → Reseller là bước nhảy khổng lồ (3 → 15 nav items), không onboarding | 🔴 |
| 3 | Bookmark/link đã lưu bị "vô hiệu hóa thầm lặng" — không có route guard theo profile | 🔴 |
| 4 | Cart data "biến mất" về mặt nhận thức khi FullCommerce → Directory | 🔴 |
| 5 | Reseller badge chỉ xuất hiện ở Home, không ở deep link pages | 🟡 |
| 6 | Reseller → FullCommerce: chuyển đổi quá tinh vi (chỉ badge biến mất) | 🟡 |
| 7 | Không có profile indicator persistent | 🟡 |
| 8 | Mid-session jarring (cache 1 phút — nav đổi giữa chừng session) | 🟡 |
| 9 | Service Worker không trigger update khi profile đổi | 🟢 |
| 10 | PWA shortcuts không sync với profile | 🟢 |

### Ai quyết định chuyển đổi, khi nào, qua UI nào

| Khía cạnh | Chi tiết |
|---|---|
| **Người quyết định** | **SystemAdmin** (Vạn An platform owner) — duy nhất. Policy `[Authorize(Policy = "SystemAdmin")]` trên cả Gateway API (`KhachLinkInstanceController`) và ShopERP admin page (`KhachLinkInstances.razor`). KHÔNG phải khách hàng, KHÔNG phải tenant owner. |
| **UI thực hiện** | `/admin/khachlink-instances` trong ShopERP (Blazor Server, port 5003). Click "Sửa" → modal → đổi dropdown Profile → auto-apply preset NavFlags → click "Lưu" → `PUT /api/v1/khachlink-instances/{id}`. |
| **Khi nào** | Khởi tạo (onboarding tenant mới), nâng cấp (Directory → Reseller/FullCommerce khi tenant đăng ký gói cao), hạ cấp (FullCommerce → Directory khi ngừng đóng phí), Reseller → FullCommerce (thôi làm trung gian). Hiếm nhưng tác động lớn — ảnh hưởng toàn bộ khách hàng trên domain đó. |
| **Hiện trạng guardrail** | KHÔNG confirmation dialog, KHÔNG preview impact, KHÔNG audit log riêng, KHÔNG notification đến tenant owner, KHÔNG notification đến khách hàng, KHÔNG rollback. Cache 1 phút = khách thấy thay đổi sau tối đa 1 phút. |

### Out of scope (DEFER — chống over-build)

- **Logistics + JobMarket profiles** — R3, chưa implement `ForProfile` preset (TODO trong `KhachLinkNavFlags.cs`). Plan này chỉ cover Directory/Reseller/FullCommerce.
- **Auto-rollback** khi đổi sai profile — SystemAdmin tự đổi lại thủ công (audit log + confirmation dialog đủ giảm rủi ro MVP).
- **Notification đến tenant owner** khi profile đổi — defer đến sprint riêng (cần email/notification infra).
- **Profile change scheduling** (đổi theo lịch, không tức thời) — over-build MVP.
- **A/B test profile transition messaging** — defer đến khi có telemetry (Gate G1).
- **PWA shortcuts dynamic** theo profile — over-build MVP, shortcuts cố định đủ.

## Solution

### Kiến trúc tổng thể

```
SystemAdmin đổi profile (ShopERP /admin/khachlink-instances)
     ↓
[P1.1] Confirmation dialog + impact preview (chặn lỗi ở nguồn)
     ↓
[P1.2] Audit log qua AuditLog pattern hiện có (AuditTrailService.LogUpdateAsync)
     ↓
PUT /api/v1/khachlink-instances/{id} → DB update
     ↓
Khách hàng mở app (sau tối đa 1 phút cache TTL)
     ↓
[P2.1] "What's New" banner — detect profile change qua UpdatedAt timestamp
     ↓
[P2.2] Profile indicator persistent (footer label)
     ↓
[P2.3] Route guard — redirect trang không khả dụng về Home + toast
     ↓
[P3.1] Onboarding tour (driver.js) cho Directory → Reseller/FullCommerce
[P3.2] Cart preservation notice cho FullCommerce → Directory
     ↓
[P4.1] Reseller badge global (header, không chỉ Home)
     ↓
[P5.1] SW version bump qua UpdatedAt cache buster
```

### Tech approach

- **P1.1 Confirm dialog:** UI-only trong `KhachLinkInstances.razor` — thêm `_showConfirmModal` + diff computation (so sánh `_editForm.NavFlags` vs `item.NavFlags` cũ) trước khi gọi `UpdateAsync`.
- **P1.2 Audit log:** Reuse `IAuditTrailService.LogUpdateAsync` (pattern hiện có, precedent `AccountingEntryService`). Thêm `AuditableEntityType.KhachLinkInstance = 12` vào enum. Hook vào `KhachLinkInstanceService.UpdateAsync` — inject `IAuditTrailService`, serialize old/new values JSON, log trước khi save. Hiển thị history trong admin page (tab "Lịch sử" hoặc query `GetEntityHistoryAsync`).
- **P2.1 What's New banner:** KhachLink client lưu `lastSeenProfileAt` trong localStorage. So sánh `instance.UpdatedAt > lastSeenProfileAt` → hiển thị banner 1 lần với message theo hướng chuyển đổi. Dùng field `UpdatedAt` có sẵn trong DTO (không cần API change).
- **P2.2 Profile indicator:** Thêm label nhỏ ở footer `KhachLinkLayout.razor` — chỉ hiển thị khi khác FullCommerce (default ẩn).
- **P2.3 Route guard:** Thêm `ProfileGuard.razor` component wrap các page commerce-only. Check `_navFlags.ShowXxx` (cascaded) → nếu false, redirect về `/` + toast.
- **P3.1 Onboarding tour:** Dùng `driver.js` (approved, ~20KB, popular, MIT). 3-step tooltip tour khi detect Directory → FullCommerce/Reseller.
- **P3.2 Cart preservation:** Khi detect FullCommerce → Directory + `CartService.Items.Count > 0`, hiển thị modal.
- **P4.1 Reseller badge global:** Di chuyển badge logic từ `Home.razor` vào `KhachLinkLayout.razor` — fetch `GetCommerceModeAsync` trong layout, hiển thị badge dưới header ở mọi page.
- **P5.1 SW version bump:** Dùng `UpdatedAt` từ API response làm cache buster — client so sánh timestamp, force reload nav nếu đổi (không cần write file từ Gateway).

### Quy tắc audit log (Ground Rule — bắt buộc)

- **Reuse `AuditLog` entity + `AuditTrailService`** — KHÔNG tạo entity audit mới. Pattern hiện có đủ: `ForUpdate(tenantId, entityType, entityId, oldValues, newValues, userId, ...)`.
- **Append-only** — AuditLog immutable, không update/delete (governance hard stop).
- **Platform-level** — `KhachLinkInstance.TenantId = Guid.Empty` (sentinel). AuditLog cho instance dùng `TenantId = Guid.Empty` + `AuditableEntityType.KhachLinkInstance`.
- **User context** — `AuditTrailService` tự capture userId/userName từ HttpContext claims (SystemAdmin JWT).

## Component reuse map

| Hạ tầng có sẵn [V] | Mảnh UX dùng | Reuse strategy |
|---|---|---|
| `AuditLog` entity + `AuditTrailService` + `IAuditLogRepository` | P1.2 Audit log | Inject `IAuditTrailService` vào `KhachLinkInstanceService`, gọi `LogUpdateAsync` |
| `AuditableEntityType` enum | P1.2 | Thêm `KhachLinkInstance = 12` vào enum |
| `KhachLinkInstance.UpdatedAt` (BaseEntity field) | P2.1 + P5.1 | Dùng làm timestamp detect change + cache buster |
| `KhachLinkInstanceHttpService` (cache 1 min) | P2.1 | Client so sánh `UpdatedAt` với localStorage `lastSeenProfileAt` |
| `KhachLinkNavFlagsDto` (cascaded qua `KhachLinkLayout`) | P2.3 | `ProfileGuard.razor` đọc cascaded `_navFlags.ShowXxx` |
| `CartService` (client-side state) | P3.2 | Check `Items.Count > 0` khi detect downgrade |
| `CommunityHttpService.GetCommerceModeAsync` | P4.1 | Di chuyển fetch từ Home vào Layout |
| `VanAn.UI.Platform` components (VanAnAlert, VanAnButton, VanAModal) | ALL UI | 100% trang mới dùng UI Platform |
| `driver.js` (external lib, ~20KB, MIT) | P3.1 | NPM install hoặc CDN vendored |
| `KhachLinkInstances.razor` admin page | P1.1 | Thêm confirm modal thứ 2 trong `HandleSubmit` |

## Gateway API endpoints

| Endpoint | Method | Purpose | Phase | Status |
|---|---|---|---|---|
| `/api/v1/khachlink-instances/{id}` | PUT | Update profile + nav flags (existing) | P1.1 | ✅ LIVE |
| `/api/v1/khachlink-instances/by-domain/{domain}` | GET | Public lookup (existing, returns UpdatedAt) | P2.1 | ✅ LIVE (verify UpdatedAt in DTO) |
| `/api/v1/audit-logs/entity/{entityType}/{entityId}` | GET | Audit history for instance | P1.2 | ⏳ (verify existing endpoint) |

## Files mới cần tạo

```
5_WebApps/KhachLink/Components/Shared/ProfileGuard.razor          (P2.3 — route guard wrapper)
5_WebApps/KhachLink/Components/Shared/WhatsNewBanner.razor         (P2.1 — transition banner)
5_WebApps/KhachLink/Components/Shared/OnboardingTour.razor         (P3.1 — driver.js wrapper)
5_WebApps/KhachLink/Components/Shared/CartPreservationModal.razor (P3.2 — cart notice)
5_WebApps/KhachLink/wwwroot/js/onboarding-tour.js                  (P3.1 — driver.js init)
5_WebApps/ShopERP/Components/Pages/Admin/KhachLinkInstanceAudit.razor (P1.2 — audit history tab)
6_Testing/e2e-tests/profile-transition.spec.ts                    (E2E — cả 2 domain)
```

## Files sửa

```
1_Shared/Domain/Audit/AuditLog.cs                        (P1.2 — thêm AuditableEntityType.KhachLinkInstance = 12)
3_CoreHub/Services/KhachLinkInstanceService.cs           (P1.2 — inject IAuditTrailService, log UpdateAsync)
2_Gateway/Controllers/KhachLinkInstanceController.cs      (P1.2 — pass old values to service for audit)
5_WebApps/ShopERP/Components/Pages/Admin/KhachLinkInstances.razor (P1.1 — confirm dialog + impact preview)
5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor (P2.1 + P2.2 + P4.1 — banner + indicator + badge)
5_WebApps/KhachLink/Components/Layout/NavMenu.razor       (P2.2 — profile indicator in nav header)
5_WebApps/KhachLink/Pages/Home.razor                      (P4.1 — remove Reseller badge, moved to layout)
5_WebApps/KhachLink/Pages/Cart.razor                      (P2.3 — wrap ProfileGuard)
5_WebApps/KhachLink/Pages/MyOrders.razor                  (P2.3 — wrap ProfileGuard)
5_WebApps/KhachLink/Pages/Rewards.razor                   (P2.3 — wrap ProfileGuard)
5_WebApps/KhachLink/Pages/Missions.razor                  (P2.3 — wrap ProfileGuard)
5_WebApps/KhachLink/Pages/MyLoyalty.razor                 (P2.3 — wrap ProfileGuard)
5_WebApps/KhachLink/Pages/Scan.razor                      (P2.3 — wrap ProfileGuard)
5_WebApps/KhachLink/Pages/QrWallet.razor                   (P2.3 — wrap ProfileGuard)
5_WebApps/KhachLink/Pages/Campaigns.razor                 (P2.3 — wrap ProfileGuard)
5_WebApps/KhachLink/Services/Http/KhachLinkInstanceHttpService.cs (P2.1 — expose UpdatedAt in config)
5_WebApps/KhachLink/Models/KhachLinkInstanceConfig.cs     (P2.1 — add UpdatedAt field)
5_WebApps/KhachLink/Pages/_Host.cshtml hoặc index.html    (P3.1 — driver.js script tag)
```

## Phases (3 sprints)

### Sprint 1 — Guardrail + Foundation (UI-only, không migration)
- **P1.1** Confirmation dialog + impact preview khi đổi profile
- **P2.2** Profile indicator persistent (footer label)
- **P2.3** Route guard theo profile (`ProfileGuard.razor`)
- **P4.1** Reseller badge global (di chuyển từ Home vào Layout)
- Build + RV trên cả 2 domain: `timlathay.com` (Directory) + `diemthuong2.khachvip.online` (FullCommerce/Reseller)
- **Task card:** `task_card_sprint1_guardrail_foundation.md`

### Sprint 2 — Transition Messaging (UI + JS, không migration)
- **P2.1** "What's New" banner (detect profile change qua UpdatedAt)
- **P3.2** Cart preservation notice (FullCommerce → Directory)
- **P3.1** Onboarding tour (driver.js, Directory → Reseller/FullCommerce)
- Build + RV trên cả 2 domain
- **Task card:** `task_card_sprint2_transition_messaging.md`

### Sprint 3 — Audit + SW (cần enum update + verify audit endpoint)
- **P1.2** Audit log qua `AuditLog` pattern hiện có (thêm `AuditableEntityType.KhachLinkInstance = 12`)
- **P5.1** SW version bump qua UpdatedAt cache buster
- Admin audit history view
- Build + RV trên cả 2 domain
- **Task card:** `task_card_sprint3_audit_sw.md`

## Hard stops (governance)

- **Domain PURE** — P1.2 chỉ thêm enum value vào `AuditableEntityType` (additive, không phá existing). KHÔNG đụng `AccountingEntry`/`BaseEntity`. KHÔNG tạo entity mới (reuse `AuditLog`).
- **AuditLog immutable** — append-only, không update/delete (governance hard stop). Reuse `AuditTrailService.LogUpdateAsync`.
- **UI Platform components bắt buộc** — 100% trang/component mới dùng VanAn.UI.Platform, no custom CSS.
- **KhachLink HTTP-only** — KHÔNG inject DbContext vào KhachLink. Mọi data qua Gateway. `ProfileGuard` đọc cascaded NavFlags (client-side only, không HTTP call).
- **No new .csproj** — dùng existing projects.
- **Multi-tenancy** — `KhachLinkInstance.TenantId = Guid.Empty` (platform sentinel). AuditLog cho instance dùng `TenantId = Guid.Empty`.
- **Playwright isolation** — E2E chỉ chạy SAU khi build pass + implementation complete. Test trên cả `timlathay.com` + `diemthuong2.khachvip.online`.

## Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | P1.2 enum addition phá backward compatibility | Additive only — thêm `KhachLinkInstance = 12` vào cuối enum, không renumber existing. Migration không cần (enum stored as int, new value = 12). |
| R2 | P2.3 Route guard break PWA shortcut / deep link | Test kỹ: PWA shortcut `search.png` (Directory OK), `/cart` deep link (FullCommerce OK, Directory redirect). E2E cover cả 2 domain. |
| R3 | P2.1 localStorage `lastSeenProfileAt` stale | TTL = session (clear on logout). Compare với `instance.UpdatedAt` mỗi load. Nếu stale → banner hiển thị 1 lần rồi update timestamp. |
| R4 | P3.1 driver.js conflict với Blazor WASM render | driver.js chỉ manipulate DOM, không conflict Blazor virtual DOM. Init sau `OnAfterRenderAsync(firstRender)`. |
| R5 | P1.1 confirm dialog annoy SystemAdmin nếu đổi thường xuyên | Profile change hiếm (không hàng ngày). Confirm chỉ hiện khi profile thực sự đổi (diff NavFlags), không hiện khi chỉ đổi style/color. |
| R6 | P4.1 `GetCommerceModeAsync` trong Layout gây thêm HTTP call mỗi page | Cache trong layout state (1 call/session). Reuse `CommunityHttpService` cache nếu có. |
| R7 | P5.1 UpdatedAt không có trong by-domain DTO | Verify DTO có `UpdatedAt` — nếu thiếu, thêm vào `KhachLinkInstanceDto` + `ByDomainResponse` (additive, không break). |
| R8 | E2E trên 2 domain cần ecosystem lên | `timlathay.com` (Directory SSR) + `diemthuong2.khachvip.online` (KhachLink WASM). Cả 2 đã live + RV PASS. |

## Decisions (RESOLVED — user approved 2026-09-08)

1. **Audit log: reuse `AuditLog` pattern hiện có** — KHÔNG tạo entity audit mới. Inject `IAuditTrailService` vào `KhachLinkInstanceService`, thêm `AuditableEntityType.KhachLinkInstance = 12`.
2. **Onboarding tour: dùng `driver.js`** — external lib ~20KB, MIT, popular. Init sau `OnAfterRenderAsync`.
3. **Phạm vi RV: cả `timlathay.com` + `diemthuong2.khachvip.online`** — test Directory + FullCommerce/Reseller trên production.
4. **Thứ tự: Sprint 1 (Guardrail + Foundation) → Sprint 2 (Transition Messaging) → Sprint 3 (Audit + SW)** — dependency-aware: P2.1 cần P2.2, P3.1 cần P2.1.

## Success metrics (đo sau 4 tuần — target [A], chỉnh theo dữ liệu thật)

| Metric | Target [A] |
|---|---|
| SystemAdmin đổi profile sai (rollback cần) | ≤ 0 (confirm dialog + preview chặn) |
| Audit log ghi mọi profile change | 100% |
| Khách hàng mở app sau profile change thấy banner | 100% (first session after change) |
| Khách hàng bookmark trang không khả dụng → redirect Home | 100% (route guard) |
| E2E profile transition PASS trên cả 2 domain | 100% |

## Related

- Review source: Session 2026-09-08 (REVIEW_ONLY → ANALYZE)
- AuditLog entity: `1_Shared/Domain/Audit/AuditLog.cs`
- AuditTrailService: `3_CoreHub/Services/AuditTrailService.cs`
- KhachLinkInstance entity: `1_Shared/Domain/Aggregates/KhachLinkAggregate/KhachLinkInstance.cs`
- KhachLinkInstanceService: `3_CoreHub/Services/KhachLinkInstanceService.cs`
- KhachLinkInstanceController: `2_Gateway/Controllers/KhachLinkInstanceController.cs`
- Admin UI: `5_WebApps/ShopERP/Components/Pages/Admin/KhachLinkInstances.razor`
- KhachLink Layout: `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor`
- KhachLink NavMenu: `5_WebApps/KhachLink/Components/Layout/NavMenu.razor`
- KhachLink InstanceHttpService: `5_WebApps/KhachLink/Services/Http/KhachLinkInstanceHttpService.cs`
- Task cards: `docs/AI/tasks/khachlink_profile_transition_ux/task_card_sprint1_guardrail_foundation.md` · `task_card_sprint2_transition_messaging.md` · `task_card_sprint3_audit_sw.md`
- Precedent (GTM Drill Machine): `docs/AI/tasks/gtm_drill_mvp/`
