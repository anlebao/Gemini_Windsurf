#!/bin/bash
echo "=== All tables in VanAnCoreHub ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c "\dt" 2>&1 | head -40
echo ""
echo "=== Users-related tables ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c "\dt *user*" 2>&1
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c "\dt *platform*" 2>&1
echo ""
echo "=== Try Users table ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c "SELECT username, email, \"Role\" FROM \"Users\" LIMIT 10;" 2>&1
