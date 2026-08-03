#!/bin/bash
echo "=== DEBUG: Key extraction ==="
GW_APP_PROD_RAW=$(docker exec vanan-gateway cat /app/appsettings.Production.json 2>/dev/null)
echo "RAW (first 500 chars):"
echo "$GW_APP_PROD_RAW" | head -c 500
echo
echo "---"
INTERNAL_KEY=$(echo "$GW_APP_PROD_RAW" | python3 -c "import json,sys; c=json.load(sys.stdin); print(c.get('InternalLoyalty',{}).get('ApiKey',''))" 2>&1)
echo "KEY=[$INTERNAL_KEY]"
echo "KEYLEN=${#INTERNAL_KEY}"

echo
echo "=== Try appsettings.json fallback ==="
GW_APP_RAW=$(docker exec vanan-gateway cat /app/appsettings.json 2>/dev/null)
echo "RAW (first 500 chars):"
echo "$GW_APP_RAW" | head -c 500
echo
echo "---"
INTERNAL_KEY2=$(echo "$GW_APP_RAW" | python3 -c "import json,sys; c=json.load(sys.stdin); print(c.get('InternalLoyalty',{}).get('ApiKey',''))" 2>&1)
echo "KEY2=[$INTERNAL_KEY2]"

echo
echo "=== Test request with extracted key ==="
KEY_TRIM=$(echo "$INTERNAL_KEY" | tr -d '[:space:]')
GW_IP=$(docker inspect vanan-gateway --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
echo "GW=$GW_IP KEY_TRIM=[$KEY_TRIM]"
curl -s -w "\nHTTP=%{http_code}\n" -H "X-Internal-Api-Key: $KEY_TRIM" "http://$GW_IP/api/internal/loyalty/effective-config/21cbf14f-581a-48c8-8ad6-becc21064535"
