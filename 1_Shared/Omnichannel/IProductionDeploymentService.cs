using VanAn.Shared.Domain;

namespace VanAn.Shared.Omnichannel;

/// <summary>
/// Production deployment service interface for omnichannel system
/// Provides deployment orchestration, health monitoring, and environment management
/// </summary>
public interface IProductionDeploymentService
{
    /// <summary>
    /// Deploy omnichannel system to production environment
    /// </summary>
    Task<DeploymentResult> DeployToProductionAsync(DeploymentRequest request, string deployedBy);

    /// <summary>
    /// Get deployment status and health information
    /// </summary>
    Task<DeploymentStatus> GetDeploymentStatusAsync(string environment);

    /// <summary>
    /// Perform health check on all omnichannel services
    /// </summary>
    Task<HealthCheckResult> PerformHealthCheckAsync(string environment, bool includeDependencies = true);

    /// <summary>
    /// Get system performance metrics
    /// </summary>
    Task<SystemMetrics> GetSystemMetricsAsync(string environment, TimeSpan? lookbackPeriod = null);

    /// <summary>
    /// Scale services based on load and performance requirements
    /// </summary>
    Task<ScalingResult> ScaleServicesAsync(ScalingRequest request, string environment);

    /// <summary>
    /// Rollback deployment to previous version
    /// </summary>
    Task<RollbackResult> RollbackDeploymentAsync(string environment, string targetVersion, string requestedBy);

    /// <summary>
    /// Get deployment history and audit trail
    /// </summary>
    Task<List<DeploymentHistoryEntry>> GetDeploymentHistoryAsync(string environment, DateTime? from = null, DateTime? to = null);

    /// <summary>
    /// Validate deployment prerequisites and requirements
    /// </summary>
    Task<ValidationResult> ValidateDeploymentPrerequisitesAsync(string environment, DeploymentRequest request);

    /// <summary>
    /// Get environment configuration and settings
    /// </summary>
    Task<EnvironmentConfiguration> GetEnvironmentConfigurationAsync(string environment);

    /// <summary>
    /// Update environment configuration
    /// </summary>
    Task<ConfigurationUpdateResult> UpdateEnvironmentConfigurationAsync(string environment, EnvironmentConfiguration configuration, string updatedBy);

    /// <summary>
    /// Get service dependencies and their status
    /// </summary>
    Task<List<ServiceDependency>> GetServiceDependenciesAsync(string environment);

    /// <summary>
    /// Perform load testing validation
    /// </summary>
    Task<LoadTestResult> PerformLoadTestAsync(LoadTestRequest request, string environment);

    /// <summary>
    /// Get deployment analytics and insights
    /// </summary>
    Task<DeploymentAnalytics> GetDeploymentAnalyticsAsync(string environment, DateTime? from = null, DateTime? to = null);

    /// <summary>
    /// Create deployment backup before deployment
    /// </summary>
    Task<BackupResult> CreateDeploymentBackupAsync(string environment, string backupReason);

    /// <summary>
    /// Restore from deployment backup
    /// </summary>
    Task<RestoreResult> RestoreFromBackupAsync(string environment, string backupId, string requestedBy);
}

/// <summary>
/// Deployment request
/// </summary>
public record DeploymentRequest
{
    public string Version { get; init; } = string.Empty;
    public string Environment { get; init; } = string.Empty;
    public List<string> Services { get; init; } = new();
    public DeploymentStrategy Strategy { get; init; }
    public bool SkipHealthChecks { get; init; }
    public bool SkipBackup { get; init; }
    public Dictionary<string, object> Configuration { get; init; } = new();
    public List<string> Tags { get; init; } = new();
    public string? Branch { get; init; }
    public string? CommitHash { get; init; }
    public DeploymentPriority Priority { get; init; }
    public TimeSpan? DeploymentTimeout { get; init; }
    public List<string> NotificationChannels { get; init; } = new();
}

