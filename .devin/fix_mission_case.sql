-- Fix: update mission IDs to UPPERCASE to match EF Core's Guid-to-string format
-- Existing missions (created via EF Core) have uppercase IDs: 949F3304-...
-- My SQL-inserted missions have lowercase: a0f5dd54-... → FK fails due to case mismatch
UPDATE Missions SET Id = UPPER(Id) WHERE Id != UPPER(Id);

-- Verify
SELECT Id, MissionType, Title FROM Missions WHERE IsDeleted = 0;
