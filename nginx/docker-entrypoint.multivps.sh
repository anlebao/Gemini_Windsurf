#!/bin/sh
# nginx/docker-entrypoint.multivps.sh
# Multi-VPS variant (3-VPS split): generates nginx config from vanan.multivps.conf.template
# Substitutes VANAN_DOMAIN + SHOPERP_REMOTE_HOST + KHACHLINK_REMOTE_HOST (VPC internal IPs).
#
# Required env:
#   VANAN_DOMAIN (e.g. khachvip.online)
#   SHOPERP_REMOTE_HOST (e.g. 10.148.0.3 — VPC internal IP of ShopERP VPS)
#   KHACHLINK_REMOTE_HOST (e.g. 10.148.0.4 — VPC internal IP of KhachLink VPS)
set -e

CONF_DIR="/etc/nginx/conf.d"
TEMPLATE_DIR="/etc/nginx/templates"
# Cert is issued with www2.${VANAN_DOMAIN} as primary CN (apex points to Oracle VPS)
CERT_PATH="/etc/letsencrypt/live/www2.${VANAN_DOMAIN}/fullchain.pem"

if [ -z "$VANAN_DOMAIN" ]; then
    echo "[nginx-entrypoint-multivps] ERROR: VANAN_DOMAIN env var is not set."
    exit 1
fi

if [ -z "$SHOPERP_REMOTE_HOST" ]; then
    echo "[nginx-entrypoint-multivps] ERROR: SHOPERP_REMOTE_HOST env var is not set."
    echo "  Set it to the VPC internal IP of the ShopERP VPS (e.g. 10.148.0.3)."
    exit 1
fi

if [ -z "$KHACHLINK_REMOTE_HOST" ]; then
    echo "[nginx-entrypoint-multivps] ERROR: KHACHLINK_REMOTE_HOST env var is not set."
    echo "  Set it to the VPC internal IP of the KhachLink VPS (e.g. 10.148.0.4)."
    exit 1
fi

# Ensure envsubst is available
if ! command -v envsubst > /dev/null 2>&1; then
    echo "[nginx-entrypoint-multivps] Installing gettext for envsubst..."
    apk add --no-cache gettext > /dev/null 2>&1
fi

# Substitute all 3 vars — preserve nginx's own $host, $http_upgrade, etc.
ENVSUBST_VARS='${VANAN_DOMAIN} ${SHOPERP_REMOTE_HOST} ${KHACHLINK_REMOTE_HOST}'

if [ -f "$CERT_PATH" ]; then
    echo "[nginx-entrypoint-multivps] SSL cert found — generating HTTPS multi-VPS config"
    envsubst "$ENVSUBST_VARS" < "${TEMPLATE_DIR}/vanan.multivps.conf.template" > "${CONF_DIR}/vanan.conf"
else
    echo "[nginx-entrypoint-multivps] SSL cert NOT found — generating HTTP-only multi-VPS config"
    envsubst "$ENVSUBST_VARS" < "${TEMPLATE_DIR}/vanan.multivps.http.conf.template" > "${CONF_DIR}/vanan.conf"
fi

# Check if timlathay.com cert exists. If not, strip the HTTPS server block for timlathay.com
# (the HTTP block for ACME challenge must remain, but the HTTPS block references cert files that don't exist yet)
TIMLATHAY_CERT="/etc/letsencrypt/live/timlathay.com/fullchain.pem"
if ! [ -f "$TIMLATHAY_CERT" ]; then
    echo "[nginx-entrypoint-multivps] timlathay.com cert NOT found — replacing with HTTP-only block"
    # Delete everything from the timlathay.com comment to end of file, then append HTTP-only block
    sed -i '/# timlathay.com — KhachLink Directory/,/MAGIC_END_MARKER_NEVER_MATCH/d' "${CONF_DIR}/vanan.conf" 2>/dev/null || true
    # More reliable: truncate from the timlathay.com comment line to end
    LINE_NUM=$(grep -n '# timlathay.com — KhachLink Directory' "${CONF_DIR}/vanan.conf" 2>/dev/null | head -1 | cut -d: -f1 || echo "")
    if [ -n "$LINE_NUM" ]; then
        head -n $((LINE_NUM - 1)) "${CONF_DIR}/vanan.conf" > "${CONF_DIR}/vanan.conf.tmp" && mv "${CONF_DIR}/vanan.conf.tmp" "${CONF_DIR}/vanan.conf"
    fi
    # Append HTTP-only block for ACME challenge
    cat >> "${CONF_DIR}/vanan.conf" <<'TIMLATHAY_HTTP'

# timlathay.com — HTTP-only (ACME challenge) — HTTPS block added after cert is issued
server {
    listen 80;
    server_name timlathay.com;

    location /.well-known/acme-challenge/ {
        root /var/www/certbot;
    }

    location / {
        return 301 https://$host$request_uri;
    }
}
TIMLATHAY_HTTP
    echo "[nginx-entrypoint-multivps] timlathay.com HTTP-only block added (ACME challenge ready)"
fi

# Hand off to official nginx entrypoint
exec nginx -g "daemon off;"
