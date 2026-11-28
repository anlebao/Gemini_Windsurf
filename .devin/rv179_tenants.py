import sqlite3

db = sqlite3.connect("/tmp/rv179.db")
c = db.cursor()

print("--- all Tenants (Id, Name, TenantId, ShopInstanceId, Status) ---")
for row in c.execute("SELECT Id, Name, TenantId, ShopInstanceId, Status FROM Tenants"):
    print(row)

print("--- products per tenant (Id, count) ---")
for row in c.execute("SELECT TenantId, COUNT(*) FROM Products GROUP BY TenantId ORDER BY COUNT(*) DESC"):
    print(row)
