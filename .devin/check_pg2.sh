#!/bin/bash
# Check Order table Sprint 7 columns
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT column_name FROM information_schema.columns WHERE table_name='Orders' AND column_name IN ('CommerceMode','CostPrice','SellPrice','PlatformMargin','DeliveryFee','PlatformFeeRate','CommunityFundRate','CommissionBase');"
echo "---TENANT---"
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT column_name FROM information_schema.columns WHERE table_name='Tenants' AND column_name='CommerceModeOverride';"
