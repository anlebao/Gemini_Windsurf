# NATS + SQLite Deployment Guide

## Overview

This guide provides step-by-step instructions for deploying the Van An ecosystem with NATS message broker and SQLite database, including the SQLite concurrency solution and Outbox pattern implementation.

## Prerequisites

### System Requirements
- **Operating System**: Windows 10+, Linux (Ubuntu 20.04+), macOS 11+
- **.NET Runtime**: .NET 8.0 Runtime or SDK
- **SQLite**: Version 3.35+ (WAL mode support)
- **NATS Server**: Version 2.8+
- **RAM**: Minimum 2GB, Recommended 4GB+
- **Storage**: Minimum 10GB free space
- **Network**: Port 4222 (NATS), 5000-5010 (Applications)

### Software Dependencies
```bash
# Windows (PowerShell)
# Install .NET 8.0 Runtime
winget install Microsoft.DotNet.Runtime.8

# Download SQLite CLI
Invoke-WebRequest -Uri "https://www.sqlite.org/2024/sqlite-tools-win3400000-x64-3410000.zip" -OutFile "sqlite-tools.zip"
Expand-Archive sqlite-tools.zip -DestinationPath C:\sqlite

# Download NATS Server
Invoke-WebRequest -Uri "https://github.com/nats-io/nats-server/releases/download/v2.10.7/nats-server-v2.10.7-windows-amd64.zip" -OutFile "nats-server.zip"
Expand-Archive nats-server.zip -DestinationPath C:\nats
```

```bash
# Linux (Ubuntu/Debian)
# Install .NET 8.0 Runtime
wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y aspnetcore-runtime-8.0

# Install SQLite
sudo apt-get install -y sqlite3 libsqlite3-dev

# Install NATS Server
curl -L https://github.com/nats-io/nats-server/releases/download/v2.10.7/nats-server-v2.10.7-linux-amd64.tar.gz | tar xz
sudo mv nats-server-v2.10.7-linux-amd64/nats-server /usr/local/bin/
```

