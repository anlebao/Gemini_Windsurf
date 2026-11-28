#!/bin/bash
echo "=== Bookings hôm nay (tenant A) ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d "VanAnCoreHub" -c "SELECT \"Id\", \"PublicBookingCode\", \"StartAt\", \"StaffId\", \"Status\", \"DepositRequired\", \"PaymentStatus\", \"OrderId\" FROM \"Bookings\" WHERE \"TenantId\" = '\''6aaf19e4-8b55-48cd-94d7-b61c96a2d66d'\'' ORDER BY \"StartAt\" DESC LIMIT 12;"' 2>&1 | head -18
echo "=== CommissionLedgerEntries ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d "VanAnCoreHub" -c "SELECT \"Id\", \"BookingId\", \"SalesmanId\", \"State\", \"BaseAmount\", \"GrossCommissionAmount\", \"TaxWithheldAmount\" FROM \"CommissionLedgerEntries\" ORDER BY \"CreatedAt\" DESC LIMIT 6;"' 2>&1 | head -10
echo DONE
