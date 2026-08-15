#!/bin/bash
# =============================================================
# scripts/init-ssl-timlathay.sh
# Get SSL certificate for timlathay.com (external domain, separate from *.khachvip.online)
#
# Prerequisites:
#   - DNS A record: timlathay.com → 136.85.94.119 (Gateway VPS IP)
#   - DNS A record: www.timlathay.com → 136.85.94.119
#   - DNS propagated (verify: dig timlathay.com)
#   - nginx config deployed with timlathay.com server block (HTTP block serves ACME challenge)
#   - Docker + docker compose installed on Gateway VPS
#
# Usage: sudo bash /opt/vanan/scripts/init-ssl-timlathay.sh [email]
#   email — email for Let's Encrypt (default: admin@timlathay.com)
# =============================================================
set -e

DEPLOY_DIR="/opt/vanan"
COMPOSE_FILE="$DEPLOY_DIR/docker-compose.gateway.yml"
ENV_FILE="$DEPLOY_DIR/.env.gateway"
DOMAIN="timlathay.com"
EMAIL="${1:-admin@${DOMAIN}}"

echo "=== SSL Bootstrap for $DOMAIN ==="
echo "Domain : $DOMAIN"
echo "Email  : $EMAIL"
echo ""

# Verify DNS resolves to this VPS
echo "[0/3] Verifying DNS..."
DNS_IP=$(dig +short "$DOMAIN" A 2>/dev/null | head -1 || echo "")
if [ -z "$DNS_IP" ]; then
    echo "ERROR: $DOMAIN does not resolve. Set DNS A record → VPS IP first."
    echo "       dig $DOMAIN A should return the VPS IP."
    exit 1
fi
echo "      $DOMAIN → $DNS_IP"
if [ "$DNS_IP" != "$(curl -s ifconfig.me 2>/dev/null || echo 'unknown')" ]; then
    echo "      WARNING: DNS IP ($DNS_IP) may not match this VPS IP. Continue anyway..."
fi
echo ""

# --- Step 1: Restart nginx in HTTP-only mode (ACME challenge must work) ---
# The nginx entrypoint detects if cert exists. If not, it uses HTTP-only template.
# We need to temporarily remove the cert reference or use the HTTP template.
echo "[1/3] Ensuring nginx serves HTTP (for ACME challenge)..."
# Check if cert already exists
CERT_EXISTS=$(sudo docker exec vanan-nginx-1 ls /etc/letsencrypt/live/$DOMAIN/fullchain.pem 2>/dev/null && echo "yes" || echo "no" 2>/dev/null)
NGINX_CTR=$(docker ps --format '{{.Names}}' | grep -i nginx | head -1)
if [ "$CERT_EXISTS" = "no" ]; then
    echo "      Cert not found — nginx HTTPS block will fail. Using HTTP-only template..."
    # The docker-entrypoint.multivps.sh checks for cert existence and uses HTTP template if missing.
    # Restart nginx so entrypoint re-evaluates.
    docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" restart nginx 2>/dev/null || \
        sudo docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" restart nginx
    sleep 5
fi
echo "      nginx is serving HTTP — ACME challenge will work."
echo ""

# --- Step 2: Request certificate ---
echo "[2/3] Requesting Let's Encrypt certificate for $DOMAIN + www.$DOMAIN..."
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
  -d "www.$DOMAIN"

echo "      Certificate issued successfully!"
echo ""

# --- Step 3: Restart nginx to pick up HTTPS config ---
echo "[3/3] Restarting nginx to serve HTTPS for $DOMAIN..."
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" restart nginx 2>/dev/null || \
    sudo docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" restart nginx
sleep 5

echo ""
echo "=== Done! ==="
echo "SSL certificate issued for $DOMAIN and www.$DOMAIN"
echo "Auto-renewal every 12h via certbot container."
echo ""
echo "Test:"
echo "  https://$DOMAIN"
echo ""
echo "NOTE: KhachLink runtime resolves instance config by Host header."
echo "      Create KhachLinkInstance with CustomDomain=$DOMAIN via /admin/khachlink-instances"
echo "      (or run the r1-enable workflow which seeds it)."
