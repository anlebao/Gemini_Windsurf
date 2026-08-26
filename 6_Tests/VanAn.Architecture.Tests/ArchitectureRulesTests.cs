using Xunit;
using System.IO;
using System.Reflection;

namespace VanAn.Architecture.Tests;

public class ArchitectureRulesTests
{
    private static string GetRepoRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(currentDir);
        
        // Navigate up to find repo root (contains .git directory)
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }
        
        if (dir == null)
        {
            throw new DirectoryNotFoundException("Could not find repository root (no .git directory found)");
        }
        
        return dir.FullName;
    }
    [Fact(DisplayName = "Rule A: No MapFallbackToFile in Program.cs")]
    public void ProgramCs_ShouldNotUseMapFallbackToFile()
    {
        // Arrange - Check source files directly since assemblies might not exist
        var repoRoot = GetRepoRoot();
        var programFiles = new[]
        {
            Path.Combine(repoRoot, "2_Gateway", "Program.cs"),
            Path.Combine(repoRoot, "5_WebApps", "KhachLink", "Program.cs"),
            Path.Combine(repoRoot, "5_WebApps", "ShopERP", "Program.cs")
        };

        // Act & Assert
        foreach (var programFile in programFiles)
        {
            var fullPath = programFile;
            if (File.Exists(fullPath))
            {
                var content = File.ReadAllText(fullPath);
                Assert.DoesNotContain("MapFallbackToFile", content);
            }
            else
            {
                Assert.Fail($"Program file not found: {fullPath}");
            }
        }
    }

    [Fact(DisplayName = "Rule B: No InMemory Database except in Tests")]
    public void Projects_ShouldNotReferenceInMemoryDatabase_ExceptTests()
    {
        // Arrange - Check project files directly (exclude CoreHub as it's allowed to use InMemory for testing)
        var repoRoot = GetRepoRoot();
        var projectFiles = new[]
        {
            Path.Combine(repoRoot, "1_Shared", "VanAn.Shared.csproj"),
            Path.Combine(repoRoot, "2_Gateway", "VanAn.Gateway.csproj"),
            Path.Combine(repoRoot, "5_WebApps", "KhachLink", "VanAn.KhachLink.csproj"),
            Path.Combine(repoRoot, "5_WebApps", "ShopERP", "VanAn.ShopERP.csproj")
        };

        // Act & Assert
        foreach (var projectFile in projectFiles)
        {
            var fullPath = projectFile;
            if (File.Exists(fullPath))
            {
                var content = File.ReadAllText(fullPath);
                Assert.DoesNotContain("Microsoft.EntityFrameworkCore.InMemory", content);
            }
            else
            {
                Assert.Fail($"Project file not found: {fullPath}");
            }
        }
    }

    [Fact(DisplayName = "Rule C: Client Nodes (KhachLink) Must Not Reference Npgsql — ShopERP exempt (ADR-001 accounting online)")]
    public void EdgeNodes_ShouldNotReferencePostgreSqlProvider()
    {
        // Arrange
        // WAVE 3 (2026-07-10): ShopERP removed from this check — it now legitimately references
        // Npgsql.EntityFrameworkCore.PostgreSQL for IAccountingDbContext (ADR-001: accounting always online).
        // Only KhachLink (Blazor WASM client, HTTP-only) must remain free of direct DB provider references.
        var repoRoot = GetRepoRoot();
        var edgeNodeProjects = new[]
        {
            Path.Combine(repoRoot, "5_WebApps", "KhachLink", "VanAn.KhachLink.csproj"),
        };

        // Act & Assert
        foreach (var projectFile in edgeNodeProjects)
        {
            var fullPath = projectFile;
            if (File.Exists(fullPath))
            {
                var content = File.ReadAllText(fullPath);
                Assert.DoesNotContain("Npgsql", content);
                Assert.DoesNotContain("PostgreSQL", content);
            }
            else
            {
                Assert.Fail($"Project file not found: {fullPath}");
            }
        }
    }

    [Fact(DisplayName = "Rule D: Core Entities Must Inherit IMustHaveTenant")]
    public void CoreEntities_ShouldInheritIMustHaveTenant()
    {
        // Arrange - Check domain file directly
        var repoRoot = GetRepoRoot();
        var domainFile = Path.Combine(repoRoot, "1_Shared", "Domain.cs");
        
        if (File.Exists(domainFile))
        {
            var content = File.ReadAllText(domainFile);
            
            // Look for core entities that should have tenant support
            var coreEntityPatterns = new[]
            {
                "public class Order",
                "public class Customer", 
                "public class Product",
                "public class Invoice"
            };

            // Act & Assert
            foreach (var pattern in coreEntityPatterns)
            {
                if (content.Contains(pattern))
                {
                    // Check if the entity implements IMustHaveTenant or inherits from BaseEntity
                    var startIndex = content.IndexOf(pattern);
                    if (startIndex >= 0)
                    {
                        // Get the class definition (roughly)
                        var endIndex = content.IndexOf("\n}", startIndex);
                        if (endIndex > startIndex)
                        {
                            var classDefinition = content.Substring(startIndex, endIndex - startIndex + 2);
                            
                            var hasTenantInterface = classDefinition.Contains("IMustHaveTenant");
                            var hasBaseEntity = classDefinition.Contains(": BaseEntity") || classDefinition.Contains("BaseEntity");
                            
                            Assert.True(hasTenantInterface || hasBaseEntity, 
                                $"Entity defined by {pattern} should implement IMustHaveTenant or inherit from BaseEntity");
                        }
                    }
                }
            }
        }
        else
        {
            Assert.Fail($"Domain file not found: {domainFile}");
        }
    }

    [Fact(DisplayName = "Rule E: All Projects Must Target .NET 8.0")]
    public void Projects_ShouldTargetNet8()
    {
        // Arrange
        var repoRoot = GetRepoRoot();
        var projectFiles = new Dictionary<string, string>
        {
            { Path.Combine(repoRoot, "1_Shared", "VanAn.Shared.csproj"), "net8.0" },
            { Path.Combine(repoRoot, "2_Gateway", "VanAn.Gateway.csproj"), "net8.0" },
            { Path.Combine(repoRoot, "3_CoreHub", "VanAn.CoreHub.csproj"), "net8.0" },
            { Path.Combine(repoRoot, "5_WebApps", "KhachLink", "VanAn.KhachLink.csproj"), "net8.0" },
            { Path.Combine(repoRoot, "5_WebApps", "ShopERP", "VanAn.ShopERP.csproj"), "net8.0" }
        };

        // Act & Assert
        foreach (var kvp in projectFiles)
        {
            var fullPath = kvp.Key;
            if (File.Exists(fullPath))
            {
                var content = File.ReadAllText(fullPath);
                Assert.Contains(kvp.Value, content);
            }
            else
            {
                Assert.Fail($"Project file not found: {fullPath}");
            }
        }
    }

    [Fact(DisplayName = "Rule F: CartItem must have ProductId property (FK to Product catalog)")]
    public void CartItem_MustHave_ProductId()
    {
        var repoRoot = GetRepoRoot();
        var cartItemFile = Path.Combine(repoRoot, "1_Shared", "Domain", "CartItem.cs");

        if (File.Exists(cartItemFile))
        {
            var content = File.ReadAllText(cartItemFile);
            Assert.Contains("ProductId", content);
        }
        else
        {
            Assert.Fail($"CartItem domain file not found: {cartItemFile}");
        }
    }

    [Fact(DisplayName = "Rule G: CartItem must NOT have redundant Name or Price properties (removed in refactor)")]
    public void CartItem_MustNotHave_RedundantNameOrPrice()
    {
        var repoRoot = GetRepoRoot();
        var cartItemFile = Path.Combine(repoRoot, "1_Shared", "Domain", "CartItem.cs");

        if (File.Exists(cartItemFile))
        {
            var content = File.ReadAllText(cartItemFile);
            Assert.DoesNotContain("required string Name", content);
            Assert.DoesNotContain("required decimal Price", content);
        }
        else
        {
            Assert.Fail($"CartItem domain file not found: {cartItemFile}");
        }
    }

    [Fact(DisplayName = "Rule H: ADR-001 v1 SaaS - docker-compose.prod.yml CoreHub MUST use PostgreSQL")]
    public void DockerComposeProd_CoreHub_MustUse_PostgreSQL()
    {
        // Arrange
        var repoRoot = GetRepoRoot();
        var dockerComposeFile = Path.Combine(repoRoot, "docker-compose.prod.yml");

        // Act & Assert
        if (!File.Exists(dockerComposeFile))
            Assert.Fail($"docker-compose.prod.yml not found: {dockerComposeFile}");

        var content = File.ReadAllText(dockerComposeFile);

        // v1 SaaS: CoreHub MUST connect to PostgreSQL (not SQLite)
        // Accounting data is always online — PostgreSQL is the source of truth
        var hasPostgresForCoreHub = content.Contains("Host=postgres") ||
                                    content.Contains("postgres:5432");

        Assert.True(hasPostgresForCoreHub,
            "ADR-001 v1 violation: docker-compose.prod.yml CoreHub must use PostgreSQL for cloud accounting. " +
            "SQLite is only for v2 Edge deployment (docker-compose.edge.yml).");
    }

    [Fact(DisplayName = "Rule I: ADR-001 v2 Edge - docker-compose.edge.yml MUST include SQLite volume + NATS sync worker")]
    public void DockerComposeEdge_MustInclude_SQLite_And_NatsSyncWorker()
    {
        // Arrange
        var repoRoot = GetRepoRoot();
        var edgeComposeFile = Path.Combine(repoRoot, "docker-compose.edge.yml");

        // Act & Assert
        if (!File.Exists(edgeComposeFile))
            Assert.Fail($"docker-compose.edge.yml not found at: {edgeComposeFile}. " +
                        "Create it as part of ADR001-W2 (Wave 1 of unified roadmap).");

        var content = File.ReadAllText(edgeComposeFile);

        // ADR-001 v2 Edge: Must have named SQLite volume for ShopERP persistence
        var hasSQLiteVolume = content.Contains("shoperp_sqlite_data");

        // ADR-001 v2 Edge: Must have NATS sync worker service
        var hasNatsSyncWorker = content.Contains("shoperp-nats-sync") ||
                                content.Contains("nats-sync");

        // ADR-001 v2 Edge: Must still have NATS broker for event transport
        var hasNatsBroker = content.Contains("image: nats:") ||
                            content.Contains("nats:2.10");

        Assert.True(hasSQLiteVolume,
            "ADR-001 v2 Edge violation: docker-compose.edge.yml must declare 'shoperp_sqlite_data' volume " +
            "for persisted SQLite DB on edge station.");
        Assert.True(hasNatsSyncWorker,
            "ADR-001 v2 Edge violation: docker-compose.edge.yml must include 'shoperp-nats-sync' worker service " +
            "to publish Outbox events to NATS.");
        Assert.True(hasNatsBroker,
            "ADR-001 v2 Edge violation: docker-compose.edge.yml must include NATS broker (nats:2.10-alpine) " +
            "for event-driven sync between stations.");
    }

    [Fact(DisplayName = "Rule J: ADR-001 - Accounting services/repos MUST inject IAccountingDbContext (PostgreSQL)")]
    public void AccountingServices_MustInject_IAccountingDbContext()
    {
        var repoRoot = GetRepoRoot();
        var servicesPath = Path.Combine(repoRoot, "3_CoreHub", "Services");
        var reposPath = Path.Combine(repoRoot, "3_CoreHub", "Repositories");

        // Services + repos that MUST inject IAccountingDbContext
        // NOTE: Excludes services that inject repositories (AccountingEntryService, ReversalService,
        // AuditTrailService, HKDBookService) — they don't inject DbContext directly.
        var accountingFiles = new[]
        {
            // Repositories (direct DbContext injection)
            Path.Combine(reposPath, "AccountingEntryRepository.cs"),
            Path.Combine(repoRoot, "3_CoreHub", "Infrastructure", "Repositories", "AuditLogRepository.cs"),
            Path.Combine(reposPath, "HKDBookRepository.cs"),
            // Services (direct DbContext injection — accounting-only consumers)
            Path.Combine(servicesPath, "PeriodClosingService.cs"),
            Path.Combine(servicesPath, "BalanceSheetService.cs"),
            Path.Combine(servicesPath, "IncomeStatementService.cs"),
            Path.Combine(servicesPath, "CashFlowStatementService.cs"),
            Path.Combine(servicesPath, "TrialBalanceService.cs"),
            Path.Combine(servicesPath, "AccountChartService.cs"),
            Path.Combine(servicesPath, "Data", "DataProviderService.cs"),
            // Dual-inject services (must have IAccountingDbContext alongside IVanAnDbContext)
            Path.Combine(servicesPath, "PreAggregation", "SmartPreAggregationService.cs"),
            Path.Combine(servicesPath, "TenantConversionService.cs"),
            Path.Combine(servicesPath, "Template", "HKDBookGenerationService.cs"),
        };

        var violations = new List<string>();
        foreach (var filePath in accountingFiles)
        {
            if (!File.Exists(filePath))
            {
                violations.Add($"{filePath}: file not found");
                continue;
            }
            var content = File.ReadAllText(filePath);
            if (!content.Contains("IAccountingDbContext"))
                violations.Add($"{Path.GetFileName(filePath)}: missing IAccountingDbContext injection");
        }

        Assert.True(violations.Count == 0,
            "ADR-001 violation: Accounting services/repos must inject IAccountingDbContext (PostgreSQL, online).\n" +
            string.Join("\n", violations));
    }

    [Fact(DisplayName = "Rule K: ADR-001 - ShopERPDbContext (SQLite) MUST NOT contain accounting DbSets")]
    public void ShopERPDbContext_MustNotContain_AccountingDbSets()
    {
        var repoRoot = GetRepoRoot();
        var dbContextPath = Path.Combine(repoRoot, "5_WebApps", "ShopERP", "Infrastructure", "ShopERPDbContext.cs");

        if (!File.Exists(dbContextPath))
            Assert.Fail($"ShopERPDbContext.cs not found: {dbContextPath}");

        var content = File.ReadAllText(dbContextPath);

        var forbiddenDbSets = new[]
        {
            "DbSet<AccountingEntry>",
            "DbSet<JournalEntry>",
            "DbSet<AuditLog>",
            "DbSet<PendingInvoiceQueue>",
            "DbSet<AccountChartEntity>",
            "DbSet<PeriodClosingStatusEntity>",
        };

        var violations = new List<string>();
        foreach (var dbSet in forbiddenDbSets)
        {
            if (content.Contains(dbSet))
                violations.Add($"Found accounting DbSet in SQLite context: {dbSet}");
        }

        Assert.True(violations.Count == 0,
            "ADR-001 violation: ShopERPDbContext (SQLite) must not contain accounting DbSets.\n" +
            string.Join("\n", violations));
    }

    [Fact(DisplayName = "Rule L: ADR-001 - docker-compose ShopERP MUST have AccountingConnection (PostgreSQL)")]
    public void DockerCompose_ShopERP_MustHave_AccountingConnection()
    {
        var repoRoot = GetRepoRoot();
        var composeFiles = new[]
        {
            Path.Combine(repoRoot, "docker-compose.yml"),
            Path.Combine(repoRoot, "docker-compose.prod.yml"),
            Path.Combine(repoRoot, "docker-compose.edge.yml"),
        };

        foreach (var composeFile in composeFiles)
        {
            if (!File.Exists(composeFile)) continue;
            var content = File.ReadAllText(composeFile);

            Assert.True(content.Contains("AccountingConnection"),
                $"ADR-001 violation: {Path.GetFileName(composeFile)} must have AccountingConnection env var. " +
                "Accounting is always online on PostgreSQL.");

            Assert.True(content.Contains("Host=postgres") || content.Contains("postgres:5432"),
                $"ADR-001 violation: {Path.GetFileName(composeFile)} must reference PostgreSQL host for accounting.");
        }
    }

    [Fact(DisplayName = "Rule M: ADR-001 - ShopERP Program.cs MUST register IAccountingDbContext with UseNpgsql")]
    public void ShopERP_ProgramCs_MustRegister_IAccountingDbContext_Npgsql()
    {
        var repoRoot = GetRepoRoot();
        var programCsPath = Path.Combine(repoRoot, "5_WebApps", "ShopERP", "Program.cs");

        if (!File.Exists(programCsPath))
            Assert.Fail($"Program.cs not found: {programCsPath}");

        var content = File.ReadAllText(programCsPath);

        Assert.True(content.Contains("IAccountingDbContext"),
            "ADR-001 violation: ShopERP Program.cs must register IAccountingDbContext.");

        Assert.True(content.Contains("UseNpgsql"),
            "ADR-001 violation: ShopERP Program.cs must call UseNpgsql for accounting DbContext.");
    }

    // ── Phase 5: Crawler Worker Layer Boundary ───────────────────────────────

    [Fact(DisplayName = "Rule L: Crawler project must target .NET 8.0 and have NO ProjectReference to Domain/CoreHub/Gateway/ShopERP")]
    public void CrawlerProject_MustBeStandalone_NoProjectReferences()
    {
        var repoRoot = GetRepoRoot();
        var crawlerProj = Path.Combine(repoRoot, "7_Tooling", "VanAn.Crawler", "VanAn.Crawler.csproj");

        if (!File.Exists(crawlerProj))
            Assert.Fail($"Crawler project not found: {crawlerProj}");

        var content = File.ReadAllText(crawlerProj);

        // Must target net8.0
        Assert.Contains("net8.0", content);

        // Must NOT reference any internal projects (standalone — HTTP to Gateway only)
        Assert.DoesNotContain("VanAn.Shared", content);
        Assert.DoesNotContain("VanAn.Gateway", content);
        Assert.DoesNotContain("VanAn.CoreHub", content);
        Assert.DoesNotContain("VanAn.ShopERP", content);
        Assert.DoesNotContain("VanAn.KhachLink", content);
    }

    [Fact(DisplayName = "Rule M: Crawler project must NOT reference EF Core or DbContext (layer boundary)")]
    public void CrawlerProject_MustNotReference_EntityFrameworkCore()
    {
        var repoRoot = GetRepoRoot();
        var crawlerProj = Path.Combine(repoRoot, "7_Tooling", "VanAn.Crawler", "VanAn.Crawler.csproj");

        if (!File.Exists(crawlerProj))
            Assert.Fail($"Crawler project not found: {crawlerProj}");

        var content = File.ReadAllText(crawlerProj);

        Assert.DoesNotContain("EntityFrameworkCore", content);
        Assert.DoesNotContain("IVanAnDbContext", content);
    }
}
