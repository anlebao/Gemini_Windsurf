# Testing - Lu?ng X? Lý T??ng (Ideal Architecture)

**Ngày:** 11 tháng 4, 2026  
**Module:** 6_Testing  
**Tr?ng thái:** Thi?t k? lu?ng x? lý lý t??ng ho?n ch?nh

---

## **1. T?NG QUAN ARCHITECTURE LÝ T??NG**

### **1.1 Unified Testing Framework**
```
6_Testing/
?? Configuration/
   ?? environments/
      ?? development.env
      ?? staging.env
      ?? production.env
   ?? test-config.json
   ?? quality-gate.json
   ?? performance-thresholds.json
?? Infrastructure/
   ?? docker/
      ?? Dockerfile.test-runner
      ?? docker-compose.test.yml
      ?? docker-compose.chaos.yml
   ?? kubernetes/
      ?? test-pod.yaml
      ?? chaos-experiment.yaml
   ?? ci-cd/
      ?? github-actions.yml
      ?? azure-pipelines.yml
      ?? jenkinsfile
?? TestSuites/
   ?? Unit/
      ?? Domain/
         ?? OrderTests.cs
         ?? CustomerTests.cs
         ?? ProductTests.cs
         ?? ValueObjectTests.cs
      ?? Services/
         ?? OrderServiceTests.cs
         ?? KitchenServiceTests.cs
         ?? CustomerServiceTests.cs
      ?? TestInfrastructure/
         ?? TestBase.cs
         ?? MockFactory.cs
         ?? TestDataFactory.cs
   ?? Integration/
      ?? Repositories/
         ?? OrderRepositoryTests.cs
         ?? CustomerRepositoryTests.cs
      ?? Services/
         ?? OrderWorkflowIntegrationTests.cs
         ?? KitchenServiceIntegrationTests.cs
      ?? TestInfrastructure/
         ?? IntegrationTestBase.cs
         ?? TestDatabaseFactory.cs
         ?? TestDataSeeder.cs
   ?? API/
      ?? Controllers/
         ?? OrdersControllerTests.cs
         ?? KitchenControllerTests.cs
         ?? VietQrControllerTests.cs
      ?? Middleware/
         ?? AuthenticationTests.cs
         ?? TenantMiddlewareTests.cs
      ?? TestInfrastructure/
         ?? ApiTestBase.cs
         ?? TestServerFactory.cs
   ?? E2E/
      ?? UserJourneys/
         ?? OrderCreationE2ETests.cs
         ?? KitchenWorkflowE2ETests.cs
         ?? PaymentE2ETests.cs
      ?? CrossBrowser/
         ?? ChromeTests.cs
         ?? FirefoxTests.cs
         ?? MobileTests.cs
      ?? TestInfrastructure/
         ?? E2ETestBase.cs
         ?? PageObjectFactory.cs
         ?? BrowserFactory.cs
   ?? Performance/
      ?? Load/
         ?? OrderCreationLoadTests.cs
         ?? KitchenOperationsLoadTests.cs
         ?? PaymentProcessingLoadTests.cs
      ?? Stress/
         ?? HighConcurrencyTests.cs
         ?? MemoryLeakTests.cs
         ?? DatabaseStressTests.cs
      ?? TestInfrastructure/
         ?? LoadTestBase.cs
         ?? PerformanceMonitor.cs
         ?? TestOrchestrator.cs
   ?? Security/
      ?? Authentication/
         ?? LoginSecurityTests.cs
         ?? TokenValidationTests.cs
      ?? Authorization/
         ?? RoleBasedAccessTests.cs
         ?? TenantIsolationTests.cs
      ?? Vulnerability/
         ?? SqlInjectionTests.cs
         ?? XssProtectionTests.cs
      ?? TestInfrastructure/
         ?? SecurityTestBase.cs
         ?? VulnerabilityScanner.cs
   ?? Chaos/
      ?? Network/
         ?? LatencyInjectionTests.cs
         ?? PacketLossTests.cs
         ?? BandwidthLimitTests.cs
      ?? Infrastructure/
         ?? PodRestartTests.cs
         ?? NodeFailureTests.cs
         ?? DatabaseFailureTests.cs
      ?? TestInfrastructure/
         ?? ChaosTestBase.cs
         ?? ChaosEngine.cs
         ?? BlastRadiusController.cs
?? TestManagement/
   ?? DataManagement/
      ?? TestDataFactory.cs
      ?? TestDataBuilder.cs
      ?? TestDataSeeder.cs
      ?? TestDataManager.cs
   ?? Reporting/
      ?? TestReporter.cs
      ?? CoverageReporter.cs
      ?? PerformanceReporter.cs
      ?? SecurityReporter.cs
      ?? ChaosReporter.cs
   ?? Analytics/
      ?? TestTrendAnalyzer.cs
      ?? FlakyTestDetector.cs
      ?? PerformanceAnalyzer.cs
      ?? QualityGateAnalyzer.cs
   ?? Dashboard/
      ?? TestDashboard.html
      ?? PerformanceDashboard.html
      ?? SecurityDashboard.html
      ?? QualityDashboard.html
?? Utilities/
   ?? Configuration/
      ?? TestConfigManager.cs
      ?? EnvironmentManager.cs
      ?? ServiceDiscovery.cs
   ?? Monitoring/
      ?? TestLogger.cs
      ?? PerformanceMonitor.cs
      ?? ResourceMonitor.cs
      ?? NetworkMonitor.cs
   ?? Helpers/
      ?? TestHelper.cs
      ?? AssertionHelper.cs
      ?? DataHelper.cs
      ?? SecurityHelper.cs
```

