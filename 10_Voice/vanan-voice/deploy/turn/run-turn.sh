#!/usr/bin/env bash
# Run Coturn on the voice VPS — reads TURN__Secret from /opt/vanan-voice/.env.voice.
set -euo pipefail
cd /opt/vanan-voice/turn
TS=$(grep '^TURN__Secret=' /opt/vanan-voice/.env.voice | cut -d= -f2-)
sed -e "s/__PUBLIC_HOST__/34.87.116.62/g" \
    -e "s/__SECRET__/$TS/g" \
    -e "s/__REALM__/khachvip.online/g" \
    -e "s/__MIN_RELAY_PORT__/49152/g" \
    -e "s/__MAX_RELAY_PORT__/65535/g" \
    turnserver.conf > /opt/vanan-turn.conf
chmod 600 /opt/vanan-turn.conf
sudo docker rm -f vanan-turn 2>/dev/null || true
sudo docker run -d --name vanan-turn --restart unless-stopped \
  -p 3478:3478/udp -p 3478:3478/tcp -p 5349:5349/tcp \
  -p 49152-65535:49152-65535/udp \
  -v /opt/vanan-turn.conf:/etc/turnserver.conf:ro \
  coturn/coturn:4.6 -c /etc/turnserver.conf
sleep 8
sudo docker logs vanan-turn 2>&1 | tail -5
echo "TURN_DEPLOY_DONE"