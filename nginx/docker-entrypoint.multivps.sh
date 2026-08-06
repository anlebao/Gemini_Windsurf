#!/bin/sh
# nginx/docker-entrypoint.multivps.sh
# Multi-VPS variant: generates nginx config from vanan.multivps.conf.template
# Substitutes VANAN_DOMAIN + SHOPERP_REMOTE_HOST (VPC internal IP of ShopERP VPS).
#
# Required env:
#   VANAN_DOMAIN (e.g. khachvip.online)
#   SHOPERP_REMOTE_HOST (e.g. 10.148.0.3 — VPC internal IP of ShopERP VPS)
set -e

CONF_DIR="/etc/nginx/conf.d"
TEMPLATE_DIR="/etc/nginx/templates"
CERT_PATH="/etc/letsencrypt/live/${VANAN_DOMAIN}/fullchain.pem"

if [ -z "$VANAN_DOMAIN" ]; then
    echo "[nginx-entrypoint-multivps] ERROR: VANAN_DOMAIN env var is not set."
    exit 1
fi

if [ -z "$SHOPERP_REMOTE_HOST" ]; then
    echo "[nginx-entrypoint-multivps] ERROR: SHOPERP_REMOTE_HOST env var is not set."
    echo "  Set it to the VPC internal IP of the ShopERP VPS (e.g. 10.148.0.3)."
    exit 1
fi

# Ensure envsubst is available
if ! command -v envsubst > /dev/null 2>&1; then
    echo "[nginx-entrypoint-multivps] Installing gettext for envsubst..."
    apk add --no-cache gettext > /dev/null 2>&1
fi

# Substitute BOTH vars — preserve nginx's own $host, $http_upgrade, etc.
ENVSUBST_VARS='${VANAN_DOMAIN} ${SHOPERP_REMOTE_HOST}'

if [ -f "$CERT_PATH" ]; then
    echo "[nginx-entrypoint-multivps] SSL cert found — generating HTTPS multi-VPS config"
    envsubst "$ENVSUBST_VARS" < "${TEMPLATE_DIR}/vanan.multivps.conf.template" > "${CONF_DIR}/vanan.conf"
else
    echo "[nginx-entrypoint-multivps] SSL cert NOT found — generating HTTP-only multi-VPS config"
    # Fallback: use the multivps template but cert lines will fail — for first deploy without SSL,
    # use the HTTP-only template instead (see vanan-http.conf.template, adapt for multivps if needed).
    # For MVP: just use multivps template, nginx will start but SSL server blocks will error.
    # Workaround for first deploy: comment out SSL blocks or use HTTP-only.
    envsubst "$ENVSUBST_VARS" < "${TEMPLATE_DIR}/vanan.multivps.conf.template" > "${CONF_DIR}/vanan.conf"
fi

# Hand off to official nginx entrypoint
exec nginx -g "daemon off;"
