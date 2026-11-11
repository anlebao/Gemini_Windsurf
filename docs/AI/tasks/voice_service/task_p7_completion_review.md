# Task P7 — REVIEW: Hoàn thiện Voice Service 100% (các yêu cầu nghiệp vụ còn thiếu)

> **Status:** CHỜ REVIEW — ghi lại toàn bộ yêu cầu còn thiếu để không miss (11/11/2026)
> **Nguồn:** review định nghĩa "hoàn thành 100%" (UI thao tác được + NavMenu/Sitemap + RV sweep-first) + câu hỏi SystemAdmin (tenant config + mức phí)
> **Trạng thái hiện tại:** P1–P5 code xong · dotnet 79/79 · node 18/18 · VPS voice + coturn deploy (health OK) · CI/CD wired (`5777c2b9`)

---

## 🔄 SESSION HANDOFF (11/11/2026 — session 2 ĐÓNG, chờ CD + mở session mới)

### Đã hoàn thành (session này)
- **(a)** metering: caller `voice` seeded (auth 200/401 verify) + GH secret `METERING_CALLERS` updated + rate card `voice.call`=0đ / `voice.minute`=1.000đ (Enforcement=MeterOnly — user chốt) · wallet check resolve OK.
- **(b)** GoDaddy PAT mới (user cung cấp `gd_pat_*` — key cũ 401 vì expired) → set `.env.gateway` + GH secret `GODADDY_API_KEY` · A record `voice → 34.87.116.62` (Godaddy API verify) · nginx + certbot trên voice VPS (443→127.0.0.1:5070 + WebSocket upgrade) · `https://voice.khachvip.online/v1/health` 200 + `/v1/turn/credentials` reachable (401 no-auth đúng) · firewall thêm allow-voice-http/https.
- **C1/C2** code: ShopInbox + KhachLink (ChatPanel shim + OrderTracking/DeliveryTracking/Store) wire `VoiceCallEnabled`/`VoiceServiceUrl`/`VoiceHubUrl`; SDK assets (signalr.min.js 8.0.7 + voice-media.js + voice-sdk.js) vào `UI.Platform/wwwroot/js/` + load ở ShopERP App.razor + KhachLink index.html; appsettings `Voice:ServiceUrl/HubUrl` prod (`https://voice.khachvip.online[/hubs/voice]`).
- **C3** (user chốt **Gateway PG** — SRS-compliant, không vi phạm guardrail 2 bảng): `VoiceTenantConfig` (Domain) + EF config + migration `AddVoiceTenantConfig` (PG, auto-migrate Gateway startup) · Gateway `/api/admin/voice-config` GET/PUT (SystemAdmin JWT) + `VoiceTenantConfigService` · ShopERP `/admin/voice` + NavMenu + Sitemap + bUnit **3/3** · voice `TenantFeatureStore` PG-backed (Npgsql, cache 30s, fail-open default enabled) · **PUT không-auth bị chặn** (`Voice:AllowInsecureAdminApi` default false; test/E2E bật dev-only) · E2E spec `voice-admin.spec.ts` (Gate 4) + p2p config flag.
- **C4** deny/kill-switch UX: `VoiceCallPanel` parse reason từ state info — `METERING_DENIED` → ShopERP "Hết hạn mức phí nền tảng" + link `/wallet/phi-nen-tang` (param `ShowWalletLink`, RealtimeChatPanel truyền `IsStaff`) / KhachLink trung tính "tính năng tạm ngừng" · `FEATURE_DISABLED` → "tính năng tạm ngừng" · missed-hint chỉ `missed`/`RING_TIMEOUT` (sửa bug: denied không hiện "cuộc gọi nhỡ").
- **C5** `6_Testing/rv-scripts/voice-rv-sweep.mjs` (L1 health/metrics · L2 TURN/missed-calls auth 401/403/200 · L2b voice-config PG-backed · L5 kill-switch config toggle qua Gateway admin API + restore cleanup).
- **Validate:** voice dotnet **79/79** · node **25/25** · bUnit voice-admin **3/3** · ShopERP+KhachLink build OK · guard-check ALL PASSED (`guard-report-20261112-052648`) · commits `48461b93` (C1-C4, 40 files) + `b168e057` (C5) — **PUSHED** (main SHA khớp).

### BLOCKER đã xử lý + CÒN theo dõi
- **CD #43111872269 attempt 4 đang chạy** (deploy-voice SSH auth fail attempt 1-3 sau VPS reset: authorized_keys `/home/lebao/.ssh/` mất CD key — CD user = `lebao` vì owner `/opt/vanan-voice`; fix: ed25519 `github-actions-voice-deploy` mới append + GH secret `VPS_VOICE_SSH_PRIVATE_KEY` updated 22:42Z). **Chờ user báo CD xong** (deploy-voice + Post-Deploy Smoke).
- **Nếu attempt 4 vẫn fail**: check log `gh run view 43111872269 --log-failed` — có thể VPS_VOICE_USER khác (`deploy`?) → test `ssh -i <key> deploy@34.87.116.62` (key cũng đã append `/home/deploy/.ssh/`).
- Voice VPS đã có nginx + certbot + firewall 80/443 — CD deploy-voice không đụng nginx (compose chỉ voice+turn).

