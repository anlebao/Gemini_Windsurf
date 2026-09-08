# Task Card Sprint 3: Audit Log + SW Version Bump

> **Status:** ⏳ (after Sprint 2)
> **Sprint:** 3 / 3
> **Effort:** ~1 session
> **Master plan:** `docs/AI/tasks/khachlink_profile_transition_ux/master_plan.md`
> **Top-level card:** `docs/AI/tasks/khachlink_profile_transition_ux/task_card.md`
> **Prerequisite:** ✅ Sprint 2 COMPLETE + RV PASS

## Objective

Mọi profile change được **audit log** (ai đổi, đổi khi nào, từ gì sang gì) + khách hàng install PWA nhận nav mới **ngay** (SW version bump qua UpdatedAt cache buster). Reuse `AuditLog` pattern hiện có — KHÔNG tạo entity mới.

## Scope Checklist

### Task 3.1: Audit log cho profile change (reuse AuditLog pattern)
**File:** `1_Shared/Domain/Audit/AuditLog.cs` — UPDATE (add enum value)
**File:** `3_CoreHub/Services/KhachLinkInstanceService.cs` — UPDATE (inject IAuditTrailService, log UpdateAsync)
**File:** `2_Gateway/Controllers/KhachLinkInstanceController.cs` — UPDATE (pass old values for audit)
**File:** `5_WebApps/ShopERP/Components/Pages/Admin/KhachLinkInstanceAudit.razor` — NEW (audit history view)
- [ ] **Add enum value** trong `AuditLog.cs`:
  ```csharp
  public enum AuditableEntityType
  {
      // ... existing 1-11 ...
      KhachLinkInstance = 12  // NEW — platform-level instance profile change
  }
  ```
  Additive only — không renumber existing. Enum stored as int, new value = 12, backward compatible.
- [ ] **Inject `IAuditTrailService`** vào `KhachLinkInstanceService`:
  ```csharp
  public KhachLinkInstanceService(
      IVanAnDbContext dbContext,
      IAuditTrailService auditTrailService,  // NEW
      ILogger<KhachLinkInstanceService>? logger = null)
  ```
  Register `IAuditTrailService` đã có trong DI (verify — `AuditTrailService` đã registered).
- [ ] **Log trong `UpdateAsync`** — trước khi `instance.UpdateProfile()`:
  - Capture old values: `oldProfile = instance.Profile`, `oldNavFlags = JsonSerializer.Serialize(instance.NavFlags)`
  - Call `instance.UpdateProfile(profile, navFlags)` (existing)
  - Capture new values: `newProfile = instance.Profile`, `newNavFlags = JsonSerializer.Serialize(instance.NavFlags)`
  - Call `_auditTrailService.LogUpdateAsync(AuditableEntityType.KhachLinkInstance, id, oldValues, newValues)`
  - **TenantId:** `KhachLinkInstance.TenantId = Guid.Empty` (platform sentinel). `AuditTrailService.LogUpdateAsync` dùng `_tenantProvider.TenantId` — verify SystemAdmin context returns `Guid.Empty` (platform mode, precedent `IsSystemAdminWithoutTenant()` line 321-333).
  - **CorrelationId:** `id.ToString()` (instance ID)
- [ ] **Controller** — `KhachLinkInstanceController.Update` không cần thay đổi (service tự log). Verify `IAuditTrailService` resolve trong Gateway DI scope (Gateway runs CoreHub in-process — verify).
- [ ] **Admin audit history view** — `KhachLinkInstanceAudit.razor`:
  - Route `/admin/khachlink-instances/{id}/audit`
  - Query `IAuditTrailService.GetEntityHistoryAsync(AuditableEntityType.KhachLinkInstance, instanceId)`
  - Render table: Timestamp, User, OldProfile, NewProfile, NavFlags diff
  - Link từ `KhachLinkInstances.razor` list page (column "Hành động" → thêm "Lịch sử" button)
- [ ] **Verify audit query endpoint:** `GET /api/v1/audit-logs/entity/{entityType}/{entityId}` — verify existing (grep `AuditLogController`). Nếu chưa có → add endpoint in existing audit controller (precedent `AuditTrailService.GetEntityHistoryAsync`).

