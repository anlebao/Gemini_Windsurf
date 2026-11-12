# Task P5 — PILOT: Voice Service (`vanan-voice`)

> **Status:** 🟡 INFRA + RV AUTOMATED DONE (2026-11-12) — voice+coturn deployed CD SUCCESS · rv-sweep 12/12 · call-sim CONNECTED end-to-end + metering ingest 200 · **còn call thật ≥100 (B3) + Gate 1 (B4) cần user/merchant thật**
> **Created:** 2026-11-11 · **Phase:** P5 — theo `docs/AI/plans/voice-service-master-plan.md`
> **Tài liệu gốc:** SRS v1.1 §17 Gate 1, §12/§13 (re-baseline)

---

## Đã sẵn sàng (session này)

- **`GET /v1/metrics/summary`** (VoiceMetricsService + tests 2/2): total/today/connected/rejected/missed/failed/busy/offline, METERING_DENIED, FEATURE_DISABLED, TURN relayed + %, avg duration — nguồn baseline Gate 1.
- Toàn bộ P1–P4 (signaling + SDK + TURN + chat UI) xanh: **dotnet 79/79 · node 18/18**.
- Deploy artifacts: `deploy/turn/` (compose + conf + firewall + rollback) · Dockerfile 5070 · SDK README.

## PILOT CHECKLIST (chạy khi có VPS — user phối hợp)

### Bước 0 — ĐÃ XONG (session 2026-11-12, agent)
- **CD deploy-voice fixed ×3:** (1) `93180b44` TURN `network_mode: host` — publish range 49152-65535 qua docker-proxy sinh ~16k process → OOM-wedge e2-small (snapd crash-loop, sshd chết, compose up treo 30m → CD timeout); VPS reset + dọn container cũ. (2) `6727e590` coturn args — bỏ `--no-lt-cred-mech`/`--no-dtlsv1` (unrecognized trong coturn 4.6) + peer-ip CIDR→`ip-ip` range (`--denied-peer-ip=10.0.0.0-10.255.255.255`...). (3) `eed7922b` RV findings: `VoiceTenantConfigService.IgnoreQueryFilters()` (BaseEntity:IMustHaveTenant → sysadmin tenant_id=Empty bị filter che hết → GET [] + PUT 404) · `TenantFeatureStore` uuid param (`tenantId.ToString()`→Guid, lỗi 42883) · `TURN__PublicHost` fallback `voice.khachvip.online` (secret `DEPLOY_VPS_VOICE_PUBLIC_IP` chưa set → TURN creds 503) · `02221110` metering `BaseUrl :5050` (thiếu port → usage events connection refused, fail-open ẩn bug).
- **Bug persist state:** `ecd28acf` — `FinalizeAsync` re-attach detached `VoiceCall` (hub scope khác → SaveChanges chỉ ghi event, row kẹt Ringing). Verified: call CB000AEF → Status=6 Ended/USER_ENDED/Duration=18s/P2P.
- **RV automated PASS:** `voice-rv-sweep.mjs` **12/12** (L1 health+metrics · L2 TURN creds 200 + missed-calls auth · L2b config GET · L5 kill-switch OFF→ON qua Gateway admin + PG-backed verify + restore) · `voice-call-sim.mjs` **3/3** (2 SignalR clients → RINGING→accept→SDP→CONNECTED→ENDED, events đầy đủ).
- **Metering ingest verified:** wallet check `voice.call`/`voice.minute` + `POST /v1/usage/events:batch` → **200** (sau fix :5050).
- **CD:** #43144523206 + #43147632169 + #43149094310 + #43150444872 ALL SUCCESS.

### Bước 1 — Hạ tầng
1. Deploy Coturn: `deploy/turn/deploy-turn.sh` (env `TURN_PUBLIC_HOST/TURN_SECRET/TURN_REALM`) + firewall rules (FIREWALL.md).
2. Deploy voice service: image `vanan-voice:<sha>` (Dockerfile 5070) + volume DB + env:
   `Jwt__Secret` (khớp Gateway) · `TURN__*` · `Metering__BaseUrl/CallerId=voice/CallerKey` · `Voice__GatewayDb` (read-only PG cho chat context) · `Voice__CorsOrigins` (KhachLink + ShopERP origins).
3. **Seed metering**: caller `voice` vào `Metering:BootstrapCallers` + rate card `voice.call`/`voice.minute` (**MeterOnly** ở pilot nếu tenant chưa nạp ví — SRS §14.1).
4. Cấu hình host: `VoiceServiceUrl`/`VoiceHubUrl` trong KhachLink `ChatPanel` + ShopERP (ShopInbox/ShopFeatures) + bật flag `Voice_Call_Enabled` cho 10 merchant pilot.

### Bước 2 — RV (runtime-verification, 5 lớp — STOP khi fail)
- **L1**: image + container healthy + `/v1/health` 200 + `/v1/metrics/summary` 200.
- **L2**: gọi thử qua SDK demo (2 tab) — StartCall→Accept→Connected; TURN creds 200; missed-calls 200.
- **L3**: call trong chat thật (KhachLink ↔ ShopERP inbox) với merchant pilot.
- **L4**: flow đầy đủ — call → usage event trong metering (`voice.call`/`voice.minute`) → wallet (MeterOnly không deny).
- **L5**: manual (user) — mic thật trên Chrome desktop + Android + iOS Safari.

### Bước 3 — Thu metrics (≥100 test calls, Wi-Fi + mobile mix)
- `/v1/metrics/summary` snapshot mỗi ngày pilot → bảng: P2P% · TURN% · failed% · avg duration · denied counts.
- **Re-baseline**: cập nhật SRS §12 (peak concurrency theo giờ) + §13.2 (TURN sensitivity) nếu số thật lệch giả định >20%.

### Bước 4 — Gate 1 (100 merchants)
- P2P success ≥ 85% · TURN fallback hoạt động · không critical auth issue · không regression chat/booking/shoperp.
- Báo cáo pilot + quyết định P6 rollout.