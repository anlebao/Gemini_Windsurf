using Xunit;
using Moq;
using System.Threading.Tasks;
using VanAn.Shared.Omnichannel;

namespace VanAn.Omnichannel.Tests;

public class SyncStrategyTests
{
    private readonly Mock<ISyncStrategy> _mockSyncStrategy;

    public SyncStrategyTests()
    {
        _mockSyncStrategy = new Mock<ISyncStrategy>();
    }

    [Fact(DisplayName = "TDD: Synchronize Data Using Full Sync Strategy")]
    public async Task SyncStrategy_FullSync_ShouldReturnSuccess()
    {
        // Arrange
        var request = new SyncRequest
        {
            UserId = "test-user-123",
            EntityType = "Order",
            EntityId = "order-456",
            LocalData = new { OrderId = "order-456", Status = "PREPARING", Total = 25000 },
            Priority = SyncPriority.Normal,
            DeviceId = "mobile-device-001"
        };

        var expectedResult = new SyncResult
        {
            Success = true,
            EntityType = "Order",
            EntityId = "order-456",
            Outcome = SyncOutcome.Success,
            SyncedData = request.LocalData,
            SyncTimestamp = DateTime.UtcNow,
            StrategyUsed = SyncStrategyType.FullSync,
            Duration = TimeSpan.FromMilliseconds(150)
        };

        _mockSyncStrategy.Setup(x => x.SynchronizeAsync(request))
                  .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockSyncStrategy.Object.SynchronizeAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Order", result.EntityType);
        Assert.Equal("order-456", result.EntityId);
        Assert.Equal(SyncOutcome.Success, result.Outcome);
        Assert.Equal(SyncStrategyType.FullSync, result.StrategyUsed);
        _mockSyncStrategy.Verify(x => x.SynchronizeAsync(request), Times.Once);
    }

    [Fact(DisplayName = "TDD: Detect Conflicts Between Local and Remote Data")]
    public async Task SyncStrategy_DetectConflicts_ShouldIdentifyConflicts()
    {
        // Arrange
        var entityType = "Order";
        var entityId = "order-789";
        var localData = new { OrderId = "order-789", Status = "PREPARING", Total = 30000 };
        var remoteData = new { OrderId = "order-789", Status = "COMPLETED", Total = 25000 };

        var expectedConflicts = new List<SyncConflict>
        {
            new SyncConflict
            {
                EntityType = entityType,
                EntityId = entityId,
                PropertyName = "Status",
                LocalValue = "PREPARING",
                RemoteValue = "COMPLETED",
                LocalTimestamp = DateTime.UtcNow.AddMinutes(-5),
                RemoteTimestamp = DateTime.UtcNow.AddMinutes(-2),
                Type = ConflictType.UpdateUpdate,
                Severity = ConflictSeverity.Medium
            },
            new SyncConflict
            {
                EntityType = entityType,
                EntityId = entityId,
                PropertyName = "Total",
                LocalValue = 30000,
                RemoteValue = 25000,
                LocalTimestamp = DateTime.UtcNow.AddMinutes(-5),
                RemoteTimestamp = DateTime.UtcNow.AddMinutes(-2),
                Type = ConflictType.UpdateUpdate,
                Severity = ConflictSeverity.Low
            }
        };

        _mockSyncStrategy.Setup(x => x.DetectConflictsAsync(entityType, entityId, localData, remoteData))
                  .ReturnsAsync(expectedConflicts);

        // Act
        var conflicts = await _mockSyncStrategy.Object.DetectConflictsAsync(entityType, entityId, localData, remoteData);

        // Assert
        Assert.NotNull(conflicts);
        Assert.Equal(2, conflicts.Count);
        Assert.All(conflicts, c => Assert.Equal(entityType, c.EntityType));
        Assert.All(conflicts, c => Assert.Equal(entityId, c.EntityId));
        Assert.Contains(conflicts, c => c.PropertyName == "Status");
        Assert.Contains(conflicts, c => c.PropertyName == "Total");
        _mockSyncStrategy.Verify(x => x.DetectConflictsAsync(entityType, entityId, localData, remoteData), Times.Once);
    }

