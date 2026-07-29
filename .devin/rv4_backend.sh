#!/bin/bash
echo "=== RV4-1: GET /api/community/nearby-products (no token) ==="
curl -s -o /dev/null -w "HTTP %{http_code}\n" "https://api.khachvip.online/api/community/nearby-products?lat=10.8&lng=106.7&radiusKm=10"

echo "=== RV4-2: GET /api/community/salesman/qr (no token) ==="
curl -s -o /dev/null -w "HTTP %{http_code}\n" "https://api.khachvip.online/api/community/salesman/qr?productId=00000000-0000-0000-0000-000000000001"

echo "=== RV4-3: GET /api/community/salesman/commissions (no token) ==="
curl -s -o /dev/null -w "HTTP %{http_code}\n" "https://api.khachvip.online/api/community/salesman/commissions"

echo "=== RV4-4: POST /api/community/app-install/attributed (no token) ==="
curl -s -o /dev/null -w "HTTP %{http_code}\n" -X POST "https://api.khachvip.online/api/community/app-install/attributed" -H "Content-Type: application/json" -d '{"referralCode":"ABC123|TR-001"}'

echo "=== RV4-5: POST /api/community/resolve-referral (no token) ==="
curl -s -o /dev/null -w "HTTP %{http_code}\n" -X POST "https://api.khachvip.online/api/community/resolve-referral" -H "Content-Type: application/json" -d '{"referralCode":"ABC123|TR-001"}'

echo "=== RV4-6: GET /api/admin/products/referral-configs (no auth) ==="
curl -s -o /dev/null -w "HTTP %{http_code}\n" "https://api.khachvip.online/api/admin/products/referral-configs"

echo "=== RV4-7: POST /api/admin/products/{id}/referral-config (no auth) ==="
curl -s -o /dev/null -w "HTTP %{http_code}\n" -X POST "https://api.khachvip.online/api/admin/products/00000000-0000-0000-0000-000000000099/referral-config" -H "Content-Type: application/json" -d '{"commissionRate":0.05,"appInstallBonus":10000,"productShortCode":"TEST-001"}'

echo "=== RV4-11: GET /api/community/role (no token) ==="
curl -s -o /dev/null -w "HTTP %{http_code}\n" "https://api.khachvip.online/api/community/role"

echo "=== Regression RV4-12: GET /api/community/nearby-orders (no token) ==="
curl -s -o /dev/null -w "HTTP %{http_code}\n" "https://api.khachvip.online/api/community/nearby-orders?lat=10.8&lng=106.7&radiusKm=5"

echo "=== Regression RV4-13: POST /api/community/orders/{id}/pickup (no token) ==="
curl -s -o /dev/null -w "HTTP %{http_code}\n" -X POST "https://api.khachvip.online/api/community/orders/00000000-0000-0000-0000-000000000099/pickup"

echo "=== Regression RV4-14: GET /api/community/chat/conversations/{id} (no token) ==="
curl -s -o /dev/null -w "HTTP %{http_code}\n" "https://api.khachvip.online/api/community/chat/conversations/00000000-0000-0000-0000-000000000099"
