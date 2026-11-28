#!/bin/bash
echo "=== cookie jar content ==="
cat /tmp/rv_bk_cookies.txt | grep -v '^#' | sed 's/\t.*//' 
echo "=== impersonate A verbose ==="
curl -s -X POST 'https://app2.khachvip.online/api/admin/impersonate/6aaf19e4-8b55-48cd-94d7-b61c96a2d66d' \
  -H 'Content-Type: application/json' -b /tmp/rv_bk_cookies.txt -c /tmp/rv_bk_cookies.txt -k \
  -o /tmp/imp_a.json -w 'HTTP:%{http_code}\n'
echo "--- body head ---"
head -c 200 /tmp/imp_a.json
echo ""
echo "=== retry login with verbose jar write ==="
rm -f /tmp/rv_bk2.txt
curl -s -D /tmp/login_hdr.txt -o /dev/null -c /tmp/rv_bk2.txt -X POST 'https://app2.khachvip.online/Login' \
  -d 'Username=sysadmin@vanan.vn&Password=2026@vanan&RememberMe=false' -k --max-redirs 0
grep -i "set-cookie" /tmp/login_hdr.txt | sed 's/;.*//' | head -3
echo "--- jar2 ---"
cat /tmp/rv_bk2.txt | grep -v '^#' | sed 's/\t.*//' | head -5
echo "=== impersonate with jar2 ==="
curl -s -X POST 'https://app2.khachvip.online/api/admin/impersonate/6aaf19e4-8b55-48cd-94d7-b61c96a2d66d' \
  -H 'Content-Type: application/json' -b /tmp/rv_bk2.txt -c /tmp/rv_bk2.txt -k \
  -o /tmp/imp_b.json -w 'HTTP:%{http_code}\n'
head -c 200 /tmp/imp_b.json
echo ""
echo DONE
