# TASK CARD: Phase 6 — UI KhachLink (Pending profile + Claim form)

> **Master plan:** `docs/AI/plans/crawl-onboarding-master-plan.md`
> **Research snapshot:** `docs/AI/plans/crawl-onboarding-research.md`
> **Depends on:** Phase 4 complete (Gateway endpoints exist)
> **Status:** PENDING

## 1. OBJECTIVE

KhachLink hiển thị Pending tenant profile (masked phone, read-only, no commerce) + Claim form (owner submits GPKD upload).

## 2. GATES & HARD STOPS

- **🔴 KhachLink HTTP-only:** MUST NOT inject `IVanAnDbContext` — gọi Gateway HTTP only
- **🔴 UI Platform compliance:** Dùng VanAnButton/VanAnCard/VanAnAlert/VanAForm/VanATable, KHÔNG custom HTML/CSS

## 3. PRE-CONDITIONS

- [ ] Phase 4 done — Gateway `GetBySlug` returns `IsPending` + `ClaimUrl` + masked `Phone`
- [ ] **Open O1** resolved: check `5_WebApps/KhachLink/Services/Http/` for existing image upload service (Cloudinary?)
- [ ] Re-verify `Models/ShopDto.cs`, `Pages/Store.razor`, `TenantProfileHttpService.cs` line refs

## 4. FILES TO MODIFY / CREATE

### MODIFY
| Path | Change |
|---|---|
| `5_WebApps/KhachLink/Models/ShopDto.cs` | Add `bool IsPending { get; set; }` + `string? ClaimUrl { get; set; }`. **NO `MaskedPhone` field** (correction H6 — Gateway already masks `Phone`). |
| `5_WebApps/KhachLink/Pages/Store.razor` | After load `_store`: if `IsPending == true` → hide cart, products, order section, AI chat. Show yellow banner (VanAnAlert) "⚠️ Thông tin chưa xác thực — Doanh nghiệp chưa được chủ sở hữu xác nhận". **HIDE SĐT section entirely** (M3 — `Phone` is null from Gateway, do not render phone section, do not show masked phone). Show "Đây là doanh nghiệp của bạn?" button (VanAnButton) → navigate to `/store/{Slug}/claim`. Hide checkout CTAs. Else (Active): current behavior unchanged. |

### CREATE
| Path | Role |
|---|---|
| `5_WebApps/KhachLink/Pages/Claim.razor` | `@page "/store/{Slug}/claim"`. Form (VanAForm): ClaimantName (required), ClaimantPhone (required), ClaimantEmail (optional), TaxCodeSubmitted (pre-filled from `_store.TaxCode`, editable), GPKD image upload. Submit → `POST /api/v1/tenants/{tenantId}/claims` via `ClaimHttpService`. On success: VanAnAlert "Cảm ơn! Yêu cầu xác nhận đã gửi. Chúng tôi sẽ liên hệ trong 3-5 ngày làm việc." Handle 429 (rate limit) with friendly error. |
| `5_WebApps/KhachLink/Services/Http/ClaimHttpService.cs` | `SubmitClaimAsync(Guid tenantId, SubmitClaimRequest req)` → POST to Gateway. Uses `HttpClientFactory` "gateway" client (same pattern as `TenantProfileHttpService`). |
| `5_WebApps/KhachLink/Services/Http/ImageUploadService.cs` (or reuse existing) | Upload GPKD image → return URL. Reuse Cloudinary if exists (O1), else build minimal upload service. |

## 5. ACCEPTANCE CRITERIA

- [ ] `dotnet build 5_WebApps/KhachLink/VanAn.KhachLink.csproj` — 0 errors
- [ ] `/store/pending-{taxCode}-{random4}` shows Pending banner + Claim button
- [ ] **SĐT section HIDDEN on Pending profile** (M3 — `_store.Phone` is null, render no phone section)
- [ ] No commerce UI (cart, products, checkout) on Pending profile
- [ ] `/store/{Slug}/claim` form submits to Gateway
- [ ] 429 handled with friendly error
- [ ] NO `IVanAnDbContext` injection in KhachLink
- [ ] All UI uses VanAn* components (UI Platform) — no custom HTML/CSS

## 6. VERIFICATION

```powershell
dotnet build 5_WebApps\KhachLink\VanAn.KhachLink.csproj
```
Manual: navigate `/store/pending-0106463914-a3f2` → Pending banner + Claim button → click → form → submit (mock API). No Playwright until Phase 8 (Gate 3).

## 7. CORRECTIONS APPLIED

| # | Correction |
|---|---|
| H6 | `ShopDto` has `IsPending` + `ClaimUrl` only — NO `MaskedPhone` field |
| M3 | **Pending profile HIDE SĐT section entirely** (Phone=null from Gateway, không mask, không render phone section) |
