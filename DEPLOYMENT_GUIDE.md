# 🚀 Van An Ecosystem - Deployment Guide

> **Complete Deployment Instructions for Production and Development**
>
> Version: 2.0 | Last Updated: 31/03/2026

---

## 🎯 Deployment Overview

Van An Ecosystem supports multiple deployment scenarios from local development to enterprise production environments.

### 🏗️ Deployment Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Production Architecture                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐         │
│  │  Load       │    │  CDN        │    │  Monitoring │         │
│  │  Balancer   │    │  CloudFlare │    │  Prometheus │         │
│  │  :80/443    │    │  :443       │    │  :9090      │         │
│  └─────┬───────┘    └─────┬───────┘    └─────┬───────┘         │
│        │                  │                  │                 │
│        └──────────────────┼──────────────────┘                 │
│                           │                                    │
│  ┌────────────────────────▼────────────────────────┐         │
│  │                Kubernetes Cluster                │         │
│  │  ┌─────────────┐  ┌─────────────┐  ┌───────────┐ │         │
│  │  │  App Pods   │  │  Database   │  │  Message   │ │         │
│  │  │  (3+ Reps)  │  │  PostgreSQL │  │  Broker    │ │         │
│  │  │             │  │  (HA)       │  │  (NATS)    │ │         │
│  │  └─────────────┘  └─────────────┘  └───────────┘ │         │
│  └─────────────────────────────────────────────────────┘         │
└─────────────────────────────────────────────────────────────────┘
```

---

## 📋 Deployment Options

### 1. Local Development
- **Purpose**: Development and testing
- **Environment**: Docker Compose
- **Database**: PostgreSQL + InMemory for tests
- **Monitoring**: Basic health checks

### 2. Staging Environment
- **Purpose**: Pre-production testing
- **Environment**: Docker Swarm
- **Database**: PostgreSQL with replica
- **Monitoring**: Full observability stack

### 3. Production Environment
- **Purpose**: Live customer traffic
- **Environment**: Kubernetes
- **Database**: PostgreSQL HA cluster
- **Monitoring**: Enterprise monitoring

---

## 🛠️ LOCAL DEVELOPMENT DEPLOYMENT

### Prerequisites
- **Docker Desktop** 4.15+
- **.NET 8.0 SDK**
- **Git** 2.30+
- **Node.js** 18+ (for frontend tools)

### Quick Start
```bash
# Clone repository
git clone https://github.com/vanan-ecosystem/vanan-system.git
cd vanan-system

# Start all services
docker-compose up -d

# Wait for services (30 seconds)
sleep 30

# Run database migrations
dotnet ef database update --project 3_CoreHub/VanAn.CoreHub.csproj

# Verify deployment
curl http://localhost:5001/health
```

### Development Services
| Service | URL | Description |
|---------|-----|-------------|
| **KhachLink** | http://localhost:5002 | Customer ordering interface |
| **ShopERP** | http://localhost:5003 | Staff management interface |
| **Gateway API** | http://localhost:5001 | API gateway |
| **API Docs** | http://localhost:5001/swagger | Interactive API docs |
| **PostgreSQL** | localhost:5432 | Primary database |
| **pgAdmin** | http://localhost:5050 | Database management |

### Docker Compose Configuration
```yaml
version: '3.8'
services:
  postgres:
    image: postgres:15
    environment:
      POSTGRES_DB: VanAn
      POSTGRES_USER: vanan
      POSTGRES_PASSWORD: VanAn123!
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

  nats:
    image: nats:2.9
    ports:
      - "4222:4222"
      - "8222:8222"
    command: ["--jetstream"]

  corehub:
    build: ./3_CoreHub
    ports:
      - "5010:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=VanAn;Username=vanan;Password=VanAn123!
    depends_on:
      - postgres
      - nats

  gateway:
    build: ./2_Gateway
    ports:
      - "5001:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - COREHUB_URL=http://corehub:8080
    depends_on:
      - corehub

  khachlink:
    build: ./5_WebApps/KhachLink
    ports:
      - "5002:8080"
    environment:
      - GATEWAY_URL=http://localhost:5001
    depends_on:
      - gateway

  shoperp:
    build: ./5_WebApps/ShopERP
    ports:
      - "5003:8080"
    environment:
      - GATEWAY_URL=http://localhost:5001
    depends_on:
      - gateway

