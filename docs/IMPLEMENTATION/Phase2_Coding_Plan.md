# Phase 2 Coding Plan - Guard Check Upgrade

**Version:** 1.0  
**Date:** 2026-06-06  
**Status:** Draft  
**Target:** guard-check.ps1 v7.2 → v8.0 (Phase 2)

## Executive Summary

Phase 2 focuses on infrastructure improvements for proper testing and static analysis using Roslyn Analyzers. Based on actual codebase analysis, Phase 2 addresses:
- NATS dependency resolution (GitHub Actions Services, not docker-compose)
- Roslyn Analyzer implementation to replace architecture-guard.ps1
- Integration tests addition to fast gate
- CI workflow updates

**Key Correction from Original Plan:**
- Integration tests already use In-Memory Database (no SQLite file locking issue)
- guard-check runs on Windows (no case sensitivity issue)
- WebApplicationFactory already has DbContext override (no critical DI debt)
- Only NATS dependency needs resolution

## Phase 2.1: NATS Dependency Resolution

### Current State

**NATS Usage in Codebase:**
```csharp
// 3_CoreHub/Services/Events/SimpleAccountingEventHandler.cs line 137
string natsUrl = "nats://localhost:4222"; // TODO: Make configurable
IConnection connection = new ConnectionFactory().CreateConnection(natsUrl);
```

**Sprint 3 E-Invoice Dependencies:**
- Outbox pattern implementation requires message broker
- EInvoiceWorker processes Outbox events
- CircuitBreaker pattern for NATS failure handling
- Reconnection handling for NATS client

### Critical Integration Test Requirements

**Why Mock NATS is Insufficient:**
- Mocking only proves "code calls Publish" - not end-to-end behavior
- Missing critical scenarios:
  - NATS connection loss → Circuit Breaker transition
  - Circuit Breaker OPEN → Outbox Worker pause
  - NATS reconnection → Message queue integrity
  - Circuit Breaker HALF_OPEN → Retry behavior

**Solution:** Use real NATS service in CI (GitHub Actions Services)

#### 2.1.1 Update ci.yml with NATS Service

```yaml
# .github/workflows/ci.yml
jobs:
  integration-tests:
    runs-on: ubuntu-latest
    needs: build
    timeout-minutes: 15
    
    services:
      nats:
        image: nats:latest
        ports:
          - 4222:4222
        options: >-
          --name ci-nats
          -js
    
    steps:
      - name: Checkout
        uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}
      
      - name: Wait for NATS
        run: |
          timeout 30 bash -c 'until nc -z localhost 4222; do sleep 1; done'
          echo "NATS is ready"
      
      - name: Run Integration Tests
        run: dotnet test 6_Tests/VanAn.Integration.Tests/ --configuration Release
        env:
          NATS_URL: nats://localhost:4222
```

#### 2.1.2 Make NATS URL Configurable

**File:** `3_CoreHub/Services/Events/SimpleAccountingEventHandler.cs`

```csharp
// Before
string natsUrl = "nats://localhost:4222"; // TODO: Make configurable

// After
string natsUrl = _configuration["NATS:Url"] ?? "nats://localhost:4222";
```

**File:** `appsettings.json` (add to each project)

```json
{
  "NATS": {
    "Url": "nats://localhost:4222"
  }
}
```

**File:** `appsettings.Test.json` (for test projects)

```json
{
  "NATS": {
    "Url": "nats://localhost:4222"
  }
}
```

#### 2.1.3 Circuit Breaker Integration Tests with Real NATS

**File:** `6_Tests/VanAn.Integration.Tests/Services/CircuitBreakerIntegrationTests.cs` (NEW)

