#!/bin/bash
docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -t -c "SELECT tablename FROM pg_tables WHERE schemaname='public' AND tablename IN ('SystemSettings','ProductCostPrices','CommunityFundSpendRecords');"
