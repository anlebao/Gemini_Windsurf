# Multi-stage build for VanAn Ecosystem
# .NET 8 Dockerfiles with proper context handling

## Build Strategy:
1. **Base Stage**: .NET 8 ASP.NET runtime
2. **Build Stage**: .NET 8 SDK with solution-level context
3. **Publish Stage**: Optimized release build
4. **Final Stage**: Production-ready image

## Context Handling:
- Copies from root directory (`../`) to access 1_Shared
- Uses solution file for dependency resolution
- Layered copying for optimal Docker caching

## Services:
- CoreHub (Backend API) - Port 5000
- Gateway (API Gateway) - Port 5001  
- KhachLink (Customer PWA) - Port 5002
- ShopERP (Management PWA) - Port 5003

## Usage:
```bash
# Build all services
docker-compose build

# Start ecosystem
docker-compose up -d

# View logs
docker-compose logs -f

# Stop ecosystem
docker-compose down
```

## Environment Variables:
- ASPNETCORE_ENVIRONMENT: Development
- ConnectionStrings__DefaultConnection: PostgreSQL connection
- NATS__Url: NATS messaging connection

## Health Checks:
- PostgreSQL: pg_isready command
- NATS: HTTP monitoring endpoint
- Services: Automatic health monitoring
