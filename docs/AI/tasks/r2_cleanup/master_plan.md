# R2 Cleanup Service + Per-Tenant Storage Admin — Master Plan

**Created:** 2026-08-20
**Status:** PLANNED (awaiting implementation approval)
**Branch target:** `main`
**Source:** User request — R2 storage sẽ đầy sau ~50,000 lượt xe, cần auto-cleanup + per-tenant isolation visibility.

## Context

### Vấn đề
- Ảnh biển số + khách hàng lưu trên Cloudflare R2 (bucket `vanan-guard-photos`)
- Mỗi lượt xe: 2 ảnh (~200KB sau nén)
- R2 free tier: 10GB storage → đầy sau ~50,000 lượt xe
- **KHÔNG có auto-cleanup** — ảnh tồn tại mãi mãi
- Admin không biết tenant nào dùng bao nhiêu storage

### Tenant Isolation ĐÃ CÓ SẴN (không cần làm lại)
| Layer | Cơ chế | Status |
|---|---|---|
| R2 photos | Key = `plates/{tenantId}/{guid}.jpg` + `customers/{tenantId}/{guid}.jpg` | ✅ |
| QR token | 32 random bytes + payload JSON có `tn: <tenantId>` | ✅ |
| QR lookup | `GetByQrTokenHashAsync(hash, tenantId)` — tenant-scoped | ✅ |
| Short code | `GetByShortCodeAsync(shortCode, tenantId)` — tenant-scoped + same-day | ✅ |
| VehicleSession | Entity có `TenantId`, mọi query filter | ✅ |
| GuardController | `GetTenantId()` từ JWT, mọi endpoint check `tenantId == Guid.Empty` → 401 | ✅ |

### Quyết định (user-confirmed 2026-08-20)
- **Retention:** 30 ngày sau `CheckedOut` hoặc `Voided`
- **Trigger:** Auto (background service daily) + Manual (admin button per-tenant)
- **Scope:** Sprint 1 (backend) + Sprint 2 (admin UI) — full scope

---

## Sprint 1 — R2 Cleanup Backend (P0)

**Mục tiêu:** Auto-delete ảnh R2 khi session `CheckedOut`/`Voided` sau 30 ngày + clear DB photo keys.

### Files mới

#### `3_CoreHub/Services/IR2CleanupService.cs`
```csharp
public interface IR2CleanupService
{
    /// <summary>Get storage stats for a tenant (photo count + total size).</summary>
    Task<TenantStorageStats> GetTenantStatsAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Cleanup expired photos for a specific tenant.</summary>
    Task<CleanupResult> CleanupTenantAsync(Guid tenantId, TimeSpan retentionPeriod, CancellationToken ct = default);

    /// <summary>Cleanup expired photos for ALL tenants (background service use).</summary>
    Task<CleanupResult> CleanupAllTenantsAsync(TimeSpan retentionPeriod, CancellationToken ct = default);
}

public record TenantStorageStats(int PlatePhotoCount, int CustomerPhotoCount, long TotalSizeBytes, DateTime? OldestPhotoDate);
public record CleanupResult(int SessionsProcessed, int PhotosDeleted, long BytesFreed, List<string> Errors);
```

#### `3_CoreHub/Services/R2CleanupService.cs`
- Inject: `IR2StorageService`, `IVehicleSessionRepository` (mới method `GetExpiredSessionsAsync`), `ILogger`
- Logic `CleanupTenantAsync`:
  1. Query DB: `VehicleSession WHERE TenantId = {tenantId} AND Status IN (CheckedOut, Voided) AND CheckedOutAt < UtcNow - 30 days AND (PlatePhotoKey != "" OR CustomerPhotoKey != "")`
  2. Batch collect R2 keys (plate + customer)
  3. Call `_r2Storage.DeleteObjectsAsync(keys)` (batch 1000/batch)
  4. Update DB: set `PlatePhotoKey = ""`, `CustomerPhotoKey = ""` (giữ metadata, chỉ xóa ảnh)
  5. Return `CleanupResult`

#### `3_CoreHub/Infrastructure/R2CleanupHostedService.cs`
- BackgroundService, chạy mỗi 24h
- Config: `R2Cleanup:RetentionDays=30`, `R2Cleanup:RunIntervalHours=24`, `R2Cleanup:Enabled=true`
- Gọi `CleanupAllTenantsAsync` — query distinct TenantId từ expired sessions

### Files sửa

