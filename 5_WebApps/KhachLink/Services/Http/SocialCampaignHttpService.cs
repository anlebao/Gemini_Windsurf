using System.Text.Json;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.KhachLink.Services.Http
{
    /// <summary>
    /// Thin HTTP client wrapper for CoreHub social campaign operations.
    /// KhachLink uses this adapter instead of referencing CoreHub services directly,
    /// keeping the client UI layer free of server-side CoreHub dependencies.
    /// </summary>
    public class SocialCampaignHttpService : ISocialCampaignService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SocialCampaignHttpService> _logger;

        public SocialCampaignHttpService(IHttpClientFactory httpClientFactory, ILogger<SocialCampaignHttpService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("gateway");
            _logger = logger;
        }

        public async Task<SocialCampaign> CreateCampaignAsync(SocialCampaign campaign)
        {
            var response = await _httpClient.PostAsJsonAsync("api/socialcampaigns", campaign);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SocialCampaign>()
                ?? throw new InvalidOperationException("CreateCampaign returned empty response");
        }

        public async Task<SocialCampaign?> GetCampaignByIdAsync(Guid campaignId)
        {
            var response = await _httpClient.GetAsync($"api/socialcampaigns/{campaignId}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SocialCampaign>();
        }

        public async Task<List<SocialCampaign>> GetCampaignsByShopAsync(Guid shopId)
        {
            var response = await _httpClient.GetAsync($"api/socialcampaigns/by-shop/{shopId}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<SocialCampaign>>() ?? [];
        }

        public async Task<string> GenerateTrackingUrlAsync(Guid campaignId)
        {
            var response = await _httpClient.GetAsync($"api/socialcampaigns/{campaignId}/tracking-url");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<bool> RecordClickAsync(string trackingCode)
        {
            var response = await _httpClient.PostAsync($"api/socialcampaigns/record-click/{Uri.EscapeDataString(trackingCode)}", null);
            return response.IsSuccessStatusCode;
        }

        public async Task<SocialCampaign?> GetCampaignByTrackingCodeAsync(string trackingCode)
        {
            var response = await _httpClient.GetAsync($"api/socialcampaigns/by-tracking-code/{Uri.EscapeDataString(trackingCode)}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SocialCampaign>();
        }

        public async Task<bool> IncrementConvertedOrdersAsync(Guid campaignId)
        {
            var response = await _httpClient.PostAsync($"api/socialcampaigns/{campaignId}/increment-conversion", null);
            return response.IsSuccessStatusCode;
        }

        public async Task<SocialCampaign> UpdateCampaignAsync(SocialCampaign campaign)
        {
            var response = await _httpClient.PutAsJsonAsync($"api/socialcampaigns/{campaign.Id}", campaign);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SocialCampaign>()
                ?? throw new InvalidOperationException("UpdateCampaign returned empty response");
        }

        public async Task<bool> DeleteCampaignAsync(Guid campaignId)
        {
            var response = await _httpClient.DeleteAsync($"api/socialcampaigns/{campaignId}");
            return response.IsSuccessStatusCode;
        }

        public async Task<IEnumerable<SocialCampaign>> GetAllCampaignsAsync()
        {
            var response = await _httpClient.GetAsync("api/socialcampaigns");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<SocialCampaign>>() ?? [];
        }
    }
}
