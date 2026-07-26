#!/bin/bash
# Debug RV failures
echo "=== KhachLink fingerprint.js timestamp ==="
docker exec vanan-khachlink stat -c "%y" /usr/share/nginx/html/js/fingerprint.js
echo ""
echo "=== Migration tables in PG ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT tablename FROM pg_tables WHERE schemaname='public' AND tablename ILIKE '%migration%';"
echo ""
echo "=== Sprint0 migration row (lowercase table) ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT count(*) FROM __efmigrationshistory WHERE migrationid = '20260726105331_CommunitySprint0';"
echo ""
echo "=== Sprint0 migration row (mixed case, quoted) ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c 'SELECT count(*) FROM "__EFMigrationsHistory" WHERE "MigrationId" = '"'"'20260726105331_CommunitySprint0'"'"';'
echo ""
echo "=== shoperp-home with -L (follow redirects) ==="
curl -sk -L -o /dev/null -w '%{http_code}' https://khachvip.online/
echo ""