```csharp
using Xunit;
using Xunit.Abstractions;
using NATS.Client;
using Microsoft.Extensions.DependencyInjection;
using VanAn.CoreHub.Services.Orchestration;

namespace VanAn.Integration.Tests.Services;

/// <summary>
/// Integration tests for Circuit Breaker with real NATS service
/// Tests critical scenarios: connection loss, state transitions, reconnection
/// </summary>
public class CircuitBreakerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly IConnection _natsConnection;
    private readonly ICircuitBreakerService _circuitBreaker;

    public CircuitBreakerIntegrationTests(
        CustomWebApplicationFactory factory,
        ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        
        var scope = _factory.Services.CreateScope();
        _natsConnection = scope.ServiceProvider.GetRequiredService<IConnection>();
        _circuitBreaker = scope.ServiceProvider.GetRequiredService<ICircuitBreakerService>();
    }

    [Fact]
    public async Task CircuitBreaker_TransitionsToOpen_OnNatsConnectionLoss()
    {
        // Arrange
        _output.WriteLine("Testing Circuit Breaker transition to OPEN on NATS connection loss");
        
        // Act: Simulate NATS connection loss
        _natsConnection.Close();
        
        // Wait for Circuit Breaker to detect failure
        await Task.Delay(5000);
        
        // Assert: Circuit Breaker should be OPEN
        var state = _circuitBreaker.GetState();
        Assert.Equal(CircuitBreakerState.Open, state);
        
        _output.WriteLine($"Circuit Breaker state after NATS loss: {state}");
    }

    [Fact]
    public async Task CircuitBreaker_PausesOutboxWorker_WhenOpen()
    {
        // Arrange
        _output.WriteLine("Testing Outbox Worker pause when Circuit Breaker is OPEN");
        
        // Force Circuit Breaker to OPEN
        _circuitBreaker.ForceOpen();
        
        // Act: Try to process outbox events
        var outboxWorker = _factory.Services.GetRequiredService<EInvoiceWorker>();
        var processedCount = await outboxWorker.ProcessBatchAsync(10);
        
        // Assert: Outbox Worker should not process events when CB is OPEN
        Assert.Equal(0, processedCount);
        
        _output.WriteLine($"Outbox events processed when CB OPEN: {processedCount}");
    }

    [Fact]
    public async Task CircuitBreaker_TransitionsToHalfOpen_AfterTimeout()
    {
        // Arrange
        _output.WriteLine("Testing Circuit Breaker transition to HALF_OPEN after timeout");
        
        // Force Circuit Breaker to OPEN
        _circuitBreaker.ForceOpen();
        
        // Wait for timeout period (configured in CircuitBreakerService)
        await Task.Delay(_circuitBreaker.OpenTimeout);
        
        // Act: Attempt a test call
        var success = await _circuitBreaker.ExecuteAsync(async () =>
        {
            // Simulate successful NATS call
            return true;
        });
        
        // Assert: Circuit Breaker should transition to HALF_OPEN
        var state = _circuitBreaker.GetState();
        Assert.Equal(CircuitBreakerState.HalfOpen, state);
        
        _output.WriteLine($"Circuit Breaker state after timeout: {state}");
    }

    [Fact]
    public async Task CircuitBreaker_RestartsOutboxWorker_WhenClosed()
    {
        // Arrange
        _output.WriteLine("Testing Outbox Worker restart when Circuit Breaker is CLOSED");
        
        // Force Circuit Breaker to OPEN
        _circuitBreaker.ForceOpen();
        
        // Wait for timeout and successful retry
        await Task.Delay(_circuitBreaker.OpenTimeout + 1000);
        
        // Simulate successful NATS reconnection
        _circuitBreaker.RecordSuccess();
        
        // Act: Try to process outbox events
        var outboxWorker = _factory.Services.GetRequiredService<EInvoiceWorker>();
        var processedCount = await outboxWorker.ProcessBatchAsync(10);
        
        // Assert: Outbox Worker should process events when CB is CLOSED
        Assert.True(processedCount > 0);
        
        _output.WriteLine($"Outbox events processed when CB CLOSED: {processedCount}");
    }

    [Fact]
    public async Task NatsReconnection_MaintainsMessageQueueIntegrity()
    {
        // Arrange
        _output.WriteLine("Testing NATS reconnection maintains message queue integrity");
        
        var subject = "test.invoice.submitted";
        var messageCount = 10;
        var receivedMessages = new List<string>();
        
        // Subscribe to subject
        using var subscription = _natsConnection.SubscribeAsync(subject, (sender, args) =>
        {
            receivedMessages.Add(System.Text.Encoding.UTF8.GetString(args.Message.Data));
        });
        
        // Publish messages before disconnection
        for (int i = 0; i < messageCount; i++)
        {
            var message = $"message-{i}";
            _natsConnection.Publish(subject, System.Text.Encoding.UTF8.GetBytes(message));
        }
        
        // Close connection
        _natsConnection.Close();
        
        // Wait and reconnect
        await Task.Delay(2000);
        _natsConnection = new ConnectionFactory().CreateConnection(_natsConnection.Opts.Url);
        
        // Wait for message processing
        await Task.Delay(3000);
        
        // Assert: All messages should be received after reconnection
        Assert.Equal(messageCount, receivedMessages.Count);
        
        _output.WriteLine($"Messages received after reconnection: {receivedMessages.Count}/{messageCount}");
    }
}
```