### Task 3.2: SW version bump qua UpdatedAt cache buster
**File:** `5_WebApps/KhachLink/Services/Http/KhachLinkInstanceHttpService.cs` — UPDATE (compare UpdatedAt)
**File:** `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor` — UPDATE (force reload nav on change)
- [ ] Trong `KhachLinkInstanceHttpService.GetByCurrentDomainAsync`:
  - Cache hiện: `khachlink_instance_config_v2` (key) + `khachlink_instance_config_v2_ts` (timestamp)
  - Thêm: `khachlink_instance_config_v2_updatedAt` (DateTime from API)
  - Khi cache hit → compare cached `UpdatedAt` với... (cần fetch fresh để compare? No — cache TTL 1 min đủ)
  - **Simpler approach:** Khi `KhachLinkLayout.OnInitializedAsync` detect `instanceConfig.UpdatedAt > lastSeenUpdatedAt` (localStorage) → force `StateHasChanged()` + update nav (Sprint 2 P2.1 đã detect change — reuse)
- [ ] **SW update trigger:** Khi detect profile change (Sprint 2 P2.1), gọi `JSRuntime.InvokeVoidAsync("vananTriggerSWUpdate")`:
  ```js
  window.vananTriggerSWUpdate = function() {
      if ('serviceWorker' in navigator) {
          navigator.serviceWorker.getRegistration().then(reg => {
              if (reg) reg.update();  // Force SW update check
          });
      }
  };
  ```
  Add to `onboarding-tour.js` hoặc inline in `KhachLinkLayout`.
- [ ] **Lưu ý:** SW update chỉ check for new SW version — không force reload page. Nav update từ `StateHasChanged()` đủ (Blazor re-render với new NavFlags). SW bump cho PWA install cache (next launch).

## Prerequisites

