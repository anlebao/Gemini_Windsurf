-- Migrate 7 tenants from ShopERP SQLite → Gateway PostgreSQL (2026-07-21)
-- These tenants exist in SQLite but not in PG (source of truth).
-- Preserves Id, Name, BusinessType, Status, Settings, TenantId, CreatedAt.
-- ShopInstanceId left NULL — admin can assign via /admin/tenants edit modal.

BEGIN;

INSERT INTO public."Tenants" (
  "Id", "Name", "BusinessType", "Status", "TenantId", "CreatedAt", "UpdatedAt", "IsDeleted",
  "ShopInstanceId",
  "Settings_ContactEmail", "Settings_ContactPhone", "Settings_Address",
  "Settings_TaxCode", "Settings_Latitude", "Settings_Longitude", "Settings_Slug",
  "Settings_SocialLinksFb", "Settings_SocialLinksTiktok", "Settings_BrandStory", "Settings_LogoUrl"
) VALUES
-- 1. Coffee An An
('EB7F9261-0751-4FF9-B0B2-B3698949CC80'::uuid, 'Coffee An An', 2, 1,
 'EB7F9261-0751-4FF9-B0B2-B3698949CC80'::uuid, '2026-07-18 05:41:11.0267427'::timestamp, NOW(), false,
 NULL,
 NULL, '0984496730', '105 Nguyễn Trãi, Quận 5, TP HCM',
 NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
-- 2. Công ty TNHH Thiên Lộc
('61B5C281-18CD-4B63-9537-5273C05927AF'::uuid, 'Công ty TNHH Thiên Lộc', 1, 1,
 '61B5C281-18CD-4B63-9537-5273C05927AF'::uuid, '2026-07-18 05:40:07.819499'::timestamp, NOW(), false,
 NULL,
 'lebaoan81@gmail.com', '0984496730', '12 Nguyễn thị Dưỡng',
 NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
-- 3. Kid Garden
('906387BF-2BB5-4AA3-96DC-92117046B846'::uuid, 'Kid Garden', 2, 1,
 '906387BF-2BB5-4AA3-96DC-92117046B846'::uuid, '2026-07-18 09:25:25.6859298'::timestamp, NOW(), false,
 NULL,
 'lebaoan81@gmail.com', '0984496730', '109 Cộng Hòa, phường 1, Quận Tân Bình, TP Hồ Chí Minh',
 NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
-- 4. Mimosa Spa
('BBDA2CE5-43B3-4F4B-9ED9-9F5BDAC88E52'::uuid, 'Mimosa Spa', 2, 1,
 'BBDA2CE5-43B3-4F4B-9ED9-9F5BDAC88E52'::uuid, '2026-07-18 09:24:20.3893052'::timestamp, NOW(), false,
 NULL,
 'lebaoan81@gmail.com', '0984496730', '15 Tô Ký, Quận 12, Tp Hồ Chí Minh',
 NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
-- 5. Ngô Gia Trà
('81E168D4-E44A-4728-A1EA-55151B168C96'::uuid, 'Ngô Gia Trà', 1, 1,
 '81E168D4-E44A-4728-A1EA-55151B168C96'::uuid, '2026-07-20 15:52:39.2093072'::timestamp, NOW(), false,
 NULL,
 'ngogia@tra.com', '0984496730', '12 Nguyễn Thị Dưỡng',
 NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
-- 6. Resort Thùy Dương
('97218F8C-2FE4-438D-8400-F19117350AAC'::uuid, 'Resort Thùy Dương', 1, 1,
 '97218F8C-2FE4-438D-8400-F19117350AAC'::uuid, '2026-07-18 09:26:19.0200284'::timestamp, NOW(), false,
 NULL,
 NULL, NULL, NULL,
 NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
-- 7. Tạp Hóa Bà 5
('A23C5101-7C93-49B9-858B-203E55591B82'::uuid, 'Tạp Hóa Bà 5', 2, 1,
 'A23C5101-7C93-49B9-858B-203E55591B82'::uuid, '2026-07-18 09:25:51.7136705'::timestamp, NOW(), false,
 NULL,
 NULL, NULL, NULL,
 NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL)
ON CONFLICT ("Id") DO NOTHING;

COMMIT;

-- Verify
SELECT count(*) AS total_after_migration FROM public."Tenants";
SELECT "Id", "Name", "BusinessType", "Status", "ShopInstanceId" FROM public."Tenants" ORDER BY "Name";
