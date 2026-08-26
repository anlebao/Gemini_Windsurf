# TASK CARD: Phase 7 — UI ShopERP Admin (Pending list, Claims queue, Duplicates, Crawl trigger)

> **Master plan:** `docs/AI/plans/crawl-onboarding-master-plan.md`
> **Research snapshot:** `docs/AI/plans/crawl-onboarding-research.md`
> **Depends on:** Phase 4 complete (Gateway endpoints exist). Phase 5 parallel (CrawlTrigger calls crawler).
> **Status:** PENDING

## 1. OBJECTIVE

ShopERP Admin UI: Pending tab + Duplicates tab in `TenantManagement.razor`, new `ClaimsQueue.razor` + `CrawlTrigger.razor`.

## 2. GATES & HARD STOPS

- **🔴 UI Platform compliance:** VanAnButton/VanAnCard/VanAnAlert/VanAForm/VanATable/VanAModal — no custom HTML/CSS
- **No business logic in UI** — delegate to API client → Gateway → services

## 3. PRE-CONDITIONS

- [ ] Phase 4 done — Gateway endpoints exist
- [ ] Phase 5 done (or parallel) — Crawler worker on port 5010 reachable
- [ ] Re-verify `TenantManagement.razor` line ~1003 (likely shifted)

## 4. FILES TO MODIFY / CREATE

### MODIFY
| Path | Change |
|---|---|
| `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor` | Add tab "Pending" — list tenants `Status=Pending`, columns Name/TaxCode/Address/SourceUrl/CrawledAt. "Verify" button per row → modal (VanAModal) with `VerifyTenantRequest` form (username/password/displayName/slug) → `POST /api/v1/tenants/{id}/verify`. Add tab "Duplicates" — list `PotentialDuplicateOf != null`, side-by-side compare, "Keep this / Deactivate other" buttons → `POST /api/v1/tenants/duplicates/resolve`. |

### CREATE
| Path | Role |
|---|---|
| `5_WebApps/ShopERP/Components/Pages/Admin/ClaimsQueue.razor` | Table (VanATable): ClaimId, TenantName, ClaimantName, ClaimantPhone, TaxCodeSubmitted, GpkdImageUrl (thumbnail), SubmittedAt. Per row: "View GPKD" (open image new tab), "Cross-check MST" (link to dangkykinhdoanh.gov.vn search), "Approve" (VanAModal with admin config: OwnerUsername, OwnerPassword auto-gen + show, OwnerDisplayName, Slug auto-suggest, ShopInstanceId dropdown), "Reject" (VanAModal with reason). On approve: show credentials ONCE in dismissable VanAnAlert "Sao chép credentials ngay — sẽ không hiển thị lại". |
| `5_WebApps/ShopERP/Components/Pages/Admin/CrawlTrigger.razor` | Form (VanAForm): select source (dropdown from `crawler-sources.json` via Gateway proxy), industry, province, max results. "Trigger crawl" button (VanAnButton) → `POST /api/v1/crawl/trigger` (Gateway forwards to crawler port 5010). Show progress/results: Imported X, Skipped Y, Errors Z. |
| `5_WebApps/ShopERP/Services/TenantClaimApiClient.cs` | Wraps Gateway API calls for claims/verify/duplicates/crawl. Follows pattern of existing `TenantApiClient.cs`. |

## 5. ACCEPTANCE CRITERIA

- [ ] `dotnet build 5_WebApps/ShopERP/VanAn.ShopERP.csproj` — 0 errors
- [ ] Pending tab lists Pending tenants with Verify button
- [ ] Duplicates tab lists `PotentialDuplicateOf != null` with Keep/Deactivate
- [ ] ClaimsQueue shows pending claims with Approve/Reject modals
- [ ] Approve modal auto-generates password + shows credentials once
- [ ] CrawlTrigger calls Gateway → crawler (port 5010)
- [ ] All UI uses VanAn* components — no custom HTML/CSS
- [ ] No business logic in Razor pages — all delegate to API client

## 6. VERIFICATION

```powershell
dotnet build 5_WebApps\ShopERP\VanAn.ShopERP.csproj
```
Manual RV: navigate admin pages, verify rendering. No Playwright until Phase 8 (Gate 3).

## 7. CORRECTIONS APPLIED

| # | Correction |
|---|---|
| C3 | CrawlTrigger calls Gateway → crawler port 5010 (NOT 5003) |
