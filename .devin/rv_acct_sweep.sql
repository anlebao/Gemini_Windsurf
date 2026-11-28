\echo '=== A. ALL Order-ref journals per tenant (legacy direct-path) ==='
SELECT t."Name", t."Type", j."TenantId", count(*) AS order_ref_journals,
       min(j."EntryDate")::date AS first, max(j."EntryDate")::date AS last
FROM "JournalEntries" j
JOIN "Tenants" t ON t."Id" = j."TenantId"
WHERE j."ReferenceType" = 'Order'
GROUP BY t."Name", t."Type", j."TenantId"
ORDER BY count(*) DESC;

\echo '=== B. DUPLICATE detection: Order-ref journal có bridge (ManualEntry) journal cùng order? ==='
-- Rule: order paid trong thời kỳ bridge (P2 #4, từ 2026-10-16) → vừa có journal trực tiếp (ref Order)
-- vừa có journal bridge (ref ManualEntry, ReferenceId = AccountingEntry.Id của order) → ref 'Order' = duplicate.
\echo '--- B1: count theo tenant ---'
SELECT t."Name", count(*) AS duplicate_order_ref_journals
FROM "JournalEntries" j
JOIN "Tenants" t ON t."Id" = j."TenantId"
WHERE j."ReferenceType" = 'Order'
  AND EXISTS (
    SELECT 1 FROM "JournalEntries" j2
    WHERE j2."ReferenceType" = 'ManualEntry'
      AND j2."ReferenceId" IN (SELECT "Id" FROM "AccountingEntries" e WHERE e."Reference" = j."ReferenceId"::text)
  )
GROUP BY t."Name" ORDER BY count(*) DESC;

\echo '--- B2: legit Order-ref journals (KHÔNG có bridge cùng order — pre-bridge era, GIỮ) ---'
SELECT t."Name", count(*) AS legit_order_ref_journals, min(j."EntryDate")::date AS first, max(j."EntryDate")::date AS last
FROM "JournalEntries" j
JOIN "Tenants" t ON t."Id" = j."TenantId"
WHERE j."ReferenceType" = 'Order'
  AND NOT EXISTS (
    SELECT 1 FROM "JournalEntries" j2
    WHERE j2."ReferenceType" = 'ManualEntry'
      AND j2."ReferenceId" IN (SELECT "Id" FROM "AccountingEntries" e WHERE e."Reference" = j."ReferenceId"::text)
  )
GROUP BY t."Name" ORDER BY count(*) DESC;

\echo '=== C. tổng hợp: journals ref Order theo EntryDate (bridge era từ 2026-10-16?) ==='
SELECT (j."EntryDate" >= '2026-10-16'::date) AS after_bridge_deploy, count(*)
FROM "JournalEntries" j
WHERE j."ReferenceType" = 'Order'
GROUP BY 1 ORDER BY 1;