#!/bin/bash
echo "=== Order tạo từ booking (TrackingCode = PublicBookingCode) ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d "VanAnCoreHub" -c "SELECT \"Id\", \"TrackingCode\", \"TotalAmount\", \"TenantId\", \"CreatedAt\" FROM \"Orders\" WHERE \"TrackingCode\" IN (SELECT \"PublicBookingCode\" FROM \"Bookings\" WHERE \"CustomerDeviceId\" LIKE '\''sweep-%'\'') ORDER BY \"CreatedAt\" DESC LIMIT 8;"' 2>&1 | head -12
echo "=== bookings completed hôm nay + OrderId link ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d "VanAnCoreHub" -c "SELECT \"PublicBookingCode\", \"Status\", \"OrderId\" FROM \"Bookings\" WHERE \"CustomerDeviceId\" LIKE '\''sweep-%'\'' AND \"Status\" = 6 ORDER BY \"StartAt\" DESC LIMIT 6;"' 2>&1 | head -10
echo DONE
