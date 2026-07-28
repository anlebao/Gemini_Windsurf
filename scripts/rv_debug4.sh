#!/bin/bash
GW_IP=$(docker inspect vanan-gateway --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
echo "Gateway IP: $GW_IP"
echo "=== Test Gateway endpoints directly ==="
echo "store-info: $(curl -s -o /dev/null -w '%{http_code}' http://$GW_IP:8080/api/tenants/21cbf14f-581a-48c8-8ad6-becc21064535/store-info)"
echo "catalog-rec: $(curl -s -o /dev/null -w '%{http_code}' 'http://$GW_IP:8080/api/catalog/recommended?tenantId=21cbf14f-581a-48c8-8ad6-becc21064535')"
echo "campaigns-by-tenant: $(curl -s -o /dev/null -w '%{http_code}' http://$GW_IP:8080/api/campaigns/by-tenant/21cbf14f-581a-48c8-8ad6-becc21064535)"
echo "=== Test via nginx (ShopERP proxy) ==="
echo "store-info-via-nginx: $(curl -sk -o /dev/null -w '%{http_code}' https://khachvip.online/api/tenants/21cbf14f-581a-48c8-8ad6-becc21064535/store-info)"
echo "campaigns-via-nginx: $(curl -sk -o /dev/null -w '%{http_code}' https://khachvip.online/api/campaigns/by-tenant/21cbf14f-581a-48c8-8ad6-becc21064535)"
echo "catalog-rec-via-nginx: $(curl -sk -o /dev/null -w '%{http_code}' 'https://khachvip.online/api/catalog/recommended?tenantId=21cbf14f-581a-48c8-8ad6-becc21064535')"
echo "=== Gateway port check ==="
docker exec vanan-gateway sh -c 'ss -tlnp 2>/dev/null || netstat -tlnp 2>/dev/null' | head -5
echo "=== Gateway env ==="
docker exec vanan-gateway printenv | grep -iE 'ASPNETCORE|Urls|PORT' 2>/dev/null
