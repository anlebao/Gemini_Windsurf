-- Fix missions data issue on VPS SQLite
-- Root cause: existing 2 missions have TenantId=Guid.Empty → excluded by query filter
-- when MissionService runs with real tenant. Update to Vạn An Cafe tenant.

-- 1. Fix existing missions: TenantId 00000000-0000-0000-0000-000000000000 → 00000000-0000-0000-0000-000000000001
UPDATE Missions
SET TenantId = '00000000-0000-0000-0000-000000000001'
WHERE TenantId = '00000000-0000-0000-0000-000000000000';

-- 2. Insert OtpVerify mission (MissionType=1) — 100 points, one-time
-- Only insert if not already exists for this tenant
INSERT INTO Missions (Id, MissionType, Title, Description, PointsReward, IsOneTime, DailyCap, IsActive, SortOrder, Config, TenantId, CreatedAt, UpdatedAt, IsDeleted)
SELECT
  lower(hex(randomblob(16))) as Id,
  1 as MissionType,
  'Xác thực OTP nhận điểm thưởng' as Title,
  'Xác thực số điện thoại bằng mã OTP để nhận 100 điểm thưởng (1 lần duy nhất).' as Description,
  100 as PointsReward,
  1 as IsOneTime,
  NULL as DailyCap,
  1 as IsActive,
  2 as SortOrder,
  NULL as Config,
  '00000000-0000-0000-0000-000000000001' as TenantId,
  datetime('now') as CreatedAt,
  datetime('now') as UpdatedAt,
  0 as IsDeleted
WHERE NOT EXISTS (
  SELECT 1 FROM Missions
  WHERE MissionType = 1
    AND TenantId = '00000000-0000-0000-0000-000000000001'
    AND IsDeleted = 0
);

-- 3. Insert BirthdayEntry mission (MissionType=2) — 50 points, one-time
INSERT INTO Missions (Id, MissionType, Title, Description, PointsReward, IsOneTime, DailyCap, IsActive, SortOrder, Config, TenantId, CreatedAt, UpdatedAt, IsDeleted)
SELECT
  lower(hex(randomblob(16))) as Id,
  2 as MissionType,
  'Nhập ngày sinh nhận điểm thưởng' as Title,
  'Nhập ngày sinh để nhận 50 điểm thưởng (1 lần duy nhất) + quà sinh nhật hàng năm.' as Description,
  50 as PointsReward,
  1 as IsOneTime,
  NULL as DailyCap,
  1 as IsActive,
  3 as SortOrder,
  NULL as Config,
  '00000000-0000-0000-0000-000000000001' as TenantId,
  datetime('now') as CreatedAt,
  datetime('now') as UpdatedAt,
  0 as IsDeleted
WHERE NOT EXISTS (
  SELECT 1 FROM Missions
  WHERE MissionType = 2
    AND TenantId = '00000000-0000-0000-0000-000000000001'
    AND IsDeleted = 0
);

-- 4. Insert TikTokShare mission (MissionType=4) — 100 points, daily cap 5
INSERT INTO Missions (Id, MissionType, Title, Description, PointsReward, IsOneTime, DailyCap, IsActive, SortOrder, Config, TenantId, CreatedAt, UpdatedAt, IsDeleted)
SELECT
  lower(hex(randomblob(16))) as Id,
  4 as MissionType,
  'Chia sẻ TikTok nhận điểm thưởng' as Title,
  'Chia sẻ link app diemthuong.khachvip.online lên TikTok, mỗi ngày nhận 100 điểm (tối đa 5 lần/ngày).' as Description,
  100 as PointsReward,
  0 as IsOneTime,
  5 as DailyCap,
  1 as IsActive,
  4 as SortOrder,
  NULL as Config,
  '00000000-0000-0000-0000-000000000001' as TenantId,
  datetime('now') as CreatedAt,
  datetime('now') as UpdatedAt,
  0 as IsDeleted
WHERE NOT EXISTS (
  SELECT 1 FROM Missions
  WHERE MissionType = 4
    AND TenantId = '00000000-0000-0000-0000-000000000001'
    AND IsDeleted = 0
);

-- Verify
SELECT Id, MissionType, Title, PointsReward, IsOneTime, DailyCap, IsActive, TenantId FROM Missions WHERE IsDeleted = 0;
