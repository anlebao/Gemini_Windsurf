UPDATE Users SET Username='xienque2', PasswordHash='$2b$12$T86Tsmif5xP5e9DOYoVNHOq.bUAePYO9pwHkhsKb6HlRJrtREJikW', UpdatedAt='2026-11-10 08:00:00' WHERE Id='12BE325F-B8C4-4EDB-AE2D-22055552EF1C';
SELECT Id, Username, Role, IsActive, TenantId FROM Users WHERE Id='12BE325F-B8C4-4EDB-AE2D-22055552EF1C';
SELECT Id, Username FROM Users WHERE lower(Username)='sysadmin@vanan.vn';
