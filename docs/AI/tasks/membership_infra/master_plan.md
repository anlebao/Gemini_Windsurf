# Master Plan — Vạn An Membership Infrastructure (Hybrid)

**Created:** 2026-09-29
**Status:** ✅ **DEPLOYED PROD + RV PASS** (2026-09-29, CD Multi-VPS `36550023981` SUCCESS cho `103e2a2`) — push `a17f5d37` → `103e2a2` (4 commits) · RV Layer 1-3 PASS (API 404 JSON đúng · markers Gateway DLL + KhachLink WASM · Playwright 5/5: login Owner, menu, /admin/membership render, data area, 0 console errors) · 2 prod incidents tìm ra + fix trong RV: (1) CHECK constraint PG 42703 crash ShopERP deploy (`31d9ab5` — quote identifier) · (2) RenderFragment private `<X />` → element rỗng (`103e2a2` — at-notation)
**Branch target:** `main`
**Source:** SRS `docs/requirements/SRS — Vạn An Membership Infrastruct.md` + review session 2026-09-29 ("so sánh verify tenant hiện có → chọn hướng hybrid")

## Kiến trúc (LOCKED — user duyệt 2026-09-29)

```
HTX = Tenant hiện có (đánh dấu qua HtxProfile entity — KHÔNG sửa TenantSettings)
        │
   MembershipApplication (PG)     ← ApplicantCustomerId (FK Customers) + BusinessTenantId? (FK Tenants)
        │ NetworkId = Customer.Id (Vạn An Network ID)
        ▼
        Member (PG — registry, source of truth, KHÔNG sync SQLite)
        │   MemberCustomerId? (FK Customers) XOR MemberTenantId? (FK Tenants) + CHECK
        │
        └── Member ID QR (token, không PII) → GET /api/members/{id}/status
```

| # | Quyết định | Chốt |
|---|---|---|
| A1 | HTX = Tenant hiện có | ✅ — multi-tenancy + outbox + settings reuse; đánh dấu HTX bằng entity `HtxProfile` (KHÔNG đụng `TenantSettings` — tránh phá 12 With methods) |
| A2 | Vạn An Network ID = Customer identity | ✅ — Participant ≈ Customer KhachLink; tái dùng `CustomerMergeService` cho dedup |
| A3 | Member registry PG-only tại Gateway | ✅ — không NATS sync sang SQLite (member không cần offline POS) |

## Data Integrity Contract (bắt buộc — checklist mọi phase)

1. **Polymorphic member reference = 2 FK thật + CHECK** — `Member.MemberCustomerId Guid?` FK `Customers.Id` (Restrict) · `Member.MemberTenantId Guid?` FK `Tenants.Id` (Restrict) · CHECK `(MemberCustomerId IS NULL) <> (MemberTenantId IS NULL)`. **CẤM copy pattern `Tenant.OwnerCustomerId` (Guid ref không FK).**
2. **Single-Identity Pattern**: mọi FK là `Guid`/`Guid?` trỏ PK; KHÔNG business key VO; KHÔNG `.Value` trong LINQ.
3. **Cấm xóa member/application vật lý** — lifecycle bằng status transition (`Suspended/Resigned/Terminated`); SRS §7.
4. **Self-membership guard** — `MemberTenantId != TenantId` (HTX không là member của chính nó) — guard trong domain factory.
5. **Duplicate identity guard** — 1 (htx, applicant) chỉ 1 application active (Draft/Submitted/NeedInfo); unique index `(TenantId, MemberCustomerId)` + `(TenantId, MemberTenantId)`.
6. **`CustomerMergeService` phải migrate Membership rows** — thêm `MembershipApplications` + `Members` vào danh sách migrate trước soft-delete stub (pattern Task 6 — CommunityRoles/WalletTransactions).
7. **Cross-tenant read có chủ đích** — "HTX của tôi" (member là tenant khác) phải `IgnoreQueryFilters` + authz theo actor, kèm test isolation.

## Phases

| Phase | Nội dung | Files chính |
|---|---|---|
| 1 | Domain: `MembershipApplication`, `Member`, `ConsentRecord`, `HtxProfile`, `MembershipEvents` | `1_Shared/Domain/Aggregates/MembershipAggregate/` |
| 2 | EF configs + migration (PG-only) | `3_CoreHub/Infrastructure/Configurations/`, `VanAnDbContext` |
| 3 | Services: application lifecycle, member registry, consent | `3_CoreHub/Services/Membership/` |
| 4 | API: 2 controllers + policy `HtxMembershipOfficer` (≠ SysAdmin) | `2_Gateway/Controllers/` |
| 5 | UI: KhachLink đăng ký ≤2 phút + ShopERP HTX review dashboard | `5_WebApps/KhachLink/`, `5_WebApps/ShopERP/` |
| 6 | Tests (domain lifecycle + integrity contract) + build + guard-check | `6_Tests/VanAn.Core.Tests/Services/Membership/` |

## Reuse map

| Nhu cầu | Reuse | Ref |
|---|---|---|
| State machine + guard | `TenantClaimRequest` (Submitted→Approved/Rejected) | `1_Shared/Domain/Aggregates/TenantAggregate/TenantClaimRequest.cs` |
| Aggregate + events + multi-tenant | `AggregateRoot`, `IDomainEvent`, `BaseEntity.TenantId` | `1_Shared/Domain/Common.cs` |
| EF config | `TenantClaimRequestConfiguration` (FK Restrict, index, converter) | `3_CoreHub/Infrastructure/Configurations/` |
| SMS OTP (Level 1) | `CollaboratorVerificationService` | `3_CoreHub/Services/` |
| QR member card + scan | `QrCodeService` + Guard `Scan.razor` | `3_CoreHub/Services/QrCodeService.cs` |
| Controller/DTO/error pattern | `TenantPendingController` (404/409 mapping) | `2_Gateway/Controllers/` |
| Merge/soft-delete | `CustomerMergeService` (+migrate membership rows — mục 6) | `3_CoreHub/Services/CustomerMergeService.cs` |

## Non-goals (không trôi scope)

- KHÔNG lưu ảnh CCCD plaintext, KHÔNG log CCCD/OTP (SRS SR-06) — chỉ lưu `IdentityVerificationLevel` + evidence ref
- KHÔNG KYC provider (SRS §40 Phase 3)
- KHÔNG ví/vốn góp (SRS §23 — Capital/Fee chỉ là status enum Phase 1)
- KHÔNG đụng `Tenant.Verify()`, `AccountingEntry`, Domain hiện có
- KHÔNG NATS sync registry (A3)

## Task cards
- `task_phase1_domain.md` — Domain aggregates + events (in progress)
- Phase 2-6: viết sau khi phase trước duyệt
