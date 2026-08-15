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

# ============================================================
# Generic external domain handler
# Detects all @@EXT_DOMAIN_START:<domain>@@ markers in config.
# For each domain, if SSL cert doesn't exist yet:
#   - Strip the HTTPS server block (listen 443) — prevents nginx fail on missing cert
#   - Keep the HTTP server block (listen 80) — serves ACME challenge + proxies content
# After certbot issues cert, restart nginx → entry point keeps HTTPS block.
# Adding a new external domain = just add block to template with markers (zero entry point changes).
# Uses sed (BusyBox compatible — no grep -P, no awk &&) for domain extraction + block stripping.
# ============================================================
EXT_DOMAINS=$(sed -n 's/.*@@EXT_DOMAIN_START:\([^@]*\)@@.*/\1/p' "${CONF_DIR}/vanan.conf" 2>/dev/null || echo "")

if [ -n "$EXT_DOMAINS" ]; then
    for DOMAIN in $EXT_DOMAINS; do
        CERT="/etc/letsencrypt/live/${DOMAIN}/fullchain.pem"
        if ! [ -f "$CERT" ]; then
            echo "[nginx-entrypoint-multivps] ${DOMAIN} cert NOT found — stripping HTTPS block"
            # Find the HTTPS server block (listen 443) within this domain's marker section
            MARKER_LINE=$(grep -n "@@EXT_DOMAIN_START:${DOMAIN}@@" "${CONF_DIR}/vanan.conf" | head -1 | cut -d: -f1)
            END_MARKER_LINE=$(grep -n "@@EXT_DOMAIN_END:${DOMAIN}@@" "${CONF_DIR}/vanan.conf" | head -1 | cut -d: -f1)
            # Within section, find 'listen 443' line
            FOUR43_LINE=$(sed -n "${MARKER_LINE},${END_MARKER_LINE}p" "${CONF_DIR}/vanan.conf" | grep -n 'listen 443' | head -1 | cut -d: -f1)
            if [ -z "$FOUR43_LINE" ]; then
                echo "[nginx-entrypoint-multivps] ${DOMAIN} — no 443 block found, skipping"
                continue
            fi
            FOUR43_LINE=$((MARKER_LINE + FOUR43_LINE - 1))
            # Find the 'server {' line before the 443 line (within section)
            BLOCK_START=$(head -n "$FOUR43_LINE" "${CONF_DIR}/vanan.conf" | tail -n +${MARKER_LINE} | grep -n '^server {' | tail -1 | cut -d: -f1)
            BLOCK_START=$((MARKER_LINE + BLOCK_START - 1))
            # Find closing '}' after 443 line
            BLOCK_END=$(tail -n +"$FOUR43_LINE" "${CONF_DIR}/vanan.conf" | grep -n '^}' | head -1 | cut -d: -f1)
            BLOCK_END=$((FOUR43_LINE + BLOCK_END - 1))
            # Delete the HTTPS block lines
            sed "${BLOCK_START},${BLOCK_END}d" "${CONF_DIR}/vanan.conf" > "${CONF_DIR}/vanan.conf.tmp" && mv "${CONF_DIR}/vanan.conf.tmp" "${CONF_DIR}/vanan.conf"
            echo "[nginx-entrypoint-multivps] ${DOMAIN} HTTPS block stripped (lines ${BLOCK_START}-${BLOCK_END}) — HTTP block retained (ACME + proxy)"
        else
            echo "[nginx-entrypoint-multivps] ${DOMAIN} cert found — keeping HTTPS block"
        fi
    done
else
    echo "[nginx-entrypoint-multivps] No external domains detected"
fi

# Hand off to official nginx entrypoint
exec nginx -g "daemon off;"
