-- All customers (including deleted) to see both IDs
SELECT c."Id", c."FullName", c."PhoneNumber", c."IdentityLevel",
       c."CreatedAt", c."IsDeleted", c."TenantId"
FROM "Customers" c
ORDER BY c."CreatedAt" DESC;

-- LoyaltyRewards rows
SELECT lr."Id", lr."CustomerId", lr."PointBalance", lr."TenantId"
FROM "LoyaltyRewards" lr;

-- MissionCompletions — check actual column names
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_name = 'MissionCompletions'
ORDER BY ordinal_position;
