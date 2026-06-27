#!/bin/sh
# nginx/docker-entrypoint.sh
# Smart entrypoint: HTTP-only until SSL cert exists, then HTTPS
set -e

CERT_PATH="/etc/letsencrypt/live/vanantech.io.vn/fullchain.pem"
CONF_DIR="/etc/nginx/conf.d"

if [ -f "$CERT_PATH" ]; then
    echo "[nginx-entrypoint] SSL cert found — starting with HTTPS config"
    # vanantech.conf already has HTTPS blocks — nothing to swap
else
    echo "[nginx-entrypoint] SSL cert NOT found — starting HTTP-only mode"
    # Replace conf with HTTP-only version (no ssl_certificate lines)
    cat > "$CONF_DIR/vanantech.conf" << 'HTTPONLY'
# HTTP-only bootstrap config (cert not yet issued)
server {
    listen 80;
    server_name vanantech.io.vn www.vanantech.io.vn;

    location /.well-known/acme-challenge/ {
        root /var/www/certbot;
    }

    location / {
        proxy_pass         http://vanan-shoperp:80;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection "upgrade";
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_read_timeout 60s;
    }

    # Blazor SignalR
    location /_blazor {
        proxy_pass         http://vanan-shoperp:80;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection "upgrade";
        proxy_set_header   Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}

server {
    listen 80;
    server_name diemthuong.vanantech.io.vn;

    location /.well-known/acme-challenge/ {
        root /var/www/certbot;
    }

    location / {
        proxy_pass         http://vanan-khachlink:80;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection "upgrade";
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_read_timeout 60s;
    }

    # SignalR WebSocket
    location /dashboardHub {
        proxy_pass         http://vanan-khachlink:80;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection "upgrade";
        proxy_set_header   Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}

server {
    listen 80;
    server_name app.vanantech.io.vn;

    location /.well-known/acme-challenge/ {
        root /var/www/certbot;
    }

    location / {
        proxy_pass         http://vanan-shoperp:80;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection "upgrade";
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_read_timeout 60s;
    }

    # Blazor SignalR
    location /_blazor {
        proxy_pass         http://vanan-shoperp:80;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection "upgrade";
        proxy_set_header   Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}

server {
    listen 80;
    server_name api.vanantech.io.vn;

    location /.well-known/acme-challenge/ {
        root /var/www/certbot;
    }

    location / {
        proxy_pass         http://vanan-gateway:80;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection "upgrade";
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_read_timeout 120s;
    }

    # SignalR hubs
    location ~ ^/(orderHub|kitchenhub) {
        proxy_pass         http://vanan-gateway:80;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection "upgrade";
        proxy_set_header   Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}
HTTPONLY
fi

# Hand off to official nginx entrypoint
exec nginx -g "daemon off;"
