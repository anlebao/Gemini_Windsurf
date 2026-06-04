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

    [Fact(DisplayName = "Rule C: Edge Nodes Must Not Reference Npgsql")]
    public void EdgeNodes_ShouldNotReferencePostgreSqlProvider()
    {
        // Arrange
        var repoRoot = GetRepoRoot();
        var edgeNodeProjects = new[]
        {
            Path.Combine(repoRoot, "5_WebApps", "KhachLink", "VanAn.KhachLink.csproj"),
            Path.Combine(repoRoot, "5_WebApps", "ShopERP", "VanAn.ShopERP.csproj")
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
}
