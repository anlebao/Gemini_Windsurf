#!/usr/bin/env pwsh
# ci-local.ps1 - VanAn Local CI Pipeline
# Updated 2026-05-05: explicit per-project tests; OrderFlow excluded (requires live PostgreSQL)

Write-Host "== LOCAL CI START ==" -ForegroundColor Cyan

# 1. Build solution
Write-Host "[1/4] BUILD" -ForegroundColor Yellow
dotnet build VanAn.sln --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "Build: OK" -ForegroundColor Green

# 2. Unit Tests - Domain invariants (fastest, ~5s)
Write-Host "[2/4] UNIT TESTS" -ForegroundColor Yellow
dotnet test 6_Tests\VanAn.Core.Tests\VanAn.Core.Tests.csproj --verbosity quiet --no-build --filter "Category!=Performance&Category!=Integration&Category!=E2E"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Core.Tests FAILED" -ForegroundColor Red
    exit 1
}
dotnet test 6_Tests\VanAn.Unit.Tests\VanAn.Unit.Tests.csproj --verbosity quiet --no-build --filter "Category!=Performance&Category!=Integration&Category!=E2E"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Unit.Tests FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "Unit Tests: OK" -ForegroundColor Green

# 3. Integration + Architecture Tests (~15s)
# NOTE: OrderFlow.Tests excluded - requires live PostgreSQL (marked Skip) and has missing TestInfrastructure ref
Write-Host "[3/4] INTEGRATION + ARCHITECTURE TESTS" -ForegroundColor Yellow
dotnet test 6_Tests\VanAn.Integration.Tests\VanAn.Integration.Tests.csproj --verbosity quiet --no-build --filter "Category!=Performance&Category!=Integration&Category!=E2E"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Integration.Tests FAILED" -ForegroundColor Red
    exit 1
}
dotnet test 6_Tests\VanAn.Architecture.Tests\VanAn.Architecture.Tests.csproj --verbosity quiet --no-build --filter "Category!=Performance&Category!=Integration&Category!=E2E"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Architecture.Tests FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "Integration + Architecture Tests: OK" -ForegroundColor Green

# 4. Architecture Guard
Write-Host "[4/4] GUARD" -ForegroundColor Yellow
node windsurf-guard.js --ci
if ($LASTEXITCODE -ne 0) {
    Write-Host "windsurf-guard FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "Guard: OK" -ForegroundColor Green

Write-Host "== ALL PASSED ==" -ForegroundColor Cyan
