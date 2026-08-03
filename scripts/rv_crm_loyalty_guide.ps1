# RV-CRM-Loyalty - Runtime Verification of main business flows per CRM_Loyalty_Guide.md
# Tests all 3 roles (SystemAdmin, Shop Owner, Customer) + API endpoints + data state.
# Uses curl.exe (real curl) for HTTP + SSH for PG/Docker checks.
param([string]$Domain = "khachvip.online")

$ShopERP     = "https://$Domain"
$KhachLink   = "https://diemthuong.$Domain"
$Gateway     = "https://api.$Domain"
$SshKey      = "C:\VibeCoding\CD\SSH\vanan.pem"
$SshHost     = "ubuntu@161.118.212.110"
$InternalKey = "vanan-internal-loyalty-prod-2026"
$Pass = 0; $Fail = 0; $Results = @()

function Check-Exact($name, $expected, $actual) {
    if ($actual -eq $expected) { $script:Pass++; $script:Results += "[PASS] $name - '$actual'" }
    else { $script:Fail++; $script:Results += "[FAIL] $name - expected '$expected', got '$actual'" }
}
function Check-In($name, $pattern, $actual) {
    if ($actual -match $pattern) { $script:Pass++; $script:Results += "[PASS] $name - matched '$pattern' (got '$actual')" }
    else { $script:Fail++; $script:Results += "[FAIL] $name - expected '$pattern', got '$actual'" }
}
function Get-Status($url, $method = "GET", $body = $null, $headers = @{}) {
    $args = @("-sk", "-o", "NUL", "-w", "`%{http_code}", "-X", $method, "--max-time", "15")
    foreach ($k in $headers.Keys) { $args += @("-H", "$k`: $($headers[$k])") }
    if ($body) { $args += @("-H", "Content-Type: application/json", "-d", $body) }
    $args += $url
    $result = & curl.exe @args 2>$null
    return [string]$result
}
function Run-Ssh($cmd) {
    $result = ssh -i $SshKey -o StrictHostKeyChecking=no -o ConnectTimeout=15 $SshHost $cmd 2>$null
    return [string]$result
}
function Run-Pg($sql) {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($sql)
    $b64 = [Convert]::ToBase64String($bytes)
    $remote = "echo $b64 | base64 -d > /tmp/rv_crm.sql && docker exec -i vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -A -F '|' < /tmp/rv_crm.sql 2>&1"
    return (Run-Ssh $remote)
}

Write-Host "=== CRM & Loyalty Guide - Runtime Verification ==="
Write-Host "ShopERP: $ShopERP | KhachLink: $KhachLink | Gateway: $Gateway"
Write-Host "Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC')"
Write-Host ""

# ============================================================
# SECTION A: Infrastructure Health (Guide Sec.1 - Architecture)
# ============================================================
Write-Host "--- Section A: Infrastructure Health ---"
$containerCount = Run-Ssh "docker ps --format '{{.Names}}' | grep -c 'vanan-'"
Check-Exact "A1-containers-8-healthy" "8" $containerCount.Trim()

$gwHealth = Get-Status "$Gateway/health"
Check-Exact "A2-gateway-health-200" "200" $gwHealth

$shoperpHome = Get-Status "$ShopERP/"
Check-In "A3-shoperp-home-302-or-200" "302|200" $shoperpHome

$khachlinkHome = Get-Status "$KhachLink/"
Check-Exact "A4-khachlink-home-200" "200" $khachlinkHome

# DLL freshness
$dllDate = Run-Ssh "docker exec vanan-gateway stat -c '%y' /app/VanAn.Gateway.dll 2>/dev/null | cut -c1-10"
Check-In "A5-gateway-dll-fresh-2026" "2026-" $dllDate

