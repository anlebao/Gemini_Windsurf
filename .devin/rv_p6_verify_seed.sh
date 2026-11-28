#!/bin/bash
echo "=== L2 RE-VERIFY after hotfix (QR bogus -> 404 friendly) ==="
docker exec vanan-gateway-1 sh -c 'curl -s -o /dev/null -w "qr-bogus: %{http_code}\n" "localhost:80/api/public/booking/qr/bogus-token-xyz?anonymousSessionId=rv3"'
docker exec vanan-gateway-1 sh -c 'curl -s "localhost:80/api/public/booking/qr/bogus-token-xyz?anonymousSessionId=rv3" | head -c 200'
echo ""
echo "=== RUN SEED ==="
bash /tmp/rv_p6_seed.sh 2>&1 | tail -60