### Tasks

- [ ] Add NATS service to ci.yml integration-tests job
- [ ] Add NATS:Url configuration to appsettings.json
- [ ] Add NATS:Url configuration to appsettings.Test.json
- [ ] Update SimpleAccountingEventHandler to use configuration
- [ ] Create CircuitBreakerIntegrationTests.cs
- [ ] Test NATS connectivity in CI
- [ ] Test Circuit Breaker state transitions
- [ ] Test Outbox Worker pause/resume behavior
- [ ] Test NATS reconnection message integrity

### Files to Create

- `6_Tests/VanAn.Integration.Tests/Services/CircuitBreakerIntegrationTests.cs`

### Files to Modify

- `.github/workflows/ci.yml`
- `3_CoreHub/Services/Events/SimpleAccountingEventHandler.cs`
- `2_Gateway/appsettings.json`
- `3_CoreHub/appsettings.json`
- `5_WebApps/ShopERP/appsettings.json`
- `6_Tests/VanAn.Integration.Tests/appsettings.Test.json`

---

## Phase 2.2: Create Roslyn Analyzer Project

### Project Structure

```
VanAn.Accounting.Analyzers/
├── VanAn.Accounting.Analyzers.csproj
├── Analyzers/
│   ├── DomainEntityLocationAnalyzer.cs
│   ├── DependencyDirectionAnalyzer.cs
│   ├── EfCoreInDomainAnalyzer.cs
│   ├── BusinessLogicInGatewayAnalyzer.cs
│   └── AccountingEntryImmutabilityAnalyzer.cs
├── CodeFixes/
│   └── (optional, future)
└── Resources/
    ├── AnalyzerDescriptions.resx
    └── Build.props
```

### 2.2.1 Create Analyzer Project

**File:** `VanAn.Accounting/VanAn.Accounting.Analyzers/VanAn.Accounting.Analyzers.csproj` (NEW)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <DevelopmentDependency>true</DevelopmentDependency>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis" Version="4.9.2" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.9.2" />
  </ItemGroup>

