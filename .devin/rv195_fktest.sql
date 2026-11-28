\echo '=== FK validated? ==='
SELECT conname, convalidated, condeferrable FROM pg_constraint WHERE conrelid = '"JournalEntryLine"'::regclass;
\echo '=== test: insert line with random JournalEntryId (should FK-fail if enforced) ==='
INSERT INTO "JournalEntryLine" ("JournalEntryId","Id","AccountNumber","DebitAmount","CreditAmount","Description")
VALUES (gen_random_uuid(), 999999, '999', 0, 0, 'fk-test');
\echo '=== cleanup test row if inserted ==='
DELETE FROM "JournalEntryLine" WHERE "Id" = 999999;