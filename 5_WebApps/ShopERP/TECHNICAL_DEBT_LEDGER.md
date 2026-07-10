# Technical Debt Ledger - Vạn An ShopERP Accounting Module

**File Location:** `5_WebApps\ShopERP\TECHNICAL_DEBT_LEDGER.md`  
**Created:** 2026-05-31  
**Purpose:** Tổng hợp workaround và technical debt cần refactor trong GĐ 2  
**Owner:** Code Review Team - Kiểm duyệt nội bộ  

---

## Danh sách Workarounds

### Tier 1: Tenant Isolation (Ưu tiên cao)

| # | File | Line | Mô tả | Ghi chú |
|---|------|------|-------|---------|
| 1 | `Components\Pages\Accounting\TransactionHistory.razor` | 187-194 | Fallback tenant hardcode | Đọc claim TenantId, nếu không có thì fallback về `00000000-0000-0000-0000-000000000001` |
| 2 | `Components\Pages\Accounting\ExpenseEntry.razor` | 211-219 | Fallback tenant hardcode | Tương tự TransactionHistory, dùng trong HandleSubmit |

#### Kế hoạch sửa Tier 1:

1. **File:** `Pages\Login.cshtml.cs`
   - **Action:** Thêm `TenantId` claim vào claims list (dòng 55-60)
   - **Code:**
     ```csharp
     var claims = new List<Claim>
     {
         new Claim(ClaimTypes.Name, Username),
         new Claim(ClaimTypes.Role, role.ToString()),
         new Claim("DisplayName", GetDisplayName(role)),
         new Claim("TenantId", GetTenantIdForUser(Username)) // TODO: Implement mapping
     };
     ```

2. **File:** `Components\Pages\Accounting\TransactionHistory.razor` và `ExpenseEntry.razor`
   - **Action:** Xóa block fallback tenant sau khi Login.cshtml.cs được cập nhật
   - **Behavior:** Nếu `TenantId` claim không có → throw hoặc hiển thị lỗi rõ ràng

---

### Tier 2: Component Binding (Ưu tiên sau Tier 1)

| # | File | Line | Mô tả | Ghi chú |
|---|------|------|-------|---------|
| 3 | `Components\Pages\Accounting\ExpenseEntry.razor` | 222-244 | JS Interop workaround | Đọc DOM value qua `vananReadElementValue` do @bind events bị drop qua ranh giới component |
| 4 | `Components\App.razor` | 18-27 | JS helper `vananReadElementValue` | Global JS function để hỗ trợ workaround #3 |

#### Kế hoạch sửa Tier 2:

1. **Điều tra root cause:**
   - Tại sao `@bind` bị drop qua ranh giới component/assembly?
   - Blazor hydration timing issue vs component lifecycle

2. **File:** `Components\Platform\DynamicFormFields.razor`
   - **Action:** Review `@bind:event="oninput"` + `@bind:after` có đủ không
   - **Consider:** Chuyển sang `@oninput` + manual state update

3. **File:** `Components\Pages\Accounting\ExpenseEntry.razor`
   - **Action:** Xóa JS interop block `vananReadElementValue`
   - **Behavior:** Dùng binding chuẩn Blazor

4. **File:** `Components\App.razor`
   - **Action:** Xóa JS helper `window.vananReadElementValue`

5. **Test:** Verify form submit với binding chuẩn, không cần JS fallback

---

## File đã sửa trong GĐ 1 (Fix triệt để)

| File | Thay đổi | Loại |
|------|----------|------|
| `1_Shared\Domain.cs` | Sửa `EndDate` calculation (AddTicks(-1)) | Fix (triệt để) |
| `3_CoreHub\Repositories\AccountingEntryRepository.cs` | Thêm `SaveChangesAsync` vào AddAsync/AddRangeAsync | Fix (triệt để) |
| `5_WebApps\ShopERP\Components\Platform\DynamicFormFields.razor` | Chuyển sang `@bind:event="oninput"` + `@bind:after` | Fix (triệt để) |
| `5_WebApps\ShopERP\Components\Pages\Accounting\TransactionHistory.razor` | Format vi-VN + log chẩn đoán (đã xóa) | Fix (triệt để) |

## File đã sửa trong Phase 2 (2026-05-31) - Fix 8 Remaining Test Failures

