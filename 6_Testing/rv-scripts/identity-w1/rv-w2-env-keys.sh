#!/bin/bash
# W2 — danh sách key trong /opt/vanan/.env (chỉ tên + masked)
ENV=/opt/vanan/.env
grep -E '^[A-Za-z_][A-Za-z0-9_]*=' "$ENV" 2>/dev/null | cut -d= -f1 | head -40
echo "== sender email? =="
grep -iE 'sender|brevo|esms' "$ENV" | cut -d= -f1
echo "== done =="