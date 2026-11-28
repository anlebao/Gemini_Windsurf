#!/bin/bash
PSQL="docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub"
echo "--- UserPermissionGroups cols ---"
$PSQL -tAc "SELECT column_name FROM information_schema.columns WHERE table_name='UserPermissionGroups';"
echo "--- PermissionGroups cols ---"
$PSQL -tAc "SELECT column_name FROM information_schema.columns WHERE table_name='PermissionGroups';"
