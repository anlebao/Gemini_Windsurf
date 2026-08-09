#!/bin/bash
# Check migration status + #100 columns
echo "=== Migration History ==="
echo 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 15;' | docker exec -i vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t

echo ""
echo "=== #100 Home Section Toggle Columns ==="
echo "SELECT column_name FROM information_schema.columns WHERE table_name='ShopFeatureSettings' AND column_name LIKE 'Home_%' ORDER BY column_name;" | docker exec -i vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t

echo ""
echo "=== #93 Style Columns ==="
echo "SELECT column_name FROM information_schema.columns WHERE table_name='Tenants' AND column_name LIKE 'Settings_%Color' ORDER BY column_name;" | docker exec -i vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t

echo ""
echo "=== #98 OrderStatusUpdated in Gateway DLL (grep binary) ==="
docker exec vanan-gateway grep -ac "OrderStatusUpdated" /app/VanAn.Gateway.dll 2>/dev/null || echo "0"

echo ""
echo "=== #99 Error message in CoreHub DLL ==="
docker exec vanan-gateway grep -ac "Tài khoản chưa xác minh" /app/VanAn.CoreHub.dll 2>/dev/null || echo "0"

echo ""
echo "=== #99 Error message in Gateway DLL ==="
docker exec vanan-gateway grep -ac "Tài khoản chưa xác minh" /app/VanAn.Gateway.dll 2>/dev/null || echo "0"

echo ""
echo "=== #100 Save success msg in ShopERP DLL ==="
docker exec vanan-shoperp grep -ac "Đã lưu cấu hình thành công" /app/VanAn.ShopERP.dll 2>/dev/null || echo "0"

echo ""
echo "=== #100 Home sections card in ShopERP DLL ==="
docker exec vanan-shoperp grep -ac "Hiển thị trang chủ" /app/VanAn.ShopERP.dll 2>/dev/null || echo "0"

echo ""
echo "=== DLL paths in Gateway ==="
docker exec vanan-gateway find /app -name "VanAn.CoreHub*.dll" -maxdepth 1 2>/dev/null | head -5

echo ""
echo "=== DLL paths in ShopERP ==="
docker exec vanan-shoperp find /app -name "VanAn.CoreHub*.dll" -maxdepth 1 2>/dev/null | head -5
