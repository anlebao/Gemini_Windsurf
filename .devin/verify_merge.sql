.headers on
.mode column
-- Verify merge: 6D1CEB44 should have 36,504 points
SELECT c.Id, c.FullName, lr.PointBalance, lr.History
FROM Customers c
LEFT JOIN LoyaltyRewards lr ON lr.CustomerId = c.Id
WHERE c.Id = '6D1CEB44-49A7-41BF-96D7-DB87D50D5D64';

-- Check no orphaned rewards on deleted customers
SELECT c.Id, c.FullName, c.IsDeleted, lr.PointBalance
FROM Customers c
LEFT JOIN LoyaltyRewards lr ON lr.CustomerId = c.Id
WHERE c.FullName LIKE '%Ấn%';
