namespace VanAn.CoreHub.Domain
{
    /// <summary>
    /// Facebook Lead Webhook Payload
    /// Represents data received from Facebook Lead Ads webhook
    /// </summary>
    public class FacebookWebhookPayload
    {
        public string LeadId { get; set; } = string.Empty;
        public string AdId { get; set; } = string.Empty;
        public string PageId { get; set; } = string.Empty;
        public string CampaignId { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; }
        public object FormData { get; set; } = new();
    }
}
