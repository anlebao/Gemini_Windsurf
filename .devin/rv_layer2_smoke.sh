#!/bin/bash
# ============================================================
# RV LAYER 2 SMOKE — Loyalty Consistency Fix BUG #1-#9
# Run ON VPS: bash rv_layer2_smoke.sh
# Verifies: deployment + internal API auth + 410 Gone + mode-aware endpoints
#           + PG migration (IdempotencyKey) + config + DI (container health)
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
scurl() { local r=$(curl -sk "$@"); sleep 1; echo "$r"; }
scode() { local c=$(curl -sk -o /dev/null -w "%{http_code}" "$@"); sleep 1; echo "$c"; }

SHOPERP="https://khachvip.online"
KHACHLINK="https://diemthuong.khachvip.online"
TENANT="21cbf14f-581a-48c8-8ad6-becc21064535"
GW_IP=$(docker inspect vanan-gateway --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' 2>/dev/null)
GATEWAY="http://$GW_IP"
# Read internal API key from Gateway container env var (set by docker-compose.prod.yml)
# appsettings.Production.json has a ${...} placeholder — actual key comes from env var InternalLoyalty__ApiKey
INTERNAL_KEY=$(docker exec vanan-gateway env 2>/dev/null | grep '^InternalLoyalty__ApiKey=' | cut -d= -f2-)
# Trim any whitespace
INTERNAL_KEY=$(echo "$INTERNAL_KEY" | tr -d '[:space:]')

echo "=== RV LAYER 2 SMOKE — Loyalty Consistency Fix BUG #1-#9 ==="
echo "  Gateway IP: $GW_IP"
KEY_PREVIEW=$(echo "$INTERNAL_KEY" | cut -c1-6)
echo "  Internal Key: ${KEY_PREVIEW}... (truncated)"
echo

# ============================================================
# SECTION 1: DEPLOYMENT + CONTAINER HEALTH (DI verification)
# ============================================================
echo "=== SECTION 1: Container Health + DLL Freshness ==="
for c in vanan-gateway vanan-shoperp vanan-khachlink vanan-postgres vanan-nats vanan-nginx vanan-seq; do
  status=$(docker inspect --format '{{.State.Health.Status}}' $c 2>/dev/null)
  [ -z "$status" ] && status=$(docker inspect --format '{{.State.Status}}' $c 2>/dev/null)
  check_in "container-$c" "healthy|running" "$status"
done

SE_DLL_TIME=$(docker exec vanan-shoperp stat -c %y /app/VanAn.ShopERP.dll 2>/dev/null | cut -d. -f1)
GW_DLL_TIME=$(docker exec vanan-gateway stat -c %y /app/VanAn.Gateway.dll 2>/dev/null | cut -d. -f1)
echo "  ShopERP DLL: $SE_DLL_TIME | Gateway DLL: $GW_DLL_TIME"
# Today is 2026-08-02 (CD just deployed ~5 min ago)
check_in "shoperp-dll-fresh" "2026-08-02" "$SE_DLL_TIME"
check_in "gateway-dll-fresh" "2026-08-02" "$GW_DLL_TIME"

# ============================================================
# SECTION 2: CONFIG — InternalLoyalty:ApiKey present
# ============================================================
echo
echo "=== SECTION 2: Config — InternalLoyalty:ApiKey ==="
GW_APPSETTINGS=$(docker exec vanan-gateway cat /app/appsettings.json 2>/dev/null)
GW_APP_PROD=$(docker exec vanan-gateway cat /app/appsettings.Production.json 2>/dev/null)
SE_APPSETTINGS=$(docker exec vanan-shoperp cat /app/appsettings.json 2>/dev/null)
SE_APP_PROD=$(docker exec vanan-shoperp cat /app/appsettings.Production.json 2>/dev/null)

check_contains "gw-appsettings-has-InternalLoyalty" "InternalLoyalty" "$GW_APPSETTINGS"
check_contains "gw-appsettings-has-ApiKey" "\"ApiKey\"" "$GW_APPSETTINGS"
check_contains "gw-appsettings-prod-has-InternalLoyalty" "InternalLoyalty" "$GW_APP_PROD"
check_contains "se-appsettings-has-Gateway-BaseUrl" "Gateway" "$SE_APPSETTINGS"
check_contains "se-appsettings-has-InternalLoyalty-ApiKey" "InternalLoyalty" "$SE_APPSETTINGS"
check_contains "se-appsettings-prod-has-Gateway-BaseUrl" "Gateway" "$SE_APP_PROD"

# ============================================================
# SECTION 3: PG MIGRATION — IdempotencyKey column on AllianceTransactions
# ============================================================
echo
echo "=== SECTION 3: PG Migration — IdempotencyKey column ==="
# Pattern #9: PostgreSQL uses quoted PascalCase for EF migration history
MIG_CHECK=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c \
  'SELECT "MigrationId" FROM "__EFMigrationsHistory" WHERE "MigrationId" LIKE '"'"'%IdempotencyKey%'"'"';' 2>/dev/null | tr -d ' ')
