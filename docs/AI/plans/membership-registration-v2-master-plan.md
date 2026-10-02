# MASTER PLAN: Luồng đăng ký thành viên HTX v2 (3 phân loại + góp vốn + SystemAdmin review + collaborator auto-tenant)

> Created: 2026-10-02
> Status: ⏳ PENDING USER REVIEW (code in-flight CHƯA commit — chờ duyệt plan)
> Branch: `main`

## 1. MỤC TIÊU

Cải thiện luồng đăng ký thành viên hợp tác xã (HTX) theo Membership Infrastructure hiện có
(SRS §7/§13/§15/§31), đủ **3 phân loại thành viên** + **3 user directives 2026-10-02**:

1. **UX v2**: wizard 4 bước thay form 1 trang dài — chọn loại (3 card), form động theo loại,
   khai báo **số vốn góp cam kết**, tư cách pháp nhân (HKD/DN), trang "Hồ sơ của tôi", link/QR share.
2. **User directive 1**: chỉ xét duyệt thành viên khi tenant đã **verify (Active)**.
3. **User directive 2**: **SystemAdmin có quyền xét duyệt TẤT CẢ** hồ sơ xin gia nhập (mọi HTX — global),
   song song Owner (chỉ HTX của mình). → override SRS §3.1/§6.5 ("HTX quyết định, KHÔNG phải SystemAdmin") cho quyền review.
4. **User directive 3**: cộng tác viên (Salesman/Shipper) được duyệt làm thành viên HTX → **tự động phát sinh
   tenant profile** (không loại hình, Active, OwnerCustomerId = collaborator) — member tham chiếu tenant profile đó.

## 2. HIỆN TRẠNG BASE CODE (khảo sát 2026-10-02)

| Thành phần | Hiện trạng |
|---|---|
| `MembershipType` (Official=1 / LinkedCapital=2 / LinkedNonCapital=3) | ✅ Đã có — `1_Shared/Domain/Aggregates/MembershipAggregate/MembershipTypes.cs` |
| `MembershipApplication` | ✅ Draft→Submitted→NeedInfo⇄Submitted→Approved/Rejected; applicant = Customer + `BusinessTenantId?` (polymorphic); **THIẾU field góp vốn** |
| `Member` | ✅ `MemberCustomerId` XOR `MemberTenantId` (CHECK constraint); **THIẾU field góp vốn** |
| `HtxProfileService.GetOrCreateAsync` | ✅ Hook S1: tạo profile → `MarkAsHtx()` (Type=HTX + TT71) |
| Review policy | `HtxMembershipOfficer` = Owner + tenant claim — **explicit CẤM SystemAdmin** (SRS §3.1/§6.5) |
| KhachLink form | 1 trang dài (`MembershipRegister.razor`): select 3 loại, không góp vốn, không pháp nhân, bắt buộc login sẵn |
| Trang "Hồ sơ của tôi" | ❌ Endpoint `GET /api/membership/applications/my` CÓ — **UI không có** |
| Tenant | `TenantStatus` Pending(5)→Verify()→Active(1) · `OwnerCustomerId` (Guid?) có sẵn · factory `CreateCompany/HouseholdBusiness/CreateUnverified/FromConversion` — **THIẾU factory "membership profile" (Type=null)** |
| `CommunityRoles` (PG) | ✅ DbSet có sẵn — detect Salesman/Shipper (IsActive) |

## 3. QUYẾT ĐỊNH ĐÃ CHỐT (user 2026-10-02)

