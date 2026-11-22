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
    let _initialized = false;
    let _initInFlight = false;

    async function initialize(dotNetRef, options) {
        voice = window.vananVoice;
        if (!voice) throw new Error('voice-sdk.js chưa được load trước voice-chat.js.');
        // In-flight guard: presence panel + chat panel có thể init gần như đồng thời —
        // isInitialized() phải trả true ngay cả khi initialize đang await, nếu không
        // caller thứ 2 cũng init → ghi đè callbacks của owner.
        if (_initInFlight) throw new Error('voice-chat đang khởi tạo.');
        _initInFlight = true;
        try {
            const serviceUrl = (options.serviceUrl || '').replace(/\/+$/, '');
            await voice.initialize({
                hubUrl: options.hubUrl,
                token: options.token || null,
                turnCredentialsUrl: serviceUrl ? serviceUrl + '/v1/turn/credentials' : null,
                onStateChange: (s, info) => dotNetRef.invokeMethodAsync('OnVoiceStateChange', s, JSON.stringify(info || {})),
                // S3 (2026-11-12): hub transport state — host disables the call button until Connected.
                onConnectionStateChange: (s) => dotNetRef.invokeMethodAsync('OnVoiceConnectionState', s || ''),
                onIncoming: (call) => dotNetRef.invokeMethodAsync('OnVoiceIncoming', JSON.stringify(call || {})),
                onLocalStream: () => dotNetRef.invokeMethodAsync('OnVoiceLocalStream'),
                onRemoteStream: () => dotNetRef.invokeMethodAsync('OnVoiceRemoteStream'),
                // VIDEO MVP: camera/remote-video state → Blazor renders the host divs.
                onVideoChange: (localOn, remoteOn) => dotNetRef.invokeMethodAsync('OnVoiceVideoChange', !!localOn, !!remoteOn),
                onError: (msg) => dotNetRef.invokeMethodAsync('OnVoiceError', msg || '')
            });
            _initialized = true;
        } finally {
            _initInFlight = false;
        }
    }

    // VOICE PRESENCE (2026-11-21): singleton guard — layout presence panel và chat
    // VoiceCallPanel không được initialize chồng nhau (initialize ghi đè callbacks).
    function isInitialized() { return (_initialized || _initInFlight) && voice !== null; }

    // targetUserId may be empty — the server resolves the conversation counterpart (P4).
    // peerId (2026-11-13): shop threads are per-customer — staff pass the customer id so the
    // context verifier scopes verification/counterpart resolution to THAT thread, not every
    // customer conversation of the shop.
    // callType 'audio'|'video' (VIDEO MVP 2026-11-22) — default 'audio'.
    async function startCall(targetUserId, subjectType, subjectId, peerId, callType) {
        console.info('[voice-chat] startCall invoked', { targetUserId, subjectType, subjectId, peerId, callType, sdkState: voice ? voice.getCallState() : null });
        try {
            const contextRef = subjectType + ':' + subjectId + (peerId ? ':' + peerId : '');
            await voice.startCall(targetUserId || null, {
                contextType: 'chat',
                contextReferenceId: contextRef,
                callType: callType === 'video' ? 'video' : 'audio'
            });
            console.info('[voice-chat] startCall returned');
        } catch (ex) {
            console.warn('[voice-chat] startCall failed', ex);
            throw ex;
        }
    }

    async function acceptCall() {
        const result = await voice.acceptCall();
        console.info('[voice-chat] acceptCall →', result);
        return result;
    }
    async function rejectCall() { return voice.rejectCall(); }
    async function endCall() { return voice.endCall(); }
    function mute() { return voice.mute(); }
    function unmute() { return voice.unmute(); }
    // VIDEO MVP passthroughs — optional-chained so older SDK builds degrade cleanly.
    function toggleCamera() { return voice.toggleCamera ? voice.toggleCamera() : Promise.resolve(false); }
    function switchCamera() { return voice.switchCamera ? voice.switchCamera() : Promise.resolve(null); }
    function getCallType() { return voice.getCallType ? voice.getCallType() : 'audio'; }
    function isCameraOn() { return voice.isCameraOn ? voice.isCameraOn() : false; }
    function attachVideoElements() { if (voice.attachVideoElements) voice.attachVideoElements(); }
    function getState() { return voice.getCallState(); }
    function getConnectionState() { return voice ? voice.getConnectionState() : 'disconnected'; }

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

    async function dispose() {
        if (voice) { try { await voice.dispose(); } catch (_) { /* best effort */ } voice = null; }
        _initialized = false;
    }

    return {
        initialize, startCall, acceptCall, rejectCall, endCall,
        mute, unmute, getState, getConnectionState, fetchMissedCalls, dispose,
        toggleCamera, switchCamera, getCallType, isCameraOn, attachVideoElements,
        isInitialized
    };
})();