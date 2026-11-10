#!/bin/bash
# RV W1 — copy db + wal + shm rồi check columns (WAL-aware)
SHP=$(docker ps --format '{{.Names}}' | grep -iE 'shoperp' | head -1)
mkdir -p /tmp/rvw1 && cd /tmp/rvw1
docker cp "$SHP":/app/keys/vanan_shoperp.db . 2>/dev/null
docker cp "$SHP":/app/keys/vanan_shoperp.db-wal . 2>/dev/null
docker cp "$SHP":/app/keys/vanan_shoperp.db-shm . 2>/dev/null
python3 -c "
import sqlite3
c=sqlite3.connect('/tmp/rvw1/vanan_shoperp.db')  # sẽ tự đọc WAL cùng thư mục
cols=[r[1] for r in c.execute('PRAGMA table_info(Users)')]
want=['PhoneNumber','PhoneVerifiedAt','VerifiedChannel','TrustLevel','MfaEnabled','MfaSecret','TrustedDeviceHash']
print('FOUND:', [x for x in want if x in cols])
print('MISSING:', [x for x in want if x not in cols])
rows=list(c.execute('SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" LIKE \"%AddDemoUserTrustAndMfa%\"'))
print('MIGRATION ROW:', rows)
"
rm -rf /tmp/rvw1
echo "== done =="