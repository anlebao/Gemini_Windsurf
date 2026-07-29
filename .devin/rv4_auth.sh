#!/bin/bash
# Authenticated smoke test: login as sysadmin → JWT → call admin ProductReferralConfig list
echo "=== Login as sysadmin@vanan.vn ==="
LOGIN_RESP=$(curl -s -X POST "https://khachvip.online/api/auth/login" -H "Content-Type: application/json" -d '{"email":"sysadmin@vanan.vn","password":"2026@vanan"}' -k)
echo "$LOGIN_RESP" | head -c 500
echo ""
TOKEN=$(echo "$LOGIN_RESP" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('token') or d.get('accessToken') or d.get('jwtToken') or '')" 2>/dev/null)
echo "Token (first 40 chars): ${TOKEN:0:40}..."

if [ -z "$TOKEN" ]; then
  echo "No token field — try cookie-based login"
  curl -s -i -c /tmp/rv4_cookies.txt -X POST "https://khachvip.online/Login" -d "Username=sysadmin@vanan.vn&Password=2026@vanan&RememberMe=false" -k --max-redirs 0 2>&1 | grep -iE "HTTP/|Set-Cookie: \.VanAn\.Auth" | head -3
  echo ""
  echo "=== Call /api/admin/products/referral-configs with cookie ==="
  curl -s -w "\nHTTP %{http_code}\n" -b /tmp/rv4_cookies.txt "https://khachvip.online/api/admin/products/referral-configs" -k | head -c 500
else
  echo ""
  echo "=== Call /api/admin/products/referral-configs with Bearer ==="
  curl -s -w "\nHTTP %{http_code}\n" -H "Authorization: Bearer $TOKEN" "https://api.khachvip.online/api/admin/products/referral-configs" -k | head -c 500
fi
