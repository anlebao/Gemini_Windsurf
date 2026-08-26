# TASK CARD: Phase 1 — Domain + Events

> **Master plan:** `docs/AI/plans/crawl-onboarding-master-plan.md`
> **Research snapshot:** `docs/AI/plans/crawl-onboarding-research.md` (verify line refs before edit)
> **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT — 7 step)
> **Status:** PENDING USER APPROVAL TO START

## 1. OBJECTIVE

Add Domain foundation: `TenantStatus.Pending=5`, `Tenant.CreateUnverified()`, `Tenant.Verify()`, `PotentialDuplicateOf` flag, 4 events, 2 new aggregates (`TenantClaimRequest`, `CrawlSource`). No DB yet.

## 2. GATES & HARD STOPS

- **🔴 Gate 5 (Domain modification):** User-approved exception. MUST be IMPLEMENT mode + Domain Phase active.
- **🔴 Single-Identity Pattern (HARD STOP):** FK dùng `Guid`/`Guid?` (PK reference), KHÔNG dùng `TenantId` value object. Audit checklist:
  - [ ] `PotentialDuplicateOf` is `Guid?`, NOT `TenantId?`
  - [ ] `TenantClaimRequest.TenantId` is `Guid`, NOT `TenantId`
  - [ ] `CrawlSource.TenantId` is `Guid`, NOT `TenantId`
  - [ ] No LINQ filter by `.Value` on value object Id
- **AccountingEntry immutable:** N/A — không touch.

## 3. PRE-CONDITIONS

- [ ] User approval để exit Plan mode → IMPLEMENT
- [ ] Active branch: `feature/crawl-onboard-tenant-pipeline` (tạo mới từ `main`)
- [ ] Re-verify research snapshot line refs (TenantStatus.cs, TenantSettings.cs 12 With + ctor 16 params, Tenant.cs:180-199)
- [ ] **Open O4 RESOLVED (2026-08-25, Option A approved):** Active tenant (sau Verify) MUST sync sang SQLite qua NATS — đảm bảo tenant identity nhất quán (cùng `Guid` tenantId ở PG + SQLite), tránh accounting split. Implement: `VerifyAsync` publish outbox `TenantVerifiedEvent` → NATS `vanan.cloud.tenant.verified` → `TenantSyncSubscriber` (ShopERP) upsert SQLite. **Pending tenant KHÔNG sync** (chưa có business activity). **Phase 1 phải thêm `TenantProfileUpdatedEvent` (5 events total)** để admin profile update cũng sync sang SQLite.
- [ ] **M3 RESOLVED (2026-08-25, user-approved):** Crawl SĐT + store `CrawledPhone` field trong `TenantSettings` (internal use). Pending profile KHÔNG hiển thị SĐT (hide section, không mask). Sau Verify, `ContactPhone` = owner-provided từ Claim form. CrawledPhone giữ cho SysAdmin verify, xóa sau Verify (data minimization).

## 4. FILES TO MODIFY / CREATE

### MODIFY
| Path | Change |
|---|---|
| `1_Shared/Domain/Aggregates/TenantAggregate/TenantStatus.cs` | Add `Pending = 5` (NOT `=0` — correction H1) |
| `1_Shared/Domain/Aggregates/TenantAggregate/TenantSettings.cs` | Add `CrawledPhone` (string?) — ctor param 17 + all **12** With methods (correction H3) + preserve LegalForm/BusinessField/CharterCapital. Add `WithCrawledPhone(string?)` method 13. |
| `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs` | Add `CreateUnverified(TenantId id, string name, TenantSettings settings, string pendingSlug)` (4 params — correction H2). Add `Verify()` — guard `Status == Pending && PotentialDuplicateOf == null` (correction H4). Add `Guid? PotentialDuplicateOf` + `MarkPotentialDuplicateOf(Guid otherId)` (correction C1 — Guid, NOT TenantId). **DO NOT touch `UpdateSlug()` guard** (correction C4). |
| `1_Shared/Domain/Aggregates/TenantAggregate/TenantEvents.cs` | Add **5** event records: `TenantPendingEvent`, `TenantVerifiedEvent`, `TenantClaimRequestedEvent`, `TenantClaimApprovedEvent`, **`TenantProfileUpdatedEvent(Guid TenantId, string NewName, TenantSettingsSnapshot Settings, DateTime OccurredAt)`** (correction H7 — for NATS sync khi admin update profile) |

