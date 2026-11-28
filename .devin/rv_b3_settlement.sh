#!/bin/bash
# RV Batch 3 — TC-08 shipper remit + TC-09 withdrawal (run ON vanan-gateway)
# Token: /tmp/shipper_tok.txt (minted via dev-token window on shop-a)
API="http://127.0.0.1"
TOK=$(cat /tmp/shipper_tok.txt)
SHIPPER="6e4edec9-543c-4f10-9ef1-4b8efe894b97"
TENANT="0dfab177-86ad-4513-bd69-ddf956c7b768"
PLATFORM="00000000-0000-0000-0000-000000000001"

psqlq() { sudo docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "$1"; }
psqlc() { sudo docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -c "$1"; }
call() { curl -sk -w "|HTTP %{http_code}" -X "$1" "$API$2" -H "X-Customer-Token: $TOK" -H "Content-Type: application/json" ${3:+-d "$3"}; echo; }
adm() { curl -sk -w "|HTTP %{http_code}" -X "$1" "$API$2" -H "Authorization: Bearer $JWT" -H "Content-Type: application/json" ${3:+-d "$3"}; echo; }

ORD_MKT=$(cat /proc/sys/kernel/random/uuid)
ORD_RES=$(cat /proc/sys/kernel/random/uuid)
DT_MKT=$(cat /proc/sys/kernel/random/uuid)
DT_RES=$(cat /proc/sys/kernel/random/uuid)
DEP_ID=$(cat /proc/sys/kernel/random/uuid)
echo "$ORD_MKT $ORD_RES $DT_MKT $DT_RES $DEP_ID" > /tmp/rvb3_ids.txt
echo "ORD_MKT=$ORD_MKT"
echo "ORD_RES=$ORD_RES"

echo "=== FIXTURE: marketplace + reseller orders w/ delivery tasks ==="
sudo docker exec -i vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub <<SQL
INSERT INTO "Orders" ("Id","OrderType","Status","KitchenStatus","SubTotal","TotalVatAmount","ShippingFee","DiscountAmount","TotalAmount","OrderDate","IsSyncedToCoreHub","TenantId","CreatedAt","UpdatedAt","IsDeleted","CommerceMode","CodAmount","PaymentStatus","CustomerInfo_FullName","CustomerInfo_PhoneNumber","TrackingCode")
VALUES ('$ORD_MKT','DELIVERY','delivering',0,65000,0,0,0,65000,now(),true,'$TENANT',now(),now(),false,0,65000,'Pending','RV B3 MKT','0900000001','RVB3MKT');
INSERT INTO "DeliveryTasks" ("Id","OrderId","ShipperId","Status","AssignedAt","ShopLat","ShopLng","TenantId","CreatedAt","UpdatedAt","IsDeleted")
VALUES ('$DT_MKT','$ORD_MKT','$SHIPPER',3,now(),0,0,'$TENANT',now(),now(),false);
INSERT INTO "Orders" ("Id","OrderType","Status","KitchenStatus","SubTotal","TotalVatAmount","ShippingFee","DiscountAmount","TotalAmount","OrderDate","IsSyncedToCoreHub","TenantId","CreatedAt","UpdatedAt","IsDeleted","CommerceMode","SellPrice","DeliveryFee","CostPrice","PlatformMargin","PlatformFeeRate","CommunityFundRate","PaymentStatus","CustomerInfo_FullName","CustomerInfo_PhoneNumber","TrackingCode")
VALUES ('$ORD_RES','DELIVERY','delivering',0,70000,0,15000,0,85000,now(),true,'$TENANT',now(),now(),false,1,70000,15000,40000,30000,0.5,0.1,'Pending','RV B3 RES','0900000001','RVB3RES');
INSERT INTO "DeliveryTasks" ("Id","OrderId","ShipperId","Status","AssignedAt","ShopLat","ShopLng","TenantId","CreatedAt","UpdatedAt","IsDeleted")
VALUES ('$DT_RES','$ORD_RES','$SHIPPER',3,now(),0,0,'$TENANT',now(),now(),false);
SQL

echo "=== T1 confirm-cod MKT wrong amount (expect 409) ==="
call POST /api/community/wallet/confirm-cod "{\"orderId\":\"$ORD_MKT\",\"amount\":1}"
echo "=== T2 confirm-cod MKT 65000 (expect 200) ==="
call POST /api/community/wallet/confirm-cod "{\"orderId\":\"$ORD_MKT\",\"amount\":65000}"
echo "=== T3 pending-remittances (expect ORD_MKT) ==="
call GET /api/community/wallet/pending-remittances
echo "=== T4 remit MKT (expect 200) ==="
call POST /api/community/wallet/remit "{\"orderId\":\"$ORD_MKT\"}"
echo "=== T5 remit MKT again (expect 409) ==="
call POST /api/community/wallet/remit "{\"orderId\":\"$ORD_MKT\"}"
echo "=== T6 wallet legs MKT (expect Remittance -65000 shipper + Settlement +65000 tenant) ==="
psqlc "SELECT \"OwnerId\",\"Type\",\"Amount\",\"BalanceAfter\" FROM \"WalletTransactions\" WHERE \"RelatedOrderId\"='$ORD_MKT' ORDER BY \"CreatedAt\";"

