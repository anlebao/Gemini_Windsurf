#!/bin/bash
# RV5 DLL fix — use grep -a instead of strings

echo "=== RV5-3 FIX: WalletService methods in VanAn.CoreHub.dll ==="
docker exec vanan-gateway sh -c 'grep -aoE "ConfirmCodAsync|ConfirmAdvanceAsync|ConfirmAdvanceReceivedAsync|GetPendingAdvancesAsync|ReverseTransactionAsync|GetWalletAsync|MarkCodCollected" /app/VanAn.CoreHub.dll 2>/dev/null | sort -u'

echo ""
echo "=== RV5-3 FIX: Wallet routes in VanAn.Gateway.dll ==="
docker exec vanan-gateway sh -c 'grep -aoE "wallet/confirm-cod|wallet/confirm-advance|wallet/pending-advances|wallet/confirm-advance-received|api/community/wallet" /app/VanAn.Gateway.dll 2>/dev/null | sort -u'

echo ""
echo "=== RV5-4 FIX: Wallet page in KhachLink WASM dll ==="
docker exec vanan-khachlink sh -c 'KL=$(find /usr/share/nginx/html -name "VanAn.KhachLink.dll" | head -1); echo "Using: $KL"; grep -aoE "Wallet|WalletHttpService|ConfirmCodAsync|ConfirmAdvanceAsync|GetWalletAsync|PendingAdvances" "$KL" 2>/dev/null | sort -u'

echo ""
echo "=== RV5-3 FIX: Check if VanAn.CoreHub.dll exists ==="
docker exec vanan-gateway sh -c 'ls -la /app/VanAn.CoreHub.dll 2>/dev/null'

echo ""
echo "=== RV5-3 FIX: Check IWalletService in dll (broader grep) ==="
docker exec vanan-gateway sh -c 'grep -ao "WalletService" /app/VanAn.CoreHub.dll 2>/dev/null | head -5'
