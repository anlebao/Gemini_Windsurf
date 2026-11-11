# ROLLBACK — vanan-voice Coturn (SRS v1.1 §21: mọi infra change phải có rollback)

Nguyên tắc: **TURN là optional** — voice service vẫn hoạt động P2P/STUN khi TURN tắt.
Rollback không bao giờ đụng call đang chạy (P2P media không đi qua Coturn khi đã connect).

## Tình huống A — Coturn lỗi/treo (rollback nhanh, < 1 phút)

```bash
# 1. Tắt container (giữ cấu hình)
docker stop vanan-turn

# 2. Voice service trả 503 cho /v1/turn/credentials → SDK fail-open:
#    - call MỚI: fetch TURN fail → dùng STUN-only ICE → P2P nếu mạng cho phép (SRS G9 fail-open)
#    - call đang chạy: media P2P không bị ảnh hưởng (đã connect)
# 3. Debug: docker logs vanan-turn; kiểm tra firewall/ports
```

## Tình huống B — TURN bị abuse / tốn băng thông

```bash
# 1. Tắt hoàn toàn TURN ở voice service (không cần đụng VPS):
#    - Xóa TURN:Secret/TURN:PublicHost khỏi cấu hình voice service → /v1/turn/credentials 503
# 2. Mọi client fallback STUN-only (P2P)
# 3. Điều tra: coturn log (allocations), metering TURN %, alert >20%
```

## Tình huống C — Quay lại trạng thái trước (full restore)

```bash
# 1. Redeploy đúng cấu hình cũ
docker rm -f vanan-turn
bash deploy-turn.sh          # cùng env TURN_*

# 2. Verify
docker logs vanan-turn | tail
curl -H "Authorization: Bearer <jwt>" <voice-service>/v1/turn/credentials   # 200
```

## Check list sau rollback

- [ ] `/v1/turn/credentials` trả 503 (A/B) hoặc 200 (C)
- [ ] Gọi thử 1 call P2P vẫn connect (không TURN)
- [ ] Ghi nhận sự cố + thời gian vào project_state / ledger

## Không bao giờ

- Không xóa `turnserver.conf` đã sinh (chứa secret cũ — xóa = mất khả năng debug).
- Không đổi `TURN_SECRET` mà không đồng bộ voice service (credential HMAC fail → TURN vô dụng).
- Không mở relay range ra public nếu chưa có quota/user-quota (abuse).