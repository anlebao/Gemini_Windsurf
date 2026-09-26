#!/bin/bash
# VanAn Multi-VPS Rollback — redeploy ONE VPS to a previous immutable image tag.
#
# Usage (run ON the target VPS, or via SSH):
#   ./scripts/rollback-app.sh <gateway|khachlink|shoperp> <image-tag>
#
# <image-tag> = full git SHA of a previous build (e.g. a7541fe0...) or 'latest'.
# Find the SHA of the currently deployed image:
#   cd /opt/vanan && sudo docker compose -f <compose>.yml ps --format json | grep -i image
# (or check the last CD run: workflow -> Log deploy scope -> image_tag)
#
# Target: <= 2-3 min rollback. No rebuild, no redeploy of unaffected apps.
# IMPORTANT: rollback does NOT undo DB migrations — migrations are forward-only.
# If the bad deploy applied a migration, roll back CODE only and fix-forward.
set -e

VPS="$1"
TARGET_TAG="$2"

if [ -z "$VPS" ] || [ -z "$TARGET_TAG" ]; then
  echo "Usage: $0 <gateway|khachlink|shoperp> <image-tag>"
  echo "Example: $0 shoperp a7541fe0f5c2c75d33147e8d92a08f1b5c6d3a410"
  exit 1
fi

DEPLOY_DIR="/opt/vanan"
case "$VPS" in
  gateway)   COMPOSE="docker-compose.gateway.yml";   ENV_FILE=".env.gateway" ;;
  khachlink) COMPOSE="docker-compose.khachlink.yml"; ENV_FILE=".env.khachlink" ;;
  shoperp)   COMPOSE="docker-compose.shoperp.yml";   ENV_FILE=".env.shoperp" ;;
  *) echo "Unknown VPS '$VPS' (use gateway|khachlink|shoperp)"; exit 1 ;;
esac

cd "$DEPLOY_DIR"
[ -f "$ENV_FILE" ] || { echo "[error] $ENV_FILE not found in $DEPLOY_DIR"; exit 1; }

CURRENT=$(grep -E '^IMAGE_TAG=' "$ENV_FILE" | cut -d= -f2- || echo "(unset -> latest)")
echo "[rollback] $VPS: IMAGE_TAG $CURRENT -> $TARGET_TAG"

# Swap IMAGE_TAG (idempotent: replace existing line or append)
if grep -q '^IMAGE_TAG=' "$ENV_FILE"; then
  sed -i "s|^IMAGE_TAG=.*|IMAGE_TAG=${TARGET_TAG}|" "$ENV_FILE"
else
  echo "IMAGE_TAG=${TARGET_TAG}" >> "$ENV_FILE"
fi

# Pull + recreate (same retry pattern as CD — transient ghcr.io CDN resets)
for i in 1 2 3 4 5; do
  timeout 600 sudo docker compose -f "$COMPOSE" --env-file "$ENV_FILE" pull && break
  echo "[rollback] Pull attempt $i failed, retrying in 15s..."
  sleep 15
done
sudo docker compose -f "$COMPOSE" --env-file "$ENV_FILE" up -d --remove-orphans --wait --wait-timeout 240

echo "=== $VPS rollback to $TARGET_TAG complete ==="
sudo docker compose -f "$COMPOSE" --env-file "$ENV_FILE" ps
