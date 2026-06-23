using Microsoft.AspNetCore.DataProtection;

namespace VanAn.CoreHub.Infrastructure.DataProtection
{
    /// <summary>
    /// Provides access to the configured IDataProtectionProvider for EF Core value converters
    /// and other infrastructure that cannot receive it via constructor injection.
    /// Initialized once during application startup.
    /// </summary>
    public static class DataProtectionProviderAccessor
    {
        private static IDataProtectionProvider? _provider;
        private static readonly object Lock = new();

        public static void Initialize(IDataProtectionProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);

            lock (Lock)
            {
                _provider = provider;
            }
        }

        public static IDataProtector CreateProtector(string purpose)
        {
            lock (Lock)
            {
                // Lazy fallback: use an ephemeral provider for tests or contexts where
                // Initialize was not called. Production hosts must explicitly call Initialize
                // with a persistent key provider to avoid data loss on restart.
                _provider ??= new EphemeralDataProtectionProvider();

                return _provider.CreateProtector(purpose);
            }
        }

        internal static bool IsInitialized
        {
            get
            {
                lock (Lock)
                {
                    return _provider is not null;
                }
            }
        }
    }
}
