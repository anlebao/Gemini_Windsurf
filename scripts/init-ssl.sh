#!/bin/bash
# =============================================================
# scripts/init-ssl.sh
# Chạy 1 lần trên VPS để lấy SSL certificate từ Let's Encrypt
#
# Yêu cầu:
#   - DNS A records đã trỏ đúng về IP VPS (TTL propagated)
#   - Docker + docker compose đã cài
#   - Stack đã deploy ít nhất 1 lần (có certbot_www volume)
#
# Usage: sudo bash /opt/vanan/scripts/init-ssl.sh your@email.com
# =============================================================
set -e

DOMAIN="vanantech.io.vn"
EMAIL="${1:-admin@vanantech.io.vn}"
COMPOSE_FILE="/opt/vanan/docker-compose.prod.yml"
NGINX_CONF_DIR="/opt/vanan/nginx/conf.d"

echo "=== VanAn SSL Bootstrap ==="
echo "Domain : $DOMAIN"
echo "Email  : $EMAIL"
echo ""

# --- Step 1: Start nginx with HTTP-only config ---
echo "[1/4] Switching nginx to HTTP-only mode for ACME challenge..."

cat > /tmp/vanantech-bootstrap.conf << 'NGINXEOF'
server {
    listen 80;
    server_name vanantech.io.vn www.vanantech.io.vn diemthuong.vanantech.io.vn app.vanantech.io.vn api.vanantech.io.vn;

    location /.well-known/acme-challenge/ {
        root /var/www/certbot;
    }

    location / {
        return 200 'VanAn Tech - SSL Bootstrap';
        add_header Content-Type text/plain;
    }
}
NGINXEOF

# Backup existing config and use bootstrap
sudo cp "$NGINX_CONF_DIR/vanantech.conf" "$NGINX_CONF_DIR/vanantech.conf.bak"
sudo cp /tmp/vanantech-bootstrap.conf "$NGINX_CONF_DIR/vanantech.conf"

# Restart nginx with HTTP-only config
sudo docker compose -f "$COMPOSE_FILE" restart nginx
sleep 3

echo "[2/4] Nginx started in HTTP-only mode."

# --- Step 2: Request certificate ---
echo "[3/4] Requesting Let's Encrypt certificate..."

sudo docker run --rm \
  -v vanan_certbot_conf:/etc/letsencrypt \
  -v vanan_certbot_www:/var/www/certbot \
  certbot/certbot certonly \
  --webroot \
  --webroot-path=/var/www/certbot \
  --email "$EMAIL" \
  --agree-tos \
  --no-eff-email \
  -d "$DOMAIN" \
  -d "www.$DOMAIN" \
  -d "diemthuong.$DOMAIN" \
  -d "app.$DOMAIN" \
  -d "api.$DOMAIN"

echo "Certificate issued successfully!"

# --- Step 3: Restore full HTTPS config ---
echo "[4/4] Switching nginx to HTTPS mode..."
sudo cp "$NGINX_CONF_DIR/vanantech.conf.bak" "$NGINX_CONF_DIR/vanantech.conf"
sudo docker compose -f "$COMPOSE_FILE" restart nginx
sleep 3

echo ""
echo "=== Done! ==="
echo "SSL certificate issued for $DOMAIN and subdomains."
echo "Auto-renewal every 12h via certbot container."
echo ""
echo "Test URLs:"
echo "  https://$DOMAIN"
echo "  https://diemthuong.$DOMAIN"
echo "  https://app.$DOMAIN"
echo "  https://api.$DOMAIN/health"
