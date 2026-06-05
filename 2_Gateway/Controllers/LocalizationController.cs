using Microsoft.AspNetCore.Mvc;
using VanAn.Shared.Services;

namespace VanAn.Gateway.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class LocalizationController : ControllerBase
    {
        private readonly ILocalizationService _localizationService;
        private readonly ILogger<LocalizationController> _logger;

        public LocalizationController(
            ILocalizationService localizationService,
            ILogger<LocalizationController> logger)
        {
            ArgumentNullException.ThrowIfNull(localizationService);
            ArgumentNullException.ThrowIfNull(logger);
            _localizationService = localizationService;
            _logger = logger;
        }

        [HttpGet("strings")]
        public async Task<ActionResult<Dictionary<string, string>>> GetStrings()
        {
            try
            {
                string culture = HttpContext.Items["Culture"]?.ToString() ?? _localizationService.GetCulture();
                Dictionary<string, object> strings = await _localizationService.GetAllStringsAsync(culture).ConfigureAwait(false);

                return Ok(strings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting localization strings");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("strings/{key}")]
        public async Task<ActionResult<string>> GetString(string key)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(key);
                string culture = HttpContext.Items["Culture"]?.ToString() ?? _localizationService.GetCulture();
                string value = await _localizationService.GetStringAsync(key, culture).ConfigureAwait(false);

                return Ok(value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting localization string for key: {Key}", key);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("cultures")]
        public ActionResult<IEnumerable<string>> GetSupportedCultures()
        {
            try
            {
                IEnumerable<string> cultures = _localizationService.GetSupportedCultures();
                return Ok(cultures);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting supported cultures");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost("culture")]
        public async Task<ActionResult<bool>> SetCulture([FromBody] SetCultureRequest request)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(request);
                await _localizationService.SetCultureAsync(request.Culture).ConfigureAwait(false);
                return Ok(new { Success = true, request.Culture });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting culture: {Culture}", request.Culture);
                return StatusCode(500, false);
            }
        }
    }

    public record SetCultureRequest
    {
        public string Culture { get; init; } = string.Empty;
    }
}
