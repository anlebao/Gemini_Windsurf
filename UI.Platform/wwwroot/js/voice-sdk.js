// VOICE SERVICE P2 (2026-11-11): reusable one-to-one voice SDK — the "reusable frontend
// abstraction" of SRS v1.1 §9.1. Any host app (Blazor WASM/Server/SSR) includes:
//   1. vendor/signalr.min.js   (@microsoft/signalr 8.x browser bundle)
//   2. voice-media.js          (WebRTC wrapper)
//   3. voice-sdk.js            (this file)
// then calls window.vananVoice.initialize({ hubUrl, accessTokenProvider, ... }).
//
// Principles:
//   - Mic is requested ONLY on explicit user action (startCall / acceptCall) — SRS §9.5.
//   - Client state is provisional until the server confirms (server authoritative — §5).
//   - Bounded ICE restart: max 2 within the reconnect window (SRS FR-008).
//   - callId = UUIDv7 generated client-side (idempotency key of StartCall).
//
// Host contract (callbacks in initialize options):
//   onStateChange(state, info) — state machine events for the UI
//   onIncoming(call)           — incoming call notification (callerUserId, verifiedContextLabel)
//   onLocalStream(stream)      — attach to a muted <audio> element
//   onRemoteStream(stream)     — attach to an autoplay <audio> element
//   onError(message)           — localized error (Vietnamese)

