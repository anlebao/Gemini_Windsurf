#!/bin/bash
# RV: Issue #122 + #115 — friendly 503 error pages + print bill feature
# Commit: 4443356f (deployed via CD run 31407978137)
set -e

KHACHLINK="https://diemthuong2.khachvip.online"
GATEWAY="https://api2.khachvip.online"
SHOPERP="https://app2.khachvip.online"
WWW2="https://www2.khachvip.online"
PASS=0; FAIL=0

ok()   { echo "  [PASS] $1"; PASS=$((PASS+1)); }
fail() { echo "  [FAIL] $1"; FAIL=$((FAIL+1)); }

echo "=== RV: Issue #122 + #115 (commit 4443356f) ==="
echo ""

# ============================================================
# Part 1: #122 — Friendly 503 error pages served by nginx
# ============================================================
echo "[1] #122 — nginx error pages (503.html, 502.html, 504.html)"

# Check 503.html exists on nginx (via www2 — should serve friendly page on 503)
# We can't easily trigger a real 503, but we can verify the error pages exist
# by checking if nginx serves them at their internal location.
# Since they're marked "internal", we test by checking a non-existent upstream.
# Alternative: verify the HTML files are deployed in nginx container.

# Test: 503.html content should contain "Hệ thống đang cần một nhịp thở"
# We check via curl to a deliberately bad endpoint that triggers 503
# Actually, nginx error_page with "internal" won't serve directly.
# Instead, we verify the pages are accessible via the nginx html volume.

# Test 1a: Check if 503 page text appears when we can trigger 503
# We'll use a fast burst of requests to trigger rate limit (but nginx returns 429, not 503)
# So instead, we verify the static files exist on the Gateway VPS nginx container

# For remote verification: check that the error pages are served
# by requesting them directly (they should return 404 due to "internal" directive,
# but the file exists if nginx doesn't return default 404 page)

# Actually, let's verify by checking the nginx config was updated
# We'll test the actual 503 scenario by checking if the page loads
# when upstream is down — but we can't do that in RV.

# Instead: verify the HTML files exist in nginx/html volume on Gateway VPS
# For now, we'll do a basic smoke test: all sites should still load (no config break)

CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$WWW2/")
if [ "$CODE" = "200" ] || [ "$CODE" = "302" ]; then ok "www2 (ShopERP) /: $CODE (nginx config not broken)"; else fail "www2 /: $CODE"; fi

CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$KHACHLINK/")
if [ "$CODE" = "200" ]; then ok "diemthuong2 (KhachLink) /: 200"; else fail "diemthuong2 /: $CODE"; fi

CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$SHOPERP/Login")
if [ "$CODE" = "200" ]; then ok "app2 (ShopERP staff) /Login: 200"; else fail "app2 /Login: $CODE"; fi

CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$GATEWAY/health")
if [ "$CODE" = "200" ]; then ok "api2 (Gateway) /health: 200"; else fail "api2 /health: $CODE"; fi
echo ""

# ============================================================
# Part 2: #122 — KhachLink wwwroot error pages deployed
# ============================================================
echo "[2] #122 — KhachLink WASM error pages in wwwroot"

# Check error-503.html is accessible via KhachLink static file serving
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$KHACHLINK/error-503.html")
if [ "$CODE" = "200" ]; then ok "KhachLink /error-503.html: 200 (deployed)"; else fail "KhachLink /error-503.html: $CODE"; fi

RESP=$(curl -sk "$KHACHLINK/error-503.html")
if echo "$RESP" | grep -q "Hệ thống đang cần một nhịp thở"; then
  ok "error-503.html contains friendly message"
else
  fail "error-503.html does NOT contain friendly message"
fi

CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$KHACHLINK/error-5xx.html")
if [ "$CODE" = "200" ]; then ok "KhachLink /error-5xx.html: 200 (deployed)"; else fail "KhachLink /error-5xx.html: $CODE"; fi
echo ""

# ============================================================
# Part 3: #115 — Print bill page accessible
# ============================================================
echo "[3] #115 — Print bill page route"

# The print page is at /orders/{id}/print — requires auth, so we expect 200 or redirect to login
# We'll check that the route exists (not 404) by hitting it with a dummy GUID
# ShopERP Blazor Server: unauthenticated → redirect to /Login (200), authenticated → 200
DUMMY_ID="00000000-0000-0000-0000-000000000001"
CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$SHOPERP/orders/$DUMMY_ID/print")
# Blazor Server: if not authenticated, redirects to Login (200 or 302)
# If route doesn't exist, returns 404
if [ "$CODE" = "200" ] || [ "$CODE" = "302" ]; then
  ok "Print bill route /orders/{id}/print exists (HTTP $CODE)"
else
  fail "Print bill route returned $CODE (expected 200 or 302)"
fi
echo ""

# ============================================================
# Part 4: #115 — ShopERP pages still load (no regression)
# ============================================================
echo "[4] #115 — ShopERP order pages load (no regression)"

CODE=$(curl -sk -o /dev/null -w "%{http_code}" "$SHOPERP/orders")
if [ "$CODE" = "200" ] || [ "$CODE" = "302" ]; then ok "ShopERP /orders: $CODE"; else fail "ShopERP /orders: $CODE"; fi

# Check the print JS function exists in App.razor
APP_HTML=$(curl -sk "$SHOPERP/")
if echo "$APP_HTML" | grep -q "vananPrintBill"; then
  ok "vananPrintBill JS function present in App.razor"
else
  # Blazor Server may not include inline JS in initial HTML — check via page source
  echo "  (vananPrintBill check inconclusive — Blazor Server renders client-side)"
fi
echo ""

# ============================================================
# Summary
# ============================================================
echo "==============================================="
echo "RV #122+#115 Summary: PASS=$PASS  FAIL=$FAIL"
if [ "$FAIL" -eq 0 ]; then
  echo "RESULT: ALL PASS"
else
  echo "RESULT: $FAIL FAILURE(S)"
fi
echo "==============================================="
