#!/bin/bash
# Test Gateway /api/v1/tenants with a minted SystemAdmin JWT
python3 << 'PYEOF' > /tmp/jwt.txt
import json, base64, hmac, hashlib, time
secret = 'ChieuKhoaBaoMatSieuCapCuaVanAnGroup2026!!'
header = {'alg':'HS256','typ':'JWT'}
payload = {
  'sub':'00000000-0000-0000-0000-000000000001',
  'email':'admin@vanan.vn',
  'tenant_id':'system',
  'TenantId':'system',
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role':'SystemAdmin',
  'exp':int(time.time())+3600,
  'iss':'VanAnShopERP',
  'aud':'VanAnApi'
}
def b64(d):
    return base64.urlsafe_b64encode(json.dumps(d,separators=(',',':')).encode()).rstrip(b'=').decode()
h = b64(header); p = b64(payload)
sig = base64.urlsafe_b64encode(hmac.new(secret.encode(), (h+'.'+p).encode(), hashlib.sha256).digest()).rstrip(b'=').decode()
print(h+'.'+p+'.'+sig)
PYEOF

JWT=$(cat /tmp/jwt.txt)
echo "JWT length: ${#JWT}"
echo ""
echo "=== Test 1: Gateway /api/v1/tenants (internal Docker network) ==="
curl -s -w "\nHTTP_CODE: %{http_code}\n" -H "Authorization: Bearer $JWT" "http://gateway:80/api/v1/tenants" 2>&1 | head -30
echo ""
echo "=== Test 2: Gateway /api/v1/tenants (public domain) ==="
curl -s -w "\nHTTP_CODE: %{http_code}\n" -H "Authorization: Bearer $JWT" "https://api.khachvip.online/api/v1/tenants" 2>&1 | head -30
