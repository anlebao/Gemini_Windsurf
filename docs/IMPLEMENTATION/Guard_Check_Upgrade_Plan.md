# Guard Check Upgrade Plan

**Version:** 1.0  
**Date:** 2026-06-06  
**Status:** Draft  
**Target:** guard-check.ps1 v7.1 → v8.0

## Executive Summary

Upgrade guard-check.ps1 from basic string matching to robust architecture enforcement using Roslyn Analyzers and proper CI integration. Plan spans 3 phases: short-term (quick wins), medium-term (infrastructure), long-term (native integration).

## Current State Analysis

### Existing Components

| Component | Technology | Issues | Reliability |
|---|---|---|---|
| windsurf-guard.js | Node.js + string matching | High false positives, no C# AST | Low |
| architecture-guard.ps1 | PowerShell + regex | Hardcoded patterns, not scalable | Medium |
| Build check | dotnet build | Good, but only checks compilation | High |
| Test gate | dotnet test (Core + Arch only) | Missing Integration/E2E tests | Medium |
| Warning count | Simple count | No severity classification | Low |

### CI Integration

- **Current:** Runs on `windows-latest` in ci.yml
- **Missing:** Node.js setup, docker-compose for services, Roslyn Analyzer integration
- **Artifacts:** Uploads guard-*.txt (but script doesn't generate these files)

## Phase 1: Short-Term Improvements (1-2 weeks)

**Goal:** Quick wins to improve reliability without major infrastructure changes

### 1.1 Improve windsurf-guard.js Regex

**Current Issue:**
```javascript
// Too broad - flags any 'new' or 'await'
if ((content.includes('new ') || content.includes('await ')) &&
    !content.includes('Inject') && !content.includes('_service'))
```

**Proposed Fix:**
```javascript
// More specific patterns
const businessLogicPatterns = [
    /new\s+[A-Z][a-zA-Z]+\s*\(/,  // Constructor calls
    /await\s+[^;]+\.ExecuteAsync/,  // Command execution
    /await\s+[^;]+\.HandleAsync/,   // Command handling
    /\.Add\(/,                      // Collection mutation
    /\.Update\(/,                   // Direct updates
];

const allowedPatterns = [
    /_service\./,                   // Service calls
    /_repository\./,                // Repository calls
    /ILogger/,                      // Logging
    /IMediator/,                    // Mediator pattern
];
```

**Tasks:**
- [ ] Refactor windsurf-guard.js with improved regex
- [ ] Add whitelist for allowed patterns
- [ ] Test against known false positives
- [ ] Add Node.js setup to CI guard-check job

**Files to modify:**
- `windsurf-guard.js`
- `.github/workflows/ci.yml` (add Node.js setup)

### 1.2 Enhance architecture-guard.ps1

**Current Issue:**
```powershell
# Hardcoded entity patterns
$domainEntityPatterns = @(
    'public record.*Entry\(',
    'public record.*Balance\(',
    ...
)
```

**Proposed Fix:**
```powershell
# Dynamic pattern discovery
$domainFile = "1_Shared/Domain.cs"
$domainContent = Get-Content $domainFile -Raw

# Extract all public record/class definitions from Domain.cs
$domainEntities = [regex]::Matches($domainContent, 'public (record|class)\s+(\w+)') | 
    ForEach-Object { $_.Groups[2].Value }

# Check if any domain entity is redefined in other layers
foreach ($entity in $domainEntities) {
    $pattern = "public (record|class)\s+$entity\b"
    # Check in Service and API layers
}
```

**Tasks:**
- [ ] Extract domain entities dynamically from 1_Shared/Domain.cs
- [ ] Remove hardcoded patterns
- [ ] Add check for DTOs in wrong layers
- [ ] Add check for using statements that violate dependency rules

**Files to modify:**
- `architecture-guard.ps1`

### 1.3 Warning Severity Classification

**Current Issue:**
```powershell
# Simple count, no severity
$warningCount = ($buildOutput | Select-String -Pattern "warning").Count
```

**Proposed Fix:**
```powershell
# Classify by warning code
$criticalWarnings = @('CS0105', 'CS0168', 'CS0219', 'CS0414')
$performanceWarnings = @('CS0162', 'CS0659', 'CS1030')
$securityWarnings = @('CS0618', 'CS0612')

$warningStats = @{
    Critical = 0
    Performance = 0
    Security = 0
    Other = 0
}

$buildOutput | Select-String -Pattern "warning (CS|NU)\d{4}" | ForEach-Object {
    $code = ($_ -match "warning (CS|NU)\d{4}")[1]
    if ($criticalWarnings -contains $code) { $warningStats.Critical++ }
    elseif ($performanceWarnings -contains $code) { $warningStats.Performance++ }
    elseif ($securityWarnings -contains $code) { $warningStats.Security++ }
    else { $warningStats.Other++ }
}

# Fail only on critical warnings
if ($warningStats.Critical -gt 0) {
    Write-Host "CRITICAL WARNINGS: $($warningStats.Critical)" -ForegroundColor Red
    exit 1
}
```

**Tasks:**
- [ ] Implement warning code classification
- [ ] Define warning severity tiers
- [ ] Update failure criteria (only critical warnings)
- [ ] Add warning report output

**Files to modify:**
- `guard-check.ps1`

### 1.4 Fix CI Artifacts

**Current Issue:**
```yaml
# Script doesn't generate guard-*.txt
- name: Upload Guard Results
  path: 'guard-*.txt'
```

**Proposed Fix:**
```powershell
# In guard-check.ps1, add:
$guardReport = "guard-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
@"
Guard Check Report
==================
Date: $(Get-Date)
Windsurf Guard: $windsurfResult
Architecture Guard: $architectureResult
Build: $buildResult
Tests: $testResult
Warnings: $warningStats
"@ | Out-File $guardReport
```

**Tasks:**
- [ ] Add report generation to guard-check.ps1
- [ ] Upload actual report file in CI
- [ ] Add report summary to GitHub Actions summary

**Files to modify:**
- `guard-check.ps1`
- `.github/workflows/ci.yml`

**Phase 1 Success Criteria:**
- False positives in windsurf-guard reduced by 50%
- Architecture guard scales with new domain entities
- CI uploads actual guard reports
- Warning classification prevents critical warnings

---

## Phase 2: Medium-Term Improvements (3-4 weeks)

**Goal:** Add infrastructure for proper testing and static analysis

### 2.1 Create Roslyn Analyzer for Architecture Rules

**Approach:** Replace architecture-guard.ps1 with Roslyn Analyzer

**Project Structure:**
```
VanAn.Accounting.Analyzers/
├── VanAn.Accounting.Analyzers.csproj
├── Analyzers/
│   ├── DomainEntityLocationAnalyzer.cs
│   ├── DependencyDirectionAnalyzer.cs
│   ├── EfCoreInDomainAnalyzer.cs
│   └── BusinessLogicInGatewayAnalyzer.cs
└── Resources/
    └── AnalyzerDescriptions.resx
```

**Analyzer Example:**
```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DomainEntityLocationAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        "VA1001",
        "Domain entity defined outside Domain layer",
        "Domain entities should only be defined in 1_Shared/Domain.cs",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics 
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.ClassDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.RecordDeclaration);
    }

    private void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var node = context.Node;
        var filePath = node.SyntaxTree.FilePath;
        
        // Check if file is in Domain layer
        if (!filePath.Contains("1_Shared") && IsDomainEntity(node))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, node.GetLocation()));
        }
    }

    private bool IsDomainEntity(SyntaxNode node)
    {
        // Check for naming patterns: Entry, Balance, Ledger, etc.
        var name = (node as TypeDeclarationSyntax)?.Identifier.Text;
        return name != null && (
            name.EndsWith("Entry") ||
            name.EndsWith("Balance") ||
            name.EndsWith("Ledger") ||
            name.EndsWith("Package"));
    }
}
```

**Tasks:**
- [ ] Create VanAn.Accounting.Analyzers project
- [ ] Implement DomainEntityLocationAnalyzer
- [ ] Implement DependencyDirectionAnalyzer
- [ ] Implement EfCoreInDomainAnalyzer
- [ ] Implement BusinessLogicInGatewayAnalyzer
- [ ] Add analyzer to VanAn.Accounting.csproj as development dependency
- [ ] Test analyzer on existing codebase
- [ ] Update CI to run analyzers

**Files to create:**
- `VanAn.Accounting/VanAn.Accounting.Analyzers/VanAn.Accounting.Analyzers.csproj`
- `VanAn.Accounting/VanAn.Accounting.Analyzers/Analyzers/*.cs`

**Files to modify:**
- `VanAn.Accounting/VanAn.Accounting.csproj`
- `.github/workflows/ci.yml`
- `guard-check.ps1` (remove architecture-guard.ps1 call)

### 2.2 Add Docker Compose for Integration Tests

**Current Issue:** Integration tests require services (SQLite, NATS) but CI doesn't start them

**Proposed Solution:** Create docker-compose.testing.yml

```yaml
version: '3.8'

services:
  sqlite:
    image: alpine:latest
    command: tail -f /dev/null
    volumes:
      - ./test-data:/data

  nats:
    image: nats:latest
    ports:
      - "4222:4222"
    command: -js

  redis:
    image: redis:latest
    ports:
      - "6379:6379"
```

**Tasks:**
- [ ] Create docker-compose.testing.yml
- [ ] Add test data initialization
- [ ] Update Integration.Tests to use service endpoints
- [ ] Add docker-compose setup to CI
- [ ] Add service health checks
- [ ] Configure connection strings for test environment

**Files to create:**
- `docker-compose.testing.yml`
- `.env.test` (update with service endpoints)

**Files to modify:**
- `6_Tests/VanAn.Integration.Tests/appsettings.test.json`
- `.github/workflows/ci.yml`
- `.github/workflows/pr-check.yml`

### 2.3 Add Integration Tests to Fast Gate

**Current Issue:** Fast gate only runs Core + Architecture tests

**Proposed Fix:**
```powershell
# In guard-check.ps1, add:
Write-Host "Running integration test gate..." -ForegroundColor Yellow

# Start services
docker-compose -f docker-compose.testing.yml up -d
Start-Sleep -Seconds 10  # Wait for services to be ready

# Run integration tests
dotnet test 6_Tests\VanAn.Integration.Tests\VanAn.Integration.Tests.csproj `
    --verbosity quiet --configuration Release `
    --filter "Category=Integration" 2>&1 | Out-Null

if ($LASTEXITCODE -ne 0) {
    Write-Host "INTEGRATION TEST GATE FAILED" -ForegroundColor Red
    docker-compose -f docker-compose.testing.yml down
    exit 1
}

docker-compose -f docker-compose.testing.yml down
Write-Host "Integration test gate: PASSED" -ForegroundColor Green
```

**Tasks:**
- [ ] Add integration test gate to guard-check.ps1
- [ ] Add service startup/shutdown logic
- [ ] Add timeout handling
- [ ] Test gate locally
- [ ] Update CI to allow docker-compose

**Files to modify:**
- `guard-check.ps1`
- `.github/workflows/ci.yml` (add docker-compose action)

**Phase 2 Success Criteria:**
- Roslyn Analyzer replaces architecture-guard.ps1
- Integration tests run in CI with proper services
- Fast gate includes integration tests
- Analyzer rules are enforced at compile time

---

## Phase 3: Long-Term Improvements (4-6 weeks)

**Goal:** Native build integration and comprehensive static analysis

### 3.1 Create Comprehensive Roslyn Analyzer Suite

**Expand Analyzer Coverage:**

| Analyzer ID | Rule | Severity | Description |
|---|---|---|---|
| VA1001 | DomainEntityLocation | Error | Domain entities only in 1_Shared/Domain.cs |
| VA1002 | DependencyDirection | Error | No upward dependencies |
| VA1003 | EfCoreInDomain | Error | No EF Core in Domain layer |
| VA1004 | BusinessLogicInGateway | Error | No business logic in Gateway |
| VA1005 | AccountingEntryImmutability | Error | AccountingEntry must be immutable |
| VA1006 | OutboxPatternCompliance | Warning | Outbox pattern must be used for critical operations |
| VA1007 | AsyncVoidAvoidance | Warning | Avoid async void |
| VA1008 | DisposePattern | Warning | Implement IDisposable correctly |
| VA1009 | CancellationTokenPropagation | Info | Propagate CancellationToken in async methods |

**Tasks:**
- [ ] Implement all analyzers in table
- [ ] Add code fixes for auto-correction
- [ ] Add unit tests for analyzers
- [ ] Document analyzer rules
- [ ] Create analyzer configuration file (.editorconfig)

**Files to create:**
- `VanAn.Accounting/VanAn.Accounting.Analyzers/Analyzers/*.cs`
- `VanAn.Accounting/VanAn.Accounting.Analyzers/CodeFixes/*.cs`
- `VanAn.Accounting/VanAn.Accounting.Analyzers/Tests/*.cs`
- `.editorconfig` (analyzer rules)

### 3.2 Native Build Integration

**Approach:** Remove guard-check.ps1, integrate analyzers into build

**Changes to Directory.Build.props:**
```xml
<PropertyGroup>
  <AnalysisMode>AllEnabledByDefault</AnalysisMode>
  <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  <WarningsAsErrors>VA1001;VA1002;VA1003;VA1004;VA1005</WarningsAsErrors>
  <WarningsNotAsErrors>VA1006;VA1007;VA1008;VA1009</WarningsNotAsErrors>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="VanAn.Accounting.Analyzers" Version="1.0.0" 
                    OutputItemType="Analyzer" 
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

**CI Changes:**
```yaml
# Remove guard-check job entirely
# Build job now includes analyzer checks
- name: Build with Analyzers
  run: dotnet build --configuration Release --verbosity normal
```

**Tasks:**
- [ ] Package analyzers as NuGet package
- [ ] Add analyzers to Directory.Build.props
- [ ] Configure severity levels
- [ ] Remove guard-check.ps1 from CI
- [ ] Update documentation
- [ ] Migrate existing rules to analyzers

**Files to modify:**
- `Directory.Build.props`
- `.github/workflows/ci.yml`
- `guard-check.ps1` (deprecate or remove)

### 3.3 Performance Optimization

**Current Issue:** Full guard check takes ~2-3 minutes

**Optimizations:**

1. **Incremental Analysis:**
   - Only analyze changed files
   - Use git diff to get changed file list
   - Pass file list to analyzers

2. **Parallel Execution:**
   - Run analyzers in parallel
   - Use MSBuild `/m` flag

3. **Caching:**
   - Cache analyzer results
   - Use Roslyn's incremental compilation

**Implementation:**
```powershell
# Incremental guard check
$changedFiles = git diff --name-only HEAD~1 HEAD
$changedCsFiles = $changedFiles | Where-Object { $_ -match '\.cs$' }

if ($changedCsFiles.Count -eq 0) {
    Write-Host "No C# files changed, skipping analysis"
    exit 0
}

# Run analyzers only on changed files
dotnet build /t:RunAnalyzers /p:ChangedFiles="$($changedCsFiles -join ';')"
```

**Tasks:**
- [ ] Implement incremental analysis
- [ ] Add parallel execution
- [ ] Add result caching
- [ ] Benchmark performance improvements
- [ ] Document performance characteristics

**Files to modify:**
- `guard-check.ps1` (if still used)
- CI workflows

### 3.4 Reporting and Visualization

**Current Issue:** Guard results are text-based, hard to visualize

**Proposed Solution:** Generate SARIF reports

**Implementation:**
```xml
<!-- In .csproj -->
<PropertyGroup>
  <ErrorLog>$(MSBuildThisFileDirectory)guard-results.sarif</ErrorLog>
</PropertyGroup>
```

**CI Integration:**
```yaml
- name: Upload SARIF Results
  uses: github/codeql-action/upload-sarif@v2
  with:
    sarif_file: guard-results.sarif
```

**Tasks:**
- [ ] Configure SARIF output
- [ ] Add SARIF upload to CI
- [ ] Create dashboard for guard results
- [ ] Add trend analysis
- [ ] Integrate with GitHub Security tab

**Files to modify:**
- `Directory.Build.props`
- `.github/workflows/ci.yml`

**Phase 3 Success Criteria:**
- guard-check.ps1 removed/deprecated
- All rules enforced via Roslyn Analyzers
- Build time < 2 minutes with incremental analysis
- SARIF reports integrated in GitHub
- Performance dashboard available

---

## Migration Strategy

### Rollout Plan

1. **Phase 1 Deployment:**
   - Deploy to feature branch
   - Test on existing codebase
   - Fix false positives
   - Merge to develop

2. **Phase 2 Deployment:**
   - Deploy Roslyn Analyzer alongside existing guards
   - Run both in parallel for validation
   - Compare results
   - Gradually phase out old guards

3. **Phase 3 Deployment:**
   - Enable analyzers in build
   - Deprecate guard-check.ps1
   - Update documentation
   - Remove old guard scripts

### Rollback Plan

- Keep old guard scripts in archive folder
- Feature flags to enable/disable analyzers
- CI parameter to choose guard implementation
- Revert CI workflow if issues arise

---

## Risk Assessment

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| Analyzer false positives block development | Medium | High | Phased rollout, severity tuning, whitelist |
| Docker-compose services unstable in CI | Medium | Medium | Health checks, retries, fallback to local |
| Performance regression | Low | Medium | Benchmarking, incremental analysis |
| Roslyn Analyzer learning curve | Medium | Low | Documentation, examples, training |

---

## Success Metrics

### Quantitative Metrics

- **False positive rate:** < 5% (from current ~30%)
- **Guard execution time:** < 2 minutes (from current ~3 minutes)
- **Test coverage:** Integration tests included in fast gate
- **Rule coverage:** 10+ analyzer rules (from current 6 rules)

### Qualitative Metrics

- Developer confidence in guard results
- Ease of adding new rules
- CI integration reliability
- Report clarity and actionability

---

## Timeline

| Phase | Duration | Start Date | End Date |
|---|---|---|---|
| Phase 1 | 2 weeks | Week 1 | Week 2 |
| Phase 2 | 4 weeks | Week 3 | Week 6 |
| Phase 3 | 6 weeks | Week 7 | Week 12 |

**Total Duration:** 12 weeks

---

## Dependencies

### External Dependencies
- Roslyn SDK
- Docker Compose
- GitHub Actions (SARIF upload)

### Internal Dependencies
- VanAn.Accounting.Analyzers project
- docker-compose.testing.yml
- Integration test infrastructure

---

## Resources

### Documentation
- [Roslyn Analyzer Documentation](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/)
- [SARIF Format](https://sarifweb.azurewebsites.net/)
- [GitHub Actions SARIF Upload](https://github.com/github/codeql-action/upload-sarif)

### Code References
- Microsoft.CodeAnalysis.Analyzers
- SonarAnalyzer.CSharp
- StyleCop.Analyzers

---

## Appendix A: Current Guard Rules Mapping

| Current Rule | Phase | New Implementation |
|---|---|---|
| windsurf-guard: Business logic in Gateway | Phase 1 | Improved regex → Phase 3: VA1004 |
| windsurf-guard: Hacks/suppression | Phase 1 | Keep as-is → Phase 3: VA1006 |
| windsurf-guard: AccountingEntry immutability | Phase 1 | Keep as-is → Phase 3: VA1005 |
| architecture-guard: Domain entities in Service | Phase 2 | VA1001 |
| architecture-guard: Domain entities in API | Phase 2 | VA1001 |
| architecture-guard: Dependency directions | Phase 2 | VA1002 |
| architecture-guard: EF Core in Domain | Phase 2 | VA1003 |
| Build check | Phase 1 | Keep as-is |
| Test gate (Core + Arch) | Phase 1 | Keep as-is → Phase 2: Add Integration |
| Warning count | Phase 1 | Classification → Phase 3: Native |

---

## Appendix B: CI Workflow Changes

### Current CI Jobs
```
build (ubuntu)
architecture-tests (ubuntu)
guard-check (windows) ← Current implementation
code-quality (ubuntu)
```

### Target CI Jobs (Phase 3)
```
build (ubuntu) ← Includes analyzers
unit-tests (ubuntu)
integration-tests (ubuntu) ← With docker-compose
e2e-tests (ubuntu) ← Separate workflow
```

---

## Approval

**Reviewed by:**  
**Approved by:**  
**Date:**  