### Việc còn lại (session mới — sau CD xong)
1. **RV production C5**: `node 6_Testing/rv-scripts/voice-rv-sweep.mjs` — env `VOICE_JWT_SECRET` extract từ `/opt/vanan-voice/.env.voice` (gcloud, không in giá trị, xóa temp) → 0 FAIL · verify migration PG + `/admin/voice` live.
2. **C6 E2E**: `voice-p2p.spec.ts` (máy WebRTC ICE thật) + `voice-chat.spec.ts` + `voice-admin.spec.ts` + regression chat + deny/kill-switch E2E → ledger `6_Testing/reports/playwright-ledger.md` + residue cleanup.
3. **Đóng task card P7** + update `docs/AI/project_state.md`.

### Tham chiếu nhanh
- Voice service code: `10_Voice/vanan-voice/` · CI: `.github/workflows/cd-multivps.yml` (job `deploy-voice`) · compose: `docker-compose.voice.yml` · impact: `deployment-impact.yml` (10_Voice/** → voice)
- SDK/demo: `10_Voice/vanan-voice/sdk/` (README hướng dẫn chạy) · tests node: `6_Testing/unit-tests/voice-*.test.mjs`
- Task cards: `docs/AI/tasks/voice_service/` (p0–p7) · master plan: `docs/AI/plans/voice-service-master-plan.md`
- Admin UI: Gateway `VoiceAdminController` (`/api/admin/voice-config`) · ShopERP `VoiceAdmin.razor` (`/admin/voice`) · voice `TenantFeatureStore` PG-backed

---

## Mục lục yêu cầu (đánh dấu sau khi xong)

- [x] **(a)** Seed metering caller `voice` + rate card `voice.*` — áp phí thật
- [x] **(b)** DNS `voice.khachvip.online` + TLS cert (lựa chọn A)
- [x] **C1** ShopERP `ShopInbox` wire voice call (merchant nhận call) — BẮT BUỘC (code + scripts load SDK)
- [x] **C2** Cấu hình `VoiceServiceUrl`/`VoiceHubUrl` production cho KhachLink + ShopERP
- [x] **C3** Admin UI `/admin/voice` (tenant voice config) + **NavMenu + Sitemap** + persistence DB (Gateway PG — user chốt 11/11) + bUnit 3/3
- [x] **C4** Deny/kill-switch UX: message + link nạp phí (ShopERP `/wallet/phi-nen-tang`; KhachLink trung tính)
- [ ] **C5** RV sweep script `6_Testing/rv-scripts/voice-rv-sweep.mjs` (sweep-first, đủ case)
- [ ] **C6** Chạy E2E trên môi trường WebRTC ICE hoạt động + regression chat (Gate 4) + ledger

---

## (a) Seed metering — SystemAdmin thiết lập MỨC PHÍ voice

**Yêu cầu nghiệp vụ:** SystemAdmin phải thiết lập được phí voice service (rate card) như các dịch vụ khác.

| # | Việc | Chi tiết | Trạng thái |
|---|---|---|---|
| a1 | Seed caller `voice` vào metering | Thêm `voice:<key>` vào `Metering:BootstrapCallers` trên VPS metering + GitHub secret `METERING_CALLERS` + restart metering | ✅ DONE 11/11 (auth 200/401 verify) |
| a2 | Tạo rate card `voice.call` + `voice.minute` | Qua admin API `/v1/rate-cards` — `Enforcement=MeterOnly` (pilot), creditRate theo quyết định user (call=0đ · minute=1.000đ) | ✅ DONE 11/11 |
| a3 | Verify ingest | Call thử → usage event trong metering (`voice.call`/`voice.minute`, Idempotency-Key=callId) | ✅ DONE (rate resolve qua wallet check · ingest thật trong C5) |

**Acceptance:** SystemAdmin vào `/admin/metering/rate-cards` thấy/sửa được `voice.*`; usage hiện trong summary metering.

## (b) DNS + cert

| # | Việc | Chi tiết |
|---|---|---|
| b1 | DNS A record | ✅ `voice.khachvip.online` → `34.87.116.62` (PAT mới `gd_pat_...` set `.env.gateway` + GH secret 11/11) |
| b2 | TLS cert | ✅ Letsencrypt cho `voice.khachvip.online` (nginx + certbot trên voice VPS, proxy 443→127.0.0.1:5070 + WebSocket upgrade) |
| b3 | Verify | ✅ `https://voice.khachvip.online/v1/health` 200 + `/v1/turn/credentials` reachable (401 no-auth đúng) |

## C1 — Merchant nhận cuộc gọi (BẮT BUỘC)

**Yêu cầu nghiệp vụ:** merchant (ShopERP) phải nhận được cuộc gọi từ khách trong chat — call 2 chiều.

- [ ] `5_WebApps/ShopERP/Components/Pages/Community/ShopInbox.razor`: truyền `VoiceCallEnabled` (flag) + `VoiceServiceUrl` + `StaffToken` vào `RealtimeChatPanel`.
- [ ] Verify: khách gọi → merchant thấy incoming overlay (Nghe/Từ chối) trong ShopInbox.

## C2 — Cấu hình URL voice service (host)

**Yêu cầu nghiệp vụ:** nút call phải kết nối đúng voice service (không trỏ nhầm origin).

- [ ] KhachLink + ShopERP: cấu hình `VoiceServiceUrl=https://voice.khachvip.online` + `VoiceHubUrl=https://voice.khachvip.online/hubs/voice` (production), CORS đã allow cả 2 origin.

## C3 — Admin UI: SystemAdmin thiết lập voice cho tenant

**Yêu cầu nghiệp vụ (câu hỏi user):** SystemAdmin phải thiết lập được voice service per-tenant qua UI.

- [ ] Trang `/admin/voice`: danh sách tenant + toggle `VoiceFeatureEnabled` (kill-switch) + `VoiceOpenCalling` + link wallet.
- [ ] **NavMenu** (`ShopErpMenuService`) + **Sitemap** + bUnit test.
- [ ] **Persistence**: chuyển `TenantFeatureStore` in-memory → lưu DB voice (bảng mới hoặc tái dùng — cần Gate review vì SRS giới hạn 2 bảng).
- [ ] E2E (Gate 4) cho trang admin.

**Acceptance:** SystemAdmin bật/tắt voice cho tenant X → call từ tenant X bị chặn (FEATURE_DISABLED) / được phép; giữ nguyên sau restart voice service.

## C4 — Deny/kill-switch UX

**Yêu cầu nghiệp vụ:** người dùng hiểu vì sao không gọi được + biết cách nạp phí.

- [ ] `METERING_DENIED` → ShopERP: message + link `/wallet/phi-nen-tang`; KhachLink: message trung tính (không lộ ví).
- [ ] `FEATURE_DISABLED` → "tính năng tạm ngừng" (cả 2 app).

## C5 — RV sweep (sweep-first, đủ case)

**Yêu cầu:** RV tự động bao phủ các trường hợp — theo convention `6_Testing/rv-scripts/*.mjs`.

- [ ] L1: `/v1/health` + `/v1/metrics/summary` 200
- [ ] L2: TURN creds 200 · missed-calls auth (401/403/200) · metrics có số liệu sau call
- [ ] L3: call flow SDK (2 tab demo qua domain) — StartCall→Accept→Connected
- [ ] L4: chat UI — nút call hiện/ẩn theo flag · incoming overlay · busy/offline/missed
- [ ] L5: deny (METERING_DENIED) · kill-switch (FEATURE_DISABLED) · mic denied · cross-tenant reject · regression chat (nhắn tin không đổi)
- [ ] Residue cleanup (xóa call test + event test)

## C6 — E2E + regression chạy thật

- [ ] `voice-p2p.spec.ts` + `voice-chat.spec.ts` chạy trên CI runner/máy có WebRTC ICE (flag `--allow-loopback-in-peer-connection` + fake media) — env local này chặn ICE.
- [ ] Regression chat spec (Gate 4): chat hoạt động y hệt sau khi ghép VoiceCallPanel.
- [ ] Ghi ledger `6_Testing/reports/playwright-ledger.md`.

---

## Thứ tự đề xuất (chờ user duyệt)

1. **(a)** seed metering → phí áp dụng thật
2. **C1** ShopInbox (merchant nhận call) + **C2** URL production
3. **(b)** DNS + cert
4. **C3** admin UI + persistence
5. **C4** deny/topup UX
6. **C5 + C6** RV sweep + E2E + regression

## Ghi chú kỹ thuật (đã verify)

- VPS voice: `34.87.116.62` (ext) · `10.148.0.8` (int) · container `vanan-voice` health OK · coturn `vanan-turn` chạy.
- CI/CD: `cd-multivps.yml` deploy-voice job + `docker-compose.voice.yml` + `deployment-impact.yml` (`10_Voice/**`) — commit `5777c2b9`.
- Secrets GitHub đã set: `VPS_VOICE_HOST/USER/SSH_PRIVATE_KEY` + `VOICE_JWT_SECRET/TURN_SECRET/METERING_KEY/GATEWAY_DB`.