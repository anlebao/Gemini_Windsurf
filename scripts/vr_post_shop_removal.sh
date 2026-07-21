#!/bin/bash
# VR Test for main business flows post Shop entity removal

psql_q() {
    docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -A -c "$1" 2>/dev/null | tr -d '[:space:]'
}

curl_gw() {
    curl -s -o /dev/null -w "%{http_code}" "http://vanan-gateway:5001$1" 2>/dev/null
}

curl_nginx() {
    curl -s -o /dev/null -w "%{http_code}" "https://app.khachvip.online$1" 2>/dev/null
}

curl_kl() {
    curl -s -o /dev/null -w "%{http_code}" "https://diemthuong.khachvip.online$1" 2>/dev/null
}

echo "============================================================"
echo "VR TEST: Main business flows (post Shop entity removal)"
echo "Date: $(date -u '+%Y-%m-%d %H:%M:%S UTC')"
echo "============================================================"

echo ""
echo "=== [1] DB SCHEMA VERIFICATION (PG) ==="
SHOPS=$(psql_q "SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_name='Shops';")
[ "$SHOPS" = "0" ] && echo "  [1.1] Shops table dropped                    ✅" || echo "  [1.1] Shops table EXISTS                     ❌ ($SHOPS)"

LAT=$(psql_q "SELECT count(*) FROM information_schema.columns WHERE table_name='Tenants' AND column_name='Settings_Latitude';")
[ "$LAT" = "1" ] && echo "  [1.2] Tenants.Settings_Latitude added        ✅" || echo "  [1.2] Tenants.Settings_Latitude MISSING      ❌"

LNG=$(psql_q "SELECT count(*) FROM information_schema.columns WHERE table_name='Tenants' AND column_name='Settings_Longitude';")
[ "$LNG" = "1" ] && echo "  [1.3] Tenants.Settings_Longitude added       ✅" || echo "  [1.3] Tenants.Settings_Longitude MISSING     ❌"

SC_SHOPID=$(psql_q "SELECT count(*) FROM information_schema.columns WHERE table_name='SocialCampaigns' AND column_name='ShopId';")
[ "$SC_SHOPID" = "0" ] && echo "  [1.4] SocialCampaigns.ShopId dropped         ✅" || echo "  [1.4] SocialCampaigns.ShopId EXISTS          ❌"

SI=$(psql_q "SELECT count(*) FROM information_schema.tables WHERE table_name='ShopInstances';")
SFS=$(psql_q "SELECT count(*) FROM information_schema.tables WHERE table_name='ShopFeatureSettings';")
[ "$SI" = "1" ] && [ "$SFS" = "1" ] && echo "  [1.5] ShopInstances + ShopFeatureSettings   ✅ (intact)" || echo "  [1.5] ShopInstances/Settings MISSING        ❌"

TENANTS=$(psql_q "SELECT count(*) FROM \"Tenants\";")
CAMPAIGNS=$(psql_q "SELECT count(*) FROM \"SocialCampaigns\" WHERE \"IsDeleted\" = false;")
ORDERS=$(psql_q "SELECT count(*) FROM \"Orders\";")
OI=$(psql_q "SELECT count(*) FROM \"OrderItems\";")
AE=$(psql_q "SELECT count(*) FROM \"AccountingEntries\";")
JE=$(psql_q "SELECT count(*) FROM \"JournalEntries\";")
echo "  [1.6] Data counts: Tenants=$TENANTS Campaigns=$CAMPAIGNS Orders=$ORDERS OrderItems=$OI AccEntries=$AE JnlEntries=$JE"

echo ""
echo "=== [2] CONTAINER HEALTH ==="
for c in vanan-gateway vanan-shoperp vanan-khachlink vanan-postgres vanan-nats; do
    S=$(docker inspect --format='{{.State.Health.Status}}' $c 2>/dev/null || echo "n/a")
    [ "$S" = "healthy" ] && echo "  $c: $S ✅" || echo "  $c: $S"
done

echo ""
echo "=== [3] TENANTSTORECONTROLLER (replaces ShopsController) ==="
TID=$(psql_q "SELECT \"Id\" FROM \"Tenants\" LIMIT 1;")
echo "  Test tenant: $TID"

S1=$(curl_gw "/api/tenants/$TID/store-info")
[ "$S1" = "200" ] && echo "  [3.1] GET /api/tenants/{id}/store-info → $S1   ✅" || echo "  [3.1] → $S1 ❌"

S2=$(curl_gw "/api/tenants/nearby?lat=10.77&lng=106.70&radiusKm=50")
[ "$S2" = "200" ] && echo "  [3.2] GET /api/tenants/nearby → $S2              ✅" || echo "  [3.2] → $S2 ❌"

S3=$(curl_gw "/api/tenants/search?name=")
[ "$S3" = "200" ] && echo "  [3.3] GET /api/tenants/search → $S3              ✅" || echo "  [3.3] → $S3 ❌"

S4=$(curl_gw "/api/shops/by-tenant/$TID")
[ "$S4" = "404" ] && echo "  [3.4] GET /api/shops/by-tenant/{id} → $S4 (deleted) ✅" || echo "  [3.4] → $S4 (expected 404)"

echo ""
echo "=== [4] ORDER FLOW (data preserved) ==="
[ "$ORDERS" != "0" ] && echo "  [4.1] Orders preserved: $ORDERS               ✅" || echo "  [4.1] No orders"
[ "$OI" != "0" ] && echo "  [4.2] OrderItems preserved: $OI               ✅" || echo "  [4.2] No order items"

