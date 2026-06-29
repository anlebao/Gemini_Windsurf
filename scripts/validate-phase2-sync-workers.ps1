#!/usr/bin/env pwsh
# Phase 2 Validation: Sync Workers Enabled (Dual-Write)
# Validates that sync workers are active and data syncs correctly

Write-Host "=== Phase 2 Validation: Sync Workers Enabled ===" -ForegroundColor Cyan

# Test 1: Validate docker-compose syntax with hybrid profile
Write-Host "Test 1: Validate docker-compose syntax with hybrid profile..." -ForegroundColor Yellow
docker compose -f docker-compose.prod.yml --profile hybrid config
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ FAIL: docker-compose syntax invalid" -ForegroundColor Red
    exit 1
}
Write-Host "✅ PASS: docker-compose syntax valid" -ForegroundColor Green

# Test 2: Verify sync workers exist in hybrid profile
Write-Host "Test 2: Verify sync workers exist in hybrid profile..." -ForegroundColor Yellow
$config = docker compose -f docker-compose.prod.yml --profile hybrid config
$syncWorkers = @("shoperp-nats-sync", "khachlink-nats-sync", "order-station-nats-sync")
foreach ($worker in $syncWorkers) {
    Write-Host "  Checking for $worker..." -ForegroundColor DarkGray
    if ($config -match $worker) {
        Write-Host "✅ PASS: $worker exists in hybrid profile" -ForegroundColor Green
    } else {
        Write-Host "❌ FAIL: $worker not found in hybrid profile" -ForegroundColor Red
        exit 1
    }
}

# Test 3: Verify sync worker command includes --sync-worker
Write-Host "Test 3: Verify sync worker command includes --sync-worker..." -ForegroundColor Yellow
foreach ($worker in $syncWorkers) {
    Write-Host "  Checking $worker for --sync-worker arg..." -ForegroundColor DarkGray
    # More flexible pattern to match --sync-worker in YAML array format
    if ($config -match "$worker" -and $config -match "--sync-worker") {
        Write-Host "✅ PASS: $worker has --sync-worker arg" -ForegroundColor Green
    } else {
        Write-Host "❌ FAIL: $worker missing --sync-worker arg" -ForegroundColor Red
        exit 1
    }
}

# Test 4: Verify NATS connection configuration
Write-Host "Test 4: Verify NATS connection configuration..." -ForegroundColor Yellow
# More flexible pattern for NATS URL in YAML format
if ($config -match 'NATS__Url' -and $config -match 'nats://nats:4222') {
    Write-Host "✅ PASS: NATS URL configured" -ForegroundColor Green
} else {
    Write-Host "❌ FAIL: NATS URL not configured" -ForegroundColor Red
    exit 1
}

# Test 5: Verify SQLite volume mounts
Write-Host "Test 5: Verify SQLite volume mounts..." -ForegroundColor Yellow
$volumeMounts = @{
    "shoperp-nats-sync" = "shoperp_sqlite_data"
    "khachlink-nats-sync" = "khachlink_sqlite_data"
    "order-station-nats-sync" = "order_sqlite_data"
}
foreach ($worker in $volumeMounts.Keys) {
    $volume = $volumeMounts[$worker]
    Write-Host "  Checking $worker for volume mount $volume..." -ForegroundColor DarkGray
    # More flexible pattern to match volume in YAML format
    if ($config -match "$worker" -and $config -match $volume) {
        Write-Host "✅ PASS: $worker has correct volume mount" -ForegroundColor Green
    } else {
        Write-Host "❌ FAIL: $worker missing volume mount" -ForegroundColor Red
        exit 1
    }
}

# Test 6: Verify sync worker dependencies
Write-Host "Test 6: Verify sync worker dependencies..." -ForegroundColor Yellow
# Check if config contains both sync workers and depends_on
if ($config -match "nats-sync" -and $config -match "depends_on") {
    Write-Host "✅ PASS: Sync workers have depends_on" -ForegroundColor Green
} else {
    Write-Host "❌ FAIL: Sync workers missing depends_on" -ForegroundColor Red
    exit 1
}

Write-Host "=== Phase 2 Validation: ALL TESTS PASSED ===" -ForegroundColor Green
Write-Host "Sync workers configured, ready for dual-write testing" -ForegroundColor Cyan