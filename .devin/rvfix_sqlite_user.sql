INSERT INTO Users (Id, Username, PasswordHash, DisplayName, Email, Role, IsActive, TenantId, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted)
VALUES ('12BE325F-B8C4-4EDB-AE2D-22055552EF1C', 'xienque2', '$2b$12$T86Tsmif5xP5e9DOYoVNHOq.bUAePYO9pwHkhsKb6HlRJrtREJikW', 'Xiên Que Thạnh Đông', NULL, 1, 1, 'CDBF6EB2-9D9A-47D1-8718-7E9D07721F85', '2026-11-10 06:33:44', '2026-11-10 06:33:44', NULL, NULL, 0);
INSERT INTO UserTenants (Id, UserId, TenantIdValue, Role, AssignedAt, IsActive, TenantId, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted)
VALUES ('7F3A2B1C-9D4E-4F5A-8B6C-1D2E3F4A5B6C', '12BE325F-B8C4-4EDB-AE2D-22055552EF1C', 'CDBF6EB2-9D9A-47D1-8718-7E9D07721F85', 1, '2026-11-10 06:33:44', 1, 'CDBF6EB2-9D9A-47D1-8718-7E9D07721F85', '2026-11-10 06:33:44', '2026-11-10 06:33:44', NULL, NULL, 0);
SELECT Id, Username, DisplayName, Role, IsActive, TenantId FROM Users WHERE Username='xienque2';