volumes:
  postgres_data:
```

---

## 🚀 STAGING DEPLOYMENT

### Environment Configuration
```bash
# Environment variables
export ASPNETCORE_ENVIRONMENT=Staging
export CONNECTION_STRING="Host=staging-db.vanan.vn;Database=VanAnStaging;Username=vanan_staging;Password=${STAGING_DB_PASSWORD}"
export NATS_URL="nats://staging-nats.vanan.vn:4222"
export REDIS_URL="redis://staging-redis.vanan.vn:6379"
export JWT_SECRET="${STAGING_JWT_SECRET}"
export SENTRY_DSN="${STAGING_SENTRY_DSN}"
```

### Docker Swarm Deployment
```yaml
version: '3.8'
services:
  postgres:
    image: postgres:15
    environment:
      POSTGRES_DB: VanAnStaging
      POSTGRES_USER: vanan_staging
      POSTGRES_PASSWORD: ${STAGING_DB_PASSWORD}
    volumes:
      - postgres_staging_data:/var/lib/postgresql/data
    deploy:
      replicas: 1
      resources:
        limits:
          memory: 2G
          cpus: '1.0'

  postgres-replica:
    image: postgres:15
    environment:
      POSTGRES_DB: VanAnStaging
      POSTGRES_USER: vanan_staging
      POSTGRES_PASSWORD: ${STAGING_DB_PASSWORD}
      POSTGRES_MASTER_SERVICE: postgres
    volumes:
      - postgres_replica_data:/var/lib/postgresql/data
    deploy:
      replicas: 1

  corehub:
    image: vanan/corehub:${VERSION}
    environment:
      - ASPNETCORE_ENVIRONMENT=Staging
      - ConnectionStrings__DefaultConnection=${CONNECTION_STRING}
      - NATS__Url=${NATS_URL}
    deploy:
      replicas: 2
      resources:
        limits:
          memory: 1G
          cpus: '0.5'

  gateway:
    image: vanan/gateway:${VERSION}
    environment:
      - ASPNETCORE_ENVIRONMENT=Staging
      - COREHUB_URL=http://corehub:8080
      - JWT_SECRET=${JWT_SECRET}
    deploy:
      replicas: 2
      resources:
        limits:
          memory: 512M
          cpus: '0.25'

volumes:
  postgres_staging_data:
  postgres_replica_data:
```

### Deployment Script
```bash
#!/bin/bash
# deploy-staging.sh

set -e

VERSION=${1:-latest}
ENVIRONMENT=staging

echo "Deploying Van An Ecosystem v${VERSION} to ${ENVIRONMENT}"

# Build images
docker build -t vanan/corehub:${VERSION} ./3_CoreHub
docker build -t vanan/gateway:${VERSION} ./2_Gateway
docker build -t vanan/khachlink:${VERSION} ./5_WebApps/KhachLink
docker build -t vanan/shoperp:${VERSION} ./5_WebApps/ShopERP

# Push to registry
docker push vanan/corehub:${VERSION}
docker push vanan/gateway:${VERSION}
docker push vanan/khachlink:${VERSION}
docker push vanan/shoperp:${VERSION}

# Deploy to swarm
docker-compose -f docker-compose.staging.yml pull
docker-compose -f docker-compose.staging.yml up -d

# Run database migrations
docker-compose -f docker-compose.staging.yml exec corehub \
  dotnet ef database update

# Health check
sleep 30
curl -f https://staging-api.vanan.vn/health || exit 1

echo "Deployment completed successfully!"
```

---

## 🏭 PRODUCTION DEPLOYMENT

### Kubernetes Manifests

#### Namespace
```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: vanan-production
  labels:
    name: vanan-production
