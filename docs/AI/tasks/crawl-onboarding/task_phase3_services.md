# TASK CARD: Phase 3 — Services (Onboarding split + Claim + Duplicate)

> **Master plan:** `docs/AI/plans/crawl-onboarding-master-plan.md`
> **Research snapshot:** `docs/AI/plans/crawl-onboarding-research.md`
> **Depends on:** Phase 2 complete (DbSets + migration)
> **Status:** PENDING

## 1. OBJECTIVE

Split `TenantOnboardingService` into `OnboardUnverifiedAsync` (Pending only) + `VerifyAsync` (user + groups + Activate + unmask). New `ITenantClaimService` + `IDuplicateDetectionService`.

## 2. GATES & HARD STOPS

- **No business logic in Controllers** (N/A — this is service layer)
- **Multi-tenancy** enforced via `TenantId` filter on all queries

## 3. PRE-CONDITIONS

- [ ] Phase 2 done — DbSets exist, migration applied
- [ ] Re-verify `TenantOnboardingService.cs` ctor deps + `DefaultGroups` + 5 steps of `OnboardAsync`

## 4. FILES TO MODIFY / CREATE

### MODIFY
| Path | Change |
|---|---|
| `3_CoreHub/Services/Onboarding/ITenantOnboardingService.cs` | Add `OnboardUnverifiedAsync(CrawlListingDto, ct)` + `VerifyAsync(Guid tenantId, VerifyTenantRequest, ct)`. Keep existing `OnboardAsync` for backward compat. |
| `3_CoreHub/Services/Onboarding/TenantOnboardingService.cs` | Implement 2 new methods. `OnboardUnverifiedAsync`: gen TenantId (UUIDv7), build TenantSettings (CrawledPhone=listing.CrawledPhone, ContactPhone=null), auto-gen pending slug `pending-{taxCode ?? random}-{random4hex}` with retry (3 attempts on collision), call `Tenant.CreateUnverified(id, name, settings, pendingSlug)`, save `CrawlSource` audit, **duplicate check via `IDuplicateDetectionService.MarkDuplicateIfTaxCodeExistsAsync`** (correction H5: first canonical, rest mark dup of first), save, dispatch `TenantPendingEvent`. `VerifyAsync`: load tenant (must be Pending), `tenant.Verify()`, create owner user + Owner role + 4 groups + assign owner to Quản lý (reuse `DefaultGroups`), set `ContactPhone` from owner-provided Claim form (consent), keep `CrawledPhone` internal (M3 — delete after Verify for data minimization, OR keep for audit), update slug if provided (now `UpdateSlug()` works — tenant is Active), save atomic, **publish OutboxMessage `TenantVerifiedEvent`** (Option A — trigger NATS sync sang SQLite, subject `vanan.cloud.tenant.verified`), dispatch `TenantVerifiedEvent`. |
| `3_CoreHub/Services/TenantManagementService.cs` | **NEW (Option A):** Modify `UpdateProfileAsync` — sau khi update active tenant's Name/Settings thành công, raise `TenantProfileUpdatedEvent` + publish OutboxMessage (subject `vanan.cloud.tenant.profile.updated`) → trigger `TenantSyncSubscriber` update SQLite row. Follow same outbox pattern as `OrderWorkflowService`. |
| `2_Gateway/Program.cs` + `5_WebApps/ShopERP/Program.cs` | DI: `AddScoped<ITenantClaimService, TenantClaimService>()` + `AddScoped<IDuplicateDetectionService, DuplicateDetectionService>()` |

