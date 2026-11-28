-- Entry 1d08a2cd chi tiết + journal afae337e trỏ tới entry nào + lines của 2 journal nghi
SELECT 'ENTRY' AS kind, e."Id"::text, e."EntryType"::text, e."AccountCode", e."Amount"::text,
       e."TransactionDate"::text, COALESCE(e."Reference",'<null>'), e."AccountingBookType"::text
FROM "AccountingEntries" e WHERE e."Id"='1d08a2cd-02ec-4371-a8b5-5b589fd8783f'::uuid
UNION ALL
SELECT 'J-REFID', j."Id"::text, j."ReferenceType", COALESCE(j."ReferenceId"::text,'null'), '', j."EntryDate"::text, COALESCE(j."Description",'<null>'), ''
FROM "JournalEntries" j WHERE j."Id" IN ('afae337e-3add-4712-b3ea-4fc064e9dd74'::uuid,'6cb588c3-fdb8-4f8a-b6db-5099f9da273d'::uuid)
UNION ALL
SELECT 'LINE', j."Id"::text, l."AccountNumber", l."DebitAmount"::text, l."CreditAmount"::text, '', '', ''
FROM "JournalEntryLine" l JOIN "JournalEntries" j ON j."Id"=l."JournalEntryId"
WHERE j."Id" IN ('afae337e-3add-4712-b3ea-4fc064e9dd74'::uuid,'6cb588c3-fdb8-4f8a-b6db-5099f9da273d'::uuid)
ORDER BY 1, 2;
