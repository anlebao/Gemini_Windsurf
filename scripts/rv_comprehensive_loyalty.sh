#!/bin/bash
# ============================================================
# COMPREHENSIVE RV v2 — Phase 5 + Loyalty-A + Loyalty-B + Loyalty-C
# Fixed: correct endpoint paths + sleep between requests to avoid nginx rate limit
# ============================================================
set +e
PASS=0
FAIL=0
RESULTS=""

check() {
  local name="$1"; local expected="$2"; local actual="$3"
  if [ "$actual" = "$expected" ]; then
    PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] $name — '$actual'\n"
  else
    FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] $name — expected '$expected', got '$actual'\n"
  fi
}
check_in() {
  local name="$1"; local expected="$2"; local actual="$3"
  if echo "$actual" | grep -qE "$expected"; then
    PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] $name — matched '$expected' (got '$actual')\n"
  else
    FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] $name — expected '$expected', got '$actual'\n"
  fi
}
check_contains() {
  local name="$1"; local needle="$2"; local haystack="$3"
  if echo "$haystack" | grep -q "$needle"; then
    PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] $name — contains '$needle'\n"
  else
    FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] $name — missing '$needle'\n"
  fi
}

# Helper: curl with sleep to avoid rate limiting
scurl() {
  local result=$(curl -sk "$@")
  sleep 1
  echo "$result"
}
scode() {
  local code=$(curl -sk -o /dev/null -w "%{http_code}" "$@")
  sleep 1
  echo "$code"
}

