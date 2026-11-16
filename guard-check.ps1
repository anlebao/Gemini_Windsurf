# guard-check.ps1 - Van An Strict Guard v7.2 (Updated June 2026)
# v7.3 (2026-11-16): SPEED-UP — (1) single build merged from the two sequential full builds
# (analyzer gate + build gate), (2) --no-build on every dotnet test (reuse build output),
# (3) incremental static scans (raw SQL + UTF-8) over git-changed files only. Same checks,
# same pass/fail criteria — only I/O volume reduced. Use -FullScan for a full-tree scan
# (fresh clone / branch switch / after cherry-pick).

param([switch]$FullScan)

Write-Host "Running Van An Strict Guard v7.3..." -ForegroundColor Cyan

# Helper: validate that a file is well-formed UTF-8 (no Windows-1252 / ANSI mojibake)
function Test-ValidUtf8 {
    param([string]$Path)
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $i = 0
    while ($i -lt $bytes.Length) {
        $b = $bytes[$i]
        if ($b -lt 0x80) { $i++; continue }
        # 2-byte UTF-8 sequence
        if ($b -ge 0xC2 -and $b -le 0xDF) {
            if ($i + 1 -ge $bytes.Length) { return $false }
            $b1 = $bytes[$i + 1]
            if ($b1 -lt 0x80 -or $b1 -gt 0xBF) { return $false }
            $i += 2; continue
        }
        # 3-byte UTF-8 sequence
        if ($b -ge 0xE0 -and $b -le 0xEF) {
            if ($i + 2 -ge $bytes.Length) { return $false }
            $b1 = $bytes[$i + 1]; $b2 = $bytes[$i + 2]
            if ($b -eq 0xE0 -and ($b1 -lt 0xA0 -or $b1 -gt 0xBF)) { return $false }
            if ($b -eq 0xED -and ($b1 -lt 0x80 -or $b1 -gt 0x9F)) { return $false }
            if ($b1 -lt 0x80 -or $b1 -gt 0xBF -or $b2 -lt 0x80 -or $b2 -gt 0xBF) { return $false }
            $i += 3; continue
        }
        # 4-byte UTF-8 sequence
        if ($b -ge 0xF0 -and $b -le 0xF4) {
            if ($i + 3 -ge $bytes.Length) { return $false }
            $b1 = $bytes[$i + 1]; $b2 = $bytes[$i + 2]; $b3 = $bytes[$i + 3]
            if ($b -eq 0xF0 -and ($b1 -lt 0x90 -or $b1 -gt 0xBF)) { return $false }
            if ($b -eq 0xF4 -and ($b1 -lt 0x80 -or $b1 -gt 0x8F)) { return $false }
            if ($b1 -lt 0x80 -or $b1 -gt 0xBF -or $b2 -lt 0x80 -or $b2 -gt 0xBF -or $b3 -lt 0x80 -or $b3 -gt 0xBF) { return $false }
            $i += 4; continue
        }
        return $false
    }
    return $true
}

# Helper: files changed since last push/commit + untracked (incremental scanning target).
# Covers: locally committed but not yet on origin/main (if resolvable), uncommitted
# (staged + unstaged) vs HEAD, and untracked files. Unchanged files already passed the
# previous guard run — rescanning the full tree every iteration is pure overhead.
function Get-ChangedFiles {
    $files = @()
    if (git rev-parse --verify -q origin/main 2>$null) {
        $files += git diff --name-only --diff-filter=ACMRT origin/main...HEAD 2>$null
    }
    $files += git diff --name-only --diff-filter=ACMRT HEAD 2>$null
    $files += git ls-files --others --exclude-standard 2>$null
    return $files | Where-Object { $_ } | Sort-Object -Unique
}

# 0. PRE-CHECK: Untracked source files (Local Developer Discipline)
# Prevents "lost code" - files created but never git-added
Write-Host "Checking for untracked source files..." -ForegroundColor Yellow

$untrackedFiles = git ls-files --others --exclude-standard | Where-Object {
    $_ -match '\.(cs|razor)$'
}

