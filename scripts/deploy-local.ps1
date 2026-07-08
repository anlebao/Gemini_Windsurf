<#
.SYNOPSIS
    VanAn Ecosystem - Local Docker Compose Deployment (Windows)
.DESCRIPTION
    Builds and starts all VanAn services locally via Docker Compose.
    Creates .env from .env.example if not exists.
.PARAMETER Rebuild
    Force rebuild Docker images (no cache)
.PARAMETER Down
    Stop and remove all containers
.PARAMETER Status
    Show status of all services
.PARAMETER Logs
    Tail logs from all services
.EXAMPLE
    .\scripts\deploy-local.ps1
    .\scripts\deploy-local.ps1 -Rebuild
    .\scripts\deploy-local.ps1 -Down
    .\scripts\deploy-local.ps1 -Status
    .\scripts\deploy-local.ps1 -Logs
#>
param(
    [switch]$Rebuild,
    [switch]$Down,
    [switch]$Status,
    [switch]$Logs
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ComposeFile = Join-Path $RepoRoot "docker-compose.yml"
$EnvFile = Join-Path $RepoRoot ".env"
$EnvExample = Join-Path $RepoRoot ".env.example"

function Write-Header($msg) {
    Write-Host "`n=== $msg ===" -ForegroundColor Cyan
}

function Test-Command($name) {
    return [bool](Get-Command $name -ErrorAction SilentlyContinue)
}

# --- Prerequisites check ---
Write-Header "Prerequisites Check"

if (-not (Test-Command "docker")) {
    Write-Host "[ERROR] Docker not found. Install Docker Desktop." -ForegroundColor Red
    exit 1
}

Write-Host "Docker: $(docker --version)"
Write-Host "Docker Compose: $(docker compose version)"

if (-not (docker info 2>$null)) {
    Write-Host "[ERROR] Docker daemon not running. Start Docker Desktop." -ForegroundColor Red
    exit 1
}

# --- Handle -Down ---
if ($Down) {
    Write-Header "Stopping Services"
    Push-Location $RepoRoot
    docker compose -f $ComposeFile down --remove-orphans
    Pop-Location
    Write-Host "All services stopped." -ForegroundColor Green
    exit 0
}

# --- Handle -Status ---
if ($Status) {
    Write-Header "Service Status"
    Push-Location $RepoRoot
    docker compose -f $ComposeFile ps
    Pop-Location
    exit 0
}

# --- Handle -Logs ---
if ($Logs) {
    Write-Header "Service Logs (Ctrl+C to exit)"
    Push-Location $RepoRoot
    docker compose -f $ComposeFile logs -f
    Pop-Location
    exit 0
}

# --- Ensure .env exists ---
Write-Header "Environment File"

if (-not (Test-Path $EnvFile)) {
    if (Test-Path $EnvExample) {
        Write-Host "[setup] .env not found. Copying from .env.example..."
        Copy-Item $EnvExample $EnvFile
        Write-Host "[setup] .env created. Edit it to set real passwords." -ForegroundColor Yellow
    } else {
        Write-Host "[ERROR] Neither .env nor .env.example found." -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "[setup] .env exists."
}

# --- Build & Start ---
Write-Header "Building & Starting Services"

$buildArgs = @("compose", "-f", $ComposeFile, "up", "-d", "--remove-orphans")
if ($Rebuild) {
    $buildArgs = @("compose", "-f", $ComposeFile, "up", "-d", "--build", "--remove-orphans")
    Write-Host "[deploy] Force rebuild enabled (--build, cache for base layers)"
}

Push-Location $RepoRoot
& docker @buildArgs
$exitCode = $LASTEXITCODE
Pop-Location

if ($exitCode -ne 0) {
    Write-Host "[ERROR] docker compose up failed (exit $exitCode)" -ForegroundColor Red
    exit $exitCode
}

# --- Wait for services ---
Write-Header "Waiting for Services"
Write-Host "Waiting 30s for health checks..."
Start-Sleep -Seconds 30

# --- Show status ---
Write-Header "Service Status"
Push-Location $RepoRoot
docker compose -f $ComposeFile ps
Pop-Location

# --- Print access URLs ---
Write-Header "Access URLs"
$gatewayPort = if ($env:GATEWAY_PORT) { $env:GATEWAY_PORT } else { "5010" }
$shoperpPort = if ($env:SHOPERP_PORT) { $env:SHOPERP_PORT } else { "5002" }
$khachlinkPort = if ($env:KHACHLINK_PORT) { $env:KHACHLINK_PORT } else { "5003" }

Write-Host "Gateway:   http://localhost:$gatewayPort" -ForegroundColor Green
Write-Host "ShopERP:   http://localhost:$shoperpPort" -ForegroundColor Green
Write-Host "KhachLink: http://localhost:$khachlinkPort" -ForegroundColor Green
Write-Host "pgAdmin:   http://localhost:5050" -ForegroundColor Green
Write-Host "Seq logs:  http://localhost:8081" -ForegroundColor Green
Write-Host "NATS mon:  http://localhost:8222" -ForegroundColor Green

Write-Host "`nDeploy complete!" -ForegroundColor Cyan
Write-Host "Use -Status to check, -Logs to tail, -Down to stop." -ForegroundColor DarkGray