SHOPERP="https://khachvip.online"
KHACHLINK="https://diemthuong.khachvip.online"
TENANT="21cbf14f-581a-48c8-8ad6-becc21064535"
# Gateway is accessed via container IP:80 (not through nginx which proxies to ShopERP)
GW_IP=$(docker inspect vanan-gateway --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
GATEWAY="http://$GW_IP"

echo "=== COMPREHENSIVE RV v2: Phase 5 + Loyalty-A/B/C ==="
echo

# ============================================================
# SECTION 1: CONTAINER HEALTH + DEPLOYMENT
# ============================================================
echo "=== SECTION 1: Container Health + Deployment ==="
for c in vanan-gateway vanan-shoperp vanan-khachlink vanan-postgres vanan-nats vanan-nginx vanan-seq; do
  status=$(docker inspect --format '{{.State.Health.Status}}' $c 2>/dev/null)
  [ -z "$status" ] && status=$(docker inspect --format '{{.State.Status}}' $c 2>/dev/null)
  check_in "container-$c" "healthy|running" "$status"
done

SE_DLL_TIME=$(docker exec vanan-shoperp stat -c %y /app/VanAn.ShopERP.dll 2>/dev/null | cut -d. -f1)
GW_DLL_TIME=$(docker exec vanan-gateway stat -c %y /app/VanAn.Gateway.dll 2>/dev/null | cut -d. -f1)
echo "  ShopERP DLL: $SE_DLL_TIME | Gateway DLL: $GW_DLL_TIME"
check "shoperp-dll-deployed-today" "1" "$(echo $SE_DLL_TIME | grep -c '2026-07-24')"
check "gateway-dll-deployed-today" "1" "$(echo $GW_DLL_TIME | grep -c '2026-07-24')"

# ============================================================
# SECTION 2: PHASE 5 — Push Notification + PWA
# ============================================================
echo
echo "=== SECTION 2: Phase 5 — Push Notification + PWA ==="

# P5-1: manifest.json
MANIFEST_BODY=$(scurl "$KHACHLINK/manifest.json")
check_contains "p5-manifest-has-name" "Vạn An" "$MANIFEST_BODY"
check_contains "p5-manifest-has-display" "standalone" "$MANIFEST_BODY"

# P5-2: service-worker.js
check "p5-service-worker-200" "200" "$(scode $KHACHLINK/service-worker.js)"

# P5-3: pwa.js
PWAJS_BODY=$(scurl "$KHACHLINK/js/pwa.js")
check_contains "p5-pwa-js-has-install-prompt" "beforeinstallprompt" "$PWAJS_BODY"

# P5-4: KhachLink /profile (push toggle page)
check "p5-khachlink-profile-200" "200" "$(scode $KHACHLINK/profile)"

# P5-5: Push subscribe (no token → 401)
echo '{}' > /tmp/sub.json
check "p5-push-subscribe-no-token-401" "401" "$(scode -X POST -H 'Content-Type: application/json' -d @/tmp/sub.json $SHOPERP/api/notifications/push/subscribe)"

# P5-6: Push unsubscribe (no token → 401)
check "p5-push-unsubscribe-no-token-401" "401" "$(scode -X DELETE $SHOPERP/api/notifications/push/subscribe)"

# P5-7: Push track (click tracking — POST, no token → 400 or 401, body validation may run first)
echo '{"notificationId":"test"}' > /tmp/track.json
check_in "p5-push-track-no-token" "401|400" "$(scode -X POST -H 'Content-Type: application/json' -d @/tmp/track.json $SHOPERP/api/notifications/push/track)"

# P5-8: Campaigns public endpoint (Gateway controller — access via Gateway IP)
check "p5-campaigns-public-200" "200" "$(scode $GATEWAY/api/campaigns/by-tenant/$TENANT)"

# ============================================================
# SECTION 3: LOYALTY-A — Configurable Loyalty Formula
# ============================================================
echo
echo "=== SECTION 3: Loyalty-A — Configurable Formula ==="

# L-A-1: GET /api/loyalty/my (no token → 401)
check "la-loyalty-my-no-token-401" "401" "$(scode $SHOPERP/api/loyalty/my)"

# L-A-2: ShopFeatures settings page (admin — correct path /settings/shop-features)
check_in "la-shopfeatures-admin-302-or-200" "302|200" "$(scode $SHOPERP/settings/shop-features)"

# L-A-3: Loyalty config in appsettings (verify via Gateway appsettings.json)
GW_APPSETTINGS=$(docker exec vanan-gateway cat /app/appsettings.json 2>/dev/null)
check_contains "la-appsettings-has-loyalty-section" "LoyaltyPoints" "$GW_APPSETTINGS"
check_contains "la-appsettings-has-points-rate" "PointsRate" "$GW_APPSETTINGS"

# L-A-4: ShopERP appsettings also has loyalty config
SE_APPSETTINGS=$(docker exec vanan-shoperp cat /app/appsettings.json 2>/dev/null)
check_contains "la-shoperp-appsettings-has-loyalty" "LoyaltyPoints" "$SE_APPSETTINGS"

# ============================================================
# SECTION 4: LOYALTY-B — Redemption System
# ============================================================
echo
echo "=== SECTION 4: Loyalty-B — Redemption System ==="

# L-B-1: KhachLink /rewards page
check "lb-khachlink-rewards-200" "200" "$(scode $KHACHLINK/rewards)"

# L-B-2: GET /api/redemption/catalog/active (public catalog)
CATALOG_BODY=$(scurl "$SHOPERP/api/redemption/catalog/active")
check_contains "lb-catalog-has-items" "productName" "$CATALOG_BODY"
echo "  Catalog items: $(echo $CATALOG_BODY | python3 -c 'import json,sys; d=json.load(sys.stdin); print(len(d))' 2>/dev/null || echo '?')"

# L-B-3: GET /api/redemption/catalog (admin — 302 without cookie)
check_in "lb-catalog-admin-auth" "401|302" "$(scode $SHOPERP/api/redemption/catalog)"

# L-B-4: GET /api/redemption/my/redemptions (no token → 401)
check "lb-my-redemptions-no-token-401" "401" "$(scode $SHOPERP/api/redemption/my/redemptions)"

# L-B-5: GET /api/redemption/my/vouchers (no token → 401)
check "lb-my-vouchers-no-token-401" "401" "$(scode $SHOPERP/api/redemption/my/vouchers)"

# L-B-6: POST /api/redemption/redeem (no token → 401)
echo '{"catalogItemId":"00000000-0000-0000-0000-000000000001"}' > /tmp/redeem.json
check "lb-redeem-no-token-401" "401" "$(scode -X POST -H 'Content-Type: application/json' -d @/tmp/redeem.json $SHOPERP/api/redemption/redeem)"

# L-B-7: POST /api/redemption/fulfill (admin — 302 without cookie)
echo '{"voucherCode":"TEST123"}' > /tmp/fulfill.json
check_in "lb-fulfill-admin-auth" "401|302" "$(scode -X POST -H 'Content-Type: application/json' -d @/tmp/fulfill.json $SHOPERP/api/redemption/fulfill)"

# L-B-8: ShopERP /admin/redemption-catalog page
check_in "lb-admin-redemption-catalog-page" "302|200" "$(scode $SHOPERP/admin/redemption-catalog)"

# L-B-9: ShopERP /admin/redemption-history page
check_in "lb-admin-redemption-history-page" "302|200" "$(scode $SHOPERP/admin/redemption-history)"

# L-B-10: DB migration — catalog table exists (API returned items)
check_contains "lb-db-catalog-has-real-data" "Ca phe" "$CATALOG_BODY"

# ============================================================
# SECTION 5: LOYALTY-C — Gamification + Config UI + Notifications
# ============================================================
echo
echo "=== SECTION 5: Loyalty-C — Gamification + Config + Notifications ==="

# L-C-WS-B-1: KhachLink /missions page (NEW)
check "lc-khachlink-missions-200" "200" "$(scode $KHACHLINK/missions)"

# L-C-WS-B-2: GET /api/missions/active (public)
MISSIONS_BODY=$(scurl "$SHOPERP/api/missions/active")
check_contains "lc-missions-active-valid-json" "\[" "$MISSIONS_BODY"
echo "  Missions active: $MISSIONS_BODY"

# L-C-WS-B-3: GET /api/missions/my/progress (no token → 401)
check "lc-missions-progress-no-token-401" "401" "$(scode $SHOPERP/api/missions/my/progress)"

# L-C-WS-B-4: GET /api/missions/my/completions (no token → 401)
check "lc-missions-completions-no-token-401" "401" "$(scode $SHOPERP/api/missions/my/completions)"

# L-C-WS-B-5: GET /api/missions (admin — 302 without cookie)
check "lc-missions-admin-302" "302" "$(scode $SHOPERP/api/missions)"

# L-C-WS-B-6: ShopERP /admin/missions page (NEW)
check_in "lc-admin-missions-page" "302|200" "$(scode $SHOPERP/admin/missions)"

# L-C-WS-B-7: POST /api/customer-profile/birthday (no token → 401)
echo '{"Birthday":"1990-01-01"}' > /tmp/bday.json
check "lc-customer-profile-birthday-401" "401" "$(scode -X POST -H 'Content-Type: application/json' -d @/tmp/bday.json $SHOPERP/api/customer-profile/birthday)"

# L-C-WS-B-8: POST /api/customer-profile/pwa-installed (no token → 401)
check "lc-customer-profile-pwa-installed-401" "401" "$(scode -X POST $SHOPERP/api/customer-profile/pwa-installed)"

# L-C-WS-B-9: POST /api/customer-profile/share (no token → 401)
echo '{"ShareUrl":"https://facebook.com/test"}' > /tmp/share.json
check "lc-customer-profile-share-401" "401" "$(scode -X POST -H 'Content-Type: application/json' -d @/tmp/share.json $SHOPERP/api/customer-profile/share)"

# L-C-WS-B-10: DB migration — Missions table exists
if echo "$MISSIONS_BODY" | grep -qE '^\[.*\]$'; then
  PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] lc-db-missions-table-exists — valid JSON array\n"
