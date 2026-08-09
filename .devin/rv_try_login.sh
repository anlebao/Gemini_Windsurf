#!/bin/bash
echo "=== PlatformUsers table ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c 'SELECT "Username", "Email", "Role", "IsActive" FROM "PlatformUsers" LIMIT 10;' 2>&1
echo ""
echo "=== Try login rvowner ==="
for pw in "rvowner" "RVowner@2024" "owner" "Owner@2024" "password" "123456" "rvowner123"; do
  RESP=$(curl -sk -X POST -H 'Content-Type: application/json' \
    -d "{\"username\":\"rvowner\",\"password\":\"$pw\"}" \
    "https://app.khachvip.online/api/platform/login")
  echo "  rvowner/$pw: $RESP"
done
echo ""
echo "=== Try login admin@trungnguyen.vn ==="
for pw in "admin" "Admin@2024" "password" "123456"; do
  RESP=$(curl -sk -X POST -H 'Content-Type: application/json' \
    -d "{\"username\":\"admin@trungnguyen.vn\",\"password\":\"$pw\"}" \
    "https://app.khachvip.online/api/platform/login")
  echo "  admin@trungnguyen.vn/$pw: $RESP"
done
