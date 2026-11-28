#!/bin/bash
# RV Batch 4 part 2 — re-run phases blocked by (a) missing PG customer sync,
# (b) wrong admin login endpoint. Reads /tmp/rvb4_ids.txt (written by part 1).
API="http://127.0.0.1"
TENANT_A="0dfab177-86ad-4513-bd69-ddf956c7b768"

psqlq() { sudo docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "$1"; }
psqlc() { sudo docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -c "$1"; }
tok()  { grep -oP '"customerToken":"\K[^"]+' "/tmp/rvb4_$1.json"; }
cid()  { grep -oP '"customerId":"\K[^"]+' "/tmp/rvb4_$1.json"; }
call() { curl -sk -w "|HTTP %{http_code}" -X "$1" "$API$2" -H "X-Customer-Token: $3" -H "Content-Type: application/json" ${4:+-d "$4"}; echo; }
adm()  { curl -sk -w "|HTTP %{http_code}" -X "$1" "$API$2" -H "Authorization: Bearer $JWT" -H "Content-Type: application/json" ${3:+-d "$3"}; echo; }

SHIP_TOK=$(tok shipper); OWN_A_TOK=$(tok ownerA); OWN_B_TOK=$(tok ownerB)
SHIPPER=$(cid shipper); OWNER_A=$(cid ownerA); OWNER_B=$(cid ownerB)
read ORD_MKT ORD_RES ORD_EXT1 ORD_EXT2 ORD_ADV DEP_ID _S _A _B TENANT_B < /tmp/rvb4_ids.txt
echo "ids: MKT=$ORD_MKT RES=$ORD_RES EXT1=$ORD_EXT1 EXT2=$ORD_EXT2 ADV=$ORD_ADV TENANT_B=$TENANT_B"

echo "=== wait for customer sync to PG ==="
for i in $(seq 1 20); do
  N=$(psqlq "SELECT COUNT(*) FROM \"Customers\" WHERE \"Id\" IN ('$SHIPPER','$OWNER_A','$OWNER_B');")
  echo "sync poll $i: $N/3"; [ "$N" = "3" ] && break; sleep 5
done

echo "=== bind owners ==="
psqlq "UPDATE \"Customers\" SET \"TenantId\"='$TENANT_B', \"UpdatedAt\"=now() WHERE \"Id\"='$OWNER_B';"
psqlq "UPDATE \"Tenants\" SET \"OwnerCustomerId\"='$OWNER_B', \"UpdatedAt\"=now() WHERE \"Id\"='$TENANT_B';"
psqlq "SELECT \"Id\",\"TenantId\",\"PhoneNumber\" IS NOT NULL AS has_phone FROM \"Customers\" WHERE \"Id\" IN ('$SHIPPER','$OWNER_A','$OWNER_B');"
psqlq "SELECT \"OwnerCustomerId\",\"Settings_ContactPhone\" FROM \"Tenants\" WHERE \"Id\" IN ('$TENANT_A','$TENANT_B');"

echo; echo "########## PHASE 3: ADVANCE FLOW + S1/S11 OWNER GATE ##########"
echo "=== T11 pending-advances as SHIPPER/non-owner (expect 403 — S1) ==="
call GET /api/community/wallet/pending-advances "$SHIP_TOK"
echo "=== T12 pending-advances as OWNER_A — lazy bind via ContactPhone (expect 200 + row) ==="
call GET /api/community/wallet/pending-advances "$OWN_A_TOK"
echo "=== T13 lazy bind persisted? (expect OwnerCustomerId=$OWNER_A) ==="
psqlq "SELECT \"OwnerCustomerId\" FROM \"Tenants\" WHERE \"Id\"='$TENANT_A';"
ADV_TX=$(psqlq "SELECT \"Id\" FROM \"WalletTransactions\" WHERE \"RelatedOrderId\"='$ORD_ADV' AND \"Type\"=2 LIMIT 1;")
echo "ADV_TX=$ADV_TX"
echo "=== T14 confirm-advance-received as OWNER_B (owner of tenant B, tx of A — expect 403 S11) ==="
call POST /api/community/wallet/confirm-advance-received "$OWN_B_TOK" "{\"advanceTransactionId\":\"$ADV_TX\"}"
echo "=== T15 confirm-advance-received as OWNER_A (expect 200) ==="
call POST /api/community/wallet/confirm-advance-received "$OWN_A_TOK" "{\"advanceTransactionId\":\"$ADV_TX\"}"
echo "=== T16 dup confirm (expect 409) ==="
call POST /api/community/wallet/confirm-advance-received "$OWN_A_TOK" "{\"advanceTransactionId\":\"$ADV_TX\"}"
echo "=== T17 advance legs ==="
psqlc "SELECT \"OwnerId\",\"Type\",\"Amount\",\"BalanceAfter\",\"RelatedTransactionId\" FROM \"WalletTransactions\" WHERE \"RelatedOrderId\"='$ORD_ADV' OR \"Id\"='$ADV_TX' ORDER BY \"CreatedAt\";"

