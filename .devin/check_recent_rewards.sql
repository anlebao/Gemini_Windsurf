.headers on
.mode column
-- LoyaltyRewards created today (from GetOrCreateCustomerRewardsAsync fix)
SELECT lr.Id, lr.CustomerId, lr.PointBalance, lr.CreatedAt, c.FullName, c.PhoneNumber
FROM LoyaltyRewards lr
JOIN Customers c ON c.Id = lr.CustomerId
WHERE lr.CreatedAt > datetime('now', '-3 hours')
ORDER BY lr.CreatedAt DESC;

-- All LoyaltyRewards rows
SELECT lr.Id, lr.CustomerId, lr.PointBalance, lr.CreatedAt, c.FullName
FROM LoyaltyRewards lr
JOIN Customers c ON c.Id = lr.CustomerId
ORDER BY lr.CreatedAt DESC;
