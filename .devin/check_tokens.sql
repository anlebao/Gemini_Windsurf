-- Find customers with their tokens (PhoneNumber column has token-like data?)
SELECT Id, FullName, PhoneNumber, IdentityLevel FROM Customers WHERE PhoneNumber LIKE 'CfDJ%' OR FullName = 'Bảo Ấn Lê';
