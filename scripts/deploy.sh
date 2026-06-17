#!/bin/bash
# VanAn Ecosystem - VPS Bootstrap & Deploy Script
# Usage: ./scripts/deploy.sh
# Requires: .env file in /opt/vanan/ with all required variables
set -e

DEPLOY_DIR="/opt/vanan"
COMPOSE_FILE="$DEPLOY_DIR/docker-compose.prod.yml"

echo "=== VanAn Deploy Script ==="
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
# 3. Validate .env exists
# ----------------------------------------
if [ ! -f "$DEPLOY_DIR/.env" ]; then
  echo "[error] .env file not found at $DEPLOY_DIR/.env"
  echo "        Please create it with required variables (see .env.example)"
  exit 1
fi

# ----------------------------------------
# 4. Pull latest images
# ----------------------------------------
echo "[deploy] Pulling images..."
cd "$DEPLOY_DIR"
docker compose -f "$COMPOSE_FILE" pull

# ----------------------------------------
# 5. Start / update services
# ----------------------------------------
echo "[deploy] Starting services..."
docker compose -f "$COMPOSE_FILE" up -d --remove-orphans

# ----------------------------------------
# 6. Health check
# ----------------------------------------
echo "[deploy] Waiting 30s for services to stabilize..."
sleep 30

echo "[deploy] Service status:"
docker compose -f "$COMPOSE_FILE" ps

echo ""
echo "=== Deploy complete! ==="
echo "Gateway:   http://$(hostname -I | awk '{print $1}'):5000"
echo "ShopERP:   http://$(hostname -I | awk '{print $1}'):5002"
echo "KhachLink: http://$(hostname -I | awk '{print $1}'):5003"
echo "Seq logs:  http://$(hostname -I | awk '{print $1}'):8081"
