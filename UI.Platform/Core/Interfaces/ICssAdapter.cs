using VanAn.UI.Platform.Tokens;

namespace VanAn.UI.Platform.Core.Interfaces;

/// <summary>
/// CSS adapter interface for framework abstraction
/// </summary>
public interface ICssAdapter
{
    /// <summary>
    /// Get button CSS classes
    /// </summary>
    string GetButtonClass(ButtonVariant variant, ButtonSize size, bool fullWidth = false);
    
    /// <summary>
    /// Get alert CSS classes
    /// </summary>
    string GetAlertClass(AlertVariant variant, bool dismissible = false);
    
    /// <summary>
    /// Get card CSS classes
    /// </summary>
    string GetCardClass(bool hoverable = false, bool shadow = true);
    
    /// <summary>
    /// Get modal CSS classes
    /// </summary>
    string GetModalClass(bool isOpen = false);
    
    /// <summary>
    /// Get form CSS classes
    /// </summary>
    string GetFormClass();
    
    /// <summary>
    /// Get data grid CSS classes
    /// </summary>
    string GetDataGridClass();
    
    /// <summary>
    /// Get spacing CSS class
    /// </summary>
    string GetSpacingClass(SpacingSize size);
    
    /// <summary>
    /// Get text color CSS class
    /// </summary>
    string GetTextColorClass(string colorName);
    
    /// <summary>
    /// Get background color CSS class
    /// </summary>
    string GetBackgroundColorClass(string colorName);
    
    /// <summary>
    /// Get CSS classes with base class and additional classes
    /// </summary>
    string GetClasses(string baseClass);
}
