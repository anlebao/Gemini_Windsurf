# HƯỚNG DẪN SỬ DỤNG CỔNG HỢP TÁC XÃ (HOPTACXA PORTAL)

**Phiên bản:** 1.0 — **Ngày:** 2026-11-14
**Đối tượng:** ① Thành viên HTX · ② Nhân sự IT/cấu hình hệ thống · ③ Nhân sự điều hành HTX
**URL portal:** https://hoptacxa.timlathay.com

---

## PHẦN A — DÀNH CHO THÀNH VIÊN HỢP TÁC XÃ

### A.1. Portal là gì?

Cổng thông tin chung của hệ sinh thái hợp tác xã Vạn An. Portal **không phải cửa hàng** — nó là "bảng tin + danh bạ" kết nối:

- Danh sách các **hợp tác xã** đang hoạt động trên hệ thống
- **Cửa hàng thành viên** của từng HTX (mỗi cửa hàng có gian hàng riêng, mua bán/tích điểm tại đó)

### A.2. Giao diện portal gồm những gì?

| Khu vực | Nội dung | Thao tác |
|---|---|---|
| Hero | Tên portal + giới thiệu | Nút "Khám phá hợp tác xã" (cuộn xuống danh sách), "Tìm cửa hàng" (→ trang tìm cửa hàng) |
| Module cards | 4 ô: **Cơm từ thiện** · **Cộng tác viên** · **Mua chung** (Sắp ra mắt) · **Sàn việc** (Sắp ra mắt) | Bấm để đi tới module tương ứng |
| "Các hợp tác xã" | Card mỗi HTX: logo, tên, số cửa hàng thành viên, danh sách tên cửa hàng (chips) | Xem tổng quan |
| "Cửa hàng thành viên" | Card mỗi cửa hàng: logo, tên, tên miền gian hàng | **Bấm để mở gian hàng** (mua sắm, tích điểm tại cửa hàng đó) |

### A.3. Cách mua sắm với tư cách thành viên

1. Vào https://hoptacxa.timlathay.com
2. Tìm HTX của bạn trong mục "Các hợp tác xã" → xem danh sách cửa hàng thành viên
3. Bấm vào card cửa hàng → hệ thống mở **gian hàng riêng** của cửa hàng đó (domain `*.khachvip.online` hoặc domain riêng)
4. Tại gian hàng: xem sản phẩm → thêm giỏ → đặt hàng → tích điểm theo chương trình của cửa hàng/HTX

> Lưu ý: portal chỉ là danh bạ — **đơn hàng, điểm thưởng, ví** đều nằm tại gian hàng của từng cửa hàng thành viên, không nằm trên portal.

### A.4. Câu hỏi thường gặp

- **"Sao không thấy HTX/cửa hàng của tôi?"** → Kiểm tra: HTX đã được đăng ký trên hệ thống chưa; cửa hàng đã được kết nạp làm thành viên (status = Active) chưa; gian hàng đã có tên miền chưa. Liên hệ ban điều hành HTX hoặc IT.
- **"Mua chung / Sàn việc chưa có?"** → Đúng, 2 module này đang ở trạng thái "Sắp ra mắt".
- **"Cộng tác viên là gì?"** → Chương trình bán hàng/giới thiệu của hệ thống — bấm card "Cộng tác viên" để xem trang cộng đồng.

---

## PHẦN B — DÀNH CHO NHÂN SỰ IT / CẤU HÌNH HỆ THỐNG

### B.1. Kiến trúc dữ liệu (đã triển khai)

Portal **tái sử dụng Membership Infrastructure** — KHÔNG có entity "Cooperative" riêng:

