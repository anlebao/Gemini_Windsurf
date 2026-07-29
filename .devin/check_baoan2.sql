SELECT c.Id, c.FullName, lr.PointBalance FROM Customers c LEFT JOIN LoyaltyRewards lr ON lr.CustomerId = c.Id WHERE c.FullName LIKE '%Ấn%';
