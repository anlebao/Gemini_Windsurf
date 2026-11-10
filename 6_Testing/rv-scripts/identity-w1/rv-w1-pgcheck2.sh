#!/bin/bash
# RV W1 — PG check (hiện lỗi nếu có)
CS=$(docker inspect vanan-gateway-1 --format '{{range .Config.Env}}{{println .}}{{end}}' 2>/dev/null | grep -iE 'ConnectionStrings__DefaultConnection|ConnectionStrings:DefaultConnection' | head -1)
HOST=$(echo "$CS" | grep -oE 'Host=[^;]+' | cut -d= -f2); PORT=$(echo "$CS" | grep -oE 'Port=[0-9]+' | cut -d= -f2)
DB=$(echo "$CS" | grep -oE 'Database=[^;]+' | cut -d= -f2); U=$(echo "$CS" | grep -oE 'Username=[^;]+' | cut -d= -f2)
PW=$(echo "$CS" | grep -oE 'Password=[^;]+' | cut -d= -f2)
PG=$(docker ps --format '{{.Names}}' | grep -iE 'postgres' | head -1)
docker exec -e PGPASSWORD="$PW" "$PG" psql -h "$HOST" -p "$PORT" -U "$U" -d "$DB" -c "SELECT \"Username\" AS u, \"TrustLevel\" AS tl, (\"PhoneVerifiedAt\" IS NOT NULL) AS verified, \"VerifiedChannel\" AS ch, length(\"PhoneNumber\") AS plen, (\"PhoneNumber\" <> '0909178069') AS not_plain FROM \"Users\" WHERE \"Username\" = 'rvw1178069';"
echo "== done =="