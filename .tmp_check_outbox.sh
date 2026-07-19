#!/bin/bash
echo "=== Outbox recent events ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c 'SELECT "EventType","Status","RoutingKey" FROM "OutboxMessages" ORDER BY "CreatedAt" DESC LIMIT 5;'
echo ""
echo "=== Order in PG ==="
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c 'SELECT "Id","PaymentStatus","PaymentMethod" FROM "Orders" ORDER BY "OrderDate" DESC LIMIT 3;'
echo ""
echo "=== OrderSyncSubscriber logs ==="
docker logs vanan-shoperp --since 2m 2>&1 | grep -i 'OrderSync\|order.created\|synced order' | tail -10
