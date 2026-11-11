# Task P5 — PILOT: Voice Service (`vanan-voice`)

> **Status:** READY — code + metrics + checklist hoàn tất 11/11/2026 (dotnet 79/79 · node 18/18) · **cần VPS + user phối hợp để chạy pilot thật**
> **Created:** 2026-11-11 · **Phase:** P5 — theo `docs/AI/plans/voice-service-master-plan.md`
> **Tài liệu gốc:** SRS v1.1 §17 Gate 1, §12/§13 (re-baseline)

---

## Đã sẵn sàng (session này)

- **`GET /v1/metrics/summary`** (VoiceMetricsService + tests 2/2): total/today/connected/rejected/missed/failed/busy/offline, METERING_DENIED, FEATURE_DISABLED, TURN relayed + %, avg duration — nguồn baseline Gate 1.
- Toàn bộ P1–P4 (signaling + SDK + TURN + chat UI) xanh: **dotnet 79/79 · node 18/18**.
- Deploy artifacts: `deploy/turn/` (compose + conf + firewall + rollback) · Dockerfile 5070 · SDK README.

## PILOT CHECKLIST (chạy khi có VPS — user phối hợp)

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