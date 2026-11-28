#!/bin/bash
echo "=== impersonate B response ==="
curl -s -X POST 'https://app2.khachvip.online/api/admin/impersonate/4345dff8-b4b7-411f-b8f1-faf36f242d70' \
  -H 'Content-Type: application/json' -b /tmp/rv_bk_cookies.txt -c /tmp/rv_bk_cookies.txt -k \
  -o /tmp/imp_b.json -w 'HTTP:%{http_code}\n'
head -c 400 /tmp/imp_b.json
echo ""
echo "=== impersonate A (control) ==="
curl -s -X POST 'https://app2.khachvip.online/api/admin/impersonate/6aaf19e4-8b55-48cd-94d7-b61c96a2d66d' \
  -H 'Content-Type: application/json' -b /tmp/rv_bk_cookies.txt -c /tmp/rv_bk_cookies.txt -k \
  -o /tmp/imp_a.json -w 'HTTP:%{http_code}\n'
head -c 200 /tmp/imp_a.json
echo ""
echo "=== gateway tenant API direct (bogus qr + tenant B services) ==="
docker exec vanan-gateway-1 sh -c 'curl -s -o /dev/null -w "tenant-B-services: %{http_code}\n" "localhost:80/api/public/booking/tenants/4345dff8-b4b7-411f-b8f1-faf36f242d70/services"'
docker exec vanan-gateway-1 sh -c 'curl -s "localhost:80/api/public/booking/tenants/4345dff8-b4b7-411f-b8f1-faf36f242d70/services" | head -c 200'
echo ""
echo DONE