---

## **2. LU?NG X? LÝ LÝ T??NG**

### **2.1 Configuration Management**

#### **Phase 1: Environment Configuration**
```json
// test-config.json
{
  "environments": {
    "development": {
      "services": {
        "corehub": "http://localhost:5010",
        "gateway": "http://localhost:5001",
        "khachlink": "http://localhost:3000",
        "shoperp": "http://localhost:3001"
      },
      "database": {
        "connectionString": "Data Source=test.db",
        "provider": "SQLite"
      },
      "authentication": {
        "enabled": false,
        "testToken": "test-jwt-token"
      }
    },
    "staging": {
      "services": {
        "corehub": "https://staging-api.vanan.com/corehub",
        "gateway": "https://staging-api.vanan.com/gateway",
        "khachlink": "https://staging.vanan.com",
        "shoperp": "https://staging-shoperp.vanan.com"
      },
      "database": {
        "connectionString": "Server=staging-db;Database=VanAn_Test;",
        "provider": "SqlServer"
      },
      "authentication": {
        "enabled": true,
        "clientId": "test-client-id",
        "clientSecret": "test-client-secret"
      }
    }
  },
  "testTiers": {
    "unit": {
      "enabled": true,
      "parallel": true,
      "timeout": 300,
      "retryCount": 0
    },
    "integration": {
      "enabled": true,
      "parallel": true,
      "timeout": 600,
      "retryCount": 2
    },
    "api": {
      "enabled": true,
      "parallel": true,
      "timeout": 300,
      "retryCount": 1
    },
    "e2e": {
      "enabled": true,
      "parallel": false,
      "timeout": 1200,
      "retryCount": 1
    },
    "performance": {
      "enabled": false,
      "parallel": false,
      "timeout": 1800,
      "retryCount": 0
    },
    "security": {
      "enabled": false,
      "parallel": true,
      "timeout": 900,
      "retryCount": 1
    },
    "chaos": {
      "enabled": false,
      "parallel": false,
      "timeout": 3600,
      "retryCount": 0
    }
  }
}
```

#### **Phase 2: Quality Gate Configuration**
```json
// quality-gate.json
{
  "thresholds": {
    "unit": {
      "coverage": {
        "minimum": 80,
        "target": 90
      },
      "successRate": {
        "minimum": 95,
        "target": 100
      },
      "executionTime": {
        "maximum": 300,
        "target": 180
      }
    },
    "integration": {
      "successRate": {
        "minimum": 90,
        "target": 95
      },
      "executionTime": {
        "maximum": 600,
        "target": 300
      }
    },
    "api": {
      "successRate": {
        "minimum": 95,
        "target": 100
      },
      "responseTime": {
        "p95": 500,
        "p99": 1000
      }
    },
    "e2e": {
      "successRate": {
        "minimum": 85,
        "target": 95
      },
      "executionTime": {
        "maximum": 1200,
        "target": 600
      }
    },
    "performance": {
      "throughput": {
        "minimum": 100,
        "target": 500
      },
      "responseTime": {
        "p95": 1000,
        "p99": 2000
      },
      "errorRate": {
        "maximum": 0.01,
        "target": 0.001
      }
    },
    "security": {
      "vulnerabilityCount": {
        "maximum": 0,
        "target": 0
      },
      "complianceScore": {
        "minimum": 90,
        "target": 100
      }
    }
  },
  "gates": {
    "preCommit": ["unit"],
    "pullRequest": ["unit", "integration"],
    "mergeToMain": ["unit", "integration", "api"],
    "deployToStaging": ["unit", "integration", "api", "e2e"],
    "deployToProduction": ["unit", "integration", "api", "e2e", "security"]
  }
}
```

---

### **2.2 Test Execution Engine**

