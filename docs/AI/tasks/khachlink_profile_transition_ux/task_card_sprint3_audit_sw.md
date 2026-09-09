# Task Card Sprint 3: Audit Log + SW Version Bump + Security Monitoring Dashboard

> **Status:** ⏳ (after Sprint 2)
> **Sprint:** 3 / 3
> **Effort:** ~1-2 sessions (expanded scope: audit + SW + security logging + dashboard)
> **Master plan:** `docs/AI/tasks/khachlink_profile_transition_ux/master_plan.md`
> **Coding plan:** `docs/AI/tasks/khachlink_profile_transition_ux/coding_plan_sprint3_audit_sw.md`
> **Top-level card:** `docs/AI/tasks/khachlink_profile_transition_ux/task_card.md`
> **Prerequisite:** ✅ Sprint 2 COMPLETE + PUSHED + RV PASS (`61e4d4d4` on `main`)

## Objective

Mọi profile change được **audit log** (ai đổi, đổi khi nào, từ gì sang gì) + khách hàng install PWA nhận nav mới **ngay** (SW version bump qua UpdatedAt cache buster) + **giám sát sự cố bảo mật** (đăng nhập thất bại, rate limit, hoạt động đáng ngờ) với **dashboard phân loại cảnh báo theo mức độ nghiêm trọng** (Critical/High/Medium/Low/Info). Reuse `AuditLog` pattern hiện có — KHÔNG tạo entity mới.

**EXPANDED (user-approved 2026-09-09):** Audit logging có **toggle SystemAdmin** (1 master + 3 nhóm: Kế toán / Bảo mật / KhachLink — default ON, runtime không restart) + **hybrid async persist** (kế toán SYNC bảo toàn tính toàn vẹn — bảo mật/KhachLink ASYNC fire-and-forget qua background queue, không chặn luồng chính). Chi tiết: `coding_plan_sprint3_audit_sw.md` Section 0.5.

## Scope Checklist

### Task 3.1: Audit log cho profile change (reuse AuditLog pattern)
**File:** `1_Shared/Domain/Audit/AuditLog.cs` — UPDATE (add enum value + ForSecurityEvent factory)
**File:** `3_CoreHub/Services/KhachLinkInstanceService.cs` — UPDATE (inject IAuditTrailService, log UpdateAsync)
**File:** `5_WebApps/ShopERP/Components/Pages/Admin/KhachLinkInstanceAudit.razor` — NEW (audit history view)
**File:** `5_WebApps/ShopERP/Components/Pages/Admin/KhachLinkInstances.razor` — UPDATE (add "Lịch sử" button)
- [ ] **Add enum value** trong `AuditLog.cs`:
  ```csharp
  public enum AuditableEntityType
  {
      // ... existing 1-11 ...
      KhachLinkInstance = 12,  // NEW — platform-level instance profile change
      SecurityEvent = 13       // NEW — security incident (failed login, rate limit, suspicious activity)
  }
  ```
  Additive only — không renumber existing. Enum stored as int, new values = 12-13, backward compatible.
- [ ] **Inject `IAuditTrailService`** vào `KhachLinkInstanceService`:
  ```csharp
  public KhachLinkInstanceService(
      IVanAnDbContext dbContext,
      IAuditTrailService auditTrailService,  // NEW
      ILogger<KhachLinkInstanceService>? logger = null)
  ```
  `IAuditTrailService` đã registered trong DI (verified: Gateway line 514, ShopERP line 256, CoreHub line 109).
