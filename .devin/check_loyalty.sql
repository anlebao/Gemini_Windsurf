-- 1. Customer details + loyalty rewards
SELECT c."Id", c."FullName", c."PhoneNumber", c."IdentityLevel",
       c."CreatedAt", c."LastOrderDate", c."TotalSpent",
       lr."PointBalance", lr."History"
FROM "Customers" c
LEFT JOIN "LoyaltyRewards" lr ON lr."CustomerId" = c."Id"
WHERE c."IsDeleted" = false
ORDER BY c."CreatedAt" DESC LIMIT 5;

-- 2. Mission completions for this customer
SELECT mc."Id", mc."CustomerId", mc."MissionType", mc."PointsAwarded",
       mc."CompletedAt", mc."Success"
FROM "MissionCompletions" mc
ORDER BY mc."CompletedAt" DESC LIMIT 10;

-- 3. Mission definitions configured per tenant
SELECT m."Id", m."TenantId", m."MissionType", m."PointsReward",
       m."IsActive", m."Title"
FROM "Missions" m
LIMIT 20;

-- 4. Count customers + loyalty rewards
SELECT
  (SELECT COUNT(*) FROM "Customers" WHERE "IsDeleted" = false) AS total_customers,
  (SELECT COUNT(*) FROM "LoyaltyRewards") AS total_loyalty_rows;
