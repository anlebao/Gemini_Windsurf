#!/bin/bash
# RV W1 — SQLite mirror TrustLevel cho rvw1178069 (WAL-aware)
SHP=$(docker ps --format '{{.Names}}' | grep -iE 'shoperp' | head -1)
mkdir -p /tmp/rvw1 && cd /tmp/rvw1
docker cp "$SHP":/app/keys/vanan_shoperp.db . 2>/dev/null
docker cp "$SHP":/app/keys/vanan_shoperp.db-wal . 2>/dev/null
docker cp "$SHP":/app/keys/vanan_shoperp.db-shm . 2>/dev/null
python3 -c "
import sqlite3
c=sqlite3.connect('/tmp/rvw1/vanan_shoperp.db')
for r in c.execute(\"SELECT Username, TrustLevel, PhoneNumber IS NULL AS phone_null FROM Users WHERE Username='rvw1178069'\"):
    print('sqlite user:', r)
"
rm -rf /tmp/rvw1
echo "== done =="