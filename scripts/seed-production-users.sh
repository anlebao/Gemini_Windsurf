#!/bin/bash
# Production User Seeding Script for VanAn Ecosystem (Linux VPS)
# Creates initial users for each role with BCrypt password hashing
# Wave 5: Added SystemAdmin platform-level role

set -e

# Default values
DATABASE_PATH="${1:-/app/data/vanan.db}"
DEFAULT_PASSWORD="${2:?ERROR: password argument required (usage: $0 <database_path> <password>)}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

echo -e "${CYAN}=== VanAn Production User Seeding ===${NC}"
echo "Database: $DATABASE_PATH"
echo "Default Password: $DEFAULT_PASSWORD"
echo ""

# Check if database exists
if [ ! -f "$DATABASE_PATH" ]; then
    echo -e "${RED}Error: Database not found at $DATABASE_PATH${NC}"
    exit 1
fi

# Check if sqlite3 is installed
if ! command -v sqlite3 &> /dev/null; then
    echo -e "${RED}Error: sqlite3 not found. Install with: apt-get install sqlite3${NC}"
    exit 1
fi

# Default tenant ID
DEFAULT_TENANT_ID="11111111-1111-1111-1111-111111111111"

# Generate BCrypt hash using Python
function bcrypt_hash() {
    local password="$1"
    python3 -c "
import bcrypt
import sys
password = sys.argv[1]
salt = bcrypt.gensalt(rounds=12)
hashed = bcrypt.hashpw(password.encode('utf-8'), salt)
print(hashed.decode('utf-8'))
" "$password"
}

# Check/create default tenant
echo -e "${YELLOW}Checking default tenant...${NC}"
TENANT_COUNT=$(sqlite3 "$DATABASE_PATH" "SELECT COUNT(*) FROM Tenants WHERE Id = '$DEFAULT_TENANT_ID';")

if [ "$TENANT_COUNT" -eq 0 ]; then
    echo "Creating default tenant..."
    sqlite3 "$DATABASE_PATH" <<EOF
INSERT INTO Tenants (Id, Name, BusinessType, Status, CreatedAt, UpdatedAt, TenantId)
VALUES ('$DEFAULT_TENANT_ID', 'Default Shop', 0, 0, datetime('now'), datetime('now'), '$DEFAULT_TENANT_ID');
EOF
    echo -e "${GREEN}✓ Default tenant created${NC}"
else
    echo -e "${GREEN}✓ Default tenant already exists${NC}"
fi

echo ""
echo -e "${YELLOW}Creating users...${NC}"

# Define users
declare -A USERS
USERS[owner@vanan.vn]="Owner|Shop Owner|$DEFAULT_TENANT_ID"
USERS[storekeeper@vanan.vn]="StoreKeeper|Store Keeper|$DEFAULT_TENANT_ID"
USERS[guard@vanan.vn]="Guard|Security Guard|$DEFAULT_TENANT_ID"
USERS[staff@vanan.vn]="Staff|Staff Member|$DEFAULT_TENANT_ID"
USERS[masterchef@vanan.vn]="Masterchef|Master Chef|$DEFAULT_TENANT_ID"
USERS[systemadmin@vanan.vn]="SystemAdmin|System Administrator|00000000-0000-0000-0000-000000000000"

# Create users
for username in "${!USERS[@]}"; do
    IFS='|' read -r role display_name tenant_id <<< "${USERS[$username]}"

    # Check if user exists
    USER_COUNT=$(sqlite3 "$DATABASE_PATH" "SELECT COUNT(*) FROM Users WHERE Username = '$username';")

    if [ "$USER_COUNT" -gt 0 ]; then
        echo -e "  → User $username already exists, skipping..." -e "${NC}"
        continue
    fi

    # Generate user ID
    USER_ID=$(uuidgen | tr '[:upper:]' '[:lower:]')

    # Hash password
    PASSWORD_HASH=$(bcrypt_hash "$DEFAULT_PASSWORD")

    # Insert user
    sqlite3 "$DATABASE_PATH" <<EOF
INSERT INTO Users (Id, Username, PasswordHash, DisplayName, Email, Role, IsActive, TenantId, CreatedAt, UpdatedAt)
VALUES ('$USER_ID', '$username', '$PASSWORD_HASH', '$display_name', '$username', '$role', 1, '$tenant_id', datetime('now'), datetime('now'));
EOF

    # Create UserTenant mapping for tenant-level users
    if [ "$tenant_id" != "00000000-0000-0000-0000-000000000000" ]; then
        USER_TENANT_ID=$(uuidgen | tr '[:upper:]' '[:lower:]')
        sqlite3 "$DATABASE_PATH" <<EOF
INSERT INTO UserTenants (Id, UserId, TenantId, Role, AssignedAt, IsActive, TenantIdValue)
VALUES ('$USER_TENANT_ID', '$USER_ID', '$tenant_id', '$role', datetime('now'), 1, '$tenant_id');
EOF
    fi

    echo -e "  ${GREEN}✓${NC} Created user: $username ($role)"
done

echo ""
echo -e "${GREEN}=== Seeding Complete ===${NC}"
echo "Total users created: ${#USERS[@]}"
echo ""
echo -e "${CYAN}Login credentials:${NC}"
for username in "${!USERS[@]}"; do
    echo "  $username / $DEFAULT_PASSWORD"
done
echo ""
echo -e "${YELLOW}⚠️  IMPORTANT: Change default passwords immediately after first login!${NC}"