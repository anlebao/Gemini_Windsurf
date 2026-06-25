#!/usr/bin/env python3
"""
=============================================================================
VAN AN ECOSYSTEM — Create Owner User Script
=============================================================================
Dùng khi deploy lên server mới để tạo Tenant + Owner user đầu tiên.

Yêu cầu:
  - Chạy trên server (hoặc qua SSH) khi PostgreSQL container đang chạy
  - python3-bcrypt đã cài: sudo apt-get install -y python3-bcrypt
  - Docker container tên "vanan-postgres" đang healthy

Cách dùng:
  python3 create_owner_user.py
  python3 create_owner_user.py --username admin --password MyPass@2026 --tenant "Cửa Hàng ABC"

Database schema (verified từ production):
  Tenants : Id, Name, BusinessType(int), HKDGroup(int), CreatedAt, IsActive
  Users   : Id, TenantId, Username, PasswordHash, DisplayName, Role(int),
             IsActive, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted

Enum values:
  BusinessType : Company=1, HouseholdBusiness=2
  HKDGroup     : Group1=1, Group2=2, Group3=3
  UserRole     : None=0, Owner=1, StoreKeeper=2, Guard=3, Staff=4, Masterchef=5
=============================================================================
"""

import subprocess
import sys
import uuid
import argparse
from datetime import datetime, timezone

# ── Dependency check ──────────────────────────────────────────────────────
try:
    import bcrypt
except ImportError:
    print("ERROR: python3-bcrypt not installed.")
    print("Run: sudo apt-get install -y python3-bcrypt")
    sys.exit(1)

# ── Argument parsing ──────────────────────────────────────────────────────
parser = argparse.ArgumentParser(
    description="Create Owner user for VanAn ShopERP on a fresh server."
)
parser.add_argument("--username",      default="adminvanan",      help="Login username")
parser.add_argument("--password",      default="VanAn@2026",      help="Login password (plain text)")
parser.add_argument("--display",       default="Admin Vạn An",    help="Display name")
parser.add_argument("--tenant",        default="Vạn An",          help="Tenant/shop name")
parser.add_argument("--business-type", default=2, type=int,       help="BusinessType: Company=1, HouseholdBusiness=2")
parser.add_argument("--hkd-group",     default=1, type=int,       help="HKDGroup: Group1=1, Group2=2, Group3=3")
parser.add_argument("--container",     default="vanan-postgres",  help="PostgreSQL Docker container name")
parser.add_argument("--db-user",       default="vanan_admin",     help="PostgreSQL username")
parser.add_argument("--db-name",       default="VanAnCoreHub",    help="PostgreSQL database name")
args = parser.parse_args()

POSTGRES_CONTAINER = args.container
DB_USER            = args.db_user
DB_NAME            = args.db_name
USERNAME           = args.username
PASSWORD           = args.password
DISPLAY            = args.display
TENANT_NAME        = args.tenant
BUSINESS_TYPE      = args.business_type
HKD_GROUP          = args.hkd_group
ROLE_INT           = 1  # Owner

# ── Helpers ───────────────────────────────────────────────────────────────
def separator():
    print("-" * 60)

def psql_query(sql):
    """Run SQL, return stdout text."""
    r = subprocess.run(
        ["docker", "exec", POSTGRES_CONTAINER, "psql",
         "-U", DB_USER, "-d", DB_NAME, "-t", "-c", sql],
        capture_output=True, text=True
    )
    return r.stdout.strip(), r.stderr.strip(), r.returncode

def psql_exec(sql, description=""):
    """Run SQL, print result, return True on success."""
    r = subprocess.run(
        ["docker", "exec", POSTGRES_CONTAINER, "psql",
         "-U", DB_USER, "-d", DB_NAME, "-c", sql],
        capture_output=True, text=True
    )
    if r.stdout.strip():
        print(r.stdout.strip())
    if r.returncode != 0 or "ERROR" in r.stderr:
        print(f"FAIL{' (' + description + ')' if description else ''}: {r.stderr[:400]}")
        return False
    return True

