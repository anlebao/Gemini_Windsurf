using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.SystemSettingAggregate;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S6-T5: Collaborator SMS OTP verification + deposit wallet service implementation.
/// OTP stored in IMemoryCache (5-min expiry). SMS sent via ISmsService. Fee via IWalletService.
/// Settings stored in SystemSetting (runtime toggle, no restart needed).
/// </summary>
public class CollaboratorVerificationService(
    IVanAnDbContext dbContext,
    ISmsService smsService,
    IWalletService walletService,
    IMemoryCache memoryCache,
    ILogger<CollaboratorVerificationService> logger) : ICollaboratorVerificationService
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly ISmsService _smsService = smsService;
    private readonly IWalletService _walletService = walletService;
    private readonly IMemoryCache _cache = memoryCache;
    private readonly ILogger<CollaboratorVerificationService> _logger = logger;

    // SystemSetting keys
    private const string KeyEnabled = "CollaboratorSmsVerificationEnabled";
    private const string KeyFeePerVerification = "SmsOtpFeePerVerification";
    private const string KeyMinDeposit = "CollaboratorMinDeposit";

    // Defaults (used when SystemSetting row doesn't exist yet)
    private const bool DefaultEnabled = false;
    private const decimal DefaultFeePerVerification = 200m; // 200 VND per SMS
    private const decimal DefaultMinDeposit = 10000m; // 10,000 VND minimum deposit

    // OTP cache config
    private const string OtpCacheKeyPrefix = "Otp_";
    private const string OtpRetryCountKeyPrefix = "OtpRetry_";
    private static readonly TimeSpan OtpExpiry = TimeSpan.FromMinutes(5);
    private const int MaxOtpPerDay = 3;
    private static readonly TimeSpan RetryWindow = TimeSpan.FromHours(24);

    public async Task<CollaboratorVerificationSettingsDto> GetSettingsAsync()
    {
        var enabled = await GetSettingBoolAsync(KeyEnabled, DefaultEnabled);
        var fee = await GetSettingDecimalAsync(KeyFeePerVerification, DefaultFeePerVerification);
        var minDeposit = await GetSettingDecimalAsync(KeyMinDeposit, DefaultMinDeposit);

        return new CollaboratorVerificationSettingsDto
        {
            Enabled = enabled,
            FeePerVerification = fee,
            MinDeposit = minDeposit
        };
    }

    public async Task SetSettingsAsync(bool enabled, decimal feePerVerification, decimal minDeposit, Guid updatedBy)
    {
        if (feePerVerification < 0)
            throw new ArgumentException("FeePerVerification cannot be negative", nameof(feePerVerification));
        if (minDeposit < 0)
            throw new ArgumentException("MinDeposit cannot be negative", nameof(minDeposit));

        var tenantId = new TenantId(Guid.Empty); // global setting
        await UpsertSettingAsync(KeyEnabled, enabled.ToString().ToLowerInvariant(), tenantId, updatedBy);
        await UpsertSettingAsync(KeyFeePerVerification, feePerVerification.ToString(), tenantId, updatedBy);
        await UpsertSettingAsync(KeyMinDeposit, minDeposit.ToString(), tenantId, updatedBy);

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Collaborator SMS verification settings updated: Enabled={Enabled} Fee={Fee} MinDeposit={MinDeposit} by {UpdatedBy}",
            enabled, feePerVerification, minDeposit, updatedBy);
    }

    public async Task<InitVerificationResultDto> InitVerificationAsync(Guid customerId, string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("Phone number cannot be empty", nameof(phoneNumber));

        var settings = await GetSettingsAsync();
        if (!settings.Enabled)
            throw new InvalidOperationException("Xác minh SMS không được bật. Liên hệ quản trị viên.");

        // AC-02b.6: Retry limit — max 3 OTP sends per 24h (anti-spam)
        var retryKey = OtpRetryCountKeyPrefix + customerId;
        if (_cache.TryGetValue(retryKey, out int retryCount) && retryCount >= MaxOtpPerDay)
            throw new InvalidOperationException($"Đã gửi tối đa {MaxOtpPerDay} mã OTP trong 24 giờ. Vui lòng thử lại sau.");

        // Check deposit balance
        var balance = await _walletService.GetBalanceAsync(customerId);
        if (balance < settings.FeePerVerification)
            throw new InvalidOperationException(
                $"Số dư ví không đủ để gửi SMS OTP. Số dư hiện tại: {balance:N0} VND, phí SMS: {settings.FeePerVerification:N0} VND. Vui lòng nạp tiền.");

        // Generate 6-digit OTP
        var otp = Random.Shared.Next(100000, 999999).ToString();

        // Send SMS
        var message = $"Ma xac minh Vạn An cua ban la: {otp}. Ma co hieu luc 5 phut.";
        var sent = await _smsService.SendSmsAsync(phoneNumber, message);
        if (!sent)
        {
            _logger.LogError("Failed to send SMS OTP to {PhoneNumber} for customer {CustomerId}", phoneNumber, customerId);
            throw new InvalidOperationException("Không thể gửi SMS. Vui lòng thử lại sau.");
        }

        // Deduct fee from wallet
        await _walletService.CreateTransactionAsync(
            customerId,
            WalletTransactionType.SmsOtpFee,
            -settings.FeePerVerification,
            $"SMS OTP verification fee for {phoneNumber}",
            null,
            null);

        // Store OTP in cache (5-min expiry)
        _cache.Set(OtpCacheKeyPrefix + customerId, otp, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = OtpExpiry
        });

        // Increment retry counter (24h window)
        _cache.Set(retryKey, retryCount + 1, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = RetryWindow
        });

        var balanceAfter = balance - settings.FeePerVerification;
        _logger.LogInformation("SMS OTP sent to {PhoneNumber} for customer {CustomerId}. Fee deducted: {Fee}. Balance after: {Balance}",
            phoneNumber, customerId, settings.FeePerVerification, balanceAfter);

        return new InitVerificationResultDto
        {
            Message = "Ma OTP da duoc gui den so dien thoai cua ban. Ma co hieu luc 5 phut.",
            FeeDeducted = settings.FeePerVerification,
            BalanceAfter = balanceAfter
        };
    }

    public async Task VerifyOtpAsync(Guid customerId, string otpCode)
    {
        if (string.IsNullOrWhiteSpace(otpCode))
            throw new ArgumentException("OTP code cannot be empty", nameof(otpCode));

        // Check OTP from cache
        if (!_cache.TryGetValue(OtpCacheKeyPrefix + customerId, out string? cachedOtp) || cachedOtp == null)
            throw new InvalidOperationException("Ma OTP khong ton tai hoac da het han. Vui long yeu cau ma moi.");

        if (cachedOtp != otpCode)
            throw new InvalidOperationException("Ma OTP khong chinh xac.");

        // Find active CommunityRole for this customer
        var role = await _dbContext.CommunityRoles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.CustomerId == customerId && r.IsActive);

        if (role == null)
            throw new InvalidOperationException("Khong tim thay vai tro collaborator cho khach hang nay.");

        // Mark phone verified
        role.MarkPhoneVerified();
        await _dbContext.SaveChangesAsync();

        // Remove OTP from cache (one-time use)
        _cache.Remove(OtpCacheKeyPrefix + customerId);

        _logger.LogInformation("Phone verified for customer {CustomerId} via SMS OTP", customerId);
    }

    public async Task DepositAsync(Guid customerId, decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Deposit amount must be positive", nameof(amount));

        await _walletService.CreateTransactionAsync(
            customerId,
            WalletTransactionType.Deposit,
            amount,
            $"Collaborator deposit by customer {customerId}");

        _logger.LogInformation("Deposit {Amount} for customer {CustomerId}", amount, customerId);
    }

    public async Task<bool> IsVerificationRequiredAsync(Guid customerId)
    {
        var settings = await GetSettingsAsync();
        if (!settings.Enabled)
            return false;

        // Check if customer has an active collaborator role that's not yet phone-verified
        var role = await _dbContext.CommunityRoles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.CustomerId == customerId && r.IsActive);

        if (role == null)
            return false; // not a collaborator → no verification needed

        return !role.IsPhoneVerified;
    }

    // === Private helpers (SystemSetting CRUD — mirrors CommerceModeService pattern) ===

    private async Task<bool> GetSettingBoolAsync(string key, bool defaultValue)
    {
        var raw = await GetSettingRawAsync(key);
        if (raw == null || !bool.TryParse(raw, out var value))
            return defaultValue;
        return value;
    }

    private async Task<decimal> GetSettingDecimalAsync(string key, decimal defaultValue)
    {
        var raw = await GetSettingRawAsync(key);
        if (raw == null || !decimal.TryParse(raw, out var value))
            return defaultValue;
        return value;
    }

    private async Task<string?> GetSettingRawAsync(string key)
    {
        var setting = await _dbContext.SystemSettings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key);
        return setting?.Value;
    }

    private async Task UpsertSettingAsync(string key, string value, TenantId tenantId, Guid updatedBy)
    {
        var setting = await _dbContext.SystemSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Key == key);

        if (setting == null)
        {
            setting = new SystemSetting(tenantId, key, value, updatedBy);
            _dbContext.SystemSettings.Add(setting);
        }
        else
        {
            setting.Update(value, updatedBy);
        }
    }
}
