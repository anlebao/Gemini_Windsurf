#!/bin/bash
echo "=== Gateway domains -> booking public API ==="
for dom in api.khachvip.online api2.khachvip.online; do
  code=$(curl -s -o /dev/null -w '%{http_code}' -m 8 "https://$dom/api/public/booking/qr/bogus?anonymousSessionId=t" -k)
  echo "$dom qr-bogus: $code"
done
echo "=== KhachLink domains -> /booking route ==="
for dom in diemthuong.khachvip.online timlathay.com; do
  code=$(curl -s -o /dev/null -w '%{http_code}' -m 8 "https://$dom/booking/00000000-0000-0000-0000-000000000000" -k)
  echo "$dom /booking: $code"
done
echo "=== health ==="
curl -s -o /dev/null -w "api2 /health: %{http_code}\n" -m 8 "https://api2.khachvip.online/health" -k
echo DONE