# ── Step 0: Verify container is running ──────────────────────────────────
separator()
print("STEP 0: Verify PostgreSQL container")
separator()
r = subprocess.run(
    ["docker", "inspect", "--format", "{{.State.Status}}", POSTGRES_CONTAINER],
    capture_output=True, text=True
)
status = r.stdout.strip()
print(f"Container '{POSTGRES_CONTAINER}' status: {status}")
if status != "running":
    print("ERROR: Container is not running. Start it first.")
    sys.exit(1)
print("OK")

# ── Step 1: Check if user already exists ─────────────────────────────────
separator()
print("STEP 1: Check if username already exists")
separator()
out, err, rc = psql_query(f"SELECT COUNT(*) FROM \"Users\" WHERE \"Username\" = '{USERNAME}';")
count = out.strip()
print(f"Existing rows with username '{USERNAME}': {count}")
if count != '0':
    print(f"WARNING: Username '{USERNAME}' already exists. Nothing to do.")
    print("Use --username to specify a different username.")
    sys.exit(0)
print("OK: Username is available.")

# ── Step 2: Get or create Tenant ─────────────────────────────────────────
separator()
print("STEP 2: Get or create Tenant")
separator()

out, err, rc = psql_query("SELECT \"Id\", \"Name\" FROM \"Tenants\" LIMIT 1;")
lines = [l.strip() for l in out.splitlines() if l.strip() and "|" in l]

if lines:
    parts      = lines[0].split("|")
    tenant_id  = parts[0].strip()
    tenant_name_db = parts[1].strip()
    print(f"Existing tenant found: '{tenant_name_db}' ({tenant_id})")
    print("Using existing tenant.")
else:
    tenant_id = str(uuid.uuid4())
    now_ts    = datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M:%S+00')
    print(f"No tenant found. Creating '{TENANT_NAME}'...")
    ok = psql_exec(
        f"""INSERT INTO "Tenants" ("Id", "Name", "BusinessType", "HKDGroup", "CreatedAt", "IsActive")
VALUES ('{tenant_id}', '{TENANT_NAME.replace("'", "''")}', {BUSINESS_TYPE}, {HKD_GROUP}, '{now_ts}', true);""",
        "create tenant"
    )
    if not ok:
        sys.exit(1)
    print(f"Tenant created: {tenant_id}")

# ── Step 3: Generate BCrypt hash ─────────────────────────────────────────
separator()
print("STEP 3: Generate BCrypt password hash (work factor 12)")
separator()
pw_hash     = bcrypt.hashpw(PASSWORD.encode("utf-8"), bcrypt.gensalt(12)).decode("utf-8")
pw_hash_sql = pw_hash.replace("'", "''")  # escape for SQL
print(f"Hash prefix: {pw_hash[:29]}...")
print("OK")

# ── Step 4: Insert user ───────────────────────────────────────────────────
separator()
print("STEP 4: Insert Owner user into DB")
separator()
user_id = str(uuid.uuid4())
now_ts  = datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M:%S+00')

ok = psql_exec(
    f"""INSERT INTO "Users"
    ("Id", "TenantId", "Username", "PasswordHash", "DisplayName", "Role",
     "IsActive", "CreatedAt", "UpdatedAt", "CreatedBy", "UpdatedBy", "IsDeleted")
VALUES
    ('{user_id}', '{tenant_id}', '{USERNAME}', '{pw_hash_sql}',
     '{DISPLAY.replace("'", "''")}', {ROLE_INT},
     true, '{now_ts}', '{now_ts}', 'setup-script', 'setup-script', false);""",
    "insert user"
)
if not ok:
    sys.exit(1)

# ── Step 5: Verify ────────────────────────────────────────────────────────
separator()
print("STEP 5: Verify")
separator()
out, err, rc = psql_query(
    f"SELECT \"Id\", \"Username\", \"Role\", \"IsActive\", \"TenantId\" "
    f"FROM \"Users\" WHERE \"Username\" = '{USERNAME}';"
)
print(out)

# ── Done ──────────────────────────────────────────────────────────────────
separator()
print("SUCCESS: Owner user created.")
separator()
print(f"  Tenant   : {TENANT_NAME} ({tenant_id})")
print(f"  Username : {USERNAME}")
print(f"  Password : {PASSWORD}")
print(f"  Role     : Owner")
print(f"  Display  : {DISPLAY}")
print()
print("Login at: https://<your-domain>/Login")
print()
print("IMPORTANT: Change the password after first login!")
separator()