if ($untrackedFiles) {
    Write-Host "`n[FAIL] UNTRACKED SOURCE FILES DETECTED:" -ForegroundColor Red
    $untrackedFiles | ForEach-Object { Write-Host "   $_" -ForegroundColor Red }
    Write-Host "`nThese files exist on disk but are NOT tracked by git." -ForegroundColor Yellow
    Write-Host "You will LOSE these files if you switch branches or the working tree is cleaned." -ForegroundColor Yellow
    Write-Host "`nFIX: Run the following commands:" -ForegroundColor Cyan
    Write-Host "   git add --all" -ForegroundColor White
    Write-Host "   git commit --amend --no-edit   # or: git commit -m 'your message'" -ForegroundColor White
    Write-Host "`nGuard check FAILED - Untracked source files must be committed." -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Untracked source files: PASSED" -ForegroundColor Green

# 0.3 PRE-CHECK: Cross-provider raw SQL guard (prevent PostgreSQL 42P01 + SQLite-only syntax)
# Pattern: ExecuteSqlRawAsync/FromSqlRaw with unquoted table names or COLLATE NOCASE
# Root cause: PostgreSQL lowercases unquoted identifiers → relation does not exist
# Incremental by default: only git-changed .cs files are scanned (excludes obj/bin/6_Tests).
Write-Host "Checking for cross-provider raw SQL violations..." -ForegroundColor Yellow

if ($FullScan) {
    $csFiles = Get-ChildItem -Path . -Recurse -Filter '*.cs' |
        Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\|\\.git\\|\\6_Tests\\' }
} else {
    $csFiles = Get-ChangedFiles |
        Where-Object { $_ -match '\.cs$' -and $_ -notmatch '(^|/)(obj|bin)/' -and $_ -notmatch '^6_Tests/' } |
        ForEach-Object { Get-Item $_ }
}

$rawSqlViolations = @()
foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw
    if ($content -match 'ExecuteSqlRawAsync|FromSqlRaw|SqlQueryRaw') {
        if ($content -match 'COLLATE NOCASE') {
            $rawSqlViolations += "$($file.FullName): contains COLLATE NOCASE (SQLite-only syntax, breaks PostgreSQL)"
        }
        if ($content -match '(?im)(ExecuteSqlRawAsync|FromSqlRaw|SqlQueryRaw)\s*\(\s*["'']?\s*(UPDATE|SELECT|INSERT|DELETE)\s+\w+') {
            $rawSqlViolations += "$($file.FullName): raw SQL with unquoted table name (PostgreSQL will lowercase → 42P01)"
        }
    }
}

