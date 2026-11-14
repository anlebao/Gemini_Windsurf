# Task W2-5 — FEATURE CONTEXT SERVICE + RENDER RULE + VISIBILITY (FR-V6/V7/V1/V2)

> **Status:** PENDING · **Wave:** W2
> **Phụ thuộc:** không. Unblocks: W3-1 (per-card AI/voice).

## Mục tiêu giá trị

Chuẩn hoá cách các feature cần-tenant-context (AI chat, voice call, voice note) quyết định hiển thị — nền tảng cho W3; admin có bảng nhìn thấy toggle nào đang OFF ở đâu.

## Scope

1. **FR-V6 — `FeatureContextService`** (`5_WebApps/KhachLink/Services/`):
   - Resolve `EffectiveTenantId` theo chain: explicit (param truyền vào từ page/card) → instance `OwnerTenantId` (từ `KhachLinkInstanceHttpService` cache) → `_lastTenantId` (`LastInteractionService`).
   - API: `Task<Guid?> GetEffectiveTenantIdAsync(Guid? explicitTenantId)` + `HasTenantContext`.
   - Refactor điểm hiện dùng flag + tenant: `ChatPanel`, `Store.razor` chat, Home voice mic — đọc qua service (giữ hành vi hiện tại, chỉ chuẩn hoá).
2. **FR-V7 — Render rule:** helper/extension `ShouldRenderFeature(toggle, hasContext)` — áp cho các entry mới; **không** render nút disabled/dead. Áp dụng lên các chỗ hiện có nếu phát hiện render khi không context (vd ChatPanel TenantId empty).
3. **FR-V1 — Feature Visibility Matrix** (ShopERP admin): trang mới `/admin/feature-visibility` (hoặc mở rộng `KhachLinkInstances`) — bảng: instance → profile → NavFlags hiệu lực + tenant → flagship toggles (AIChat/VoiceCall/VoiceNote) ON/OFF. Read-only trước; source: PG `KhachLinkInstances` + `ShopFeatureSettings` sync.
4. **FR-V2 — Member toggles cho HTX:** khi W2-1 có member list → script/admin batch bật `AIChat_Enabled` + `Voice_Call_Enabled` + `Voice_Note_Enabled` cho member tenants của HTX (per-tenant opt-in mặc định ON trong hệ HTX; các tenant ngoài giữ opt-in). Có thể là nút "Apply HTX defaults" trong admin page của W2-1.

## Files

- `5_WebApps/KhachLink/Services/FeatureContextService.cs` (mới) + DI `Program.cs`
- `5_WebApps/KhachLink/Components/ChatPanel.razor`, `Pages/Home.razor`, `Pages/Store.razor` (refactor nhẹ)
- `5_WebApps/ShopERP/Components/Pages/Admin/` (matrix page)
- Tests: `FeatureContextService` chain order unit tests.

## Tests

- Unit: chain priority explicit > instance > lastInteraction > null.
- guard-check + build + Core.Tests.

## RV checklist

- [ ] Admin mở matrix → thấy toggles của từng instance/tenant.
- [ ] Member tenant HTX: flagship toggles ON (verify qua matrix hoặc API).
- [ ] Chat/voice entry hiện/ẩn đúng theo context (không nút chết trên trang không-tenant).
