#!/bin/bash
SALESMAN_A="12ada883-1cf3-424e-8c9b-93f5cad27d42"
TENANT_A="6aaf19e4-8b55-48cd-94d7-b61c96a2d66d"
NOW="$(date -u '+%Y-%m-%d %H:%M:%S')"
docker exec -i vanan-postgres-1 sh -c "psql -U vanan_admin -d VanAnCoreHub" <<SQL
INSERT INTO "Customers" ("Id","FullName","PhoneNumber","LoyaltyPoints","CustomerTier","TotalSpent","IsActive","TenantId","CreatedAt","UpdatedAt","IsDeleted","IdentityLevel","FacebookShareCount","TikTokShareCount") VALUES
 ('$SALESMAN_A','RV Salesman A','0900000111',0,'Bronze',0,true,'$TENANT_A','$NOW','$NOW',false,1,0,0)
ON CONFLICT ("Id") DO NOTHING;
SELECT 'customer inserted' AS status;
SQL
echo "=== verify salesman join ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d "VanAnCoreHub" -c "SELECT r.\"RoleType\", r.\"IsActive\", r.\"SalesmanCode\", c.\"FullName\" FROM \"CommunityRoles\" r JOIN \"Customers\" c ON c.\"Id\" = r.\"CustomerId\" WHERE c.\"TenantId\" = '\''6aaf19e4-8b55-48cd-94d7-b61c96a2d66d'\'';"' 2>&1 | head -8
echo DONE
