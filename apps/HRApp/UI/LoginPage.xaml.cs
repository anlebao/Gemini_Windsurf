using Microsoft.Extensions.Logging;
using VanAn.HRApp.Application;

namespace VanAn.HRApp.UI
{
    public partial class LoginPage : ContentPage
    {
        private readonly AuthService _authService;
        private readonly TenantContext _tenantContext;
        private readonly HttpClient _httpClient;
        private readonly ILogger<LoginPage> _logger;

        public LoginPage(AuthService authService, TenantContext tenantContext, HttpClient httpClient, ILogger<LoginPage> logger)
        {
            InitializeComponent();
            _authService = authService;
            _tenantContext = tenantContext;
            _httpClient = httpClient;
            _logger = logger;

            // Check if already logged in
            CheckExistingAuth();
        }

        private async void CheckExistingAuth()
        {
            try
            {
                // Try to get stored token from secure storage
                var token = await SecureStorage.GetAsync("auth_token");
                var tenantId = await SecureStorage.GetAsync("tenant_id");
                var userId = await SecureStorage.GetAsync("user_id");
                var username = await SecureStorage.GetAsync("username");

                if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(tenantId))
                {
                    _tenantContext.TenantId = tenantId;
                    _tenantContext.UserId = userId ?? string.Empty;
                    _tenantContext.Username = username ?? string.Empty;
                    _tenantContext.IsAuthenticated = true;

                    _authService.SetAuthToken(token);
                    _httpClient.SetTenantHeader(_tenantContext);

                    ShowAuthenticatedState();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existing authentication");
            }
        }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            try
            {
                var username = UsernameEntry.Text?.Trim();
                var password = PasswordEntry.Text?.Trim();

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    StatusLabel.Text = "Please enter username and password";
                    return;
                }

                LoginButton.IsEnabled = false;
                LoginButton.Text = "Logging in...";
                StatusLabel.Text = "";

                var result = await _authService.Login(username, password);

                if (result != null && !string.IsNullOrEmpty(result.Token))
                {
                    // Save to secure storage
                    await SecureStorage.SetAsync("auth_token", result.Token);
                    await SecureStorage.SetAsync("tenant_id", result.TenantId);
                    await SecureStorage.SetAsync("user_id", result.UserId);
                    await SecureStorage.SetAsync("username", result.Username);

                    // Update context
                    _tenantContext.TenantId = result.TenantId;
                    _tenantContext.UserId = result.UserId;
                    _tenantContext.Username = result.Username;
                    _tenantContext.IsAuthenticated = true;

                    _authService.SetAuthToken(result.Token);
                    _httpClient.SetTenantHeader(_tenantContext);

                    ShowAuthenticatedState();
                    StatusLabel.Text = "Login successful!";
                    StatusLabel.TextColor = Colors.Green;
                }
                else
                {
                    StatusLabel.Text = "Invalid username or password";
                    StatusLabel.TextColor = Colors.Red;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error");
                StatusLabel.Text = "Login failed. Please try again.";
                StatusLabel.TextColor = Colors.Red;
            }
            finally
            {
                LoginButton.IsEnabled = true;
                LoginButton.Text = "Login";
            }
        }

        private void ShowAuthenticatedState()
        {
            // Hide login form
            UsernameEntry.IsVisible = false;
            PasswordEntry.IsVisible = false;
            LoginButton.IsVisible = false;

            // Show tenant info
            TenantFrame.IsVisible = true;
            ActionButtons.IsVisible = true;

            TenantLabel.Text = $"Tenant: {_tenantContext.TenantId}";
            UserLabel.Text = $"User: {_tenantContext.Username} ({_tenantContext.UserId})";
        }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            try
            {
                // Clear secure storage
                SecureStorage.Remove("auth_token");
                SecureStorage.Remove("tenant_id");
                SecureStorage.Remove("user_id");
                SecureStorage.Remove("username");

                // Clear context
                _tenantContext.TenantId = string.Empty;
                _tenantContext.UserId = string.Empty;
                _tenantContext.Username = string.Empty;
                _tenantContext.IsAuthenticated = false;

                _authService.ClearAuth();
                _httpClient.DefaultRequestHeaders.Remove("X-Tenant-Id");

                // Show login form again
                UsernameEntry.IsVisible = true;
                PasswordEntry.IsVisible = true;
                LoginButton.IsVisible = true;
                TenantFrame.IsVisible = false;
                ActionButtons.IsVisible = false;

                // Clear fields
                UsernameEntry.Text = "";
                PasswordEntry.Text = "";
                StatusLabel.Text = "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout error");
                await DisplayAlert("Error", "Logout failed", "OK");
            }
        }

        private async void OnDashboardClicked(object sender, EventArgs e)
        {
            try
            {
                // Navigate to main dashboard
                await Shell.Current.GoToAsync("//main");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Navigation error");
                await DisplayAlert("Error", "Cannot navigate to dashboard", "OK");
            }
        }
    }
}
