#!/bin/bash
# RV on VPS: #114 r1+r2+r3 — comprehensive verification via HTTPS
# Tests all 3 VPS endpoints + API + JS files + data integrity
set +e

KHACHLINK="https://diemthuong2.khachvip.online"
GATEWAY="https://api2.khachvip.online"
SHOPERP="https://app2.khachvip.online"
PASS=0; FAIL=0; WARN=0
ok()   { echo "  [PASS] $1"; PASS=$((PASS+1)); }
fail() { echo "  [FAIL] $1"; FAIL=$((FAIL+1)); }
warn() { echo "  [WARN] $1"; WARN=$((WARN+1)); }

echo "=== RV on VPS: #114 r1+r2+r3 — comprehensive verification ==="
echo "  Gateway:  $GATEWAY"
echo "  ShopERP:  $SHOPERP"
echo "  KhachLink: $KHACHLINK"
echo ""

# ============================================================
# 1. Sites load (no regression)
# ============================================================
echo "[1] No regression — all sites load"
for url in "$GATEWAY/health" "$SHOPERP/Login" "$KHACHLINK/" "$KHACHLINK/rewards" "$KHACHLINK/stores"; do
  CODE=$(curl -sk -o /dev/null -w "%{http_code}" --max-time 10 "$url")
  if [ "$CODE" = "200" ]; then ok "$url: 200"; else fail "$url: $CODE"; fi
done
echo ""

# ============================================================
# 2. POS-only product hidden from public catalog (KhachLink)
# ============================================================
echo "[2] POS-only 'Sản phẩm dịch vụ' hidden from public catalog"
RESP=$(curl -sk --max-time 10 "$GATEWAY/api/products?shopId=00000000-0000-0000-0000-000000000001")
if echo "$RESP" | grep -qi "Sản phẩm dịch vụ"; then
  fail "Public catalog contains 'Sản phẩm dịch vụ' (IsPosOnly filter NOT working)"
else
  ok "Public catalog does NOT contain 'Sản phẩm dịch vụ'"
fi

# Check grouped catalog endpoint too
RESP=$(curl -sk --max-time 10 "$GATEWAY/api/products/grouped")
if echo "$RESP" | grep -qi "Sản phẩm dịch vụ"; then
  fail "Grouped catalog contains 'Sản phẩm dịch vụ'"
else
  ok "Grouped catalog does NOT contain 'Sản phẩm dịch vụ'"
fi
echo ""

# ============================================================
# 3. POS + Kitchen pages accessible (auth redirect)
# ============================================================
echo "[3] POS + Kitchen pages accessible"
for path in "/pos" "/kitchen" "/products" "/orders" "/admin/redemption-catalog"; do
  CODE=$(curl -sk -o /dev/null -w "%{http_code}" --max-time 10 "$SHOPERP$path")
  if [ "$CODE" = "302" ] || [ "$CODE" = "200" ]; then ok "$SHOPERP$path: $CODE"; else fail "$SHOPERP$path: $CODE"; fi
done
echo ""

# ============================================================
# 4. JS files served (pos-voice-note + tts-reader)
# ============================================================
echo "[4] JS files served"
for js in "js/pos-voice-note.js" "js/tts-reader.js" "js/qr-scanner.js" "js/tenant-map.js"; do
  CODE=$(curl -sk -o /dev/null -w "%{http_code}" --max-time 10 "$SHOPERP/$js")
  if [ "$CODE" = "200" ]; then ok "$js: 200"; else fail "$js: $CODE"; fi
done

# Verify pos-voice-note.js content
CONTENT=$(curl -sk --max-time 10 "$SHOPERP/js/pos-voice-note.js")
if echo "$CONTENT" | grep -q "vananPosStartRecording"; then
  ok "pos-voice-note.js has vananPosStartRecording function"
else
  fail "pos-voice-note.js missing vananPosStartRecording"
