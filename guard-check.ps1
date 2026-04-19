# guard-check.ps1 - Vãn An Strict Guard v7.0 (Updated April 2026)

Write-Host "ð Running Vãn An Strict Guard v7.0..." -ForegroundColor Cyan

# 1. Run windsurf-guard.js
Write-Host "Running windsurf-guard.js v6.0..." -ForegroundColor Yellow
node windsurf-guard.js
if ($LASTEXITCODE -ne 0) {
    Write-Host "â WINDSURF GUARD FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "â WINDSURF GUARD PASSED" -ForegroundColor Green

# 2. Run dotnet build with detailed output
Write-Host "Running dotnet build..." -ForegroundColor Yellow
$buildOutput = dotnet build --verbosity normal 2>&1 | Tee-Object -FilePath "build.log"

if ($LASTEXITCODE -ne 0) {
    Write-Host "â BUILD FAILED" -ForegroundColor Red
    exit 1
}

# 3. Check for critical issues
$criticalIssues = $buildOutput | Select-String -Pattern "CS8034|NU1109|NU1603|NU1605|VA0004|error" -CaseSensitive

if ($criticalIssues) {
    Write-Host "â  CRITICAL ISSUES DETECTED:" -ForegroundColor Red
    $criticalIssues | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    Write-Host "â Guard check failed due to critical issues" -ForegroundColor Red
    exit 1
}

# 4. Summary
$warningCount = ($buildOutput | Select-String -Pattern "warning").Count
Write-Host "â BUILD SUCCEEDED - $warningCount warning(s)" -ForegroundColor Green

if ($warningCount -gt 5) {
    Write-Host "â  Warning count ($warningCount) is higher than target (<=5). Please review." -ForegroundColor Yellow
} else {
    Write-Host "â Excellent! Warning count is within target." -ForegroundColor Green
}

Write-Host "â ALL CHECKS PASSED - Ready for review" -ForegroundColor Cyan