- ✅ Sprint 2 COMPLETE + RV PASS (P2.1 What's New banner — detect change qua UpdatedAt)
- ✅ `AuditLog` entity + `AuditTrailService` + `IAuditLogRepository` (live — verified 2026-09-08)
- ✅ `AccountingEntryService` precedent (inject `IAuditTrailService`, call `LogCreateAsync` — pattern reuse)
- ✅ `KhachLinkInstanceService.UpdateAsync` (live — inject point)
- ✅ `AuditableEntityType` enum (live — add value 12)
- ✅ `KhachLinkInstanceDto.UpdatedAt` (verify — BaseEntity field, should be in DTO)
- ⏳ Verify `IAuditTrailService` registered in Gateway DI scope
- ⏳ Verify audit query endpoint existing (or add)

## Verification

1. **Build:** `dotnet build VanAn.sln` → 0 errors
2. **Audit log (P1.2):** đổi profile Directory → FullCommerce → query `AuditLog` table → row inserted with `EntityType=12 (KhachLinkInstance)`, `EntityId={instanceId}`, `OldValues={"Profile":"Directory",...}`, `NewValues={"Profile":"FullCommerce",...}`, `UserId={systemAdminId}`
3. **Admin audit history (P1.2):** vào `/admin/khachlink-instances/{id}/audit` → table hiển thị lịch sử profile change (Timestamp, User, Old→New)
4. **SW version bump (P5.1):** đổi profile → mở app → detect UpdatedAt change → `vananTriggerSWUpdate` call → SW update check → next launch nav fresh
5. **E2E:** `profile-transition.spec.ts` PASS trên cả 2 domain (thêm test cases cho Sprint 3)

## Governance checklist

- [ ] **DOMAIN MOD (enum additive only):** Thêm `KhachLinkInstance = 12` vào `AuditableEntityType` — additive, không renumber existing, không phá backward compat. KHÔNG tạo entity mới (reuse `AuditLog`).
- [ ] **AuditLog immutable** — append-only, reuse `AuditTrailService.LogUpdateAsync`, không update/delete
- [ ] Domain PURE — chỉ thêm enum value, không thêm entity, không đụng `AccountingEntry`/`BaseEntity`
- [ ] UI Platform — `KhachLinkInstanceAudit.razor` dùng VanAn.UI.Platform
- [ ] KhachLink HTTP-only — SW update client-side only (JS interop)
- [ ] No new .csproj
- [ ] No migration — enum stored as int, new value = 12, backward compatible (no PG migration needed)
- [ ] Multi-tenancy — `KhachLinkInstance.TenantId = Guid.Empty` (platform sentinel), audit log platform-level
- [ ] Playwright isolation — E2E chỉ chạy sau build pass

## Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | Enum addition phá backward compatibility | Additive only — value 12, không renumber. Enum stored as int, existing values 1-11 unchanged. |
| R2 | `IAuditTrailService` không resolve trong Gateway scope | Verify DI registration. Gateway runs CoreHub in-process — `AuditTrailService` đã registered. Nếu chưa → add registration. |
| R3 | `TenantId` sai cho audit log | `KhachLinkInstance.TenantId = Guid.Empty`. `AuditTrailService` dùng `_tenantProvider.TenantId` — SystemAdmin without tenant returns `Guid.Empty` (platform mode). Verify `IsSystemAdminWithoutTenant()` path. |
| R4 | Audit query endpoint chưa có | Verify `AuditLogController` existing. Nếu chưa → add `GET /api/v1/audit-logs/entity/{entityType}/{entityId}` (precedent `AuditTrailService.GetEntityHistoryAsync`). |
| R5 | SW update không trigger trên iOS Safari | iOS PWA SW update limited. Fallback: nav update từ `StateHasChanged()` đủ cho current session. SW bump cho next launch. |
| R6 | Audit log JSON serialize NavFlags quá lớn | NavFlags = 15 bools → JSON ~200 chars. Acceptable. |

## Files

| # | File | Action | Status |
|---|---|---|---|
| 1 | `1_Shared/Domain/Audit/AuditLog.cs` | UPDATE (add `KhachLinkInstance = 12` to enum) | ⏳ |
| 2 | `3_CoreHub/Services/KhachLinkInstanceService.cs` | UPDATE (inject IAuditTrailService, log UpdateAsync) | ⏳ |
| 3 | `2_Gateway/Controllers/KhachLinkInstanceController.cs` | UPDATE (verify — likely no change needed) | ⏳ |
| 4 | `5_WebApps/ShopERP/Components/Pages/Admin/KhachLinkInstanceAudit.razor` | NEW (audit history view) | ⏳ |
| 5 | `5_WebApps/ShopERP/Components/Pages/Admin/KhachLinkInstances.razor` | UPDATE (add "Lịch sử" button) | ⏳ |
| 6 | `5_WebApps/KhachLink/Services/Http/KhachLinkInstanceHttpService.cs` | UPDATE (SW trigger on UpdatedAt change) | ⏳ |
| 7 | `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor` | UPDATE (call vananTriggerSWUpdate) | ⏳ |
| 8 | `5_WebApps/KhachLink/wwwroot/js/onboarding-tour.js` | UPDATE (add vananTriggerSWUpdate function) | ⏳ |
| 9 | `6_Testing/e2e-tests/profile-transition.spec.ts` | UPDATE (Sprint 3 test cases) | ⏳ |

## Open questions (resolve during Sprint 3)

1. **`IAuditTrailService` DI in Gateway:** Verify `AuditTrailService` registered in Gateway DI container. Gateway runs CoreHub in-process — should be registered. Verify lúc implement.
2. **`_tenantProvider.TenantId` for SystemAdmin:** Returns `Guid.Empty` (platform mode) or actual tenant? Verify `IsSystemAdminWithoutTenant()` path in `AuditTrailService` line 321-333. If returns actual tenant → audit log tenant-filtered wrong. Verify lúc implement.
3. **Audit query endpoint:** `GET /api/v1/audit-logs/entity/{entityType}/{entityId}` existing? Grep `AuditLogController`. If not → add endpoint. Verify lúc implement.
4. **`KhachLinkInstanceDto.UpdatedAt`:** Verify DTO has `UpdatedAt` field (from `BaseEntity.UpdatedAt`). If missing → add to `ToDto` mapping in `KhachLinkInstanceController`. Verify lúc implement.

## Related

- Master plan: `docs/AI/tasks/khachlink_profile_transition_ux/master_plan.md`
- Top-level card: `docs/AI/tasks/khachlink_profile_transition_ux/task_card.md`
- Sprint 1 (prerequisite): `docs/AI/tasks/khachlink_profile_transition_ux/task_card_sprint1_guardrail_foundation.md`
- Sprint 2 (prerequisite): `docs/AI/tasks/khachlink_profile_transition_ux/task_card_sprint2_transition_messaging.md`
- AuditLog entity: `1_Shared/Domain/Audit/AuditLog.cs`
- AuditTrailService: `3_CoreHub/Services/AuditTrailService.cs`
- IAuditTrailService: `3_CoreHub/Services/IAuditTrailService.cs`
- AccountingEntryService (precedent — inject IAuditTrailService): `3_CoreHub/Services/AccountingEntryService.cs` lines 94, 157, 208
- KhachLinkInstanceService (inject point): `3_CoreHub/Services/KhachLinkInstanceService.cs`
- KhachLinkInstanceController (verify): `2_Gateway/Controllers/KhachLinkInstanceController.cs`
