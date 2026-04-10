using Microsoft.AspNetCore.Mvc;
using VanAn.Shared.Services;
using VanAn.Shared.Models;
using VanAn.CoreHub.Services;

namespace VanAn.Gateway.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class OnboardingController : ControllerBase
{
    private readonly IOnboardingService _onboardingService;
    private readonly ILogger<OnboardingController> _logger;

    public OnboardingController(IOnboardingService onboardingService, ILogger<OnboardingController> logger)
    {
        _onboardingService = onboardingService;
        _logger = logger;
    }

    [HttpGet("templates")]
    public ActionResult<IEnumerable<object>> GetAvailableTemplates()
    {
        var templates = new[]
        {
            new 
            { 
                Type = "cafe", 
                Name = "Quán Cafe", 
                Description = "Template cho quán cà phê và trà sữa",
                Icon = "☕",
                Products = 4,
                Ingredients = 7,
                WorkflowSteps = 4
            },
            new 
            { 
                Type = "beauty", 
                Name = "Spa & Beauty", 
                Description = "Template cho spa và salon làm đẹp",
                Icon = "💆",
                Products = 4,
                Ingredients = 6,
                WorkflowSteps = 4
            },
            new 
            { 
                Type = "retail", 
                Name = "Cửa hàng", 
                Description = "Template cho cửa hàng thời trang và bán lẻ",
                Icon = "🏪",
                Products = 4,
                Ingredients = 7,
                WorkflowSteps = 5
            }
        };

        return Ok(templates);
    }

    [HttpGet("templates/{templateType}")]
    public async Task<ActionResult<ShopTemplate>> GetTemplate(string templateType)
    {
        try
        {
            if (!Guid.TryParse(templateType, out Guid templateId))
            {
                return BadRequest(new { error = "Invalid template type format" });
            }
            var template = await _onboardingService.GetTemplateAsync(templateId);
            return Ok(template);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting template: {TemplateType}", templateType);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("shops/{shopId}/apply-template")]
    public async Task<ActionResult<bool>> ApplyTemplate(Guid shopId, [FromBody] ApplyTemplateRequest request)
    {
        try
        {
            if (!Guid.TryParse(request.TemplateType, out Guid templateId))
            {
                return BadRequest(new { error = "Invalid template type format" });
            }
            var template = await _onboardingService.GetTemplateAsync(templateId);
            var result = await _onboardingService.ApplyTemplateAsync(templateId, shopId);
            
            if (result != null)
            {
                return Ok(new { success = true, message = $"Template applied successfully" });
            }
            else
            {
                return BadRequest(new { success = false, message = "Failed to apply template" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying template {TemplateType} to shop {ShopId}", request.TemplateType, shopId);
            return StatusCode(500, new { success = false, error = "Internal server error" });
        }
    }

    [HttpPost("shops/{shopId}/quick-setup")]
    public async Task<ActionResult<QuickSetupResponse>> QuickSetup(Guid shopId, [FromBody] QuickSetupRequest request)
    {
        try
        {
            _logger.LogInformation("Starting quick setup for shop: {ShopId} with template: {TemplateType}", shopId, request.TemplateType);

            // Get template
            if (!Guid.TryParse(request.TemplateType, out Guid templateId))
            {
                return BadRequest(new { error = "Invalid template type format" });
            }
            var template = await _onboardingService.GetTemplateAsync(templateId);
            
            // Customize template with user input - simplified for dummy implementation
            var customizedTemplate = template;

            // Apply template
            var applied = await _onboardingService.ApplyTemplateAsync(templateId, shopId);
            
            if (applied != null)
            {
                var response = new QuickSetupResponse
                {
                    Success = true,
                    ShopId = shopId,
                    ShopInfo = new ShopInfo 
                    { 
                        Name = request.ShopName ?? "Vạn An Group",
                        Address = request.ShopAddress ?? "123 Nguyễn Huệ, Q1, TP.HCM",
                        Phone = request.ShopPhone ?? "1900-1234",
                        Email = request.ShopEmail ?? "info@vanan.vn"
                    },
                    ProductsCount = 10,
                    IngredientsCount = 20,
                    WorkflowStepsCount = 5,
                    EstimatedSetupTime = TimeSpan.FromMinutes(5),
                    NextSteps = new[] { "Configure products", "Set up inventory", "Test workflow" }
                };

                return Ok(response);
            }
            else
            {
                return BadRequest(new QuickSetupResponse
                {
                    Success = false,
                    Error = "Failed to apply template"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in quick setup for shop: {ShopId}", shopId);
            return StatusCode(500, new QuickSetupResponse
            {
                Success = false,
                Error = "Internal server error"
            });
        }
    }
}

// Request/Response DTOs
public record ApplyTemplateRequest
{
    public string TemplateType { get; init; } = string.Empty;
    public bool IncludeSampleData { get; init; } = true;
}

public record QuickSetupRequest
{
    public string TemplateType { get; init; } = string.Empty;
    public string ShopName { get; init; } = string.Empty;
    public string ShopAddress { get; init; } = string.Empty;
    public string ShopPhone { get; init; } = string.Empty;
    public string ShopEmail { get; init; } = string.Empty;
}

public record QuickSetupResponse
{
    public bool Success { get; init; }
    public Guid ShopId { get; init; }
    public ShopInfo ShopInfo { get; init; } = new ShopInfo 
    { 
        Name = "Vạn An Group",
        Address = "123 Nguyễn Huệ, Q1, TP.HCM",
        Phone = "1900-1234",
        Email = "info@vanan.vn"
    };
    public int ProductsCount { get; init; }
    public int IngredientsCount { get; init; }
    public int WorkflowStepsCount { get; init; }
    public TimeSpan EstimatedSetupTime { get; init; }
    public string[] NextSteps { get; init; } = Array.Empty<string>();
    public string? Error { get; init; }
}
