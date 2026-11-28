\echo '=== bookings OrderId ==='
SELECT "Id", "PublicBookingCode", "OrderId", "Status", "PaymentStatus" FROM "Bookings"
WHERE "Id" IN ('fc24ec08-a6ea-4092-a91e-8add1c30bf67', '5926dcbd-4178-4bc3-b283-d1a3900e3f9e');
\echo '=== orders by tracking code ==='
SELECT "Id", "TrackingCode", "SourceType", "Status", "PaymentStatus", "DepositPaidAmount", "TotalAmount" FROM "Orders"
WHERE "TrackingCode" IN ('3E362E1A31534650', '4E5C970E2A1B45B5');