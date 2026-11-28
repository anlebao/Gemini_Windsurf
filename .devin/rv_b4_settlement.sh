#!/bin/bash
# RV Batch 4 (TC-10) — full settlement flow on vanan-gateway.
# Requires: /tmp/rvb4_shipper.json /tmp/rvb4_ownerA.json /tmp/rvb4_ownerB.json (from shop-a window script).
API="http://127.0.0.1"
TENANT_A="0dfab177-86ad-4513-bd69-ddf956c7b768"
PLATFORM="00000000-0000-0000-0000-000000000001"
FUND="00000000-0000-0000-0000-000000000002"

psqlq() { sudo docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "$1"; }
psqlc() { sudo docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -c "$1"; }
tok()  { grep -oP '"customerToken":"\K[^"]+' "/tmp/rvb4_$1.json"; }
cid()  { grep -oP '"customerId":"\K[^"]+' "/tmp/rvb4_$1.json"; }
call() { curl -sk -w "|HTTP %{http_code}" -X "$1" "$API$2" -H "X-Customer-Token: $3" -H "Content-Type: application/json" ${4:+-d "$4"}; echo; }
adm()  { curl -sk -w "|HTTP %{http_code}" -X "$1" "$API$2" -H "Authorization: Bearer $JWT" -H "Content-Type: application/json" ${3:+-d "$3"}; echo; }

SHIP_TOK=$(tok shipper); OWN_A_TOK=$(tok ownerA); OWN_B_TOK=$(tok ownerB)
SHIPPER=$(cid shipper); OWNER_A=$(cid ownerA); OWNER_B=$(cid ownerB)
echo "SHIPPER=$SHIPPER"; echo "OWNER_A=$OWNER_A"; echo "OWNER_B=$OWNER_B"

echo "=== L2a: migration — Tenants.OwnerCustomerId exists ==="
psqlq "SELECT COUNT(*) FROM information_schema.columns WHERE table_name='Tenants' AND column_name='OwnerCustomerId';"

echo "=== L2b: DLL markers ==="
sudo docker exec vanan-gateway-1 sh -c "grep -c 'OwnerCustomerId' /app/VanAn.Gateway.dll /app/VanAn.CoreHub.dll 2>/dev/null" || \
sudo docker exec $(sudo docker ps --format '{{.Names}}' | grep -i gateway | head -1) sh -c "grep -c 'OwnerCustomerId' /app/VanAn.Gateway.dll /app/VanAn.CoreHub.dll"

echo "=== wait for customer sync to PG ==="
for i in 1 2 3 4 5 6 7 8 9 10; do
  N=$(psqlq "SELECT COUNT(*) FROM \"Customers\" WHERE \"Id\" IN ('$SHIPPER','$OWNER_A','$OWNER_B');")
  echo "sync poll $i: $N/3"; [ "$N" = "3" ] && break; sleep 3
done

echo "=== pick tenant B + bind owners ==="
TENANT_B=$(psqlq "SELECT \"Id\" FROM \"Tenants\" WHERE \"Id\" != '$TENANT_A' AND NOT \"IsDeleted\" ORDER BY \"CreatedAt\" LIMIT 1;")
echo "TENANT_B=$TENANT_B"
# Owner A: lazy-bind path — set declared ContactPhone to owner's phone (S1 lazy bind).
# Snapshot the original value first so cleanup can restore it.
psqlq "SELECT COALESCE(\"Settings_ContactPhone\",'') FROM \"Tenants\" WHERE \"Id\"='$TENANT_A';" > /tmp/rvb4_orig_contactphone.txt
psqlq "UPDATE \"Tenants\" SET \"Settings_ContactPhone\"='0900000012', \"UpdatedAt\"=now() WHERE \"Id\"='$TENANT_A';"
# Owner B: explicit bind + re-pin customer to tenant B (cross-tenant S11 test)
psqlq "UPDATE \"Customers\" SET \"TenantId\"='$TENANT_B', \"UpdatedAt\"=now() WHERE \"Id\"='$OWNER_B';"
psqlq "UPDATE \"Tenants\" SET \"OwnerCustomerId\"='$OWNER_B', \"UpdatedAt\"=now() WHERE \"Id\"='$TENANT_B';"
echo "bound: tenantB.OwnerCustomerId=$OWNER_B"