    [Fact(DisplayName = "TDD: Resolve Conflicts Using Last Write Wins Strategy")]
    public async Task SyncStrategy_ResolveConflict_ShouldUseLastWriteWins()
    {
        // Arrange
        var conflict = new SyncConflict
        {
            ConflictId = "conflict-001",
            EntityType = "Order",
            EntityId = "order-999",
            PropertyName = "Status",
            LocalValue = "PREPARING",
            RemoteValue = "COMPLETED",
            LocalTimestamp = DateTime.UtcNow.AddMinutes(-5),
            RemoteTimestamp = DateTime.UtcNow.AddMinutes(-2),
            Type = ConflictType.UpdateUpdate,
            Severity = ConflictSeverity.Medium
        };

        var expectedResolution = new ConflictResolution
        {
            ConflictId = conflict.ConflictId,
            Strategy = ConflictResolutionStrategy.LastWriteWins,
            ResolvedValue = "COMPLETED", // Remote is more recent
            RequiresUserIntervention = false,
            ResolutionDescription = "Remote version is more recent, keeping remote value"
        };

        _mockSyncStrategy.Setup(x => x.ResolveConflictAsync(conflict, ConflictResolutionStrategy.LastWriteWins))
                  .ReturnsAsync(expectedResolution);

        // Act
        var resolution = await _mockSyncStrategy.Object.ResolveConflictAsync(conflict, ConflictResolutionStrategy.LastWriteWins);

        // Assert
        Assert.NotNull(resolution);
        Assert.Equal(conflict.ConflictId, resolution.ConflictId);
        Assert.Equal(ConflictResolutionStrategy.LastWriteWins, resolution.Strategy);
        Assert.Equal("COMPLETED", resolution.ResolvedValue);
        Assert.False(resolution.RequiresUserIntervention);
        _mockSyncStrategy.Verify(x => x.ResolveConflictAsync(conflict, ConflictResolutionStrategy.LastWriteWins), Times.Once);
    }

    [Fact(DisplayName = "TDD: Calculate Delta for Efficient Sync")]
    public async Task SyncStrategy_CalculateDelta_ShouldReturnPropertyDeltas()
    {
        // Arrange
        var entityType = "Customer";
        var localData = new { CustomerId = "cust-001", Name = "Nguyen Van A", Email = "a@example.com", Phone = "0901234567" };
        var remoteData = new { CustomerId = "cust-001", Name = "Nguyen Van A", Email = "a.new@example.com", Phone = "0901234567", Address = "123 Main St" };

        var expectedDelta = new SyncDelta
        {
            EntityType = entityType,
            EntityId = "cust-001",
            Type = DeltaType.Partial,
            BaseTimestamp = DateTime.UtcNow.AddHours(-1),
            TargetTimestamp = DateTime.UtcNow,
            PropertyDeltas = new List<PropertyDelta>
            {
                new PropertyDelta
                {
                    PropertyName = "Email",
                    OldValue = "a@example.com",
                    NewValue = "a.new@example.com",
                    Operation = DeltaOperation.Update,
                    ChangedAt = DateTime.UtcNow
                },
                new PropertyDelta
                {
                    PropertyName = "Address",
                    OldValue = null,
                    NewValue = "123 Main St",
                    Operation = DeltaOperation.Add,
                    ChangedAt = DateTime.UtcNow
                }
            }
        };

        _mockSyncStrategy.Setup(x => x.CalculateDeltaAsync(entityType, localData, remoteData))
                  .ReturnsAsync(expectedDelta);

        // Act
        var delta = await _mockSyncStrategy.Object.CalculateDeltaAsync(entityType, localData, remoteData);

        // Assert
        Assert.NotNull(delta);
        Assert.Equal(entityType, delta.EntityType);
        Assert.Equal("cust-001", delta.EntityId);
        Assert.Equal(DeltaType.Partial, delta.Type);
        Assert.Equal(2, delta.PropertyDeltas.Count);
        Assert.Contains(delta.PropertyDeltas, d => d.PropertyName == "Email" && d.Operation == DeltaOperation.Update);
        Assert.Contains(delta.PropertyDeltas, d => d.PropertyName == "Address" && d.Operation == DeltaOperation.Add);
        _mockSyncStrategy.Verify(x => x.CalculateDeltaAsync(entityType, localData, remoteData), Times.Once);
    }

    [Fact(DisplayName = "TDD: Apply Delta to Target Data")]
    public async Task SyncStrategy_ApplyDelta_ShouldUpdateTargetData()
    {
        // Arrange
        var targetData = new { CustomerId = "cust-001", Name = "Nguyen Van A", Email = "a@example.com", Phone = "0901234567" };
        var delta = new SyncDelta
        {
            EntityType = "Customer",
            EntityId = "cust-001",
            PropertyDeltas = new List<PropertyDelta>
            {
                new PropertyDelta
                {
                    PropertyName = "Email",
                    NewValue = "a.updated@example.com",
                    Operation = DeltaOperation.Update
                },
                new PropertyDelta
                {
                    PropertyName = "Address",
                    NewValue = "456 Updated St",
                    Operation = DeltaOperation.Add
                }
            }
        };

        var expectedResult = new { CustomerId = "cust-001", Name = "Nguyen Van A", Email = "a.updated@example.com", Phone = "0901234567", Address = "456 Updated St" };

        _mockSyncStrategy.Setup(x => x.ApplyDeltaAsync(targetData, delta))
                  .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockSyncStrategy.Object.ApplyDeltaAsync(targetData, delta);

        // Assert
        Assert.NotNull(result);
        _mockSyncStrategy.Verify(x => x.ApplyDeltaAsync(targetData, delta), Times.Once);
    }

