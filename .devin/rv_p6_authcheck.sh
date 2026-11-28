#!/bin/bash
echo "=== Impersonate WITHOUT any auth (clean jar, no login) ==="
rm -f /tmp/rv_nocookie.txt
RESP=$(curl -s -o /tmp/rv_imp_resp.txt -w '%{http_code}' -X POST 'https://app2.khachvip.online/api/admin/impersonate/6aaf19e4-8b55-48cd-94d7-b61c96a2d66d' \
  -H 'Content-Type: application/json' -b /tmp/rv_nocookie.txt -c /tmp/rv_nocookie.txt -k)
echo "HTTP: $RESP"
head -c 300 /tmp/rv_imp_resp.txt
echo ""
echo "=== Login THEN impersonate (proper flow) ==="
rm -f /tmp/rv_bk_cookies.txt
curl -s -o /dev/null -c /tmp/rv_bk_cookies.txt -X POST 'https://app2.khachvip.online/Login' \
  -d 'Username=sysadmin@vanan.vn&Password=2026@vanan&RememberMe=false' -k --max-redirs 0
wc -l /tmp/rv_bk_cookies.txt
RESP2=$(curl -s -o /tmp/rv_imp2.txt -w '%{http_code}' -X POST 'https://app2.khachvip.online/api/admin/impersonate/6aaf19e4-8b55-48cd-94d7-b61c96a2d66d' \
  -H 'Content-Type: application/json' -b /tmp/rv_bk_cookies.txt -c /tmp/rv_bk_cookies.txt -k)
echo "HTTP: $RESP2"
head -c 300 /tmp/rv_imp2.txt
echo ""
echo DONE
