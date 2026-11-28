#!/bin/bash
set +e
for h in api.khachvip.online api2.khachvip.online app2.khachvip.online app.khachvip.online; do
  echo -n "$h: "
  curl -s -o /dev/null -w '%{http_code}' -k --max-time 8 "https://$h/api/public/orders/checkout" -X POST -H 'Content-Type: application/json' -d '{}'
  echo ""
done
echo "--- nginx server_names ---"
docker exec vanan-nginx-1 sh -c 'grep -rhoE "server_name[^;]+" /etc/nginx/conf.d/ 2>/dev/null | sort -u | head -20'