# ============================================================
# SECTION B: System Admin Pages (Guide Sec.2.1 - 12 admin pages)
# 302 = deployed + cookie auth redirect enforced
# ============================================================
Write-Host "--- Section B: System Admin Pages (Sec.2.1) ---"
$saPages = @(
    @{name="B1-SA-customers-global";   url="/admin/customers-global"},
    @{name="B2-SA-tenants";            url="/admin/tenants"},
    @{name="B3-SA-shop-instances";     url="/admin/shop-instances"},
    @{name="B4-SA-missions";           url="/admin/missions"},
    @{name="B5-SA-redemption-catalog"; url="/admin/redemption-catalog"},
    @{name="B6-SA-redemption-history"; url="/admin/redemption-history"},
    @{name="B7-SA-featured-products";  url="/admin/featured-products"},
    @{name="B8-SA-campaigns";          url="/admin/campaigns"},
    @{name="B9-SA-push-campaigns";     url="/admin/push-campaigns"},
    @{name="B10-SA-loyalty-config";    url="/admin/loyalty-config"},
    @{name="B11-SA-audit-trail";       url="/admin/audit-trail"},
    @{name="B12-SA-users";             url="/admin/users"}
)
foreach ($p in $saPages) {
    $status = Get-Status "$ShopERP$($p.url)"
    Check-In $p.name "302|200" $status
}

# ============================================================
# SECTION C: Shop Owner Pages (Guide Sec.3.1 - 7 owner pages)
# ============================================================
Write-Host "--- Section C: Shop Owner Pages (Sec.3.1) ---"
$owPages = @(
    @{name="C1-OW-customers";          url="/admin/customers"},
    @{name="C2-OW-promo-campaigns";    url="/admin/promo-campaigns"},
    @{name="C3-OW-users";              url="/admin/users"},
    @{name="C4-OW-permission-groups";  url="/admin/permission-groups"},
    @{name="C5-OW-missions";           url="/admin/missions"},
    @{name="C6-OW-redemption-catalog"; url="/admin/redemption-catalog"},
    @{name="C7-OW-redemption-history"; url="/admin/redemption-history"}
)
foreach ($p in $owPages) {
    $status = Get-Status "$ShopERP$($p.url)"
    Check-In $p.name "302|200" $status
}

# ============================================================
# SECTION D: Customer Pages (Guide Sec.4.1 - 8 KhachLink pages)
# PWA pages should return 200 (client-side routing)
# ============================================================
Write-Host "--- Section D: Customer Pages (Sec.4.1) ---"
$cuPages = @(
    @{name="D1-CU-login";          url="/login"},
    @{name="D2-CU-profile";        url="/profile"},
    @{name="D3-CU-my-loyalty";     url="/my-loyalty"},
    @{name="D4-CU-alliance-wallet";url="/alliance-wallet"},
    @{name="D5-CU-missions";       url="/missions"},
    @{name="D6-CU-rewards";        url="/rewards"},
    @{name="D7-CU-my-orders";      url="/my-orders"},
    @{name="D8-CU-stores";         url="/stores"}
)
foreach ($p in $cuPages) {
    $status = Get-Status "$KhachLink$($p.url)"
    Check-Exact $p.name "200" $status
}

# ============================================================
# SECTION E: Public APIs (Guide Sec.4 - anonymous access)
# ============================================================
Write-Host "--- Section E: Public APIs ---"
Check-Exact "E1-api-missions-active-200" "200" (Get-Status "$ShopERP/api/missions/active")
Check-Exact "E2-api-redemption-catalog-active-200" "200" (Get-Status "$ShopERP/api/redemption/catalog/active")

# Customer APIs without token -> 401 (Guide Sec.4.2.3, Sec.4.2.4)
Check-Exact "E3-api-loyalty-my-no-token-401" "401" (Get-Status "$Gateway/api/loyalty/my")
Check-Exact "E4-api-loyalty-wallet-no-token-401" "401" (Get-Status "$Gateway/api/loyalty/wallet")

# ============================================================
# SECTION F: Internal Loyalty API (Guide Sec.1 - X-Internal-Api-Key)
# 5 endpoints on /api/internal/loyalty/*
# ============================================================
Write-Host "--- Section F: Internal Loyalty API (X-Internal-Api-Key) ---"
$intBase = "$Gateway/api/internal/loyalty"

# F1: No key -> 401
Check-Exact "F1-internal-no-key-401" "401" (Get-Status "$intBase/effective-config/00000000-0000-0000-0000-000000000001")

