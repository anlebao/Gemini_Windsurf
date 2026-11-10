#!/bin/bash
# RV W1 L1a — GATEWAY VPS: image sha + PG migration applied
echo "== gateway image =="
docker ps --format '{{.Names}} {{.Image}}' | grep -iE 'gateway' || true
echo "== PG migration AddDemoUserTrustAndMfa =="
PG=$(docker ps --format '{{.Names}}' | grep -iE 'postgres' | head -1)
echo "PG container: $PG"
for DB in vanan vanan_gateway postgres; do
  for U in postgres vanan; do
    R=$(docker exec "$PG" sh -c "psql -U $U -d $DB -tAc \"SELECT MigrationId FROM __EFMigrationsHistory WHERE MigrationId LIKE '%AddDemoUserTrustAndMfa%';\"" 2>/dev/null)
    if [ -n "$R" ]; then echo "FOUND ($U/$DB): $R"; break 2; fi
  done
done
echo "== PG Users columns =="
docker exec "$PG" sh -c "psql -U postgres -d vanan -tAc \"SELECT column_name FROM information_schema.columns WHERE table_name='Users' AND column_name IN ('PhoneNumber','PhoneVerifiedAt','VerifiedChannel','TrustLevel','MfaEnabled','MfaSecret','TrustedDeviceHash');\"" 2>/dev/null || echo "pg users column query failed (fallback needed)"
echo "== done =="