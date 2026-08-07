#!/bin/bash
# VanAn Multi-VPS Deploy — KhachLink VPS
# Run on: vanan-khachlink VPS
# Prerequisites: SSH into the VPS, clone repo, .env.khachlink configured.
# Usage: ./scripts/deploy-khachlink.sh
set -e

DEPLOY_DIR="/opt/vanan"
COMPOSE_FILE="$DEPLOY_DIR/docker-compose.khachlink.yml"
ENV_FILE="$DEPLOY_DIR/.env.khachlink"

echo "=== VanAn Multi-VPS Deploy — KhachLink VPS ==="
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
# 3. Validate .env.khachlink exists
# ----------------------------------------
if [ ! -f "$ENV_FILE" ]; then
  echo "[error] .env.khachlink not found at $ENV_FILE"
  echo "        Copy .env.khachlink.example and fill in real values:"
  echo "        cp .env.khachlink.example .env.khachlink"
  echo "        nano .env.khachlink"
  exit 1
fi

source "$ENV_FILE"
if [[ "$SEQ_ADMIN_PASSWORD" == *"CHANGE_THIS"* ]]; then
  echo "[error] .env.khachlink still has placeholder values. Edit it first."
  exit 1
fi
if [ -z "$GATEWAY_REMOTE_HOST" ]; then
  echo "[error] GATEWAY_REMOTE_HOST not set in .env.khachlink"
  echo "        Set to VPC internal IP of vanan-gateway (e.g. 10.148.0.2)"
  exit 1
fi

echo "[config] VANAN_DOMAIN=$VANAN_DOMAIN"
echo "[config] GATEWAY_REMOTE_HOST=$GATEWAY_REMOTE_HOST"

# ----------------------------------------
# 4. Test connectivity to Gateway VPS (API port 80)
# ----------------------------------------
echo "[connectivity] Testing Gateway API on ${GATEWAY_REMOTE_HOST}:80..."
if ! timeout 5 bash -c "echo > /dev/tcp/${GATEWAY_REMOTE_HOST}/80" 2>&1; then
  echo "[error] Cannot reach Gateway at ${GATEWAY_REMOTE_HOST}:80"
  echo "        Check: Gateway VPS is running + nginx container is up."
  echo "        Check: GCP firewall rule allows TCP 80 from khachlink tag to gateway tag."
  exit 1
fi
echo "[connectivity] Gateway API reachable ✓"

# ----------------------------------------
# 5. Pull latest image
# ----------------------------------------
echo "[deploy] Pulling KhachLink image..."
cd "$DEPLOY_DIR"
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" pull

# ----------------------------------------
# 6. Start KhachLink + Seq + Certbot
# ----------------------------------------
echo "[deploy] Starting services..."
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" up -d --remove-orphans

# ----------------------------------------
# 7. Health check
# ----------------------------------------
echo "[deploy] Waiting 30s for services to stabilize..."
sleep 30

echo ""
echo "[deploy] Service status:"
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" ps

echo ""
echo "=== KhachLink VPS deploy complete! ==="
echo ""
echo "Services on this VPS:"
echo "  KhachLink: port 80 (accessible from Gateway nginx via VPC internal IP)"
echo "  Seq:       port 5341 (receives logs from Gateway via VPC)"
echo "  Certbot:   SSL renewal (shares volumes with Gateway nginx via NFS/sync)"
echo ""
echo "NEXT STEPS:"
echo "  1. Verify health: curl http://localhost/health"
echo "  2. Check Seq UI: curl http://localhost:5341"
echo "  3. On Gateway VPS, verify Serilog sends logs to ${GATEWAY_REMOTE_HOST}:5341"
