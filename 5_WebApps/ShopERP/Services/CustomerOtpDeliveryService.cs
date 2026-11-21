using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Otp;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Customer OTP delivery — email-first (2026-11-21, same policy as merchant signup P4):
    /// email contact → IIdentityOtpChannel (Brevo 0đ, fallback chain); phone contact → ISmsService
    /// (eSMS — returns false gracefully khi chưa cấu hình keys).
    /// Trả về false khi KHÔNG kênh nào gửi được — caller phải báo lỗi thật, không fake "đã gửi".
    /// </summary>
    public interface ICustomerOtpDelivery
    {
        /// <param name="contact">Email hoặc số điện thoại nhận mã.</param>
        /// <param name="isEmail">true = gửi email; false = gửi SMS.</param>
        /// <param name="code">Mã OTP 6 số.</param>
        Task<bool> DeliverAsync(string contact, bool isEmail, string code, CancellationToken ct = default);
    }

    public class CustomerOtpDeliveryService(
        IIdentityOtpChannel otpChannel,
        ISmsService smsService,
        ILogger<CustomerOtpDeliveryService> logger) : ICustomerOtpDelivery
    {
        public async Task<bool> DeliverAsync(string contact, bool isEmail, string code, CancellationToken ct = default)
        {
            if (isEmail)
            {
                var sent = await otpChannel.SendOtpAsync(contact, code, ct);
                if (!sent)
                    logger.LogWarning("CustomerOtpDelivery: email OTP gửi thất bại — {Contact}", MaskContact(contact));
                return sent;
            }

            // SMS: nội dung không dấu (eSMS SMS thường mất ký tự Unicode).
            var message = $"Ma xac minh Van An: {code} (hieu luc 5 phut).";
            var smsSent = await smsService.SendSmsAsync(contact, message, ct);
            if (!smsSent)
                logger.LogWarning("CustomerOtpDelivery: SMS OTP gửi thất bại — {Contact}", MaskContact(contact));
            return smsSent;
        }

        private static string MaskContact(string contact)
        {
            if (string.IsNullOrEmpty(contact)) return "***";
            return contact.Length <= 4 ? "***" : $"***{contact[^4..]}";
        }
    }
}
