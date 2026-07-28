#!/bin/bash
GW_IP=$(docker inspect vanan-gateway --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
echo "Gateway IP: $GW_IP"
echo "=== Try port 80 ==="
echo "health: $(curl -s -o /dev/null -w '%{http_code}' http://$GW_IP/health)"
echo "store-info: $(curl -s -o /dev/null -w '%{http_code}' http://$GW_IP/api/tenants/21cbf14f-581a-48c8-8ad6-becc21064535/store-info)"
echo "catalog-rec: $(curl -s -o /dev/null -w '%{http_code}' 'http://$GW_IP/api/catalog/recommended?tenantId=21cbf14f-581a-48c8-8ad6-becc21064535')"
echo "campaigns: $(curl -s -o /dev/null -w '%{http_code}' http://$GW_IP/api/campaigns/by-tenant/21cbf14f-581a-48c8-8ad6-becc21064535)"
echo "=== Check what ports are actually listening in gateway ==="
docker exec vanan-gateway sh -c 'cat /proc/1/cmdline | tr "\0" " "' 2>/dev/null
echo
echo "=== Check ShopERP YARP config ==="
docker exec vanan-shoperp cat /app/appsettings.json 2>/dev/null | grep -A 20 "ReverseProxy\|YARP\|Forward" | head -30
