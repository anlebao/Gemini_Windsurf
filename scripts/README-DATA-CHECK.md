# VanAn Ecosystem - Data Verification Guide

## 📋 Tổng quan

VanAn Ecosystem sử dụng kiến trúc Hybrid Data:
- **PostgreSQL (Central Hub)**: Database trung tâm, source of truth
- **SQLite (Edge Nodes)**: Local database cho KhachLink & ShopERP

## 🚀 Quick Start

### 1. Kiểm tra tất cả data (Recommended)
```powershell
cd scripts
.\check-all-data.ps1
```

### 2. Chỉ kiểm tra PostgreSQL
```powershell
cd scripts
.\check-postgresql.ps1 -Server localhost -Database VanAnCoreHub -Username postgres
```

### 3. Chỉ kiểm tra SQLite
```powershell
cd scripts
.\check-sqlite.ps1 -NodeType KhachLink
# hoặc
.\check-sqlite.ps1 -NodeType ShopERP
```

## 🗄️ PostgreSQL (Central Hub)

### Connection Parameters
- **Server**: localhost (default)
- **Port**: 5432
- **Database**: VanAnCoreHub
- **Username**: postgres (default)

### Commands
```powershell
# Basic check
.\check-postgresql.ps1

# Custom connection
.\check-postgresql.ps1 -Server "192.168.1.100" -Database "VanAnProd" -Username "vanan_user" -Password "your_password"

# Save to custom file
.\check-postgresql.ps1 -OutputFile "custom-report.log"
```

### What's Checked
- Database size and table counts
- Tenant data distribution
- Order statistics by tenant
- Product performance
- Sync status verification
- Data integrity checks
- Recent activity (last 24 hours)
- Performance metrics

## 📱 SQLite (Edge Nodes)

### Node Types
- **KhachLink**: Customer-facing app database
- **ShopERP**: Staff management app database

### Commands
```powershell
# Auto-detect KhachLink database
.\check-sqlite.ps1 -NodeType KhachLink

# Auto-detect ShopERP database
.\check-sqlite.ps1 -NodeType ShopERP

# Custom database path
.\check-sqlite.ps1 -DatabasePath "custom\path\database.db"

# Check both nodes
.\check-all-data.ps1 -SQLiteNode Both
```

### What's Checked
- Database size and WAL mode
- Table record counts
- Business data analysis
- Sync status (LastSyncedAt)
- Data integrity checks
- Recent activity
- Index information

## 📊 Report Analysis

### Key Metrics to Monitor

#### PostgreSQL Central Hub
- **Tenant Count**: Số lượng tenant hoạt động
- **Total Orders**: Tổng số orders toàn hệ thống
- **Total Revenue**: Tổng doanh thu
- **Sync Status**: Tình trạng đồng bộ từ edge nodes

#### SQLite Edge Nodes
- **Local Orders**: Orders trên local database
- **Sync Percentage**: % orders đã đồng bộ lên central
- **Pending Sync**: Orders chờ đồng bộ
- **WAL Mode**: Verify WAL mode enabled for performance

### Alert Thresholds
- **Sync Rate < 90%**: Cần kiểm tra kết nối
- **Orphaned Records > 0**: Data integrity issue
- **Database Size > 1GB**: Cần optimize
- **Response Time > 500ms**: Performance issue

## 🔧 Troubleshooting

### Common Issues

#### PostgreSQL Connection Failed
```powershell
# Check PostgreSQL service
Get-Service postgres*

# Test connection manually
psql -h localhost -p 5432 -U postgres -d VanAnCoreHub

# Check firewall
Test-NetConnection -ComputerName localhost -Port 5432
```

#### SQLite Database Locked
```powershell
# Check what's using the database
Get-Process | Where-Object {$_.ProcessName -like "*sqlite*" -or $_.ProcessName -like "*dotnet*"}

# Stop web apps
Stop-Process -Name "dotnet" -Force

# Check database integrity
sqlite3 database.db "PRAGMA integrity_check;"
```

#### Missing Tools
```powershell
# Install PostgreSQL client
choco install postgresql

# Install SQLite
choco install sqlite

# Add to PATH manually
$env:PATH += ";C:\Program Files\PostgreSQL\16\bin"
$env:PATH += ";C:\Program Files\SQLite"
```

## 📈 Performance Monitoring

### Regular Checks
```powershell
# Daily health check
.\check-all-data.ps1 > "logs\daily-health-$(Get-Date -Format 'yyyyMMdd').log"

# Weekly detailed analysis
.\check-postgresql.ps1 -OutputFile "logs\weekly-postgres-$(Get-Date -Format 'yyyyMMdd').log"
.\check-sqlite.ps1 -NodeType Both -OutputFile "logs\weekly-sqlite-$(Get-Date -Format 'yyyyMMdd').log"
```

### Automation Script
```powershell
# Create scheduled task
$action = New-ScheduledTaskAction -Execute "PowerShell.exe" -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$PSScriptRoot\check-all-data.ps1`""
$trigger = New-ScheduledTaskTrigger -Daily -At 9am
Register-ScheduledTask -Action $action -Trigger $trigger -TaskName "VanAnDataHealthCheck" -Description "Daily VanAn data health check"
```

## 📋 File Locations

### Database Files
- **PostgreSQL**: Managed by PostgreSQL service
- **KhachLink SQLite**: `5_WebApps\KhachLink\vanan_khachlink.db`
- **ShopERP SQLite**: `5_WebApps\ShopERP\vanan_shoperp.db`

### Log Files
- **All Reports**: `scripts\logs\`
- **PostgreSQL**: `logs\postgresql-check-*.log`
- **SQLite**: `logs\sqlite-check-*.log`
- **Complete**: `logs\complete-data-check-*.log`

### SQL Scripts
- **PostgreSQL**: `scripts\check-postgresql-data.sql`
- **SQLite**: `scripts\check-sqlite-data.sql`

## 🎯 Best Practices

1. **Daily Checks**: Run basic health check daily
2. **Weekly Analysis**: Run detailed analysis weekly
3. **Monthly Review**: Review trends and performance
4. **Alert Setup**: Monitor sync rates and error counts
5. **Backup Verification**: Check backup integrity regularly

## 📞 Support

For issues with data checking:
1. Check log files for detailed error messages
2. Verify database connections and permissions
3. Ensure all services are running
4. Review the troubleshooting section above

---

**Ready to check your VanAn data? Run `.\check-all-data.ps1` to get started! 🚀**
