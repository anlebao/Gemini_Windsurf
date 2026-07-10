using Xunit;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace VanAn.Architecture.Tests
{
    /// <summary>
    /// W5: Architecture guard tests ensuring <c>DevLoginController</c> is compiled out of Release builds.
    ///
    /// The controller issues auth cookies + JWT tokens with a fixed test tenant, bypassing OIDC.
    /// If it leaked into a Production/Staging binary, an attacker could POST to <c>/dev/login</c>
    /// and obtain a valid authenticated session without credentials.
    ///
    /// Defense layers (all verified here):
    /// <list type="number">
    /// <item><c>#if DEBUG</c> compile-time guard on the controller class (Controllers/DevLoginController.cs).</item>
    /// <item><c>#if DEBUG</c> compile-time guard on the minimal-API dev route (Program.cs).</item>
    /// <item>Reflection check: type exists in Debug-built ShopERP assembly (proves the guard is syntactically valid).</item>
    /// </list>
    /// </summary>
    public class DevLoginControllerReleaseBuildGuardTests
    {
        private static string GetRepoRoot()
        {
            DirectoryInfo dir = new(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                dir = dir.Parent;
            }
            return dir?.FullName
                ?? throw new DirectoryNotFoundException("Could not find repository root (no .git directory found)");
        }

        [Fact(DisplayName = "W5-ARCH-001: DevLoginController.cs must be wrapped in #if DEBUG guard")]
        public void DevLoginController_Source_Must_Be_Wrapped_In_Debug_Guard()
        {
            // Arrange
            string repoRoot = GetRepoRoot();
            string controllerPath = Path.Combine(repoRoot, "5_WebApps", "ShopERP", "Controllers", "DevLoginController.cs");
            Assert.True(File.Exists(controllerPath), $"DevLoginController.cs not found at {controllerPath}");

            string source = File.ReadAllText(controllerPath);

            // Assert: #if DEBUG must appear BEFORE the class declaration
            int debugGuardIndex = source.IndexOf("#if DEBUG", System.StringComparison.Ordinal);
            Assert.True(debugGuardIndex >= 0,
                "W5-ARCH-001: DevLoginController.cs is missing '#if DEBUG' guard. " +
                "The controller MUST be wrapped in #if DEBUG ... #endif to prevent it from " +
                "being compiled into Release builds (security: bypasses OIDC auth).");

            // Assert: #endif must appear AFTER the class closing brace
            int endIndex = source.LastIndexOf("#endif", System.StringComparison.Ordinal);
            Assert.True(endIndex > debugGuardIndex,
                "W5-ARCH-001: DevLoginController.cs has '#if DEBUG' but is missing the closing '#endif'.");

            // Assert: the class declaration must be BETWEEN #if DEBUG and #endif
            int classIndex = source.IndexOf("class DevLoginController", System.StringComparison.Ordinal);
            Assert.True(classIndex > debugGuardIndex && classIndex < endIndex,
                "W5-ARCH-001: DevLoginController class declaration must be between '#if DEBUG' and '#endif'. " +
                "Current structure: #if DEBUG at {debugGuardIndex}, class at {classIndex}, #endif at {endIndex}.");
        }

        [Fact(DisplayName = "W5-ARCH-002: Program.cs dev login route must be wrapped in #if DEBUG guard")]
        public void ProgramCs_DevLogin_Route_Must_Be_Wrapped_In_Debug_Guard()
        {
            // Arrange
            string repoRoot = GetRepoRoot();
            string programPath = Path.Combine(repoRoot, "5_WebApps", "ShopERP", "Program.cs");
            Assert.True(File.Exists(programPath), $"Program.cs not found at {programPath}");

            string source = File.ReadAllText(programPath);

            // Find the dev login route block
            int devLoginIndex = source.IndexOf("/dev/login", System.StringComparison.Ordinal);
            Assert.True(devLoginIndex >= 0,
                "W5-ARCH-002: Program.cs does not contain '/dev/login' route. " +
                "If the dev login route was intentionally removed, this test can be deleted. " +
                "Otherwise, restore the route with #if DEBUG guard.");

            // Find the #if DEBUG guard that should precede the dev login route
            // (search backwards from the dev login route for the nearest #if DEBUG)
            string beforeDevLogin = source.Substring(0, devLoginIndex);
            int lastDebugGuard = beforeDevLogin.LastIndexOf("#if DEBUG", System.StringComparison.Ordinal);
            int lastEndif = beforeDevLogin.LastIndexOf("#endif", System.StringComparison.Ordinal);

            Assert.True(lastDebugGuard >= 0 && lastDebugGuard > lastEndif,
                "W5-ARCH-002: Program.cs '/dev/login' route is not inside a '#if DEBUG' block. " +
                "The dev login minimal-API route MUST be wrapped in #if DEBUG ... #endif " +
                "to prevent it from being compiled into Release builds.");
        }

        [Fact(DisplayName = "W5-ARCH-003: DevLoginController type must exist in Debug-built ShopERP assembly")]
        public void DevLoginController_Type_Must_Exist_In_Debug_Build()
        {
            // Arrange — load the ShopERP assembly (built in Debug for test runs)
            string repoRoot = GetRepoRoot();
            string shopErpAssemblyPath = Path.Combine(repoRoot,
                "5_WebApps", "ShopERP", "bin", "Debug", "net8.0", "VanAn.ShopERP.dll");

            // Skip if the assembly hasn't been built yet (e.g. fresh clone before first build)
            if (!File.Exists(shopErpAssemblyPath))
            {
                // Try Release path as fallback (test should still pass if Release build exists —
                // but in that case the type should NOT exist, which is the correct behavior)
                shopErpAssemblyPath = Path.Combine(repoRoot,
                    "5_WebApps", "ShopERP", "bin", "Release", "net8.0", "VanAn.ShopERP.dll");
                if (!File.Exists(shopErpAssemblyPath))
                {
                    return; // No build output found — skip (CI will build first)
                }
            }

            // Act — use MetadataReader to read type names directly from the PE file without
            // loading the assembly or its dependencies. This avoids ReflectionTypeLoadException
            // when loading a Debug-built assembly from a Release test run where dependencies
            // may not resolve correctly.
            bool devLoginControllerExists;
            using (var stream = File.OpenRead(shopErpAssemblyPath))
            using (var peReader = new PEReader(stream))
            {
                var metadataReader = peReader.GetMetadataReader();
                devLoginControllerExists = metadataReader.TypeDefinitions
                    .Select(h => metadataReader.GetTypeDefinition(h))
                    .Any(td =>
                        metadataReader.GetString(td.Name) == "DevLoginController" &&
                        metadataReader.GetString(td.Namespace) == "VanAn.ShopERP.Controllers");
            }

            // Assert — in Debug build, the type MUST exist (proves the #if DEBUG guard is syntactically valid
            // and the class compiles correctly). In Release build, the type MUST NOT exist.
            string config = shopErpAssemblyPath.Contains("\\Debug\\") ? "Debug" : "Release";
            if (config == "Debug")
            {
                Assert.True(devLoginControllerExists,
                    "W5-ARCH-003: DevLoginController type not found in Debug-built VanAn.ShopERP.dll. " +
                    "The #if DEBUG guard may be malformed or the class may have a compilation error. " +
                    "Expected: type exists in Debug, absent in Release.");
            }
            else
            {
                Assert.True(!devLoginControllerExists,
                    "W5-ARCH-003: DevLoginController type found in Release-built VanAn.ShopERP.dll! " +
                    "This is a SECURITY VIOLATION — the controller bypasses OIDC auth and must NOT " +
                    "exist in Release builds. Verify the #if DEBUG guard in Controllers/DevLoginController.cs.");
            }
        }
    }
}