| Khái niệm portal | Bảng/entity PG | Điều kiện hiển thị |
|---|---|---|
| Hợp tác xã (HTX) | `Tenants` + `HtxProfiles` | Tenant `Status=Active` + có `HtxProfile` |
| Cửa hàng thành viên | `Members` (`MemberTenantId`) | `Status=Active` + tenant thành viên `Status=Active` |
| Tên miền gian hàng | `KhachLinkInstances` (`OwnerTenantId` → `CustomDomain`) | Instance `IsActive`, profile ≠ Directory |

Public API (anonymous, read-only, rate-limit 60 req/min/IP, nginx cache 5 phút):

```
GET /api/cooperatives                          → list HTX + member count + domain
GET /api/cooperatives/{htxTenantId}/members    → member shops của 1 HTX
GET /api/cooperatives/by-domain/{domain}       → resolve portal → scope HTX
```

### B.2. Hạ tầng đã dựng sẵn (không cần làm lại)

- DNS wildcard + SSL cert đã cover `hoptacxa.timlathay.com` (certbot SAN, hết hạn 2027-02-12)
- `KhachLinkInstances` row: profile `Hoptacxa`, `OwnerTenantId=null` (portal đa-HTX), NavFlags browse-only
- nginx `dir_api_cache` cho `/api/cooperatives` (200→5m, 404→1m) trên cả 2 server block
- E2E: `6_Testing/e2e-tests/hoptacxa-portal.spec.ts`

### B.3. Quy trình đưa 1 HTX lên portal (checklist)

1. **Tenant HTX tồn tại & Active** — tenant phải verified/Active (tenant Pending/Suspended sẽ bị ẩn khỏi portal).
2. **Đánh dấu HTX** — SystemAdmin gọi:
   ```
   POST /api/admin/membership/htx-profile
   { "htxTenantId": "<guid>", "charterVersion": "v1.0", "termsVersion": "v1.0", "charterUrl": "<url điều lệ, optional>" }
   ```
   (UI tương đương: trang admin membership — luồng "tenant → Hợp tác xã".)
   → Side-effect: tenant được set `TenantType=HTX` + `AccountingStandard=TT71_2024`.
3. **Kết nạp cửa hàng thành viên** — tenant thành viên phải Active trước, sau đó:
   ```
   POST /api/admin/membership/members
   { "htxTenantId": "<guid HTX>", "memberTenantId": "<guid cửa hàng>",
     "membershipType": "OfficialMember|LinkedCapitalMember|LinkedNonCapitalMember",
     "capitalContributionAmount": <số, tuỳ loại> }
   ```
4. **Gian hàng có domain** — cửa hàng cần `KhachLinkInstances` active non-Directory để card link đúng storefront; chưa có domain → card fallback `/stores`.
5. **Verify** — `GET https://hoptacxa.timlathay.com/api/cooperatives/by-domain/hoptacxa.timlathay.com` (qua nginx) hoặc `GET /api/cooperatives` trên Gateway → thấy HTX + `memberCount` đúng.

### B.4. Ràng buộc cần nhớ khi cấu hình

- **Không tạo entity Cooperative song song** — mọi thứ đi qua `HtxProfiles` + `Members`; sửa sai chỗ = 2 nguồn truth.
- HTX **không thể là thành viên của chính mình**; mỗi cặp (HTX, member tenant) chỉ 1 record — trùng bị reject.
- Chỉ **tenant-member** (`MemberTenantId != null`) mới tính là "cửa hàng thành viên" — member là cá nhân (`MemberCustomerId`) không hiện trên portal.
- Revert HTX → tenant thường: `POST /api/admin/membership/htx-profile/revert` — **bị chặn khi còn thành viên Active**.
- Cache nginx 5 phút → sau khi thêm member, portal cập nhật chậm nhất sau 5 phút (hoặc purge cache).

---

## PHẦN C — DÀNH CHO NHÂN SỰ ĐIỀU HÀNH HỢP TÁC XÃ

### C.1. Vòng đời thành viên (ai làm gì)

