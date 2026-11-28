#!/bin/bash
echo "=== L2: probe INSIDE gateway container ==="
docker exec vanan-gateway-1 sh -c 'curl -s -o /dev/null -w "health: %{http_code}\n" localhost:80/health'
docker exec vanan-gateway-1 sh -c 'curl -s -o /dev/null -w "public-services(bogus): %{http_code}\n" "localhost:80/api/public/booking/tenants/00000000-0000-0000-0000-00000000b0b0/services"'
docker exec vanan-gateway-1 sh -c 'curl -s -o /dev/null -w "public-qr(bogus): %{http_code}\n" "localhost:80/api/public/booking/qr/bogus-token-xyz?anonymousSessionId=rv"'
docker exec vanan-gateway-1 sh -c 'curl -s -o /dev/null -w "tenant-queue(no-auth): %{http_code}\n" "localhost:80/api/tenant/booking/bookings"'
docker exec vanan-gateway-1 sh -c 'curl -s -o /dev/null -w "tenant-config(no-auth): %{http_code}\n" "localhost:80/api/tenant/booking/config"'
echo "=== L2b: QR resolve bogus message (Q5 gate friendly) ==="
docker exec vanan-gateway-1 sh -c 'curl -s "localhost:80/api/public/booking/qr/bogus-token-xyz?anonymousSessionId=rv" | head -c 200'
echo ""
echo "=== L1b: MIGRATIONS via postgres container ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d vanan_gateway -t -c "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" LIKE '\''%Booking%'\'' OR \"MigrationId\" LIKE '\''%Attribution%'\'' ORDER BY \"MigrationId\";"' 2>&1 | head -10 || \
docker exec vanan-postgres-1 sh -c 'psql -U postgres -d vanan_gateway -t -c "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" LIKE '\''%Booking%'\'' OR \"MigrationId\" LIKE '\''%Attribution%'\'' ORDER BY \"MigrationId\";"' 2>&1 | head -10
echo "=== L1b2: booking tables exist ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d vanan_gateway -t -c "SELECT tablename FROM pg_tables WHERE tablename IN ('\''Bookings'\'','\''Staffs'\'','\''QRChannels'\'','\''AttributionSessions'\'','\''CommissionLedgerEntries'\'','\''BookingTenantConfigs'\'');"' 2>&1 | head -10
echo DONE
