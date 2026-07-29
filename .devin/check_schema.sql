.headers on
-- Show MissionCompletions schema + FKs
SELECT sql FROM sqlite_master WHERE name = 'MissionCompletions';
-- Show all FKs on MissionCompletions
PRAGMA foreign_key_list(MissionCompletions);
-- Check if Tenants table has the tenant
SELECT Id FROM Tenants WHERE Id = '00000000-0000-0000-0000-000000000001';
-- Check the new customer that was just created
SELECT Id, FullName, PhoneNumber, TenantId, IdentityLevel FROM Customers WHERE PhoneNumber = '0900000077' OR PhoneNumber = '0900000099';
