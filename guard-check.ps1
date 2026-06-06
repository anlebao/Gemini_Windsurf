# guard-check.ps1 - Van An Strict Guard v7.1 (Updated May 2026)

Write-Host "Running Van An Strict Guard v7.1..." -ForegroundColor Cyan

# 1. Run windsurf-guard.js
Write-Host "Running windsurf-guard.js v6.0..." -ForegroundColor Yellow
node windsurf-guard.js
if ($LASTEXITCODE -ne 0) {
    Write-Host "â WINDSURF GUARD FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "â WINDSURF GUARD PASSED" -ForegroundColor Green

# 2. Run architecture-guard.ps1 (TEMPORARY - will be removed in Phase 3)
Write-Host "Running architecture-guard.ps1..." -ForegroundColor Yellow
.\architecture-guard.ps1
if ($LASTEXITCODE -ne 0) {
    Write-Host "â ARCHITECTURE GUARD FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "â ARCHITECTURE GUARD PASSED" -ForegroundColor Green

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
    Write-Host "â BUILD FAILED" -ForegroundColor Red
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

dotnet test 6_Tests\VanAn.Core.Tests\VanAn.Core.Tests.csproj --verbosity quiet --configuration Release --filter "Category!=Performance&Category!=Integration&Category!=E2E" 2>&1 | Out-Null
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

# NEW: Integration tests (with real NATS service) - Phase 2.4
Write-Host "Running Integration test gate (requires NATS service)..." -ForegroundColor Yellow
dotnet test 6_Tests\VanAn.Integration.Tests\VanAn.Integration.Tests.csproj --verbosity quiet --configuration Release 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "INTEGRATION TEST GATE FAILED" -ForegroundColor Red
    Write-Host "Run: dotnet test 6_Tests\VanAn.Integration.Tests\VanAn.Integration.Tests.csproj for details" -ForegroundColor Yellow
    exit 1
}

Write-Host "Fast test gate: PASSED" -ForegroundColor Green

# 6. Summary
$warningCount = ($buildOutput | Select-String -Pattern "warning").Count
Write-Host "â BUILD SUCCEEDED - $warningCount warning(s)" -ForegroundColor Green

if ($warningCount -gt 5) {
    Write-Host "â  Warning count ($warningCount) is higher than target (<=5). Please review." -ForegroundColor Yellow
} else {
    Write-Host "â Excellent! Warning count is within target." -ForegroundColor Green
}

# 7. Generate guard report
Write-Host "Generating guard report..." -ForegroundColor Yellow

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportFile = "guard-report-$timestamp.txt"

@"
Guard Check Report
==================
Date: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Version: v8.0 (Phase 2 Upgrade)

Component Results
------------------
Windsurf Guard: PASSED
Architecture Guard: PASSED
Roslyn Analyzers: PASSED
Build: SUCCEEDED
Core Tests: PASSED
Architecture Tests: PASSED
Integration Tests: PASSED

Warning Classification
----------------------
Critical: $($warningStats.Critical)
Performance: $($warningStats.Performance)
Security: $($warningStats.Security)
Other: $($warningStats.Other)
Total Warnings: $warningCount

Analyzer Violations
-------------------
VA1001 (Domain Entity Location): 0
VA1002 (Dependency Direction): 0
VA1003 (EF Core in Domain): 0
VA1004 (Business Logic in Gateway): 0
VA1005 (AccountingEntry Immutability): 0

Status: ALL CHECKS PASSED
"@ | Out-File $reportFile

Write-Host "Report generated: $reportFile" -ForegroundColor Green

Write-Host "â ALL CHECKS PASSED - Ready for review" -ForegroundColor Cyan
