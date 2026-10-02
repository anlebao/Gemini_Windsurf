# MASTER PLAN: Quản lý thành viên HTX v2 (2 luồng admin — ShopERP, data sync PG)

> Created: 2026-10-02 (revised 2026-10-02 — user đơn giản hóa triệt để, thay thiết kế applicant/application cũ)
> Status: ⏳ PENDING USER REVIEW
> Branch: `main`

## 1. MỤC TIÊU

Đơn giản hóa quản lý thành viên HTX theo **2 luồng admin, đặt ở ShopERP, dữ liệu ghi vào PG** (qua Gateway API):

- **Luồng 1 — Tenant trở thành Hợp tác xã:** từ trang **danh sách Tenants**, bấm 1 nút → mở form **upload tài liệu (file doc, KHÔNG bắt buộc)** → tenant thành HTX (HtxProfile + Type=HTX + TT 71).
- **Luồng 2 — Chỉ tenant được làm thành viên HTX, do SystemAdmin thực hiện:**
  - Tenant khác muốn làm thành viên 1 HTX → **search tenant → add vào danh sách thành viên HTX** (SystemAdmin).
  - Cộng tác viên (Salesman/Shipper) → **nâng cấp lên thành tenant trước**, sau đó add như tenant thường.
- Giữ **3 phân loại thành viên** (chính thức / liên kết góp vốn / liên kết không góp vốn) + **số vốn góp cam kết** khi thêm thành viên.

**KHÔNG còn luồng applicant tự đăng ký** (KhachLink wizard, hồ sơ Draft/Submitted/NeedInfo, consent applicant, my-applications). Hệ thống application hiện có (SRS §7/§13/§15/§31) **giữ nguyên code (legacy)** — không xóa, không nằm trong phạm vi feature này.

## 2. HIỆN TRẠNG BASE CODE (khảo sát 2026-10-02)

| Thành phần | Hiện trạng |
|---|---|
| `MembershipType` (Official=1 / LinkedCapital=2 / LinkedNonCapital=3) | ✅ Đã có — `1_Shared/Domain/Aggregates/MembershipAggregate/MembershipTypes.cs` |
| `Member` | ✅ `MemberCustomerId` XOR `MemberTenantId` (CHECK constraint) · `Member.CreateActive` · **THIẾU field góp vốn** |
| `HtxProfileService.GetOrCreateAsync` | ✅ Hook S1: tạo profile → `MarkAsHtx()` (Type=HTX + TT71) — **tái dùng cho Luồng 1** |
| `MembershipProfileController` | POST /api/membership/htx-profile — `HtxMembershipOfficer` (Owner + tenant claim) — **SystemAdmin chưa tạo được hộ** |
| `Tenant` | `OwnerCustomerId` (Guid?) có sẵn · `CreateCompany/HouseholdBusiness/CreateUnverified/FromConversion` — **THIẾU factory "membership profile" (Type=null)** |
| `CommunityRoles` (PG) | ✅ DbSet — detect Salesman/Shipper (IsActive) |
| ShopERP `/admin/tenants` | Có trang danh sách tenant (SystemAdmin) — chưa có nút HTX |
| `MemberRegistryService` | Có List/Suspend/Resign/Terminate — **THIẾU AddMemberAsync (thêm trực tiếp)** |
| Membership data | PG-only (EF config: "NOT mirrored to ShopERP SQLite") — ShopERP UI gọi Gateway API → PG ✓ đúng kiến trúc |

## 3. QUYẾT ĐỊNH ĐÃ CHỐT (user 2026-10-02)

| # | Quyết định |
|---|---|
| D1 | **Bỏ luồng applicant/application** khỏi feature (giữ code legacy) — thay bằng 2 luồng admin |
| D2 | **Luồng 1**: tenant → HTX từ trang danh sách Tenants (ShopERP) — 1 nút + form upload tài liệu (tùy chọn) → HtxProfile (S1 hook: Type=HTX + TT71) |
| D3 | **Luồng 2**: chỉ TENANT làm thành viên HTX — SystemAdmin search tenant → add vào danh sách thành viên (member = `MemberTenantId`) |
| D4 | **Collaborator (Salesman/Shipper)**: nâng cấp thành tenant (auto-create tenant profile) TRƯỚC, rồi add như tenant thường — 2 bước riêng, SystemAdmin thực hiện |
| D5 | Auto-tenant profile (collaborator upgrade): **Type = null** (không loại hình, chỉ phục vụ membership) · Status = Active · `OwnerCustomerId` = customer · BusinessType = HouseholdBusiness · idempotent (1 tenant/customer) |
| D6 | **Góp vốn**: Member có `CapitalContributionAmount` — Official/LinkedCapital bắt buộc > 0 khi add; LinkedNonCapital cấm khai |
| D7 | **Guard verify**: member tenant phải **Active** (đã verify) mới add được |
| D8 | Cả 2 luồng đặt ở **ShopERP UI**, ghi **PG** qua Gateway API (không sync SQLite mới — đúng kiến trúc hiện có) |

