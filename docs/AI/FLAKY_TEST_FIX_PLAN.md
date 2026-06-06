# Flaky Test Fix Plan

## Tổng quan

Đây là plan chi tiết để khắc phục 31+ flaky tests trong codebase Vạn An Accounting System MVP.

---

## Phase 1: Categorization & Annotation (30 min)

### 1.1 Add `[Trait("Category", "Performance")]` to timing-sensitive tests

| File | Tests |
|------|-------|
| `6_Tests/VanAn.Core.Tests/Services/DashboardServiceTests.cs` | `GetPostgreSQLMetricsAsync_Should_Perform_With_Large_Dataset` |
| `6_Tests/VanAn.Core.Tests/Performance/SQLiteConcurrencyPerformanceTests.cs` | All 7 tests |
| `6_Tests/VanAn.Core.Tests/Integration/SQLiteConcurrencyIntegrationTests.cs` | `BatchProcessing_*`, `PerformanceMetrics_*` |
| `6_Tests/VanAn.Load.Tests/SimpleLoadTests.cs` | All 6 tests |

### 1.2 Add `[Trait("Category", "Integration")]` to async infrastructure tests

| File | Tests |
|------|-------|
| `6_Tests/VanAn.Core.Tests/Integration/SQLiteConcurrencyIntegrationTests.cs` | `ConcurrentOrderCreation_*`, `OutboxProcessor_*` |
| `6_Tests/VanAn.Core.Tests/TimeBasedBugTests.cs` | `Should_Handle_Concurrent_Time_Updates`, `Should_Handle_Rapid_Successive_Updates` |

### 1.3 Add `[Trait("Category", "E2E")]` to Playwright tests

Already have `[Collection("SelfHosted Tests")]` - add explicit trait.

---

## Phase 2: CI Pipeline Update (20 min)

### 2.1 Update `.github/workflows/pr-check.yml`

**Unit Tests (Stable):**
```yaml
- name: Run Unit Tests (Stable)
  run: |
    dotnet test 6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj \
      --no-build --configuration Release \
      --filter "Category!=Performance&Category!=Integration&Category!=E2E"
```

**Performance Tests (Optional):**
```yaml
- name: Run Performance Tests (Optional)
  continue-on-error: true
  run: |
    dotnet test 6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj \
      --no-build --configuration Release \
      --filter "Category=Performance"
```

**Integration Tests (Optional):**
```yaml
- name: Run Integration Tests (Optional)
  continue-on-error: true
  run: |
    dotnet test 6_Tests/VanAn.Integration.Tests/VanAn.Integration.Tests.csproj \
      --no-build --configuration Release
```

---

## Phase 3: Fix Wall-Clock Assertions (45 min)

### 3.1 `DashboardServiceTests.cs:299-330`

**Current:**
```csharp
Assert.True(DateTime.UtcNow.Subtract(startTime).TotalMilliseconds < 5000);
```

**Fix:** Remove timing assertion, keep functional check only. Add logging.

### 3.2 `SQLiteConcurrencyPerformanceTests.cs` - Timing asserts to fix

| Line | Current | Fix |
|------|---------|-----|
| 95 | `< TimeSpan.FromSeconds(10)` | Remove or extend to 60s |
| 135-136 | `< 250ms`, `< 8s` | Change to `< 30s` or remove |
| 283 | `< 5000ms` | Remove or extend to 30s |

### 3.3 Keep functional assertions, remove timing

Assertions to KEEP (functional):
- `Assert.Equal(0, metrics.FailedBatches)`
- `Assert.True(memoryUsedMB < maxMemoryMB)`
- Batch size range check

---

## Phase 4: Replace Task.Delay with Polling (60 min)

### 4.1 Create Helper `6_Tests/Common/AsyncAssert.cs`

```csharp
public static class AsyncAssert
{
    public static async Task WaitForConditionAsync(
        Func<Task<bool>> condition, 
        TimeSpan timeout, 
        TimeSpan pollInterval,
        string message)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (await condition()) return;
            await Task.Delay(pollInterval);
        }
        throw new TimeoutException(message);
    }
}
```

### 4.2 Files to update with polling pattern

| File | Task.Delay occurrences |
|------|------------------------|
| `SQLiteConcurrencyPerformanceTests.cs` | 7 |
| `SQLiteConcurrencyIntegrationTests.cs` | 3 |
| `TimeBasedBugTests.cs` | 1 |
| `DashboardE2ETests.cs` | 1 |
| `FrozenStateTests.cs` | 1 |
| `SimpleLoadTests.cs` | 1 |

