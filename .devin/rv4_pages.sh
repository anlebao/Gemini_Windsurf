#!/bin/bash
echo "=== RV4-8: KhachLink /community/nearby-products ==="
curl -s -o /dev/null -w "HTTP %{http_code}\n" "https://diemthuong.khachvip.online/community/nearby-products"

echo "=== RV4-9: KhachLink /community/salesman-qr?productId=... ==="
curl -s -o /dev/null -w "HTTP %{http_code}\n" "https://diemthuong.khachvip.online/community/salesman-qr?productId=00000000-0000-0000-0000-000000000001"

echo "=== RV4-10: KhachLink /community/sales-dashboard ==="
curl -s -o /dev/null -w "HTTP %{http_code}\n" "https://diemthuong.khachvip.online/community/sales-dashboard"

echo "=== RV4-15: KhachLink / (regression) ==="
curl -s -o /dev/null -w "HTTP %{http_code}\n" "https://diemthuong.khachvip.online/"

echo "=== ShopERP Admin /admin/product-referral-configs ==="
curl -s -o /dev/null -w "HTTP %{http_code}\n" -L --max-redirs 0 "https://khachvip.online/admin/product-referral-configs"

echo "=== ShopERP Admin /admin/products (regression) ==="
curl -s -o /dev/null -w "HTTP %{http_code}\n" -L --max-redirs 0 "https://khachvip.online/admin/products"

echo "=== WASM assets: qrcode.js ==="
curl -s -o /dev/null -w "HTTP %{http_code} size=%{size_download}\n" "https://diemthuong.khachvip.online/js/qrcode.js"

echo "=== WASM assets: app-install-tracker.js ==="
curl -s -o /dev/null -w "HTTP %{http_code} size=%{size_download}\n" "https://diemthuong.khachvip.online/js/app-install-tracker.js"

echo "=== WASM assets: fingerprintjs (regression from S0) ==="
curl -s -o /dev/null -w "HTTP %{http_code} size=%{size_download}\n" "https://diemthuong.khachvip.online/lib/fingerprintjs/fingerprint.js"
