-- Task G2 pilot: SmartEntryLlmEnabled=true cho Cafe Tân Quy (7c021960).
INSERT OR IGNORE INTO SystemSettings (Id,Key,Value,UpdatedAt,UpdatedBy,TenantId,CreatedAt,CreatedBy,IsDeleted)
VALUES (lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-4' || substr(lower(hex(randomblob(2))),2) || '-' || substr('89ab',abs(random())%4+1,1) || substr(lower(hex(randomblob(2))),2) || '-' || lower(hex(randomblob(6))),
        'ShopFeature:7c021960-6d6c-4de4-88a1-cf8373184f49:SmartEntryLlmEnabled', 'true',
        strftime('%Y-%m-%dT%H:%M:%f','now'), NULL, '00000000-0000-0000-0000-000000000000',
        strftime('%Y-%m-%dT%H:%M:%f','now'), NULL, 0);
SELECT count(*) FROM SystemSettings WHERE Key LIKE '%SmartEntryLlmEnabled%';