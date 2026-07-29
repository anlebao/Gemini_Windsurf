.headers on
.mode column
-- Verify loyalty settings
SELECT TenantId, Loyalty_PointsRate, Loyalty_MinPointsPerOrder FROM ShopFeatureSettings WHERE TenantId = '00000000-0000-0000-0000-000000000001';
-- Verify Bảo Ấn Lê points
SELECT c.Id, c.FullName, lr.PointBalance FROM Customers c JOIN LoyaltyRewards lr ON lr.CustomerId = c.Id WHERE c.Id = '6D1CEB44-49A7-41BF-96D7-DB87D50D5D64';
