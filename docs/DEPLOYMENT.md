# Deployment Guide

## Environment Variables Configuration

### Required Variables

The following environment variables are required for the application to run:

| Variable | Description | Example |
|----------|-------------|---------|
| `POSTGRES_PASSWORD` | PostgreSQL database password | Strong password (32+ chars) |
| `JWT_SECRET_KEY` | JWT signing key for authentication | 256-bit random key |
| `SEQ_ADMIN_PASSWORD` | Seq log viewer admin password | Strong password |
| `VAPID_PUBLIC_KEY` | VAPID public key for push notifications | Generated via web-push |
| `VAPID_PRIVATE_KEY` | VAPID private key for push notifications | Generated via web-push |
| `VAPID_SUBJECT` | VAPID subject email | mailto:admin@vanan.com |

### Environment Files

#### `.env.example`
Template file with placeholder values. Copy this to create your local `.env` file.

```bash
cp .env.example .env
```

**Important**: Replace placeholder values with actual secrets before running in production.

#### `.env.test`
Test environment file for CI/CD. This file is committed to source control and contains test-only values.

**DO NOT** use these values in production.

#### `.env`
Local development environment file. This file is **NOT** committed to source control (see `.gitignore`).

### Development Setup

1. Copy the example environment file:
   ```bash
   cp .env.example .env
   ```

2. Edit `.env` and replace placeholder values:
   ```bash
   # Use strong passwords for local development
   POSTGRES_PASSWORD=your-local-dev-password
   JWT_SECRET_KEY=your-local-dev-jwt-key-256bit
   SEQ_ADMIN_PASSWORD=your-local-dev-seq-password
   ```

3. For VAPID keys, generate them:
   ```bash
   npm install -g web-push
   web-push generate-vapid-keys
   ```

4. Update the VAPID keys in `.env`

### Production Deployment

#### GitHub Actions (CD)

The CD workflow (`cd.yml`) automatically:
1. Builds and pushes Docker images to GHCR
2. Validates environment configuration using `.env.example`
3. Deploys to VPS using secrets from GitHub Actions

**Required GitHub Secrets:**
- `POSTGRES_PASSWORD` - Production PostgreSQL password
- `JWT_SECRET_KEY` - Production JWT signing key
- `VPS_HOST` - VPS hostname/IP
- `VPS_USER` - VPS SSH username
- `VPS_SSH_PRIVATE_KEY` - SSH private key for VPS access

#### Manual VPS Deployment

1. SSH into VPS:
   ```bash
   ssh user@your-vps-host
   ```

2. Create environment file:
   ```bash
   cd /opt/vanan
   nano .env
   ```

3. Add production values:
   ```bash
   POSTGRES_PASSWORD=STRONG_PRODUCTION_PASSWORD_HERE
   JWT_SECRET_KEY=256BIT_RANDOM_KEY_HERE
   SEQ_ADMIN_PASSWORD=STRONG_PRODUCTION_PASSWORD_HERE
   VAPID_PUBLIC_KEY=YOUR_VAPID_PUBLIC_KEY
   VAPID_PRIVATE_KEY=YOUR_VAPID_PRIVATE_KEY
   VAPID_SUBJECT=mailto:admin@vanan.com
   ```

4. Deploy using docker-compose:
   ```bash
   docker compose -f docker-compose.prod.yml up -d
   ```

### Database Configuration

The Gateway supports multiple database providers through configuration:

#### Development (SQLite)
For local development and tests, Gateway uses SQLite in-memory database:

```json
{
  "Database": {
    "Provider": "SQLite",
    "ConnectionString": "DataSource=:memory:"
  }
}
```

#### Production (PostgreSQL)
For production, Gateway uses PostgreSQL:

```json
{
  "Database": {
    "Provider": "PostgreSQL"
  }
}
```

The PostgreSQL connection string is read from `ConnectionStrings:DefaultConnection` in the environment or `docker-compose.prod.yml`.

### Security Best Practices

1. **Never commit actual secrets** to source control
2. **Use strong passwords** (32+ characters, mixed case, numbers, symbols)
3. **Rotate secrets regularly** (every 90 days for production)
4. **Use different secrets** for each environment (dev, staging, production)
5. **Limit access** to secrets to only authorized personnel
6. **Audit secret access** logs regularly

### Validation

Run the environment validation script locally:

```bash
pwsh -File scripts/validate-env-vars.ps1 -EnvFile .env
```

This script checks:
- Required variables are present
- Variable naming conventions (UPPER_CASE)
- Empty values
- Weak/common passwords
- Consistency with docker-compose files

### Troubleshooting

#### CI/CD Failures

**Error**: `Missing required variables: POSTGRES_PASSWORD, JWT_SECRET_KEY, SEQ_ADMIN_PASSWORD`

**Solution**: Ensure `.env.test` exists and contains all required variables.

**Error**: `Variable is using placeholder value`

**Solution**: This is a warning for `.env.example`. For production, replace placeholders with actual values in GitHub Secrets or VPS `.env` file.

#### Database Connection Issues

**Error**: `Failed to connect to PostgreSQL`

**Solution**:
1. Verify PostgreSQL is running: `docker ps | grep postgres`
2. Check connection string in `.env` or docker-compose
3. Ensure database is created and accessible

#### Application Startup Issues

**Error**: `Jwt:Secret configuration is required`

**Solution**: Ensure `JWT_SECRET_KEY` is set in environment variables or appsettings.

### Monitoring

After deployment, monitor:
- Application logs via Seq: `http://your-vps:5341`
- Docker container health: `docker compose ps`
- Gateway health endpoint: `http://your-vps:5001/health`

### Rollback

If deployment fails, rollback to previous version:

```bash
# List previous images
docker images | grep vanan

# Rollback to specific image tag
docker compose -f docker-compose.prod.yml up -d --scale gateway=0
docker compose -f docker-compose.prod.yml up -d
```

### Support

For deployment issues:
1. Check logs: `docker compose logs`
2. Validate configuration: `pwsh -File scripts/validate-env-vars.ps1`
3. Review CI/CD logs in GitHub Actions
4. Check this documentation for common issues