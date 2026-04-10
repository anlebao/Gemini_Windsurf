# =============================================================================
# START-VANAN.PS1 - Script khởi động hệ sinh thái Vạn An Group
# Tuân thủ Nguyên tắc Vàng: Fail-Fast, Clean Logs, Production-Ready
# =============================================================================

# Configuration
$DOCKER_DESKTOP_PATH = "C:\Program Files\Docker\Docker\Docker Desktop.exe"
$PROJECT_ROOT = Split-Path -Parent $MyInvocation.MyCommand.Path
$COMPOSE_FILE = Join-Path $PROJECT_ROOT "docker-compose.yml"
$HEALTH_CHECK_INTERVAL = 5
$MAX_WAIT_TIME = 300  # 5 minutes max wait time

# LoggerMessage-style logging for production
function Write-LogInfo {
    param([string]$Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$timestamp] [INFO] VạnAn: $Message" -ForegroundColor Green
}

function Write-LogWarning {
    param([string]$Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$timestamp] [WARN] VạnAn: $Message" -ForegroundColor Yellow
}

function Write-LogError {
    param([string]$Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$timestamp] [ERROR] VạnAn: $Message" -ForegroundColor Red
}

# Fail-Fast: Check prerequisites
function Test-Prerequisites {
    Write-LogInfo "Kiểm tra điều kiện khởi động..."
    
    # Check if docker-compose file exists
    if (!(Test-Path $COMPOSE_FILE)) {
        Write-LogError "Không tìm thấy docker-compose.yml tại: $PROJECT_ROOT"
        exit 1
    }
    
    # Check if Docker Desktop executable exists
    if (!(Test-Path $DOCKER_DESKTOP_PATH)) {
        Write-LogError "Không tìm thấy Docker Desktop tại: $DOCKER_DESKTOP_PATH"
        Write-LogInfo "Vui lòng cài đặt Docker Desktop trước khi chạy script này"
        exit 1
    }
    
    Write-LogInfo "✅ Điều kiện khởi động hợp lệ"
}

# TẦNG 2: Verify-Integrity - Pre-deployment validation
function Test-CodeIntegrity {
    Write-LogInfo "🔍 Tầng phòng thủ cửa khẩu: Kiểm tra tính toàn vẹn code..."
    
    # Change to project root
    Push-Location $PROJECT_ROOT
    
    try {
        # Build in Release mode to catch all errors
        Write-LogInfo "Chạy dotnet build VanAn.sln -c Release..."
        $buildResult = & dotnet build VanAn.sln -c Release --verbosity quiet 2>&1
        
        if ($LASTEXITCODE -ne 0) {
            Write-LogError "❌ BUILD FAILED! Code không đạt chuẩn production"
            Write-LogError "Chi tiết lỗi:"
            $buildResult | ForEach-Object { Write-LogError "  $_" }
            Write-LogError "🚫 DỪNG KHỞI ĐỘNG! Vui lòng sửa lỗi trước khi tiếp tục"
            Pop-Location
            exit 1
        }
        
        Write-LogInfo "✅ Code integrity verified - Đạt chuẩn production"
        
        # Check for sensitive data logging
        Write-LogInfo "🔍 Kiểm tra Sensitive Data Logging..."
        $sensitiveLogs = Select-String -Path "*.cs" -Pattern "EnableSensitiveDataLogging\(true\)" -Recurse
        
        if ($sensitiveLogs) {
            Write-LogWarning "⚠️ Phát hiện Sensitive Data Logging vẫn bật:"
            $sensitiveLogs | ForEach-Object { Write-LogWarning "  $($_.FileName):$($_.LineNumber)" }
            Write-LogWarning "Vui lòng tắt để bảo mật production"
        } else {
            Write-LogInfo "✅ Không phát hiện Sensitive Data Logging"
        }
        
    } catch {
        Write-LogError "❌ Lỗi khi kiểm tra tính toàn vẹn: $($_.Exception.Message)"
        Pop-Location
        exit 1
    }
    
    Pop-Location
}

# Check Docker status with proper error handling
function Test-DockerStatus {
    try {
        $null = docker info 2>$null
        return $true
    }
    catch {
        return $false
    }
}

