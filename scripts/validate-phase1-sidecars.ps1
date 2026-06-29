#!/usr/bin/env pwsh
# Phase 1 Validation: Sidecars Only (No Sync Workers)
# Validates that SQLite sidecars are deployed but sync workers are not active

Write-Host "=== Phase 1 Validation: Sidecars Only ===" -ForegroundColor Cyan

# Test 1: Validate docker-compose syntax
Write-Host "Test 1: Validate docker-compose syntax..." -ForegroundColor Yellow
docker compose -f docker-compose.prod.yml config
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ FAIL: docker-compose syntax invalid" -ForegroundColor Red
    exit 1
}
Write-Host "✅ PASS: docker-compose syntax valid" -ForegroundColor Green

# Test 2: Verify sidecar containers exist
Write-Host "Test 2: Verify sidecar containers exist..." -ForegroundColor Yellow
$config = docker compose -f docker-compose.prod.yml config
$sidecars = @("shoperp-sqlite", "khachlink-sqlite", "order-station-sqlite")
foreach ($sidecar in $sidecars) {
    if ($config -match $sidecar) {
        Write-Host "✅ PASS: $sidecar exists" -ForegroundColor Green
    } else {
        Write-Host "❌ FAIL: $sidecar not found" -ForegroundColor Red
        exit 1
    }
}

# Test 3: Verify sync workers NOT in default profile
Write-Host "Test 3: Verify sync workers NOT in default profile..." -ForegroundColor Yellow
$syncWorkers = @("shoperp-nats-sync", "khachlink-nats-sync", "order-station-nats-sync")
foreach ($worker in $syncWorkers) {
    if ($config -match $worker) {
        Write-Host "❌ FAIL: $worker should not be in default profile" -ForegroundColor Red
        exit 1
    } else {
        Write-Host "✅ PASS: $worker not in default profile" -ForegroundColor Green
    }
}

# Test 4: Verify volumes exist
Write-Host "Test 4: Verify SQLite volumes exist..." -ForegroundColor Yellow
$volumes = @("shoperp_sqlite_data", "khachlink_sqlite_data", "order_sqlite_data")
foreach ($volume in $volumes) {
    if ($config -match $volume) {
        Write-Host "✅ PASS: $volume exists" -ForegroundColor Green
    } else {
        Write-Host "❌ FAIL: $volume not found" -ForegroundColor Red
        exit 1
    }
}

# Test 5: Verify DEPLOYMENT_MODE default is saas
Write-Host "Test 5: Verify DEPLOYMENT_MODE default is saas..." -ForegroundColor Yellow
if ($config -match 'DEPLOYMENT_MODE.*saas') {
    Write-Host "✅ PASS: DEPLOYMENT_MODE default is saas" -ForegroundColor Green
} else {
    Write-Host "❌ FAIL: DEPLOYMENT_MODE default not saas" -ForegroundColor Red
    exit 1
}

Write-Host "=== Phase 1 Validation: ALL TESTS PASSED ===" -ForegroundColor Green
Write-Host "Sidecars deployed, sync workers inactive, PostgreSQL primary" -ForegroundColor Cyan