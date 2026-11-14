# Task W2-1 — COOPERATIVE GROUPING MODEL + PORTAL ENDPOINTS (FR-G1/G2)

> **Status:** PENDING · **Wave:** W2 · **Gate 5:** CÓ — model mới, cần approve design trước khi sửa
> **Phụ thuộc:** W1-1 (instance tồn tại). Unblocks: W2-2 member listing, FR-V2 member toggles.

## Mục tiêu giá trị

Portal có data thật: `GET /api/cooperatives` + `/api/cooperatives/{id}/members` trả HTX + member shops — nền tảng cho landing portal và mọi module HTX sau này (mua chung, sàn việc).

## Design cần chốt trước (đề xuất — user duyệt)

**Option A (đề xuất):** entity `Cooperative` mới + join `CooperativeMember` (TenantId) — PG source of truth (CoreHub/Gateway), multi-tenant-safe (Cooperative không thuộc tenant nào — platform-level, giống KhachLinkInstance).
**Option B:** field `CooperativeId` trên `Tenant` — đơn giản nhưng cứng (1 tenant chỉ thuộc 1 HTX, khó mở rộng member-of-nhiều-HTX).
**Option C:** member list JSON trên `KhachLinkInstance` — nhanh nhưng không query được, không scale cho modules sau.

Seed: HTX đầu tiên (tên thật — hỏi user), member ban đầu = tenant của commienphi nếu cùng tổ chức.

## Scope

1. `Cooperative` entity + `CooperativeMember` (CooperativeId, TenantId, joined metadata: JoinedAt, Role?) — **model tối thiểu, không permission phức tạp** (giới hạn scope, risk R2).
2. EF config + migration PG (CoreHub) — single-identity pattern theo governance.
3. Public endpoints (Gateway, anonymous, cacheable — pattern `dir_api_cache`):
   - `GET /api/cooperatives` → list HTX active (id, name, logo, member count).
   - `GET /api/cooperatives/{id}/members` → member shops (tenantId, name, logo, slug, khachLinkDomain — reuse shape `TenantStoreDto` nếu phù hợp).
   - `GET /api/cooperatives/by-domain/{domain}` → resolve portal instance → cooperative scope (nếu 1 domain = 1 portal nhiều HTX: trả list; nếu per-HTX subdomain: trả 1).
4. Admin CRUD tối thiểu: ShopERP `/admin/cooperatives` (list + add member + remove member) — SystemAdmin only.
5. Seed HTX đầu tiên.

## Files

- `3_CoreHub/Domain` hoặc shared aggregate (theo pattern KhachLinkInstance — verify location)
- `3_CoreHub/Infrastructure` EF config + migration
- `2_Gateway/Controllers/CooperativesController.cs` (public, rate-limited, cache headers)
- `5_WebApps/ShopERP/Components/Pages/Admin/CooperativesAdmin.razor`
- nginx template: thêm `/api/cooperatives*` vào `dir_api_cache` block cho `*.timlathay.com` + `timlathay.com`

## Tests

- CRUD service tests + endpoint auth/anonymous + member filter.
- Arch test: controller nằm exemption public-anonymous pattern (như `PublicMeteringController`).
- guard-check + build + Core.Tests.

## RV checklist

- [ ] `GET /api/cooperatives` → 200 JSON list HTX seeded.
- [ ] `GET /api/cooperatives/{id}/members` → member tenants đúng.
- [ ] nginx cache header `X-Cache-Status` hoạt động trên `*.timlathay.com`.
- [ ] Anonymous access OK; write endpoints 401/403.
