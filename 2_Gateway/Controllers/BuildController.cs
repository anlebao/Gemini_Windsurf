using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Threading.Tasks;

namespace VanAn.Gateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BuildController : ControllerBase
    {
        [HttpGet("status")]
        public async Task<ActionResult<object>> GetBuildStatus()
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
                    return Ok(new { success = false, error = "Failed to start build process" });
                }

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await Task.WhenAll(outputTask, errorTask);
                await process.WaitForExitAsync();

                var output = await outputTask;
                var error = await errorTask;
                var exitCode = process.ExitCode;
                
                return Ok(new { 
                    success = exitCode == 0,
                    exitCode = exitCode,
                    output = output,
                    error = error,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, error = ex.Message });
            }
        }
    }
}
