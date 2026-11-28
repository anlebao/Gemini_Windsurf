#!/bin/bash
# RV Batch 1 — ShopERP SQLite: LoyaltyRewards + customer stub + subscriber logs
set +e
CONTAINER=$(docker ps --format '{{.Names}}' | grep -i shoperp | head -1)
echo "container: $CONTAINER"
echo ""
echo "=== SQLite file ==="
docker exec "$CONTAINER" sh -c 'ls -la /app/*.db 2>/dev/null || ls -la /app/data/*.db 2>/dev/null || find /app -name "*.db" 2>/dev/null | head'
echo ""
echo "=== LoyaltyRewards rows gần đây (SQLite) ==="
docker exec "$CONTAINER" sh -c 'which sqlite3 >/dev/null 2>&1 && echo has_sqlite3 || echo no_sqlite3; which python3 >/dev/null 2>&1 && echo has_python3 || echo no_python3'
