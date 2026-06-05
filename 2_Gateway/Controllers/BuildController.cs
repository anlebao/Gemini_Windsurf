using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;

namespace VanAn.Gateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BuildController(IBuildService buildService) : ControllerBase
    {
        private readonly IBuildService _buildService = buildService;

        [HttpGet("status")]
        public async Task<ActionResult<object>> GetBuildStatus()
        {
            try
            {
                object result = await _buildService.GetBuildStatusAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, error = ex.Message });
            }
        }
    }
}
