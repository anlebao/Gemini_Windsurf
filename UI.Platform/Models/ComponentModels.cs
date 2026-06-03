using VanAn.Shared.Domain;

namespace VanAn.UI.Platform.Models;

/// <summary>
/// Component models for UI Platform components
/// Phase 2.5.3: ShopERP Dashboard - Real-Time Staff Management
/// </summary>

/// <summary>
/// Staff assignment model for VanAStaffForm
/// </summary>
public class StaffAssignment
{
    public Guid OrderId { get; set; }
    public Guid StaffId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
    public string? AssignmentNotes { get; set; }
    public string Priority { get; set; } = "normal";
}

/// <summary>
/// Order status update model for VanAStatusForm
/// </summary>
public class OrderStatusUpdate
{
    public Guid OrderId { get; set; }
    public OrderStatusId NewStatus { get; set; } = new OrderStatusId("pending");
    public OrderStatusId PreviousStatus { get; set; } = new OrderStatusId("pending");
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public Guid? UpdatedByStaffId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool NotifyCustomer { get; set; } = true;
}

/// <summary>
/// Staff model for staff selection in forms
/// </summary>
public class Staff
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = true;
    public int CurrentOrders { get; set; } = 0;
    public string? Avatar { get; set; }
    public DateTime? LastActiveAt { get; set; }
}

/// <summary>
/// Metrics data model for VanAMetricsCard
/// </summary>
public class MetricsData
{
    public string Title { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Trend { get; set; } = string.Empty;
    public string Color { get; set; } = "primary";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? Subtitle { get; set; }
    public decimal? NumericValue { get; set; }
}


/// <summary>
/// Alert model for VanAAlert
/// </summary>
public class AlertData
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Message { get; set; } = string.Empty;
    public string Variant { get; set; } = "info";
    public bool Dismissible { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Title { get; set; }
    public TimeSpan? AutoCloseAfter { get; set; }
}

/// <summary>
/// Table column configuration for VanATable
/// </summary>
public class TableColumn<T>
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Width { get; set; }
    public bool Sortable { get; set; } = true;
    public bool Filterable { get; set; } = false;
    public Func<T, object>? GetValue { get; set; }
    public Func<T, string>? GetDisplayValue { get; set; }
    public string? CssClass { get; set; }
}

/// <summary>
/// Form field configuration for VanAForm
/// </summary>
public class FormField
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "text"; // text, email, number, select, textarea, checkbox, radio
    public string? Placeholder { get; set; }
    public bool Required { get; set; }
    public object? Value { get; set; }
    public List<string>? Options { get; set; } // For select/radio
    public string? ValidationPattern { get; set; }
    public string? ValidationMessage { get; set; }
    public string? CssClass { get; set; }
    public bool Disabled { get; set; }
    public bool Readonly { get; set; }
}

/// <summary>
/// Chart data model for VanAChart
/// </summary>
public class ChartData
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = "line"; // line, bar, pie, doughnut, area
    public string Title { get; set; } = string.Empty;
    public List<ChartDataset> Datasets { get; set; } = new();
    public List<string> Labels { get; set; } = new();
    public Dictionary<string, object>? Options { get; set; }
}

/// <summary>
/// Chart dataset model
/// </summary>
public class ChartDataset
{
    public string Label { get; set; } = string.Empty;
    public List<decimal> Data { get; set; } = new();
    public string BackgroundColor { get; set; } = "#3b82f6";
    public string BorderColor { get; set; } = "#3b82f6";
    public int BorderWidth { get; set; } = 2;
    public bool Fill { get; set; } = false;
}

/// <summary>
/// Modal configuration model
/// </summary>
public class ModalConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Size { get; set; } = "medium"; // small, medium, large, fullscreen
    public bool CloseOnBackdrop { get; set; } = true;
    public bool CloseOnEscape { get; set; } = true;
    public bool ShowCloseButton { get; set; } = true;
    public string? CssClass { get; set; }
}

/// <summary>
/// Button configuration model
/// </summary>
public class ButtonConfig
{
    public string Text { get; set; } = string.Empty;
    public string Variant { get; set; } = "primary"; // primary, secondary, success, warning, error, info, outline, ghost
    public string Size { get; set; } = "medium"; // small, medium, large
    public bool Disabled { get; set; }
    public bool Loading { get; set; }
    public string? Icon { get; set; }
    public string? CssClass { get; set; }
    public string? Type { get; set; } = "button"; // button, submit, reset
}
