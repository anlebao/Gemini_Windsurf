SELECT Id, FullName, PhoneNumber, IdentityLevel, CreatedAt, IsDeleted FROM Customers ORDER BY CreatedAt DESC LIMIT 10;
.mode column
.headers on
SELECT COUNT(*) AS customer_count FROM Customers;
SELECT COUNT(*) AS loyalty_count FROM LoyaltyRewards;
SELECT * FROM LoyaltyRewards LIMIT 5;
