#!/bin/bash
set +e
CONTAINER=$(docker ps --format '{{.Names}}' | grep -i shoperp | head -1)
echo "container: $CONTAINER"
echo ""
echo "=== Subscriber mirror logs (last 15m) — customer eb5b6a96 / device 05124909 ==="
docker logs "$CONTAINER" --since 15m 2>&1 | grep -iE "LoyaltySyncSubscriber|eb5b6a96|05124909" | tail -25
echo ""
echo "=== Mọi log Loyalty trong 15m ==="
docker logs "$CONTAINER" --since 15m 2>&1 | grep -iE "loyalty" | tail -15
