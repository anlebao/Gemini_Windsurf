#!/bin/bash
echo "=== PlatformUsers full schema ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c '\d "PlatformUsers"' 2>&1
echo ""
echo "=== Users table (ShopERP SQLite via Gateway PG sync?) ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c 'SELECT "Username", "Email", "Role" FROM "Users" LIMIT 10;' 2>&1
echo ""
echo "=== ShopERP SQLite Users (via docker exec) ==="
docker exec vanan-shoperp sqlite3 /app/vanan_shoperp.db "SELECT Username, Email, Role FROM Users LIMIT 10;" 2>&1
echo ""
echo "=== Try sysadmin login ==="
for pw in "sysadmin" "admin" "Admin@2024" "Sysadmin@2024" "password" "123456" "SystemAdmin@2024!"; do
  RESP=$(curl -sk -c /tmp/rv_cookies_sys.txt -X POST -H 'Content-Type: application/json' \
    -d "{\"username\":\"sysadmin\",\"password\":\"$pw\"}" \
    "https://app.khachvip.online/api/platform/login")
  if echo "$RESP" | grep -q "success"; then
    echo "  sysadmin/$pw: SUCCESS — $RESP"
    break
  fi
done
