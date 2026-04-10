using VanAn.Shared.Models;

namespace VanAn.Shared.Models;

public class OnboardingTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
