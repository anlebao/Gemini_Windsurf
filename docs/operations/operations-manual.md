# HUONG DAN VAN HANH - VANAN ECOSYSTEM

## **THONG TIN CHUNG**

**Tên Hê Thông:** VanAn Ecosystem  
**Phiên Ban Hiên Tai:** MVP 1.0  
**Ngày Câp Nhât:** 10/04/2026  
**Dành Cho:** Admin, DevOps, System Administrator

---

## **1. OVERVIEW HÊ THÔNG**

### **1.1 Kiên Trúc Hê Thông**

```
Internet Gateway
    |
    v
2_Gateway (API Gateway) - Port 5000
    |
    v
3_CoreHub (Business Logic) - Port 5001
    |
    v
PostgreSQL Database - Port 5432
    |
    v
Redis Cache - Port 6379
```

### **1.2 Components**

**Application Layer:**
- **Gateway:** API Gateway, Authentication, Routing
- **CoreHub:** Business Logic, Domain Services
- **Web Apps:** ShopERP, KhachLink
- **Mobile Apps:** iOS/Android applications

**Infrastructure Layer:**
- **Database:** PostgreSQL 15+
- **Cache:** Redis 7+
- **Container:** Docker & Docker Compose
- **Monitoring:** Logs, Metrics, Health Checks

---

## **2. SETUP & DEPLOYMENT**

### **2.1 Yêu Câu Hê Thông**

**Minimum Requirements:**
- **CPU:** 4 cores
- **RAM:** 8GB
- **Storage:** 100GB SSD
- **OS:** Ubuntu 20.04+ / Windows 10+

**Recommended:**
- **CPU:** 8 cores
- **RAM:** 16GB
- **Storage:** 500GB SSD
- **Network:** 1Gbps

### **2.2 Cài Dat Environment**

**Prerequisites:**
```bash
# Docker & Docker Compose
curl -fsSL https://get.docker.com -o get-docker.sh
sh get-docker.sh

# .NET 8.0 Runtime
wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
apt-get update
apt-get install -y aspnetcore-runtime-8.0

# Git (nêu chua có)
apt-get install git
```

**Clone Repository:**
```bash
git clone https://github.com/vanan/ecosystem.git
cd ecosystem
```

### **2.3 Configuration**

**Environment Variables:**
```bash
# .env file
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__VanAnDb=Host=localhost;Database=vanan;Username=vanan;Password=your_password
Redis__ConnectionString=localhost:6379
JwtSettings__Secret=your_jwt_secret_key
JwtSettings__Issuer=VanAn
JwtSettings__Audience=VanAnUsers
JwtSettings__ExpiryMinutes=60
Facebook__AppId=your_facebook_app_id
Facebook__AppSecret=your_facebook_app_secret
```

**Database Setup:**
```bash
# Tao database PostgreSQL
sudo -u postgres createdb vanan
sudo -u postgres createuser vanan
sudo -u postgres psql -c "ALTER USER vanan PASSWORD 'your_password';"
sudo -u postgres psql -c "GRANT ALL PRIVILEGES ON DATABASE vanan TO vanan;"
```

---

## **3. DEPLOYMENT**

### **3.1 Docker Deployment**

**Build Images:**
```bash
# Build all services
docker-compose build

# Hoac build tung service
docker-compose build gateway
docker-compose build corehub
docker-compose build shoperp
docker-compose build khachlink
```

**Start Services:**
```bash
# Start all services
docker-compose up -d

# Kiêm tra status
docker-compose ps

# Xem logs
docker-compose logs -f
```

**Production Compose:**
```yaml
# docker-compose.production.yml
version: '3.8'
services:
  gateway:
    image: vanan/gateway:latest
    ports:
      - "5000:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    depends_on:
      - corehub
      - redis

  corehub:
    image: vanan/corehub:latest
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    depends_on:
      - postgres
      - redis

  postgres:
    image: postgres:15
    environment:
      - POSTGRES_DB=vanan
      - POSTGRES_USER=vanan
      - POSTGRES_PASSWORD=your_password
    volumes:
      - postgres_data:/var/lib/postgresql/data

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"

volumes:
  postgres_data:
```

