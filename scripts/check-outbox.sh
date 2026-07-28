#!/bin/bash
sudo docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c 'SELECT status, count(*) FROM "OutboxMessages" GROUP BY status ORDER BY status;'
echo "---RECENT---"
sudo docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c 'SELECT "Id", "EventType", "Status", "RoutingKey", "CreatedAt", "ProcessedAt", "Error" FROM "OutboxMessages" ORDER BY "CreatedAt" DESC LIMIT 10;'
