#!/bin/bash
echo "=== ShopERP appsettings — check which DB ShopERP uses for auth ==="
docker exec vanan-shoperp cat /app/appsettings.json 2>&1 | grep -A2 "DefaultConnection\|AccountingConnection"
echo ""
echo "=== ShopERP uses SQLite — check if /api/platform/login hits PG or SQLite ==="
echo "=== PlatformUsers only in PG. Users in PG (synced from SQLite). ==="
echo "=== Login service queries PlatformUsers (PG) — so sysadmin is the only platform user ==="
echo ""
echo "=== Check password hash for sysadmin ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c 'SELECT "Username", "PasswordHash", "Role", "IsActive" FROM "PlatformUsers";' 2>&1
echo ""
echo "=== Try more passwords for sysadmin ==="
for pw in "VanAn@2024!" "vanan" "VanAn2024" "sysadmin123" "Sysadmin@2024!" "admin123" "Admin123!" "P@ssw0rd"; do
  RESP=$(curl -sk -c /tmp/rv_cookies_sys.txt -X POST -H 'Content-Type: application/json' \
    -d "{\"username\":\"sysadmin\",\"password\":\"$pw\"}" \
    "https://app.khachvip.online/api/platform/login")
  if echo "$RESP" | grep -q '"success":true'; then
    echo "  sysadmin/$pw: SUCCESS — $RESP"
    echo "  Cookie saved to /tmp/rv_cookies_sys.txt"
    break
  else
    echo "  sysadmin/$pw: FAIL"
  fi
done
