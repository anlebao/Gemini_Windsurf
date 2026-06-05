using Microsoft.Extensions.Logging;
using VanAn.Shared.Models;

namespace VanAn.CoreHub.Services
{
    public interface IOnboardingService
    {
        Task<IEnumerable<OnboardingTemplate>> GetTemplatesAsync();
        Task<OnboardingTemplate?> GetTemplateAsync(Guid id);
        Task<OnboardingTemplate> CreateTemplateAsync(OnboardingTemplate template);
        Task<OnboardingTemplate> ApplyTemplateAsync(Guid templateId, Guid shopId);
        Task<OnboardingTemplate> UpdateTemplateAsync(OnboardingTemplate template);
        Task<bool> DeleteTemplateAsync(Guid id);
    }

    public class OnboardingService(ILogger<OnboardingService> logger) : IOnboardingService
    {
        private readonly ILogger<OnboardingService> _logger = logger;

        public async Task<IEnumerable<OnboardingTemplate>> GetTemplatesAsync()
        {
            // Dummy implementation
            await Task.Delay(10);
            return new List<OnboardingTemplate>
            {
                new() { Id = Guid.NewGuid(), Name = "Template 1", Description = "Default template" },
                new() { Id = Guid.NewGuid(), Name = "Template 2", Description = "Advanced template" }
            };
        }

        public async Task<OnboardingTemplate?> GetTemplateAsync(Guid id)
        {
            await Task.Delay(10);
            return new OnboardingTemplate
            {
                Id = id,
                Name = "Template " + id.ToString()[..8],
                Description = "Sample template"
            };
        }

        public async Task<OnboardingTemplate> ApplyTemplateAsync(Guid templateId, Guid shopId)
        {
            await Task.Delay(10);
            return new OnboardingTemplate
            {
                Id = templateId,
                Name = "Applied Template",
                Description = $"Template applied to shop {shopId}"
            };
        }

        public async Task<OnboardingTemplate> CreateTemplateAsync(OnboardingTemplate template)
        {
            await Task.Delay(10);
            return template;
        }

        public async Task<OnboardingTemplate> UpdateTemplateAsync(OnboardingTemplate template)
        {
            await Task.Delay(10);
            return template;
        }

        public async Task<bool> DeleteTemplateAsync(Guid id)
        {
            await Task.Delay(10);
            return true;
        }
    }
}
