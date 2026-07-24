SELECT u."Email", ut."TenantId"
FROM "Users" u
JOIN "UserTenants" ut ON u."Id" = ut."UserId"
LIMIT 10;
