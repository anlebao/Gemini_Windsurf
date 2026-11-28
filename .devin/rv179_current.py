import sqlite3

db = sqlite3.connect("/tmp/rv179_full/vanan_shoperp.db")
c = db.cursor()

print("--- total products / ingredients / recipes ---")
print(c.execute("SELECT COUNT(*) FROM Products").fetchone())
print(c.execute("SELECT COUNT(*) FROM Ingredients").fetchone())
print(c.execute("SELECT COUNT(*) FROM Recipes").fetchone())

print("--- products per tenant ---")
for row in c.execute("SELECT TenantId, COUNT(*) FROM Products GROUP BY TenantId ORDER BY COUNT(*) DESC"):
    print(row)

print("--- latest 10 products by CreatedAt ---")
for row in c.execute("SELECT Id, TenantId, Name, CreatedAt FROM Products ORDER BY CreatedAt DESC LIMIT 10"):
    print(row)
