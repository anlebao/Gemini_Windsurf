#!/bin/bash
# Test from inside ShopERP container — can it reach Gateway?
echo "=== ShopERP container → Gateway connectivity ==="
echo "--- env Gateway:BaseUrl ---"
docker exec vanan-shoperp printenv Gateway__BaseUrl 2>&1
echo ""
echo "--- curl from ShopERP to Gateway (internal) ---"
JWT=$(cat /tmp/jwt.txt)
docker exec vanan-shoperp curl -s -w "\nHTTP_CODE: %{http_code}\n" -H "Authorization: Bearer $JWT" "http://gateway:80/api/v1/tenants" 2>&1 | head -5
echo ""
echo "--- curl from ShopERP to Gateway (vanan-gateway hostname) ---"
docker exec vanan-shoperp curl -s -w "\nHTTP_CODE: %{http_code}\n" -H "Authorization: Bearer $JWT" "http://vanan-gateway:80/api/v1/tenants" 2>&1 | head -5
echo ""
echo "--- curl from ShopERP to Gateway (localhost:5001) ---"
docker exec vanan-shoperp curl -s -w "\nHTTP_CODE: %{http_code}\n" -H "Authorization: Bearer $JWT" "http://localhost:5001/api/v1/tenants" 2>&1 | head -5
