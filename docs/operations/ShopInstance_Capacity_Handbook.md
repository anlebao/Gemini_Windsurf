# Sổ tay triển khai: Chọn ShopInstance cho tenant mới

> **Mục đích:** Hướng dẫn nhân viên triển khai (SystemAdmin / DevOps) chọn ShopInstance (VPS) phù hợp khi thêm tenant mới vào hệ thống VanAn, sao cho **không vượt capacity thực tế** của VPS và **tối ưu chi phí bandwidth**.
>
> **Phạm vi áp dụng:** Kiến trúc Option C (Gateway PG source of truth + routed async delivery qua NATS, multi-VPS).
>
> **Cập nhật:** 2026-08-06

---

## 0. Quy tắc vàng (ĐỌC TRƯỚC KHI LÀM)

1. **KHÔNG tin con số `MaxTenants` trong hệ thống một cách mù quáng.** Đây là trường metadata do người nhập, code KHÔNG enforce (xem §6). Phải đối chiếu với bảng tham chiếu §3.
2. **Bandwidth là ràng buộc chặt hơn RAM** trên GCP Free Tier. e2-micro có RAM chứa 20 tenant nhưng bandwidth chỉ chứa 3–5 tenant active.
3. **Tenant mới luôn vào Instance còn capacity THẤP NHẤT** (ít tenant nhất) trong cùng region — không phải Instance có `MaxTenants` lớn nhất.
4. **Tenant "nặng" (có ảnh sản phẩm nhiều, order cao) phải vào VPS trả phí** — không cho vào Free Tier.
5. **Khi tổng tenant sắp vượt capacity toàn bộ Instance hiện có → mở VPS mới**, không cố nhồi thêm.

---

## 1. Hiểu nhanh về ShopInstance

Mỗi `ShopInstance` = 1 VPS chạy ShopERP (Blazor Server + SQLite per-tenant + NATS subscriber).

**Vai trò:**
- Chứa SQLite DB riêng cho mỗi tenant (products, orders, accounting entries, e-invoice).
- Nhận order từ Gateway qua NATS subject `vanan.cloud.order.created.{shopInstanceId}`.
- Hiển thị kitchen/POS cho owner.
- Sync order status + e-invoice kết quả ngược về Gateway PG.

**KHÔNG chứa:** Products catalog tổng, Orders source-of-truth (PG Gateway mới giữ), Tenants metadata, Users auth (Gateway PG giữ).

**Trường `MaxTenants` trong DB:**
- Default: `50` (hardcode trong `1_Shared/Domain/ShopInstance.cs`).
- Admin tự nhập khi tạo/cập nhật qua `/admin/shop-instances`.
- **CHỈ LÀ GHI CHÚ** — code không chặn gán tenant vượt quá số này (tech debt, xem §6).

---

## 2. Các ràng buộc capacity của 1 VPS

| Ràng buộc | Đặc điểm | Giới hạn thật trên Free Tier |
|---|---|---|
| **RAM** | Mỗi tenant = 1 SQLite file + 1 DbContext pool + 1 NATS subscription. Blazor Server circuit cũng tốn RAM (~50MB/circuit active). | e2-micro 1GB: ~10–20 tenant; e2-small 2GB: ~30–50 tenant |
| **CPU** | Accounting entry generation + e-invoice XML + Blazor render. Tải cao khi checkout đông. | e2-micro 0.25 vCPU: ~10 tenant hoạt động thấp |
| **Disk** | Mỗi tenant 1 SQLite file. Quán nhỏ ~5–50MB, quán lớn có lịch sử 1–5 năm có thể 100MB+. | 30GB free tier: ~100–500 tenant tùy data size |
| **Disk I/O** | SQLite có 1 writer/DB. Concurrent writes cao → cần SSD. | Standard PD: chậm; SSD: cần trả phí |
| **Bandwidth egress Internet** | KhachLink customer browse catalog, product images, API calls. | **200 MB/tháng free** — RÀNG BUỘC CHẶT NHẤT |
| **Bandwidth egress same-region GCP** | NATS Gateway↔ShopERP, HTTP forward. | **Miễn phí** nếu cùng region |
| **NATS throughput** | Mỗi order = 1 message. Không giới hạn số tenant, chỉ giới hạn msg/s. | ~1000 msg/s/VPS — không lo cho MVP |