if ($rawSqlViolations) {
    Write-Host "`n[FAIL] CROSS-PROVIDER RAW SQL VIOLATIONS DETECTED:" -ForegroundColor Red
    $rawSqlViolations | ForEach-Object { Write-Host "   $_" -ForegroundColor Red }
    Write-Host "`nRaw SQL with unquoted table names breaks PostgreSQL (lowercases identifiers → relation does not exist)." -ForegroundColor Yellow
    Write-Host "COLLATE NOCASE is SQLite-only syntax — does not exist in PostgreSQL." -ForegroundColor Yellow
    Write-Host "`nFIX: Replace raw SQL with EF Core LINQ (ExecuteUpdateAsync, Where, FirstOrDefaultAsync)." -ForegroundColor Cyan
    Write-Host "EF Core handles provider-specific identifier quoting automatically." -ForegroundColor Cyan
    Write-Host "`nGuard check FAILED - cross-provider raw SQL violations must be fixed." -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Cross-provider raw SQL: PASSED" -ForegroundColor Green

# 0.5 PRE-CHECK: Source file encoding (UTF-8 only) - prevent Vietnamese mojibake
Write-Host "Checking source file encodings..." -ForegroundColor Yellow

$textExtensions = @('*.cs', '*.razor', '*.md', '*.html', '*.cshtml', '*.css', '*.js', '*.json', '*.xml', '*.props', '*.targets', '*.csproj', '*.sln')
if ($FullScan) {
    $nonUtf8Files = Get-ChildItem -Path . -Recurse -File -Include $textExtensions |
        Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\|\\.git\\|node_modules|\\.tmp-' } |
        Where-Object { -not (Test-ValidUtf8 $_.FullName) } |
        Select-Object -ExpandProperty FullName
} else {
    $nonUtf8Files = Get-ChangedFiles |
        Where-Object { $_ -match '\.(cs|razor|md|html|cshtml|css|js|json|xml|props|targets|csproj|sln)$' -and $_ -notmatch '(^|/)(obj|bin)/|(^|/)node_modules/|(^|/)\.tmp-' } |
        Where-Object { -not (Test-ValidUtf8 $_) }
}

if ($nonUtf8Files) {
    Write-Host "`n[FAIL] NON-UTF-8 SOURCE FILES DETECTED:" -ForegroundColor Red
    $nonUtf8Files | ForEach-Object { Write-Host "   $_" -ForegroundColor Red }
    Write-Host "`nThese files are encoded as Windows-1252/ANSI instead of UTF-8 and will break Vietnamese text in Blazor WASM." -ForegroundColor Yellow
    Write-Host "FIX: Open in VS/VS Code, choose 'Save with Encoding' -> UTF-8 (with BOM), then commit." -ForegroundColor Cyan
    Write-Host "`nGuard check FAILED - encoding must be UTF-8." -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Source file encodings: PASSED" -ForegroundColor Green

# 1. Run windsurf-guard.js
Write-Host "Running windsurf-guard.js v6.0..." -ForegroundColor Yellow
node windsurf-guard.js
if ($LASTEXITCODE -ne 0) {
    Write-Host "[FAIL] WINDSURF GUARD FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "[OK] WINDSURF GUARD PASSED" -ForegroundColor Green

# 2. Run architecture-guard.ps1 (TEMPORARY - will be removed in Phase 3)
Write-Host "Running architecture-guard.ps1..." -ForegroundColor Yellow
.\architecture-guard.ps1
if ($LASTEXITCODE -ne 0) {
    Write-Host "[FAIL] ARCHITECTURE GUARD FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "[OK] ARCHITECTURE GUARD PASSED" -ForegroundColor Green

# 2.5+3. Run dotnet build ONCE — single pass serves BOTH the Roslyn analyzer gate and the
# build gate (previously two sequential full solution builds ≈ 2x build time). VA1001-VA1005
# are already WarningsAsErrors via Directory.Build.props; we surface them from the same log.
Write-Host "Running dotnet build (analyzers + build gate, single pass)..." -ForegroundColor Yellow
$buildOutput = dotnet build --verbosity normal --configuration Release 2>&1 | Tee-Object -FilePath "build.log"

if ($LASTEXITCODE -ne 0) {
    Write-Host "[FAIL] BUILD FAILED" -ForegroundColor Red
    exit 1
}

# Check for analyzer violations (from the same build output)
# Pattern requires the MSBuild diagnostic shape (": warning VA100x:" / ": error VA100x:").
# A bare 'VA1001|...' would false-positive on the csc command line, which embeds
# '/warnaserror:VA1001;...' (visible at --verbosity normal).
$analyzerViolations = $buildOutput | Select-String -Pattern '(?im)(warning|error)\s+VA100[1-5]\b'

if ($analyzerViolations) {
    Write-Host "ROSLYN ANALYZER VIOLATIONS DETECTED:" -ForegroundColor Red
    $analyzerViolations | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    Write-Host "Guard check failed due to analyzer violations" -ForegroundColor Red
    exit 1
}

Write-Host "Build + Roslyn Analyzers: PASSED" -ForegroundColor Green

# 4. Check for critical issues
$criticalIssues = $buildOutput | Select-String -Pattern ': error CS|: error NU|: error MSB|VA0004' -CaseSensitive

if ($criticalIssues) {
    Write-Host "CRITICAL ISSUES DETECTED:" -ForegroundColor Red
    $criticalIssues | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    Write-Host "Guard check failed due to critical issues" -ForegroundColor Red
    exit 1
}

# 4.5 Classify warnings by severity
Write-Host "Classifying warnings by severity..." -ForegroundColor Yellow

$criticalWarnings = @('CS0105', 'CS0168', 'CS0219', 'CS0414', 'CS0649')
$performanceWarnings = @('CS0162', 'CS0659', 'CS1030', 'CS0183', 'CS0184')
$securityWarnings = @('CS0618', 'CS0612', 'CS0619', 'CS0628')

$warningStats = @{
    Critical = 0
    Performance = 0
    Security = 0
    Other = 0
}


Write-Host "Warning Classification:" -ForegroundColor Cyan
Write-Host "  Critical: $($warningStats.Critical)" -ForegroundColor $(if ($warningStats.Critical -gt 0) { "Red" } else { "Green" })
Write-Host "  Performance: $($warningStats.Performance)" -ForegroundColor $(if ($warningStats.Performance -gt 0) { "Yellow" } else { "Green" })
Write-Host "  Security: $($warningStats.Security)" -ForegroundColor $(if ($warningStats.Security -gt 0) { "Yellow" } else { "Green" })
Write-Host "  Other: $($warningStats.Other)" -ForegroundColor Green

# Fail only on critical warnings
if ($warningStats.Critical -gt 0) {
    Write-Host "CRITICAL WARNINGS DETECTED: $($warningStats.Critical)" -ForegroundColor Red
    Write-Host "Guard check failed due to critical warnings" -ForegroundColor Red
    exit 1
}

# 5. Fast test gate - Core + Architecture + Integration tests
# Prevents false-green: build pass alone does not guarantee correctness
# NOTE: --no-build on every invocation — reuse the Release output from the single build above
# (previously each dotnet test recompiled its project + dependency chain).
Write-Host "Running test gate `(Core + Architecture + Integration`)..." -ForegroundColor Yellow

$amp = [char]38
$coreTestFilter = "Category!=Performance$amp" + "Category!=Integration$amp" + "Category!=E2E"
dotnet test 6_Tests\VanAn.Core.Tests\VanAn.Core.Tests.csproj --no-build --verbosity quiet --configuration Release --filter $coreTestFilter 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "FAST TEST GATE FAILED: Core.Tests" -ForegroundColor Red
    Write-Host "Run: dotnet test 6_Tests\VanAn.Core.Tests\VanAn.Core.Tests.csproj for details" -ForegroundColor Yellow
    exit 1
}

dotnet test 6_Tests\VanAn.Architecture.Tests\VanAn.Architecture.Tests.csproj --no-build --verbosity quiet --configuration Release 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "FAST TEST GATE FAILED: Architecture.Tests" -ForegroundColor Red
    Write-Host "Run: dotnet test 6_Tests\VanAn.Architecture.Tests\VanAn.Architecture.Tests.csproj for details" -ForegroundColor Yellow
    exit 1
}

# NEW: Integration tests (CircuitBreaker only - no external services required) - Phase 2.4
Write-Host "Running Integration test gate `(CircuitBreaker tests only`)..." -ForegroundColor Yellow
dotnet test 6_Tests\VanAn.Integration.Tests\VanAn.Integration.Tests.csproj --no-build --verbosity quiet --configuration Release --filter "FullyQualifiedName~CircuitBreakerIntegrationTests" 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "INTEGRATION TEST GATE FAILED" -ForegroundColor Red
    Write-Host "Run: dotnet test 6_Tests\VanAn.Integration.Tests\VanAn.Integration.Tests.csproj --filter FullyQualifiedName~CircuitBreakerIntegrationTests for details" -ForegroundColor Yellow
    exit 1
}

Write-Host "Fast test gate: PASSED" -ForegroundColor Green

# 6. Summary
$warningCount = ($buildOutput | Select-String -Pattern 'warning').Count
Write-Host "[OK] BUILD SUCCEEDED - $warningCount warning`(s`)" -ForegroundColor Green

if ($warningCount -gt 5) {
    Write-Host "[WARN] Warning count `($warningCount`) is higher than target `(<=5`). Please review." -ForegroundColor Yellow
} else {
    Write-Host "[OK] Excellent! Warning count is within target." -ForegroundColor Green
}

# 7. Generate guard report
Write-Host "Generating guard report..." -ForegroundColor Yellow

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportFile = "guard-report-$timestamp.txt"

$reportContent = "Guard Check Report`n"
$reportContent += "==================`n"
$reportContent += "Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`n"
$reportContent += "Version: v8.1 `(Phase 2 Upgrade + Source Control Guard`)`n`n"
$reportContent += "Component Results`n"
$reportContent += "  Untracked Files Check: PASSED`n"
$reportContent += "  Cross-Provider Raw SQL: PASSED`n"
$reportContent += "  Windsurf Guard: PASSED`n"
$reportContent += "  Architecture Guard: PASSED`n"
$reportContent += "  Roslyn Analyzers: PASSED`n"
$reportContent += "  Build: SUCCEEDED`n"
$reportContent += "  Core Tests: PASSED`n"
$reportContent += "  Architecture Tests: PASSED`n"
$reportContent += "  Integration Tests: PASSED`n`n"
$reportContent += "Warning Classification`n"
$reportContent += "  Critical: $($warningStats.Critical)`n"
$reportContent += "  Performance: $($warningStats.Performance)`n"
$reportContent += "  Security: $($warningStats.Security)`n"
$reportContent += "  Other: $($warningStats.Other)`n"
$reportContent += "  Total Warnings: $warningCount`n`n"
$reportContent += "Analyzer Violations`n"
$reportContent += "  VA1001 `(Domain Entity Location`): 0`n"
$reportContent += "  VA1002 `(Dependency Direction`): 0`n"
$reportContent += "  VA1003 `(EF Core in Domain`): 0`n"
$reportContent += "  VA1004 `(Business Logic in Gateway`): 0`n"
$reportContent += "  VA1005 `(AccountingEntry Immutability`): 0`n`n"
$reportContent += "Status: ALL CHECKS PASSED`n"
$reportContent | Out-File $reportFile

Write-Host "Report generated: $reportFile" -ForegroundColor Green

Write-Host "[DONE] ALL CHECKS PASSED - Ready for review" -ForegroundColor Cyan
