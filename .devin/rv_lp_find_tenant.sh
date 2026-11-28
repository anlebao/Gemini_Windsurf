#!/bin/bash
# Find a tenant with Loyalty_Program_Enabled=true (via public feature-settings endpoint)
set +e
echo "=== Tenants (PG) ==="
docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "
SELECT \"Id\", \"Name\" FROM \"Tenants\" WHERE \"IsDeleted\" = false ORDER BY \"CreatedAt\" DESC LIMIT 25;"
echo ""
echo "=== Probe feature-settings per tenant ==="
for TID in $(docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "SELECT \"Id\" FROM \"Tenants\" WHERE \"IsDeleted\" = false ORDER BY \"CreatedAt\" DESC LIMIT 25;"); do
  RESP=$(curl -s -k --max-time 8 "https://api2.khachvip.online/api/tenants/$TID/feature-settings")
  EN=$(echo "$RESP" | python3 -c "import sys,json
try:
  d=json.load(sys.stdin)
  print(d.get('loyalty_Program_Enabled', d.get('Loyalty_Program_Enabled','?')))
except: print('ERR')" 2>/dev/null)
  RATE=$(echo "$RESP" | python3 -c "import sys,json
try:
  d=json.load(sys.stdin)
  print(d.get('loyalty_PointsRate', d.get('Loyalty_PointsRate','?')))
except: print('-')" 2>/dev/null)
  echo "$TID -> enabled=$EN rate=$RATE"
done
