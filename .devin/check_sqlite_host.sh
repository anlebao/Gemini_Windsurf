#!/bin/bash
DB=/var/lib/docker/volumes/vanan_shoperp_data/_data/vanan_shoperp.db
echo "=== SQLite: order 019f8e5d ==="
sudo sqlite3 "$DB" "SELECT Id, Status, TenantId FROM Orders WHERE Id LIKE '019f8e5d%';"
echo "---"
echo "=== SQLite: total orders ==="
sudo sqlite3 "$DB" "SELECT COUNT(*) FROM Orders;"
echo "=== SQLite: last 5 orders ==="
sudo sqlite3 "$DB" "SELECT Id, Status, CreatedAt FROM Orders ORDER BY CreatedAt DESC LIMIT 5;"
