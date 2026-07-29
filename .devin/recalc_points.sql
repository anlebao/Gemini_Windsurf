-- Recalculate Bảo Ấn Lê's points with new rate (0.001 = 1 point / 1000 VND)
-- Rebuild LoyaltyRewards history + PointBalance

UPDATE LoyaltyRewards
SET PointBalance = 364,
    History = '[{"Type":"EARN","Points":38,"Reason":"Hoàn tiền từ chiến dịch Direct Order - Đơn hàng #019fa299-dedc-75c5-8051-35ee27a39d70","Timestamp":"2026-07-27T08:05:44.2166178Z","BalanceAfter":38},{"Type":"EARN","Points":55,"Reason":"Hoàn tiền từ chiến dịch Direct Order - Đơn hàng #019fa684-3c13-72df-b479-34527990b256","Timestamp":"2026-07-28T02:19:16.1807641Z","BalanceAfter":93},{"Type":"EARN","Points":74,"Reason":"Hoàn tiền từ chiến dịch Direct Order - Đơn hàng #019fa91a-ede1-7633-8806-96dc79cedf7c","Timestamp":"2026-07-28T14:22:40.7298801Z","BalanceAfter":167},{"Type":"EARN","Points":44,"Reason":"Hoàn tiền từ chiến dịch Direct Order - Đơn hàng #019fa97f-2ec3-717c-ae5a-f54141e03dd6","Timestamp":"2026-07-28T16:13:49.2431665Z","BalanceAfter":211},{"Type":"EARN","Points":93,"Reason":"Hoàn tiền từ chiến dịch Direct Order - Đơn hàng #019fa9e7-e111-778c-a3b9-5f21ddfe57ed","Timestamp":"2026-07-28T18:09:12.6408254Z","BalanceAfter":304},{"Type":"EARN","Points":33,"Reason":"Hoàn tiền từ chiến dịch Direct Order - Đơn hàng #019fa9ec-82f6-71b4-a75b-298613949cb2","Timestamp":"2026-07-28T18:12:02.9136537Z","BalanceAfter":337},{"Type":"EARN","Points":27,"Reason":"Hoàn tiền từ chiến dịch Direct Order - Đơn hàng #019fa9f0-4411-723d-888d-ba88d3b3a3b0","Timestamp":"2026-07-28T18:16:08.883463Z","BalanceAfter":364}]'
WHERE CustomerId = '6D1CEB44-49A7-41BF-96D7-DB87D50D5D64';

-- Verify
SELECT CustomerId, PointBalance FROM LoyaltyRewards WHERE CustomerId = '6D1CEB44-49A7-41BF-96D7-DB87D50D5D64';
