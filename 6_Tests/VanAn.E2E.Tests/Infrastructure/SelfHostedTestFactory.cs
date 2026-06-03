using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using VanAn.KhachLink;

namespace VanAn.E2E.Tests.Infrastructure
{
    public class SelfHostedTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private Process? _khachLinkProcess;
        private Process? _shopErpProcess;
        private readonly string _testDataDir;
        private readonly string _khachLinkDbPath;
        private readonly string _shopErpDbPath;

        public SelfHostedTestFactory()
        {
            _testDataDir = Path.Combine(Path.GetTempPath(), "VanAnE2E", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDataDir);
            
            _khachLinkDbPath = Path.Combine(_testDataDir, "khachlink.db");
            _shopErpDbPath = Path.Combine(_testDataDir, "shoperp.db");
        }

        public string KhachLinkUrl => "http://localhost:5002";
        public string ShopErpUrl => "http://localhost:5003";
        public string ShopERPUrl => ShopErpUrl;
        public string GatewayUrl => "http://localhost:5000";

        public async Task InitializeAsync()
        {
            // Initialize test databases
            await InitializeTestDatabasesAsync();
            
            // Start KhachLink on port 5002
            _khachLinkProcess = StartWebApp("KhachLink", "5002", _khachLinkDbPath);
            
            // Start ShopERP on port 5003
            _shopErpProcess = StartWebApp("ShopERP", "5003", _shopErpDbPath);
            
            // Wait for applications to start
            await Task.Delay(3000);
        }

        public async Task DisposeAsync()
        {
            _khachLinkProcess?.Kill();
            _shopErpProcess?.Kill();
            
            // Clean up test data
            if (Directory.Exists(_testDataDir))
            {
                Directory.Delete(_testDataDir, true);
            }
            
            await Task.CompletedTask;
        }

        async Task IAsyncLifetime.DisposeAsync()
        {
            await DisposeAsync();
        }

        private async Task InitializeTestDatabasesAsync()
        {
            // Create empty SQLite databases for testing
            await File.WriteAllTextAsync(_khachLinkDbPath, "");
            await File.WriteAllTextAsync(_shopErpDbPath, "");
        }

        private Process StartWebApp(string appName, string port, string dbPath)
        {
            // Use existing built DLLs from solution
            var solutionDir = @"C:\VibeCoding\Gemini_Windsurf";
            var dllPath = Path.Combine(solutionDir, "5_WebApps", appName, "bin", "Debug", "net8.0", $"VanAn.{appName}.dll");
            
            Console.WriteLine($"Starting {appName} from: {dllPath}");
            
            // Verify DLL exists
            if (!File.Exists(dllPath))
            {
                throw new InvalidOperationException($"DLL not found: {dllPath}");
            }

            // Configure environment for test
            var environment = new Dictionary<string, string>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Testing",
                ["ASPNETCORE_URLS"] = $"http://localhost:{port}",
                ["ConnectionStrings__DefaultConnection"] = $"Data Source={dbPath}",
                ["Logging__LogLevel__Default"] = "Warning",
                ["Logging__LogLevel__Microsoft"] = "Warning"
            };

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{dllPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            foreach (var env in environment)
            {
                startInfo.EnvironmentVariables[env.Key] = env.Value;
            }

            var process = Process.Start(startInfo);
            
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start {appName} process");
            }

            // Log startup for debugging
            process.OutputDataReceived += (sender, e) => Console.WriteLine($"[{appName}] {e.Data}");
            process.ErrorDataReceived += (sender, e) => Console.WriteLine($"[{appName} ERROR] {e.Data}");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return process;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Configure test services if needed
            });
        }
    }

    [CollectionDefinition("SelfHosted Tests")]
    public class SelfHostedTestCollection : ICollectionFixture<SelfHostedTestFactory>
    {
        // This class makes the factory available to all tests in the collection
    }
}