/// <summary>
/// Deployment result
/// </summary>
public record DeploymentResult
{
    public bool Success { get; init; }
    public string DeploymentId { get; init; } = Guid.NewGuid().ToString();
    public string Environment { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; init; }
    public TimeSpan DeploymentDuration { get; init; }
    public List<ServiceDeploymentResult> ServiceResults { get; init; } = new();
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public string DeployedBy { get; init; } = string.Empty;
    public string BackupId { get; init; } = string.Empty;
    public DeploymentState State { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Service deployment result
/// </summary>
public record ServiceDeploymentResult
{
    public string ServiceName { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string PreviousVersion { get; init; } = string.Empty;
    public string NewVersion { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; init; }
    public TimeSpan DeploymentDuration { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public ServiceHealth Health { get; init; }
    public Dictionary<string, object> Metrics { get; init; } = new();
}

/// <summary>
/// Deployment status
/// </summary>
public record DeploymentStatus
{
    public string Environment { get; init; } = string.Empty;
    public string CurrentVersion { get; init; } = string.Empty;
    public DeploymentState State { get; init; }
    public DateTime LastDeploymentAt { get; init; } = DateTime.UtcNow;
    public string LastDeployedBy { get; init; } = string.Empty;
    public TimeSpan Uptime { get; init; }
    public List<ServiceStatus> ServiceStatuses { get; init; } = new();
    public SystemHealth OverallHealth { get; init; }
    public Dictionary<string, object> Metrics { get; init; } = new();
    public List<string> ActiveDeployments { get; init; } = new();
    public int PendingUpdates { get; init; }
}

/// <summary>
/// Service status
/// </summary>
public record ServiceStatus
{
    public string ServiceName { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public ServiceHealth Health { get; init; }
    public DateTime LastHealthCheck { get; init; } = DateTime.UtcNow;
    public TimeSpan Uptime { get; init; }
    public int InstanceCount { get; init; }
    public int HealthyInstances { get; init; }
    public Dictionary<string, object> Metrics { get; init; } = new();
    public List<string> Endpoints { get; init; } = new();
}

/// <summary>
/// Health check result
/// </summary>
public record HealthCheckResult
{
    public bool OverallHealthy { get; init; }
    public string Environment { get; init; } = string.Empty;
    public DateTime CheckedAt { get; init; } = DateTime.UtcNow;
    public TimeSpan CheckDuration { get; init; }
    public List<ServiceHealthCheck> ServiceChecks { get; init; } = new();
    public List<DependencyHealthCheck> DependencyChecks { get; init; } = new();
    public List<InfrastructureHealthCheck> InfrastructureChecks { get; init; } = new();
    public SystemHealth OverallSystemHealth { get; init; }
    public List<string> CriticalIssues { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public Dictionary<string, object> HealthMetrics { get; init; } = new();
}

/// <summary>
/// Service health check
/// </summary>
public record ServiceHealthCheck
{
    public string ServiceName { get; init; } = string.Empty;
    public ServiceHealth Health { get; init; }
    public DateTime CheckedAt { get; init; } = DateTime.UtcNow;
    public TimeSpan ResponseTime { get; init; }
    public string? Endpoint { get; init; }
    public List<string> Issues { get; init; } = new();
    public Dictionary<string, object> Metrics { get; init; } = new();
    public bool IsCritical { get; init; }
}

/// <summary>
/// Dependency health check
/// </summary>
public record DependencyHealthCheck
{
    public string DependencyName { get; init; } = string.Empty;
    public DependencyType Type { get; init; }
    public DependencyHealth Health { get; init; }
    public DateTime CheckedAt { get; init; } = DateTime.UtcNow;
    public TimeSpan ResponseTime { get; init; }
    public string? ConnectionString { get; init; }
    public List<string> Issues { get; init; } = new();
    public Dictionary<string, object> Metrics { get; init; } = new();
    public bool IsCritical { get; init; }
}

/// <summary>
/// Infrastructure health check
/// </summary>
public record InfrastructureHealthCheck
{
    public string ComponentName { get; init; } = string.Empty;
    public InfrastructureComponent Type { get; init; }
    public InfrastructureHealth Health { get; init; }
    public DateTime CheckedAt { get; init; } = DateTime.UtcNow;
    public List<string> Issues { get; init; } = new();
    public Dictionary<string, object> Metrics { get; init; } = new();
    public bool IsCritical { get; init; }
}

/// <summary>
/// System metrics
/// </summary>
public record SystemMetrics
{
    public string Environment { get; init; } = string.Empty;
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    public TimeSpan LookbackPeriod { get; init; }
    public PerformanceMetrics Performance { get; init; } = new();
    public ResourceMetrics Resources { get; init; } = new();
    public BusinessMetrics Business { get; init; } = new();
    public ErrorMetrics Errors { get; init; } = new();
    public Dictionary<string, ServiceMetrics> ServiceMetrics { get; init; } = new();
    public List<Alert> ActiveAlerts { get; init; } = new();
    public SystemTrends Trends { get; init; } = new();
}

/// <summary>
/// Performance metrics
/// </summary>
public record PerformanceMetrics
{
    public decimal AverageResponseTime { get; init; }
    public decimal P95ResponseTime { get; init; }
    public decimal P99ResponseTime { get; init; }
    public decimal RequestsPerSecond { get; init; }
    public decimal ErrorRate { get; init; }
    public decimal Throughput { get; init; }
    public decimal CpuUtilization { get; init; }
    public decimal MemoryUtilization { get; init; }
    public decimal DiskUtilization { get; init; }
    public decimal NetworkUtilization { get; init; }
}

/// <summary>
/// Resource metrics
/// </summary>
public record ResourceMetrics
{
    public int TotalInstances { get; init; }
    public int ActiveInstances { get; init; }
    public int HealthyInstances { get; init; }
    public decimal TotalMemory { get; init; }
    public decimal UsedMemory { get; init; }
    public decimal AvailableMemory { get; init; }
    public decimal TotalStorage { get; init; }
    public decimal UsedStorage { get; init; }
    public decimal AvailableStorage { get; init; }
    public int DatabaseConnections { get; init; }
    public int CacheConnections { get; init; }
}

/// <summary>
/// Business metrics
/// </summary>
public record BusinessMetrics
{
    public int ActiveUsers { get; init; }
    public int OrdersPerMinute { get; init; }
    public decimal RevenuePerHour { get; init; }
    public int SyncOperationsPerMinute { get; init; }
    public decimal SyncSuccessRate { get; init; }
    public int ActiveDevices { get; init; }
    public int ConcurrentSessions { get; init; }
    public decimal CustomerSatisfactionScore { get; init; }
}

/// <summary>
/// Error metrics
/// </summary>
public record ErrorMetrics
{
    public int TotalErrors { get; init; }
    public int CriticalErrors { get; init; }
    public int WarningCount { get; init; }
    public decimal ErrorRate { get; init; }
    public Dictionary<string, int> ErrorsByService { get; init; } = new();
    public Dictionary<string, int> ErrorsByType { get; init; } = new();
    public List<ErrorTrend> ErrorTrends { get; init; } = new();
    public TimeSpan MeanTimeToRecovery { get; init; }
}

/// <summary>
/// Service metrics
/// </summary>
public record ServiceMetrics
{
    public string ServiceName { get; init; } = string.Empty;
    public decimal AverageResponseTime { get; init; }
    public decimal RequestsPerSecond { get; init; }
    public decimal ErrorRate { get; init; }
    public int InstanceCount { get; init; }
    public decimal CpuUtilization { get; init; }
    public decimal MemoryUtilization { get; init; }
    public ServiceHealth Health { get; init; }
    public DateTime LastUpdated { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Alert
/// </summary>
public record Alert
{
    public string AlertId { get; init; } = Guid.NewGuid().ToString();
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public AlertSeverity Severity { get; init; }
    public AlertType Type { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAt { get; init; }
    public string? AcknowledgedBy { get; init; }
    public bool IsActive { get; init; }
    public string Source { get; init; } = string.Empty;
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// System trends
/// </summary>
public record SystemTrends
{
    public List<PerformanceTrend> PerformanceTrends { get; init; } = new();
    public List<ResourceTrend> ResourceTrends { get; init; } = new();
    public List<BusinessTrend> BusinessTrends { get; init; } = new();
    public List<ErrorTrend> ErrorTrends { get; init; } = new();
    public List<UserTrend> UserTrends { get; init; } = new();
}

/// <summary>
/// Scaling request
/// </summary>
public record ScalingRequest
{
    public List<ServiceScalingRequest> ServiceRequests { get; init; } = new();
    public ScalingStrategy Strategy { get; init; }
    public bool AutoScale { get; init; }
    public Dictionary<string, object> ScalingRules { get; init; } = new();
    public TimeSpan? CooldownPeriod { get; init; }
    public List<string> NotificationChannels { get; init; } = new();
}

/// <summary>
/// Service scaling request
/// </summary>
public record ServiceScalingRequest
{
    public string ServiceName { get; init; } = string.Empty;
    public int TargetInstances { get; init; }
    public ScalingDirection Direction { get; init; }
    public string? Reason { get; init; }
    public Dictionary<string, object> Configuration { get; init; } = new();
}

/// <summary>
/// Scaling result
/// </summary>
public record ScalingResult
{
    public bool Success { get; init; }
    public string ScalingId { get; init; } = Guid.NewGuid().ToString();
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; init; }
    public TimeSpan ScalingDuration { get; init; }
    public List<ServiceScalingResult> ServiceResults { get; init; } = new();
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public ScalingStrategy AppliedStrategy { get; init; }
    public Dictionary<string, object> Metrics { get; init; } = new();
}

/// <summary>
/// Service scaling result
/// </summary>
public record ServiceScalingResult
{
    public string ServiceName { get; init; } = string.Empty;
    public bool Success { get; init; }
    public int PreviousInstances { get; init; }
    public int NewInstances { get; init; }
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; init; }
    public TimeSpan ScalingDuration { get; init; }
    public List<string> Errors { get; init; } = new();
    public Dictionary<string, object> Metrics { get; init; } = new();
}

/// <summary>
/// Rollback result
/// </summary>
public record RollbackResult
{
    public bool Success { get; init; }
    public string RollbackId { get; init; } = Guid.NewGuid().ToString();
    public string Environment { get; init; } = string.Empty;
    public string FromVersion { get; init; } = string.Empty;
    public string ToVersion { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; init; }
    public TimeSpan RollbackDuration { get; init; }
    public List<ServiceRollbackResult> ServiceResults { get; init; } = new();
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public string RequestedBy { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public string BackupId { get; init; } = string.Empty;
}

/// <summary>
/// Service rollback result
/// </summary>
public record ServiceRollbackResult
{
    public string ServiceName { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string FromVersion { get; init; } = string.Empty;
    public string ToVersion { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; init; }
    public TimeSpan RollbackDuration { get; init; }
    public List<string> Errors { get; init; } = new();
    public ServiceHealth Health { get; init; }
}

/// <summary>
/// Deployment history entry
/// </summary>
public record DeploymentHistoryEntry
{
    public string DeploymentId { get; init; } = Guid.NewGuid().ToString();
    public string Environment { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public DeploymentState State { get; init; }
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; init; }
    public TimeSpan DeploymentDuration { get; init; }
    public string DeployedBy { get; init; } = string.Empty;
    public bool Success { get; init; }
    public List<string> Services { get; init; } = new();
    public string? Branch { get; init; }
    public string? CommitHash { get; init; }
    public List<string> Tags { get; init; } = new();
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Validation result
/// </summary>
public record ValidationResult
{
    public bool IsValid { get; init; }
    public List<ValidationIssue> Issues { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public List<string> Recommendations { get; init; } = new();
    public Dictionary<string, object> ValidationMetrics { get; init; } = new();
    public DateTime ValidatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Validation issue
/// </summary>
public record ValidationIssue
{
    public string Code { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public ValidationSeverity Severity { get; init; }
    public string Component { get; init; } = string.Empty;
    public string? Recommendation { get; init; }
    public bool IsBlocking { get; init; }
}

/// <summary>
/// Environment configuration
/// </summary>
public record EnvironmentConfiguration
{
    public string Environment { get; init; } = string.Empty;
    public Dictionary<string, string> AppSettings { get; init; } = new();
    public Dictionary<string, string> ConnectionStrings { get; init; } = new();
    public Dictionary<string, object> FeatureFlags { get; init; } = new();
    public Dictionary<string, object> Logging { get; init; } = new();
    public Dictionary<string, object> Monitoring { get; init; } = new();
    public Dictionary<string, object> Caching { get; init; } = new();
    public Dictionary<string, object> Security { get; init; } = new();
    public Dictionary<string, object> Performance { get; init; } = new();
    public List<string> AllowedHosts { get; init; } = new();
    public string Version { get; init; } = string.Empty;
    public DateTime LastUpdated { get; init; } = DateTime.UtcNow;
    public string UpdatedBy { get; init; } = string.Empty;
}

/// <summary>
/// Configuration update result
/// </summary>
public record ConfigurationUpdateResult
{
    public bool Success { get; init; }
    public string Environment { get; init; } = string.Empty;
    public string UpdateId { get; init; } = Guid.NewGuid().ToString();
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
    public string UpdatedBy { get; init; } = string.Empty;
    public List<string> UpdatedSettings { get; init; } = new();
    public List<string> RestartedServices { get; init; } = new();
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public string PreviousVersion { get; init; } = string.Empty;
    public string NewVersion { get; init; } = string.Empty;
}

/// <summary>
/// Service dependency
/// </summary>
public record ServiceDependency
{
    public string ServiceName { get; init; } = string.Empty;
    public string DependencyName { get; init; } = string.Empty;
    public DependencyType Type { get; init; }
    public DependencyHealth Health { get; init; }
    public string? ConnectionString { get; init; }
    public DateTime LastHealthCheck { get; init; } = DateTime.UtcNow;
    public TimeSpan ResponseTime { get; init; }
    public bool IsCritical { get; init; }
    public List<string> Issues { get; init; } = new();
    public Dictionary<string, object> Metrics { get; init; } = new();
}

/// <summary>
/// Load test request
/// </summary>
public record LoadTestRequest
{
    public string TestName { get; init; } = string.Empty;
    public int ConcurrentUsers { get; init; }
    public TimeSpan Duration { get; init; }
    public int RequestsPerSecond { get; init; }
    public List<string> TargetServices { get; init; } = new();
    public List<string> TargetEndpoints { get; init; } = new();
    public Dictionary<string, object> TestParameters { get; init; } = new();
    public LoadTestProfile Profile { get; init; }
    public bool EnableMonitoring { get; init; }
    public List<string> NotificationChannels { get; init; } = new();
}

/// <summary>
/// Load test result
/// </summary>
public record LoadTestResult
{
    public bool Success { get; init; }
    public string TestId { get; init; } = Guid.NewGuid().ToString();
    public string TestName { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; init; }
    public TimeSpan TestDuration { get; init; }
    public LoadTestMetrics Metrics { get; init; } = new();
    public List<ServiceLoadTestResult> ServiceResults { get; init; } = new();
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public bool PassedPerformanceThresholds { get; init; }
    public Dictionary<string, object> TestResults { get; init; } = new();
}

/// <summary>
/// Load test metrics
/// </summary>
public record LoadTestMetrics
{
    public int TotalRequests { get; init; }
    public int SuccessfulRequests { get; init; }
    public int FailedRequests { get; init; }
    public decimal SuccessRate { get; init; }
    public decimal AverageResponseTime { get; init; }
    public decimal P95ResponseTime { get; init; }
    public decimal P99ResponseTime { get; init; }
    public decimal RequestsPerSecond { get; init; }
    public decimal Throughput { get; init; }
    public decimal ErrorRate { get; init; }
    public int PeakConcurrentUsers { get; init; }
    public decimal CpuUtilization { get; init; }
    public decimal MemoryUtilization { get; init; }
    public List<PerformanceTimeSeries> TimeSeries { get; init; } = new();
}

/// <summary>
/// Service load test result
/// </summary>
public record ServiceLoadTestResult
{
    public string ServiceName { get; init; } = string.Empty;
    public bool Success { get; init; }
    public int TotalRequests { get; init; }
    public int SuccessfulRequests { get; init; }
    public decimal AverageResponseTime { get; init; }
    public decimal P95ResponseTime { get; init; }
    public decimal RequestsPerSecond { get; init; }
    public decimal ErrorRate { get; init; }
    public List<string> Errors { get; init; } = new();
    public Dictionary<string, object> Metrics { get; init; } = new();
}

/// <summary>
/// Deployment analytics
/// </summary>
public record DeploymentAnalytics
{
    public string Environment { get; init; } = string.Empty;
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }
    public int TotalDeployments { get; init; }
    public int SuccessfulDeployments { get; init; }
    public int FailedDeployments { get; init; }
    public decimal SuccessRate { get; init; }
    public TimeSpan AverageDeploymentTime { get; init; }
    public TimeSpan AverageRollbackTime { get; init; }
    public Dictionary<string, int> DeploymentsByService { get; init; } = new();
    public Dictionary<string, int> DeploymentsByDay { get; init; } = new();
    public List<DeploymentTrend> DeploymentTrends { get; init; } = new();
    public List<string> CommonErrors { get; init; } = new();
    public List<string> CommonWarnings { get; init; } = new();
    public Dictionary<string, object> PerformanceMetrics { get; init; } = new();
}

/// <summary>
/// Backup result
/// </summary>
public record BackupResult
{
    public bool Success { get; init; }
    public string BackupId { get; init; } = Guid.NewGuid().ToString();
    public string Environment { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string BackupReason { get; init; } = string.Empty;
    public List<string> BackedUpServices { get; init; } = new();
    public List<string> BackedUpDatabases { get; init; } = new();
    public string BackupLocation { get; init; } = string.Empty;
    public decimal BackupSize { get; init; }
    public TimeSpan BackupDuration { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public string BackupType { get; init; } = string.Empty;
}

/// <summary>
/// Restore result
/// </summary>
public record RestoreResult
{
    public bool Success { get; init; }
    public string RestoreId { get; init; } = Guid.NewGuid().ToString();
    public string Environment { get; init; } = string.Empty;
    public string BackupId { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; init; }
    public TimeSpan RestoreDuration { get; init; }
    public List<string> RestoredServices { get; init; } = new();
    public List<string> RestoredDatabases { get; init; } = new();
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public string RequestedBy { get; init; } = string.Empty;
    public string? Reason { get; init; }
}

/// <summary>
/// Supporting enums and helper classes
/// </summary>

public enum DeploymentStrategy
{
    Rolling,         // Rolling update with zero downtime
    BlueGreen,       // Blue-green deployment
    Canary,          // Canary deployment
    Recreate,        // Recreate all instances
    Custom           // Custom deployment strategy
}

public enum DeploymentState
{
    Pending,         // Deployment pending
    InProgress,      // Deployment in progress
    Completed,       // Deployment completed
    Failed,          // Deployment failed
    RollingBack,     // Rolling back
    Cancelled        // Deployment cancelled
}

public enum DeploymentPriority
{
    Low,             // Low priority
    Normal,          // Normal priority
    High,            // High priority
    Critical         // Critical priority
}

public enum ServiceHealth
{
    Healthy,         // Service is healthy
    Warning,         // Service has warnings
    Critical,        // Service is critical
    Unhealthy,       // Service is unhealthy
    Unknown          // Health status unknown
}

public enum SystemHealth
{
    Excellent,       // All systems healthy
    Good,            // Minor issues
    Fair,            // Some issues
    Poor,            // Major issues
    Critical         // Critical issues
}

public enum DependencyType
{
    Database,        // Database dependency
    Cache,           // Cache dependency
    MessageQueue,    // Message queue dependency
    ExternalApi,     // External API dependency
    FileSystem,      // File system dependency
    Service,         // Service dependency
    Network,         // Network dependency
    Other           // Other dependency type
}

public enum DependencyHealth
{
    Healthy,         // Dependency is healthy
    Degraded,        // Dependency is degraded
    Unhealthy,       // Dependency is unhealthy
    Unknown          // Health status unknown
}

public enum InfrastructureComponent
{
    Server,          // Server component
    Database,        // Database component
    Cache,           // Cache component
    LoadBalancer,    // Load balancer component
    Network,         // Network component
    Storage,         // Storage component
    Security,        // Security component
    Monitoring       // Monitoring component
}

public enum InfrastructureHealth
{
    Healthy,         // Infrastructure is healthy
    Warning,         // Infrastructure has warnings
    Critical,        // Infrastructure is critical
    Unhealthy,       // Infrastructure is unhealthy
    Unknown          // Health status unknown
}

public enum AlertSeverity
{
    Info,            // Informational alert
    Warning,         // Warning alert
    Error,           // Error alert
    Critical         // Critical alert
}

public enum AlertType
{
    Performance,     // Performance alert
    Availability,    // Availability alert
    Security,        // Security alert
    Capacity,        // Capacity alert
    Error,           // Error alert
    Custom           // Custom alert type
}

public enum ScalingDirection
{
    ScaleUp,         // Scale up (increase instances)
    ScaleDown,       // Scale down (decrease instances)
    ScaleOut,        // Scale out (add instances)
    ScaleIn          // Scale in (remove instances)
}

public enum ScalingStrategy
{
    Manual,          // Manual scaling
    Auto,            // Automatic scaling
    Scheduled,       // Scheduled scaling
    EventDriven,     // Event-driven scaling
    Predictive       // Predictive scaling
}

public enum ValidationSeverity
{
    Info,            // Informational issue
    Warning,         // Warning issue
    Error,           // Error issue
    Critical         // Critical issue
}

public enum LoadTestProfile
{
    Smoke,           // Smoke test
    Load,            // Load test
    Stress,          // Stress test
    Spike,           // Spike test
    Volume,          // Volume test
    Endurance,       // Endurance test
    Custom           // Custom profile
}

/// <summary>
/// Time series data points
/// </summary>
public record PerformanceTimeSeries
{
    public DateTime Timestamp { get; init; }
    public decimal ResponseTime { get; init; }
    public decimal RequestsPerSecond { get; init; }
    public decimal ErrorRate { get; init; }
    public decimal CpuUtilization { get; init; }
    public decimal MemoryUtilization { get; init; }
}

public record PerformanceTrend
{
    public DateTime Timestamp { get; init; }
    public decimal AverageResponseTime { get; init; }
    public decimal RequestsPerSecond { get; init; }
    public decimal ErrorRate { get; init; }
}

public record ResourceTrend
{
    public DateTime Timestamp { get; init; }
    public decimal CpuUtilization { get; init; }
    public decimal MemoryUtilization { get; init; }
    public decimal DiskUtilization { get; init; }
    public int ActiveInstances { get; init; }
}

public record BusinessTrend
{
    public DateTime Timestamp { get; init; }
    public int ActiveUsers { get; init; }
    public int OrdersPerMinute { get; init; }
    public decimal RevenuePerHour { get; init; }
    public int SyncOperationsPerMinute { get; init; }
}

public record ErrorTrend
{
    public DateTime Timestamp { get; init; }
    public int ErrorCount { get; init; }
    public decimal ErrorRate { get; init; }
    public string ErrorType { get; init; } = string.Empty;
}

public record UserTrend
{
    public DateTime Timestamp { get; init; }
    public int ConcurrentUsers { get; init; }
    public int NewUsers { get; init; }
    public int ActiveDevices { get; init; }
    public decimal CustomerSatisfactionScore { get; init; }
}

public record DeploymentTrend
{
    public DateTime Date { get; init; }
    public int DeploymentCount { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public TimeSpan AverageDuration { get; init; }
}
