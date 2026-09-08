# Task Card: KhachLink Profile Transition UX

> **Status:** ⏳ NEXT (awaiting IMPLEMENT session start)
> **Master plan:** `docs/AI/tasks/khachlink_profile_transition_ux/master_plan.md`
> **Prerequisite:** ✅ Review complete (REVIEW_ONLY → ANALYZE 2026-09-08) · ✅ User approved 4 decisions

## Objective

Khách hàng tải KhachLink PWA trên điện thoại **không cảm thấy khó hiểu** khi SystemAdmin chuyển đổi profile (Directory → Reseller → FullCommerce). 10 điểm nghẽn UX đã phát hiện — 8 khuyến nghị chia 3 sprint.

## Scope summary

| Sprint | Mảnh | Mode | Cần migration? | Chạm Domain? |
|---|---|---|---|---|
| **S1** | P1.1 Confirm dialog + P2.2 Profile indicator + P2.3 Route guard + P4.1 Reseller badge global | IMPLEMENT | Không | Không |
| **S2** | P2.1 What's New banner + P3.2 Cart preservation + P3.1 Onboarding tour (driver.js) | IMPLEMENT | Không | Không |
| **S3** | P1.2 Audit log (reuse AuditLog pattern) + P5.1 SW version bump | IMPLEMENT | Không (enum additive) | Không (enum value only) |

## Decisions (approved 2026-09-08)

1. **Audit log = reuse `AuditLog` pattern hiện có** — KHÔNG tạo entity mới. Thêm `AuditableEntityType.KhachLinkInstance = 12`.
2. **Onboarding tour = `driver.js`** — external lib ~20KB, MIT.
3. **RV scope = `timlathay.com` (Directory) + `diemthuong2.khachvip.online` (FullCommerce/Reseller).**
4. **Thứ tự = S1 → S2 → S3** (dependency-aware).

## Prerequisites

- ✅ Review complete (REVIEW_ONLY mode, 2026-09-08)
- ✅ ANALYZE complete — master plan + 3 task cards
- ✅ User approved 4 decisions
- ✅ `AuditLog` + `AuditTrailService` pattern verified (live, precedent `AccountingEntryService`)
- ✅ `KhachLinkInstanceService.UpdateAsync` verified (live, inject point for audit)
- ✅ `KhachLinkInstances.razor` admin page verified (live, inject point for confirm dialog)
- ✅ `KhachLinkLayout.razor` verified (live, inject point for banner + indicator + badge)
- ✅ `KhachLinkInstanceHttpService` cache 1 min verified (live, P2.1 detect change)
- ✅ Both RV domains live: `timlathay.com` (Directory SSR) + `diemthuong2.khachvip.online` (KhachLink WASM)

## Verification (per sprint)

### Sprint 1
1. Build: `dotnet build VanAn.sln` → 0 errors
2. Admin UI: đổi profile Directory → FullCommerce → confirm dialog hiện diff nav items
3. KhachLink `timlathay.com`: footer hiện "Chế độ: Danh bạ cửa hàng"
4. KhachLink `diemthuong2.khachvip.online`: footer ẩn (FullCommerce = default)
5. Route guard: `/cart` trên Directory → redirect `/` + toast
6. Reseller badge: hiện ở mọi page (không chỉ Home) khi Reseller mode
7. E2E `profile-transition.spec.ts` PASS trên cả 2 domain

### Sprint 2
1. Build: 0 errors
2. What's New banner: đổi profile → mở app → banner hiện 1 lần
3. Cart preservation: FullCommerce có cart items → đổi Directory → modal hiện
4. Onboarding tour: Directory → FullCommerce → 3-step tooltip tour
5. E2E PASS trên cả 2 domain

### Sprint 3
1. Build: 0 errors
2. Audit log: đổi profile → `AuditLog` row inserted (entityType=KhachLinkInstance)
3. Admin audit history: view history cho 1 instance
4. SW version bump: đổi profile → client detect UpdatedAt change → force reload nav
5. E2E PASS trên cả 2 domain

## Governance checklist

- [ ] Domain PURE — P1.2 chỉ thêm enum value (additive), reuse `AuditLog`, KHÔNG tạo entity mới
- [ ] AuditLog immutable — append-only, reuse `AuditTrailService.LogUpdateAsync`
- [ ] UI Platform — 100% component mới dùng VanAn.UI.Platform
- [ ] KhachLink HTTP-only — `ProfileGuard` client-side only (đọc cascaded NavFlags, không HTTP call)
- [ ] No new .csproj
- [ ] Multi-tenancy — `KhachLinkInstance.TenantId = Guid.Empty` (platform sentinel)
- [ ] Playwright isolation — E2E chỉ chạy sau build pass + implementation complete

## Related

- Master plan: `docs/AI/tasks/khachlink_profile_transition_ux/master_plan.md`
- Sprint 1: `docs/AI/tasks/khachlink_profile_transition_ux/task_card_sprint1_guardrail_foundation.md`
- Sprint 2: `docs/AI/tasks/khachlink_profile_transition_ux/task_card_sprint2_transition_messaging.md`
- Sprint 3: `docs/AI/tasks/khachlink_profile_transition_ux/task_card_sprint3_audit_sw.md`
- Precedent (GTM Drill Machine): `docs/AI/tasks/gtm_drill_mvp/`