#### `3_CoreHub/Services/IR2StorageService.cs` — thêm 3 methods
```csharp
/// <summary>List objects under a prefix (e.g. "plates/{tenantId}/").</summary>
Task<List<S3ObjectInfo>> ListObjectsByPrefixAsync(string prefix, CancellationToken ct = default);

/// <summary>Batch delete objects by keys (max 1000 per R2 API call).</summary>
Task<int> DeleteObjectsAsync(IEnumerable<string> keys, CancellationToken ct = default);

/// <summary>Get the R2 key prefix for a tenant's photos.</summary>
static string GetTenantPrefix(Guid tenantId) => $"plates/{tenantId}/";
```

#### `3_CoreHub/Services/R2StorageService.cs` — implement 3 methods
- `ListObjectsByPrefixAsync`: dùng `AmazonS3.ListObjectsV2Async` với `Prefix`
- `DeleteObjectsAsync`: dùng `AmazonS3.DeleteObjectsAsync` (batch, max 1000)
- Trả `S3ObjectInfo { Key, Size, LastModified }`

#### `3_CoreHub/Repositories/IVehicleSessionRepository.cs` — thêm method
```csharp
/// <summary>Get sessions with photos that are past retention period (for cleanup).</summary>
Task<List<VehicleSession>> GetExpiredSessionsAsync(Guid tenantId, DateTime cutoff, CancellationToken ct = default);

/// <summary>Get distinct tenant IDs that have expired sessions (for cleanup-all).</summary>
Task<List<Guid>> GetTenantsWithExpiredSessionsAsync(DateTime cutoff, CancellationToken ct = default);

/// <summary>Clear photo keys for a session (after R2 delete).</summary>
Task ClearPhotoKeysAsync(IEnumerable<Guid> sessionIds, CancellationToken ct = default);
```

#### `3_CoreHub/Repositories/VehicleSessionRepository.cs` — implement 3 methods
- `GetExpiredSessionsAsync`: `WHERE TenantId = {tenantId} AND Status IN (CheckedOut, Voided) AND CheckedOutAt < cutoff AND (PlatePhotoKey != "" OR CustomerPhotoKey != "")`
- `GetTenantsWithExpiredSessionsAsync`: `SELECT DISTINCT TenantId WHERE ...`
- `ClearPhotoKeysAsync`: `UPDATE VehicleSession SET PlatePhotoKey = "", CustomerPhotoKey = "" WHERE Id IN (...)`

#### `2_Gateway/Program.cs` — register services
```csharp
services.AddScoped<IR2CleanupService, R2CleanupService>();
services.AddHostedService<R2CleanupHostedService>();
services.Configure<R2CleanupOptions>(context.Configuration.GetSection("R2Cleanup"));
```

#### `2_Gateway/appsettings.json` — thêm config
```json
"R2Cleanup": {
  "Enabled": true,
  "RetentionDays": 30,
  "RunIntervalHours": 24,
  "BatchSize": 1000
}
```

#### `3_CoreHub/Infrastructure/R2CleanupOptions.cs` (mới)
```csharp
public class R2CleanupOptions
{
    public bool Enabled { get; set; } = true;
    public int RetentionDays { get; set; } = 30;
    public int RunIntervalHours { get; set; } = 24;
    public int BatchSize { get; set; } = 1000;
}
```

---

## Sprint 2 — Per-Tenant Storage Admin API + UI (P1)

**Mục tiêu:** Admin xem storage usage per-tenant + trigger manual cleanup.

### Files mới

#### `2_Gateway/Controllers/R2StorageController.cs`
```csharp
[ApiController]
[Route("api/r2storage")]
[Authorize]
public class R2StorageController : ControllerBase
{
    // GET /api/r2storage/stats/{tenantId} — SystemAdmin: any tenant, TenantAdmin: own only
    [HttpGet("stats/{tenantId}")]
    public async Task<IActionResult> GetStats(Guid tenantId) { ... }

    // POST /api/r2storage/cleanup/{tenantId} — SystemAdmin only, trigger immediate cleanup
    [HttpPost("cleanup/{tenantId}")]
    [Authorize(Policy = "SystemAdminOnly")]
    public async Task<IActionResult> TriggerCleanup(Guid tenantId, [FromQuery] int? retentionDays) { ... }

    // POST /api/r2storage/cleanup-all — SystemAdmin only, cleanup all tenants
    [HttpPost("cleanup-all")]
    [Authorize(Policy = "SystemAdminOnly")]
    public async Task<IActionResult> TriggerCleanupAll([FromQuery] int? retentionDays) { ... }
}
```

