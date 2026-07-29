.headers on
.mode column
SELECT Id, MissionType, Title, length(Id) as id_len, TenantId FROM Missions WHERE IsDeleted = 0;
SELECT COUNT(*) as mission_count FROM Missions;
SELECT COUNT(*) as customer_count FROM Customers;
