SELECT '=== Order 019f8f52 in SQLite ===' AS section;
SELECT Id, Status, TotalAmount, TenantId, datetime(CreatedAt) AS Created FROM Orders WHERE Id = '019f8f52-85a4-7394-be2a-045f80e46def';

SELECT '=== OrderItems for 019f8f52 ===' AS section;
SELECT Id, OrderId, ProductId, ProductName, Quantity, UnitPrice FROM OrderItems WHERE OrderId = '019f8f52-85a4-7394-be2a-045f80e46def';

SELECT '=== Latest 3 orders ===' AS section;
SELECT Id, Status, TotalAmount, datetime(CreatedAt) AS Created FROM Orders ORDER BY CreatedAt DESC LIMIT 3;
