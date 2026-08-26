# TASK CARD: Phase 8 — Tests + Runtime Verification

> **Master plan:** `docs/AI/plans/crawl-onboarding-master-plan.md`
> **Research snapshot:** `docs/AI/plans/crawl-onboarding-research.md`
> **Depends on:** Phases 1-7 complete
> **Status:** PENDING

## 1. OBJECTIVE

Domain tests, service tests, integration tests, crawler tests + 5-layer RV. Enable Playwright (Gate 3 lift — build must pass first).

## 2. GATES & HARD STOPS

- **🔴 Playwright Gate 3:** Enable ONLY after `dotnet build VanAn.sln` PASS + implementation complete.
- **🔴 guard-check.ps1 + dotnet build VanAn.sln MUST PASS** before any submission (governance)

## 3. PRE-CONDITIONS

- [ ] Phases 1-7 done — build PASS
- [ ] **Open O3** resolved: `VanAnDbContextTestFactory` updated for 2 new DbSets (`TenantClaimRequests`, `CrawlSources`)
- [ ] All migrations applied to test DB

## 4. FILES TO CREATE

### Domain tests
| Path | Tests |
|---|---|
| `6_Tests/VanAn.Core.Tests/Domain/TenantPendingTests.cs` | `CreateUnverified_ProducesPendingTenant`, `CreateUnverified_RaisesTenantPendingEvent_NotTenantCreatedEvent`, `CreateUnverified_SetsPendingSlug_OnSettings`, `Verify_FromPending_TransitionsToActive`, `Verify_RaisesTenantVerifiedEvent`, `Verify_FromActive_Throws`, `Verify_FromInactive_Throws`, `Verify_WithPotentialDuplicateOf_Throws` (correction H4), `UpdateSlug_OnPendingTenant_StillWorks` (correction C4 — guard unchanged, Pending CAN update slug if needed... wait, Verify sets slug via UpdateSlug after Active. Actually Pending bypasses UpdateSlug via factory. Test: `UpdateSlug_OnSuspendedTenant_Succeeds` — verify guard NOT tightened), `UpdateSlug_OnActiveTenant_Succeeds`, `MarkPotentialDuplicateOf_SetsFlag`, `TenantClaimRequest_Create_RaisesEvent`, `TenantClaimRequest_Approve_TransitionsStatus`, `TenantClaimRequest_Reject_SetsReason` |

### Service tests
| Path | Tests |
|---|---|
| `6_Tests/VanAn.Core.Tests/Services/Onboarding/OnboardUnverifiedTests.cs` | `OnboardUnverifiedAsync_CreatesPendingTenant_WithMaskedPhone` (ContactPhone=null, CrawledPhone=raw), `OnboardUnverifiedAsync_DoesNotCreateUser`, `OnboardUnverifiedAsync_MarksDuplicate_WhenTaxCodeExists` (correction H5: first canonical), `OnboardUnverifiedAsync_SavesCrawlSourceAudit`, `OnboardUnverifiedAsync_PendingSlugFormat`, `VerifyAsync_CreatesOwnerUser_AndPermissionGroups`, `VerifyAsync_TransitionsTenantToActive`, `VerifyAsync_UnmasksPhone_CopiesCrawledToContact`, `VerifyAsync_UpdatesSlug_ToCleanSlug`, `VerifyAsync_Throws_WhenPotentialDuplicateOfNotNull` (correction H4) |
| `6_Tests/VanAn.Core.Tests/Services/Claims/TenantClaimServiceTests.cs` | `SubmitClaimAsync_CreatesClaimRequest_WithSubmittedStatus`, `SubmitClaimAsync_OnActiveTenant_Throws`, `ApproveClaimAsync_VerifiesTenant_AndApprovesClaim`, `RejectClaimAsync_SetsRejectedStatus_AndReason`, `ListPendingClaimsAsync_ReturnsOnlySubmittedClaims` |

### Integration tests
| Path | Tests |
|---|---|
| `6_Tests/VanAn.Integration.Tests/CrawlOnboardingEndpointTests.cs` | `POST_crawl_batch_CreatesPendingTenants`, `POST_tenants_id_claims_CreatesClaimRequest`, `GET_claims_ReturnsPendingClaims`, `POST_claims_id_approve_VerifiesTenant`, `GET_tenants_by-slug_Pending_ReturnsNullPhone` (M3 — `Phone` field null, no `MaskedPhone` field, no `CrawledPhone` exposed), `GET_tenants_by-slug_Active_ReturnsFullPhone`, `GET_tenants_by-slug_Suspended_Returns404`, `POST_tenants_pending_id_verify_DirectVerify`, `POST_tenants_duplicates_resolve_DeactivatesOther`, `POST_tenants_id_claims_RateLimited_After3PerDay`, **`POST_claims_id_approve_TriggersNATSSync_TenantVerifiedEvent`** (Option A — verify outbox publish), **`PATCH_tenants_id_profile_TriggersNATSSync_TenantProfileUpdatedEvent`** (Option A) |
| `6_Tests/VanAn.Integration.Tests/TenantSyncSubscriberTests.cs` (NEW — Option A) | `TenantSyncSubscriber_OnTenantVerifiedEvent_UpsertsSQLiteTenant` (verify tenant row exists in SQLite with same Guid + correct Name + Settings), `TenantSyncSubscriber_OnTenantProfileUpdatedEvent_UpdatesSQLiteTenant`, `TenantSyncSubscriber_OnTenantPendingEvent_Ignored` (Pending not synced), `TenantSyncSubscriber_Idempotent_OnDuplicateEvent` (re-delivery safe — upsert, not insert twice) |

