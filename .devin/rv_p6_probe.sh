#!/bin/bash
echo "=== containers ==="
docker ps --format '{{.Names}} {{.Ports}}' | head -10
echo "=== gateway ports ==="
docker exec vanan-gateway-1 sh -c 'cat /etc/appsettings*.json 2>/dev/null | head -5' || true
docker port vanan-gateway-1 2>/dev/null || true
echo "=== probe various health paths/ports ==="
for p in 80 8080 5001 5000; do
  for path in health healthz api/health api/healthz ""; do
    code=$(curl -s -o /dev/null -w "%{http_code}" -m 3 "http://localhost:$p/$path" 2>/dev/null)
    [ "$code" != "000" ] && echo "port=$p path=/$path -> $code"
  done
done
echo "=== nginx conf location blocks for booking ==="
docker exec vanan-gateway-1 sh -c 'cat /etc/nginx/conf.d/*.conf 2>/dev/null | grep -n "booking\|health\|location" | head -30' || true
echo "=== gateway logs tail ==="
docker logs vanan-gateway-1 --tail 15 2>&1 | head -20
echo DONE
