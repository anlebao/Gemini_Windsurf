#!/bin/bash
# W2 — kiểm tra trạng thái Brevo trên gateway VPS (KHÔNG in giá trị key)
echo "== gateway env Brevo (masked) =="
docker inspect vanan-gateway-1 --format '{{range .Config.Env}}{{println .}}{{end}}' 2>/dev/null | grep -iE '^Brevo' | sed -E 's/=(.{0,6}).*/=\1***MASKED***/'
echo "== compose dir + env files =="
ls -la /opt/vanan-gateway/ 2>/dev/null | head -8 || ls -la /opt/*/ 2>/dev/null | grep -iE 'compose|env' | head -8
echo "== done =="