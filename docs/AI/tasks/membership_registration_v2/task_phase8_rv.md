# TASK CARD: Phase 8 — RV production + tài liệu

> **Master plan:** `docs/AI/plans/membership-registration-v2-master-plan.md`
> **Status:** ✅ COMPLETE 2026-10-03 — RV kế toán HTX production HOÀN TẤT (3 bug phát hiện + fix + verify live; DN/HKD không regress)

## 1. RV CHECKLIST (KẾT QUẢ)

- [x] **L1 API:** `POST /api/admin/membership/htx-profile` (Luồng 1 — fix DI `ICollaboratorTenantProvisioningService` `dc5a972b`) · `POST /api/admin/membership/members` (Luồng 2 — fix `ListMembers` chưa commit `c28c7256`) · collaborator-upgrade · htx-profiles — **user manual test PASS**
- [x] **L1 data:** PG — HTX tenants Type=5 (a5b6c7d8 + 0000...0001) · Members 3 (MEM-HTX-000001/2/3 — loại + vốn đúng) · SQLite Orders `IsInternalToHtx` column ✓
- [x] **L2 markers:** `MembershipAdminController`/`CollaboratorTenantProvisioningService` (Gateway/CoreHub.dll) · `MembershipAdminApiClient` (ShopERP.dll) · migrations PG+SQLite applied
- [x] **L3 REAL postings (kế toán HTX) — verified live:**
  - **NGOÀI** order `01a0ffbf` (guest, HTX a5b6c7d8): **511** 100.000 + 3331 10.000 + **611** 77.000 ✓ (611 thay 632)
  - **NỘI BỘ** order `01a0ffe5` (buyer = chủ member Samho): **512** 150.000 + 3331 15.000 + **612** 115.500 ✓
  - **DN regression** order `01a10006` (Test Minimal, TT133): **511** + 3331 + **632** ✓ KHÔNG regress
- [x] **L4 RV phát hiện + fix 3 bug production (mỗi fix deploy + verify live):**
  1. **P6b `f16e22d9`**: OrderService quyết định HTX đọc SQLite (IVanAnDbContext=ShopERPDbContext) nhưng AccountingStandard/Members PG-only → inject `VanAnDbContext? pgDbContext` → `pgDbContext ?? _dbContext` (đồng nguồn với bút toán PG)
  2. **P6c `1e196436`**: checkout (`CreateOrderFromCommandAsync`) không chạy tag nội bộ/ngoài (chỉ `CreateOrderAsync`) → `ApplyHtxInternalTagAsync` dùng chung 2 path + regression test (7/7)
  3. **P6d `870bcd75`**: OrderCreated payload (anonymous DTO) thiếu `IsInternalToHtx` → sync SQLite mất flag → thêm field vào payload (verify: SQLite flag=1 → entries 512/612)
- [x] **L5:** user giữ RV data làm dữ liệu mẫu (đơn test 01a0ffbf/01a0ffc0/01a0ffd9/01a0ffe5/01a10006 + customer c0a8584b → tenant a5b6c7d8 + Samho.OwnerCustomerId)

## 2. DATA/QUY TRÌNH

- RV data giữ nguyên (user quyết định 2026-10-03 — làm dữ liệu mẫu B02-HTX nội/ngoài).
- Samho.OwnerCustomerId = c0a8584b (gán cho test internal — nếu đúng chủ thật thì giữ).
- 3 fix P6b/c/d nằm trong chuỗi RV — đều FAST PUSH + CD SUCCESS + verify live.

## 3. TÀI LIỆU

- [x] `docs/AI/project_state.md` Sections 2/3/4/10
- [x] Task cards Phase 1-8 → COMPLETE
- [x] Master plan → DONE
