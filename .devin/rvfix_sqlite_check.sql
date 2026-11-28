SELECT Id, Username, DisplayName, Role, IsActive, TenantId FROM Users WHERE Id='12BE325F-B8C4-4EDB-AE2D-22055552EF1C' OR lower(Username)='sysadmin@vanan.vn';
SELECT UserId, TenantIdValue, Role, IsActive FROM UserTenants WHERE UserId='12BE325F-B8C4-4EDB-AE2D-22055552EF1C';
