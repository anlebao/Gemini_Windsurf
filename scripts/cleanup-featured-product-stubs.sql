-- ============================================================================
-- Featured Product Picker Refactor — Cleanup Stub Products
-- Updated 2026-07-23: trigger CD redeploy (disk space cleared on VPS)
-- ============================================================================
-- Purpose: Remove auto-created stub products in ShopERP SQLite that were created
--          by OrderSyncSubscriber when orders referenced ProductIds not in SQLite
--          (e.g., featured products with hand-typed GUIDs).
--
-- Stub signature: Description = 'Synced from Gateway' (see OrderSyncSubscriber.cs line 186).
--
-- Run AFTER deploying the Featured Product Picker fix (commit pending).
-- Run on each ShopERP VPS SQLite instance (vanan_shoperp.db).
--
-- IMPORTANT: Review the SELECT output BEFORE running DELETE/UPDATE.
--            Backup SQLite file before running: cp vanan_shoperp.db vanan_shoperp.db.bak
-- ============================================================================

-- Step 1: IDENTIFY stub products
SELECT Id, Name, TenantId, Price, VatRate, IsActive, Description
FROM Products
WHERE Description = 'Synced from Gateway';

-- Step 2: CHECK FK references (OrderItems pointing to stubs)
SELECT p.Id, p.Name, COUNT(oi.Id) AS OrderItemCount
FROM Products p
LEFT JOIN OrderItems oi ON oi.ProductId = p.Id
WHERE p.Description = 'Synced from Gateway'
GROUP BY p.Id, p.Name;

-- Step 3: SAFE DELETE — stubs with NO OrderItem references
DELETE FROM Products
WHERE Description = 'Synced from Gateway'
  AND Id NOT IN (SELECT DISTINCT ProductId FROM OrderItems WHERE ProductId IS NOT NULL);

-- Step 4: DEACTIVATE — stubs WITH OrderItem references (cannot delete — FK constraint)
--         Hide from UI instead. Owner can manually merge/delete later via admin UI.
UPDATE Products
SET IsActive = 0
WHERE Description = 'Synced from Gateway'
  AND Id IN (SELECT DISTINCT ProductId FROM OrderItems WHERE ProductId IS NOT NULL);

-- Step 5: VERIFY — should return 0 active stubs
SELECT COUNT(*) AS RemainingActiveStubs
FROM Products
WHERE Description = 'Synced from Gateway' AND IsActive = 1;
