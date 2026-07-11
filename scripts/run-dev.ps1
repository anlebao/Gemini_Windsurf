<#
.SYNOPSIS
    Start Vạn An apps for local smoke test (SQLite + Docker infra: PostgreSQL + NATS).
.DESCRIPTION
    Launches Gateway (5001), ShopERP (5003), and optionally KhachLink (5002)
    in separate PowerShell windows.

    Infrastructure (PostgreSQL + NATS) is started via docker-compose.infra.yml.
    If Docker Desktop is not running, it is auto-started and waited for readiness.
    If Docker Desktop is not installed, the script falls back to SQLite-only mode
    (Gateway/ShopERP run without NATS in degraded mode).

      - ShopERP uses vanan_shoperp.db (auto-created + seeded on first run)
      - Gateway runs in monolithic mode (in-process CoreHub services)
      - PostgreSQL (5432) for Accounting, NATS (4222) for event sync

    Auto-detects .NET SDK 8.0.422 in user-local path if not on system PATH.

    After services start, prints smoke-test quick-reference URLs.

.PARAMETER NoKhachLink
    Skip launching KhachLink (only Gateway + ShopERP).
.PARAMETER ShopERPOnly
    Launch only ShopERP (Gateway runs in-process per Option B monolithic mode).
.PARAMETER NoInfra
    Skip Docker infrastructure startup (SQLite-only, no PostgreSQL/NATS).
.EXAMPLE
    .\run-dev.ps1
.EXAMPLE
    .\run-dev.ps1 -NoKhachLink
.EXAMPLE
    .\run-dev.ps1 -ShopERPOnly
.EXAMPLE
    .\run-dev.ps1 -NoInfra
#>
[CmdletBinding()]
param(
    [switch]$NoKhachLink,
    [switch]$ShopERPOnly,
    [switch]$NoInfra
)

$ErrorActionPreference = "Stop"

# --- 1. Locate repo root (must be first - used by step 0 + 2) ---
$rootDir = Split-Path -Parent $PSScriptRoot
Set-Location $rootDir

Write-Host ">> Vạn An Smoke Test Launcher (SQLite + Docker Infra)" -ForegroundColor Cyan
Write-Host "   SDK: 8.0.422+ | DB: SQLite + PostgreSQL | Mode: Development (Debug build)" -ForegroundColor DarkGray
Write-Host ""

# --- 0. Kill any running dotnet/VS processes to avoid DLL lock (CS2012) ---
$processesToKill = Get-Process dotnet,devenv,ServiceHub,RoslynCodeAnalysis -ErrorAction SilentlyContinue
if ($processesToKill) {
    Write-Host "[Cleanup] Killing $($processesToKill.Count) process(es) to avoid DLL lock (CS2012)" -ForegroundColor Yellow
    $processesToKill | Stop-Process -Force
    Start-Sleep -Seconds 3
}

# Clear locked analyzer obj/bin (Roslyn analyzer DLLs are frequently locked by VS)
$analyzerObj = Join-Path $rootDir "VanAn.Accounting\VanAn.Accounting.Analyzers\obj"
$analyzerBin = Join-Path $rootDir "VanAn.Accounting\VanAn.Accounting.Analyzers\bin"
foreach ($p in @($analyzerObj, $analyzerBin)) {
    if (Test-Path $p) { Remove-Item $p -Recurse -Force -ErrorAction SilentlyContinue }
}

# --- 2. Ensure .NET SDK 8.0.422+ is on PATH ---
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
Write-Host "[Repo] $rootDir" -ForegroundColor Green

# --- 2.5. Ensure Docker infrastructure (PostgreSQL + NATS) ---
# Docker CLI writes errors to stderr, which $ErrorActionPreference=Stop treats as fatal.
# Use a helper that temporarily relaxes EAP for Docker calls.
function Invoke-DockerSafe {
    param([string[]]$DockerArgs)
    $prevEAP = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = & docker @DockerArgs 2>&1
        return @{ ExitCode = $LASTEXITCODE; Output = $output }
    } finally {
        $ErrorActionPreference = $prevEAP
    }
}