---

## 3. Bảng tham chiếu capacity (CHUẨN — dùng cái này để quyết định)

### 3.1. Bảng theo VPS spec + bandwidth tier

| VPS spec | RAM/CPU | Bandwidth tier | **MaxTenants đề xuất** | Khi nào dùng |
|---|---|---|---|---|
| e2-micro | 1GB / 0.25 vCPU | Free (200MB/mo) | **3–5** | Test, tenant demo, tenant không có ảnh SP |
| e2-micro | 1GB / 0.25 vCPU | Paid 1GB/mo | 10–15 | Tenant nhỏ, ít order (<20/ngày) |
| e2-small | 2GB / 0.5 vCPU | Free (200MB/mo) | 8–12 | Tenant nhỏ, KHÔNG serve ảnh từ ShopERP |
| e2-small | 2GB / 0.5 vCPU | Paid 1GB/mo | 25–40 | Tenant vừa, order 20–50/ngày |
| e2-medium | 4GB / 1 vCPU | Free (200MB/mo) | 15–25 | Tenant vừa, ảnh trên Cloud Storage |
| e2-medium | 4GB / 1 vCPU | Paid 5GB/mo | 60–100 | Tenant vừa nhiều, có POS |
| e2-standard-2 | 8GB / 2 vCPU | Paid 10GB/mo | 150–250 | Đa tenant active, có kitchen display |
| e2-standard-4 | 16GB / 4 vCPU | Paid 20GB/mo | 300–500 | Multi-tenant lớn, order cao |
| Custom n1-highmem-8 | 64GB / 8 vCPU | Paid 50GB/mo | 800–1500 | Enterprise, white-label |

### 3.2. Bảng theo loại tenant (QUAN TRỌNG — chọn VPS theo tenant)

| Loại tenant | Đặc điểm | Bandwidth ước tính/tháng | VPS đề xuất |
|---|---|---|---|
| **Demo / Trial** | 1–5 order/ngày, không ảnh SP | ~10 MB | e2-micro Free |
| **Quán nhỏ không web** | POS only, 10–30 order/ngày, không ảnh | ~30 MB | e2-micro Free (cùng region) |
| **Quán nhỏ có catalog** | 20–50 order/ngày, có 5–20 ảnh SP | ~150–300 MB | e2-small Free (ảnh trên CDN) HOẶC e2-small Paid |
| **Quán vừa có QR ordering** | 50–150 order/ngày, KhachLink active | ~300–800 MB | e2-medium Paid 5GB |
| **Quán lớn / chuỗi nhỏ** | 150–500 order/ngày, POS + kitchen display | ~1–3 GB | e2-standard-2 Paid 10GB |
| **Chuỗi lớn / enterprise** | 500+ order/ngày, multi-branch | ~5–20 GB | Custom + Cloud CDN |

### 3.3. Ước tính bandwidth cho 1 tenant (để tự tính)

| Loại traffic | Lượng/ngày (50 order) | Lượng/tháng |
|---|---|---|
| Order NATS in/out (Gateway↔ShopERP, cùng region = FREE) | ~1 MB | 30 MB (free) |
| Price validation HTTP (nếu `Price_Validation_Enabled = ON`) | 150 KB | 4.5 MB |
| E-invoice sync (ShopERP → NATS → Gateway) | 500 KB | 15 MB |
| Order status sync (ShopERP → Gateway) | 400 KB | 12 MB |
| **Subtotal operational (KHÔNG ảnh)** | **~2 MB** | **~60 MB** |
| Product images serve từ ShopERP (10 view × 5 ảnh × 100KB) | 5 MB | **150 MB** |
| Product images serve từ Cloud Storage/CDN (ShopERP không tính) | 0 | 0 |
| **TỔNG 1 tenant/tháng** | | **~210 MB (có ảnh SP)** / **~60 MB (ảnh trên CDN)** |

