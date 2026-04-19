#!/usr/bin/env pwsh

Write-Host "?? LOCAL CI START"

Write-Host "1? BUILD"
dotnet build
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "2? TEST"
dotnet test
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "3? GUARD"
node windsurf-guard.js --ci
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "?? ALL PASSED"
