SELECT DISTINCT "ProductId", MAX("ProductName") as name
FROM "OrderItems"
WHERE "ProductId" != '00000000-0000-0000-0000-000000000000'
GROUP BY "ProductId"
LIMIT 5;
