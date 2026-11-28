# -*- coding: utf-8 -*-
import io

fn = 'docs/AI/plans/membership-registration-v2-master-plan.md'
c = io.open(fn, encoding='utf-8').read()

c = c.replace('> Status: ⏳ PENDING USER REVIEW',
              '> Status: 🏗 CODE DONE (P1-P7 2026-10-03, pushed `fd18fba5` — CD chạy) · P8 RV: L1/L2 pending, L3/L4/L5 chờ user thao tác thật')

repl = [
    ('| 1 | Domain/Data: `Member.CapitalContributionAmount` + `Tenant.CreateMembershipProfile` + EF config + migration PG | 1_Shared + CoreHub | `task_phase1_domain_data.md` | 🔨 code in-flight (chưa commit) |',
     '| 1 | Domain/Data: `Member.CapitalContributionAmount` + `Tenant.CreateMembershipProfile` + EF config + migration PG | 1_Shared + CoreHub | `task_phase1_domain_data.md` | ✅ `8e7a8149` |'),
    ('| 2 | Services: `CollaboratorTenantProvisioningService` + `MemberRegistryService.AddMemberAsync` (guard D6/D7) + `HtxProfileService.ListAsync` | 3_CoreHub | `task_phase2_services.md` | 🔨 code in-flight (1 phần) |',
     '| 2 | Services: `CollaboratorTenantProvisioningService` + `MemberRegistryService.AddMemberAsync` (guard D6/D7) + `HtxProfileService.ListAsync` | 3_CoreHub | `task_phase2_services.md` | ✅ `1388bfe6` |'),
    ('| 3 | API admin: `POST /api/admin/membership/htx-profile` (SystemAdmin tạo hộ, Luồng 1) + `POST /api/admin/membership/members` (add, Luồng 2) + `POST /api/admin/membership/collaborator-upgrade` (D4) + `GET /api/membership/htx-profiles` + tenant search | 2_Gateway | `task_phase3_api_admin.md` | ⏳ |',
     '| 3 | API admin: `POST /api/admin/membership/htx-profile` (SystemAdmin tạo hộ, Luồng 1) + `POST /api/admin/membership/members` (add, Luồng 2) + `POST /api/admin/membership/collaborator-upgrade` (D4) + `GET /api/membership/htx-profiles` + tenant search | 2_Gateway | `task_phase3_api_admin.md` | ✅ `296ea496` |'),
    ('| 4 | ShopERP Luồng 1: `/admin/tenants` nút "Chuyển thành HTX" + modal upload tài liệu (tùy chọn) | 5_WebApps/ShopERP | `task_phase4_shoperp_flow1.md` | ⏳ |',
     '| 4 | ShopERP Luồng 1: `/admin/tenants` nút "Chuyển thành HTX" + modal upload tài liệu (tùy chọn) | 5_WebApps/ShopERP | `task_phase4_shoperp_flow1.md` | ✅ `0e95a72e` |'),
    ('| 5 | ShopERP Luồng 2: trang quản lý thành viên HTX (list + add/search tenant + nâng cấp CTV) | 5_WebApps/ShopERP | `task_phase5_shoperp_flow2.md` | ⏳ |',
     '| 5 | ShopERP Luồng 2: trang quản lý thành viên HTX (list + add/search tenant + nâng cấp CTV) | 5_WebApps/ShopERP | `task_phase5_shoperp_flow2.md` | ✅ `1803698a` |'),
    ('| **6** | **HTX Order Accounting — kết nối membership → kế toán TT 71** (tag nội bộ/ngoài + định khoản 511/512, 611/612) | 1_Shared + 3_CoreHub | `task_phase6_htx_order_accounting.md` | ⏳ |',
     '| **6** | **HTX Order Accounting — kết nối membership → kế toán TT 71** (tag nội bộ/ngoài + định khoản 511/512, 611/612) | 1_Shared + 3_CoreHub | `task_phase6_htx_order_accounting.md` | ✅ `7c1e0010` |'),
    ('| 7 | Tests + validation | 6_Tests | `task_phase7_tests.md` | ⏳ |',
     '| 7 | Tests + validation | 6_Tests | `task_phase7_tests.md` | ✅ `ad978ef7` (18 tests + guard PASS) |'),
    ('| 8 | RV production + docs | production | `task_phase8_rv.md` | ⏳ |',
     '| 8 | RV production + docs | production | `task_phase8_rv.md` | 🏗 CD chạy — L1/L2 pending, L3-L5 chờ user |'),
]
for old, new in repl:
    assert old in c, old[:70]
    c = c.replace(old, new)

io.open(fn, 'w', encoding='utf-8', newline='\n').write(c)
print('master plan updated OK')
