# VanAn Ecosystem - Complete Data Check (PostgreSQL + SQLite)
# Version: 1.0 | Last Updated: 06/04/2026

param(
    [switch]$SkipPostgreSQL,
    [switch]$SkipSQLite,
    [string]$PostgreSQLServer = "localhost",
    [string]$PostgreSQLDatabase = "VanAnCoreHub",
    [string]$PostgreSQLUsername = "postgres",
    [string]$PostgreSQLPassword = "",
    [string]$SQLiteNode = "KhachLink" # KhachLink, ShopERP, or Both
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "VanAn Ecosystem - Complete Data Check" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Create logs directory
if (-not (Test-Path "logs")) {
    New-Item -ItemType Directory -Path "logs" | Out-Null
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportFile = "logs\complete-data-check-$timestamp.log"

# Initialize report
Add-Content -Path $reportFile -Value "VanAn Ecosystem - Complete Data Check Report"
Add-Content -Path $reportFile -Value "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Add-Content -Path $reportFile -Value "========================================"
Add-Content -Path $reportFile -Value ""

# ============================================
# POSTGRESQL CHECK
# ============================================
if (-not $SkipPostgreSQL) {
    Write-Host "=== POSTGRESQL (Central Hub) CHECK ===" -ForegroundColor Yellow
    Write-Host ""
    
    try {
        # Set password environment variable
        if ($PostgreSQLPassword) {
            $env:PGPASSWORD = $PostgreSQLPassword
        }
        
        # Test connection
        $connectionTest = & psql -h $PostgreSQLServer -p 5432 -U $PostgreSQLUsername -d $PostgreSQLDatabase -c "SELECT version();" -t 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ PostgreSQL connection successful" -ForegroundColor Green
            
            # Get summary stats
            $tenantCount = & psql -h $PostgreSQLServer -p 5432 -U $PostgreSQLUsername -d $PostgreSQLDatabase -c "SELECT COUNT(DISTINCT TenantId) FROM `"Orders`" WHERE TenantId IS NOT NULL;" -t 2>$null
            $orderCount = & psql -h $PostgreSQLServer -p 5432 -U $PostgreSQLUsername -d $PostgreSQLDatabase -c "SELECT COUNT(*) FROM `"Orders`" WHERE TenantId IS NOT NULL;" -t 2>$null
            $productCount = & psql -h $PostgreSQLServer -p 5432 -U $PostgreSQLUsername -d $PostgreSQLDatabase -c "SELECT COUNT(*) FROM `"Products`" WHERE TenantId IS NOT NULL;" -t 2>$null
            $totalRevenue = & psql -h $PostgreSQLServer -p 5432 -U $PostgreSQLUsername -d $PostgreSQLDatabase -c "SELECT COALESCE(SUM(TotalAmount), 0) FROM `"Orders`" WHERE TenantId IS NOT NULL;" -t 2>$null
            
            Write-Host "Tenants: $tenantCount" -ForegroundColor White
            Write-Host "Orders: $orderCount" -ForegroundColor White
            Write-Host "Products: $productCount" -ForegroundColor White
            Write-Host "Revenue: $([math]::Round([decimal]$totalRevenue, 0)) VNĐ" -ForegroundColor Green
            
            # Add to report
            Add-Content -Path $reportFile -Value "POSTGRESQL CENTRAL HUB SUMMARY"
            Add-Content -Path $reportFile -Value "=========================="
            Add-Content -Path $reportFile -Value "Tenants: $tenantCount"
            Add-Content -Path $reportFile -Value "Orders: $orderCount"
            Add-Content -Path $reportFile -Value "Products: $productCount"
            Add-Content -Path $reportFile -Value "Revenue: $([math]::Round([decimal]$totalRevenue, 0)) VNĐ"
            Add-Content -Path $reportFile -Value ""
            
        } else {
            Write-Host "❌ PostgreSQL connection failed" -ForegroundColor Red
            Add-Content -Path $reportFile -Value "POSTGRESQL: Connection failed"
        }
    } catch {
        Write-Host "❌ PostgreSQL error: $($_.Exception.Message)" -ForegroundColor Red
        Add-Content -Path $reportFile -Value "POSTGRESQL: Error - $($_.Exception.Message)"
    } finally {
        $env:PGPASSWORD = $null
    }
} else {
    Write-Host "⏭️ Skipping PostgreSQL check" -ForegroundColor Gray
}

Write-Host ""

# ============================================
# SQLITE CHECK
# ============================================
if (-not $SkipSQLite) {
    Write-Host "=== SQLITE (Edge Nodes) CHECK ===" -ForegroundColor Yellow
    Write-Host ""
    
    $nodes = @()
    if ($SQLiteNode -eq "Both") {
        $nodes += @("KhachLink", "ShopERP")
    } else {
        $nodes += $SQLiteNode
    }
    
    foreach ($node in $nodes) {
        Write-Host "--- Checking $node ---" -ForegroundColor Cyan
        
        $dbPath = switch ($node) {
            "KhachLink" { "5_WebApps\KhachLink\vanan_khachlink.db" }
            "ShopERP" { "5_WebApps\ShopERP\vanan_shoperp.db" }
            default { $node }
        }
        
        $fullPath = Resolve-Path $dbPath -ErrorAction SilentlyContinue
        if (-not $fullPath) {
            Write-Host "❌ Database not found: $dbPath" -ForegroundColor Red
            Add-Content -Path $reportFile -Value "$node: Database not found"
            continue
        }
        
        try {
            # Test connection
            $connectionTest = & sqlite3 $fullPath "SELECT sqlite_version();" 2>$null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ $node connection successful" -ForegroundColor Green
                
                # Get summary stats
                $orderCount = & sqlite3 $fullPath "SELECT COUNT(*) FROM Orders;" 2>$null
                $productCount = & sqlite3 $fullPath "SELECT COUNT(*) FROM Products;" 2>$null
                $customerCount = & sqlite3 $fullPath "SELECT COUNT(*) FROM Customers;" 2>$null
                $totalRevenue = & sqlite3 $fullPath "SELECT COALESCE(SUM(TotalAmount), 0) FROM Orders;" 2>$null
                $syncStatus = & sqlite3 $fullPath "SELECT COUNT(CASE WHEN LastSyncedAt IS NOT NULL THEN 1 END) * 100.0 / COUNT(*) FROM Orders;" 2>$null
                
                Write-Host "Orders: $orderCount" -ForegroundColor White
                Write-Host "Products: $productCount" -ForegroundColor White
                Write-Host "Customers: $customerCount" -ForegroundColor White
                Write-Host "Revenue: $([math]::Round([decimal]$totalRevenue, 0)) VNĐ" -ForegroundColor Green
                Write-Host "Synced: $([math]::Round([decimal]$syncStatus, 1))%" -ForegroundColor $(if ([decimal]$syncStatus -ge 90) { 'Green' } else { 'Yellow' })
                
                # Add to report
                Add-Content -Path $reportFile -Value "$node EDGE NODE SUMMARY"
                Add-Content -Path $reportFile -Value "======================"
                Add-Content -Path $reportFile -Value "Orders: $orderCount"
                Add-Content -Path $reportFile -Value "Products: $productCount"
                Add-Content -Path $reportFile -Value "Customers: $customerCount"
                Add-Content -Path $reportFile -Value "Revenue: $([math]::Round([decimal]$totalRevenue, 0)) VNĐ"
                Add-Content -Path $reportFile -Value "Synced: $([math]::Round([decimal]$syncStatus, 1))%"
                Add-Content -Path $reportFile -Value ""
                
            } else {
                Write-Host "❌ $node connection failed" -ForegroundColor Red
                Add-Content -Path $reportFile -Value "$node: Connection failed"
            }
        } catch {
            Write-Host "❌ $node error: $($_.Exception.Message)" -ForegroundColor Red
            Add-Content -Path $reportFile -Value "$node: Error - $($_.Exception.Message)"
        }
    }
} else {
    Write-Host "⏭️ Skipping SQLite check" -ForegroundColor Gray
}

# ============================================
# SUMMARY
# ============================================
Write-Host ""
Write-Host "=== COMPLETE CHECK SUMMARY ===" -ForegroundColor Yellow
Write-Host "Report saved to: $reportFile" -ForegroundColor Cyan
Write-Host ""
Write-Host "Press Enter to open the complete report..." -ForegroundColor Gray
Read-Host

# Open report
if (Test-Path $reportFile) {
    notepad $reportFile
}

Write-Host ""
Write-Host "Complete data check finished." -ForegroundColor Green