check_in "pg-migration-idempotency-applied" "AddAllianceTransactionIdempotencyKey" "$MIG_CHECK"

COL_CHECK=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c \
  "SELECT column_name FROM information_schema.columns WHERE table_name='AllianceTransactions' AND column_name='IdempotencyKey';" 2>/dev/null | tr -d ' ')
check "pg-column-IdempotencyKey-exists" "IdempotencyKey" "$COL_CHECK"

IDX_CHECK=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c \
  "SELECT indexname FROM pg_indexes WHERE tablename='AllianceTransactions' AND indexname LIKE '%IdempotencyKey%';" 2>/dev/null | tr -d ' ')
check_in "pg-index-IdempotencyKey-exists" "IdempotencyKey" "$IDX_CHECK"

# ============================================================
# SECTION 4: INTERNAL API AUTH (Phase 0 — Layer 1 infra)
# ============================================================
echo
echo "=== SECTION 4: Internal API Auth (InternalApiKey filter) ==="

# 4a: Without X-Internal-Api-Key → 401
check "internal-no-key-401" "401" "$(scode $GATEWAY/api/internal/loyalty/effective-config/$TENANT)"

# 4b: With wrong key → 401
check "internal-wrong-key-401" "401" "$(scode -H "X-Internal-Api-Key: wrong-key" $GATEWAY/api/internal/loyalty/effective-config/$TENANT)"

# 4c: With correct key → 200 + valid JSON
if [ -n "$INTERNAL_KEY" ]; then
  CONFIG_BODY=$(scurl -H "X-Internal-Api-Key: $INTERNAL_KEY" $GATEWAY/api/internal/loyalty/effective-config/$TENANT)
  CONFIG_CODE=$(curl -sk -o /dev/null -w "%{http_code}" -H "X-Internal-Api-Key: $INTERNAL_KEY" $GATEWAY/api/internal/loyalty/effective-config/$TENANT)
  check "internal-correct-key-200" "200" "$CONFIG_CODE"
  check_contains "internal-config-has-mode" "\"mode\"" "$CONFIG_BODY"
  check_contains "internal-config-has-isAllianceMember" "isAllianceMember" "$CONFIG_BODY"
  echo "  Effective config: $CONFIG_BODY"
else
  FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] internal-key-not-found-in-appsettings — cannot test 4c\n"
fi

# 4d: GET wallet with key → 200 (non-existent device → balance 0)
if [ -n "$INTERNAL_KEY" ]; then
  WALLET_BODY=$(scurl -H "X-Internal-Api-Key: $INTERNAL_KEY" $GATEWAY/api/internal/loyalty/wallet/00000000-0000-0000-0000-000000000001)
  WALLET_CODE=$(curl -sk -o /dev/null -w "%{http_code}" -H "X-Internal-Api-Key: $INTERNAL_KEY" $GATEWAY/api/internal/loyalty/wallet/00000000-0000-0000-0000-000000000001)
  check "internal-wallet-200" "200" "$WALLET_CODE"
  check_contains "internal-wallet-has-totalPointBalance" "totalPointBalance" "$WALLET_BODY"
  echo "  Wallet response: $WALLET_BODY"
fi