| # | Quyết định |
|---|---|
| D1 | **Phase A đầy đủ**: wizard 4 bước + 3 card loại + góp vốn (bắt buộc với Official/LinkedCapital) + tư cách pháp nhân + trang "Hồ sơ của tôi" + link/QR share + cột vốn ShopERP review |
| D2 | **SystemAdmin duyệt TẤT CẢ** hồ sơ (mọi HTX) — bên cạnh Owner (chỉ HTX của mình). Ghi nhận override SRS §3.1/§6.5 |
| D3 | **Auto-tạo tenant profile cho collaborator** khi được APPROVE (không phải lúc nộp đơn) |
| D4 | Auto-tenant: **Type = null** (không loại hình, chỉ phục vụ membership) · Status = Active · OwnerCustomerId = collaborator · BusinessType = HouseholdBusiness |
| D5 | Góp vốn: Official/LinkedCapital → bắt buộc số tiền > 0 (enforce tại Submit domain); LinkedNonCapital → cấm khai (guard factory) |
| D6 | Guard verify: `BusinessTenantId` phải Active khi Create + Approve (re-check) |

## 4. SCOPE (phases + task cards)

| Phase | Nội dung | Layer | Task card | Trạng thái |
|---|---|---|---|---|
| 1 | Domain/Data: `CapitalContributionAmount` (Application+Member) + `Tenant.CreateMembershipProfile` + EF config + migration PG | 1_Shared + CoreHub | `task_phase1_domain_data.md` | 🔨 code in-flight (chưa commit) |
| 2 | Services: guard tenant-verify (D6) + `CollaboratorTenantProvisioningService` + `ApproveAsync` auto-provision (D3) | 3_CoreHub | `task_phase2_services.md` | 🔨 code in-flight |
| 3 | API: policy `MembershipReviewer` (D2) + controller SystemAdmin + `GET /api/membership/htx-profiles` + DTO capital | 2_Gateway | `task_phase3_api_review.md` | 🔨 code in-flight |
| 4 | KhachLink UI: wizard 4 bước + góp vốn + pháp nhân + trang "Hồ sơ của tôi" | 5_WebApps/KhachLink | `task_phase4_khachlink_ui.md` | ⏳ chưa code |
| 5 | ShopERP UI: MembershipApplications (SystemAdmin + cột vốn + filter loại) + MembershipHtxProfile link/QR | 5_WebApps/ShopERP | `task_phase5_shoperp_ui.md` | ⏳ chưa code |
| 6 | Tests + validation | 6_Tests | `task_phase6_tests.md` | ⏳ |
| 7 | RV production + docs | production | `task_phase7_rv.md` | ⏳ |

**Dependency chain:** 1 → 2 → 3 → (4 ∥ 5) → 6 → 7.

## 5. THIẾT KẾ CHI TIẾT

### 5.1 Domain/Data (Phase 1)
- `MembershipApplication.CapitalContributionAmount` (decimal?, precision 18,2):
  - factory guard: LinkedNonCapital + amount != null → throw.
  - `Submit()` guard: Official/LinkedCapital + amount null/<=0 → throw InvalidOperationException.
- `Member.CapitalContributionAmount` (decimal?) — snapshot từ application khi approve.
- `Tenant.CreateMembershipProfile(TenantId, name, settings?)`: Type=null · Status=Active · BusinessType=HouseholdBusiness · raise TenantCreatedEvent. OwnerCustomerId gán qua `AssignOwnerCustomer` (service).
- EF config: map 2 cột mới (MembershipApplicationConfiguration + MemberConfiguration).
- Migration PG: `AddMembershipCapitalContribution` (numeric(18,2) ×2).

### 5.2 Services (Phase 2)
- `ICollaboratorTenantProvisioningService` (mới, `3_CoreHub/Services/Membership/`):
  - `IsCollaboratorAsync(customerId)`: CommunityRoles (IgnoreQueryFilters) active Salesman/Shipper, !IsDeleted.
  - `GetExistingProfileAsync(customerId)`: Tenant có `OwnerCustomerId == customerId` (reuse — 1 tenant / 1 customer, dùng chung nhiều HTX).
  - `GetOrCreateProfileAsync(customerId, displayName, phone, email)`: reuse hoặc tạo `CreateMembershipProfile` + `AssignOwnerCustomer` + save.