else
  FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] lc-db-missions-table-exists — unexpected: $MISSIONS_BODY\n"
fi

# ============================================================
# SECTION 6: NAVIGATION + SITEMAP
# ============================================================
echo
echo "=== SECTION 6: Navigation + Sitemap ==="
# Sitemap is an admin page — 302 redirect to login is correct without cookie
check_in "sitemap-page-302-or-200" "302|200" "$(scode $SHOPERP/sitemap)"

# ============================================================
# SECTION 7: REGRESSION — Core Endpoints
# ============================================================
echo
echo "=== SECTION 7: Regression — Core Endpoints ==="
check "health-200" "200" "$(scode $GATEWAY/health)"
check "store-info-200" "200" "$(scode $GATEWAY/api/tenants/$TENANT/store-info)"
check "catalog-recommended-200" "200" "$(scode "$GATEWAY/api/catalog/recommended?tenantId=$TENANT")"
check "khachlink-home-200" "200" "$(scode $KHACHLINK/)"
check "khachlink-stores-200" "200" "$(scode $KHACHLINK/stores)"
check "khachlink-cart-200" "200" "$(scode $KHACHLINK/cart)"
check "khachlink-login-200" "200" "$(scode $KHACHLINK/login)"
check "khachlink-my-loyalty-200" "200" "$(scode $KHACHLINK/my-loyalty)"

