#!/bin/bash
P="docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub"
echo "=== AccrualReceivableJE flags ==="
$P -c "SELECT \"Key\", \"Value\" FROM \"SystemSettings\" WHERE \"Key\" ILIKE '%AccrualReceivable%' LIMIT 20;"
echo "=== PostingStatus skip reasons ==="
$P -c "SELECT \"PostingStatus\", \"SkipReason\", count(*) FROM \"EntryPostingStatuses\" WHERE \"SkipReason\" IS NOT NULL GROUP BY 1,2 ORDER BY 3 DESC LIMIT 10;"
echo "=== JE ManualEntry gần nhất — lines + MappingVersion ==="
$P -c "SELECT j.\"PostingPurpose\", j.\"MappingVersion\", l.\"AccountNumber\", l.\"DebitAmount\", l.\"CreditAmount\" FROM \"JournalEntries\" j JOIN \"JournalEntryLine\" l ON l.\"JournalEntryId\" = j.\"Id\" WHERE j.\"ReferenceType\" = 'ManualEntry' ORDER BY j.\"CreatedAt\" DESC LIMIT 10;"
