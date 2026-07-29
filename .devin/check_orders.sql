-- Check Orders table schema
SELECT sql FROM sqlite_master WHERE name = 'Orders';
-- Check orders for 6A8DE489 (before soft delete) and 6D1CEB44
SELECT Id, CustomerId, Status, TotalAmount, OrderDate FROM Orders WHERE CustomerId LIKE '%6A8DE%' OR CustomerId LIKE '%6D1CE%' LIMIT 10;
-- Count all orders
SELECT COUNT(*) as total_orders FROM Orders;
-- Check if orders have CustomerId at all
SELECT CustomerId, COUNT(*) as cnt FROM Orders GROUP BY CustomerId LIMIT 10;