| Trạng thái | Ý nghĩa | Ai chuyển |
|---|---|---|
| `Active` | Thành viên chính thức — **hiển thị trên portal** | Duyệt đơn xin gia nhập (HTX officer) hoặc SystemAdmin add trực tiếp |
| `Suspended` | Tạm đình chỉ — **ẩn khỏi portal** | HTX officer (có lý do) |
| `Resigned` | Tự nguyện ra khỏi | Thành viên/HTX officer |
| `Terminated` | Khai trừ — **ẩn khỏi portal** | HTX officer (có lý do) |

### C.2. Quy trình kết nạp cửa hàng mới (vận hành)

1. Cửa hàng đăng ký tenant trên hệ thống → chờ verify Active (điều kiện bắt buộc).
2. Nhờ SystemAdmin add vào HTX (hoặc HTX officer duyệt đơn `MembershipApplication` nếu đi luồng đơn xin gia nhập).
3. Kiểm tra portal sau ≤5 phút: cửa hàng xuất hiện trong "Cửa hàng thành viên" + chip trong card HTX.
4. Nếu cửa hàng **biến mất khỏi portal**: kiểm tra `Member.Status` (Suspended/Terminated → ẩn) và `Tenant.Status` (Suspended/Deleted → ẩn).

### C.3. Phân quyền thao tác

| Việc | Ai được làm |
|---|---|
| Đánh dấu tenant → HTX / revert | **SystemAdmin** (`/api/admin/membership/*`) |
| Add member tenant trực tiếp | **SystemAdmin** |
| Duyệt/từ chối đơn xin gia nhập, suspend/reactivate/resign/terminate member | **HTX officer** (Owner của tenant HTX — `/api/membership/*`) |
| Xem portal | Mọi người (anonymous) |

### C.4. Dữ liệu portal hiển thị từ đâu — kiểm tra nhanh

- Số "cửa hàng thành viên" trên card HTX = đếm `Members` Active có `MemberTenantId` (không tính member cá nhân).
- Tên/logo/slug cửa hàng = `Tenants.Settings` của tenant thành viên.
- Link gian hàng = `KhachLinkInstances.CustomDomain` của tenant đó.
- Mọi truy vấn đều `IgnoreQueryFilters` phía server → **tenant bị soft-delete/status khác Active sẽ không lọt vào portal**.

### C.5. Sự cố thường gặp & cách xử lý

| Triệu chứng | Nguyên nhân khả dĩ | Xử lý |
|---|---|---|
| Portal trắng, "Chưa có hợp tác xã" | Chưa có HTX nào Active | Seed HTX theo B.3 |
| HTX có nhưng memberCount = 0 | Member chưa Active / là customer-member / tenant chưa Active | Kiểm tra `Members.Status` + `Tenants.Status` |
| Card cửa hàng không link được | Tenant chưa có KhachLinkInstance active | Provision instance/domain cho tenant |
| Dữ liệu cũ sau khi sửa | nginx cache 5m | Chờ hoặc purge `dir_api_cache` |
| API trả 404 by-domain | Domain chưa đăng ký `KhachLinkInstances` | Kiểm tra instance row + CustomDomain |

---

## PHỤ LỤC — Tham chiếu kỹ thuật

- SRS: `docs/specs/SRS_EntryPoints_Retention_Upgrade_v1.0.md`
- Task cards: `docs/AI/tasks/entry_points_retention/task_w2_1_cooperative_model.md`, `task_w2_2_hoptacxa_landing.md`
- Code: `2_Gateway/Controllers/CooperativesController.cs` · `5_WebApps/KhachLink/Components/HoptacxaPortal.razor` · `5_WebApps/KhachLink/Services/Http/CooperativeHttpService.cs`
- Tests: `6_Tests/VanAn.Core.Tests/Cooperatives/CooperativesControllerTests.cs` · `6_Testing/e2e-tests/hoptacxa-portal.spec.ts`
- Membership docs: `docs/HopTacXa/` (BOM HTX-in-a-Box)
