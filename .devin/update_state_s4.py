# -*- coding: utf-8 -*-
import io

fn = 'docs/AI/project_state.md'
c = io.open(fn, encoding='utf-8').read()

old = '> **Last Updated:** 2026-10-02 · **Branch:** `main` @ `0dfb848f` (TT 71 kế toán HTX — S1-S4 CODE DONE; Phase 7 L1/L2/spec ✅, L3-L5 chờ tenant HTX thật)'
new = '> **Last Updated:** 2026-10-03 · **Branch:** `main` @ `fd18fba5` (Membership HTX v2 P1-P7 DONE + TT 71 S1-S4; CD chạy)'
assert old in c
c = c.replace(old, new)

marker = '* **2026-10-02 — TT 71 KẾ TOÁN HTX — S2 (PHASE 3: Tt71Templates B01/B02/B09-HTX) DONE + PUSHED'
entry = (
    '* **2026-10-03 — QUẢN LÝ THÀNH VIÊN HTX v2 — P1-P7 CODE DONE + PUSHED (`fd18fba5`, FAST PUSH no-verify — guard + build + tests PASS trong session; CD Multi-VPS #37053077940 chạy; Core.Tests **1889 PASS (+18)** · ShopERP.Tests 120 · Architecture 41 · guard ALL PASSED).** '
    'User đơn giản hóa triệt để luồng membership thành **2 luồng admin (ShopERP → PG)** — bỏ applicant/application (SRS §7/§13/§15/§31 legacy giữ nguyên, không xóa). '
    '**P1 Domain/Data (`8e7a8149`):** `Member.CapitalContributionAmount` (D6 — snapshot khi add) · `Tenant.CreateMembershipProfile` (D5 — Type=null, Active, HouseholdBusiness) · migration `AddMembershipCapitalContribution` (PG Members). '
    '**P2 Services (`1388bfe6`):** `CollaboratorTenantProvisioningService` (IsCollaborator qua CommunityRoles active Salesman/Shipper · GetOrCreateProfile idempotent — 1 tenant/customer, OwnerCustomerId) · `MemberRegistryService.AddMemberAsync` (guard D6: Official/LinkedCapital vốn >0, NonCapital cấm khai; D7: tenant Active; HTX có HtxProfile; self-membership; duplicate). '
    '**P3 API (`296ea496`):** `MembershipAdminController` [SystemAdmin] — POST htx-profile (Luồng 1, charterUrl tùy chọn) · POST members (Luồng 2) · POST collaborator-upgrade (guard isCollaborator) + GET htx-profiles + GET members?htxTenantId. '
    '**P4 (`0e95a72e`)** TenantManagement.razor nút "Chuyển thành HTX" + modal upload doc (pdf/doc/docx — tùy chọn) · **P5 (`1803698a`)** MembershipMembers.razor (dropdown HTX + list member + add/search tenant + nâng cấp CTV) + MembershipAdminApiClient (SystemAdmin JWT) + menu. '
    '**P6 (`7c1e0010`) — HTX Order Accounting (check user directive):** `Order.IsInternalToHtx` (bool?) + `SetHtxInternalFlag` — tag tại CreateOrderAsync (tenant bán TT71 → buyer `Tenants.OwnerCustomerId == Order.CustomerId` là member Active → nội bộ) · định khoản tại GenerateAccountingEntriesAsync: HTX → revenue **512 (nội)/511 (ngoài)**, COGS **612/611** (632 KHÔNG tồn tại TT 71); DN giữ 511/632 (không regress) · migrations ×2 (PG VanAnDbContext + SQLite ShopERPDbContext `AddOrderHtxInternalFlag` — P6b move vào Migrations/ đúng folder + namespace, fix VA-ARCH-001). '
    '**P7 (`ad978ef7`):** 18 tests (MembershipHtxAdminTests 12 — guards D6/D7/provisioning/ListAsync; OrderServiceHtxAccountingTests 6 — tag + 512/612/511/611/632). '
    '**Bài học:** (a) Moq Setup phải khớp chính xác signature (CreateRevenueEntryAsync không CancellationToken; expense có vendor/category) — expression-tree optional-args error; (b) out-param không dùng trong lambda Moq → capture class; (c) `Contains(t.Id.Value)` không translate — dùng VO list (Pattern #8); (d) ShopERP migrations folder = `Migrations/` root (không phải Infrastructure/Migrations) — VA-ARCH-001; (e) guard-check FAST gate bắt architecture vi phạm (migration sai folder). '
    '**Còn lại (Phase 8):** L1/L2 RV sau CD (markers + migration applied) + user thao tác thật (L3/L4/L5) → đóng plan + project_state.'
)
assert marker in c
c = c.replace(marker, entry + '\n\n' + marker, 1)
io.open(fn, 'w', encoding='utf-8', newline='\n').write(c)
print('project_state updated OK')