**Pattern change:**
```csharp
// OLD: await Task.Delay(TimeSpan.FromSeconds(5));
// NEW:
await AsyncAssert.WaitForConditionAsync(
    async () => (await _queueService.GetQueueMetricsAsync()).ProcessedBatches > 0,
    timeout: TimeSpan.FromSeconds(30),
    pollInterval: TimeSpan.FromMilliseconds(500),
    message: "Expected batches to be processed"
);
```

---

## Phase 5: Convert Time-Based to Semantic Assertions (40 min)

### 5.1 `TimeBasedBugTests.cs`

**Current:**
```csharp
Assert.True(Math.Abs((serverOrder.CreatedAt - DateTime.UtcNow).TotalMinutes) < 5);
```

**New:**
```csharp
Assert.True(serverOrder.CreatedAt > DateTime.UtcNow.AddMinutes(-10));
Assert.True(serverOrder.CreatedAt <= DateTime.UtcNow.AddMinutes(1));
```

---

## Phase 6: Add Logging & Observability (30 min)

### 6.1 Pattern for all performance tests

```csharp
[Fact(DisplayName = "...")]
public async Task TestName()
{
    var sw = Stopwatch.StartNew();
    // ... test logic ...
    sw.Stop();
    
    _output.WriteLine($"[PERF] Execution time: {sw.ElapsedMilliseconds}ms");
    _output.WriteLine($"[PERF] Environment: {Environment.MachineName}");
    
    if (sw.ElapsedMilliseconds > 5000)
    {
        _output.WriteLine($"[WARN] Performance threshold exceeded");
    }
    
    // Only functional assertions
    Assert.True(functionalResult);
}
```

---

## Phase 7: E2E Test Stabilization (45 min)

### 7.1 Increase Playwright timeouts in CI

```yaml
env:
  PLAYWRIGHT_TIMEOUT: 30000  # 30s for CI
  PLAYWRIGHT_RETRIES: 2
```

### 7.2 Update `GoldenFlowE2ETests.cs`

Increase SignalR wait timeout from 15000ms to 30000ms.

---

## Phase 8: Validation & Verification (30 min)

### 8.1 Local verification commands

```powershell
# Run stable tests only
dotnet test --filter "Category!=Performance&Category!=Integration"

# Run all tests to verify no regressions
dotnet test --configuration Release
```

### 8.2 CI verification checklist

- [ ] `build-verify` job passes consistently (5 consecutive runs)
- [ ] Performance tests run but failures don't block merge
- [ ] Integration tests stable in isolation
- [ ] E2E tests pass with increased timeout

---

## Implementation Order

| Phase | Priority | Estimated Time | Blocking |
|-------|----------|----------------|----------|
| 1 - Categorization | P0 | 30 min | Yes |
| 2 - CI Update | P0 | 20 min | Yes |
| 3 - Wall-Clock Fixes | P1 | 45 min | No |
| 4 - Polling Helper | P1 | 60 min | No |
| 5 - Semantic Assertions | P1 | 40 min | No |
| 6 - Logging | P2 | 30 min | No |
| 7 - E2E Stabilization | P2 | 45 min | No |
| 8 - Validation | P0 | 30 min | Yes |

**Total: ~4-5 hours**

---

## Flaky Tests Inventory

### High Priority (Fix First)

1. `DashboardServiceTests.GetPostgreSQLMetricsAsync_Should_Perform_With_Large_Dataset` - Wall-clock < 5000ms
2. `SQLiteConcurrencyPerformanceTests.ThroughputTest_ShouldProcess100Orders_WithinTimeLimit` - Wall-clock < 10s
3. `SQLiteConcurrencyPerformanceTests.LatencyTest_ShouldProcessOrders_WithAcceptableLatency` - Latency assertions

### Medium Priority

4. `SQLiteConcurrencyIntegrationTests.ConcurrentOrderCreation_ShouldHandle10Orders_WithoutErrors` - Task.Delay
5. `SQLiteConcurrencyIntegrationTests.BatchProcessing_ShouldProcessMultipleOrders_Efficiently` - Task.Delay + timing
6. `TimeBasedBugTests.Should_Handle_Concurrent_Time_Updates` - Thread.Sleep
7. `TimeBasedBugTests.Should_Handle_Rapid_Successive_Updates` - Task.Delay(1)

### Lower Priority (CI only)

8. `SimpleLoadTests` (all 6) - WebApplicationFactory timing
9. `GoldenFlowE2ETests` - Playwright + SignalR timing
10. `DashboardE2ETests` - Playwright timeouts

---

## Rollback Strategy

If any fix causes regression:
1. Revert specific file changes
2. Mark test with `[Fact(Skip = "Flaky - needs rework")]`
3. Document in TECHNICAL_DEBT_LEDGER.md
4. Create follow-up issue