</Project>
```

### 2.2.2 Implement DomainEntityLocationAnalyzer (VA1001)

**File:** `VanAn.Accounting/VanAn.Accounting.Analyzers/Analyzers/DomainEntityLocationAnalyzer.cs` (NEW)

```csharp
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VanAn.Accounting.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DomainEntityLocationAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        "VA1001",
        "Domain entity defined outside Domain layer",
        "Domain entities should only be defined in 1_Shared/Domain.cs",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics 
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.ClassDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.RecordDeclaration);
    }

    private void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var node = context.Node;
        var filePath = node.SyntaxTree.FilePath;
        
        // Allow domain entities in 1_Shared
        if (filePath.Contains("1_Shared")) return;
        
        // Allow test files to define test entities
        if (filePath.Contains("Tests")) return;
        
        // Check if this is a domain entity by naming pattern
        var isDomainEntity = IsDomainEntity(node);
        if (!isDomainEntity) return;
        
        context.ReportDiagnostic(Diagnostic.Create(Rule, node.GetLocation()));
    }

    private bool IsDomainEntity(SyntaxNode node)
    {
        var name = (node as TypeDeclarationSyntax)?.Identifier.Text;
        if (string.IsNullOrEmpty(name)) return false;
        
        // Domain entity naming patterns
        return name.EndsWith("Entry") ||
               name.EndsWith("Balance") ||
               name.EndsWith("Ledger") ||
               name.EndsWith("Package") ||
               name.EndsWith("Invoice") ||
               name.EndsWith("Aggregate") ||
               name.EndsWith("Event");
    }
}
```

### 2.2.3 Implement DependencyDirectionAnalyzer (VA1002)

**File:** `VanAn.Accounting/VanAn.Accounting.Analyzers/Analyzers/DependencyDirectionAnalyzer.cs` (NEW)

```csharp
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VanAn.Accounting.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DependencyDirectionAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        "VA1002",
        "Invalid dependency direction",
        "Service layer should not reference API layer",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics 
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.RegisterCompilationStartAction(compilationContext =>
        {
            compilationContext.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.UsingDirective);
        });
    }

    private void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var usingDirective = (UsingDirectiveSyntax)context.Node;
        var filePath = context.Node.SyntaxTree.FilePath;
        
        // Only check Service layer
        if (!filePath.Contains("3_CoreHub/Services")) return;
        
        var import = usingDirective.Name?.ToString();
        if (string.IsNullOrEmpty(import)) return;
        
        // Check for API layer references
        if (import.Contains("VanAn.Gateway") || import.Contains("VanAn.KhachLink") || import.Contains("VanAn.ShopERP"))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, usingDirective.GetLocation()));
        }
    }
}
```

### 2.2.4 Implement EfCoreInDomainAnalyzer (VA1003)

**File:** `VanAn.Accounting/VanAn.Accounting.Analyzers/Analyzers/EfCoreInDomainAnalyzer.cs` (NEW)

```csharp
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VanAn.Accounting.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class EfCoreInDomainAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        "VA1003",
        "EF Core found in Domain layer",
        "Domain layer should not reference EF Core (purity violation)",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics 
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.RegisterCompilationStartAction(compilationContext =>
        {
            compilationContext.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.UsingDirective);
        });
    }

    private void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var usingDirective = (UsingDirectiveSyntax)context.Node;
        var filePath = context.Node.SyntaxTree.FilePath;
        
        // Only check Domain layer
        if (!filePath.Contains("1_Shared")) return;
        
        var import = usingDirective.Name?.ToString();
        if (string.IsNullOrEmpty(import)) return;
        
        // Check for EF Core references
        if (import.Contains("Microsoft.EntityFrameworkCore") || 
            import.Contains("System.ComponentModel.DataAnnotations"))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, usingDirective.GetLocation()));
        }
    }
}
```

### 2.2.5 Implement BusinessLogicInGatewayAnalyzer (VA1004)

**File:** `VanAn.Accounting/VanAn.Accounting.Analyzers/Analyzers/BusinessLogicInGatewayAnalyzer.cs` (NEW)

```csharp
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VanAn.Accounting.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class BusinessLogicInGatewayAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        "VA1004",
        "Business logic in Gateway layer",
        "Gateway controllers should delegate to services, not implement business logic",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics 
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InvocationExpression);
    }

    private void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var filePath = context.Node.SyntaxTree.FilePath;
        
        // Only check Gateway layer
        if (!filePath.Contains("2_Gateway/Controllers")) return;
        
        var methodName = invocation.Expression?.ToString();
        if (string.IsNullOrEmpty(methodName)) return;
        
        // Check for business logic patterns
        var businessLogicPatterns = new[]
        {
            ".ExecuteAsync",
            ".HandleAsync",
            ".Add(",
            ".Update(",
            ".SaveChangesAsync("
        };
        
        foreach (var pattern in businessLogicPatterns)
        {
            if (methodName.Contains(pattern))
            {
                // Allow if it's a service call
                if (methodName.Contains("_service.") || methodName.Contains("_repository."))
                    continue;
                
                context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
                return;
            }
        }
    }
}
```

### 2.2.6 Implement AccountingEntryImmutabilityAnalyzer (VA1005)

**File:** `VanAn.Accounting/VanAn.Accounting.Analyzers/Analyzers/AccountingEntryImmutabilityAnalyzer.cs` (NEW)

```csharp
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace VanAn.Accounting.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AccountingEntryImmutabilityAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        "VA1005",
        "AccountingEntry mutability violation",
        "AccountingEntry must be immutable - use Reversal Entry pattern",
        "Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics 
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.PropertyDeclaration);
    }

    private void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var property = (PropertyDeclarationSyntax)context.Node;
        var filePath = context.Node.SyntaxTree.FilePath;
        
        // Only check Domain layer
        if (!filePath.Contains("1_Shared")) return;
        
        // Check if this is in AccountingEntry
        var classNode = property.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classNode == null) return;
        
        var className = classNode.Identifier.Text;
        if (className != "AccountingEntry" && className != "GeneralLedgerEntry") return;
        
        // Check if property has setter
        if (property.AccessorList != null)
        {
            var hasSetter = property.AccessorList.Accessors.Any(a => 
                a.IsKind(SyntaxKind.SetAccessorDeclaration) && 
                !a.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)));
            
            if (hasSetter)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, property.GetLocation()));
            }
        }
    }
}
```

### Tasks

- [ ] Create VanAn.Accounting.Analyzers.csproj
- [ ] Implement DomainEntityLocationAnalyzer (VA1001)
- [ ] Implement DependencyDirectionAnalyzer (VA1002)
- [ ] Implement EfCoreInDomainAnalyzer (VA1003)
- [ ] Implement BusinessLogicInGatewayAnalyzer (VA1004)
- [ ] Implement AccountingEntryImmutabilityAnalyzer (VA1005)
- [ ] Build analyzer project
- [ ] Test analyzers on existing codebase
- [ ] Fix violations found by analyzers

### Files to Create

- `VanAn.Accounting/VanAn.Accounting.Analyzers/VanAn.Accounting.Analyzers.csproj`
- `VanAn.Accounting/VanAn.Accounting.Analyzers/Analyzers/DomainEntityLocationAnalyzer.cs`
- `VanAn.Accounting/VanAn.Accounting.Analyzers/Analyzers/DependencyDirectionAnalyzer.cs`
- `VanAn.Accounting/VanAn.Accounting.Analyzers/Analyzers/EfCoreInDomainAnalyzer.cs`
- `VanAn.Accounting/VanAn.Accounting.Analyzers/Analyzers/BusinessLogicInGatewayAnalyzer.cs`
- `VanAn.Accounting/VanAn.Accounting.Analyzers/Analyzers/AccountingEntryImmutabilityAnalyzer.cs`

---

## Phase 2.3: Integrate Analyzers into Build

### 2.3.1 Add Analyzer to Directory.Build.props

**File:** `Directory.Build.props` (MODIFY)

```xml
<Project>
  <PropertyGroup>
    <AnalysisMode>AllEnabledByDefault</AnalysisMode>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <WarningsAsErrors>VA1001;VA1002;VA1003;VA1004;VA1005</WarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <Analyzer Include="VanAn.Accounting/VanAn.Accounting.Analyzers/bin/$(Configuration)/VanAn.Accounting.Analyzers.dll" />
  </ItemGroup>
