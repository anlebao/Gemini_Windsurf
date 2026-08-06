#!/bin/bash
# VanAn Multi-VPS Deploy — ShopERP VPS
# Run on: vanan-shop-a VPS (34.177.89.248, internal 10.148.0.3)
# Prerequisites: SSH into the VPS, clone repo, .env.shoperp configured.
# Usage: ./scripts/deploy-shoperp.sh
set -e

DEPLOY_DIR="/opt/vanan"
COMPOSE_FILE="$DEPLOY_DIR/docker-compose.shoperp.yml"
ENV_FILE="$DEPLOY_DIR/.env.shoperp"

echo "=== VanAn Multi-VPS Deploy — ShopERP VPS ==="
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
# 3. Validate .env.shoperp exists
# ----------------------------------------
if [ ! -f "$ENV_FILE" ]; then
  echo "[error] .env.shoperp not found at $ENV_FILE"
  echo "        Copy .env.shoperp.example and fill in real values:"
  echo "        cp .env.shoperp.example .env.shoperp"
  echo "        nano .env.shoperp"
  exit 1
fi

# Validate critical vars
source "$ENV_FILE"
if [[ "$POSTGRES_PASSWORD" == *"CHANGE_THIS"* ]] || [[ "$JWT_SECRET_KEY" == *"CHANGE_THIS"* ]]; then
  echo "[error] .env.shoperp still has placeholder values. Edit it first."
  exit 1
fi
if [ -z "$NATS_REMOTE_HOST" ] || [ -z "$GATEWAY_REMOTE_HOST" ]; then
  echo "[error] NATS_REMOTE_HOST or GATEWAY_REMOTE_HOST not set in .env.shoperp"
  echo "        Set both to the VPC internal IP of vanan-gateway (e.g. 10.148.0.2)"
  exit 1
fi
if [ -z "$SHOP_INSTANCE_ID" ] || [[ "$SHOP_INSTANCE_ID" == "00000000-0000-0000-0000-000000000001" ]]; then
  echo "[error] SHOP_INSTANCE_ID not set or still default in .env.shoperp"
  echo "        Generate a NEW Guid for this VPS: uuidgen or python3 -c 'import uuid; print(uuid.uuid4())'"
  echo "        This must match the ShopInstance record in Gateway PG."
  exit 1
fi

echo "[config] VANAN_DOMAIN=$VANAN_DOMAIN"
echo "[config] NATS_REMOTE_HOST=$NATS_REMOTE_HOST"
echo "[config] GATEWAY_REMOTE_HOST=$GATEWAY_REMOTE_HOST"
echo "[config] SHOP_INSTANCE_ID=$SHOP_INSTANCE_ID"

# ----------------------------------------
# 4. Test connectivity to Gateway VPS (NATS + PostgreSQL)
# ----------------------------------------
echo "[connectivity] Testing NATS port on Gateway VPS..."
if ! nc -zv "$NATS_REMOTE_HOST" 4222 -w 5 2>&1; then
  echo "[error] Cannot reach NATS at ${NATS_REMOTE_HOST}:4222"
  echo "        Check: GCP firewall rule 'allow-nats-internal' allows TCP 4222 from this VPS."
  echo "        Check: NATS container is running on Gateway VPS."
  exit 1
fi
echo "[connectivity] NATS reachable ✓"

echo "[connectivity] Testing PostgreSQL port on Gateway VPS..."
if ! nc -zv "$GATEWAY_REMOTE_HOST" 5432 -w 5 2>&1; then
  echo "[error] Cannot reach PostgreSQL at ${GATEWAY_REMOTE_HOST}:5432"
  echo "        Check: GCP firewall rule allows TCP 5432 from this VPS (shop-erp tag)."
  echo "        Check: PostgreSQL container is running on Gateway VPS."
  echo "        You may need to create a firewall rule:"
  echo "        gcloud compute firewall-rules create allow-postgres-internal \\"
  echo "          --direction=INGRESS --action=ALLOW --rules=tcp:5432 \\"
  echo "          --source-tags=shop-erp --target-tags=gateway --network=vanan-vpc"
  exit 1
fi
echo "[connectivity] PostgreSQL reachable ✓"

# ----------------------------------------
# 5. Pull latest image
# ----------------------------------------
echo "[deploy] Pulling ShopERP image..."
cd "$DEPLOY_DIR"
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" pull

# ----------------------------------------
# 6. Start ShopERP
# ----------------------------------------
echo "[deploy] Starting ShopERP..."
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" up -d --remove-orphans

# ----------------------------------------
# 7. Health check
# ----------------------------------------
echo "[deploy] Waiting 60s for ShopERP to stabilize..."
sleep 60

echo ""
echo "[deploy] Service status:"
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" ps

echo ""
echo "=== ShopERP VPS deploy complete! ==="
echo ""
echo "Services on this VPS:"
echo "  ShopERP:  port 80 (accessible from Gateway VPS nginx via VPC internal IP)"
echo "  SQLite:   /app/keys/vanan_shoperp.db (persistent volume)"
echo ""
echo "NEXT STEPS:"
echo "  1. Verify health: curl http://localhost/health"
echo "  2. Check NATS subscription: docker logs vanan-shoperp 2>&1 | grep 'OrderSyncSubscriber'"
echo "  3. Create ShopInstance record in Gateway PG (via Gateway API or SQL) with Id=$SHOP_INSTANCE_ID"
echo "  4. Onboard first tenant via Gateway API with shopInstanceId=$SHOP_INSTANCE_ID"