#### **Phase 1: Test Orchestrator**
```csharp
// TestOrchestrator.cs
public class TestOrchestrator
{
    private readonly ITestConfigurationManager _configManager;
    private readonly ITestRunnerFactory _runnerFactory;
    private readonly ITestReporter _reporter;
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly ILogger<TestOrchestrator> _logger;

    public async Task<TestExecutionResult> ExecuteTestsAsync(
        TestExecutionRequest request)
    {
        var executionId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting test execution {ExecutionId}", executionId);

            // Load configuration
            var config = await _configManager.LoadConfigurationAsync(request.Environment);
            
            // Validate request
            ValidateExecutionRequest(request, config);

            // Initialize performance monitoring
            await _performanceMonitor.StartMonitoringAsync(executionId);

            // Execute test tiers
            var results = new List<TestTierResult>();
            
            foreach (var tier in request.TestTiers)
            {
                if (!config.TestTiers[tier].Enabled)
                {
                    _logger.LogInformation("Skipping disabled test tier {Tier}", tier);
                    continue;
                }

                var tierResult = await ExecuteTestTierAsync(tier, config, executionId);
                results.Add(tierResult);

                // Early termination if critical tier fails
                if (tierResult.Critical && !tierResult.Success)
                {
                    _logger.LogWarning("Critical test tier {Tier} failed, terminating execution", tier);
                    break;
                }
            }

            // Generate comprehensive report
            var executionResult = new TestExecutionResult
            {
                ExecutionId = executionId,
                Environment = request.Environment,
                TestTiers = results,
                TotalDuration = stopwatch.Elapsed,
                Success = results.All(r => r.Success || !r.Critical),
                PerformanceMetrics = await _performanceMonitor.GetMetricsAsync(executionId)
            };

            // Generate reports
            await _reporter.GenerateReportAsync(executionResult);

            // Check quality gates
            var qualityGateResult = await CheckQualityGatesAsync(executionResult, config);
            executionResult.QualityGateResult = qualityGateResult;

            _logger.LogInformation("Test execution {ExecutionId} completed in {Duration}", 
                executionId, stopwatch.Elapsed);

            return executionResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test execution {ExecutionId} failed", executionId);
            
            return new TestExecutionResult
            {
                ExecutionId = executionId,
                Environment = request.Environment,
                Success = false,
                Error = ex.Message,
                TotalDuration = stopwatch.Elapsed
            };
        }
        finally
        {
            await _performanceMonitor.StopMonitoringAsync(executionId);
        }
    }

    private async Task<TestTierResult> ExecuteTestTierAsync(
        TestTier tier, 
        TestConfiguration config, 
        Guid executionId)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Executing test tier {Tier} for execution {ExecutionId}", tier, executionId);

            // Get test runner
            var runner = _runnerFactory.CreateRunner(tier);
            
            // Prepare test context
            var context = new TestContext
            {
                ExecutionId = executionId,
                Tier = tier,
                Configuration = config,
                Environment = config.TestTiers[tier]
            };

            // Execute tests
            var result = await runner.ExecuteAsync(context);

            return new TestTierResult
            {
                Tier = tier,
                Success = result.Success,
                TestResults = result.TestResults,
                Duration = stopwatch.Elapsed,
                Coverage = result.Coverage,
                PerformanceMetrics = result.PerformanceMetrics,
                Critical = IsCriticalTier(tier)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test tier {Tier} execution failed", tier);
            
            return new TestTierResult
            {
                Tier = tier,
                Success = false,
                Duration = stopwatch.Elapsed,
                Error = ex.Message,
                Critical = IsCriticalTier(tier)
            };
        }
    }

    private void ValidateExecutionRequest(TestExecutionRequest request, TestConfiguration config)
    {
        if (string.IsNullOrEmpty(request.Environment))
        {
            throw new ArgumentException("Environment is required");
        }

        if (!config.Environments.ContainsKey(request.Environment))
        {
            throw new ArgumentException($"Environment {request.Environment} not configured");
        }

        if (!request.TestTiers.Any())
        {
            throw new ArgumentException("At least one test tier must be specified");
        }
    }

    private bool IsCriticalTier(TestTier tier)
    {
        return tier switch
        {
            TestTier.Unit => true,
            TestTier.Integration => true,
            TestTier.API => true,
            TestTier.E2E => false,
            TestTier.Performance => false,
            TestTier.Security => true,
            TestTier.Chaos => false,
            _ => false
        };
    }
}
```

---

### **2.3 Test Data Management**

