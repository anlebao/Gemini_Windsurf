SELECT "Id", "FullName", "PhoneNumber", "IdentityLevel", "TenantId", "CreatedAt"
FROM "Customers"
WHERE "FullName" LIKE '%Ấn%' OR "FullName" LIKE '%Bảo%'
ORDER BY "CreatedAt";
