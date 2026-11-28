#!/bin/bash
# RV Batch 4 — open dev-token window on vanan-shop-a + mint 3 test customer tokens.
# Per .devin/rules/dev-token-secret.md: secret is inline env ONLY (never persisted to .env.shoperp,
# never echoed). Tokens printed to stdout for use by the gateway-side RV script.
set -e
cd /opt/vanan

echo "=== open dev-token window ==="
openssl rand -hex 32 > /tmp/.devtoken_rv_secret
chmod 600 /tmp/.devtoken_rv_secret
sudo sh -c 'DEV_TOKEN_SECRET="$(cat /tmp/.devtoken_rv_secret)" docker compose -f docker-compose.shoperp.yml --env-file .env.shoperp up -d --force-recreate shoperp'
sleep 12
echo "--- env check (expect 1):"
sudo docker exec vanan-shoperp-1 sh -c 'env | grep -c "^DevToken__Secret=.\+"'

SECRET=$(cat /tmp/.devtoken_rv_secret)
mint() {
  curl -sk -X POST https://api2.khachvip.online/api/customer-identity/dev-token \
    -H "X-Dev-Secret: $SECRET" -H "Content-Type: application/json" \
    -d "{\"phoneNumber\":\"$1\",\"tenantId\":\"$2\",\"displayName\":\"$3\"}"
  echo
}

TENANT_A="0dfab177-86ad-4513-bd69-ddf956c7b768"
# Tenant B resolved dynamically on the gateway script — mint owner B without tenant pin first;
# we re-pin below once we know a real second tenant id (done gateway-side via PG update).
echo "=== mint SHIPPER (phone 0900000011, tenant A) ==="
mint "0900000011" "$TENANT_A" "RV B4 Shipper" | tee /tmp/rvb4_shipper.json
echo "=== mint OWNER_A (phone 0900000012, tenant A — lazy bind via ContactPhone) ==="
mint "0900000012" "$TENANT_A" "RV B4 OwnerA" | tee /tmp/rvb4_ownerA.json
echo "=== mint OWNER_B (phone 0900000013 — tenant pinned gateway-side after tenant list query) ==="
mint "0900000013" "$TENANT_A" "RV B4 OwnerB" | tee /tmp/rvb4_ownerB.json
echo "=== window OPEN — tokens in /tmp/rvb4_*.json ==="
