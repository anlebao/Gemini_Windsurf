# TASK CARD: Accounting PostgreSQL Online — Wave 3 — Architecture Tests + Existing Tests + Verify

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Add 4 Architecture Tests (Rule J/K/L/M) để enforce accounting-online, fix existing test mocks, full verification
- **Nghiệp vụ áp dụng:** ADR-001 compliance enforcement — đảm bảo vi phạm không tái diễn
- **Status:** ⏳ PENDING — Wave 1 ✅ complete, Wave 2 🟡 partial (docker-compose pending), Wave 3 awaiting Wave 2 completion
- **Branch:** `feature/accounting-pg-wave3-tests-verify`
- **Estimated Sessions:** 1-2

> **CONTEXT UPDATE:** Wave 1 merged Wave 2 service-swap work (user approved). 3 test files already fixed in Wave 1 (PeriodClosingPersistenceTests, VasFeatureFlagTests, SmartPreAggregationServiceWave2Tests). Wave 3 scope reduced — remaining test mocks may be fewer than originally estimated.

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 3 of 3
- **Dependency:** Wave 2 complete (docker-compose config done — W2-T6 residual pending)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/accounting_postgresql_online_master_plan.md` (READ)
- `6_Tests/VanAn.Architecture.Tests/ArchitectureRulesTests.cs` (MODIFY — add Rule J/K/L/M)
- `6_Tests/VanAn.Core.Tests/` (MODIFY — fix mocks cho accounting tests)
- `6_Tests/VanAn.Integration.Tests/` (MODIFY — fix mocks cho accounting tests)
- `6_Tests/VanAn.ShopERP.Tests/` (MODIFY — verify accounting services inject IAccountingDbContext)
- `docs/AI/project_state.md` (MODIFY — update hard stop description)

### Boundary Rules
- KHÔNG sửa `1_Shared/Domain.cs`
- KHÔNG sửa implementation code (services/repos) — chỉ sửa tests
- KHÔNG tạo throw stubs
- KHÔNG chạy Playwright (DISABLED)

---

## 4. TECHNICAL CONSTRAINTS
- [ ] **Domain Protection:** KHÔNG sửa Domain.cs
- [ ] **Architecture Tests:** File-based string check (consistent với existing Rule A-H pattern)
- [ ] **Test Mocks:** Mock `IAccountingDbContext` cho accounting tests, giữ `IVanAnDbContext` mock cho business tests
- [ ] **Green phase:** Tất cả Rule J/K/L/M phải PASS sau khi Wave 1+2 complete

---

## 5. SUCCESS CRITERIA
- [ ] **SC1:** Rule J PASS — accounting services inject `IAccountingDbContext`
- [ ] **SC2:** Rule K PASS — `ShopERPDbContext` has no accounting DbSets
- [ ] **SC3:** Rule L PASS — docker-compose has `AccountingConnection` (PostgreSQL)
- [ ] **SC4:** Rule M PASS — ShopERP `Program.cs` registers `IAccountingDbContext` with `UseNpgsql`
- [ ] **SC5:** All existing tests pass (after mock updates)
- [ ] **SC6:** `dotnet build VanAn.sln` → 0 errors
- [ ] **SC7:** `scripts/guard-check.ps1` → PASS
- [ ] **SC8:** `project_state.md` updated

---

## 6. DETAILED IMPLEMENTATION

### 6.1. Rule J — Accounting services inject IAccountingDbContext (W3-T1)

**File:** `6_Tests/VanAn.Architecture.Tests/ArchitectureRulesTests.cs`

```csharp
[Fact(DisplayName = "Rule J: ADR-001 - Accounting services MUST inject IAccountingDbContext (PostgreSQL)")]
public void AccountingServices_MustInject_IAccountingDbContext()
{
    var repoRoot = GetRepoRoot();
    var servicesPath = Path.Combine(repoRoot, "3_CoreHub", "Services");
    var reposPath = Path.Combine(repoRoot, "3_CoreHub", "Repositories");

    // Services + repos that MUST inject IAccountingDbContext
    var accountingFiles = new[]
    {
        // Repositories (direct DbContext injection)
        Path.Combine(reposPath, "AccountingEntryRepository.cs"),
        Path.Combine(reposPath, "AuditLogRepository.cs"),
        Path.Combine(reposPath, "HKDBookRepository.cs"),
        // Services (direct DbContext injection)
        Path.Combine(servicesPath, "PeriodClosingService.cs"),
        Path.Combine(servicesPath, "BalanceSheetService.cs"),
        Path.Combine(servicesPath, "IncomeStatementService.cs"),
        Path.Combine(servicesPath, "CashFlowStatementService.cs"),
        Path.Combine(servicesPath, "TrialBalanceService.cs"),
        Path.Combine(servicesPath, "AccountChartService.cs"),
        Path.Combine(servicesPath, "PreAggregation", "SmartPreAggregationService.cs"),
    };

    var violations = new List<string>();
    foreach (var filePath in accountingFiles)
    {
        if (!File.Exists(filePath)) continue;
        var content = File.ReadAllText(filePath);
        if (!content.Contains("IAccountingDbContext"))
            violations.Add($"{Path.GetFileName(filePath)}: missing IAccountingDbContext injection");
    }

    Assert.True(violations.Count == 0,
        "ADR-001 violation: Accounting services/repos must inject IAccountingDbContext (PostgreSQL, online).\n" +
        string.Join("\n", violations));
}
```

**Note:** KHÔNG include `AccountingEntryService.cs`, `ReversalService.cs`, `AuditTrailService.cs`, `HKDBookService.cs` — chúng inject **repositories**, không inject DbContext trực tiếp.

### 6.2. Rule K — ShopERPDbContext has no accounting DbSets (W3-T2)

```csharp
[Fact(DisplayName = "Rule K: ADR-001 - ShopERPDbContext (SQLite) MUST NOT contain accounting DbSets")]
public void ShopERPDbContext_MustNotContain_AccountingDbSets()
{
    var repoRoot = GetRepoRoot();
    var dbContextPath = Path.Combine(repoRoot, "5_WebApps", "ShopERP", "Infrastructure", "ShopERPDbContext.cs");

    if (!File.Exists(dbContextPath))
        Assert.Fail($"ShopERPDbContext.cs not found: {dbContextPath}");

    var content = File.ReadAllText(dbContextPath);

    var forbiddenDbSets = new[]
    {
        "DbSet<AccountingEntry>",
        "DbSet<JournalEntry>",
        "DbSet<AuditLog>",
        "DbSet<PendingInvoiceQueue>",
        "DbSet<AccountChartEntity>",
        "DbSet<PeriodClosingStatusEntity>",
    };

    var violations = new List<string>();
    foreach (var dbSet in forbiddenDbSets)
    {
        if (content.Contains(dbSet))
            violations.Add($"Found accounting DbSet in SQLite context: {dbSet}");
    }

    Assert.True(violations.Count == 0,
        "ADR-001 violation: ShopERPDbContext (SQLite) must not contain accounting DbSets.\n" +
        string.Join("\n", violations));
}
```

**Note:** Option B = no throw stubs, nên check đơn giản — chỉ cần string contains. Không cần line-by-line logic như Option A.

### 6.3. Rule L — docker-compose has AccountingConnection (W3-T3)

```csharp
[Fact(DisplayName = "Rule L: ADR-001 - docker-compose ShopERP MUST have AccountingConnection (PostgreSQL)")]
public void DockerCompose_ShopERP_MustHave_AccountingConnection()
{
    var repoRoot = GetRepoRoot();
    var composeFiles = new[]
    {
        Path.Combine(repoRoot, "docker-compose.yml"),
        Path.Combine(repoRoot, "docker-compose.prod.yml"),
    };

    foreach (var composeFile in composeFiles)
    {
        if (!File.Exists(composeFile)) continue;
        var content = File.ReadAllText(composeFile);

        Assert.True(content.Contains("AccountingConnection"),
            $"ADR-001 violation: {Path.GetFileName(composeFile)} must have AccountingConnection env var. " +
            "Accounting is always online on PostgreSQL.");

        Assert.True(content.Contains("Host=postgres") || content.Contains("postgres:5432"),
            $"ADR-001 violation: {Path.GetFileName(composeFile)} must reference PostgreSQL host for accounting.");
    }
}
```

### 6.4. Rule M — ShopERP Program.cs registers IAccountingDbContext with UseNpgsql (W3-T4)

```csharp
[Fact(DisplayName = "Rule M: ADR-001 - ShopERP Program.cs MUST register IAccountingDbContext with UseNpgsql")]
public void ShopERP_ProgramCs_MustRegister_IAccountingDbContext_Npgsql()
{
    var repoRoot = GetRepoRoot();
    var programCsPath = Path.Combine(repoRoot, "5_WebApps", "ShopERP", "Program.cs");

    if (!File.Exists(programCsPath))
        Assert.Fail($"Program.cs not found: {programCsPath}");

    var content = File.ReadAllText(programCsPath);

    Assert.True(content.Contains("IAccountingDbContext"),
        "ADR-001 violation: ShopERP Program.cs must register IAccountingDbContext.");

    Assert.True(content.Contains("UseNpgsql"),
        "ADR-001 violation: ShopERP Program.cs must call UseNpgsql for accounting DbContext.");
}
```

### 6.5. Fix Existing Tests (W3-T5)

**Audit affected tests:**
```bash
# Find tests that mock IVanAnDbContext for accounting operations
rg "IVanAnDbContext" 6_Tests/ --type cs -l
```

**Pattern fix:**
- Tests cho accounting services (BalanceSheet, IncomeStatement, etc.): đổi mock `IVanAnDbContext` → mock `IAccountingDbContext`
- Tests cho business services (OrderService, etc.): giữ mock `IVanAnDbContext`
- Tests cho dual-inject services (TenantConversion, HKDBookGeneration): mock cả 2

**Mock setup pattern:**
```csharp
// Before:
var mockContext = new Mock<IVanAnDbContext>();
mockContext.Setup(c => c.JournalEntries).Returns(MockDbSet<JournalEntry>());