### **3.2 Database Migration**

**Run Migrations:**
```bash
# Vào container corehub
docker-compose exec corehub bash

# Run EF Core migrations
dotnet ef database update

# Hoac tao migration mói
dotnet ef migrations add InitialCreate
dotnet ef database update
```

**Seed Data:**
```bash
# Seed initial data
dotnet run --project SeedData
```

### **3.3 Health Checks**

**Service Health:**
```bash
# Kiêm tra health endpoints
curl http://localhost:5000/health/gateway
curl http://localhost:5001/health/corehub
curl http://localhost:5002/health/shoperp
curl http://localhost:5003/health/khachlink
```

**Database Health:**
```bash
# Kiêm tra ket noi database
docker-compose exec postgres psql -U vanan -d vanan -c "SELECT 1;"

# Kiêm tra Redis
docker-compose exec redis redis-cli ping
```

---

## **4. MONITORING & LOGGING**

### **4.1 Application Logs**

**View Logs:**
```bash
# Real-time logs
docker-compose logs -f gateway
docker-compose logs -f corehub

# Logs theo thoi gian
docker-compose logs --since="2024-01-01" --until="2024-01-02"

# Logs theo level
docker-compose logs gateway | grep ERROR
docker-compose logs corehub | grep WARNING
```

**Log Locations:**
- **Gateway:** `/var/log/vanan/gateway.log`
- **CoreHub:** `/var/log/vanan/corehub.log`
- **Database:** `/var/log/postgresql/`
- **System:** `/var/log/syslog`

### **4.2 Performance Monitoring**

**System Metrics:**
```bash
# CPU & Memory
htop
docker stats

# Disk Usage
df -h
du -sh /var/lib/docker/

# Network
netstat -tulpn
ss -tulpn
```

**Application Metrics:**
```bash
# Response time
curl -w "@curl-format.txt" -o /dev/null -s http://localhost:5000/api/orders

# Database performance
docker-compose exec postgres psql -U vanan -d vanan -c "SELECT * FROM pg_stat_activity;"
```

### **4.3 Alerting**

**Critical Alerts:**
- Service down (> 5 minutes)
- Database connection failed
- Disk usage > 80%
- Memory usage > 90%
- Error rate > 5%

**Warning Alerts:**
- High response time (> 2s)
- Database slow queries
- Cache miss rate > 20%
- Low disk space (< 20%)

---

## **5. BACKUP & RECOVERY**

### **5.1 Database Backup**

**Automated Backup:**
```bash
#!/bin/bash
# backup.sh
DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_DIR="/backups/vanan"
mkdir -p $BACKUP_DIR

# PostgreSQL backup
docker-compose exec postgres pg_dump -U vanan vanan > $BACKUP_DIR/vanan_$DATE.sql

# Compress backup
gzip $BACKUP_DIR/vanan_$DATE.sql

# Delete old backups (7 days)
find $BACKUP_DIR -name "*.gz" -mtime +7 -delete
```

**Manual Backup:**
```bash
# Full backup
docker-compose exec postgres pg_dump -U vanan vanan > backup.sql

# Schema only
docker-compose exec postgres pg_dump -U vanan --schema-only vanan > schema.sql

# Data only
docker-compose exec postgres pg_dump -U vanan --data-only vanan > data.sql
```

### **5.2 Restore Database**

**From Backup:**
```bash
# Stop services
docker-compose stop corehub gateway

# Restore database
docker-compose exec postgres psql -U vanan -d vanan < backup.sql

# Restart services
docker-compose start corehub gateway
```

**Point-in-Time Recovery:**
```bash
# Restore to specific time
docker-compose exec postgres pg_restore -U vanan --clean --if-exists --verbose backup.sql
```

### **5.3 File System Backup**

**Application Files:**
```bash
# Backup application files
tar -czf app_backup.tar.gz /opt/vanan/

# Backup configuration
tar -czf config_backup.tar.gz /etc/vanan/
```

**Docker Volumes:**
```bash
# Backup Docker volumes
docker run --rm -v postgres_data:/data -v $(pwd):/backup alpine tar czf /backup/postgres_data.tar.gz /data
```

