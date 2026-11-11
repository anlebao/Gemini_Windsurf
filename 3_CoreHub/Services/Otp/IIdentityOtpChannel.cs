using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services.Otp;

/// <summary>
/// W2 / P4 (2026-11-10, user duyệt): kênh gửi OTP — transport pluggable (email/SMS/ZNS/voice).
/// Zero-Cost Identity L2: channel = commodity, registry phía trên không đổi.
/// Email (Brevo 0đ) là kênh đầu tiên (chốt) — ZNS 300đ slot khi có Zalo OA.
/// </summary>
public interface IIdentityOtpChannel
{
    string ChannelName { get; }                       // "email" | "sms" | "zns" | "voice"
    Task<bool> SendOtpAsync(string contact, string code, CancellationToken ct = default);
}