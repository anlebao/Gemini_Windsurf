using System.Collections.ObjectModel;

namespace VanAn.Shared.Models
{
    public record ShopInfo
    {
        public required string Name { get; init; } = string.Empty;
        public required string Address { get; init; } = string.Empty;
        public required string Phone { get; init; } = string.Empty;
        public required string Email { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Logo { get; init; } = string.Empty;
        public string Theme { get; init; } = "default";
    }

    public record ShopTemplate
    {
        public required string TemplateType { get; init; } = string.Empty;
        public required string TemplateName { get; init; } = string.Empty;
        public required string Description { get; init; } = string.Empty;
        public required ShopInfo ShopInfo { get; init; }
        public required Collection<ProductTemplate> Products { get; init; } = [];
        public required Collection<IngredientTemplate> Ingredients { get; init; } = [];
        public required Collection<WorkflowStepTemplate> WorkflowSteps { get; init; } = [];
    }

    public record ProductTemplate
    {
        public required string Name { get; init; } = string.Empty;
        public required string Description { get; init; } = string.Empty;
        public required decimal Price { get; init; }
        public required string Category { get; init; } = string.Empty;
        public required Collection<string> Ingredients { get; init; } = [];
    }

    public record IngredientTemplate
    {
        public required string Name { get; init; } = string.Empty;
        public required string Unit { get; init; } = string.Empty;
        public required decimal PricePerUnit { get; init; }
        public required decimal MinStockThreshold { get; init; }
    }

    public record WorkflowStepTemplate
    {
        public required string StepName { get; init; } = string.Empty;
        public required string Description { get; init; } = string.Empty;
        public required int EstimatedMinutes { get; init; }
        public required Collection<string> RequiredRoles { get; init; } = [];
    }
}
