SELECT u."Email", ut."TenantId"
FROM "Users" u
JOIN "UserTenants" ut ON u."Id" = ut."UserId"
WHERE ut."TenantId" = 'eb7f9261-0751-4ff9-b0b2-b3698949cc80'
LIMIT 5;
