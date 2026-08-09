#!/bin/bash
# RV script for #101 + #102 fixes (commit a842a8b9)
# #101: Gateway tenant list loading — verify PG migration applied + Gateway API returns tenants
# #102: Financial Statement Notes (B 09-DN) — verify SQLite migration applied + page loads

set -u
PASS=0
WARN=0
FAIL=0
DOMAIN="khachvip.online"

echo "=== RV #101 + #102 — $(date) ==="
echo "Commit: a842a8b9"
echo ""

# ── #101: Gateway tenant list ──────────────────────────────────────────────
echo "── #101: Gateway tenant list (PG migration + API) ──"

# 1. Verify PG migration applied — check Settings_LegalForm column exists
echo -n "[1] PG: Settings_LegalForm column exists in Tenants table... "
COL=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -tAc "SELECT column_name FROM information_schema.columns WHERE table_name='Tenants' AND column_name='Settings_LegalForm';" 2>&1)
if echo "$COL" | grep -q "Settings_LegalForm"; then
  echo "PASS"; PASS=$((PASS+1))
else
  echo "FAIL ($COL)"; FAIL=$((FAIL+1))
fi

# 2. Verify PG: Settings_NavColor column exists
echo -n "[2] PG: Settings_NavColor column exists in Tenants table... "
COL=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -tAc "SELECT column_name FROM information_schema.columns WHERE table_name='Tenants' AND column_name='Settings_NavColor';" 2>&1)
if echo "$COL" | grep -q "Settings_NavColor"; then
  echo "PASS"; PASS=$((PASS+1))
else
  echo "FAIL ($COL)"; FAIL=$((FAIL+1))
fi

# 3. Verify PG: Settings_CharterCapital column exists
echo -n "[3] PG: Settings_CharterCapital column exists in Tenants table... "
COL=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -tAc "SELECT column_name FROM information_schema.columns WHERE table_name='Tenants' AND column_name='Settings_CharterCapital';" 2>&1)
if echo "$COL" | grep -q "Settings_CharterCapital"; then
  echo "PASS"; PASS=$((PASS+1))
else
  echo "FAIL ($COL)"; FAIL=$((FAIL+1))
fi

# 4. Verify Gateway container healthy
echo -n "[4] Gateway container healthy... "
HEALTH=$(docker inspect --format='{{.State.Health.Status}}' vanan-gateway 2>&1)
if echo "$HEALTH" | grep -q "healthy"; then
  echo "PASS ($HEALTH)"; PASS=$((PASS+1))
else
  echo "WARN ($HEALTH)"; WARN=$((WARN+1))
fi

# 5. Verify Gateway /health endpoint returns 200
echo -n "[5] Gateway /health returns 200... "
CODE=$(curl -s -o /dev/null -w "%{http_code}" "https://api.${DOMAIN}/health" 2>&1)
if [ "$CODE" = "200" ]; then
  echo "PASS (200)"; PASS=$((PASS+1))
else
  echo "FAIL ($CODE)"; FAIL=$((FAIL+1))
fi

# 6. Verify Gateway tenants API endpoint reachable (will 401 without auth, NOT 500)
echo -n "[6] Gateway /api/v1/tenants returns 401 (not 500 — migration applied)... "
CODE=$(curl -s -o /dev/null -w "%{http_code}" "https://api.${DOMAIN}/api/v1/tenants" 2>&1)
if [ "$CODE" = "401" ]; then
  echo "PASS (401 — auth required, not 500)"; PASS=$((PASS+1))
elif [ "$CODE" = "500" ]; then
  echo "FAIL (500 — migration may not be applied)"; FAIL=$((FAIL+1))
else
  echo "WARN ($CODE)"; WARN=$((WARN+1))
fi

# ── #102: Financial Statement Notes (B 09-DN) ──────────────────────────────
echo ""
echo "── #102: Financial Statement Notes (B 09-DN) ──"

