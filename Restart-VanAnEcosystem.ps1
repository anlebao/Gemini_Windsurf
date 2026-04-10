<#
.SYNOPSIS
Script to completely clean, kill ghost processes, and restart the Van An Ecosystem.
#>

Write-Host "===================================================" -ForegroundColor Cyan
Write-Host " 🔄 VAN AN ECOSYSTEM - CLEAN RESTART SEQUENCE 🔄 " -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan

Write-Host "`n[1/4] 🐳 Stopping Docker infrastructure..." -ForegroundColor Yellow
docker-compose down

Write-Host "`n[2/4] 💀 Terminating lingering .NET processes..." -ForegroundColor Yellow
# Using 2>$null to suppress errors if no dotnet processes are currently running
taskkill /F /IM dotnet.exe /T 2>$null

Write-Host "`n[3/4] 🧹 Cleaning corrupted build caches..." -ForegroundColor Yellow
dotnet clean

Write-Host "`n[4/4] 🚀 Launching Van An Ecosystem in High-Performance Mode..." -ForegroundColor Green
.\Start-VanAnEcosystem.ps1 -SeparateWindows

Write-Host "`n✅ Restart sequence initiated! Please check your separate terminal windows." -ForegroundColor Cyan
Write-Host "👉 Open VanAn_Dashboard.html in your browser to monitor real-time status." -ForegroundColor Cyan
