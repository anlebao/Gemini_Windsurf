#!/bin/bash
echo "=== bookings tables state ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d "VanAnCoreHub" -c "SELECT (SELECT COUNT(*) FROM \"AppointmentOfferings\") AS offerings, (SELECT COUNT(*) FROM \"ServiceCategories\") AS cats, (SELECT COUNT(*) FROM \"Staffs\") AS staffs, (SELECT COUNT(*) FROM \"StaffWorkingSchedules\") AS schedules, (SELECT COUNT(*) FROM \"QRChannels\") AS qrs, (SELECT COUNT(*) FROM \"CommissionRules\") AS rules, (SELECT COUNT(*) FROM \"BookingTenantConfigs\") AS configs;"' 2>&1 | head -6
echo "=== offerings by tenant ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d "VanAnCoreHub" -c "SELECT \"TenantId\", \"DisplayName\", \"Price\" FROM \"AppointmentOfferings\";"' 2>&1 | head -8
echo "=== staff by tenant ==="
docker exec vanan-postgres-1 sh -c 'psql -U vanan_admin -d "VanAnCoreHub" -c "SELECT \"TenantId\", \"DisplayName\", \"IsActive\" FROM \"Staffs\";"' 2>&1 | head -8
echo DONE
