# Start-VanAnEcosystem.ps1
# Van An Ecosystem Startup Script - Version 3.0
# Author: Van An DevOps Team
# Description: Infrastructure + Local .NET Applications for manual testing

param(
    [switch]$SkipDocker,
    [switch]$SkipBuild,
    [switch]$SeparateWindows,
    [switch]$DisableFileLog,
    [string]$BaseIP = "localhost",
    [string[]]$LogContainers = @() # e.g., -LogContainers "postgres", "nats"
)

# Check execution policy
try {
    $currentPolicy = Get-ExecutionPolicy -Scope CurrentUser
    if ($currentPolicy -eq "Restricted") {
        Write-Host "Setting execution policy for current user..."
        Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser -Force
        Write-Host "Execution policy updated successfully." -ForegroundColor Green
    }
} catch {
    Write-Host "Warning: Could not set execution policy. You may need to run as Administrator." -ForegroundColor Yellow
}

# Color scheme for console output
$Colors = @{
    Success = "Green"
    Warning = "Yellow"
    Error = "Red"
    Info = "Cyan"
    Service = "Magenta"
    URL = "White"
    Header = "Yellow"
    DarkGray = "DarkGray"
}

Write-Host "[STARTUP] Van An Ecosystem Startup Script v3.0" -ForegroundColor $Colors.Header
Write-Host "[STARTUP] ======================================" -ForegroundColor $Colors.Header
Write-Host "[ARCH] Infrastructure: Docker + Local .NET Apps" -ForegroundColor $Colors.Info
Write-Host ""

# Function to write colored output
function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    $foregroundColor = $Colors[$Color]
    if ($null -eq $foregroundColor) {
        $foregroundColor = "White"
    }
    Write-Host $Message -ForegroundColor $foregroundColor
}

# Function to check if a port is in use
function Test-Port {
    param([int]$Port)
    try {
        $connection = New-Object System.Net.Sockets.TcpClient
        $connection.Connect("localhost", $Port)
        $connection.Close()
        return $true
    }
    catch {
        return $false
    }
}

# Function to wait for service to be ready
function Wait-ForService {
    param(
        [string]$ServiceName,
        [int]$Port,
        [int]$MaxWaitSeconds = 60
    )
    
    Write-ColorOutput "[WAIT] Waiting for $ServiceName (port $Port) to be ready..." "Info"
    $waited = 0
    
    while ($waited -lt $MaxWaitSeconds) {
        if (Test-Port -Port $Port) {
            Write-ColorOutput "[SUCCESS] $ServiceName is ready!" "Success"
            return $true
        }
        
        Start-Sleep -Seconds 2
        $waited += 2
        
        if ($waited % 10 -eq 0) {
            Write-Host "   Still waiting... ($waited/$MaxWaitSeconds seconds)" -ForegroundColor $Colors.Warning
        }
    }
    
    Write-ColorOutput "[ERROR] $ServiceName failed to start within $MaxWaitSeconds seconds" "Error"
    return $false
}

