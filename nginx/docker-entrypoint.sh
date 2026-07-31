#!/bin/sh
# nginx/docker-entrypoint.sh
# Smart entrypoint: generates nginx config from template via envsubst,
# then picks HTTPS or HTTP-only depending on whether SSL cert exists.
#
# Required env: VANAN_DOMAIN (e.g. khachvip.online)
set -e

CONF_DIR="/etc/nginx/conf.d"
TEMPLATE_DIR="/etc/nginx/templates"
CERT_PATH="/etc/letsencrypt/live/${VANAN_DOMAIN}/fullchain.pem"

if [ -z "$VANAN_DOMAIN" ]; then
    echo "[nginx-entrypoint] ERROR: VANAN_DOMAIN env var is not set. Cannot generate config."
    exit 1
fi

# Ensure envsubst is available (nginx:alpine may not include it)
if ! command -v envsubst > /dev/null 2>&1; then
    echo "[nginx-entrypoint] Installing gettext for envsubst..."
    apk add --no-cache gettext > /dev/null 2>&1
fi

# Only substitute VANAN_DOMAIN â€” preserve nginx's own $host, $http_upgrade, etc.
ENVSUBST_VARS='${VANAN_DOMAIN}'

if [ -f "$CERT_PATH" ]; then
    echo "[nginx-entrypoint] SSL cert found for ${VANAN_DOMAIN} â€” generating HTTPS config"
    envsubst "$ENVSUBST_VARS" < "${TEMPLATE_DIR}/vanan.conf.template" > "${CONF_DIR}/vanan.conf"
else
    echo "[nginx-entrypoint] SSL cert NOT found for ${VANAN_DOMAIN} â€” generating HTTP-only config"
    envsubst "$ENVSUBST_VARS" < "${TEMPLATE_DIR}/vanan-http.conf.template" > "${CONF_DIR}/vanan.conf"
fi

# Hand off to official nginx entrypoint
exec nginx -g "daemon off;"