```

#### ConfigMap
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: vanan-config
  namespace: vanan-production
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  NATS__Url: "nats://nats-service:4222"
  REDIS__Url: "redis://redis-service:6379"
  LOGGING__MINIMUMLEVEL: "Information"
  LOGGING__CONSOLE__ENABLED: "false"
```

#### Secrets
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: vanan-secrets
  namespace: vanan-production
type: Opaque
data:
  connection-string: <base64-encoded-connection-string>
  jwt-secret: <base64-encoded-jwt-secret>
  sentry-dsn: <base64-encoded-sentry-dsn>
```

#### PostgreSQL Deployment
```yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: postgres-primary
  namespace: vanan-production
spec:
  serviceName: postgres-primary
  replicas: 1
  selector:
    matchLabels:
      app: postgres-primary
  template:
    metadata:
      labels:
        app: postgres-primary
    spec:
      containers:
      - name: postgres
        image: postgres:15
        env:
        - name: POSTGRES_DB
          value: VanAn
        - name: POSTGRES_USER
          value: vanan
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: vanan-secrets
              key: postgres-password
        ports:
        - containerPort: 5432
        volumeMounts:
        - name: postgres-storage
          mountPath: /var/lib/postgresql/data
        resources:
          requests:
            memory: "2Gi"
            cpu: "1000m"
          limits:
            memory: "4Gi"
            cpu: "2000m"
  volumeClaimTemplates:
  - metadata:
      name: postgres-storage
    spec:
      accessModes: ["ReadWriteOnce"]
      resources:
        requests:
          storage: 100Gi
```

#### CoreHub Deployment
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: corehub
  namespace: vanan-production
spec:
  replicas: 3
  selector:
    matchLabels:
      app: corehub
  template:
    metadata:
      labels:
        app: corehub
    spec:
      containers:
      - name: corehub
        image: vanan/corehub:2.0.0
        ports:
        - containerPort: 8080
        envFrom:
        - configMapRef:
            name: vanan-config
        - secretRef:
            name: vanan-secrets
        resources:
          requests:
            memory: "1Gi"
            cpu: "500m"
          limits:
            memory: "2Gi"
            cpu: "1000m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 5
---
apiVersion: v1
kind: Service
metadata:
  name: corehub-service
  namespace: vanan-production
spec:
  selector:
    app: corehub
  ports:
  - port: 8080
    targetPort: 8080
  type: ClusterIP
```

#### Gateway Deployment
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: gateway
  namespace: vanan-production
spec:
  replicas: 2
  selector:
    matchLabels:
      app: gateway
  template:
    metadata:
      labels:
        app: gateway
    spec:
      containers:
      - name: gateway
        image: vanan/gateway:2.0.0
        ports:
        - containerPort: 8080
        envFrom:
        - configMapRef:
            name: vanan-config
        - secretRef:
            name: vanan-secrets
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "1Gi"
            cpu: "500m"
---
apiVersion: v1
kind: Service
metadata:
  name: gateway-service
  namespace: vanan-production
spec:
  selector:
    app: gateway
  ports:
  - port: 8080
    targetPort: 8080
  type: ClusterIP
```

#### Ingress Configuration
```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: vanan-ingress
  namespace: vanan-production
  annotations:
    kubernetes.io/ingress.class: "nginx"
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
    nginx.ingress.kubernetes.io/rate-limit: "100"
    nginx.ingress.kubernetes.io/rate-limit-window: "1m"
spec:
  tls:
  - hosts:
    - api.vanan.vn
    - app.vanan.vn
    secretName: vanan-tls
  rules:
  - host: api.vanan.vn
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: gateway-service
            port:
              number: 8080
  - host: app.vanan.vn
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: khachlink-service
            port:
              number: 8080
```

---

## 🔧 DEPLOYMENT AUTOMATION

### CI/CD Pipeline (GitHub Actions)
```yaml
name: Deploy to Production

