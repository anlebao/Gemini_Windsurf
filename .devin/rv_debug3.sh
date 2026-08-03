#!/bin/bash
echo "=== Gateway env vars (InternalLoyalty) ==="
docker exec vanan-gateway env | grep -i InternalLoyalty
echo
echo "=== ShopERP env vars (InternalLoyalty + Gateway) ==="
docker exec vanan-shoperp env | grep -iE "InternalLoyalty|Gateway"
echo
GW_IP=$(docker inspect vanan-gateway --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
echo "=== Test with PROD key: vanan-internal-loyalty-prod-2026 ==="
curl -s -w "\nHTTP=%{http_code}\n" -H "X-Internal-Api-Key: vanan-internal-loyalty-prod-2026" "http://$GW_IP/api/internal/loyalty/effective-config/21cbf14f-581a-48c8-8ad6-becc21064535"
echo
echo "=== Test with DEV key: vanan-internal-loyalty-dev-key-2026 ==="
curl -s -w "\nHTTP=%{http_code}\n" -H "X-Internal-Api-Key: vanan-internal-loyalty-dev-key-2026" "http://$GW_IP/api/internal/loyalty/effective-config/21cbf14f-581a-48c8-8ad6-becc21064535"
