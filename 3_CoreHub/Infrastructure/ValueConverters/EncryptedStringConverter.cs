using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace VanAn.CoreHub.Infrastructure.ValueConverters
{
    /// <summary>
    /// EF Core value converter that encrypts string values before saving to the database
    /// and decrypts them when reading back. Uses ASP.NET Core Data Protection.
    /// </summary>
    public class EncryptedStringConverter : ValueConverter<string, string>
    {
        public EncryptedStringConverter(IDataProtector protector)
            : base(
                v => string.IsNullOrEmpty(v) ? string.Empty : protector.Protect(v),
                v => string.IsNullOrEmpty(v) ? null! : protector.Unprotect(v))
        {
            ArgumentNullException.ThrowIfNull(protector);
        }

    }
}