> **Kết luận:** Tách product images sang Cloud Storage/CDN giảm bandwidth ShopERP **~3.5×** — BẮT BUỘC nếu muốn dùng Free Tier cho tenant có catalog.

---

## 4. Quy trình triển khai: Thêm tenant mới

### Bước 1 — Thu thập thông tin tenant

Hỏi chủ tenant / sales:
1. **Loại hình:** Quán cafe nhỏ / tạp hóa / spa / nhà hàng / chuỗi?
2. **Order/ngày dự kiến:** <20 / 20–50 / 50–150 / 150–500 / 500+?
3. **Có catalog web (KhachLink) không?** Có → cần bandwidth cao hơn.
4. **Có product images không?** Bao nhiêu ảnh? Kích thước trung bình?
5. **Có POS / kitchen display không?** Có → cần CPU cao hơn.
6. **Số user đồng thời cao điểm?** >10 concurrent → cần RAM cao hơn.

### Bước 2 — Phân loại tenant

Dùng §3.2 để xác định **loại tenant** và **VPS spec đề xuất**.

### Bước 3 — Kiểm tra capacity Instance hiện có

1. Vào **ShopERP Admin UI → /admin/shop-instances**.
2. Với mỗi Instance active, ghi lại:
   - `Label` (ví dụ "VPS-SG-1 e2-small")
   - `MaxTenants` (số cấu hình — chỉ tham khảo)
   - **Số tenant hiện tại** (cột "Tenants" — đếm từ `CountTenantsAsync`)
   - **Region** (phải cùng region với Gateway để NATS free)
   - **VPS spec thực tế** (ghi trong Label hoặc tài liệu triển khai nội bộ)
3. Tính **capacity còn lại** = `MaxTenants đề xuất (§3.1)` − `Số tenant hiện tại`.

> ⚠️ **KHÔNG dùng `MaxTenants` trong DB để tính capacity còn lại.** Dùng `MaxTenants đề xuất` từ §3.1 (đã tính bandwidth).

### Bước 4 — Chọn Instance cho tenant mới

**Quy tắc chọn:**
1. Lọc các Instance **cùng region với Gateway** (NATS egress free).
2. Lọc các Instance **active** (`IsActive = true`, `HealthStatus = "Healthy"`).
3. Lọc các Instance **còn capacity** theo §3.1 (không phải theo DB `MaxTenants`).
4. Lọc các Instance **spec phù hợp với loại tenant** (§3.2).
5. Trong các Instance thỏa mãn, chọn **Instance có số tenant hiện tại THẤP NHẤT** (cân bằng tải).

**Ví dụ:**
- Tenant mới: quán cafe nhỏ có catalog, 30 order/ngày, 10 ảnh SP.
- Loại: "Quán nhỏ có catalog" → cần e2-small Free (ảnh trên CDN) hoặc e2-small Paid.
- Instance hiện có:
  - `VPS-SG-1` (e2-small, Singapore, 12/40 tenant) → còn 28 slot ✅
  - `VPS-SG-2` (e2-small, Singapore, 5/40 tenant) → còn 35 slot ✅
  - `VPS-TW-1` (e2-micro, Đài Loan, 3/15 tenant) → spec thấp, KHÔNG phù hợp
- **Chọn: `VPS-SG-2`** (còn nhiều slot nhất, cùng region, spec phù hợp).

### Bước 5 — Triển khai

1. Đảm bảo product images sẽ được upload lên **Cloud Storage bucket** (không serve từ ShopERP). Xem §5.
2. Đảm bảo `Price_Validation_Enabled` được set đúng (ON nếu giá hay đổi, OFF nếu giá ổn định → giảm HTTP round-trip).
3. Onboard tenant qua Gateway API:
   ```
   POST /api/v1/onboarding/tenants
   Body: { name, businessType, shopInstanceId: <Id của Instance đã chọn>, ownerUsername, ... }
   ```
