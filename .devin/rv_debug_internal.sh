#!/bin/bash
KEY=$(docker exec vanan-gateway python3 -c "import json; c=json.load(open('/app/appsettings.Production.json')); print(c.get('InternalLoyalty',{}).get('ApiKey',''))" 2>/dev/null)
echo "KEY=[$KEY]"
echo "KEYLEN=${#KEY}"
# Trim whitespace
KEY_TRIM=$(echo "$KEY" | tr -d '[:space:]')
echo "KEY_TRIM=[$KEY_TRIM]"
echo "KEY_TRIM_LEN=${#KEY_TRIM}"

GW_IP=$(docker inspect vanan-gateway --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
echo "GW=$GW_IP"

echo "--- RESPONSE (with trimmed key) ---"
curl -sv -H "X-Internal-Api-Key: $KEY_TRIM" "http://$GW_IP/api/internal/loyalty/effective-config/21cbf14f-581a-48c8-8ad6-becc21064535" 2>&1 | tail -30

echo
echo "--- RESPONSE (with raw key) ---"
curl -s -w "\nHTTP_CODE=%{http_code}\n" -H "X-Internal-Api-Key: $KEY" "http://$GW_IP/api/internal/loyalty/effective-config/21cbf14f-581a-48c8-8ad6-becc21064535"

echo
echo "--- RESPONSE (no key) ---"
curl -s -w "\nHTTP_CODE=%{http_code}\n" "http://$GW_IP/api/internal/loyalty/effective-config/21cbf14f-581a-48c8-8ad6-becc21064535"

echo
echo "--- appsettings.Production.json InternalLoyalty section ---"
docker exec vanan-gateway python3 -c "import json; c=json.load(open('/app/appsettings.Production.json')); print(json.dumps(c.get('InternalLoyalty',{}), indent=2))" 2>/dev/null
