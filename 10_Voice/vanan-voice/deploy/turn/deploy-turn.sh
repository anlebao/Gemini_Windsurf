# vanan-voice — Coturn on Ubuntu VPS (SRS v1.1 §8.2). Run as root/ubuntu with sudo.
# Usage:
#   export TURN_PUBLIC_HOST=<public-ip-or-dns> TURN_SECRET=<secret> TURN_REALM=<realm>
#   export TURN_MIN_RELAY_PORT=49152 TURN_MAX_RELAY_PORT=65535
#   bash deploy-turn.sh
#
# Requires: docker + docker compose plugin. Separate node from CoreHub DB workloads (SRS §8.2).
set -euo pipefail

: "${TURN_PUBLIC_HOST:?set TURN_PUBLIC_HOST}"
: "${TURN_SECRET:?set TURN_SECRET (never commit)}"
TURN_REALM="${TURN_REALM:-khachvip.online}"
TURN_MIN_RELAY_PORT="${TURN_MIN_RELAY_PORT:-49152}"
TURN_MAX_RELAY_PORT="${TURN_MAX_RELAY_PORT:-65535}"
TURN_IMAGE="${TURN_IMAGE:-coturn/coturn:4.6}"

APP_DIR=/opt/vanan-turn
mkdir -p "$APP_DIR"

# Generate turnserver.conf from the template (no secret on the command line).
sed -e "s/__PUBLIC_HOST__/$TURN_PUBLIC_HOST/g" \
    -e "s/__SECRET__/$TURN_SECRET/g" \
    -e "s/__REALM__/$TURN_REALM/g" \
    -e "s/__MIN_RELAY_PORT__/$TURN_MIN_RELAY_PORT/g" \
    -e "s/__MAX_RELAY_PORT__/$TURN_MAX_RELAY_PORT/g" \
    "$(dirname "$0")/turnserver.conf" > "$APP_DIR/turnserver.conf"
chmod 600 "$APP_DIR/turnserver.conf"

docker rm -f vanan-turn 2>/dev/null || true
docker run -d --name vanan-turn --restart unless-stopped \
  -p 3478:3478/udp \
  -p 3478:3478/tcp \
  -p 5349:5349/tcp \
  -p "$TURN_MIN_RELAY_PORT-$TURN_MAX_RELAY_PORT:$TURN_MIN_RELAY_PORT-$TURN_MAX_RELAY_PORT/udp" \
  -v "$APP_DIR/turnserver.conf:/etc/turnserver.conf:ro" \
  -v "$APP_DIR/logs:/var/log" \
  "$TURN_IMAGE" -c /etc/turnserver.conf

echo "Coturn started. Verify:"
echo "  docker logs vanan-turn | tail"
echo "  ss -ulpn | grep 3478   (STUN/TURN UDP)"
echo "  docker exec vanan-turn turnadmin -l   (if troubleshooting)"