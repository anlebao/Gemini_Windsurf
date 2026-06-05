using Microsoft.AspNetCore.Components;

namespace VanAn.UI.Platform.Components.Base
{
    /// <summary>
    /// Base component class for all VanAn UI Platform components
    /// Phase 2.5.3: ShopERP Dashboard - Real-Time Staff Management
    /// </summary>
    public abstract class VanAnComponentBase : ComponentBase
    {
        /// <summary>
        /// Gets the current CSS class based on component state
        /// </summary>
        protected virtual string GetCssClass()
        {
            return string.Empty;
        }

        /// <summary>
        /// Gets the component ID for accessibility
        /// </summary>
        protected virtual string GetComponentId()
        {
            return $"vanan-{GetType().Name.ToLower(System.Globalization.CultureInfo.CurrentCulture)}-{Guid.NewGuid().ToString("N")[..8]}";
        }

        /// <summary>
        /// Handles parameter changes
        /// </summary>
        protected override void OnParametersSet()
        {
            base.OnParametersSet();
        }

        /// <summary>
        /// Handles component initialization
        /// </summary>
        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
        }

        /// <summary>
        /// Handles after render
        /// </summary>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
        }
    }
}
