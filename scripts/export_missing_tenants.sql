.mode list
.separator "|"
SELECT
  "Id" || '|' ||
  "Name" || '|' ||
  "BusinessType" || '|' ||
  "Status" || '|' ||
  COALESCE("ShopInstanceId", '') || '|' ||
  "TenantId" || '|' ||
  "CreatedAt" || '|' ||
  COALESCE("Settings_ContactEmail", '') || '|' ||
  COALESCE("Settings_ContactPhone", '') || '|' ||
  COALESCE("Settings_Address", '') || '|' ||
  COALESCE("Settings_TaxCode", '') || '|' ||
  COALESCE("Settings_Latitude", '') || '|' ||
  COALESCE("Settings_Longitude", '') || '|' ||
  COALESCE("Settings_Slug", '') || '|' ||
  COALESCE("Settings_SocialLinksFb", '') || '|' ||
  COALESCE("Settings_SocialLinksTiktok", '') || '|' ||
  COALESCE("Settings_BrandStory", '') || '|' ||
  COALESCE("Settings_LogoUrl", '')
FROM Tenants
WHERE "Id" NOT IN (
  '00000000-0000-0000-0000-000000000001',
  'A5B6C7D8-1234-5678-9ABC-DEF012345678',
  '21cbf14f-581a-48c8-8ad6-becc21064535'
)
ORDER BY "Name";
