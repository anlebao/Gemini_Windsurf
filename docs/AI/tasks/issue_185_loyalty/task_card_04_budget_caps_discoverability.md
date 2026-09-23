# Task Card 185-4: "Không tìm thấy chỗ thiết lập giới hạn ngân sách điểm thưởng cho từng tenant"

> **Status:** DECISION APPROVED 2026-09-23 — **Option C: read-only**. Owner xem caps + counters (không sửa) trong `/settings/shop-features`; chỉ SystemAdmin set tại `/admin/loyalty-config`.
> **Priority:** P2
> **Created:** 2026-09-23
> **Master plan:** `docs/AI/tasks/issue_185_loyalty/master_plan.md`
> **Effort:** 1-3h tuỳ hướng

## Problem

User không tìm được chỗ set giới hạn ngân sách điểm thưởng per-tenant.

## Verify (VERIFIED — file:line)

**Feature đã tồn tại + deployed + RV pass (Batch 3, `2c12852b`, 2026-09-21):**
- `/admin/loyalty-config` → card "Ghi đè theo tenant" → **chọn tenant** → section "Ngân sách điểm (Budget Caps)" (`LoyaltyConfigAdmin.razor:151-206`):
  - Ngân sách tháng (điểm) · Ngân sách ngày (điểm) · Giới hạn mỗi khách/ngày · Trần mỗi đơn (% giá trị đơn)
  - Counters "Đã dùng tháng này / hôm nay" + nút Reset daily/monthly
  - Backend: `PUT /api/platform/loyalty/tenant/{id}/config` + `POST .../reset-counters`

**Tại sao user không thấy:**
1. Fields ẩn hoàn toàn cho tới khi chọn tenant trong dropdown (không có hướng dẫn "budget nằm ở đây").
2. Page `[Authorize(Policy = "SystemAdmin")]` (`LoyaltyConfigAdmin.razor:19`) — Owner tenant **không bao giờ vào được**; menu chỉ có ở admin menu (`ShopErpMenuService.cs:244` "Loyalty Alliance Config").
3. Trang owner-facing `/settings/shop-features` có section "Công thức điểm thưởng" (rate/min/max) nhưng **không có budget**.

## Solution — APPROVED: Option C (read-only)

- [ ] A1: Owner xem **read-only** caps + counters trong `/settings/shop-features` (section "Công thức điểm thưởng"): Ngân sách tháng/ngày, giới hạn khách/ngày, trần đơn %, "Đã dùng tháng này/hôm nay" — kèm note "Chỉ quản trị hệ thống (SystemAdmin) thay đổi tại /admin/loyalty-config".
- [ ] A2: Data path: `GET /api/platform/loyalty/tenant/{id}/config` hiện SystemAdmin-gated → cần owner-scoped read: thêm `GET /api/loyalty/tenant-budget` (tenant từ auth context — ResolveCustomerTenant/cookie) trên **ShopERP** forward hoặc đọc qua `LoyaltyConfigApiClient` với tenantId từ `TenantProvider` (kiểm tra authz: endpoint hiện tại policy SystemAdmin → tạo GET riêng cho owner read).
- [ ] A3: Rename menu admin "Loyalty Alliance Config" → "Loyalty Alliance + Ngân sách" (`ShopErpMenuService.cs:244`) cho dễ tìm.
- ~~Option A/B~~ — rejected (owner không tự set budget).

## Acceptance
- [ ] User (theo role được duyệt) tìm được budget caps không cần hỏi
