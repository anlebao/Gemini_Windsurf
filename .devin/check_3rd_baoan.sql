.headers on
.mode column
SELECT c.Id, c.FullName, c.PhoneNumber, c.IdentityLevel, c.TenantId, c.CreatedAt, c.IsDeleted,
       lr.PointBalance, lr.Id as RewardId
FROM Customers c
LEFT JOIN LoyaltyRewards lr ON lr.CustomerId = c.Id
WHERE c.FullName LIKE '%Ấn%'
ORDER BY c.CreatedAt;

-- Check orders for each Bảo Ấn Lê
SELECT o.CustomerId, c.FullName, COUNT(*) as OrderCount, SUM(o.TotalAmount) as TotalSpent
FROM Orders o JOIN Customers c ON c.Id = o.CustomerId
WHERE c.FullName LIKE '%Ấn%'
GROUP BY o.CustomerId, c.FullName;
