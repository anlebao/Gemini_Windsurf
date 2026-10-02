# TASK CARD: Phase 3 — API (SystemAdmin duyệt TẤT CẢ + DTO góp vốn)

> **Master plan:** `docs/AI/plans/membership-registration-v2-master-plan.md`
> **Status:** 🔨 IN-FLIGHT (code chưa commit — chờ duyệt plan)

## 1. CHANGES

| File | Change |
|---|---|
| `2_Gateway/Program.cs` | + policy `MembershipReviewer` = AuthenticatedUser + (SystemAdmin) OR (Owner + tenant_id claim) — giữ `HtxMembershipOfficer` (HTX self-config) |
| `2_Gateway/Controllers/MembershipApplicationsController.cs` | Review endpoints (List/GetById/RequestMoreInfo/Approve/Reject/ListDocuments) → policy `MembershipReviewer` · `IsSystemAdmin` helper · List: SystemAdmin bắt buộc `htxTenantId` query, Owner dùng claim · Owner path giữ IDOR (HtxTenantId == claim) · ReviewedByUserId = sub |
| `2_Gateway/Controllers/MembershipProfileController.cs` | + `GET /api/membership/htx-profiles` (SystemAdmin) → `HtxProfileSummaryDto` list |
| `3_CoreHub/Services/Membership/IHtxProfileService.cs` + impl | + `ListAsync()` (join tenant name, IgnoreQueryFilters) |
| `3_CoreHub/Services/Membership/Dtos/MembershipDtos.cs` | `CreateMembershipApplicationRequest` += `CapitalContributionAmount` · `MembershipApplicationDto`/`MemberDto` += capital · `HtxProfileSummaryDto` (mới) |

## 2. LƯU Ý

- D2 (plan): override SRS §3.1/§6.5 cho quyền REVIEW — ghi chú trong code comment + card. Self-config HTX (charter/terms) KHÔNG đổi (vẫn Owner).
- SystemAdmin global (không tenant claim) → htxTenantId từ request (List query param); action theo id lấy từ application.
- `CreateMembershipApplicationRequest.CapitalContributionAmount` — KhachLink DTO mirror (`CreateMembershipApplicationRequestDto`) cập nhật ở Phase 4.

## 3. ACCEPTANCE

- [ ] SystemAdmin approve/reject/request-info mọi HTX (200) · Owner IDOR giữ (Forbid khi khác HTX)
- [ ] `GET /api/membership/htx-profiles` trả list HTX (SystemAdmin)
- [ ] Build sln 0 errors