4. Owner tenant login vào ShopERP của Instance → chạy QuickSetup → seed products (vào SQLite của VPS đó).
5. Verify: tạo 1 order test từ KhachLink → kiểm tra NATS subject `vanan.cloud.order.created.{shopInstanceId}` đến đúng ShopERP.

### Bước 6 — Ghi nhận vào sổ theo dõi

Cập nhật bảng theo dõi internal (Google Sheet / Notion / file nội bộ):
| Tenant | Loại | ShopInstance | VPS spec | Region | Ngày onboard | Bandwidth ước tính/tháng | Ghi chú |
|---|---|---|---|---|---|---|---|

---

## 5. Bắt buộc: Tách product images sang Cloud Storage/CDN

**Lý do:** Serve ảnh từ ShopERP ăn bandwidth egress Internet (200MB free tier). 50 tenant × 150MB/tháng = 7.5GB → vượt free tier 37×.

**Cách triển khai:**
1. Tạo GCS bucket `vanan-product-images-{env}` (multi-region hoặc cùng region với ShopERP).
2. Cấp signed URL cho ShopERP upload (owner upload qua admin UI).
3. Serve qua **Cloud CDN** (cache ở edge, giảm egress 90%+).
4. ShopERP chỉ lưu **URL ảnh** trong SQLite, không lưu blob.
5. KhachLink load ảnh trực tiếp từ CDN URL, không qua ShopERP.

**Chi phí ước tính (50 tenant, 500 ảnh tổng, 10 view/ảnh/ngày):**
- Storage: 500 × 100KB = 50MB → $0.001/tháng (gần như free).
- CDN egress: 500 × 10 × 100KB × 30 = 15GB → ~$1.20/tháng (cache hit 90% → thực tế ~$0.12/tháng).
- → Rẻ hơn nhiều so với serve từ ShopERP (vượt free tier → $0.12/GB × 7.5GB = $0.90/tháng NHƯNG còn tốn RAM/CPU ShopERP).

---

## 6. Tech debt đã biết (ghi nhận, chưa fix)

| # | Vấn đề | Trạng thái | Ảnh hưởng |
|---|---|---|---|
| TD-1 | `MaxTenants` là hardcode default 50, không tự tính theo VPS spec | Đã ghi nhận | Admin phải tự đối chiếu §3.1, dễ nhập sai |
| TD-2 | `AssignShopInstanceAsync` KHÔNG check capacity khi gán tenant | Đã ghi nhận | Có thể gán tenant vượt `MaxTenants`, không có cảnh báo |
| TD-3 | `MaxTenants` không phân biệt theo loại tenant (nhẹ/nặng) | Đã ghi nhận | Tenant nặng vào VPS free → vượt bandwidth |
| TD-4 | Không có dashboard capacity real-time (RAM/bandwidth usage per Instance) | Đã ghi nhận | Admin phải ước tính tay |
| TD-5 | Product images chưa tách sang Cloud Storage/CDN | Đã ghi nhận | Bandwidth ShopERP cao, không thể dùng Free Tier cho tenant có catalog |

**Khi nào fix:** Khi số VPS > 3 hoặc số tenant > 50. Cần approval Tech Lead (feature mới, không phải bug).

---

## 7. Checklist triển khai nhanh (1 trang)

```
□ Thu thập thông tin tenant (loại hình, order/ngày, có catalog/ảnh không)
□ Phân loại tenant theo §3.2
□ Mở /admin/shop-instances, ghi lại capacity còn lại của từng Instance (dùng §3.1, KHÔNG dùng DB MaxTenants)
□ Chọn Instance: cùng region Gateway + active + spec phù hợp + ít tenant nhất
□ Đảm bảo product images → Cloud Storage/CDN (KHÔNG serve từ ShopERP)
□ Set Price_Validation_Enabled đúng
□ POST /api/v1/onboarding/tenants với shopInstanceId đã chọn
□ Owner login → QuickSetup → seed products
□ Tạo order test từ KhachLink → verify NATS routing
□ Ghi vào sổ theo dõi internal
```

---

## 8. FAQ triển khai