#### **Phase 1: Test Data Factory**
```csharp
// TestDataFactory.cs
public class TestDataFactory
{
    private readonly ITestDataProvider _provider;
    private readonly ITestDataSeeder _seeder;
    private readonly ITestDataCleaner _cleaner;
    private readonly ILogger<TestDataFactory> _logger;

    public async Task<TestDataSet> CreateDataSetAsync(
        TestDataSetRequest request)
    {
        var dataSetId = Guid.NewGuid();
        
        try
        {
            _logger.LogInformation("Creating test data set {DataSetId}", dataSetId);

            var dataSet = new TestDataSet
            {
                Id = dataSetId,
                Name = request.Name,
                Environment = request.Environment,
                CreatedAt = DateTime.UtcNow
            };

            // Create entities
            foreach (var entityType in request.EntityTypes)
            {
                var entities = await CreateEntitiesAsync(entityType, request.Count);
                dataSet.Entities[entityType] = entities;
            }

            // Create relationships
            await CreateRelationshipsAsync(dataSet, request.Relationships);

            // Seed data
            await _seeder.SeedAsync(dataSet);

            // Register for cleanup
            await _cleaner.RegisterForCleanupAsync(dataSetId);

            _logger.LogInformation("Test data set {DataSetId} created successfully", dataSetId);

            return dataSet;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create test data set {DataSetId}", dataSetId);
            await _cleaner.CleanupAsync(dataSetId);
            throw;
        }
    }

    private async Task<List<TestEntity>> CreateEntitiesAsync(
        TestEntityType entityType, 
        int count)
    {
        return entityType switch
        {
            TestEntityType.Customer => await CreateCustomersAsync(count),
            TestEntityType.Product => await CreateProductsAsync(count),
            TestEntityType.Order => await CreateOrdersAsync(count),
            TestEntityType.Shop => await CreateShopsAsync(count),
            _ => throw new NotSupportedException($"Entity type {entityType} not supported")
        };
    }

    private async Task<List<TestEntity>> CreateCustomersAsync(int count)
    {
        var customers = new List<TestEntity>();
        
        for (int i = 0; i < count; i++)
        {
            var customer = new TestEntity
            {
                Type = TestEntityType.Customer,
                Data = new Dictionary<string, object>
                {
                    ["Id"] = Guid.NewGuid(),
                    ["FullName"] = $"Test Customer {i}",
                    ["PhoneNumber"] = $"0123456{i:D4}",
                    ["Email"] = $"customer{i}@test.com",
                    ["DeviceId"] = Guid.NewGuid(),
                    ["LoyaltyPoints"] = 0,
                    ["CustomerTier"] = "Bronze",
                    ["IsActive"] = true,
                    ["CreatedAt"] = DateTime.UtcNow
                }
            };
            
            customers.Add(customer);
        }

        return customers;
    }

    private async Task<List<TestEntity>> CreateProductsAsync(int count)
    {
        var products = new List<TestEntity>();
        var categories = new[] { "Coffee", "Tea", "Juice", "Snack", "Dessert" };
        
        for (int i = 0; i < count; i++)
        {
            var product = new TestEntity
            {
                Type = TestEntityType.Product,
                Data = new Dictionary<string, object>
                {
                    ["Id"] = Guid.NewGuid(),
                    ["Name"] = $"Test Product {i}",
                    ["Description"] = $"Test product description {i}",
                    ["Category"] = categories[i % categories.Length],
                    ["BasePrice"] = 10000 + (i * 1000),
                    ["VatRate"] = 0.10m,
                    ["ImageUrl"] = $"https://test.com/product{i}.jpg",
                    ["IsActive"] = true,
                    ["CreatedAt"] = DateTime.UtcNow
                }
            };
            
            products.Add(product);
        }

        return products;
    }

    private async Task<List<TestEntity>> CreateOrdersAsync(int count)
    {
        var orders = new List<TestEntity>();
        var orderTypes = new[] { "DINEIN", "TAKEAWAY", "DELIVERY" };
        
        for (int i = 0; i < count; i++)
        {
            var order = new TestEntity
            {
                Type = TestEntityType.Order,
                Data = new Dictionary<string, object>
                {
                    ["Id"] = Guid.NewGuid(),
                    ["CustomerId"] = Guid.NewGuid(),
                    ["OrderType"] = orderTypes[i % orderTypes.Length],
                    ["Status"] = "Draft",
                    ["SubTotal"] = 10000 + (i * 5000),
                    ["VatAmount"] = (10000 + (i * 5000)) * 0.10m,
                    ["TotalAmount"] = (10000 + (i * 5000)) * 1.10m,
                    ["CustomerNotes"] = $"Test order notes {i}",
                    ["OrderDate"] = DateTime.UtcNow,
                    ["CreatedAt"] = DateTime.UtcNow
                }
            };
            
            orders.Add(order);
        }

        return orders;
    }

    private async Task CreateRelationshipsAsync(
        TestDataSet dataSet, 
        List<TestRelationshipRequest> relationships)
    {
        foreach (var relationship in relationships)
        {
            await CreateRelationshipAsync(dataSet, relationship);
        }
    }

    private async Task CreateRelationshipAsync(
        TestDataSet dataSet, 
        TestRelationshipRequest relationship)
    {
        switch (relationship.Type)
        {
            case TestRelationshipType.CustomerOrders:
                await CreateCustomerOrdersAsync(dataSet, relationship);
                break;
            case TestRelationshipType.OrderItems:
                await CreateOrderItemsAsync(dataSet, relationship);
                break;
            default:
                throw new NotSupportedException($"Relationship type {relationship.Type} not supported");
        }
    }
}
```

---

### **2.4 Performance Testing**

