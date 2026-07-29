-- Add 200 points to Fresh Test 3 (1A43AE1F) so they can redeem "Ca phe mien phi" (200 pts)
UPDATE LoyaltyRewards
SET PointBalance = 300,
    History = '[{"Type":"EARN","Points":100,"Reason":"Mission: Xác thực OTP nhận điểm thưởng","Timestamp":"2026-07-28T18:07:32.2791456Z","BalanceAfter":100},{"Type":"ADMIN","Points":200,"Reason":"Test points for redemption verification","Timestamp":"2026-07-28T19:00:00Z","BalanceAfter":300}]'
WHERE CustomerId = '1A43AE1F-0588-4A79-A018-2BFF107451ED';
SELECT CustomerId, PointBalance FROM LoyaltyRewards WHERE CustomerId = '1A43AE1F-0588-4A79-A018-2BFF107451ED';