| File | Thay đổi | Loại |
|------|----------|------|
| `5_WebApps\ShopERP\VanAn.ShopERP.csproj` | Thêm `<InvariantGlobalization>false</InvariantGlobalization>` vào PropertyGroup | Fix (triệt để) |
| `3_CoreHub\Repositories\AccountingEntryRepository.cs` | `DateTime.SpecifyKind(..., DateTimeKind.Utc)` cho startDate/endDate trong `GetByTenantAndPeriodAsync` | Fix (triệt để) |
| `5_WebApps\ShopERP\Components\Pages\Accounting\TransactionHistory.razor` | Format amount: `InvariantCulture.Replace(",",".")` thay vì `GetCultureInfo("vi-VN")` | Fix (triệt để) |

---

## Tier 3: Flaky Performance Tests (2026-07-04)

| # | File | Test Method | Mô tả | Ghi chú |
|---|------|-------------|-------|---------|
| 5 | `6_Tests\VanAn.Core.Tests\ProductionDataTests.cs` | `Should_Handle_Production_Data_Volume_Spikes` (L280) | Stopwatch-based throughput ratio assert — flaky trên máy chậm/CI runner load cao | **Đã exclude khỏi CI** qua `Category=Performance` filter |
| 6 | `6_Tests\VanAn.Core.Tests\ProductionDataTests.cs` | `Should_Handle_Large_Production_Dataset` (L40) | Stopwatch assert `< 30000ms` — phụ thuộc phần cứng | **Đã exclude khỏi CI** |
| 7 | `6_Tests\VanAn.Core.Tests\ProductionDataTests.cs` | `Should_Handle_Production_Network_Conditions` (L208) | Stopwatch assert `< 60000ms` — phụ thuộc phần cứng | **Đã exclude khỏi CI** |
| 8 | `6_Tests\VanAn.Core.Tests\ProductionDataTests.cs` | `Should_Handle_Production_Memory_Constraints` (L252) | `GC.GetTotalMemory` assert `< 100MB` — non-deterministic do GC timing | **Đã exclude khỏi CI** |
| 9 | `6_Tests\VanAn.Core.Tests\ProductionDataTests.cs` | `Should_Handle_Production_Long_Running_Operations` (L320) | `Thread.Sleep(10)` + Stopwatch assert `< 120000ms` — phụ thuộc phần cứng | **Đã exclude khỏi CI** |

### Trạng thái CI hiện tại

**Tất cả 5 test trên ĐÃ được exclude khỏi CI** thông qua:
- `guard-check.ps1` line 113: `--filter "Category!=Performance&Category!=Integration&Category!=E2E"`
- `ci-full.ps1` line 131: `--filter "Category!=Performance&Category!=Integration&Category!=E2E"`
- `ci-local.ps1` line 18: cùng filter

`ProductionDataTests` class có `[Trait("Category", "Performance")]` ở class level → tất cả test trong class tự động bị exclude.

### Kế hoạch sửa Tier 3 (REVIEW_ONLY — chưa implement)

**Vấn đề root cause:** Unit test không nên đo absolute performance bằng `Stopwatch`/`GC.GetTotalMemory` — kết quả phụ thuộc phần cứng, load CPU, GC timing → flaky.

**Đề xuất tách thành `VanAn.Benchmarks` project riêng:**

1. **Tạo project mới:** `6_Tests\VanAn.Benchmarks\VanAn.Benchmarks.csproj`
   - Type: Console app (không phải test project)
   - Dependency: `BenchmarkDotNet` (industry standard cho .NET benchmarking)
   - KHÔNG tham gia CI pipeline (không trong `ci-full.ps1`, `ci-local.ps1`, `guard-check.ps1`)
   - Chạy manually: `dotnet run -c Release --project 6_Tests\VanAn.Benchmarks`

2. **Di chuyển 5 flaky tests** thành BenchmarkDotNet benchmarks:
   - `[Fact] Should_Handle_Large_Production_Dataset` → `[Benchmark] SyncLargeDataset_1000Orders`
   - `[Fact] Should_Handle_Production_Data_Volume_Spikes` → `[Benchmark] SyncVolumeSpike_100To1000`
   - `[Fact] Should_Handle_Production_Long_Running_Operations` → `[Benchmark] SyncLongRunning_500Orders`
   - `[Fact] Should_Handle_Production_Network_Conditions` → giữ logic retry verify, bỏ Stopwatch assert
   - `[Fact] Should_Handle_Production_Memory_Constraints` → `[Benchmark] MemoryUsage_Sync100Orders` (dùng BenchmarkDotNet memory diagnoser)