echo; echo "########## PHASE 4: EXTERNAL PAYMENT (S9) ##########"
echo "=== T18 sysadmin JWT via /api/platform/login ==="
JWT=$(curl -sk -X POST "$API/api/platform/login" -H "Content-Type: application/json" -d '{"username":"sysadmin@vanan.vn","password":"2026@vanan"}' | grep -oP '"token":"\K[^"]+')
echo "JWT len=${#JWT}"
if [ -z "$JWT" ]; then
  echo "retry with email field:"
  JWT=$(curl -sk -X POST "$API/api/platform/login" -H "Content-Type: application/json" -d '{"username":"admin@vanan.vn","password":"2026@vanan"}' | grep -oP '"token":"\K[^"]+')
  echo "JWT len=${#JWT} (admin@vanan.vn)"
fi
echo "=== T19 confirm-external ORD_EXT1 ref=VQR-RVB4-001 (expect 200) ==="
adm POST /api/admin/commerce-mode/confirm-external-payment "{\"orderId\":\"$ORD_EXT1\",\"amount\":85000,\"paymentRef\":\"VQR-RVB4-001\"}"
echo "=== T20 legs EXT1 — expect ExternalPayment +85000 Platform · Settlement +40000 tenant · DeliveryFee +15000 shipper · PlatformFee +15000 · CommunityFund +3000 ==="
psqlc "SELECT \"OwnerId\",\"Type\",\"Amount\" FROM \"WalletTransactions\" WHERE \"RelatedOrderId\"='$ORD_EXT1' ORDER BY \"CreatedAt\";"
echo "=== T21 order Paid/External ref ==="
psqlc "SELECT \"PaymentStatus\",\"PaymentMethod\",\"VietQR_TransactionId\" FROM \"Orders\" WHERE \"Id\"='$ORD_EXT1';"
echo "=== T22 dup same order (expect 409 already-paid) ==="
adm POST /api/admin/commerce-mode/confirm-external-payment "{\"orderId\":\"$ORD_EXT1\",\"amount\":85000,\"paymentRef\":\"VQR-RVB4-001\"}"
echo "=== T23 SAME paymentRef DIFFERENT order ORD_EXT2 (expect 409 — S9 replay guard) ==="
adm POST /api/admin/commerce-mode/confirm-external-payment "{\"orderId\":\"$ORD_EXT2\",\"amount\":85000,\"paymentRef\":\"VQR-RVB4-001\"}"
echo "=== T24 wrong amount ORD_EXT2 (expect 409) ==="
adm POST /api/admin/commerce-mode/confirm-external-payment "{\"orderId\":\"$ORD_EXT2\",\"amount\":1,\"paymentRef\":\"VQR-RVB4-002\"}"
echo "=== T25 marketplace order external (expect 409 not-reseller) ==="
adm POST /api/admin/commerce-mode/confirm-external-payment "{\"orderId\":\"$ORD_MKT\",\"amount\":65000,\"paymentRef\":\"VQR-RVB4-003\"}"
echo "=== T26 valid external ORD_EXT2 ref=VQR-RVB4-002 (expect 200) ==="
adm POST /api/admin/commerce-mode/confirm-external-payment "{\"orderId\":\"$ORD_EXT2\",\"amount\":85000,\"paymentRef\":\"VQR-RVB4-002\"}"
psqlc "SELECT \"PaymentStatus\",\"VietQR_TransactionId\" FROM \"Orders\" WHERE \"Id\"='$ORD_EXT2';"

echo; echo "########## PHASE 6b: WITHDRAWAL lifecycle ##########"
WID=$(psqlq "SELECT \"Id\" FROM \"WithdrawalRequests\" WHERE \"OwnerId\"='$SHIPPER' ORDER BY \"CreatedAt\" DESC LIMIT 1;")
echo "WID=$WID (pending from part 1)"
echo "=== T34 approve + pay ==="
adm POST "/api/admin/withdrawals/$WID/approve" ""
adm POST "/api/admin/withdrawals/$WID/pay" "{\"bankReference\":\"RVB4-BANK-001\"}"
echo "=== T35 double-pay (expect 409) ==="
adm POST "/api/admin/withdrawals/$WID/pay" "{\"bankReference\":\"RVB4-BANK-002\"}"
echo "=== T36 withdrawal request row (expect Status=3 Paid + bankRef + tx) ==="
psqlc "SELECT \"Status\",\"BankReference\",\"WalletTransactionId\" IS NOT NULL AS has_tx FROM \"WithdrawalRequests\" WHERE \"Id\"='$WID';"
psqlc "SELECT \"Type\",\"Amount\",\"BalanceAfter\" FROM \"WalletTransactions\" WHERE \"OwnerId\"='$SHIPPER' AND \"Type\"=4 ORDER BY \"CreatedAt\";"

echo; echo "########## PHASE 7: ADMIN SETTLEMENTS (S2) ##########"
echo "=== T37 GET /api/admin/settlements?tenantId=A (expect 200) ==="
adm GET "/api/admin/settlements?tenantId=$TENANT_A&page=1&pageSize=5"
echo "=== T38 unauthenticated (expect 401) ==="
curl -sk -o /dev/null -w "HTTP %{http_code}" "$API/api/admin/settlements"; echo

echo; echo "########## PHASE 8b: AUDIT recount ##########"
sleep 5
psqlq "SELECT COUNT(*) FROM \"AuditLogs\" WHERE \"EntityType\"=14 AND \"CreatedAt\" > now() - interval '1 hour';"
echo "=== DONE part 2 ==="
