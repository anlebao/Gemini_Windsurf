using Xunit;
using Moq;
using System.Threading.Tasks;
using VanAn.Shared.Omnichannel;

namespace VanAn.Omnichannel.Tests;

public class DataVersioningTests
{
    private readonly Mock<IDataVersioning> _mockDataVersioning;

    public DataVersioningTests()
    {
        _mockDataVersioning = new Mock<IDataVersioning>();
    }

    [Fact(DisplayName = "TDD: Create New Version of Entity Data")]
    public async Task DataVersioning_CreateVersion_ShouldReturnVersionWithMetadata()
    {
        // Arrange
        var entityType = "Order";
        var entityId = "order-123";
        var data = new { OrderId = "order-123", CustomerId = "cust-456", Total = 25000, Status = "CONFIRMED" };
        var userId = "user-789";
        var deviceId = "mobile-001";

        var expectedVersion = new DataVersion
        {
            VersionId = "version-001",
            EntityType = entityType,
            EntityId = entityId,
            VersionNumber = 1,
            Data = data,
            DataHash = "hash123",
            CreatedBy = userId,
            CreatedByDevice = deviceId,
            CreatedAt = DateTime.UtcNow,
            Type = VersionType.Create,
            Comment = "Initial order creation",
            DataSizeBytes = 1024,
            IsDeleted = false
        };

        _mockDataVersioning.Setup(x => x.CreateVersionAsync(entityType, entityId, data, userId, deviceId))
                  .ReturnsAsync(expectedVersion);

        // Act
        var version = await _mockDataVersioning.Object.CreateVersionAsync(entityType, entityId, data, userId, deviceId);

        // Assert
        Assert.NotNull(version);
        Assert.Equal(entityType, version.EntityType);
        Assert.Equal(entityId, version.EntityId);
        Assert.Equal(1, version.VersionNumber);
        Assert.Equal(userId, version.CreatedBy);
        Assert.Equal(deviceId, version.CreatedByDevice);
        Assert.Equal(VersionType.Create, version.Type);
        Assert.False(version.IsDeleted);
        Assert.NotEmpty(version.DataHash);
        _mockDataVersioning.Verify(x => x.CreateVersionAsync(entityType, entityId, data, userId, deviceId), Times.Once);
    }

    [Fact(DisplayName = "TDD: Get Current Version of Entity")]
    public async Task DataVersioning_GetCurrentVersion_ShouldReturnLatestVersion()
    {
        // Arrange
        var entityType = "Customer";
        var entityId = "cust-456";

        var expectedVersion = new DataVersion
        {
            VersionId = "version-003",
            EntityType = entityType,
            EntityId = entityId,
            VersionNumber = 3,
            Data = new { CustomerId = "cust-456", Name = "Nguyen Van A", Email = "a.updated@example.com" },
            CreatedBy = "user-123",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            Type = VersionType.Update,
            IsDeleted = false
        };

        _mockDataVersioning.Setup(x => x.GetCurrentVersionAsync(entityType, entityId))
                  .ReturnsAsync(expectedVersion);

        // Act
        var version = await _mockDataVersioning.Object.GetCurrentVersionAsync(entityType, entityId);

        // Assert
        Assert.NotNull(version);
        Assert.Equal(entityType, version.EntityType);
        Assert.Equal(entityId, version.EntityId);
        Assert.Equal(3, version.VersionNumber);
        Assert.Equal(VersionType.Update, version.Type);
        Assert.False(version.IsDeleted);
        _mockDataVersioning.Verify(x => x.GetCurrentVersionAsync(entityType, entityId), Times.Once);
    }

    [Fact(DisplayName = "TDD: Get Version History for Entity")]
    public async Task DataVersioning_GetVersionHistory_ShouldReturnOrderedVersions()
    {
        // Arrange
        var entityType = "Product";
        var entityId = "prod-789";

        var expectedHistory = new List<DataVersion>
        {
            new DataVersion
            {
                VersionId = "version-001",
                EntityType = entityType,
                EntityId = entityId,
                VersionNumber = 1,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                Type = VersionType.Create
            },
            new DataVersion
            {
                VersionId = "version-002",
                EntityType = entityType,
                EntityId = entityId,
                VersionNumber = 2,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                Type = VersionType.Update
            },
            new DataVersion
            {
                VersionId = "version-003",
                EntityType = entityType,
                EntityId = entityId,
                VersionNumber = 3,
                CreatedAt = DateTime.UtcNow.AddHours(-2),
                Type = VersionType.Update
            }
        };

        _mockDataVersioning.Setup(x => x.GetVersionHistoryAsync(entityType, entityId, 50))
                  .ReturnsAsync(expectedHistory);

        // Act
        var history = await _mockDataVersioning.Object.GetVersionHistoryAsync(entityType, entityId, 50);

        // Assert
        Assert.NotNull(history);
        Assert.Equal(3, history.Count);
        Assert.All(history, v => Assert.Equal(entityType, v.EntityType));
        Assert.All(history, v => Assert.Equal(entityId, v.EntityId));
        Assert.Equal(1, history[0].VersionNumber);
        Assert.Equal(2, history[1].VersionNumber);
        Assert.Equal(3, history[2].VersionNumber);
        _mockDataVersioning.Verify(x => x.GetVersionHistoryAsync(entityType, entityId, 50), Times.Once);
    }

