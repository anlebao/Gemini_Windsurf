#!/bin/bash
GATEWAY_IP=$(docker inspect vanan-gateway --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
echo "=== Gateway IP: $GATEWAY_IP ==="
echo "-- store-info (valid tenant) --"
curl -s -w "\nHTTP: %{http_code}\n" "http://$GATEWAY_IP/api/tenants/21cbf14f-581a-48c8-8ad6-becc21064535/store-info"
echo "-- store-info (invalid tenant, expect 404) --"
curl -s -o /dev/null -w "HTTP: %{http_code}\n" "http://$GATEWAY_IP/api/tenants/00000000-0000-0000-0000-000000000099/store-info"
echo "-- nearby --"
curl -s -w "\nHTTP: %{http_code}\n" "http://$GATEWAY_IP/api/tenants/nearby?lat=10.8&lng=106.7&radius=20"
echo "-- search --"
curl -s -w "\nHTTP: %{http_code}\n" "http://$GATEWAY_IP/api/tenants/search?name=Trung"
echo "-- health --"
curl -s -w "\nHTTP: %{http_code}\n" "http://$GATEWAY_IP/health"
