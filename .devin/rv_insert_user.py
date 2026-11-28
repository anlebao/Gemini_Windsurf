import sqlite3, sys
con = sqlite3.connect("/var/lib/docker/volumes/vanan_shoperp_data/_data/vanan_shoperp.db", timeout=30)
cur = con.execute("select count(*) from PlatformUsers where Username=?", ("rv-presence-16610c22",))
assert cur.fetchone()[0]==0
con.execute("insert into PlatformUsers (Id,Username,PasswordHash,DisplayName,Email,Role,IsActive,CreatedAt) values (?,?,?,?,?,?,?,datetime('now'))",
  ("C8814362-4D7F-4A5F-A08C-04DEE7AC2182", "rv-presence-16610c22", "$2b$10$mLoTjrhr/3WSYDzEDkQht.5TIrlS6M3kdgrrsdDXlk6Y2LVbLaEce", "RV Presence", "rv-presence-16610c22@rv.local", 0, 1))
con.commit()
print("inserted", con.execute("select count(*) from PlatformUsers").fetchone()[0])
con.close()
