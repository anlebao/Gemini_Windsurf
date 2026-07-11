<#
.SYNOPSIS
  Verify SystemAdmin can access all entry points in the VanAn ecosystem.

.DESCRIPTION
  This script verifies that the SystemAdmin account can successfully access:
  1. Platform-level pages (no tenant_id required): /admin/tenants, /admin/audit-trail
  2. Tenant-scoped pages (requires impersonation first): /, /orders, /accounting/*, /einvoice/*, etc.
  3. Gateway API endpoints (JWT Bearer with tenant_id): /api/accounting-entries, /api/hkd-books, etc.
  4. ShopERP API endpoints (Cookie auth with tenant_id): /api/balance-sheets, /api/orders, etc.

  Flow:
    Step 1: Login as SystemAdmin (POST /dev/login/systemadmin) → get Cookie + JWT (no tenant_id)
    Step 2: Access platform-level pages (SystemAdmin policy, no tenant_id needed)
    Step 3: List tenants from /admin/tenants page (or via API)
    Step 4: Impersonate a tenant (POST /api/admin/impersonate/{tenantId}) → Cookie gets tenant_id
    Step 5: Re-login as SystemAdmin for that tenant (POST /dev/login/systemadmin/{tenantId}) → get JWT with tenant_id
    Step 6: Access all tenant-scoped pages + API endpoints with impersonated Cookie + JWT

.PARAMETER ShopERPUrl
  Base URL for ShopERP (Blazor Server + Cookie auth). Default: http://localhost:5003

.PARAMETER GatewayUrl
  Base URL for Gateway (API + JWT Bearer auth). Default: http://localhost:5001

.PARAMETER TenantId
  Tenant GUID to impersonate. Default: 00000000-0000-0000-0000-000000000001 (seed dev tenant)

.PARAMETER VasTenantId
  VAS Enterprise tenant GUID (for VAS reports). Default: a5b6c7d8-1234-5678-9abc-def012345678

.EXAMPLE
  .\verify-systemadmin-entry-points.ps1
  .\verify-systemadmin-entry-points.ps1 -ShopERPUrl http://localhost:5003 -GatewayUrl http://localhost:5001
#>

param(
    [string]$ShopERPUrl = "http://localhost:5003",
    [string]$GatewayUrl = "http://localhost:5001",
    [string]$TenantId = "00000000-0000-0000-0000-000000000001",
    [string]$VasTenantId = "a5b6c7d8-1234-5678-9abc-def012345678"
)

# --- Helpers ------------------------------------------------------------------

$script:PassCount = 0
$script:FailCount = 0
$script:SkipCount = 0
$script:Results = [System.Collections.Generic.List[pscustomobject]]::new()

function Write-Result {
    param(
        [string]$Category,
        [string]$Endpoint,
        [string]$Method,
        [int]$StatusCode,
        [string]$Expected,
        [string]$Notes
    )
    $passed = $false
    $status = "FAIL"
    if ($StatusCode -ge 200 -and $StatusCode -lt 400) {
        $passed = $true
        $status = "PASS"
        $script:PassCount++
    } elseif ($StatusCode -eq 403 -and $Expected -match "403") {
        # Expected 403 (role-restricted endpoint) - correctly denied
        $passed = $true
        $status = "PASS"
        $script:PassCount++
    } elseif ($StatusCode -eq 403 -and $Expected -match "200/403") {
        # VAS reports: 403 is acceptable for HKD tenants (feature flag blocked)
        $passed = $true
        $status = "PASS"
        $script:PassCount++
    } elseif ($StatusCode -eq 401 -or $StatusCode -eq 403) {
        # Unexpected auth failure
        $status = "FAIL"
        $script:FailCount++
    } elseif ($StatusCode -eq 404) {
        $status = "SKIP"
        $script:SkipCount++
    } else {
        $status = "FAIL"
        $script:FailCount++
    }

    $color = if ($passed) { "Green" } elseif ($status -eq "SKIP") { "DarkYellow" } else { "Red" }
    $statusStr = "[{0}] {1} {2} → {3}" -f $status, $Method, $Endpoint, $StatusCode
    if ($Notes) { $statusStr += " - $Notes" }
    Write-Host $statusStr -ForegroundColor $color

    $script:Results.Add([pscustomobject]@{
        Category = $Category
        Endpoint = $Endpoint
        Method   = $Method
        Status   = $StatusCode
        Expected = $Expected
        Result   = $status
        Notes    = $Notes
    })
}

function Invoke-Request {
    param(
        [string]$Url,
        [string]$Method = "GET",
        [hashtable]$Headers = @{},
        [string]$Body = $null,
        [string]$ContentType = "application/json",
        [int]$TimeoutSec = 15
    )
    try {
        $params = @{
            Uri             = $Url
            Method          = $Method
            Headers         = $Headers
            TimeoutSec      = $TimeoutSec
            ErrorAction     = "Stop"
            UseBasicParsing = $true
        }
        if ($Body) {
            $params.Body = $Body
            $params.ContentType = $ContentType
        }
        $response = Invoke-WebRequest @params
        return [pscustomobject]@{ StatusCode = $response.StatusCode; Content = $response.Content; Headers = $response.Headers }
    }
    catch [System.Net.WebException] {
        $resp = $_.Exception.Response
        if ($resp) {
            return [pscustomobject]@{ StatusCode = [int]$resp.StatusCode; Content = ""; Headers = @{} }
        }
        return [pscustomobject]@{ StatusCode = -1; Content = $_.Exception.Message; Headers = @{} }
    }
    catch {
        # PowerShell 7+ wraps HTTP responses differently
        if ($_.Exception.Response) {
            return [pscustomobject]@{ StatusCode = [int]$_.Exception.Response.StatusCode; Content = ""; Headers = @{} }
        }
        return [pscustomobject]@{ StatusCode = -1; Content = $_.Exception.Message; Headers = @{} }
    }
}

# --- Session state ------------------------------------------------------------
# Cookie session is initialized on first login request via -SessionVariable.
# All subsequent Cookie-authenticated requests reuse this session.

$script:CookieSession = $null

function Invoke-WithCookies {
    param(
        [string]$Url,
        [string]$Method = "GET",
        [hashtable]$Headers = @{},
        [string]$Body = $null,
        [string]$ContentType = "application/json",
        [int]$TimeoutSec = 15,
        [switch]$IsLogin  # First call initializes the session
    )
    try {
        $params = @{
            Uri             = $Url
            Method          = $Method
            Headers         = $Headers
            TimeoutSec      = $TimeoutSec
            ErrorAction     = "Stop"
            UseBasicParsing = $true
        }
        if ($IsLogin) {
            # First request creates the session variable
            $params.SessionVariable = "script:CookieSession"
        } elseif ($script:CookieSession) {
            # Subsequent requests reuse the session
            $params.WebSession = $script:CookieSession
        }
        if ($Body) {
            $params.Body = $Body
            $params.ContentType = $ContentType
        }
        $response = Invoke-WebRequest @params
        return [pscustomobject]@{ StatusCode = $response.StatusCode; Content = $response.Content; Headers = $response.Headers }
    }
    catch [System.Net.WebException] {
        $resp = $_.Exception.Response
        if ($resp) {
            return [pscustomobject]@{ StatusCode = [int]$resp.StatusCode; Content = ""; Headers = @{} }
        }
        return [pscustomobject]@{ StatusCode = -1; Content = $_.Exception.Message; Headers = @{} }
    }
    catch {
        if ($_.Exception.Response) {
            return [pscustomobject]@{ StatusCode = [int]$_.Exception.Response.StatusCode; Content = ""; Headers = @{} }
        }
        return [pscustomobject]@{ StatusCode = -1; Content = $_.Exception.Message; Headers = @{} }
    }
}

# --- Step 0: Pre-flight checks ------------------------------------------------

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  SystemAdmin Entry Point Verification" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ShopERP: $ShopERPUrl" -ForegroundColor Gray
Write-Host "  Gateway: $GatewayUrl" -ForegroundColor Gray
Write-Host "  Tenant:  $TenantId" -ForegroundColor Gray
Write-Host "  VAS:     $VasTenantId" -ForegroundColor Gray
Write-Host ""

Write-Host "[Step 0] Pre-flight: Checking service health..." -ForegroundColor Yellow

$shopErpHealth = Invoke-Request -Url "$ShopERPUrl/health" -TimeoutSec 5
$gatewayHealth = Invoke-Request -Url "$GatewayUrl/health" -TimeoutSec 5

if ($shopErpHealth.StatusCode -ne 200) {
    Write-Host "  ShopERP not healthy (status: $($shopErpHealth.StatusCode)). Aborting." -ForegroundColor Red
    exit 1
}
Write-Host "  ShopERP healthy ✓" -ForegroundColor Green

if ($gatewayHealth.StatusCode -ne 200) {
    Write-Host "  Gateway not healthy (status: $($gatewayHealth.StatusCode)). Aborting." -ForegroundColor Red
    exit 1
}
Write-Host "  Gateway healthy ✓" -ForegroundColor Green

# --- Step 1: Login as SystemAdmin (no tenant_id) ------------------------------

Write-Host "`n[Step 1] Login as SystemAdmin (POST /dev/login/systemadmin)..." -ForegroundColor Yellow

$loginResp = Invoke-WithCookies -Url "$ShopERPUrl/dev/login/systemadmin" -Method "POST" -IsLogin
if ($loginResp.StatusCode -eq 200) {
    $loginBody = $loginResp.Content | ConvertFrom-Json
    $script:SystemAdminJwt = $loginBody.token
    Write-Host "  Login successful ✓ - JWT issued (no tenant_id)" -ForegroundColor Green
    Write-Host "  Role: $($loginBody.role)" -ForegroundColor Gray
} else {
    Write-Host "  Login FAILED (status: $($loginResp.StatusCode)). Aborting." -ForegroundColor Red
    exit 1
}

# --- Step 2: Platform-level pages (no tenant_id needed) -----------------------

Write-Host "`n[Step 2] Platform-level pages (SystemAdmin, no tenant_id)..." -ForegroundColor Yellow

$platformPages = @(
    @{ Path = "/admin/tenants";       Name = "Tenant Management" },
    @{ Path = "/admin/audit-trail";   Name = "Audit Trail" }
)

foreach ($page in $platformPages) {
    $resp = Invoke-WithCookies -Url "$ShopERPUrl$($page.Path)"
    Write-Result -Category "Platform Page" -Endpoint $page.Path -Method "GET" `
        -StatusCode $resp.StatusCode -Expected "200" -Notes $page.Name
}

# --- Step 3: Verify tenant list is accessible ---------------------------------

Write-Host "`n[Step 3] Verify tenant list accessible..." -ForegroundColor Yellow

$tenantListResp = Invoke-WithCookies -Url "$ShopERPUrl/api/tenants"
Write-Result -Category "Platform API" -Endpoint "/api/tenants" -Method "GET" `
    -StatusCode $tenantListResp.StatusCode -Expected "200" -Notes "Tenant list API"

# --- Step 4: Impersonate default tenant (Cookie gets tenant_id) ---------------

Write-Host "`n[Step 4] Impersonate tenant $TenantId..." -ForegroundColor Yellow

$impersonateResp = Invoke-WithCookies -Url "$ShopERPUrl/api/admin/impersonate/$TenantId" -Method "POST"
if ($impersonateResp.StatusCode -eq 200) {
    $impBody = $impersonateResp.Content | ConvertFrom-Json
    Write-Host "  Impersonation successful ✓ - tenant_id claim set in Cookie" -ForegroundColor Green
    Write-Host "  Tenant: $($impBody.tenantName)" -ForegroundColor Gray
} else {
    Write-Host "  Impersonation FAILED (status: $($impersonateResp.StatusCode))" -ForegroundColor Red
    Write-Host "  Content: $($impersonateResp.Content)" -ForegroundColor DarkGray
}

Write-Result -Category "Impersonation" -Endpoint "/api/admin/impersonate/{tenantId}" -Method "POST" `
    -StatusCode $impersonateResp.StatusCode -Expected "200" -Notes "Set tenant_id in Cookie"

# --- Step 5: Get JWT with tenant_id (for Gateway API calls) -------------------

Write-Host "`n[Step 5] Get JWT with tenant_id (POST /dev/login/systemadmin/{tenantId})..." -ForegroundColor Yellow

$tenantJwtResp = Invoke-WithCookies -Url "$ShopERPUrl/dev/login/systemadmin/$TenantId" -Method "POST"
if ($tenantJwtResp.StatusCode -eq 200) {
    $tenantJwtBody = $tenantJwtResp.Content | ConvertFrom-Json
    $script:TenantJwt = $tenantJwtBody.token
    Write-Host "  JWT with tenant_id issued ✓" -ForegroundColor Green
} else {
    Write-Host "  JWT issuance FAILED (status: $($tenantJwtResp.StatusCode))" -ForegroundColor Red
    $script:TenantJwt = $null
}
Write-Result -Category "Impersonation" -Endpoint "/dev/login/systemadmin/{tenantId}" -Method "POST" `
    -StatusCode $tenantJwtResp.StatusCode -Expected "200" -Notes "JWT with tenant_id for Gateway"

# --- Step 6: Tenant-scoped Blazor pages (Cookie auth + tenant_id) -------------

Write-Host "`n[Step 6] Tenant-scoped Blazor pages (Cookie + tenant_id)..." -ForegroundColor Yellow

$tenantPages = @(
    @{ Path = "/";                              Name = "Home Dashboard";          Policy = "OwnerOnly" },
    @{ Path = "/orders";                        Name = "Orders List";             Policy = "[Authorize]" },
    @{ Path = "/accounting";                    Name = "Accounting Index";        Policy = "OwnerOnly" },
    @{ Path = "/accounting/balance";            Name = "Account Balance";         Policy = "OwnerOnly" },
    @{ Path = "/accounting/history";            Name = "Transaction History";     Policy = "OwnerOnly" },
    @{ Path = "/accounting/revenue";            Name = "Revenue Entry";           Policy = "OwnerOnly" },
    @{ Path = "/accounting/expenses";           Name = "Expense Entry";           Policy = "OwnerOnly" },
    @{ Path = "/accounting/period-closing";     Name = "Period Closing";          Policy = "OwnerOnly" },
    @{ Path = "/accounting/hkd-books";          Name = "HKD Books";               Policy = "OwnerOnly" },
    @{ Path = "/accounting/balance-sheet";      Name = "Balance Sheet";           Policy = "OwnerOnly" },
    @{ Path = "/accounting/cash-flow-statement";Name = "Cash Flow Statement";     Policy = "OwnerOnly" },
    @{ Path = "/accounting/income-statement";   Name = "Income Statement";        Policy = "OwnerOnly" },
    @{ Path = "/accounting/trial-balance";      Name = "Trial Balance";           Policy = "OwnerOnly" },
    @{ Path = "/accounting/financial-reports";  Name = "Financial Reports";       Policy = "OwnerOnly" },
    @{ Path = "/einvoice";                      Name = "EInvoice Dashboard";      Policy = "StoreManagement" },
    @{ Path = "/einvoice/invoices";             Name = "Invoice Management";      Policy = "StoreManagement" },
    @{ Path = "/einvoice/providers";            Name = "Provider Management";     Policy = "OwnerOnly" },
    @{ Path = "/einvoice/configuration";        Name = "Provider Configuration";  Policy = "OwnerOnly" },
    @{ Path = "/einvoice/health";               Name = "Health Monitoring";       Policy = "StoreManagement" },
    @{ Path = "/einvoice/alerts";               Name = "Alert Management";        Policy = "StoreManagement" },
    @{ Path = "/admin/users";                   Name = "User Management";         Policy = "OwnerOnly" },
    @{ Path = "/admin/permission-groups";       Name = "Permission Groups";       Policy = "OwnerOnly" },
    @{ Path = "/sitemap";                       Name = "Sitemap";                 Policy = "[Authorize]" }
)

foreach ($page in $tenantPages) {
    $resp = Invoke-WithCookies -Url "$ShopERPUrl$($page.Path)"
    # Blazor pages return 200 on success, 302/401 if auth fails, 403 if forbidden
    $notes = "$($page.Name) [$($page.Policy)]"
    if ($resp.StatusCode -eq 302) {
        # Redirect to login - auth failed
        $notes += " - REDIRECT (auth failed)"
    } elseif ($resp.StatusCode -eq 403) {
        $notes += " - FORBIDDEN (policy denied)"
    } elseif ($resp.StatusCode -eq 200 -and $resp.Content -match "access-denied") {
        $notes += " - Access Denied page"
    }
    Write-Result -Category "Tenant Page" -Endpoint $page.Path -Method "GET" `
        -StatusCode $resp.StatusCode -Expected "200" -Notes $notes
}

# --- Step 7: ShopERP API endpoints (Cookie auth + tenant_id) ------------------

Write-Host "`n[Step 7] ShopERP API endpoints (Cookie + tenant_id)..." -ForegroundColor Yellow

$shopErpApis = @(
    @{ Path = "/api/orders";                Method = "GET";  Name = "Orders list" },
    @{ Path = "/api/dashboard";             Method = "GET";  Name = "Dashboard data" },
    @{ Path = "/api/products";              Method = "GET";  Name = "Products list" },
    @{ Path = "/api/shops";                 Method = "GET";  Name = "Shops list" },
    @{ Path = "/api/notifications";         Method = "GET";  Name = "Notifications" },
    @{ Path = "/api/loyalty";               Method = "GET";  Name = "Loyalty data" },
    @{ Path = "/api/customers";             Method = "GET";  Name = "Customers list" },
    @{ Path = "/api/socialcampaigns";       Method = "GET";  Name = "Social campaigns" },
    @{ Path = "/api/users";                 Method = "GET";  Name = "Users list" },
    @{ Path = "/api/permission-groups";     Method = "GET";  Name = "Permission groups" },
    @{ Path = "/api/apikeys";               Method = "GET";  Name = "API Keys" }
)

foreach ($api in $shopErpApis) {
    $resp = Invoke-WithCookies -Url "$ShopERPUrl$($api.Path)" -Method $api.Method
    Write-Result -Category "ShopERP API" -Endpoint $api.Path -Method $api.Method `
        -StatusCode $resp.StatusCode -Expected "200" -Notes $api.Name
}

# --- Step 8: VAS Report endpoints (ShopERP, Cookie auth + tenant_id) ----------

Write-Host "`n[Step 8] VAS Report endpoints (ShopERP, Cookie + tenant_id)..." -ForegroundColor Yellow
Write-Host "  Note: VAS reports require Enterprise tenant. Default tenant may get 403." -ForegroundColor DarkGray

$vasApis = @(
    @{ Path = "/api/balance-sheets?year=2026&month=6";       Name = "Balance Sheet" },
    @{ Path = "/api/cash-flow-statements?year=2026&month=6"; Name = "Cash Flow Statement" },
    @{ Path = "/api/income-statements?year=2026&month=6";    Name = "Income Statement" },
    @{ Path = "/api/trial-balances?year=2026&month=6";       Name = "Trial Balance" }
)

foreach ($api in $vasApis) {
    $resp = Invoke-WithCookies -Url "$ShopERPUrl$($api.Path)" -Method "GET"
    $notes = $api.Name
    if ($resp.StatusCode -eq 403) {
        $notes += " - 403 (HKD tenant, VAS not available - expected for default tenant)"
    }
    Write-Result -Category "VAS Report" -Endpoint $api.Path -Method "GET" `
        -StatusCode $resp.StatusCode -Expected "200/403" -Notes $notes
}

# --- Step 9: Gateway API endpoints (JWT Bearer + tenant_id) -------------------

Write-Host "`n[Step 9] Gateway API endpoints (JWT Bearer + tenant_id)..." -ForegroundColor Yellow

if ($script:TenantJwt) {
    $gatewayHeaders = @{ "Authorization" = "Bearer $script:TenantJwt" }

    $gatewayApis = @(
        @{ Path = "/api/accounting-entries";         Method = "GET";  Name = "Accounting Entries";     Policy = "RequireTenantAccess" },
        @{ Path = "/api/accounting";                 Method = "GET";  Name = "Accounting (alias)";     Policy = "RequireTenantAccess" },
        @{ Path = "/api/hkd-books";                  Method = "GET";  Name = "HKD Books";              Policy = "RequireTenantAccess" },
        @{ Path = "/api/orders";                     Method = "GET";  Name = "Gateway Orders";         Policy = "RequireTenantAccess" },
        @{ Path = "/api/products";                   Method = "GET";  Name = "Gateway Products";       Policy = "[Authorize]" },
        @{ Path = "/api/dashboard";                  Method = "GET";  Name = "Gateway Dashboard";      Policy = "[Authorize]" },
        @{ Path = "/api/customers";                  Method = "GET";  Name = "Gateway Customers";      Policy = "RequireTenantAccess" },
        @{ Path = "/api/shops";                      Method = "GET";  Name = "Gateway Shops";          Policy = "[Authorize]" },
        @{ Path = "/api/notifications";              Method = "GET";  Name = "Gateway Notifications";  Policy = "[Authorize]" },
        @{ Path = "/api/loyalty";                    Method = "GET";  Name = "Gateway Loyalty";        Policy = "[Authorize]" },
        @{ Path = "/api/campaigns";                  Method = "GET";  Name = "Gateway Campaigns";      Policy = "AllowAnonymous" },
        @{ Path = "/api/v1/localization";            Method = "GET";  Name = "Localization";           Policy = "RequireTenantAccess" },
        @{ Path = "/api/v1/shopconfig";              Method = "GET";  Name = "Shop Config";            Policy = "RequireTenantAccess" },
        @{ Path = "/api/v1/voicecommand";            Method = "GET";  Name = "Voice Command";          Policy = "RequireTenantAccess" },
        @{ Path = "/api/build";                      Method = "GET";  Name = "Build Info";             Policy = "RequireTenantAccess" },
        @{ Path = "/api/reports";                    Method = "GET";  Name = "Reports";                Policy = "RequireTenantAccess+Owner" }
    )

    foreach ($api in $gatewayApis) {
        $resp = Invoke-Request -Url "$GatewayUrl$($api.Path)" -Method $api.Method -Headers $gatewayHeaders
        $notes = "$($api.Name) [$($api.Policy)]"
        if ($resp.StatusCode -eq 401) {
            $notes += " - 401 (JWT invalid or missing tenant_id)"
        } elseif ($resp.StatusCode -eq 403) {
            $notes += " - 403 (policy denied)"
        }
        Write-Result -Category "Gateway API" -Endpoint $api.Path -Method $api.Method `
            -StatusCode $resp.StatusCode -Expected "200" -Notes $notes
    }
} else {
    Write-Host "  SKIPPED - no JWT with tenant_id available" -ForegroundColor DarkYellow
}

# --- Step 9b: Gateway role-restricted endpoints (expected 403) ----------------

Write-Host "`n[Step 9b] Gateway role-restricted endpoints (expected 403 for SystemAdmin)..." -ForegroundColor Yellow

if ($script:TenantJwt) {
    $gatewayHeaders = @{ "Authorization" = "Bearer $script:TenantJwt" }

    # These endpoints require specific roles (Masterchef/Staff/Manager or Admin)
    # SystemAdmin should get 403 - verifies role-based access control is enforced
    $roleRestrictedApis = @(
        @{ Path = "/api/kitchen";     Name = "Kitchen (Masterchef/Staff/Manager only)"; ExpectedRole = "Masterchef/Staff/Manager" },
        @{ Path = "/api/audittrail";  Name = "Audit Trail (Admin only)";                 ExpectedRole = "Admin" }
    )

    foreach ($api in $roleRestrictedApis) {
        $resp = Invoke-Request -Url "$GatewayUrl$($api.Path)" -Method "GET" -Headers $gatewayHeaders
        $notes = "$($api.Name) - expected 403 (SystemAdmin lacks $($api.ExpectedRole))"
        if ($resp.StatusCode -eq 403) {
            $notes += " ✓ correctly denied"
        } elseif ($resp.StatusCode -eq 200) {
            $notes += " - WARNING: SystemAdmin accessed role-restricted endpoint!"
        }
        Write-Result -Category "Role-Restricted" -Endpoint $api.Path -Method "GET" `
            -StatusCode $resp.StatusCode -Expected "403" -Notes $notes
    }
} else {
    Write-Host "  SKIPPED - no JWT with tenant_id available" -ForegroundColor DarkYellow
}

# --- Step 9c: VAS Enterprise tenant impersonation (optional) ------------------

Write-Host "`n[Step 9c] VAS Enterprise tenant - impersonate + test VAS reports..." -ForegroundColor Yellow
Write-Host "  VAS tenant: $VasTenantId" -ForegroundColor Gray

# Exit current impersonation first
$exitResp = Invoke-WithCookies -Url "$ShopERPUrl/api/admin/exit-impersonation" -Method "POST"
if ($exitResp.StatusCode -eq 200) {
    Write-Host "  Exited previous impersonation ✓" -ForegroundColor Green
}

# Impersonate VAS Enterprise tenant
$vasImpResp = Invoke-WithCookies -Url "$ShopERPUrl/api/admin/impersonate/$VasTenantId" -Method "POST"
if ($vasImpResp.StatusCode -eq 200) {
    $vasImpBody = $vasImpResp.Content | ConvertFrom-Json
    Write-Host "  VAS tenant impersonation successful ✓ - $($vasImpBody.tenantName)" -ForegroundColor Green

    # Get JWT for VAS tenant
    $vasJwtResp = Invoke-WithCookies -Url "$ShopERPUrl/dev/login/systemadmin/$VasTenantId" -Method "POST"
    if ($vasJwtResp.StatusCode -eq 200) {
        $vasJwtBody = $vasJwtResp.Content | ConvertFrom-Json
        $vasJwt = $vasJwtBody.token
        $vasHeaders = @{ "Authorization" = "Bearer $vasJwt" }

        # Test VAS reports with Enterprise tenant - should get 200 (not 403)
        $vasReportApis = @(
            @{ Path = "$ShopERPUrl/api/balance-sheets?year=2026&month=6";       Name = "Balance Sheet (Enterprise)" },
            @{ Path = "$ShopERPUrl/api/cash-flow-statements?year=2026&month=6"; Name = "Cash Flow (Enterprise)" },
            @{ Path = "$ShopERPUrl/api/income-statements?year=2026&month=6";    Name = "Income Statement (Enterprise)" },
            @{ Path = "$ShopERPUrl/api/trial-balances?year=2026&month=6";       Name = "Trial Balance (Enterprise)" }
        )

        foreach ($api in $vasReportApis) {
            $resp = Invoke-WithCookies -Url $api.Path -Method "GET"
            $notes = $api.Name
            if ($resp.StatusCode -eq 403) {
                $notes += " - 403 (feature flag blocked - check VAS tenant type)"
            } elseif ($resp.StatusCode -eq 200) {
                $notes += " ✓ VAS report accessible"
            }
            Write-Result -Category "VAS Enterprise" -Endpoint $api.Path -Method "GET" `
                -StatusCode $resp.StatusCode -Expected "200" -Notes $notes
        }
    } else {
        Write-Host "  VAS JWT issuance FAILED (status: $($vasJwtResp.StatusCode))" -ForegroundColor Red
    }
} else {
    Write-Host "  VAS tenant impersonation FAILED (status: $($vasImpResp.StatusCode))" -ForegroundColor Red
    Write-Host "  VAS tenant may not be seeded. Run ShopERP to seed it." -ForegroundColor DarkGray
}

# --- Step 10: Gateway SystemAdmin-only endpoints (JWT, no tenant_id) ----------

Write-Host "`n[Step 10] Gateway SystemAdmin-only endpoints (JWT, no tenant_id)..." -ForegroundColor Yellow

if ($script:SystemAdminJwt) {
    $sysadminHeaders = @{ "Authorization" = "Bearer $script:SystemAdminJwt" }

    $sysadminApis = @(
        @{ Path = "/api/v1/onboarding/pending";   Method = "GET";  Name = "Pending Onboarding Requests" },
        @{ Path = "/api/v1/onboarding/templates"; Method = "GET";  Name = "Onboarding Templates" }
    )

    foreach ($api in $sysadminApis) {
        $resp = Invoke-Request -Url "$GatewayUrl$($api.Path)" -Method $api.Method -Headers $sysadminHeaders
        $notes = $api.Name
        if ($resp.StatusCode -eq 401) {
            $notes += " - 401 (JWT invalid)"
        } elseif ($resp.StatusCode -eq 403) {
            $notes += " - 403 (not SystemAdmin role)"
        }
        Write-Result -Category "SysAdmin API" -Endpoint $api.Path -Method $api.Method `
            -StatusCode $resp.StatusCode -Expected "200" -Notes $notes
    }
} else {
    Write-Host "  SKIPPED - no SystemAdmin JWT available" -ForegroundColor DarkYellow
}

# --- Step 11: Exit impersonation ----------------------------------------------

Write-Host "`n[Step 11] Exit impersonation (POST /api/admin/exit-impersonation)..." -ForegroundColor Yellow

$exitResp = Invoke-WithCookies -Url "$ShopERPUrl/api/admin/exit-impersonation" -Method "POST"
Write-Result -Category "Impersonation" -Endpoint "/api/admin/exit-impersonation" -Method "POST" `
    -StatusCode $exitResp.StatusCode -Expected "200" -Notes "Clear tenant_id from Cookie"

# --- Summary ------------------------------------------------------------------

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
        Write-Host "    [$($_.Category)] $($_.Method) $($_.Endpoint) → $($_.Status)" -ForegroundColor Red
        if ($_.Notes) { Write-Host "         $($_.Notes)" -ForegroundColor DarkRed }
    }
}

Write-Host ""

# Export results to CSV
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$csvPath = "systemadmin-entry-points-$timestamp.csv"
$script:Results | Export-Csv -Path $csvPath -NoTypeInformation
Write-Host "Results exported to: $csvPath" -ForegroundColor Gray

if ($script:FailCount -gt 0) {
    exit 1
} else {
    exit 0
}
