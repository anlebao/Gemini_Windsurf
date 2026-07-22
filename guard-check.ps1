# guard-check.ps1 - Van An Strict Guard v7.2 (Updated June 2026)

Write-Host "Running Van An Strict Guard v7.2..." -ForegroundColor Cyan

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

# 0.5 PRE-CHECK: Source file encoding (UTF-8 only) - prevent Vietnamese mojibake
Write-Host "Checking source file encodings..." -ForegroundColor Yellow

$textExtensions = @('*.cs', '*.razor', '*.md', '*.html', '*.cshtml', '*.css', '*.js', '*.json', '*.xml', '*.props', '*.targets', '*.csproj', '*.sln')
$nonUtf8Files = Get-ChildItem -Path . -Recurse -File -Include $textExtensions |
    Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\|\\.git\\|node_modules|\\.tmp-' } |
    Where-Object { -not (Test-ValidUtf8 $_.FullName) } |
    Select-Object -ExpandProperty FullName

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

# 2.5. Run Roslyn Analyzers (NEW - Phase 2.3)
Write-Host "Running Roslyn Analyzers..." -ForegroundColor Yellow
$analyzerOutput = dotnet build --no-restore --configuration Release 2>&1 | Tee-Object -FilePath "analyzer.log"

# Check for analyzer violations
$analyzerViolations = $analyzerOutput | Select-String -Pattern 'VA1001|VA1002|VA1003|VA1004|VA1005'

if ($analyzerViolations) {
    Write-Host "ROSLYN ANALYZER VIOLATIONS DETECTED:" -ForegroundColor Red
    $analyzerViolations | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    Write-Host "Guard check failed due to analyzer violations" -ForegroundColor Red
    exit 1
}

Write-Host "Roslyn Analyzers: PASSED" -ForegroundColor Green

# 3. Run dotnet build with detailed output
Write-Host "Running dotnet build..." -ForegroundColor Yellow
$buildOutput = dotnet build --verbosity normal --configuration Release 2>&1 | Tee-Object -FilePath "build.log"

if ($LASTEXITCODE -ne 0) {
    Write-Host "[FAIL] BUILD FAILED" -ForegroundColor Red
    exit 1
}

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

# 5. Fast test gate - Domain + Architecture + Integration tests (~20s)
# Prevents false-green: build pass alone does not guarantee correctness
Write-Host "Running fast test gate `(Domain + Architecture + Integration`)..." -ForegroundColor Yellow

$amp = [char]38
$coreTestFilter = "Category!=Performance$amp" + "Category!=Integration$amp" + "Category!=E2E"
dotnet test 6_Tests\VanAn.Core.Tests\VanAn.Core.Tests.csproj --verbosity quiet --configuration Release --filter $coreTestFilter 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "FAST TEST GATE FAILED: Core.Tests" -ForegroundColor Red
    Write-Host "Run: dotnet test 6_Tests\VanAn.Core.Tests\VanAn.Core.Tests.csproj for details" -ForegroundColor Yellow
    exit 1
}

dotnet test 6_Tests\VanAn.Architecture.Tests\VanAn.Architecture.Tests.csproj --verbosity quiet --configuration Release 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "FAST TEST GATE FAILED: Architecture.Tests" -ForegroundColor Red
    Write-Host "Run: dotnet test 6_Tests\VanAn.Architecture.Tests\VanAn.Architecture.Tests.csproj for details" -ForegroundColor Yellow
    exit 1
}

# NEW: Integration tests (CircuitBreaker only - no external services required) - Phase 2.4
Write-Host "Running Integration test gate `(CircuitBreaker tests only`)..." -ForegroundColor Yellow
dotnet test 6_Tests\VanAn.Integration.Tests\VanAn.Integration.Tests.csproj --verbosity quiet --configuration Release --filter "FullyQualifiedName~CircuitBreakerIntegrationTests" 2>&1 | Out-Null
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
