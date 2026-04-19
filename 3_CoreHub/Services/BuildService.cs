using System.Diagnostics;
using System.Threading.Tasks;

namespace VanAn.CoreHub.Services
{
    public class BuildService : IBuildService
    {
        public async Task<object> GetBuildStatusAsync()
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "build --no-restore --verbosity quiet",
                    WorkingDirectory = @"C:\VibeCoding\Gemini_Windsurf",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process == null)
                {
                    return new { success = false, error = "Failed to start build process" };
                }

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await Task.WhenAll(outputTask, errorTask);
                await process.WaitForExitAsync();

                var output = await outputTask;
                var error = await errorTask;
                var exitCode = process.ExitCode;
                
                return new { 
                    success = exitCode == 0,
                    exitCode = exitCode,
                    output = output,
                    error = error,
                    timestamp = System.DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }
    }
}