fi
if echo "$CONTENT" | grep -q "vi-VN"; then
  ok "pos-voice-note.js configured for vi-VN"
else
  fail "pos-voice-note.js missing vi-VN locale"
fi

# Verify tts-reader.js content
CONTENT=$(curl -sk --max-time 10 "$SHOPERP/js/tts-reader.js")
if echo "$CONTENT" | grep -q "ttsReader"; then
  ok "tts-reader.js has ttsReader object"
else
  fail "tts-reader.js missing ttsReader"
fi
if echo "$CONTENT" | grep -q "speechSynthesis"; then
  ok "tts-reader.js uses speechSynthesis API"
else
  fail "tts-reader.js missing speechSynthesis"
fi
echo ""

# ============================================================
# 5. API endpoints respond
# ============================================================
echo "[5] API endpoints respond"
# Local catalog (forwarded to ShopERP)
RESP=$(curl -sk --max-time 10 -o /dev/null -w "%{http_code}" "$GATEWAY/api/redemption/catalog/active")
if [ "$RESP" = "200" ]; then ok "GET /api/redemption/catalog/active: 200"; else fail "GET /api/redemption/catalog/active: $RESP"; fi

# Global catalog
RESP=$(curl -sk --max-time 10 -o /dev/null -w "%{http_code}" "$GATEWAY/api/redemption/catalog/global")
if [ "$RESP" = "200" ]; then ok "GET /api/redemption/catalog/global: 200"; else fail "GET /api/redemption/catalog/global: $RESP"; fi

# Global catalog has IsAvailable field
RESP=$(curl -sk --max-time 10 "$GATEWAY/api/redemption/catalog/global")
if echo "$RESP" | grep -q "isAvailable"; then
  ok "Global catalog response has 'isAvailable' field (#124 fix verified)"
else
  warn "Global catalog response missing 'isAvailable' (may be empty catalog)"
fi
echo ""

# ============================================================
# 6. KhachLink pages load (no regression from #125 bottom nav fix)
# ============================================================
echo "[6] KhachLink pages load (no regression)"
for path in "/" "/rewards" "/stores" "/store-finder" "/my-orders" "/login"; do
  CODE=$(curl -sk -o /dev/null -w "%{http_code}" --max-time 10 "$KHACHLINK$path")
  if [ "$CODE" = "200" ]; then ok "$KHACHLINK$path: 200"; else fail "$KHACHLINK$path: $CODE"; fi
done
echo ""

# ============================================================
# 7. ShopERP Login page renders (check for Blazor SSR)
# ============================================================
echo "[7] ShopERP Login page renders correctly"
HTML=$(curl -sk --max-time 10 "$SHOPERP/Login")
if echo "$HTML" | grep -qi "blazor"; then ok "Login page has Blazor reference"; else warn "Login page missing Blazor reference"; fi
if echo "$HTML" | grep -qi "form"; then ok "Login page has form element"; else warn "Login page missing form"; fi
echo ""

# ============================================================
# 8. Check for 502/503/504 errors (upstream health)
# ============================================================
echo "[8] No 502/503/504 errors"
for url in "$GATEWAY/health" "$SHOPERP/Login" "$KHACHLINK/"; do
  CODE=$(curl -sk -o /dev/null -w "%{http_code}" --max-time 10 "$url")
  case "$CODE" in
    502|503|504) fail "$url: $CODE (upstream error)";;
    *) ok "$url: $CODE (no upstream error)";;
  esac
done
echo ""

# ============================================================
# Summary
# ============================================================
echo "==============================================="
echo "RV Summary: PASS=$PASS  FAIL=$FAIL  WARN=$WARN"
if [ "$FAIL" -eq 0 ]; then
  echo "RESULT: ALL PASS"
else
  echo "RESULT: $FAIL FAILURE(S)"
fi
if [ "$WARN" -gt 0 ]; then echo "NOTES: $WARN warning(s) — review above"; fi
echo "==============================================="
