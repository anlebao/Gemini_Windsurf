-- Confirm the RV DELIVERY order (simulate owner confirmation)
UPDATE "Orders" SET "Status" = 'confirmed', "UpdatedAt" = NOW()
WHERE "Id" = '01a0a427-b2a5-75a6-ab0b-b1871ba2f199';
SELECT "Id"::text, "OrderType", "Status" FROM "Orders" WHERE "Id" = '01a0a427-b2a5-75a6-ab0b-b1871ba2f199';
