#!/bin/bash
echo "=== Customers schema ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d "VanAnCoreHub" -c "\d \"Customers\""' 2>&1 | head -35
echo DONE