ORD_MKT=$(cat /proc/sys/kernel/random/uuid)
ORD_RES=$(cat /proc/sys/kernel/random/uuid)
ORD_EXT1=$(cat /proc/sys/kernel/random/uuid)
ORD_EXT2=$(cat /proc/sys/kernel/random/uuid)
ORD_ADV=$(cat /proc/sys/kernel/random/uuid)
DEP_ID=$(cat /proc/sys/kernel/random/uuid)
echo "$ORD_MKT $ORD_RES $ORD_EXT1 $ORD_EXT2 $ORD_ADV $DEP_ID $SHIPPER $OWNER_A $OWNER_B $TENANT_B" > /tmp/rvb4_ids.txt
echo "ORD_MKT=$ORD_MKT ORD_RES=$ORD_RES"; echo "ORD_EXT1=$ORD_EXT1 ORD_EXT2=$ORD_EXT2 ORD_ADV=$ORD_ADV"

echo "=== FIXTURES: orders + delivery tasks ==="
sudo docker exec -i vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub <<SQL
INSERT INTO "Orders" ("Id","OrderType","Status","KitchenStatus","SubTotal","TotalVatAmount","ShippingFee","DiscountAmount","TotalAmount","OrderDate","IsSyncedToCoreHub","TenantId","CreatedAt","UpdatedAt","IsDeleted","CommerceMode","CodAmount","PaymentStatus","CustomerInfo_FullName","CustomerInfo_PhoneNumber","TrackingCode")
VALUES ('$ORD_MKT','DELIVERY','delivering',0,65000,0,0,0,65000,now(),true,'$TENANT_A',now(),now(),false,0,65000,'Pending','RV B4 MKT','0900000099','RVB4MKT');
INSERT INTO "Orders" ("Id","OrderType","Status","KitchenStatus","SubTotal","TotalVatAmount","ShippingFee","DiscountAmount","TotalAmount","OrderDate","IsSyncedToCoreHub","TenantId","CreatedAt","UpdatedAt","IsDeleted","CommerceMode","SellPrice","DeliveryFee","CostPrice","PlatformMargin","PlatformFeeRate","CommunityFundRate","PaymentStatus","CustomerInfo_FullName","CustomerInfo_PhoneNumber","TrackingCode")
VALUES ('$ORD_RES','DELIVERY','delivering',0,70000,0,15000,0,85000,now(),true,'$TENANT_A',now(),now(),false,1,70000,15000,40000,30000,0.5,0.1,'Pending','RV B4 RES','0900000099','RVB4RES');
INSERT INTO "Orders" ("Id","OrderType","Status","KitchenStatus","SubTotal","TotalVatAmount","ShippingFee","DiscountAmount","TotalAmount","OrderDate","IsSyncedToCoreHub","TenantId","CreatedAt","UpdatedAt","IsDeleted","CommerceMode","SellPrice","DeliveryFee","CostPrice","PlatformMargin","PlatformFeeRate","CommunityFundRate","PaymentStatus","CustomerInfo_FullName","CustomerInfo_PhoneNumber","TrackingCode")
VALUES ('$ORD_EXT1','DELIVERY','delivered',0,70000,0,15000,0,85000,now(),true,'$TENANT_A',now(),now(),false,1,70000,15000,40000,30000,0.5,0.1,'Pending','RV B4 EXT1','0900000099','RVB4EXT1');
INSERT INTO "Orders" ("Id","OrderType","Status","KitchenStatus","SubTotal","TotalVatAmount","ShippingFee","DiscountAmount","TotalAmount","OrderDate","IsSyncedToCoreHub","TenantId","CreatedAt","UpdatedAt","IsDeleted","CommerceMode","SellPrice","DeliveryFee","CostPrice","PlatformMargin","PlatformFeeRate","CommunityFundRate","PaymentStatus","CustomerInfo_FullName","CustomerInfo_PhoneNumber","TrackingCode")
VALUES ('$ORD_EXT2','DELIVERY','delivered',0,70000,0,15000,0,85000,now(),true,'$TENANT_A',now(),now(),false,1,70000,15000,40000,30000,0.5,0.1,'Pending','RV B4 EXT2','0900000099','RVB4EXT2');
INSERT INTO "Orders" ("Id","OrderType","Status","KitchenStatus","SubTotal","TotalVatAmount","ShippingFee","DiscountAmount","TotalAmount","OrderDate","IsSyncedToCoreHub","TenantId","CreatedAt","UpdatedAt","IsDeleted","CommerceMode","CodAmount","PaymentStatus","CustomerInfo_FullName","CustomerInfo_PhoneNumber","TrackingCode")
VALUES ('$ORD_ADV','DELIVERY','delivering',0,50000,0,0,0,50000,now(),true,'$TENANT_A',now(),now(),false,0,50000,'Pending','RV B4 ADV','0900000099','RVB4ADV');
INSERT INTO "DeliveryTasks" ("Id","OrderId","ShipperId","Status","AssignedAt","ShopLat","ShopLng","TenantId","CreatedAt","UpdatedAt","IsDeleted")
VALUES (gen_random_uuid(),'$ORD_MKT','$SHIPPER',3,now(),0,0,'$TENANT_A',now(),now(),false),
       (gen_random_uuid(),'$ORD_RES','$SHIPPER',3,now(),0,0,'$TENANT_A',now(),now(),false),
       (gen_random_uuid(),'$ORD_EXT1','$SHIPPER',4,now(),0,0,'$TENANT_A',now(),now(),false),
       (gen_random_uuid(),'$ORD_EXT2','$SHIPPER',4,now(),0,0,'$TENANT_A',now(),now(),false),
       (gen_random_uuid(),'$ORD_ADV','$SHIPPER',3,now(),0,0,'$TENANT_A',now(),now(),false);
