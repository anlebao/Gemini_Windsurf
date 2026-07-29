-- Merge duplicate "Bảo Ấn Lê" customers
-- Source: 6A8DE489 (36,504 points + orders) → Target: 6D1CEB44 (logged-in customer, 0 points)
-- Also clean up 76056D36 (3rd duplicate, 0 points, corrupted phone=token)

-- 1. Transfer LoyaltyRewards from 6A8DE489 → 6D1CEB44
--    First delete the empty 0-point rewards row on 6D1CEB44 (created by GetOrCreate fix)
DELETE FROM LoyaltyRewards WHERE CustomerId = '6D1CEB44-49A7-41BF-96D7-DB87D50D5D64';

--    Then reassign the 36,504-point rewards row to 6D1CEB44
UPDATE LoyaltyRewards
SET CustomerId = '6D1CEB44-49A7-41BF-96D7-DB87D50D5D64'
WHERE CustomerId = '6A8DE489-3D26-48F5-A4B3-51343FA350AD';

-- 2. Transfer Orders from 6A8DE489 → 6D1CEB44
UPDATE Orders
SET CustomerId = '6D1CEB44-49A7-41BF-96D7-DB87D50D5D64'
WHERE CustomerId = '6A8DE489-3D26-48F5-A4B3-51343FA350AD';

-- 3. Transfer MissionCompletions from 6A8DE489 → 6D1CEB44
UPDATE MissionCompletions
SET CustomerId = '6D1CEB44-49A7-41BF-96D7-DB87D50D5D64'
WHERE CustomerId = '6A8DE489-3D26-48F5-A4B3-51343FA350AD';

-- 4. Transfer any PushNotificationDeliveries
UPDATE PushNotificationDeliveries
SET CustomerId = '6D1CEB44-49A7-41BF-96D7-DB87D50D5D64'
WHERE CustomerId = '6A8DE489-3D26-48F5-A4B3-51343FA350AD';

-- 5. Transfer any Vouchers
UPDATE Vouchers
SET CustomerId = '6D1CEB44-49A7-41BF-96D7-DB87D50D5D64'
WHERE CustomerId = '6A8DE489-3D26-48F5-A4B3-51343FA350AD';

-- 6. Transfer any RedemptionRecords
UPDATE RedemptionRecords
SET CustomerId = '6D1CEB44-49A7-41BF-96D7-DB87D50D5D64'
WHERE CustomerId = '6A8DE489-3D26-48F5-A4B3-51343FA350AD';

-- 7. Update customer stats on 6D1CEB44 (TotalSpent + LastOrderDate from orders)
UPDATE Customers
SET TotalSpent = (
    SELECT COALESCE(SUM(TotalAmount), 0) FROM Orders
    WHERE CustomerId = '6D1CEB44-49A7-41BF-96D7-DB87D50D5D64'
),
LastOrderDate = (
    SELECT MAX(OrderDate) FROM Orders
    WHERE CustomerId = '6D1CEB44-49A7-41BF-96D7-DB87D50D5D64'
)
WHERE Id = '6D1CEB44-49A7-41BF-96D7-DB87D50D5D64';

-- 8. Soft delete the duplicate customers
UPDATE Customers SET IsDeleted = 1 WHERE Id = '6A8DE489-3D26-48F5-A4B3-51343FA350AD';
UPDATE Customers SET IsDeleted = 1 WHERE Id = '76056D36-F3F6-4844-9562-328EE58E4B8E';

-- Verify
SELECT '=== Merged customer 6D1CEB44 ===' as info;
SELECT c.Id, c.FullName, c.TotalSpent, c.LastOrderDate, lr.PointBalance
FROM Customers c
LEFT JOIN LoyaltyRewards lr ON lr.CustomerId = c.Id
WHERE c.Id = '6D1CEB44-49A7-41BF-96D7-DB87D50D5D64';

SELECT '=== Orders count ===' as info;
SELECT CustomerId, COUNT(*) as order_count FROM Orders WHERE CustomerId = '6D1CEB44-49A7-41BF-96D7-DB87D50D5D64' GROUP BY CustomerId;

SELECT '=== Deleted duplicates ===' as info;
SELECT Id, FullName, IsDeleted FROM Customers WHERE FullName LIKE '%Ấn%';
