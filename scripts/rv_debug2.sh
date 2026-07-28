#!/bin/bash
echo "=== ShopERP running process ==="
docker exec vanan-shoperp ps aux 2>/dev/null | grep -i dotnet | head -5
echo
echo "=== ShopERP /app DLLs (all) ==="
docker exec vanan-shoperp find /app -name "VanAn.ShopERP.dll" -exec ls -la {} \; 2>/dev/null
echo
echo "=== ShopERP /app top-level ==="
docker exec vanan-shoperp ls -la /app/ 2>/dev/null | head -30
echo
echo "=== ShopERP actual runtime DLL (check process maps) ==="
docker exec vanan-shoperp sh -c "cat /proc/1/maps 2>/dev/null | grep VanAn.ShopERP | head -5" 2>/dev/null || echo "proc maps not available"
echo
echo "=== Gateway DLL timestamp ==="
docker exec vanan-gateway ls -la /app/VanAn.Gateway.dll 2>/dev/null
echo
echo "=== Test: does /api/missions/active actually work? ==="
curl -sk "https://khachvip.online/api/missions/active" | head -c 200
echo
echo
echo "=== Test: does /api/missions (admin, no auth) return 401? ==="
code=$(curl -sk -o /dev/null -w "%{http_code}" "https://khachvip.online/api/missions")
echo "Status: $code"
echo
echo "=== Test: POST /api/customer-profile/birthday (no token, expect 401) ==="
code=$(curl -sk -o /dev/null -w "%{http_code}" -X POST -H "Content-Type: application/json" -d '{"birthday":"1990-01-01"}' "https://khachvip.online/api/customer-profile/birthday")
echo "Status: $code"
echo
echo "=== Test: POST /api/customer-profile/pwa-install (no token, expect 401) ==="
code=$(curl -sk -o /dev/null -w "%{http_code}" -X POST "https://khachvip.online/api/customer-profile/pwa-install")
echo "Status: $code"
echo
echo "=== KhachLink: check if WASM DLL has Missions page ==="
KL_WASM_DLL=$(docker exec vanan-khachlink find /app/wwwroot/_framework -name "VanAn.KhachLink.dll" 2>/dev/null | head -1)
echo "WASM DLL: $KL_WASM_DLL"
if [ -n "$KL_WASM_DLL" ]; then
  docker exec vanan-khachlink ls -la "$KL_WASM_DLL" 2>/dev/null
fi
echo
echo "=== KhachLink: list all DLLs in _framework ==="
docker exec vanan-khachlink find /app/wwwroot/_framework -name "VanAn.*.dll" -exec ls -la {} \; 2>/dev/null
