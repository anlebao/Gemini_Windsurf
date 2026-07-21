#!/bin/bash
PSQL="docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub"
echo "=== Tenants.Settings_Latitude/Longitude (expect 2 rows) ==="
$PSQL -c "SELECT column_name FROM information_schema.columns WHERE table_name='Tenants' AND column_name IN ('Settings_Latitude','Settings_Longitude');"
echo "=== Shops table (should be null) ==="
$PSQL -c "SELECT to_regclass('public.Shops') AS shops_table;"
echo "=== SocialCampaigns.ShopId (should be empty) ==="
$PSQL -c "SELECT column_name FROM information_schema.columns WHERE table_name='SocialCampaigns' AND column_name='ShopId';"
echo "=== Tenants count ==="
$PSQL -c "SELECT COUNT(*) FROM Tenants;"
echo "=== Migration history (latest 3) ==="
$PSQL -c "SELECT migration_id, product_version FROM __EFMigrationsHistory ORDER BY id DESC LIMIT 3;"

GATEWAY_IP=$(docker inspect vanan-gateway --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
echo "=== Gateway IP: $GATEWAY_IP ==="
echo "-- nearby --"
curl -s -o /tmp/nearby.json -w "nearby: %{http_code}\n" "http://$GATEWAY_IP/api/tenants/nearby?lat=10.8&lng=106.7&radius=20"
head -c 400 /tmp/nearby.json; echo
echo "-- search --"
curl -s -o /tmp/search.json -w "search: %{http_code}\n" "http://$GATEWAY_IP/api/tenants/search?q=test"
head -c 400 /tmp/search.json; echo
echo "-- tenants list --"
curl -s -o /tmp/tenants.json -w "tenants: %{http_code}\n" "http://$GATEWAY_IP/api/tenants"
head -c 400 /tmp/tenants.json; echo
echo "-- health --"
curl -s -o /tmp/health.txt -w "health: %{http_code}\n" "http://$GATEWAY_IP/health"
head -c 200 /tmp/health.txt; echo
