import sqlite3

db = sqlite3.connect("/tmp/rv179_full/vanan_shoperp.db")
c = db.cursor()
tid = "f60b64d0-a0d1-4220-9d7f-8f52dab05fa0"

print("--- products for RV B2 tenant ---")
print(c.execute("SELECT COUNT(*) FROM Products WHERE TenantId=?", (tid,)).fetchone())
print("--- ingredients ---")
print(c.execute("SELECT COUNT(*) FROM Ingredients WHERE TenantId=?", (tid,)).fetchone())
print("--- recipes ---")
print(c.execute("SELECT COUNT(*) FROM Recipes WHERE TenantId=?", (tid,)).fetchone())
print("--- sample products ---")
for row in c.execute("SELECT Name, Price FROM Products WHERE TenantId=? LIMIT 5", (tid,)):
    print(row)
print("--- tenant row ---")
print(c.execute("SELECT COUNT(*) FROM Tenants WHERE Id=?", (tid,)).fetchone())