# ============================================================
# SECTION 8: ERROR LOGS
# ============================================================
echo
echo "=== SECTION 8: Error Logs ==="
GW_ERR=$(docker logs vanan-gateway --since 10m 2>&1 | grep -iE "error|exception" | grep -vi "OutboxMessages" | grep -v 'SELECT' | grep -vi "ECONNRESET" | grep -vi "Connection refused" | grep -vi "WebSocket" | head -5)
if [ -z "$GW_ERR" ]; then
  PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] gateway-no-recent-errors\n"
else
  FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] gateway-errors: ${GW_ERR}\n"
fi

SE_ERR=$(docker logs vanan-shoperp --since 10m 2>&1 | grep -iE "error|exception" | grep -vi "OutboxMessages" | grep -v 'SELECT' | grep -vi "warn" | head -5)
if [ -z "$SE_ERR" ]; then
  PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] shoperp-no-recent-errors\n"
else
  FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] shoperp-errors: ${SE_ERR}\n"
fi

KL_ERR=$(docker logs vanan-khachlink --since 10m 2>&1 | grep -iE "error|exception" | grep -vi "warn" | grep -vi "ECONNRESET" | head -5)
if [ -z "$KL_ERR" ]; then
  PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] khachlink-no-recent-errors\n"
else
  FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] khachlink-errors: ${KL_ERR}\n"
fi

# ============================================================
# SECTION 9: HOSTED SERVICES (L-C Jobs)
# ============================================================
echo
echo "=== SECTION 9: Hosted Services (L-C Jobs) ==="
BIRTHDAY_JOB_LOG=$(docker logs vanan-shoperp 2>&1 | grep -i "BirthdayBonusJob" | head -2)
if [ -n "$BIRTHDAY_JOB_LOG" ]; then
  PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] lc-birthday-bonus-job-started\n"
else
  FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] lc-birthday-bonus-job-not-found\n"
fi

VOUCHER_JOB_LOG=$(docker logs vanan-shoperp 2>&1 | grep -i "VoucherExpiryReminderJob" | head -2)
if [ -n "$VOUCHER_JOB_LOG" ]; then
  PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] lc-voucher-expiry-job-started\n"
else
  FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] lc-voucher-expiry-job-not-found\n"
fi

# ============================================================
# SUMMARY
# ============================================================
echo
echo "============================================"
echo "COMPREHENSIVE RV v2 RESULTS: $PASS PASS / $FAIL FAIL"
echo "============================================"
echo
echo -e "$RESULTS"

echo
echo "=== BREAKDOWN BY PHASE ==="
P5_P=$(echo -e "$RESULTS" | grep -c '\[PASS\] p5-')
P5_F=$(echo -e "$RESULTS" | grep -c '\[FAIL\] p5-')
LA_P=$(echo -e "$RESULTS" | grep -c '\[PASS\] la-')
LA_F=$(echo -e "$RESULTS" | grep -c '\[FAIL\] la-')
LB_P=$(echo -e "$RESULTS" | grep -c '\[PASS\] lb-')
LB_F=$(echo -e "$RESULTS" | grep -c '\[FAIL\] lb-')
LC_P=$(echo -e "$RESULTS" | grep -c '\[PASS\] lc-')
LC_F=$(echo -e "$RESULTS" | grep -c '\[FAIL\] lc-')
echo "Phase 5 (Push + PWA):      $P5_P PASS / $P5_F FAIL"
echo "Loyalty-A (Formula):       $LA_P PASS / $LA_F FAIL"
echo "Loyalty-B (Redemption):    $LB_P PASS / $LB_F FAIL"
echo "Loyalty-C (Gamification):  $LC_P PASS / $LC_F FAIL"

[ $FAIL -gt 0 ] && exit 1 || exit 0
