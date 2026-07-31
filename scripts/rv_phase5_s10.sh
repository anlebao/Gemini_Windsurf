#!/bin/bash
# ============================================================
# RV Phase 5 S10 + SC10 + SC14 + SC22 — VPS Runtime Verification
# Verifies: SC10 (campaigns/{id}/send-push), SC14 (push endpoints live),
#           SC18-22 (S10 notification alerts — bell + vibrate prefs)
# VPS-only: endpoint + service health check (not real-device push delivery)
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
GW_IP=$(docker inspect vanan-gateway --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')
GATEWAY="http://$GW_IP"

echo "=== RV Phase 5 S10 + SC10 + SC14 + SC22 — VPS ==="
echo "Commit: a8a26f62 | Date: 2026-07-31"
echo

# ============================================================
# SECTION 1: CONTAINER HEALTH + DEPLOYMENT
# ============================================================
echo "=== SECTION 1: Container Health + Deployment ==="

# Get deployed image tag
DEPLOYED_TAG=$(docker inspect vanan-shoperp --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' 2>/dev/null || echo "unknown")
echo "Deployed ShopERP image revision: $DEPLOYED_TAG"

# Container health
for c in vanan-gateway vanan-shoperp vanan-khachlink vanan-nats vanan-postgres; do
  STATUS=$(docker inspect --format '{{.State.Status}}' $c 2>/dev/null || echo "missing")
  check "container-$c-running" "running" "$STATUS"
done

# ============================================================
# SECTION 2: SC10 — POST /api/campaigns/{id}/send-push (Gateway)
# ============================================================
echo
echo "=== SECTION 2: SC10 — Campaigns send-push endpoint (Gateway) ==="

# SC10-1: POST /api/campaigns/{id}/send-push without auth → 401
echo '{"Title":"test","Body":"test"}' > /tmp/sendpush.json
check "sc10-campaigns-send-push-no-auth-401" "401" "$(scode -X POST -H 'Content-Type: application/json' -d @/tmp/sendpush.json $GATEWAY/api/campaigns/$TENANT/send-push)"

# SC10-2: POST /api/campaigns/{id}/send-push with invalid auth → 401
check "sc10-campaigns-send-push-bad-auth-401" "401" "$(scode -X POST -H 'Content-Type: application/json' -H 'Authorization: Bearer invalid' -d @/tmp/sendpush.json $GATEWAY/api/campaigns/$TENANT/send-push)"

# SC10-3: POST /api/push/send (ShopERP) without auth → 401
check "sc10-push-send-no-auth-401" "401" "$(scode -X POST -H 'Content-Type: application/json' -d @/tmp/sendpush.json $SHOPERP/api/push/send)"

# SC10-4: GET /api/push/jobs (ShopERP) without auth → 401
check "sc10-push-jobs-no-auth-401" "401" "$(scode $SHOPERP/api/push/jobs)"

# SC10-5: GET /api/push/jobs/{id} (ShopERP) without auth → 401
check "sc10-push-jobs-detail-no-auth-401" "401" "$(scode $SHOPERP/api/push/jobs/00000000-0000-0000-0000-000000000000)"

# ============================================================
# SECTION 3: SC14 — Push endpoints live (VPS)
# ============================================================
echo
echo "=== SECTION 3: SC14 — Push endpoints live on VPS ==="

# SC14-1: Push subscribe (no token → 401)
echo '{}' > /tmp/sub.json
check "sc14-push-subscribe-no-token-401" "401" "$(scode -X POST -H 'Content-Type: application/json' -d @/tmp/sub.json $SHOPERP/api/notifications/push/subscribe)"

# SC14-2: Push unsubscribe (no token → 401)
check "sc14-push-unsubscribe-no-token-401" "401" "$(scode -X DELETE $SHOPERP/api/notifications/push/subscribe)"

# SC14-3: Push status (no token → 401)
check "sc14-push-status-no-token-401" "401" "$(scode $SHOPERP/api/notifications/push/status)"

# SC14-4: Push track (no token → 400 or 401 — body validation may run first)
echo '{"notificationId":"00000000-0000-0000-0000-000000000000"}' > /tmp/track.json
check_in "sc14-push-track-no-token" "401|400" "$(scode -X POST -H 'Content-Type: application/json' -d @/tmp/track.json $SHOPERP/api/notifications/push/track)"

# SC14-5: Push track via Gateway forward (no token → 400 or 401)
check_in "sc14-push-track-gateway-forward" "401|400" "$(scode -X POST -H 'Content-Type: application/json' -d @/tmp/track.json $GATEWAY/api/notifications/push/track)"

# SC14-6: Push subscribe via Gateway forward (no token → 401)
check "sc14-push-subscribe-gateway-forward-401" "401" "$(scode -X POST -H 'Content-Type: application/json' -d @/tmp/sub.json $GATEWAY/api/notifications/push/subscribe)"

# SC14-7: Push unsubscribe via Gateway forward (no token → 401)
check "sc14-push-unsubscribe-gateway-forward-401" "401" "$(scode -X DELETE $GATEWAY/api/notifications/push/subscribe)"

# ============================================================
# SECTION 4: SC18-22 — S10 Notification Alerts (bell + vibrate)
# ============================================================
echo
echo "=== SECTION 4: SC18-22 — S10 Notification Alerts (bell + vibrate) ==="

# SC18-19: Verify SW v16 deployed (contains prefs-driven push handler)
SW_JS=$(curl -sk $KHACHLINK/service-worker.js)
check_contains "sc18-19-sw-v16-deployed" "v16-push-alerts" "$SW_JS"
check_contains "sc18-19-sw-prefs-cache-name" "vanan-notification-prefs" "$SW_JS"
check_contains "sc18-19-sw-getNotificationPrefsFromSW" "getNotificationPrefsFromSW" "$SW_JS"
check_contains "sc18-19-sw-postMessage-play-bell" "play-bell" "$SW_JS"
check_contains "sc18-19-sw-silent-prefs-driven" "prefs.sound" "$SW_JS"

# SC20: Verify pwa.js deployed with Web Audio API bell + prefs functions
PWA_JS=$(curl -sk $KHACHLINK/js/pwa.js)
check_contains "sc20-pwa-setNotificationPrefs" "setNotificationPrefs" "$PWA_JS"
check_contains "sc20-pwa-getNotificationPrefs" "getNotificationPrefs" "$PWA_JS"
check_contains "sc20-pwa-playBellSound-webaudio" "playBellSound" "$PWA_JS"
check_contains "sc20-pwa-audiocontext-oscillator" "OscillatorNode" "$PWA_JS"
check_contains "sc20-pwa-setupBellMessageListener" "setupBellMessageListener" "$PWA_JS"

# SC21: DESCOPE — no bell.mp3 needed (Web Audio API oscillator). Verify no /sounds/bell.mp3 required.
# (If 404, that's expected — Web Audio API doesn't need the file.)
check_in "sc21-no-bell-mp3-needed-404-or-200" "404|200" "$(scode $KHACHLINK/sounds/bell.mp3)"

# SC22: Verify Profile.razor deployed with 2 toggle (sound + vibrate)
PROFILE_HTML=$(curl -sk $KHACHLINK/profile)
check_contains "sc22-profile-sound-toggle" "soundToggle" "$PROFILE_HTML"
check_contains "sc22-profile-vibrate-toggle" "vibrateToggle" "$PROFILE_HTML"
check_contains "sc22-profile-ios-limitation-note" "iOS" "$PROFILE_HTML"

# ============================================================
# SECTION 5: DLL DEPLOYMENT VERIFICATION
# ============================================================
echo
echo "=== SECTION 5: DLL Deployment Verification ==="

# Gateway DLL has CampaignsController.send-push
GW_DLL_CHECK=$(docker exec vanan-gateway strings /app/VanAn.Gateway.dll 2>/dev/null | grep -c "SendCampaignPush" || echo 0)
check_in "dll-gateway-sendCampaignPush-present" "[1-9]" "$GW_DLL_CHECK"

# Gateway DLL has SendCampaignPushRequest
GW_REQ_CHECK=$(docker exec vanan-gateway strings /app/VanAn.Gateway.dll 2>/dev/null | grep -c "SendCampaignPushRequest" || echo 0)
check_in "dll-gateway-sendCampaignPushRequest-present" "[1-9]" "$GW_REQ_CHECK"

# ShopERP DLL has PushAdminController (existing from S5)
SE_PUSH_CHECK=$(docker exec vanan-shoperp strings /app/VanAn.ShopERP.dll 2>/dev/null | grep -c "PushAdminController" || echo 0)
check_in "dll-shoperp-pushAdminController-present" "[1-9]" "$SE_PUSH_CHECK"

# ============================================================
# SECTION 6: VAPID + PUSH BACKGROUND SERVICE
# ============================================================
echo
echo "=== SECTION 6: VAPID + Push Background Service ==="

# VAPID key set in ShopERP appsettings
SE_APPSETTINGS=$(docker exec vanan-shoperp cat /app/appsettings.json 2>/dev/null)
check_contains "vapid-shoperp-has-public-key" "VapidPublicKey" "$SE_APPSETTINGS"
check_contains "vapid-shoperp-has-private-key" "VapidPrivateKey" "$SE_APPSETTINGS"

# VAPID_PRIVATE_KEY env var set in container
VAPID_ENV=$(docker exec vanan-shoperp printenv VAPID_PRIVATE_KEY 2>/dev/null || echo "")
if [ -n "$VAPID_ENV" ]; then
  PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] vapid-env-private-key-set — length ${#VAPID_ENV}\n"
else
  FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] vapid-env-private-key-set — empty\n"
fi

# PushNotificationBackgroundService running (check logs for NATS subscription)
PUSH_LOGS=$(docker logs vanan-shoperp --since 10m 2>&1 | grep -i "PushNotificationBackgroundService\|loyalty.points.changed\|subscribed" | tail -5)
if [ -n "$PUSH_LOGS" ]; then
  PASS=$((PASS+1)); RESULTS="${RESULTS}[PASS] push-background-service-active — logs found\n"
else
  FAIL=$((FAIL+1)); RESULTS="${RESULTS}[FAIL] push-background-service-active — no recent logs\n"
fi

# ============================================================
# SUMMARY
# ============================================================
echo
echo "========================================"
echo "RV SUMMARY: Phase 5 S10 + SC10 + SC14 + SC22"
echo "========================================"
echo -e "$RESULTS"
echo
echo "PASS: $PASS | FAIL: $FAIL | TOTAL: $((PASS+FAIL))"
if [ $FAIL -eq 0 ]; then
  echo "VERDICT: ALL PASS ✅"
else
  echo "VERDICT: $FAIL FAILED ❌"
fi
echo "========================================"
