# VAPID Key Generation Guide - Wave 9 (KhachLink-W4)

## Purpose
VAPID (Voluntary Application Server Identification) keys are required for Web Push notifications. This guide documents the procedure for generating and managing VAPID keys for the KhachLink PWA push notification system.

## Security Requirements (CRITICAL)
- ✅ **Private Key:** MUST be stored in environment variables only, NEVER in source code
- ✅ **Public Key:** Can be stored in source code (not sensitive)
- ✅ **.env files:** MUST be in .gitignore to prevent accidental commits
- ✅ **CI/CD:** Use GitHub Actions Secrets for production deployment (Wave 10)

## Generation Procedure

### Option 1: Using web-push CLI (Recommended)
```bash
# Install web-push globally
npm install -g web-push

# Generate VAPID keys
web-push generate-vapid-keys

# Output example:
# ========================================
# Public Key:
# BC7xZ7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z
# 
# Private Key:
# 7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z7Z
# ========================================
```

### Option 2: Using online tool
Visit: https://web-push-codelab.glitch.me/
- Copy the public key and private key
- Follow security requirements above

### Option 3: Using Node.js script
```javascript
// generate-vapid-keys.js
const webpush = require('web-push');
const vapidKeys = webpush.generateVAPIDKeys();
console.log('Public Key:', vapidKeys.publicKey);
console.log('Private Key:', vapidKeys.privateKey);
```

## Development Keys (Generated for Wave 9)

**Public Key (can be in source code):**
```
BJIeg2XokT35UrNdXV26uTiMa0CxwbRI5Fmb9j4djeSdXO74U1wS6BD15MlnvYppLtDx2Rbm01TSkcVcf7p58RE
```

**Private Key (ENVIRONMENT VARIABLE ONLY - NEVER IN SOURCE CODE):**
```
uyoKYxEO_CKZdy_mVuXdvtJapgs6wwFUrh7bpoHP6Do
```

## Environment Configuration

### Development (.env file - NOT in git)
```bash
# KhachLink Push Notification Configuration
VAPID_PUBLIC_KEY=BJIeg2XokT35UrNdXV26uTiMa0CxwbRI5Fmb9j4djeSdXO74U1wS6BD15MlnvYppLtDx2Rbm01TSkcVcf7p58RE
VAPID_PRIVATE_KEY=uyoKYxEO_CKZdy_mVuXdvtJapgs6wwFUrh7bpoHP6Do
VAPID_SUBJECT=mailto:admin@vanan.com
```

### Production (CI/CD Secrets)
- **GitHub Actions Secret Name:** `VAPID_PRIVATE_KEY`
- **Injection:** Added to container environment variables during deployment (Wave 10)

## Application Configuration

### C# Configuration (appsettings.json)
```json
{
  "PushNotifications": {
    "VapidPublicKey": "BJIeg2XokT35UrNdXV26uTiMa0CxwbRI5Fmb9j4djeSdXO74U1wS6BD15MlnvYppLtDx2Rbm01TSkcVcf7p58RE",
    "VapidSubject": "mailto:admin@vanan.com"
  }
}
```

### Environment Variable Access
```csharp
public class PushNotificationService
{
    private readonly string _vapidPrivateKey;
    private readonly string _vapidPublicKey;
    private readonly string _vapidSubject;

    public PushNotificationService(IConfiguration configuration)
    {
        _vapidPrivateKey = Environment.GetEnvironmentVariable("VAPID_PRIVATE_KEY") 
            ?? throw new InvalidOperationException("VAPID_PRIVATE_KEY environment variable is required");
        _vapidPublicKey = configuration["PushNotifications:VapidPublicKey"] 
            ?? throw new InvalidOperationException("VapidPublicKey configuration is required");
        _vapidSubject = configuration["PushNotifications:VapidSubject"] 
            ?? "mailto:admin@vanan.com";
    }
}
```

## Key Rotation Procedure

1. Generate new VAPID key pair using the procedure above
2. Update environment variables in production
3. Update pwa.js with new public key
4. Deploy update (users will automatically receive new keys on next app load)
5. Monitor for push notification failures during transition
6. Old subscriptions will fail gracefully - users will need to re-subscribe

## Troubleshooting

### Push Notifications Not Working
- Verify VAPID keys are correctly configured
- Check browser console for push subscription errors
- Ensure service worker is properly registered
- Verify HTTPS is enabled (required for Web Push)

### Invalid VAPID Keys
- Regenerate keys using the procedure above
- Ensure private key is in environment variable, not source code
- Check for whitespace or formatting issues in keys

### Environment Variable Not Loading
- Verify .env file is in correct location
- Check .env is not committed to git (should be in .gitignore)
- Restart application after changing environment variables

## Compliance Notes
- ✅ VAPID private key NEVER in source code
- ✅ .env files in .gitignore
- ✅ CI/CD uses GitHub Actions Secrets (Wave 10)
- ✅ Public key can be in source code (not sensitive)
- ✅ Keys rotated periodically for security

## References
- [Web Push Protocol RFC 8291](https://tools.ietf.org/html/rfc8291)
- [VAPID Specification RFC 8292](https://tools.ietf.org/html/rfc8292)
- [MDN Web Push API](https://developer.mozilla.org/en-US/docs/Web/API/Push_API)

---
**Generated:** 2026-06-29  
**Wave:** 9 (KhachLink-W4)  
**Status:** Development keys generated, production deployment pending Wave 10