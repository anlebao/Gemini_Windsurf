#!/bin/bash
echo "=== Platform Users ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c "SELECT username, email, role FROM platform_users LIMIT 10;"
echo ""
echo "=== Try login as admin ==="
LOGIN_RESP=$(curl -sk -c /tmp/rv_cookies2.txt -X POST -H 'Content-Type: application/json' \
  -d '{"username":"admin","password":"admin"}' \
  "https://app.khachvip.online/api/platform/login")
echo "Login (admin/admin): $LOGIN_RESP"

LOGIN_RESP2=$(curl -sk -c /tmp/rv_cookies3.txt -X POST -H 'Content-Type: application/json' \
  -d '{"username":"admin","password":"Admin@2024!"}' \
  "https://app.khachvip.online/api/platform/login")
echo "Login (admin/Admin@2024!): $LOGIN_RESP2"

LOGIN_RESP3=$(curl -sk -c /tmp/rv_cookies4.txt -X POST -H 'Content-Type: application/json' \
  -d '{"username":"systemadmin","password":"admin"}' \
  "https://app.khachvip.online/api/platform/login")
echo "Login (systemadmin/admin): $LOGIN_RESP3"
