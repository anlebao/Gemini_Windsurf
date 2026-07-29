#!/bin/bash
# Bảo Ấn Lê (6A8DE489) — should have 21,154 points
# But this customer has empty PhoneNumber — token may not be valid
# Try the token stored in PhoneNumber column of customer 6A8DE489... wait, that's empty.
# Use customer 5E375512 (RV Bug6 V2, 8250 points) — has token in PhoneNumber
TOKEN='CfDJ8Db37i_tjt5MgkXNijh-sxoq9keY_GnRn1WqDgpPlQKFFZ3MFnUxJeqHSq_8Rft8dDgVlfiXyhUQ8iJ6JW5fJ7nGChVpRvi9TcqgIBJ_T2-iaqCS0mrGx_vmqZeOFlnVRQ'
echo "=== GET /api/customers/me (RV Bug6 V2, expect 8250 points) ==="
curl -sk -H "X-Customer-Token: $TOKEN" https://api.khachvip.online/api/customers/me
echo
echo "=== GET /api/loyalty/my ==="
curl -sk -H "X-Customer-Token: $TOKEN" https://api.khachvip.online/api/loyalty/my
