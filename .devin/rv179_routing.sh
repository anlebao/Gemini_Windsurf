#!/bin/bash
echo "=== vanan-gateway containers ==="
docker ps --format '{{.Names}} {{.Image}}'
echo "=== nginx configs mentioning app2/shoperp ==="
docker exec vanan-nginx-1 sh -c 'grep -rl "app2" /etc/nginx/conf.d/ 2>/dev/null; grep -rl "shoperp\|5003" /etc/nginx/conf.d/ 2>/dev/null | head -5'
echo "=== nginx app2 server block ==="
docker exec vanan-nginx-1 sh -c 'grep -rl "app2" /etc/nginx/conf.d/ 2>/dev/null | head -3 | xargs -I{} sh -c "echo FILE: {}; grep -A8 -B2 -i app2 {} | head -40"'
echo "=== nginx upstreams ==="
docker exec vanan-nginx-1 sh -c 'grep -rh "upstream\|proxy_pass\|server " /etc/nginx/conf.d/ 2>/dev/null | head -30'
