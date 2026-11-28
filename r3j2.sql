-- Journal kỳ 10 trỏ entry ngoài kỳ hoặc lệch số tiền
SELECT j."Id"::text, j."EntryDate"::date::text AS jdate, e."TransactionDate"::text AS edate,
       e."AccountCode" AS eacc, e."EntryType" AS etype, e."Amount" AS eamt,
       SUM(CASE WHEN l."AccountNumber"~'^[57]' THEN l."CreditAmount" ELSE 0 END) AS jrev,
       COALESCE(e."Reference",'') AS eref
FROM "JournalEntries" j
JOIN "JournalEntryLine" l ON l."JournalEntryId"=j."Id"
JOIN "AccountingEntries" e ON e."Id"=j."ReferenceId"
WHERE j."TenantId"='00000000-0000-0000-0000-000000000001'::uuid
  AND j."EntryDate" >= '2026-10-01'::date AND j."EntryDate" < '2026-11-01'::date
GROUP BY j."Id", j."EntryDate", e."TransactionDate", e."AccountCode", e."EntryType", e."Amount", e."Reference"
HAVING (e."TransactionDate" < '2026-10-01' OR e."TransactionDate" >= '2026-11-01')
   OR ABS(SUM(CASE WHEN l."AccountNumber"~'^[57]' THEN l."CreditAmount" ELSE 0 END) - e."Amount") > 0.01
ORDER BY j."EntryDate" LIMIT 40;
