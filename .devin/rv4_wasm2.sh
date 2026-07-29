#!/bin/bash
echo "=== KhachLink: full _framework listing ==="
docker exec vanan-khachlink sh -c 'ls /usr/share/nginx/html/_framework 2>&1 | head -20'

echo ""
echo "=== KhachLink: find any VanAn*.dll (incl. compressed) ==="
docker exec vanan-khachlink sh -c 'find /usr/share/nginx/html -iname "vanan*" 2>/dev/null | head -20'

echo ""
echo "=== KhachLink: find all .dll / .br / .gz in _framework ==="
docker exec vanan-khachlink sh -c 'ls /usr/share/nginx/html/_framework/*.dll 2>/dev/null | wc -l; ls /usr/share/nginx/html/_framework/ 2>/dev/null | head -30'

echo ""
echo "=== KhachLink: check for VanAn.KhachLink.dll anywhere ==="
docker exec vanan-khachlink sh -c 'find / -iname "VanAn.KhachLink*" 2>/dev/null | head -10'

echo ""
echo "=== KhachLink: try common WASM dll names ==="
docker exec vanan-khachlink sh -c 'ls /usr/share/nginx/html/_framework/ 2>/dev/null | grep -iE "khachlink|vanan" | head -10'

echo ""
echo "=== KhachLink: grep -a on blazor.boot.json (lists all WASM assemblies) ==="
docker exec vanan-khachlink sh -c 'cat /usr/share/nginx/html/_framework/blazor.boot.json 2>/dev/null | head -c 2000'