SQL

echo; echo "########## PHASE 1: MARKETPLACE COD ##########"
echo "=== T1 confirm-cod wrong amount (expect 409) ==="
call POST /api/community/wallet/confirm-cod "$SHIP_TOK" "{\"orderId\":\"$ORD_MKT\",\"amount\":1}"
echo "=== T2 confirm-cod 65000 (expect 200) ==="
call POST /api/community/wallet/confirm-cod "$SHIP_TOK" "{\"orderId\":\"$ORD_MKT\",\"amount\":65000}"
echo "=== T3 dup confirm-cod (expect 409) ==="
call POST /api/community/wallet/confirm-cod "$SHIP_TOK" "{\"orderId\":\"$ORD_MKT\",\"amount\":65000}"
echo "=== T4 wallet legs MKT (expect CODCollection +65000 shipper, Settlement +65000 tenant) ==="
psqlc "SELECT \"OwnerId\",\"Type\",\"Amount\",\"BalanceAfter\" FROM \"WalletTransactions\" WHERE \"RelatedOrderId\"='$ORD_MKT' ORDER BY \"CreatedAt\";"
echo "=== T5 order Paid + COD ref ==="
psqlc "SELECT \"PaymentStatus\",\"PaymentMethod\",\"VietQR_TransactionId\" IS NOT NULL AS has_ref,\"CodCollectedAt\" IS NOT NULL AS cod_at FROM \"Orders\" WHERE \"Id\"='$ORD_MKT';"

