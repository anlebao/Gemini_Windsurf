#!/bin/bash
# RV Batch 1 (loyalty PG->SQLite mirror sync) — Gateway VPS checks
set +e
echo "=== RV-L1: Deploy marker — LoyaltyBalanceSyncPublisher trong DLL ==="
docker exec vanan-gateway-1 sh -c "grep -ac 'LoyaltyBalanceSync' /app/VanAn.CoreHub.dll 2>/dev/null" && echo "  OK: marker found in CoreHub.dll" || echo "  FAIL: marker NOT found"
docker exec vanan-gateway-1 sh -c "grep -ac 'LoyaltyBalanceSync' /app/VanAn.Gateway.dll 2>/dev/null" && echo "  OK: marker in Gateway.dll" || echo "  (no marker in Gateway.dll — expected, publisher in CoreHub)"

echo ""
echo "=== RV-L2: Orders completed/delivered sau deploy (>= 2026-09-20T11:40:00Z) ==="
docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "
SELECT o.\"Id\", substring(o.\"TenantId\"::text,1,8) AS tenant, substring(coalesce(o.\"CustomerId\"::text,''),1,8) AS cust, o.\"Status\", o.\"CompletedAt\"
FROM \"Orders\" o
WHERE o.\"Status\" IN ('completed','delivered') AND o.\"CompletedAt\" >= '2026-09-20T11:40:00Z'
ORDER BY o.\"CompletedAt\" DESC LIMIT 10;"

echo ""
echo "=== RV-L3: LoyaltyIssuanceRecords gần đây (per-order award) ==="
docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "
SELECT substring(\"OrderId\"::text,1,8), substring(\"CustomerId\"::text,1,8), \"PointsIssued\", \"IssuedAt\"
FROM \"LoyaltyIssuanceRecords\"
WHERE \"IssuedAt\" >= '2026-09-20T11:40:00Z'
ORDER BY \"IssuedAt\" DESC LIMIT 10;"

echo ""
echo "=== RV-L4: Gateway logs — LOYALTY Awarded sau deploy ==="
docker logs vanan-gateway-1 --since 2026-09-20T11:40:00Z 2>&1 | grep -E "LOYALTY: Awarded|LoyaltyBalanceSync|Skipped duplicate" | tail -15

echo ""
echo "=== RV-L5: PG LoyaltyRewards cập nhật gần đây ==="
docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -tAc "
SELECT substring(\"CustomerId\"::text,1,8), substring(\"TenantId\"::text,1,8), \"PointBalance\", \"UpdatedAt\"
FROM \"LoyaltyRewards\"
WHERE \"UpdatedAt\" >= '2026-09-20T11:40:00Z'
ORDER BY \"UpdatedAt\" DESC LIMIT 10;"
echo "=== END ==="