#### **Phase 1: Load Test Engine**
```csharp
// LoadTestEngine.cs
public class LoadTestEngine
{
    private readonly ITestConfigurationManager _configManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly ILogger<LoadTestEngine> _logger;

    public async Task<LoadTestResult> ExecuteLoadTestAsync(
        LoadTestRequest request)
    {
        var testId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting load test {TestId}", testId);

            // Load configuration
            var config = await _configManager.LoadConfigurationAsync(request.Environment);
            
            // Initialize performance monitoring
            await _performanceMonitor.StartMonitoringAsync(testId);

            // Create test scenarios
            var scenarios = await CreateScenariosAsync(request.Scenarios);

            // Execute load test
            var results = new List<ScenarioResult>();
            
            foreach (var scenario in scenarios)
            {
                var result = await ExecuteScenarioAsync(scenario, config);
                results.Add(result);
            }

            // Aggregate results
            var loadTestResult = new LoadTestResult
            {
                TestId = testId,
                Environment = request.Environment,
                Scenarios = results,
                TotalDuration = stopwatch.Elapsed,
                PerformanceMetrics = await _performanceMonitor.GetMetricsAsync(testId)
            };

            // Validate against thresholds
            var validationResult = await ValidateThresholdsAsync(loadTestResult, config);
            loadTestResult.ValidationResult = validationResult;

            _logger.LogInformation("Load test {TestId} completed in {Duration}", testId, stopwatch.Elapsed);

            return loadTestResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load test {TestId} failed", testId);
            throw;
        }
        finally
        {
            await _performanceMonitor.StopMonitoringAsync(testId);
        }
    }

    private async Task<ScenarioResult> ExecuteScenarioAsync(
        LoadTestScenario scenario, 
        TestConfiguration config)
    {
        var stopwatch = Stopwatch.StartNew();
        var results = new List<RequestResult>();

        // Create concurrent users
        var tasks = new List<Task<RequestResult>>();
        
        for (int i = 0; i < scenario.ConcurrentUsers; i++)
        {
            for (int j = 0; j < scenario.IterationsPerUser; j++)
            {
                tasks.Add(ExecuteRequestAsync(scenario.Request, config));
            }
        }

        // Wait for all requests to complete
        var allResults = await Task.WhenAll(tasks);
        results.AddRange(allResults);

        // Calculate metrics
        var successfulRequests = results.Count(r => r.Success);
        var failedRequests = results.Count(r => !r.Success);
        var responseTimes = results.Where(r => r.Success).Select(r => r.ResponseTime).ToArray();

        return new ScenarioResult
        {
            ScenarioName = scenario.Name,
            Duration = stopwatch.Elapsed,
            TotalRequests = results.Count,
            SuccessfulRequests = successfulRequests,
            FailedRequests = failedRequests,
            SuccessRate = (double)successfulRequests / results.Count,
            AverageResponseTime = responseTimes.Any() ? responseTimes.Average() : 0,
            P95ResponseTime = CalculatePercentile(responseTimes, 0.95),
            P99ResponseTime = CalculatePercentile(responseTimes, 0.99),
            RequestsPerSecond = results.Count / stopwatch.Elapsed.TotalSeconds,
            Results = results
        };
    }

    private async Task<RequestResult> ExecuteRequestAsync(
        LoadTestRequest request, 
        TestConfiguration config)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            
            // Prepare request
            var httpRequest = new HttpRequestMessage(
                new HttpMethod(request.Method), 
                request.Url);

            if (request.Headers != null)
            {
                foreach (var header in request.Headers)
                {
                    httpRequest.Headers.Add(header.Key, header.Value);
                }
            }

            if (request.Body != null)
            {
                httpRequest.Content = new StringContent(
                    JsonSerializer.Serialize(request.Body),
                    Encoding.UTF8,
                    "application/json");
            }

            // Execute request
            var response = await httpClient.SendAsync(httpRequest);
            var responseTime = stopwatch.ElapsedMilliseconds;

            return new RequestResult
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                ResponseTime = responseTime,
                Content = await response.Content.ReadAsStringAsync(),
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new RequestResult
            {
                Success = false,
                ResponseTime = stopwatch.ElapsedMilliseconds,
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    private double CalculatePercentile(long[] values, double percentile)
    {
        if (!values.Any()) return 0;
        
        var sorted = values.OrderBy(x => x).ToArray();
        var index = (int)Math.Ceiling(percentile * sorted.Length) - 1;
        return sorted[Math.Max(0, index)];
    }

    private async Task<ValidationResult> ValidateThresholdsAsync(
        LoadTestResult result, 
        TestConfiguration config)
    {
        var thresholds = config.PerformanceThresholds;
        var violations = new List<ThresholdViolation>();

        foreach (var scenario in result.Scenarios)
        {
            // Check success rate
            if (scenario.SuccessRate < thresholds.SuccessRate.Maximum)
            {
                violations.Add(new ThresholdViolation
                {
                    Metric = "SuccessRate",
                    Scenario = scenario.ScenarioName,
                    Actual = scenario.SuccessRate,
                    Expected = thresholds.SuccessRate.Maximum,
                    Severity = ViolationSeverity.Critical
                });
            }

            // Check response times
            if (scenario.P95ResponseTime > thresholds.ResponseTime.P95)
            {
                violations.Add(new ThresholdViolation
                {
                    Metric = "P95ResponseTime",
                    Scenario = scenario.ScenarioName,
                    Actual = scenario.P95ResponseTime,
                    Expected = thresholds.ResponseTime.P95,
                    Severity = ViolationSeverity.High
                });
            }

            // Check throughput
            if (scenario.RequestsPerSecond < thresholds.Throughput.Minimum)
            {
                violations.Add(new ThresholdViolation
                {
                    Metric = "Throughput",
                    Scenario = scenario.ScenarioName,
                    Actual = scenario.RequestsPerSecond,
                    Expected = thresholds.Throughput.Minimum,
                    Severity = ViolationSeverity.Medium
                });
            }
        }

        return new ValidationResult
        {
            Success = !violations.Any(v => v.Severity == ViolationSeverity.Critical),
            Violations = violations,
            OverallScore = CalculateOverallScore(violations)
        };
    }

    private double CalculateOverallScore(List<ThresholdViolation> violations)
    {
        if (!violations.Any()) return 100;

        var deductions = violations.Sum(v => v.Severity switch
        {
            ViolationSeverity.Critical => 25,
            ViolationSeverity.High => 15,
            ViolationSeverity.Medium => 10,
            ViolationSeverity.Low => 5,
            _ => 0
        });

        return Math.Max(0, 100 - deductions);
    }
}
```

---

### **2.5 Chaos Testing**

