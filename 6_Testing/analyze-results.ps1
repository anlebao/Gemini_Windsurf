# VanAn Ecosystem - Performance Test Results Analyzer
# Analyzes 100 Orders in 1 Minute Test Results

param(
    [string]$LogFile = "logs\100-orders-test-latest.txt",
    [string]$JsonFile = "logs\100-orders-metrics-latest.json"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "VanAn Performance Test Results Analyzer" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if files exist
if (-not (Test-Path $LogFile)) {
    Write-Host "ERROR: Log file not found: $LogFile" -ForegroundColor Red
    Write-Host "Please run the test first using run-100-orders-test.bat" -ForegroundColor Yellow
    exit 1
}

Write-Host "Analyzing test results..." -ForegroundColor Green
Write-Host ""

# Extract metrics from log file
Write-Host "=== ORDER CREATION METRICS ===" -ForegroundColor Yellow
$orderStats = Select-String -Path $LogFile -Pattern "ORDER_TEST:" | ForEach-Object {
    $data = $_.Line -replace '.*ORDER_TEST: (.*)', '$1' | ConvertFrom-Json
    $data
}

if ($orderStats) {
    $totalOrders = $orderStats.Count
    $successfulOrders = ($orderStats | Where-Object { $_.success -eq $true }).Count
    $failedOrders = $totalOrders - $successfulOrders
    $successRate = if ($totalOrders -gt 0) { ($successfulOrders / $totalOrders) * 100 } else { 0 }
    
    $avgResponseTime = if ($orderStats.Count -gt 0) {
        ($orderStats | Measure-Object -Property responseTime -Average).Average
    } else { 0 }
    
    $maxResponseTime = if ($orderStats.Count -gt 0) {
        ($orderStats | Measure-Object -Property responseTime -Maximum).Maximum
    } else { 0 }
    
    $minResponseTime = if ($orderStats.Count -gt 0) {
        ($orderStats | Measure-Object -Property responseTime -Minimum).Minimum
    } else { 0 }
    
    Write-Host "Total Orders Attempted: $totalOrders" -ForegroundColor White
    Write-Host "Successful Orders: $successfulOrders" -ForegroundColor Green
    Write-Host "Failed Orders: $failedOrders" -ForegroundColor $(if ($failedOrders -gt 0) { 'Red' } else { 'Green' })
    Write-Host "Success Rate: $([math]::Round($successRate, 2))%" -ForegroundColor $(if ($successRate -ge 90) { 'Green' } else { 'Yellow' })
    Write-Host ""
    Write-Host "=== RESPONSE TIME METRICS ===" -ForegroundColor Yellow
    Write-Host "Average Response Time: $([math]::Round($avgResponseTime, 2))ms" -ForegroundColor White
    Write-Host "Min Response Time: $([math]::Round($minResponseTime, 2))ms" -ForegroundColor Green
    Write-Host "Max Response Time: $([math]::Round($maxResponseTime, 2))ms" -ForegroundColor $(if ($maxResponseTime -lt 500) { 'Green' } else { 'Red' })
    Write-Host ""
    
    # Response time distribution
    Write-Host "=== RESPONSE TIME DISTRIBUTION ===" -ForegroundColor Yellow
    $fastOrders = $orderStats | Where-Object { $_.responseTime -lt 200 }
    $mediumOrders = $orderStats | Where-Object { $_.responseTime -ge 200 -and $_.responseTime -lt 500 }
    $slowOrders = $orderStats | Where-Object { $_.responseTime -ge 500 }
    
    Write-Host "Fast (< 200ms): $($fastOrders.Count) orders" -ForegroundColor Green
    Write-Host "Medium (200-500ms): $($mediumOrders.Count) orders" -ForegroundColor Yellow
    Write-Host "Slow (> 500ms): $($slowOrders.Count) orders" -ForegroundColor Red
    Write-Host ""
    
    # Order value analysis
    Write-Host "=== ORDER VALUE ANALYSIS ===" -ForegroundColor Yellow
    $totalRevenue = ($orderStats | Measure-Object -Property totalAmount -Sum).Sum
    $avgOrderValue = if ($totalOrders -gt 0) { $totalRevenue / $totalOrders } else { 0 }
    
    Write-Host "Total Revenue: $([math]::Round($totalRevenue, 0)) VNĐ" -ForegroundColor Green
    Write-Host "Average Order Value: $([math]::Round($avgOrderValue, 0)) VNĐ" -ForegroundColor White
    Write-Host ""
    
    # Performance assessment
    Write-Host "=== PERFORMANCE ASSESSMENT ===" -ForegroundColor Yellow
    $ordersPerSecond = $totalOrders / 60
    $targetOrdersPerSecond = 100 / 60
    
    Write-Host "Actual Rate: $([math]::Round($ordersPerSecond, 2)) orders/second" -ForegroundColor White
    Write-Host "Target Rate: $([math]::Round($targetOrdersPerSecond, 2)) orders/second" -ForegroundColor Gray
    Write-Host "Achievement: $([math]::Round(($ordersPerSecond / $targetOrdersPerSecond) * 100, 1))%" -ForegroundColor $(if ($ordersPerSecond -ge $targetOrdersPerSecond) { 'Green' } else { 'Yellow' })
    Write-Host ""
    
    # Overall verdict
    Write-Host "=== OVERALL VERDICT ===" -ForegroundColor Yellow
    if ($successRate -ge 95 -and $avgResponseTime -lt 500 -and $ordersPerSecond -ge ($targetOrdersPerSecond * 0.8)) {
        Write-Host "🎉 EXCELLENT: Test passed all criteria!" -ForegroundColor Green
    } elseif ($successRate -ge 90 -and $avgResponseTime -lt 1000) {
        Write-Host "✅ GOOD: Test passed most criteria" -ForegroundColor Yellow
    } else {
        Write-Host "❌ NEEDS IMPROVEMENT: Test failed critical criteria" -ForegroundColor Red
    }
} else {
    Write-Host "No order data found in log file" -ForegroundColor Red
}

# Check for errors
Write-Host ""
Write-Host "=== ERROR ANALYSIS ===" -ForegroundColor Yellow
$errorStats = Select-String -Path $LogFile -Pattern "ERROR_TEST:" | ForEach-Object {
    $data = $_.Line -replace '.*ERROR_TEST: (.*)', '$1' | ConvertFrom-Json
    $data
}

if ($errorStats) {
    Write-Host "Total Errors: $($errorStats.Count)" -ForegroundColor Red
    $errorStats | Group-Object -Property error | ForEach-Object {
        Write-Host "  $($_.Name): $($_.Count) occurrences" -ForegroundColor Red
    }
} else {
    Write-Host "No errors detected" -ForegroundColor Green
}

Write-Host ""
Write-Host "Analysis complete. Detailed logs available in: $LogFile" -ForegroundColor Cyan
Write-Host "Press Enter to exit..." -ForegroundColor Gray
Read-Host
