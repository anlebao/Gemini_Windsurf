#!/bin/bash
set +e
CONTAINER=$(docker ps --format '{{.Names}}' | grep -i shoperp | head -1)
echo "=== mọi log chứa 01a0beb3 hoặc Loyalty/LOYALTY (last 60m) ==="
docker logs "$CONTAINER" --since 60m 2>&1 | grep -iE "01a0beb3|loyalty|LOYALTY|AddPoints|award" | tail -40
