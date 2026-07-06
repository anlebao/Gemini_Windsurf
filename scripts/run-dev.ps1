<#
.SYNOPSIS
    Start Vạn An apps for local smoke test (SQLite-only, no Docker required).
.DESCRIPTION
    Launches Gateway (5001), ShopERP (5003), and optionally KhachLink (5002)
    in separate PowerShell windows using the SQLite dev database.

    Unlike start-apps.ps1 (which requires Docker PostgreSQL + NATS), this script
    is designed for quick W8 smoke testing against the seeded SQLite DB:
      - ShopERP uses vanan_shoperp.db (auto-created + seeded on first run)
      - Gateway runs in monolithic mode (in-process CoreHub services)
      - No external infrastructure required

    Auto-detects .NET SDK 8.0.422 in user-local path if not on system PATH.

    After services start, prints smoke-test quick-reference URLs.

.PARAMETER NoKhachLink
    Skip launching KhachLink (only Gateway + ShopERP).
.PARAMETER ShopERPOnly
    Launch only ShopERP (Gateway runs in-process per Option B monolithic mode).
.EXAMPLE
    .\run-dev.ps1
.EXAMPLE
    .\run-dev.ps1 -NoKhachLink
.EXAMPLE
    .\run-dev.ps1 -ShopERPOnly
#>
[CmdletBinding()]
param(
    [switch]$NoKhachLink,
    [switch]$ShopERPOnly
)

$ErrorActionPreference = "Stop"

Write-Host ">> Vạn An Smoke Test Launcher (SQLite-only)" -ForegroundColor Cyan
Write-Host "   SDK: 8.0.422+ | DB: SQLite | Mode: Development (Debug build)" -ForegroundColor DarkGray
Write-Host ""

