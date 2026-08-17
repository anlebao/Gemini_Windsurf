#!/bin/bash
# =============================================================
# scripts/init-ssl-tenant-domains.sh
# Domain Reseller R1: SSL certificate provisioning for tenant-owned domains.
#
# Tenant domains (e.g. shopa.com, cafeB.vn) are apex/second-level domains
# registered via Vạn An reseller (GoDaddy). Unlike *.khachvip.online subdomains
# (covered by wildcard cert), each tenant domain needs its own Let's Encrypt cert.
#
# This script:
#   1. Queries TenantDomains table (Gateway PG) for active domains NOT matching
#      *.khachvip.online (those are covered by wildcard cert).
#   2. For each tenant domain: checks if cert already exists, if not requests
#      a new cert via certbot webroot challenge.
#   3. Restarts nginx to pick up new certs.
#
# Prerequisites:
#   - DNS A record for each tenant domain → Gateway VPS IP (set via GoDaddy API)
#   - DNS propagated (verify: dig <domain>)
#   - nginx config deployed with tenant domain server block (or wildcard fallback)
#   - Docker + docker compose installed
#   - vanan_certbot_conf + vanan_certbot_www Docker volumes exist
#
# Usage: sudo bash /opt/vanan/scripts/init-ssl-tenant-domains.sh
#
# Cron: Run every 1 hour to pick up new tenant domains.
#   0 * * * * /opt/vanan/scripts/init-ssl-tenant-domains.sh >> /var/log/vanan-ssl-tenant.log 2>&1
# =============================================================
set -e

DEPLOY_DIR="/opt/vanan"
COMPOSE_FILE="$DEPLOY_DIR/docker-compose.gateway.yml"
ENV_FILE="$DEPLOY_DIR/.env.gateway"
PG_CONTAINER="vanan-postgres"
PG_USER="vanan_admin"
PG_DB="VanAnCoreHub"

# Base domains that are covered by wildcard cert (skip these)
WILDCARD_DOMAINS="khachvip.online timlathay.com"

echo "=== Domain Reseller R1 — Tenant Domain SSL Provisioning ==="
echo "Time: $(date -u '+%Y-%m-%d %H:%M:%S UTC')"
echo ""

# --- Step 1: Collect tenant domains from PG ---
echo "[1/3] Collecting tenant domains from TenantDomains table..."

# Query active tenant domains with linked KhachLinkInstances
# Pattern #9: quoted PascalCase for PostgreSQL
TENANT_DOMAINS=$(sudo docker exec "$PG_CONTAINER" psql -U "$PG_USER" -d "$PG_DB" -t -A -c \
    "SELECT \"Domain\" FROM \"TenantDomains\" WHERE \"Status\" = 0 AND \"KhachLinkInstanceId\" IS NOT NULL AND \"IsDeleted\" = false;" \
    2>/dev/null || echo "")

if [ -z "$TENANT_DOMAINS" ]; then
    echo "      No active tenant domains with linked KhachLinkInstances found."
    echo "=== Nothing to do. Exiting. ==="
    exit 0
fi

echo "      Found tenant domains:"
echo "$TENANT_DOMAINS" | sed 's/^/        - /'
echo ""

# Filter out wildcard-covered domains (subdomains of khachvip.online/timlathay.com)
FILTERED_DOMAINS=""
for d in $TENANT_DOMAINS; do
    skip=false
    for wd in $WILDCARD_DOMAINS; do
        # Skip if domain ends with .<wildcard_domain> (it's a subdomain covered by wildcard)
        if [[ "$d" == *.$wd ]]; then
            skip=true
            break
        fi
    done
    if [ "$skip" = false ]; then
        FILTERED_DOMAINS="$FILTERED_DOMAINS $d"
    else
        echo "      Skipping $d (covered by wildcard cert *.$(echo $d | rev | cut -d. -f1-2 | rev))"
    fi
done

if [ -z "$(echo $FILTERED_DOMAINS | tr -d ' ')" ]; then
    echo "      All tenant domains are covered by wildcard cert. Nothing to do."
    echo "=== Exiting. ==="
    exit 0
fi

echo "      Domains needing individual SSL cert:"
for d in $FILTERED_DOMAINS; do
    echo "        - $d"
done
echo ""

# --- Step 2: Request cert for each domain (skip if already exists) ---
echo "[2/3] Requesting Let's Encrypt certificates..."

# Restart nginx in HTTP-only mode for ACME challenge
sudo docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" restart nginx
sleep 5

EMAIL="admin@khachvip.online"
NEW_CERTS=0
SKIPPED=0
FAILED=0

for d in $FILTERED_DOMAINS; do
    # Check if cert already exists
    CERT_EXISTS=$(sudo docker exec vanan-nginx-1 ls /etc/letsencrypt/live/$d/fullchain.pem 2>/dev/null && echo "yes" || echo "no" 2>/dev/null)

    if [ "$CERT_EXISTS" = "yes" ]; then
        echo "      ✓ $d — cert already exists, skipping"
        SKIPPED=$((SKIPPED + 1))
        continue
    fi

    # Verify DNS resolves to this VPS before requesting cert
    DNS_IP=$(dig +short "$d" A 2>/dev/null | head -1 || echo "")
    if [ -z "$DNS_IP" ]; then
        echo "      ✗ $d — DNS not propagated (no A record), skipping"
        FAILED=$((FAILED + 1))
        continue
    fi

    echo "      → $d — DNS: $DNS_IP — requesting cert..."
    if sudo docker run --rm \
        -v vanan_certbot_conf:/etc/letsencrypt \
        -v vanan_certbot_www:/var/www/certbot \
        certbot/certbot certonly \
        --webroot \
        --webroot-path=/var/www/certbot \
        --email "$EMAIL" \
        --agree-tos \
        --no-eff-email \
        --non-interactive \
        -d "$d" \
        -d "www.$d" 2>&1; then
        echo "      ✓ $d — cert issued successfully"
        NEW_CERTS=$((NEW_CERTS + 1))
    else
        echo "      ✗ $d — cert request failed"
        FAILED=$((FAILED + 1))
    fi
done

echo ""
echo "      Summary: $NEW_CERTS new, $SKIPPED existing, $FAILED failed"
echo ""

# --- Step 3: Restart nginx to pick up new certs ---
if [ "$NEW_CERTS" -gt 0 ]; then
    echo "[3/3] Restarting nginx to serve HTTPS with new certs..."
    sudo docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" restart nginx
    sleep 5
    echo "      Nginx restarted."
else
    echo "[3/3] No new certs — skipping nginx restart."
fi

echo ""
echo "=== Done! ==="
echo "Tenant domain SSL provisioning complete."
echo "Auto-renewal: certbot container renews all certs every 12h."
echo ""
echo "Test URLs:"
for d in $FILTERED_DOMAINS; do
    echo "  https://$d"
done
echo ""
echo "NOTE: Each tenant domain needs an explicit nginx server block OR the wildcard"
echo "      fallback server block routes it to KhachLink. The wildcard block in"
echo "      vanan.multivps.conf.template handles *.khachvip.online — tenant domains"
echo "      (e.g. shopa.com) need their own server block added to nginx config."
