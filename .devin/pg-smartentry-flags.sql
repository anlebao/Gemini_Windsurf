-- Task PG: widen SmartEntryEnabled=true cho mọi tenant (mirror PG 142 rows).
-- Source of truth list: existing ShopFeature:{tenant}:AccountMappingEnabled keys.
INSERT INTO SystemSettings (Id,Key,Value,UpdatedAt,UpdatedBy,TenantId,CreatedAt,CreatedBy,IsDeleted)
SELECT lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-4' || substr(lower(hex(randomblob(2))),2) || '-' || substr('89ab',abs(random())%4+1,1) || substr(lower(hex(randomblob(2))),2) || '-' || lower(hex(randomblob(6))),
       replace(Key,'AccountMappingEnabled','SmartEntryEnabled'), 'true',
       strftime('%Y-%m-%dT%H:%M:%f','now'), NULL, '00000000-0000-0000-0000-000000000000',
       strftime('%Y-%m-%dT%H:%M:%f','now'), NULL, 0
FROM SystemSettings
WHERE Key LIKE 'ShopFeature:%:AccountMappingEnabled'
  AND NOT EXISTS (SELECT 1 FROM SystemSettings x
                  WHERE x.Key = replace(SystemSettings.Key,'AccountMappingEnabled','SmartEntryEnabled'));
SELECT count(*) AS smart_entry_flags FROM SystemSettings WHERE Key LIKE '%SmartEntryEnabled%';
