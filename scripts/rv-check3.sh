#!/bin/bash
PSQL='docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub'
echo "=== Latest 5 migrations ==="
$PSQL -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY id DESC LIMIT 5;'
echo "=== Test store-info endpoint ==="
GATEWAY_IP=$(docker inspect vanan-gateway --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
curl -s -w "\nstore-info: %{http_code}\n" "http://$GATEWAY_IP/api/tenants/21cbf14f-581a-48c8-8ad6-becc21064535/store-info" | head -c 500
echo
echo "=== Test invalid tenant store-info (expect 404) ==="
curl -s -o /dev/null -w "invalid-tenant: %{http_code}\n" "http://$GATEWAY_IP/api/tenants/00000000-0000-0000-0000-000000000099/store-info"