echo; echo "########## PHASE 2: RESELLER COD SPLIT (S3) ##########"
echo "=== T6 confirm-cod RES 85000 (expect 200) ==="
call POST /api/community/wallet/confirm-cod "$SHIP_TOK" "{\"orderId\":\"$ORD_RES\",\"amount\":85000}"
echo "=== T7 legs RES — expect: COD +85000 shipper · Settlement +40000 tenant · DeliveryFee +15000 shipper · PlatformFee +15000 · CommunityFund +3000 (margin 30000, rates 0.5/0.1; residual 12000 stays PlatformWallet) ==="
psqlc "SELECT \"OwnerId\",\"Type\",\"Amount\",\"BalanceAfter\" FROM \"WalletTransactions\" WHERE \"RelatedOrderId\"='$ORD_RES' ORDER BY \"CreatedAt\";"
echo "=== T8 dup (expect 409) ==="
call POST /api/community/wallet/confirm-cod "$SHIP_TOK" "{\"orderId\":\"$ORD_RES\",\"amount\":85000}"

echo; echo "########## PHASE 3: ADVANCE FLOW + S1/S11 OWNER GATE ##########"
echo "=== T9 shipper confirm-advance 50000 (expect 200) ==="
ADVRESP=$(call POST /api/community/wallet/confirm-advance "$SHIP_TOK" "{\"orderId\":\"$ORD_ADV\",\"amount\":50000}")
echo "$ADVRESP"
ADV_TX=$(echo "$ADVRESP" | grep -oP '"transactionId":"\K[^"]+')
echo "ADV_TX=$ADV_TX"
echo "=== T10 dup advance (expect 409) ==="
call POST /api/community/wallet/confirm-advance "$SHIP_TOK" "{\"orderId\":\"$ORD_ADV\",\"amount\":50000}"
echo "=== T11 pending-advances as SHIPPER/non-owner (expect 403 — S1) ==="
call GET /api/community/wallet/pending-advances "$SHIP_TOK"
echo "=== T12 pending-advances as OWNER_A — lazy bind via ContactPhone (expect 200 + row) ==="
call GET /api/community/wallet/pending-advances "$OWN_A_TOK"
echo "=== T13 lazy bind persisted? (expect OwnerCustomerId=OWNER_A) ==="
psqlq "SELECT \"OwnerCustomerId\" FROM \"Tenants\" WHERE \"Id\"='$TENANT_A';"
echo "=== T14 confirm-advance-received as OWNER_B (owner of tenant B, tx of A — expect 403 S11) ==="
call POST /api/community/wallet/confirm-advance-received "$OWN_B_TOK" "{\"advanceTransactionId\":\"$ADV_TX\"}"
echo "=== T15 confirm-advance-received as OWNER_A (expect 200) ==="
call POST /api/community/wallet/confirm-advance-received "$OWN_A_TOK" "{\"advanceTransactionId\":\"$ADV_TX\"}"
echo "=== T16 dup confirm (expect 409) ==="
call POST /api/community/wallet/confirm-advance-received "$OWN_A_TOK" "{\"advanceTransactionId\":\"$ADV_TX\"}"
echo "=== T17 advance legs (AdvancePayment -50000 shipper · Settlement +50000 tenant) ==="
psqlc "SELECT \"OwnerId\",\"Type\",\"Amount\",\"BalanceAfter\",\"RelatedTransactionId\" FROM \"WalletTransactions\" WHERE \"RelatedOrderId\"='$ORD_ADV' ORDER BY \"CreatedAt\";"

