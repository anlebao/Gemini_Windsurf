# VanAn Ecosystem - SQLite Data Check PowerShell Script
# Version: 1.0 | Last Updated: 06/04/2026

param(
    [string]$DatabasePath = "",
    [string]$NodeType = "KhachLink", # KhachLink, ShopERP, or custom path
    [string]$OutputFile = "logs\sqlite-check-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "VanAn Ecosystem - SQLite Data Check" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Create logs directory if not exists
if (-not (Test-Path "logs")) {
    New-Item -ItemType Directory -Path "logs" | Out-Null
}

# Auto-detect database path if not provided
if (-not $DatabasePath) {
    Write-Host "Auto-detecting SQLite database path..." -ForegroundColor Yellow
    
    switch ($NodeType.ToLower()) {
        "khachlink" {
            $DatabasePath = "5_WebApps\KhachLink\vanan_khachlink.db"
            Write-Host "Detected KhachLink database: $DatabasePath" -ForegroundColor Green
        }
        "shoperp" {
            $DatabasePath = "5_WebApps\ShopERP\vanan_shoperp.db"
            Write-Host "Detected ShopERP database: $DatabasePath" -ForegroundColor Green
        }
        default {
            Write-Host "Please specify DatabasePath or NodeType (KhachLink/ShopERP)" -ForegroundColor Yellow
            Write-Host "Usage: .\check-sqlite.ps1 -NodeType KhachLink" -ForegroundColor Gray
            Write-Host "Usage: .\check-sqlite.ps1 -DatabasePath 'custom\path\to.db'" -ForegroundColor Gray
            exit 1
        }
    }
}

# Convert to absolute path
$DatabasePath = Resolve-Path $DatabasePath -ErrorAction SilentlyContinue
if (-not $DatabasePath) {
    Write-Host "❌ ERROR: Database file not found: $DatabasePath" -ForegroundColor Red
    Write-Host "Please check the path and ensure the database exists" -ForegroundColor Yellow
    exit 1
}

Write-Host "Database: $DatabasePath" -ForegroundColor White

# Check if sqlite3 is installed
try {
    $sqliteVersion = & sqlite3 --version 2>$null
    Write-Host "✅ Found: $sqliteVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ ERROR: sqlite3 not found" -ForegroundColor Red
    Write-Host "Please install SQLite CLI tools:" -ForegroundColor Yellow
    Write-Host "1. Download from: https://www.sqlite.org/download.html" -ForegroundColor Yellow
    Write-Host "2. Add sqlite3.exe to PATH" -ForegroundColor Yellow
    Write-Host "3. Or use: choco install sqlite" -ForegroundColor Yellow
    exit 1
}

# Test database connection
Write-Host "Testing database connection..." -ForegroundColor Yellow
try {
    $connectionTest = & sqlite3 $DatabasePath "SELECT sqlite_version();" 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Connection successful" -ForegroundColor Green
        Write-Host "SQLite Version: $connectionTest" -ForegroundColor Gray
    } else {
        throw "Connection failed"
    }
} catch {
    Write-Host "❌ ERROR: Cannot connect to SQLite database" -ForegroundColor Red
    Write-Host "Database: $DatabasePath" -ForegroundColor Red
    Write-Host "Please check:" -ForegroundColor Yellow
    Write-Host "1. Database file exists and is not corrupted" -ForegroundColor Yellow
    Write-Host "2. File permissions allow reading" -ForegroundColor Yellow
    Write-Host "3. Database is not locked by another process" -ForegroundColor Yellow
    exit 1
}

# Run data check script
Write-Host "Running comprehensive data check..." -ForegroundColor Yellow
Write-Host "Output will be saved to: $OutputFile" -ForegroundColor Gray
Write-Host ""

try {
    # Create timestamp for report
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Add-Content -Path $OutputFile -Value "VanAn Ecosystem - SQLite Data Check Report"
    Add-Content -Path $OutputFile -Value "Generated: $timestamp"
    Add-Content -Path $OutputFile -Value "Node Type: $NodeType"
    Add-Content -Path $OutputFile -Value "Database: $DatabasePath"
    Add-Content -Path $OutputFile -Value "========================================"
    Add-Content -Path $OutputFile -Value ""

    # Run SQL script
    & sqlite3 $DatabasePath < "scripts\check-sqlite-data.sql" | Out-File -Append -FilePath $OutputFile

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Data check completed successfully" -ForegroundColor Green
        
        # Display summary
        Write-Host ""
        Write-Host "=== QUICK SUMMARY ===" -ForegroundColor Yellow
        
        # Get order count
        $orderCount = & sqlite3 $DatabasePath "SELECT COUNT(*) FROM Orders;" 2>$null
        Write-Host "Total Orders: $orderCount" -ForegroundColor White
        
        # Get product count
        $productCount = & sqlite3 $DatabasePath "SELECT COUNT(*) FROM Products;" 2>$null
        Write-Host "Total Products: $productCount" -ForegroundColor White
        
        # Get customer count
        $customerCount = & sqlite3 $DatabasePath "SELECT COUNT(*) FROM Customers;" 2>$null
        Write-Host "Total Customers: $customerCount" -ForegroundColor White
        
        # Get total revenue
        $totalRevenue = & sqlite3 $DatabasePath "SELECT COALESCE(SUM(TotalAmount), 0) FROM Orders;" 2>$null
        Write-Host "Total Revenue: $([math]::Round([decimal]$totalRevenue, 0)) VNĐ" -ForegroundColor Green
        
        # Get sync status
        $syncStatus = & sqlite3 $DatabasePath "SELECT COUNT(CASE WHEN LastSyncedAt IS NOT NULL THEN 1 END) * 100.0 / COUNT(*) FROM Orders;" 2>$null
        Write-Host "Orders Synced: $([math]::Round([decimal]$syncStatus, 1))%" -ForegroundColor $(if ([decimal]$syncStatus -ge 90) { 'Green' } else { 'Yellow' })
        
    } else {
        Write-Host "❌ ERROR: Data check failed" -ForegroundColor Red
        Write-Host "Check the log file for details: $OutputFile" -ForegroundColor Yellow
    }
} catch {
    Write-Host "❌ ERROR: Exception during data check" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
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
Write-Host "SQLite data check completed." -ForegroundColor Green
