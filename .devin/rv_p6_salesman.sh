#!/bin/bash
# Seed PG (fix -i) + tạo QR_A salesman cho tenant A
APP="https://app2.khachvip.online"
GW="https://api2.khachvip.online"
TENANT_A="6aaf19e4-8b55-48cd-94d7-b61c96a2d66d"
NOW="$(date -u '+%Y-%m-%d %H:%M:%S')"
SALESMAN_A="$(python3 -c 'import uuid; print(uuid.uuid4())')"

echo "=== login ==="
rm -f /tmp/rv_sysadmin_cookies.txt
curl -s -o /dev/null -c /tmp/rv_sysadmin_cookies.txt -X POST "$APP/Login" \
  -d 'Username=sysadmin@vanan.vn&Password=2026@vanan&RememberMe=false' -k --max-redirs 0
JWT_A=$(curl -s -X POST "$APP/api/admin/impersonate/$TENANT_A" -H 'Content-Type: application/json' \
  -b /tmp/rv_sysadmin_cookies.txt -k | python3 -c "import sys,json
try: print(json.load(sys.stdin).get('token',''))
except Exception: print('')")
echo "JWT_A len=${#JWT_A}"

echo "=== PG seed (docker exec -i) ==="
docker exec -i vanan-postgres-1 sh -c "psql -U vanan_admin -d VanAnCoreHub" <<SQL
INSERT INTO "Customers" ("Id","CustomerId","FullName","PhoneNumber","LoyaltyPoints","CustomerTier","IsActive","TenantId","CreatedAt","UpdatedAt","IsDeleted") VALUES
 ('$SALESMAN_A','$SALESMAN_A','RV Salesman A','0900000111',0,'Bronze',true,'$TENANT_A','$NOW','$NOW',false)
ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "CommunityRoles" ("Id","CustomerId","RoleType","ActivatedAt","ActivatedBy","IsActive","SalesmanCode","TenantId","CreatedAt","UpdatedAt","IsDeleted") VALUES
 ('$(python3 -c 'import uuid; print(uuid.uuid4())')','$SALESMAN_A',2,'$NOW','00000000-0000-0000-0000-000000000001',true,'RVA001','$TENANT_A','$NOW','$NOW',false)
ON CONFLICT ("Id") DO NOTHING;
SELECT 'customer+role inserted' AS status;
SQL

echo "=== verify salesman ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d "VanAnCoreHub" -c "SELECT r.\"RoleType\", r.\"IsActive\", c.\"FullName\" FROM \"CommunityRoles\" r JOIN \"Customers\" c ON c.\"Id\" = r.\"CustomerId\" WHERE c.\"TenantId\" = '\''6aaf19e4-8b55-48cd-94d7-b61c96a2d66d'\'';"' 2>&1 | head -8

echo "=== tạo QR_A (salesman) ==="
QR_A="$(python3 -c 'import uuid; print(uuid.uuid4())')"
curl -s -X POST "$GW/api/tenant/booking/qr-channels" -H "Authorization: Bearer $JWT_A" -H 'Content-Type: application/json' \
  -d "{\"qrToken\":\"$QR_A\",\"salesmanId\":\"$SALESMAN_A\",\"campaignId\":null,\"attributionExpiryAt\":null}" | head -c 300
echo ""
echo "QR_A_TOKEN=$QR_A"
echo "SALESMAN_A=$SALESMAN_A"
echo DONE
