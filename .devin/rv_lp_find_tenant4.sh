#!/bin/bash
set +e
for TID in $(docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "SELECT \"Id\" FROM \"Tenants\" WHERE \"Status\" = 1 ORDER BY \"CreatedAt\" DESC;"); do
  NAME=$(docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "SELECT \"Name\" FROM \"Tenants\" WHERE \"Id\" = '$TID';")
  RESP=$(curl -s -k --max-time 8 "https://api2.khachvip.online/api/tenants/$TID/feature-settings")
  EN=$(echo "$RESP" | python3 -c "import sys,json
try:
  d=json.load(sys.stdin)
  v=d.get('loyalty_Program_Enabled', d.get('Loyalty_Program_Enabled','?'))
  r=d.get('loyalty_PointsRate', d.get('Loyalty_PointsRate','?'))
  print(f'enabled={v} rate={r}')
except Exception as e: print('ERR')" 2>/dev/null)
  echo "$TID $NAME -> $EN"
done
