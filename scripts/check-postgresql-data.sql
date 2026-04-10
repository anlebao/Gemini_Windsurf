-- VanAn Ecosystem - PostgreSQL Data Check Script
-- Use this script to verify data in Central Hub

-- ============================================
-- 1. DATABASE OVERVIEW
-- ============================================

-- Check database size
SELECT 
    pg_database.datname as database_name,
    pg_size_pretty(pg_database_size(pg_database.datname)) as size,
    pg_database_size(pg_database.datname) as size_bytes
FROM pg_database 
WHERE pg_database.datname = 'VanAnCoreHub';

-- Check table counts
SELECT 
    schemaname,
    tablename,
    n_tup_ins as inserts,
    n_tup_upd as updates,
    n_tup_del as deletes,
    n_live_tup as live_rows,
    n_dead_tup as dead_rows
FROM pg_stat_user_tables 
ORDER BY n_live_tup DESC;

-- ============================================
-- 2. TENANT DATA VERIFICATION
-- ============================================

-- Check all tenants
SELECT 
    TenantId,
    COUNT(*) as total_records,
    MIN(CreatedAt) as earliest_record,
    MAX(CreatedAt) as latest_record
FROM "Orders"
WHERE TenantId IS NOT NULL
GROUP BY TenantId
ORDER BY total_records DESC;

-- ============================================
-- 3. BUSINESS DATA ANALYSIS
-- ============================================

-- Order statistics by tenant
SELECT 
    o.TenantId,
    COUNT(*) as total_orders,
    COUNT(CASE WHEN o.Status = 'Completed' THEN 1 END) as completed_orders,
    COUNT(CASE WHEN o.Status = 'Pending' THEN 1 END) as pending_orders,
    SUM(o.TotalAmount) as total_revenue,
    AVG(o.TotalAmount) as avg_order_value,
    MIN(o.TotalAmount) as min_order_value,
    MAX(o.TotalAmount) as max_order_value
FROM "Orders" o
WHERE o.TenantId IS NOT NULL
GROUP BY o.TenantId
ORDER BY total_revenue DESC;

-- Product performance
SELECT 
    p.TenantId,
    p.Name as product_name,
    p.Price,
    COUNT(oi.OrderId) as order_count,
    SUM(oi.Quantity) as total_quantity_sold,
    SUM(oi.TotalPrice) as total_revenue
FROM "Products" p
LEFT JOIN "OrderItems" oi ON p.Id = oi.ProductId
WHERE p.TenantId IS NOT NULL
GROUP BY p.TenantId, p.Id, p.Name, p.Price
ORDER BY total_revenue DESC NULLS LAST
LIMIT 20;

-- ============================================
-- 4. SYNC STATUS VERIFICATION
-- ============================================

-- Check sync status (if LastSyncedAt column exists)
SELECT 
    'Orders' as table_name,
    COUNT(*) as total_records,
    COUNT(CASE WHEN "LastSyncedAt" IS NOT NULL THEN 1 END) as synced_records,
    COUNT(CASE WHEN "LastSyncedAt" IS NULL THEN 1 END) as pending_sync,
    MAX("LastSyncedAt") as last_sync_time
FROM "Orders"
WHERE TenantId IS NOT NULL

UNION ALL

SELECT 
    'Products' as table_name,
    COUNT(*) as total_records,
    COUNT(CASE WHEN "LastSyncedAt" IS NOT NULL THEN 1 END) as synced_records,
    COUNT(CASE WHEN "LastSyncedAt" IS NULL THEN 1 END) as pending_sync,
    MAX("LastSyncedAt") as last_sync_time
FROM "Products"
WHERE TenantId IS NOT NULL;

-- ============================================
-- 5. DATA INTEGRITY CHECKS
-- ============================================

-- Check for orphaned order items
SELECT 
    COUNT(*) as orphaned_items
FROM "OrderItems" oi
LEFT JOIN "Orders" o ON oi.OrderId = o.Id
WHERE o.Id IS NULL;

-- Check for orders without items
SELECT 
    COUNT(*) as empty_orders
FROM "Orders" o
LEFT JOIN "OrderItems" oi ON o.Id = oi.OrderId
WHERE oi.OrderId IS NULL AND o.TenantId IS NOT NULL;

-- Check for negative amounts
SELECT 
    'Orders with negative total' as issue,
    COUNT(*) as count
FROM "Orders" 
WHERE TotalAmount < 0

UNION ALL

SELECT 
    'Products with negative price' as issue,
    COUNT(*) as count
FROM "Products" 
WHERE Price < 0;

-- ============================================
-- 6. RECENT ACTIVITY
-- ============================================

-- Last 10 orders
SELECT 
    o.Id,
    o.TenantId,
    o.Status,
    o.TotalAmount,
    o.CreatedAt,
    o."LastSyncedAt"
FROM "Orders" o
WHERE o.TenantId IS NOT NULL
ORDER BY o.CreatedAt DESC
LIMIT 10;

-- Recent sync activity (last 24 hours)
SELECT 
    'Orders' as table_name,
    COUNT(*) as records_synced,
    MAX("LastSyncedAt") as last_sync
FROM "Orders" 
WHERE "LastSyncedAt" >= NOW() - INTERVAL '24 hours'

UNION ALL

SELECT 
    'Products' as table_name,
    COUNT(*) as records_synced,
    MAX("LastSyncedAt") as last_sync
FROM "Products" 
WHERE "LastSyncedAt" >= NOW() - INTERVAL '24 hours';

-- ============================================
-- 7. PERFORMANCE METRICS
-- ============================================

-- Large tables
SELECT 
    schemaname,
    tablename,
    pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) as size,
    pg_total_relation_size(schemaname||'.'||tablename) as size_bytes,
    n_live_tup as row_count
FROM pg_stat_user_tables 
ORDER BY size_bytes DESC
LIMIT 10;

-- Index usage
SELECT 
    schemaname,
    tablename,
    indexname,
    idx_tup_read as reads,
    idx_tup_fetch as fetches
FROM pg_stat_user_indexes 
ORDER BY reads DESC
LIMIT 10;
