// VOICE SERVICE P4 (2026-11-11): Blazor ↔ window.vananVoice bridge for chat-first calls.
// Hosted by UI.Platform so BOTH KhachLink (storefront chat) and ShopERP (inbox) get the
// same voice UI with one component (SRS v1.1 §9.2 — "no duplicated WebRTC implementation").
//
// Blazor contract (JSInvokable on the calling component):
//   OnVoiceStateChange(state, infoJson)  — UI state machine events
//   OnVoiceIncoming(callJson)            — incoming call (callerUserId, verifiedContextLabel)
//   OnVoiceLocalStream()                 — local mic ready (attach muted <audio>)
//   OnVoiceRemoteStream()                — remote audio ready (attach autoplay <audio>)
//   OnVoiceError(message)                — localized error (Vietnamese)
//
// Loaded via <script src="/_content/VanAn.UI.Platform/js/voice-chat.js"> after
// voice-sdk.js + voice-media.js (host includes them, e.g. in App.razor).

window.vananVoiceChat = (function () {
    let voice = null;

    async function initialize(dotNetRef, options) {
        voice = window.vananVoice;
        if (!voice) throw new Error('voice-sdk.js chưa được load trước voice-chat.js.');

        const serviceUrl = (options.serviceUrl || '').replace(/\/+$/, '');
        await voice.initialize({
            hubUrl: options.hubUrl,
            token: options.token || null,
            turnCredentialsUrl: serviceUrl ? serviceUrl + '/v1/turn/credentials' : null,
            onStateChange: (s, info) => dotNetRef.invokeMethodAsync('OnVoiceStateChange', s, JSON.stringify(info || {})),
            onIncoming: (call) => dotNetRef.invokeMethodAsync('OnVoiceIncoming', JSON.stringify(call || {})),
            onLocalStream: () => dotNetRef.invokeMethodAsync('OnVoiceLocalStream'),
            onRemoteStream: () => dotNetRef.invokeMethodAsync('OnVoiceRemoteStream'),
            onError: (msg) => dotNetRef.invokeMethodAsync('OnVoiceError', msg || '')
        });
    }

    // targetUserId may be empty — the server resolves the conversation counterpart (P4).
    async function startCall(targetUserId, subjectType, subjectId) {
        await voice.startCall(targetUserId || null, {
            contextType: 'chat',
            contextReferenceId: subjectType + ':' + subjectId
        });
    }

    async function acceptCall() { return voice.acceptCall(); }
    async function rejectCall() { return voice.rejectCall(); }
    async function endCall() { return voice.endCall(); }
    function mute() { return voice.mute(); }
    function unmute() { return voice.unmute(); }
    function getState() { return voice.getCallState(); }

    // FR-014: callee reads its own missed-call events on next app open.
    async function fetchMissedCalls(serviceUrl, userId, token) {
        try {
            const url = (serviceUrl || '').replace(/\/+$/, '') + '/v1/missed-calls/' + userId;
            const headers = { Accept: 'application/json' };
            if (token) headers.Authorization = 'Bearer ' + token;
            const resp = await fetch(url, { headers });
            if (!resp.ok) return [];
            return await resp.json();
        } catch (_) {
            return [];
        }
    }

    async function dispose() { if (voice) await voice.dispose(); }

    return {
        initialize, startCall, acceptCall, rejectCall, endCall,
        mute, unmute, getState, fetchMissedCalls, dispose
    };
})();