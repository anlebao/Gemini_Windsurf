# TASK CARD: Phase 4 — API Gateway endpoints + TenantSyncSubscriber

> **Master plan:** `docs/AI/plans/crawl-onboarding-master-plan.md`
> **Research snapshot:** `docs/AI/plans/crawl-onboarding-research.md`
> **Depends on:** Phase 3 complete (services exist + outbox publish)
> **Status:** PENDING

## 1. OBJECTIVE

3 new Gateway controllers (Crawl, TenantClaim, TenantPending) + modify `TenantStoreController.GetBySlug` (Pending: hide SĐT section, không mask). Rate limit claim endpoint. YARP forward to crawler worker. **NEW (Option A):** `TenantSyncSubscriber` ở ShopERP — sync Active tenant PG→SQLite qua NATS để đảm bảo tenant identity nhất quán (tránh accounting split).

## 2. GATES & HARD STOPS

- **No business logic in Controllers** — delegate to services
- **Pattern #10 compliance:** Any `StringContent`/`MediaTypeHeaderValue` strips charset from `Request.ContentType`
- **KhachLink HTTP-only** — N/A here (this is Gateway)

## 3. PRE-CONDITIONS

- [ ] Phase 3 done — services registered in DI
- [ ] Re-verify `TenantStoreController.cs` GetBySlug + MapToStoreDto line refs (likely shifted)
- [ ] **Open M5** resolved: rate limit impl approach (`Microsoft.AspNetCore.RateLimiting` policy code)

## 4. FILES TO MODIFY / CREATE

### MODIFY
| Path | Change |
|---|---|
| `2_Gateway/Controllers/TenantStoreController.cs` | `GetBySlug`: after load, check Status. Active → full DTO (current behavior). **Pending → `Phone = null`, `Email = null`** (M3 — HIDE SĐT section entirely, không mask, không trả CrawledPhone), add `IsPending = true`, `ClaimUrl = $"/store/{slug}/claim"` (correction H6: NO separate `MaskedPhone` field — just null Phone). Suspended/Inactive/Converted → 404. **NO `MaskPhone` helper needed** (M3 changed: hide, not mask). Add `IsPending` + `ClaimUrl` to `TenantStoreDto`. |
| `2_Gateway/Program.cs` | Add rate limit policy `ClaimSubmitPolicy` — 3 req per IP per day for `POST /api/v1/tenants/{id}/claims`. Add YARP forward `/api/v1/crawl/trigger` → crawler worker (port **5010** — correction C3, NOT 5003). |

### CREATE
| Path | Role |
|---|---|
| `2_Gateway/Controllers/CrawlController.cs` | `[Route("api/v1/crawl")]`, `[Authorize(Policy="SystemAdmin")]`. `POST /api/v1/crawl/batch` — accepts `List<CrawlListingDto>`, calls `OnboardUnverifiedAsync` per item, returns `BatchCrawlResult(Imported, Skipped, Errors)`. `GET /api/v1/crawl/sources/{tenantId}` — audit trail. `POST /api/v1/crawl/trigger` — YARP forward to crawler worker (port 5010). |
| `2_Gateway/Controllers/TenantClaimController.cs` | Split auth: `POST /api/v1/tenants/{tenantId}/claims` — `[AllowAnonymous]` + rate-limited (owner submits). `GET /api/v1/claims` + `GET /api/v1/claims/{id}` — `[Authorize(Policy="SystemAdmin")]`. `POST /api/v1/claims/{id}/approve` — `[Authorize(Policy="SystemAdmin")]`, accepts `ApproveClaimRequest(VerifyTenantRequest)`, returns `VerifyResult` + credentials (shown once). `POST /api/v1/claims/{id}/reject` — `[Authorize(Policy="SystemAdmin")]`. |
| `2_Gateway/Controllers/TenantPendingController.cs` | `[Route("api/v1/tenants/pending")]`, `[Authorize(Policy="SystemAdmin")]`. `GET /api/v1/tenants/pending` — list Pending. `POST /api/v1/tenants/{id}/verify` — direct verify (bypass claim). `GET /api/v1/tenants/duplicates` — list `PotentialDuplicateOf != null`. `POST /api/v1/tenants/duplicates/resolve` — `ResolveDuplicateRequest(KeepTenantId, DeactivateTenantId, Reason)`. |

### CREATE — ShopERP TenantSyncSubscriber (Option A — NEW)
| Path | Role |
|---|---|
| `5_WebApps/ShopERP/Services/TenantSyncSubscriber.cs` | `BackgroundService` subscribe NATS subjects `vanan.cloud.tenant.verified` + `vanan.cloud.tenant.profile.updated`. On event: parse JSON payload (`tenantId`, `newName`, `settings`), upsert Tenant row in SQLite (cùng `Guid` tenantId, copy Name + Settings — slug, contactPhone, address, taxCode, brandStory, logoUrl, lat/lng, theme, etc.). Follow `OrderSyncSubscriber` pattern. **Pending tenant events ignored** (subject `vanan.cloud.tenant.pending` not subscribed — Pending không sync). |
| `5_WebApps/ShopERP/Program.cs` | Register `TenantSyncSubscriber` as `HostedService`. Subscribe pattern same as `OrderSyncSubscriber`. |

## 5. ACCEPTANCE CRITERIA

- [ ] `dotnet build VanAn.sln` — 0 errors
- [ ] `GetBySlug` for Pending returns `Phone=null` + `IsPending=true` + `ClaimUrl` (M3 — hide, not mask)
- [ ] `GetBySlug` for Pending does NOT return `CrawledPhone` (internal field, never in DTO)
- [ ] `GetBySlug` for Suspended/Inactive/Converted returns 404
- [ ] Claim submit endpoint is `[AllowAnonymous]` + rate-limited (3/IP/day)
- [ ] SysAdmin endpoints are `[Authorize(Policy="SystemAdmin")]`
- [ ] No `StringContent` with raw `Request.ContentType` (Pattern #10 audit)
- [ ] YARP forward to crawler uses port **5010** (correction C3)
- [ ] No business logic in controllers — all delegate to services
- [ ] **`TenantSyncSubscriber` subscribes `vanan.cloud.tenant.verified` + `vanan.cloud.tenant.profile.updated`** (Option A)
- [ ] **`TenantSyncSubscriber` upserts Tenant row in SQLite with same Guid tenantId** (identity consistency)
- [ ] **Pending tenant events NOT synced** (subject `vanan.cloud.tenant.pending` not subscribed)

## 6. VERIFICATION

```powershell
dotnet build VanAn.sln
```
Integration tests deferred to Phase 8. Manual curl test against running Gateway optional.

## 7. CORRECTIONS APPLIED

| # | Correction |
|---|---|
| C3 | Crawler worker port 5010 (NOT 5003 — ShopERP conflict) |
| H6 | `GetBySlug` reuses `Phone` field for masked value, NO separate `MaskedPhone` field |