echo; echo "########## PHASE 4: EXTERNAL PAYMENT (S9) ##########"
echo "=== T18 sysadmin JWT via /api/platform/login ==="
JWT=$(curl -sk -X POST "$API/api/platform/login" -H "Content-Type: application/json" -d '{"username":"sysadmin@vanan.vn","password":"2026@vanan"}' | grep -oP '"token":"\K[^"]+')
echo "JWT len=${#JWT}"
echo "=== T19 confirm-external ORD_EXT1 ref=VQR-RVB4-001 (expect 200) ==="
adm POST /api/admin/commerce-mode/confirm-external-payment "{\"orderId\":\"$ORD_EXT1\",\"amount\":85000,\"paymentRef\":\"VQR-RVB4-001\"}"
echo "=== T20 legs EXT1 — expect ExternalPayment +85000 PlatformWallet · Settlement +40000 tenant · DeliveryFee +15000 shipper · PlatformFee +15000 · CommunityFund +3000 ==="
psqlc "SELECT \"OwnerId\",\"Type\",\"Amount\" FROM \"WalletTransactions\" WHERE \"RelatedOrderId\"='$ORD_EXT1' ORDER BY \"CreatedAt\";"
echo "=== T21 order Paid/External ref ==="
psqlc "SELECT \"PaymentStatus\",\"PaymentMethod\",\"VietQR_TransactionId\" FROM \"Orders\" WHERE \"Id\"='$ORD_EXT1';"
echo "=== T22 dup same order (expect 409 already-paid) ==="
adm POST /api/admin/commerce-mode/confirm-external-payment "{\"orderId\":\"$ORD_EXT1\",\"amount\":85000,\"paymentRef\":\"VQR-RVB4-001\"}"
echo "=== T23 SAME paymentRef on DIFFERENT order ORD_EXT2 (expect 409 — S9 replay guard) ==="
adm POST /api/admin/commerce-mode/confirm-external-payment "{\"orderId\":\"$ORD_EXT2\",\"amount\":85000,\"paymentRef\":\"VQR-RVB4-001\"}"
echo "=== T24 wrong amount ORD_EXT2 (expect 409) ==="
adm POST /api/admin/commerce-mode/confirm-external-payment "{\"orderId\":\"$ORD_EXT2\",\"amount\":1,\"paymentRef\":\"VQR-RVB4-002\"}"
echo "=== T25 marketplace order external (expect 409 not-reseller) ==="
adm POST /api/admin/commerce-mode/confirm-external-payment "{\"orderId\":\"$ORD_MKT\",\"amount\":65000,\"paymentRef\":\"VQR-RVB4-003\"}"

echo; echo "########## PHASE 5: REMIT (TC-08 regression) ##########"
echo "=== T26 pending-remittances (expect MKT+RES) ==="
call GET /api/community/wallet/pending-remittances "$SHIP_TOK"
echo "=== T27 remit MKT (expect 200) ==="
call POST /api/community/wallet/remit "$SHIP_TOK" "{\"orderId\":\"$ORD_MKT\"}"
echo "=== T28 remit RES (expect 200 — Reseller remits to PlatformWallet) ==="
call POST /api/community/wallet/remit "$SHIP_TOK" "{\"orderId\":\"$ORD_RES\"}"
echo "=== T29 dup remit (expect 409) ==="
call POST /api/community/wallet/remit "$SHIP_TOK" "{\"orderId\":\"$ORD_MKT\"}"
echo "=== T30 remit legs ==="
psqlc "SELECT \"RelatedOrderId\",\"OwnerId\",\"Type\",\"Amount\" FROM \"WalletTransactions\" WHERE \"Type\"=14 OR (\"Type\"=5 AND \"RelatedOrderId\" IN ('$ORD_MKT','$ORD_RES')) ORDER BY \"CreatedAt\";"

echo; echo "########## PHASE 6: WITHDRAWAL (TC-09 regression) ##########"
echo "=== T31 deposit +600000 fixture (BalanceAfter = current + 600000) ==="
psqlq "INSERT INTO \"WalletTransactions\" (\"Id\",\"OwnerId\",\"Type\",\"Amount\",\"Description\",\"BalanceAfter\",\"TenantId\",\"CreatedAt\",\"UpdatedAt\",\"IsDeleted\") SELECT '$DEP_ID','$SHIPPER',12,600000,'RV B4 test deposit',COALESCE((SELECT \"BalanceAfter\" FROM \"WalletTransactions\" WHERE \"OwnerId\"='$SHIPPER' AND NOT \"IsDeleted\" ORDER BY \"CreatedAt\" DESC LIMIT 1),0)+600000,'$TENANT_A',now(),now(),false;"
echo "=== T32 withdraw below-min 100000 (expect 400) ==="
call POST /api/community/wallet/withdraw "$SHIP_TOK" "{\"amount\":100000}"
echo "=== T33 withdraw 500000 (expect 200 Pending) ==="
WRESP=$(call POST /api/community/wallet/withdraw "$SHIP_TOK" "{\"amount\":500000}")
echo "$WRESP"
WID=$(echo "$WRESP" | grep -oP '"requestId":"\K[^"]+')
echo "WID=$WID"
echo "=== T34 admin approve + pay ==="
adm POST "/api/admin/withdrawals/$WID/approve" ""
adm POST "/api/admin/withdrawals/$WID/pay" "{\"bankReference\":\"RVB4-BANK-001\"}"
echo "=== T35 double-pay (expect 409) ==="
adm POST "/api/admin/withdrawals/$WID/pay" "{\"bankReference\":\"RVB4-BANK-002\"}"
echo "=== T36 withdrawal request row ==="
psqlc "SELECT \"Status\",\"BankReference\",\"WalletTransactionId\" IS NOT NULL AS has_tx FROM \"WithdrawalRequests\" WHERE \"Id\"='$WID';"