3. **Giữ lại trong Core.Tests** (không phải flaky):
   - `Should_Handle_Production_Order_Variety` — verify count, không dùng Stopwatch ✓
   - `Should_Handle_Production_Error_Rates` — verify callCount + result, không dùng Stopwatch ✓
   - `Should_Handle_Production_Concurrent_Load` — verify Times.AtLeast(500), không dùng Stopwatch ✓
   - `Should_Handle_Production_Data_Corruption` — verify no crash, không dùng Stopwatch ✓
   - `Should_Handle_Production_Data_Consistency` — verify data integrity, không dùng Stopwatch ✓

4. **Xóa `[Trait("Category", "Performance")]`** khỏi các test GIỮ LẠI (chỉ giữ cho các test thực sự đo performance — nhưng tất cả đều đã di chuyển, nên xóa trait hoàn toàn khỏi class).

**Lợi ích:**
- CI chạy nhanh + deterministic (không flaky)
- BenchmarkDotNet cung cấp statistical analysis (mean, median, stddev, outlier detection)
- Benchmark chạy riêng, có thể schedule nightly hoặc manual trước release
- Tách rõ: unit test = correctness, benchmark = performance

**Priority:** Thấp — CI đã exclude, không block development. Implement khi có time hoặc khi cần baseline performance cho optimization.

---

## Tier 4: Roslyn Analyzers Dead Code (2026-07-09)

**Location:** `VanAn.Accounting/VanAn.Accounting.Analyzers/`
**Severity:** Medium — compile-time guards không hoạt động, nhưng runtime tests + architecture tests vẫn enforce

### Vấn đề

9 Roslyn analyzers được build nhưng **không analyze code thật**:

| ID | Analyzer | Giá trị | Trạng thái |
|----|----------|---------|------------|
| VA1001 | DomainEntityLocationAnalyzer | Thấp (false positive) | Dead — wiring sai |
| VA1002 | DependencyDirectionAnalyzer | Cao (Clean Arch) | Dead — wiring sai + path separator bug |
| VA1003 | EfCoreInDomainAnalyzer | Cao (Domain purity) | Dead — wiring sai |
| VA1004 | BusinessLogicInGatewayAnalyzer | Thấp (outdated Option B) | Dead + outdated |
| VA1005 | AccountingEntryImmutabilityAnalyzer | Cao (ADR-003) | Dead — wiring sai |
| VA0001 | ImmutableAccountingAnalyzer | Cao (ADR-003) | Dead — wiring sai |
| VA0002 | ReversalOnlyAnalyzer | Cao (ADR-003) | Dead — wiring sai |
| VA0003 | NoBusinessInGatewayAnalyzer | Thấp (outdated Option B) | Dead + outdated |
| VA0004 | CentralPackageManagementAnalyzer | Cao (CPM) | Commented out |

### Root Cause

