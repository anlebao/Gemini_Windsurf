# Task P3 — COTURN FALLBACK: Voice Service (`vanan-voice`)

> **Status:** DONE (code + tests + deploy artifacts) — 11/11/2026 · **dotnet 72/72 · node 14/14 pass** · chưa deploy VPS (cần node Coturn thật + network test)
> **Created:** 2026-11-11 · **Phase:** P3 — theo `docs/AI/plans/voice-service-master-plan.md` (chạy SONG SONG với P2)
> **Tài liệu gốc:** SRS v1.1 §8 (STUN/TURN), §10 SEC-004, §14.5 config

---

## KẾT QUẢ (11/11/2026)

### Code — TURN credential issuance (SRS SEC-004, TURN REST)

- **`TurnCredentialService`** (`Services/TurnCredentialService.cs`): username = `{unixTimestamp}:{callerId}`, credential = `base64(HMAC-SHA1(TURN_SECRET, username))` — đúng Coturn TURN REST scheme (use-auth-secret + static-auth-secret verify phía server). TTL mặc định 1800s (`TURN:CredentialTtlSeconds`). URLs: UDP primary + TCP fallback + TLS (`turns:`). Không cấu hình → trả null (SDK fallback STUN-only).
- **Endpoint `GET /v1/turn/credentials`** (auth như hub: query `access_token` / `Authorization: Bearer`; rate limit **30/h/user** SEC-005; 401 chưa auth, 503 chưa cấu hình TURN).
- Config: `TURN:PublicHost/UdpPort/TcpPort/TlsPort/Secret/Realm/CredentialTtlSeconds` (env `TURN__*`).
- **SDK**: `resolveIceServers()` — fetch credentials **mỗi call** qua `turnCredentialsUrl` (kèm token), **fail-open**: lỗi/503 → STUN-only ICE, P2P vẫn chạy (SRS G9 tinh thần). Nếu không set `turnCredentialsUrl` → dùng `iceServers` cấu hình tay.

### Tests

- `TurnCredentialServiceTests` 4/4: not-configured→null · username format + signature recompute (base64 HMAC-SHA1 khớp) · URLs UDP/TCP/TLS · TTL configurable · tls-port=0 → không có `turns:`.
- `TurnEndpointTests` 3/3 (WebApplicationFactory): 401 khi chưa auth · 200 + shape khi có JWT + TURN configured · 503 khi TURN chưa cấu hình.
- Node SDK 2 tests mới (14/14 tổng): fetch TURN per-call + pass iceServers vào createPeer · fetch fail → fallback STUN-only.

### Deploy artifacts (`deploy/turn/`)

| File | Nội dung |
|---|---|
| `turnserver.conf` | Template: use-auth-secret + static-auth-secret, realm, fingerprint, **user-quota 12 / total-quota 1200 / max-bps 1Mbps** (abuse), denied-peer-ip private ranges, no-TLSv1, relay min/max port |
| `deploy-turn.sh` | Sinh conf từ env (secret không lộ command line) + docker run coturn/coturn:4.6, port map UDP/TCP 3478 + TLS 5349 + relay range |
| `FIREWALL.md` | Rules (UDP 3478 + TCP 3478 + TLS 5349 + relay UDP range), GCP/ufw commands, ma trận test mạng (home Wi-Fi / 4G / corporate / CGNAT / UDP-blocked) |
| `ROLLBACK.md` | 3 tình huống: Coturn lỗi (stop container → 503 → SDK fail-open) · abuse (tắt TURN ở voice service) · full restore (redeploy + verify) + checklist + "không bao giờ" |

## Còn lại (cần VPS thật — ghi nhận, không phải bug)

1. **Deploy thật**: chạy `deploy-turn.sh` trên node Coturn (tách CoreHub DB workload — SRS §8.2) + set `TURN__*` env cho voice service + firewall rules.
2. **Forced-TURN test** (P5 pilot): ép ICE dùng relay → call connect P95 < 8s; UDP-blocked → TCP/TLS fallback; credential hết hạn → từ chối.
3. **Monitoring**: TURN allocations/bandwidth (coturn log → metrics) — voice đã ghi `ConnectionType` P2P|TURN trong `voice_calls` (P1) + alert >20% TURN (SRS §15) — wire vào monitoring hệ thống khi deploy.

## Acceptance

1. 🔶 TURN-only call connect — pending VPS (code + config sẵn sàng).
2. ✅ Firewall doc + rollback procedure có (chưa test thật trên VPS).
3. ✅ Credential TTL 30 phút; secret không bao giờ vào frontend/repo (template + env).
4. ✅ Metrics: `ConnectionType` ghi từ P1; TURN endpoint rate-limited.