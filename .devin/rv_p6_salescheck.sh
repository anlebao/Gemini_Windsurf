#!/bin/bash
echo "=== salesman seed check (tenant A) ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d "VanAnCoreHub" -c "SELECT r.\"Id\", r.\"CustomerId\", r.\"RoleType\", r.\"IsActive\", r.\"TenantId\", c.\"FullName\", c.\"TenantId\" AS c_tenant FROM \"CommunityRoles\" r JOIN \"Customers\" c ON c.\"Id\" = r.\"CustomerId\" WHERE c.\"TenantId\" = '\''6aaf19e4-8b55-48cd-94d7-b61c96a2d66d'\'';"' 2>&1 | head -8
echo "=== how does QR create validate salesman? (QRAttributionService) ==="
docker exec vanan-gateway-1 sh -c 'grep -o "Salesman[^;]*" /app/VanAn.CoreHub.dll 2>/dev/null | head -3' || true
echo DONE
