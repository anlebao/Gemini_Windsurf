#!/usr/bin/env bash
#
# vps-disk-cleanup.sh — VanAn prod VPS disk auto-cleanup
#
# Runs from cron daily; only acts when disk usage >= threshold.
#   docker image prune -af  : remove images not used by any container — never touches
#                             volumes/containers/networks; CD re-pulls on next deploy
#   journalctl --vacuum-size: cap systemd journal at 100M
#
# Usage:
#   vps-disk-cleanup.sh [--prune-if-above PCT] [--check]
#     --prune-if-above PCT  prune only when / usage >= PCT (default 80)
#     --check               report only, do not prune
#
# Requirements: docker group member (docker CLI) + passwordless sudo (journalctl).
# Log: stdout — point cron at a log file, e.g.
#   0 3 * * * /opt/vanan/scripts/vps-disk-cleanup.sh --prune-if-above 80 >> /home/lebao/vps-disk-cleanup.log 2>&1

set -euo pipefail

THRESHOLD=80
CHECK_ONLY=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --prune-if-above) THRESHOLD="${2:-80}"; shift 2 ;;
    --check) CHECK_ONLY=1; shift ;;
    *) echo "usage: $0 [--prune-if-above PCT] [--check]" >&2; exit 2 ;;
  esac
done

now()    { date '+%Y-%m-%d %H:%M:%S'; }
usage_pct() { df -h / | awk 'NR==2 {gsub("%","",$5); print $5}'; }
used_gb()   { df -h / | awk 'NR==2 {print $3}'; }
avail_gb()  { df -h / | awk 'NR==2 {print $4}'; }

USAGE=$(usage_pct)
USED=$(used_gb)
AVAIL=$(avail_gb)
echo "[$(now)] disk / = ${USAGE}% used (${USED} used, ${AVAIL} available), threshold ${THRESHOLD}%"

if [[ "$CHECK_ONLY" == "1" ]]; then
  echo "[$(now)] --check: no prune performed"
  exit 0
fi

if (( USAGE < THRESHOLD )); then
  echo "[$(now)] below threshold — skip"
  exit 0
fi

BEFORE=$(used_gb)
echo "[$(now)] pruning unused docker images..."
docker image prune -af || echo "[$(now)] WARN: docker image prune failed"
echo "[$(now)] vacuuming journal to 100M..."
sudo -n journalctl --vacuum-size=100M || echo "[$(now)] WARN: journalctl vacuum failed"
AFTER=$(used_gb)
AFTER_USAGE=$(usage_pct)
AFTER_AVAIL=$(avail_gb)
echo "[$(now)] done: ${BEFORE} -> ${AFTER} used (${USAGE}% -> ${AFTER_USAGE}%), ${AFTER_AVAIL} available"
