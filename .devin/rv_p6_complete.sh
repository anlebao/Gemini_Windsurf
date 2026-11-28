#!/bin/bash
# RV P7.4 — HOÀN THIỆN SEED: categories + offerings + rule (PG, docker exec -i) + re-set skills
APP="https://app2.khachvip.online"
GW="https://api2.khachvip.online"
TENANT_A="6aaf19e4-8b55-48cd-94d7-b61c96a2d66d"
TENANT_B="4345dff8-b4b7-411f-b8f1-faf36f242d70"
NOW="$(date -u '+%Y-%m-%d %H:%M:%S')"

OFF_A1="$(python3 -c 'import uuid; print(uuid.uuid4())')"
OFF_A2="$(python3 -c 'import uuid; print(uuid.uuid4())')"
CAT_A="$(python3 -c 'import uuid; print(uuid.uuid4())')"
OFF_B1="$(python3 -c 'import uuid; print(uuid.uuid4())')"
CAT_B="$(python3 -c 'import uuid; print(uuid.uuid4())')"

echo "=== PG seed (docker exec -i) ==="
docker exec -i vanan-postgres-1 sh -c "psql -U vanan_admin -d VanAnCoreHub" <<SQL
DELETE FROM "StaffServices";
INSERT INTO "ServiceCategories" ("Id","Name","DisplayOrder","IsActive","TenantId","CreatedAt","UpdatedAt","IsDeleted") VALUES
 ('$CAT_A','Massage',1,true,'$TENANT_A','$NOW','$NOW',false),
 ('$CAT_B','Cat toc',1,true,'$TENANT_B','$NOW','$NOW',false)
ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "AppointmentOfferings" ("Id","CategoryId","OfferingType","DisplayName","DurationMinutes","Price","IsActive","TenantId","CreatedAt","UpdatedAt","IsDeleted") VALUES
 ('$OFF_A1','$CAT_A',0,'Massage 60 phut',60,300000,true,'$TENANT_A','$NOW','$NOW',false),
 ('$OFF_A2','$CAT_A',0,'Massage 90 phut',90,450000,true,'$TENANT_A','$NOW','$NOW',false),
 ('$OFF_B1','$CAT_B',0,'Cat toc nam',30,150000,true,'$TENANT_B','$NOW','$NOW',false)
ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "CommissionRules" ("Id","OfferingId","SalesmanId","CommissionType","CommissionValue","QualificationStatus","IsActive","TenantId","CreatedAt","UpdatedAt","IsDeleted") VALUES
 ('$(python3 -c 'import uuid; print(uuid.uuid4())')',NULL,NULL,0,10,0,true,'$TENANT_A','$NOW','$NOW',false)
ON CONFLICT ("Id") DO NOTHING;
SELECT 'pg seed done' AS status;
SQL

echo "=== re-set skills qua API (JWT mới) ==="
rm -f /tmp/rv_sysadmin_cookies.txt
curl -s -o /dev/null -c /tmp/rv_sysadmin_cookies.txt -X POST "$APP/Login" \
  -d 'Username=sysadmin@vanan.vn&Password=2026@vanan&RememberMe=false' -k --max-redirs 0
JWT_A=$(curl -s -X POST "$APP/api/admin/impersonate/$TENANT_A" -H 'Content-Type: application/json' -b /tmp/rv_sysadmin_cookies.txt -k | python3 -c "import sys,json
try: print(json.load(sys.stdin).get('token',''))
except Exception: print('')")
JWT_B=$(curl -s -X POST "$APP/api/admin/impersonate/$TENANT_B" -H 'Content-Type: application/json' -b /tmp/rv_sysadmin_cookies.txt -k | python3 -c "import sys,json
try: print(json.load(sys.stdin).get('token','')) 
except Exception: print('')")
echo "JWT_A len=${#JWT_A} JWT_B len=${#JWT_B}"

# Staff ids đã tạo trước đó
STAFF_A1="4e2fb124-adc9-467f-a79f-db2ff87bc84e"
STAFF_A2="96236f9d-30b9-47fe-bb76-e40530585e9d"
STAFF_B1="2c47885c-0f86-4e55-b296-e0ea831978de"

curl -s -X POST "$GW/api/tenant/booking/staff/$STAFF_A1/services" -H "Authorization: Bearer $JWT_A" -H 'Content-Type: application/json' -d "{\"offeringIds\":[\"$OFF_A1\",\"$OFF_A2\"]}" | head -c 120; echo ""
curl -s -X POST "$GW/api/tenant/booking/staff/$STAFF_A2/services" -H "Authorization: Bearer $JWT_A" -H 'Content-Type: application/json' -d "{\"offeringIds\":[\"$OFF_A1\"]}" | head -c 120; echo ""
curl -s -X POST "$GW/api/tenant/booking/staff/$STAFF_B1/services" -H "Authorization: Bearer $JWT_B" -H 'Content-Type: application/json' -d "{\"offeringIds\":[\"$OFF_B1\"]}" | head -c 120; echo ""

echo "=== VERIFY end-to-end (public API) ==="
QR_A="8e6d3264-c316-49f3-9267-ccedaed33a73"
QR_B="d4eda276-73e3-4fda-95d9-7c94503c0442"
echo "--- QR A resolve ---"
docker exec vanan-gateway-1 sh -c "curl -s 'localhost:80/api/public/booking/qr/$QR_A?anonymousSessionId=rv-verify' | head -c 300"
echo ""
echo "--- catalog tenant A ---"
docker exec vanan-gateway-1 sh -c "curl -s 'localhost:80/api/public/booking/tenants/$TENANT_A/services' | head -c 400"
echo ""
echo "--- availability tenant A (offering A1, hôm nay) ---"
docker exec vanan-gateway-1 sh -c "curl -s 'localhost:80/api/public/booking/availability?tenantId=$TENANT_A&offeringId=$OFF_A1&date=$(date -u +%F)' | head -c 400"
echo ""
echo "=== FINAL SUMMARY (E2E env) ==="
echo "BOOKING_TEST_TENANT_ID=$TENANT_A"
echo "BOOKING_TEST_QR_TOKEN=$QR_A"
echo "BOOKING_TEST_QR_TOKEN_A=$QR_A"
echo "BOOKING_TEST_QR_TOKEN_B=$QR_B"
echo "BOOKING_TEST_OFFERING_ID=$OFF_A1"
echo "BOOKING_TEST_OFFERING_ID_A=$OFF_A1"
echo "BOOKING_TEST_OFFERING_ID_B=$OFF_B1"
echo "BOOKING_TEST_STAFF_ID=$STAFF_A1"
echo "BOOKING_TEST_DATE=$(date -u +%F)"
echo "BOOKING_TEST_SLOT=14:00"
echo "BOOKING_TEST_COMMISSION_RULE=true"
echo "BOOKING_TEST_DEPOSIT=true"
echo "BOOKING_TEST_TENANT_TOKEN=$JWT_A"
echo "BOOKING_TEST_TENANT_TOKEN_A=$JWT_A"
echo "BOOKING_TEST_TENANT_TOKEN_B=$JWT_B"
echo "BOOKING_TEST_SALESMAN_ID=12ada883-1cf3-424e-8c9b-93f5cad27d42"
echo DONE