## 4. SCOPE (phases + task cards)

| Phase | Nội dung | Layer | Task card | Trạng thái |
|---|---|---|---|---|
| 1 | Domain/Data: `Member.CapitalContributionAmount` + `Tenant.CreateMembershipProfile` + EF config + migration PG | 1_Shared + CoreHub | `task_phase1_domain_data.md` | 🔨 code in-flight (chưa commit) |
| 2 | Services: `CollaboratorTenantProvisioningService` + `MemberRegistryService.AddMemberAsync` (guard D6/D7) + `HtxProfileService.ListAsync` | 3_CoreHub | `task_phase2_services.md` | 🔨 code in-flight (1 phần) |
| 3 | API admin: `POST /api/admin/membership/htx-profile` (SystemAdmin tạo hộ, Luồng 1) + `POST /api/admin/membership/members` (add, Luồng 2) + `POST /api/admin/membership/collaborator-upgrade` (D4) + `GET /api/membership/htx-profiles` + tenant search | 2_Gateway | `task_phase3_api_admin.md` | ⏳ |
| 4 | ShopERP Luồng 1: `/admin/tenants` nút "Chuyển thành HTX" + modal upload tài liệu (tùy chọn) | 5_WebApps/ShopERP | `task_phase4_shoperp_flow1.md` | ⏳ |
| 5 | ShopERP Luồng 2: trang quản lý thành viên HTX (list + add/search tenant + nâng cấp CTV) | 5_WebApps/ShopERP | `task_phase5_shoperp_flow2.md` | ⏳ |
| **6** | **HTX Order Accounting — kết nối membership → kế toán TT 71** (tag nội bộ/ngoài + định khoản 511/512, 611/612) | 1_Shared + 3_CoreHub | `task_phase6_htx_order_accounting.md` | ⏳ |
| 7 | Tests + validation | 6_Tests | `task_phase7_tests.md` | ⏳ |
| 8 | RV production + docs | production | `task_phase8_rv.md` | ⏳ |

**Dependency chain:** 1 → 2 → 3 → (4 ∥ 5) → 6 → (7 ∥ 8).

## 5. THIẾT KẾ CHI TIẾT

### 5.1 Domain/Data (Phase 1)
- `Member.CapitalContributionAmount` (decimal?, precision 18,2) — factory param trên `Member.CreateActive` (snapshot khi add).
- `Tenant.CreateMembershipProfile(TenantId, name, settings?)`: Type=null · Status=Active · BusinessType=HouseholdBusiness · TenantCreatedEvent. OwnerCustomerId gán qua `AssignOwnerCustomer` (service).
- EF config: `MemberConfiguration` += CapitalContributionAmount (precision 18,2).
- Migration PG: `AddMembershipCapitalContribution` (numeric(18,2) — Members).
- ⚠️ KHÔNG thêm CapitalContributionAmount vào MembershipApplication (application flow legacy — không dùng trong feature này).

### 5.2 Services (Phase 2)
- `ICollaboratorTenantProvisioningService` (mới): `IsCollaboratorAsync` (CommunityRoles active Salesman/Shipper) · `GetExistingProfileAsync` (Tenant.OwnerCustomerId) · `GetOrCreateProfileAsync` (CreateMembershipProfile + AssignOwnerCustomer, idempotent — D5).
- `MemberRegistryService` (hoặc service mới `HtxMemberAdminService`): **`AddMemberAsync(htxTenantId, memberTenantId, membershipType, capitalContributionAmount?, reviewedByUserId)`**:
  - Guard D7: member tenant phải Active (EnsureVerifiedTenant).
  - Guard D6: Official/LinkedCapital → capital > 0; LinkedNonCapital → capital null.
  - Guard: HTX phải có HtxProfile (đã là HTX) — member chỉ thêm vào HTX thật.
  - Duplicate guard: (htx, memberTenantId) unique index có sẵn (UX_Members_HtxTenant).
  - `Member.CreateActive` (MemberTenantId = tenant) + member number (pattern GenerateMemberNumber).
- `HtxProfileService.ListAsync` (+ join tenant name — cho dropdown chọn HTX ở ShopERP).

