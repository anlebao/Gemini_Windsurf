using VanAn.Shared.Omnichannel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Implementation of sync strategy for multi-device data consistency
    /// Provides comprehensive conflict resolution and delta synchronization
    /// </summary>
    public class SyncStrategyService(ILogger<SyncStrategyService> logger, IMemoryCache cache) : ISyncStrategy
    {
        private readonly ILogger<SyncStrategyService> _logger = logger;
        private readonly IMemoryCache _cache = cache;
        private readonly Dictionary<string, SyncStrategyType> _entityStrategies = InitializeEntityStrategies();

        public async Task<SyncResult> SynchronizeAsync(SyncRequest request)
        {
            try
            {
                _logger.LogInformation("Starting synchronization for {EntityType}:{EntityId} using {Strategy}",
                    request.EntityType, request.EntityId, request.Priority);

                System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Get strategy type for entity
                SyncStrategyType strategyType = await GetStrategyTypeAsync(request.EntityType);

                // Validate data integrity
                DataIntegrityResult integrityValidation = await ValidateDataIntegrityAsync(request.EntityType, request.LocalData);
                if (!integrityValidation.IsValid)
                {
                    _logger.LogWarning("Data integrity validation failed for {EntityType}:{EntityId}",
                        request.EntityType, request.EntityId);
                    return CreateFailureResult(request, "Data integrity validation failed", stopwatch.Elapsed);
                }

                // Detect conflicts
                List<SyncConflict> conflicts = [];
                if (request.RemoteData != null)
                {
                    conflicts = await DetectConflictsAsync(request.EntityType, request.EntityId, request.LocalData, request.RemoteData);
                }

                // Resolve conflicts
                List<SyncConflict> resolvedConflicts = [];
                foreach (SyncConflict conflict in conflicts)
                {
                    ConflictResolution resolution = await ResolveConflictAsync(conflict, ConflictResolutionStrategy.LastWriteWins);
                    if (resolution.RequiresUserIntervention)
                    {
                        return CreateConflictResult(request, conflicts, stopwatch.Elapsed);
                    }
                    resolvedConflicts.Add(conflict);
                }

                // Apply synchronization based on strategy
                SyncResult result = strategyType switch
                {
                    SyncStrategyType.FullSync => PerformFullSync(request),
                    SyncStrategyType.DeltaSync => await PerformDeltaSyncAsync(request),
                    SyncStrategyType.ConflictResolution => PerformConflictResolution(request, conflicts),
                    SyncStrategyType.PrioritySync => throw new NotImplementedException(),
                    SyncStrategyType.BatchSync => throw new NotImplementedException(),
                    SyncStrategyType.RealTimeSync => throw new NotImplementedException(),
                    _ => PerformFullSync(request)
                };

                stopwatch.Stop();

                SyncResult syncResult = new()
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

                List<SyncConflict> conflicts = [];
                string localJson = JsonSerializer.Serialize(localData);
                string remoteJson = JsonSerializer.Serialize(remoteData);
                JsonDocument localDoc = JsonDocument.Parse(localJson);
                JsonDocument remoteDoc = JsonDocument.Parse(remoteJson);

                foreach (JsonProperty localProperty in localDoc.RootElement.EnumerateObject())
                {
                    string propertyName = localProperty.Name;
                    JsonElement localValue = localProperty.Value;

                    if (remoteDoc.RootElement.TryGetProperty(propertyName, out JsonElement remoteValue))
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
                foreach (JsonProperty remoteProperty in remoteDoc.RootElement.EnumerateObject())
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
                ILogger<SyncStrategyService> logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<SyncStrategyService>();
                logger.LogError(ex, "Error detecting conflicts for {EntityType}:{EntityId}", entityType, entityId);
                throw;
            }
        }

        public async Task<ConflictResolution> ResolveConflictAsync(SyncConflict conflict, ConflictResolutionStrategy strategy)
        {
            await Task.CompletedTask;
            try
            {
                ILogger<SyncStrategyService> logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<SyncStrategyService>();
                logger.LogInformation("Resolving conflict {ConflictId} using strategy {Strategy}",
                    conflict.ConflictId, strategy);

                ConflictResolution resolution = strategy switch
                {
                    ConflictResolutionStrategy.LocalWins => CreateLocalWinsResolution(conflict),
                    ConflictResolutionStrategy.RemoteWins => CreateRemoteWinsResolution(conflict),
                    ConflictResolutionStrategy.LastWriteWins => CreateLastWriteWinsResolution(conflict),
                    ConflictResolutionStrategy.Merge => CreateMergeResolution(conflict),
                    ConflictResolutionStrategy.UserChoice => CreateUserChoiceResolution(conflict),
                    ConflictResolutionStrategy.Skip => throw new NotImplementedException(),
                    ConflictResolutionStrategy.CreateBoth => throw new NotImplementedException(),
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

                string localJson = JsonSerializer.Serialize(localData);
                string remoteJson = JsonSerializer.Serialize(remoteData);
                JsonDocument localDoc = JsonDocument.Parse(localJson);
                JsonDocument remoteDoc = JsonDocument.Parse(remoteJson);

                List<PropertyDelta> propertyDeltas = [];

                // Find all differences
                foreach (JsonProperty remoteProperty in remoteDoc.RootElement.EnumerateObject())
                {
                    string propertyName = remoteProperty.Name;
                    JsonElement remoteValue = remoteProperty.Value;

                    if (localDoc.RootElement.TryGetProperty(propertyName, out JsonElement localValue))
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
                foreach (JsonProperty localProperty in localDoc.RootElement.EnumerateObject())
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

                SyncDelta delta = new()
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

                string targetJson = JsonSerializer.Serialize(targetData);
                JsonDocument targetDoc = JsonDocument.Parse(targetJson);
                Dictionary<string, object> mutableTarget = JsonSerializer.Deserialize<Dictionary<string, object>>(targetJson)!;

                foreach (PropertyDelta propertyDelta in delta.PropertyDeltas)
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
                        default:
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
                if (_entityStrategies.TryGetValue(entityType, out SyncStrategyType existingStrategy))
                {
                    return existingStrategy;
                }

                // Default strategy based on entity type pattern
                SyncStrategyType strategy = entityType.ToLower(CultureInfo.InvariantCulture) switch
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

                List<IntegrityViolation> violations = [];
                string dataJson = JsonSerializer.Serialize(data);

                // Check for required fields based on entity type
                List<string> requiredFields = GetRequiredFields(entityType);
                JsonDocument dataDoc = JsonDocument.Parse(dataJson);

                foreach (string field in requiredFields)
                {
                    if (!dataDoc.RootElement.TryGetProperty(field, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
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
                string checksum = CalculateChecksum(dataJson);

                DataIntegrityResult result = new()
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
                Errors = [error],
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
            object syncedData = request.RemoteData ?? request.LocalData;

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

            SyncDelta delta = await CalculateDeltaAsync(request.EntityType, request.LocalData, request.RemoteData);
            object syncedData = await ApplyDeltaAsync(request.LocalData, delta);

            return new SyncResult
            {
                Success = true,
                SyncedData = syncedData
            };
        }

        private static SyncResult PerformConflictResolution(SyncRequest request, List<SyncConflict> conflicts)
        {
            ConflictResolution resolution = conflicts.Count > 0 ? CreateLocalWinsResolution(conflicts[0]) : new ConflictResolution();

            // Apply conflict resolution to merge local and remote data
            object syncedData = MergeDataWithResolution(request.LocalData, request.RemoteData, resolution);

            return new SyncResult
            {
                Success = true,
                SyncedData = syncedData,
                ResolvedConflicts = conflicts
            };
        }

        private static object MergeDataWithResolution(object localData, object remoteData, ConflictResolution resolution)
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
                case ConflictResolutionStrategy.LastWriteWins:
                    return localData;
                case ConflictResolutionStrategy.UserChoice:
                    return localData;
                case ConflictResolutionStrategy.Skip:
                    return localData;
                case ConflictResolutionStrategy.CreateBoth:
                    return localData;
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
            object? resolvedValue = conflict.RemoteTimestamp > conflict.LocalTimestamp ? conflict.RemoteValue : conflict.LocalValue;
            ConflictResolutionStrategy strategy = conflict.RemoteTimestamp > conflict.LocalTimestamp ? ConflictResolutionStrategy.RemoteWins : ConflictResolutionStrategy.LocalWins;

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
                JsonValueKind.Number => element.TryGetInt32(out int intValue) ? (object)intValue : element.GetDouble(),
                JsonValueKind.True or JsonValueKind.False => element.GetBoolean(),
                JsonValueKind.Null => null,
                JsonValueKind.Undefined => throw new NotImplementedException(),
                JsonValueKind.Object => throw new NotImplementedException(),
                JsonValueKind.Array => throw new NotImplementedException(),
                _ => element.GetRawText()
            };
        }

        private static ConflictSeverity DetermineConflictSeverity(string propertyName, JsonElement localValue, JsonElement remoteValue)
        {
            // Critical fields that require immediate attention
            string[] criticalFields = ["Status", "PaymentStatus", "OrderId", "CustomerId"];

            if (criticalFields.Contains(propertyName))
            {
                return ConflictSeverity.High;
            }

            // Financial fields are medium severity
            string[] financialFields = ["Total", "Price", "Amount", "Tax"];
            return financialFields.Contains(propertyName) ? ConflictSeverity.Medium : ConflictSeverity.Low;
        }

        private static long CalculateDataSize(object? data)
        {
            if (data == null)
            {
                return 0;
            }

            string json = JsonSerializer.Serialize(data);
            return Encoding.UTF8.GetByteCount(json);
        }

        private static string ExtractEntityId(object data)
        {
            if (data is JsonElement element)
            {
                if (element.TryGetProperty("Id", out JsonElement idProp) ||
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
                "order" => ["OrderId", "CustomerId", "Total"],
                "customer" => ["CustomerId", "Name"],
                "inventory" => ["ProductId", "Quantity"],
                "product" => ["ProductId", "Name", "Price"],
                _ => []
            };
        }

        private static string CalculateChecksum(string data)
        {
            return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(data)));
        }

        #endregion
    }
}
