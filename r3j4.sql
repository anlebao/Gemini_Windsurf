-- Tất cả journal kỳ 10 có rev-credit + entry linked (eyeball toàn bộ)
SELECT j."EntryDate"::date::text AS jdate, j."Id"::text, COALESCE(j."ReferenceType",'null') AS rtype,
       SUM(CASE WHEN l."AccountNumber"~'^[57]' THEN l."CreditAmount" ELSE 0 END) AS jrev,
       COALESCE(e."Id"::text,'NO-ENTRY') AS eid, COALESCE(e."EntryType"::text,'') AS et,
       COALESCE(e."AccountCode",'') AS eacc, COALESCE(e."Amount"::text,'') AS eamt,
       COALESCE(e."TransactionDate"::text,'') AS edate, COALESCE(e."Reference",'') AS eref
FROM "JournalEntries" j
JOIN "JournalEntryLine" l ON l."JournalEntryId"=j."Id"
LEFT JOIN "AccountingEntries" e ON e."Id"=j."ReferenceId"
WHERE j."TenantId"='00000000-0000-0000-0000-000000000001'::uuid
  AND j."EntryDate" >= '2026-10-01'::date AND j."EntryDate" < '2026-11-01'::date
GROUP BY j."Id", j."EntryDate", j."ReferenceType", e."Id", e."EntryType", e."AccountCode", e."Amount", e."TransactionDate", e."Reference"
HAVING SUM(CASE WHEN l."AccountNumber"~'^[57]' THEN l."CreditAmount" ELSE 0 END) > 0
ORDER BY jrev DESC LIMIT 40;
