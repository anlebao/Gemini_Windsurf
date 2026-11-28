\echo '=== distinct account PREFIXES trong JournalEntryLine (toàn bộ lịch sử) ==='
SELECT left(l."AccountNumber", 1) AS prefix, count(*) AS lines, count(DISTINCT l."AccountNumber") AS accounts
FROM "JournalEntryLine" l
GROUP BY left(l."AccountNumber", 1)
ORDER BY prefix;

\echo '=== account ngoài prefix 1-8 (9xx, 0xx, malformed...) ==='
SELECT DISTINCT l."AccountNumber", count(*) AS lines
FROM "JournalEntryLine" l
WHERE l."AccountNumber" !~ '^[1-8]'
GROUP BY l."AccountNumber" ORDER BY l."AccountNumber";

\echo '=== journal mẫu có TK 911 (kết chuyển) nếu có ==='
SELECT j."ReferenceType", j."Description", j."EntryDate"::date,
       (SELECT string_agg(l."AccountNumber"||':'||l."DebitAmount"||'/'||l."CreditAmount", ', ') FROM "JournalEntryLine" l WHERE l."JournalEntryId"=j."Id") AS lines
FROM "JournalEntries" j
WHERE EXISTS (SELECT 1 FROM "JournalEntryLine" l WHERE l."JournalEntryId"=j."Id" AND l."AccountNumber" LIKE '9%')
ORDER BY j."EntryDate" DESC LIMIT 5;