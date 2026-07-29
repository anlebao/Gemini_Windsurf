SELECT "Id", "OrderId", "ProductId", "ProductName", "Quantity", "UnitPrice", "VatRate", "SubTotal", "VatAmount", "TotalAmount"
FROM "OrderItems"
WHERE "OrderId" = '019fa91a-ede1-7633-8806-96dc79cedf7c'
ORDER BY "ProductName";
