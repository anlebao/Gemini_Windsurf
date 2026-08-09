#!/bin/bash
echo "=== ShopERP logs (last 50 lines, filter loyalty/dashboard) ==="
docker logs vanan-shoperp --tail 200 2>&1 | grep -i "loyalty\|dashboard\|error\|exception" | tail -30
echo ""
echo "=== Check what page returns 661 bytes (V7) ==="
curl -sk -b /tmp/rv_login_cookies.txt -w "\nHTTP %{http_code}, Size: %{size_download}" "https://app.khachvip.online/" 2>&1 | tail -5
echo ""
echo "=== Check /sitemap (should have nav) ==="
curl -sk -b /tmp/rv_login_cookies.txt -w "\nHTTP %{http_code}, Size: %{size_download}" "https://app.khachvip.online/sitemap" 2>&1 | tail -5
echo ""
echo "=== Check /loyalty/dashboard HTML content (first 500 chars) ==="
curl -sk -b /tmp/rv_login_cookies.txt "https://app.khachvip.online/loyalty/dashboard" 2>&1 | head -c 500
echo ""
echo ""
echo "=== Full error from API ==="
curl -sk -b /tmp/rv_login_cookies.txt "https://app.khachvip.online/api/loyalty/dashboard" 2>&1
