#!/bin/bash
echo "=== KhachLink: find VanAn.KhachLink.dll ==="
docker exec vanan-khachlink sh -c 'find /usr/share/nginx/html -name "*.dll" 2>/dev/null | head -10'

echo ""
echo "=== KhachLink: find VanAn.KhachLink.dll (broader) ==="
docker exec vanan-khachlink sh -c 'find / -name "VanAn.KhachLink.dll" 2>/dev/null | head -5'

echo ""
echo "=== KhachLink: _framework dir ==="
docker exec vanan-khachlink sh -c 'ls /usr/share/nginx/html/_framework/ 2>/dev/null | head -10'

echo ""
echo "=== KhachLink: grep Wallet in all dlls ==="
docker exec vanan-khachlink sh -c 'find /usr/share/nginx/html -name "VanAn.KhachLink.dll" -exec grep -aoE "Wallet|WalletHttpService" {} \; 2>/dev/null | sort -u | head -10'

echo ""
echo "=== KhachLink: check wwwroot structure ==="
docker exec vanan-khachlink sh -c 'ls /usr/share/nginx/html/ 2>/dev/null | head -20'