- `MembershipApplicationService`:
  - inject `ICollaboratorTenantProvisioningService`.
  - `CreateApplicationAsync`: pass `CapitalContributionAmount` + **guard D6** (BusinessTenantId → tenant Active).
  - `ApproveAsync`: re-check D6 + **auto-provision D3** — member party: BusinessTenantId (đã verify) → tenant đó; nếu null && IsCollaborator → GetOrCreateProfile → memberTenantId = auto-tenant; nếu null → memberCustomerId = applicant. Pass `CapitalContributionAmount` vào Member.CreateActive.
  - `EnsureVerifiedTenantAsync(tenantId)` helper (TenantStatus.Active check, IgnoreQueryFilters).
- `MemberRegistryService.MapToDto` += CapitalContributionAmount.

### 5.3 API (Phase 3)
- Gateway `Program.cs`: policy mới `MembershipReviewer` = AuthenticatedUser + (SystemAdmin) OR (Owner + tenant_id claim). Giữ `HtxMembershipOfficer` (HTX self-config — charter — không đổi).
- `MembershipApplicationsController`: review endpoints (List/GetById/RequestMoreInfo/Approve/Reject/ListDocuments) đổi policy → `MembershipReviewer`:
  - `IsSystemAdmin` helper; Owner path giữ claim; SystemAdmin: List bắt buộc `htxTenantId` query param, các action bỏ IDOR claim check.
  - ReviewedByUserId = sub claim (cả 2 role).
- `MembershipProfileController`: `GET /api/membership/htx-profiles` (SystemAdmin) → `HtxProfileSummaryDto(htxTenantId, tenantName, charterVersion)` — bộ chọn HTX cho review UI.
- `IHtxProfileService.ListAsync` + impl (join tenant name).
- DTOs: `CreateMembershipApplicationRequest` += `CapitalContributionAmount`; `MembershipApplicationDto`/`MemberDto` += CapitalContributionAmount; `HtxProfileSummaryDto` (mới).

### 5.4 KhachLink UI (Phase 4) — wizard 4 bước (`MembershipRegister.razor`)
- **Bước 0 (entry)**: tên HTX + Điều lệ vX + "Bắt đầu đăng ký"; login inline nếu chưa có customer_token (nút đăng nhập → OTP).
- **Bước 1 — 3 card chọn loại** (thay select): Official (quyền biểu quyết/bầu cử + nghĩa vụ góp vốn), LinkedCapital (góp vốn theo Điều lệ), LinkedNonCapital (không góp vốn). Mỗi card nút "Chọn" + "Xem Điều lệ vX".
- **Bước 2 — form động theo loại**: Họ tên/SĐT prefill (khớp xác thực), Email, Khu vực, Vai trò dự kiến; **field "Số vốn góp cam kết (VNĐ)"** (Official/LinkedCapital — bắt buộc, format tiền, note "HTX xác nhận thu sau duyệt"); **tư cách**: Cá nhân / Tổ chức (HKD/DN) — nếu collaborator (detect qua `/api/community/role`) và chưa có tenant sở hữu → option "Hệ thống tự tạo hồ sơ" (D3 — tạo tại Approve); nếu có tenant sở hữu → dropdown chọn.
- **Bước 3 — Xác nhận + consent**: tóm tắt (loại + số vốn góp) + checkbox Điều lệ (bắt buộc) + ký tay online (tùy chọn) → "Gửi hồ sơ".
- **Bước 4 — Kết quả + theo dõi**: mã hồ sơ + trạng thái + nút "Theo dõi hồ sơ" → trang mới `/membership/my-applications` (endpoint đã có `GET /api/membership/applications/my`): list hồ sơ mọi HTX + trạng thái + nút Nộp (Draft)/Bổ sung (NeedInfo→Resubmit). Giữ in form giấy + upload bản ký tay.
- `MembershipHttpService`: MyApplicationsAsync + ResubmitAsync + DTO capital.