- [ ] **Log trong `UpdateAsync`** — capture old values BEFORE `instance.UpdateProfile()`, capture new values AFTER, call `_auditTrailService.LogUpdateAsync()` AFTER `SaveChangesAsync` (best-effort try/catch):
  - Old: `oldProfile = instance.Profile`, `oldNavFlagsJson = JsonSerializer.Serialize(instance.NavFlags)`, `oldStyleJson`
  - Call `instance.UpdateProfile(profile, navFlags)` + `instance.UpdateStyle(...)`
  - New: `newNavFlagsJson`, `newStyleJson`
  - Call `_auditTrailService.LogUpdateAsync(AuditableEntityType.KhachLinkInstance, id, oldValues, newValues, correlationId: id.ToString())`
  - **TenantId:** `KhachLinkInstance.TenantId = Guid.Empty` (platform sentinel). `AuditTrailService.LogUpdateAsync` dùng `_tenantProvider.TenantId` — SystemAdmin context returns `Guid.Empty` (verified: `IsSystemAdminWithoutTenant()` line 321-333).
  - **CorrelationId:** `id.ToString()` (instance ID)
- [ ] **Controller** — `KhachLinkInstanceController.Update` KHÔNG cần thay đổi (service tự log).
- [ ] **Admin audit history view** — `KhachLinkInstanceAudit.razor`:
  - Route `/admin/khachlink-instances/{InstanceId:guid}/audit`
  - Query `IAuditTrailService.GetEntityHistoryAsync(AuditableEntityType.KhachLinkInstance, instanceId)`
  - Render table: Timestamp, User, OldProfile, NewProfile, NavFlags diff summary
  - Link từ `KhachLinkInstances.razor` list page (column "Hành động" → thêm "📝 Lịch sử" button → `NavigationManager.NavigateTo`)
- [ ] **Audit query endpoint:** ✅ VERIFIED existing — `GET /api/audit-trail/entity/{entityType}/{entityId}` (NOT `/api/v1/audit-logs/...`). `AuditTrailController` route `api/audit-trail`, endpoint `entity/{entityType}/{entityId}` returns `IReadOnlyList<AuditLog>`. Authorized `Roles = "Admin"`.

### Task 3.2: SW version bump qua UpdatedAt cache buster
**File:** `5_WebApps/KhachLink/wwwroot/js/onboarding-tour.js` — UPDATE (add vananTriggerSWUpdate)
**File:** `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor` — UPDATE (call vananTriggerSWUpdate on profile change)
- [ ] **Sprint 2 đã add:** UpdatedAt to client model + ByDomainResponse (cache key `_v2`→`_v3`) + profile-change detection in `KhachLinkLayout.OnInitializedAsync` (compares `instanceConfig.UpdatedAt` vs localStorage `last_seen_profile_at`).
- [ ] **SW update trigger:** Add `vananTriggerSWUpdate` function to `onboarding-tour.js`:
  ```js
  window.vananTriggerSWUpdate = function() {
      if ('serviceWorker' in navigator) {
          navigator.serviceWorker.getRegistration().then(reg => {
              if (reg) reg.update();  // Force SW update check
          });
      }
  };
  ```
- [ ] **Call from KhachLinkLayout:** Khi detect profile change (Sprint 2 P2.1 detection block), gọi `JSRuntime.InvokeVoidAsync("vananTriggerSWUpdate")` (best-effort try/catch).
- [ ] **Lưu ý:** SW update chỉ check for new SW version — không force reload page. Nav update từ `StateHasChanged()` đủ (Blazor re-render với new NavFlags). SW bump cho PWA install cache (next launch).

### Task 3.3: Security event audit logging (NEW — log at source)
**File:** `1_Shared/Domain/Audit/AuditLog.cs` — UPDATE (add AuditActionType values + ForSecurityEvent factory)
**File:** `3_CoreHub/Services/IAuditTrailService.cs` — UPDATE (add LogSecurityEventAsync)
**File:** `3_CoreHub/Services/AuditTrailService.cs` — UPDATE (add LogSecurityEventAsync impl)
**File:** `3_CoreHub/Services/PlatformUserLoginService.cs` — UPDATE (inject IAuditTrailService + IHttpContextAccessor, log failed login)
**File:** `2_Gateway/Program.cs` — UPDATE (log rate limit hits in OnRejected)
- [ ] **Add AuditActionType values** trong `AuditLog.cs`:
  ```csharp
  public enum AuditActionType
  {
      // ... existing 1-11 ...
      SecurityAlert = 12,       // Generic security alert
      FailedLogin = 13,         // Failed login attempt (bad credentials, inactive user)
      SuspiciousActivity = 14,  // Suspicious pattern detected (repeated failures, anomaly)
      RateLimitHit = 15         // Rate limit triggered (potential brute-force / abuse)
  }
  ```
  Additive only — values 12-15, backward compatible.
