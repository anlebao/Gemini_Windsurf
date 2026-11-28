#!/bin/bash
echo "=== reproduce QR bogus ==="
docker exec vanan-gateway-1 sh -c 'curl -s -o /dev/null -w "qr-bogus: %{http_code}\n" "localhost:80/api/public/booking/qr/bogus-token-xyz?anonymousSessionId=rv2"'
sleep 2
echo "=== gateway logs: QR + exception ==="
docker logs vanan-gateway-1 --since 3m 2>&1 | grep -i -A 20 "booking/qr\|ResolveQr\|PublicQR" | tail -50
echo "=== migrations in VanAnCoreHub ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d "VanAnCoreHub" -t -c "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" LIKE '\''%Booking%'\'' OR \"MigrationId\" LIKE '\''%Attribution%'\'' ORDER BY \"MigrationId\";"' 2>&1 | head -10
echo "=== booking tables ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d "VanAnCoreHub" -t -c "SELECT tablename FROM pg_tables WHERE schemaname='\''public'\'' AND tablename IN ('\''Bookings'\'','\''Staffs'\'','\''QRChannels'\'','\''AttributionSessions'\'','\''CommissionLedgerEntries'\'','\''BookingTenantConfigs'\'');"' 2>&1 | head -10
echo DONE
