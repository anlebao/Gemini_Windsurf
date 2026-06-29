# TASK CARD: W4-1-T1 — Add SQLite Sidecar Containers to docker-compose.prod.yml

**Wave:** 5 (ADR001-W4.1) — SQLite Sidecar Infrastructure
**Branch:** `feature/adr001-wave4-sqlite-sidecars`
**Estimated effort:** 1 hour
**Dependency:** Wave 4 (KhachLink-W2) complete ✅

---

## 1. GOAL & CONTEXT

Thêm SQLite sidecar containers vào `docker-compose.prod.yml` để chuẩn bị cho v2 Hybrid Edge/Cloud deployment. Sidecar containers sẽ hold SQLite database files với persistent volumes.

**Critical:** Đây là Phase 1 của migration strategy — sidecars được thêm nhưng KHÔNG active trong v1 SaaS. PostgreSQL vẫn là primary database.

**Architecture Reference:** `docs/Architecture/ADR001-Station-Architecture.md` (Step 3: Add SQLite Sidecars to docker-compose.prod.yml)

---

## 2. VERIFIED FACTS

| Fact | Source |
|------|--------|
| `docker-compose.prod.yml` hiện tại có postgres, seq, nats, corehub, gateway, shoperp, khachlink, nginx, certbot services | `docker-compose.prod.yml` L8-227 |
| Current volumes: postgres_data, nats_data, seq_data, shoperp_data, certbot_www, certbot_conf | `docker-compose.prod.yml` L229-235 |
| NATS service đã tồn tại (nats:2.10-alpine) với jetstream enabled | `docker-compose.prod.yml` L49-68 |
| ShopERP hiện tại dùng SQLite local file trong container (không phải volume persist) | ADR001-Station-Architecture.md L47 |
| Architecture document yêu cầu 3 sidecar containers: shoperp-sqlite, khachlink-sqlite, order-station-sqlite | ADR001-Station-Architecture.md L420-448 |

---

## 3. IMPLEMENTATION SPEC

### 3.1 Edit location: `docker-compose.prod.yml`

**Insert SAU `khachlink` service (line 192), TRƯỚC `nginx` service:**

```yaml
  # SQLite local databases for offline-first stations (v2 Hybrid Edge/Cloud)
  # Phase 1: Sidecars added but not active in v1 SaaS
  shoperp-sqlite:
    image: alpine:latest
    container_name: vanan-shoperp-sqlite
    command: ["sh", "-c", "mkdir -p /data && touch /data/shoperp.db && tail -f /dev/null"]
    volumes:
      - shoperp_sqlite_data:/data
    networks:
      - vanan-network
    restart: unless-stopped
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"

  khachlink-sqlite:
    image: alpine:latest
    container_name: vanan-khachlink-sqlite
    command: ["sh", "-c", "mkdir -p /data && touch /data/khachlink.db && tail -f /dev/null"]
    volumes:
      - khachlink_sqlite_data:/data
    networks:
      - vanan-network
    restart: unless-stopped
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"

  order-station-sqlite:
    image: alpine:latest
    container_name: vanan-order-station-sqlite
    command: ["sh", "-c", "mkdir -p /data && touch /data/order.db && tail -f /dev/null"]
    volumes:
      - order_sqlite_data:/data
    networks:
      - vanan-network
    restart: unless-stopped
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"
```

**Lý do design:**
- Alpine image nhẹ (~5MB) cho sidecar containers
- Command tạo empty SQLite file + keep container running với `tail -f /dev/null`
- Logging config nhất quán với các services khác
- Phase 1: sidecars tồn tại nhưng không được sử dụng bởi main services

---

## 4. HARDENING GATES

- [ ] Sidecar containers KHÔNG affect v1 SaaS operation (PostgreSQL vẫn primary)
- [ ] Alpine image version pinned (alpine:latest → alpine:3.19 cho production stability)
- [ ] Logging config nhất quán với existing services
- [ ] Health checks KHÔNG cần thiết cho sidecars (Phase 1 — không active)
- [ ] KHÔNG sửa existing services (shoperp, khachlink, nginx, etc.)
- [ ] KHÔNG sửa existing volumes

---

## 5. VALIDATION

```powershell
# Validate docker-compose syntax
docker compose -f docker-compose.prod.yml config

# Verify sidecar containers can start (dry-run)
docker compose -f docker-compose.prod.yml up shoperp-sqlite khachlink-sqlite order-station-sqlite --dry-run

# Verify volume creation
docker compose -f docker-compose.prod.yml config | grep "volumes:"
```

---

## 6. EXIT CRITERIA

- [ ] 3 sidecar containers added to docker-compose.prod.yml (shoperp-sqlite, khachlink-sqlite, order-station-sqlite)
- [ ] Sidecar containers use alpine:3.19 (pinned version)
- [ ] Sidecar containers have logging config nhất quán
- [ ] docker-compose.prod.yml syntax valid (`docker compose config` pass)
- [ ] Existing services unchanged (v1 SaaS behavior preserved)
- [ ] Proceed to W4-1-T2 (Add Docker volumes for SQLite persistence)