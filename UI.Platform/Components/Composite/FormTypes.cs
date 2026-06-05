namespace VanAn.UI.Platform.Components.Composite
{
    /// <summary>
    /// Form field definition
    /// </summary>
    public class FormField
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public FieldType Type { get; set; } = FieldType.Text;
        public string? Value { get; set; }
        public string? Placeholder { get; set; }
        public string? HelpText { get; set; }
        public List<FieldOption>? Options { get; set; }
        public bool Required { get; set; }
    }

    /// <summary>
    /// Field type enumeration
    /// </summary>
    public enum FieldType
    {
        Text,
        Select,
        Checkbox,
        TextArea,
        Number,
        Email,
        Password,
        Date,
        Currency
    }

    /// <summary>
    /// Field option for select fields
    /// </summary>
    public class FieldOption
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>
    /// Form data container
    /// </summary>
    public class FormData
    {
        public Dictionary<string, string> Values { get; set; } = [];
    }
}
