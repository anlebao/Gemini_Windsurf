namespace VanAn.UI.Platform.Tokens
{
    /// <summary>
    /// Minimal spacing token system for UI components
    /// </summary>
    public static class SpacingTokens
    {
        /// <summary>
        /// Extra small spacing
        /// </summary>
        public const string XS = "0.25rem";

        /// <summary>
        /// Small spacing
        /// </summary>
        public const string SM = "0.5rem";

        /// <summary>
        /// Medium spacing
        /// </summary>
        public const string MD = "1rem";

        /// <summary>
        /// Large spacing
        /// </summary>
        public const string LG = "1.5rem";

        /// <summary>
        /// Extra large spacing
        /// </summary>
        public const string XL = "3rem";

        /// <summary>
        /// Get spacing as CSS value
        /// </summary>
        public static string GetSpacing(SpacingSize size)
        {
            return size switch
            {
                SpacingSize.XS => XS,
                SpacingSize.SM => SM,
                SpacingSize.MD => MD,
                SpacingSize.LG => LG,
                SpacingSize.XL => XL,
                _ => MD
            };
        }
    }

    /// <summary>
    /// Spacing size enumeration
    /// </summary>
    public enum SpacingSize
    {
        XS,
        SM,
        MD,
        LG,
        XL
    }
}
