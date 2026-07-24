SELECT "Id", "TenantId", "Status", "CreatedAt"
FROM "Orders"
WHERE "Status" = 'pending'
ORDER BY "CreatedAt" DESC
LIMIT 3;
