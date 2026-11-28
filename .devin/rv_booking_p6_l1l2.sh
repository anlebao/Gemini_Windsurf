#!/bin/bash
# RV L1/L2 — Booking P6 deploy (2026-10-06, sha 40a6f01c)
set -e

echo "=== L1: MARKERS ==="
echo "--- Gateway.dll ---"
docker exec vanan-gateway-1 sh -c 'ls /app/*.dll 2>/dev/null | head -3' || true
docker exec vanan-gateway-1 sh -c 'grep -c "PublicBookingController" /app/VanAn.Gateway.dll 2>/dev/null' || echo "0"
docker exec vanan-gateway-1 sh -c 'grep -c "TenantBookingController" /app/VanAn.Gateway.dll 2>/dev/null' || echo "0"

echo "--- CoreHub.dll (booking services) ---"
docker exec vanan-gateway-1 sh -c 'grep -c "BookingService" /app/VanAn.CoreHub.dll 2>/dev/null' || echo "0"
docker exec vanan-gateway-1 sh -c 'grep -c "CommissionService" /app/VanAn.CoreHub.dll 2>/dev/null' || echo "0"
docker exec vanan-gateway-1 sh -c 'grep -c "QRAttributionService" /app/VanAn.CoreHub.dll 2>/dev/null' || echo "0"

echo "--- ShopERP.dll (BookingTenantApiClient) ---"
docker exec vanan-shop-a-1 sh -c 'grep -c "BookingTenantApiClient" /app/VanAn.ShopERP.dll 2>/dev/null' || echo "0"
docker exec vanan-shop-a-1 sh -c 'grep -c "BookingQueue" /app/VanAn.ShopERP.dll 2>/dev/null' || echo "0"

echo "=== L1b: MIGRATIONS (PG __EFMigrationsHistory) ==="
docker exec vanan-gateway-1 sh -c 'psql -U vanan_admin -d vanan_gateway -t -c "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" LIKE '\''%Booking%'\'' OR \"MigrationId\" LIKE '\''%Attribution%'\'' ORDER BY \"MigrationId\";"' 2>/dev/null || \
docker exec vanan-gateway-1 sh -c 'psql -U postgres -d vanan_gateway -t -c "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" LIKE '\''%Booking%'\'' OR \"MigrationId\" LIKE '\''%Attribution%'\'' ORDER BY \"MigrationId\";"' || echo "psql credentials unknown - fallback needed"

echo "=== L2: HEALTH + PUBLIC ROUTE ==="
curl -s -o /dev/null -w "health: %{http_code}\n" http://localhost:80/health || echo "health probe failed"
# Public booking route — bogus tenant → kỳ vọng 404 friendly (Q5 gate, không 500)
curl -s -o /dev/null -w "public-services(bogus): %{http_code}\n" http://localhost:80/api/public/booking/tenants/00000000-0000-0000-0000-00000000b0b0/services
curl -s -o /dev/null -w "public-qr(bogus): %{http_code}\n" http://localhost:80/api/public/booking/qr/bogus-token-xyz?anonymousSessionId=rv
# Tenant API không auth → 401 (endpoint tồn tại + JWT enforced)
curl -s -o /dev/null -w "tenant-queue(no-auth): %{http_code}\n" http://localhost:80/api/tenant/booking/bookings

echo "=== DONE ==="