$infraStarted = $false
if ($NoInfra) {
    Write-Host "[Infra] -NoInfra specified, skipping Docker infrastructure (SQLite-only mode)" -ForegroundColor Yellow
} else {
    $dockerDesktopExe = "C:\Program Files\Docker\Docker\Docker Desktop.exe"
    $dockerAvailable = $false

    # Check if Docker CLI is available
    $dockerCmd = Get-Command docker -ErrorAction SilentlyContinue
    if (-not $dockerCmd) {
        Write-Host "[Infra] Docker CLI not found on PATH. Checking Docker Desktop install..." -ForegroundColor Yellow
        if (Test-Path $dockerDesktopExe) {
            # Add Docker CLI to PATH (Docker Desktop installs to standard location)
            $dockerPath = "C:\Program Files\Docker\Docker\resources\bin"
            if (Test-Path $dockerPath) {
                $env:PATH = "$dockerPath;$env:PATH"
                $dockerCmd = Get-Command docker -ErrorAction SilentlyContinue
            }
        }
    }

    if (-not $dockerCmd) {
        Write-Host "[Infra] Docker not installed. Falling back to SQLite-only mode (no NATS/PostgreSQL)." -ForegroundColor Yellow
        Write-Host "[Infra] Gateway will run in degraded mode (NATS retry every 10s — harmless)." -ForegroundColor DarkGray
    } else {
        # Check if Docker daemon is running
        $result = Invoke-DockerSafe -DockerArgs @("info")
        if ($result.ExitCode -eq 0) {
            $dockerAvailable = $true
        } else {
            Write-Host "[Infra] Docker daemon not running. Starting Docker Desktop..." -ForegroundColor Yellow
            if (Test-Path $dockerDesktopExe) {
                Start-Process $dockerDesktopExe
                Write-Host "[Infra] Waiting for Docker daemon to become ready (up to 90s)..." -ForegroundColor DarkGray
                $maxWait = 90
                $waited = 0
                while ($waited -lt $maxWait) {
                    Start-Sleep -Seconds 5
                    $waited += 5
                    $result = Invoke-DockerSafe -DockerArgs @("info")
                    if ($result.ExitCode -eq 0) {
                        $dockerAvailable = $true
                        break
                    }
                    Write-Host "   ...waiting ($waited`s)" -ForegroundColor DarkGray
                }
                if (-not $dockerAvailable) {
                    Write-Host "[Infra] Docker daemon did not become ready in ${maxWait}s. Falling back to SQLite-only." -ForegroundColor Yellow
                }
            } else {
                Write-Host "[Infra] Docker Desktop not found at standard path. Falling back to SQLite-only." -ForegroundColor Yellow
            }
        }

        if ($dockerAvailable) {
            Write-Host "[Infra] Docker daemon is ready." -ForegroundColor Green

            # Start infrastructure containers
            $infraCompose = Join-Path $rootDir "docker-compose.infra.yml"
            if (Test-Path $infraCompose) {
                Write-Host "[Infra] Starting PostgreSQL + NATS via docker-compose.infra.yml..." -ForegroundColor Green
                $result = Invoke-DockerSafe -DockerArgs @("compose", "-f", $infraCompose, "up", "-d")
                $result.Output | ForEach-Object { Write-Host "   $_" -ForegroundColor DarkGray }
                $composeExit = $result.ExitCode

                if ($composeExit -eq 0) {
                    # Wait for healthchecks to pass
                    Write-Host "[Infra] Waiting for container healthchecks (up to 30s)..." -ForegroundColor DarkGray
                    $maxHealthWait = 30
                    $healthWaited = 0
                    while ($healthWaited -lt $maxHealthWait) {
                        $hResult = Invoke-DockerSafe -DockerArgs @("inspect", "--format={{.State.Health.Status}}", "vanan-postgres-local", "vanan-nats-local")
                        $unhealthy = ($hResult.Output | Where-Object { $_ -ne "healthy" }).Count
                        if ($unhealthy -eq 0) {
                            break
                        }
                        Start-Sleep -Seconds 3
                        $healthWaited += 3
                        Write-Host "   ...waiting ($healthWaited`s)" -ForegroundColor DarkGray
                    }

                    $psResult = Invoke-DockerSafe -DockerArgs @("ps", "--format", "table {{.Names}}`t{{.Status}}`t{{.Ports}}")
                    Write-Host "[Infra] Containers:" -ForegroundColor Green
                    $psResult.Output | ForEach-Object { Write-Host "   $_" -ForegroundColor DarkGray }
                    $infraStarted = $true
                } else {
                    Write-Host "[Infra] docker compose up failed (exit $composeExit). Continuing without infra (degraded mode)." -ForegroundColor Yellow
                }
            } else {
                Write-Host "[Infra] docker-compose.infra.yml not found. Continuing without infra." -ForegroundColor Yellow
            }
        }
    }
}

