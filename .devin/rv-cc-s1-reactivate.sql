-- Re-activate shipper role for RV
UPDATE "CommunityRoles" SET "IsActive" = true, "DeactivatedAt" = NULL, "UpdatedAt" = NOW()
WHERE "Id" = 'bb000002-0000-0000-0000-000000000002';
SELECT 'Active shipper roles' as t, COUNT(*) as n FROM "CommunityRoles" WHERE "Id" = 'bb000002-0000-0000-0000-000000000002' AND "IsActive" = true;