#### **Phase 1: Chaos Engine**
```csharp
// ChaosEngine.cs
public class ChaosEngine
{
    private readonly IChaosExperimentFactory _experimentFactory;
    private readonly IBlastRadiusController _blastRadiusController;
    private readonly IChaosMonitor _chaosMonitor;
    private readonly ILogger<ChaosEngine> _logger;

    public async Task<ChaosTestResult> ExecuteChaosTestAsync(
        ChaosTestRequest request)
    {
        var testId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting chaos test {TestId}", testId);

            // Initialize monitoring
            await _chaosMonitor.StartMonitoringAsync(testId);

            // Create experiments
            var experiments = await CreateExperimentsAsync(request.Experiments);

            // Execute chaos test
            var results = new List<ExperimentResult>();
            
            foreach (var experiment in experiments)
            {
                var result = await ExecuteExperimentAsync(experiment, request);
                results.Add(result);

                // Check blast radius
                if (result.BlastRadiusExceeded)
                {
                    _logger.LogWarning("Blast radius exceeded for experiment {ExperimentName}", experiment.Name);
                    break;
                }
            }

            // Aggregate results
            var chaosTestResult = new ChaosTestResult
            {
                TestId = testId,
                Environment = request.Environment,
                Experiments = results,
                TotalDuration = stopwatch.Elapsed,
                SystemResilience = await CalculateResilienceScoreAsync(results),
                RecoveryMetrics = await _chaosMonitor.GetRecoveryMetricsAsync(testId)
            };

            _logger.LogInformation("Chaos test {TestId} completed in {Duration}", testId, stopwatch.Elapsed);

            return chaosTestResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chaos test {TestId} failed", testId);
            throw;
        }
        finally
        {
            await _chaosMonitor.StopMonitoringAsync(testId);
        }
    }

    private async Task<ExperimentResult> ExecuteExperimentAsync(
        ChaosExperiment experiment, 
        ChaosTestRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Executing chaos experiment {ExperimentName}", experiment.Name);

            // Set blast radius
            await _blastRadiusController.SetBlastRadiusAsync(experiment.BlastRadius);

            // Record baseline metrics
            var baseline = await _chaosMonitor.GetBaselineMetricsAsync();

            // Inject chaos
            await experiment.InjectAsync();

            // Monitor system during chaos
            var chaosMetrics = await _chaosMonitor.MonitorDuringChaosAsync(experiment.Duration);

            // Recover from chaos
            await experiment.RecoverAsync();

            // Monitor recovery
            var recoveryMetrics = await _chaosMonitor.MonitorRecoveryAsync(experiment.RecoveryTime);

            // Calculate blast radius impact
            var blastRadiusImpact = await _blastRadiusController.CalculateImpactAsync();

            return new ExperimentResult
            {
                ExperimentName = experiment.Name,
                Duration = stopwatch.Elapsed,
                Success = experiment.Success,
                BaselineMetrics = baseline,
                ChaosMetrics = chaosMetrics,
                RecoveryMetrics = recoveryMetrics,
                BlastRadiusExceeded = blastRadiusImpact.Exceeded,
                BlastRadiusImpact = blastRadiusImpact
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chaos experiment {ExperimentName} failed", experiment.Name);
            
            // Force recovery
            try
            {
                await experiment.RecoverAsync();
            }
            catch (Exception recoveryEx)
            {
                _logger.LogError(recoveryEx, "Failed to recover from chaos experiment {ExperimentName}", experiment.Name);
            }

            return new ExperimentResult
            {
                ExperimentName = experiment.Name,
                Duration = stopwatch.Elapsed,
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<List<ChaosExperiment>> CreateExperimentsAsync(
        List<ChaosExperimentRequest> requests)
    {
        var experiments = new List<ChaosExperiment>();

        foreach (var request in requests)
        {
            var experiment = await _experimentFactory.CreateExperimentAsync(request);
            experiments.Add(experiment);
        }

        return experiments;
    }

    private async Task<double> CalculateResilienceScoreAsync(List<ExperimentResult> results)
    {
        if (!results.Any()) return 100;

        var successfulExperiments = results.Count(r => r.Success);
        var baseScore = (double)successfulExperiments / results.Count * 100;

        // Deduct points for blast radius violations
        var blastRadiusViolations = results.Count(r => r.BlastRadiusExceeded);
        var blastRadiusDeduction = blastRadiusViolations * 20;

        // Deduct points for slow recovery
        var slowRecoveries = results.Count(r => r.RecoveryMetrics.RecoveryTime > TimeSpan.FromMinutes(5));
        var recoveryDeduction = slowRecoveries * 10;

        return Math.Max(0, baseScore - blastRadiusDeduction - recoveryDeduction);
    }
}
```

---

### **2.6 Security Testing**

#### **Phase 1: Security Test Engine**
```csharp
// SecurityTestEngine.cs
public class SecurityTestEngine
{
    private readonly IVulnerabilityScanner _vulnerabilityScanner;
    private readonly IAuthenticationTester _authenticationTester;
    private readonly IAuthorizationTester _authorizationTester;
    private readonly ILogger<SecurityTestEngine> _logger;

    public async Task<SecurityTestResult> ExecuteSecurityTestAsync(
        SecurityTestRequest request)
    {
        var testId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting security test {TestId}", testId);

            var results = new List<SecurityTestResult>();

            // Authentication tests
            if (request.TestCategories.Contains(SecurityTestCategory.Authentication))
            {
                var authResults = await _authenticationTester.TestAuthenticationAsync(request);
                results.AddRange(authResults);
            }

            // Authorization tests
            if (request.TestCategories.Contains(SecurityTestCategory.Authorization))
            {
                var authzResults = await _authorizationTester.TestAuthorizationAsync(request);
                results.AddRange(authzResults);
            }

            // Vulnerability scanning
            if (request.TestCategories.Contains(SecurityTestCategory.Vulnerability))
            {
                var vulnResults = await _vulnerabilityScanner.ScanVulnerabilitiesAsync(request);
                results.AddRange(vulnResults);
            }

            // Aggregate results
            var securityTestResult = new SecurityTestResult
            {
                TestId = testId,
                Environment = request.Environment,
                TestResults = results,
                TotalDuration = stopwatch.Elapsed,
                SecurityScore = CalculateSecurityScore(results),
                ComplianceStatus = await CheckComplianceAsync(results)
            };

            _logger.LogInformation("Security test {TestId} completed in {Duration}", testId, stopwatch.Elapsed);

            return securityTestResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Security test {TestId} failed", testId);
            throw;
        }
    }

    private double CalculateSecurityScore(List<SecurityTestResult> results)
    {
        if (!results.Any()) return 100;

        var totalTests = results.Count;
        var passedTests = results.Count(r => r.Success);
        var baseScore = (double)passedTests / totalTests * 100;

        // Deduct points for vulnerabilities
        var criticalVulns = results.Count(r => r.Severity == VulnerabilitySeverity.Critical);
        var highVulns = results.Count(r => r.Severity == VulnerabilitySeverity.High);
        var mediumVulns = results.Count(r => r.Severity == VulnerabilitySeverity.Medium);

        var deduction = (criticalVulns * 25) + (highVulns * 15) + (mediumVulns * 10);

        return Math.Max(0, baseScore - deduction);
    }

    private async Task<ComplianceStatus> CheckComplianceAsync(List<SecurityTestResult> results)
    {
        // Check against security standards (OWASP, NIST, etc.)
        var complianceChecks = new List<ComplianceCheck>
        {
            new ComplianceCheck { Standard = "OWASP Top 10", Passed = !results.Any(r => r.OwaspCategory != null) },
            new ComplianceCheck { Standard = "NIST Cybersecurity", Passed = CalculateNISTScore(results) >= 90 },
            new ComplianceCheck { Standard = "GDPR", Passed = CheckGDPRCompliance(results) },
            new ComplianceCheck { Standard = "PCI DSS", Passed = CheckPCIDSSCompliance(results) }
        };

        return new ComplianceStatus
        {
            OverallCompliant = complianceChecks.All(c => c.Passed),
            Checks = complianceChecks,
            ComplianceScore = complianceChecks.Average(c => c.Passed ? 100 : 0)
        };
    }
}
```

