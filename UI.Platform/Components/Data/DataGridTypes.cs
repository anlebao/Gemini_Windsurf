namespace VanAn.UI.Platform.Components.Data;

/// <summary>
/// Data grid column definition
/// </summary>
public class DataGridColumn
{
    /// <summary>
    /// Column header text
    /// </summary>
    public string Header { get; set; } = string.Empty;
    
    /// <summary>
    /// Property name to bind
    /// </summary>
    public string? Property { get; set; }
    
    /// <summary>
    /// Column CSS classes
    /// </summary>
    public string? CssClass { get; set; }
    
    /// <summary>
    /// Column width (1-12)
    /// </summary>
    public int? Width { get; set; }
    
    /// <summary>
    /// Is column sortable
    /// </summary>
    public bool Sortable { get; set; }
    
    /// <summary>
    /// Custom template for column
    /// </summary>
    public object? Template { get; set; }
}
