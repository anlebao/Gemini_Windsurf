#!/bin/bash
# Comprehensive RV — test all tenant-based endpoints to find remaining bugs
GATEWAY_IP=$(docker inspect vanan-gateway --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
echo "=== Gateway IP: $GATEWAY_IP ==="

# Valid tenant ID from DB
VALID_TENANT="21cbf14f-581a-48c8-8ad6-becc21064535"

test_endpoint() {
  local name="$1"
  local method="$2"
  local url="$3"
  local body="$4"
  local code
  if [ -n "$body" ]; then
    code=$(curl -s -o /tmp/resp.json -w "%{http_code}" -X "$method" -H "Content-Type: application/json" -d "$body" "http://$GATEWAY_IP$url")
  else
    code=$(curl -s -o /tmp/resp.json -w "%{http_code}" -X "$method" "http://$GATEWAY_IP$url")
  fi
  echo "[$code] $name $method $url"
  if [ "$code" = "500" ]; then
    echo "  RESPONSE: $(head -c 300 /tmp/resp.json)"
  fi
}

echo "=== TenantStoreController ==="
test_endpoint "store-info" GET "/api/tenants/$VALID_TENANT/store-info"
test_endpoint "store-info-invalid" GET "/api/tenants/00000000-0000-0000-0000-000000000099/store-info"
test_endpoint "nearby" GET "/api/tenants/nearby?lat=10.8&lng=106.7&radius=20"
test_endpoint "search" GET "/api/tenants/search?name=Trung"

echo "=== CatalogController ==="
test_endpoint "recommended" GET "/api/catalog/recommended?tenantId=$VALID_TENANT"
test_endpoint "recommended-no-tenant" GET "/api/catalog/recommended"

echo "=== PublicOrdersController (tracking) ==="
test_endpoint "track-invalid" GET "/api/public-orders/track/INVALIDCODE123"

echo "=== CampaignsController ==="
test_endpoint "campaigns-public" GET "/api/campaigns/public?tenantId=$VALID_TENANT"

echo "=== ShopConfigController ==="
test_endpoint "shop-config" GET "/api/shop-config/$VALID_TENANT"

echo "=== Health/Root ==="
test_endpoint "health" GET "/health"

echo "=== Recent gateway errors ==="
docker logs vanan-gateway --since 2m 2>&1 | grep -E "ERR|Exception" | grep -v "OutboxMessages" | head -20