# ============================================================
# SECTION 5: BUG #3 — /api/loyalty/redeem returns 410 Gone
# ============================================================
echo
echo "=== SECTION 5: BUG #3 — Legacy /api/loyalty/redeem → 410 Gone ==="
echo '{"catalogItemId":"00000000-0000-0000-0000-000000000001"}' > /tmp/redeem_legacy.json
REDEEM_CODE=$(scode -X POST -H "Content-Type: application/json" -d @/tmp/redeem_legacy.json $SHOPERP/api/loyalty/redeem)
check "bug3-legacy-redeem-410-gone" "410" "$REDEEM_CODE"

# ============================================================
# SECTION 6: BUG #4/#5 — /api/loyalty/my mode-aware (still 401 without token)
# ============================================================
echo
echo "=== SECTION 6: BUG #4/#5 — /api/loyalty/my mode-aware (auth gate intact) ==="
check "bug4-loyalty-my-no-token-401" "401" "$(scode $SHOPERP/api/loyalty/my)"

# ============================================================
# SECTION 7: BUG #7 — /api/customers/me mode-aware (auth gate intact)
# ============================================================
echo
echo "=== SECTION 7: BUG #7 — /api/customers/me mode-aware (auth gate intact) ==="
check "bug7-customer-identity-me-no-token-401" "401" "$(scode $SHOPERP/api/customer-identity/me)"

# ============================================================
# SECTION 8: BUG #8 — /api/customers admin list (auth gate intact)
# ============================================================
echo
echo "=== SECTION 8: BUG #8 — /api/customers admin list (auth gate intact) ==="
check_in "bug8-customers-list-no-cookie" "401|302" "$(scode $SHOPERP/api/customers)"

# ============================================================
# SECTION 9: KhachLink pages still load (no regression from Layer 2)
# ============================================================
echo
echo "=== SECTION 9: KhachLink pages load (no regression) ==="
check "kl-profile-200" "200" "$(scode $KHACHLINK/profile)"
check "kl-rewards-200" "200" "$(scode $KHACHLINK/rewards)"
check "kl-missions-200" "200" "$(scode $KHACHLINK/missions)"

# ============================================================
# SECTION 10: ShopERP admin pages still load (no regression)
# ============================================================
echo
echo "=== SECTION 10: ShopERP admin pages (no regression) ==="
check_in "se-admin-redemption-catalog" "302|200" "$(scode $SHOPERP/admin/redemption-catalog)"
check_in "se-admin-redemption-history" "302|200" "$(scode $SHOPERP/admin/redemption-history)"
check_in "se-settings-shop-features" "302|200" "$(scode $SHOPERP/settings/shop-features)"

# ============================================================
# SECTION 11: ShopERP logs — no DI errors on startup (Layer 2 wiring)
# ============================================================
echo
echo "=== SECTION 11: ShopERP startup logs — no DI errors ==="
SE_LOGS=$(docker logs vanan-shoperp --since 10m 2>&1 | tail -100)
DI_ERRORS=$(echo "$SE_LOGS" | grep -cE "InvalidOperationException|Unable to resolve service|Unable to find.*constructor" 2>/dev/null)
[ -z "$DI_ERRORS" ] && DI_ERRORS=0
check "se-no-DI-errors-on-startup" "0" "$DI_ERRORS"
GW_LOGS=$(docker logs vanan-gateway --since 10m 2>&1 | tail -100)
GW_DI_ERRORS=$(echo "$GW_LOGS" | grep -cE "InvalidOperationException|Unable to resolve service" 2>/dev/null)
[ -z "$GW_DI_ERRORS" ] && GW_DI_ERRORS=0
check "gw-no-DI-errors-on-startup" "0" "$GW_DI_ERRORS"

# ============================================================
# SUMMARY
# ============================================================
echo
echo "========================================"
echo "  RV LAYER 2 SMOKE SUMMARY"
echo "========================================"
printf "$RESULTS"
echo
echo "  PASS: $PASS | FAIL: $FAIL"
echo "========================================"
[ "$FAIL" -eq 0 ] && echo "  ✅ ALL PASSED — Layer 2 bug fixes verified on VPS" || echo "  ❌ FAILURES — investigate above"
exit $FAIL
