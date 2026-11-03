#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# RV Smart Entry parser — chạy TRÊN VPS (vanan-shop-a).
# Copy harness vào container shoperp đang chạy, load DLL deployed /app/VanAn.CoreHub.dll,
# chạy toàn bộ case trong cases.json, kéo report về host.
#
# Cần trên host: RvSmartEntryParser.dll + RvSmartEntryParser.runtimeconfig.json + cases.json
# Usage: bash run-on-vps.sh   (từ thư mục chứa artifacts)
# Env:   SHOPERP_CONTAINER=<name>  (mặc định auto-detect `docker ps | grep shoperp`)
#        APP_DIR=/app              (thư mục publish trong container)
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail
SRC="$(cd "$(dirname "$0")" && pwd)"
APP_DIR="${APP_DIR:-/app}"
WORK="/tmp/rv-smart-entry"

CID="${SHOPERP_CONTAINER:-$(docker ps --format '{{.Names}}' | grep -iE 'shoperp' | head -n1)}"
if [ -z "$CID" ]; then echo "ERROR: không tìm thấy container shoperp"; docker ps --format '{{.Names}}'; exit 2; fi
echo "container : $CID"
docker exec "$CID" test -f "$APP_DIR/VanAn.CoreHub.dll" || { echo "ERROR: $APP_DIR/VanAn.CoreHub.dll không tồn tại"; exit 2; }
docker exec "$CID" sh -c "ls -l $APP_DIR/VanAn.CoreHub.dll $APP_DIR/VanAn.Shared.dll 2>/dev/null | head -5"

docker exec "$CID" mkdir -p "$WORK"
for f in RvSmartEntryParser.dll RvSmartEntryParser.runtimeconfig.json cases.json; do
  docker cp "$SRC/$f" "$CID:$WORK/$f"
done

echo "── run harness ──"
set +e
docker exec "$CID" dotnet "$WORK/RvSmartEntryParser.dll" \
  "$APP_DIR/VanAn.CoreHub.dll" "$WORK/cases.json" "$WORK/report.json"
RC=$?
set -e

docker cp "$CID:$WORK/report.json" "$SRC/rv-smart-entry-report.json" 2>/dev/null || true
echo "report → $SRC/rv-smart-entry-report.json"
exit $RC
