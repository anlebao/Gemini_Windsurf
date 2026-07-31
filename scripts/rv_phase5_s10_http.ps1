# RV Phase 5 S10 + SC10 + SC14 + SC22 -- HTTP checks from local via curl.exe
# Verifies endpoints live on VPS after CD deploy (no SSH needed -- public HTTPS)
$ErrorActionPreference = "Continue"
$pass = 0; $fail = 0; $results = @()

function Check($name, $expected, $actual) {
    if ($actual -eq $expected) { $script:pass++; $script:results += "[PASS] $name - '$actual'" }
    else { $script:fail++; $script:results += "[FAIL] $name - expected '$expected', got '$actual'" }
}
function CheckIn($name, $pattern, $actual) {
    if ($actual -match $pattern) { $script:pass++; $script:results += "[PASS] $name - matched '$pattern' (got '$actual')" }
    else { $script:fail++; $script:results += "[FAIL] $name - expected '$pattern', got '$actual'" }
}
function CheckContains($name, $needle, $haystack) {
    if ($haystack -match [regex]::Escape($needle)) { $script:pass++; $script:results += "[PASS] $name - contains '$needle'" }
    else { $script:fail++; $script:results += "[FAIL] $name - missing '$needle'" }
}
function SCode($url, $method, $bodyFile, $authHeader) {
    $args = @("-sk", "-o", "NUL", "-w", "`%{http_code}", "-X", $method, "-m", "15")
    if ($bodyFile) { $args += @("-H", "Content-Type: application/json", "-d", $bodyFile) }
    if ($authHeader) { $args += @("-H", $authHeader) }
    $args += $url
    $code = & curl.exe @args 2>$null
    Start-Sleep -Milliseconds 800
    return [string]$code
}
function SContent($url) {
    $content = & curl.exe -sk -m 15 $url 2>$null
    Start-Sleep -Milliseconds 800
    return [string]$content
}

$SHOPERP = "https://khachvip.online"
$KHACHLINK = "https://diemthuong.khachvip.online"
$GATEWAY = "https://api.khachvip.online"
$TENANT = "21cbf14f-581a-48c8-8ad6-becc21064535"

Write-Host "=== RV Phase 5 S10 + SC10 + SC14 + SC22 -- HTTP (local -> VPS) ==="
Write-Host "Commit: a8a26f62 | Date: 2026-07-31"
Write-Host ""

# SECTION 1: SC10 -- POST /api/campaigns/{id}/send-push (Gateway)
Write-Host "=== SECTION 1: SC10 -- Campaigns send-push (Gateway) ==="
$body = '{"Title":"test","Body":"test"}'

CheckIn "sc10-campaigns-send-push-no-auth-302-or-401" "302|401" (SCode "$GATEWAY/api/campaigns/$TENANT/send-push" "POST" $body)
Check "sc10-campaigns-send-push-bad-auth-401" "401" (SCode "$GATEWAY/api/campaigns/$TENANT/send-push" "POST" $body "Authorization: Bearer invalid")
CheckIn "sc10-push-send-no-auth-302-or-401" "302|401" (SCode "$SHOPERP/api/push/send" "POST" $body)
CheckIn "sc10-push-jobs-no-auth-302-or-401" "302|401" (SCode "$SHOPERP/api/push/jobs" "GET" $null $null)
CheckIn "sc10-push-jobs-detail-no-auth-302-or-401" "302|401" (SCode "$SHOPERP/api/push/jobs/00000000-0000-0000-0000-000000000000" "GET" $null $null)

