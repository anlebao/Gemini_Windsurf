# 🔧 Van An Ecosystem - Technical Handover

> **Tài liệu kỹ thuật cho DevOps Team - Deployment, Maintenance, Troubleshooting**
> 
> Version: 2.1 | Last Updated: 01/04/2026

---

## 📑 MỤC LỤC

1. [System Overview & Architecture](#chương-1-system-overview--architecture)
2. [Deployment Procedures](#chương-2-deployment-procedures)
3. [Monitoring & Logging](#chương-3-monitoring--logging)
4. [Database Management](#chương-4-database-management)
5. [Security & Compliance](#chương-5-security--compliance)
6. [Troubleshooting Guide](#chương-6-troubleshooting-guide)
7. [Maintenance Procedures](#chương-7-maintenance-procedures)
8. [Emergency Procedures](#chương-8-emergency-procedures)

---

## 🏗️ CHƯƠNG 1: SYSTEM OVERVIEW & ARCHITECTURE

### 1.1 Architecture Diagram (Current Status)

```
┌─────────────────────────────────────────────────────────────────┐
│                    VanAn Ecosystem v2.1                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐         │
│  │  KhachLink  │    │  ShopERP    │    │ Mobile Apps │         │
│  │  :5002      │    │  :5003      │    │  :6000+     │         │
│  │  Customer   │    │  Staff      │    │  Staff      │         │
│  │  Ordering   │    │  Management │    │  Mobile     │         │
│  │  (200 OK)   │    │  (200 OK)   │    │  (Dev)      │         │
│  └─────┬───────┘    └─────┬───────┘    └─────┬───────┘         │
│        │                  │                  │                 │
│        └──────────────────┼──────────────────┘                 │
│                           │                                    │
│  ┌────────────────────────▼────────────────────────┐         │
│  │                  Gateway                         │         │
│  │                  :5001                            │         │
│  │  ┌─────────────┐  ┌─────────────┐  ┌───────────┐ │         │
│  │  │ VietQR API  │  │ Voice API   │  │ Local API │ │         │
│  │  │ (Payment)   │  │ (Commands)  │  │ (Config)  │ │         │
│  │  │ (30+ Banks) │  │ (VN/EN)     │  │ (Onboard)│ │         │
│  │  └─────────────┘  └─────────────┘  └───────────┘ │         │
│  └──────────────────┬────────────────────────────────┘         │
│                     │                                    │
│  ┌──────────────────▼──────────────────┐    ┌─────────────┐  │
│  │              CoreHub                │    │    NATS     │  │
│  │              :5010                  │    │   :4222     │  │
│  │  ┌─────────────┐  ┌─────────────┐   │    │   :8222     │  │
│  │  │ PostgreSQL   │  │   Services  │   │    │  (JetStream) │  │
│  │  │   :5432     │  │ (Business)  │   │    │             │  │
│  │  │ (Multi-Ten)  │  │ (AI/ML)     │   │    │             │  │
│  │  └─────────────┘  └─────────────┘   │    └─────────────┘  │
│  └─────────────────────────────────────┘                     │
└─────────────────────────────────────────────────────────────────┘

🟢 SYSTEM STATUS: ALL SERVICES ONLINE (200 OK)
🚀 DEPLOYMENT: Docker + Local .NET Apps
📱 MOBILE: In Development (Phase 4)
```

### 1.2 Technology Stack

**Backend (.NET 8.0):**
- **CoreHub**: Business logic & API
- **Gateway**: API Gateway & Reverse Proxy
- **KhachLink**: Customer ordering portal
- **ShopERP**: Staff management system

**Database:**
- **PostgreSQL**: Primary database (Multi-tenant)
- **SQLite**: Local development/testing

**Infrastructure:**
- **Docker**: Container orchestration
- **NATS**: Message streaming (JetStream)
- **Seq**: Centralized logging
- **pgAdmin**: Database management

**Frontend:**
- **Razor Pages**: Server-side rendering
- **Bootstrap 5**: UI framework
- **JavaScript**: Client-side interactions

### 1.3 Service Dependencies

```
KhachLink → Gateway → CoreHub → PostgreSQL
ShopERP   → Gateway → CoreHub → PostgreSQL
Mobile    → Gateway → CoreHub → PostgreSQL
```

**External Services:**
- **VietQR API**: Payment processing
- **Email Service**: Notifications
- **SMS Gateway**: SMS notifications
- **Social Media APIs**: Marketing automation

---

## 🚀 CHƯƠNG 2: DEPLOYMENT PROCEDURES

### 2.1 Local Development Setup

**Prerequisites:**
- **.NET 8.0 SDK**: Latest version
- **Docker Desktop**: Version 4.0+
- **Git**: Version 2.30+
- **PowerShell**: Version 7.0+

**Quick Start:**
```bash
# Clone repository
git clone https://github.com/vanan/ecosystem.git
cd ecosystem

# Start infrastructure
.\Start-VanAnEcosystem.ps1 -SeparateWindows

# Verify services
curl http://localhost:5001/health
curl http://localhost:5002
curl http://localhost:5003
curl http://localhost:5010
```

### 2.2 Production Deployment

**Environment Setup:**
```bash
# Production environment variables
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__DefaultConnection="Host=prod-db;Port=5432;Database=VanAnProd;Username=vanan_prod;Password=SecurePassword123!"
export NATS__Url="nats://prod-nats:4222"
export LoggingConfig__EnableFileLogging=true
```

**Docker Compose Production:**
```yaml
version: '3.8'
services:
  postgres:
    image: postgres:15
    environment:
      POSTGRES_DB: VanAnProd
      POSTGRES_USER: vanan_prod
      POSTGRES_PASSWORD: SecurePassword123!
    volumes:
      - postgres_data:/var/lib/postgresql/data
    ports:
      - "5432:5432"

  nats:
    image: nats:2.9
    ports:
      - "4222:4222"
      - "8222:8222"
    command: ["--jetstream", "--store_dir", "/data"]
    volumes:
      - nats_data:/data

  corehub:
    image: vanan/corehub:latest
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    ports:
      - "5010:80"
    depends_on:
      - postgres
      - nats

  gateway:
    image: vanan/gateway:latest
    ports:
      - "5001:80"
    depends_on:
      - corehub

  khachlink:
    image: vanan/khachlink:latest
    ports:
      - "5002:80"
    depends_on:
      - gateway

  shoperp:
    image: vanan/shoperp:latest
    ports:
      - "5003:80"
    depends_on:
      - gateway
```

### 2.3 Database Migration

**Run Migrations:**
```bash
# CoreHub migrations
dotnet ef database update --project 3_CoreHub --startup-project 3_CoreHub

# Verify migration status
dotnet ef migrations list --project 3_CoreHub
```

**Rollback Procedures:**
```bash
# Rollback to specific migration
dotnet ef database update PreviousMigration --project 3_CoreHub

# Create new migration for rollback
dotnet ef migration add Rollback_Changes --project 3_CoreHub
```

### 2.4 Configuration Management

**appsettings.Production.json:**
```json
{
  "LoggingConfig": {
    "EnableFileLogging": true
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=prod-db;Port=5432;Database=VanAnProd;Username=vanan_prod;Password=SecurePassword123!"
  },
  "NATS": {
    "Url": "nats://prod-nats:4222"
  },
  "Jwt": {
    "Issuer": "VanAnProduction",
    "Audience": "VanAnUsers",
    "SecretKey": "ProductionSecretKey123456789012345678901234567890"
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "/app/logs/log-.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7
        }
      }
    ]
  }
}
```

---

## 📊 CHƯƠNG 3: MONITORING & LOGGING

### 3.1 Serilog Configuration

**Dynamic Logging Setup:**
```csharp
// Program.cs - All applications
builder.Host.UseSerilog((context, config) => 
{
    config.WriteTo.Console();
    
    // Conditional file logging
    if (context.Configuration.GetValue<bool>("LoggingConfig:EnableFileLogging"))
    {
        var appName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
        config.WriteTo.File(
            path: System.IO.Path.Combine(AppContext.BaseDirectory, "Logs", $"{appName}-.txt"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 2
        );
    }
});
```

**Logging Levels:**
- **Verbose**: Detailed debugging
- **Debug**: Development information
- **Information**: General information
- **Warning**: Warning messages
- **Error**: Error messages
- **Fatal**: Critical errors

### 3.2 Seq Centralized Logging

**Seq Configuration:**
```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://seq:8081",
          "apiKey": "YourSeqApiKey"
        }
      }
    ]
  }
}
```

**Log Queries:**
```csharp
// Search for specific errors
ApplicationName == "VanAn.CoreHub" && Level == "Error"

// Performance monitoring
@Duration > 1000 && ApplicationName == "VanAn.Gateway"

// Customer activity
ApplicationName == "VanAn.KhachLink" && RequestPath like "/api/orders%"
```

### 3.3 Health Checks

**Health Check Endpoints:**
- **Gateway**: `http://localhost:5001/health`
- **CoreHub**: `http://localhost:5010/health`
- **KhachLink**: `http://localhost:5002/health`
- **ShopERP**: `http://localhost:5003/health`

**Health Check Response:**
```json
{
  "status": "Healthy",
  "service": "VanAn Gateway",
  "timestamp": "2026-04-01T09:00:00Z",
  "checks": {
    "database": "Healthy",
    "nats": "Healthy",
    "memory": "Healthy"
  }
}
```

### 3.4 Performance Monitoring

**Key Metrics:**
- **Response Time**: < 200ms (95th percentile)
- **Throughput**: > 1000 requests/second
- **Memory Usage**: < 512MB per service
- **CPU Usage**: < 70% average
- **Error Rate**: < 0.1%

**Monitoring Tools:**
- **Application Insights**: Azure monitoring
- **Prometheus**: Metrics collection
- **Grafana**: Visualization dashboard
- **Alertmanager**: Alert management

---

## 🗄️ CHƯƠNG 4: DATABASE MANAGEMENT

### 4.1 PostgreSQL Administration

**Connection Information:**
- **Host**: localhost:5432 (development)
- **Database**: VanAnCoreHub
- **Username**: vanan_admin
- **Password**: VanAn@2024!

**Common Operations:**
```sql
-- Check database size
SELECT pg_size_pretty(pg_database_size('VanAnCoreHub'));

-- Check table sizes
SELECT 
    schemaname,
    tablename,
    pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) as size
FROM pg_tables 
WHERE schemaname = 'public'
ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC;

-- Check active connections
SELECT count(*) FROM pg_stat_activity WHERE datname = 'VanAnCoreHub';

-- Kill long-running queries
SELECT pg_terminate_backend(pid) FROM pg_stat_activity 
WHERE datname = 'VanAnCoreHub' AND query_start < now() - interval '5 minutes';
```

### 4.2 Multi-tenancy Management

**Tenant Isolation:**
```sql
-- List all tenants
SELECT DISTINCT "TenantId" FROM "Products";

-- Check tenant data
SELECT COUNT(*) FROM "Orders" WHERE "TenantId" = 'tenant-uuid';

-- Tenant-specific queries
SET "app.tenant_id" = 'tenant-uuid';
SELECT * FROM "Products";
```

**Tenant Management:**
```csharp
// Create new tenant
var tenant = new Tenant
{
    Id = Guid.NewGuid(),
    Name = "New Shop",
    DatabaseSchema = $"tenant_{Guid.NewGuid():N}"
};

// Apply tenant-specific migrations
await context.Database.MigrateAsync(tenant.Id);
```

### 4.3 Backup & Recovery

**Automated Backup:**
```bash
# Daily backup script
#!/bin/bash
BACKUP_DIR="/backups/postgres"
DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="$BACKUP_DIR/vanan_backup_$DATE.sql"

# Create backup
pg_dump -h localhost -U vanan_admin -d VanAnCoreHub > $BACKUP_FILE

# Compress backup
gzip $BACKUP_FILE

# Keep only 7 days of backups
find $BACKUP_DIR -name "*.sql.gz" -mtime +7 -delete
```

**Recovery Procedures:**
```bash
# Restore from backup
gunzip -c /backups/postgres/vanan_backup_20260401_020000.sql.gz | psql -h localhost -U vanan_admin -d VanAnCoreHub

# Point-in-time recovery
pg_basebackup -h localhost -D /backup/base -U vanan_admin -v -P -W
```

---

## 🔒 CHƯƠNG 5: SECURITY & COMPLIANCE

### 5.1 Authentication & Authorization

**JWT Configuration:**
```csharp
// JWT Token Generation
var token = new JwtSecurityToken(
    issuer: jwtOptions.Issuer,
    audience: jwtOptions.Audience,
    claims: claims,
    expires: DateTime.Now.AddMinutes(jwtOptions.ExpiryMinutes),
    signingCredentials: signingCredentials
);
```

**Role-based Access:**
```csharp
// Policy configuration
services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => 
        policy.RequireRole("Admin"));
    options.AddPolicy("StaffOnly", policy => 
        policy.RequireRole("Staff", "Admin"));
});
```

### 5.2 Data Protection

**Encryption Setup:**
```csharp
// Data protection configuration
services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/app/keys"))
    .SetApplicationName("VanAn")
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90));
```

**Sensitive Data Handling:**
```csharp
// Encrypt sensitive data
public class EncryptionService
{
    public string Encrypt(string plainText)
    {
        // AES-256 encryption
        // Key rotation every 90 days
    }
}
```

### 5.3 Security Monitoring

**Security Events:**
- **Failed login attempts**
- **Unauthorized access attempts**
- **Data access patterns**
- **API usage anomalies**

**Security Alerts:**
```csharp
// Security event logging
Log.Warning("Security alert: {EventType} from {IPAddress}", 
    "FailedLogin", 
    HttpContext.Connection.RemoteIpAddress);
```

---

## 🚨 CHƯƠNG 6: TROUBLESHOOTING GUIDE

### 6.1 Common Issues

**Service Not Starting:**
```bash
# Check port conflicts
netstat -an | findstr ":500"

# Check Docker containers
docker-compose ps

# Restart services
docker-compose restart
```

**Database Connection Issues:**
```bash
# Test database connection
psql -h localhost -U vanan_admin -d VanAnCoreHub

# Check PostgreSQL logs
docker-compose logs postgres

# Restart PostgreSQL
docker-compose restart postgres
```

**Performance Issues:**
```bash
# Check system resources
docker stats

# Check application logs
docker-compose logs corehub

# Monitor database queries
SELECT query, mean_time, calls FROM pg_stat_statements ORDER BY mean_time DESC LIMIT 10;
```

### 6.2 Debugging Procedures

**Enable Debug Logging:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "VanAn": "Trace"
    }
  }
}
```

**Memory Leak Detection:**
```bash
# Monitor memory usage
dotnet-counters monitor --process-id <pid> System.Runtime

# Generate memory dump
dotnet-dump collect --process-id <pid> --output-path ./dumps/
```

### 6.3 Performance Tuning

**Database Optimization:**
```sql
-- Create indexes for performance
CREATE INDEX CONCURRENTLY idx_orders_tenant_created 
ON "Orders"("TenantId", "CreatedAt");

-- Analyze query performance
EXPLAIN ANALYZE SELECT * FROM "Orders" 
WHERE "TenantId" = 'uuid' AND "CreatedAt" > '2026-04-01';
```

**Application Optimization:**
```csharp
// Connection pooling
services.AddDbContext<VanAnDbContext>(options =>
    options.UseNpgsql(connectionString, 
        npgsql => npgsql.EnableRetryOnFailure()));

// Caching
services.AddMemoryCache();
services.AddDistributedMemoryCache();
```

---

## 🛠️ CHƯƠNG 7: MAINTENANCE PROCEDURES

### 7.1 Daily Maintenance

**Automated Tasks:**
```bash
#!/bin/bash
# Daily maintenance script

# Clean up old logs
find /app/logs -name "*.txt" -mtime +7 -delete

# Database maintenance
psql -h localhost -U vanan_admin -d VanAnCoreHub -c "VACUUM ANALYZE;"

# Health check
curl -f http://localhost:5001/health || echo "Gateway down" | mail -s "Gateway Alert" admin@vanan.com
```

**Monitoring Checks:**
- Service health status
- Database performance
- Disk space usage
- Memory consumption
- Error rates

### 7.2 Weekly Maintenance

**System Updates:**
```bash
# Update Docker images
docker-compose pull

# Restart services with new images
docker-compose up -d

# Verify updates
docker-compose ps
```

**Database Maintenance:**
```bash
# Full database backup
pg_dump -h localhost -U vanan_admin -d VanAnCoreHub > backup_$(date +%Y%m%d).sql

# Update statistics
psql -h localhost -U vanan_admin -d VanAnCoreHub -c "ANALYZE;"
```

### 7.3 Monthly Maintenance

**Security Updates:**
```bash
# Update .NET packages
dotnet list --outdated
dotnet add package <package-name>

# Security scan
dotnet dev-certs https --check
```

**Performance Review:**
- Analyze performance metrics
- Review error logs
- Optimize slow queries
- Update documentation

---

## 🆘 CHƯƠNG 8: EMERGENCY PROCEDURES

### 8.1 System Down

**Immediate Response:**
1. **Check all services**: `docker-compose ps`
2. **Review logs**: `docker-compose logs`
3. **Restart services**: `docker-compose restart`
4. **Verify recovery**: Test all endpoints

**Escalation:**
- **Level 1**: DevOps team (5 minutes)
- **Level 2**: System architect (15 minutes)
- **Level 3**: CTO (30 minutes)

### 8.2 Data Corruption

**Recovery Steps:**
1. **Stop all services**: `docker-compose down`
2. **Restore backup**: `psql < backup.sql`
3. **Verify data integrity**: Check critical tables
4. **Restart services**: `docker-compose up -d`
5. **Test functionality**: Verify all features

### 8.3 Security Breach

**Incident Response:**
1. **Isolate affected systems**
2. **Preserve evidence**: Logs, memory dumps
3. **Notify security team**
4. **Patch vulnerabilities**
5. **Monitor for suspicious activity**

### 8.4 Contact Information

**Emergency Contacts:**
- **DevOps Lead**: devops@vanan.com | +84-XXX-XXX-XXXX
- **System Architect**: architect@vanan.com | +84-XXX-XXX-XXXX
- **CTO**: cto@vanan.com | +84-XXX-XXX-XXXX

**Service Providers:**
- **Hosting Provider**: hosting@provider.com
- **DNS Provider**: dns@provider.com
- **Security Team**: security@vanan.com

---

## 📚 APPENDIX

### A.1 Quick Reference Commands

**Docker Commands:**
```bash
# Start all services
docker-compose up -d

# Stop all services
docker-compose down

# View logs
docker-compose logs -f [service-name]

# Restart specific service
docker-compose restart [service-name]

# Execute command in container
docker-compose exec [service-name] [command]
```

**Database Commands:**
```bash
# Connect to database
psql -h localhost -U vanan_admin -d VanAnCoreHub

# Backup database
pg_dump -h localhost -U vanan_admin -d VanAnCoreHub > backup.sql

# Restore database
psql -h localhost -U vanan_admin -d VanAnCoreHub < backup.sql
```

**.NET Commands:**
```bash
# Build solution
dotnet build

# Run tests
dotnet test

# Publish application
dotnet publish -c Release -o ./publish

# Create migration
dotnet ef migrations add MigrationName

# Update database
dotnet ef database update
```

### A.2 Configuration Files

**Environment Variables:**
```bash
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=VanAnCoreHub;Username=vanan_admin;Password=VanAn@2024!"
export NATS__Url="nats://localhost:4222"
export LoggingConfig__EnableFileLogging=true
```

**Docker Compose Override:**
```yaml
version: '3.8'
services:
  corehub:
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - LoggingConfig__EnableFileLogging=true
    volumes:
      - ./logs:/app/logs
```

---

**🎯 Technical Handover Complete**

**📞 24/7 Support: devops@vanan.com | Emergency: +84-XXX-XXX-XXXX**

**🔗 System Status: https://status.vanan.com**

**📚 Documentation: https://docs.vanan.com**
│  └─────────────────────────────────────┘                     │
└─────────────────────────────────────────────────────────────────┘
```

### 1.2 Service Dependencies

| Service | Port | Dependencies | Purpose |
|---------|------|--------------|---------|
| **PostgreSQL** | 5432 | - | Primary database (CoreHub) |
| **NATS** | 4222/8222 | - | Message broker, event streaming |
| **CoreHub** | 5010 | PostgreSQL, NATS | Business logic, domain services |
| **Gateway** | 5001 | CoreHub, NATS | API gateway, external integrations |
| **KhachLink** | 5002 | Gateway | Customer ordering interface |
| **ShopERP** | 5003 | Gateway, CoreHub | Staff management interface |
| **QualityGate** | 8080 | All services | Testing, quality assurance |

### 1.3 Network Configuration

**Internal Network**: `vanan-network`
```yaml
networks:
  default:
    name: vanan-network
    driver: bridge
```

**Service Discovery**: Internal DNS resolution
- `postgres:5432` → PostgreSQL database
- `nats:4222` → NATS message broker
- `vanan-corehub:5010` → CoreHub API
- `vanan-gateway:5001` → Gateway API
- `vanan-khachlink:5002` → KhachLink UI
- `vanan-shoperp:5003` → ShopERP UI

### 1.4 Environment Variables

**Core Environment**:
```bash
# Database
POSTGRES_DB=VanAn
POSTGRES_USER=vanan
POSTGRES_PASSWORD=VanAn123!

# NATS
NATS__Url=nats://nats:4222

# Services
COREHUB_URL=http://vanan-corehub:5010
GATEWAY_URL=http://vanan-gateway:5001
KHACHLINK_URL=http://vanan-khachlink:5002
SHOPERP_URL=http://vanan-shoperp:5003
```

**Production Variables**:
```bash
# Security
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:80

# Logging
LOGGING__MINIMUMLEVEL=Information
LOGGING__CONSOLE__ENABLED=true

# Performance
DOTNET_GC_SERVER=true
DOTNET_GC_CONCURRENT=true
```

---

## 🧪 QUY TRÌNH QUALITY GATE

### 2.1 Quality Gate Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Quality Gate Pipeline                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐         │
│  │  Smoke Tests│    │  E2E Tests  │    │ Load Tests  │         │
│  │  (Health)   │    │ (Playwright)│    │ (k6)        │         │
│  └─────┬───────┘    └─────┬───────┘    └─────┬───────┘         │
│        │                  │                  │                 │
│        └──────────────────┼──────────────────┘                 │
│                           │                                    │
│  ┌────────────────────────▼────────────────────────┐         │
│  │              Quality Gate Service               │         │
│  │              (Node.js + Reports)               │         │
│  └──────────────────┬─────────────────────────────┘         │
│                     │                                    │
│  ┌──────────────────▼─────────────────────────────┐         │
│  │              Dashboard & Reports               │         │
│  │              (HTML + JSON)                    │         │
│  └─────────────────────────────────────────────────┘         │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 Running Quality Gate

**Quick Test (Development)**:
```bash
# Simple quality gate
docker-compose -f docker-compose.simple.yml run --rm quality-gate

# With custom configuration
ENABLE_E2E=true SMOKE_TEST_ENABLED=true \
docker-compose -f docker-compose.simple.yml run --rm quality-gate
```

**Full Test Suite (Pre-production)**:
```bash
# Complete testing environment
docker-compose -f docker-compose.testing.yml up -d

# Wait for services to be healthy
sleep 30

# Run quality gate
docker-compose -f docker-compose.testing.yml run --rm quality-gate

# View results
docker-compose -f docker-compose.testing.yml exec quality-gate \
  cat /app/reports/quality-gate-latest.json
```

### 2.3 Test Categories

**Smoke Tests** (Health Checks):
```bash
# Service health endpoints
curl -f http://vanan-corehub:5010/health
curl -f http://vanan-gateway:5001/health
curl -f http://vanan-khachlink:5002/health
curl -f http://vanan-shoperp:5003/health
```

**E2E Tests** (User Workflows):
```bash
# QR Payment flow
npx playwright test e2e-tests/qr-payment.spec.ts

# Voice Command flow
npx playwright test e2e-tests/voice-command.spec.ts

# Internationalization
npx playwright test e2e-tests/i18n.spec.ts
```

**Load Tests** (Performance):
```bash
# API load testing
k6 run load-tests/api-performance.js

# Concurrent users
k6 run --vus 50 --duration 2m load-tests/user-load.js
```

### 2.4 Quality Gate Configuration

**Environment Variables**:
```bash
# Test Configuration
SMOKE_TEST_ENABLED=true
ENABLE_E2E=true
ENABLE_LOAD_TEST=false
ENABLE_CHAOS=false

# Timeouts (seconds)
SMOKE_TEST_TIMEOUT=30
E2E_TEST_TIMEOUT=120
LOAD_TEST_DURATION=60
CHAOS_TEST_DURATION=300

# Intensity
LOAD_TEST_VUS=10
CHAOS_INTENSITY=0.3

# Environment
TEST_ENVIRONMENT=production
QUALITY_GATE_STRICT=true
```

### 2.5 Quality Gate Results

**Success Criteria**:
- ✅ All smoke tests pass (100% service health)
- ✅ E2E tests pass (> 95% success rate)
- ✅ Load tests meet SLA (< 2s response time)
- ✅ No critical security vulnerabilities

**Report Structure**:
```json
{
  "summary": {
    "totalDuration": 120.5,
    "passed": 15,
    "failed": 0,
    "skipped": 2,
    "overallStatus": "PASS"
  },
  "tiers": [
    {
      "name": "smoke",
      "status": "PASS",
      "duration": 5.2,
      "tests": ["CoreHub Health", "Gateway Health", "..."]
    }
  ],
  "metrics": {
    "responseTime": { "avg": 245, "p95": 512, "p99": 892 },
    "throughput": { "requests": 1250, "errors": 0 },
    "resources": { "cpu": 45.2, "memory": 68.7 }
  }
}
```

---

## 🛠️ BẢO TRÌ HỆ THỐNG

### 3.1 AudioCleanupService Configuration

**Service Overview**:
- **Purpose**: Tự động xóa file âm thanh gốc sau 24h
- **Schedule**: Chạy mỗi giờ
- **Retention**: 24 hours (configurable)
- **Scope**: Tất cả audio files trong system

**Configuration**:
```csharp
// In CoreHub/Program.cs
services.AddHostedService<AudioCleanupService>();

// Configuration options
services.Configure<AudioCleanupOptions>(options =>
{
    options.CleanupInterval = TimeSpan.FromHours(1);
    options.RetentionPeriod = TimeSpan.FromHours(24);
    options.BatchSize = 100;
    options.MaxRetries = 3;
});
```

**Monitoring**:
```bash
# Check cleanup logs
docker logs vanan-corehub | grep "AudioCleanup"

# Monitor storage usage
docker exec vanan-corehub du -sh /app/wwwroot/uploads/audio/

# Manual cleanup (emergency)
docker exec vanan-corehub curl -X POST http://localhost:5010/api/v1/voicecommand/cleanup-expired
```

**Performance Tuning**:
```csharp
// Optimize for high-volume scenarios
options.BatchSize = 500;          // Process more files per batch
options.CleanupInterval = TimeSpan.FromMinutes(30);  // More frequent
options.MaxConcurrentOperations = 5;  // Parallel processing
```

### 3.2 Database Maintenance

**Automated Backup**:
```bash
#!/bin/bash
# backup-database.sh
DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="/backups/vanan_${DATE}.sql"

# Create backup
docker exec vanan-postgres pg_dump -U vanan VanAn > $BACKUP_FILE

# Compress
gzip $BACKUP_FILE

# Cleanup old backups (keep 7 days)
find /backups -name "vanan_*.sql.gz" -mtime +7 -delete

echo "Backup completed: ${BACKUP_FILE}.gz"
```

**Database Optimization**:
```sql
-- Weekly optimization script
-- Run every Sunday at 2:00 AM

-- Update statistics
ANALYZE;

-- Reindex fragmented indexes
REINDEX DATABASE VanAn;

-- Vacuum and reclaim space
VACUUM FULL;

-- Check table sizes
SELECT 
    schemaname,
    tablename,
    pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) as size,
    pg_size_pretty(pg_relation_size(schemaname||'.'||tablename)) as table_size,
    pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename) - pg_relation_size(schemaname||'.'||tablename)) as index_size
FROM pg_tables 
WHERE schemaname = 'public'
ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC;
```

**Connection Pool Monitoring**:
```bash
# Check active connections
docker exec vanan-postgres psql -U vanan -d VanAn -c "
SELECT 
    state,
    count(*) as connections,
    avg(EXTRACT(EPOCH FROM (now() - query_start))) as avg_duration
FROM pg_stat_activity 
WHERE datname = 'VanAn'
GROUP BY state;
"

# Monitor long-running queries
docker exec vanan-postgres psql -U vanan -d VanAn -c "
SELECT 
    pid,
    now() - pg_stat_activity.query_start AS duration,
    query,
    state
FROM pg_stat_activity 
WHERE (now() - pg_stat_activity.query_start) > interval '5 minutes'
ORDER BY duration DESC;
"
```

### 3.3 Service Health Monitoring

**Health Check Endpoints**:
```bash
# CoreHub health
curl http://localhost:5010/health

# Gateway health
curl http://localhost:5001/health

# Service dependencies
curl http://localhost:5010/api/test/db
curl http://localhost:5001/api/test/nats
```

**Custom Health Checks**:
```csharp
// In Gateway/Program.cs
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<NatsHealthCheck>("nats")
    .AddCheck<VietQrHealthCheck>("vietqr");

// Health check response
{
  "status": "Healthy",
  "totalDuration": "00:00:00.123",
  "entries": {
    "database": {
      "status": "Healthy",
      "duration": "00:00:00.045"
    },
    "nats": {
      "status": "Healthy",
      "duration": "00:00:00.012"
    }
  }
}
```

---

## 📊 MONITORING & ALERTING

### 4.1 Key Metrics Dashboard

**System Metrics**:
- CPU Usage (< 80% threshold)
- Memory Usage (< 85% threshold)
- Disk Space (< 90% threshold)
- Network I/O
- Container Health

**Application Metrics**:
- API Response Time (< 500ms p95)
- Request Rate (RPM)
- Error Rate (< 1%)
- Voice Recognition Success Rate (> 95%)
- QR Payment Success Rate (> 98%)

**Business Metrics**:
- Orders per minute
- Voice commands processed
- Audio files cleaned up
- Localization service usage

### 4.2 Alert Configuration

**Critical Alerts**:
```yaml
# Prometheus Alert Rules
groups:
  - name: vanan-critical
    rules:
      - alert: ServiceDown
        expr: up == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Service {{ $labels.instance }} is down"
          
      - alert: HighErrorRate
        expr: rate(http_requests_total{status=~"5.."}[5m]) > 0.01
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: "High error rate detected"
          
      - alert: DiskSpaceLow
        expr: (node_filesystem_size_bytes - node_filesystem_free_bytes) / node_filesystem_size_bytes > 0.9
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Disk space below 10%"
```

**Warning Alerts**:
```yaml
  - name: vanan-warning
    rules:
      - alert: HighResponseTime
        expr: histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m])) > 1
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "95th percentile response time > 1s"
          
      - alert: VoiceRecognitionLowAccuracy
        expr: rate(voice_commands_success_total[5m]) / rate(voice_commands_total[5m]) < 0.9
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "Voice recognition accuracy below 90%"
```

### 4.3 Log Aggregation

**Log Structure**:
```json
{
  "timestamp": "2026-03-26T14:30:00.000Z",
  "level": "Information",
  "category": "VoiceCommand",
  "message": "Voice command processed successfully",
  "data": {
    "orderId": "ORD-123456",
    "commandType": "update_status",
    "processingTime": 1250,
    "language": "vi-VN"
  },
  "service": "Vanan.Gateway",
  "version": "1.0.0",
  "correlationId": "abc-123-def-456"
}
```

**Log Analysis**:
```bash
# Search for errors
grep "ERROR" /var/log/vanan/*.log

# Voice command analytics
grep "VoiceCommand" /var/log/vanan/gateway.log | \
  jq -r '.data.commandType' | sort | uniq -c

# Performance analysis
grep "processingTime" /var/log/vanan/gateway.log | \
  jq -r '.data.processingTime' | awk '{sum+=$1; count++} END {print "Avg:", sum/count}'
```

---

## 🚨 DISASTER RECOVERY

### 5.1 Backup Strategy

**Data Classification**:
- **Critical**: Database (PostgreSQL), configuration files
- **Important**: Audio files (24h retention), logs (30 days)
- **Normal**: Temporary files, cache

**Backup Schedule**:
```bash
# Critical data - Every 6 hours
0 */6 * * * /scripts/backup-database.sh

# Important data - Daily
0 2 * * * /scripts/backup-audio.sh

# Configuration - Weekly
0 3 * * 0 /scripts/backup-config.sh
```

**Retention Policy**:
- Database backups: 30 days
- Audio backups: 7 days
- Configuration backups: 90 days
- Logs: 30 days (compressed)

### 5.2 Recovery Procedures

**Database Recovery**:
```bash
# Stop services
docker-compose -f docker-compose.testing.yml down

# Restore database
docker-compose -f docker-compose.testing.yml up -d postgres
sleep 10

# Restore from backup
docker exec -i vanan-postgres psql -U vanan VanAn < /backups/vanan_20260326_020000.sql

# Start services
docker-compose -f docker-compose.testing.yml up -d
```

**Service Recovery**:
```bash
# Health check after recovery
for service in corehub gateway khachlink shoperp; do
    echo "Checking $service..."
    curl -f http://localhost:${PORTS[$service]}/health || \
        echo "$service failed health check"
done

# Run quality gate to verify
docker-compose -f docker-compose.simple.yml run --rm quality-gate
```

### 5.3 Failover Procedures

**High Availability Setup**:
```yaml
# docker-compose.ha.yml
version: '3.8'
services:
  postgres-ha:
    image: postgres:15
    environment:
      POSTGRES_REPLICATION_USER: replicator
      POSTGRES_REPLICATION_PASSWORD: repl_pass
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./pg_hba.conf:/etc/postgresql/pg_hba.conf
```

**Manual Failover**:
```bash
# Promote replica to primary
docker exec postgres-replica pg_ctl promote

# Update connection strings
# Update DNS records
# Restart application services
```

---

## 🔒 SECURITY & COMPLIANCE

### 6.1 Security Configuration

**Network Security**:
```yaml
# Docker network isolation
networks:
  vanan-internal:
    driver: bridge
    internal: true
  vanan-external:
    driver: bridge
```

**API Security**:
```csharp
// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("ApiPolicy", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString(),
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 10
            }));
});

// Input validation
services.AddValidators<Assembly>(typeof(VietQrRequest).Assembly);
```

**Data Encryption**:
```csharp
// Audio file encryption
services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/keys"))
    .UseCryptographicAlgorithms(
        new AuthenticatedEncryptorConfiguration()
        {
            EncryptionAlgorithm = EncryptionAlgorithm.AES_256_GCM,
            ValidationAlgorithm = ValidationAlgorithm.HMACSHA256
        });
```

### 6.2 Compliance Monitoring

**Privacy Compliance**:
- Audio files auto-delete after 24h ✅
- PII anonymization in logs ✅
- GDPR right to deletion ✅
- Data retention policies enforced ✅

**Audit Trail**:
```sql
-- Voice command audit log
CREATE TABLE voice_command_audit (
    id SERIAL PRIMARY KEY,
    command_id UUID NOT NULL,
    user_id VARCHAR(255),
    action_type VARCHAR(50),
    action_timestamp TIMESTAMP DEFAULT NOW(),
    ip_address INET,
    user_agent TEXT,
    success BOOLEAN,
    error_message TEXT
);

-- Index for performance
CREATE INDEX idx_voice_command_audit_timestamp 
ON voice_command_audit(action_timestamp);
```

### 6.3 Security Scanning

**Vulnerability Scanning**:
```bash
# Docker image scanning
docker scan vanan-gateway:latest
docker scan vanan-corehub:latest

# Dependency scanning
dotnet list package --vulnerable
npm audit

# Infrastructure scanning
nmap -sV -p 5001,5010,5432 localhost
```

**Penetration Testing**:
```bash
# API security testing
nuclei -target http://localhost:5001 -t nuclei-templates/

# Authentication testing
curl -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"password"}'

# Rate limiting test
for i in {1..150}; do
    curl -s http://localhost:5001/api/v1/health > /dev/null
done
```

---

## 📋 HANDOVER CHECKLIST

### Pre-Handover Tasks
- [ ] All services running and healthy
- [ ] Quality gate passing 100%
- [ ] Documentation complete and reviewed
- [ ] Backup procedures tested
- [ ] Monitoring dashboards configured
- [ ] Alert rules tested
- [ ] Security scans completed
- [ ] Performance benchmarks documented

### Post-Handover Tasks
- [ ] Access credentials transferred
- [ ] Monitoring access granted
- [ ] Emergency contacts updated
- [ ] Training sessions completed
- [ ] Support procedures documented
- [ ] Change management process defined

---

## 🆘 EMERGENCY PROCEDURES

### Service Outage
1. **Check health endpoints**: `curl http://localhost:5001/health`
2. **Review logs**: `docker logs vanan-gateway --tail 100`
3. **Restart services**: `docker-compose restart`
4. **Run diagnostic**: `docker-compose run --rm quality-gate`
5. **Escalate**: Contact DevOps team

### Data Loss
1. **Stop all services**: `docker-compose down`
2. **Assess impact**: Check last backup time
3. **Restore database**: `psql -U vanan VanAn < backup.sql`
4. **Verify data integrity**: Run data validation scripts
5. **Start services**: `docker-compose up -d`

### Security Incident
1. **Isolate affected systems**
2. **Preserve evidence**: Don't delete logs
3. **Assess scope**: Check all services
4. **Patch vulnerabilities**: Update containers
5. **Report**: Document incident timeline

---

## 📞 CONTACT INFORMATION

### DevOps Team
- **Primary**: devops@vanan.com
- **Secondary**: backup@vanan.com
- **24/7 Hotline**: +84-123-456-789

### Service Owners
- **CoreHub**: corehub-team@vanan.com
- **Gateway**: gateway-team@vanan.com
- **Frontend**: frontend-team@vanan.com

### External Services
- **VietQR Support**: support@vietqr.io
- **Cloud Provider**: support@cloud-provider.com
- **CDN Provider**: cdn-support@provider.com

---

## 📚 REFERENCE DOCUMENTS

- [Operator Manual](./OPERATOR_MANUAL.md) - User-facing documentation
- [API Documentation](http://localhost:5001/swagger) - Interactive API docs
- [Architecture Decisions](./docs/adr/) - Design decisions
- [Change Log](./CHANGELOG.md) - Version history
- [Troubleshooting Guide](./docs/troubleshooting.md) - Common issues

---

> **🎯 Mission**: Ensure 99.9% uptime and sub-second response times for VanAn Ecosystem
> 
> **🏆 Goal**: Zero-downtime deployments and automated failover capabilities
> 
> **💚 Values**: Reliability, Security, Performance, Maintainability

---

**© 2026 VanAn Ecosystem - Confidential Technical Documentation**
