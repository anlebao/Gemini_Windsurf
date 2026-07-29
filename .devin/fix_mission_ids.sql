-- Fix mission IDs: convert 32-char hex to proper UUID format (36 chars with hyphens)
-- EF Core Guid expects format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx

UPDATE Missions SET Id = substr(Id,1,8) || '-' || substr(Id,9,4) || '-' || substr(Id,13,4) || '-' || substr(Id,17,4) || '-' || substr(Id,21,12)
WHERE length(Id) = 32;

-- Verify
SELECT Id, MissionType, Title, length(Id) as id_len FROM Missions WHERE IsDeleted = 0;
