using VanAn.Shared.Omnichannel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Implementation of sync strategy for multi-device data consistency
/// Provides comprehensive conflict resolution and delta synchronization
/// </summary>
public class SyncStrategyService : ISyncStrategy
{
    private readonly ILogger<SyncStrategyService> _logger;
    private readonly IMemoryCache _cache;
    private readonly Dictionary<string, SyncStrategyType> _entityStrategies;

    public SyncStrategyService(ILogger<SyncStrategyService> logger, IMemoryCache cache)
    {
        _logger = logger;
        _cache = cache;
        _entityStrategies = InitializeEntityStrategies();
    }

    public async Task<SyncResult> SynchronizeAsync(SyncRequest request)
    {
        try
        {
            _logger.LogInformation("Starting synchronization for {EntityType}:{EntityId} using {Strategy}", 
                request.EntityType, request.EntityId, request.Priority);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Get strategy type for entity
            var strategyType = await GetStrategyTypeAsync(request.EntityType);
            
            // Validate data integrity
            var integrityValidation = await ValidateDataIntegrityAsync(request.EntityType, request.LocalData);
            if (!integrityValidation.IsValid)
            {
                _logger.LogWarning("Data integrity validation failed for {EntityType}:{EntityId}", 
                    request.EntityType, request.EntityId);
                return CreateFailureResult(request, "Data integrity validation failed", stopwatch.Elapsed);
            }

            // Detect conflicts
            var conflicts = new List<SyncConflict>();
            if (request.RemoteData != null)
            {
                conflicts = await DetectConflictsAsync(request.EntityType, request.EntityId, request.LocalData, request.RemoteData);
            }

            // Resolve conflicts
            var resolvedConflicts = new List<SyncConflict>();
            foreach (var conflict in conflicts)
            {
                var resolution = await ResolveConflictAsync(conflict, ConflictResolutionStrategy.LastWriteWins);
                if (resolution.RequiresUserIntervention)
                {
                    return CreateConflictResult(request, conflicts, stopwatch.Elapsed);
                }
                resolvedConflicts.Add(conflict);
            }

            // Apply synchronization based on strategy
            var result = strategyType switch
            {
                SyncStrategyType.FullSync => PerformFullSync(request),
                SyncStrategyType.DeltaSync => await PerformDeltaSyncAsync(request),
                SyncStrategyType.ConflictResolution => PerformConflictResolution(request, conflicts),
                _ => PerformFullSync(request)
            };

            stopwatch.Stop();

            var syncResult = new SyncResult
            {
                Success = result.Success,
                EntityType = request.EntityType,
                EntityId = request.EntityId,
                Outcome = result.Success ? SyncOutcome.Success : SyncOutcome.Failure,
                SyncedData = result.SyncedData,
                ResolvedConflicts = resolvedConflicts,
                SyncTimestamp = DateTime.UtcNow,
                DataSizeBytes = CalculateDataSize(result.SyncedData),
                Duration = stopwatch.Elapsed,
                StrategyUsed = strategyType
            };

            _logger.LogInformation("Synchronization completed for {EntityType}:{EntityId} in {Duration}ms", 
                request.EntityType, request.EntityId, stopwatch.ElapsedMilliseconds);

            return syncResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during synchronization for {EntityType}:{EntityId}", 
                request.EntityType, request.EntityId);
            throw;
        }
    }

    public async Task<List<SyncConflict>> DetectConflictsAsync(string entityType, string entityId, object localData, object remoteData)
    {
        await Task.CompletedTask;
        try
        {
            _logger.LogInformation("Detecting conflicts for {EntityType}:{EntityId}", entityType, entityId);

            var conflicts = new List<SyncConflict>();
            var localJson = JsonSerializer.Serialize(localData);
            var remoteJson = JsonSerializer.Serialize(remoteData);
            var localDoc = JsonDocument.Parse(localJson);
            var remoteDoc = JsonDocument.Parse(remoteJson);

            foreach (var localProperty in localDoc.RootElement.EnumerateObject())
            {
                var propertyName = localProperty.Name;
                var localValue = localProperty.Value;

                if (remoteDoc.RootElement.TryGetProperty(propertyName, out var remoteValue))
                {
                    if (!JsonElementEquals(localValue, remoteValue))
                    {
                        conflicts.Add(new SyncConflict
                        {
                            EntityType = entityType,
                            EntityId = entityId,
                            PropertyName = propertyName,
                            LocalValue = ExtractValue(localValue),
                            RemoteValue = ExtractValue(remoteValue),
                            LocalTimestamp = DateTime.UtcNow.AddMinutes(-5), // TODO: Get actual timestamps
                            RemoteTimestamp = DateTime.UtcNow.AddMinutes(-2),
                            Type = ConflictType.UpdateUpdate,
                            Severity = DetermineConflictSeverity(propertyName, localValue, remoteValue),
                            Description = $"Conflict detected for property {propertyName}"
                        });
                    }
                }
                else
                {
                    // Property exists locally but not remotely
                    conflicts.Add(new SyncConflict
                    {
                        EntityType = entityType,
                        EntityId = entityId,
                        PropertyName = propertyName,
                        LocalValue = ExtractValue(localValue),
                        RemoteValue = null,
                        LocalTimestamp = DateTime.UtcNow.AddMinutes(-5),
                        RemoteTimestamp = DateTime.MinValue,
                        Type = ConflictType.UpdateUpdate,
                        Severity = ConflictSeverity.Low,
                        Description = $"Property {propertyName} exists locally but not remotely"
                    });
                }
            }

            // Check for properties that exist remotely but not locally
            foreach (var remoteProperty in remoteDoc.RootElement.EnumerateObject())
            {
                if (!localDoc.RootElement.TryGetProperty(remoteProperty.Name, out _))
                {
                    conflicts.Add(new SyncConflict
                    {
                        EntityType = entityType,
                        EntityId = entityId,
                        PropertyName = remoteProperty.Name,
                        LocalValue = null,
                        RemoteValue = ExtractValue(remoteProperty.Value),
                        LocalTimestamp = DateTime.MinValue,
                        RemoteTimestamp = DateTime.UtcNow.AddMinutes(-2),
                        Type = ConflictType.UpdateUpdate,
                        Severity = ConflictSeverity.Low,
                        Description = $"Property {remoteProperty.Name} exists remotely but not locally"
                    });
                }
            }

            _logger.LogInformation("Detected {ConflictCount} conflicts for {EntityType}:{EntityId}", 
                conflicts.Count, entityType, entityId);

            return conflicts;
        }
        catch (Exception ex)
        {
            var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<SyncStrategyService>();
            logger.LogError(ex, "Error detecting conflicts for {EntityType}:{EntityId}", entityType, entityId);
            throw;
        }
    }

    public async Task<ConflictResolution> ResolveConflictAsync(SyncConflict conflict, ConflictResolutionStrategy strategy)
    {
        await Task.CompletedTask;
        try
        {
            var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<SyncStrategyService>();
            logger.LogInformation("Resolving conflict {ConflictId} using strategy {Strategy}", 
                conflict.ConflictId, strategy);

            var resolution = strategy switch
            {
                ConflictResolutionStrategy.LocalWins => CreateLocalWinsResolution(conflict),
                ConflictResolutionStrategy.RemoteWins => CreateRemoteWinsResolution(conflict),
                ConflictResolutionStrategy.LastWriteWins => CreateLastWriteWinsResolution(conflict),
                ConflictResolutionStrategy.Merge => CreateMergeResolution(conflict),
                ConflictResolutionStrategy.UserChoice => CreateUserChoiceResolution(conflict),
                _ => CreateRemoteWinsResolution(conflict)
            };

            _logger.LogInformation("Conflict {ConflictId} resolved using {Strategy}", 
                conflict.ConflictId, resolution.Strategy);

            return resolution;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving conflict {ConflictId}", conflict.ConflictId);
            throw;
        }
    }

    public async Task<SyncDelta> CalculateDeltaAsync(string entityType, object localData, object remoteData)
    {
        await Task.CompletedTask;
        try
        {
            _logger.LogInformation("Calculating delta for {EntityType}", entityType);

            var localJson = JsonSerializer.Serialize(localData);
            var remoteJson = JsonSerializer.Serialize(remoteData);
            var localDoc = JsonDocument.Parse(localJson);
            var remoteDoc = JsonDocument.Parse(remoteJson);

            var propertyDeltas = new List<PropertyDelta>();

            // Find all differences
            foreach (var remoteProperty in remoteDoc.RootElement.EnumerateObject())
            {
                var propertyName = remoteProperty.Name;
                var remoteValue = remoteProperty.Value;

                if (localDoc.RootElement.TryGetProperty(propertyName, out var localValue))
                {
                    if (!JsonElementEquals(localValue, remoteValue))
                    {
                        propertyDeltas.Add(new PropertyDelta
                        {
                            PropertyName = propertyName,
                            OldValue = ExtractValue(localValue),
                            NewValue = ExtractValue(remoteValue),
                            Operation = DeltaOperation.Update,
                            ChangedAt = DateTime.UtcNow
                        });
                    }
                }
                else
                {
                    // Property added remotely
                    propertyDeltas.Add(new PropertyDelta
                    {
                        PropertyName = propertyName,
                        OldValue = null,
                        NewValue = ExtractValue(remoteValue),
                        Operation = DeltaOperation.Add,
                        ChangedAt = DateTime.UtcNow
                    });
                }
            }

            // Check for deleted properties
            foreach (var localProperty in localDoc.RootElement.EnumerateObject())
            {
                if (!remoteDoc.RootElement.TryGetProperty(localProperty.Name, out _))
                {
                    propertyDeltas.Add(new PropertyDelta
                    {
                        PropertyName = localProperty.Name,
                        OldValue = ExtractValue(localProperty.Value),
                        NewValue = null,
                        Operation = DeltaOperation.Delete,
                        ChangedAt = DateTime.UtcNow
                    });
                }
            }

            var delta = new SyncDelta
            {
                EntityType = entityType,
                EntityId = ExtractEntityId(localData),
                PropertyDeltas = propertyDeltas,
                Type = propertyDeltas.Count > 0 ? DeltaType.Partial : DeltaType.Full,
                BaseTimestamp = DateTime.UtcNow.AddHours(-1),
                TargetTimestamp = DateTime.UtcNow,
                DeltaSizeBytes = CalculateDataSize(propertyDeltas)
            };

            _logger.LogInformation("Calculated delta with {DeltaCount} changes for {EntityType}", 
                propertyDeltas.Count, entityType);

            return delta;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating delta for {EntityType}", entityType);
            throw;
        }
    }

    public async Task<object> ApplyDeltaAsync(object targetData, SyncDelta delta)
    {
        await Task.CompletedTask;
        try
        {
            _logger.LogInformation("Applying delta to {EntityType}:{EntityId}", delta.EntityType, delta.EntityId);

            var targetJson = JsonSerializer.Serialize(targetData);
            var targetDoc = JsonDocument.Parse(targetJson);
            var mutableTarget = JsonSerializer.Deserialize<Dictionary<string, object>>(targetJson)!;

            foreach (var propertyDelta in delta.PropertyDeltas)
            {
                switch (propertyDelta.Operation)
                {
                    case DeltaOperation.Add:
                    case DeltaOperation.Update:
                    case DeltaOperation.Replace:
                        if (propertyDelta.NewValue != null)
                        {
                            mutableTarget[propertyDelta.PropertyName] = propertyDelta.NewValue;
                        }
                        break;

                    case DeltaOperation.Delete:
                        mutableTarget.Remove(propertyDelta.PropertyName);
                        break;
                }
            }

            _logger.LogInformation("Applied {DeltaCount} changes to {EntityType}:{EntityId}", 
                delta.PropertyDeltas.Count, delta.EntityType, delta.EntityId);

            return mutableTarget;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying delta to {EntityType}:{EntityId}", delta.EntityType, delta.EntityId);
            throw;
        }
    }

    public async Task<SyncStrategyType> GetStrategyTypeAsync(string entityType)
    {
        await Task.CompletedTask;
        try
        {
            if (_entityStrategies.TryGetValue(entityType, out var existingStrategy))
            {
                return existingStrategy;
            }

            // Default strategy based on entity type pattern
            var strategy = entityType.ToLower(CultureInfo.InvariantCulture) switch
            {
                var s when s.Contains("inventory", StringComparison.OrdinalIgnoreCase) => SyncStrategyType.DeltaSync,
                var s when s.Contains("order", StringComparison.OrdinalIgnoreCase) => SyncStrategyType.ConflictResolution,
                var s when s.Contains("customer", StringComparison.OrdinalIgnoreCase) => SyncStrategyType.FullSync,
                var s when s.Contains("product", StringComparison.OrdinalIgnoreCase) => SyncStrategyType.DeltaSync,
                _ => SyncStrategyType.FullSync
            };
            return strategy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting strategy type for {EntityType}", entityType);
            return SyncStrategyType.FullSync;
        }
    }

    public async Task<DataIntegrityResult> ValidateDataIntegrityAsync(string entityType, object data)
    {
        await Task.CompletedTask;
        try
        {
            _logger.LogInformation("Validating data integrity for {EntityType}", entityType);

            var violations = new List<IntegrityViolation>();
            var dataJson = JsonSerializer.Serialize(data);

            // Check for required fields based on entity type
            var requiredFields = GetRequiredFields(entityType);
            var dataDoc = JsonDocument.Parse(dataJson);

            foreach (var field in requiredFields)
            {
                if (!dataDoc.RootElement.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null)
                {
                    violations.Add(new IntegrityViolation
                    {
                        PropertyName = field,
                        ViolationType = "RequiredFieldMissing",
                        Description = $"Required field '{field}' is missing or null",
                        Severity = ViolationSeverity.Error
                    });
                }
            }

            // Calculate checksum
            var checksum = CalculateChecksum(dataJson);

            var result = new DataIntegrityResult
            {
                IsValid = violations.Count == 0,
                EntityType = entityType,
                Violations = violations,
                ValidatedAt = DateTime.UtcNow,
                Checksum = checksum
            };

            _logger.LogInformation("Data integrity validation completed for {EntityType}: {IsValid}", 
                entityType, result.IsValid);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating data integrity for {EntityType}", entityType);
            throw;
        }
    }

    #region Private Helper Methods

    private static Dictionary<string, SyncStrategyType> InitializeEntityStrategies()
    {
        return new Dictionary<string, SyncStrategyType>
        {
            ["Order"] = SyncStrategyType.ConflictResolution,
            ["Customer"] = SyncStrategyType.FullSync,
            ["Inventory"] = SyncStrategyType.DeltaSync,
            ["Product"] = SyncStrategyType.DeltaSync,
            ["UserPreferences"] = SyncStrategyType.RealTimeSync,
            ["OrderStatus"] = SyncStrategyType.RealTimeSync,
            ["InventoryStatus"] = SyncStrategyType.RealTimeSync
        };
    }

    private static SyncResult CreateFailureResult(SyncRequest request, string error, TimeSpan duration)
    {
        return new SyncResult
        {
            Success = false,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            Outcome = SyncOutcome.RequiresIntervention,
            Errors = new List<string> { error },
            SyncTimestamp = DateTime.UtcNow,
            Duration = duration,
            StrategyUsed = SyncStrategyType.FullSync
        };
    }

    private static SyncResult CreateConflictResult(SyncRequest request, List<SyncConflict> conflicts, TimeSpan duration)
    {
        return new SyncResult
        {
            Success = false,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            Outcome = SyncOutcome.RequiresIntervention,
            ResolvedConflicts = conflicts,
            SyncTimestamp = DateTime.UtcNow,
            Duration = duration,
            StrategyUsed = SyncStrategyType.ConflictResolution
        };
    }

    private static SyncResult PerformFullSync(SyncRequest request)
    {
        // For full sync, we simply use the remote data if available, otherwise keep local
        var syncedData = request.RemoteData ?? request.LocalData;
        
        return new SyncResult
        {
            Success = true,
            SyncedData = syncedData
        };
    }

    private async Task<SyncResult> PerformDeltaSyncAsync(SyncRequest request)
    {
        if (request.RemoteData == null)
        {
            return new SyncResult { Success = true, SyncedData = request.LocalData };
        }

        var delta = await CalculateDeltaAsync(request.EntityType, request.LocalData, request.RemoteData);
        var syncedData = await ApplyDeltaAsync(request.LocalData, delta);
        
        return new SyncResult
        {
            Success = true,
            SyncedData = syncedData
        };
    }

    private SyncResult PerformConflictResolution(SyncRequest request, List<SyncConflict> conflicts)
    {
        var resolution = conflicts.Count > 0 ? CreateLocalWinsResolution(conflicts[0]) : new ConflictResolution();
        
        // Apply conflict resolution to merge local and remote data
        var syncedData = MergeDataWithResolution(request.LocalData, request.RemoteData, resolution);
        
        return new SyncResult
        {
            Success = true,
            SyncedData = syncedData,
            ResolvedConflicts = conflicts
        };
    }
    
    private object MergeDataWithResolution(object localData, object remoteData, ConflictResolution resolution)
    {
        // Implement proper data merging based on resolution strategy
        switch (resolution.Strategy)
        {
            case ConflictResolutionStrategy.LocalWins:
                return localData;
            case ConflictResolutionStrategy.RemoteWins:
                return remoteData;
            case ConflictResolutionStrategy.Merge:
                // Implement intelligent merge logic
                return localData; // Simplified - would need proper merging
            default:
                return localData;
        }
    }

    private static ConflictResolution CreateLocalWinsResolution(SyncConflict conflict)
    {
        return new ConflictResolution
        {
            ConflictId = conflict.ConflictId,
            Strategy = ConflictResolutionStrategy.LocalWins,
            ResolvedValue = conflict.LocalValue,
            RequiresUserIntervention = false,
            ResolutionDescription = "Local version selected"
        };
    }

    private static ConflictResolution CreateRemoteWinsResolution(SyncConflict conflict)
    {
        return new ConflictResolution
        {
            ConflictId = conflict.ConflictId,
            Strategy = ConflictResolutionStrategy.RemoteWins,
            ResolvedValue = conflict.RemoteValue,
            RequiresUserIntervention = false,
            ResolutionDescription = "Remote version selected"
        };
    }

    private static ConflictResolution CreateLastWriteWinsResolution(SyncConflict conflict)
    {
        var resolvedValue = conflict.RemoteTimestamp > conflict.LocalTimestamp ? conflict.RemoteValue : conflict.LocalValue;
        var strategy = conflict.RemoteTimestamp > conflict.LocalTimestamp ? ConflictResolutionStrategy.RemoteWins : ConflictResolutionStrategy.LocalWins;

        return new ConflictResolution
        {
            ConflictId = conflict.ConflictId,
            Strategy = ConflictResolutionStrategy.LastWriteWins,
            ResolvedValue = resolvedValue,
            RequiresUserIntervention = false,
            ResolutionDescription = $"Most recent version selected ({strategy})"
        };
    }

    private static ConflictResolution CreateMergeResolution(SyncConflict conflict)
    {
        // Simple merge logic - in production, this would be more sophisticated
        return new ConflictResolution
        {
            ConflictId = conflict.ConflictId,
            Strategy = ConflictResolutionStrategy.Merge,
            ResolvedValue = conflict.RemoteValue, // Default to remote for now
            RequiresUserIntervention = true,
            ResolutionDescription = "Manual merge required"
        };
    }

    private static ConflictResolution CreateUserChoiceResolution(SyncConflict conflict)
    {
        return new ConflictResolution
        {
            ConflictId = conflict.ConflictId,
            Strategy = ConflictResolutionStrategy.UserChoice,
            RequiresUserIntervention = true,
            ResolutionDescription = "User intervention required"
        };
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
            JsonValueKind.Number => element.TryGetInt32(out var intValue) ? (object)intValue : element.GetDouble(),
            JsonValueKind.True or JsonValueKind.False => element.GetBoolean(),
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    private static ConflictSeverity DetermineConflictSeverity(string propertyName, JsonElement localValue, JsonElement remoteValue)
    {
        // Critical fields that require immediate attention
        var criticalFields = new[] { "Status", "PaymentStatus", "OrderId", "CustomerId" };
        
        if (criticalFields.Contains(propertyName))
        {
            return ConflictSeverity.High;
        }

        // Financial fields are medium severity
        var financialFields = new[] { "Total", "Price", "Amount", "Tax" };
        if (financialFields.Contains(propertyName))
        {
            return ConflictSeverity.Medium;
        }

        return ConflictSeverity.Low;
    }

    private static long CalculateDataSize(object? data)
    {
        if (data == null) return 0;
        var json = JsonSerializer.Serialize(data);
        return System.Text.Encoding.UTF8.GetByteCount(json);
    }

    private static string ExtractEntityId(object data)
    {
        if (data is JsonElement element)
        {
            if (element.TryGetProperty("Id", out var idProp) || 
                element.TryGetProperty("EntityId", out idProp) ||
                element.TryGetProperty($"{data.GetType().Name}Id", out idProp))
            {
                return idProp.GetString() ?? Guid.NewGuid().ToString();
            }
        }
        return Guid.NewGuid().ToString();
    }

    private static List<string> GetRequiredFields(string entityType)
    {
        return entityType.ToLower(CultureInfo.InvariantCulture) switch
        {
            "order" => new List<string> { "OrderId", "CustomerId", "Total" },
            "customer" => new List<string> { "CustomerId", "Name" },
            "inventory" => new List<string> { "ProductId", "Quantity" },
            "product" => new List<string> { "ProductId", "Name", "Price" },
            _ => new List<string>()
        };
    }

    private static string CalculateChecksum(string data)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(data)));
    }

    #endregion
}
