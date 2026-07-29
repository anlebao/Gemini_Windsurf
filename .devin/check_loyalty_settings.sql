.headers on
.mode column
-- Check ShopFeatureSettings for tenant
SELECT TenantId, Loyalty_Program_Enabled, Loyalty_PointsRate, Loyalty_MinPointsPerOrder,
       Loyalty_MaxPointsPerOrder, Loyalty_AwardOnAllOrders
FROM ShopFeatureSettings;
