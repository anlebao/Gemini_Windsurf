# FIREWALL — vanan-voice Coturn node (SRS v1.1 §8.4)

Mở đúng 4 loại cổng dưới đây cho **địa chỉ công khai của node Coturn** (GCP firewall rule tag `voice-turn`). Không mở cổng khác. Node này **tách khỏi** CoreHub/Gateway — không chung workload DB.

| Port | Protocol | Mục đích | Bắt buộc |
|---|---|---|---|
| 3478 | UDP | STUN + TURN relay chính (SRS: UDP primary) | ✅ |
| 3478 | TCP | TURN TCP fallback (network chặn UDP) | ✅ |
| 5349 | TCP | TURN TLS (TURN_TLS_PORT) | theo cấu hình |
| `49152–65535` | UDP | Relay port range (TURN_MIN/MAX_RELAY_PORT) | ✅ |

## GCP (example)

```bash
gcloud compute firewall-rules create allow-voice-turn-udp \
  --allow udp:3478,udp:49152-65535 --target-tags voice-turn --direction INGRESS
gcloud compute firewall-rules create allow-voice-turn-tcp \
  --allow tcp:3478,tcp:5349 --target-tags voice-turn --direction INGRESS
```

## Ubuntu host (ufw)

```bash
ufw allow 3478/udp
ufw allow 3478/tcp
ufw allow 5349/tcp
ufw allow 49152:65535/udp
ufw enable
```

## Verify từ bên ngoài (máy dev — không phải node Coturn)

```bash
# STUN UDP reachable
curl -s "https://stun-test.example/?..."   # hoặc dùng trickle-ice / WebRTC samples

# TURN credential flow (endpoint voice service đã cấp)
curl -H "Authorization: Bearer <jwt>" http://<voice-service>/v1/turn/credentials
```

## Ma trận test mạng (SRS §8.4 — chạy ở P5 pilot)

| Môi trường | UDP direct | TURN UDP | TURN TCP/TLS | Kỳ vọng |
|---|---|---|---|---|
| Home Wi-Fi | ✅ | fallback | fallback | P2P |
| Mobile 4G/5G | ✅ thường | fallback | fallback | P2P phần lớn |
| Corporate network | ⚠️ chặn UDP | fallback | **TCP/TLS fallback** | TURN |
| Restrictive NAT (CGNAT) | ❌ | ✅ | fallback | TURN |
| UDP blocked hoàn toàn | ❌ | ❌ | ✅ | TURN TCP/TLS |

Theo dõi: TURN fallback % > 20% → cảnh báo + review cost (SRS §13.2, §15 alerts).