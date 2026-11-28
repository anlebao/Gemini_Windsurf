SELECT COALESCE(j.m::text, e.m::text) AS month,
       COALESCE(j.rev_j,0), COALESCE(e.rev_e,0), COALESCE(e.off_e,0),
       COALESCE(j.rev_j,0)-COALESCE(e.rev_e,0)-COALESCE(e.off_e,0) AS diff
FROM (SELECT date_trunc('month',j."EntryDate")::date m,
             SUM(CASE WHEN l."AccountNumber"~'^[57]' THEN l."CreditAmount" ELSE 0 END) rev_j
      FROM "JournalEntries" j JOIN "JournalEntryLine" l ON l."JournalEntryId"=j."Id"
      WHERE j."TenantId"='00000000-0000-0000-0000-000000000001'::uuid GROUP BY 1) j
FULL JOIN (SELECT date_trunc('month',e."TransactionDate")::date m,
             SUM(CASE WHEN e."EntryType"=1 AND e."AccountCode"~'^[57]' THEN e."Amount" ELSE 0 END) rev_e,
             SUM(CASE WHEN e."EntryType"=5 AND e."AccountCode"~'^[57]' THEN e."Amount" ELSE 0 END) off_e
      FROM "AccountingEntries" e WHERE e."TenantId"='00000000-0000-0000-0000-000000000001'::uuid GROUP BY 1) e
  ON e.m=j.m ORDER BY 1;
