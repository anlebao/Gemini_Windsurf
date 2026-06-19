# guard-check.ps1 - Van An Strict Guard v7.2 (Updated June 2026)

Write-Host "Running Van An Strict Guard v7.2..." -ForegroundColor Cyan

# 0. PRE-CHECK: Untracked source files (Local Developer Discipline)
# Prevents "lost code" - files created but never git-added
Write-Host "Checking for untracked source files..." -ForegroundColor Yellow

$untrackedFiles = git ls-files --others --exclude-standard | Where-Object {
    $_ -match '\.(cs|razor)$'
}

if ($untrackedFiles) {
    Write-Host "`nâŒ UNTRACKED SOURCE FILES DETECTED:" -ForegroundColor Red
    $untrackedFiles | ForEach-Object { Write-Host "   $_" -ForegroundColor Red }
    Write-Host "`nThese files exist on disk but are NOT tracked by git." -ForegroundColor Yellow
    Write-Host "You will LOSE these files if you switch branches or the working tree is cleaned." -ForegroundColor Yellow
    Write-Host "`nFIX: Run the following commands:" -ForegroundColor Cyan
    Write-Host "   git add --all" -ForegroundColor White
    Write-Host "   git commit --amend --no-edit   # or: git commit -m 'your message'" -ForegroundColor White
    Write-Host "`nGuard check FAILED - Untracked source files must be committed." -ForegroundColor Red
    exit 1
}

Write-Host "âœ“ Untracked source files: PASSED" -ForegroundColor Green

# 1. Run windsurf-guard.js
Write-Host "Running windsurf-guard.js v6.0..." -ForegroundColor Yellow
node windsurf-guard.js
if ($LASTEXITCODE -ne 0) {
    Write-Host "Ã¢ WINDSURF GUARD FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "Ã¢ WINDSURF GUARD PASSED" -ForegroundColor Green

# 2. Run architecture-guard.ps1 (TEMPORARY - will be removed in Phase 3)
Write-Host "Running architecture-guard.ps1..." -ForegroundColor Yellow
.\architecture-guard.ps1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Ã¢ ARCHITECTURE GUARD FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "Ã¢ ARCHITECTURE GUARD PASSED" -ForegroundColor Green

# 2.5. Run Roslyn Analyzers (NEW - Phase 2.3)
Write-Host "Running Roslyn Analyzers..." -ForegroundColor Yellow
$analyzerOutput = dotnet build --no-restore --configuration Release 2>&1 | Tee-Object -FilePath "analyzer.log"

# Check for analyzer violations
$analyzerViolations = $analyzerOutput | Select-String -Pattern "VA1001|VA1002|VA1003|VA1004|VA1005"

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
    Write-Host "Ã¢ BUILD FAILED" -ForegroundColor Red
    exit 1
}

# 4. Check for critical issues
$criticalIssues = $buildOutput | Select-String -Pattern ": error CS|: error NU|: error MSB|VA0004" -CaseSensitive

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

$buildOutput | Select-String -Pattern "warning (CS|NU)\d{4}" | ForEach-Object {
    if ($_ -match "warning (CS|NU)(\d{4})") {
        $code = "$($Matches[1])$($Matches[2])"
        if ($criticalWarnings -contains $code) { 
            $warningStats.Critical++ 
        } elseif ($performanceWarnings -contains $code) { 
            $warningStats.Performance++ 
        } elseif ($securityWarnings -contains $code) { 
            $warningStats.Security++ 
        } else { 
            $warningStats.Other++ 
        }
    }
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
Write-Host "Running fast test gate (Domain + Architecture + Integration)..." -ForegroundColor Yellow

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
Write-Host "Running Integration test gate (CircuitBreaker tests only)..." -ForegroundColor Yellow
dotnet test 6_Tests\VanAn.Integration.Tests\VanAn.Integration.Tests.csproj --verbosity quiet --configuration Release --filter "FullyQualifiedName~CircuitBreakerIntegrationTests" 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "INTEGRATION TEST GATE FAILED" -ForegroundColor Red
    Write-Host "Run: dotnet test 6_Tests\VanAn.Integration.Tests\VanAn.Integration.Tests.csproj --filter FullyQualifiedName~CircuitBreakerIntegrationTests for details" -ForegroundColor Yellow
    exit 1
}

Write-Host "Fast test gate: PASSED" -ForegroundColor Green

# 6. Summary
$warningCount = ($buildOutput | Select-String -Pattern "warning").Count
Write-Host "Ã¢ BUILD SUCCEEDED - $warningCount warning(s)" -ForegroundColor Green

if ($warningCount -gt 5) {
    Write-Host "Ã¢  Warning count ($warningCount) is higher than target (<=5). Please review." -ForegroundColor Yellow
} else {
    Write-Host "Ã¢ Excellent! Warning count is within target." -ForegroundColor Green
}

# 7. Generate guard report
Write-Host "Generating guard report..." -ForegroundColor Yellow

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportFile = "guard-report-$timestamp.txt"

$reportContent = "Guard Check Report`n"
$reportContent += "==================`n"
$reportContent += "Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`n"
$reportContent += "Version: v8.1 (Phase 2 Upgrade + Source Control Guard)`n`n"
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
$reportContent += "  VA1001 (Domain Entity Location): 0`n"
$reportContent += "  VA1002 (Dependency Direction): 0`n"
$reportContent += "  VA1003 (EF Core in Domain): 0`n"
$reportContent += "  VA1004 (Business Logic in Gateway): 0`n"
$reportContent += "  VA1005 (AccountingEntry Immutability): 0`n`n"
$reportContent += "Status: ALL CHECKS PASSED`n"
$reportContent | Out-File $reportFile

Write-Host "Report generated: $reportFile" -ForegroundColor Green

Write-Host "âœ… ALL CHECKS PASSED - Ready for review" -ForegroundColor Cyan
