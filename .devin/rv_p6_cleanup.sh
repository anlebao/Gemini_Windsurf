#!/bin/bash
# Dọn 2 booking sweep cũ (StaffAssigned — không cancel được qua API do §9.3) + dependents
TENANT_A="6aaf19e4-8b55-48cd-94d7-b61c96a2d66d"
docker exec -i vanan-postgres-1 sh -c "psql -U vanan_admin -d VanAnCoreHub" <<SQL
DELETE FROM "BookingIdempotencyRecords" WHERE "TenantId" = '$TENANT_A' AND "BookingId" IN
  (SELECT "Id" FROM "Bookings" WHERE "TenantId" = '$TENANT_A' AND "CustomerDeviceId" LIKE 'sweep-%' AND "Status" IN (1,2,3,4,5));
DELETE FROM "BookingStaffAssignments" WHERE "BookingId" IN
  (SELECT "Id" FROM "Bookings" WHERE "TenantId" = '$TENANT_A' AND "CustomerDeviceId" LIKE 'sweep-%' AND "Status" IN (1,2,3,4,5));
DELETE FROM "BookingEvents" WHERE "BookingId" IN
  (SELECT "Id" FROM "Bookings" WHERE "TenantId" = '$TENANT_A' AND "CustomerDeviceId" LIKE 'sweep-%' AND "Status" IN (1,2,3,4,5));
DELETE FROM "BookingItems" WHERE "BookingId" IN
  (SELECT "Id" FROM "Bookings" WHERE "TenantId" = '$TENANT_A' AND "CustomerDeviceId" LIKE 'sweep-%' AND "Status" IN (1,2,3,4,5));
DELETE FROM "Bookings" WHERE "TenantId" = '$TENANT_A' AND "CustomerDeviceId" LIKE 'sweep-%' AND "Status" IN (1,2,3,4,5);
SELECT 'cleaned' AS status;
SQL
echo "=== còn lại sweep active? ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d "VanAnCoreHub" -t -c "SELECT COUNT(*) FROM \"Bookings\" WHERE \"TenantId\" = '\''6aaf19e4-8b55-48cd-94d7-b61c96a2d66d'\'' AND \"CustomerDeviceId\" LIKE '\''sweep-%'\'' AND \"Status\" IN (1,2,3,4,5);"' 2>&1 | head -3
echo DONE