---

## **3. REPORTING & ANALYTICS**

### **3.1 Test Dashboard**
```typescript
// TestDashboard.ts
export class TestDashboard {
    private testResults: TestResult[] = [];
    private performanceMetrics: PerformanceMetrics[] = [];
    private securityMetrics: SecurityMetrics[] = [];

    async initialize(): Promise<void> {
        await this.loadTestResults();
        await this.loadPerformanceMetrics();
        await this.loadSecurityMetrics();
        this.setupRealTimeUpdates();
        this.renderDashboard();
    }

    private renderDashboard(): void {
        this.renderTestSummary();
        this.renderPerformanceCharts();
        this.renderSecurityStatus();
        this.renderQualityGateStatus();
        this.renderTrendAnalysis();
    }

    private renderTestSummary(): void {
        const summary = this.calculateTestSummary();
        
        document.getElementById('test-summary').innerHTML = `
            <div class="summary-card">
                <h3>Test Summary</h3>
                <div class="metrics">
                    <div class="metric">
                        <span class="value">${summary.totalTests}</span>
                        <span class="label">Total Tests</span>
                    </div>
                    <div class="metric">
                        <span class="value ${summary.successRate >= 95 ? 'success' : 'warning'}">
                            ${summary.successRate.toFixed(1)}%
                        </span>
                        <span class="label">Success Rate</span>
                    </div>
                    <div class="metric">
                        <span class="value">${summary.coverage.toFixed(1)}%</span>
                        <span class="label">Coverage</span>
                    </div>
                    <div class="metric">
                        <span class="value">${summary.duration}</span>
                        <span class="label">Duration</span>
                    </div>
                </div>
            </div>
        `;
    }

    private renderPerformanceCharts(): void {
        const container = document.getElementById('performance-charts');
        
        // Response time chart
        this.createLineChart(container, 'response-times', 
            this.performanceMetrics.map(m => m.timestamp),
            this.performanceMetrics.map(m => m.averageResponseTime),
            'Average Response Time (ms)');
        
        // Throughput chart
        this.createLineChart(container, 'throughput',
            this.performanceMetrics.map(m => m.timestamp),
            this.performanceMetrics.map(m => m.throughput),
            'Throughput (req/s)');
        
        // Error rate chart
        this.createLineChart(container, 'error-rate',
            this.performanceMetrics.map(m => m.timestamp),
            this.performanceMetrics.map(m => m.errorRate * 100),
            'Error Rate (%)');
    }

    private renderSecurityStatus(): void {
        const security = this.calculateSecuritySummary();
        
        document.getElementById('security-status').innerHTML = `
            <div class="security-card">
                <h3>Security Status</h3>
                <div class="security-metrics">
                    <div class="metric">
                        <span class="value ${security.score >= 90 ? 'success' : 'danger'}">
                            ${security.score.toFixed(1)}
                        </span>
                        <span class="label">Security Score</span>
                    </div>
                    <div class="metric">
                        <span class="value ${security.vulnerabilities === 0 ? 'success' : 'warning'}">
                            ${security.vulnerabilities}
                        </span>
                        <span class="label">Vulnerabilities</span>
                    </div>
                    <div class="metric">
                        <span class="value ${security.complianceScore >= 90 ? 'success' : 'warning'}">
                            ${security.complianceScore.toFixed(1)}%
                        </span>
                        <span class="label">Compliance</span>
                    </div>
                </div>
                ${security.vulnerabilities > 0 ? this.renderVulnerabilityList(security.vulnerabilityList) : ''}
            </div>
        `;
    }

    private renderQualityGateStatus(): void {
        const qualityGate = this.calculateQualityGateStatus();
        
        document.getElementById('quality-gate').innerHTML = `
            <div class="quality-gate-card">
                <h3>Quality Gate Status</h3>
                <div class="gate-status ${qualityGate.passed ? 'passed' : 'failed'}">
                    <span class="status-icon">${qualityGate.passed ? '??' : '??'}</span>
                    <span class="status-text">${qualityGate.passed ? 'PASSED' : 'FAILED'}</span>
                </div>
                <div class="gate-checks">
                    ${qualityGate.checks.map(check => `
                        <div class="gate-check ${check.passed ? 'passed' : 'failed'}">
                            <span class="check-name">${check.name}</span>
                            <span class="check-status">${check.passed ? '??' : '??'}</span>
                            <span class="check-value">${check.value}</span>
                        </div>
                    `).join('')}
                </div>
            </div>
        `;
    }

    private setupRealTimeUpdates(): void {
        const eventSource = new EventSource('/api/test-results/stream');
        
        eventSource.onmessage = (event) => {
            const result = JSON.parse(event.data);
            this.updateDashboard(result);
        };
    }

    private updateDashboard(result: TestResult): void {
        this.testResults.push(result);
        this.renderDashboard();
    }
}
```

