#!/bin/bash
# RV P7.4 — SEED DATA booking-enabled tenant (Test HKD = A, Test Minimal = B)
# Chạy trên gateway VPS. In SUMMARY dùng cho E2E env. KHÔNG set -e — mỗi bước tự kiểm tra.
APP="https://app2.khachvip.online"
# Nginx host 404 với Host=localhost → dùng public API domain (api2 → gateway vhost).
GW="https://api2.khachvip.online"
TENANT_A="6aaf19e4-8b55-48cd-94d7-b61c96a2d66d"   # Test HKD
TENANT_B="4345dff8-b4b7-411f-b8f1-faf36f242d70"   # Test Minimal
DATE="$(date -u +%F)"

# ── helpers ──────────────────────────────────────────────────────────────────
# LƯU Ý: impersonate trả cookie mới (session impersonated) → LUÔN dùng -b jar
# SystemAdmin (read-only, KHÔNG -c) để giữ quyền SystemAdmin cho cả 2 lần gọi.
get_token() { # $1 = tenant id
  curl -s -X POST "$APP/api/admin/impersonate/$1" -H 'Content-Type: application/json' \
    -b /tmp/rv_sysadmin_cookies.txt -k \
    | python3 -c "import sys,json
try:
    print(json.load(sys.stdin).get('token',''))
except Exception:
    print('')"
}
api() { # $1 = method, $2 = path, $3 = bearer, $4 = body (optional)
  if [ -n "$4" ]; then
    curl -s -X "$1" "$GW$2" -H "Authorization: Bearer $3" -H 'Content-Type: application/json' -d "$4"
  else
    curl -s -X "$1" "$GW$2" -H "Authorization: Bearer $3" -H 'Content-Type: application/json'
  fi
}
jget() { python3 -c "import sys,json
try:
    d=json.load(sys.stdin)
    print(d.get('$1',''))
except Exception:
    print('')" 2>/dev/null; }

echo "=== Step 1: Login SystemAdmin ==="
rm -f /tmp/rv_sysadmin_cookies.txt
curl -s -o /dev/null -c /tmp/rv_sysadmin_cookies.txt -X POST "$APP/Login" \
  -d 'Username=sysadmin@vanan.vn&Password=2026@vanan&RememberMe=false' -k --max-redirs 0
echo "cookie jar: $(wc -l < /tmp/rv_sysadmin_cookies.txt) lines"

echo "=== Step 2: Impersonate tenant A + B ==="
JWT_A=$(get_token "$TENANT_A")
JWT_B=$(get_token "$TENANT_B")
echo "JWT_A len=${#JWT_A} | JWT_B len=${#JWT_B}"
if [ ${#JWT_A} -lt 50 ] || [ ${#JWT_B} -lt 50 ]; then echo "FAIL: token empty — dừng"; exit 1; fi

echo "=== Step 3: Seed PG — category + offerings + salesman + rule (A); offering (B) ==="
SALESMAN_A="$(python3 -c 'import uuid; print(uuid.uuid4())')"
OFF_A1="$(python3 -c 'import uuid; print(uuid.uuid4())')"
OFF_A2="$(python3 -c 'import uuid; print(uuid.uuid4())')"
CAT_A="$(python3 -c 'import uuid; print(uuid.uuid4())')"
OFF_B1="$(python3 -c 'import uuid; print(uuid.uuid4())')"
CAT_B="$(python3 -c 'import uuid; print(uuid.uuid4())')"
NOW="$(date -u '+%Y-%m-%d %H:%M:%S')"

docker exec vanan-postgres-1 sh -c "psql -U vanan_admin -d VanAnCoreHub" <<SQL
INSERT INTO "ServiceCategories" ("Id","Name","DisplayOrder","IsActive","TenantId","CreatedAt","UpdatedAt","IsDeleted") VALUES
 ('$CAT_A','Massage',1,true,'$TENANT_A','$NOW','$NOW',false),
 ('$CAT_B','Cắt tóc',1,true,'$TENANT_B','$NOW','$NOW',false)
ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "AppointmentOfferings" ("Id","CategoryId","OfferingType","DisplayName","DurationMinutes","Price","IsActive","TenantId","CreatedAt","UpdatedAt","IsDeleted") VALUES
 ('$OFF_A1','$CAT_A',0,'Massage 60 phút',60,300000,true,'$TENANT_A','$NOW','$NOW',false),
 ('$OFF_A2','$CAT_A',0,'Massage 90 phút',90,450000,true,'$TENANT_A','$NOW','$NOW',false),
 ('$OFF_B1','$CAT_B',0,'Cắt tóc nam',30,150000,true,'$TENANT_B','$NOW','$NOW',false)
ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "CommissionRules" ("Id","OfferingId","SalesmanId","CommissionType","CommissionValue","QualificationStatus","IsActive","TenantId","CreatedAt","UpdatedAt","IsDeleted") VALUES
 ('$(python3 -c 'import uuid; print(uuid.uuid4())')',NULL,NULL,0,10,0,true,'$TENANT_A','$NOW','$NOW',false)
ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "Customers" ("Id","CustomerId","FullName","PhoneNumber","LoyaltyPoints","CustomerTier","IsActive","TenantId","CreatedAt","UpdatedAt","IsDeleted") VALUES
 ('$SALESMAN_A','$SALESMAN_A','RV Salesman A','0900000111',0,'Bronze',true,'$TENANT_A','$NOW','$NOW',false)
ON CONFLICT ("Id") DO NOTHING;
INSERT INTO "CommunityRoles" ("Id","CustomerId","RoleType","ActivatedAt","ActivatedBy","IsActive","SalesmanCode","TenantId","CreatedAt","UpdatedAt","IsDeleted") VALUES
 ('$(python3 -c 'import uuid; print(uuid.uuid4())')','$SALESMAN_A',2,'$NOW','00000000-0000-0000-0000-000000000001',true,'RVA000','$TENANT_A','$NOW','$NOW',false)
ON CONFLICT ("Id") DO NOTHING;
SELECT 'pg seed done' AS status;
SQL

echo "=== Step 4: Config (A: enabled + deposit Fixed 100k; B: enabled) ==="
api PUT /api/tenant/booking/config "$JWT_A" '{"isEnabled":true,"depositPolicy":"Fixed","depositFixedAmount":100000,"depositPercentage":null,"cancelReschedulePolicy":"Hủy trước 2 giờ","einvoiceMode":"security_deposit"}' | head -c 250; echo ""
api PUT /api/tenant/booking/config "$JWT_B" '{"isEnabled":true,"depositPolicy":"None","depositFixedAmount":null,"depositPercentage":null,"cancelReschedulePolicy":"Hủy trước 1 giờ","einvoiceMode":"security_deposit"}' | head -c 250; echo ""

echo "=== Step 5: Staff A1/A2 + skills + schedules ==="
STAFF_A1=$(api POST /api/tenant/booking/staff "$JWT_A" '{"displayName":"KTV Lan","role":"Technician","staffUserId":null}' | jget id)
STAFF_A2=$(api POST /api/tenant/booking/staff "$JWT_A" '{"displayName":"KTV Hoa","role":"Technician","staffUserId":null}' | jget id)
echo "staff A1=$STAFF_A1 A2=$STAFF_A2"
api POST /api/tenant/booking/staff/$STAFF_A1/services "$JWT_A" "{\"offeringIds\":[\"$OFF_A1\",\"$OFF_A2\"]}" | head -c 120; echo ""
api POST /api/tenant/booking/staff/$STAFF_A2/services "$JWT_A" "{\"offeringIds\":[\"$OFF_A1\"]}" | head -c 120; echo ""
for wd in 0 1 2 3 4 5 6; do
  api POST /api/tenant/booking/staff/$STAFF_A1/schedules "$JWT_A" "{\"weekday\":$wd,\"startTime\":\"09:00\",\"endTime\":\"18:00\",\"breakStart\":\"12:00\",\"breakEnd\":\"13:00\"}" -o /dev/null
  api POST /api/tenant/booking/staff/$STAFF_A2/schedules "$JWT_A" "{\"weekday\":$wd,\"startTime\":\"09:00\",\"endTime\":\"18:00\",\"breakStart\":\"12:00\",\"breakEnd\":\"13:00\"}" -o /dev/null
done
echo "schedules A done"

echo "=== Step 6: Staff B + skills + schedules ==="
STAFF_B1=$(api POST /api/tenant/booking/staff "$JWT_B" '{"displayName":"Thợ Tuấn","role":"Barber","staffUserId":null}' | jget id)
echo "staff B1=$STAFF_B1"
api POST /api/tenant/booking/staff/$STAFF_B1/services "$JWT_B" "{\"offeringIds\":[\"$OFF_B1\"]}" | head -c 120; echo ""
for wd in 0 1 2 3 4 5 6; do
  api POST /api/tenant/booking/staff/$STAFF_B1/schedules "$JWT_B" "{\"weekday\":$wd,\"startTime\":\"08:00\",\"endTime\":\"20:00\",\"breakStart\":null,\"breakEnd\":null}" -o /dev/null
done
echo "schedules B done"

echo "=== Step 7: QR channels ==="
QR_A="$(python3 -c 'import uuid; print(uuid.uuid4())')"
QR_B="$(python3 -c 'import uuid; print(uuid.uuid4())')"
echo "QR_A (salesman):"
api POST /api/tenant/booking/qr-channels "$JWT_A" "{\"qrToken\":\"$QR_A\",\"salesmanId\":\"$SALESMAN_A\",\"campaignId\":null,\"attributionExpiryAt\":null}" | head -c 250; echo ""
echo "QR_B (no salesman):"
api POST /api/tenant/booking/qr-channels "$JWT_B" "{\"qrToken\":\"$QR_B\",\"salesmanId\":null,\"campaignId\":null,\"attributionExpiryAt\":null}" | head -c 250; echo ""

echo ""
echo "=== SUMMARY (dùng cho E2E env) ==="
echo "BOOKING_TEST_TENANT_ID=$TENANT_A"
echo "BOOKING_TEST_QR_TOKEN=$QR_A"
echo "BOOKING_TEST_QR_TOKEN_A=$QR_A"
echo "BOOKING_TEST_QR_TOKEN_B=$QR_B"
echo "BOOKING_TEST_OFFERING_ID=$OFF_A1"
echo "BOOKING_TEST_OFFERING_ID_A=$OFF_A1"
echo "BOOKING_TEST_OFFERING_ID_B=$OFF_B1"
echo "BOOKING_TEST_STAFF_ID=$STAFF_A1"
echo "BOOKING_TEST_DATE=$DATE"
echo "BOOKING_TEST_SLOT=14:00"
echo "BOOKING_TEST_COMMISSION_RULE=true"
echo "BOOKING_TEST_DEPOSIT=true"
echo "BOOKING_TEST_TENANT_TOKEN=$JWT_A"
echo "BOOKING_TEST_TENANT_TOKEN_A=$JWT_A"
echo "BOOKING_TEST_TENANT_TOKEN_B=$JWT_B"
echo "BOOKING_TEST_SALESMAN_ID=$SALESMAN_A"
echo DONE