---

## **6. MAINTENANCE**

### **6.1 Regular Maintenance**

**Daily Tasks:**
- [ ] Kiêm tra health status
- [ ] Kiêm tra disk space
- [ ] Review error logs
- [ ] Monitor performance metrics

**Weekly Tasks:**
- [ ] Database backup verification
- [ ] Security updates
- [ ] Performance optimization
- [ ] Log rotation

**Monthly Tasks:**
- [ ] Full system backup
- [ ] Security audit
- [ ] Capacity planning
- [ ] Documentation update

### **6.2 Software Updates**

**Application Updates:**
```bash
# Pull latest code
git pull origin main

# Build new images
docker-compose build --no-cache

# Rolling update
docker-compose up -d --no-deps gateway
docker-compose up -d --no-deps corehub
```

**System Updates:**
```bash
# Ubuntu/Debian
apt update && apt upgrade -y

# CentOS/RHEL
yum update -y
```

**Database Updates:**
```bash
# PostgreSQL updates
docker-compose exec postgres psql -U vanan -c "SELECT version();"

# Run migrations after update
docker-compose exec corehub dotnet ef database update
```

### **6.3 Performance Tuning**

**Database Optimization:**
```sql
-- Analyze query performance
EXPLAIN ANALYZE SELECT * FROM Orders WHERE CreatedAt > '2024-01-01';

-- Rebuild indexes
REINDEX DATABASE vanan;

-- Update statistics
ANALYZE;
```

**Application Tuning:**
```bash
# Connection pool tuning
# In appsettings.json
"ConnectionStrings": {
  "VanAnDb": "Host=localhost;Database=vanan;Username=vanan;Password=xxx;Maximum Pool Size=100;Timeout=30;"
}

# Redis optimization
"Redis": {
  "ConnectionString": "localhost:6379",
  "Database": 0,
  "ConnectTimeout": 5000,
  "SyncTimeout": 5000
}
```

---

## **7. TROUBLESHOOTING**

### **7.1 Common Issues**

**Service Won't Start:**
```bash
# Kiêm tra logs
docker-compose logs service_name

# Kiêm tra port conflicts
netstat -tulpn | grep :5000

# Kiêm tra resources
docker stats
```

**Database Connection Issues:**
```bash
# Test connection
docker-compose exec postgres psql -U vanan -d vanan -c "SELECT 1;"

# Kiêm tra connection limits
docker-compose exec postgres psql -U vanan -d vanan -c "SELECT * FROM pg_settings WHERE name LIKE 'max_connections';"
```

**Performance Issues:**
```bash
# Kiêm tra slow queries
docker-compose exec postgres psql -U vanan -d vanan -c "SELECT query, mean_time, calls FROM pg_stat_statements ORDER BY mean_time DESC LIMIT 10;"

# Kiêm table sizes
docker-compose exec postgres psql -U vanan -d vanan -c "SELECT schemaname,tablename,attname,n_distinct,correlation FROM pg_stats;"
```

### **7.2 Emergency Procedures**

**Service Down:**
1. Kiêm tra logs: `docker-compose logs -f`
2. Restart service: `docker-compose restart service_name`
3. Kiêm tra dependencies
4. Scale up if needed: `docker-compose up -d --scale service_name=2`

**Database Corruption:**
1. Stop application: `docker-compose stop corehub gateway`
2. Restore from backup: `psql -U vanan vanan < backup.sql`
3. Verify data integrity
4. Restart services

**Full System Recovery:**
1. Restore from latest backup
2. Verify all services
3. Test critical functionality
4. Monitor for issues

---

## **8. SECURITY**

### **8.1 Security Best Practices**

**Network Security:**
```bash
# Firewall setup
ufw allow 22/tcp    # SSH
ufw allow 80/tcp    # HTTP
ufw allow 443/tcp   # HTTPS
ufw enable

# SSL/TLS
# Use Let's Encrypt or commercial certificates
```

**Application Security:**
```bash
# Update dependencies
dotnet list package --outdated
dotnet add package [package_name]

# Security scanning
dotnet dev-certs https --trust
```

