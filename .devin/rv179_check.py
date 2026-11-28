import sqlite3

db = sqlite3.connect("/tmp/vanan_shoperp.db")
c = db.cursor()
tid = "1833b55c-1022-4a6c-8056-8935f503a62b"

print("--- Tenants cols ---")
print([r[1] for r in c.execute("PRAGMA table_info(Tenants)")])
print("--- Products cols ---")
print([r[1] for r in c.execute("PRAGMA table_info(Products)")])

print("--- tenant row by Id ---")
print(c.execute("SELECT COUNT(*) FROM Tenants WHERE Id=?", (tid,)).fetchone())
print("--- tenant row by TenantId col (if exists) ---")
try:
    print(c.execute("SELECT COUNT(*) FROM Tenants WHERE TenantId=?", (tid,)).fetchone())
except Exception as e:
    print("n/a:", e)

print("--- products count by TenantId ---")
try:
    print(c.execute("SELECT COUNT(*) FROM Products WHERE TenantId=?", (tid,)).fetchone())
except Exception as e:
    print("err:", e)

print("--- totals: products / tenants ---")
print(c.execute("SELECT COUNT(*) FROM Products").fetchone())
print(c.execute("SELECT COUNT(*) FROM Tenants").fetchone())

print("--- products grouped by TenantId (top 8) ---")
try:
    for row in c.execute("SELECT TenantId, COUNT(*) FROM Products GROUP BY TenantId ORDER BY COUNT(*) DESC LIMIT 8"):
        print(row)
except Exception as e:
    print("err:", e)

print("--- sample product rows for target tenant (if any) ---")
try:
    for row in c.execute("SELECT Id, TenantId, Name, UnitPrice FROM Products WHERE TenantId=? LIMIT 5", (tid,)):
        print(row)
except Exception as e:
    print("err:", e)
