using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;

namespace VanAn.Atrs.Infra;

/// <summary>
/// WebAuthn/passkey ceremony — dựa trên Fido2 lib (không tự viết crypto, FR-004/NFR-001).
/// Challenge: random CSPRNG (lib sinh), one-time, TTL 5', binding subject+origin+RP ID+purpose.
/// Attestation policy: None (consumer, ADR ATRS-5). KHÔNG lưu private key.
/// </summary>
public sealed class WebAuthnService(
    AtrsDbContext db,
    IOptions<WebAuthnOptions> options) : IWebAuthnService
{
    private const int MaxActiveChallengesPerSubject = 5;
    private readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<BeginRegistrationResultDto> BeginRegistrationAsync(
        Guid trustSubjectId, Guid? deviceRecordId, string origin, string rpId, CancellationToken ct = default)
    {
        var opt = options.Value;
        var fido2 = CreateFido2(origin, rpId);

        var existing = await db.DeviceCredentials
            .Where(c => c.TrustSubjectId == trustSubjectId && c.Status == CredentialStatus.ACTIVE)
            .Select(c => c.CredentialId)
            .ToListAsync(ct);
        var exclude = existing
            .Select(id => new PublicKeyCredentialDescriptor(Base64Url.Decode(id)))
            .ToList();

        var createOptions = fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = new Fido2User
            {
                Id = trustSubjectId.ToByteArray(),
                Name = trustSubjectId.ToString("N"),
                DisplayName = "Vạn An customer"
            },
            ExcludeCredentials = exclude,
            AuthenticatorSelection = new AuthenticatorSelection
            {
                UserVerification = ParseEnum(opt.UserVerification, UserVerificationRequirement.Preferred),
                ResidentKey = ParseEnum(opt.ResidentKey, ResidentKeyRequirement.Preferred)
            },
            AttestationPreference = ParseEnum(opt.AttestationConveyance, AttestationConveyancePreference.None),
            Extensions = new AuthenticationExtensionsClientInputs { Extensions = true, CredProps = true }
        });

        var challenge = await StoreChallengeAsync(
            trustSubjectId, WebAuthnChallengePurpose.Registration, createOptions.Challenge,
            origin, rpId, deviceRecordId, createOptions, ct);

        return new BeginRegistrationResultDto(
            challenge, ToJsonElement(createOptions), DateTime.UtcNow.AddMinutes(opt.ChallengeTtlMinutes));
    }

    public async Task<FinishRegistrationResultDto> FinishRegistrationAsync(
        Guid trustSubjectId, string challenge, string origin, string rpId, JsonElement attestationResponse,
        Guid? deviceRecordId, CancellationToken ct = default)
    {
        var opt = options.Value;
        var stored = await LoadChallengeAsync(trustSubjectId, WebAuthnChallengePurpose.Registration, challenge, origin, rpId, ct);
        if (stored is null)
            return new FinishRegistrationResultDto(false, "InvalidChallenge", null, null);

        var fido2 = CreateFido2(origin, rpId);
        var rawResponse = attestationResponse.Deserialize<AuthenticatorAttestationRawResponse>(_jsonOpts);
        if (rawResponse is null)
        {
            await ConsumeAsync(stored, ct);
            return new FinishRegistrationResultDto(false, "InvalidAttestation", null, null);
        }

        var originalOptions = DeserializeOptions<CredentialCreateOptions>(stored.OptionsJson);

        RegisteredPublicKeyCredential result;
        try
        {
            result = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = rawResponse,
                OriginalOptions = originalOptions,
                IsCredentialIdUniqueToUserCallback = async (p, _) =>
                    !await db.DeviceCredentials.AnyAsync(c => c.CredentialId == Base64Url.Encode(p.CredentialId), ct)
            }, ct);
        }
        catch (Fido2VerificationException)
        {
            await ConsumeAsync(stored, ct);
            return new FinishRegistrationResultDto(false, "InvalidAttestation", null, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await ConsumeAsync(stored, ct);
            return new FinishRegistrationResultDto(false, "InvalidAttestation", null, null);
        }

        var credential = new DeviceCredential(
            id: Guid.NewGuid(),
            trustSubjectId,
            credentialId: Base64Url.Encode(result.Id),
            publicKey: Base64Url.Encode(result.PublicKey),
            signCount: result.SignCount,
            rpId: rpId,
            deviceRecordId: deviceRecordId ?? stored.DeviceRecordId,
            aaguid: result.AaGuid,
            transportsJson: SerializeTransports(result.Transports),
            backupEligible: result.IsBackupEligible,
            backupState: result.IsBackedUp);
        db.DeviceCredentials.Add(credential);

        if (deviceRecordId.HasValue)
        {
            var device = await db.UserDevices.FirstOrDefaultAsync(d => d.Id == deviceRecordId.Value && d.TrustSubjectId == trustSubjectId, ct);
            if (device is not null)
            {
                device.Activate();
                device.Touch();
            }
        }

        await ConsumeAsync(stored, ct);
        await db.SaveChangesAsync(ct);
        return new FinishRegistrationResultDto(true, "Success", ToSummary(credential), credential.DeviceRecordId);
    }

    public async Task<BeginAuthenticationResultDto> BeginAuthenticationAsync(
        Guid trustSubjectId, string origin, string rpId, CancellationToken ct = default)
    {
        var opt = options.Value;
        var fido2 = CreateFido2(origin, rpId);

        var active = await db.DeviceCredentials
            .Where(c => c.TrustSubjectId == trustSubjectId && c.Status == CredentialStatus.ACTIVE)
            .ToListAsync(ct);
        var allowed = active
            .Select(c => new PublicKeyCredentialDescriptor(
                PublicKeyCredentialType.PublicKey,
                Base64Url.Decode(c.CredentialId),
                c.TransportsJson is null ? null : DeserializeTransports(c.TransportsJson)))
            .ToList();

        var assertOptions = fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = allowed,
            UserVerification = ParseEnum(opt.UserVerification, UserVerificationRequirement.Preferred)
        });

        var challenge = await StoreChallengeAsync(
            trustSubjectId, WebAuthnChallengePurpose.Authentication, assertOptions.Challenge,
            origin, rpId, null, assertOptions, ct);

        return new BeginAuthenticationResultDto(
            challenge, ToJsonElement(assertOptions), DateTime.UtcNow.AddMinutes(opt.ChallengeTtlMinutes));
    }

    public async Task<FinishAuthenticationResultDto> FinishAuthenticationAsync(
        Guid trustSubjectId, string challenge, string origin, string rpId, JsonElement assertionResponse,
        CancellationToken ct = default)
    {
        var stored = await LoadChallengeAsync(trustSubjectId, WebAuthnChallengePurpose.Authentication, challenge, origin, rpId, ct);
        if (stored is null)
            return new FinishAuthenticationResultDto(false, "InvalidChallenge", null, null);

        var rawResponse = assertionResponse.Deserialize<AuthenticatorAssertionRawResponse>(_jsonOpts);
        if (rawResponse is null)
        {
            await ConsumeAsync(stored, ct);
            return new FinishAuthenticationResultDto(false, "InvalidAssertion", null, null);
        }

        var credentialId = Base64Url.Encode(rawResponse.RawId);
        var credential = await db.DeviceCredentials
            .FirstOrDefaultAsync(c => c.TrustSubjectId == trustSubjectId
                && c.CredentialId == credentialId
                && c.Status == CredentialStatus.ACTIVE, ct);
        if (credential is null)
        {
            await ConsumeAsync(stored, ct);
            return new FinishAuthenticationResultDto(false, "RevokedCredential", null, null);
        }

        var fido2 = CreateFido2(origin, rpId);
        var originalOptions = DeserializeOptions<AssertionOptions>(stored.OptionsJson);
        try
        {
            var result = await fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = rawResponse,
                OriginalOptions = originalOptions,
                StoredPublicKey = Base64Url.Decode(credential.PublicKey),
                StoredSignatureCounter = credential.SignCount,
                IsUserHandleOwnerOfCredentialIdCallback = async (p, _) =>
                {
                    // userHandle = subjectId bytes (Fido2User.Id) — chỉ credential của subject này.
                    var user = p.UserHandle;
                    return user is not null
                        && user.Length == 16
                        && new Guid(user) == trustSubjectId
                        && Base64Url.Encode(p.CredentialId) == credentialId;
                }
            }, ct);

            credential.MarkUsed(result.SignCount, result.IsBackedUp);
            await ConsumeAsync(stored, ct);
            await db.SaveChangesAsync(ct);
            return new FinishAuthenticationResultDto(true, "Success", credential.Id, null);
        }
        catch (Fido2VerificationException)
        {
            await ConsumeAsync(stored, ct);
            return new FinishAuthenticationResultDto(false, "InvalidAssertion", null, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await ConsumeAsync(stored, ct);
            return new FinishAuthenticationResultDto(false, "InvalidAssertion", null, null);
        }
    }

    public async Task<IReadOnlyList<DeviceCredentialSummaryDto>> ListCredentialsAsync(Guid trustSubjectId, CancellationToken ct = default)
    {
        var creds = await db.DeviceCredentials
            .AsNoTracking()
            .Where(c => c.TrustSubjectId == trustSubjectId && c.Status == CredentialStatus.ACTIVE)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);
        return creds.Select(ToSummary).ToList();
    }

    public async Task<bool> RevokeCredentialAsync(Guid trustSubjectId, Guid credentialId, CancellationToken ct = default)
    {
        var credential = await db.DeviceCredentials
            .FirstOrDefaultAsync(c => c.Id == credentialId && c.TrustSubjectId == trustSubjectId && c.Status == CredentialStatus.ACTIVE, ct);
        if (credential is null)
            return false;
        credential.Revoke();
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> RevokeAllForDeviceAsync(Guid trustSubjectId, Guid deviceRecordId, CancellationToken ct = default)
    {
        var creds = await db.DeviceCredentials
            .Where(c => c.TrustSubjectId == trustSubjectId
                && c.DeviceRecordId == deviceRecordId
                && c.Status == CredentialStatus.ACTIVE)
            .ToListAsync(ct);
        foreach (var c in creds)
            c.Revoke();
        if (creds.Count > 0)
            await db.SaveChangesAsync(ct);
        return creds.Count;
    }

    // ---- helpers ----

    private Fido2 CreateFido2(string origin, string rpId)
    {
        var opt = options.Value;
        var origins = new HashSet<string>(opt.Origins, StringComparer.OrdinalIgnoreCase);
        if (!origins.Contains(origin))
            throw new InvalidOperationException("Origin không nằm trong allowlist WebAuthn.");

        return new Fido2(new Fido2Configuration
        {
            RPID = rpId,
            RPName = opt.RpName,
            Origins = origins,
            Timeout = opt.TimeoutMs,
            ChallengeSize = 32,
            TimestampDriftTolerance = 300000
        });
    }

    private async Task<string> StoreChallengeAsync(
        Guid trustSubjectId, WebAuthnChallengePurpose purpose, byte[] challengeBytes,
        string origin, string rpId, Guid? deviceRecordId, object ceremonyOptions, CancellationToken ct)
    {
        var opt = options.Value;

        // Rate guard: tối đa N challenge đang active/subject (anti-spam, anti-account-doom, FR-004/NFR).
        var activeCount = await db.WebAuthnChallenges
            .CountAsync(c => c.TrustSubjectId == trustSubjectId && c.ConsumedAt == null, ct);
        if (activeCount >= MaxActiveChallengesPerSubject)
            throw new InvalidOperationException("Quá nhiều yêu cầu xác thực gần đây. Vui lòng thử lại sau.");

        var challenge = Base64Url.Encode(challengeBytes);
        db.WebAuthnChallenges.Add(new WebAuthnChallenge(
            Guid.NewGuid(), trustSubjectId, purpose, challenge, origin, rpId, deviceRecordId,
            DateTime.UtcNow.AddMinutes(opt.ChallengeTtlMinutes),
            JsonSerializer.Serialize(ceremonyOptions, _jsonOpts)));
        // Dọn challenge hết hạn cũ (mỗi lần begin — best-effort, không chặn luồng).
        var expired = await db.WebAuthnChallenges
            .Where(c => c.ExpiresAt < DateTime.UtcNow && c.ConsumedAt == null)
            .ToListAsync(ct);
        if (expired.Count > 0)
            db.WebAuthnChallenges.RemoveRange(expired);
        await db.SaveChangesAsync(ct);
        return challenge;
    }

    private async Task<WebAuthnChallenge?> LoadChallengeAsync(
        Guid trustSubjectId, WebAuthnChallengePurpose purpose, string challenge,
        string origin, string rpId, CancellationToken ct)
    {
        var row = await db.WebAuthnChallenges
            .FirstOrDefaultAsync(c => c.TrustSubjectId == trustSubjectId
                && c.Purpose == purpose
                && c.Challenge == challenge
                && c.ConsumedAt == null, ct);
        if (row is null)
            return null;
        if (row.IsExpired(DateTime.UtcNow))
            return null;
        // Binding origin + RP ID — replay từ origin khác → reject.
        if (!string.Equals(row.Origin, origin, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(row.RpId, rpId, StringComparison.OrdinalIgnoreCase))
            return null;
        return row;
    }

    private async Task ConsumeAsync(WebAuthnChallenge row, CancellationToken ct)
    {
        row.Consume(DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    private T DeserializeOptions<T>(string json) where T : class
        => JsonSerializer.Deserialize<T>(json, _jsonOpts) ?? throw new InvalidOperationException("Stored options invalid.");

    private JsonElement ToJsonElement(object value)
        => JsonSerializer.SerializeToElement(value, _jsonOpts);

    private static string? SerializeTransports(AuthenticatorTransport[] transports)
    {
        if (transports is null || transports.Length == 0)
            return null;
        return JsonSerializer.Serialize(transports.Select(t => t.ToString()).ToArray());
    }

    private static AuthenticatorTransport[]? DeserializeTransports(string json)
    {
        var names = JsonSerializer.Deserialize<string[]>(json);
        return names?.Select(n => Enum.TryParse<AuthenticatorTransport>(n, out var t) ? t : AuthenticatorTransport.Internal).ToArray();
    }

    private static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

    private static DeviceCredentialSummaryDto ToSummary(DeviceCredential c)
        => new(
            c.Id,
            c.CredentialId,
            c.RpId,
            c.TransportsJson is null ? null : JsonSerializer.Deserialize<string[]>(c.TransportsJson),
            c.BackupEligible,
            c.BackupState,
            c.Status.ToString(),
            c.CreatedAt,
            c.LastUsedAt);
}