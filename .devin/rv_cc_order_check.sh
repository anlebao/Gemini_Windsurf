#!/bin/bash
# Debug: shipper không thấy đơn charity (order id ...01a0bc99)
PSQL="docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub"

echo "=== Order 01a0bc99 ==="
$PSQL -tAc "SELECT \"Id\", \"TenantId\", \"OrderType\", \"Status\", \"CustomerName\", \"CustomerPhone\", \"DeliveryAddress\", \"DeliveryLat\", \"DeliveryLng\", \"CreatedAt\" FROM \"Orders\" WHERE \"Id\"::text LIKE '%01a0bc99';"

echo ""
echo "=== Order items ==="
$PSQL -tAc "SELECT oi.\"OrderId\", oi.\"ProductName\", oi.\"Quantity\", oi.\"UnitPrice\", oi.\"IsFree\" FROM \"OrderItems\" oi WHERE oi.\"OrderId\"::text LIKE '%01a0bc99';"

echo ""
echo "=== Shipper feed filter check: DELIVERY orders within radius gần đây ==="
$PSQL -tAc "SELECT \"Id\"::text, \"OrderType\", \"Status\", \"CreatedAt\"::date FROM \"Orders\" WHERE \"OrderType\"='DELIVERY' AND \"CreatedAt\" > now() - interval '2 days' ORDER BY \"CreatedAt\" DESC LIMIT 15;"

echo ""
echo "=== DeliveryTasks gần đây ==="
$PSQL -tAc "SELECT \"Id\"::text, \"OrderId\"::text, \"Status\", \"CreatedAt\"::date FROM \"DeliveryTasks\" ORDER BY \"CreatedAt\" DESC LIMIT 10;" 2>&1 | head -12