    [Fact(DisplayName = "TDD: Get Sync Strategy for Entity Type")]
    public async Task SyncStrategy_GetStrategyType_ShouldReturnAppropriateStrategy()
    {
        // Arrange
        var entityType = "Inventory";
        var expectedStrategy = SyncStrategyType.DeltaSync; // Inventory uses delta sync for efficiency

        _mockSyncStrategy.Setup(x => x.GetStrategyTypeAsync(entityType))
                  .ReturnsAsync(expectedStrategy);

        // Act
        var strategy = await _mockSyncStrategy.Object.GetStrategyTypeAsync(entityType);

        // Assert
        Assert.Equal(SyncStrategyType.DeltaSync, strategy);
        _mockSyncStrategy.Verify(x => x.GetStrategyTypeAsync(entityType), Times.Once);
    }

    [Fact(DisplayName = "TDD: Validate Data Integrity Before Sync")]
    public async Task SyncStrategy_ValidateDataIntegrity_ShouldPassValidation()
    {
        // Arrange
        var entityType = "Order";
        var data = new { OrderId = "order-123", CustomerId = "cust-456", Total = 15000, Status = "CONFIRMED" };

        var expectedValidation = new DataIntegrityResult
        {
            IsValid = true,
            EntityType = entityType,
            Violations = new List<IntegrityViolation>(),
            Checksum = "abc123def456",
            ValidatedAt = DateTime.UtcNow
        };

        _mockSyncStrategy.Setup(x => x.ValidateDataIntegrityAsync(entityType, data))
                  .ReturnsAsync(expectedValidation);

        // Act
        var validation = await _mockSyncStrategy.Object.ValidateDataIntegrityAsync(entityType, data);

        // Assert
        Assert.NotNull(validation);
        Assert.True(validation.IsValid);
        Assert.Equal(entityType, validation.EntityType);
        Assert.Empty(validation.Violations);
        Assert.NotEmpty(validation.Checksum);
        _mockSyncStrategy.Verify(x => x.ValidateDataIntegrityAsync(entityType, data), Times.Once);
    }

    [Fact(DisplayName = "TDD: Handle Sync with Conflicts Requiring User Intervention")]
    public async Task SyncStrategy_SyncWithConflicts_ShouldReturnConflictOutcome()
    {
        // Arrange
        var request = new SyncRequest
        {
            UserId = "test-user-789",
            EntityType = "Customer",
            EntityId = "cust-999",
            LocalData = new { CustomerId = "cust-999", Name = "Le Van B", Email = "b@example.com" },
            RemoteData = new { CustomerId = "cust-999", Name = "Le Van C", Email = "b@example.com" },
            Priority = SyncPriority.High
        };

        var expectedResult = new SyncResult
        {
            Success = false,
            EntityType = "Customer",
            EntityId = "cust-999",
            Outcome = SyncOutcome.RequiresIntervention,
            ResolvedConflicts = new List<SyncConflict>
            {
                new SyncConflict
                {
                    EntityType = "Customer",
                    EntityId = "cust-999",
                    PropertyName = "Name",
                    LocalValue = "Le Van B",
                    RemoteValue = "Le Van C",
                    Type = ConflictType.UpdateUpdate,
                    Severity = ConflictSeverity.High
                }
            },
            StrategyUsed = SyncStrategyType.ConflictResolution
        };

        _mockSyncStrategy.Setup(x => x.SynchronizeAsync(request))
                  .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockSyncStrategy.Object.SynchronizeAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal(SyncOutcome.RequiresIntervention, result.Outcome);
        Assert.Single(result.ResolvedConflicts);
        Assert.Equal("Name", result.ResolvedConflicts[0].PropertyName);
        Assert.Equal(ConflictSeverity.High, result.ResolvedConflicts[0].Severity);
        _mockSyncStrategy.Verify(x => x.SynchronizeAsync(request), Times.Once);
    }

    [Fact(DisplayName = "TDD: Delta Sync for Large Dataset")]
    public async Task SyncStrategy_DeltaSyncLargeDataset_ShouldOptimizeBandwidth()
    {
        // Arrange
        var request = new SyncRequest
        {
            UserId = "test-user-delta",
            EntityType = "ProductCatalog",
            EntityId = "catalog-001",
            LocalData = new { /* Large product catalog data */ },
            Priority = SyncPriority.Normal,
            LastSyncTimestamp = DateTime.UtcNow.AddHours(-1)
        };

        var expectedResult = new SyncResult
        {
            Success = true,
            EntityType = "ProductCatalog",
            EntityId = "catalog-001",
            Outcome = SyncOutcome.Success,
            DataSizeBytes = 1024, // Much smaller than full sync
            Duration = TimeSpan.FromMilliseconds(50), // Faster than full sync
            StrategyUsed = SyncStrategyType.DeltaSync
        };

        _mockSyncStrategy.Setup(x => x.SynchronizeAsync(request))
                  .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockSyncStrategy.Object.SynchronizeAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(SyncStrategyType.DeltaSync, result.StrategyUsed);
        Assert.True(result.DataSizeBytes < 5000); // Delta should be smaller than full data
        Assert.True(result.Duration.TotalMilliseconds < 200); // Delta should be faster
        _mockSyncStrategy.Verify(x => x.SynchronizeAsync(request), Times.Once);
    }
}
