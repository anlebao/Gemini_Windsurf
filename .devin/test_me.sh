#!/bin/bash
TOKEN='CfDJ8Db37i_tjt5MgkXNijh-sxq-IVZojumlc4Navuw78DzMr7Y_SeA8yciqODM_8lFHjRlOxKvv3r6mTltqxqRcSreD2xKoe5fl9zCNWThFZeoDmnE9HzfVeKZRDE1mBTI655fk8AVjL4029mcSCTGt-XYYWp6GMqHICGCh59mvUSEj5-AdMH2neTcJz_p9H36cXRDY1AjIdhcxdQJorXWNfZg'
echo "=== GET /api/customers/me ==="
curl -sk -H "X-Customer-Token: $TOKEN" https://api.khachvip.online/api/customers/me
echo
echo "=== GET /api/loyalty/my ==="
curl -sk -H "X-Customer-Token: $TOKEN" https://api.khachvip.online/api/loyalty/my
