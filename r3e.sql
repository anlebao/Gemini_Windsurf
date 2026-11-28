-- Entries được tính revE kỳ 10 — so với 21 linked eids
SELECT e."Id"::text, e."Amount"::text, e."AccountCode", e."EntryType"::text, e."TransactionDate"::text, COALESCE(e."Reference",'')
FROM "AccountingEntries" e
WHERE e."TenantId"='00000000-0000-0000-0000-000000000001'::uuid
  AND e."EntryType"=1 AND e."AccountCode"~'^[57]'
  AND e."TransactionDate">='2026-10-01' AND e."TransactionDate"<'2026-11-01'
ORDER BY e."Amount" DESC;
