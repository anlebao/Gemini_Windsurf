#!/bin/bash
# ============================================================
# Sprint 0 Community Commerce Foundation — Runtime Verification
# Verifies: container health, DLL freshness, PG migration applied,
#           11 Community tables, 8 Order columns, public endpoints
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

TODAY="2026-07-26"
SHOPERP="https://khachvip.online"
KHACHLINK="https://diemthuong.khachvip.online"
GW_IP=$(docker inspect vanan-gateway --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
GATEWAY="http://$GW_IP"

echo "=== Sprint 0 Community Foundation RV — $TODAY ==="
echo

# ============================================================
# SECTION 1: CONTAINER HEALTH + DLL FRESHNESS
# ============================================================
echo "=== SECTION 1: Container Health + DLL Freshness ==="
for c in vanan-gateway vanan-shoperp vanan-khachlink vanan-postgres vanan-nats vanan-nginx vanan-seq; do
  status=$(docker inspect --format '{{.State.Health.Status}}' $c 2>/dev/null)
  [ -z "$status" ] && status=$(docker inspect --format '{{.State.Status}}' $c 2>/dev/null)
  check_in "container-$c" "healthy|running" "$status"
done

SE_DLL_TIME=$(docker exec vanan-shoperp stat -c %y /app/VanAn.ShopERP.dll 2>/dev/null | cut -d. -f1)
GW_DLL_TIME=$(docker exec vanan-gateway stat -c %y /app/VanAn.Gateway.dll 2>/dev/null | cut -d. -f1)
KL_DLL_TIME=$(docker exec vanan-khachlink stat -c %y /app/VanAn.KhachLink.dll 2>/dev/null | cut -d. -f1)
echo "  ShopERP DLL: $SE_DLL_TIME | Gateway DLL: $GW_DLL_TIME | KhachLink DLL: $KL_DLL_TIME"
check "shoperp-dll-deployed-today" "1" "$(echo $SE_DLL_TIME | grep -c "$TODAY")"
check "gateway-dll-deployed-today" "1" "$(echo $GW_DLL_TIME | grep -c "$TODAY")"
check "khachlink-dll-deployed-today" "1" "$(echo $KL_DLL_TIME | grep -c "$TODAY")"

# ============================================================
# SECTION 2: MIGRATION APPLIED
# ============================================================
echo
echo "=== SECTION 2: Migration Applied ==="
MIG_RESULT=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT count(*) FROM __EFMigrationsHistory WHERE MigrationId = '20260726105331_CommunitySprint0';")
check "sprint0-migration-recorded" "1" "$(echo $MIG_RESULT | tr -d '[:space:]')"

# ============================================================
# SECTION 3: 11 COMMUNITY TABLES EXIST
# ============================================================
echo
echo "=== SECTION 3: 11 Community Tables ==="
EXPECTED_TABLES="AppInstallAttributions CommunityRoles Conversations DeliveryTasks DeliveryTrackings DeviceRegistrations FraudFlags Messages ProductReferralConfigs SalesReferrals WalletTransactions"
TABLE_COUNT=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT count(*) FROM pg_tables WHERE schemaname='public' AND tablename IN ('CommunityRoles','DeliveryTasks','DeliveryTrackings','Conversations','Messages','SalesReferrals','WalletTransactions','ProductReferralConfigs','AppInstallAttributions','DeviceRegistrations','FraudFlags');")
check "community-tables-count-11" "11" "$(echo $TABLE_COUNT | tr -d '[:space:]')"

for t in $EXPECTED_TABLES; do
  EXISTS=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT count(*) FROM pg_tables WHERE schemaname='public' AND tablename='$t';")
  check "table-$t-exists" "1" "$(echo $EXISTS | tr -d '[:space:]')"
done

# ============================================================
# SECTION 4: 8 ORDER COLUMNS ADDED
# ============================================================
echo
echo "=== SECTION 4: 8 Order Community Columns ==="
EXPECTED_COLS="ShipperId SalesmanId ReferralCode ReferralProductId DeliveryLat DeliveryLng CodAmount CodCollectedAt"
COL_COUNT=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT count(*) FROM information_schema.columns WHERE table_name='Orders' AND column_name IN ('ShipperId','SalesmanId','ReferralCode','ReferralProductId','DeliveryLat','DeliveryLng','CodAmount','CodCollectedAt');")
check "order-community-columns-count-8" "8" "$(echo $COL_COUNT | tr -d '[:space:]')"

for c in $EXPECTED_COLS; do
  EXISTS=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT count(*) FROM information_schema.columns WHERE table_name='Orders' AND column_name='$c';")
  check "order-col-$c-exists" "1" "$(echo $EXISTS | tr -d '[:space:]')"
done

# ============================================================
# SECTION 5: SMOKE TESTS — PUBLIC ENDPOINTS STILL WORK
# ============================================================
echo
echo "=== SECTION 5: Smoke Tests (no regressions) ==="
sleep 5  # give containers a moment to fully bind
check "shoperp-home-200" "200" "$(curl -sk -o /dev/null -w '%{http_code}' $SHOPERP/)"
check "shoperp-health-200" "200" "$(curl -sk -o /dev/null -w '%{http_code}' $SHOPERP/health)"
check "khachlink-home-200" "200" "$(curl -sk -o /dev/null -w '%{http_code}' $KHACHLINK/)"
check "khachlink-manifest-200" "200" "$(curl -sk -o /dev/null -w '%{http_code}' $KHACHLINK/manifest.json)"
check "khachlink-service-worker-200" "200" "$(curl -sk -o /dev/null -w '%{http_code}' $KHACHLINK/service-worker.js)"
check "gateway-health-200" "200" "$(curl -sk -o /dev/null -w '%{http_code}' $GATEWAY/health)"

# ============================================================
# SECTION 6: FINGERPRINTJS REAL LIBRARY DEPLOYED (KhachLink) — F1 fix
# ============================================================
echo
echo "=== SECTION 6: FingerprintJS v5.2.0 Real Library (KhachLink) ==="
FPJS_BODY=$(curl -sk $KHACHLINK/lib/fingerprintjs/fingerprint.js)
# Real FingerprintJS v5.2.0 (MIT) — header contains "FingerprintJS v5.2.0" + "MIT License"
check_contains "fpjs-real-library-served" "FingerprintJS v5.2.0" "$FPJS_BODY"
check_contains "fpjs-mit-license" "MIT License" "$FPJS_BODY"
# Stub had "STUB" marker — real library must NOT contain it
if echo "$FPJS_BODY" | grep -q "STUB"; then
  FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] fpjs-no-stub-marker — found 'STUB' in body\n"
else
  PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] fpjs-no-stub-marker — no 'STUB' marker (real library)\n"
fi
FPJS_WRAPPER_BODY=$(curl -sk $KHACHLINK/js/fingerprint.js)
check_contains "fpjs-wrapper-served" "window.fingerprint" "$FPJS_WRAPPER_BODY"
check_contains "fpjs-wrapper-collect-api" "collect" "$FPJS_WRAPPER_BODY"

# ============================================================
# SUMMARY
# ============================================================
echo
echo "========================================"
echo "RV SUMMARY: PASS=$PASS  FAIL=$FAIL"
echo "========================================"
echo
printf "$RESULTS"
echo
if [ "$FAIL" -eq 0 ]; then
  echo "ALL CHECKS PASSED — Sprint 0 Foundation verified on VPS"
  exit 0
else
  echo "FAILURES DETECTED — review above"
  exit 1
fi