</Project>
```

### 2.3.2 Build Analyzer First

**File:** `VanAn.Accounting/VanAn.Accounting.csproj` (MODIFY)

```xml
<ItemGroup>
  <ProjectReference Include="VanAn.Accounting.Analyzers\VanAn.Accounting.Analyzers.csproj" 
                    OutputItemType="Analyzer" 
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

### 2.3.3 Update guard-check.ps1

**File:** `guard-check.ps1` (MODIFY)

```powershell
# 2. Run architecture-guard.ps1 (TEMPORARY - will be removed in Phase 3)
Write-Host "Running architecture-guard.ps1..." -ForegroundColor Yellow
.\architecture-guard.ps1
if ($LASTEXITCODE -ne 0) {
    Write-Host "â ARCHITECTURE GUARD FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "â ARCHITECTURE GUARD PASSED" -ForegroundColor Green

# 2.5. Run Roslyn Analyzers (NEW)
Write-Host "Running Roslyn Analyzers..." -ForegroundColor Yellow
$analyzerOutput = dotnet build --no-restore --configuration Release 2>&1 | Tee-Object -FilePath "analyzer.log"

# Check for analyzer violations
$analyzerViolations = $analyzerOutput | Select-String -Pattern "VA1001|VA1002|VA1003|VA1004|VA1005"

if ($analyzerViolations) {
    Write-Host "ROSLYN ANALYZER VIOLATIONS DETECTED:" -ForegroundColor Red
    $analyzerViolations | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    Write-Host "Guard check failed due to analyzer violations" -ForegroundColor Red
    exit 1
}

Write-Host "Roslyn Analyzers: PASSED" -ForegroundColor Green
```

