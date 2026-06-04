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

# 2. Run architecture-guard.ps1
Write-Host "Running architecture-guard.ps1..." -ForegroundColor Yellow
.\architecture-guard.ps1
if ($LASTEXITCODE -ne 0) {
    Write-Host "â ARCHITECTURE GUARD FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "â ARCHITECTURE GUARD PASSED" -ForegroundColor Green

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

# 5. Fast test gate - Domain + Architecture tests only (~20s)
# Prevents false-green: build pass alone does not guarantee correctness
Write-Host "Running fast test gate (Domain + Architecture)..." -ForegroundColor Yellow

dotnet test 6_Tests\VanAn.Core.Tests\VanAn.Core.Tests.csproj --verbosity quiet --configuration Release 2>&1 | Out-Null
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

Write-Host "Fast test gate: PASSED" -ForegroundColor Green

# 6. Summary
$warningCount = ($buildOutput | Select-String -Pattern "warning").Count
Write-Host "â BUILD SUCCEEDED - $warningCount warning(s)" -ForegroundColor Green

if ($warningCount -gt 5) {
    Write-Host "â  Warning count ($warningCount) is higher than target (<=5). Please review." -ForegroundColor Yellow
} else {
    Write-Host "â Excellent! Warning count is within target." -ForegroundColor Green
}

Write-Host "â ALL CHECKS PASSED - Ready for review" -ForegroundColor Cyan
