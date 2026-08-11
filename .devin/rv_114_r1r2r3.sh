#!/bin/bash
# RV: #114 r1+r2+r3 — POS price entry + kitchen items + notes/voice + TTS
set -e

KHACHLINK="https://diemthuong2.khachvip.online"
GATEWAY="https://api2.khachvip.online"
SHOPERP="https://app2.khachvip.online"
PASS=0; FAIL=0
ok()   { echo "  [PASS] $1"; PASS=$((PASS+1)); }
fail() { echo "  [FAIL] $1"; FAIL=$((FAIL+1)); }

echo "=== RV: #114 r1+r2+r3 — POS price + kitchen + notes/voice + TTS ==="
echo ""

# ============================================================
# Part 1: Sites load (no regression)
# ============================================================
echo "[1] No regression — all sites load"
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$GATEWAY/health"); if [ "$CODE" = "200" ]; then ok "Gateway /health: 200"; else fail "Gateway /health: $CODE"; fi
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$SHOPERP/Login"); if [ "$CODE" = "200" ]; then ok "ShopERP /Login: 200"; else fail "ShopERP /Login: $CODE"; fi
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$KHACHLINK/"); if [ "$CODE" = "200" ]; then ok "KhachLink home: 200"; else fail "KhachLink home: $CODE"; fi
echo ""

# ============================================================
# Part 2: POS-only product hidden from public catalog
# ============================================================
echo "[2] POS-only product hidden from public catalog"
RESP=$(curl -sk "$GATEWAY/api/products?shopId=00000000-0000-0000-0000-000000000001")
if echo "$RESP" | grep -qi "Sản phẩm dịch vụ"; then
  fail "Public catalog still contains 'Sản phẩm dịch vụ' (should be hidden)"
else
  ok "Public catalog does not contain 'Sản phẩm dịch vụ' (IsPosOnly filter works)"
fi
echo ""

# ============================================================
# Part 3: POS page loads (behind auth — 302 redirect is OK)
# ============================================================
echo "[3] POS page accessible"
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$SHOPERP/pos")
if [ "$CODE" = "302" ] || [ "$CODE" = "200" ]; then ok "ShopERP /pos: $CODE (auth redirect OK)"; else fail "ShopERP /pos: $CODE"; fi
echo ""

# ============================================================
# Part 4: Kitchen page loads
# ============================================================
echo "[4] Kitchen page accessible"
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$SHOPERP/kitchen")
if [ "$CODE" = "302" ] || [ "$CODE" = "200" ]; then ok "ShopERP /kitchen: $CODE (auth redirect OK)"; else fail "ShopERP /kitchen: $CODE"; fi
echo ""

# ============================================================
# Part 5: JS files served
# ============================================================
echo "[5] JS files served"
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$SHOPERP/js/pos-voice-note.js")
if [ "$CODE" = "200" ]; then ok "pos-voice-note.js: 200"; else fail "pos-voice-note.js: $CODE"; fi
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$SHOPERP/js/tts-reader.js")
if [ "$CODE" = "200" ]; then ok "tts-reader.js: 200"; else fail "tts-reader.js: $CODE"; fi
echo ""

# ============================================================
# Part 6: Product management page loads (should not show POS-only items)
# ============================================================
echo "[6] Product management page loads"
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$SHOPERP/products")
if [ "$CODE" = "302" ] || [ "$CODE" = "200" ]; then ok "ShopERP /products: $CODE"; else fail "ShopERP /products: $CODE"; fi
echo ""

# ============================================================
# Summary
# ============================================================
echo "==============================================="
echo "RV Summary: PASS=$PASS  FAIL=$FAIL"
if [ "$FAIL" -eq 0 ]; then echo "RESULT: ALL PASS"; else echo "RESULT: $FAIL FAILURE(S)"; fi
echo "==============================================="