### Tasks

- [ ] Update Directory.Build.props with analyzer reference
- [ ] Add analyzer project reference to VanAn.Accounting.csproj
- [ ] Update guard-check.ps1 to run analyzers
- [ ] Test build with analyzers
- [ ] Fix any analyzer violations

### Files to Modify

- `Directory.Build.props`
- `VanAn.Accounting/VanAn.Accounting.csproj`
- `guard-check.ps1`

---

## Phase 2.4: Add Integration Tests to Fast Gate

### 2.4.1 Update guard-check.ps1

**File:** `guard-check.ps1` (MODIFY)

```powershell
# 5. Fast test gate - Domain + Architecture + Integration tests
Write-Host "Running fast test gate (Domain + Architecture + Integration)..." -ForegroundColor Yellow

dotnet test 6_Tests\VanAn.Core.Tests\VanAn.Core.Tests.csproj --verbosity quiet --configuration Release --filter "Category!=Performance&Category!=Integration&Category!=E2E" 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "FAST TEST GATE FAILED: Core.Tests" -ForegroundColor Red
    Write-Host "Run: dotnet test 6_Tests\VanAn.Core.Tests\VanAn.Core.Tests.csproj for details" -ForegroundColor Yellow
    exit 1
}

dotnet test 6_Tests\VanAn.Architecture.Tests\VanAn.Architecture.Tests.csproj --verbosity quiet --configuration Release 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "FAST TEST GATE FAILED: Architecture.Tests" -ForegroundColor Red
    Write-Host "Run: dotnet test 6_Tests\VanAn.Architecture.Tests\VanAn.Architecture.Tests.csproj for details" -ForegroundColor Yellow
    exit 1
}

# NEW: Integration tests (with real NATS service)
Write-Host "Running Integration test gate (requires NATS service)..." -ForegroundColor Yellow
dotnet test 6_Tests\VanAn.Integration.Tests\VanAn.Integration.Tests.csproj --verbosity quiet --configuration Release 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "INTEGRATION TEST GATE FAILED" -ForegroundColor Red
    Write-Host "Run: dotnet test 6_Tests\VanAn.Integration.Tests\VanAn.Integration.Tests.csproj for details" -ForegroundColor Yellow
    exit 1
}

Write-Host "Fast test gate: PASSED" -ForegroundColor Green
```

### 2.4.2 Update CI to Run Integration Tests

