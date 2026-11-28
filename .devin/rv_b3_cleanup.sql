-- RV Batch 3 cleanup — remove ALL test fixture rows (3 runs)
-- Shipper: 6e4edec9-543c-4f10-9ef1-4b8efe894b97, Tenant test: 0dfab177-86ad-4513-bd69-ddf956c7b768
BEGIN;
CREATE TEMP TABLE rv_orders AS
SELECT "Id" FROM "Orders" WHERE "TrackingCode" IN ('RVB3MKT','RVB3RES')
   OR "CustomerInfo_FullName" LIKE 'RV B3%';
SELECT COUNT(*) AS fixture_orders FROM rv_orders;

DELETE FROM "WalletTransactions"
 WHERE "RelatedOrderId" IN (SELECT "Id" FROM rv_orders)
    OR ("OwnerId"='6e4edec9-543c-4f10-9ef1-4b8efe894b97' AND "Description" LIKE 'RV B3%')
    OR ("OwnerId"='6e4edec9-543c-4f10-9ef1-4b8efe894b97' AND "Type"=4 AND "Description" LIKE 'Withdrawal payout%');

DELETE FROM "WithdrawalRequests" WHERE "OwnerId"='6e4edec9-543c-4f10-9ef1-4b8efe894b97';
DELETE FROM "AccountingEntries" WHERE "CorrelationId" IN (SELECT "Id" FROM rv_orders);
DELETE FROM "JournalEntries" WHERE "ReferenceId" IN (SELECT "Id" FROM rv_orders);
DELETE FROM "OutboxMessages" WHERE "CorrelationId" IN (SELECT "Id" FROM rv_orders);
DELETE FROM "DeliveryTasks" WHERE "OrderId" IN (SELECT "Id" FROM rv_orders);
DELETE FROM "Orders" WHERE "Id" IN (SELECT "Id" FROM rv_orders);

SELECT (SELECT COUNT(*) FROM "WalletTransactions" WHERE "OwnerId"='6e4edec9-543c-4f10-9ef1-4b8efe894b97') AS shipper_tx_left,
       (SELECT COUNT(*) FROM "WithdrawalRequests" WHERE "OwnerId"='6e4edec9-543c-4f10-9ef1-4b8efe894b97') AS wr_left,
       (SELECT COUNT(*) FROM "Orders" WHERE "CustomerInfo_FullName" LIKE 'RV B3%') AS orders_left;
COMMIT;
