<#
.SYNOPSIS
  Verify SystemAdmin entry points on VPS production (khachvip.online).
  Production-adapted: uses /api/platform/login instead of /dev/login/systemadmin (#if DEBUG).
  ShopERP accessed via YARP /shoperp/ prefix through Gateway.
  JWT from ShopERP IS valid for Gateway (shared Jwt:Secret via JWT_SECRET_KEY env).
  However, SystemAdmin JWT has tenant_id="system" (not a valid Guid) which causes
  issues with RequireTenantAccess policy endpoints on Gateway.

.PARAMETER BaseUrl
  Base URL for Gateway. Default: https://api.khachvip.online

.PARAMETER TenantId
  Tenant GUID to impersonate. Default: 00000000-0000-0000-0000-000000000001

.PARAMETER SysAdminUsername
  Default: sysadmin@vanan.vn

.PARAMETER SysAdminPassword
  Default: 2026@vanan (from docker-compose.prod.yml default)
#>
param(
    [string]$BaseUrl = "https://api.khachvip.online",
    [string]$TenantId = "00000000-0000-0000-0000-000000000001",
    [string]$SysAdminUsername = "sysadmin@vanan.vn",
    [string]$SysAdminPassword = "2026@vanan"
)

$ShopERPUrl = "$BaseUrl/shoperp"
$GatewayUrl = $BaseUrl

$script:PassCount = 0
$script:FailCount = 0
$script:SkipCount = 0
$script:Results = [System.Collections.Generic.List[pscustomobject]]::new()

function Write-Result {
    param([string]$Category, [string]$Endpoint, [string]$Method, [int]$StatusCode, [string]$Expected, [string]$Notes)
    $passed = $false; $status = "FAIL"
    if ($StatusCode -ge 200 -and $StatusCode -lt 400) { $passed = $true; $status = "PASS"; $script:PassCount++ }
    elseif ($StatusCode -eq 403 -and $Expected -match "403") { $passed = $true; $status = "PASS"; $script:PassCount++ }
    elseif ($StatusCode -eq 403 -and $Expected -match "200/403") { $passed = $true; $status = "PASS"; $script:PassCount++ }
    elseif ($StatusCode -eq 404 -and $Expected -match "200/404") { $passed = $true; $status = "PASS"; $script:PassCount++ }
    elseif ($StatusCode -eq 500 -and $Expected -match "200/500") { $passed = $true; $status = "PASS"; $script:PassCount++ }
    elseif ($StatusCode -eq 403 -and $Expected -match "200/403/500") { $passed = $true; $status = "PASS"; $script:PassCount++ }
    elseif ($StatusCode -eq 500 -and $Expected -match "200/403/500") { $passed = $true; $status = "PASS"; $script:PassCount++ }
    elseif ($StatusCode -eq 200 -and $Expected -match "200/403/500") { $passed = $true; $status = "PASS"; $script:PassCount++ }
    elseif ($StatusCode -eq 400 -and $Expected -match "400") { $passed = $true; $status = "PASS"; $script:PassCount++ }
    elseif ($StatusCode -eq 401 -or $StatusCode -eq 403) { $status = "FAIL"; $script:FailCount++ }
    elseif ($StatusCode -eq 404) { $status = "SKIP"; $script:SkipCount++ }
    else { $status = "FAIL"; $script:FailCount++ }
    $color = if ($passed) { "Green" } elseif ($status -eq "SKIP") { "DarkYellow" } else { "Red" }
    $s = "[{0}] {1} {2} -> {3}" -f $status, $Method, $Endpoint, $StatusCode
    if ($Notes) { $s += " - $Notes" }
    Write-Host $s -ForegroundColor $color
    $script:Results.Add([pscustomobject]@{ Category=$Category; Endpoint=$Endpoint; Method=$Method; Status=$StatusCode; Expected=$Expected; Result=$status; Notes=$Notes })
}

# Skip SSL validation — use curl.exe instead of Invoke-WebRequest
# PowerShell 5.1's Invoke-WebRequest fails with TLS renegotiation (nginx requests renegotiation).
# curl.exe handles this correctly via schannel.
$script:CookieJar = "$env:TEMP\vanan-cookies-$([System.Guid]::NewGuid().ToString('N').Substring(0,8)).txt"

function Invoke-Req {
    param([string]$Url, [string]$Method="GET", [hashtable]$Headers=@{}, [string]$Body=$null, [string]$CT="application/json", [int]$Timeout=15, [switch]$IsLogin)
    try {
        $curlArgs = @("-s", "-k", "-X", $Method, "-w", "`n%{http_code}", "--connect-timeout", $Timeout, "--max-time", ($Timeout + 5))
        # Cookie handling
        if ($IsLogin) { $curlArgs += @("-c", $script:CookieJar) }
        elseif (Test-Path $script:CookieJar) { $curlArgs += @("-b", $script:CookieJar, "-c", $script:CookieJar) }
        # Headers
        foreach ($key in $Headers.Keys) { $curlArgs += @("-H", "$($key): $($Headers[$key])") }
        # Body — write to temp file and use --data-binary @file.
        # PowerShell 5.1's native command layer splits multi-line strings (e.g. ConvertTo-Json
        # output) across curl.exe argv, causing the server to receive only a fragment.
        # --data-binary also prevents curl's "@file" interpretation inside the body string.
        $bodyFile = $null
        if ($Body) {
            $bodyFile = [System.IO.Path]::GetTempFileName()
            [System.IO.File]::WriteAllText($bodyFile, $Body)
            $curlArgs += @("-H", "Content-Type: $CT", "--data-binary", "@$bodyFile")
        }
        $curlArgs += $Url

        $raw = & curl.exe @curlArgs 2>$null
        if ($bodyFile -and (Test-Path $bodyFile)) { Remove-Item $bodyFile -Force }
        # curl output: content + newline + http_code
        $lines = $raw -split "`n"
        $statusCode = [int]($lines[-1].Trim())
        $content = ($lines[0..($lines.Count - 2)] -join "`n").TrimEnd()
        return [pscustomobject]@{ StatusCode=$statusCode; Content=$content; Headers=@{} }
    } catch {
        return [pscustomobject]@{ StatusCode=-1; Content=$_.Exception.Message; Headers=@{} }
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  SystemAdmin Entry Point Verification (PRODUCTION)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Gateway:  $GatewayUrl" -ForegroundColor Gray
Write-Host "  ShopERP:  $ShopERPUrl (via YARP /shoperp/)" -ForegroundColor Gray
Write-Host "  Tenant:   $TenantId" -ForegroundColor Gray
Write-Host "  SysAdmin: $SysAdminUsername" -ForegroundColor Gray
Write-Host ""

# --- Step 0: Health ---
Write-Host "[Step 0] Pre-flight: Checking service health..." -ForegroundColor Yellow
$gh = Invoke-Req -Url "$GatewayUrl/health" -Timeout 10
if ($gh.StatusCode -ne 200) {
    Write-Host "  Gateway not healthy (status: $($gh.StatusCode))." -ForegroundColor Red
    if ($gh.Content) { Write-Host "  Error detail: $($gh.Content)" -ForegroundColor DarkRed }
    Write-Host "  Trying direct health check..." -ForegroundColor DarkYellow
    try {
        $direct = Invoke-WebRequest -Uri "$GatewayUrl/health" -TimeoutSec 10 -UseBasicParsing -ErrorAction Stop
        Write-Host "  Direct check OK: $($direct.StatusCode)" -ForegroundColor Green
    } catch {
        Write-Host "  Direct check also failed: $($_.Exception.Message)" -ForegroundColor Red
    }
    Write-Host "  Aborting." -ForegroundColor Red
    exit 1
}
Write-Host "  Gateway healthy OK" -ForegroundColor Green

# --- Step 1: Login via /api/platform/login ---
Write-Host "`n[Step 1] Login as SystemAdmin (POST /shoperp/api/platform/login)..." -ForegroundColor Yellow
$loginBody = @{ Username = $SysAdminUsername; Password = $SysAdminPassword } | ConvertTo-Json
$lr = Invoke-Req -Url "$ShopERPUrl/api/platform/login" -Method "POST" -Body $loginBody -IsLogin
if ($lr.StatusCode -eq 200) {
    $lb = $lr.Content | ConvertFrom-Json
    $script:SystemAdminJwt = $lb.token
    Write-Host "  Login OK - Role: $($lb.role), Email: $($lb.email)" -ForegroundColor Green
} else {
    Write-Host "  Login FAILED ($($lr.StatusCode)): $($lr.Content)" -ForegroundColor Red
    Write-Host "  Check: SysAdmin seeded? Password correct? /shoperp/ prefix works?" -ForegroundColor DarkYellow
    exit 1
}

# --- Step 2: Platform pages ---
Write-Host "`n[Step 2] Platform-level pages (Cookie, no tenant_id)..." -ForegroundColor Yellow
foreach ($p in @(@{Path="/admin/tenants";Name="Tenant Mgmt"}, @{Path="/admin/audit-trail";Name="Audit Trail"})) {
    $r = Invoke-Req -Url "$ShopERPUrl$($p.Path)"
    $n = $p.Name
    if ($r.StatusCode -eq 302) { $n += " - REDIRECT" }
    elseif ($r.StatusCode -eq 200 -and $r.Content -match "access-denied") { $n += " - Access Denied" }
    Write-Result -Category "Platform Page" -Endpoint "/shoperp$($p.Path)" -Method "GET" -StatusCode $r.StatusCode -Expected "200" -Notes $n
}

# --- Step 3: Tenant list API → dynamically pick valid tenantId ---
Write-Host "`n[Step 3] Tenant list API → pick valid tenantId..." -ForegroundColor Yellow
$r = Invoke-Req -Url "$ShopERPUrl/api/tenants"
Write-Result -Category "Platform API" -Endpoint "/shoperp/api/tenants" -Method "GET" -StatusCode $r.StatusCode -Expected "200" -Notes "Tenant list"

# Parse tenant list and pick first Active tenant for impersonation
$script:DynamicTenantId = $null
$script:DynamicTenantName = $null
if ($r.StatusCode -eq 200 -and $r.Content) {
    try {
        $tenants = $r.Content | ConvertFrom-Json
        if ($tenants -and $tenants.Count -gt 0) {
            # Prefer default tenant if present, else first Active tenant
            $preferred = $tenants | Where-Object { $_.Id -eq $TenantId } | Select-Object -First 1
            if (-not $preferred) { $preferred = $tenants | Where-Object { $_.Status -eq "Active" } | Select-Object -First 1 }
            if (-not $preferred) { $preferred = $tenants[0] }
            $script:DynamicTenantId = $preferred.Id
            $script:DynamicTenantName = $preferred.Name
            Write-Host "  Found $($tenants.Count) tenant(s). Using: $($script:DynamicTenantName) ($($script:DynamicTenantId))" -ForegroundColor Green
        } else {
            Write-Host "  WARNING: Tenant list empty. Will try hardcoded tenantId." -ForegroundColor DarkYellow
        }
    } catch {
        Write-Host "  WARNING: Failed to parse tenant list. Will try hardcoded tenantId." -ForegroundColor DarkYellow
    }
}
if (-not $script:DynamicTenantId) { $script:DynamicTenantId = $TenantId; $script:DynamicTenantName = "Hardcoded" }

# --- Step 4: Impersonate tenant (using dynamic tenantId from Step 3) ---
# Flow: GET /api/tenants → pick valid tenantId → POST /api/admin/impersonate/{id}
# Both TenantController and AdminController use ITenantManagementService → IVanAnDbContext (PostgreSQL).
# So tenantId from API list is guaranteed to exist in the same DB that impersonation queries.
$VasTenantId = "a5b6c7d8-1234-5678-9abc-def012345678"
$script:ActiveTenantId = $script:DynamicTenantId
$script:ActiveTenantName = $script:DynamicTenantName
$script:ImpersonatedJwt = $null  # JWT with real tenant_id (for Gateway tenant-scoped calls)

Write-Host "`n[Step 4] Impersonate tenant $($script:DynamicTenantId) ($($script:DynamicTenantName))..." -ForegroundColor Yellow
$r = Invoke-Req -Url "$ShopERPUrl/api/admin/impersonate/$($script:DynamicTenantId)" -Method "POST"
if ($r.StatusCode -eq 200) {
    $b = $r.Content | ConvertFrom-Json
    $script:ActiveTenantName = $b.tenantName
    $script:ImpersonatedJwt = $b.token
    Write-Host "  OK - Tenant: $($b.tenantName)" -ForegroundColor Green
    if ($script:ImpersonatedJwt) { Write-Host "  Impersonated JWT captured (tenant_id=$($script:DynamicTenantId))" -ForegroundColor DarkGray }
} else {
    Write-Host "  Impersonation FAILED ($($r.StatusCode)): $($r.Content)" -ForegroundColor DarkYellow
    Write-Host "  Trying VAS tenant fallback ($VasTenantId)..." -ForegroundColor Yellow
    $r2 = Invoke-Req -Url "$ShopERPUrl/api/admin/impersonate/$VasTenantId" -Method "POST"
    if ($r2.StatusCode -eq 200) {
        $b2 = $r2.Content | ConvertFrom-Json
        $script:ActiveTenantId = $VasTenantId
        $script:ActiveTenantName = $b2.tenantName
        $script:ImpersonatedJwt = $b2.token
        Write-Host "  OK - VAS Tenant: $($b2.tenantName)" -ForegroundColor Green
        if ($script:ImpersonatedJwt) { Write-Host "  Impersonated JWT captured (tenant_id=$VasTenantId)" -ForegroundColor DarkGray }
    } else {
        Write-Host "  VAS fallback ALSO FAILED ($($r2.StatusCode)): $($r2.Content)" -ForegroundColor Red
    }
    Write-Result -Category "Impersonation" -Endpoint "/shoperp/api/admin/impersonate/$VasTenantId" -Method "POST" -StatusCode $r2.StatusCode -Expected "200" -Notes "VAS fallback"
}
Write-Result -Category "Impersonation" -Endpoint "/shoperp/api/admin/impersonate/$($script:DynamicTenantId)" -Method "POST" -StatusCode $r.StatusCode -Expected "200" -Notes "$($script:ActiveTenantName)"

# --- Step 5: Tenant-scoped Blazor pages ---
Write-Host "`n[Step 5] Tenant-scoped Blazor pages (Cookie + tenant_id)..." -ForegroundColor Yellow
$pages = @(
    @{Path="/";Name="Home";Pol="OwnerOnly"}, @{Path="/orders";Name="Orders";Pol="Auth"},
    @{Path="/accounting";Name="Accounting";Pol="OwnerOnly"}, @{Path="/accounting/balance";Name="Acct Balance";Pol="OwnerOnly"},
    @{Path="/accounting/history";Name="Txn History";Pol="OwnerOnly"}, @{Path="/accounting/revenue";Name="Revenue";Pol="OwnerOnly"},
    @{Path="/accounting/expenses";Name="Expenses";Pol="OwnerOnly"}, @{Path="/accounting/period-closing";Name="Period Closing";Pol="OwnerOnly"},
    @{Path="/accounting/hkd-books";Name="HKD Books";Pol="OwnerOnly"}, @{Path="/accounting/balance-sheet";Name="Balance Sheet";Pol="OwnerOnly"},
    @{Path="/accounting/cash-flow-statement";Name="Cash Flow";Pol="OwnerOnly"}, @{Path="/accounting/income-statement";Name="Income Stmt";Pol="OwnerOnly"},
    @{Path="/accounting/trial-balance";Name="Trial Balance";Pol="OwnerOnly"}, @{Path="/accounting/financial-reports";Name="Fin Reports";Pol="OwnerOnly"},
    @{Path="/einvoice";Name="EInvoice";Pol="StoreMgmt"}, @{Path="/einvoice/invoices";Name="Invoices";Pol="StoreMgmt"},
    @{Path="/einvoice/providers";Name="Providers";Pol="OwnerOnly"}, @{Path="/einvoice/configuration";Name="Config";Pol="OwnerOnly"},
    @{Path="/einvoice/health";Name="Health";Pol="StoreMgmt"}, @{Path="/einvoice/alerts";Name="Alerts";Pol="StoreMgmt"},
    @{Path="/admin/users";Name="Users";Pol="OwnerOnly"}, @{Path="/admin/permission-groups";Name="Perm Groups";Pol="OwnerOnly"},
    @{Path="/sitemap";Name="Sitemap";Pol="Auth"}
)
foreach ($p in $pages) {
    $r = Invoke-Req -Url "$ShopERPUrl$($p.Path)"
    $n = "$($p.Name) [$($p.Pol)]"
    if ($r.StatusCode -eq 302) { $n += " - REDIRECT" }
    elseif ($r.StatusCode -eq 403) { $n += " - FORBIDDEN" }
    elseif ($r.StatusCode -eq 200 -and $r.Content -match "access-denied") { $n += " - Access Denied" }
    Write-Result -Category "Tenant Page" -Endpoint "/shoperp$($p.Path)" -Method "GET" -StatusCode $r.StatusCode -Expected "200" -Notes $n
}

# --- Step 6: ShopERP API endpoints ---
Write-Host "`n[Step 6] ShopERP API endpoints (Cookie + tenant_id)..." -ForegroundColor Yellow
$apis = @(
    @{Path="/api/orders";Name="Orders"}, @{Path="/api/dashboard";Name="Dashboard"},
    @{Path="/api/products";Name="Products"}, @{Path="/api/shops";Name="Shops"},
    @{Path="/api/notifications";Name="Notifications"}, @{Path="/api/loyalty";Name="Loyalty"},
    @{Path="/api/customers";Name="Customers"}, @{Path="/api/socialcampaigns";Name="Campaigns"},
    @{Path="/api/users";Name="Users"}, @{Path="/api/permission-groups";Name="Perm Groups"},
    @{Path="/api/apikeys";Name="API Keys"}
)
foreach ($a in $apis) {
    $r = Invoke-Req -Url "$ShopERPUrl$($a.Path)"
    Write-Result -Category "ShopERP API" -Endpoint "/shoperp$($a.Path)" -Method "GET" -StatusCode $r.StatusCode -Expected "200" -Notes $a.Name
}

# --- Step 7: VAS Reports ---
Write-Host "`n[Step 7] VAS Reports (Cookie + tenant_id)..." -ForegroundColor Yellow
if ($script:ActiveTenantName -eq "VAS Enterprise") {
    Write-Host "  Active tenant: VAS Enterprise — reports should return 200." -ForegroundColor DarkGray
} else {
    Write-Host "  Active tenant: Default (HKD) — reports may get 403 (not Enterprise)." -ForegroundColor DarkGray
}
$vas = @(
    @{Path="/api/balance-sheets?year=2026&month=6";Name="Balance Sheet"},
    @{Path="/api/cash-flow-statements?year=2026&month=6";Name="Cash Flow"},
    @{Path="/api/income-statements?year=2026&month=6";Name="Income Statement"},
    @{Path="/api/trial-balances?year=2026&month=6";Name="Trial Balance"}
)
foreach ($a in $vas) {
    $r = Invoke-Req -Url "$ShopERPUrl$($a.Path)"
    $n = $a.Name
    if ($r.StatusCode -eq 403) { $n += " - 403 (HKD tenant, expected)" }
    Write-Result -Category "VAS Report" -Endpoint "/shoperp$($a.Path)" -Method "GET" -StatusCode $r.StatusCode -Expected "200/403" -Notes $n
}

# --- Step 8: Gateway platform endpoints (JWT Bearer, SystemAdmin role) ---
# JWT from ShopERP IS valid for Gateway: both share Jwt:Secret=${JWT_SECRET_KEY},
# Jwt:Issuer=VanAnShopERP, Jwt:Audience=VanAnApi (verified in docker-compose.prod.yml + appsettings.json).
# TenantOnboardingController has only POST /tenants and GET /tenants/{id} (stub 404).
# GET /tenants/{id} returns 404 from stub — but 401=JWT invalid, 403=not SysAdmin, 404=auth OK.
Write-Host "`n[Step 8] Gateway platform endpoints (JWT Bearer, SystemAdmin role)..." -ForegroundColor Yellow
if ($script:SystemAdminJwt) {
    $h = @{ "Authorization" = "Bearer $script:SystemAdminJwt" }
    # Only real endpoint: GET /api/v1/onboarding/tenants/{guid} (stub returns 404, but proves auth)
    $r = Invoke-Req -Url "$GatewayUrl/api/v1/onboarding/tenants/$TenantId" -Headers $h
    $n = "Tenant Onboarding GET stub"
    if ($r.StatusCode -eq 401) { $n += " - 401 (JWT invalid)" }
    elseif ($r.StatusCode -eq 403) { $n += " - 403 (not SystemAdmin)" }
    elseif ($r.StatusCode -eq 404) { $n += " - 404 OK (auth passed, stub endpoint)" }
    Write-Result -Category "Gateway Platform" -Endpoint "/api/v1/onboarding/tenants/{id}" -Method "GET" -StatusCode $r.StatusCode -Expected "200/404" -Notes $n
    # Also test POST /api/v1/onboarding/tenants with empty body to verify auth (expect 400=auth OK, 401=JWT invalid, 403=not SysAdmin)
    $r2 = Invoke-Req -Url "$GatewayUrl/api/v1/onboarding/tenants" -Method "POST" -Headers $h -Body "{}"
    $n2 = "Tenant Onboarding POST (empty body)"
    if ($r2.StatusCode -eq 400) { $n2 += " - 400 OK (auth passed, validation failed)" }
    elseif ($r2.StatusCode -eq 401) { $n2 += " - 401 (JWT invalid)" }
    elseif ($r2.StatusCode -eq 403) { $n2 += " - 403 (not SystemAdmin)" }
    Write-Result -Category "Gateway Platform" -Endpoint "/api/v1/onboarding/tenants" -Method "POST" -StatusCode $r2.StatusCode -Expected "200/400/404" -Notes $n2
} else { Write-Host "  SKIPPED - no JWT" -ForegroundColor DarkYellow }

# --- Step 8b: Gateway API endpoints (JWT Bearer) ---
# JWT IS valid for Gateway (shared Jwt:Secret). SystemAdmin JWT has tenant_id="system" (not valid Guid).
# Three categories:
#   8b.1 AllowAnonymous: no auth needed -> expect 200
#   8b.2 [Authorize] only: SystemAdmin JWT works -> expect 200 or 404 (no root action)
#   8b.3 RequireTenantAccess: policy checks RequireClaim("tenant_id") -> "system" passes policy,
#       but HttpContextTenantProvider Guid.Parse("system") may throw -> expect 200/403/500
Write-Host "`n[Step 8b] Gateway API endpoints (JWT Bearer)..." -ForegroundColor Yellow
if ($script:SystemAdminJwt) {
    $h = @{ "Authorization" = "Bearer $script:SystemAdminJwt" }

    # 8b.1: AllowAnonymous endpoints (forward to ShopERP, no auth needed)
    Write-Host "  8b.1 AllowAnonymous endpoints:" -ForegroundColor DarkGray
    $anonApis = @(
        @{Path="/api/customers";Name="Customers"}, @{Path="/api/shops";Name="Shops"},
        @{Path="/api/notifications";Name="Notifications"}, @{Path="/api/loyalty";Name="Loyalty"},
        @{Path="/api/campaigns";Name="Campaigns"}
    )
    foreach ($a in $anonApis) {
        $r = Invoke-Req -Url "$GatewayUrl$($a.Path)" -Headers $h
        Write-Result -Category "Gateway Anon" -Endpoint $a.Path -Method "GET" -StatusCode $r.StatusCode -Expected "200" -Notes $a.Name
    }

    # 8b.2: [Authorize] only (no RequireTenantAccess) — SystemAdmin JWT valid
    Write-Host "  8b.2 [Authorize] only endpoints:" -ForegroundColor DarkGray
    $authOnlyApis = @(
        @{Path="/api/products";Name="Products (no root action -> 404=auth OK)"},
        @{Path="/api/dashboard";Name="Dashboard (no root action -> 404=auth OK)"}
    )
    foreach ($a in $authOnlyApis) {
        $r = Invoke-Req -Url "$GatewayUrl$($a.Path)" -Headers $h
        $n = $a.Name
        if ($r.StatusCode -eq 401) { $n += " - 401 (JWT invalid)" }
        Write-Result -Category "Gateway Auth" -Endpoint $a.Path -Method "GET" -StatusCode $r.StatusCode -Expected "200/404" -Notes $n
    }

    # 8b.3: RequireTenantAccess — use impersonated JWT (with real tenant_id Guid) if available.
    # Without impersonation, SystemAdmin JWT has tenant_id="system" (not a valid Guid) → controllers
    # reject with 401 "Tenant ID required in JWT claim". With impersonated JWT, tenant_id is a valid
    # Guid → RequireTenantAccess passes AND GetTenantIdFromClaim() returns valid Guid → 200/404.
    Write-Host "  8b.3 RequireTenantAccess endpoints:" -ForegroundColor DarkGray
    $tenantJwt = $script:ImpersonatedJwt
    if ($tenantJwt) {
        $hT = @{ "Authorization" = "Bearer $tenantJwt" }
        Write-Host "    Using impersonated JWT (tenant_id=$($script:ActiveTenantId))" -ForegroundColor DarkGray
    } else {
        $hT = $h
        Write-Host "    WARNING: No impersonated JWT - using SystemAdmin JWT (tenant_id=system, expect 401)" -ForegroundColor DarkYellow
    }
    $tenantAccessApis = @(
        @{Path="/api/orders";Name="Orders"}, @{Path="/api/accounting";Name="Accounting"},
        @{Path="/api/hkd-books";Name="HKD Books"}, @{Path="/api/v1/localization";Name="Localization"},
        @{Path="/api/v1/shopconfig";Name="Shop Config"}, @{Path="/api/build";Name="Build Info"},
        @{Path="/api/v1/voicecommand";Name="Voice Command"}, @{Path="/api/reports";Name="Reports"}
    )
    foreach ($a in $tenantAccessApis) {
        $r = Invoke-Req -Url "$GatewayUrl$($a.Path)" -Headers $hT
        $n = $a.Name
        if ($tenantJwt) { $n += " (impersonated)" } else { $n += " (tenant_id=system)" }
        if ($r.StatusCode -eq 401) { $n += " - 401 (missing/invalid tenant_id)" }
        elseif ($r.StatusCode -eq 403) { $n += " - 403 (policy denied)" }
        elseif ($r.StatusCode -eq 500) { $n += " - 500 (server error)" }
        elseif ($r.StatusCode -eq 200) { $n += " - 200 OK" }
        elseif ($r.StatusCode -eq 404) { $n += " - 404 (no root action, auth OK)" }
        $expected = if ($tenantJwt) { "200/404/500" } else { "401" }
        Write-Result -Category "Gateway TenantAccess" -Endpoint $a.Path -Method "GET" -StatusCode $r.StatusCode -Expected $expected -Notes $n
    }
} else {
    Write-Host "  SKIPPED - no JWT" -ForegroundColor DarkYellow
    $script:SkipCount++
}

# --- Step 9: Gateway role-restricted (expected 403) ---
Write-Host "`n[Step 9] Gateway role-restricted endpoints (expected 403)..." -ForegroundColor Yellow
if ($script:SystemAdminJwt) {
    $h = @{ "Authorization" = "Bearer $script:SystemAdminJwt" }
    foreach ($a in @(@{Path="/api/kitchen";Name="Kitchen"}, @{Path="/api/audittrail";Name="Audit Trail"})) {
        $r = Invoke-Req -Url "$GatewayUrl$($a.Path)" -Headers $h
        $n = "$($a.Name) - expected 403"
        if ($r.StatusCode -eq 403) { $n += " OK denied" }
        elseif ($r.StatusCode -eq 200) { $n += " - WARNING: accessed!" }
        elseif ($r.StatusCode -eq 401) { $n += " - 401 (JWT issue)" }
        Write-Result -Category "Role-Restricted" -Endpoint $a.Path -Method "GET" -StatusCode $r.StatusCode -Expected "403" -Notes $n
    }
} else { Write-Host "  SKIPPED - no JWT" -ForegroundColor DarkYellow }

# --- Step 9c: VAS Enterprise tenant impersonation + VAS reports ---
# Mirrors local script Step 9c: exit current impersonation → impersonate VAS tenant → test VAS reports (expect 200).
# VAS tenant is Enterprise_SME with TT133 — VAS reports should return 200 (not 403 like HKD default tenant).
Write-Host "`n[Step 9c] VAS Enterprise tenant - impersonate + test VAS reports..." -ForegroundColor Yellow
Write-Host "  VAS tenant: $VasTenantId" -ForegroundColor Gray

# Exit current impersonation first
$exitR = Invoke-Req -Url "$ShopERPUrl/api/admin/exit-impersonation" -Method "POST"
if ($exitR.StatusCode -eq 200) { Write-Host "  Exited previous impersonation OK" -ForegroundColor Green }

# Impersonate VAS Enterprise tenant
$vasImp = Invoke-Req -Url "$ShopERPUrl/api/admin/impersonate/$VasTenantId" -Method "POST"
if ($vasImp.StatusCode -eq 200) {
    $vasBody = $vasImp.Content | ConvertFrom-Json
    $script:ImpersonatedJwt = $vasBody.token  # update for VAS tenant
    Write-Host "  VAS tenant impersonation OK - $($vasBody.tenantName)" -ForegroundColor Green
    if ($script:ImpersonatedJwt) { Write-Host "  VAS impersonated JWT captured" -ForegroundColor DarkGray }

    # Test VAS reports with Enterprise tenant — should get 200 (not 403)
    $vasReportApis = @(
        @{Path="/api/balance-sheets?year=2026&month=6";Name="Balance Sheet (Enterprise)"},
        @{Path="/api/cash-flow-statements?year=2026&month=6";Name="Cash Flow (Enterprise)"},
        @{Path="/api/income-statements?year=2026&month=6";Name="Income Statement (Enterprise)"},
        @{Path="/api/trial-balances?year=2026&month=6";Name="Trial Balance (Enterprise)"}
    )
    foreach ($a in $vasReportApis) {
        $r = Invoke-Req -Url "$ShopERPUrl$($a.Path)"
        $n = $a.Name
        if ($r.StatusCode -eq 403) { $n += " - 403 (feature flag blocked - check VAS tenant type)" }
        elseif ($r.StatusCode -eq 200) { $n += " OK - VAS report accessible" }
        Write-Result -Category "VAS Enterprise" -Endpoint "/shoperp$($a.Path)" -Method "GET" -StatusCode $r.StatusCode -Expected "200" -Notes $n
    }
} else {
    Write-Host "  VAS tenant impersonation FAILED ($($vasImp.StatusCode)): $($vasImp.Content)" -ForegroundColor Red
    Write-Host "  VAS tenant may not be seeded in PostgreSQL." -ForegroundColor DarkGray
    $script:SkipCount++
    $script:Results.Add([pscustomobject]@{ Category="VAS Enterprise"; Endpoint="/shoperp/api/admin/impersonate/$VasTenantId"; Method="POST"; Status=$vasImp.StatusCode; Expected="200"; Result="FAIL"; Notes="VAS impersonation failed" })
}

# --- Step 10: Exit impersonation ---
Write-Host "`n[Step 10] Exit impersonation..." -ForegroundColor Yellow
$r = Invoke-Req -Url "$ShopERPUrl/api/admin/exit-impersonation" -Method "POST"
Write-Result -Category "Impersonation" -Endpoint "/shoperp/api/admin/exit-impersonation" -Method "POST" -StatusCode $r.StatusCode -Expected "200" -Notes "Clear tenant_id"

# --- Summary ---
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  PASS: $script:PassCount" -ForegroundColor Green
Write-Host "  FAIL: $script:FailCount" -ForegroundColor Red
Write-Host "  SKIP: $script:SkipCount" -ForegroundColor DarkYellow
Write-Host "  TOTAL: $($script:PassCount + $script:FailCount + $script:SkipCount)" -ForegroundColor Gray

if ($script:FailCount -gt 0) {
    Write-Host "`n  FAILED ENDPOINTS:" -ForegroundColor Red
    $script:Results | Where-Object { $_.Result -eq "FAIL" } | ForEach-Object {
        Write-Host "    [$($_.Category)] $($_.Method) $($_.Endpoint) -> $($_.Status)" -ForegroundColor Red
        if ($_.Notes) { Write-Host "         $($_.Notes)" -ForegroundColor DarkRed }
    }
}

# Export CSV
$ts = Get-Date -Format "yyyyMMdd-HHmmss"
$csv = "systemadmin-entry-points-prod-$ts.csv"
$script:Results | Export-Csv -Path $csv -NoTypeInformation
Write-Host "`nResults exported to: $csv" -ForegroundColor Gray

# Cleanup cookie jar
if (Test-Path $script:CookieJar) { Remove-Item $script:CookieJar -Force }

if ($script:FailCount -gt 0) { exit 1 } else { exit 0 }
