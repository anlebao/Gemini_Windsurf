-- Check tenant ShopInstanceId + ShopInstances table
SELECT t."Id"::text, t."Name", t."ShopInstanceId"::text as shop_instance_id
FROM "Tenants" t WHERE t."Id" = '00000000-0000-0000-0000-000000000001';

-- Check ShopInstances
SELECT "Id"::text, "TenantId"::text, "BaseUrl", "IsActive" FROM "ShopInstances" LIMIT 5;

-- Check ShopERP container env for SHOP_INSTANCE_ID