1. **Wiring sai:** `VanAn.Accounting.Analyzers.csproj` chỉ được reference bởi `VanAn.Accounting.csproj` (project gần như trống). Code thật nằm trong 1_Shared, 3_CoreHub, 2_Gateway, 5_WebApps — không reference analyzer.
2. **0 test:** Không có `VanAn.Accounting.Analyzers.Tests` project. Không có `DiagnosticVerifier` test.
3. **Path separator bug:** Hardcoded `/` trong `filePath.Contains("3_CoreHub/Services")` — fail trên Windows (`\`).
4. **3 analyzers outdated:** VA1004 + VA0003 (Gateway Option B = in-process, rule inverted 2026-07-05), VA1001 (naming pattern quá broad).

### Kế hoạch sửa (REVIEW_ONLY — chưa implement)

**Priority:** Thấp — Architecture Tests (Rule J/K/L) đã enforce accounting-online + PostgreSQL. Analyzer là compile-time guard bổ sung, không phải primary enforcement.

**Khi implement:**
1. Reference analyzer từ 1_Shared, 3_CoreHub, 2_Gateway, 5_WebApps csproj (`OutputItemType="Analyzer"`)
2. Fix path separator: dùng `Path.DirectorySeparatorChar` hoặc check cả `/` và `\`
3. Xóa VA1004, VA0003 (outdated Gateway Option B)
4. Refactor VA1001: dùng namespace check thay vì naming pattern
5. Enable VA0004 (CPM) — uncomment
6. Tạo `VanAn.Accounting.Analyzers.Tests` project với DiagnosticVerifier
7. Giữ VA1005, VA0001, VA0002 (immutability), VA1003 (Domain purity), VA1002 (dependency direction)

---

## Tier 5: True Offline Edge — Accounting via HTTP (2026-07-09)

**Location:** `5_WebApps/ShopERP/Program.cs` (conditional DI), `3_CoreHub/Services/Http/` (new), `2_Gateway/Controllers/` (new)
**Severity:** Low — hiện tại cả 3 compose files đều chạy PostgreSQL trên cùng máy, debt chưa trigger
**Task Card:** `docs/AI/tasks/true_offline_edge_accounting_http_task_card.md`

### Vấn đề

Wave 1 (commit `9d589bd`) split `IVanAnDbContext` → `IAccountingDbContext` để enforce ADR-001. ShopERP `Program.cs` luôn đăng ký `VanAnDbContext` với `UseNpgsql` — không có conditional logic.

Khi triển khai **true 2-server Edge mode** (ShopERP trên Server A không có PostgreSQL, Gateway + PostgreSQL trên Server B), `IAccountingDbContext` (UseNpgsql) sẽ throw khi resolve vì không kết nối được PostgreSQL.

### Giải pháp (Option C — Gateway HTTP API)

1. Tạo `AccountingHttpService : IAccountingDbContext` — gọi Gateway HTTP API thay vì direct DbContext
2. Conditional DI: `EdgeMode:TrueOffline` flag → register `AccountingHttpService`, else register `VanAnDbContext` (UseNpgsql)
3. Gateway expose accounting query HTTP endpoints (JournalEntries, AccountingEntries, etc.)
4. Disable accounting UI khi offline (KHÔNG trả empty data — vi phạm TT 152)

### 14 services/repos KHÔNG cần đổi

11 SWAP + 3 DUAL-INJECT files đã inject `IAccountingDbContext` (interface) — không quan tâm implementation là `VanAnDbContext` (direct) hay `AccountingHttpService` (HTTP). Đây là giá trị của interface segregation (Option B).

### Anti-patterns đã bác bỏ

| # | Anti-pattern | Lý do bác |
|---|-------------|-----------|
| 1 | Throw stub `IAccountingDbContext` | Option A rejected — crash runtime |
| 2 | Service Locator (`IServiceProvider`) | Ẩn dependencies, phá testability |
| 3 | Graceful degradation (return empty) | Vi phạm TT 152 — báo cáo sai |
| 4 | Null check boilerplate × 55 methods | Over-engineering cho kịch bản không tồn tại |

### Trigger

Khi có khách hàng cần edge node không có PostgreSQL (true offline). Hiện tại `docker-compose.edge.yml` chạy PostgreSQL trên cùng máy (`shoperp.depends_on: postgres: condition: service_healthy`).

### Estimated effort

5-8 sessions (4 phases: Gateway HTTP endpoints → AccountingHttpService + conditional DI → UI feature flag → tests)

---

## Chú thích

- **Fix (triệt để):** Đã sửa đúng root cause, không cần refactor sau
- **Workaround (Tier 1):** Tạm thời, ưu tiên sửa trong Hardening GĐ 2 (Tenant)
- **Workaround (Tier 2):** Tạm thời, ưu tiên sửa sau Tier 1 (Binding)
- **Flaky (Tier 3):** Đã exclude khỏi CI, cần tách thành Benchmark project riêng

---

## Review Checklist

- [ ] Tier 1: TenantId claim được thêm vào Login.cshtml.cs
- [ ] Tier 1: Fallback tenant blocks đã xóa khỏi TransactionHistory.razor
- [ ] Tier 1: Fallback tenant blocks đã xóa khỏi ExpenseEntry.razor
- [ ] Tier 2: JS Interop workaround đã xóa khỏi ExpenseEntry.razor
- [ ] Tier 2: JS helper đã xóa khỏi App.razor
- [ ] Tier 2: Form binding hoạt động chuẩn không cần JS fallback
- [ ] E2E Tests: Tất cả expense-entry-flow tests PASSED

---

**Người phê duyệt:** _________________  **Ngày:** _________________
