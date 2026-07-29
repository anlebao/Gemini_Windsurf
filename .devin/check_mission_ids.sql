.headers on
.mode column
SELECT Id, MissionType, Title, length(Id) as id_len, TenantId FROM Missions WHERE IsDeleted = 0;
SELECT Id, FullName FROM Customers WHERE IsDeleted = 0 LIMIT 5;