# Start Docker Desktop with validation
function Start-DockerDesktop {
    Write-LogInfo "Docker Desktop chưa chạy. Đang khởi động..."
    
    try {
        Start-Process -FilePath $DOCKER_DESKTOP_PATH -NoNewWindow
        Write-LogInfo "Đã gửi lệnh khởi động Docker Desktop"
    }
    catch {
        Write-LogError "Không thể khởi động Docker Desktop: $_"
        exit 1
    }
    
    # Wait for Docker to be ready with timeout
    $waitTime = 0
    Write-LogInfo "Chờ Docker khởi động (tối đa $MAX_WAIT_TIME giây)..."
    
    while ($waitTime -lt $MAX_WAIT_TIME) {
        if (Test-DockerStatus) {
            Write-LogInfo "✅ Docker Desktop đã sẵn sàng!"
            return $true
        }
        
        Write-LogWarning "Docker chưa sẵn sàng... chờ $HEALTH_CHECK_INTERVAL giây ($($waitTime + $HEALTH_CHECK_INTERVAL)/$MAX_WAIT_TIME)"
        Start-Sleep -Seconds $HEALTH_CHECK_INTERVAL
        $waitTime += $HEALTH_CHECK_INTERVAL
    }
    
    Write-LogError "Docker Desktop không sẵn sàng sau $MAX_WAIT_TIME giây. Vui lòng kiểm tra thủ công."
    exit 1
}

# Main deployment function
function Start-VananEcosystem {
    Write-LogInfo "Khởi động hệ sinh thái Vạn An Group..."
    
    try {
        # Change to project directory
        Set-Location $PROJECT_ROOT
        
        # Run docker-compose with error handling
        Write-LogInfo "Thực thi: docker-compose up -d"
        $composeResult = docker-compose up -d 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-LogInfo "✅ Hệ sinh thái Vạn An khởi động thành công!"
            Write-LogInfo "Kiểm tra trạng thái containers:"
            
            # Show container status
            Start-Sleep -Seconds 3
            docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
            
            Write-LogInfo ""
            Write-LogInfo "🌐 Các endpoint dịch vụ:"
            Write-LogInfo "   Gateway (hosts CoreHub): http://localhost:5001"  
            Write-LogInfo "   KhachLink: http://localhost:5002"
            Write-LogInfo "   ShopERP: http://localhost:5003"
            Write-LogInfo ""
            Write-LogInfo "📊 Health Check:"
            Write-LogInfo "   Gateway: http://localhost:5001/health"
            Write-LogInfo "   KhachLink: http://localhost:5002/health"
            Write-LogInfo "   ShopERP: http://localhost:5003/health"
            Write-LogInfo ""
            Write-LogInfo "🎯 Vạn An Group sẵn sàng phục vụ!"
            
        } else {
            Write-LogError "Lỗi khi khởi động docker-compose:"
            Write-LogError $composeResult
            exit 1
        }
    }
    catch {
        Write-LogError "Lỗi không xác định khi khởi động hệ sinh thái: $_"
        exit 1
}

# Main execution with 3-tier defense
try {
    Write-LogInfo "🚀 Khởi động hệ sinh thái Vạn An Group với 3 tầng phòng thủ..."
    Write-LogInfo ""
    
    # TẦNG 1: Phòng thủ từ phôi (Static Analysis)
    Write-LogInfo "🛡️ TẦNG 1: Phòng thủ từ phôi - Kiểm tra prerequisites..."
    Test-Prerequisites
    
    # TẦNG 2: Phòng thủ cửa khẩu (Pre-deployment Validation)
    Write-LogInfo ""
    Write-LogInfo "🛡️ TẦNG 2: Phòng thủ cửa khẩu - Verify-Integrity..."
    Test-CodeIntegrity
    
    # TẦNG 3: Trung tâm quan sát tập trung (Container Deployment)
    Write-LogInfo ""
    Write-LogInfo "🛡️ TẦNG 3: Trung tâm quan sát tập trung - Deploy containers..."
    
    # Start Docker if needed
    if (!(Test-DockerStatus)) {
        Write-LogInfo "Docker Desktop chưa chạy, đang khởi động..."
        Start-DockerDesktop
        Wait-DockerHealthy
    } else {
        Write-LogInfo "Docker Desktop đã sẵn sàng"
    }
    
    # Deploy containers
    Write-LogInfo "Deploy containers với docker-compose..."
    Push-Location $PROJECT_ROOT
    & docker-compose up -d
    
    if ($LASTEXITCODE -ne 0) {
        Write-LogError "❌ Docker compose failed!"
        Pop-Location
        exit 1
    }
    
    Pop-Location
    
    Write-LogInfo "========================================"
    Write-LogInfo "✨ Hoàn thành khởi động Vạn An Group!"
    Write-LogInfo "========================================"
}
catch {
    Write-LogError "Script thất bại: $_"
    exit 1
}

# Clean exit
exit 0
