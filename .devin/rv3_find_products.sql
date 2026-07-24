SELECT DISTINCT p.TenantId, COUNT(*) as product_count FROM Products p WHERE p.IsActive = 1 GROUP BY p.TenantId ORDER BY product_count DESC LIMIT 5;