# Phase 1: Start Docker Infrastructure Only
if (-not $SkipDocker) {
    Write-ColorOutput "[DOCKER] Phase 1: Starting Docker Infrastructure Only" "Header"
    Write-Host "[DOCKER] -------------------------------------------" -ForegroundColor $Colors.Header
    
    try {
        # Check if Docker is running
        $dockerInfo = docker info 2>$null
        if ($LASTEXITCODE -ne 0) {
            throw "Docker is not running or not accessible"
        }
        
        Write-ColorOutput "[SUCCESS] Docker is running" "Success"
        
        # Start ONLY infrastructure services
        Write-ColorOutput "[DOCKER] Starting infrastructure services (postgres, nats, seq, pgadmin)..." "Info"
        $composeResult = docker-compose up -d postgres nats seq pgadmin
        
        if ($LASTEXITCODE -eq 0) {
            Write-ColorOutput "[SUCCESS] Docker infrastructure started successfully" "Success"
            
            # Architect: Container logging configuration
            if ($LogContainers.Count -gt 0) {
                Write-ColorOutput "[LOGS] Starting file loggers for specified containers..." "Info"
                New-Item -ItemType Directory -Force -Path ".\Logs\Infrastructure" | Out-Null
                foreach ($container in $LogContainers) {
                    if ($container -eq "none") { continue }
                    Write-ColorOutput " -> Tailing logs for vanan-$container to Logs/Infrastructure/$container.txt" "DarkGray"
                    # Run docker logs in the background and pipe to txt file
                    Start-Process -NoNewWindow -FilePath "docker" -ArgumentList "logs -f vanan-$container" -RedirectStandardOutput ".\Logs\Infrastructure\$container.txt" -RedirectStandardError ".\Logs\Infrastructure\$container-err.txt"
                }
            }
            
            # Wait for critical services
            Write-ColorOutput "[WAIT] Waiting for PostgreSQL to be ready..." "Info"
            Wait-ForService -ServiceName "PostgreSQL" -Port 5432 -MaxWaitSeconds 30
            
            Write-ColorOutput "[WAIT] Waiting for NATS to be ready..." "Info"
            Wait-ForService -ServiceName "NATS" -Port 4222 -MaxWaitSeconds 15
            
            Write-ColorOutput "[WAIT] Waiting for Seq to be ready..." "Info"
            Wait-ForService -ServiceName "Seq" -Port 8081 -MaxWaitSeconds 15
            
        } else {
            Write-ColorOutput "[ERROR] Failed to start Docker infrastructure" "Error"
            Write-Host "Error output:" -ForegroundColor $Colors.Error
            $composeResult
            exit 1
        }
        
    }
    catch {
        Write-ColorOutput "[ERROR] Docker setup failed: $($_.Exception.Message)" "Error"
        exit 1
    }
    
    Write-Host ""
} else {
    Write-ColorOutput "[SKIP] Skipping Docker infrastructure (as requested)" "Warning"
    Write-Host ""
}

# Phase 2: Build .NET Projects (if needed)
if (-not $SkipBuild) {
    Write-ColorOutput "[BUILD] Phase 2: Building .NET Projects" "Header"
    Write-Host "[BUILD] -----------------------------------" -ForegroundColor $Colors.Header
    
    $projects = @(
        "3_CoreHub/VanAn.CoreHub.csproj",
        "2_Gateway/VanAn.Gateway.csproj",
        "5_WebApps/KhachLink/VanAn.KhachLink.csproj",
        "5_WebApps/ShopERP/VanAn.ShopERP.csproj"
    )
    
    foreach ($project in $projects) {
        Write-ColorOutput "[BUILD] Building $project..." "Info"
        $buildResult = dotnet build $project --configuration Development --no-restore
        
        if ($LASTEXITCODE -eq 0) {
            Write-ColorOutput "[SUCCESS] Build successful: $project" "Success"
        } else {
            Write-ColorOutput "[ERROR] Build failed: $project" "Error"
            Write-Host "Error output:" -ForegroundColor $Colors.Error
            $buildResult
            exit 1
        }
    }
    
    Write-Host ""
} else {
    Write-ColorOutput "[SKIP] Skipping .NET build (as requested)" "Warning"
    Write-Host ""
}

# Phase 3: Start .NET Applications Locally
Write-ColorOutput "[APPS] Phase 3: Starting .NET Applications Locally" "Header"
Write-Host "[APPS] -----------------------------------------" -ForegroundColor $Colors.Header

$services = @(
    # CoreHub is a class library, not a runnable application
    # It is hosted by Gateway and other applications
    @{
        Name = "Gateway"
        Project = "2_Gateway/VanAn.Gateway.csproj"
        Port = 5001
        URL = "Gateway API (hosts CoreHub services)"
        ProcessName = "VanAn-Gateway"
    },
    @{
        Name = "KhachLink"
        Project = "5_WebApps/KhachLink/VanAn.KhachLink.csproj"
        Port = 5002
        URL = "KhachLink Customer Portal"
        ProcessName = "VanAn-KhachLink"
    },
    @{
        Name = "ShopERP"
        Project = "5_WebApps/ShopERP/VanAn.ShopERP.csproj"
        Port = 5003
        URL = "ShopERP Management"
        ProcessName = "VanAn-ShopERP"
    }
)

$startedProcesses = @()

