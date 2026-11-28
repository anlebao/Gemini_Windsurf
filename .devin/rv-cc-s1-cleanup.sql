-- Cleanup RV test orders
UPDATE "Orders" SET "Status" = 'cancelled', "UpdatedAt" = NOW()
WHERE "Id" IN ('01a0a426-7328-7535-9704-e408420d1263', '01a0a427-b2a5-75a6-ab0b-b1871ba2f199');
SELECT 'Cleaned orders' as t, COUNT(*) as n FROM "Orders" WHERE "Id" IN ('01a0a426-7328-7535-9704-e408420d1263', '01a0a427-b2a5-75a6-ab0b-b1871ba2f199') AND "Status" = 'cancelled';
