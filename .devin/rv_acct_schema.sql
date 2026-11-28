\echo '=== column types: JournalEntries / AccountingEntries / Orders / Tenants ==='
SELECT table_name, column_name, data_type
FROM information_schema.columns
WHERE (table_name IN ('JournalEntries','AccountingEntries','Orders','Tenants'))
  AND column_name IN ('TenantId','Id','EntryType','AccountCode','Reference','PaymentStatus','CreatedAt','Type','EntryDate')
ORDER BY table_name, column_name;