#!/bin/bash
TOKEN='CfDJ8Db37i_tjt5MgkXNijh-sxoe8JkbEXWDPU4zTBp_MmV37u_O01NvhPA8PsbgdOVo4onLa4nwKV6Aa1s4MyJUwGthWdtxn2C7HIqzIZb-f_8Hk-Q4K41O4sai8jrzFxvGm9BmJAeAySdhrv2jddKfRbG_Y1CwpLMfLRpnjioMy-lMqbtfXJzqJc8GKYMMWANB6U6KdcTfX5Yet1SqgkKeQY'
CATALOG_ID='8bcc833e-51c3-4508-adfb-41e2ff96ff79'

echo "=== Attempt redeem (100 pts, item needs 200 pts — expect insufficient) ==="
curl -sk -D /tmp/redeem_headers -X POST -H 'Content-Type: application/json' \
  -H "X-Customer-Token: $TOKEN" \
  -d "{\"CatalogItemId\":\"$CATALOG_ID\"}" \
  https://api.khachvip.online/api/redemption/redeem
echo
echo "=== Response headers ==="
grep -iE 'HTTP|content-type' /tmp/redeem_headers | head -5
