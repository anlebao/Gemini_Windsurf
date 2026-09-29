# TASK CARD: Phase 1 — Domain + Events

> **Master plan:** `docs/AI/tasks/membership_infra/master_plan.md`
> **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT — 7 step)
> **Status:** IMPLEMENTING (2026-09-29)

## 1. OBJECTIVE

Thêm Domain foundation cho Membership Infrastructure (hybrid — kế thừa pattern `TenantClaimRequest`):
4 aggregates/entities trong `1_Shared/Domain/Aggregates/MembershipAggregate/` + events.
KHÔNG đụng `Tenant`, `Customer`, `AccountingEntry`. Chưa có DB (Phase 2).

## 2. GATES & HARD STOPS

- **🔴 Data Integrity Contract (mục 6 master plan):** polymorphic member = 2 FK `Guid?` + CHECK; cấm Guid-ref không FK.
- **🔴 Single-Identity Pattern:** mọi FK là `Guid`/`Guid?` trỏ PK; KHÔNG business key VO; KHÔNG `.Value` trong LINQ; ctor KHÔNG cần sync (không VO).
- **🔴 Không xóa member/application vật lý** — status transition only (SRS §7).
- **AccountingEntry immutable:** N/A — không touch.

## 3. PRE-CONDITIONS (đã chốt)

- [x] User duyệt hybrid + 3 quyết định (A1: HTX=Tenant · A2: NetworkId=Customer · A3: registry PG-only)
- [x] User duyệt Data Integrity Contract (2 FK + CHECK + guards + merge migrate + cross-tenant)
- [x] Pattern reference verified: `TenantClaimRequest` (state machine), `Common.cs` (BaseEntity/AggregateRoot), `Customer` (Id=PK Guid, `Domain.cs:659`)

## 4. FILES TO CREATE

| Path | Role |
|---|---|
| `1_Shared/Domain/Aggregates/MembershipAggregate/MembershipTypes.cs` | Enums: `MembershipType` (OfficialMember, LinkedCapitalMember, LinkedNonCapitalMember) · `ApplicationStatus` (Draft, Submitted, NeedInfo, Approved, Rejected) · `MemberStatus` (Active, Suspended, Resigned, Terminated) · `IdentityVerificationLevel` (Level1Otp, Level2PersonalInfo, Level3Kyc, Level4Manual) · `CapitalFeeStatus` (None, Pending, Satisfied) · `ExpectedRole` (Seller, Ctv, Shipper, Supplier, Customer, Other) |
| `1_Shared/Domain/Aggregates/MembershipAggregate/MembershipApplication.cs` | Aggregate root (template `TenantClaimRequest`): `Id` PK · `BaseEntity.TenantId` = htx_id · `ApplicantCustomerId Guid` (FK Customers — network id) · `BusinessTenantId Guid?` (FK Tenants — nếu xin gia nhập với tư cách HKD/DN) · `MembershipType` · `FullName/Phone/Email/Region/ExpectedRole` (Applicant Profile snapshot — SRS §13) · `IdentityVerificationLevel` · `ConsentVersion`/`CharterVersion` (snapshot) · `CapitalFeeStatus` · `SubmittedAt/ReviewedByUserId/ReviewedAt/RejectionReason` · Factory `Create(...)` · Methods `Submit()`, `RequestMoreInfo(reviewer, reason)`, `Resubmit()`, `Approve(reviewer)`, `Reject(reviewer, reason)`. Guards: chỉ transition hợp lệ; `BusinessTenantId != TenantId` (self-membership). |
| `1_Shared/Domain/Aggregates/MembershipAggregate/Member.cs` | Aggregate root: `Id` PK · `BaseEntity.TenantId` = htx_id · `MemberCustomerId Guid?` + `MemberTenantId Guid?` (**exactly-one — CHECK ở Phase 2**) · `MembershipType` · `MemberNumber` (MEM-HTX-Q1-000182 format — SRS §18, generate ở service) · `Status` · `JoinedAt/ApprovedAt/EffectiveAt/TerminatedAt` · Factory `CreateActive(...)` guard: self-membership (`MemberTenantId != TenantId`) + exactly-one + status=Active · Methods `Suspend(reason)`, `Reactivate()`, `Resign()`, `Terminate(reason)`. |
| `1_Shared/Domain/Aggregates/MembershipAggregate/ConsentRecord.cs` | Audit entity (BaseEntity, KHÔNG aggregate): `Id` · `TenantId` (htx) · `ApplicantCustomerId Guid` · `DocumentType` (Charter/Terms/DataPolicy) · `DocumentVersion` · `Timestamp` · `EvidenceReference` (IP/device) · Factory `Create(...)`. |
| `1_Shared/Domain/Aggregates/MembershipAggregate/HtxProfile.cs` | Entity: đánh dấu tenant là HTX + phiên bản Điều lệ hiện hành. `Id` · `TenantId` (unique FK) · `CharterVersion` · `TermsVersion` · `CharterUrl?` · Factory `Create(...)` + `UpdateCharter(charterVersion, charterUrl)`. |
| `1_Shared/Domain/Aggregates/MembershipAggregate/MembershipEvents.cs` | `MembershipApplicationSubmittedEvent` · `MembershipApplicationApprovedEvent(TenantId, ApplicationId, MemberId, ReviewedByUserId, OccurredAt)` · `MembershipApplicationRejectedEvent` · `MemberActivatedEvent(TenantId, MemberId, MemberNumber, NetworkCustomerId, MembershipType, OccurredAt)` · `MemberStatusChangedEvent(TenantId, MemberId, OldStatus, NewStatus, Reason, OccurredAt)`. |

