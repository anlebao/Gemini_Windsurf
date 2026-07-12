#!/bin/bash
# =============================================================
# scripts/init-ssl.sh
# Chạy 1 lần trên VPS để lấy SSL certificate từ Let's Encrypt
#
# Kiến trúc mới: docker-entrypoint.sh tự động detect cert và chọn
# template (HTTP-only nếu chưa có cert, HTTPS nếu đã có).
# Script này chỉ cần: restart nginx (HTTP-only) → certbot → restart nginx (HTTPS).
#
# Yêu cầu:
#   - DNS A records đã trỏ đúng về IP VPS (TTL propagated)
#   - Docker + docker compose đã cài
#   - Stack đã deploy ít nhất 1 lần (có certbot_www volume)
#   - VANAN_DOMAIN đã set trong /opt/vanan/.env
#
# Usage: sudo bash /opt/vanan/scripts/init-ssl.sh [domain] [email]
#   domain — apex domain (default: đọc từ .env VANAN_DOMAIN)
#   email  — email cho Let's Encrypt (default: admin@<domain>)
#
# Example: sudo bash /opt/vanan/scripts/init-ssl.sh
#          sudo bash /opt/vanan/scripts/init-ssl.sh khachvip.online admin@khachvip.online
# =============================================================
set -e

DEPLOY_DIR="/opt/vanan"
COMPOSE_FILE="$DEPLOY_DIR/docker-compose.prod.yml"

# Load domain from .env if not provided as argument
DOMAIN="${1:-$(grep -E '^VANAN_DOMAIN=' "$DEPLOY_DIR/.env" 2>/dev/null | cut -d= -f2)}"
if [ -z "$DOMAIN" ]; then
    echo "ERROR: domain not provided and VANAN_DOMAIN not found in $DEPLOY_DIR/.env"
    echo "Usage: sudo bash init-ssl.sh <domain> [email]"
    exit 1
fi
EMAIL="${2:-admin@${DOMAIN}}"

echo "=== VanAn SSL Bootstrap ==="
echo "Domain : $DOMAIN"
echo "Email  : $EMAIL"
echo ""

# --- Step 1: Restart nginx (entrypoint will use HTTP-only template since no cert) ---
echo "[1/3] Restarting nginx in HTTP-only mode (no cert detected yet)..."
sudo docker compose -f "$COMPOSE_FILE" restart nginx
sleep 3
echo "      Nginx is now serving HTTP-only — ACME challenge will work."

# --- Step 2: Request certificate ---
echo "[2/3] Requesting Let's Encrypt certificate..."

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

# --- Step 3: Restart nginx (entrypoint will now detect cert and use HTTPS template) ---
echo "[3/3] Restarting nginx to pick up HTTPS config..."
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