foreach ($service in $services) {
    Write-ColorOutput "[START] Starting $($service.Name) locally..." "Service"
    
    try {
        if ($SeparateWindows) {
            # Start in separate terminal window for Hot Reload and debugging
            # Dynamically construct argument list with explicit "dotnet" command
            $argumentList = @("new-tab", "--title", $service.Name, "dotnet", "run", "--project", $service.Project, "--urls", "http://localhost:$($service.Port)")
            
            # Add logging configuration based on DisableFileLog parameter
            if ($DisableFileLog) {
                $argumentList += "--LoggingConfig:EnableFileLogging=false"
            } else {
                $argumentList += "--LoggingConfig:EnableFileLogging=true"
            }
            
            $startArgs = @{
                FilePath = "wt"
                ArgumentList = $argumentList
                PassThru = $true
            }
        } else {
            # Start in background process
            # FilePath is "dotnet", ArgumentList starts with "run"
            $argumentList = @("run", "--project", $service.Project, "--urls", "http://localhost:$($service.Port)")
            
            # Add logging configuration based on DisableFileLog parameter
            if ($DisableFileLog) {
                $argumentList += "--LoggingConfig:EnableFileLogging=false"
            } else {
                $argumentList += "--LoggingConfig:EnableFileLogging=true"
            }
            
            $startArgs = @{
                FilePath = "dotnet"
                ArgumentList = $argumentList
                PassThru = $true
                WindowStyle = "Hidden"
            }
        }
        
        $process = Start-Process @startArgs
        $startedProcesses += $process
        
        Write-ColorOutput "[SUCCESS] $($service.Name) started (PID: $($process.Id))" "Success"
        
        # Give the service a moment to start
        Start-Sleep -Seconds 3
        
        # Check if the service is responding
        if (Wait-ForService -ServiceName $service.Name -Port $service.Port -MaxWaitSeconds 30) {
            Write-ColorOutput "[SUCCESS] $($service.URL) is accessible!" "Success"
        } else {
            Write-ColorOutput "[WARNING] $($service.Name) started but not yet responding on port $($service.Port)" "Warning"
        }
    }
    catch {
        Write-ColorOutput "[ERROR] Failed to start $($service.Name): $($_.Exception.Message)" "Error"
    }
    
    Write-Host ""
}

# Phase 4: Display Service URLs
Write-ColorOutput "[URLS] Phase 4: Service URLs" "Header"
Write-Host "[URLS] ------------------------" -ForegroundColor $Colors.Header

Write-Host "[SUCCESS] Van An Ecosystem is now running!" -ForegroundColor $Colors.Success
Write-Host "[ARCH] Infrastructure: Docker | Applications: Local .NET" -ForegroundColor $Colors.Info
Write-Host ""

Write-ColorOutput "[ACCESS] Quick Access Links:" "Info"
Write-Host ""

# Display URLs with clickable formatting
$urls = @(
    @{ Port = 5001; Name = "Gateway API"; Description = "API Gateway with CoreHub Services"; Icon = "[API]" },
    @{ Port = 5002; Name = "KhachLink"; Description = "Customer Ordering Portal"; Icon = "[SHOP]" },
    @{ Port = 5003; Name = "ShopERP"; Description = "Management System"; Icon = "[ERP]" },
    @{ Port = 5050; Name = "pgAdmin"; Description = "Database Management"; Icon = "[DB]" },
    @{ Port = 8081; Name = "Seq Logs"; Description = "Log Monitoring"; Icon = "[LOG]" }
)

foreach ($url in $urls) {
    $fullUrl = "http://${BaseIP}:$($url.Port)"
    Write-Host "$($url.Icon) $($url.Name): " -NoNewline
    Write-Host $fullUrl -ForegroundColor $Colors.URL
    Write-Host "   $($url.Description)" -ForegroundColor $Colors.Info
    Write-Host ""
}

Write-ColorOutput "[DASHBOARD] Dashboard:" "Info"
$dashboardUrl = "http://${BaseIP}/VanAn_Dashboard.html"
Write-Host "[WEB] Ecosystem Dashboard: " -NoNewline
Write-Host $dashboardUrl -ForegroundColor $Colors.URL
Write-Host ""

# Phase 5: Health Check Summary
Write-ColorOutput "[HEALTH] Phase 5: Health Check Summary" "Header"
Write-Host "[HEALTH] ------------------------------" -ForegroundColor $Colors.Header

$healthyServices = 0
$totalServices = $urls.Count