## 5. ACCEPTANCE CRITERIA

- [ ] `dotnet build 1_Shared/VanAn.Shared.csproj` — 0 errors
- [ ] `MembershipApplication.Create` guard: `BusinessTenantId != TenantId.Value` throw; `Create` không tự động Submitted (Draft)
- [ ] `Submit()` chỉ từ Draft → Submitted; `Approve()` chỉ từ Submitted; `Reject()` chỉ từ Submitted; `RequestMoreInfo()` chỉ từ Submitted → NeedInfo; `Resubmit()` chỉ từ NeedInfo → Submitted — mọi transition sai throw `InvalidOperationException`
- [ ] `Approve()` set `ReviewedByUserId/ReviewedAt`, raise `MembershipApplicationApprovedEvent` (KHÔNG tự tạo Member — service làm)
- [ ] `Member.CreateActive` guard: exactly-one (customer XOR tenant) + `MemberTenantId != TenantId`; status=Active; raise `MemberActivatedEvent` + `MembershipApplicationApprovedEvent` KHÔNG raise ở đây
- [ ] `Member` status machine: Active→Suspended→Active (Reactivate) · Active→Resigned · Active→Terminated; cấm xóa (không có Delete method)
- [ ] `ConsentRecord.Create` + `HtxProfile.Create/UpdateCharter` — audit fields đúng
- [ ] Mọi FK field là `Guid`/`Guid?` — Single-Identity compliant
- [ ] Có thể nhận xét: không phá vỡ namespace `VanAn.Shared.Domain.Aggregates.*` hiện có

## 6. VERIFICATION

```powershell
dotnet build 1_Shared\VanAn.Shared.csproj
```

- Domain unit tests ở Phase 6 (theo plan) — Phase 1 chỉ build.
- Không DB migration ở Phase 1 (Phase 2).

## 7. QUYẾT ĐỊNH ĐÃ CHỐT (tránh hỏi lại)

| # | Quyết định | Giá trị |
|---|---|---|
| D1 | Application có phân biệt Draft vs tạo-thẳng-Submitted không? | Có `Draft` — Assisted Registration (CTV nhập hộ) cần lưu nháp |
| D2 | Ai tạo `Member` khi Approve? | Service (`MembershipApplicationService.ApproveAsync`) — domain `Approve()` chỉ chuyển status + event |
| D3 | MemberNumber format | `MEM-{htxSlug}-{seq}` — sinh ở service, không trong domain |
| D4 | HtxProfile có cần không Phase 1? | Có — Consent cần CharterVersion để chứng minh bản xác nhận |
