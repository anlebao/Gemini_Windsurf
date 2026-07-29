.headers on
.mode column
-- All customers named "Bảo Ấn Lê" with their points
SELECT c.Id, c.FullName, c.PhoneNumber, c.IdentityLevel, c.TenantId, c.CreatedAt,
       lr.PointBalance, lr.Id as RewardId
FROM Customers c
LEFT JOIN LoyaltyRewards lr ON lr.CustomerId = c.Id
WHERE c.FullName LIKE '%Ấn%' OR c.FullName LIKE '%An Le%' OR c.FullName LIKE '%Bảo%'
ORDER BY c.CreatedAt;

-- All customers with points > 0
SELECT c.Id, c.FullName, c.PhoneNumber, lr.PointBalance
FROM Customers c
JOIN LoyaltyRewards lr ON lr.CustomerId = c.Id
WHERE lr.PointBalance > 0
ORDER BY lr.PointBalance DESC;
