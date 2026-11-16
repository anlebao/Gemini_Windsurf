// ATRS-5 (2026-11-16): WebAuthn JS interop cho KhachLink (navigator.credentials).
// KhachLink WASM gọi qua IJSRuntime: vananWebAuthn.createCredential / getAssertion.
// Trung thực về capability: unsupported → trả null (UI hiện "thiết bị không hỗ trợ").
// KHÔNG bao giờ lộ private key — browser quản lý; chỉ trả về attestation/assertion JSON.
(function () {
    "use strict";

    function isSupported() {
        return typeof window !== "undefined"
            && !!window.PublicKeyCredential
            && !!navigator.credentials
            && typeof navigator.credentials.create === "function"
            && typeof navigator.credentials.get === "function";
    }

    // options: CredentialCreateOptions JSON (từ server) → chuyển Uint8Array base64url sang ArrayBuffer
    function decodeBase64Url(value) {
        if (typeof value !== "string") return value;
        var base64 = value.replace(/-/g, "+").replace(/_/g, "/");
        while (base64.length % 4) base64 += "=";
        var binary = atob(base64);
        var bytes = new Uint8Array(binary.length);
        for (var i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
        return bytes.buffer;
    }

    function encodeBase64Url(buffer) {
        var bytes = new Uint8Array(buffer);
        var binary = "";
        for (var i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);
        return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
    }

    function prepareCreateOptions(options) {
        var copy = JSON.parse(JSON.stringify(options));
        if (copy.challenge) copy.challenge = decodeBase64Url(copy.challenge);
        if (copy.user && copy.user.id) copy.user.id = decodeBase64Url(copy.user.id);
        if (Array.isArray(copy.excludeCredentials)) {
            copy.excludeCredentials = copy.excludeCredentials.map(function (c) {
                var x = JSON.parse(JSON.stringify(c));
                if (x.id) x.id = decodeBase64Url(x.id);
                return x;
            });
        }
        return copy;
    }

    function prepareGetOptions(options) {
        var copy = JSON.parse(JSON.stringify(options));
        if (copy.challenge) copy.challenge = decodeBase64Url(copy.challenge);
        if (Array.isArray(copy.allowCredentials)) {
            copy.allowCredentials = copy.allowCredentials.map(function (c) {
                var x = JSON.parse(JSON.stringify(c));
                if (x.id) x.id = decodeBase64Url(x.id);
                return x;
            });
        }
        return copy;
    }

    // Trả attestation response ở dạng WebAuthn JSON (toJSON) — chính là shape
    // AuthenticatorAttestationRawResponse mà server (Fido2 lib) cần.
    function attestationToJson(credential) {
        var response = credential.response;
        return {
            id: credential.id,
            rawId: encodeBase64Url(credential.rawId),
            type: credential.type,
            response: {
                clientDataJSON: encodeBase64Url(response.clientDataJSON),
                attestationObject: encodeBase64Url(response.attestationObject),
                transports: Array.isArray(response.getTransports) ? response.getTransports() : []
            },
            clientExtensionResults: credential.getClientExtensionResults ? credential.getClientExtensionResults() : {}
        };
    }

    function assertionToJson(credential) {
        var response = credential.response;
        return {
            id: credential.id,
            rawId: encodeBase64Url(credential.rawId),
            type: credential.type,
            response: {
                clientDataJSON: encodeBase64Url(response.clientDataJSON),
                authenticatorData: encodeBase64Url(response.authenticatorData),
                signature: encodeBase64Url(response.signature),
                userHandle: response.userHandle ? encodeBase64Url(response.userHandle) : null
            },
            clientExtensionResults: credential.getClientExtensionResults ? credential.getClientExtensionResults() : {}
        };
    }

    window.vananWebAuthn = {
        isSupported: function () { return isSupported(); },

        // Origin thật của trang (https://host) — dùng cho WebAuthn ceremony + RP ID derive.
        getOrigin: function () { return window.location.origin; },

        // options: CredentialCreateOptions (JSON nguyên từ server begin). → attestation JSON | null
        createCredential: async function (options) {
            if (!isSupported()) return null;
            try {
                var credential = await navigator.credentials.create({
                    publicKey: prepareCreateOptions(options)
                });
                if (!credential) return null; // user cancel
                return attestationToJson(credential);
            } catch (err) {
                // NotAllowedError = user cancel; NotSupportedError/unsupported = null → UI message
                console.warn("[vananWebAuthn] create failed:", err && err.name);
                return null;
            }
        },

        // options: AssertionOptions (JSON nguyên từ server begin). → assertion JSON | null
        getAssertion: async function (options) {
            if (!isSupported()) return null;
            try {
                var credential = await navigator.credentials.get({
                    publicKey: prepareGetOptions(options)
                });
                if (!credential) return null;
                return assertionToJson(credential);
            } catch (err) {
                console.warn("[vananWebAuthn] get failed:", err && err.name);
                return null;
            }
        }
    };
})();