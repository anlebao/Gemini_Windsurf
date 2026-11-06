#!/bin/sh
# Validate modified nginx template + nginx.conf inside the running nginx container.
set -e
docker cp /tmp/vanan.multivps.conf.template vanan-nginx-1:/tmp/tpl.template
docker cp /tmp/nginx.conf vanan-nginx-1:/tmp/nginx.conf
docker exec vanan-nginx-1 sh -c "envsubst '\${VANAN_DOMAIN} \${SHOPERP_REMOTE_HOST} \${KHACHLINK_REMOTE_HOST} \${METERING_REMOTE_HOST}' < /tmp/tpl.template > /tmp/vanan-test.conf"
echo "--- unresolved placeholders (should be none) ---"
docker exec vanan-nginx-1 grep -n 'SHOPERP_REMOTE_HOST}\|KHACHLINK_REMOTE_HOST}\|VANAN_DOMAIN}\|METERING_REMOTE_HOST}' /tmp/vanan-test.conf || echo "none"
docker exec vanan-nginx-1 sh -c \
  'sed "s|include /etc/nginx/conf.d/\*.conf;|include /tmp/vanan-test.conf;|" /tmp/nginx.conf > /tmp/test.conf && nginx -t -c /tmp/test.conf'
