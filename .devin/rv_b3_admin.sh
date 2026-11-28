#!/bin/bash
# RV Batch 3 — admin withdrawal lifecycle (run ON vanan-gateway)
API="http://127.0.0.1"
TOK=$(cat /tmp/shipper_tok.txt)
JWT=$(cat /tmp/admin_jwt.txt)
SHIPPER="6e4edec9-543c-4f10-9ef1-4b8efe894b97"
WID="03896670-12d6-4d7d-b162-db5aee3562fb"

psqlc() { sudo docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -c "$1"; }
psqlq() { sudo docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "$1"; }
call() { curl -sk -w "|HTTP %{http_code}" -X "$1" "$API$2" -H "X-Customer-Token: $TOK" -H "Content-Type: application/json" ${3:+-d "$3"}; echo; }
adm() { curl -sk -w "|HTTP %{http_code}" -X "$1" "$API$2" -H "Authorization: Bearer $JWT" -H "Content-Type: application/json" ${3:+-d "$3"}; echo; }

echo "=== A1 admin list Pending (expect 200 + WID) ==="
adm GET "/api/admin/withdrawals?status=Pending"
echo "=== A2 approve WID (expect 200) ==="
adm POST "/api/admin/withdrawals/$WID/approve" "{}"
echo "=== A3 pay WITHOUT bankRef (expect 400) ==="
adm POST "/api/admin/withdrawals/$WID/pay" "{\"bankReference\":\"\"}"
echo "=== A4 pay with bankRef (expect 200) ==="
adm POST "/api/admin/withdrawals/$WID/pay" "{\"bankReference\":\"RVB3-BANK-001\"}"
echo "=== A5 request row (expect Status=3 + bankRef + admin + txId) ==="
psqlc "SELECT \"Status\",\"BankReference\",\"ProcessedBy\" IS NOT NULL AS has_admin,\"WalletTransactionId\",\"ProcessedAt\" IS NOT NULL AS has_ts FROM \"WithdrawalRequests\" WHERE \"Id\"='$WID';"
echo "=== A6 Withdrawal tx (expect Type=4 -500000) ==="
psqlc "SELECT \"OwnerId\",\"Type\",\"Amount\",\"Description\" FROM \"WalletTransactions\" WHERE \"OwnerId\"='$SHIPPER' AND \"Type\"=4 ORDER BY \"CreatedAt\";"
echo "=== A7 double-pay (expect 409) ==="
adm POST "/api/admin/withdrawals/$WID/pay" "{\"bankReference\":\"RVB3-BANK-002\"}"
echo "=== A8 approve already-Paid (expect 409) ==="
adm POST "/api/admin/withdrawals/$WID/approve" "{}"

echo "=== A9 reject path: request → approve → reject → no tx ==="
RESP=$(call POST /api/community/wallet/withdraw "{\"amount\":500000}")
echo "$RESP"
WID2=$(echo "$RESP" | grep -oP '"requestId":"\K[^"]+')
echo "WID2=$WID2"
adm POST "/api/admin/withdrawals/$WID2/approve" "{}"
adm POST "/api/admin/withdrawals/$WID2/reject" "{\"reason\":\"RV B3 test reject\"}"
psqlc "SELECT \"Status\",\"RejectReason\" FROM \"WithdrawalRequests\" WHERE \"Id\"='$WID2';"
echo "withdrawal tx count (expect still 1):"
psqlq "SELECT COUNT(*) FROM \"WalletTransactions\" WHERE \"OwnerId\"='$SHIPPER' AND \"Type\"=4;"

echo "=== A10 cancel path: request → owner cancel (expect Cancelled=4) ==="
RESP=$(call POST /api/community/wallet/withdraw "{\"amount\":500000}")
echo "$RESP"
WID3=$(echo "$RESP" | grep -oP '"requestId":"\K[^"]+')
echo "WID3=$WID3"
call POST "/api/community/wallet/withdrawals/$WID3/cancel"
psqlc "SELECT \"Status\" FROM \"WithdrawalRequests\" WHERE \"Id\"='$WID3';"
echo "=== A11 cancel someone else's (cross-owner, expect 403) ==="
call POST "/api/community/wallet/withdrawals/$WID/cancel"

echo "=== A12 final wallet summary ==="
call GET /api/community/wallet
echo "=== A13 withdrawals list (customer view) ==="
call GET /api/community/wallet/withdrawals