```bash
# macOS (Homebrew)
# Install .NET 8.0 Runtime
brew install dotnet

# Install SQLite
brew install sqlite

# Install NATS Server
brew install nats-server
```

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    Van An Ecosystem                        │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐        │
│  │   ShopERP   │  │   CoreHub   │  │   Gateway   │        │
│  │  (Web App)  │  │ (Background)│  │   (API)     │        │
│  └─────────────┘  └─────────────┘  └─────────────┘        │
│         │                 │                 │             │
│         └─────────────────┼─────────────────┘             │
│                           │                               │
│  ┌─────────────────────────────────────────────────────┐ │
│  │              NATS Message Broker                     │ │
│  │              (nats://localhost:4222)                │ │
│  └─────────────────────────────────────────────────────┘ │
│                           │                               │
│  ┌─────────────────────────────────────────────────────┐ │
│  │              SQLite Database                          │ │
│  │           (vanan_shoperp.db)                         │ │
│  └─────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

## Installation Steps

### Step 1: Database Setup

#### 1.1 Initialize SQLite Database
```bash
# Create database directory
mkdir -p /opt/vanan/data
cd /opt/vanan/data

# Initialize database with optimizations
sqlite3 vanan_shoperp.db "
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA cache_size = 10000;
PRAGMA temp_store = memory;
PRAGMA mmap_size = 268435456;
PRAGMA foreign_keys = ON;
"

# Verify database creation
sqlite3 vanan_shoperp.db ".tables"
```

#### 1.2 Database Configuration File
```bash
# Create database configuration
cat > /opt/vanan/config/database.conf << EOF
# SQLite Configuration
journal_mode=WAL
synchronous=NORMAL
cache_size=10000
temp_store=memory
mmap_size=268435456
foreign_keys=ON
query_only=FALSE
EOF
```

### Step 2: NATS Server Setup

#### 2.1 NATS Configuration
```bash
# Create NATS configuration directory
mkdir -p /opt/vanan/nats

# Create NATS server configuration
cat > /opt/vanan/nats/nats.conf << EOF
# NATS Server Configuration
port: 4222
http_port: 8222
monitor_port: 6422

# JetStream enabled for persistence
jetstream: {
  store_dir: "/opt/vanan/nats/jetstream"
  max_memory_store: 1GB
  max_file_store: 10GB
}

# Authorization (optional)
authorization: {
  users: [
    {
      user: "vanan"
      password: "vanan123"
      permissions: {
        publish: ">"
        subscribe: ">"
      }
    }
  ]
}

# Logging
log_file: "/opt/vanan/logs/nats.log"
logtime: true
debug: false
trace: false
EOF
```

#### 2.2 Start NATS Server
```bash
# Create logs directory
mkdir -p /opt/vanan/logs

# Start NATS server
nats-server -c /opt/vanan/nats/nats.conf -l /opt/vanan/logs/nats.log &

# Verify NATS is running
curl http://localhost:8222/varz | jq '.server_id'
```

#### 2.3 Verify NATS Connectivity
```bash
# Test NATS connection
nats-cli stream add ORDERS --subjects "orders.>" --storage file --retention 1h
nats-cli stream ls
nats-cli consumer add ORDERS orders-worker --deliver-policy all
```

### Step 3: Application Deployment

#### 3.1 Create Application Directory
```bash
# Create application directories
mkdir -p /opt/vanan/apps
mkdir -p /opt/vanan/config
mkdir -p /opt/vanan/logs
```

#### 3.2 Build Applications
```bash
# Navigate to source directory
cd /path/to/VibeCoding/Gemini_Windsurf

# Build ShopERP application
dotnet publish 5_WebApps/ShopERP/VanAn.ShopERP.csproj \
    -c Release \
    -o /opt/vanan/apps/ShopERP \
    --self-contained false \
    --runtime linux-x64

# Build CoreHub application
dotnet publish 3_CoreHub/VanAn.CoreHub.csproj \
    -c Release \
    -o /opt/vanan/apps/CoreHub \
    --self-contained false \
    --runtime linux-x64

# Build Gateway application
dotnet publish 2_Gateway/VanAn.Gateway.csproj \
    -c Release \
    -o /opt/vanan/apps/Gateway \
    --self-contained false \
    --runtime linux-x64
```

#### 3.3 Application Configuration
```bash
# Create ShopERP configuration
cat > /opt/vanan/config/ShopERP.appsettings.json << EOF
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=/opt/vanan/data/vanan_shoperp.db"
  },
  "NATS": {
    "Url": "nats://localhost:4222"
  },
  "OrderQueue": {
    "BatchSize": 8,
    "ChannelCapacity": 1000
  },
  "OutboxProcessor": {
    "BatchSize": 15,
    "ErrorDelayMs": 5000,
    "ProcessingIntervalMs": 1000
  },
  "SqliteRetry": {
    "MaxRetries": 3,
    "BaseDelayMs": 100
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "VanAn": "Debug"
    }
  },
  "AllowedHosts": "*"
}
EOF

# Create CoreHub configuration
cat > /opt/vanan/config/CoreHub.appsettings.json << EOF
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=/opt/vanan/data/vanan_shoperp.db"
  },
  "NATS": {
    "Url": "nats://localhost:4222"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "VanAn": "Debug"
    }
  }
}
EOF

# Create Gateway configuration
cat > /opt/vanan/config/Gateway.appsettings.json << EOF
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=/opt/vanan/data/vanan_shoperp.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "VanAn": "Debug"
    }
  },
  "AllowedHosts": "*"
}
EOF
```

### Step 4: Service Configuration

#### 4.1 Create Systemd Services (Linux)
```bash
# Create ShopERP service
cat > /etc/systemd/system/vanan-shoperp.service << EOF
[Unit]
Description=Van An ShopERP Application
After=network.target nats.service

[Service]
Type=notify
User=vanan
Group=vanan
WorkingDirectory=/opt/vanan/apps/ShopERP
ExecStart=/usr/bin/dotnet VanAn.ShopERP.dll
Restart=always
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5000

[Install]
WantedBy=multi-user.target
EOF

# Create CoreHub service
cat > /etc/systemd/system/vanan-corehub.service << EOF
[Unit]
Description=Van An CoreHub Background Services
After=network.target nats.service

[Service]
Type=notify
User=vanan
Group=vanan
WorkingDirectory=/opt/vanan/apps/CoreHub
ExecStart=/usr/bin/dotnet VanAn.CoreHub.dll
Restart=always
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
EOF

# Create Gateway service
cat > /etc/systemd/system/vanan-gateway.service << EOF
[Unit]
Description=Van An API Gateway
After=network.target nats.service

[Service]
Type=notify
User=vanan
Group=vanan
WorkingDirectory=/opt/vanan/apps/Gateway
ExecStart=/usr/bin/dotnet VanAn.Gateway.dll
Restart=always
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5001

[Install]
WantedBy=multi-user.target
EOF

# Create NATS service
cat > /etc/systemd/system/nats.service << EOF
[Unit]
Description=NATS Message Broker
After=network.target

[Service]
Type=simple
User=nats
Group=nats
ExecStart=/usr/local/bin/nats-server -c /opt/vanan/nats/nats.conf
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
EOF
```

#### 4.2 Create Windows Services
```powershell
# Create ShopERP service
New-Service -Name "VanAnShopERP" `
    -BinaryPathName "C:\opt\vanan\apps\ShopERP\VanAn.ShopERP.exe" `
    -DisplayName "Van An ShopERP Application" `
    -Description "Van An ShopERP web application with order processing" `
    -StartupType Automatic

# Create CoreHub service
New-Service -Name "VanAnCoreHub" `
    -BinaryPathName "C:\opt\vanan\apps\CoreHub\VanAn.CoreHub.exe" `
    -DisplayName "Van An CoreHub Services" `
    -Description "Van An background services for accounting and event processing" `
    -StartupType Automatic

# Create Gateway service
New-Service -Name "VanAnGateway" `
    -BinaryPathName "C:\opt\vanan\apps\Gateway\VanAn.Gateway.exe" `
    -DisplayName "Van An API Gateway" `
    -Description "Van An API gateway for external integrations" `
    -StartupType Automatic
```

### Step 5: Start Services

#### 5.1 Linux Services
```bash
# Reload systemd configuration
sudo systemctl daemon-reload

# Create vanan user
sudo useradd -r -s /bin/false vanan
sudo chown -R vanan:vanan /opt/vanan

# Start services
sudo systemctl start nats
sudo systemctl start vanan-shoperp
sudo systemctl start vanan-corehub
sudo systemctl start vanan-gateway

# Enable services to start on boot
sudo systemctl enable nats
sudo systemctl enable vanan-shoperp
sudo systemctl enable vanan-corehub
sudo systemctl enable vanan-gateway

# Check service status
sudo systemctl status nats
sudo systemctl status vanan-shoperp
sudo systemctl status vanan-corehub
sudo systemctl status vanan-gateway
```

#### 5.2 Windows Services
```powershell
# Start services
Start-Service -Name "VanAnShopERP"
Start-Service -Name "VanAnCoreHub"
Start-Service -Name "VanAnGateway"

# Set services to start automatically
Set-Service -Name "VanAnShopERP" -StartupType Automatic
Set-Service -Name "VanAnCoreHub" -StartupType Automatic
Set-Service -Name "VanAnGateway" -StartupType Automatic

# Check service status
Get-Service -Name "VanAnShopERP"
Get-Service -Name "VanAnCoreHub"
Get-Service -Name "VanAnGateway"
```

## Verification and Testing

### Step 6: System Verification

#### 6.1 Health Checks
```bash
# Check NATS server
curl http://localhost:8222/varz | jq '.server_id'

# Check ShopERP application
curl http://localhost:5000/health

# Check Gateway application
curl http://localhost:5001/health

# Check database connectivity
sqlite3 /opt/vanan/data/vanan_shoperp.db ".tables"
```

#### 6.2 Test Order Processing
```bash
# Create test order
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "550e8400-e29b-41d4-a716-446655440000",
    "customerDeviceId": "device_test_001",
    "items": [
      {
        "productId": "550e8400-e29b-41d4-a716-446655440001",
        "productName": "Test Product",
        "quantity": 2,
        "unitPrice": 50.00
      }
    ]
  }'

# Check order in database
sqlite3 /opt/vanan/data/vanan_shoperp.db "
SELECT Id, TotalAmount, Status, CreatedAt 
FROM Orders 
ORDER BY CreatedAt DESC 
LIMIT 5;
"

# Check outbox messages
sqlite3 /opt/vanan/data/vanan_shoperp.db "
SELECT EventType, RetryCount, ProcessedOn 
FROM OutboxMessages 
ORDER BY CreatedAt DESC 
LIMIT 10;
"
```

#### 6.3 Test Event Publishing
```bash
# Subscribe to order events
nats-cli sub "orders.>" "test-worker"

# Wait for events to be published
# You should see events when orders are processed
```

## Monitoring and Maintenance

### Step 7: Monitoring Setup

#### 7.1 Log Monitoring
```bash
# Monitor application logs
tail -f /opt/vanan/logs/shoperp.log
tail -f /opt/vanan/logs/corehub.log
tail -f /opt/vanan/logs/nats.log

# Monitor system logs
sudo journalctl -u vanan-shoperp -f
sudo journalctl -u vanan-corehub -f
sudo journalctl -u vanan-gateway -f
sudo journalctl -u nats -f
```

#### 7.2 Performance Monitoring
```bash
# Monitor NATS performance
curl http://localhost:8222/monitorz | jq '.connections'

# Monitor database performance
sqlite3 /opt/vanan/data/vanan_shoperp.db "
PRAGMA database_list;
PRAGMA journal_mode;
PRAGMA synchronous;
"

# Monitor system resources
top -p $(pgrep -f "VanAn.ShopERP")
top -p $(pgrep -f "VanAn.CoreHub")
top -p $(pgrep -f "VanAn.Gateway")
```

#### 7.3 Health Check Script
```bash
# Create health check script
cat > /opt/vanan/scripts/health-check.sh << EOF
#!/bin/bash

echo "=== Van An System Health Check ==="
echo "Time: \$(date)"
echo ""

# Check NATS
if curl -s http://localhost:8222/varz > /dev/null; then
    echo "✅ NATS Server: Running"
else
    echo "❌ NATS Server: Down"
fi

# Check ShopERP
if curl -s http://localhost:5000/health > /dev/null; then
    echo "✅ ShopERP: Running"
else
    echo "❌ ShopERP: Down"
fi

# Check CoreHub
if pgrep -f "VanAn.CoreHub" > /dev/null; then
    echo "✅ CoreHub: Running"
else
    echo "❌ CoreHub: Down"
fi

# Check Gateway
if curl -s http://localhost:5001/health > /dev/null; then
    echo "✅ Gateway: Running"
else
    echo "❌ Gateway: Down"
fi

# Check Database
if [ -f "/opt/vanan/data/vanan_shoperp.db" ]; then
    echo "✅ Database: Accessible"
else
    echo "❌ Database: Missing"
fi

# Check Outbox Queue
PENDING_COUNT=\$(sqlite3 /opt/vanan/data/vanan_shoperp.db "SELECT COUNT(*) FROM OutboxMessages WHERE ProcessedOn IS NULL;")
echo "📊 Pending Outbox Messages: \$PENDING_COUNT"

if [ \$PENDING_COUNT -gt 1000 ]; then
    echo "⚠️  Warning: High outbox queue depth"
fi

echo ""
echo "=== End Health Check ==="
EOF

chmod +x /opt/vanan/scripts/health-check.sh

# Run health check
/opt/vanan/scripts/health-check.sh
```

### Step 8: Backup and Recovery

#### 8.1 Database Backup
```bash
# Create backup script
cat > /opt/vanan/scripts/backup-database.sh << EOF
#!/bin/bash

BACKUP_DIR="/opt/vanan/backups"
DATE=\$(date +%Y%m%d_%H%M%S)
DB_FILE="/opt/vanan/data/vanan_shoperp.db"
BACKUP_FILE="\$BACKUP_DIR/vanan_shoperp_\$DATE.db"

mkdir -p \$BACKUP_DIR

# Create backup
sqlite3 \$DB_FILE ".backup \$BACKUP_FILE"

if [ \$? -eq 0 ]; then
    echo "✅ Database backup successful: \$BACKUP_FILE"
    
    # Keep only last 7 days of backups
    find \$BACKUP_DIR -name "vanan_shoperp_*.db" -mtime +7 -delete
else
    echo "❌ Database backup failed"
    exit 1
fi
EOF

chmod +x /opt/vanan/scripts/backup-database.sh

# Schedule daily backup (crontab -e)
# 0 2 * * * /opt/vanan/scripts/backup-database.sh
```

#### 8.2 Configuration Backup
```bash
# Backup configuration files
tar -czf /opt/vanan/backups/config_\$(date +%Y%m%d_%H%M%S).tar.gz \
    /opt/vanan/config/ \
    /opt/vanan/nats/ \
    /etc/systemd/system/vanan-*.service
```

#### 8.3 Recovery Procedures
```bash
# Stop services
sudo systemctl stop vanan-shoperp vanan-corehub vanan-gateway nats

# Restore database
cp /opt/vanan/backups/vanan_shoperp_YYYYMMDD_HHMMSS.db /opt/vanan/data/vanan_shoperp.db

# Start services
sudo systemctl start nats vanan-shoperp vanan-corehub vanan-gateway

# Verify recovery
/opt/vanan/scripts/health-check.sh
```

## Troubleshooting

### Common Issues and Solutions

#### Issue 1: Database Lock Errors
**Symptoms**: "database is locked" errors in logs
**Solutions**:
```bash
# Check for long-running transactions
sqlite3 /opt/vanan/data/vanan_shoperp.db "PRAGMA busy_timeout;"

# Increase timeout in configuration
# Add to appsettings.json:
# "ConnectionStrings": {
#   "DefaultConnection": "Data Source=/opt/vanan/data/vanan_shoperp.db;Timeout=30;"
# }

# Restart services
sudo systemctl restart vanan-shoperp vanan-corehub
```

#### Issue 2: NATS Connection Failures
**Symptoms**: "Connection refused" errors
**Solutions**:
```bash
# Check NATS status
sudo systemctl status nats

# Restart NATS
sudo systemctl restart nats

# Check network connectivity
netstat -tlnp | grep 4222
```

#### Issue 3: Outbox Message Backlog
**Symptoms**: Growing pending message count
**Solutions**:
```bash
# Check processor status
sudo systemctl status vanan-corehub

# Manually process messages
curl -X POST http://localhost:5000/admin/outbox/process

# Clear old failed messages
sqlite3 /opt/vanan/data/vanan_shoperp.db "
DELETE FROM OutboxMessages 
WHERE RetryCount > 10 AND ProcessedOn IS NULL;
"
```

#### Issue 4: High Memory Usage
**Symptoms**: Memory growth over time
**Solutions**:
```bash
# Monitor memory usage
ps aux | grep -E "(VanAn|nats)"

# Restart services
sudo systemctl restart vanan-shoperp vanan-corehub vanan-gateway

# Check for memory leaks
dotnet-trace collect --process-id \$(pgrep -f "VanAn.ShopERP")
```

### Debug Commands

#### Database Debugging
```bash
# Check database status
sqlite3 /opt/vanan/data/vanan_shoperp.db "
PRAGMA database_list;
PRAGMA journal_mode;
PRAGMA synchronous;
PRAGMA cache_size;
"

# Analyze query performance
sqlite3 /opt/vanan/data/vanan_shoperp.db "
EXPLAIN QUERY PLAN 
SELECT * FROM OutboxMessages 
WHERE ProcessedOn IS NULL 
ORDER BY OccurredOn 
LIMIT 15;
"

# Check table sizes
sqlite3 /opt/vanan/data/vanan_shoperp.db "
SELECT 
    name,
    COUNT(*) as row_count,
    SUM(length(CAST(CAST(CAST(id AS TEXT) AS BLOB) AS TEXT))) as id_size
FROM sqlite_master 
WHERE type='table'
GROUP BY name;
"
```

#### NATS Debugging
```bash
# Check NATS streams
nats-cli stream ls

# Check NATS consumers
nats-cli consumer ls ORDERS

# Monitor NATS traffic
nats-cli traffic
```

#### Application Debugging
```bash
# Check application logs
sudo journalctl -u vanan-shoperp --since "1 hour ago" -f
sudo journalctl -u vanan-corehub --since "1 hour ago" -f

# Check configuration
cat /opt/vanan/config/ShopERP.appsettings.json

# Test connectivity
curl -v http://localhost:5000/health
curl -v http://localhost:5001/health
```

## Performance Optimization

### Database Optimization
```bash
# Run database maintenance
sqlite3 /opt/vanan/data/vanan_shoperp.db "VACUUM;"
sqlite3 /opt/vanan/data/vanan_shoperp.db "ANALYZE;"

# Optimize indexes
sqlite3 /opt/vanan/data/vanan_shoperp.db "
REINDEX;
PRAGMA optimize;
"
```

### Application Optimization
```bash
# Configure garbage collection
export DOTNET_GCHeapCount=2
export DOTNET_GCServer=1

# Optimize thread pool
export DOTNET_ThreadPool_StarvationDetection=1
export DOTNET_ThreadPool_MinThreads=10
export DOTNET_ThreadPool_MaxThreads=100
```

### System Optimization
```bash
# Optimize file descriptors
echo "* soft nofile 65536" >> /etc/security/limits.conf
echo "* hard nofile 65536" >> /etc/security/limits.conf

# Optimize network settings
echo "net.core.somaxconn = 65536" >> /etc/sysctl.conf
echo "net.ipv4.tcp_max_syn_backlog = 65536" >> /etc/sysctl.conf
sysctl -p
```

## Security Considerations

### Database Security
```bash
# Set proper file permissions
chmod 600 /opt/vanan/data/vanan_shoperp.db
chmod 700 /opt/vanan/data/

# Enable SQLite encryption (optional)
sqlite3 /opt/vanan/data/vanan_shoperp.db "
PRAGMA key = 'your-encryption-key';
"
```

### NATS Security
```bash
# Enable TLS
cat >> /opt/vanan/nats/nats.conf << EOF
# TLS Configuration
tls: {
  cert_file: "/opt/vanan/certs/server.crt"
  key_file: "/opt/vanan/certs/server.key"
  ca_file: "/opt/vanan/certs/ca.crt"
}
EOF
```

### Application Security
```bash
# Configure HTTPS
# Add to appsettings.json:
{
  "HttpsRedirection": {
    "HttpsPort": 443
  },
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://localhost:5000"
      }
    }
  }
}
```

## Scaling Considerations

### Horizontal Scaling
```bash
# Multiple application instances
# Deploy additional instances with different ports
# Use load balancer (nginx, haproxy) for distribution

# Example nginx configuration
cat > /etc/nginx/sites-available/vanan << EOF
upstream vanan_shoperp {
    server localhost:5000;
    server localhost:5001;
    server localhost:5002;
}

server {
    listen 80;
    server_name vanan.example.com;
    
    location / {
        proxy_pass http://vanan_shoperp;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
    }
}
EOF
```

### Database Scaling
```bash
# Read replicas for reporting
# Use SQLite WAL mode with multiple readers
# Consider PostgreSQL for high-throughput scenarios

# Connection pooling configuration
cat >> /opt/vanan/config/ShopERP.appsettings.json << EOF
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=/opt/vanan/data/vanan_shoperp.db;Pooling=true;Max Pool Size=100;"
  }
}
EOF
```

---

**Document Version**: 1.0  
**Last Updated**: April 28, 2026  
**Author**: Van An Development Team
