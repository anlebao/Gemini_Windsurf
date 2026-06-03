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
            List<string> classes = new()
            {
                "btn",
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
                size switch
                {
                    ButtonSize.Small => "btn-sm",
                    ButtonSize.Large => "btn-lg",
                    ButtonSize.Medium => throw new NotImplementedException(),
                    _ => ""
                }
            };
            if (fullWidth)
            {
                classes.Add("w-100");
            }

            return string.Join(" ", classes.Where(c => !string.IsNullOrEmpty(c)));
        }

        public string GetAlertClass(AlertVariant variant, bool dismissible = false)
        {
            List<string> classes = new()
            {
                "alert",
                variant switch
                {
                    AlertVariant.Success => "alert-success",
                    AlertVariant.Warning => "alert-warning",
                    AlertVariant.Error => "alert-danger",
                    AlertVariant.Info => "alert-info",
                    _ => "alert-info"
                }
            };
            if (dismissible)
            {
                classes.Add("alert-dismissible");
            }

            return string.Join(" ", classes);
        }

        public string GetCardClass(bool hoverable = false, bool shadow = true)
        {
            List<string> classes = new() { "card" };
            if (shadow)
            {
                classes.Add("shadow-sm");
            }

            if (hoverable)
            {
                classes.Add("card-hover");
            }

            return string.Join(" ", classes);
        }

        public string GetModalClass(bool isOpen = false)
        {
            List<string> classes = new() { "modal" };
            if (isOpen)
            {
                classes.Add("show");
            }

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