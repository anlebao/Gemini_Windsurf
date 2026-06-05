namespace VanAn.UI.Platform.Core.Base
{
    /// <summary>
    /// Component state for managing UI interactions
    /// </summary>
    public class ComponentState
    {
        /// <summary>
        /// Is component disabled
        /// </summary>
        public bool IsDisabled { get; set; }

        /// <summary>
        /// Is component loading
        /// </summary>
        public bool IsLoading { get; set; }

        /// <summary>
        /// Is component visible
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// Has validation errors
        /// </summary>
        public bool HasErrors { get; set; }

        /// <summary>
        /// Is component in interactive state
        /// </summary>
        public bool IsInteractive => !IsDisabled && !IsLoading;

        /// <summary>
        /// Set loading state
        /// </summary>
        public void SetLoading(bool loading = true)
        {
            IsLoading = loading;
        }

        /// <summary>
        /// Set disabled state
        /// </summary>
        public void SetDisabled(bool disabled = true)
        {
            IsDisabled = disabled;
        }

        /// <summary>
        /// Reset state
        /// </summary>
        public void Reset()
        {
            IsDisabled = false;
            IsLoading = false;
            IsVisible = true;
            HasErrors = false;
        }
    }
}
