SELECT c."Id", c."FullName", c."PhoneNumber", lr."PointBalance", c."LastOrderDate", c."TotalSpent"
FROM "Customers" c
LEFT JOIN "LoyaltyRewards" lr ON lr."CustomerId" = c."Id"
WHERE c."IsDeleted" = false
ORDER BY c."CreatedAt" DESC LIMIT 10;