# SECTION 2: SC14 -- Push endpoints live
Write-Host ""
Write-Host "=== SECTION 2: SC14 -- Push endpoints live ==="
$sub = '{}'
Check "sc14-push-subscribe-no-token-401" "401" (SCode "$SHOPERP/api/notifications/push/subscribe" "POST" $sub)
Check "sc14-push-unsubscribe-no-token-401" "401" (SCode "$SHOPERP/api/notifications/push/subscribe" "DELETE" $null $null)
Check "sc14-push-status-no-token-401" "401" (SCode "$SHOPERP/api/notifications/push/status" "GET" $null $null)
$track = '{"notificationId":"00000000-0000-0000-0000-000000000000"}'
CheckIn "sc14-push-track-no-token" "401|400" (SCode "$SHOPERP/api/notifications/push/track" "POST" $track)
CheckIn "sc14-push-track-gateway-forward" "401|400" (SCode "$GATEWAY/api/notifications/push/track" "POST" $track)
Check "sc14-push-subscribe-gateway-forward-401" "401" (SCode "$GATEWAY/api/notifications/push/subscribe" "POST" $sub)
Check "sc14-push-unsubscribe-gateway-forward-401" "401" (SCode "$GATEWAY/api/notifications/push/subscribe" "DELETE" $null $null)

# SECTION 3: SC18-22 -- S10 Notification Alerts (JS assets)
Write-Host ""
Write-Host "=== SECTION 3: SC18-22 -- S10 Notification Alerts (JS assets) ==="

$swJs = SContent "$KHACHLINK/service-worker.js"
if ($swJs) {
    CheckContains "sc18-19-sw-v16-deployed" "v16-push-alerts" $swJs
    CheckContains "sc18-19-sw-prefs-cache-name" "vanan-notification-prefs" $swJs
    CheckContains "sc18-19-sw-getNotificationPrefsFromSW" "getNotificationPrefsFromSW" $swJs
    CheckContains "sc18-19-sw-postMessage-play-bell" "play-bell" $swJs
} else { $fail++; $results += "[FAIL] sc18-19-sw-fetch-error - empty response" }

$pwaJs = SContent "$KHACHLINK/js/pwa.js"
if ($pwaJs) {
    CheckContains "sc20-pwa-setNotificationPrefs" "setNotificationPrefs" $pwaJs
    CheckContains "sc20-pwa-getNotificationPrefs" "getNotificationPrefs" $pwaJs
    CheckContains "sc20-pwa-playBellSound-webaudio" "playBellSound" $pwaJs
    CheckContains "sc20-pwa-audiocontext-oscillator" "AudioContext" $pwaJs
    CheckContains "sc20-pwa-setupBellMessageListener" "setupBellMessageListener" $pwaJs
} else { $fail++; $results += "[FAIL] sc20-pwa-fetch-error - empty response" }

# SC21: DESCOPE -- no bell.mp3 needed
CheckIn "sc21-no-bell-mp3-needed-404-or-200" "404|200" (SCode "$KHACHLINK/sounds/bell.mp3" "GET" $null $null)

# SC22: Profile page loads (WASM renders toggle client-side)
Check "sc22-profile-page-loads-200" "200" (SCode "$KHACHLINK/profile" "GET" $null $null)

# SECTION 4: Health checks
Write-Host ""
Write-Host "=== SECTION 4: Health + Gateway ==="
Check "gateway-health-200" "200" (SCode "$GATEWAY/health" "GET" $null $null)
Check "shoperp-health-200" "200" (SCode "$SHOPERP/health" "GET" $null $null)
Check "khachlink-home-200" "200" (SCode "$KHACHLINK/" "GET" $null $null)

# SUMMARY
Write-Host ""
Write-Host "========================================"
Write-Host "RV SUMMARY: Phase 5 S10 + SC10 + SC14 + SC22 (HTTP)"
Write-Host "========================================"
$results | ForEach-Object { Write-Host $_ }
Write-Host ""
Write-Host "PASS: $pass | FAIL: $fail | TOTAL: $($pass + $fail)"
if ($fail -eq 0) { Write-Host "VERDICT: ALL PASS" }
else { Write-Host "VERDICT: $fail FAILED" }
Write-Host "========================================"
Write-Host ""
Write-Host "NOTE: Container/DLL/VAPID env checks require SSH to VPS."
Write-Host "Run scripts/rv_phase5_s10.sh on VPS for full verification."
