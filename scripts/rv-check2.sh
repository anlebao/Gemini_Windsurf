#!/bin/bash
PSQL='docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub'
echo "=== Tenants count ==="
$PSQL -c 'SELECT COUNT(*) FROM "Tenants";'
echo "=== Latest 3 migrations ==="
$PSQL -c 'SELECT migration_id FROM "__EFMigrationsHistory" ORDER BY id DESC LIMIT 3;'
echo "=== Tenants with coordinates ==="
$PSQL -c 'SELECT "Id", "Name", "Settings_Latitude", "Settings_Longitude" FROM "Tenants" LIMIT 5;'