**File:** `.github/workflows/ci.yml` (MODIFY)

```yaml
  # Job 2: Architecture Tests
  architecture-tests:
    runs-on: ubuntu-latest
    needs: build
    timeout-minutes: 10

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Cache NuGet packages
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/Directory.Packages.props', '**/*.csproj') }}
          restore-keys: |
            ${{ runner.os }}-nuget-

      - name: Restore
        run: dotnet restore ${{ env.SOLUTION_PATH }}

      - name: Build
        run: dotnet build ${{ env.SOLUTION_PATH }} --no-restore --configuration Release

      - name: Architecture Tests
        run: dotnet test 6_Tests/VanAn.Architecture.Tests/ --configuration Release --verbosity normal --logger trx
        
      - name: Upload Test Results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results-architecture
          path: '**/TestResults/*.trx'
          retention-days: 30

  # Job 2b: Integration Tests (NEW)
  integration-tests:
    runs-on: ubuntu-latest
    needs: build
    timeout-minutes: 15
    
    services:
      nats:
        image: nats:latest
        ports:
          - 4222:4222
        options: >-
          --name ci-nats
          -js

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Cache NuGet packages
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/Directory.Packages.props', '**/*.csproj') }}
          restore-keys: |
            ${{ runner.os }}-nuget-

      - name: Restore
        run: dotnet restore ${{ env.SOLUTION_PATH }}

      - name: Build
        run: dotnet build ${{ env.SOLUTION_PATH }} --no-restore --configuration Release

      - name: Wait for NATS
        run: |
          timeout 30 bash -c 'until nc -z localhost 4222; do sleep 1; done'
          echo "NATS is ready"

      - name: Integration Tests
        run: dotnet test 6_Tests/VanAn.Integration.Tests/ --configuration Release --verbosity normal --logger trx
        env:
          NATS_URL: nats://localhost:4222
        
      - name: Upload Test Results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results-integration
          path: '**/TestResults/*.trx'
          retention-days: 30
```

### Tasks

- [ ] Update guard-check.ps1 to include Integration tests
- [ ] Add integration-tests job to ci.yml
- [ ] Add NATS service to integration-tests job
- [ ] Add NATS health check
- [ ] Test Integration tests in CI
- [ ] Update guard report to include Integration test results

### Files to Modify

- `guard-check.ps1`
- `.github/workflows/ci.yml`

---

## Phase 2.5: Update Guard Report

### 2.5.1 Update Report Generation

**File:** `guard-check.ps1` (MODIFY)

```powershell
# 7. Generate guard report
Write-Host "Generating guard report..." -ForegroundColor Yellow

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportFile = "guard-report-$timestamp.txt"

@"
Guard Check Report
==================
Date: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Version: v8.0 (Phase 2 Upgrade)

Component Results
------------------
Windsurf Guard: PASSED
Architecture Guard: PASSED
Roslyn Analyzers: PASSED
Build: SUCCEEDED
Core Tests: PASSED
Architecture Tests: PASSED
Integration Tests: PASSED

Warning Classification
----------------------
Critical: $($warningStats.Critical)
Performance: $($warningStats.Performance)
Security: $($warningStats.Security)
Other: $($warningStats.Other)
Total Warnings: $warningCount

Analyzer Violations
-------------------
VA1001 (Domain Entity Location): 0
VA1002 (Dependency Direction): 0
VA1003 (EF Core in Domain): 0
VA1004 (Business Logic in Gateway): 0
VA1005 (AccountingEntry Immutability): 0

Status: ALL CHECKS PASSED
"@ | Out-File $reportFile

Write-Host "Report generated: $reportFile" -ForegroundColor Green
```

### Tasks

- [ ] Update guard report template
- [ ] Add analyzer violation counts
- [ ] Add Integration test status
- [ ] Test report generation

### Files to Modify

- `guard-check.ps1`

---

## Migration Strategy

### Rollout Plan

