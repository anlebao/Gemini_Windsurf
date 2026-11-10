#!/bin/bash
# RV W1 — PG migration history row (tìm đúng tên bảng)
CS=$(docker inspect vanan-gateway-1 --format '{{range .Config.Env}}{{println .}}{{end}}' 2>/dev/null | grep -iE 'ConnectionStrings__DefaultConnection|ConnectionStrings:DefaultConnection|PG.*CONN' | head -1)
HOST=$(echo "$CS" | grep -oE 'Host=[^;]+' | cut -d= -f2)
PORT=$(echo "$CS" | grep -oE 'Port=[0-9]+' | cut -d= -f2)
DB=$(echo "$CS" | grep -oE 'Database=[^;]+' | cut -d= -f2)
U=$(echo "$CS" | grep -oE 'Username=[^;]+' | cut -d= -f2)
PW=$(echo "$CS" | grep -oE 'Password=[^;]+' | cut -d= -f2)
PG=$(docker ps --format '{{.Names}}' | grep -iE 'postgres' | head -1)
echo "== migration tables =="
docker exec -e PGPASSWORD="$PW" "$PG" sh -c "psql -h $HOST -p $PORT -U $U -d $DB -tAc \"SELECT tablename FROM pg_tables WHERE tablename LIKE '%Migration%';\"" 2>/dev/null
echo "== row =="
docker exec -e PGPASSWORD="$PW" "$PG" sh -c "psql -h $HOST -p $PORT -U $U -d $DB -tAc \"SELECT \\\"MigrationId\\\" FROM \\\"__EFMigrationsHistory\\\" WHERE \\\"MigrationId\\\" LIKE '%AddDemoUserTrustAndMfa%';\"" 2>/dev/null || echo "row query failed"
echo "== done =="