### 5.3 API admin (Phase 3)
- **Luồng 1:** `POST /api/admin/membership/htx-profile` [SystemAdmin] body `{ htxTenantId, charterVersion?, termsVersion?, charterUrl? }` → `HtxProfileService.GetOrCreateAsync` (S1 hook: Type=HTX + TT71). Charter mặc định "v1.0" nếu trống.
- **Luồng 2:** `POST /api/admin/membership/members` [SystemAdmin] body `{ htxTenantId, memberTenantId, membershipType, capitalContributionAmount? }` → `AddMemberAsync` → trả `{ memberId, memberNumber }`.
- **Nâng cấp CTV (D4):** `POST /api/admin/membership/collaborator-upgrade` [SystemAdmin] body `{ customerId, displayName? }` → provisioning → trả `{ tenantId }` (idempotent — reuse nếu đã có).
- `GET /api/membership/htx-profiles` [SystemAdmin] (đã code in-flight — giữ) — bộ chọn HTX.
- **Tenant search** (cho Luồng 2): dùng endpoint tenant list hiện có của ShopERP admin (`/api/admin/tenants`? — TenantClaimApiClient) hoặc thêm query search name/MST.
- Bỏ thay đổi `MembershipReviewer`/controller review (application flow legacy — KHÔNG đụng nữa).

### 5.4 ShopERP Luồng 1 — Tenant → HTX (Phase 4)
- `/admin/tenants` (trang danh sách tenant): mỗi dòng tenant Active thêm nút **"Chuyển thành HTX"** → modal:
  - Upload tài liệu (file doc/pdf/jpg — **tùy chọn**, không bắt buộc) → upload qua endpoint upload ảnh/file hiện có → URL.
  - Nút "Xác nhận" → `POST /api/admin/membership/htx-profile` (charterUrl = URL tài liệu nếu có).
  - Success → tenant thành HTX (menu kế toán đổi TT 71, hiển thị badge "HTX").
- UI Platform compliance: VanAButton/VanAnModal/VanACard — không custom HTML/CSS.

### 5.5 ShopERP Luồng 2 — Quản lý thành viên HTX (Phase 5)
- Trang mới `/admin/membership/members` (SystemAdmin):
  - **Bộ chọn HTX** (dropdown từ `GET /api/membership/htx-profiles`).
  - **Danh sách thành viên** (Member list của HTX — MemberRegistryService.ListForHtx, cột: số thành viên, tên tenant, loại, vốn góp, trạng thái).
  - **"Thêm thành viên"** → modal: **search tenant** (tên/MST) → chọn → chọn **loại thành viên** (3 card/select) + **số vốn góp cam kết** (bắt buộc Official/LinkedCapital, ẩn LinkedNonCapital) → `POST /api/admin/membership/members`.
  - **"Nâng cấp CTV"** → modal: search cộng tác viên (customer Salesman/Shipper — tên/SĐT) → `POST /api/admin/membership/collaborator-upgrade` → trả tenant mới → tiếp tục "Thêm thành viên" với tenant đó.
  - Hành động member: Suspend/Terminate (MemberRegistryService có sẵn — tùy chọn thêm nút, giữ tối thiểu).

### 5.6 HTX Order Accounting (Phase 6) — CHECK 2026-10-02 (user directive)

**Kết quả check kết nối membership ↔ kế toán HTX:**
| Câu hỏi | Trạng thái | Bằng chứng |
|---|---|---|
| Membership ↔ kế toán HTX đã kết nối? | ❌ CHƯA | Order/OrderService không tham chiếu `Member`/HtxProfile; member (tenant) không tham gia order flow |
| Đủ thông tin phân biệt nội bộ/ngoài? | ❌ THIẾU | `Order` KHÔNG có field đánh dấu nội bộ/ngoài; không lookup counterparty là member của HTX bán |
| Định khoản đúng luật TT 71? | ❌ CHƯA | `OrderService` HARDCODE `accountCode: "511"` (revenue) + `"632"` (COGS) — **632 KHÔNG tồn tại trong TT 71**; không branch theo `AccountingStandard` (HTX phải 511/512 + 611/612) |

**Thiết kế Phase 6:**
- **Tag nội bộ/ngoài tại `CreateOrderAsync`** (Gateway order creator): tenant bán là HTX (`AccountingStandard == TT71_2024` hoặc Type=HTX) → xác định buyer:
  - Buyer = `Order.CustomerId` → tìm tenant có `OwnerCustomerId == customerId` (tenant sở hữu bởi buyer) → nếu tenant đó là **Member active của HTX bán** (`Members.TenantId == HTX && MemberTenantId == buyerTenant`) → **nội bộ**.
  - Ngược lại → ngoài. Lưu **`Order.IsInternalToHtx`** (bool?, Domain change + migration PG + OrderCreated payload sync SQLite).
