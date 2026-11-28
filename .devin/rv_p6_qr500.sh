#!/bin/bash
echo "=== gateway connection string (db name only, no secrets) ==="
docker exec vanan-gateway-1 sh -c 'env | grep -i "CONNECTION\|PGHOST\|DB_" | sed "s/Password=.*/Password=***/" | head -8'
echo "=== gateway log around the QR 500 (last 2 min) ==="
docker logs vanan-gateway-1 --since 5m 2>&1 | grep -i -B2 -A15 "exception\|error\|Unhandled" | tail -60
echo "=== postgres databases ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d postgres -t -c "SELECT datname FROM pg_database;"' 2>&1 | head -10
echo DONE
