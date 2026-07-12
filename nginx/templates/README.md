# Nginx Config Templates — VanAn

## Files

- `vanan.conf.template` — Production config (HTTP redirect + HTTPS). Active sau khi có SSL cert.
- `vanan-http.conf.template` — HTTP-only bootstrap config. Active khi chưa có SSL cert (cho ACME challenge).

## How It Works

`docker-entrypoint.sh` runs `envsubst` on the appropriate template at container startup:
- If SSL cert exists at `/etc/letsencrypt/live/${VANAN_DOMAIN}/fullchain.pem` → uses `vanan.conf.template` (HTTPS)
- If not → uses `vanan-http.conf.template` (HTTP-only, for ACME challenge)

Only `${VANAN_DOMAIN}` is substituted — nginx's own variables (`$host`, `$http_upgrade`, etc.) are preserved.

## Configuration

Set `VANAN_DOMAIN` in `.env` on the VPS:
```bash
VANAN_DOMAIN=khachvip.online
```

## SSL Bootstrap (chạy 1 lần)

```bash
# Trên VPS, sau khi deploy lần đầu:
sudo bash /opt/vanan/scripts/init-ssl.sh khachvip.online admin@khachvip.online
```

**Thứ tự thực hiện:**
1. Thêm DNS A records (xem bên dưới)
2. Set `VANAN_DOMAIN` trong `/opt/vanan/.env`
3. Deploy qua CD pipeline bình thường
4. SSH vào VPS, chạy `init-ssl.sh <domain> [email]`
5. Sau đó nginx tự restart với HTTPS

## DNS Records cần tạo

| Subdomain | Type | Value |
|---|---|---|
| `@` | A | `<VPS_IP>` |
| `www` | A | `<VPS_IP>` |
| `diemthuong` | A | `<VPS_IP>` |
| `app` | A | `<VPS_IP>` |
| `api` | A | `<VPS_IP>` |

## Routing

| Domain | → Container |
|---|---|
| `${VANAN_DOMAIN}` | vanan-shoperp:80 |
| `diemthuong.${VANAN_DOMAIN}` | vanan-khachlink:80 |
| `app.${VANAN_DOMAIN}` | vanan-shoperp:80 |
| `api.${VANAN_DOMAIN}` | vanan-gateway:80 |
