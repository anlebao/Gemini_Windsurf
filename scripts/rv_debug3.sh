#!/bin/bash
echo "=== Birthday endpoint (proper JSON) ==="
code=$(curl -sk -o /tmp/bday.json -w "%{http_code}" -X POST -H "Content-Type: application/json" -d '{"Birthday":"1990-01-01"}' "https://khachvip.online/api/customer-profile/birthday")
echo "Status: $code"
cat /tmp/bday.json
echo
echo
echo "=== Share endpoint (proper JSON) ==="
code2=$(curl -sk -o /tmp/share.json -w "%{http_code}" -X POST -H "Content-Type: application/json" -d '{"ShareUrl":"https://facebook.com/test"}' "https://khachvip.online/api/customer-profile/share")
echo "Status: $code2"
cat /tmp/share.json
echo
echo
echo "=== Birthday with no body (expect 400 or 401) ==="
code3=$(curl -sk -o /dev/null -w "%{http_code}" -X POST "https://khachvip.online/api/customer-profile/birthday")
echo "Status: $code3"
