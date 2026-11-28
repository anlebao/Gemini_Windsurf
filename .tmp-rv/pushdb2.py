import sqlite3, sys
c = sqlite3.connect('/tmp/v.db')
print('== PushSubscriptions ==')
try:
    rows = c.execute("SELECT Id, TenantId, CustomerId, IsActive, IsDeleted, ExpiresAt FROM PushSubscriptions").fetchall()
    print('rows:', len(rows))
    for r in rows:
        print('id=', str(r[0])[:8], 'tenant=', r[1], 'cust=', str(r[2])[:12], 'active=', r[3], 'deleted=', r[4], 'exp=', r[5])
except Exception as e:
    print('ERR', e)
print('== Customers (rv) ==')
try:
    rows = c.execute("SELECT Id, TenantId, PhoneNumber, IsDeleted FROM Customers WHERE CAST(Id AS TEXT) LIKE '43094916%' OR PhoneNumber LIKE '%0900000001%'").fetchall()
    for r in rows:
        print('id=', str(r[0])[:12], 'tenant=', r[1], 'phone=', r[2], 'deleted=', r[3])
except Exception as e:
    print('ERR', e)