- [ ] **Add ForSecurityEvent factory** to `AuditLog` entity — new factory method (additive, existing factories unchanged). `EntityType = SecurityEvent`, `EntityId = Guid.Empty`, `Reason = description`.
- [ ] **Add LogSecurityEventAsync** to `IAuditTrailService` + `AuditTrailService` — new method (additive). Uses `AuditLog.ForSecurityEvent` factory. `LogWarning` level (security events always Warning).
- [ ] **Log failed login** in `PlatformUserLoginService.LoginAsync`:
  - Inject `IAuditTrailService` + `IHttpContextAccessor` (both registered in DI).
  - 3 failure paths: user not found, wrong password, inactive user — each logs `FailedLogin` with IP + username in description.
  - `correlationId: clientIp` — IP as correlation (dashboard groups repeated failures from same IP = brute-force).
  - Best-effort try/catch — login flow never fails due to audit error.
- [ ] **Log rate limit hits** in Gateway `Program.cs` `OnRejected`:
  - Resolve `IAuditTrailService` from `context.HttpContext.RequestServices` (scoped, available in request pipeline).
  - Log `RateLimitHit` with policy name + endpoint + IP in description.
  - `correlationId: clientIp` — same IP correlation as failed login.
  - Best-effort try/catch — 429 response never blocked by audit failure.

### Task 3.4: Audit monitoring dashboard (NEW — enhance existing AuditTrail.razor)
**File:** `5_WebApps/ShopERP/Components/Pages/Admin/AuditTrail.razor` — UPDATE (summary cards + severity classification + filters)
- [ ] **Severity classification** (presentation-only, in `@code` block):
  - `IncidentSeverity` enum: Critical, High, Medium, Low, Info
  - `GetSeverity(AuditLog)` switch expression — maps (Action, EntityType) → severity:
    - **Critical (🔴 Nghiêm trọng):** SuspiciousActivity, Delete on AccountingEntry (fraud), Delete on Tenant (destructive), RateLimitHit (brute-force)
    - **High (🟠 Cao):** FailedLogin, SecurityAlert, PermissionChange, Delete on User/KhachLinkInstance
    - **Medium (🟡 Trung bình):** Correction, Reversal, PeriodClose/Reopen, Update on AccountingEntry/KhachLinkInstance/Tenant, Create Tenant
    - **Info (ℹ️ Thông tin):** Login, Logout
    - **Low (🔵 Thấp):** default (Create/Update on non-financial, Export)
  - **NO Domain change for classification** — `IncidentSeverity` enum + `GetSeverity` live in UI layer (AuditTrail.razor `@code` block).
- [ ] **Summary cards** (top of page, before filter panel):
  - 5 cards: Total (24h), Critical count, High count, Failed login count, Rate limit count
  - Data from `GetRecentAsync(200)` — separate data load from filtered table
  - UI Platform: `VanACard` for each card, scoped CSS for card styling
- [ ] **Severity column** in results table — `GetSeverityDisplay` + `GetSeverityClass` (scoped CSS badges)
- [ ] **Severity filter** in filter panel — client-side filter applied after `QueryAsync` returns
- [ ] **Update EntityType filter** — add KhachLinkInstance (12) + SecurityEvent (13) options
- [ ] **Update Action filter** — add SecurityAlert (12) + FailedLogin (13) + SuspiciousActivity (14) + RateLimitHit (15) options