if ($infraStarted) {
    Write-Host "[Infra] PostgreSQL (5432) + NATS (4222) ready." -ForegroundColor Green
} else {
    Write-Host "[Infra] Running without Docker infra — NATS degraded mode (harmless retry logs)." -ForegroundColor Yellow
}

# --- 3. Verify projects exist ---
$gatewayProj    = Join-Path $rootDir "2_Gateway\VanAn.Gateway.csproj"
$shopErpProj    = Join-Path $rootDir "5_WebApps\ShopERP\VanAn.ShopERP.csproj"
$khachLinkProj  = Join-Path $rootDir "5_WebApps\KhachLink\VanAn.KhachLink.csproj"

foreach ($p in @($gatewayProj, $shopErpProj)) {
    if (-not (Test-Path $p)) { Write-Error "Missing: $p"; exit 1 }
}

# --- 4. Launch services ---
$envScript = "`$env:ASPNETCORE_ENVIRONMENT='Development'"

# --- 3.5. Pre-build solution ONCE (synchronous) to avoid parallel-build DLL race (CS2012).
#          Without this, the 3 parallel `dotnet run` invocations below each rebuild
#          1_Shared/VanAn.Shared.csproj simultaneously and collide on writing
#          1_Shared\obj\Debug\net8.0\VanAn.Shared.dll -> CS2012 on the losers.
$solutionPath = Join-Path $rootDir 'VanAn.sln'
if (-not (Test-Path $solutionPath)) { Write-Error "Missing: $solutionPath"; exit 1 }

Write-Host "`n[Build] Pre-building VanAn.sln (single-pass to avoid parallel-build DLL lock)..." -ForegroundColor Green
$buildLog = Join-Path $PSScriptRoot '.last-dev-build.log'
& dotnet build $solutionPath --configuration Debug --nologo 2>&1 |
    Tee-Object -FilePath $buildLog | Out-Host
if ($LASTEXITCODE -ne 0) {
    Write-Error "Pre-build failed (exit $LASTEXITCODE). See log: $buildLog"
    exit 1
}
Write-Host "[Build] Pre-build OK." -ForegroundColor Green

# Shut down MSBuild / VBCSCompiler server nodes so they do not hold handles
# to VanAn.Shared.dll when the app processes start. (nodeReuse=true keeps them
# alive ~60s after build, which overlaps with `dotnet run` startup.)
Write-Host "[Build] Shutting down dotnet build-server (release MSBuild/VBCSCompiler handles)..." -ForegroundColor DarkGray
& dotnet build-server shutdown 2>&1 | Out-Null
Start-Sleep -Seconds 1

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

# Gateway (5001) - monolithic mode, in-process CoreHub
if (-not $ShopERPOnly) {
    Write-Host "`n[Gateway] Starting on http://localhost:5001 ..." -ForegroundColor Green
    $gatewayDir = Split-Path $gatewayProj -Parent
    $cmd = "$envScript; Set-Location '$gatewayDir'; dotnet run --project '$gatewayProj' --configuration Debug --no-build --urls 'http://localhost:5001'"
    Start-Process powershell -ArgumentList "-NoExit", "-Command", $cmd
}

# ShopERP (5003) - Blazor Server + API controllers + DevLogin
Write-Host "[ShopERP] Starting on http://localhost:5003 ..." -ForegroundColor Green
$shopErpDir = Split-Path $shopErpProj -Parent
$cmd = "$envScript; Set-Location '$shopErpDir'; dotnet run --project '$shopErpProj' --configuration Debug --no-build --urls 'http://localhost:5003'"
Start-Process powershell -ArgumentList "-NoExit", "-Command", $cmd

# KhachLink (5002) - optional PWA
if (-not $NoKhachLink -and (Test-Path $khachLinkProj)) {
    Write-Host "[KhachLink] Starting on http://localhost:5002 ..." -ForegroundColor Green
    $khachLinkDir = Split-Path $khachLinkProj -Parent
    $cmd = "$envScript; Set-Location '$khachLinkDir'; dotnet run --project '$khachLinkProj' --configuration Debug --no-build --urls 'http://localhost:5002'"
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
if ($infraStarted) {
    Write-Host "Note: PostgreSQL + NATS running via Docker. Stop with: docker compose -f docker-compose.infra.yml down" -ForegroundColor DarkGray
} else {
    Write-Host "Note: No Docker infra — NATS degraded mode (retry logs every 10s are harmless)." -ForegroundColor DarkGray
}