#### `5_WebApps/ShopERP/Components/Pages/Admin/R2StorageAdmin.razor`
- Table: Tenant Name | Photo Count | Total Size (MB) | Oldest Photo | Last Cleanup | Actions
- Button "Cleanup Now" per tenant (confirm dialog)
- Button "Cleanup All Tenants" (SystemAdmin only, confirm dialog)
- Progress bar khi cleanup đang chạy (polling status)
- Auto-refresh stats mỗi 30s

#### `5_WebApps/ShopERP/Services/R2StorageApiClient.cs`
- `GetStatsAsync(Guid tenantId)` → `TenantStorageStatsDto`
- `TriggerCleanupAsync(Guid tenantId, int? retentionDays)` → `CleanupResultDto`
- `TriggerCleanupAllAsync(int? retentionDays)` → `CleanupResultDto`

#### `5_WebApps/ShopERP/wwwroot/js/r2-storage-admin.js`
- Confirm dialogs (Vietnamese)
- Polling cleanup status
- Toast notifications

### Files sửa

#### `5_WebApps/ShopERP/Program.cs` — register `R2StorageApiClient`
#### `5_WebApps/ShopERP/Components/Pages/Admin/AdminLayout.razor` — thêm nav item "Quản lý lưu trữ ảnh"
#### `5_WebApps/ShopERP/Components/Pages/Sitemap.razor` — thêm link "Quản lý lưu trữ ảnh" vào SystemAdmin card

---

## Sprint 3 — Tests + RV (P0)

### Unit tests (`6_Tests/VanAn.Core.Tests/`)
- `R2/R2CleanupServiceTests.cs`:
  - `CleanupTenantAsync_DeletesExpiredPhotos_AndClearsDbKeys`
  - `CleanupTenantAsync_SkipsActiveSessions`
  - `CleanupTenantAsync_SkipsSessionsWithoutPhotos`
  - `CleanupAllTenantsAsync_ProcessesAllTenants`
  - `GetTenantStatsAsync_ReturnsCorrectCounts`
- `R2/R2StorageServiceTests.cs`:
  - `ListObjectsByPrefixAsync_ReturnsAllObjects`
  - `DeleteObjectsAsync_BatchesCorrectly` (1000+ keys → multiple batches)

### Integration tests (`6_Tests/VanAn.Integration.Tests/`)
- `Guard/R2StorageControllerTests.cs`:
  - `GetStats_SystemAdmin_CanAccessAnyTenant`
  - `GetStats_TenantAdmin_CanOnlyAccessOwnTenant`
  - `TriggerCleanup_NonAdmin_Returns403`
  - `TriggerCleanup_SystemAdmin_DeletesPhotos`

### RV Protocol
- **L1 API:** `GET /api/r2storage/stats/{tenantId}` 200/401/403, `POST /api/r2storage/cleanup/{tenantId}` 200/403
- **L2 Static:** `r2-storage-admin.js` served, `R2StorageAdmin.razor` route exists
- **L3 Playwright:** Login SystemAdmin → navigate `/admin/r2-storage` → see table → trigger cleanup → verify status polling → verify photos deleted
- **L4 Manual browser:** Admin UI flow end-to-end

---

## Thứ tự thực hiện

| # | Sprint | Effort | Priority | Output |
|---|---|---|---|---|
| 1 | Sprint 1 — R2 Cleanup Backend | Medium | P0 | `IR2CleanupService` + `R2CleanupHostedService` + DB methods |
| 2 | Sprint 2 — Admin API + UI | Medium | P1 | `R2StorageController` + `R2StorageAdmin.razor` |
| 3 | Sprint 3 — Tests + RV | Low | P0 | Unit + integration tests + Playwright RV |

## Risks & Mitigations

| Risk | Mitigation |
|---|---|
| R2 `ListObjectsV2` pagination (1000 keys/page) | Loop với `ContinuationToken` cho đến khi hết |
| R2 `DeleteObjects` max 1000/batch | Chunk keys thành batches 1000 |
| Background service chạy lâu với nhiều tenants | Process per-tenant sequentially, log progress |
| Admin trigger cleanup trên production → lock | Run async, return job ID, poll status (không block HTTP) |
| DB update fail sau R2 delete → orphaned keys | Wrap trong try/catch, log error, retry DB update 3 lần |
| Tenant admin xem stats tenant khác | Check `tenantId == GetTenantId()` trừ SystemAdmin |
