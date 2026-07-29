.headers on
.mode column
-- Customer tokens table
SELECT name FROM sqlite_master WHERE type='table' AND name LIKE '%Token%' OR name LIKE '%Customer%';
-- All customer phone numbers (real phones, not tokens)
SELECT Id, FullName, PhoneNumber FROM Customers WHERE PhoneNumber NOT LIKE 'CfDJ%' AND PhoneNumber != '' AND PhoneNumber IS NOT NULL;
