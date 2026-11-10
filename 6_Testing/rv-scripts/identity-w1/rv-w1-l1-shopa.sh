#!/bin/bash
# RV W1 L1b — SHOP-A VPS: ShopERP image + CoreHub.dll markers + SQLite Users columns
echo "== shoperp container =="
docker ps --format '{{.Names}} {{.Image}}' | grep -iE 'shoperp' || true
SHP=$(docker ps --format '{{.Names}}' | grep -iE 'shoperp' | head -1)
echo "ShopERP container: $SHP"
if [ -z "$SHP" ]; then echo "NO SHOPERP CONTAINER"; exit 1; fi
echo "== CoreHub.dll markers =="
for M in MfaService GenerateSetup RevokeBusinessVerification UserTrustLevel; do
  C=$(docker exec "$SHP" sh -c "grep -c '$M' /app/VanAn.CoreHub.dll" 2>/dev/null || echo 0)
  echo "$M: $C"
done
echo "== SQLite Users columns =="
DB=$(docker inspect "$SHP" --format '{{range .Config.Env}}{{println .}}{{end}}' 2>/dev/null | grep -iE 'SQLITE_DB_PATH' | head -1 | cut -d= -f2)
echo "SQLITE_DB_PATH: $DB"
if [ -z "$DB" ]; then DB=/app/vanan_shoperp.db; fi
docker exec "$SHP" sh -c "sqlite3 '$DB' 'PRAGMA table_info(Users);'" 2>/dev/null | grep -E 'PhoneNumber|PhoneVerifiedAt|VerifiedChannel|TrustLevel|MfaEnabled|MfaSecret|TrustedDeviceHash' || echo "sqlite3 fallback needed"
echo "== done =="