# TASK CARD: W4-1-T3 — Update Service Dependencies for Sidecars

**Wave:** 5 (ADR001-W4.1) — SQLite Sidecar Infrastructure
**Branch:** `feature/adr001-wave4-sqlite-sidecars`
**Estimated effort:** 30 minutes
**Dependency:** W4-1-T1 ✅ (sidecar containers added) + W4-1-T2 ✅ (volumes added)

---

## 1. GOAL & CONTEXT

Update service dependencies trong `docker-compose.prod.yml` để main services (shoperp, khachlink) có thể depend trên sidecar containers khi chuyển sang v2 hybrid mode.

**Critical:** Phase 1 hiện tại — dependencies được thêm nhưng CONDITIONAL via environment variable để không affect v1 SaaS.

**Architecture Reference:** `docs/Architecture/ADR001-Station-Architecture.md` (Reverse Impact Analysis - Step 3)

---

## 2. VERIFIED FACTS

| Fact | Source |
|------|--------|
| shoperp service hiện tại depends_on: corehub | `docker-compose.prod.yml` L145-146 |
| khachlink service hiện tại depends_on: gateway | `docker-compose.prod.yml` L174-175 |
| Sidecar containers names: vanan-shoperp-sqlite, vanan-khachlink-sqlite, vanan-order-station-sqlite | W4-1-T1-card.md L27, L39, L51 |
| Docker Compose supports conditional dependencies via environment variables | Docker Compose specification |
| Phase 1 strategy: sidecars exist but main services không depend on them (PostgreSQL vẫn primary) | ADR001-Station-Architecture.md L191-195 |

---

## 3. IMPLEMENTATION SPEC

### 3.1 Edit location: `docker-compose.prod.yml`

**Update `shoperp` service depends_on (line 145-146):**

```yaml
shoperp:
  image: ${IMAGE_PREFIX:-ghcr.io/anlebao}/vanan-shoperp:${IMAGE_TAG:-latest}
  container_name: vanan-shoperp
  environment:
    - ASPNETCORE_ENVIRONMENT=Production
    - ASPNETCORE_URLS=http://+:80
    - DEPLOYMENT_MODE=${DEPLOYMENT_MODE:-saas}  # v1 SaaS (default) or v2 hybrid
    - Serilog__WriteTo__1__Args__serverUrl=http://seq:5341
    - Authentication__Authority=https://api.vanantech.io.vn
    - Seed__OwnerUsername=${SHOPERP_OWNER_USERNAME:-adminvanan1}
    - Seed__OwnerPassword=${SHOPERP_OWNER_PASSWORD:-2026@vanan}
    - Seed__TenantId=${SHOPERP_TENANT_ID:-00000000-0000-0000-0000-000000000001}
  volumes:
    - shoperp_data:/app
  networks:
    - vanan-network
  depends_on:
    corehub:
      condition: service_started
    # Conditional sidecar dependency (v2 hybrid only)
    shoperp-sqlite:
      condition: service_started
      required: ${DEPLOYMENT_MODE:-saas} == hybrid
```

**Update `khachlink` service depends_on (line 174-175):**

```yaml
khachlink:
  image: ${IMAGE_PREFIX:-ghcr.io/anlebao}/vanan-khachlink:${IMAGE_TAG:-latest}
  container_name: vanan-khachlink
  environment:
    - ASPNETCORE_ENVIRONMENT=Production
    - ASPNETCORE_URLS=http://+:80
    - DEPLOYMENT_MODE=${DEPLOYMENT_MODE:-saas}  # v1 SaaS (default) or v2 hybrid
    - Gateway__BaseUrl=https://api.vanantech.io.vn
    - Serilog__WriteTo__1__Args__serverUrl=http://seq:5341
  networks:
    - vanan-network
  depends_on:
    gateway:
      condition: service_started
    # Conditional sidecar dependency (v2 hybrid only)
    khachlink-sqlite:
      condition: service_started
      required: ${DEPLOYMENT_MODE:-saas} == hybrid
```

**Lý do design:**
- `DEPLOYMENT_MODE` environment variable với default `saas` (v1)
- Khi `DEPLOYMENT_MODE=hybrid`, sidecar dependencies become required
- Phase 1: `DEPLOYMENT_MODE=saas` (default) → sidecar dependencies ignored → v1 SaaS unchanged
- Phase 2: `DEPLOYMENT_MODE=hybrid` → sidecar dependencies active → v2 hybrid mode

---

## 4. HARDENING GATES

- [ ] `DEPLOYMENT_MODE` default là `saas` (v1 SaaS behavior unchanged)
- [ ] Sidecar dependencies CONDITIONAL via `required: ${DEPLOYMENT_MODE:-saas} == hybrid`
- [ ] Existing dependencies (corehub, gateway) unchanged
- [ ] Health checks cho sidecars KHÔNG needed trong Phase 1
- [ ] Environment variable documented trong .env.example hoặc deployment guide
- [ ] KHÔNG break existing v1 SaaS deployment

---

## 5. VALIDATION

```powershell
# Test v1 SaaS mode (default)
docker compose -f docker-compose.prod.yml config
# Verify: sidecar dependencies ignored when DEPLOYMENT_MODE=saas

# Test v2 hybrid mode
DEPLOYMENT_MODE=hybrid docker compose -f docker-compose.prod.yml config
# Verify: sidecar dependencies active when DEPLOYMENT_MODE=hybrid

# Validate syntax
docker compose -f docker-compose.prod.yml config
```

---

## 6. EXIT CRITERIA

- [ ] `DEPLOYMENT_MODE` environment variable added to shoperp và khachlink
- [ ] Default value là `saas` (v1 SaaS unchanged)
- [ ] Sidecar dependencies conditional via `required: ${DEPLOYMENT_MODE:-saas} == hybrid`
- [ ] Existing dependencies (corehub, gateway) unchanged
- [ ] docker-compose.prod.yml syntax valid in both modes
- [ ] v1 SaaS deployment unchanged (verified via default config)
- [ ] Wave 5 (ADR001-W4.1) COMPLETE → Proceed to Wave 6 (ADR001-W4.2: NATS Sync Worker Mode)