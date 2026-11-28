#!/bin/bash
echo "=== Bookings staff 4e2fb124 (tenant A) ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d "VanAnCoreHub" -c "SELECT \"PublicBookingCode\", \"StartAt\", \"Status\", \"CustomerDeviceId\", \"StaffId\" FROM \"Bookings\" WHERE \"TenantId\" = '\''6aaf19e4-8b55-48cd-94d7-b61c96a2d66d'\'' AND \"StaffId\" = '\''4e2fb124-adc9-467f-a79f-db2ff87bc84e'\'' ORDER BY \"StartAt\" DESC LIMIT 10;"' 2>&1 | head -14
echo "=== tất cả booking active hôm nay ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d "VanAnCoreHub" -c "SELECT \"PublicBookingCode\", \"StartAt\", \"Status\", \"StaffId\", \"CustomerDeviceId\" FROM \"Bookings\" WHERE \"TenantId\" = '\''6aaf19e4-8b55-48cd-94d7-b61c96a2d66d'\'' AND \"Status\" IN (1,2,3,4,5) ORDER BY \"StartAt\" LIMIT 15;"' 2>&1 | head -18
echo "=== index hiện tại ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d "VanAnCoreHub" -c "SELECT indexname, indexdef FROM pg_indexes WHERE tablename = '\''Bookings'\'' AND indexname LIKE '\''%StaffId_StartAt%'\'';"' 2>&1 | head -5
echo DONE
