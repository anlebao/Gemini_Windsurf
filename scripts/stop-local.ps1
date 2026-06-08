<#
.SYNOPSIS
    Stop all Vạn An local development services
.DESCRIPTION
    Stops infrastructure containers and kills .NET processes
.EXAMPLE
    .\stop-local.ps1
#>

[CmdletBinding()]
param(
    [switch]$KeepInfra
)

Write-Host ">> Stopping Vạn An Local Development..." -ForegroundColor Cyan

# Stop .NET processes by querying specific ports (CRITICAL: Do NOT use Get-Process -Name dotnet)
Write-Host "[Stop] Stopping .NET applications..." -ForegroundColor Yellow
$targetPorts = @(5010, 5001, 5003, 5002)
$stoppedPIDs = @()

foreach ($port in $targetPorts) {
    try {
        $connections = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue | Where-Object { $_.State -eq "Listen" }
        if ($connections) {
            foreach ($conn in $connections) {
                $processId = $conn.OwningProcess
                if ($processId -and ($stoppedPIDs -notcontains $processId)) {
                    try {
                        $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
                        if ($process) {
                            Write-Host "   Stopping process on port $port (PID: $processId, $($process.ProcessName))..." -ForegroundColor Gray
                            Stop-Process -Id $processId -Force
                            $stoppedPIDs += $processId
                        }
                    } catch {
                        Write-Host "   Warning: Could not stop PID $processId on port $port - $_" -ForegroundColor Yellow
                    }
                }
            }
        }
    } catch {
        Write-Host "   No listener found on port $port" -ForegroundColor Gray
    }
}

if ($stoppedPIDs.Count -gt 0) {
    Write-Host "   $($stoppedPIDs.Count) process(es) stopped." -ForegroundColor Green
} else {
    Write-Host "   No Vạn An processes found running on target ports." -ForegroundColor Gray
}

# Stop infrastructure unless KeepInfra specified
if (-not $KeepInfra) {
    Write-Host "[Stop] Stopping infrastructure containers..." -ForegroundColor Yellow
    docker compose -f docker-compose.infra.yml down
    Write-Host "   Infrastructure stopped." -ForegroundColor Green
} else {
    Write-Host "[Keep] Infrastructure containers kept running (use -KeepInfra)" -ForegroundColor Yellow
}

Write-Host "[OK] All services stopped." -ForegroundColor Green
