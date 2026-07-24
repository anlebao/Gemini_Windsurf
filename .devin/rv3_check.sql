SELECT '=== Products with Id=00000000 ===' AS section;
SELECT Id, Name, Price, VatRate, TenantId FROM Products WHERE Id = '00000000-0000-0000-0000-000000000000' LIMIT 5;

SELECT '=== Order 019f8f50 in SQLite ===' AS section;
SELECT Id, Status, TotalAmount, TenantId FROM Orders WHERE Id = '019f8f50-b4cc-73b5-8cab-be5e7308e414';

SELECT '=== Latest 5 orders in SQLite ===' AS section;
SELECT Id, Status, TotalAmount, datetime(CreatedAt) AS Created FROM Orders ORDER BY CreatedAt DESC LIMIT 5;

SELECT '=== OrderItems for latest order ===' AS section;
SELECT Id, OrderId, ProductId, ProductName, Quantity, UnitPrice FROM OrderItems ORDER BY rowid DESC LIMIT 5;
