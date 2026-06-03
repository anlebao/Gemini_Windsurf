using VanAn.UI.Platform.Core.Interfaces;
using VanAn.UI.Platform.Tokens;

namespace VanAn.UI.Platform.Adapters
{
    /// <summary>
    /// Bootstrap CSS adapter implementation
    /// </summary>
    public class BootstrapAdapter : ICssAdapter
    {
        public string GetButtonClass(ButtonVariant variant, ButtonSize size, bool fullWidth = false)
        {
            List<string> classes =
            [
                "btn",
                // Variant classes
                variant switch
                {
                    ButtonVariant.Primary => "btn-primary",
                    ButtonVariant.Secondary => "btn-secondary",
                    ButtonVariant.Success => "btn-success",
                    ButtonVariant.Error => "btn-danger",
                    ButtonVariant.Warning => "btn-warning",
                    ButtonVariant.Info => "btn-info",
                    ButtonVariant.Outline => "btn-outline-primary",
                    ButtonVariant.Ghost => "btn-link",
                    _ => "btn-primary"
                },

                // Size classes
                size switch
                {
                    ButtonSize.Small => "btn-sm",
                    ButtonSize.Large => "btn-lg", ButtonSize.Medium => throw new NotImplementedException(), _ => ""
                },
                // Full width
                .. fullWidth ? ["w-100"] : [],
            ];

            return string.Join(" ", classes.Where(c => !string.IsNullOrEmpty(c)));
        }

        public string GetAlertClass(AlertVariant variant, bool dismissible = false)
        {
            List<string> classes =
            [
                "alert",
                variant switch
                {
                    AlertVariant.Success => "alert-success",
                    AlertVariant.Warning => "alert-warning",
                    AlertVariant.Error => "alert-danger",
                    AlertVariant.Info => "alert-info",
                    _ => "alert-info"
                },
                .. dismissible ? ["alert-dismissible"] : [],
            ];

            return string.Join(" ", classes);
        }

        public string GetCardClass(bool hoverable = false, bool shadow = true)
        {
            List<string> classes = ["card", .. shadow ? ["shadow-sm"] : [], .. hoverable ? ["card-hover"] : []];

            return string.Join(" ", classes);
        }

        public string GetModalClass(bool isOpen = false)
        {
            List<string> classes = ["modal", .. isOpen ? ["show"] : []];

            return string.Join(" ", classes);
        }

        public string GetFormClass()
        {
            return "form";
        }

        public string GetDataGridClass()
        {
            return "table table-striped table-hover";
        }

        public string GetSpacingClass(SpacingSize size)
        {
            return size switch
            {
                SpacingSize.XS => "p-1",
                SpacingSize.SM => "p-2",
                SpacingSize.MD => "p-3",
                SpacingSize.LG => "p-4",
                SpacingSize.XL => "p-5",
                _ => "p-3"
            };
        }

        public string GetTextColorClass(string colorName)
        {
            return $"text-{colorName.ToLower(System.Globalization.CultureInfo.CurrentCulture)}";
        }

        public string GetBackgroundColorClass(string colorName)
        {
            return $"bg-{colorName.ToLower(System.Globalization.CultureInfo.CurrentCulture)}";
        }

        public string GetClasses(string baseClass)
        {
            return baseClass;
        }
    }
}
