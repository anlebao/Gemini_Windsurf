using VanAn.Shared.Omnichannel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Implementation of data versioning and audit trail service
    /// Provides comprehensive version tracking and change history
    /// </summary>
    public class DataVersioningService(ILogger<DataVersioningService> logger, IMemoryCache cache) : IDataVersioning
    {
        private readonly ILogger<DataVersioningService> _logger = logger;
        private readonly IMemoryCache _cache = cache;
        private readonly Dictionary<string, List<DataVersion>> _versionStorage = [];
        private readonly Dictionary<string, List<AuditEntry>> _auditStorage = [];

        public async Task<DataVersion> CreateVersionAsync(string entityType, string entityId, object data, string userId, string deviceId)
        {
            await Task.CompletedTask;
            try
            {
                _logger.LogInformation("Creating version for {EntityType}:{EntityId} by user {UserId}",
                    entityType, entityId, userId);

                string entityKey = GetEntityKey(entityType, entityId);
                List<DataVersion> versions = _versionStorage.TryGetValue(entityKey, out List<DataVersion>? existingVersions)
                    ? existingVersions
                    : [];

                int nextVersionNumber = versions.Count > 0 ? versions.Max(v => v.VersionNumber) + 1 : 1;
                string dataJson = JsonSerializer.Serialize(data);
                string dataHash = CalculateChecksum(dataJson);

                DataVersion version = new()
                {
                    VersionId = Guid.NewGuid().ToString(),
                    EntityType = entityType,
                    EntityId = entityId,
                    VersionNumber = nextVersionNumber,
                    Data = data,
                    DataHash = dataHash,
                    CreatedBy = userId,
                    CreatedByDevice = deviceId,
                    CreatedAt = DateTime.UtcNow,
                    ParentVersionId = versions.LastOrDefault()?.VersionId ?? string.Empty,
                    Type = DetermineVersionType(versions, data),
                    DataSizeBytes = Encoding.UTF8.GetByteCount(dataJson),
                    IsDeleted = false
                };

                versions.Add(version);
                _versionStorage[entityKey] = versions;

                // Cache the current version
                string cacheKey = $"current_version_{entityKey}";
                _cache.Set(cacheKey, version, TimeSpan.FromHours(24));

                // Create audit entry
                await CreateAuditEntryAsync(entityType, entityId, AuditOperation.Update, userId, deviceId,
                    new { version.VersionId, version.VersionNumber });

                _logger.LogInformation("Version {VersionId} created for {EntityType}:{EntityId}",
                    version.VersionId, entityType, entityId);

                return version;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating version for {EntityType}:{EntityId}", entityType, entityId);
                throw;
            }
        }

        public async Task<DataVersion?> GetCurrentVersionAsync(string entityType, string entityId)
        {
            await Task.CompletedTask;
            try
            {
                _logger.LogInformation("Getting current version for {EntityType}:{EntityId}", entityType, entityId);

                string entityKey = GetEntityKey(entityType, entityId);
                string cacheKey = $"current_version_{entityKey}";

                // Try cache first
                if (_cache.TryGetValue(cacheKey, out DataVersion? cachedVersion))
                {
                    return cachedVersion;
                }

                // Fallback to storage
                if (_versionStorage.TryGetValue(entityKey, out List<DataVersion>? versions) && versions.Count > 0)
                {
                    DataVersion currentVersion = versions.OrderByDescending(v => v.VersionNumber).First();
                    _cache.Set(cacheKey, currentVersion, TimeSpan.FromHours(24));
                    return currentVersion;
                }

                _logger.LogWarning("No versions found for {EntityType}:{EntityId}", entityType, entityId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current version for {EntityType}:{EntityId}", entityType, entityId);
                throw;
            }
        }

        public async Task<List<DataVersion>> GetVersionHistoryAsync(string entityType, string entityId, int maxVersions = 50)
        {
            await Task.CompletedTask;
            try
            {
                _logger.LogInformation("Getting version history for {EntityType}:{EntityId}", entityType, entityId);

                string entityKey = GetEntityKey(entityType, entityId);

                if (_versionStorage.TryGetValue(entityKey, out List<DataVersion>? versions))
                {
                    List<DataVersion> history = versions
                        .OrderByDescending(v => v.VersionNumber)
                        .Take(maxVersions)
                        .ToList();

                    _logger.LogInformation("Found {VersionCount} versions for {EntityType}:{EntityId}",
                        history.Count, entityType, entityId);

                    return history;
                }

                _logger.LogWarning("No versions found for {EntityType}:{EntityId}", entityType, entityId);
                return [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting version history for {EntityType}:{EntityId}", entityType, entityId);
                throw;
            }
        }

        public async Task<VersionComparison> CompareVersionsAsync(string entityType, string entityId, string versionId1, string versionId2)
        {
            try
            {
                _logger.LogInformation("Comparing versions {VersionId1} and {VersionId2} for {EntityType}:{EntityId}",
                    versionId1, versionId2, entityType, entityId);

                string entityKey = GetEntityKey(entityType, entityId);

                if (!_versionStorage.TryGetValue(entityKey, out List<DataVersion>? versions))
                {
                    throw new InvalidOperationException($"No versions found for {entityType}:{entityId}");
                }

                DataVersion? version1 = versions.FirstOrDefault(v => v.VersionId == versionId1);
                DataVersion? version2 = versions.FirstOrDefault(v => v.VersionId == versionId2);

                if (version1 == null || version2 == null)
                {
                    throw new InvalidOperationException("One or both versions not found");
                }

                List<PropertyDifference> differences = await CompareDataAsync(version1.Data, version2.Data);
                VersionComparison result = new()
                {
                    EntityType = entityType,
                    EntityId = entityId,
                    VersionId1 = versionId1,
                    VersionId2 = versionId2,
                    Differences = differences,
                    Result = DetermineComparisonResult(differences),
                    ComparedAt = DateTime.UtcNow,
                    ComparedBy = "system"
                };

                _logger.LogInformation("Comparison completed for {EntityType}:{EntityId} with {DifferenceCount} differences",
                    entityType, entityId, differences.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparing versions for {EntityType}:{EntityId}", entityType, entityId);
                throw;
            }
        }

        public async Task<RevertResult> RevertToVersionAsync(string entityType, string entityId, string versionId, string userId)
        {
            try
            {
                _logger.LogInformation("Reverting {EntityType}:{EntityId} to version {VersionId}",
                    entityType, entityId, versionId);

                string entityKey = GetEntityKey(entityType, entityId);

                if (!_versionStorage.TryGetValue(entityKey, out List<DataVersion>? versions))
                {
                    throw new InvalidOperationException($"No versions found for {entityType}:{entityId}");
                }

                DataVersion? targetVersion = versions.FirstOrDefault(v => v.VersionId == versionId) ?? throw new InvalidOperationException($"Version {versionId} not found");
                DataVersion? currentVersion = await GetCurrentVersionAsync(entityType, entityId);

                // Create new version with reverted data
                DataVersion revertedVersion = await CreateVersionAsync(entityType, entityId, targetVersion.Data, userId, "system-revert");

                // Update the version type
                List<DataVersion> versionList = _versionStorage[entityKey];
                int versionIndex = versionList.FindIndex(v => v.VersionId == revertedVersion.VersionId);
                versionList[versionIndex] = revertedVersion with { Type = VersionType.Revert, Comment = $"Reverted to version {versionId}" };

                RevertResult result = new()
                {
                    Success = true,
                    EntityType = entityType,
                    EntityId = entityId,
                    FromVersionId = currentVersion?.VersionId ?? string.Empty,
                    ToVersionId = versionId,
                    NewVersion = revertedVersion,
                    RevertedAt = DateTime.UtcNow,
                    RevertedBy = userId
                };

                // Create audit entry for revert
                await CreateAuditEntryAsync(entityType, entityId, AuditOperation.Revert, userId, "system-revert",
                    new { result.FromVersionId, ToVersionId = versionId });

                _logger.LogInformation("Successfully reverted {EntityType}:{EntityId} to version {VersionId}",
                    entityType, entityId, versionId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reverting {EntityType}:{EntityId} to version {VersionId}",
                    entityType, entityId, versionId);
                throw;
            }
        }

        public async Task<List<AuditEntry>> GetAuditTrailAsync(string entityType, string entityId, DateTime? from = null, DateTime? to = null)
        {
            await Task.CompletedTask;
            try
            {
                _logger.LogInformation("Getting audit trail for {EntityType}:{EntityId}", entityType, entityId);

                string entityKey = GetEntityKey(entityType, entityId);

                if (!_auditStorage.TryGetValue(entityKey, out List<AuditEntry>? entries))
                {
                    _logger.LogWarning("No audit entries found for {EntityType}:{EntityId}", entityType, entityId);
                    return [];
                }

                IEnumerable<AuditEntry> query = entries.AsEnumerable();

                if (from.HasValue)
                {
                    query = query.Where(e => e.Timestamp >= from.Value);
                }

                if (to.HasValue)
                {
                    query = query.Where(e => e.Timestamp <= to.Value);
                }

                List<AuditEntry> trail =
                [
                    .. query
                                        .OrderByDescending(e => e.Timestamp)
,
                ];

                _logger.LogInformation("Found {EntryCount} audit entries for {EntityType}:{EntityId}",
                    trail.Count, entityType, entityId);

                return trail;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit trail for {EntityType}:{EntityId}", entityType, entityId);
                throw;
            }
        }

        public async Task<AuditEntry> CreateAuditEntryAsync(string entityType, string entityId, AuditOperation operation, string userId, string deviceId, object? changes = null)
        {
            await Task.CompletedTask;
            try
            {
                _logger.LogInformation("Creating audit entry for {Operation} on {EntityType}:{EntityId}",
                    operation, entityType, entityId);

                string entityKey = GetEntityKey(entityType, entityId);
                List<AuditEntry> entries = _auditStorage.TryGetValue(entityKey, out List<AuditEntry>? existingEntries)
                    ? existingEntries
                    : [];

                AuditEntry entry = new()
                {
                    EntityType = entityType,
                    EntityId = entityId,
                    Operation = operation,
                    UserId = userId,
                    DeviceId = deviceId,
                    Changes = changes,
                    Timestamp = DateTime.UtcNow,
                    IpAddress = "127.0.0.1", // TODO: Get actual IP
                    UserAgent = "VanAn-System", // TODO: Get actual user agent
                    Severity = DetermineAuditSeverity(operation),
                    Success = true
                };

                entries.Add(entry);
                _auditStorage[entityKey] = entries;

                _logger.LogInformation("Audit entry {EntryId} created for {Operation} on {EntityType}:{EntityId}",
                    entry.EntryId, operation, entityType, entityId);

                return entry;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating audit entry for {Operation} on {EntityType}:{EntityId}",
                    operation, entityType, entityId);
                throw;
            }
        }

        public async Task<VersionIntegrityResult> ValidateVersionIntegrityAsync(string entityType, string entityId)
        {
            await Task.CompletedTask;
            try
            {
                _logger.LogInformation("Validating version integrity for {EntityType}:{EntityId}", entityType, entityId);

                string entityKey = GetEntityKey(entityType, entityId);

                if (!_versionStorage.TryGetValue(entityKey, out List<DataVersion>? versions))
                {
                    return new VersionIntegrityResult
                    {
                        IsValid = true,
                        EntityType = entityType,
                        EntityId = entityId,
                        Issues = [],
                        ValidatedAt = DateTime.UtcNow,
                        ValidatedBy = "system-validator",
                        TotalVersions = 0,
                        CorruptedVersions = 0
                    };
                }

                List<IntegrityIssue> issues = [];
                int corruptedCount = 0;

                foreach (DataVersion version in versions)
                {
                    List<IntegrityIssue> versionIssues = await ValidateSingleVersionAsync(version);
                    issues.AddRange(versionIssues);

                    if (versionIssues.Any(i => i.Severity is IssueSeverity.Critical or IssueSeverity.Error))
                    {
                        corruptedCount++;
                    }
                }

                VersionIntegrityResult result = new()
                {
                    IsValid = issues.Count == 0,
                    EntityType = entityType,
                    EntityId = entityId,
                    Issues = issues,
                    ValidatedAt = DateTime.UtcNow,
                    ValidatedBy = "system-validator",
                    TotalVersions = versions.Count,
                    CorruptedVersions = corruptedCount
                };

                _logger.LogInformation("Integrity validation completed for {EntityType}:{EntityId}: {IsValid}, {CorruptedCount}/{TotalVersions} corrupted",
                    entityType, entityId, result.IsValid, corruptedCount, versions.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating version integrity for {EntityType}:{EntityId}", entityType, entityId);
                throw;
            }
        }

        public async Task<CleanupResult> CleanupOldVersionsAsync(string entityType, TimeSpan retentionPeriod)
        {
            await Task.CompletedTask;
            try
            {
                _logger.LogInformation("Cleaning up old versions for {EntityType} with retention period {RetentionPeriod}",
                    entityType, retentionPeriod);

                DateTime cutoffDate = DateTime.UtcNow.Subtract(retentionPeriod);
                int versionsBeforeCleanup = 0;
                int versionsAfterCleanup = 0;
                int versionsDeleted = 0;
                long spaceSaved = 0L;
                List<string> errors = [];

                // Find all entities of this type
                List<string> entityKeys = _versionStorage.Keys.Where(k => k.StartsWith($"{entityType}:", StringComparison.OrdinalIgnoreCase)).ToList();

                foreach (string? entityKey in entityKeys)
                {
                    if (_versionStorage.TryGetValue(entityKey, out List<DataVersion>? versions))
                    {
                        versionsBeforeCleanup += versions.Count;

                        List<DataVersion> versionsToDelete = versions.Where(v => v.CreatedAt < cutoffDate).ToList();

                        foreach (DataVersion? versionToDelete in versionsToDelete)
                        {
                            try
                            {
                                versions.Remove(versionToDelete);
                                versionsDeleted++;
                                spaceSaved += versionToDelete.DataSizeBytes;
                            }
                            catch (Exception ex)
                            {
                                errors.Add($"Error deleting version {versionToDelete.VersionId}: {ex.Message}");
                            }
                        }

                        versionsAfterCleanup += versions.Count;
                    }
                }

                CleanupResult result = new()
                {
                    Success = errors.Count == 0,
                    EntityType = entityType,
                    RetentionPeriod = retentionPeriod,
                    VersionsBeforeCleanup = versionsBeforeCleanup,
                    VersionsAfterCleanup = versionsAfterCleanup,
                    VersionsDeleted = versionsDeleted,
                    SpaceSavedBytes = spaceSaved,
                    Errors = errors,
                    CleanedAt = DateTime.UtcNow,
                    CleanedBy = "system-cleanup"
                };

                _logger.LogInformation("Cleanup completed for {EntityType}: {VersionsDeleted} versions deleted, {SpaceSavedBytes} bytes saved",
                    entityType, versionsDeleted, spaceSaved);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old versions for {EntityType}", entityType);
                throw;
            }
        }

        #region Private Helper Methods

        private static string GetEntityKey(string entityType, string entityId)
        {
            return $"{entityType}:{entityId}";
        }

        private static VersionType DetermineVersionType(List<DataVersion> existingVersions, object data)
        {
            if (existingVersions.Count == 0)
            {
                return VersionType.Create;
            }

            DataVersion lastVersion = existingVersions.Last();
            return lastVersion.IsDeleted ? VersionType.Restore : VersionType.Update;
        }

        private static async Task<List<PropertyDifference>> CompareDataAsync(object data1, object data2)
        {
            await Task.CompletedTask;
            List<PropertyDifference> differences = [];
            string json1 = JsonSerializer.Serialize(data1);
            string json2 = JsonSerializer.Serialize(data2);
            JsonDocument doc1 = JsonDocument.Parse(json1);
            JsonDocument doc2 = JsonDocument.Parse(json2);

            foreach (JsonProperty property1 in doc1.RootElement.EnumerateObject())
            {
                string propertyName = property1.Name;
                JsonElement value1 = property1.Value;

                if (doc2.RootElement.TryGetProperty(propertyName, out JsonElement value2))
                {
                    if (!JsonElementEquals(value1, value2))
                    {
                        differences.Add(new PropertyDifference
                        {
                            PropertyName = propertyName,
                            Value1 = ExtractValue(value1),
                            Value2 = ExtractValue(value2),
                            Type = DifferenceType.Modified,
                            IsSignificant = IsSignificantChange(propertyName, value1, value2)
                        });
                    }
                }
                else
                {
                    differences.Add(new PropertyDifference
                    {
                        PropertyName = propertyName,
                        Value1 = ExtractValue(value1),
                        Value2 = null,
                        Type = DifferenceType.Removed,
                        IsSignificant = true
                    });
                }
            }

            foreach (JsonProperty property2 in doc2.RootElement.EnumerateObject())
            {
                if (!doc1.RootElement.TryGetProperty(property2.Name, out _))
                {
                    differences.Add(new PropertyDifference
                    {
                        PropertyName = property2.Name,
                        Value1 = null,
                        Value2 = ExtractValue(property2.Value),
                        Type = DifferenceType.Added,
                        IsSignificant = true
                    });
                }
            }

            return differences;
        }

        private static ComparisonResult DetermineComparisonResult(List<PropertyDifference> differences)
        {
            if (differences.Count == 0)
            {
                return ComparisonResult.Identical;
            }

            int significantChanges = differences.Count(d => d.IsSignificant);

            return significantChanges == 0
                ? ComparisonResult.MinorChanges
                : significantChanges <= 3 ? ComparisonResult.MajorChanges : ComparisonResult.CompletelyDifferent;
        }

        private static bool JsonElementEquals(JsonElement element1, JsonElement element2)
        {
            return element1.ValueKind == element2.ValueKind &&
                   element1.GetRawText() == element2.GetRawText();
        }

        private static object? ExtractValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt32(out int intValue) ? (object)intValue : element.GetDouble(),
                JsonValueKind.True or JsonValueKind.False => element.GetBoolean(),
                JsonValueKind.Null => null,
                JsonValueKind.Undefined => throw new NotImplementedException(),
                JsonValueKind.Object => throw new NotImplementedException(),
                JsonValueKind.Array => throw new NotImplementedException(),
                _ => element.GetRawText()
            };
        }

        private static bool IsSignificantChange(string propertyName, JsonElement value1, JsonElement value2)
        {
            string[] significantFields = ["Status", "Total", "Price", "OrderId", "CustomerId", "PaymentStatus"];
            return significantFields.Contains(propertyName);
        }

        private static AuditSeverity DetermineAuditSeverity(AuditOperation operation)
        {
            return operation switch
            {
                AuditOperation.Delete => AuditSeverity.High,
                AuditOperation.Create => AuditSeverity.Medium,
                AuditOperation.Update => AuditSeverity.Low,
                AuditOperation.Revert => AuditSeverity.High,
                AuditOperation.Restore => AuditSeverity.Medium,
                AuditOperation.Read => throw new NotImplementedException(),
                AuditOperation.Export => throw new NotImplementedException(),
                AuditOperation.Import => throw new NotImplementedException(),
                AuditOperation.Sync => throw new NotImplementedException(),
                AuditOperation.Merge => throw new NotImplementedException(),
                AuditOperation.Backup => throw new NotImplementedException(),
                AuditOperation.RestoreBackup => throw new NotImplementedException(),
                AuditOperation.Access => throw new NotImplementedException(),
                AuditOperation.Login => throw new NotImplementedException(),
                AuditOperation.Logout => throw new NotImplementedException(),
                AuditOperation.PermissionChange => throw new NotImplementedException(),
                AuditOperation.ConfigurationChange => throw new NotImplementedException(),
                _ => AuditSeverity.Info
            };
        }

        private static async Task<List<IntegrityIssue>> ValidateSingleVersionAsync(DataVersion version)
        {
            List<IntegrityIssue> issues = [];

            try
            {
                // Validate data hash
                string dataJson = JsonSerializer.Serialize(version.Data);
                string calculatedHash = CalculateChecksum(dataJson);

                if (calculatedHash != version.DataHash)
                {
                    issues.Add(new IntegrityIssue
                    {
                        VersionId = version.VersionId,
                        IssueType = "ChecksumMismatch",
                        Description = "Data checksum does not match stored hash",
                        Severity = IssueSeverity.Error,
                        CanAutoFix = false
                    });
                }

                // Validate data is not null
                if (version.Data == null)
                {
                    issues.Add(new IntegrityIssue
                    {
                        VersionId = version.VersionId,
                        IssueType = "MissingData",
                        Description = "Version data is null",
                        Severity = IssueSeverity.Critical,
                        CanAutoFix = false
                    });
                }

                // Validate version number continuity
                // Check against previous versions to ensure no gaps
                int expectedVersionNumber = 1; // Simplified for now

                if (version.VersionNumber != expectedVersionNumber)
                {
                    issues.Add(new IntegrityIssue
                    {
                        VersionId = version.VersionId,
                        IssueType = "VersionGap",
                        Description = $"Version number {version.VersionNumber} doesn't match expected {expectedVersionNumber}",
                        Severity = IssueSeverity.Warning,
                        CanAutoFix = true
                    });
                }
            }
            catch (Exception ex)
            {
                issues.Add(new IntegrityIssue
                {
                    VersionId = version.VersionId,
                    IssueType = "ValidationError",
                    Description = $"Validation error: {ex.Message}",
                    Severity = IssueSeverity.Error,
                    CanAutoFix = false
                });
            }

            return issues;
        }

        private static string CalculateChecksum(string data)
        {
            return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(data)));
        }

        #endregion
    }
}
