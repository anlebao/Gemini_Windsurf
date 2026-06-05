using VanAn.Shared.Domain;

namespace VanAn.Shared.Extensions
{
    public static class ThemeTypeExtensions
    {
        public static string ToCssClass(this ThemeType theme)
        {
            return theme switch
            {
                ThemeType.Classic => "theme-classic",
                ThemeType.Modern => "theme-modern",
                ThemeType.Teen => "theme-teen",
                ThemeType.Lady => "theme-lady",
                ThemeType.Premium => "theme-premium",
                _ => "theme-default"
            };
        }
    }
}
