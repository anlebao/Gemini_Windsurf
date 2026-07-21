SELECT
  COUNT(*) AS total,
  COUNT(*) FILTER (WHERE "Status" = 1) AS active,
  COUNT(*) FILTER (WHERE "Status" = 2) AS suspended,
  COUNT(*) FILTER (WHERE "Status" = 3) AS inactive
FROM public."Tenants";

SELECT "Id", "Name", "Status", "ShopInstanceId"
FROM public."Tenants"
ORDER BY "Name";
