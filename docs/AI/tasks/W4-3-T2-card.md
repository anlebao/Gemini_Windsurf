# TASK CARD: W4-3-T2 — Phase 2 Validation: Sync Workers Enabled (Dual-Write)

**Wave:** 7 (ADR001-W4.3) — Phased Migration Validation
**Branch:** `feature/adr001-wave4-migration-validation`
**Estimated effort:** 1 hour
**Dependency:** W4-3-T1 complete ✅ (Phase 1 validation pass)

---

## 1. GOAL & CONTEXT

Validate Phase 2 của migration strategy: Sync workers enabled, dual-write mode (SQLite primary + PostgreSQL sync target), monitor sync lag.

**Critical:** Phase 2 validation đảm bảo sync workers operational và data consistency maintained.

**Architecture Reference:** `docs/Architecture/ADR001-Station-Architecture.md` (Migration Strategy: Phase 2)

---

## 2. VERIFIED FACTS

| Fact | Source |
|------|--------|
| Phase 2 strategy: Switch to SQLite primary, sync workers start publishing, PostgreSQL becomes read-only sync target | ADR001-Station-Architecture.md L197-201 |
| Sync workers activated via `--profile hybrid` | W4-2-T3-card.md L95 |
| Sync worker env vars: NATS__Url=nats://nats:4222, Sync__PollIntervalMs=1000 | W4-2-T3-card.md L60-62 |
| NatsSyncWorker polls Outbox → publishes to NATS → marks processed | W3-ADR-T2-card.md |
| NATS subjects: "order.status.changed" for KhachLink-W4 | UNIFIED_ROADMAP_master_plan.md L159 |

---

## 3. IMPLEMENTATION SPEC

### 3.1 Create Validation Script

**File tạo mới:** `scripts/validate-phase2-sync-workers.ps1`

```powershell
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
    if ($config -match "$worker.*--sync-worker") {
        Write-Host "✅ PASS: $worker has --sync-worker arg" -ForegroundColor Green
    } else {
        Write-Host "❌ FAIL: $worker missing --sync-worker arg" -ForegroundColor Red
        exit 1
    }
}

# Test 4: Verify NATS connection configuration
Write-Host "Test 4: Verify NATS connection configuration..." -ForegroundColor Yellow
if ($config -match 'NATS__Url=nats://nats:4222') {
    Write-Host "✅ PASS: NATS URL configured" -ForegroundColor Green
} else {
    Write-Host "❌ FAIL: NATS URL not configured" -ForegroundColor Red
    exit 1
}

# Test 5: Verify SQLite volume mounts
Write-Host "Test 5: Verify SQLite volume mounts..." -ForegroundColor Yellow
$volumeMounts = @{
    "shoperp-nats-sync" = "shoperp_sqlite_data:/data"
    "khachlink-nats-sync" = "khachlink_sqlite_data:/data"
    "order-station-nats-sync" = "order_sqlite_data:/data"
}
foreach ($worker in $volumeMounts.Keys) {
    $mount = $volumeMounts[$worker]
    if ($config -match "$worker.*$mount") {
        Write-Host "✅ PASS: $worker has correct volume mount" -ForegroundColor Green
    } else {
        Write-Host "❌ FAIL: $worker missing volume mount" -ForegroundColor Red
        exit 1
    }
}

# Test 6: Verify sync worker dependencies
Write-Host "Test 6: Verify sync worker dependencies..." -ForegroundColor Yellow
if ($config -match 'shoperp-nats-sync.*depends_on.*nats') {
    Write-Host "✅ PASS: shoperp-nats-sync depends on NATS" -ForegroundColor Green
} else {
    Write-Host "❌ FAIL: shoperp-nats-sync missing NATS dependency" -ForegroundColor Red
    exit 1
}

Write-Host "=== Phase 2 Validation: ALL TESTS PASSED ===" -ForegroundColor Green
Write-Host "Sync workers configured, ready for dual-write testing" -ForegroundColor Cyan
```

### 3.2 Manual Integration Testing

**Test sync worker startup:**
```powershell
# Start Phase 2 deployment
DEPLOYMENT_MODE=hybrid docker compose -f docker-compose.prod.yml --profile hybrid up -d

# Verify sync worker containers started
docker ps | grep nats-sync

# Verify sync worker logs (should show "NatsSyncWorker registered — running in edge sync mode")
docker logs vanan-shoperp-nats-sync
docker logs vanan-khachlink-nats-sync
docker logs vanan-order-station-nats-sync

# Verify NATS connection (sync workers should connect to NATS)
docker logs vanan-nats | grep "client connect"
```

**Test data sync (manual):**
```powershell
# Insert test data into SQLite Outbox
# Verify sync worker picks up and publishes to NATS
# Verify PostgreSQL receives data via NATS consumer
# This requires actual data flow testing with running services
```

### 3.3 Monitor Sync Lag

**Create sync lag monitor script:** `scripts/monitor-sync-lag.ps1`

```powershell
#!/usr/bin/env pwsh
# Monitor sync lag between SQLite Outbox and PostgreSQL
# This is a placeholder - actual implementation depends on your monitoring setup

Write-Host "=== Sync Lag Monitor ===" -ForegroundColor Cyan
Write-Host "Query SQLite Outbox for unprocessed events..." -ForegroundColor Yellow
Write-Host "Query PostgreSQL for synced events..." -ForegroundColor Yellow
Write-Host "Calculate lag time..." -ForegroundColor Yellow
# TODO: Implement actual monitoring logic
```

---

## 4. HARDENING GATES

- [ ] Validation script executable (chmod +x on Linux/Mac)
- [ ] Script tests all Phase 2 configuration requirements
- [ ] Manual testing: sync workers start successfully with --profile hybrid
- [ ] Manual testing: sync workers connect to NATS
- [ ] Manual testing: no errors in sync worker logs
- [ ] Sync lag monitoring placeholder created (full implementation deferred to production deployment)
- [ ] Rollback plan ready (W4-3-T3)

---

## 5. VALIDATION

```powershell
# Execute validation script
./scripts/validate-phase2-sync-workers.ps1

# Expected output:
# === Phase 2 Validation: Sync Workers Enabled ===
# ✅ PASS: docker-compose syntax valid
# ✅ PASS: shoperp-nats-sync exists in hybrid profile
# ✅ PASS: khachlink-nats-sync exists in hybrid profile
# ✅ PASS: order-station-nats-sync exists in hybrid profile
# ✅ PASS: shoperp-nats-sync has --sync-worker arg
# ✅ PASS: khachlink-nats-sync has --sync-worker arg
# ✅ PASS: order-station-nats-sync has --sync-worker arg
# ✅ PASS: NATS URL configured
# ✅ PASS: shoperp-nats-sync has correct volume mount
# ✅ PASS: khachlink-nats-sync has correct volume mount
# ✅ PASS: order-station-nats-sync has correct volume mount
# ✅ PASS: shoperp-nats-sync depends on NATS
# === Phase 2 Validation: ALL TESTS PASSED ===
```

---

## 6. EXIT CRITERIA

- [ ] Validation script `validate-phase2-sync-workers.ps1` created
- [ ] Script executes successfully (exit code 0)
- [ ] All configuration tests PASS
- [ ] Manual testing: sync workers start with `--profile hybrid`
- [ ] Manual testing: sync workers connect to NATS successfully
- [ ] Manual testing: no errors in sync worker logs
- [ ] Sync lag monitor placeholder created
- [ ] Proceed to W4-3-T3 (Rollback plan testing + documentation)