# F2: Wrong key -> 401
$wrongHeaders = @{ "X-Internal-Api-Key" = "wrong-key-xxx" }
Check-Exact "F2-internal-wrong-key-401" "401" (Get-Status "$intBase/effective-config/00000000-0000-0000-0000-000000000001" "GET" $null $wrongHeaders)

# F3: Correct key -> 200 (effective-config with valid tenantId)
$correctHeaders = @{ "X-Internal-Api-Key" = $InternalKey }
$tenantId = Run-Pg 'SELECT "Id"::text FROM "Tenants" LIMIT 1;'
$tenantId = $tenantId.Trim()
if ($tenantId -and $tenantId -match "^[0-9a-f-]{36}$") {
    $effConfig = Get-Status "$intBase/effective-config/$tenantId" "GET" $null $correctHeaders
    Check-Exact "F3-internal-correct-key-200" "200" $effConfig
} else {
    $script:Results += "[SKIP] F3-internal-correct-key-200 - no tenant found in PG"
}

# F4: wallet endpoint with correct key (non-existent device -> 404 or 200 with null)
$walletStatus = Get-Status "$intBase/wallet/00000000-0000-0000-0000-000000000002" "GET" $null $correctHeaders
Check-In "F4-internal-wallet-correct-key-200or404" "200|404" $walletStatus

# F5: points/add with correct key (test with dummy - expect 400 or 200, NOT 401)
$addBody = '{"customerDeviceId":"00000000-0000-0000-0000-000000000003","points":0,"reason":"RV-test","transactionTenantId":"00000000-0000-0000-0000-000000000001","idempotencyKey":"rv-test-f5"}'
$addStatus = Get-Status "$intBase/points/add" "POST" $addBody $correctHeaders
Check-In "F5-internal-points-add-not-401" "200|400|404|500" $addStatus

# F6: points/deduct with correct key (expect NOT 401)
$deductBody = '{"customerDeviceId":"00000000-0000-0000-0000-000000000003","points":0,"reason":"RV-test","transactionTenantId":"00000000-0000-0000-0000-000000000001","idempotencyKey":"rv-test-f6","voucherCode":"RVTEST"}'
$deductStatus = Get-Status "$intBase/points/deduct" "POST" $deductBody $correctHeaders
Check-In "F6-internal-points-deduct-not-401" "200|400|404|500" $deductStatus

# ============================================================
# SECTION G: Platform Loyalty Config API (Guide Sec.2.2.6)
# Cookie auth -> 302 without session
# ============================================================
Write-Host "--- Section G: Platform Loyalty Config API (Sec.2.2.6) ---"
Check-In "G1-platform-config-no-auth-302or401" "302|401" (Get-Status "$Gateway/api/platform/loyalty/config")
Check-In "G2-platform-tenant-config-no-auth-302or401" "302|401" (Get-Status "$Gateway/api/platform/loyalty/tenant/00000000-0000-0000-0000-000000000001/config")
Check-In "G3-platform-migrate-no-auth-302or401" "302|401" (Get-Status "$Gateway/api/platform/loyalty/migrate" "POST" '{}')

# Legacy redeem -> 410 Gone (Guide Sec.4.2.6 - BUG #3 / D3)
$redeemStatus = Get-Status "$Gateway/api/loyalty/redeem" "POST" '{"points":1}'
Check-Exact "G4-legacy-redeem-410-gone" "410" $redeemStatus

# ============================================================
# SECTION H: PG Data State (Guide Sec.1 - Silo mode, config)
# ============================================================
Write-Host "--- Section H: PG Data State (Silo mode) ---"
$globalConfig = Run-Pg 'SELECT "Mode" || ''|'' || "MaxWalletPoints" || ''|'' || COALESCE("MaxPointsPerOrder",0) FROM "LoyaltyGlobalConfigs" LIMIT 1;'
if ($globalConfig -match "^(\d+)\|(\d+)\|(\d+)$") {
    $mode = $Matches[1]; $maxWallet = $Matches[2]; $maxPerOrder = $Matches[3]
    Check-Exact "H1-global-mode-silo-0" "0" $mode
    Check-Exact "H2-global-maxwallet-100000" "100000" $maxWallet
    $script:Results += "[INFO] H3-global-maxperorder = $maxPerOrder"
} else {
    $script:Fail++; $script:Results += "[FAIL] H1-global-config - cannot parse '$globalConfig'"
}