CAMP=$(curl_gw "/api/campaigns/by-tenant/$TID")
[ "$CAMP" = "200" ] && echo "  [4.3] Campaigns endpoint → $CAMP                 ✅" || echo "  [4.3] → $CAMP ❌"

CAMP_BODY=$(curl -s "http://vanan-gateway:5001/api/campaigns/by-tenant/$TID" 2>/dev/null)
if echo "$CAMP_BODY" | grep -q '"shopId"'; then
    echo "  [4.4] Campaign response contains shopId          ⚠️ (should be removed)"
else
    echo "  [4.4] Campaign response has NO shopId field      ✅ (clean)"
fi

echo ""
echo "=== [5] PAYMENT FLOW (AccountingEntry generation) ==="
PAID=$(psql_q "SELECT count(*) FROM \"Orders\" WHERE \"PaymentStatus\" = 'Paid';")
REV=$(psql_q "SELECT count(*) FROM \"AccountingEntries\" WHERE \"AccountCode\" = '511';")
VAT=$(psql_q "SELECT count(*) FROM \"AccountingEntries\" WHERE \"AccountCode\" = '3331';")
COGS=$(psql_q "SELECT count(*) FROM \"AccountingEntries\" WHERE \"AccountCode\" = '632';")
echo "  [5.1] Paid orders: $PAID"
echo "  [5.2] Revenue entries (511): $REV"
echo "  [5.3] VAT entries (3331): $VAT"
echo "  [5.4] COGS entries (632): $COGS"
[ "$AE" != "0" ] && echo "  [5.5] AccountingEntries preserved: $AE          ✅" || echo "  [5.5] No AccEntries"
[ "$JE" != "0" ] && echo "  [5.6] JournalEntries preserved: $JE             ✅" || echo "  [5.6] No JnlEntries"

echo ""
echo "=== [6] KITCHEN DISPLAY FLOW (ShopERP) ==="
KH=$(curl_nginx "/kitchen")
[ "$KH" = "302" ] && echo "  [6.1] /kitchen reachable → $KH                   ✅" || echo "  [6.1] → $KH ❌"

PENDING=$(psql_q "SELECT count(*) FROM \"Orders\" WHERE \"Status\" = 'pending';")
CONFIRMED=$(psql_q "SELECT count(*) FROM \"Orders\" WHERE \"Status\" = 'confirmed';")
PREPARING=$(psql_q "SELECT count(*) FROM \"Orders\" WHERE \"Status\" = 'preparing';")
READY=$(psql_q "SELECT count(*) FROM \"Orders\" WHERE \"Status\" = 'ready';")
COMPLETED=$(psql_q "SELECT count(*) FROM \"Orders\" WHERE \"Status\" = 'completed';")
CANCELLED=$(psql_q "SELECT count(*) FROM \"Orders\" WHERE \"Status\" = 'cancelled';")
echo "  [6.2] Order status: pending=$PENDING confirmed=$CONFIRMED preparing=$PREPARING ready=$READY completed=$COMPLETED cancelled=$CANCELLED"

echo ""
echo "=== [7] ACCOUNTING FLOW (immutability + pages) ==="
REV_ENTRIES=$(psql_q "SELECT count(*) FROM \"AccountingEntries\" WHERE \"ReversalEntryId\" IS NOT NULL;")
PERIODS=$(psql_q "SELECT count(DISTINCT \"PeriodYear\" || ''-'' || \"PeriodMonth\") FROM \"AccountingEntries\";")
echo "  [7.1] Reversal entries (immutability): $REV_ENTRIES"
echo "  [7.2] Distinct accounting periods: $PERIODS"

AH=$(curl_nginx "/accounting/history")
[ "$AH" = "302" ] && echo "  [7.3] /accounting/history → $AH                  ✅" || echo "  [7.3] → $AH ❌"

AD=$(curl_nginx "/accounting")
[ "$AD" = "302" ] && echo "  [7.4] /accounting dashboard → $AD                ✅" || echo "  [7.4] → $AD ❌"

echo ""
echo "=== [8] KHACHLINK CUSTOMER APP ==="
KLH=$(curl_kl "/")
[ "$KLH" = "200" ] && echo "  [8.1] KhachLink home → $KLH                       ✅" || echo "  [8.1] → $KLH ❌"

KLS=$(curl_kl "/scan")
[ "$KLS" = "200" ] && echo "  [8.2] KhachLink /scan → $KLS                      ✅" || echo "  [8.2] → $KLS ❌"

KLC=$(curl_kl "/campaign")
[ "$KLC" = "200" ] && echo "  [8.3] KhachLink /campaign → $KLC                  ✅" || echo "  [8.3] → $KLC ❌"

echo ""
echo "=== [9] SHOPERP ADMIN PAGES ==="
ORD=$(curl_nginx "/orders")
[ "$ORD" = "302" ] && echo "  [9.1] /orders → $ORD                              ✅" || echo "  [9.1] → $ORD ❌"

CAMP_ADM=$(curl_nginx "/admin/campaigns")
[ "$CAMP_ADM" = "302" ] && echo "  [9.2] /admin/campaigns → $CAMP_ADM                ✅" || echo "  [9.2] → $CAMP_ADM ❌"

SHOPS_ADM=$(curl_nginx "/admin/shops")
[ "$SHOPS_ADM" = "404" ] && echo "  [9.3] /admin/shops → $SHOPS_ADM (deleted)         ✅" || echo "  [9.3] → $SHOPS_ADM (expected 404)"

echo ""
echo "============================================================"
echo "VR TEST COMPLETE"
echo "============================================================"