### Crawler tests
| Path | Tests |
|---|---|
| `6_Tests/VanAn.Integration.Tests/CrawlerWorkerTests.cs` | `RestApiAdapter_FetchAsync_ParsesDoanhNghiepResponse` (use fixture HTML/JSON matching M2-verified schema), `RestApiAdapter_RespectsRateLimit`, `TrangVangHtmlAdapter_FetchAsync_ParsesListingHtml` (mock HTML), `CrawlerCoordinator_OnTrigger_PostsBatchToGateway`, `CrawlerCoordinator_UsesPort5010` (correction C3) |

### Architecture test update
| Path | Change |
|---|---|
| `6_Tests/VanAn.Architecture.Tests/ArchitectureRulesTests.cs` | Verify `7_Tooling/VanAn.Crawler.csproj` does NOT reference Domain layer (only HTTP client). Verify crawler does NOT inject `IVanAnDbContext`. Whitelist `7_Tooling` from "NO new .csproj" check. |

## 5. ACCEPTANCE CRITERIA

- [ ] `dotnet build VanAn.sln` — 0 errors
- [ ] `dotnet test 6_Tests/VanAn.Core.Tests` — all domain + service tests PASS
- [ ] `dotnet test 6_Tests/VanAn.Integration.Tests --filter "CrawlOnboarding|CrawlerWorker"` — all PASS
- [ ] `dotnet test 6_Tests/VanAn.Architecture.Tests` — PASS (with whitelist)
- [ ] `guard-check.ps1` PASS
- [ ] Tests verify corrections: H4 (Verify throws on duplicate), H5 (first canonical duplicate), H6 (no MaskedPhone field), C3 (port 5010), C4 (UpdateSlug guard unchanged)

## 6. RUNTIME VERIFICATION (5-layer per `.devin/rules/runtime-verification.md`)

1. **API checks:** `curl` each new endpoint against running Gateway
   - `POST /api/v1/crawl/batch` with sample listing
   - `GET /api/tenants/by-slug/pending-{taxCode}-{random4}` → verify `IsPending=true`, `Phone` masked, `ClaimUrl` present
   - `POST /api/v1/tenants/{id}/claims` with sample claim
   - `GET /api/v1/claims` (SysAdmin auth)
   - `POST /api/v1/claims/{id}/approve` → verify tenant Active
2. **Static assets:** KhachLink `/store/pending-*` page loads, `/store/{slug}/claim` form loads
3. **Playwright runtime:** (Gate 3 lifted) E2E: crawl batch → Pending tenant appears → `/store/pending-*` shows profile WITHOUT SĐT section (M3 — hide, not mask) → submit claim → admin queue shows claim → approve → tenant Active → `/store/clean-slug` shows full profile + owner login works
4. **UI flow:** manual browser — same flow as Playwright but manual
5. **DB inspection:** verify `TenantClaimRequests`, `CrawlSources` tables populated; `Tenants.PotentialDuplicateOf` + `Tenants.Settings_CrawledPhone` columns exist in BOTH PG (CoreHub) + SQLite (ShopERP) — correction C2; **verify Active tenant row exists in SQLite với cùng Guid tenantId sau Verify** (Option A — `TenantSyncSubscriber` worked)

## 7. VERIFICATION COMMANDS

```powershell
dotnet build VanAn.sln
dotnet test 6_Tests\VanAn.Core.Tests
dotnet test 6_Tests\VanAn.Integration.Tests --filter "CrawlOnboarding|CrawlerWorker"
dotnet test 6_Tests\VanAn.Architecture.Tests
.\guard-check.ps1
```

## 8. CORRECTIONS APPLIED

All corrections from review verified via tests:
- C1 (Single-Identity Guid FK) — verified in integration tests
- C2 (ShopERP SQLite migration) — verified in DB inspection layer 5
- C3 (port 5010) — verified in `CrawlerCoordinator_UsesPort5010`
- C4 (UpdateSlug guard unchanged) — verified in `UpdateSlug_OnSuspendedTenant_Succeeds`
- H4 (Verify throws on duplicate) — verified in `Verify_WithPotentialDuplicateOf_Throws` + `VerifyAsync_Throws_WhenPotentialDuplicateOfNotNull`
- H5 (first canonical duplicate) — verified in `OnboardUnverifiedAsync_MarksDuplicate_WhenTaxCodeExists`
- H6 (no MaskedPhone field) — verified in `GET_tenants_by-slug_Pending_ReturnsMaskedPhone`