### CREATE
| Path | Role |
|---|---|
| `3_CoreHub/Services/Onboarding/Dtos/CrawlDtos.cs` | `CrawlListingDto(Name, TaxCode?, Address?, CrawledPhone?, ContactName?, IndustryCode?, SourceSite, SourceUrl, CrawledAt, Lat?, Lng?)`, `VerifyTenantRequest(OwnerUsername, OwnerPassword, OwnerDisplayName, ShopInstanceId?, Slug?)`, `VerifyResult(TenantId, OwnerUserId, PermissionGroupsCreated, PublishedSlug)` |
| `3_CoreHub/Services/Claims/ITenantClaimService.cs` + `TenantClaimService.cs` | `SubmitClaimAsync(Guid tenantId, SubmitClaimRequest, ct)` — load tenant (Pending only, reject if Active), create `TenantClaimRequest` aggregate, save, dispatch `TenantClaimRequestedEvent`. `ApproveClaimAsync(Guid claimRequestId, VerifyTenantRequest adminConfig, Guid sysAdminUserId, ct)` — load claim (must be Submitted), call `VerifyAsync(claim.TenantId, adminConfig)`, `claim.Approve(sysAdminUserId)`, save, dispatch `TenantClaimApprovedEvent`. `RejectClaimAsync(...)`, `ListPendingClaimsAsync(ct)`, `GetClaimAsync(...)`. |
| `3_CoreHub/Services/Claims/ClaimDtos.cs` | `SubmitClaimRequest(ClaimantName, ClaimantPhone, ClaimantEmail?, GpkdImageUrl, TaxCodeSubmitted)`, `ClaimDto(...)` |
| `3_CoreHub/Services/IDuplicateDetectionService.cs` + `DuplicateDetectionService.cs` | `MarkDuplicateIfTaxCodeExistsAsync(Guid newTenantId, string? taxCode, ct)` — query existing tenant by TaxCode (Active OR Pending), if found set `PotentialDuplicateOf = existingTenant.Id` (correction H5: first canonical — query returns oldest/most-recent-active). `ListPotentialDuplicatesAsync(ct)`. `ResolveDuplicateAsync(Guid keepTenantId, Guid deactivateTenantId, string reason, ct)` — verify keep, deactivate other (NO merge). |

## 5. ACCEPTANCE CRITERIA

- [ ] `dotnet build VanAn.sln` — 0 errors
- [ ] `OnboardUnverifiedAsync` creates Pending tenant + CrawlSource audit + duplicate flag. NO user, NO groups, NO welcome email.
- [ ] `OnboardUnverifiedAsync` duplicate check: first canonical tenant kept, rest get `PotentialDuplicateOf = canonical.Id` (correction H5)
- [ ] `VerifyAsync` transitions Pending → Active, creates user + 4 groups, sets ContactPhone from owner-provided Claim form (consent), updates slug. Atomic transaction.
- [ ] **`VerifyAsync` publish OutboxMessage `TenantVerifiedEvent`** (Option A — subject `vanan.cloud.tenant.verified`) → trigger NATS sync sang SQLite
- [ ] **`UpdateProfileAsync` publish OutboxMessage `TenantProfileUpdatedEvent`** (Option A — subject `vanan.cloud.tenant.profile.updated`) khi admin update active tenant → SQLite không stale
- [ ] **M3:** `VerifyAsync` does NOT copy `CrawledPhone` → `ContactPhone` (legacy plan said "unmask"). Instead, `ContactPhone` = owner-provided from Claim form. CrawledPhone kept internal (or deleted post-Verify per data minimization).
- [ ] `SubmitClaimAsync` rejects Active tenant ("already verified")
- [ ] `ApproveClaimAsync` reuses `VerifyAsync` (DRY)
- [ ] `ResolveDuplicateAsync` deactivates one, verifies other — NO data merge
- [ ] All queries use `Guid` tenantId (Single-Identity Pattern — correction C1)

## 6. VERIFICATION

```powershell
dotnet build VanAn.sln
```
Unit tests deferred to Phase 8 (per plan), but recommended to write 2-3 sanity tests for `OnboardUnverifiedAsync` + `VerifyAsync` inline.

## 7. CORRECTIONS APPLIED

| # | Correction |
|---|---|
| C1 | All service queries filter by `Guid tenantId`, not `TenantId` value object |
| H5 | Duplicate check: first canonical, rest mark dup of first (not chain) |
| H4 | `VerifyAsync` will throw if `PotentialDuplicateOf != null` (Domain guard from Phase 1) — service must call `ResolveDuplicateAsync` first if duplicate |
