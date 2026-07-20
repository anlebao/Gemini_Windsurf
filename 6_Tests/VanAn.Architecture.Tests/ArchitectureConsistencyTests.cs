using Xunit;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace VanAn.Architecture.Tests
{
    /// <summary>
    /// Architecture Consistency Tests - Validate code vs deployment configuration consistency.
    ///
    /// PURPOSE: Detect architecture mismatches between code implementation and deployment config.
    ///
    /// CRITICAL TEST: CoreHub Background Service vs HTTP Service Detection
    ///   - CoreHub Program.cs uses Host.CreateDefaultBuilder (background service pattern)
    ///   - docker-compose.prod.yml MUST NOT configure CoreHub as HTTP service
    ///   - This test prevents the architecture mismatch that reached production
    ///
    /// WHY THESE TESTS ARE BLOCKING:
    ///   - Architecture violations not detected by code-only validation
    ///   - Deployment failures occur despite CI/CD success
    ///   - Resource waste and production instability
    /// </summary>
    public class ArchitectureConsistencyTests
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

        /// <summary>
        /// VALIDATE: CoreHub Program.cs uses Host.CreateDefaultBuilder (background service).
        /// If this test fails, CoreHub is being configured as HTTP service in code.
        /// </summary>
        [Fact(DisplayName = "VA-CONSISTENCY-001: CoreHub uses Host.CreateDefaultBuilder (background service)")]
        public void CoreHub_Uses_Host_CreateDefaultBuilder_Background_Service()
        {
            // Arrange
            var repoRoot = GetRepoRoot();
            var coreHubProgramPath = Path.Combine(repoRoot, "3_CoreHub", "Program.cs");

            if (!File.Exists(coreHubProgramPath))
            {
                return; // Skip if file doesn't exist
            }

            var content = File.ReadAllText(coreHubProgramPath);

            // Act & Assert - Check for background service pattern
            var hasHostCreateDefaultBuilder = content.Contains("Host.CreateDefaultBuilder");
            var hasConfigureWebHostDefaults = content.Contains("ConfigureWebHostDefaults");
            var hasUseKestrel = content.Contains("UseKestrel");
            var hasBuildWebApplication = content.Contains("WebApplication.CreateBuilder");

            // CoreHub MUST use Host.CreateDefaultBuilder (background service)
            Assert.True(hasHostCreateDefaultBuilder,
                "VA-CONSISTENCY-001: CoreHub Program.cs must use Host.CreateDefaultBuilder for background service pattern");

            // CoreHub MUST NOT use web host patterns
            Assert.False(hasConfigureWebHostDefaults,
                "VA-CONSISTENCY-001: CoreHub Program.cs must NOT use ConfigureWebHostDefaults (HTTP service pattern)");
            Assert.False(hasUseKestrel,
                "VA-CONSISTENCY-001: CoreHub Program.cs must NOT use UseKestrel (HTTP service pattern)");
            Assert.False(hasBuildWebApplication,
                "VA-CONSISTENCY-001: CoreHub Program.cs must NOT use WebApplication.CreateBuilder (HTTP service pattern)");
        }

        /// <summary>
        /// VALIDATE: docker-compose.prod.yml does NOT configure CoreHub as HTTP service.
        /// If this test fails, CoreHub is being deployed as HTTP service despite being background service in code.
        /// </summary>
        [Fact(DisplayName = "VA-CONSISTENCY-002: docker-compose.prod.yml does NOT configure CoreHub as HTTP service")]
        public void Docker_Compose_Prod_Does_Not_Configure_CoreHub_As_HTTP_Service()
        {
            // Arrange
            var repoRoot = GetRepoRoot();
            var dockerComposePath = Path.Combine(repoRoot, "docker-compose.prod.yml");

            if (!File.Exists(dockerComposePath))
            {
                return; // Skip if file doesn't exist
            }

            var content = File.ReadAllText(dockerComposePath);

            // Act & Assert - Check for HTTP service configuration in CoreHub
            var coreHubSectionRegex = new Regex(@"corehub:.*?(?=\n\s{0,2}\w+:|\n\s{0,2}volumes:|\n\s{0,2}networks:|$)", 
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var coreHubMatch = coreHubSectionRegex.Match(content);

            if (!coreHubMatch.Success)
            {
                return; // CoreHub section not found, skip test
            }

            var coreHubSection = coreHubMatch.Value;

            // CoreHub MUST NOT have ASPNETCORE_URLS configured
            var hasAspNetCoreUrls = coreHubSection.Contains("ASPNETCORE_URLS");

            // CoreHub MUST NOT have HTTP port exposure (e.g., "80:80")
            var hasHttpPortExposure = Regex.IsMatch(coreHubSection, @"ports:\s*-\s*[""']?\d+:\d+[""']?");

            // CoreHub MUST NOT have healthcheck (background services don't have HTTP endpoints)
            var hasHealthcheck = coreHubSection.Contains("healthcheck:");

            // Assert - CoreHub should NOT be configured as HTTP service
            if (hasAspNetCoreUrls || hasHttpPortExposure || hasHealthcheck)
            {
                var failures = new System.Collections.Generic.List<string>();
                if (hasAspNetCoreUrls) failures.Add("ASPNETCORE_URLS configured");
                if (hasHttpPortExposure) failures.Add("HTTP port exposed");
                if (hasHealthcheck) failures.Add("healthcheck configured");

                Assert.Fail($"VA-CONSISTENCY-002: CoreHub must NOT be configured as HTTP service in docker-compose.prod.yml. Found: {string.Join(", ", failures)}");
            }
        }

        /// <summary>
        /// VALIDATE: Gateway dependencies in docker-compose match code architecture.
        /// Gateway depends on CoreHub in deployment, but this should be validated.
        /// </summary>
        [Fact(DisplayName = "VA-CONSISTENCY-003: Gateway docker-compose dependencies match architecture")]
        public void Gateway_Docker_Compose_Dependencies_Match_Architecture()
        {
            // Arrange
            var repoRoot = GetRepoRoot();
            var dockerComposePath = Path.Combine(repoRoot, "docker-compose.prod.yml");

            if (!File.Exists(dockerComposePath))
            {
                return; // Skip if file doesn't exist
            }

            var content = File.ReadAllText(dockerComposePath);

            // Act & Assert - Gateway should have proper dependencies
            var gatewaySectionRegex = new Regex(@"gateway:.*?(?=\n\s{0,2}\w+:|\n\s{0,2}volumes:|\n\s{0,2}networks:|$)",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var gatewayMatch = gatewaySectionRegex.Match(content);

            if (!gatewayMatch.Success)
            {
                return; // Gateway section not found, skip test
            }

            var gatewaySection = gatewayMatch.Value;

            // Gateway MUST have depends_on section
            Assert.True(gatewaySection.Contains("depends_on:"),
                "VA-CONSISTENCY-003: Gateway must have depends_on section in docker-compose.prod.yml");

            // Phase 2 Complete: CoreHub is now in-process (monolith). Gateway depends on postgres + nats directly.
            // Gateway MUST NOT depend on corehub (corehub is no longer a standalone Docker service).
            Assert.False(gatewaySection.Contains("corehub"),
                "VA-CONSISTENCY-003: Gateway must NOT depend on corehub — corehub is in-process (Phase 2 monolith)");
            Assert.True(gatewaySection.Contains("postgres"),
                "VA-CONSISTENCY-003: Gateway depends_on must include postgres in docker-compose.prod.yml");
            Assert.True(gatewaySection.Contains("nats"),
                "VA-CONSISTENCY-003: Gateway depends_on must include nats in docker-compose.prod.yml");
        }

        /// <summary>
        /// VALIDATE: docker-compose.prod.yml has consistent environment variable naming.
        /// </summary>
        [Fact(DisplayName = "VA-CONSISTENCY-004: docker-compose.prod.yml has consistent environment variable naming")]
        public void Docker_Compose_Prod_Has_Consistent_Environment_Variable_Naming()
        {
            // Arrange
            var repoRoot = GetRepoRoot();
            var dockerComposePath = Path.Combine(repoRoot, "docker-compose.prod.yml");

            if (!File.Exists(dockerComposePath))
            {
                return; // Skip if file doesn't exist
            }

            var content = File.ReadAllText(dockerComposePath);

            // Act & Assert - Check for consistent environment variable patterns
            // All services should use double underscore for nested config (e.g., ConnectionStrings__DefaultConnection)
            var singleUnderscorePattern = new Regex(@"-\s+([A-Z_][A-Z0-9_]*)=", RegexOptions.Multiline);
            var matches = singleUnderscorePattern.Matches(content);

            var invalidVars = new System.Collections.Generic.List<string>();
            foreach (Match match in matches)
            {
                var varName = match.Groups[1].Value;
                // Exclude known valid single-underscore patterns
                // Also exclude double underscore patterns (valid for nested config)
                if (!varName.StartsWith("ASPNETCORE_") &&
                    !varName.StartsWith("POSTGRES_") &&
                    !varName.StartsWith("NATS_") &&
                    !varName.StartsWith("SEQ_") &&
                    !varName.StartsWith("JWT_") &&
                    !varName.StartsWith("SHOPERP_") &&
                    !varName.StartsWith("SHOP_INSTANCE_ID") && // Phase 4: fail-fast env var for OrderSyncSubscriber routing
                    !varName.StartsWith("IMAGE_") &&
                    !varName.StartsWith("VANAN_") &&
                    !varName.Equals("ACCEPT_EULA") &&
                    !varName.Contains("__")) // Allow double underscore for nested config
                {
                    invalidVars.Add(varName);
                }
            }

            Assert.True(invalidVars.Count == 0,
                $"VA-CONSISTENCY-004: docker-compose.prod.yml has inconsistent environment variable naming. Found single-underscore vars: {string.Join(", ", invalidVars)}");
        }

        /// <summary>
        /// VALIDATE: All application services in docker-compose.prod.yml have proper logging configuration.
        /// </summary>
        [Fact(DisplayName = "VA-CONSISTENCY-005: All application services have proper logging configuration")]
        public void All_Services_Have_Proper_Logging_Configuration()
        {
            // Arrange
            var repoRoot = GetRepoRoot();
            var dockerComposePath = Path.Combine(repoRoot, "docker-compose.prod.yml");

            if (!File.Exists(dockerComposePath))
            {
                return; // Skip if file doesn't exist
            }

            var content = File.ReadAllText(dockerComposePath);

            // Act & Assert - Check for logging configuration in main application services
            // NOTE: corehub is NOT a standalone Docker service (Phase 2 — in-process monolith inside Gateway).
            // Only validate services that actually exist in docker-compose.prod.yml.
            var appServices = new[] { "gateway", "shoperp", "khachlink" };
            var servicesWithoutLogging = new System.Collections.Generic.List<string>();

            foreach (var serviceName in appServices)
            {
                // Extract service section using regex
                var serviceSectionRegex = new Regex($@"{serviceName}:.*?(?=\n\s{{0,2}}\w+:|\n\s{{0,2}}volumes:|\n\s{{0,2}}networks:|$)",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase);
                var serviceMatch = serviceSectionRegex.Match(content);

                if (!serviceMatch.Success)
                {
                    servicesWithoutLogging.Add($"{serviceName} (section not found)");
                    continue;
                }

                var serviceContent = serviceMatch.Value;
                if (!serviceContent.Contains("logging:"))
                {
                    servicesWithoutLogging.Add(serviceName);
                }
            }

            Assert.True(servicesWithoutLogging.Count == 0,
                $"VA-CONSISTENCY-005: Application services missing logging configuration: {string.Join(", ", servicesWithoutLogging)}");
        }
    }
}