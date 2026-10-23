# Task P5 — Deploy `vanan-metering` lên VPS RIÊNG + Runtime Verification

> **Status:** ✅ ĐÃ DUYỆT — VPS riêng (user chốt), sẵn sàng implement
> **Phase:** P5 (GĐ-1) · **Gate:** CD pass + health 200 + RV L1-L2
> **Căn cứ:** master plan §6.5 RV · cd-multivps.yml matrix `metering` (P1 đã wire) · Pattern #32 (image phải build trước deploy)

---

## 1. Mục tiêu

`vanan-metering` chạy container trên **VPS riêng** (độc lập hoàn toàn khỏi stack Vạn An — đúng định hướng D3 policy "container riêng/VPS riêng"), reachable qua HTTPS cho các caller ở VPS khác.

## 2. Quyết định thiết kế — ĐÃ CHỐT

| # | Điểm | Quyết định |
|---|---|---|
| D1 | VPS | **VPS riêng** mới cho metering (user chốt) — caller (ShopERP/Gateway/Directory) ở VPS khác gọi qua mạng ngoài |
| D2 | Compose | **`docker-compose.metering.yml`** riêng — file mới trong repo, deploy lên VPS metering. Không động vào `docker-compose.shoperp.yml` |
| D3 | Public reachability — **CHỐT: qua domain app2 hiện có** | KHÔNG domain mới (user chốt). nginx trên **shop-a** (đang serve `app2.khachvip.online` + TLS sẵn) thêm location `location /admin/metering/api/ { proxy_pass http://<metering-vps>:5050/; }` → API metering = `https://app2.khachvip.online/admin/metering/api/v1/...`. **Path conflict đã xử:** prefix `/admin/metering/api/` đi metering; `/admin/metering/rate-cards`... vẫn do Blazor ShopERP phục vụ (P6 UI) — nginx location cụ thể hơn thắng. VPS metering firewall chỉ mở `5050` cho **IP shop-a** (tất cả caller đi qua app2 domain → chỉ shop-a cần reach 5050). Caller-key auth vẫn bắt buộc lớp 2 |
| D4 | Volume | `metering-data:/app/data` named volume — SQLite persist qua deploy |
| D5 | Secrets | `Metering__BootstrapCaller` = `shoperp-a:{key}` (+ `gateway`, `directory` callers) + `Metering__BootstrapAdmin` = `sysadmin:{key}` — qua `.env` trên VPS metering, KHÔNG commit key |
| D6 | Deploy path | `cd-multivps.yml`: deploy job cho app `metering` → SSH **VPS metering** → pull image + `docker compose -f docker-compose.metering.yml up -d` + `ensure_image` (Pattern #32). Verify tên/pattern secrets SSH hiện hữu của workflow (vd `*_HOST`/`*_USER`/`*_SSH_KEY`) lúc implement — mirror đúng convention |
| D7 | Healthcheck | Dockerfile `HEALTHCHECK` đã có (P1) + compose `healthcheck` + deploy script chờ healthy |

## 3. Điều kiện cần từ user (ops — chặn P5 nếu thiếu)

| # | Item | Ghi chú |
|---|---|---|
| O1 | **VPS metering đã provision** (IP, OS Ubuntu, Docker) | user mua/setup |
| O2 | **GitHub secrets** cho deploy: host/IP · SSH user · SSH key của VPS metering | mirror naming convention secrets VPS hiện có |
| O3 | **Firewall VPS metering**: mở `5050` chỉ cho IP shop-a | 1 rule iptables/ufw — thực hiện lúc deploy |
| O4 | `.env` nội dung trên VPS metering (caller keys + admin key) | tạo trong quá trình deploy |

*(Không cần domain/DNS mới — user chốt dùng `app2.khachvip.online/admin/metering/api/`.)*

## 4. Changes

| File | Action |
|---|---|
| `docker-compose.metering.yml` | BUILD — service `vanan-metering` (image ghcr, env `Metering__DbPath`/`BootstrapCaller`/`BootstrapAdmin`, volume `metering-data`, `5050:5050` — firewall giới hạn IP shop-a, restart unless-stopped) |
| `.github/workflows/cd-multivps.yml` | MODIFY — deploy job/target cho `metering` (SSH VPS metering, pull+up, ensure_image) |
| `scripts/` deploy metering | theo pattern script VPS hiện hữu (đọc lúc implement) |
| `nginx/` conf shop-a (repo) | MODIFY — location `/admin/metering/api/` → `http://<metering-vps>:5050/` (strip prefix) — **nginx/ thuộc ops_paths → trigger full deploy_set (impact rules)** |
| `.env` VPS metering (ngoài repo) | caller + admin keys |
| `.env` VPS metering (ngoài repo) | caller + admin keys |

## 5. RV plan (STOP at first failure)

| Layer | Check | Pass criteria |
|---|---|---|
| L1 | Image GHCR `ghcr.io/anlebao/vanan-metering:<sha>` + container Up+healthy trên VPS metering | `docker ps` healthy |
| L2 | Trên VPS metering: `curl 127.0.0.1:5050/v1/health` → 200 · từ ngoài: `https://app2.khachvip.online/admin/metering/api/v1/health` → 200 · `.../auth/check` no-auth → 401 · đúng key → 200 · sai → 401 · **từ IP lạ (không phải shop-a): `curl http://<metering-ip>:5050/v1/health` → bị firewall chặn** | đủ 6 check |
| L2b | Rate card e2e qua `app2/admin/metering/api`: POST (admin) → GET list → PATCH (version mới, cũ đóng) → DELETE → resolve đúng | CRUD qua TLS |
| L3 | Ingest e2e: POST event → summary có count → replay cùng key → count không đổi | shadow works |
| L4 | (sau P4) event thật từ ShopERP ở VPS khác → summary | defer |
| L5 | manual user | — |

**Rollback:** `docker compose down` trên VPS metering — 0 ảnh hưởng stack Vạn An (chưa caller nào phụ thuộc đến P4).

## 6. Test/verify ngoài RV

- `docker compose -f docker-compose.metering.yml config` validate trước push
- YAML lint cd-multivps edit; `impact-analysis` apps map đã có `metering` (P1)
- Verify latency thật caller→metering qua WAN (đo `curl -w https://app2.khachvip.online/admin/metering/api/v1/health`): kỳ vọng ≤50ms cùng region — nếu VPS metering khác region xa → báo user cân nhắc (check API có cache 30s nên chịu được, nhưng ingest cũng qua WAN)

## 7. Rủi ro

| Rủi ro | Giảm thiểu |
|---|---|
| Endpoint public (qua app2 domain) → bị scan/spam event giả | TLS sẵn trên app2 + caller-key auth (401) + nginx `limit_req` + firewall 5050 chỉ mở shop-a → attack surface trực tiếp ~0 |
| WAN latency/availability: caller VPS khác mất kết nối metering | client timeout 3s + circuit breaker + fire-and-forget (P4) — nghiệp vụ không chết; check API cache 30s |
| SQLite volume mất khi recreate VPS | named volume + hướng dẫn backup (đơn giản: nightly `sqlite3 .backup` hoặc tar volume — GĐ-1 ghi quy trình tay, automation GĐ-3) |
| Secrets caller key lộ | `.env` ngoài repo + file mode 600; key rotation = sửa CallerKeys trong DB |
| Cert/domain chưa sẵn sàng | phương án tạm firewall-allowlist (ghi rõ kém an toàn, chặn trước thu tiền thật) |

## 8. File plan

`docker-compose.metering.yml` BUILD · `.github/workflows/cd-multivps.yml` MODIFY (deploy target metering → VPS riêng) · `.env` VPS tạo ngoài repo · nginx conf trên VPS (ops). KHÔNG code mới trong service.

**Out of scope:** backup automation · monitoring stack · multi-region/failover (GĐ-3) · CallerKey rotation tooling.
