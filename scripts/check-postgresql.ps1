# VanAn Ecosystem - PostgreSQL Data Check PowerShell Script
# Version: 1.0 | Last Updated: 06/04/2026

param(
    [string]$Server = "localhost",
    [string]$Port = "5432",
    [string]$Database = "VanAnCoreHub",
    [string]$Username = "postgres",
    [string]$Password = "",
    [string]$OutputFile = "logs\postgresql-check-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "VanAn Ecosystem - PostgreSQL Data Check" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Create logs directory if not exists
if (-not (Test-Path "logs")) {
    New-Item -ItemType Directory -Path "logs" | Out-Null
}

# Check if psql is installed
try {
    $psqlVersion = & psql --version 2>$null
    Write-Host "✅ Found: $psqlVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ ERROR: psql (PostgreSQL client) not found" -ForegroundColor Red
    Write-Host "Please install PostgreSQL client tools:" -ForegroundColor Yellow
    Write-Host "1. Download from: https://www.postgresql.org/download/windows/" -ForegroundColor Yellow
    Write-Host "2. Add psql to PATH" -ForegroundColor Yellow
    exit 1
}

# Test connection
Write-Host "Testing connection to PostgreSQL..." -ForegroundColor Yellow
try {
    if ($Password) {
        $env:PGPASSWORD = $Password
    }
    
    $connectionTest = & psql -h $Server -p $Port -U $Username -d $Database -c "SELECT version();" -t 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Connection successful" -ForegroundColor Green
    } else {
        throw "Connection failed"
    }
} catch {
    Write-Host "❌ ERROR: Cannot connect to PostgreSQL" -ForegroundColor Red
    Write-Host "Server: $Server`:$Port" -ForegroundColor Red
    Write-Host "Database: $Database" -ForegroundColor Red
    Write-Host "Username: $Username" -ForegroundColor Red
    Write-Host "Please check:" -ForegroundColor Yellow
    Write-Host "1. PostgreSQL service is running" -ForegroundColor Yellow
    Write-Host "2. Connection parameters are correct" -ForegroundColor Yellow
    Write-Host "3. Firewall allows connection" -ForegroundColor Yellow
    exit 1
}

# Run data check script
Write-Host "Running comprehensive data check..." -ForegroundColor Yellow
Write-Host "Output will be saved to: $OutputFile" -ForegroundColor Gray
Write-Host ""

try {
    # Create timestamp for report
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Add-Content -Path $OutputFile -Value "VanAn Ecosystem - PostgreSQL Data Check Report"
    Add-Content -Path $OutputFile -Value "Generated: $timestamp"
    Add-Content -Path $OutputFile -Value "Server: $Server`:$Port"
    Add-Content -Path $OutputFile -Value "Database: $Database"
    Add-Content -Path $OutputFile -Value "========================================"
    Add-Content -Path $OutputFile -Value ""

    # Run SQL script
    & psql -h $Server -p $Port -U $Username -d $Database -f "scripts\check-postgresql-data.sql" | Out-File -Append -FilePath $OutputFile

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Data check completed successfully" -ForegroundColor Green
        
        # Display summary
        Write-Host ""
        Write-Host "=== QUICK SUMMARY ===" -ForegroundColor Yellow
        
        # Get tenant count
        $tenantCount = & psql -h $Server -p $Port -U $Username -d $Database -c "SELECT COUNT(DISTINCT TenantId) FROM `"Orders`" WHERE TenantId IS NOT NULL;" -t 2>$null
        Write-Host "Total Tenants: $tenantCount" -ForegroundColor White
        
        # Get order count
        $orderCount = & psql -h $Server -p $Port -U $Username -d $Database -c "SELECT COUNT(*) FROM `"Orders`" WHERE TenantId IS NOT NULL;" -t 2>$null
        Write-Host "Total Orders: $orderCount" -ForegroundColor White
        
        # Get product count
        $productCount = & psql -h $Server -p $Port -U $Username -d $Database -c "SELECT COUNT(*) FROM `"Products`" WHERE TenantId IS NOT NULL;" -t 2>$null
        Write-Host "Total Products: $productCount" -ForegroundColor White
        
        # Get total revenue
        $totalRevenue = & psql -h $Server -p $Port -U $Username -d $Database -c "SELECT COALESCE(SUM(TotalAmount), 0) FROM `"Orders`" WHERE TenantId IS NOT NULL;" -t 2>$null
        Write-Host "Total Revenue: $([math]::Round([decimal]$totalRevenue, 0)) VNĐ" -ForegroundColor Green
        
    } else {
        Write-Host "❌ ERROR: Data check failed" -ForegroundColor Red
        Write-Host "Check the log file for details: $OutputFile" -ForegroundColor Yellow
    }
} catch {
    Write-Host "❌ ERROR: Exception during data check" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
} finally {
    # Clear password from environment
    $env:PGPASSWORD = $null
}

Write-Host ""
Write-Host "Detailed report saved to: $OutputFile" -ForegroundColor Cyan
Write-Host "Press Enter to open the report..." -ForegroundColor Gray
Read-Host

# Open report in notepad
if (Test-Path $OutputFile) {
    notepad $OutputFile
}

Write-Host ""
Write-Host "PostgreSQL data check completed." -ForegroundColor Green
