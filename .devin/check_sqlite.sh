#!/bin/sh
echo "=== SQLite: order 019f8e5d ==="
sqlite3 /app/keys/vanan_shoperp.db "SELECT Id, Status, TenantId FROM Orders WHERE Id LIKE '019f8e5d%' LIMIT 5;"
echo "---"
echo "=== SQLite: total orders ==="
sqlite3 /app/keys/vanan_shoperp.db "SELECT COUNT(*) FROM Orders;"
echo "=== SQLite: last 5 orders ==="
sqlite3 /app/keys/vanan_shoperp.db "SELECT Id, Status, CreatedAt FROM Orders ORDER BY CreatedAt DESC LIMIT 5;"
