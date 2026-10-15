# MASTER PLAN — Fix chế độ kế toán HTX trên UI ShopERP + Chuyển ngược HTX → Công ty/HKD

> **Date:** 2026-10-15 · **Status:** PLAN — chờ user duyệt (workflow newfeaturebuild Step 4)
> **Objective:** (1) Tenant HTX phải hiển thị báo cáo TT 71 trên ShopERP UI. (2) Thêm nút chuyển ngược HTX → Công ty/HKD.
> **Root cause (đã verify production 2026-10-15):** `MarkAsHtx()` chỉ ghi PG (Gateway). ShopERP UI đọc Tenant.Type từ **SQLite mirror** (`VasFeatureFlagService` → `IVanAnDbContext` = `ShopERPDbContext`) → mirror vẫn HKD → `AccountingStandardResolver.Resolve(HKD)` = TT133 → báo cáo HKD, không phải TT71.
> **Bằng chứng:** PG: `Vạn An Cafe (HKD Group 1)` Type=5/Standard=3 (TT71) · SQLite shop-a: Type=1 (HKD)/NULL. Tương tự `Vạn An Trading Co.` (PG HTX, SQLite SME).

## Quyết định user (đã chốt 2026-10-15)
- Q1 Fix báo cáo HTX: **Cả 2 hướng** — (a) UI đọc PG như OrderService P6b + (b) sync Type PG → SQLite khi MarkAsHtx.
- Q2 Chuyển ngược HTX: **Có** — thêm chức năng (Domain + API + UI).
- Q3 Xử lý Members khi revert: **Chặn khi có Member** — HTX còn thành viên thì không cho revert (bảo toàn dữ liệu; phải tự xóa tay trước). HtxProfile bị xóa khi revert.
- Q4 Đợt push: **1 đợt gộp** — P1-P6 + tests → 1 push → CD → RV.

## Phạm vi (KHÔNG mở rộng)
- Không đụng `AccountingEntry` (immutable) — bút toán cũ 512/612/611 giữ nguyên lịch sử.
- Không đổi enum `TenantType`/`AccountingStandard` (append-only).
- Không migration DB mới (Tenant.Type/AccountingStandard đã có cột; HtxProfile/Members đã có).

## Phases

### P0 — Data fix production (sau deploy code)
- Update SQLite mirror (shop-a + mọi shop VPS) cho 2 tenant HTX khớp PG: Type=5, AccountingStandard=3.
- Idempotent — chạy được lặp lại.

### P1 — Domain (Gate 5 — user đã duyệt Domain change)
- `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs`: thêm `RevertTenantType(TenantType targetType, AccountingStandard targetStandard)`:
  - Guard: `Type == HTX` bắt buộc; inactive → throw; audit (`UpdateAudit`).
  - Set `Type` + `AccountingStandard` (HKD → standard null; Công ty → TT133/TT99).
- `MarkAsHtx` giữ nguyên (1 chiều vào HTX). `SetTenantType` giữ nguyên (one-way guard).

### P2 — CoreHub service
- `IHtxProfileService` + `HtxProfileService`:
  - Inject optional `INatsEventPublisher?` (fail-safe như `LoyaltyConfigController.PublishGlobalConfigChangedAsync`).
  - `GetOrCreateAsync`: sau `MarkAsHtx` → publish `vanan.cloud.tenant.type.changed` { TenantId, Type, AccountingStandard }.
  - Thêm `RevertAsync(htxTenantId, targetType, targetStandard, ct)`: load profile (throw nếu không có) → **guard: nếu HTX còn Member active → throw (Q3)** → xóa HtxProfile → `tenant.RevertTenantType(...)` → SaveChanges → publish event.
- DTO revert nếu cần (MembershipDtos).

### P3 — Gateway API
- `MembershipAdminController`: `POST /api/admin/membership/htx-profile/revert` (SystemAdmin only):
  - Body { HtxTenantId, TargetType } — TargetType: `HKD` | `Enterprise_SME` | `Enterprise_Large`.
  - Map target → standard (HKD → null; SME → TT133; Large → TT99) → `RevertAsync`.
  - Log + trả DTO.

