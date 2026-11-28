# -*- coding: utf-8 -*-
import io

fn = 'docs/AI/project_state.md'
c = io.open(fn, encoding='utf-8').read()

# Last Updated
old = '> **Last Updated:** 2026-10-03 · **Branch:** `main` @ `fd18fba5` (Membership HTX v2 P1-P7 DONE + TT 71 S1-S4; CD chạy)'
new = '> **Last Updated:** 2026-10-03 · **Branch:** `main` @ `870bcd75` (Membership HTX v2 DONE P1-P8 + RV kế toán HTX PASS + TT 71 S1-S4)'
assert old in c
c = c.replace(old, new)

# Maintenance log entry — chèn sau entry P1-P7 (marker = '**Còn lại (Phase 8):** L1/L2 RV sau CD (markers + migration applied) + user thao tác thật (L3/L4/L5) → đóng plan + project_state.')
marker = '**Còn lại (Phase 8):** L1/L2 RV sau CD (markers + migration applied) + user thao tác thật (L3/L4/L5) → đóng plan + project_state.'
entry = (
    '* **2026-10-03 — QUẢN LÝ THÀNH VIÊN HTX v2 — PHASE 8 RV KẾ TOÁN HTX PRODUCTION HOÀN TẤT + 3 BUG FIX (P6b/c/d) — `f16e22d9`/`1e196436`/`870bcd75`, CD SUCCESS ×3, RV verified live).** '
    '**RV data (PG + SQLite, giữ làm mẫu — user quyết định):** HTX tenants Type=5 (a5b6c7d8 + 0000...0001) · Members 3 (MEM-HTX-000001/2/3 loại+vốn) · đơn test 01a0ffbf/01a0ffc0/01a0ffd9/01a0ffe5/01a10006 + customer c0a8584b (tenant→a5b6c7d8) + Samho.OwnerCustomerId=c0a8584b. '
    '**Định khoản verified:** HTX NGOÀI (01a0ffbf, guest 100k) = **511** 100.000 + 3331 10.000 + **611** 77.000 ✓ (611 thay 632) · HTX NỘI BỘ (01a0ffe5, buyer=chủ member Samho 150k) = **512** 150.000 + 3331 15.000 + **612** 115.500 ✓ · **DN regression** (01a10006, Test Minimal TT133) = **511** + 3331 + **632** ✓ KHÔNG regress (mọi nhánh HTX guard `AccountingStandard == TT71_2024`). '
    '**3 bug RV phát hiện + fix + verify live:** '
    '(1) **P6b `f16e22d9`** — OrderService quyết định HTX (IsHtxSellingTenantAsync/IsBuyerHtxMemberAsync/ApplyHtxInternalTagAsync) dùng `IVanAnDbContext` = ShopERPDbContext (SQLite) ở ShopERP, nhưng AccountingStandard (MarkAsHtx không sync SQLite) + Members là PG-only → HTX bị 511/632. Fix: inject `VanAnDbContext? pgDbContext` (PG — đã register ShopERP/Gateway) → `pgDbContext ?? _dbContext` (đồng nguồn bút toán PG). '
    '(2) **P6c `1e196436`** — checkout `CreateOrderFromCommandAsync` (path đặt hàng thật) KHÔNG chạy tag nội bộ/ngoài (chỉ `CreateOrderAsync` POS) → đơn checkout IsInternalToHtx luôn null. Fix: extract `ApplyHtxInternalTagAsync` dùng chung 2 path + regression test `CreateOrderFromCommandAsync_HtxCheckout_MarksInternal` (mock AddAsyncNoSave/SaveChanges/BeginTransaction + Customer tenant=TestTenantProvider — 7/7 PASS). '
    '(3) **P6d `870bcd75`** — OrderCreated event payload là anonymous DTO (không phải Order entity) — THIẾU IsInternalToHtx → OrderSyncSubscriber (P6b đã có logic đọc) không có gì map → SQLite mirror flag NULL → internal vẫn 511/611. Fix: thêm `IsInternalToHtx = createdOrder.IsInternalToHtx` vào payload → verify: SQLite flag=1 → entries 512/612 ✓. '
    '**Bài học RV:** (a) production path sinh bút toán = ShopERP subscriber đọc SQLite mirror, còn bảng quyết định (Members/AccountingStandard) PG-only → 2 nguồn dữ liệu lệch — phải inject PG context riêng; (b) đơn thật tạo qua CreateOrderFromCommandAsync (không phải CreateOrderAsync) — tag phải nằm ở path thật; (c) event payload là DTO riêng — field mới phải thêm vào payload + subscriber đồng bộ; (d) verify từng lớp: PG order flag → SQLite mirror flag → bút toán PG.'
)
assert marker in c
c = c.replace(marker, marker + '\n\n' + entry, 1)
io.open(fn, 'w', encoding='utf-8', newline='\n').write(c)
print('project_state log updated OK')