echo "=== T7 confirm-cod RES 85000 (expect 200) ==="
call POST /api/community/wallet/confirm-cod "{\"orderId\":\"$ORD_RES\",\"amount\":85000}"
echo "=== T8 remit RES (expect 200) ==="
call POST /api/community/wallet/remit "{\"orderId\":\"$ORD_RES\"}"
echo "=== T9 wallet legs RES (expect Remittance -85000 shipper + Settlement +85000 PlatformWallet) ==="
psqlc "SELECT \"OwnerId\",\"Type\",\"Amount\" FROM \"WalletTransactions\" WHERE \"RelatedOrderId\"='$ORD_RES' ORDER BY \"CreatedAt\";"
echo "--- PlatformWallet leg check:"
psqlq "SELECT COUNT(*) FROM \"WalletTransactions\" WHERE \"RelatedOrderId\"='$ORD_RES' AND \"OwnerId\"='$PLATFORM' AND \"Type\"=5 AND \"Amount\"=85000;"

echo "=== T10 withdraw 500000 insufficient (expect 409/400) ==="
call POST /api/community/wallet/withdraw "{\"amount\":500000}"
echo "=== T10b fixture deposit +600000 for shipper ==="
psqlq "INSERT INTO \"WalletTransactions\" (\"Id\",\"OwnerId\",\"Type\",\"Amount\",\"Description\",\"BalanceAfter\",\"TenantId\",\"CreatedAt\",\"UpdatedAt\",\"IsDeleted\") VALUES ('$DEP_ID','$SHIPPER',12,600000,'RV B3 test deposit',615000,'$TENANT',now(),now(),false);"
echo "=== T11 withdraw 100000 below-min (expect 400) ==="
call POST /api/community/wallet/withdraw "{\"amount\":100000}"
echo "=== T12 withdraw 500000 (expect 200 Pending) ==="
RESP=$(call POST /api/community/wallet/withdraw "{\"amount\":500000}")
echo "$RESP"
WID=$(echo "$RESP" | grep -oP '"requestId":"\K[^"]+')
echo "WID=$WID"
echo "=== T13 second withdraw while open (expect 409) ==="
call POST /api/community/wallet/withdraw "{\"amount\":500000}"
echo "=== T14 admin list WITHOUT jwt (expect 401) ==="
curl -sk -w "|HTTP %{http_code}" "$API/api/admin/withdrawals" ; echo

echo "=== T15 sysadmin login ==="
JWT=$(curl -sk -X POST "$API/api/auth/login" -H "Content-Type: application/json" -d '{"email":"sysadmin@vanan.vn","password":"2026@vanan"}' | grep -oP '"token":"\K[^"]+')
echo "JWT len=${#JWT}"
echo "=== T16 admin list Pending (expect 200 + our request) ==="
adm GET "/api/admin/withdrawals?status=Pending"
echo "=== T17 approve (expect 200) ==="
adm POST "/api/admin/withdrawals/$WID/approve" ""
echo "=== T18 pay WITHOUT bankRef (expect 400) ==="
adm POST "/api/admin/withdrawals/$WID/pay" "{\"bankReference\":\"\"}"
echo "=== T19 pay with bankRef (expect 200) ==="
adm POST "/api/admin/withdrawals/$WID/pay" "{\"bankReference\":\"RVB3-BANK-001\"}"
echo "=== T20 request row (expect Status=3 Paid + bankRef + walletTxId + processedBy) ==="
psqlc "SELECT \"Status\",\"BankReference\",\"ProcessedBy\" IS NOT NULL AS has_admin,\"WalletTransactionId\" FROM \"WithdrawalRequests\" WHERE \"Id\"='$WID';"
echo "=== T21 withdrawal wallet tx (expect Type=4 Withdrawal -500000) ==="
psqlc "SELECT \"OwnerId\",\"Type\",\"Amount\",\"Description\" FROM \"WalletTransactions\" WHERE \"OwnerId\"='$SHIPPER' AND \"Type\"=4 ORDER BY \"CreatedAt\";"
echo "=== T22 double-pay (expect 409) ==="
adm POST "/api/admin/withdrawals/$WID/pay" "{\"bankReference\":\"RVB3-BANK-002\"}"

echo "=== T23 reject path: new request → reject (expect Rejected, no new Withdrawal tx) ==="
RESP2=$(call POST /api/community/wallet/withdraw "{\"amount\":500000}")
echo "$RESP2"
WID2=$(echo "$RESP2" | grep -oP '"requestId":"\K[^"]+')
adm POST "/api/admin/withdrawals/$WID2/approve" ""
adm POST "/api/admin/withdrawals/$WID2/reject" "{\"reason\":\"RV B3 test reject\"}"
psqlc "SELECT \"Status\",\"RejectReason\" FROM \"WithdrawalRequests\" WHERE \"Id\"='$WID2';"
echo "withdrawal tx count (expect still 1):"
psqlq "SELECT COUNT(*) FROM \"WalletTransactions\" WHERE \"OwnerId\"='$SHIPPER' AND \"Type\"=4;"

echo "=== T24 cancel path: new request → owner cancel ==="
RESP3=$(call POST /api/community/wallet/withdraw "{\"amount\":500000}")
echo "$RESP3"
WID3=$(echo "$RESP3" | grep -oP '"requestId":"\K[^"]+')
call POST "/api/community/wallet/withdrawals/$WID3/cancel"
psqlc "SELECT \"Status\" FROM \"WithdrawalRequests\" WHERE \"Id\"='$WID3';"

echo "=== T25 wallet summary ==="
call GET /api/community/wallet
echo "=== DONE — ids saved to /tmp/rvb3_ids.txt ==="
