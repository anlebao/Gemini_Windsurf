#!/bin/bash
echo "=== RV FULL: Plate-Optional Refactor ==="

echo ""
echo "=== L1: Migration + DB ==="
echo "L1.1 Migration:"
docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc \
  'SELECT "MigrationId" FROM "__EFMigrationsHistory" WHERE "MigrationId" LIKE '\''%PlateNumberOptional%'\'''
echo "L1.2 Column nullable:"
docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc \
  "SELECT is_nullable FROM information_schema.columns WHERE table_name='VehicleSessions' AND column_name='PlateNumber'"

echo ""
echo "=== L2: Gateway listening ports ==="
docker exec vanan-gateway-1 cat /proc/net/tcp | awk '{print $2}' | tail -n +2 | while read line; do
  port_hex=$(echo $line | cut -d: -f2)
  port=$((16#$port_hex))
  if [ $port -gt 1000 ] && [ $port -lt 65536 ]; then
    echo "  Listening on port: $port"
  fi
done

echo ""
echo "=== L3: Try dev/login on various ports ==="
for port in 80 8080 5001 5000; do
  code=$(docker exec vanan-gateway-1 curl -s -o /dev/null -w "%{http_code}" -X POST http://localhost:$port/dev/login \
    -H "Content-Type: application/json" \
    -d '{"role":"SystemAdmin"}' 2>/dev/null)
  echo "  localhost:$port/dev/login -> $code"
  if [ "$code" = "200" ] || [ "$code" = "201" ]; then
    echo "  SUCCESS on port $port!"
    TOKEN=$(docker exec vanan-gateway-1 curl -s -X POST http://localhost:$port/dev/login \
      -H "Content-Type: application/json" \
      -d '{"role":"SystemAdmin"}' | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
    if [ -z "$TOKEN" ]; then
      TOKEN=$(docker exec vanan-gateway-1 curl -s -X POST http://localhost:$port/dev/login \
        -H "Content-Type: application/json" \
        -d '{"role":"SystemAdmin"}' | grep -o '"accessToken":"[^"]*"' | head -1 | cut -d'"' -f4)
    fi
    echo "  Token length: ${#TOKEN}"
    break
  fi
done

echo ""
echo "=== L4: Try via nginx (port 80) ==="
for path in "/dev/login" "/api/auth/login"; do
  code=$(curl -s -o /dev/null -w "%{http_code}" -X POST http://127.0.0.1$path \
    -H "Content-Type: application/json" \
    -d '{"role":"SystemAdmin"}' 2>/dev/null)
  echo "  http://127.0.0.1$path -> $code"
done

echo ""
echo "=== L5: Check DevLoginController availability ==="
docker exec vanan-gateway-1 env | grep -i "ASPNETCORE_ENVIRONMENT\|ENABLE_DEV" 2>/dev/null || echo "  (no env vars found)"

echo ""
echo "=== L6: Check Gateway logs for dev/login ==="
docker logs vanan-gateway-1 2>&1 | grep -i "dev/login\|DevLogin" | tail -5 || echo "  (no dev/login in logs)"

echo ""
echo "=== L7: Guard API via nginx (no auth) ==="
curl -s -o /dev/null -w "  GET /api/guard/sessions/today: %{http_code}\n" http://127.0.0.1/api/guard/sessions/today
curl -s -o /dev/null -w "  POST /api/guard/issue: %{http_code}\n" -X POST http://127.0.0.1/api/guard/issue \
  -H "Content-Type: application/json" -d '{}'

echo ""
echo "=== L8: Existing sessions ==="
docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc \
  'SELECT count(*) FILTER (WHERE "PlateNumber" IS NULL) AS null_plates, count(*) FILTER (WHERE "PlateNumber" IS NOT NULL) AS has_plates, count(*) AS total FROM "VehicleSessions"'

echo ""
echo "=== RV Complete ==="
