-- Journal kỳ 10 có rev-credit KHÔNG khớp phần entry được tính (nguồn +diff)
SELECT j."Id"::text, j."EntryDate"::date::text AS jdate, COALESCE(j."ReferenceType",'null') AS rtype,
       SUM(CASE WHEN l."AccountNumber"~'^[57]' THEN l."CreditAmount" ELSE 0 END) AS jrev,
       COALESCE(e."EntryType"::text,'null') AS etype, COALESCE(e."AccountCode",'null') AS eacc,
       COALESCE(e."Amount"::text,'null') AS eamt, COALESCE(e."TransactionDate"::text,'null') AS edate,
       COALESCE(e."Reference",'') AS eref
FROM "JournalEntries" j
JOIN "JournalEntryLine" l ON l."JournalEntryId"=j."Id"
LEFT JOIN "AccountingEntries" e ON e."Id"=j."ReferenceId"
WHERE j."TenantId"='00000000-0000-0000-0000-000000000001'::uuid
  AND j."EntryDate" >= '2026-10-01'::date AND j."EntryDate" < '2026-11-01'::date
GROUP BY j."Id", j."EntryDate", j."ReferenceType", e."TransactionDate", e."AccountCode", e."EntryType", e."Amount", e."Reference"
HAVING ABS(SUM(CASE WHEN l."AccountNumber"~'^[57]' THEN l."CreditAmount" ELSE 0 END)
       - COALESCE(CASE WHEN e."EntryType" IN (1,5) AND e."AccountCode"~'^[57]'
                        AND e."TransactionDate">='2026-10-01' AND e."TransactionDate"<'2026-11-01'
                       THEN e."Amount" ELSE 0 END, 0)) > 0.01
   AND SUM(CASE WHEN l."AccountNumber"~'^[57]' THEN l."CreditAmount" ELSE 0 END) > 0
ORDER BY j."EntryDate";
