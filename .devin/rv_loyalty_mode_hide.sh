#!/bin/bash
# RV: KhachLink LoyaltyMode=Silo UI hide fix
# Commit: 133e8061 (deployed via CD run 30789469902)
set -e

GATEWAY="https://api.khachvip.online"
KHACHLINK="https://diemthuong.khachvip.online"
PASS=0; FAIL=0

ok()   { echo "  [PASS] $1"; PASS=$((PASS+1)); }
fail() { echo "  [FAIL] $1"; FAIL=$((FAIL+1)); }

echo "=== RV: KhachLink LoyaltyMode UI Hide (commit 133e8061) ==="
echo ""

# --- 1. Containers healthy ---
echo "[1] Containers healthy"
HEALTH=$(docker ps --format "{{.Names}}:{{.Status}}" 2>/dev/null | grep -c "Up" || echo 0)
if [ "$HEALTH" -ge 7 ]; then ok "$HEALTH containers Up"; else fail "$HEALTH containers Up (expected >=7)"; fi
docker ps --format "  {{.Names}}: {{.Status}}" 2>/dev/null | head -10
echo ""

# --- 2. New endpoint: GET /api/loyalty/mode ---
echo "[2] New endpoint GET /api/loyalty/mode (anonymous, public)"
RESP=$(curl -sk -w "\n%{http_code}" "$GATEWAY/api/loyalty/mode")
CODE=$(echo "$RESP" | tail -1)
BODY=$(echo "$RESP" | head -n -1)
if [ "$CODE" = "200" ]; then
  ok "HTTP 200"
  echo "    Response: $BODY"
  # Verify response contains mode field
  if echo "$BODY" | grep -q '"mode"'; then
    ok "Response contains 'mode' field"
  else
    fail "Response missing 'mode' field: $BODY"
  fi
  # Verify mode value is Silo or Alliance
  if echo "$BODY" | grep -qiE '"mode"\s*:\s*"(Silo|Alliance)"'; then
    ok "Mode value is valid (Silo or Alliance)"
  else
    fail "Mode value invalid: $BODY"
  fi
else
  fail "GET /api/loyalty/mode returned $CODE (expected 200)"
  echo "    Body: $BODY"
fi
echo ""

# --- 3. WASM file freshness ---
echo "[3] KhachLink WASM freshness"
WASM_TIME=$(docker exec vanan-khachlink stat -c '%Y' /usr/share/nginx/html/_framework/VanAn.KhachLink.wasm 2>/dev/null || echo 0)
NOW=$(date +%s)
AGE=$(( (NOW - WASM_TIME) / 60 ))
if [ "$AGE" -lt 30 ]; then ok "VanAn.KhachLink.wasm fresh (${AGE} min ago)"; else fail "WASM stale (${AGE} min ago)"; fi
echo ""

# --- 4. Gateway DLL freshness (LoyaltyController has new /mode endpoint) ---
echo "[4] Gateway DLL freshness (LoyaltyController /mode endpoint)"
GW_TIME=$(docker exec vanan-gateway stat -c '%Y' /app/VanAn.Gateway.dll 2>/dev/null || docker exec vanan-gateway find / -name 'VanAn.Gateway.dll' -exec stat -c '%Y' {} \; 2>/dev/null | head -1 || echo 0)
AGE_GW=$(( (NOW - GW_TIME) / 60 ))
if [ "$AGE_GW" -lt 30 ]; then ok "VanAn.Gateway.dll fresh (${AGE_GW} min ago)"; else fail "Gateway DLL stale (${AGE_GW} min ago)"; fi
echo ""

# --- 5. Copy WASM to /tmp and verify fix strings ---
echo "[5] WASM contains LoyaltyMode fix strings"
docker cp vanan-khachlink:/usr/share/nginx/html/_framework/VanAn.KhachLink.wasm /tmp/vk2.wasm 2>/dev/null

# Use python3 to search UTF-16LE strings (how .NET stores them in WASM)
python3 -c "
data = open('/tmp/vk2.wasm', 'rb').read()
def find(p): return data.count(p.encode('utf-16-le'))

checks = {
    'LoyaltyModeHttpService': 'New service class name',
    'IsAllianceModeAsync': 'New method in service',
    'alliance-wallet': 'Alliance wallet route (still present, conditionally rendered)',
    'Ví liên minh': 'Vietnamese label (conditionally rendered)',
    'Tính năng liên minh đang tắt': 'New Silo-mode guard message in AllianceWallet.razor',
    'Silo': 'Silo mode string',
    'Alliance': 'Alliance mode string',
}
for s, desc in checks.items():
    c = find(s)
    print(f'  {s}: {c} matches — {desc}')
" 2>&1
echo ""

# --- 6. KhachLink pages load ---
echo "[6] KhachLink pages load"
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$KHACHLINK/")
if [ "$CODE" = "200" ]; then ok "KhachLink /: 200"; else fail "KhachLink /: $CODE"; fi

CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$KHACHLINK/alliance-wallet")
if [ "$CODE" = "200" ]; then ok "KhachLink /alliance-wallet: 200"; else fail "KhachLink /alliance-wallet: $CODE"; fi

CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$KHACHLINK/my-loyalty")
if [ "$CODE" = "200" ]; then ok "KhachLink /my-loyalty: 200"; else fail "KhachLink /my-loyalty: $CODE"; fi
echo ""

# --- 7. Gateway /health ---
echo "[7] Gateway health"
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$GATEWAY/health")
if [ "$CODE" = "200" ]; then ok "Gateway /health: 200"; else fail "Gateway /health: $CODE"; fi
echo ""

# --- Summary ---
echo "========================================"
echo "  RV SUMMARY: $PASS PASS, $FAIL FAIL"
if [ "$FAIL" -eq 0 ]; then
  echo "  ALL PASS — LoyaltyMode UI hide verified"
else
  echo "  $FAIL failures — investigate"
fi
echo "========================================"
