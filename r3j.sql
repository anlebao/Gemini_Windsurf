SELECT j."Id"::text, j."EntryDate"::date::text, COALESCE(j."ReferenceType",'null') AS reftype,
       COALESCE(j."ReferenceId"::text,'null') AS refid,
       SUM(CASE WHEN l."AccountNumber"~'^[57]' THEN l."CreditAmount" ELSE 0 END) AS rev,
       SUM(CASE WHEN l."AccountNumber"~'^[68]' THEN l."DebitAmount" ELSE 0 END) AS exp
FROM "JournalEntries" j JOIN "JournalEntryLine" l ON l."JournalEntryId"=j."Id"
WHERE j."TenantId"='00000000-0000-0000-0000-000000000001'::uuid
  AND j."EntryDate" >= '2026-10-01'::date AND j."EntryDate" < '2026-11-01'::date
GROUP BY j."Id", j."EntryDate", j."ReferenceType", j."ReferenceId"
HAVING (SUM(CASE WHEN l."AccountNumber"~'^[57]' THEN l."CreditAmount" ELSE 0 END)>0
    OR SUM(CASE WHEN l."AccountNumber"~'^[68]' THEN l."DebitAmount" ELSE 0 END)>0)
  AND (j."ReferenceId" IS NULL OR NOT EXISTS (SELECT 1 FROM "AccountingEntries" e WHERE e."Id"=j."ReferenceId"))
ORDER BY j."EntryDate";