on:
  push:
    tags:
      - 'v*'

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout code
        uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'

      - name: Build and test
        run: |
          dotnet build --configuration Release
          dotnet test --configuration Release --no-build

      - name: Build Docker images
        run: |
          docker build -t vanan/corehub:${{ github.ref_name }} ./3_CoreHub
          docker build -t vanan/gateway:${{ github.ref_name }} ./2_Gateway
          docker build -t vanan/khachlink:${{ github.ref_name }} ./5_WebApps/KhachLink
          docker build -t vanan/shoperp:${{ github.ref_name }} ./5_WebApps/ShopERP

      - name: Login to Container Registry
        run: echo ${{ secrets.DOCKER_PASSWORD }} | docker login -u ${{ secrets.DOCKER_USERNAME }} --password-stdin

      - name: Push Docker images
        run: |
          docker push vanan/corehub:${{ github.ref_name }}
          docker push vanan/gateway:${{ github.ref_name }}
          docker push vanan/khachlink:${{ github.ref_name }}
          docker push vanan/shoperp:${{ github.ref_name }}

      - name: Deploy to Kubernetes
        run: |
          echo ${{ secrets.KUBECONFIG }} | base64 -d > kubeconfig
          export KUBECONFIG=kubeconfig
          
          # Update image tags in manifests
          sed -i "s/VERSION/${{ github.ref_name }}/g" k8s/production/*.yaml
          
          # Apply manifests
          kubectl apply -f k8s/production/
          
          # Wait for rollout
          kubectl rollout status deployment/corehub -n vanan-production
          kubectl rollout status deployment/gateway -n vanan-production

      - name: Run smoke tests
        run: |
          sleep 60
          curl -f https://api.vanan.vn/health
          curl -f https://app.vanan.vn/health
```

### Database Migration Pipeline
```bash
#!/bin/bash
# migrate-database.sh

set -e

ENVIRONMENT=${1:-production}
VERSION=${2:-latest}

echo "Running database migrations for ${ENVIRONMENT}"

# Backup database before migration
kubectl exec -n vanan-production postgres-primary-0 -- \
  pg_dump -U vanan VanAn > "backup-$(date +%Y%m%d_%H%M%S).sql"

# Run migrations
kubectl exec -n vanan-production deployment/corehub -- \
  dotnet ef database update --no-build

# Verify migration
kubectl exec -n vanan-production postgres-primary-0 -- \
  psql -U vanan -d VanAn -c "SELECT version FROM __EFMigrationsHistory ORDER BY version DESC LIMIT 1;"

echo "Database migration completed successfully!"
```

---

## 📊 MONITORING & LOGGING

### Prometheus Configuration
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: prometheus-config
  namespace: monitoring
data:
  prometheus.yml: |
    global:
      scrape_interval: 15s
    scrape_configs:
      - job_name: 'vanan-corehub'
        static_configs:
          - targets: ['corehub-service:8080']
        metrics_path: /metrics
      - job_name: 'vanan-gateway'
        static_configs:
          - targets: ['gateway-service:8080']
        metrics_path: /metrics
```

### Grafana Dashboard
```json
{
  "dashboard": {
    "title": "Van An Ecosystem",
    "panels": [
      {
        "title": "API Response Time",
        "type": "graph",
        "targets": [
          {
            "expr": "histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))",
            "legendFormat": "P95 Response Time"
          }
        ]
      },
      {
        "title": "Request Rate",
        "type": "graph", 
        "targets": [
          {
            "expr": "rate(http_requests_total[5m])",
            "legendFormat": "Requests/sec"
          }
        ]
      }
    ]
  }
}
```

### ELK Stack for Logging
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: elasticsearch
  namespace: logging
spec:
  replicas: 1
  selector:
    matchLabels:
      app: elasticsearch
  template:
    metadata:
      labels:
        app: elasticsearch
    spec:
      containers:
      - name: elasticsearch
        image: elasticsearch:8.5.0
        env:
        - name: discovery.type
          value: single-node
        - name: ES_JAVA_OPTS
          value: "-Xms1g -Xmx1g"
        ports:
        - containerPort: 9200
        resources:
          requests:
            memory: "2Gi"
            cpu: "1000m"
          limits:
            memory: "4Gi"
            cpu: "2000m"
```

---

## 🔒 SECURITY CONFIGURATION

### Network Policies
```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: vanan-network-policy
  namespace: vanan-production
spec:
  podSelector: {}
  policyTypes:
  - Ingress
  - Egress
  ingress:
  - from:
    - namespaceSelector:
        matchLabels:
          name: ingress-nginx
    ports:
    - protocol: TCP
      port: 8080
  egress:
  - to:
    - podSelector:
        matchLabels:
          app: postgres
    ports:
    - protocol: TCP
      port: 5432
```

### Pod Security Policy
```yaml
apiVersion: policy/v1beta1
kind: PodSecurityPolicy
metadata:
  name: vanan-psp
spec:
  privileged: false
  allowPrivilegeEscalation: false
  requiredDropCapabilities:
    - ALL
  volumes:
    - 'configMap'
    - 'emptyDir'
    - 'projected'
    - 'secret'
    - 'downwardAPI'
    - 'persistentVolumeClaim'
  runAsUser:
    rule: 'MustRunAsNonRoot'
  seLinux:
    rule: 'RunAsAny'
  fsGroup:
    rule: 'RunAsAny'
```

---

## 🚨 ROLLBACK PROCEDURES

### Quick Rollback
```bash
# Rollback to previous version
kubectl rollout undo deployment/corehub -n vanan-production
kubectl rollout undo deployment/gateway -n vanan-production

# Verify rollback
kubectl rollout status deployment/corehub -n vanan-production
curl -f https://api.vanan.vn/health
```

### Database Rollback
```bash
# List available migrations
kubectl exec -n vanan-production postgres-primary-0 -- \
  psql -U vanan -d VanAn -c "SELECT version FROM __EFMigrationsHistory;"

# Rollback to specific migration
kubectl exec -n vanan-production deployment/corehub -- \
  dotnet ef database update PreviousMigration --no-build
```

### Emergency Rollback
```bash
# Scale down to zero
kubectl scale deployment corehub --replicas=0 -n vanan-production
kubectl scale deployment gateway --replicas=0 -n vanan-production

# Restore database from backup
kubectl exec -i postgres-primary-0 -- \
  psql -U vanan VanAn < backup-20260331_140000.sql

# Scale up services
kubectl scale deployment corehub --replicas=3 -n vanan-production
kubectl scale deployment gateway --replicas=2 -n vanan-production
```

---

## 📋 DEPLOYMENT CHECKLIST

### Pre-deployment Checklist
- [ ] All tests passing (unit, integration, API)
- [ ] Security scan completed
- [ ] Performance benchmarks met
- [ ] Documentation updated
- [ ] Backup procedures tested
- [ ] Monitoring dashboards configured
- [ ] Alert rules tested
- [ ] Rollback procedures verified

### Post-deployment Checklist
- [ ] Health checks passing
- [ ] Database migrations successful
- [ ] API endpoints responding
- [ ] Monitoring data flowing
- [ ] Load balancer configured
- [ ] SSL certificates valid
- [ ] Smoke tests passing
- [ ] Performance within SLA

---

## 📞 DEPLOYMENT SUPPORT

### Contact Information
- **DevOps Team**: devops@vanan.vn
- **On-call Engineer**: +84-123-456-789
- **Emergency Channel**: #deployment-alerts

### Troubleshooting Resources
- **Logs**: `kubectl logs -f deployment/corehub -n vanan-production`
- **Events**: `kubectl get events -n vanan-production --sort-by='.lastTimestamp'`
- **Metrics**: https://grafana.vanan.vn
- **Alerts**: https://alertmanager.vanan.vn

---

**© 2026 Van An Ecosystem - Deployment Guide v2.0**
