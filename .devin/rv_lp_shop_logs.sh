#!/bin/bash
# RV Batch 1 — ShopERP logs: award + sync event + subscriber mirror
set +e
CONTAINER=$(docker ps --format '{{.Names}}' | grep -i shoperp | head -1)
echo "container: $CONTAINER"
echo ""
echo "=== [A] LOYALTY award + BalanceSync (last 30m) ==="
docker logs "$CONTAINER" --since 30m 2>&1 | grep -E "LOYALTY|LoyaltyBalanceSync|LoyaltySyncSubscriber|Bug 6 fix|customer stub" | tail -25
echo ""
echo "=== [B] mọi log LoyaltySyncSubscriber (last 60m) ==="
docker logs "$CONTAINER" --since 60m 2>&1 | grep -E "LoyaltySyncSubscriber" | tail -20