echo; echo "########## PHASE 7: ADMIN SETTLEMENTS ENDPOINT (S2) ##########"
echo "=== T37 GET /api/admin/settlements?tenantId=A (expect 200 — was LINQ crash before S2) ==="
adm GET "/api/admin/settlements?tenantId=$TENANT_A&page=1&pageSize=5"
echo "=== T38 unauthenticated (expect 401) ==="
curl -sk -w "|HTTP %{http_code}" "$API/api/admin/settlements"; echo

echo; echo "########## PHASE 8: AUDIT TRAIL (S7) ##########"
sleep 6   # AuditLogQueue is async
echo "=== T39 audit rows for our wallet txs (expect >= 10 rows, EntityType=14) ==="
psqlq "SELECT COUNT(*) FROM \"AuditLogs\" WHERE \"EntityType\"=14 AND \"CreatedAt\" > now() - interval '30 minutes';"
psqlc "SELECT \"EntityId\",\"Action\",\"EntityType\" FROM \"AuditLogs\" WHERE \"EntityType\"=14 AND \"CreatedAt\" > now() - interval '30 minutes' ORDER BY \"CreatedAt\" LIMIT 12;"

echo; echo "########## PHASE 9: BALANCE CHAIN ##########"
echo "=== T40 BalanceAfter chain per owner (no gaps/dups) ==="
psqlc "SELECT \"OwnerId\",\"Amount\",\"BalanceAfter\",\"CreatedAt\" FROM \"WalletTransactions\" WHERE \"RelatedOrderId\" IN ('$ORD_MKT','$ORD_RES','$ORD_EXT1','$ORD_ADV') OR \"Id\"='$DEP_ID' OR \"Description\" LIKE 'RV B4%' ORDER BY \"OwnerId\",\"CreatedAt\";"

echo; echo "########## PHASE 10: TENANT ATTRIBUTION (post-72f1a2a8 fix) ##########"
echo "=== T41 distinct TenantId on order-driven wallet txs (expect ONLY $TENANT_A) ==="
psqlc "SELECT DISTINCT \"TenantId\" FROM \"WalletTransactions\" WHERE \"RelatedOrderId\" IN ('$ORD_MKT','$ORD_RES','$ORD_EXT1','$ORD_EXT2','$ORD_ADV');"
echo "=== T42 zero-tenant order txs (expect 0) ==="
psqlq "SELECT COUNT(*) FROM \"WalletTransactions\" WHERE \"RelatedOrderId\" IN ('$ORD_MKT','$ORD_RES','$ORD_EXT1','$ORD_EXT2','$ORD_ADV') AND \"TenantId\"='00000000-0000-0000-0000-000000000000';"
echo "=== T43 per-order tenant check ==="
psqlc "SELECT \"RelatedOrderId\",\"Type\",\"Amount\",\"TenantId\" FROM \"WalletTransactions\" WHERE \"RelatedOrderId\" IN ('$ORD_MKT','$ORD_RES','$ORD_EXT1','$ORD_EXT2','$ORD_ADV') ORDER BY \"RelatedOrderId\",\"CreatedAt\";"

echo "=== DONE — ids in /tmp/rvb4_ids.txt ==="
