.headers on
.mode column
-- Check PointBalance column type in LoyaltyRewards
SELECT sql FROM sqlite_master WHERE name = 'LoyaltyRewards';

-- Check the 7 EARN entries with order IDs
SELECT json_extract(value, '$.Points') as points,
       json_extract(value, '$.Reason') as reason,
       json_extract(value, '$.BalanceAfter') as balance_after
FROM LoyaltyRewards, json_each(LoyaltyRewards.History)
WHERE CustomerId = '6D1CEB44-49A7-41BF-96D7-DB87D50D5D64';

-- Extract order IDs from reasons and check their TotalAmount
SELECT
  json_extract(value, '$.Points') as points_awarded,
  REPLACE(REPLACE(json_extract(value, '$.Reason'), 'Hoàn tiền từ chiến dịch Direct Order - Đơn hàng #', ''), '#', '') as order_id_str,
  json_extract(value, '$.BalanceAfter') as balance_after
FROM LoyaltyRewards, json_each(LoyaltyRewards.History)
WHERE CustomerId = '6D1CEB44-49A7-41BF-96D7-DB87D50D5D64';
