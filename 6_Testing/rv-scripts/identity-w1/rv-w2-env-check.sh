#!/bin/bash
# W2 — .env gateway: Brevo keys (masked) + sender
ENV=/opt/vanan/.env
echo "== .env exists =="
ls -la "$ENV" 2>/dev/null || echo "NO /opt/vanan/.env"
echo "== Brevo lines (masked) =="
grep -iE '^Brevo' "$ENV" 2>/dev/null | sed -E 's/=(.{0,8}).*/=\1***MASKED***/'
echo "== compose env_file refs =="
grep -nE 'env_file|Brevo' /opt/vanan/docker-compose.gateway.yml 2>/dev/null | head -8
echo "== done =="