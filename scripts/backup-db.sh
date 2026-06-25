#!/bin/bash
set -euo pipefail

# Wave 7: SQLite backup script
# - WAL checkpoint (truncate) for all *.db files in current directory
# - Copy databases to ./backups/YYYY-MM-DD/
# - Retain 7 days of backups

BACKUP_ROOT="./backups"
DB_PATTERN="*.db"
RETENTION_DAYS=7

TODAY=$(date +%Y-%m-%d)
BACKUP_DIR="$BACKUP_ROOT/$TODAY"

mkdir -p "$BACKUP_DIR"

for db in $DB_PATTERN; do
    if [ -f "$db" ]; then
        echo "Checkpointing $db..."
        sqlite3 "$db" "PRAGMA wal_checkpoint(TRUNCATE);"
        echo "Backing up $db to $BACKUP_DIR..."
        cp "$db" "$BACKUP_DIR/$(basename "$db")"
    fi
done

if [ -d "$BACKUP_ROOT" ]; then
    echo "Cleaning up backups older than $RETENTION_DAYS days..."
    find "$BACKUP_ROOT" -maxdepth 1 -type d -mtime +$RETENTION_DAYS -exec rm -rf {} +
fi

echo "Backup completed: $BACKUP_DIR"
