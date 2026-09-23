# Task Card 185-1: /loyalty/dashboard — "Lỗi kết nối"

> **Status:** VERIFIED — chờ implement (Batch 1)
> **Priority:** P1 — trang thống kê điểm thưởng hoàn toàn không dùng được
> **Created:** 2026-09-23
> **Master plan:** `docs/AI/tasks/issue_185_loyalty/master_plan.md`
> **Effort:** 1-2h

## Problem

`https://app2.khachvip.online/loyalty/dashboard` → luôn hiển thị "Lỗi kết nối. Vui lòng thử lại."

## Root cause (VERIFIED — file:line)

```
LoyaltyDashboard.razor:8    @inject HttpClient Http
LoyaltyDashboard.razor:111  await Http.GetAsync("/api/loyalty/dashboard")   // relative URI
```

- `HttpClient` resolve được (transient default client từ `AddHttpClient`) nhưng **không có `BaseAddress`** → `GetAsync` với relative URI throw `InvalidOperationException` → catch → `_errorMessage = "Lỗi kết nối. Vui lòng thử lại."` (line 124).
- Đây là page DUY NHẤT inject raw `HttpClient` — mọi page khác dùng `IHttpClientFactory` + named client.
- `CookieForwarding` client cũng không set BaseAddress (`Program.cs:490-502`) — cùng footgun đã từng xảy ra: `Pages/Impersonate.cshtml.cs:21` ghi chú "Replaces the broken HttpClient-based flow (AdminController + CookieForwarding)".
- Backend `GET /api/loyalty/dashboard` (`ShopERP/Controllers/LoyaltyController.cs:98-167`) hoạt động bình thường — endpoint không phải vấn đề.

## Solution

### Option A (đề xuất) — bỏ HTTP hop, gọi trực tiếp
Blazor Server chạy in-process — không cần gọi API của chính mình:
- Inject `IVanAnDbContext` (hoặc `ShopERPDbContext`), `ITenantProvider`, `IShopFeatureSettingsService`, `IOptions<LoyaltyPointsConfig>` vào component.
- Port 4 metric queries từ `LoyaltyController.GetDashboard()` (lines 103-160) vào component — hoặc extract sang 1 service dùng chung cho cả controller + page (khuyến nghị, tránh duplicate query logic).
- Giữ nguyên `[Authorize]` + layout; TenantProvider resolve tenant từ auth cookie như controller.

### Option B — giữ HTTP, fix BaseAddress + cookie
- Dùng `IHttpClientFactory.CreateClient("CookieForwarding")` (forward auth cookie — endpoint cần `ResolveCustomerTenant`) + build absolute URI từ `NavigationManager.BaseUri`.
- Nhược điểm: thêm 1 HTTP hop tự gọi chính mình, vẫn giữ pattern đã từng hỏng.

### Phase C — Tests + E2E (Gate 4 — UI change)
- [ ] C1: E2E spec `6_Testing/e2e-tests/` — owner login app2 → `/loyalty/dashboard` render 4 stat cards, 0 page errors.
- [ ] C2: nếu extract service → unit test 4 metrics (có thể reuse pattern test hiện có).

## Acceptance
- [ ] `/loyalty/dashboard` trên app2 hiển thị stats thật (không còn "Lỗi kết nối")
- [ ] E2E spec pass