# --- 1. Ensure .NET SDK 8.0.422+ is on PATH ---
$dotnetVersion = & dotnet --version 2>$null
if (-not $dotnetVersion -or [version]$dotnetVersion -lt [version]"8.0.400") {
    $userDotnet = Join-Path $env:LOCALAPPDATA "dotnet"
    if (Test-Path (Join-Path $userDotnet "dotnet.exe")) {
        Write-Host "[PATH] Adding user-local dotnet to PATH: $userDotnet" -ForegroundColor Yellow
        $env:PATH = "$userDotnet;$env:PATH"
        $dotnetVersion = & dotnet --version
    } else {
        Write-Error "dotnet 8.0.422+ not found. Install from https://dot.net or run: &([scriptblock]::Create((Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1'))) -Channel 8.0.4xx -InstallDir `"$userDotnet`""
        exit 1
    }
}
Write-Host "[SDK] dotnet $dotnetVersion" -ForegroundColor Green

# --- 2. Locate repo root ---
$rootDir = Split-Path -Parent $PSScriptRoot
Set-Location $rootDir
Write-Host "[Repo] $rootDir" -ForegroundColor Green

# --- 3. Verify projects exist ---
$gatewayProj    = Join-Path $rootDir "2_Gateway\VanAn.Gateway.csproj"
$shopErpProj    = Join-Path $rootDir "5_WebApps\ShopERP\VanAn.ShopERP.csproj"
$khachLinkProj  = Join-Path $rootDir "5_WebApps\KhachLink\VanAn.KhachLink.csproj"

foreach ($p in @($gatewayProj, $shopErpProj)) {
    if (-not (Test-Path $p)) { Write-Error "Missing: $p"; exit 1 }
}

# --- 4. Launch services ---
$envScript = "`$env:ASPNETCORE_ENVIRONMENT='Development'"

# --- 4a. Pre-flight: remove stale SQLite DB if it predates W3 (missing AccountCharts table) ---
$dbPath = Join-Path $rootDir "5_WebApps\ShopERP\vanan_shoperp.db"
if (Test-Path $dbPath) {
    $dbAge = (Get-Date) - (Get-Item $dbPath).LastWriteTime
    if ($dbAge.TotalDays -gt 1) {
        Write-Host "[DB] Found stale vanan_shoperp.db ($([int]$dbAge.TotalDays) days old) - removing to force fresh schema creation" -ForegroundColor Yellow
        Remove-Item "$dbPath*", -Force -ErrorAction SilentlyContinue
        Write-Host "[DB] Stale DB removed - EnsureCreatedAsync will recreate with all W3/W5 tables" -ForegroundColor Green
    } else {
        Write-Host "[DB] vanan_shoperp.db is recent (<1 day) - keeping" -ForegroundColor Green
    }
} else {
    Write-Host "[DB] No vanan_shoperp.db found - will be created on first run" -ForegroundColor Green
}

# Gateway (5001) — monolithic mode, in-process CoreHub
if (-not $ShopERPOnly) {
    Write-Host "`n[Gateway] Starting on http://localhost:5001 ..." -ForegroundColor Green
    $gatewayDir = Split-Path $gatewayProj -Parent
    $cmd = "$envScript; Set-Location '$gatewayDir'; dotnet run --project '$gatewayProj' --configuration Debug --urls 'http://localhost:5001'"
    Start-Process powershell -ArgumentList "-NoExit", "-Command", $cmd
}

# ShopERP (5003) — Blazor Server + API controllers + DevLogin
Write-Host "[ShopERP] Starting on http://localhost:5003 ..." -ForegroundColor Green
$shopErpDir = Split-Path $shopErpProj -Parent
$cmd = "$envScript; Set-Location '$shopErpDir'; dotnet run --project '$shopErpProj' --configuration Debug --urls 'http://localhost:5003'"
Start-Process powershell -ArgumentList "-NoExit", "-Command", $cmd

# KhachLink (5002) — optional PWA
if (-not $NoKhachLink -and (Test-Path $khachLinkProj)) {
    Write-Host "[KhachLink] Starting on http://localhost:5002 ..." -ForegroundColor Green
    $khachLinkDir = Split-Path $khachLinkProj -Parent
    $cmd = "$envScript; Set-Location '$khachLinkDir'; dotnet run --project '$khachLinkProj' --configuration Debug --urls 'http://localhost:5002'"
    Start-Process powershell -ArgumentList "-NoExit", "-Command", $cmd
}

# --- 5. Wait + print smoke-test guide ---
Write-Host "`n>> Services launching in separate windows. Waiting 5s for startup..." -ForegroundColor Cyan
Start-Sleep -Seconds 5

Write-Host @"

============================================================
SMOKE TEST QUICK REFERENCE
============================================================

1. LOGIN (open in browser or curl):
   Owner:          POST http://localhost:5003/dev/login
   VAS Enterprise: POST http://localhost:5003/dev/login/vas
   Staff:          POST http://localhost:5003/dev/login/staff
   StoreKeeper:    POST http://localhost:5003/dev/login/storekeeper
   SystemAdmin:    POST http://localhost:5003/dev/login/systemadmin

   Browser DevTools Console (on http://localhost:5003):
     fetch('/dev/login/vas', {method:'POST', credentials:'same-origin'}).then(r=>r.json()).then(console.log)

2. ORDER -> PAYMENT -> ACCOUNTING:
   KhachLink UI:  http://localhost:5002  (create order, confirm payment)
   ShopERP history: http://localhost:5003/accounting/history

3. 4 BCTC REPORTS (login as VAS tenant first):
   Balance Sheet:     http://localhost:5003/accounting/balance-sheet
   Income Statement:  http://localhost:5003/accounting/income-statement
   Cash Flow:         http://localhost:5003/accounting/cash-flow-statement
   Trial Balance:     http://localhost:5003/accounting/trial-balance
   Reports Hub:       http://localhost:5003/accounting/financial-reports

4. HKD BOOKS (TT 152):
   List:   http://localhost:5003/accounting/hkd-books
   Detail: http://localhost:5003/accounting/hkd-books/{TemplateCode}

5. PERIOD CLOSING (persist test):
   http://localhost:5003/accounting/period-closing
   -> Close a period -> Ctrl+C ShopERP -> restart -> verify status persists

6. E-INVOICE (needs credentials in appsettings.Development.json):
   Providers:  http://localhost:5003/einvoice/providers
   Invoices:   http://localhost:5003/einvoice/invoices

7. STOP ALL SERVICES:
   scripts\stop-local.ps1   (or close each window / kill dotnet)

============================================================
"@ -ForegroundColor White

Write-Host "Note: First run will seed SQLite DB (vanan_shoperp.db) - may take 10-15s extra." -ForegroundColor Yellow
Write-Host 'Note: DevLogin endpoints only exist in Debug build (#if DEBUG guard, W5).' -ForegroundColor Yellow
