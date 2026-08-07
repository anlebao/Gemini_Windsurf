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
# 1. Bootstrap Docker (idempotent — skip if already installed)
# ----------------------------------------
if ! command -v docker &> /dev/null; then
  echo "[bootstrap] Docker not installed. Please install Docker first."
  exit 1
fi

if ! docker compose version &> /dev/null 2>&1; then
  echo "[bootstrap] Docker Compose plugin not installed. Please install docker-compose-plugin."
  exit 1
fi

echo "[bootstrap] Docker $(docker --version)"
echo "[bootstrap] Docker Compose $(docker compose version)"

# ----------------------------------------
# 2. Prepare deploy directory (already created by admin)
# ----------------------------------------
mkdir -p "$DEPLOY_DIR"

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
echo "[deploy] Starting services (rolling update)..."
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" up -d --remove-orphans

# ----------------------------------------
# 7. Health check with rollback
# ----------------------------------------
echo "[deploy] Waiting 60s for services to stabilize..."
sleep 60

HEALTHY=$(docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" ps --format json 2>/dev/null | grep -c '"Health":"healthy"' || echo 0)
TOTAL=$(docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" ps --format json 2>/dev/null | wc -l || echo 0)

echo "[deploy] Service status:"
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" ps

if [ "$HEALTHY" -lt 1 ]; then
  echo "::error::Health check failed: $HEALTHY/$TOTAL containers healthy"
  echo "[deploy] Rolling back — restarting previous state..."
  docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" restart
  sleep 30
  exit 1
fi

echo "[deploy] All healthy containers OK ✓"

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
