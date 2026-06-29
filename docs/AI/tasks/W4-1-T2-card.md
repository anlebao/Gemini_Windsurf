# TASK CARD: W4-1-T2 — Add Docker Volumes for SQLite Persistence

**Wave:** 5 (ADR001-W4.1) — SQLite Sidecar Infrastructure
**Branch:** `feature/adr001-wave4-sqlite-sidecars`
**Estimated effort:** 30 minutes
**Dependency:** W4-1-T1 complete ✅ (sidecar containers added)

---

## 1. GOAL & CONTEXT

Thêm Docker volumes vào `docker-compose.prod.yml` để SQLite database files được persist across container restarts. Đây là prerequisite cho v2 Hybrid Edge/Cloud deployment.

**Critical:** Volumes được thêm nhưng không sử dụng trong v1 SaaS — chỉ active khi chuyển sang v2 hybrid mode.

**Architecture Reference:** `docs/Architecture/ADR001-Station-Architecture.md` (Step 3: Add to `volumes:` section)

---

## 2. VERIFIED FACTS

| Fact | Source |
|------|--------|
| Current volumes section: postgres_data, nats_data, seq_data, shoperp_data, certbot_www, certbot_conf | `docker-compose.prod.yml` L229-235 |
| Sidecar containers trong W4-1-T1 reference volumes: shoperp_sqlite_data, khachlink_sqlite_data, order_sqlite_data | W4-1-T1-card.md L28, L40, L52 |
| Docker volumes default driver là local (persist on host) | Docker Compose specification |
| Volume naming convention: `<service>_<volume>_data` để consistency | Existing volumes pattern |

---

## 3. IMPLEMENTATION SPEC

### 3.1 Edit location: `docker-compose.prod.yml`

**Thêm vào `volumes:` section (sau certbot_conf):**

```yaml
volumes:
  postgres_data:
  nats_data:
  seq_data:
  shoperp_data:
  certbot_www:
  certbot_conf:
  # SQLite persistent volumes for v2 Hybrid Edge/Cloud (Phase 1: added, not active)
  shoperp_sqlite_data:
    driver: local
  khachlink_sqlite_data:
    driver: local
  order_sqlite_data:
    driver: local
```

**Lý do:**
- Explicit `driver: local` cho clarity (default nhưng explicit tốt cho documentation)
- Volume names match sidecar container references từ W4-1-T1
- Phase 1: volumes tồn tại nhưng không mounted bởi main services

---

## 4. HARDENING GATES

- [ ] KHÔNG sửa existing volumes (postgres_data, nats_data, seq_data, shoperp_data, certbot_www, certbot_conf)
- [ ] Volume names match sidecar container references exactly
- [ ] Driver explicitly specified (local) cho production clarity
- [ ] KHÔNG add volume options hoặc labels (keep simple cho Phase 1)
- [ ] Comments rõ ràng indicate v2 hybrid purpose

---

## 5. VALIDATION

```powershell
# Validate docker-compose syntax
docker compose -f docker-compose.prod.yml config

# Verify volumes defined
docker compose -f docker-compose.prod.yml config | grep -A 10 "volumes:"

# Verify volume names match sidecar references
docker compose -f docker-compose.prod.yml config | grep "shoperp_sqlite_data"
docker compose -f docker-compose.prod.yml config | grep "khachlink_sqlite_data"
docker compose -f docker-compose.prod.yml config | grep "order_sqlite_data"
```

---

## 6. EXIT CRITERIA

- [ ] 3 new volumes added: shoperp_sqlite_data, khachlink_sqlite_data, order_sqlite_data
- [ ] All volumes have explicit `driver: local`
- [ ] Existing volumes unchanged
- [ ] docker-compose.prod.yml syntax valid
- [ ] Volume names match sidecar container references from W4-1-T1
- [ ] Proceed to W4-1-T3 (Update service dependencies for sidecars)