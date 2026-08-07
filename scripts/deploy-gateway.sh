#!/bin/bash
# VanAn Multi-VPS Deploy — Gateway VPS
# Run on: vanan-gateway VPS (136.85.94.119, internal 10.148.0.2)
# Prerequisites: SSH into the VPS, clone repo, .env.gateway configured.
# Usage: ./scripts/deploy-gateway.sh
set -e

DEPLOY_DIR="/opt/vanan"
COMPOSE_FILE="$DEPLOY_DIR/docker-compose.gateway.yml"
ENV_FILE="$DEPLOY_DIR/.env.gateway"

echo "=== VanAn Multi-VPS Deploy — Gateway VPS ==="
echo "Deploy dir: $DEPLOY_DIR"
echo "Time: $(date)"

# ----------------------------------------
# 1. Bootstrap Docker (idempotent)
# ----------------------------------------
if ! command -v docker &> /dev/null; then
  echo "[bootstrap] Installing Docker..."
  curl -fsSL https://get.docker.com | sh
  sudo usermod -aG docker "$USER"
  echo "[bootstrap] Docker installed. Re-run this script if group changes don't take effect."
fi

if ! docker compose version &> /dev/null 2>&1; then
  echo "[bootstrap] Installing Docker Compose plugin..."
  sudo apt-get update -qq
  sudo apt-get install -y docker-compose-plugin
fi

echo "[bootstrap] Docker $(docker --version)"
echo "[bootstrap] Docker Compose $(docker compose version)"

# ----------------------------------------
# 2. Prepare deploy directory
# ----------------------------------------
sudo mkdir -p "$DEPLOY_DIR"
sudo chown "$USER":"$USER" "$DEPLOY_DIR"

# ----------------------------------------
# 3. Validate .env.gateway exists
# ----------------------------------------
if [ ! -f "$ENV_FILE" ]; then
  echo "[error] .env.gateway not found at $ENV_FILE"
  echo "        Copy .env.gateway.example and fill in real values:"
  echo "        cp .env.gateway.example .env.gateway"
  echo "        nano .env.gateway"
  exit 1
fi

# Validate critical vars are not placeholders
source "$ENV_FILE"
if [[ "$POSTGRES_PASSWORD" == *"CHANGE_THIS"* ]] || [[ "$JWT_SECRET_KEY" == *"CHANGE_THIS"* ]]; then
  echo "[error] .env.gateway still has placeholder values. Edit it first."
  exit 1
fi
if [ -z "$SHOPERP_REMOTE_HOST" ]; then
  echo "[error] SHOPERP_REMOTE_HOST not set in .env.gateway"
  echo "        Set it to the VPC internal IP of vanan-shop-a (e.g. 10.148.0.3)"
  exit 1
fi

echo "[config] VANAN_DOMAIN=$VANAN_DOMAIN"
echo "[config] SHOPERP_REMOTE_HOST=$SHOPERP_REMOTE_HOST"

# ----------------------------------------
# 4. Pull latest images
# ----------------------------------------
echo "[deploy] Pulling images..."
cd "$DEPLOY_DIR"
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" pull

# ----------------------------------------
# 5. Start services
# ----------------------------------------
echo "[deploy] Stopping existing services..."
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" down --remove-orphans 2>/dev/null || true

echo "[deploy] Starting services..."
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" up -d --remove-orphans

# ----------------------------------------
# 6. Health check
# ----------------------------------------
echo "[deploy] Waiting 60s for services to stabilize..."
sleep 60

echo ""
echo "[deploy] Service status:"
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" ps

echo ""
echo "=== Gateway VPS deploy complete! ==="
echo ""
echo "Services on this VPS:"
echo "  PostgreSQL:  internal only (port 5432 exposed for shop-a VPS)"
echo "  NATS:         port 4222 (VPC), monitoring 8222 (localhost)"
echo "  Gateway API:  internal (via nginx)"
echo "  KhachLink:    internal (via nginx)"
echo "  nginx:        ports 80, 443 (public)"
echo "  Seq logs:     http://localhost:5341 (SSH tunnel: ssh -L 5341:localhost:5341 gateway)"
echo ""
echo "NEXT STEPS:"
echo "  1. Deploy ShopERP on vanan-shop-a VPS (run deploy-shoperp.sh there)"
echo "  2. Create ShopInstance record in Gateway PG for vanan-shop-a"
echo "  3. Setup SSL certs (run scripts/init-ssl.sh)"
echo "  4. Test: curl http://localhost/health (via nginx) or http://api.${VANAN_DOMAIN}/health"
