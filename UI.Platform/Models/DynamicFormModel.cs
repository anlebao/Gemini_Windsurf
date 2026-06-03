using System.ComponentModel.DataAnnotations;

namespace VanAn.UI.Platform.Models;

public class DynamicField
{
    public object? Value { get; set; }
    public bool Required { get; set; }
    public string? Label { get; set; }
}

public class DynamicFormModel : IValidatableObject
{
    public Dictionary<string, DynamicField> Fields { get; set; } = new();
    
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var field in Fields)
        {
            if (field.Value.Required && 
                (field.Value.Value == null || string.IsNullOrEmpty(field.Value.Value.ToString())))
            {
                yield return new ValidationResult($"{field.Value.Label ?? field.Key} không được để trống", new[] { field.Key });
            }
        }
    }
}