- **Định khoản tại `ConfirmPaymentAsync`** (cash-basis TT 152 — accounting generation):
  - Tenant standard TT71: revenue → **512** (nội) / **511** (ngoài); COGS → **612** (nội) / **611** (ngoài); VAT **3331** (giữ). LN nội/ngoài theo dõi qua tài khoản (4211/4212 — không cần entry riêng MVP).
  - Tenant khác: giữ 511/632 (KHÔNG regress).
- Scope: chỉ đơn của tenant bán = HTX (Marketplace đơn giản); reseller path KHÔNG mở rộng cho HTX (note — HTX reseller chưa hỗ trợ).
- Tài liệu: B02-HTX tách 01a/01b (511/512) + 11a/11b (611/612) đã có (TT 71 S2) — Phase 6 cung cấp DỮ LIỆU đúng cho template.

### 5.7 Tests (Phase 7)
- Domain: `Member.CreateActive` capital guard · `Tenant.CreateMembershipProfile` (Type null/Active).
- Service: `AddMemberAsync` (guard D6/D7, HTX có HtxProfile, duplicate) · provisioning (tạo/reuse/idempotent) · ListAsync.
- API/controller: SystemAdmin htx-profile/members/collaborator-upgrade (200/400/409) · Owner bị chặn (403).
- Component (bUnit): Tenants nút HTX + modal · members page (list/add/search/upgrade).
- Full suites không regress + guard-check.

## 6. ACCEPTANCE CRITERIA

- [ ] Luồng 1: từ `/admin/tenants` bấm nút → upload tài liệu (tùy chọn) → tenant thành HTX (Type=HTX + TT71 + HtxProfile + badge)
- [ ] Luồng 2: SystemAdmin search tenant Active → add thành viên (3 loại + vốn góp đúng) → Member (MemberTenantId) trong PG
- [ ] Tenant chưa Active → chặn add (D7) · NonCapital khai vốn → chặn (D6)
- [ ] Collaborator nâng cấp thành tenant (Type=null, Active, OwnerCustomerId) → add thành viên được; nhiều HTX dùng chung 1 profile (D5)
- [ ] Dữ liệu nằm ở PG (qua Gateway API từ ShopERP UI) — đúng kiến trúc
- [ ] **HTX order accounting (Phase 6)**: đơn bán của HTX → tag nội bộ/ngoài đúng (buyer là member tenant → nội bộ) · định khoản 512/612 (nội) vs 511/611 (ngoài) · tenant DN giữ 511/632 (không regress) · B02-HTX 01a/01b + 11a/11b có số liệu đúng
- [ ] Core.Tests/ShopERP.Tests/Architecture không regress · guard-check PASS · RV production

## 7. GATES & LƯU Ý

- **Legacy application flow**: giữ nguyên code (SRS §7/§13/§15/§31) — KHÔNG xóa, KHÔNG đụng trong feature này; ghi chú "legacy" trong docs. MembershipApplication/KhachLink wizard/my-applications nằm ngoài scope.
- Domain: KHÔNG đụng AccountingEntry/TT 71 kế toán path.
- Migration PG chỉ 1 lần (Phase 1) — additive.
- Code in-flight (Phases 1-2 một phần, CHƯA COMMIT): `Member.CapitalContributionAmount` ✓ giữ · `Tenant.CreateMembershipProfile` ✓ giữ · `CollaboratorTenantProvisioningService` ✓ giữ · `HtxProfileService.ListAsync` + htx-profiles endpoint ✓ giữ · `MembershipApplication.CapitalContributionAmount` + `ApproveAsync` auto-provision + `MembershipReviewer` policy + controller review thay đổi → **REVERT** (application flow legacy — không thuộc feature mới). Nếu plan bị sửa tiếp → revert sạch trước khi implement lại.
- Playwright Gate 3: chỉ Phase 7.

## 8. RELATED FILES

- Domain: `1_Shared/Domain/Aggregates/MembershipAggregate/Member.cs` · `TenantAggregate/Tenant.cs`
- EF: `3_CoreHub/Infrastructure/Configurations/MemberConfiguration.cs` + Migration
- Services: `3_CoreHub/Services/Membership/{CollaboratorTenantProvisioningService(mới),MemberRegistryService,HtxProfileService,IHtxProfileService,Dtos/MembershipDtos.cs}`
- API: `2_Gateway/Controllers/MembershipProfileController.cs` + (mới) `2_Gateway/Controllers/MembershipAdminController.cs`
- UI: `5_WebApps/ShopERP/Components/Pages/Admin/Tenants.razor` + (mới) `MembershipMembers.razor` + `Services/MembershipApiClient.cs`
