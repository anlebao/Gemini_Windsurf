#!/bin/bash
# =============================================================
# scripts/init-ssl-multivps.sh
# Chạy 1 lần trên Gateway VPS để lấy SSL certificate từ Let's Encrypt
# cho subdomain GCP (api2, app2, diemthuong2, www2).
#
# Yêu cầu:
#   - DNS A records đã trỏ đúng về IP Gateway VPS (TTL propagated)
#   - Docker + docker compose đã cài
#   - Stack đã deploy ít nhất 1 lần (có certbot_www volume)
#   - VANAN_DOMAIN đã set trong /opt/vanan/.env.gateway
#
# Usage: sudo bash /opt/vanan/scripts/init-ssl-multivps.sh [domain] [email]
#   domain — apex domain (default: đọc từ .env.gateway VANAN_DOMAIN)
#   email  — email cho Let's Encrypt (default: admin@<domain>)
#
# Example: sudo bash /opt/vanan/scripts/init-ssl-multivps.sh
#          sudo bash /opt/vanan/scripts/init-ssl-multivps.sh khachvip.online admin@khachvip.online
# =============================================================
set -e

DEPLOY_DIR="/opt/vanan"
COMPOSE_FILE="$DEPLOY_DIR/docker-compose.gateway.yml"
ENV_FILE="$DEPLOY_DIR/.env.gateway"

# Load domain from .env.gateway if not provided as argument
DOMAIN="${1:-$(grep -E '^VANAN_DOMAIN=' "$ENV_FILE" 2>/dev/null | cut -d= -f2)}"
if [ -z "$DOMAIN" ]; then
    echo "ERROR: domain not provided and VANAN_DOMAIN not found in $ENV_FILE"
    echo "Usage: sudo bash init-ssl-multivps.sh <domain> [email]"
    exit 1
fi
EMAIL="${2:-admin@${DOMAIN}}"

echo "=== VanAn Multi-VPS SSL Bootstrap ==="
echo "Domain : $DOMAIN"
echo "Email  : $EMAIL"
echo "Subdomains: ${DOMAIN} www2.${DOMAIN} api2.${DOMAIN} app2.${DOMAIN} diemthuong2.${DOMAIN}"
echo ""

# --- Step 1: Restart nginx (entrypoint will use HTTP-only template since no cert) ---
echo "[1/3] Restarting nginx in HTTP-only mode (no cert detected yet)..."
sudo docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" restart nginx
sleep 5
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
  -d "www2.$DOMAIN" \
  -d "api2.$DOMAIN" \
  -d "app2.$DOMAIN" \
  -d "diemthuong2.$DOMAIN"

echo "Certificate issued successfully!"

# --- Step 3: Restart nginx (entrypoint will now detect cert and use HTTPS template) ---
echo "[3/3] Restarting nginx to pick up HTTPS config..."
sudo docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" restart nginx
sleep 5

echo ""
echo "=== Done! ==="
echo "SSL certificate issued for $DOMAIN and GCP subdomains."
echo "Auto-renewal every 12h via certbot container."
echo ""
echo "Test URLs:"
echo "  https://$DOMAIN"
echo "  https://www2.$DOMAIN"
echo "  https://api2.$DOMAIN/health"
echo "  https://app2.$DOMAIN"
echo "  https://diemthuong2.$DOMAIN"
