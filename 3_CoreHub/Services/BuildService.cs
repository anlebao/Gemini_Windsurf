using System.Diagnostics;

namespace VanAn.CoreHub.Services
{
    public class BuildService : IBuildService
    {
        public async Task<object> GetBuildStatusAsync()
        {
            try
            {
                ProcessStartInfo processInfo = new()
                {
                    FileName = "dotnet",
                    Arguments = "build --no-restore --verbosity quiet",
                    WorkingDirectory = @"C:\VibeCoding\Gemini_Windsurf",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using Process? process = Process.Start(processInfo);
                if (process == null)
                {
                    return new { success = false, error = "Failed to start build process" };
                }

                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();

                _ = await Task.WhenAll(outputTask, errorTask);
                await process.WaitForExitAsync();

                string output = await outputTask;
                string error = await errorTask;
                int exitCode = process.ExitCode;

                return new
                {
                    success = exitCode == 0,
                    exitCode,
                    output,
                    error,
                    timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }
    }
}
