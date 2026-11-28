#!/bin/bash
PSQL="docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub"
OID="01a0bc99-e003-7764-890f-03fd5c2d4859"

echo "=== Order columns ==="
$PSQL -tAc "SELECT column_name FROM information_schema.columns WHERE table_name='Orders' AND column_name IN ('TenantId','OrderType','Status','CustomerId','CustomerDeviceId','CustomerName','CustomerPhone','DeliveryAddress','DeliveryLat','DeliveryLng','ShippingFee','TotalAmount','CreatedAt','SalesmanId','ReferralProductId');"

echo ""
echo "=== Order 01a0bc99 detail ==="
$PSQL -tAc "SELECT \"TenantId\", \"OrderType\", \"Status\", COALESCE(\"CustomerId\"::text,'null'), COALESCE(\"CustomerDeviceId\",'null'), COALESCE(\"DeliveryAddress\",'null'), COALESCE(\"DeliveryLat\"::text,'null'), COALESCE(\"DeliveryLng\"::text,'null'), \"ShippingFee\", \"TotalAmount\", \"CreatedAt\" FROM \"Orders\" WHERE \"Id\"='$OID';"

echo ""
echo "=== Tenant (shop) coordinates ==="
$PSQL -tAc "SELECT \"Id\", \"Name\", \"Settings_Latitude\", \"Settings_Longitude\", \"Settings_Address\" FROM \"Tenants\" WHERE \"Id\" IN (SELECT \"TenantId\" FROM \"Orders\" WHERE \"Id\"='$OID');"

echo ""
echo "=== DeliveryTasks cho order này (mọi status) ==="
$PSQL -tAc "SELECT \"Id\", \"ShipperId\", \"Status\", \"CreatedAt\" FROM \"DeliveryTasks\" WHERE \"OrderId\"='$OID';"

echo ""
echo "=== Shipper gần đây (có role Shipper active) ==="
$PSQL -tAc "SELECT \"CustomerId\", \"RoleType\", \"IsActive\" FROM \"CommunityRoles\" WHERE \"RoleType\"='Shipper' AND \"IsActive\"=true LIMIT 10;"