// After (accounting test):
var mockAccountingContext = new Mock<IAccountingDbContext>();
mockAccountingContext.Setup(c => c.JournalEntries).Returns(MockDbSet<JournalEntry>());
```

**Integration tests:** Nếu test tạo `ShopERPDbContext` in-memory và query accounting → cần tạo separate `VanAnDbContext` in-memory hoặc mock `IAccountingDbContext`.

### 6.6. Full Verification (W3-T6)

| Step | Command | Expected |
|------|---------|----------|
| 1 | `dotnet build VanAn.sln` | 0 errors |
| 2 | `scripts/guard-check.ps1` | PASS |
| 3 | `dotnet test 6_Tests/VanAn.Architecture.Tests` | Rule J/K/L/M PASS |
| 4 | `dotnet test 6_Tests/VanAn.Core.Tests --filter "Category!=Performance"` | All PASS |
| 5 | `dotnet test 6_Tests/VanAn.Integration.Tests` | All PASS |

### 6.7. Update project_state.md (W3-T7)

**Section 1, Hard stops line 27:**
```
// Before:
ShopERP SQLite (Business) + PostgreSQL (Accounting)
// After (clarify):
ShopERP SQLite (Business) + PostgreSQL (Accounting) — ADR-001 enforced via IAccountingDbContext
```

**Section 5, Active Architecture Decisions:**
```
| ShopERP = SQLite (Business) + PostgreSQL (Accounting) | ADR-001: accounting always online. ShopERPDbContext (SQLite) cho Business/Platform, VanAnDbContext (PostgreSQL) cho Accounting qua IAccountingDbContext. ✅ ENFORCED 2026-07-09 |
```

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 4
- **Verified Facts:**
  - Fact 1: Architecture Tests file exists at `6_Tests/VanAn.Architecture.Tests/ArchitectureRulesTests.cs`
  - Fact 2: Existing tests use `Mock<IVanAnDbContext>` pattern (47 files reference IVanAnDbContext)
  - Fact 3: Option B = no throw stubs → Rule K check đơn giản (string contains)
  - Fact 4: Rule J phải exclude services inject repos (AccountingEntryService, ReversalService, AuditTrailService, HKDBookService)
- **Assumptions:**
  - Số tests affected: 10-30 (cần audit)
  - Integration tests có thể cần larger refactor (ShopERPDbContext in-memory + accounting)
- **Open Questions:**
  - Q1: Có integration test nào tạo ShopERPDbContext in-memory và query accounting không? (Cần audit)
  - Q2: Số lượng tests affected chính xác? (Cần grep + count)

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| ArchitectureRulesTests.cs (add 4 rules) | No impact — new tests | None |
| Core Tests (fix mocks) | Test compilation break | Fix mock setup per service type |
| Integration Tests | Test setup change | May need separate DbContext for accounting |
| project_state.md | Documentation update | None |

---

## 9. TDD & TESTING STRATEGY
- **Architecture tests:** Rule J/K/L/M — green phase (pass sau Wave 1+2)
- **Unit tests:** Fix mocks — accounting tests mock `IAccountingDbContext`, business tests giữ `IVanAnDbContext`
- **Integration tests:** Fix DbContext setup — accounting queries dùng `IAccountingDbContext`
- **E2E tests:** Out of scope
- **Playwright:** DISABLED
- **Verification:** Full build + guard-check + all test suites pass
