#!/bin/bash
# RV script for #101 + #102 fixes (commit a842a8b9) — v2 with WAL copy
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

echo -n "[1] PG: Settings_LegalForm column exists in Tenants table... "
COL=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -tAc "SELECT column_name FROM information_schema.columns WHERE table_name='Tenants' AND column_name='Settings_LegalForm';" 2>&1)
if echo "$COL" | grep -q "Settings_LegalForm"; then echo "PASS"; PASS=$((PASS+1)); else echo "FAIL ($COL)"; FAIL=$((FAIL+1)); fi

echo -n "[2] PG: Settings_NavColor column exists in Tenants table... "
COL=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -tAc "SELECT column_name FROM information_schema.columns WHERE table_name='Tenants' AND column_name='Settings_NavColor';" 2>&1)
if echo "$COL" | grep -q "Settings_NavColor"; then echo "PASS"; PASS=$((PASS+1)); else echo "FAIL ($COL)"; FAIL=$((FAIL+1)); fi

echo -n "[3] PG: Settings_CharterCapital column exists in Tenants table... "
COL=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -tAc "SELECT column_name FROM information_schema.columns WHERE table_name='Tenants' AND column_name='Settings_CharterCapital';" 2>&1)
if echo "$COL" | grep -q "Settings_CharterCapital"; then echo "PASS"; PASS=$((PASS+1)); else echo "FAIL ($COL)"; FAIL=$((FAIL+1)); fi

echo -n "[4] PG: All 6 new columns present (LegalForm, BusinessField, CharterCapital, NavColor, HeaderColor, FooterColor)... "
COUNT=$(docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -tAc "SELECT count(*) FROM information_schema.columns WHERE table_name='Tenants' AND column_name IN ('Settings_LegalForm','Settings_BusinessField','Settings_CharterCapital','Settings_NavColor','Settings_HeaderColor','Settings_FooterColor');" 2>&1)
if [ "$COUNT" = "6" ]; then echo "PASS (6/6)"; PASS=$((PASS+1)); else echo "FAIL ($COUNT/6)"; FAIL=$((FAIL+1)); fi

echo -n "[5] Gateway container healthy... "
HEALTH=$(docker inspect --format='{{.State.Health.Status}}' vanan-gateway 2>&1)
if echo "$HEALTH" | grep -q "healthy"; then echo "PASS ($HEALTH)"; PASS=$((PASS+1)); else echo "WARN ($HEALTH)"; WARN=$((WARN+1)); fi

echo -n "[6] Gateway /health returns 200... "
CODE=$(curl -s -o /dev/null -w "%{http_code}" "https://api.${DOMAIN}/health" 2>&1)
if [ "$CODE" = "200" ]; then echo "PASS (200)"; PASS=$((PASS+1)); else echo "FAIL ($CODE)"; FAIL=$((FAIL+1)); fi

echo -n "[7] Gateway /api/v1/tenants returns 401/302 (not 500 — migration applied)... "
CODE=$(curl -s -o /dev/null -w "%{http_code}" "https://api.${DOMAIN}/api/v1/tenants" 2>&1)
if [ "$CODE" = "401" ] || [ "$CODE" = "302" ]; then echo "PASS ($CODE — auth required, not 500)"; PASS=$((PASS+1)); elif [ "$CODE" = "500" ]; then echo "FAIL (500)"; FAIL=$((FAIL+1)); else echo "WARN ($CODE)"; WARN=$((WARN+1)); fi

# ── #102: Financial Statement Notes (B 09-DN) ──────────────────────────────
echo ""
echo "── #102: Financial Statement Notes (B 09-DN) ──"

# Copy SQLite DB WITH WAL files for accurate read
rm -f /tmp/rvL.db*
docker cp vanan-shoperp:/app/keys/vanan_shoperp.db /tmp/rvL.db 2>/dev/null
docker cp vanan-shoperp:/app/keys/vanan_shoperp.db-wal /tmp/rvL.db-wal 2>/dev/null
docker cp vanan-shoperp:/app/keys/vanan_shoperp.db-shm /tmp/rvL.db-shm 2>/dev/null

echo -n "[8] SQLite: Migration 20260804155333 applied... "
MIG=$(sqlite3 /tmp/rvL.db "SELECT MigrationId FROM __EFMigrationsHistory WHERE MigrationId='20260804155333_AddTenantSettingsB09DNAndStyleColumns';" 2>&1)
if echo "$MIG" | grep -q "20260804155333"; then echo "PASS"; PASS=$((PASS+1)); else echo "FAIL ($MIG)"; FAIL=$((FAIL+1)); fi

echo -n "[9] SQLite: All 6 new TenantSettings columns present... "
COUNT=$(sqlite3 /tmp/rvL.db "SELECT count(*) FROM pragma_table_info('Tenants') WHERE name IN ('Settings_LegalForm','Settings_BusinessField','Settings_CharterCapital','Settings_NavColor','Settings_HeaderColor','Settings_FooterColor');" 2>&1)
if [ "$COUNT" = "6" ]; then echo "PASS (6/6)"; PASS=$((PASS+1)); else echo "FAIL ($COUNT/6)"; FAIL=$((FAIL+1)); fi

echo -n "[10] ShopERP container healthy... "
HEALTH=$(docker inspect --format='{{.State.Health.Status}}' vanan-shoperp 2>&1)
if echo "$HEALTH" | grep -q "healthy"; then echo "PASS ($HEALTH)"; PASS=$((PASS+1)); else echo "WARN ($HEALTH)"; WARN=$((WARN+1)); fi

echo -n "[11] ShopERP /accounting/financial-statement-notes returns 200/302 (not 500)... "
CODE=$(curl -s -o /dev/null -w "%{http_code}" "https://app.${DOMAIN}/accounting/financial-statement-notes" 2>&1)
if [ "$CODE" = "200" ] || [ "$CODE" = "302" ] || [ "$CODE" = "301" ]; then echo "PASS ($CODE)"; PASS=$((PASS+1)); elif [ "$CODE" = "500" ]; then echo "FAIL (500)"; FAIL=$((FAIL+1)); else echo "WARN ($CODE)"; WARN=$((WARN+1)); fi

echo -n "[12] ShopERP /admin/tenants returns 200/302 (not 500)... "
CODE=$(curl -s -o /dev/null -w "%{http_code}" "https://app.${DOMAIN}/admin/tenants" 2>&1)
if [ "$CODE" = "200" ] || [ "$CODE" = "302" ] || [ "$CODE" = "301" ]; then echo "PASS ($CODE)"; PASS=$((PASS+1)); elif [ "$CODE" = "500" ]; then echo "FAIL (500)"; FAIL=$((FAIL+1)); else echo "WARN ($CODE)"; WARN=$((WARN+1)); fi

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
