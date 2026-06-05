namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Notification Service Interface
    /// Handles sending notifications to customers
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Sends email to customer
        /// </summary>
        /// <param name="email">Email address</param>
        /// <param name="subject">Email subject</param>
        /// <param name="message">Email message</param>
        /// <returns>Success status</returns>
        Task<bool> SendEmailAsync(string email, string subject, string message);

        /// <summary>
        /// Sends SMS to customer
        /// </summary>
        /// <param name="phoneNumber">Phone number</param>
        /// <param name="message">SMS message</param>
        /// <returns>Success status</returns>
        Task<bool> SendSMSAsync(string phoneNumber, string message);

        /// <summary>
        /// Sends push notification to customer
        /// </summary>
        /// <param name="customerId">Customer ID</param>
        /// <param name="title">Notification title</param>
        /// <param name="message">Notification message</param>
        /// <returns>Success status</returns>
        Task<bool> SendPushNotificationAsync(Guid customerId, string title, string message);
    }
}
