#!/bin/bash
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c 'SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 5;'