**Database Security:**
```sql
-- Limit database access
REVOKE ALL ON SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO vanan;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO vanan;

-- Enable row-level security
ALTER TABLE orders ENABLE ROW LEVEL SECURITY;
```

### **8.2 Access Control**

**User Management:**
```bash
# Create admin user
docker-compose exec corehub dotnet user create admin@vanan.shop --role Admin

# Create staff user
docker-compose exec corehub dotnet user create staff@vanan.shop --role Staff
```

**API Security:**
```bash
# API Key management
# Generate secure API keys
openssl rand -hex 32

# Rate limiting
# Configure in Gateway
```

---

## **9. SCALING**

### **9.1 Horizontal Scaling**

**Load Balancing:**
```yaml
# docker-compose.scale.yml
version: '3.8'
services:
  gateway:
    image: vanan/gateway:latest
    deploy:
      replicas: 3
    
  corehub:
    image: vanan/corehub:latest
    deploy:
      replicas: 2
```

**Database Scaling:**
```bash
# Read replicas
# Configure in appsettings.json
"ConnectionStrings": {
  "VananDbReadOnly": "Host=replica1;Database=vanan;Username=readonly;Password=xxx;"
}
```

### **9.2 Vertical Scaling**

**Resource Allocation:**
```yaml
# docker-compose.production.yml
services:
  corehub:
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 4G
        reservations:
          cpus: '1.0'
          memory: 2G
```

---

## **10. COMPLIANCE & AUDIT**

### **10.1 Logging Requirements**

**Audit Trail:**
- All user actions logged
- Data changes tracked
- System events recorded
- Logs retained 90 days

**Log Format:**
```json
{
  "timestamp": "2024-01-01T12:00:00Z",
  "level": "INFO",
  "service": "Gateway",
  "action": "UserLogin",
  "userId": "user123",
  "tenantId": "shop456",
  "ipAddress": "192.168.1.100",
  "message": "User logged in successfully"
}
```

### **10.2 Data Protection**

**GDPR Compliance:**
- User consent management
- Data retention policies
- Right to be forgotten
- Data breach procedures

**Data Encryption:**
- Data at rest encrypted
- Data in transit encrypted
- Key management
- Access logging

---

## **11. DISASTER RECOVERY**

### **11.1 RTO/RPO**

**Recovery Time Objective (RTO):** 4 hours  
**Recovery Point Objective (RPO):** 1 hour

**Recovery Steps:**
1. Assess damage
2. Restore from backup
3. Verify data integrity
4. Restart services
5. Test functionality
6. Monitor performance

### **11.2 Backup Strategy**

**3-2-1 Rule:**
- **3** copies of data
- **2** different media types
- **1** off-site backup

**Backup Schedule:**
- **Real-time:** Transaction logs
- **Hourly:** Incremental backups
- **Daily:** Full backups
- **Weekly:** Off-site backup

---

## **12. CONTACT & SUPPORT**

### **12.1 Emergency Contacts**

**Primary Contact:**
- **Name:** System Administrator
- **Phone:** +84-xxx-xxx-xxx
- **Email:** admin@vanan.shop

**Secondary Contact:**
- **Name:** DevOps Engineer
- **Phone:** +84-xxx-xxx-xxx
- **Email:** devops@vanan.shop

### **12.2 Vendor Support**

**Software Vendors:**
- **Microsoft:** .NET Support
- **PostgreSQL:** Database Support
- **Redis:** Cache Support
- **Docker:** Container Support

**Service Providers:**
- **Cloud Provider:** AWS/Azure/GCP
- **CDN Provider:** CloudFlare
- **Monitoring:** DataDog/New Relic

---

## **PHÊ DUYÊT TÀI LIÊU**

**Ngày:** 10/04/2026  
**Ngày Viêt:** [Tên]  
**Ngày Phê Duyêt:** [Tên]  
**Chuc Vu:** DevOps Manager  
**Email:** devops@vanan.shop

---

*Luu y: Tài liêu này se câp nhât khi có thay doi trong hê thông hoac khi thêm tính nang mói. Vui lòng kiêm tra phiên ban mói nhât.*
