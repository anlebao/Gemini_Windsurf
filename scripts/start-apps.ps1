<#
.SYNOPSIS
    Start all Vạn An .NET applications in separate terminal windows
.DESCRIPTION
    Launches CoreHub, Gateway, ShopERP, and KhachLink in separate
    PowerShell windows for local development.
.EXAMPLE
    .\start-apps.ps1
.EXAMPLE
    .\start-apps.ps1 -CoreHubOnly
#>

[CmdletBinding()]
param(
    [switch]$CoreHubOnly,
    [switch]$GatewayOnly,
    [switch]$ShopERPOnly,
    [switch]$KhachLinkOnly
)

$ErrorActionPreference = "Stop"
Write-Host ">> Starting Vạn An .NET Applications..." -ForegroundColor Cyan

# Verify infrastructure is running
$infraRunning = docker ps --format "{{.Names}}" | Select-String "vanan-postgres-local|vanan-nats-local"
if (-not $infraRunning) {
    Write-Error "Infrastructure not running! Run .\start-local-infra.ps1 first."
    exit 1
}

$rootDir = Split-Path -Parent $PSScriptRoot

# Start CoreHub
if (-not $GatewayOnly -and -not $ShopERPOnly -and -not $KhachLinkOnly) {
    Write-Host "[App] Starting CoreHub on http://localhost:5010" -ForegroundColor Green
    $coreHubDir = Join-Path $rootDir "3_CoreHub"
    # CRITICAL: Use Single-Quote Here-String to prevent host variable interpolation
    $coreHubEnv = @'
$env:ASPNETCORE_ENVIRONMENT="Development"
$env:ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=VanAnLocal;Username=vanan_dev;Password=VanAnLocal@2026"
$env:NATS__Url="nats://localhost:4222"
cd "REPLACE_COREHUB_DIR"
dotnet run --urls "http://localhost:5010"
'@
    $coreHubEnv = $coreHubEnv.Replace("REPLACE_COREHUB_DIR", $coreHubDir)
    Start-Process powershell -ArgumentList "-NoExit", "-Command", $coreHubEnv
}

# Start Gateway
if (-not $CoreHubOnly -and -not $ShopERPOnly -and -not $KhachLinkOnly) {
    Write-Host "[App] Starting Gateway on http://localhost:5001" -ForegroundColor Green
    $gatewayDir = Join-Path $rootDir "2_Gateway"
    # CRITICAL: Use Single-Quote Here-String to prevent host variable interpolation
    $gatewayEnv = @'
$env:ASPNETCORE_ENVIRONMENT="Development"
$env:COREHUB_URL="http://localhost:5010"
$env:NATS__Url="nats://localhost:4222"
cd "REPLACE_GATEWAY_DIR"
dotnet run --urls "http://localhost:5001"
'@
    $gatewayEnv = $gatewayEnv.Replace("REPLACE_GATEWAY_DIR", $gatewayDir)
    Start-Process powershell -ArgumentList "-NoExit", "-Command", $gatewayEnv
}

# Start ShopERP
if (-not $CoreHubOnly -and -not $GatewayOnly -and -not $KhachLinkOnly) {
    Write-Host "[App] Starting ShopERP on http://localhost:5003" -ForegroundColor Green
    $shopERPDir = Join-Path $rootDir "5_WebApps\ShopERP"
    # CRITICAL: Use Single-Quote Here-String to prevent host variable interpolation
    $shopERPEnv = @'
$env:ASPNETCORE_ENVIRONMENT="Development"
$env:GATEWAY_URL="http://localhost:5001"
$env:NATS__Url="nats://localhost:4222"
cd "REPLACE_SHOPERP_DIR"
dotnet run --urls "http://localhost:5003"
'@
    $shopERPEnv = $shopERPEnv.Replace("REPLACE_SHOPERP_DIR", $shopERPDir)
    Start-Process powershell -ArgumentList "-NoExit", "-Command", $shopERPEnv
}

# Start KhachLink
if (-not $CoreHubOnly -and -not $GatewayOnly -and -not $ShopERPOnly) {
    Write-Host "[App] Starting KhachLink on http://localhost:5002" -ForegroundColor Green
    $khachLinkDir = Join-Path $rootDir "5_WebApps\KhachLink"
    # CRITICAL: Use Single-Quote Here-String to prevent host variable interpolation
    $khachLinkEnv = @'
$env:ASPNETCORE_ENVIRONMENT="Development"
$env:GATEWAY_URL="http://localhost:5001"
$env:NATS__Url="nats://localhost:4222"
cd "REPLACE_KHACHLINK_DIR"
dotnet run --urls "http://localhost:5002"
'@
    $khachLinkEnv = $khachLinkEnv.Replace("REPLACE_KHACHLINK_DIR", $khachLinkDir)
    Start-Process powershell -ArgumentList "-NoExit", "-Command", $khachLinkEnv
}

Write-Host "" -ForegroundColor White
Write-Host "[OK] Applications starting in separate windows..." -ForegroundColor Green
Write-Host "   Press Ctrl+C in each window to stop." -ForegroundColor Yellow
Write-Host "" -ForegroundColor White
Write-Host "Service URLs:" -ForegroundColor Cyan
Write-Host "   CoreHub:  http://localhost:5010" -ForegroundColor Gray
Write-Host "   Gateway:  http://localhost:5001" -ForegroundColor Gray
Write-Host "   ShopERP:  http://localhost:5003" -ForegroundColor Gray
Write-Host "   KhachLink: http://localhost:5002" -ForegroundColor Gray
