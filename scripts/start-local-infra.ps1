<#
.SYNOPSIS
    Start Vạn An infrastructure containers for local development
.DESCRIPTION
    Starts Postgres and NATS containers with optimized resource limits
    for 8GB RAM machines.
.EXAMPLE
    .\start-local-infra.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Write-Host ">> Starting Vạn An Local Infrastructure..." -ForegroundColor Cyan

# Check Docker is running
try {
    $null = docker info 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Docker is not running. Please start Docker Desktop first."
        exit 1
    }
} catch {
    Write-Error "Docker is not installed or not running."
    exit 1
}

# Check if already running
$running = docker ps --format "{{.Names}}" | Select-String "vanan-postgres-local|vanan-nats-local"
if ($running) {
    Write-Host "[WARN] Infrastructure containers already running. Skipping start." -ForegroundColor Yellow
    Write-Host "   To restart: docker compose -f docker-compose.infra.yml restart" -ForegroundColor Gray
    exit 0
}

# Start infrastructure
Write-Host "[Container] Starting Postgres (768MB limit)..." -ForegroundColor Green
Write-Host "[Container] Starting NATS with JetStream (256MB limit)..." -ForegroundColor Green

docker compose -f docker-compose.infra.yml up -d

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to start infrastructure containers."
    exit 1
}

# Wait for health checks
Write-Host "... Waiting for services to be healthy..." -ForegroundColor Yellow
$maxAttempts = 30
$attempt = 0
$healthy = $false

while ($attempt -lt $maxAttempts -and -not $healthy) {
    Start-Sleep -Seconds 2
    $attempt++
    
    $postgresHealth = docker inspect --format='{{.State.Health.Status}}' vanan-postgres-local 2>$null
    $natsHealth = docker inspect --format='{{.State.Health.Status}}' vanan-nats-local 2>$null
    
    if ($postgresHealth -eq "healthy" -and $natsHealth -eq "healthy") {
        $healthy = $true
    } else {
        Write-Host "   Attempt $attempt/$maxAttempts - Postgres: $postgresHealth, NATS: $natsHealth" -ForegroundColor Gray
    }
}

if (-not $healthy) {
    Write-Error "Services failed to become healthy within timeout."
    Write-Host "Check logs: docker logs vanan-postgres-local" -ForegroundColor Red
    Write-Host "Check logs: docker logs vanan-nats-local" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Infrastructure is ready!" -ForegroundColor Green
Write-Host "" -ForegroundColor White
Write-Host "   Resource Usage:" -ForegroundColor Cyan
$containerStats = docker stats --no-stream --format "table {{.Name}}\t{{.MemUsage}}" | Select-String "vanan-"
$containerStats | ForEach-Object { Write-Host "      $_" -ForegroundColor Gray }
Write-Host "" -ForegroundColor White
Write-Host "   Connection Info:" -ForegroundColor Cyan
Write-Host "      Postgres: localhost:5432 (user: vanan_dev, pass: VanAnLocal@2026)" -ForegroundColor Gray
Write-Host "      NATS:     localhost:4222" -ForegroundColor Gray
Write-Host "" -ForegroundColor White