window.vananVoice = (function () {
    const STATES = {
        Idle: 'idle', Calling: 'calling', Ringing: 'ringing', Accepted: 'accepted',
        Negotiating: 'negotiating', Connected: 'connected', Reconnecting: 'reconnecting',
        Ended: 'ended', Rejected: 'rejected', Cancelled: 'cancelled', Missed: 'missed',
        Busy: 'busy', Offline: 'offline', CallCollision: 'collision', Failed: 'failed'
    };
    const ACTIVE = new Set(['calling', 'ringing', 'accepted', 'negotiating', 'connected', 'reconnecting']);
    // S6 (2026-11-12): terminal outcomes are retryable — without this the SDK stayed in
    // e.g. 'offline' forever and every later startCall threw "Đang có cuộc gọi khác."
    // while the Blazor panel silently swallowed the click (buttons looked dead).
    const TERMINAL = new Set(['ended', 'rejected', 'cancelled', 'missed', 'busy', 'offline', 'collision', 'failed']);
    const MAX_ICE_RESTARTS = 2;

    let connection = null;
    let media = null;
    let options = null;
    let state = STATES.Idle;
    let callId = null;
    let isCaller = false;
    let acceptedLocally = false;
    let muted = false;
    let iceRestartCount = 0;
    let remoteStream = null;
    // S3 (2026-11-12): hub transport state — the call button stays disabled until the
    // SignalR connection is actually Connected; invoking earlier throws
    // "Cannot send data if the connection is not in the 'Connected' State".
    let hubState = 'disconnected';
    let timeoutConfig = { ringTimeoutSeconds: 30, negotiationTimeoutSeconds: 20, iceTimeoutSeconds: 15, maxDurationMinutes: 120 };
    const timers = { ring: null, negotiate: null, reconnect: null, idle: null };

    // ---- helpers ----

    function newCallId() {
        // UUIDv7 (RFC 9562): version 7, variant 10, random payload — 36-char lowercase.
        const bytes = new Uint8Array(16);
        crypto.getRandomValues(bytes);
        bytes[6] = (bytes[6] & 0x0f) | 0x70;
        bytes[8] = (bytes[8] & 0x3f) | 0x80;
        const hex = Array.from(bytes, b => b.toString(16).padStart(2, '0')).join('');
        return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
    }

    function setState(next, info) {
        if (state === next) return;
        state = next;
        if (options && options.onStateChange) options.onStateChange(next, info || {});
    }

    function isActive() { return ACTIVE.has(state); }

    function clearTimers() {
        Object.keys(timers).forEach(k => { if (timers[k]) { clearTimeout(timers[k]); timers[k] = null; } });
    }

    // Terminal states are final: late server confirmations are idempotent no-ops.
    function finishTerminal(finalState, info) {
        if (!isActive() && state !== finalState) return;
        clearTimers();
        teardownMedia();
        setState(finalState, info);
        // Return to idle shortly after so the UI can offer a retry — hosts render
        // terminal outcomes (error/missed hints) from their own state, not from
        // the SDK staying parked in the terminal state forever.
        timers.idle = setTimeout(() => {
            timers.idle = null;
            if (state === finalState) {
                state = STATES.Idle;
                if (options && options.onStateChange) options.onStateChange(STATES.Idle, {});
            }
        }, 1500);
    }

    function teardownMedia() {
        if (media) { media.closePeer(); media.stopLocal(); }
        remoteStream = null;
        stopRemoteAudio();
        acceptedLocally = false;
        iceRestartCount = 0;
    }

    function emitError(message) {
        if (options && options.onError) options.onError(message);
    }

    function emitHubState(next) {
        if (hubState === next) return;
        hubState = next;
        console.info('[vananVoice] hub state →', next);
        if (options && options.onConnectionStateChange) options.onConnectionStateChange(next);
    }

    async function prepareMedia() {
        if (!media) return;
        const stream = await media.requestMic();
        if (options && options.onLocalStream && stream) options.onLocalStream(stream);
    }

    // ---- hub event handlers ----

    function onCallCreated(d) {
        callId = d.callId;
        if (d.timeoutConfig) timeoutConfig = d.timeoutConfig;
        setState(STATES.Calling, { callId });
    }

    function onIncoming(d) {
        callId = d.callId;
        isCaller = false;
        acceptedLocally = false;
        if (d.timeoutConfig) timeoutConfig = d.timeoutConfig;
        setState(STATES.Ringing, { callId, callerUserId: d.callerUserId, verifiedContextLabel: d.verifiedContextLabel });
        if (options && options.onIncoming) options.onIncoming(d);
        timers.ring = setTimeout(() => {
            if (state === STATES.Ringing && !acceptedLocally) finishTerminal(STATES.Missed, { reason: 'RING_TIMEOUT' });
        }, timeoutConfig.ringTimeoutSeconds * 1000);
    }

    // Called on BOTH sides after acceptance (caller's device + every callee device).
    async function onCallAccepted(d) {
        if (d && d.callId && d.callId !== callId) return;

        if (isCaller) {
            // Caller: the callee picked up — start negotiation.
            setState(STATES.Accepted, { callId });
            await startNegotiationAsCaller();
        } else if (acceptedLocally) {
            // Callee that accepted: wait for the caller's offer.
            setState(STATES.Accepted, { callId });
            startNegotiationTimer();
        } else {
            // Another device of the same user accepted — clean up locally (first-accept-wins).
            clearTimers();
            teardownMedia();
            setState(STATES.Idle, { answeredElsewhere: true });
        }
    }

    async function startNegotiationAsCaller() {
        try {
            await prepareMedia();
            const iceServers = await resolveIceServers();
            await media.createPeer(iceServers, {
                onIceCandidate: c => relayIceCandidate(c),
                onConnectionStateChange: onConnectionStateChange,
                onTrack: onRemoteTrack
            });
            const offer = await media.createOffer(false);
            setState(STATES.Negotiating, { callId });
            await connection.invoke('SendOffer', { callId, sdp: offer.sdp });
            startNegotiationTimer();
        } catch (ex) {
            emitError('Không thể khởi tạo cuộc gọi: ' + (ex && ex.message ? ex.message : ex));
        }
    }

    async function onReceiveOffer(d) {
        if (d.callId !== callId) return;
        try {
            const answer = await media.createAnswer(d.sdp);
            setState(STATES.Negotiating, { callId });
            await connection.invoke('SendAnswer', { callId, sdp: answer.sdp });
        } catch (ex) {
            emitError('Lỗi xử lý offer: ' + (ex && ex.message ? ex.message : ex));
        }
    }

    async function onReceiveAnswer(d) {
        if (d.callId !== callId) return;
        try {
            await media.setRemoteAnswer(d.sdp);
        } catch (ex) {
            emitError('Lỗi xử lý answer: ' + (ex && ex.message ? ex.message : ex));
        }
    }

    async function onReceiveIce(d) {
        if (d.callId !== callId) return;
        try {
            await media.addIceCandidate({
                candidate: d.candidate,
                sdpMid: d.sdpMid ?? null,
                sdpMLineIndex: d.sdpMLineIndex ?? null
            });
        } catch (_) { /* stale candidate */ }
    }

    function onCallConnected(d) {
        if (d && d.callId && d.callId !== callId) return;
        clearTimers();
        setState(STATES.Connected, { callId });
    }

    // Remote audio playback: JSInvokable cannot receive a MediaStream into .NET, so the
    // stream is played HERE on a hidden autoplay <audio> element. Without this the call
    // connects but the user hears nothing (prod "connected, no audio", 2026-11-13).
    let remoteAudioEl = null;

    function playRemoteStream(stream) {
        try {
            if (!remoteAudioEl) {
                remoteAudioEl = document.createElement('audio');
                remoteAudioEl.id = 'vanan-voice-remote-audio';
                remoteAudioEl.autoplay = true;
                remoteAudioEl.playsInline = true;
                remoteAudioEl.style.display = 'none';
                document.body.appendChild(remoteAudioEl);
            }
            remoteAudioEl.srcObject = stream;
            remoteAudioEl.play().catch(() => { /* autoplay blocked until next gesture */ });
        } catch (_) { /* best effort — element may not exist in non-DOM hosts */ }
    }

    function stopRemoteAudio() {
        if (remoteAudioEl) {
            try { remoteAudioEl.pause(); remoteAudioEl.srcObject = null; } catch (_) { /* best effort */ }
        }
    }

    function onRemoteTrack(stream) {
        remoteStream = stream;
        playRemoteStream(stream);
        if (options && options.onRemoteStream) options.onRemoteStream(stream);
    }

    // ICE candidate relay: the server contract carries sdpMid/sdpMLineIndex alongside
    // the candidate string — without them addIceCandidate() throws and trickle ICE
    // silently dies (prod NEGOTIATION_TIMEOUT, 2026-11-13). End-of-candidates is
    // relayed as an empty candidate string.
    function relayIceCandidate(candidate) {
        if (!connection) return;
        if (candidate == null) {
            connection.invoke('SendIceCandidate', { callId, candidate: '' }).catch(() => {});
            return;
        }
        const value = typeof candidate === 'string' ? candidate : candidate.candidate;
        if (!value) return;
        connection.invoke('SendIceCandidate', {
            callId,
            candidate: value,
            sdpMid: typeof candidate === 'string' ? null : (candidate.sdpMid ?? null),
            sdpMLineIndex: typeof candidate === 'string' ? null : (candidate.sdpMLineIndex ?? null)
        }).catch(() => {});
    }

    // Media connection state → ICE restart (bounded) + connected confirmation.
    // Initial connect: Negotiating → media 'connected' → notify server → CallConnected confirms.
    function onConnectionStateChange(mediaState) {
        if (state === STATES.Negotiating && mediaState === 'connected') {
            connection.invoke('NotifyConnected', callId).catch(() => {});
        } else if (state === STATES.Reconnecting && mediaState === 'connected') {
            clearTimeout(timers.reconnect); timers.reconnect = null;
            connection.invoke('NotifyReconnected', callId).catch(() => {});
            setState(STATES.Connected, { callId });
        } else if (state === STATES.Connected && (mediaState === 'disconnected' || mediaState === 'failed')) {
            if (iceRestartCount >= MAX_ICE_RESTARTS) return; // server will Fail the call
            iceRestartCount++;
            setState(STATES.Reconnecting, { attempt: iceRestartCount });
            connection.invoke('NotifyReconnecting', callId).catch(() => {});
            media.createOffer(true)
                .then(offer => connection.invoke('SendOffer', { callId, sdp: offer.sdp }))
                .catch(() => {});
            timers.reconnect = setTimeout(() => {
                if (state === STATES.Reconnecting) finishTerminal(STATES.Failed, { reason: 'RECONNECT_FAILED' });
            }, timeoutConfig.iceTimeoutSeconds * 1000);
        }
    }

    function startNegotiationTimer() {
        timers.negotiate = setTimeout(() => {
            if (state === STATES.Accepted || state === STATES.Negotiating) {
                finishTerminal(STATES.Failed, { reason: 'NEGOTIATION_TIMEOUT' });
            }
        }, timeoutConfig.negotiationTimeoutSeconds * 1000);
    }

    // Short-lived TURN credentials from the voice service (SRS §8.2): fetched fresh per call,
    // FAIL-OPEN — any error falls back to configured/STUN-only ICE (P2P keeps working).
    async function resolveIceServers() {
        if (!options.turnCredentialsUrl) return options.iceServers || [];
        try {
            const token = options.accessTokenProvider ? await options.accessTokenProvider() : (options.token || null);
            const headers = { Accept: 'application/json' };
            if (token) headers.Authorization = 'Bearer ' + token;
            const resp = await fetch(options.turnCredentialsUrl, { headers });
            if (!resp.ok) return options.iceServers || [];
            const data = await resp.json();
            if (!data || !Array.isArray(data.urls) || !data.username || !data.credential) return options.iceServers || [];
            return [{ urls: data.urls, username: data.username, credential: data.credential }];
        } catch (_) {
            return options.iceServers || [];
        }
    }

    // ---- public API (SRS §9.1) ----

    async function initialize(opts) {
        options = opts || {};
        media = options.media || window.vananVoiceMedia;
        if (!media || !media.isSupported()) throw new Error('Trình duyệt không hỗ trợ WebRTC.');

        if (options.connection) {
            connection = options.connection; // injected (tests / custom transports)
        } else if (window.signalR) {
            connection = new window.signalR.HubConnectionBuilder()
                .withUrl(options.hubUrl, {
                    accessTokenFactory: options.accessTokenProvider
                        || (() => options.token || null)
                })
                .build();
        } else {
            throw new Error('Thiếu thư viện @microsoft/signalr.');
        }

        connection.on('CallCreated', onCallCreated);
        connection.on('IncomingCall', onIncoming);
        connection.on('CallAccepted', onCallAccepted);
        connection.on('CallRejected', () => finishTerminal(STATES.Rejected));
        connection.on('CallCancelled', () => finishTerminal(STATES.Cancelled));
        connection.on('CallBusy', () => finishTerminal(STATES.Busy));
        connection.on('CallCollision', () => finishTerminal(STATES.CallCollision));
        connection.on('CallOffline', d => finishTerminal(STATES.Offline, d));
        connection.on('ReceiveOffer', onReceiveOffer);
        connection.on('ReceiveAnswer', onReceiveAnswer);
        connection.on('ReceiveIceCandidate', onReceiveIce);
        connection.on('CallConnected', onCallConnected);
        connection.on('CallReconnecting', () => setState(STATES.Reconnecting));
        connection.on('CallReconnected', () => setState(STATES.Connected));
        connection.on('CallEnded', d => finishTerminal(STATES.Ended, d));
        connection.on('CallFailed', d => finishTerminal(STATES.Failed, d));

        // Transport state → host UI. Injected/mock connections may lack these.
        if (typeof connection.onreconnecting === 'function') connection.onreconnecting(() => emitHubState('reconnecting'));
        if (typeof connection.onreconnected === 'function') connection.onreconnected(() => emitHubState('connected'));
        if (typeof connection.onclose === 'function') connection.onclose(() => emitHubState('disconnected'));

        emitHubState('connecting');
        try {
            await connection.start();
        } catch (ex) {
            emitHubState('disconnected');
            throw ex;
        }
        emitHubState('connected');
        state = STATES.Idle;
        if (options.onStateChange) options.onStateChange(STATES.Idle, {});
        return this;
    }

    // Outgoing call. contextType/contextReferenceId are optional verified business context.
    async function startCall(targetUserId, context) {
        console.info('[vananVoice] startCall', { targetUserId, state, hubState });
        // Terminal states don't block a retry — reset locally and let the server
        // decide (a pending idle-reset timer no-ops because state already moved on).
        if (state !== STATES.Idle) {
            if (!TERMINAL.has(state)) throw new Error('Đang có cuộc gọi khác.');
            if (timers.idle) { clearTimeout(timers.idle); timers.idle = null; }
            state = STATES.Idle;
            // Keep the Blazor panel in sync — its call button gates on state == idle.
            if (options && options.onStateChange) options.onStateChange(STATES.Idle, {});
        }
        if (hubState !== 'connected') throw new Error('Mất kết nối máy chủ cuộc gọi.');
        callId = newCallId();
        isCaller = true;
        acceptedLocally = false;
        iceRestartCount = 0;
        setState(STATES.Calling, { callId, targetUserId });

        let result;
        try {
            result = await connection.invoke('StartCall', {
                callId,
                // SignalR/STJ cannot bind JSON null into non-nullable Guid — it throws
                // during argument materialization BEFORE the hub method runs (no server
                // log, no DB row). Send the empty-guid literal for chat-first calls;
                // the hub accepts it when contextType/contextReferenceId are present.
                targetUserId: targetUserId || '00000000-0000-0000-0000-000000000000',
                contextType: context ? context.contextType : null,
                contextReferenceId: context ? context.contextReferenceId : null
            });
        } catch (ex) {
            // Hub-level rejection (validation/rate-limit/transport) — never park in
            // 'calling' forever; return to idle so the UI can retry (S6).
            console.warn('[vananVoice] StartCall invoke rejected', ex);
            state = STATES.Idle;
            emitError(ex && ex.message ? ex.message : String(ex));
            if (options && options.onStateChange) options.onStateChange(STATES.Idle, {});
            throw ex;
        }

        // Server already resolved a terminal outcome (OFFLINE/BUSY/COLLISION/FAILED).
        if (result && result.status) {
            const terminalMap = {
                OFFLINE: STATES.Offline, BUSY: STATES.Busy, CALL_COLLISION: STATES.CallCollision, FAILED: STATES.Failed
            };
            if (terminalMap[result.status] && state === STATES.Calling) {
                finishTerminal(terminalMap[result.status], { reason: result.reason });
            }
        }
        return result;
    }

    // Accept the incoming call (callee, explicit user action).
    async function acceptCall() {
        if (state !== STATES.Ringing) return false;
        acceptedLocally = true;
        clearTimeout(timers.ring); timers.ring = null;
        try {
            await prepareMedia();
            const iceServers = await resolveIceServers();
            await media.createPeer(iceServers, {
                onIceCandidate: c => relayIceCandidate(c),
                onConnectionStateChange: onConnectionStateChange,
                onTrack: onRemoteTrack
            });
            await connection.invoke('AcceptCall', callId);
            // onCallAccepted (acceptedLocally=true) will move us to Accepted.
            return true;
        } catch (ex) {
            emitError('Không thể nghe máy: ' + (ex && ex.message ? ex.message : ex));
            return false;
        }
    }

    async function rejectCall() {
        if (state !== STATES.Ringing) return false;
        clearTimeout(timers.ring); timers.ring = null;
        await connection.invoke('RejectCall', callId);
        finishTerminal(STATES.Rejected);
        return true;
    }

    async function cancelCall() {
        if (state !== STATES.Calling && state !== STATES.Ringing) return false;
        await connection.invoke('CancelCall', callId);
        finishTerminal(STATES.Cancelled);
        return true;
    }

    async function endCall() {
        if (!isActive()) return false;
        await connection.invoke('EndCall', callId);
        // Terminal event (CallEnded) from the server confirms; local UI may already clear.
        return true;
    }

    function mute() {
        muted = true;
        if (media) media.setMuted(true);
        return muted;
    }

    function unmute() {
        muted = false;
        if (media) media.setMuted(false);
        return muted;
    }

    function getCallState() {
        return { state, callId, muted, iceRestartCount, isCaller, connectionState: hubState };
    }

    function getConnectionState() {
        return hubState;
    }

    async function dispose() {
        clearTimers();
        if (connection) { try { await connection.stop(); } catch (_) { /* best effort */ } }
        teardownMedia();
        state = STATES.Idle;
        emitHubState('disconnected');
    }

    return {
        initialize, startCall, acceptCall, rejectCall, cancelCall, endCall,
        mute, unmute, getCallState, getConnectionState, dispose,
        // test hooks
        _states: STATES
    };
})();