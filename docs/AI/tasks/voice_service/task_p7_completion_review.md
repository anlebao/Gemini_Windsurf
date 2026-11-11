# Task P7 — REVIEW: Hoàn thiện Voice Service 100% (các yêu cầu nghiệp vụ còn thiếu)

> **Status:** CHỜ REVIEW — ghi lại toàn bộ yêu cầu còn thiếu để không miss (11/11/2026)
> **Nguồn:** review định nghĩa "hoàn thành 100%" (UI thao tác được + NavMenu/Sitemap + RV sweep-first) + câu hỏi SystemAdmin (tenant config + mức phí)
> **Trạng thái hiện tại:** P1–P5 code xong · dotnet 79/79 · node 18/18 · VPS voice + coturn deploy (health OK) · CI/CD wired (`5777c2b9`)

---

## 🔄 SESSION HANDOFF (11/11/2026 — đóng session để mở session mới)

### Đã hoàn thành
- P0→P5 code + tests (79/79 dotnet · 18/18 node) · commit `8756f51a` (voice + CI/CD) + `2b37acc5` (bỏ publish/) + `5777c2b9` (deployment-impact voice) — đã push.
- VPS `vanan-voice` (ext `34.87.116.62` · int `10.148.0.8` · asia-southeast1-b): container `vanan-voice` health OK + `vanan-turn` (coturn) chạy; firewall `voice-turn` (3478 udp/tcp, 5349 tcp, relay udp, 5070 tcp).
- Env: `/opt/vanan-voice/.env.voice` (Jwt__Secret từ gateway, TURN, Metering caller `voice`, GatewayDb=PG 10.148.0.2) + `/opt/vanan-voice/.env.gateway` (bản copy).
- GitHub secrets đã set: `VPS_VOICE_HOST/USER/SSH_PRIVATE_KEY` · `VOICE_JWT_SECRET` · `VOICE_TURN_SECRET` · `VOICE_METERING_KEY` · `VOICE_GATEWAY_DB`.

### BLOCKER cần xử lý đầu session mới
- **SSH tới `vanan-voice` bị flaky** (plink drop "Remote side unexpectedly closed network connection" / treo) — lệnh (a) dở dang. Pattern ĐÃ chạy được:
  ```
  gcloud compute ssh vanan-voice --zone=asia-southeast1-b --ssh-flag="-hostkey SHA256:/Xq+Ag8u/2v6r0yIsQaKRbp+NMWok14P424ebbcFe/A" --quiet --command="..."
  ```
  (bắt buộc `-hostkey` vì plink; retry nếu drop; SSH tới `vanan-metering`/`vanan-gateway` ổn định hơn).
- **(a) chưa xong**: `voice:<key>` CHƯA vào `/opt/vanan-metering/.env.metering` + GitHub secret `METERING_CALLERS` (còn 2026-11-06) → metering chưa nhận caller `voice` (fail-open nên call vẫn chạy, chưa trừ phí). Rate card `voice.call`/`voice.minute` CHƯA tạo (admin API localhost:5050 trên metering VPS, key `METERING_ADMIN` trong `.env.metering`).

### Việc còn lại (đã duyệt — thực thi theo thứ tự)
1. **(a)** seed caller `voice` + rate card (xem BLOCKER)
2. **C1** ShopERP `ShopInbox.razor` wire `VoiceCallEnabled`+`VoiceServiceUrl`+`StaffToken` vào RealtimeChatPanel (local code)
3. **C2** URL production `VoiceServiceUrl=https://voice.khachvip.online` + `VoiceHubUrl` cho KhachLink + ShopERP (local code + config)
4. **(b)** DNS `voice.khachvip.online` → 34.87.116.62 (Godaddy API key trong `/opt/vanan/.env.gateway` trên gateway VPS) + TLS cert + verify health qua domain
5. **C3** Admin UI `/admin/voice` (toggle VoiceFeatureEnabled/VoiceOpenCalling per tenant) + NavMenu + Sitemap + persistence DB + bUnit + E2E
6. **C4** Deny/kill-switch UX: `METERING_DENIED` → link `/wallet/phi-nen-tang` (ShopERP) / message trung tính (KhachLink); `FEATURE_DISABLED` → "tính năng tạm ngừng"
7. **C5** RV sweep `6_Testing/rv-scripts/voice-rv-sweep.mjs` (health/metrics/TURN/missed-calls/call flow/chat UI/deny/kill-switch/cross-tenant/regression + residue)
8. **C6** E2E `voice-p2p.spec.ts`+`voice-chat.spec.ts` trên môi trường WebRTC ICE hoạt động + regression chat + ledger

### Tham chiếu nhanh
- Voice service code: `10_Voice/vanan-voice/` · CI: `.github/workflows/cd-multivps.yml` (job `deploy-voice`) · compose: `docker-compose.voice.yml` · impact: `deployment-impact.yml` (10_Voice/** → voice)
- SDK/demo: `10_Voice/vanan-voice/sdk/` (README hướng dẫn chạy) · tests node: `6_Testing/unit-tests/voice-*.test.mjs`
- Task cards: `docs/AI/tasks/voice_service/` (p0–p7) · master plan: `docs/AI/plans/voice-service-master-plan.md`

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