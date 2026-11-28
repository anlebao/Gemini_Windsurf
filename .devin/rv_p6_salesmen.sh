#!/bin/bash
echo "=== tenant status check ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d "VanAnCoreHub" -c "SELECT \"Id\", \"Name\", \"Status\" FROM \"Tenants\" WHERE \"Id\" IN ('\''6aaf19e4-8b55-48cd-94d7-b61c96a2d66d'\'','\''4345dff8-b4b7-411f-b8f1-faf36f242d70'\'','\''35037648-335e-40fc-b6a9-ef8562f815a6'\'');"' 2>&1 | head -8
echo "=== salesmen (CommunityRole Salesman) per tenant ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d "VanAnCoreHub" -c "SELECT r.\"TenantId\", c.\"FullName\", c.\"Id\" AS customer_id FROM \"CommunityRoles\" r JOIN \"Customers\" c ON c.\"Id\" = r.\"CustomerId\" WHERE r.\"RoleType\" IN (1, 2, 3) OR r.\"RoleType\" = 4 LIMIT 15;"' 2>&1 | head -15
echo "=== CommunityRole RoleType values ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d "VanAnCoreHub" -c "SELECT DISTINCT \"RoleType\" FROM \"CommunityRoles\" LIMIT 10;"' 2>&1 | head -8
echo DONE