    [Fact(DisplayName = "TDD: Compare Two Versions and Return Differences")]
    public async Task DataVersioning_CompareVersions_ShouldIdentifyDifferences()
    {
        // Arrange
        var entityType = "Order";
        var entityId = "order-999";
        var versionId1 = "version-001";
        var versionId2 = "version-002";

        var expectedComparison = new VersionComparison
        {
            EntityType = entityType,
            EntityId = entityId,
            VersionId1 = versionId1,
            VersionId2 = versionId2,
            Differences = new List<PropertyDifference>
            {
                new PropertyDifference
                {
                    PropertyName = "Status",
                    Value1 = "PENDING",
                    Value2 = "CONFIRMED",
                    Type = DifferenceType.Modified,
                    IsSignificant = true
                },
                new PropertyDifference
                {
                    PropertyName = "Total",
                    Value1 = 20000,
                    Value2 = 25000,
                    Type = DifferenceType.Modified,
                    IsSignificant = true
                }
            },
            Result = ComparisonResult.MinorChanges,
            ComparedAt = DateTime.UtcNow,
            ComparedBy = "user-123"
        };

        _mockDataVersioning.Setup(x => x.CompareVersionsAsync(entityType, entityId, versionId1, versionId2))
                  .ReturnsAsync(expectedComparison);

        // Act
        var comparison = await _mockDataVersioning.Object.CompareVersionsAsync(entityType, entityId, versionId1, versionId2);

        // Assert
        Assert.NotNull(comparison);
        Assert.Equal(entityType, comparison.EntityType);
        Assert.Equal(entityId, comparison.EntityId);
        Assert.Equal(versionId1, comparison.VersionId1);
        Assert.Equal(versionId2, comparison.VersionId2);
        Assert.Equal(2, comparison.Differences.Count);
        Assert.Equal(ComparisonResult.MinorChanges, comparison.Result);
        Assert.Contains(comparison.Differences, d => d.PropertyName == "Status");
        Assert.Contains(comparison.Differences, d => d.PropertyName == "Total");
        _mockDataVersioning.Verify(x => x.CompareVersionsAsync(entityType, entityId, versionId1, versionId2), Times.Once);
    }

