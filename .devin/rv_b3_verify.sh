#!/bin/bash
# RV Batch 3 re-verify — post-BalanceAfter-fix (run ON vanan-gateway)
API="http://127.0.0.1"
TOK=$(cat /tmp/shipper_tok.txt)
JWT=$(cat /tmp/admin_jwt.txt)
SHIPPER="6e4edec9-543c-4f10-9ef1-4b8efe894b97"
TENANT="0dfab177-86ad-4513-bd69-ddf956c7b768"
EMPTY="00000000-0000-0000-0000-000000000000"

psqlc() { sudo docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -c "$1"; }
psqlq() { sudo docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "$1"; }
call() { curl -sk -w "|HTTP %{http_code}" -X "$1" "$API$2" -H "X-Customer-Token: $TOK" -H "Content-Type: application/json" ${3:+-d "$3"}; echo; }
adm() { curl -sk -w "|HTTP %{http_code}" -X "$1" "$API$2" -H "Authorization: Bearer $JWT" -H "Content-Type: application/json" ${3:+-d "$3"}; echo; }

ORD=$(cat /proc/sys/kernel/random/uuid)
DT=$(cat /proc/sys/kernel/random/uuid)
DEP=$(cat /proc/sys/kernel/random/uuid)
echo "$ORD $DT $DEP" > /tmp/rvb3_v2_ids.txt
echo "ORD=$ORD"

echo "=== V1 fixture: marketplace order + task ==="
sudo docker exec -i vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub <<SQL
INSERT INTO "Orders" ("Id","OrderType","Status","KitchenStatus","SubTotal","TotalVatAmount","ShippingFee","DiscountAmount","TotalAmount","OrderDate","IsSyncedToCoreHub","TenantId","CreatedAt","UpdatedAt","IsDeleted","CommerceMode","CodAmount","PaymentStatus","CustomerInfo_FullName","CustomerInfo_PhoneNumber","TrackingCode")
VALUES ('$ORD','DELIVERY','delivering',0,65000,0,0,0,65000,now(),true,'$TENANT',now(),now(),false,0,65000,'Pending','RV B3 V2','0900000001','RVB3V2');
INSERT INTO "DeliveryTasks" ("Id","OrderId","ShipperId","Status","AssignedAt","ShopLat","ShopLng","TenantId","CreatedAt","UpdatedAt","IsDeleted")
VALUES ('$DT','$ORD','$SHIPPER',3,now(),0,0,'$TENANT',now(),now(),false);
SQL

echo "=== V2 confirm-cod 65000 (expect 200) ==="
call POST /api/community/wallet/confirm-cod "{\"orderId\":\"$ORD\",\"amount\":65000}"
echo "=== V3 remit (expect 200, shipper 65000→0) ==="
call POST /api/community/wallet/remit "{\"orderId\":\"$ORD\"}"
echo "=== V4 deposit fixture +1600000 (tenant=empty, matches service rows) ==="
psqlq "INSERT INTO \"WalletTransactions\" (\"Id\",\"OwnerId\",\"Type\",\"Amount\",\"Description\",\"BalanceAfter\",\"TenantId\",\"CreatedAt\",\"UpdatedAt\",\"IsDeleted\") VALUES ('$DEP','$SHIPPER',12,1600000,'RV B3 V2 deposit',1600000,'$EMPTY',now(),now(),false);"
echo "=== V5 withdraw 500000 (expect 200 Pending) ==="
RESP=$(call POST /api/community/wallet/withdraw "{\"amount\":500000}")
echo "$RESP"
WID=$(echo "$RESP" | grep -oP '"requestId":"\K[^"]+')
echo "WID=$WID"
echo "=== V6 approve + pay (expect BalanceAfter=1100000) ==="
adm POST "/api/admin/withdrawals/$WID/approve" "{}"
adm POST "/api/admin/withdrawals/$WID/pay" "{\"bankReference\":\"RVB3-V2\"}"
psqlc "SELECT \"Type\",\"Amount\",\"BalanceAfter\",\"TenantId\" FROM \"WalletTransactions\" WHERE \"OwnerId\"='$SHIPPER' ORDER BY \"CreatedAt\";"
psqlc "SELECT \"Status\",\"BankReference\",\"WalletTransactionId\" FROM \"WithdrawalRequests\" WHERE \"Id\"='$WID';"

echo "=== V7 reject path (request → reject → no tx) ==="
RESP=$(call POST /api/community/wallet/withdraw "{\"amount\":500000}")
echo "$RESP"
WID2=$(echo "$RESP" | grep -oP '"requestId":"\K[^"]+')
adm POST "/api/admin/withdrawals/$WID2/reject" "{\"reason\":\"RV B3 reject test\"}"
psqlc "SELECT \"Status\",\"RejectReason\",\"WalletTransactionId\" FROM \"WithdrawalRequests\" WHERE \"Id\"='$WID2';"
echo "withdrawal tx count (expect 1):"
psqlq "SELECT COUNT(*) FROM \"WalletTransactions\" WHERE \"OwnerId\"='$SHIPPER' AND \"Type\"=4;"

echo "=== V8 cancel path (expect Cancelled) ==="
RESP=$(call POST /api/community/wallet/withdraw "{\"amount\":500000}")
echo "$RESP"
WID3=$(echo "$RESP" | grep -oP '"requestId":"\K[^"]+')
call POST "/api/community/wallet/withdrawals/$WID3/cancel"
psqlc "SELECT \"Status\" FROM \"WithdrawalRequests\" WHERE \"Id\"='$WID3';"

echo "=== V9 final wallet summary ==="
call GET /api/community/wallet | head -c 600; echo
echo "=== V10 DONE ==="
