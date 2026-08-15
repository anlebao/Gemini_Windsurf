#!/bin/bash
# =============================================================
# scripts/init-ssl-khachlink-instances.sh
# KhachLink Multi-Profile R1 Sprint 5 — SSL SAN expand script
#
# Reads all CustomDomain values from KhachLinkInstances table (Gateway PG)
# and expands the existing Let's Encrypt certificate to include them.
# Uses certbot --expand (webroot challenge, no DNS challenge).
#
# Prerequisites:
#   - init-ssl-multivps.sh already run (base cert exists for www2/api2/app2/diemthuong2)
#   - DNS A records for each new KhachLinkInstance CustomDomain → Gateway VPS IP
#   - Docker + docker compose installed
#   - VANAN_DOMAIN set in /opt/vanan/.env.gateway
#
# Usage: sudo bash /opt/vanan/scripts/init-ssl-khachlink-instances.sh [domain] [email]
#   domain — apex domain (default: read from .env.gateway VANAN_DOMAIN)
#   email  — email for Let's Encrypt (default: admin@<domain>)
#
# Example: sudo bash /opt/vanan/scripts/init-ssl-khachlink-instances.sh
# =============================================================
set -e

DEPLOY_DIR="/opt/vanan"
COMPOSE_FILE="$DEPLOY_DIR/docker-compose.gateway.yml"
ENV_FILE="$DEPLOY_DIR/.env.gateway"
PG_CONTAINER="vanan-postgres"
PG_USER="vanan_admin"
PG_DB="VanAnCoreHub"

# Load domain from .env.gateway if not provided as argument
DOMAIN="${1:-$(grep -E '^VANAN_DOMAIN=' "$ENV_FILE" 2>/dev/null | cut -d= -f2)}"
if [ -z "$DOMAIN" ]; then
    echo "ERROR: domain not provided and VANAN_DOMAIN not found in $ENV_FILE"
    echo "Usage: sudo bash init-ssl-khachlink-instances.sh <domain> [email]"
    exit 1
fi
EMAIL="${2:-admin@${DOMAIN}}"

echo "=== KhachLink Multi-Profile — SSL SAN Expand ==="
echo "Domain : $DOMAIN"
echo ""

# --- Step 1: Collect all subdomains (base + KhachLinkInstance CustomDomains) ---
echo "[1/4] Collecting subdomains from KhachLinkInstances table..."

# Base subdomains (always included — certbot --expand never removes existing SANs)
BASE_SUBDOMAINS="www2.$DOMAIN api2.$DOMAIN app2.$DOMAIN diemthuong2.$DOMAIN"

# Query KhachLinkInstance CustomDomains from Gateway PG (Pattern #9: quoted PascalCase)
# Only active instances with non-empty CustomDomain
CUSTOM_DOMAINS=$(sudo docker exec "$PG_CONTAINER" psql -U "$PG_USER" -d "$PG_DB" -t -A -c \
    "SELECT \"CustomDomain\" FROM \"KhachLinkInstances\" WHERE \"IsActive\" = true AND \"CustomDomain\" != '' AND \"CustomDomain\" IS NOT NULL;" \
    2>/dev/null || echo "")

if [ -z "$CUSTOM_DOMAINS" ]; then
    echo "      No KhachLinkInstance CustomDomains found. Expanding with base subdomains only."
    ALL_DOMAINS=$BASE_SUBDOMAINS
else
    echo "      Found KhachLinkInstance CustomDomains:"
    echo "$CUSTOM_DOMAINS" | sed 's/^/        - /'
    ALL_DOMAINS="$BASE_SUBDOMAINS $CUSTOM_DOMAINS"
fi

echo ""
echo "      All SANs in expanded cert:"
for d in $ALL_DOMAINS; do
    echo "        - $d"
done
echo ""

# --- Step 2: Restart nginx in HTTP-only mode (ACME challenge must work) ---
echo "[2/4] Restarting nginx in HTTP-only mode..."
sudo docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" restart nginx
sleep 5
echo "      Nginx is now serving HTTP — ACME challenge will work."
echo ""

# --- Step 3: Expand certificate (certbot --expand adds new SANs, keeps existing) ---
echo "[3/4] Expanding Let's Encrypt certificate with new SANs..."

CERTBOT_ARGS=""
for d in $ALL_DOMAINS; do
    CERTBOT_ARGS="$CERTBOT_ARGS -d $d"
done

sudo docker run --rm \
  -v vanan_certbot_conf:/etc/letsencrypt \
  -v vanan_certbot_www:/var/www/certbot \
  certbot/certbot certonly \
  --webroot \
  --webroot-path=/var/www/certbot \
  --email "$EMAIL" \
  --agree-tos \
  --no-eff-email \
  --expand \
  --non-interactive \
  $CERTBOT_ARGS

echo "      Certificate expanded successfully!"
echo ""

# --- Step 4: Restart nginx to pick up expanded cert ---
echo "[4/4] Restarting nginx to serve HTTPS with expanded cert..."
sudo docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" restart nginx
sleep 5

echo ""
echo "=== Done! ==="
echo "SSL certificate expanded to cover all KhachLinkInstance CustomDomains."
echo "Auto-renewal every 12h via certbot container (renews all SANs)."
echo ""
echo "Test URLs:"
for d in $CUSTOM_DOMAINS; do
    echo "  https://$d"
done
echo ""
echo "NOTE: nginx wildcard server block routes any *.${DOMAIN} (except api2/app2/www2/diemthuong2)"
echo "      to KhachLink container. New KhachLinkInstance CustomDomains work immediately after"
echo "      DNS propagates + this script runs — no KhachLink container restart needed."
echo "      KhachLink runtime fetches instance config via Gateway API on page load."
