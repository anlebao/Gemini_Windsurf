-- RV Batch 1 cleanup — xóa test tenant "Quán Cà Phê RV <ts>" + user + groups + crawl source
-- Run: docker exec -i vanan-postgres psql -U vanan_admin -d VanAnCoreHub < rv_batch1_cleanup.sql
-- Biến: thay <TID> bằng tenant id output từ rv_batch1_176_181.sh

\set tid '<TID>'

-- Xem trước những gì sẽ xóa
SELECT 'Tenants' AS tbl, COUNT(*) FROM "Tenants" WHERE "Id" = :'tid'
UNION ALL SELECT 'CrawlSources', COUNT(*) FROM "CrawlSources" WHERE "TenantId" = :'tid'
UNION ALL SELECT 'Users', COUNT(*) FROM "Users" WHERE "TenantId" = :'tid'
UNION ALL SELECT 'PermissionGroups', COUNT(*) FROM "PermissionGroups" WHERE "TenantId" = :'tid';

-- Xóa theo thứ tự FK
DELETE FROM "CrawlSources" WHERE "TenantId" = :'tid';
DELETE FROM "UserPermissionGroups" WHERE "PermissionGroupId" IN (SELECT "Id" FROM "PermissionGroups" WHERE "TenantId" = :'tid');
DELETE FROM "PermissionGroups" WHERE "TenantId" = :'tid';
DELETE FROM "Users" WHERE "TenantId" = :'tid';
DELETE FROM "Tenants" WHERE "Id" = :'tid';

-- Verify sạch
SELECT 'Tenants còn' AS tbl, COUNT(*) FROM "Tenants" WHERE "Id" = :'tid'
UNION ALL SELECT 'CrawlSources còn', COUNT(*) FROM "CrawlSources" WHERE "TenantId" = :'tid'
UNION ALL SELECT 'Users còn', COUNT(*) FROM "Users" WHERE "TenantId" = :'tid';
