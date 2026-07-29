-- Set loyalty rate for Vạn An Cafe: 1 point per 1,000 VND
-- rate = 0.001, min = 1 point (so even small orders get at least 1 point)
UPDATE ShopFeatureSettings
SET Loyalty_PointsRate = 0.001,
    Loyalty_MinPointsPerOrder = 1
WHERE TenantId = '00000000-0000-0000-0000-000000000001';

-- Verify
SELECT TenantId, Loyalty_PointsRate, Loyalty_MinPointsPerOrder, Loyalty_MaxPointsPerOrder, Loyalty_AwardOnAllOrders
FROM ShopFeatureSettings WHERE TenantId = '00000000-0000-0000-0000-000000000001';
