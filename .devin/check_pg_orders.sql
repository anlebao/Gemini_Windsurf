SELECT "Id", "TotalAmount", "SubTotal", "TotalVatAmount", "Status", "OrderDate"
FROM "Orders"
WHERE "Id" IN (
  '019fa299-dedc-75c5-8051-35ee27a39d70',
  '019fa684-3c13-72df-b479-34527990b256',
  '019fa91a-ede1-7633-8806-96dc79cedf7c',
  '019fa97f-2ec3-717c-ae5a-f54141e03dd6',
  '019fa9e7-e111-778c-a3b9-5f21ddfe57ed',
  '019fa9ec-82f6-71b4-a75b-298613949cb2',
  '019fa9f0-4411-723d-888d-ba88d3b3a3b0'
)
ORDER BY "OrderDate";
