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
    envsubst "$ENVSUBST_VARS" < "${TEMPLATE_DIR}/vanan.multivps.conf.template" > "${CONF_DIR}/vanan.conf"
fi

# Hand off to official nginx entrypoint
exec nginx -g "daemon off;"