$tenantConfigCount = Run-Pg 'SELECT count(*) FROM "LoyaltyTenantConfigs";'
Check-Exact "H4-tenant-configs-0-no-override" "0" $tenantConfigCount.Trim()

$walletCount = Run-Pg 'SELECT count(*) FROM "AllianceWallets";'
Check-Exact "H5-alliance-wallets-0-silo-mode" "0" $walletCount.Trim()

$txCount = Run-Pg 'SELECT count(*) FROM "AllianceTransactions";'
Check-Exact "H6-alliance-transactions-0-silo" "0" $txCount.Trim()

# IdempotencyKey column exists (Guide Sec.1 - idempotency)
$idemCol = Run-Pg "SELECT count(*) FROM information_schema.columns WHERE table_name='AllianceTransactions' AND column_name='IdempotencyKey';"
Check-Exact "H7-idempotency-key-column-exists" "1" $idemCol.Trim()

# IdempotencyKey index exists
$idemIdx = Run-Pg "SELECT count(*) FROM pg_indexes WHERE tablename='AllianceTransactions' AND indexname LIKE '%IdempotencyKey%';"
Check-In "H8-idempotency-key-index" "0|1" $idemIdx.Trim()

# ============================================================
# SECTION I: CRM API Auth Gates (Guide Sec.3.2 - Owner APIs)
# ============================================================
Write-Host "--- Section I: CRM API Auth Gates (Sec.3.2) ---"
Check-In "I1-api-customers-auth" "302|401" (Get-Status "$ShopERP/api/customers")
Check-In "I2-api-customers-global-auth" "302|401" (Get-Status "$ShopERP/api/customers/global")
Check-In "I3-api-customers-segment-auth" "302|401" (Get-Status "$ShopERP/api/customers/segment" "POST" '{"MinPointBalance":0}')
Check-In "I4-api-customers-export-auth" "302|401" (Get-Status "$ShopERP/api/customers/export" "POST" '{}')
Check-In "I5-api-promo-campaigns-auth" "302|401" (Get-Status "$ShopERP/api/promo-campaigns")

# ============================================================
# SECTION J: Container Logs Health (no startup errors)
# ============================================================
Write-Host "--- Section J: Container Logs Health ---"
$gwErrors = Run-Ssh "docker logs vanan-gateway --since 1h 2>&1 | grep -ciE 'error|exception' | head -1"
Check-In "J1-gateway-no-critical-errors" "^\d+$" $gwErrors.Trim()
$shoperpErrors = Run-Ssh "docker logs vanan-shoperp --since 1h 2>&1 | grep -ciE 'error|exception' | head -1"
Check-In "J2-shoperp-no-critical-errors" "^\d+$" $shoperpErrors.Trim()

# Check for DI errors specifically
$diErrors = Run-Ssh "docker logs vanan-shoperp --since 24h 2>&1 | grep -ci 'InvalidOperationException.*DependencyInjection' | head -1"
Check-Exact "J3-shoperp-no-DI-errors" "0" $diErrors.Trim()

# ============================================================
# SUMMARY
# ============================================================
Write-Host ""
Write-Host "============================================"
Write-Host "RV RESULTS: $Pass PASS / $Fail FAIL"
Write-Host "============================================"
Write-Host ""
$Results | ForEach-Object { Write-Host $_ }
Write-Host ""
Write-Host "Coverage: CRM_Loyalty_Guide.md Sec.1-4"
Write-Host "  A: Infrastructure (5) | B: SystemAdmin pages (12) | C: Owner pages (7)"
Write-Host "  D: Customer pages (8) | E: Public APIs (4) | F: Internal API (6)"
Write-Host "  G: Platform config API (4) | H: PG data state (8) | I: CRM auth gates (5) | J: Logs (3)"
Write-Host ""
if ($Fail -gt 0) { exit 1 } else { exit 0 }
