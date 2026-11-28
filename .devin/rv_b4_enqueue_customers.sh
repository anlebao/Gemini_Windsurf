#!/bin/bash
# RV Batch 4 — enqueue CustomerCreated outbox events on shop-a so the 3 test
# customers sync to Gateway PG (dev-token path doesn't enqueue the event itself).
set -e
DB=/var/lib/docker/volumes/vanan_shoperp_data/_data/vanan_shoperp.db

sudo python3 - "$DB" <<'PY'
import sqlite3, sys, json, uuid, datetime
db = sys.argv[1]
con = sqlite3.connect(db)
cols = [r[1] for r in con.execute("PRAGMA table_info(OutboxMessages)")]
print("OutboxMessages cols:", cols)
ten = "0dfab177-86ad-4513-bd69-ddf956c7b768"
customers = [
    ("97baf992-f7f4-4b0f-a1ca-539b2705d873", "RV B4 Shipper", "0900000011"),
    ("55ce0560-e06f-4247-8274-cb9262bed837", "RV B4 OwnerA",  "0900000012"),
    ("372317ff-4f31-45ae-8d8f-dbb728e4e522", "RV B4 OwnerB",  "0900000013"),
]
now = datetime.datetime.utcnow().strftime("%Y-%m-%d %H:%M:%S")
for cid, name, phone in customers:
    exists = con.execute("SELECT COUNT(*) FROM OutboxMessages WHERE EventType='CustomerCreated' AND EventData LIKE ?", ('%'+cid+'%',)).fetchone()[0]
    if exists:
        print("already enqueued:", cid); continue
    payload = json.dumps({"CustomerId": cid, "TenantId": ten, "FullName": name,
                          "PhoneNumber": phone, "Email": None, "DeviceId": None, "IdentityLevel": 1})
    con.execute(
        'INSERT INTO OutboxMessages ("Id","EventType","EventData","CreatedAt","ProcessedAt","Error","RetryCount","NextRetryAt","RoutingKey","CorrelationId","TenantId","Status") '
        'VALUES (?,?,?,?,NULL,NULL,0,?,NULL,NULL,?,0)',
        (str(uuid.uuid4()), "CustomerCreated", payload, now, now, ten))
    print("enqueued:", cid, name)
con.commit()
print("pending outbox:", con.execute("SELECT COUNT(*) FROM OutboxMessages WHERE Status=0").fetchone()[0])
PY
echo "=== enqueued — NatsSyncWorker will publish within its poll interval ==="