**Q1: Tenant mới là quán demo, cho vào VPS nào?**
→ e2-micro Free, cùng region Gateway. Đặt `MaxTenants` đề xuất = 5. Nếu sau này tenant nâng cấp lên có catalog → migrate sang e2-small.

**Q2: Chủ tenant muốn upload 100 ảnh sản phẩm, VPS Free Tier chứa được không?**
→ KHÔNG nếu serve từ ShopERP. BẮT BUỘC tách sang Cloud Storage/CDN (§5). Sau khi tách, VPS Free e2-small chứa được 8–12 tenant loại này.

**Q3: 1 VPS đã có 40 tenant, thêm 1 nữa được không?**
→ Phụ thuộc spec VPS + bandwidth tier. e2-small Free tier đề xuất 8–12 → 40 là ĐÃ VƯỢT. e2-medium Paid 5GB đề xuất 60–100 → 40 còn slot. Đối chiếu §3.1.

**Q4: Tenant phàn nàn chậm, làm sao kiểm tra có phải do VPS quá tải?**
→ Kiểm tra SSH vào VPS: `htop` (RAM/CPU), `df -h` (disk), `iftop` (bandwidth). Nếu RAM > 80% hoặc CPU > 70% liên tục → VPS quá tải, cần migrate tenant sang Instance khác hoặc upgrade VPS.

**Q5: Có thể migrate tenant sang Instance khác không?**
→ Có. Gọi `PUT /api/v1/tenants/{tenantId}/shop-instance` với ShopInstanceId mới. Lưu ý: data SQLite cũ KHÔNG tự sync sang VPS mới — cần export/import thủ công hoặc chờ NATS sync lại orders từ Gateway PG. Products phải owner re-seed trên VPS mới. **Chỉ migrate khi thật sự cần.**

**Q6: Gateway và ShopERP khác region thì sao?**
→ NATS egress Gateway→ShopERP sẽ tính phí ($0.08–0.12/GB). Với 50 tenant × 30MB NATS/tháng = 1.5GB → ~$0.15/tháng. Có thể chấp nhận nếu region gần (Singapore ↔ Đài Loan). KHÔNG khuyến nghị nếu region xa (Singapore ↔ US).

**Q7: Làm sao biết `MaxTenants` trong DB đã được set đúng theo §3.1?**
→ Kiểm tra `/admin/shop-instances` → cột `MaxTenants` phải khớp với §3.1 theo spec VPS. Nếu sai → PUT update. Đây là trách nhiệm của SystemAdmin khi tạo Instance mới.

---

## 9. Tham chiếu code

| File | Vai trò |
|---|---|
| `1_Shared/Domain/ShopInstance.cs` | Entity `ShopInstance` với `MaxTenants` (default 50, hardcode) |
| `3_CoreHub/Services/TenantManagementService.cs` | `AssignShopInstanceAsync` — gán tenant cho Instance (KHÔNG check capacity) |
| `3_CoreHub/Services/Onboarding/TenantOnboardingService.cs` | Onboard tenant mới + gán ShopInstance |
| `3_CoreHub/Services/IShopInstanceService.cs` | `CountTenantsAsync` — đếm tenant per Instance (chỉ hiển thị) |
| `2_Gateway/Controllers/TenantsController.cs` | API `PUT /tenants/{id}/shop-instance` |
| `2_Gateway/Controllers/ShopInstancesController.cs` | API CRUD ShopInstance (SystemAdmin only) |
| `5_WebApps/ShopERP/Components/Pages/Admin/ShopInstances.razor` | Admin UI quản lý Instance |
| `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor` | Admin UI gán tenant cho Instance |
| `docs/AI/tasks/archive/gateway_infra/gateway_router_multi_vps_master_plan.md` | Master plan Option C (8 phase) |

---

> **Bảo trì sổ tay:** Cập nhật mỗi khi (a) thêm VPS spec mới, (b) thay đổi chính sách bandwidth GCP, (c) fix tech debt TD-1 đến TD-5. Phiên bản hiện tại: 1.0 (2026-08-06).
