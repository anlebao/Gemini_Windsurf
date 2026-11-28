#!/bin/bash
# RV Batch 4 — CLOSE dev-token window on vanan-shop-a + verify + cleanup SQLite test customers.
# Per dev-token-secret.md: secret removed via plain compose up (CD-generated .env.shoperp has no
# DEV_TOKEN_SECRET), verify env=0 + endpoint 404.
set -e
cd /opt/vanan

echo "=== delete test customers from ShopERP SQLite ==="
sudo docker exec vanan-shoperp-1 sh -c "python3 - <<'PY' 2>/dev/null || sqlite3 /app/keys/vanan_shoperp.db \"DELETE FROM Customers WHERE PhoneNumber IN ('0900000011','0900000012','0900000013');\"
import sqlite3
con = sqlite3.connect('/app/keys/vanan_shoperp.db')
cur = con.execute(\"DELETE FROM Customers WHERE PhoneNumber IN ('0900000011','0900000012','0900000013')\")
con.commit()
print('sqlite deleted:', cur.rowcount)
PY" || echo "sqlite cleanup best-effort (container may lack python3/sqlite3)"

echo "=== close window: recreate shoperp WITHOUT DEV_TOKEN_SECRET ==="
sudo docker compose -f docker-compose.shoperp.yml --env-file .env.shoperp up -d --force-recreate shoperp
sleep 12
echo "--- env check (expect 0):"
sudo docker exec vanan-shoperp-1 sh -c 'env | grep -c "^DevToken__Secret=" || true'
echo "--- endpoint check (expect 404):"
curl -sk -o /dev/null -w "%{http_code}" -X POST https://api2.khachvip.online/api/customer-identity/dev-token -H "Content-Type: application/json" -d '{"phoneNumber":"0900000099"}'; echo
rm -f /tmp/.devtoken_rv_secret /tmp/rvb4_*.json
echo "=== window CLOSED ==="
