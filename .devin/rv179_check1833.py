import sqlite3

db = sqlite3.connect("/tmp/rv179_full/vanan_shoperp.db")
c = db.cursor()

for tid in ["1833B55C-1022-4A6C-8056-8935F503A62B", "1833b55c-1022-4a6c-8056-8935f503a62b"]:
    print(tid, "products:", c.execute("SELECT COUNT(*) FROM Products WHERE TenantId=?", (tid,)).fetchone(),
          "| tenants row:", c.execute("SELECT COUNT(*) FROM Tenants WHERE Id=?", (tid,)).fetchone())

print("--- tenants list (Id, Name) ---")
for row in c.execute("SELECT Id, Name FROM Tenants"):
    print(row)
