#!/bin/bash
echo "=== booking seed data hiện có (PG VanAnCoreHub) ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d "VanAnCoreHub" -t -c "SELECT COUNT(*) AS qr FROM \"QRChannels\"; SELECT COUNT(*) AS staffs FROM \"Staffs\"; SELECT COUNT(*) AS offerings FROM \"AppointmentOfferings\"; SELECT COUNT(*) AS configs FROM \"BookingTenantConfigs\"; SELECT COUNT(*) AS bookings FROM \"Bookings\";"' 2>&1 | head -10
echo "=== configs detail ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d "VanAnCoreHub" -c "SELECT \"TenantId\", \"IsEnabled\", \"DepositPolicy\" FROM \"BookingTenantConfigs\" LIMIT 5;"' 2>&1 | head -10
echo "=== tenants active (top 5) ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d "VanAnCoreHub" -t -c "SELECT \"Id\", \"Name\" FROM \"Tenants\" WHERE \"Status\" = 1 LIMIT 8;"' 2>&1 | head -10
echo "=== dev-token endpoint (RV window?) ==="
docker exec vanan-gateway-1 sh -c 'curl -s -o /dev/null -w "dev-token: %{http_code}\n" -X POST "localhost:80/api/dev-token" -H "Content-Type: application/json" -d "{\"tenantId\":\"00000000-0000-0000-0000-000000000001\",\"role\":\"Owner\"}"'
echo DONE