### Task 3.5: Audit toggle SystemAdmin (EXPANDED — user-approved)
**File:** `3_CoreHub/Services/IFeatureFlagService.cs` — UPDATE (optional `defaultWhenMissing` param)
**File:** `3_CoreHub/Services/FeatureFlagService.cs` — UPDATE (4 audit flags + Default field)
**File:** `5_WebApps/ShopERP/Components/Pages/Admin/ValcnFeatures.razor` (VERIFY path) — VERIFY (likely no change)
- [ ] **4 flags, default ON** (ngược VALCN v2 default OFF): `Audit_Enabled` (master) + `Audit_Accounting` + `Audit_Security` + `Audit_KhachLink`
- [ ] **`IsEnabledAsync` optional param** `defaultWhenMissing = false` — audit gọi với `true`, VALCN callers không đổi (backward compatible)
- [ ] **Gate mapping:** AccountingEntry/PeriodClosing → `Audit_Accounting`; SecurityEvent → `Audit_Security`; KhachLinkInstance → `Audit_KhachLink`; EntityType khác → master only
- [ ] **Admin UI:** 4 toggle hiển thị ở `/admin/valcn-features` (tự động nếu page iterate `GetAllAsync` — verify)
- [ ] Query methods (QueryAsync/GetEntityHistoryAsync/GetRecentAsync) KHÔNG bị toggle — đọc luôn cho phép

### Task 3.6: Hybrid async persist (EXPANDED — user-approved, kế toán sync)
**File:** `3_CoreHub/Services/AuditLogQueue.cs` — NEW (bounded Channel 1000, DropOldest)
**File:** `3_CoreHub/Services/AuditLogBackgroundWriter.cs` — NEW (BackgroundService, flush 5s/100, residual flush)
**File:** `2_Gateway/Program.cs` + `5_WebApps/ShopERP/Program.cs` — UPDATE (register queue + writer)
**File:** `3_CoreHub/Services/IAuditTrailService.cs` + `AuditTrailService.cs` — UPDATE (return `Task<AuditLog?>` + hybrid persist)
- [ ] **Accounting audit = SYNC** — `await` repo AddAsync giữ nguyên (toàn vẹn kế toán TT 152/2025, không mất log khi crash)
- [ ] **Security + KhachLink audit = ASYNC** — enqueue `AuditLogQueue` → `AuditLogBackgroundWriter` batch INSERT (flush mỗi 5s hoặc đủ 100)
- [ ] **Graceful shutdown** — residual flush khi stoppingToken cancel
- [ ] **Best-effort semantics** — batch fail → LogError + drop (không re-enqueue, tránh vòng lặp DB down); accounting không ảnh hưởng
- [ ] **`Task<AuditLog?>`** — trả null khi toggle OFF; grep callers dùng return value (hiện verified: tất cả discard)
- [ ] **Async delay note** — audit row security/KhachLink xuất hiện trễ tối đa ~5s (RV Layer 4 chờ trước query)

## Prerequisites

