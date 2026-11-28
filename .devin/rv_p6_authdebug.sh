#!/bin/bash
echo "=== Login debug ==="
rm -f /tmp/rv_bk_cookies.txt
curl -s -i -c /tmp/rv_bk_cookies.txt -X POST 'https://app2.khachvip.online/Login' \
  -d 'Username=sysadmin@vanan.vn&Password=2026@vanan&RememberMe=false' -k --max-redirs 0 2>&1 | head -25
echo "=== cookies ==="
cat /tmp/rv_bk_cookies.txt | grep -v "^#" | head -10
echo "=== impersonate debug ==="
curl -s -X POST 'https://app2.khachvip.online/api/admin/impersonate/6aaf19e4-8b55-48cd-94d7-b61c96a2d66d' \
  -H 'Content-Type: application/json' -b /tmp/rv_bk_cookies.txt -c /tmp/rv_bk_cookies.txt -k | head -c 400
echo ""
echo DONE