    [Fact(DisplayName = "TDD: Revert Entity to Specific Version")]
    public async Task DataVersioning_RevertToVersion_ShouldCreateNewVersion()
    {
        // Arrange
        var entityType = "Customer";
        var entityId = "cust-001";
        var versionId = "version-002";
        var userId = "admin-123";

        var revertedVersion = new DataVersion
        {
            VersionId = "version-004",
            EntityType = entityType,
            EntityId = entityId,
            VersionNumber = 4,
            Data = new { CustomerId = "cust-001", Name = "Original Name", Email = "original@example.com" },
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            Type = VersionType.Revert,
            Comment = "Reverted to version 002"
        };

        var expectedResult = new RevertResult
        {
            Success = true,
            EntityType = entityType,
            EntityId = entityId,
            FromVersionId = "version-003",
            ToVersionId = versionId,
            NewVersion = revertedVersion,
            RevertedAt = DateTime.UtcNow,
            RevertedBy = userId
        };

        _mockDataVersioning.Setup(x => x.RevertToVersionAsync(entityType, entityId, versionId, userId))
                  .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockDataVersioning.Object.RevertToVersionAsync(entityType, entityId, versionId, userId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(entityType, result.EntityType);
        Assert.Equal(entityId, result.EntityId);
        Assert.Equal(versionId, result.ToVersionId);
        Assert.NotNull(result.NewVersion);
        Assert.Equal(VersionType.Revert, result.NewVersion.Type);
        Assert.Equal(userId, result.RevertedBy);
        _mockDataVersioning.Verify(x => x.RevertToVersionAsync(entityType, entityId, versionId, userId), Times.Once);
    }

    [Fact(DisplayName = "TDD: Get Audit Trail for Entity")]
    public async Task DataVersioning_GetAuditTrail_ShouldReturnOrderedEntries()
    {
        // Arrange
        var entityType = "Inventory";
        var entityId = "inv-123";
        var fromDate = DateTime.UtcNow.AddDays(-7);
        var toDate = DateTime.UtcNow;

        var expectedTrail = new List<AuditEntry>
        {
            new AuditEntry
            {
                EntityType = entityType,
                EntityId = entityId,
                Operation = AuditOperation.Create,
                UserId = "user-001",
                DeviceId = "mobile-001",
                Timestamp = DateTime.UtcNow.AddDays(-5),
                Severity = AuditSeverity.Info,
                Success = true
            },
            new AuditEntry
            {
                EntityType = entityType,
                EntityId = entityId,
                Operation = AuditOperation.Update,
                UserId = "user-002",
                DeviceId = "desktop-001",
                Timestamp = DateTime.UtcNow.AddDays(-2),
                Severity = AuditSeverity.Medium,
                Success = true
            },
            new AuditEntry
            {
                EntityType = entityType,
                EntityId = entityId,
                Operation = AuditOperation.Sync,
                UserId = "system",
                DeviceId = "server-001",
                Timestamp = DateTime.UtcNow.AddHours(-1),
                Severity = AuditSeverity.Low,
                Success = true
            }
        };

        _mockDataVersioning.Setup(x => x.GetAuditTrailAsync(entityType, entityId, fromDate, toDate))
                  .ReturnsAsync(expectedTrail);

        // Act
        var trail = await _mockDataVersioning.Object.GetAuditTrailAsync(entityType, entityId, fromDate, toDate);

        // Assert
        Assert.NotNull(trail);
        Assert.Equal(3, trail.Count);
        Assert.All(trail, e => Assert.Equal(entityType, e.EntityType));
        Assert.All(trail, e => Assert.Equal(entityId, e.EntityId));
        Assert.Contains(trail, e => e.Operation == AuditOperation.Create);
        Assert.Contains(trail, e => e.Operation == AuditOperation.Update);
        Assert.Contains(trail, e => e.Operation == AuditOperation.Sync);
        _mockDataVersioning.Verify(x => x.GetAuditTrailAsync(entityType, entityId, fromDate, toDate), Times.Once);
    }

    [Fact(DisplayName = "TDD: Create Audit Entry for Entity Operation")]
    public async Task DataVersioning_CreateAuditEntry_ShouldRecordOperation()
    {
        // Arrange
        var entityType = "Order";
        var entityId = "order-456";
        var operation = AuditOperation.Update;
        var userId = "user-789";
        var deviceId = "tablet-001";
        var changes = new { StatusFrom = "PENDING", StatusTo = "CONFIRMED" };

        var expectedEntry = new AuditEntry
        {
            EntityType = entityType,
            EntityId = entityId,
            Operation = operation,
            UserId = userId,
            DeviceId = deviceId,
            Changes = changes,
            Timestamp = DateTime.UtcNow,
            Severity = AuditSeverity.Medium,
            Success = true,
            IpAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0..."
        };

        _mockDataVersioning.Setup(x => x.CreateAuditEntryAsync(entityType, entityId, operation, userId, deviceId, changes))
                  .ReturnsAsync(expectedEntry);

        // Act
        var entry = await _mockDataVersioning.Object.CreateAuditEntryAsync(entityType, entityId, operation, userId, deviceId, changes);

        // Assert
        Assert.NotNull(entry);
        Assert.Equal(entityType, entry.EntityType);
        Assert.Equal(entityId, entry.EntityId);
        Assert.Equal(operation, entry.Operation);
        Assert.Equal(userId, entry.UserId);
        Assert.Equal(deviceId, entry.DeviceId);
        Assert.Equal(changes, entry.Changes);
        Assert.Equal(AuditSeverity.Medium, entry.Severity);
        Assert.True(entry.Success);
        _mockDataVersioning.Verify(x => x.CreateAuditEntryAsync(entityType, entityId, operation, userId, deviceId, changes), Times.Once);
    }

    [Fact(DisplayName = "TDD: Validate Version Integrity")]
    public async Task DataVersioning_ValidateVersionIntegrity_ShouldPassValidation()
    {
        // Arrange
        var entityType = "Product";
        var entityId = "prod-999";

        var expectedValidation = new VersionIntegrityResult
        {
            IsValid = true,
            EntityType = entityType,
            EntityId = entityId,
            Issues = new List<IntegrityIssue>(),
            ValidatedAt = DateTime.UtcNow,
            ValidatedBy = "system-validator",
            TotalVersions = 5,
            CorruptedVersions = 0
        };

        _mockDataVersioning.Setup(x => x.ValidateVersionIntegrityAsync(entityType, entityId))
                  .ReturnsAsync(expectedValidation);

        // Act
        var validation = await _mockDataVersioning.Object.ValidateVersionIntegrityAsync(entityType, entityId);

        // Assert
        Assert.NotNull(validation);
        Assert.True(validation.IsValid);
        Assert.Equal(entityType, validation.EntityType);
        Assert.Equal(entityId, validation.EntityId);
        Assert.Empty(validation.Issues);
        Assert.Equal(5, validation.TotalVersions);
        Assert.Equal(0, validation.CorruptedVersions);
        _mockDataVersioning.Verify(x => x.ValidateVersionIntegrityAsync(entityType, entityId), Times.Once);
    }

    [Fact(DisplayName = "TDD: Cleanup Old Versions Based on Retention Policy")]
    public async Task DataVersioning_CleanupOldVersions_ShouldRemoveExpiredVersions()
    {
        // Arrange
        var entityType = "Customer";
        var retentionPeriod = TimeSpan.FromDays(30);

        var expectedCleanup = new CleanupResult
        {
            Success = true,
            EntityType = entityType,
            RetentionPeriod = retentionPeriod,
            VersionsBeforeCleanup = 100,
            VersionsAfterCleanup = 45,
            VersionsDeleted = 55,
            SpaceSavedBytes = 1024 * 1024, // 1MB
            CleanedAt = DateTime.UtcNow,
            CleanedBy = "system-cleanup"
        };

        _mockDataVersioning.Setup(x => x.CleanupOldVersionsAsync(entityType, retentionPeriod))
                  .ReturnsAsync(expectedCleanup);

        // Act
        var cleanup = await _mockDataVersioning.Object.CleanupOldVersionsAsync(entityType, retentionPeriod);

        // Assert
        Assert.NotNull(cleanup);
        Assert.True(cleanup.Success);
        Assert.Equal(entityType, cleanup.EntityType);
        Assert.Equal(retentionPeriod, cleanup.RetentionPeriod);
        Assert.Equal(100, cleanup.VersionsBeforeCleanup);
        Assert.Equal(45, cleanup.VersionsAfterCleanup);
        Assert.Equal(55, cleanup.VersionsDeleted);
        Assert.True(cleanup.SpaceSavedBytes > 0);
        _mockDataVersioning.Verify(x => x.CleanupOldVersionsAsync(entityType, retentionPeriod), Times.Once);
    }

    [Fact(DisplayName = "TDD: Handle Version Integrity Issues")]
    public async Task DataVersioning_ValidateIntegrity_ShouldDetectCorruption()
    {
        // Arrange
        var entityType = "Order";
        var entityId = "order-corrupt";

        var expectedValidation = new VersionIntegrityResult
        {
            IsValid = false,
            EntityType = entityType,
            EntityId = entityId,
            Issues = new List<IntegrityIssue>
            {
                new IntegrityIssue
                {
                    VersionId = "version-corrupt-001",
                    IssueType = "ChecksumMismatch",
                    Description = "Data checksum does not match stored hash",
                    Severity = IssueSeverity.Error,
                    CanAutoFix = false
                },
                new IntegrityIssue
                {
                    VersionId = "version-corrupt-002",
                    IssueType = "MissingData",
                    Description = "Version data is null or empty",
                    Severity = IssueSeverity.Critical,
                    CanAutoFix = false
                }
            },
            ValidatedAt = DateTime.UtcNow,
            ValidatedBy = "system-validator",
            TotalVersions = 10,
            CorruptedVersions = 2
        };

        _mockDataVersioning.Setup(x => x.ValidateVersionIntegrityAsync(entityType, entityId))
                  .ReturnsAsync(expectedValidation);

        // Act
        var validation = await _mockDataVersioning.Object.ValidateVersionIntegrityAsync(entityType, entityId);

        // Assert
        Assert.NotNull(validation);
        Assert.False(validation.IsValid);
        Assert.Equal(2, validation.Issues.Count);
        Assert.Equal(10, validation.TotalVersions);
        Assert.Equal(2, validation.CorruptedVersions);
        Assert.Contains(validation.Issues, i => i.IssueType == "ChecksumMismatch");
        Assert.Contains(validation.Issues, i => i.IssueType == "MissingData");
        Assert.Contains(validation.Issues, i => i.Severity == IssueSeverity.Critical);
        _mockDataVersioning.Verify(x => x.ValidateVersionIntegrityAsync(entityType, entityId), Times.Once);
    }
}
