# TASK CARD: W4-3-T1 — Phase 1 Validation: Sidecars Only (No Sync Workers)

**Wave:** 7 (ADR001-W4.3) — Phased Migration Validation
**Branch:** `feature/adr001-wave4-migration-validation`
**Estimated effort:** 30 minutes
**Dependency:** Wave 6 (ADR001-W4.2) complete ✅ (sync workers implemented) + Wave 5 (sidecars) ✅

---

## 1. GOAL & CONTEXT

Validate Phase 1 của migration strategy: SQLite sidecars deployed nhưng sync workers KHÔNG active. PostgreSQL vẫn là primary database.

**Critical:** Phase 1 validation đảm bảo sidecar deployment không affect v1 SaaS operation.

**Architecture Reference:** `docs/Architecture/ADR001-Station-Architecture.md` (Migration Strategy: Phase 1)

---

## 2. VERIFIED FACTS

| Fact | Source |
|------|--------|
| Phase 1 strategy: Add SQLite sidecars, keep PostgreSQL primary, sync workers start but do NOT publish | ADR001-Station-Architecture.md L191-196 |
| Sidecar containers added trong Wave 5: shoperp-sqlite, khachlink-sqlite, order-station-sqlite | W4-1-T1-card.md |
| Sync workers have `profiles: [hybrid]` — không auto-start trong default mode | W4-2-T3-card.md L95 |
| v1 SaaS mode: `DEPLOYMENT_MODE=saas` (default) | W4-1-T3-card.md L45 |
| PostgreSQL vẫn primary database trong Phase 1 | ADR001-Station-Architecture.md L193 |

---

## 3. IMPLEMENTATION SPEC

### 3.1 Create Validation Script

**File tạo mới:** `scripts/validate-phase1-sidecars.ps1`

```powershell
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
```

### 3.2 Execute Validation

```powershell
# Run validation script
cd c:/VibeCoding/Gemini_Windsurf
./scripts/validate-phase1-sidecars.ps1
```

---

## 4. HARDENING GATES

- [ ] Validation script executable (chmod +x on Linux/Mac)
- [ ] Script tests all Phase 1 requirements
- [ ] Script returns exit code 0 on success, 1 on failure
- [ ] Manual verification: sidecar containers can start independently
- [ ] Manual verification: existing services unchanged
- [ ] No data migration required trong Phase 1

---

## 5. VALIDATION

```powershell
# Execute validation script
./scripts/validate-phase1-sidecars.ps1

# Expected output:
# === Phase 1 Validation: Sidecars Only ===
# ✅ PASS: docker-compose syntax valid
# ✅ PASS: shoperp-sqlite exists
# ✅ PASS: khachlink-sqlite exists
# ✅ PASS: order-station-sqlite exists
# ✅ PASS: shoperp-nats-sync not in default profile
# ✅ PASS: khachlink-nats-sync not in default profile
# ✅ PASS: order-station-nats-sync not in default profile
# ✅ PASS: shoperp_sqlite_data exists
# ✅ PASS: khachlink_sqlite_data exists
# ✅ PASS: order_sqlite_data exists
# ✅ PASS: DEPLOYMENT_MODE default is saas
# === Phase 1 Validation: ALL TESTS PASSED ===
```

---

## 6. EXIT CRITERIA

- [ ] Validation script `validate-phase1-sidecars.ps1` created
- [ ] Script executes successfully (exit code 0)
- [ ] All tests PASS: sidecars exist, sync workers inactive, volumes exist, DEPLOYMENT_MODE default is saas
- [ ] Manual verification: `docker compose -f docker-compose.prod.yml up -d` starts sidecars but not sync workers
- [ ] Existing v1 SaaS services unchanged
- [ ] PostgreSQL still primary database
- [ ] Proceed to W4-3-T2 (Phase 2 validation: sync workers enabled)