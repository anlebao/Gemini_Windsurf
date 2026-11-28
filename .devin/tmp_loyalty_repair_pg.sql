-- Manual repair 2026-10-17: merge 3000 pts from guest stub 59357447 (tenant b9a49ad8)
-- into real login customer 206f88b7 (tenant 0000...01), per MergeDeviceStubsIntoLoginAsync design.
BEGIN;

UPDATE "LoyaltyRewards"
SET "PointBalance" = 3010,
    "History" = '[{"Type":"EARN","Points":10,"Reason":"Hoàn tiền từ chiến dịch Direct Order - Đơn hàng #01a0187f-f0cc-70e2-8602-abe6485bd7df","Timestamp":"2026-08-21T12:57:06.8366975Z","BalanceAfter":10},{"Type":"EARN","Points":3000,"Reason":"Hoàn tiền từ chiến dịch Direct Order - Đơn hàng #01a14898-d9e3-74e0-8fe8-7110aaf0ffad","Timestamp":"2026-10-17T06:46:08.635441Z","BalanceAfter":3000}]',
    "UpdatedAt" = NOW()
WHERE "Id" = 'f78a23cf-ce84-415b-8b74-5c38ff526d0b' AND "PointBalance" = 10;

UPDATE "LoyaltyRewards"
SET "PointBalance" = 0,
    "History" = '[{"Type":"EARN","Points":3000,"Reason":"Hoàn tiền từ chiến dịch Direct Order - Đơn hàng #01a14898-d9e3-74e0-8fe8-7110aaf0ffad","Timestamp":"2026-10-17T06:46:08.635441Z","BalanceAfter":3000},{"Type":"ADJUST","Points":-3000,"Reason":"Merged into login customer 206f88b7-cb91-4ec4-8dcb-f286456efe47 (manual repair 2026-10-17)","Timestamp":"2026-10-17T07:03:58Z","BalanceAfter":0}]',
    "IsDeleted" = true,
    "UpdatedAt" = NOW()
WHERE "Id" = '6e22dd81-9e05-42ba-8416-e65854aa4a67' AND "PointBalance" = 3000;

UPDATE "Customers"
SET "IsDeleted" = true, "UpdatedAt" = NOW()
WHERE "Id" = '59357447-c25b-4edc-a797-5010e55c6b80' AND "IdentityLevel" = 0;

UPDATE "LoyaltyIssuanceRecords"
SET "CustomerId" = '206f88b7-cb91-4ec4-8dcb-f286456efe47', "UpdatedAt" = NOW()
WHERE "Id" = '70ad103d-9acb-4943-bc9f-59e1c40de69f' AND "CustomerId" = '59357447-c25b-4edc-a797-5010e55c6b80';

COMMIT;