# 7. Verify SQLite migration applied — check Settings_LegalForm column exists in ShopERP SQLite
echo -n "[7] SQLite: Settings_LegalForm column exists in Tenants table... "
COL=$(docker exec vanan-shoperp sqlite3 /app/keys/vanan_shoperp.db "PRAGMA table_info(Tenants);" 2>&1 | grep "Settings_LegalForm")
if [ -n "$COL" ]; then
  echo "PASS"; PASS=$((PASS+1))
else
  echo "FAIL ($COL)"; FAIL=$((FAIL+1))
fi

# 8. Verify SQLite: Settings_NavColor column exists
echo -n "[8] SQLite: Settings_NavColor column exists in Tenants table... "
COL=$(docker exec vanan-shoperp sqlite3 /app/keys/vanan_shoperp.db "PRAGMA table_info(Tenants);" 2>&1 | grep "Settings_NavColor")
if [ -n "$COL" ]; then
  echo "PASS"; PASS=$((PASS+1))
else
  echo "FAIL ($COL)"; FAIL=$((FAIL+1))
fi

# 9. Verify SQLite: Settings_CharterCapital column exists
echo -n "[9] SQLite: Settings_CharterCapital column exists in Tenants table... "
COL=$(docker exec vanan-shoperp sqlite3 /app/keys/vanan_shoperp.db "PRAGMA table_info(Tenants);" 2>&1 | grep "Settings_CharterCapital")
if [ -n "$COL" ]; then
  echo "PASS"; PASS=$((PASS+1))
else
  echo "FAIL ($COL)"; FAIL=$((FAIL+1))
fi

# 10. Verify ShopERP container healthy
echo -n "[10] ShopERP container healthy... "
HEALTH=$(docker inspect --format='{{.State.Health.Status}}' vanan-shoperp 2>&1)
if echo "$HEALTH" | grep -q "healthy"; then
  echo "PASS ($HEALTH)"; PASS=$((PASS+1))
else
  echo "WARN ($HEALTH)"; WARN=$((WARN+1))
fi

# 11. Verify ShopERP /accounting/financial-statement-notes page returns 200 (not 500)
echo -n "[11] ShopERP /accounting/financial-statement-notes returns 200... "
CODE=$(curl -s -o /dev/null -w "%{http_code}" "https://app.${DOMAIN}/accounting/financial-statement-notes" 2>&1)
if [ "$CODE" = "200" ]; then
  echo "PASS (200)"; PASS=$((PASS+1))
elif [ "$CODE" = "302" ] || [ "$CODE" = "301" ]; then
  echo "WARN ($CODE — redirect to login, expected for unauthenticated)"; WARN=$((WARN+1))
elif [ "$CODE" = "500" ]; then
  echo "FAIL (500 — page still erroring)"; FAIL=$((FAIL+1))
else
  echo "WARN ($CODE)"; WARN=$((WARN+1))
fi

# 12. Verify ShopERP /admin/tenants page returns 200/302 (not 500)
echo -n "[12] ShopERP /admin/tenants returns 200/302 (not 500)... "
CODE=$(curl -s -o /dev/null -w "%{http_code}" "https://app.${DOMAIN}/admin/tenants" 2>&1)
if [ "$CODE" = "200" ]; then
  echo "PASS (200)"; PASS=$((PASS+1))
elif [ "$CODE" = "302" ] || [ "$CODE" = "301" ]; then
  echo "PASS ($CODE — redirect to login, expected for unauthenticated)"; PASS=$((PASS+1))
elif [ "$CODE" = "500" ]; then
  echo "FAIL (500 — page still erroring)"; FAIL=$((FAIL+1))
else
  echo "WARN ($CODE)"; WARN=$((WARN+1))
fi

# ── Summary ────────────────────────────────────────────────────────────────
echo ""
echo "=== SUMMARY ==="
echo "PASS: $PASS"
echo "WARN: $WARN"
echo "FAIL: $FAIL"
echo "Total: $((PASS+WARN+FAIL))"
if [ "$FAIL" -eq 0 ]; then
  echo "RESULT: ✅ ALL CRITICAL CHECKS PASS"
else
  echo "RESULT: ❌ $FAIL FAILURES — investigate"
fi
