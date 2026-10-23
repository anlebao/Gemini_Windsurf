# Task P6 — Admin UI Rate Card (SystemAdmin)

> **Status:** ✅ ĐÃ DUYỆT — sẵn sàng implement
> **Phase:** P6 (GĐ-1) · **Gate:** RV — SystemAdmin tạo/sửa/ngưng rate card qua UI
> **Căn cứ:** D2 (admin CRUD) · P2 API sẵn · pattern `OrderFlowAdmin.razor` + `GatewayAdminApiClientBase` (đã verify production)

---

## 1. Mục tiêu

SystemAdmin quản rate card từ ShopERP UI — không cần vào DB/API tay: xem danh sách + history, tạo hạng mục, sửa định mức (scheduled), ngưng hạng mục. UI Platform components (hard rule).

## 2. Quyết định thiết kế

| # | Điểm | Đề xuất |
|---|---|---|
| D1 | Trang | `/admin/metering/rate-cards` trong ShopERP — SystemAdmin only (pattern `OrderFlowAdmin.razor` + `_isSystemAdmin` guard đã verify ở `LoyaltyReconcile.razor`) |
| D2 | Đường gọi | ShopERP → `MeteringApiClient` (P4) với **admin caller** (`sysadmin` key từ config `Metering:AdminCallerId`/`Metering:AdminKey`) — metering chỉ thấy caller `sysadmin`, không cần biết user cụ thể; actor ghi vào `AuditLog` = `sysadmin` (+ optional `ChangedBy` truyền user email qua request field nếu muốn audit cấp người — **đề xuất truyền**: thêm optional `actorOverride` trong body admin endpoints? → đơn giản hơn: `ChangedBy` = `{callerId}:{userEmail}` gộp ở ShopERP truyền lên) |
| D3 | Sửa giá | Form PATCH: chọn version hiện tại → nhập rate mới + `effectiveFrom` (default = ngay, cho phép chọn ngày tương lai — theo D4 task P2) + note |
| D4 | Hiển thị | Bảng current versions (serviceCode, unit, rate, enforcement badge, effectiveFrom, label) + toggle "Xem lịch sử" → tất cả versions. Enforcement chỉnh bằng dropdown MeterOnly/Deny |
| D5 | Gate 4 | UI mới → **BẮT BUỘC E2E spec** `6_Testing/e2e-tests/` (theo `.windsurfrules` Gate 4) — spec kiểm tra render + tạo item qua impersonate sysadmin |
| D6 | Menu | NavMenu `/admin` nhóm — mục "Phí nền tảng" (theo Sitemap convention hiện hữu — verify cách đăng ký route lúc implement) |

## 3. UI spec

```
┌ Phí nền tảng — Rate Card ──────────────────────────────┐
│ [+ Thêm hạng mục]           [x] Hiện lịch sử           │
│ ┌────────────────────────────────────────────────────┐ │
│ │ serviceCode | unit | rate | enforce | hiệu lực từ │ │
│ │ store.visit | visit| 1    | MeterOnly| 23/10      │ │
│ │ chat.message| msg  | 2    | Deny     | 23/10      │ │
│ │ ...                              [Sửa] [Ngưng]   │ │
│ └────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────┘
Modal Thêm: serviceCode · meterUnit · creditRate · enforcement · effectiveFrom · label
Modal Sửa: (readonly code) rate mới · effectiveFrom [datetime] · unit/enforcement/label · note
Confirm Ngưng: hiển thị hậu quả "hạng mục ngừng tính phí kể từ bây giờ, lịch sử giữ nguyên"
```

VanAnCard/VanAnButton/VanAModal/DataTable theo UI Platform — check component bảng hiện hữu (`VanAnDataGrid`?) lúc implement.

## 4. Edge cases đã liệt kê hết

- (a) Metering API down → trang hiển thị banner lỗi, không crash (client trả null) · (b) non-sysadmin vào route → redirect/deny (guard pattern sẵn) · (c) PATCH conflict (đã có người sửa — version không còn current) → hiển thị lỗi 409 + refresh list · (d) nhập rate âm/code trống → validate client trước khi gọi · (e) effectiveFrom quá khứ → server 400 → hiển thị message · (f) list rỗng → empty state "chưa có hạng mục" + nút tạo đầu tiên · (g) audit trail hiển thị `ChangedBy` (gộp caller:user)

## 5. Test matrix

| # | Test |
|---|---|
| T01 | bUnit: render list → bảng đúng cột (ShopERP.Tests) |
| T02 | bUnit: non-admin → guard chặn |
| T03 | bUnit: modal validate client (rate âm, code trống) |
| T04 | E2E spec `metering-ratecard.spec.ts` — impersonate sysadmin: render + tạo item → item xuất hiện (Gate 4) |
| T05 | RV L4 manual: tạo → sửa → ngưng → verify GET /v1/rate-cards qua curl |

## 6. File plan

| File | Action |
|---|---|
| `5_WebApps/ShopERP/Components/Pages/Admin/MeteringRateCards.razor` (+ .razor.css nếu cần) | BUILD |
| `5_WebApps/ShopERP/Services/MeteringApiClient.cs` | EXTEND — += `GetRateCardsAsync/CreateRateCardAsync/PatchRateCardAsync/DeactivateRateCardAsync` (admin key) |
| NavMenu + Sitemap | MODIFY |
| `ShopERP.Tests` | += T01–T03 |
| `6_Testing/e2e-tests/metering-ratecard.spec.ts` | BUILD (Gate 4) |

**Out of scope:** usage dashboard UI (để P7 cùng wallet/topup UI hoặc sau — quyết định: P6 chỉ rate card) · CallerKey management UI · topup config UI (P7).
