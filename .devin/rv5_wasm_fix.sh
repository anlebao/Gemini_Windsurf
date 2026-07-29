#!/bin/bash
echo "=== KhachLink: find VanAn.KhachLink.wasm ==="
docker exec vanan-khachlink sh -c 'find /usr/share/nginx/html/_framework -name "VanAn.KhachLink*" 2>/dev/null | head -10'

echo ""
echo "=== KhachLink: grep Wallet in VanAn.KhachLink.wasm ==="
docker exec vanan-khachlink sh -c 'KL=$(find /usr/share/nginx/html/_framework -name "VanAn.KhachLink.wasm" | head -1); echo "Using: $KL"; grep -aoE "Wallet|WalletHttpService|ConfirmCodAsync|ConfirmAdvanceAsync|GetWalletAsync|PendingAdvances" "$KL" 2>/dev/null | sort -u'

echo ""
echo "=== KhachLink: _content dir (lazy-loaded dlls) ==="
docker exec vanan-khachlink sh -c 'ls /usr/share/nginx/html/_content/ 2>/dev/null | head -10; find /usr/share/nginx/html/_content -name "VanAn.KhachLink*" 2>/dev/null | head -5'
