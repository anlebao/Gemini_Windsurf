-- VanAn Ecosystem - SQLite Data Check Script
-- Use this script to verify data in Edge Nodes (KhachLink, ShopERP)

-- ============================================
-- 1. DATABASE OVERVIEW
-- ============================================

-- Check database info
SELECT 
    'Database Info' as metric,
    'Size' as item,
    page_count * page_size as size_bytes,
    'Bytes' as unit
FROM pragma_page_count(), pragma_page_size()

UNION ALL

SELECT 
    'Database Info' as metric,
    'Page Count' as item,
    pragma_page_count() as value,
    'Pages' as unit

UNION ALL

SELECT 
    'Database Info' as metric,
    'Page Size' as item,
    pragma_page_size() as value,
    'Bytes' as unit;

-- Check all tables
SELECT 
    name as table_name,
    sql as create_statement
FROM sqlite_master 
WHERE type = 'table' AND name NOT LIKE 'sqlite_%'
ORDER BY name;

-- ============================================
-- 2. TABLE RECORD COUNTS
-- ============================================

SELECT 
    'Orders' as table_name,
    COUNT(*) as record_count,
    MIN(CreatedAt) as earliest_record,
    MAX(CreatedAt) as latest_record
FROM Orders

UNION ALL

SELECT 
    'Products' as table_name,
    COUNT(*) as record_count,
    MIN(CreatedAt) as earliest_record,
    MAX(CreatedAt) as latest_record
FROM Products

UNION ALL

SELECT 
    'OrderItems' as table_name,
    COUNT(*) as record_count,
    NULL as earliest_record,
    NULL as latest_record
FROM OrderItems

UNION ALL

SELECT 
    'Customers' as table_name,
    COUNT(*) as record_count,
    MIN(CreatedAt) as earliest_record,
    MAX(CreatedAt) as latest_record
FROM Customers;

-- ============================================
-- 3. BUSINESS DATA ANALYSIS
-- ============================================

-- Order statistics
SELECT 
    COUNT(*) as total_orders,
    COUNT(CASE WHEN Status = 'Completed' THEN 1 END) as completed_orders,
    COUNT(CASE WHEN Status = 'Pending' THEN 1 END) as pending_orders,
    COUNT(CASE WHEN Status = 'Cancelled' THEN 1 END) as cancelled_orders,
    SUM(TotalAmount) as total_revenue,
    AVG(TotalAmount) as avg_order_value,
    MIN(TotalAmount) as min_order_value,
    MAX(TotalAmount) as max_order_value
FROM Orders;

-- Product performance
SELECT 
    p.Name as product_name,
    p.Price,
    COUNT(oi.OrderId) as order_count,
    SUM(oi.Quantity) as total_quantity_sold,
    SUM(oi.TotalPrice) as total_revenue
FROM Products p
LEFT JOIN OrderItems oi ON p.Id = oi.ProductId
GROUP BY p.Id, p.Name, p.Price
ORDER BY total_revenue DESC;

-- Recent orders (last 10)
SELECT 
    o.Id,
    o.CustomerId,
    o.Status,
    o.TotalAmount,
    o.CreatedAt,
    o.LastSyncedAt
FROM Orders o
ORDER BY o.CreatedAt DESC
LIMIT 10;

-- ============================================
-- 4. SYNC STATUS VERIFICATION
-- ============================================

-- Check sync status
SELECT 
    'Orders' as table_name,
    COUNT(*) as total_records,
    COUNT(CASE WHEN LastSyncedAt IS NOT NULL THEN 1 END) as synced_records,
    COUNT(CASE WHEN LastSyncedAt IS NULL THEN 1 END) as pending_sync,
    MAX(LastSyncedAt) as last_sync_time,
    (COUNT(CASE WHEN LastSyncedAt IS NOT NULL THEN 1 END) * 100.0 / COUNT(*)) as sync_percentage
FROM Orders

UNION ALL

SELECT 
    'Products' as table_name,
    COUNT(*) as total_records,
    COUNT(CASE WHEN LastSyncedAt IS NOT NULL THEN 1 END) as synced_records,
    COUNT(CASE WHEN LastSyncedAt IS NULL THEN 1 END) as pending_sync,
    MAX(LastSyncedAt) as last_sync_time,
    (COUNT(CASE WHEN LastSyncedAt IS NOT NULL THEN 1 END) * 100.0 / COUNT(*)) as sync_percentage
FROM Products;

-- Records needing sync (last 24 hours)
SELECT 
    'Orders' as table_name,
    COUNT(*) as records_to_sync,
    MIN(CreatedAt) as oldest_pending,
    MAX(CreatedAt) as newest_pending
FROM Orders 
WHERE LastSyncedAt IS NULL
   OR LastSyncedAt < CreatedAt

UNION ALL

SELECT 
    'Products' as table_name,
    COUNT(*) as records_to_sync,
    MIN(CreatedAt) as oldest_pending,
    MAX(CreatedAt) as newest_pending
FROM Products 
WHERE LastSyncedAt IS NULL
   OR LastSyncedAt < CreatedAt;

-- ============================================
-- 5. DATA INTEGRITY CHECKS
-- ============================================

-- Check for orphaned order items
SELECT 
    COUNT(*) as orphaned_items
FROM OrderItems oi
LEFT JOIN Orders o ON oi.OrderId = o.Id
WHERE o.Id IS NULL;

-- Check for orders without items
SELECT 
    COUNT(*) as empty_orders
FROM Orders o
LEFT JOIN OrderItems oi ON o.Id = oi.OrderId
WHERE oi.OrderId IS NULL;

-- Check for missing customer references
SELECT 
    COUNT(*) as orders_without_customers
FROM Orders o
LEFT JOIN Customers c ON o.CustomerId = c.Id
WHERE c.Id IS NULL;

-- Check for negative amounts
SELECT 
    'Orders with negative total' as issue,
    COUNT(*) as count
FROM Orders 
WHERE TotalAmount < 0

UNION ALL

SELECT 
    'Products with negative price' as issue,
    COUNT(*) as count
FROM Products 
WHERE Price < 0;

-- ============================================
-- 6. WAL MODE CHECK
-- ============================================

-- Check WAL mode status
SELECT 
    name as setting,
    value as current_value
FROM pragma_journal_mode()

UNION ALL

SELECT 
    name as setting,
    value as current_value
FROM pragma_synchronous_mode();

-- ============================================
-- 7. INDEX INFORMATION
-- ============================================

-- Check indexes
SELECT 
    name as index_name,
    tbl_name as table_name,
    sql as create_statement
FROM sqlite_master 
WHERE type = 'index' AND name NOT LIKE 'sqlite_%'
ORDER BY tbl_name, name;

-- ============================================
-- 8. RECENT ACTIVITY (Last 24 Hours)
-- ============================================

-- Recent orders (last 24 hours)
SELECT 
    COUNT(*) as orders_last_24h,
    SUM(TotalAmount) as revenue_last_24h,
    AVG(TotalAmount) as avg_order_value_last_24h
FROM Orders 
WHERE CreatedAt >= datetime('now', '-1 day');

-- Recent sync activity
SELECT 
    'Orders' as table_name,
    COUNT(*) as records_synced_last_24h,
    MAX(LastSyncedAt) as last_sync_time
FROM Orders 
WHERE LastSyncedAt >= datetime('now', '-1 day')

UNION ALL

SELECT 
    'Products' as table_name,
    COUNT(*) as records_synced_last_24h,
    MAX(LastSyncedAt) as last_sync_time
FROM Products 
WHERE LastSyncedAt >= datetime('now', '-1 day');