### CREATE
| Path | Role |
|---|---|
| `1_Shared/Domain/Aggregates/TenantAggregate/TenantClaimRequest.cs` | Aggregate: `Id (Guid)`, `TenantId (Guid — NOT TenantId)`, `ClaimantName`, `ClaimantPhone`, `ClaimantEmail?`, `GpkdImageUrl`, `TaxCodeSubmitted`, `Status (ClaimStatus enum: Submitted=0, Approved=1, Rejected=2)`, `SubmittedAt`, `ReviewedByUserId?`, `ReviewedAt?`, `RejectionReason?`. Factory `Create(...)` raises `TenantClaimRequestedEvent`. Methods `Approve(reviewedByUserId)`, `Reject(reviewedByUserId, reason)`. |
| `1_Shared/Domain/Aggregates/TenantAggregate/CrawlSource.cs` | Audit entity: `Id (Guid)`, `TenantId (Guid — NOT TenantId)`, `SourceSite`, `SourceUrl`, `RawJson`, `CrawledAt`. Factory `Create(...)`. |

## 5. ACCEPTANCE CRITERIA

- [ ] `dotnet build 1_Shared/VanAn.Shared.csproj` — 0 errors
- [ ] `CreateUnverified` sets `Status=Pending=5`, raises `TenantPendingEvent` (NOT `TenantCreatedEvent`)
- [ ] `CreateUnverified` accepts `pendingSlug` param, sets via Settings directly (bypass `UpdateSlug()` — giải C4)
- [ ] `Verify()` throws if `Status != Pending` OR `PotentialDuplicateOf != null` (correction H4)
- [ ] `Verify()` sets `Status=Active`, raises `TenantVerifiedEvent` (will trigger NATS sync sang SQLite — Option A)
- [ ] **5 events defined** (correction H7): `TenantPendingEvent`, `TenantVerifiedEvent`, `TenantClaimRequestedEvent`, `TenantClaimApprovedEvent`, `TenantProfileUpdatedEvent`
- [ ] `TenantSettings` has `CrawledPhone` field (M3 — internal use, NOT displayed on Pending profile)
- [ ] **M3:** Pending tenant's `ContactPhone` = null (SĐT section hidden on profile); `CrawledPhone` stored internal only
- [ ] `UpdateSlug()` guard UNCHANGED (`Status == Inactive` only) — Suspended/Converted still work (correction C4)
- [ ] All FKs are `Guid`/`Guid?` — Single-Identity Pattern compliant (correction C1)
- [ ] `TenantSettings.With*` methods preserve `CrawledPhone` + `LegalForm`/`BusinessField`/`CharterCapital`
- [ ] Domain tests (Phase 8) sẽ verify behavior — not required in Phase 1

## 6. VERIFICATION

```powershell
dotnet build 1_Shared\VanAn.Shared.csproj
```
- No DB migration in Phase 1 (Phase 2 handles EF + migration).
- Domain unit tests deferred to Phase 8 (per plan), but recommended to write at least 3-4 sanity tests inline if time permits.

## 7. CORRECTIONS APPLIED (from review)

| # | Correction |
|---|---|
| C1 | All FKs `Guid` not `TenantId` value object |
| H1 | `Pending=5` not `=0` |
| H2 | `CreateUnverified` 4 params (with pendingSlug), bypass UpdateSlug |
| H3 | 12 With methods (not 14), thread CrawledPhone + preserve LegalForm/BusinessField/CharterCapital |
| H4 | `Verify()` also guards `PotentialDuplicateOf == null` |
| H7 | **NEW (Option A):** 5 events total (add `TenantProfileUpdatedEvent`) — for NATS sync khi admin update active tenant profile |
| M3 | CrawledPhone field kept (internal use), Pending profile hides SĐT section entirely (not masked) |
| C4 | DO NOT tighten `UpdateSlug()` guard — leave `Status == Inactive` |
