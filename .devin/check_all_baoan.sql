.headers on
.mode column
-- ALL customers named Bảo Ấn Lê (including deleted)
SELECT c.Id, c.FullName, c.PhoneNumber, c.IdentityLevel, c.TenantId, c.CreatedAt, c.IsDeleted,
       lr.PointBalance
FROM Customers c
LEFT JOIN LoyaltyRewards lr ON lr.CustomerId = c.Id
WHERE c.FullName LIKE '%Ấn%' OR c.FullName LIKE '%Bảo%'
ORDER BY c.CreatedAt;

-- Orders for customer 6A8DE489 (the one with points)
SELECT Id, CustomerId, Status, TotalAmount, CreatedAt FROM Orders WHERE CustomerId = '6A8DE489-3D26-48F5-A4B3-51343FA350AD' ORDER BY CreatedAt DESC LIMIT 10;

-- Any customers created in last 2 hours (from my OTP tests)?
SELECT Id, FullName, PhoneNumber, TenantId, CreatedAt FROM Customers WHERE CreatedAt > datetime('now', '-2 hours') ORDER BY CreatedAt DESC;
