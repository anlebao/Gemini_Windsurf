-- VanAn Ecosystem - Database Reset Script
-- Purpose: Clean database and restore to "Clean Seed" state
-- Only keeps sample data for F&B, Beauty, Retail industries

-- PostgreSQL CoreHub Database
-- ----------------------------------------
-- Clean up test orders and transactions
DELETE FROM "Orders" WHERE "OrderId" LIKE 'TEST_%' OR "OrderId" LIKE 'test_%';
DELETE FROM "Orders" WHERE "CreatedAt" < NOW() - INTERVAL '7 days';
DELETE FROM "OrderItems" WHERE "OrderId" NOT IN (SELECT "OrderId" FROM "Orders");

-- Clean up voice command logs older than 30 days (keep recent for audit)
DELETE FROM "VoiceCommands" WHERE "CreatedAt" < NOW() - INTERVAL '30 days';

-- Clean up expired audio files (keep structure)
DELETE FROM "AudioFiles" WHERE "ExpiresAt" < NOW();

-- Reset inventory to initial seed levels
UPDATE "Inventory" SET "CurrentStock" = "ReorderLevel" * 2 WHERE "CurrentStock" > "ReorderLevel" * 2;

-- Keep sample products for 3 industries (F&B, Beauty, Retail)
-- These should be preserved as they are part of the seed data

-- SQLite Local Databases (for satellite apps)
-- ----------------------------------------
-- KhachLink local database
DELETE FROM "LocalOrders" WHERE "OrderId" LIKE 'TEST_%' OR "OrderId" LIKE 'test_%';
DELETE FROM "LocalOrders" WHERE "CreatedAt" < datetime('now', '-7 days');
DELETE FROM "Outbox" WHERE "ProcessedAt" IS NOT NULL AND "ProcessedAt" < datetime('now', '-1 day');

-- ShopERP local database
DELETE FROM "LocalOrders" WHERE "OrderId" LIKE 'TEST_%' OR "OrderId" LIKE 'test_%';
DELETE FROM "LocalOrders" WHERE "CreatedAt" < datetime('now', '-7 days');
DELETE FROM "Outbox" WHERE "ProcessedAt" IS NOT NULL AND "ProcessedAt" < datetime('now', '-1 day');

-- Reset sequences for clean start
ALTER SEQUENCE orders_orderid_seq RESTART WITH 1000;
ALTER SEQUENCE voicecommands_id_seq RESTART WITH 1000;
ALTER SEQUENCE audiofiles_id_seq RESTART WITH 1000;

-- Vacuum and optimize
VACUUM ANALYZE;

-- Log reset completion
INSERT INTO "SystemLogs" ("LogType", "Message", "CreatedAt", "Severity") 
VALUES ('SYSTEM_RESET', 'Database reset to clean seed state - completed', NOW(), 'INFO');

COMMIT;

-- Verification queries
SELECT 'Orders count after reset: ' || COUNT(*) FROM "Orders";
SELECT 'Products count (should be preserved): ' || COUNT(*) FROM "Products";
SELECT 'VoiceCommands count (last 30 days): ' || COUNT(*) FROM "VoiceCommands" WHERE "CreatedAt" >= NOW() - INTERVAL '30 days';
SELECT 'AudioFiles count (active): ' || COUNT(*) FROM "AudioFiles" WHERE "ExpiresAt" >= NOW();
