-- Verify RV order OrderType
SELECT "Id"::text, "OrderType", "Status", "DeliveryAddress", "ShippingFee", "CustomerNotes"
FROM "Orders"
WHERE "Id" = '01a0a426-7328-7535-9704-e408420d1263';
