# ADR001 Rollback Plan — v2 Hybrid → v1 SaaS

**Created:** 2026-06-29
**Purpose:** Document rollback procedures for v2 Hybrid Edge/Cloud deployment
**Scope:** Phase 1-2 rollback (PostgreSQL still source of truth)

---

## Rollback Scenarios

### Scenario 1: Phase 1 Rollback (Sidecars Only)

**Trigger:** Sidecar deployment issues, volume problems, container startup failures

**Impact:** Zero data loss — sidecars not yet in use

**Steps:**
1. Stop sidecar containers:
   ```bash
   docker compose -f docker-compose.prod.yml stop shoperp-sqlite khachlink-sqlite order-station-sqlite
   ```

2. Remove sidecar containers (optional):
   ```bash
   docker compose -f docker-compose.prod.yml rm -f shoperp-sqlite khachlink-sqlite order-station-sqlite
   ```

3. Remove sidecar volumes (optional — frees disk space):
   ```bash
   docker volume rm vanan_shoperp_sqlite_data vanan_khachlink_sqlite_data vanan_order_sqlite_data
   ```

4. Revert docker-compose.prod.yml changes:
   - Remove sidecar service definitions
   - Remove volume definitions
   - Remove DEPLOYMENT_MODE env var from main services

5. Restart main services:
   ```bash
   docker compose -f docker-compose.prod.yml up -d
   ```

**Validation:**
- Main services start successfully
- PostgreSQL still primary
- No data loss (sidecars never used)

---

### Scenario 2: Phase 2 Rollback (Sync Workers Active)

**Trigger:** Sync worker failures, NATS connectivity issues, data inconsistency

**Impact:** Zero data loss — PostgreSQL still source of truth, SQLite can be discarded

**Steps:**
1. Stop sync workers:
   ```bash
   docker compose -f docker-compose.prod.yml --profile hybrid stop shoperp-nats-sync khachlink-nats-sync order-station-nats-sync
   ```

2. Stop sidecar containers:
   ```bash
   docker compose -f docker-compose.prod.yml stop shoperp-sqlite khachlink-sqlite order-station-sqlite
   ```

3. Switch main services back to PostgreSQL:
   - Remove `SQLITE_DB_PATH` env var from shoperp, khachlink
   - Set `DEPLOYMENT_MODE=saas` (or remove env var to use default)
   - Remove conditional sidecar dependencies

4. Remove sync worker and sidecar definitions from docker-compose.prod.yml

5. Remove volumes:
   ```bash
   docker volume rm vanan_shoperp_sqlite_data vanan_khachlink_sqlite_data vanan_order_sqlite_data
   ```

6. Restart main services:
   ```bash
   docker compose -f docker-compose.prod.yml up -d
   ```

**Validation:**
- Main services connect to PostgreSQL
- No sync workers running
- No data loss (PostgreSQL was source of truth)
- Data consistency verified

---

### Scenario 3: Emergency Rollback (Production Issues)

**Trigger:** Critical production issues, data corruption, security concerns

**Impact:** Potential minimal data loss (recent writes to SQLite not synced to PostgreSQL)

**Steps:**
1. Immediately stop all containers:
   ```bash
   docker compose -f docker-compose.prod.yml down
   ```

2. Restore PostgreSQL backup (if available):
   ```bash
   # Use your backup restore procedure
   ```

3. Revert docker-compose.prod.yml to v1 SaaS configuration:
   - Remove all sidecar, sync worker, and volume definitions
   - Remove DEPLOYMENT_MODE env vars
   - Remove conditional dependencies

4. Start v1 SaaS deployment:
   ```bash
   docker compose -f docker-compose.prod.yml up -d
   ```

5. Monitor for issues and data consistency

**Validation:**
- System operational in v1 SaaS mode
- PostgreSQL data integrity verified
- Minimal data loss (if any)

---

## Rollback Testing

### Test Scenario 1: Phase 1 Rollback

```bash
# Deploy Phase 1
docker compose -f docker-compose.prod.yml up -d

# Verify sidecars running
docker ps | grep sqlite

# Execute rollback
docker compose -f docker-compose.prod.yml stop shoperp-sqlite khachlink-sqlite order-station-sqlite
docker compose -f docker-compose.prod.yml rm -f shoperp-sqlite khachlink-sqlite order-station-sqlite
docker volume rm vanan_shoperp_sqlite_data vanan_khachlink_sqlite_data vanan_order_sqlite_data

# Verify main services still operational
docker ps | grep -E "shoperp|khachlink|corehub"
```

### Test Scenario 2: Phase 2 Rollback

```bash
# Deploy Phase 2
DEPLOYMENT_MODE=hybrid docker compose -f docker-compose.prod.yml --profile hybrid up -d

# Verify sync workers running
docker ps | grep nats-sync

# Execute rollback
DEPLOYMENT_MODE=saas docker compose -f docker-compose.prod.yml --profile hybrid down
# Revert docker-compose.prod.yml changes manually
docker compose -f docker-compose.prod.yml up -d

# Verify main services connect to PostgreSQL
docker logs vanan-shoperp | grep -i postgres
```

---

## Rollback Decision Criteria

**When to rollback:**
- Sync worker failure rate > 5%
- Data inconsistency detected between SQLite and PostgreSQL
- Sync lag > 5 minutes
- NATS connectivity issues > 10 minutes
- Critical production issues affecting users

**When to continue:**
- Minor sync worker hiccups (< 1% failure rate)
- Temporary NATS blips (< 1 minute)
- Sync lag within acceptable range (< 1 minute)

---

## Post-Rollback Actions

1. Document root cause of rollback
2. Implement fixes for identified issues
3. Re-test migration strategy
4. Schedule retry migration window
5. Update rollback plan based on lessons learned