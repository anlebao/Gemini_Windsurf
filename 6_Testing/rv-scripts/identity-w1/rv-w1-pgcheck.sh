#!/bin/bash
# RV W1 — PG check owner trust fields (sau verify + deactivate)
CS=$(docker inspect vanan-gateway-1 --format '{{range .Config.Env}}{{println .}}{{end}}' 2>/dev/null | grep -iE 'ConnectionStrings__DefaultConnection|ConnectionStrings:DefaultConnection' | head -1)
HOST=$(echo "$CS" | grep -oE 'Host=[^;]+' | cut -d= -f2); PORT=$(echo "$CS" | grep -oE 'Port=[0-9]+' | cut -d= -f2)
DB=$(echo "$CS" | grep -oE 'Database=[^;]+' | cut -d= -f2); U=$(echo "$CS" | grep -oE 'Username=[^;]+' | cut -d= -f2)
PW=$(echo "$CS" | grep -oE 'Password=[^;]+' | cut -d= -f2)
PG=$(docker ps --format '{{.Names}}' | grep -iE 'postgres' | head -1)
docker exec -e PGPASSWORD="$PW" "$PG" sh -c "psql -h $HOST -p $PORT -U $U -d $DB -tAc \"SELECT Username, TrustLevel, \\\"PhoneVerifiedAt\\\" IS NOT NULL AS verified, VerifiedChannel, length(\\\"PhoneNumber\\\") AS phone_len, \\\"PhoneNumber\\\" LIKE '%090%' AS phone_plaintext, Role FROM \\\"Users\\\" WHERE Username='rvw1178069';\"" 2>/dev/null || echo "QUERY FAILED"
echo "== done =="