- ✅ Sprint 2 COMPLETE + PUSHED + RV PASS (`61e4d4d4` on `main`) — P2.1 What's New banner (detect change qua UpdatedAt), P3.2 cart modal, P3.1 onboarding tour, ProfileChangeDirection enum, UpdatedAt client model, nav IDs
- ✅ `AuditLog` entity + `AuditTrailService` + `IAuditLogRepository` (live — verified 2026-09-09)
- ✅ `AccountingEntryService` precedent (inject `IAuditTrailService`, call `LogCreateAsync` — pattern reuse)
- ✅ `KhachLinkInstanceService.UpdateAsync` (live — inject point)
- ✅ `AuditableEntityType` enum (live — add values 12-13)
- ✅ `AuditActionType` enum (live — add values 12-15)
- ✅ `KhachLinkInstanceDto.UpdatedAt` (verified — BaseEntity field, in DTO line 275)
- ✅ `IAuditTrailService` registered in DI (verified: Gateway line 514, ShopERP line 256, CoreHub line 109 — all AddScoped)
- ✅ Audit query endpoint existing (verified: `AuditTrailController` route `api/audit-trail`, endpoint `entity/{entityType}/{entityId}`)
- ✅ `AuditTrail.razor` existing (verified: `/admin/audit-trail`, filter panel + results table + detail modal, UI Platform components)
- ✅ `PlatformUserLoginService.LoginAsync` identified (inject point for failed login audit — 3 failure paths: user not found, wrong password, inactive user)
- ✅ Gateway rate limiter `OnRejected` identified (inject point for rate limit audit — line 209-215 in Program.cs)
- ⏳ Verify `IHttpContextAccessor` registered in DI (precedent: `AuditTrailService` already injects it → should be registered)
- ⏳ Verify `RateLimitLease.GetAllTags()` API (fallback: hardcode policy name)
- ⏳ Verify `IAuditLogRepository.GetByEntityAsync` is cross-tenant (not tenant-filtered — `GetEntityHistoryAsync` doesn't check `IsSystemAdminWithoutTenant`)

## Verification

1. **Build:** `dotnet build VanAn.sln` → 0 errors
2. **Audit log (P1.2):** đổi profile Directory → FullCommerce → query `AuditLog` table → row inserted with `EntityType=12 (KhachLinkInstance)`, `EntityId={instanceId}`, `OldValues={"Profile":"Directory",...}`, `NewValues={"Profile":"FullCommerce",...}`, `UserId={systemAdminId}`
3. **Admin audit history (P1.2):** vào `/admin/khachlink-instances/{id}/audit` → table hiển thị lịch sử profile change (Timestamp, User, Old→New, NavFlags diff)
4. **Security event — failed login (P3.3):** attempt bad password on `/api/platform/login` → `AuditLog` row inserted with `Action=13 (FailedLogin)`, `EntityType=13 (SecurityEvent)`, `Reason="Failed login: wrong password for 'admin' from IP ..."`
5. **Security event — rate limit (P3.3):** trigger auth rate limit (6+ login attempts/min) → `AuditLog` row inserted with `Action=15 (RateLimitHit)`, `EntityType=13 (SecurityEvent)`, `Reason="Rate limit hit: policy 'auth' on '/api/platform/login' from IP ..."`
6. **Monitoring dashboard (P3.4):** vào `/admin/audit-trail` → summary cards hiển thị counts (Total 24h, Critical, High, Failed login, Rate limit) + severity column trong table (🔴🟠🟡🔵ℹ️ badges) + severity filter dropdown
7. **SW version bump (P5.1):** đổi profile → mở app → detect UpdatedAt change → `vananTriggerSWUpdate` call → SW update check → console log `[VanAn SW] Update check triggered` → next launch nav fresh
8. **E2E:** `profile-transition.spec.ts` PASS trên cả 2 domain (thêm Sprint 3 test cases: SW function exists, SW triggers on change, audit API endpoint exists — GATEWAY_URL, KHÔNG phải KHACHLINK_URL)
9. **EXPANDED — Toggle (P3.5):** vào `/admin/valcn-features` → 4 audit toggles hiển thị default ON; tắt `Audit_KhachLink` → đổi profile → KHÔNG có audit row mới; tắt master `Audit_Enabled` → không log gì cả (kể cả accounting); bật lại → log trở lại (cache 30s)
10. **EXPANDED — Hybrid async (P3.6):** (a) tạo bút toán kế toán → audit row NGAY LẬP TỨC (sync); (b) failed login → audit row sau ~5-10s (async flush) + `IpAddress` structured field populated; (c) unit tests toggle gating + queue/writer flush PASS
11. **EXPANDED — Unit tests (P3.5/3.6):** `AuditToggleAndQueueTests.cs` — master off, group off, default-on, hybrid sync/async paths, queue roundtrip, writer flush — ALL PASS

## Governance checklist

- [ ] **DOMAIN MOD (enum additive only):** Thêm `KhachLinkInstance = 12` + `SecurityEvent = 13` vào `AuditableEntityType`; thêm `SecurityAlert = 12` + `FailedLogin = 13` + `SuspiciousActivity = 14` + `RateLimitHit = 15` vào `AuditActionType` — additive, không renumber existing, không phá backward compat. KHÔNG tạo entity mới (reuse `AuditLog`).
- [ ] **DOMAIN MOD (factory additive):** Thêm `AuditLog.ForSecurityEvent` factory method — new method, existing factories unchanged. `AuditLog` remains immutable (private constructor, factory-only).
- [ ] **AuditLog immutable** — append-only, reuse `AuditTrailService.LogUpdateAsync` + new `LogSecurityEventAsync`, không update/delete
- [ ] Domain PURE — chỉ thêm enum values + factory method, không thêm entity, không đụng `AccountingEntry`/`BaseEntity`
- [ ] **Severity classification = presentation-only** — `IncidentSeverity` enum + `GetSeverity` switch sống trong `AuditTrail.razor` `@code` block (UI layer). NO Domain change for classification.
- [ ] UI Platform — `AuditTrail.razor` (enhanced) + `KhachLinkInstanceAudit.razor` (new) dùng VanAn.UI.Platform (VanACard, VanAButton, VanAModal, VanAAlert, VanASpinner)
- [ ] KhachLink HTTP-only — SW update client-side only (JS interop)
- [ ] No new .csproj
- [ ] No migration — enum stored as int, new values 12-15 backward compatible (no PG migration needed)
- [ ] Multi-tenancy — `KhachLinkInstance.TenantId = Guid.Empty` (platform sentinel), audit log platform-level. Security events use `_tenantProvider.TenantId` (Guid.Empty for anonymous).
- [ ] Playwright isolation — E2E chỉ chạy sau build pass
- [ ] **EXPANDED — Toggle:** Service layer only (`IFeatureFlagService` optional param backward compatible + `SystemSetting` pattern sẵn có). Default ON cho audit flags, default OFF giữ nguyên cho VALCN. Không đụng Domain.
- [ ] **EXPANDED — Hybrid async:** Accounting audit SYNC (toàn vẹn kế toán). Security/KhachLink ASYNC qua `AuditLogQueue` (bounded, DropOldest — không bao giờ block request). `AuditLog` immutable — queue chỉ defer write. BackgroundService dùng IServiceScopeFactory (không inject scoped vào singleton).
- [ ] **EXPANDED — Return type `Task<AuditLog?>`:** source-compatible (callers discard — verified + grep lại trước build)

## Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | Enum addition phá backward compatibility | Additive only — values 12-15, không renumber. Enum stored as int, existing values 1-11 unchanged. No migration. |
| R2 | `IAuditTrailService` không resolve trong rate limiter `OnRejected` | `HttpContext.RequestServices.GetService<IAuditTrailService>()` — scoped service resolvable in request pipeline. Best-effort try/catch — 429 response not blocked. |
| R3 | `IHttpContextAccessor` không registered trong `PlatformUserLoginService` DI | `AuditTrailService` already injects it → registered in all 3 hosts. Verify DI registration. |
| R4 | `context.Lease.GetAllTags()` API không tồn tại | Fallback: hardcode `var policy = "rate-limited";`. Verify `RateLimitLease` API in .NET 8. |
| R5 | `TenantId` sai cho audit log | `KhachLinkInstance.TenantId = Guid.Empty`. `AuditTrailService` dùng `_tenantProvider.TenantId` — SystemAdmin without tenant returns `Guid.Empty` (verified: `IsSystemAdminWithoutTenant()` line 321-333). |
| R6 | Audit query endpoint chưa có | ✅ VERIFIED existing — `AuditTrailController` at `api/audit-trail/entity/{entityType}/{entityId}`. |
| R7 | SW update không trigger trên iOS Safari | iOS PWA SW update limited. Fallback: nav update từ `StateHasChanged()` đủ cho current session. SW bump cho next launch. |
| R8 | Audit log JSON serialize NavFlags quá lớn | NavFlags = 15 bools → JSON ~200 chars. Style = 5 fields → ~100 chars. Total ~400 chars each. Acceptable. |
| R9 | Summary cards `GetRecentAsync(200)` không cover 24h nếu volume cao | MVP acceptable — admin dashboard, not real-time monitoring. Future: add server-side count endpoint. |
| R10 | Severity classification sai cho action type mới | `GetSeverity` switch covers all new action types (FailedLogin=High, RateLimitHit=Critical, SuspiciousActivity=Critical, SecurityAlert=High). Default = Low. |
| R11 | `KhachLinkInstanceAudit.razor` NavFlags double-serialized parse fail | `GetNavFlagsDiffSummary` wraps in try/catch → returns "—" on parse error. Defensive parsing. |
| R12 | Failed login audit spam nếu brute-force attack | Rate limiter (5 req/min/IP) caps login attempts. Each failed login logs 1 audit row. 5 rows/min max per IP. Acceptable — audit log is append-only. |
| RE1 | **EXPANDED:** Async queue mất Security/KhachLink log khi crash đột ngột | User chấp nhận hybrid — Accounting SYNC không mất. Mitigation: bounded 1000 + DropOldest + flush 5s/100 + residual flush graceful shutdown. |
| RE2 | **EXPANDED:** Default-true audit flags phá VALCN default-false | Optional param `defaultWhenMissing = false` — VALCN callers không đổi. `GetAllAsync` hiển thị đúng theo Default per feature. |
| RE3 | **EXPANDED:** `AuditTrailService` ctor thêm deps → thiếu registration = audit silently skipped | Register `AuditLogQueue` + verify `IFeatureFlagService` ở Gateway + ShopERP. Gateway `OnRejected` dùng `GetService` (null-safe) → phải verify bằng test/RV. |
| RE4 | **EXPANDED:** BackgroundWriter drop batch khi DB lỗi | LogError + drop (không re-enqueue). Best-effort cho security/KhachLink. Accounting sync path riêng — không ảnh hưởng. |
| RE5 | **EXPANDED:** Async audit row trễ ~5s — admin query ngay không thấy | RV Layer 4: chờ 5-10s trước query. Acceptable MVP. |
| RE6 | **EXPANDED:** `Task<AuditLog?>` breaking cho caller dùng return value | Verified callers discard (`AccountingEntryService.cs:93`). Grep lại toàn repo trước build. |

## Files

| # | File | Action | Status |
|---|---|---|---|
| 1 | `1_Shared/Domain/Audit/AuditLog.cs` | UPDATE (add enum values + `ForSecurityEvent` factory) | ⏳ |
| 2 | `3_CoreHub/Services/KhachLinkInstanceService.cs` | UPDATE (inject IAuditTrailService, log UpdateAsync) | ⏳ |
| 3 | `2_Gateway/Controllers/KhachLinkInstanceController.cs` | VERIFY (no change needed — verified 2026-09-09) | ✅ |
| 4 | `5_WebApps/ShopERP/Components/Pages/Admin/KhachLinkInstanceAudit.razor` | NEW (audit history view) | ⏳ |
| 5 | `5_WebApps/ShopERP/Components/Pages/Admin/KhachLinkInstances.razor` | UPDATE (add "Lịch sử" button) | ⏳ |
| 6 | `5_WebApps/KhachLink/Services/Http/KhachLinkInstanceHttpService.cs` | ~~UPDATE~~ NO CHANGE (Sprint 2 đã add UpdatedAt + cache `_v3` — coding plan Step 9 note) | ✅ |
| 7 | `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor` | UPDATE (call vananTriggerSWUpdate) | ⏳ |
| 8 | `5_WebApps/KhachLink/wwwroot/js/onboarding-tour.js` | UPDATE (add vananTriggerSWUpdate function) | ⏳ |
| 9 | `6_Testing/e2e-tests/profile-transition.spec.ts` | UPDATE (Sprint 3 test cases — GATEWAY_URL cho audit API test) | ⏳ |
| 10 | `3_CoreHub/Services/IAuditTrailService.cs` | UPDATE (LogSecurityEventAsync + `Task<AuditLog?>` returns) | ⏳ |
| 11 | `3_CoreHub/Services/AuditTrailService.cs` | UPDATE (toggle gate + hybrid persist + LogSecurityEventAsync) | ⏳ |
| 12 | `3_CoreHub/Services/PlatformUserLoginService.cs` | UPDATE (inject + log failed login với IP/UserAgent structured) | ⏳ |
| 13 | `2_Gateway/Program.cs` | UPDATE (OnRejected audit + register queue/writer) | ⏳ |
| 14 | `5_WebApps/ShopERP/Components/Pages/Admin/AuditTrail.razor` | UPDATE (summary cards + severity + filters) | ⏳ |
| 15 | `3_CoreHub/Services/IFeatureFlagService.cs` | UPDATE (optional `defaultWhenMissing` param) | ⏳ |
| 16 | `3_CoreHub/Services/FeatureFlagService.cs` | UPDATE (4 audit flags + Default field) | ⏳ |
| 17 | `3_CoreHub/Services/AuditLogQueue.cs` | NEW (bounded Channel 1000, DropOldest) | ⏳ |
| 18 | `3_CoreHub/Services/AuditLogBackgroundWriter.cs` | NEW (BackgroundService, flush 5s/100) | ⏳ |
| 19 | `5_WebApps/ShopERP/Program.cs` | UPDATE (register AuditLogQueue + BackgroundWriter) | ⏳ |
| 20 | `5_WebApps/ShopERP/Components/Pages/Admin/ValcnFeatures.razor` (VERIFY path) | VERIFY (likely no change — iterate GetAllAsync) | ⏳ |
| 21 | `6_Testing/VanAn.Core.Tests/Audit/AuditToggleAndQueueTests.cs` (VERIFY structure) | NEW (toggle + queue/writer tests) | ⏳ |

## Open questions (resolve during Sprint 3)

1. ~~**`IAuditTrailService` DI in Gateway:**~~ ✅ RESOLVED (2026-09-09) — registered Gateway line 514, ShopERP line 256, CoreHub line 109 (verify CoreHost Program.cs — class library?).
2. ~~**`_tenantProvider.TenantId` for SystemAdmin:**~~ ✅ RESOLVED (2026-09-09) — `Guid.Empty` platform mode (`IsSystemAdminWithoutTenant()` line 321-333).
3. ~~**Audit query endpoint:**~~ ✅ RESOLVED (2026-09-09) — `AuditTrailController` route `api/audit-trail`, endpoint `entity/{entityType}/{entityId}` (NOT `api/v1/audit-logs`).
4. ~~**`KhachLinkInstanceDto.UpdatedAt`:**~~ ✅ RESOLVED (2026-09-09) — exists, mapped from `BaseEntity.UpdatedAt` (ToDto line 236).
5. **`IHttpContextAccessor` DI:** Verify registered (`AuditTrailService` injects → có lẽ đã có). Verify lúc implement.
6. **`RateLimitLease.GetAllTags()` API:** Verify .NET 8. Fallback: hardcode `"rate-limited"`. Verify lúc implement.
7. **`IAuditLogRepository.GetByEntityAsync` cross-tenant:** Verify không tenant-filtered cho SystemAdmin per-instance history page. Verify lúc implement.
8. **EXPANDED — `IFeatureFlagService` DI registration Gateway/ShopERP:** VALCN đang dùng → có lẽ có. Nếu thiếu → add. Verify lúc implement.
9. **EXPANDED — "CoreHub Program.cs line 109":** Governance nói 3_CoreHub pure Class Library. Verify file này (test host? legacy?). Nếu host thật → register queue/writer ở đó. Verify lúc implement.
10. **EXPANDED — Callers dùng return value `Log*Async`:** Grep toàn repo — nếu caller nào assign result → null-check. Hiện verified discard. Verify lúc implement.
11. **EXPANDED — ValcnFeatures.razor exact path + iterate vs hardcode:** Verify Step 7A. Verify lúc implement.
12. **EXPANDED — Test project structure:** Verify `VanAn.Core.Tests` convention (stub pattern) trước khi tạo test file. Verify lúc implement.

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
