SELECT u."Email", u."Id", ut."TenantId"
FROM "Users" u
JOIN "UserTenants" ut ON u."Id" = ut."UserId"
WHERE ut."TenantId" = '00000000-0000-0000-0000-000000000001'
LIMIT 5;