---

## **4. CI/CD INTEGRATION**

### **4.1 GitHub Actions Workflow**
```yaml
# .github/workflows/quality-gate.yml
name: Quality Gate

on:
  pull_request:
    branches: [ main, develop ]
  push:
    branches: [ main ]

jobs:
  test:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        test-tier: [unit, integration, api, e2e]
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
    
    - name: Setup Node.js
      uses: actions/setup-node@v3
      with:
        node-version: '18.x'
    
    - name: Install dependencies
      run: |
        dotnet restore
        npm ci
    
    - name: Run tests
      run: |
        case "${{ matrix.test-tier }}" in
          "unit")
            dotnet test --logger trx --results-directory TestResults
            ;;
          "integration")
            dotnet test --logger trx --results-directory TestResults --filter Category=Integration
            ;;
          "api")
            dotnet test --logger trx --results-directory TestResults --filter Category=API
            ;;
          "e2e")
            npm run test:e2e
            ;;
        esac
    
    - name: Upload test results
      uses: actions/upload-artifact@v3
      if: always()
      with:
        name: test-results-${{ matrix.test-tier }}
        path: TestResults/
    
    - name: Publish test results
      uses: dorny/test-reporter@v1
      if: always()
      with:
        name: Test Results (${{ matrix.test-tier }})
        path: TestResults/**/*.trx
        reporter: dotnet-trx

  quality-gate:
    needs: test
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Download all test results
      uses: actions/download-artifact@v3
    
    - name: Run quality gate
      run: |
        npm run test:quality-gate
    
    - name: Generate quality report
      run: |
        npm run report
    
    - name: Upload quality report
      uses: actions/upload-artifact@v3
      with:
        name: quality-report
        path: reports/
    
    - name: Comment PR with quality status
      uses: actions/github-script@v6
      with:
        script: |
          const fs = require('fs');
          const qualityReport = JSON.parse(fs.readFileSync('reports/quality-report.json', 'utf8'));
          
          const comment = `
          ## Quality Gate Status: ${qualityReport.passed ? '?? PASSED' : '?? FAILED'}
          
          ### Test Results
          - Unit Tests: ${qualityReport.tests.unit.successRate}% (${qualityReport.tests.unit.passed}/${qualityReport.tests.unit.total})
          - Integration Tests: ${qualityReport.tests.integration.successRate}% (${qualityReport.tests.integration.passed}/${qualityReport.tests.integration.total})
          - API Tests: ${qualityReport.tests.api.successRate}% (${qualityReport.tests.api.passed}/${qualityReport.tests.api.total})
          - E2E Tests: ${qualityReport.tests.e2e.successRate}% (${qualityReport.tests.e2e.passed}/${qualityReport.tests.e2e.total})
          
          ### Code Coverage
          - Overall Coverage: ${qualityReport.coverage.overall}%
          - Unit Coverage: ${qualityReport.coverage.unit}%
          
          ${qualityReport.violations.length > 0 ? '### Violations\n' + qualityReport.violations.map(v => `- ${v.type}: ${v.message}`).join('\n') : ''}
          `;
          
          github.rest.issues.createComment({
            issue_number: context.issue.number,
            owner: context.repo.owner,
            repo: context.repo.repo,
            body: comment
          });
```

---

## **5. SUMMARY**

### **5.1 Key Features of Ideal Testing Module**
- **Unified Testing Framework:** Single framework for all test types
- **7-Layer Testing:** Unit, Integration, API, E2E, Performance, Security, Chaos
- **Dynamic Configuration:** Environment-aware test configuration
- **Test Data Management:** Automated test data generation and cleanup
- **Performance Monitoring:** Real-time performance metrics
- **Security Testing:** Comprehensive vulnerability scanning
- **Chaos Engineering:** Resilience testing with blast radius control
- **Quality Gates:** Automated quality checks and thresholds
- **Real-time Dashboard:** Live test results and analytics
- **CI/CD Integration:** Seamless pipeline integration

### **5.2 Technical Excellence**
- **Clean Architecture:** Proper separation of concerns
- **Dependency Injection:** Flexible test configuration
- **Async/Await:** Non-blocking test execution
- **Error Handling:** Robust failure recovery
- **Logging:** Comprehensive test logging
- **Monitoring:** Real-time system monitoring
- **Scalability:** Parallel test execution
- **Maintainability:** Modular test design

### **5.3 Business Value**
- **Quality Assurance:** High code quality and reliability
- **Risk Mitigation:** Early detection of issues
- **Performance Validation:** Ensure system scalability
- **Security Compliance:** Meet security standards
- **Resilience Testing:** Ensure system reliability
- **Continuous Integration:** Automated quality gates
- **Developer Experience:** Easy test creation and execution

This ideal Testing module provides a comprehensive, professional-grade testing framework that ensures code quality, performance, security, and resilience while maintaining excellent developer experience and CI/CD integration.