### 5.5 ShopERP UI (Phase 5)
- `MembershipApplications.razor`: cho phép SystemAdmin (nếu SystemAdmin → dropdown chọn HTX từ `GET /api/membership/htx-profiles`, review mọi HTX; Owner → giữ tenant claim) + cột **"Số vốn góp cam kết"** + bộ lọc theo loại thành viên.
- `MembershipHtxProfile.razor`: nút "Link đăng ký thành viên" (copy) + **QR code** (IQrCodeService — pattern PrintTicket) → dán quầy/in.

### 5.6 Tests (Phase 6)
- Domain: Submit() capital guard (3 loại) · factory guard NonCapital + amount → throw · CreateMembershipProfile (Type null/Active).
- Service: EnsureVerifiedTenantAsync (Pending/Suspended → throw) · CollaboratorTenantProvisioning (idempotent, tạo mới, reuse) · ApproveAsync auto-provision (collaborator → MemberTenantId = auto-tenant; non-collaborator → MemberCustomerId; BusinessTenantId → tenant).
- API/controller: SystemAdmin approve global · Owner IDOR giữ · htx-profiles endpoint.
- Component (bUnit): wizard 3 card + field vốn động · my-applications render · ShopERP review cột vốn.
- Full suites không regress + guard-check.

## 6. ACCEPTANCE CRITERIA

- [ ] 3 loại thành viên đăng ký được với góp vốn đúng (Official/LinkedCapital bắt buộc > 0; LinkedNonCapital không có field)
- [ ] Tenant tư cách pháp nhân phải Active mới tạo hồ sơ / được duyệt (D6)
- [ ] SystemAdmin duyệt/từ chối/request-info mọi HTX; Owner chỉ HTX của mình (D2 — override SRS §3.1 ghi nhận)
- [ ] Collaborator (Salesman/Shipper) được approve → tự động có tenant profile (Type=null, Active) + Member gắn tenant đó; nhiều HTX dùng chung 1 profile (D3/D4)
- [ ] Trang "Hồ sơ của tôi" hoạt động (list + trạng thái + submit/resubmit)
- [ ] Link đăng ký + QR từ ShopERP
- [ ] Core.Tests/ShopERP.Tests/Architecture không regress · guard-check PASS · RV production

## 7. GATES & LƯU Ý

- **SRS override (D2)**: ghi nhận rõ trong code comment + card — SystemAdmin có quyền review membership (user directive), KHÔNG đổi quyền self-config HTX (charter vẫn Owner).
- Domain: KHÔNG đụng AccountingEntry/TT 71 kế toán path. Membership aggregates là phạm vi riêng.
- Migration PG chỉ 1 lần (Phase 1) — additive, không đụng bảng khác.
- Code in-flight (Phases 1-3, CHƯA COMMIT): nếu plan bị sửa → revert sạch trước khi implement lại.
- Playwright Gate 3: chỉ Phase 7.

## 8. RELATED FILES

- Domain: `1_Shared/Domain/Aggregates/MembershipAggregate/{MembershipApplication,Member,MembershipTypes}.cs` · `TenantAggregate/Tenant.cs`
- EF: `3_CoreHub/Infrastructure/Configurations/{MembershipApplicationConfiguration,MemberConfiguration}.cs` + Migration
- Services: `3_CoreHub/Services/Membership/{MembershipApplicationService,CollaboratorTenantProvisioningService(mới),HtxProfileService,MemberRegistryService,IHtxProfileService,Dtos/MembershipDtos.cs}`
- API: `2_Gateway/Controllers/{MembershipApplicationsController,MembershipProfileController}.cs` · `2_Gateway/Program.cs`
- UI: `5_WebApps/KhachLink/Pages/MembershipRegister.razor` + `MembershipMyApplications.razor (mới)` + `Services/Http/MembershipHttpService.cs`
- UI: `5_WebApps/ShopERP/Components/Pages/Admin/{MembershipApplications,MembershipHtxProfile}.razor`
