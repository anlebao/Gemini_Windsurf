#!/bin/bash
# Add Directory domain to nginx map + reload
# Usage: ./add-directory-domain.sh <domain>
# Checks Gateway API — domain must have KhachLinkProfile.Directory
#
# Prerequisites:
# - nginx config at /etc/nginx/conf.d/default.conf (inside vanan-khachlink container)
# - jq installed (apt-get install -y jq)
# - Gateway accessible at $GATEWAY_URL

set -e

DOMAIN="$1"
NGINX_CONF="${NGINX_CONF:-/etc/nginx/conf.d/default.conf}"
GATEWAY_URL="${GATEWAY_URL:-http://vanan-gateway-1:80}"

if [ -z "$DOMAIN" ]; then
    echo "Usage: $0 <domain>"
    exit 1
fi

# Verify domain is Directory profile via Gateway API
PROFILE=$(curl -s "$GATEWAY_URL/api/v1/khachlink-instances/by-domain/$DOMAIN" | jq -r '.profile // empty')

if [ "$PROFILE" != "Directory" ]; then
    echo "Error: $DOMAIN is not Directory profile (got: '$PROFILE')"
    echo "Ensure the KhachLinkInstance exists with Profile=Directory in Gateway admin UI."
    exit 1
fi

# Add to nginx map (uncomment if commented, or add new line after "default 0;")
if grep -q "# $DOMAIN 1;" "$NGINX_CONF"; then
    sed -i "s|# $DOMAIN 1;|$DOMAIN 1;|" "$NGINX_CONF"
elif ! grep -q "^[[:space:]]*$DOMAIN 1;" "$NGINX_CONF"; then
    sed -i "/default 0;/a\\    $DOMAIN 1;" "$NGINX_CONF"
fi

# Reload nginx
nginx -t && nginx -s reload
echo "Added $DOMAIN to Directory routing. nginx reloaded."
