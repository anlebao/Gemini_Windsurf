using Xunit;
using System.IO;
using System.Reflection;
using System.Linq;

namespace VanAn.Architecture.Tests
{
    public class ArchitectureTests
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

        [Fact(DisplayName = "VA-DDD-002: Domain Layer Should Not Have Dependency On Infrastructure Or Application")]
        public void Domain_Layer_Should_Not_Have_Dependency_On_Infrastructure_Or_Application()
        {
            // Arrange
            var repoRoot = GetRepoRoot();
            var domainPath = Path.Combine(repoRoot, "3_CoreHub", "Domain");
            
            if (!Directory.Exists(domainPath))
            {
                return; // Skip if directory doesn't exist
            }

            // Get all C# files in Domain layer
            var domainFiles = Directory.GetFiles(domainPath, "*.cs", SearchOption.AllDirectories);
            
            // Act & Assert - Check for forbidden references
            var forbiddenNamespaces = new[]
            {
                "VanAn.CoreHub.Infrastructure",
                "VanAn.KhachLink",
                "VanAn.ShopERP",
                "VanAn.Gateway",
                "Microsoft.EntityFrameworkCore" // EF Core should not be in Domain
            };

            foreach (var file in domainFiles)
            {
                var content = File.ReadAllText(file);
                
                foreach (var forbiddenNamespace in forbiddenNamespaces)
                {
                    // Check for using statements
                    if (content.Contains($"using {forbiddenNamespace}") || 
                        content.Contains($"using static {forbiddenNamespace}"))
                    {
                        Assert.Fail($"VA-DDD-002: Tầng Domain layer vi phạm chiều phụ thuộc kiến trúc - Domain không được phép tham chiếu tới Infrastructure hoặc Application layers! File: {Path.GetFileName(file)} references {forbiddenNamespace}");
                    }
                }
            }
        }

        [Fact(DisplayName = "VA-ARCH-001: Application Layer Should Not Contain Migration Classes")]
        public void Application_Layer_Should_Not_Contain_Migration_Classes()
        {
            // Arrange
            var repoRoot = GetRepoRoot();
            var applicationPaths = new[]
            {
                Path.Combine(repoRoot, "5_WebApps", "KhachLink"),
                Path.Combine(repoRoot, "5_WebApps", "ShopERP"),
                Path.Combine(repoRoot, "2_Gateway")
            };

            // Act & Assert - Check for Migration classes or Migrations folder
            foreach (var appPath in applicationPaths)
            {
                if (!Directory.Exists(appPath))
                {
                    continue;
                }

                // Check for Migrations folder
                var migrationsFolder = Path.Combine(appPath, "Migrations");
                if (Directory.Exists(migrationsFolder))
                {
                    var migrationFiles = Directory.GetFiles(migrationsFolder, "*.cs");
                    if (migrationFiles.Length > 0)
                    {
                        Assert.Fail($"VA-ARCH-001: Phát hiện Migration file phá vỡ Layer Boundary hoặc sai lệch chiến lược EnsureCreatedAsync của hệ thống! Migrations folder found in: {appPath}");
                    }
                }

                // Check for Migration class inheritance in C# files
                var csFiles = Directory.GetFiles(appPath, "*.cs", SearchOption.AllDirectories);
                foreach (var file in csFiles)
                {
                    var content = File.ReadAllText(file);
                    
                    // Check for Migration inheritance
                    if (content.Contains(": Migration") || 
                        content.Contains(": Microsoft.EntityFrameworkCore.Migrations.Migration"))
                    {
                        Assert.Fail($"VA-ARCH-001: Phát hiện Migration file phá vỡ Layer Boundary hoặc sai lệch chiến lược EnsureCreatedAsync của hệ thống! Migration class found in: {file}");
                    }
                }
            }
        }

        [Fact(DisplayName = "VA-GATEWAY-003: Gateway Should Not Contain DbContext Or Business Logic")]
        public void Gateway_Should_Not_Contain_DbContext_Or_Business_Logic()
        {
            // Arrange
            var repoRoot = GetRepoRoot();
            var gatewayPath = Path.Combine(repoRoot, "2_Gateway");
            
            if (!Directory.Exists(gatewayPath))
            {
                return; // Skip if directory doesn't exist
            }

            // Get all C# files in Gateway project
            var gatewayFiles = Directory.GetFiles(gatewayPath, "*.cs", SearchOption.AllDirectories);
            
            // Act & Assert - Check for forbidden patterns
            var forbiddenPatterns = new[]
            {
                "DbContext",
                "IVanAnDbContext",
                "ProductService",
                "Microsoft.EntityFrameworkCore"
            };

            foreach (var file in gatewayFiles)
            {
                var content = File.ReadAllText(file);
                
                foreach (var pattern in forbiddenPatterns)
                {
                    if (content.Contains(pattern))
                    {
                        Assert.Fail($"VA-GATEWAY-003: Gateway must remain pure proxy - found {pattern} in {Path.GetFileName(file)}");
                    }
                }
            }
        }

        [Fact(DisplayName = "VA-KHACHLINK-004: Client UI Should Not Directly Access Database")]
        public void Client_UI_Should_Not_Directly_Access_Database()
        {
            // Arrange
            var repoRoot = GetRepoRoot();
            var khachLinkPath = Path.Combine(repoRoot, "5_WebApps", "KhachLink");
            
            if (!Directory.Exists(khachLinkPath))
            {
                return; // Skip if directory doesn't exist
            }

            // Get all .razor and .cs files in KhachLink project
            var razorFiles = Directory.GetFiles(khachLinkPath, "*.razor", SearchOption.AllDirectories);
            var csFiles = Directory.GetFiles(khachLinkPath, "*.cs", SearchOption.AllDirectories);
            var allFiles = razorFiles.Concat(csFiles);
            
            // Act & Assert - Check for forbidden database access patterns
            var forbiddenPatterns = new[]
            {
                "IVanAnDbContext",
                "DbContext",
                "UseSqlite",
                "UseNpgsql",
                "AddDbContext"
            };

            foreach (var file in allFiles)
            {
                var content = File.ReadAllText(file);
                
                foreach (var pattern in forbiddenPatterns)
                {
                    if (content.Contains(pattern))
                    {
                        Assert.Fail($"VA-KHACHLINK-004: Client UI cannot directly access database - found {pattern} in {Path.GetFileName(file)}");
                    }
                }
            }
        }
    }
}
