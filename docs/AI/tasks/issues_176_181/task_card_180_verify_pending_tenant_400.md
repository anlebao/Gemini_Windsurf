# Task Card #180: Verify Pending Tenant → 400 Bad Request

> **Status:** ✅ IMPLEMENTED 2026-09-19 (chờ deploy + RV)
> **Priority:** P0 — chặn toàn bộ crawl-to-onboard pipeline
> **Created:** 2026-09-19
> **Master plan:** `docs/AI/tasks/issues_176_181/master_plan.md`
> **Effort:** 3-4h

## Problem

`https://app2.khachvip.online/admin/tenants` → chọn tenant Pending, nhập thông tin đủ, bấm Verify → `Lỗi: Response status code does not indicate success: 400 (Bad Request)`.

## Root cause (VERIFIED — file:line)

Chain:
```
TenantOnboardingService.VerifyAsync (TenantOnboardingService.cs:231-234)
  publishedSlug = req.Slug ?? Slugify(tenant.Name)
  tenant.UpdateSlug(publishedSlug)                        // Tenant.cs:275-295
      regex ^[a-z0-9]+(?:-[a-z0-9]+)*$                    // Tenant.cs:285 → ArgumentException nếu có dấu/không hợp lệ
Slugify (TenantOnboardingService.cs:295-303)
  Regex.Replace(slug, @"[^\w\s-]", "")  → .NET \w = Unicode letters → GIỮ NGUYÊN dấu tiếng Việt
  "Quán Cà Phê" → "quán-cà-phê" → UpdateSlug THROW ArgumentException
→ Controller chỉ catch KeyNotFoundException(→404) / InvalidOperationException(→409)  (TenantPendingController.cs:69-76)
→ ArgumentException rơi vào UnifiedErrorHandler (Gateway/Middleware/UnifiedErrorHandler.cs:82-84) → **400 Bad Request**
```

Mọi tenant crawl có tên tiếng Việt có dấu đều dính → verify không bao giờ thành công. Secondary: `SendAndReadAsync` (`GatewayAdminApiClientBase.cs:97-102`) không đọc body lỗi → user chỉ thấy "400 (Bad Request)" không rõ lý do.

## Solution

### Phase A — Slug chuẩn hóa (fix gốc)
- [ ] A1: `TenantOnboardingService.Slugify` — NFD normalize (`.Normalize(NormalizationForm.FormD)`), bỏ combining marks, rồi mới regex `[^\w\s-]`; fallback `tenant-{guid8}` nếu rỗng. Test: `"Quán Cà Phê"` → `"quan-ca-phe"`, `"Ốc Quê"` → `"oc-que"`.
- [ ] A2: Slug từ user input (`req.Slug`) cũng qua chuẩn hóa tương tự (hoặc validate + trả lỗi rõ ràng client-side).

### Phase B — Surface lỗi đúng cách
- [ ] B1: `TenantPendingController.DirectVerify` — catch `ArgumentException` → `BadRequest(new { error = ex.Message })` (message rõ ràng, không qua middleware generic).
- [ ] B2: `GatewayAdminApiClientBase.SendAndReadAsync` — khi non-success, đọc body + throw `InvalidOperationException(body.error)` thay vì `HttpRequestException` chung.

### Phase C — Tests
- [ ] C1: Unit test Slugify (tiếng Việt có dấu, tên chỉ toàn dấu, slug rỗng, >100 chars).
- [ ] C2: Integration test DirectVerify tenant tên "Quán Cà Phê Test" → 200 + slug đúng.

## Acceptance
- [ ] Verify Pending tenant tên tiếng Việt thành công trên app2 (RV) · lỗi thật hiển thị cho user nếu có