### P4 — ShopERP: UI đọc PG + sync SQLite mirror
- `VasFeatureFlagService`: ctor thêm optional `VanAnDbContext? pgDbContext = null` (pattern `OrderService` P6b). `GetTenantTypeAsync`/`CanAccessVasReportsAsync`/`IsReadOnlyAsync` ưu tiên đọc PG, fallback `IVanAnDbContext` (Gateway/tests).
  → 6 trang báo cáo + FinancialReports hub + menu + RevenueEntry/ExpenseEntry resolve đúng TT71 ngay lập tức.
- `TenantSyncSubscriber` (ShopERP): subscribe `vanan.cloud.tenant.type.changed` → cập nhật SQLite mirror `Type` + `AccountingStandard` (dùng domain method: HTX → `MarkAsHtx()`; revert → `RevertTenantType()`; EF-tracked sau khi load).

### P5 — UI ShopERP
- `TenantManagement.razor` (`/admin/tenants`): tenant `Type == HTX` → nút "Chuyển về Công ty/HKD" + modal (chọn loại mục tiêu: Hộ kinh doanh / Công ty, + cảnh báo xóa HtxProfile + Members). Nút "Chuyển thành HTX" ẩn khi đã HTX (đã có guard).
- `MembershipAdminApiClient`: `RevertHtxProfileAsync(tenantId, targetType)`.

### P6 — Tests (TDD)
- Domain: `RevertTenantType` guard (không HTX → throw; inactive → throw; audit).
- `HtxProfileServiceTests`: revert xóa profile+members+revert tenant+publish; GetOrCreate publish event.
- `TenantSyncSubscriberTests`: event type-changed → SQLite mirror cập nhật.
- `VasFeatureFlagServiceTests`: ưu tiên PG.
- Controller: endpoint revert (auth + mapping target).
- ShopERP bUnit: TenantManagement hiện nút revert cho HTX.

### P7 — Validation + Deploy + RV
- `guard-check.ps1` + `dotnet build VanAn.sln` + Core.Tests + ShopERP.Tests ALL PASS.
- **1 đợt push gộp (Q4)**: P1-P6 + tests → 1 push → CD Multi-VPS → RV.
- FAST PUSH → CD Multi-VPS → RV:
  - L1: markers production (RevertTenantType trong CoreHub.dll · revert endpoint · TenantSyncSubscriber subject).
  - L2: health 200 + route `/api/admin/membership/htx-profile/revert`.
  - L3/L4: UI kế toán tenant cafe → B01-HTX/B02-HTX + menu "Báo Cáo Tài Chính" · revert tenant test → Type HKD · SQLite mirror khớp PG.

## Risks
- Revert xóa HtxProfile → mất dữ liệu hồ sơ HTX (điều lệ/charter). Members KHÔNG bị xóa — revert bị chặn nếu còn member (Q3, bảo toàn dữ liệu).
- Order cũ `IsInternalToHtx=true` giữ nguyên (lịch sử bất biến); order mới sau revert định khoản DN (511/611/632) — tự động qua `OrderService.IsHtxSellingTenantAsync` đọc PG.
- NATS publish fail-safe — miss event chỉ trễ mirror (subscriber + backfill P0 đảm bảo nhất quán).

## Files dự kiến (~12)
| # | File | Change |
|---|---|---|
| 1 | `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs` | + `RevertTenantType` |
| 2 | `3_CoreHub/Services/Membership/IHtxProfileService.cs` | + `RevertAsync` |
| 3 | `3_CoreHub/Services/Membership/HtxProfileService.cs` | + publish + revert |
| 4 | `3_CoreHub/Services/Membership/Dtos/MembershipDtos.cs` | + revert DTO |
| 5 | `2_Gateway/Controllers/MembershipAdminController.cs` | + revert endpoint |
| 6 | `3_CoreHub/Services/VasFeatureFlagService.cs` | ưu tiên PG |
| 7 | `5_WebApps/ShopERP/Services/TenantSyncSubscriber.cs` | + type-changed subject |
| 8 | `5_WebApps/ShopERP/Services/MembershipAdminApiClient.cs` | + revert call |
| 9 | `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor` | + nút + modal revert |
| 10-12 | Tests (Core: Domain/Service/Subscriber; ShopERP: bUnit) | + mới/sửa |