foreach ($url in $urls) {
    if (Test-Port -Port $url.Port) {
        Write-ColorOutput "[SUCCESS] $($url.Name) (port $($url.Port)) - HEALTHY" "Success"
        $healthyServices++
    } else {
        Write-ColorOutput "[ERROR] $($url.Name) (port $($url.Port)) - UNHEALTHY" "Error"
    }
}

Write-Host ""
Write-ColorOutput "[SUMMARY] Health Summary: $healthyServices/$totalServices services healthy" "Info"

if ($healthyServices -eq $totalServices) {
    Write-ColorOutput "[SUCCESS] All services are running perfectly!" "Success"
} elseif ($healthyServices -gt 0) {
    Write-ColorOutput "[WARNING] Some services may still be starting up..." "Warning"
} else {
    Write-ColorOutput "[ERROR] No services are responding. Please check the logs." "Error"
}

# Phase 6: Management Commands
Write-Host ""
Write-ColorOutput "[COMMANDS] Management Commands:" "Header"
Write-Host "[COMMANDS] ----------------------------" -ForegroundColor $Colors.Header

Write-ColorOutput "[DOCKER] View infrastructure status:" "Info"
Write-Host "docker-compose ps" -ForegroundColor $Colors.URL
Write-Host ""

Write-ColorOutput "[LOGS] View infrastructure logs:" "Info"
Write-Host "docker-compose logs -f" -ForegroundColor $Colors.URL
Write-Host ""

Write-ColorOutput "[STOP] Stop infrastructure only:" "Info"
Write-Host "docker-compose down" -ForegroundColor $Colors.URL
Write-Host ""

Write-ColorOutput "[RESTART] Restart infrastructure:" "Info"
Write-Host "docker-compose restart" -ForegroundColor $Colors.URL
Write-Host ""

Write-ColorOutput "[LOGGING] Start with file logging enabled (default):" "Info"
Write-Host ".\Start-VanAnEcosystem.ps1" -ForegroundColor $Colors.URL
Write-Host ""

Write-ColorOutput "[LOGGING] Start with file logging disabled:" "Info"
Write-Host ".\Start-VanAnEcosystem.ps1 -DisableFileLog" -ForegroundColor $Colors.URL
Write-Host ""

Write-ColorOutput "[BUILD] Rebuild .NET projects:" "Info"
Write-Host "dotnet build 3_CoreHub/VanAn.CoreHub.csproj" -ForegroundColor $Colors.URL
Write-Host "dotnet build 2_Gateway/VanAn.Gateway.csproj" -ForegroundColor $Colors.URL
Write-Host "dotnet build 5_WebApps/KhachLink/VanAn.KhachLink.csproj" -ForegroundColor $Colors.URL
Write-Host "dotnet build 5_WebApps/ShopERP/VanAn.ShopERP.csproj" -ForegroundColor $Colors.URL
Write-Host ""

# Development Information
Write-Host ""
Write-ColorOutput "[DEV] Development Information:" "Header"
Write-Host "[DEV] ---------------------------" -ForegroundColor $Colors.Header

Write-ColorOutput "[LOGGING] File Logging: $(-not $DisableFileLog)" "Info"
Write-ColorOutput "[LOGGING] Use -DisableFileLog to disable file logging" "Info"
Write-Host ""

Write-ColorOutput "[HOT-RELOAD] Hot Reload is enabled for all .NET applications" "Info"
Write-ColorOutput "[DEBUGGING] Each application runs in its own process for easy debugging" "Info"
Write-ColorOutput "[LOGS] Application logs appear in their respective terminal windows" "Info"
Write-Host ""

if ($SeparateWindows) {
    Write-ColorOutput "[WINDOWS] Applications are running in separate Windows Terminal tabs" "Info"
    Write-ColorOutput "[SWITCH] Use Ctrl+Tab to switch between application windows" "Info"
} else {
    Write-ColorOutput "[BACKGROUND] Applications are running in background processes" "Info"
    Write-ColorOutput "[TASKS] Use Task Manager to view and manage individual processes" "Info"
}

Write-Host ""
Write-ColorOutput "[COMPLETE] Van An Ecosystem startup completed!" "Success"
Write-ColorOutput "[THANKS] Thank you for using Van An Ecosystem!" "Info"