1. **Week 1: NATS Dependency**
   - Deploy NATS mock to test projects
   - Add NATS service to CI
   - Test Integration tests with NATS

2. **Week 2: Roslyn Analyzers**
   - Create analyzer project
   - Implement 5 analyzer rules
   - Integrate into build
   - Fix violations

3. **Week 3: Integration Tests in Fast Gate**
   - Add Integration tests to guard-check.ps1
   - Update CI workflows
   - Monitor CI stability

### Rollback Plan

- Keep architecture-guard.ps1 as backup
- Feature flag to enable/disable analyzers
- CI parameter to skip Integration tests if needed
- Revert ci.yml if NATS service causes issues

---

## Risk Assessment

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| NATS service unstable in CI | Medium | Medium | Health checks, retries, fallback to mock |
| Analyzer false positives block development | Medium | High | Phased rollout, severity tuning, whitelist |
| Integration tests slow down CI | Low | Medium | Parallel execution, test filtering |
| CI timeout with Integration tests | Low | Medium | Increase timeout, optimize tests |

---

## Success Metrics

### Quantitative Metrics

- **Analyzer coverage:** 5 rules implemented (VA1001-VA1005)
- **Integration test coverage:** Added to fast gate
- **CI execution time:** < 15 minutes (from current ~10 minutes)
- **False positive rate:** < 5% for analyzers

### Qualitative Metrics

- Integration tests pass reliably in CI
- Analyzer rules catch actual architecture violations
- NATS dependency resolved without docker-compose complexity
- Guard report includes all component results

---

## Timeline

| Phase | Duration | Start Date | End Date |
|---|---|---|---|
| Phase 2.1: NATS Dependency | 1 week | Week 1 | Week 1 |
| Phase 2.2: Roslyn Analyzer Project | 1 week | Week 2 | Week 2 |
| Phase 2.3: Integrate Analyzers | 0.5 week | Week 2 | Week 2 |
| Phase 2.4: Integration Tests | 0.5 week | Week 3 | Week 3 |
| Phase 2.5: Update Reports | 0.5 week | Week 3 | Week 3 |

**Total Duration:** 3 weeks

---

## Dependencies

### External Dependencies
- Microsoft.CodeAnalysis 4.9.2
- NATS.Client (already in project)

### Internal Dependencies
- VanAn.Accounting.Analyzers project
- Integration test infrastructure
- CI workflow updates

---

## Resources

### Documentation
- [Roslyn Analyzer Documentation](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/)
- [GitHub Actions Services](https://docs.github.com/en/actions/using-containerized-services/about-service-containers)
- [NATS Documentation](https://docs.nats.io/)

### Code References
- Microsoft.CodeAnalysis.Analyzers
- SonarAnalyzer.CSharp
- StyleCop.Analyzers

---

## Appendix A: Analyzer Rules Mapping

| Analyzer ID | Rule | Severity | Replaces |
|---|---|---|---|
| VA1001 | DomainEntityLocation | Error | architecture-guard.ps1 (Service layer check) |
| VA1002 | DependencyDirection | Error | architecture-guard.ps1 (Dependency check) |
| VA1003 | EfCoreInDomain | Error | architecture-guard.ps1 (EF Core check) |
| VA1004 | BusinessLogicInGateway | Error | windsurf-guard.js (Rule 1) |
| VA1005 | AccountingEntryImmutability | Error | windsurf-guard.js (Rule 3) |

---

## Appendix B: CI Workflow Changes

### Current CI Jobs
```
build (ubuntu)
architecture-tests (ubuntu)
guard-check (windows) ← Phase 1 upgraded
code-quality (ubuntu)
```

### Target CI Jobs (Phase 2)
```
build (ubuntu)
architecture-tests (ubuntu)
integration-tests (ubuntu) ← NEW with NATS service
guard-check (windows) ← Includes analyzers + integration tests
code-quality (ubuntu)
```

---

## Approval

**Reviewed by:**  
**Approved by:**  
**Date:**  
