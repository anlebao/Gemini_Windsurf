#!/bin/bash
# RV Batch 4 — cleanup fixtures on vanan-gateway (PG). Reads /tmp/rvb4_ids.txt.
# Deletes ONLY rows tied to our test orders/customers — real tenant/platform
# wallet rows are never touched (RelatedOrderId-scoped deletes only).
set -e
psqlq() { sudo docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "$1"; }
psqlc() { sudo docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -c "$1"; }

read ORD_MKT ORD_RES ORD_EXT1 ORD_EXT2 ORD_ADV DEP_ID SHIPPER OWNER_A OWNER_B TENANT_B < /tmp/rvb4_ids.txt
TENANT_A="0dfab177-86ad-4513-bd69-ddf956c7b768"
ORDS="'$ORD_MKT','$ORD_RES','$ORD_EXT1','$ORD_EXT2','$ORD_ADV'"

echo "=== audit rows for our wallet txs (delete FIRST — EntityId refs txs) ==="
psqlc "DELETE FROM \"AuditLogs\" WHERE \"EntityType\"=14 AND \"EntityId\" IN (SELECT \"Id\" FROM \"WalletTransactions\" WHERE \"RelatedOrderId\" IN ($ORDS) OR \"OwnerId\" IN ('$SHIPPER','$OWNER_A','$OWNER_B'));"
echo "=== withdrawal requests of test customers ==="
psqlc "DELETE FROM \"WithdrawalRequests\" WHERE \"OwnerId\" IN ('$SHIPPER','$OWNER_A','$OWNER_B');"
echo "=== wallet txs: our orders + deposit + all txs of test customers ==="
psqlc "DELETE FROM \"WalletTransactions\" WHERE \"RelatedOrderId\" IN ($ORDS) OR \"Id\"='$DEP_ID' OR \"OwnerId\" IN ('$SHIPPER','$OWNER_A','$OWNER_B');"
echo "=== delivery tasks + orders ==="
psqlc "DELETE FROM \"DeliveryTasks\" WHERE \"OrderId\" IN ($ORDS);"
psqlc "DELETE FROM \"Orders\" WHERE \"Id\" IN ($ORDS);"
echo "=== restore tenants (OwnerCustomerId=NULL, ContactPhone restore) ==="
psqlc "UPDATE \"Tenants\" SET \"OwnerCustomerId\"=NULL, \"UpdatedAt\"=now() WHERE \"Id\" IN ('$TENANT_A','$TENANT_B');"
psqlc "UPDATE \"Tenants\" SET \"Settings_ContactPhone\"='$(cat /tmp/rvb4_orig_contactphone.txt 2>/dev/null)' WHERE \"Id\"='$TENANT_A';" 2>/dev/null || \
psqlc "UPDATE \"Tenants\" SET \"Settings_ContactPhone\"=NULL WHERE \"Id\"='$TENANT_A';"
echo "=== delete test customers (PG) ==="
psqlc "DELETE FROM \"Customers\" WHERE \"Id\" IN ('$SHIPPER','$OWNER_A','$OWNER_B');"

echo "=== verify pristine ==="
echo "wallet txs (2h window): $(psqlq "SELECT COUNT(*) FROM \"WalletTransactions\" WHERE \"CreatedAt\" > now() - interval '2 hours';")"
echo "test orders: $(psqlq "SELECT COUNT(*) FROM \"Orders\" WHERE \"Id\" IN ($ORDS);")"
echo "test customers: $(psqlq "SELECT COUNT(*) FROM \"Customers\" WHERE \"Id\" IN ('$SHIPPER','$OWNER_A','$OWNER_B');")"
echo "tenants w/ OwnerCustomerId set: $(psqlq "SELECT COUNT(*) FROM \"Tenants\" WHERE \"OwnerCustomerId\" IS NOT NULL;")"
echo "=== DONE gateway cleanup ==="
