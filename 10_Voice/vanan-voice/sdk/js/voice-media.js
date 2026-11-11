// VOICE SERVICE P2 (2026-11-11): thin WebRTC media wrapper for the reusable voice SDK.
// Contract (SRS v1.1 §9.5): mic requested ONLY on explicit user action; mono Opus
// 32–64 kbps target with a 128 kbps hard cap; echoCancellation/noiseSuppression/autoGainControl
// mandatory; ICE restart support for network handover (Wi-Fi → 4G).
//
// Loaded via <script src=".../voice-media.js"> — plain script (not module) so any host
// (WASM/Server/SSR) can include it (same convention as voice-input.js).
//
// Blazor/host contract: all methods return Promises; asynchronous media events are
// delivered through the handlers passed to createPeer():
//   onIceCandidate(candidate)  — relay to the signaling server
//   onConnectionStateChange(state) — 'new'|'connecting'|'connected'|'disconnected'|'failed'|'closed'
//   onTrack(stream)            — remote audio stream (attach to <audio autoplay>)

window.vananVoiceMedia = (function () {
    const MIC_CONSTRAINTS = {
        audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true },
        video: false
    };
    const OPUS_MAX_BITRATE = 128 * 1000; // bits/s — SRS §9.5 hard cap

    let localStream = null;
    let peer = null;
    let handlers = null;

    function isSupported() {
        return !!(window.RTCPeerConnection
            && navigator.mediaDevices && navigator.mediaDevices.getUserMedia);
    }

    // Request microphone access (explicit user action only — SDK guarantees the call site).
    async function requestMic() {
        if (localStream) return localStream;
        const stream = await navigator.mediaDevices.getUserMedia(MIC_CONSTRAINTS);
        stream.getAudioTracks().forEach(track => {
            try { track.applyConstraints({ maxBitrate: OPUS_MAX_BITRATE }); } catch (_) { /* unsupported */ }
        });
        localStream = stream;
        return stream;
    }

    function stopLocal() {
        if (localStream) {
            localStream.getTracks().forEach(track => track.stop());
            localStream = null;
        }
    }

    function setMuted(muted) {
        if (localStream) localStream.getAudioTracks().forEach(track => { track.enabled = !muted; });
    }

    // Create an RTCPeerConnection; local tracks are attached immediately.
    async function createPeer(iceServers, eventHandlers) {
        closePeer();
        handlers = eventHandlers || {};
        peer = new RTCPeerConnection({ iceServers: iceServers || [] });
        if (localStream) localStream.getTracks().forEach(track => peer.addTrack(track, localStream));
        peer.onicecandidate = e => {
            if (e.candidate && handlers.onIceCandidate) handlers.onIceCandidate(e.candidate);
        };
        peer.onconnectionstatechange = () => {
            if (handlers.onConnectionStateChange) handlers.onConnectionStateChange(peer.connectionState);
        };
        peer.ontrack = e => {
            if (handlers.onTrack) handlers.onTrack(e.streams[0]);
        };
        return peer;
    }

    function closePeer() {
        if (peer) { peer.close(); peer = null; }
    }

    async function createOffer(iceRestart) {
        const offer = await peer.createOffer(iceRestart ? { iceRestart: true } : undefined);
        await peer.setLocalDescription(offer);
        return offer;
    }

    async function createAnswer(offerSdp) {
        await peer.setRemoteDescription({ type: 'offer', sdp: offerSdp });
        const answer = await peer.createAnswer();
        await peer.setLocalDescription(answer);
        return answer;
    }

    async function setRemoteAnswer(answerSdp) {
        await peer.setRemoteDescription({ type: 'answer', sdp: answerSdp });
    }

    async function addIceCandidate(candidate) {
        if (peer && candidate) await peer.addIceCandidate(candidate);
    }

    function getConnectionState() {
        return peer ? peer.connectionState : 'new';
    }

    // Diagnostic hook (tests/debug only).
    function debug() {
        return {
            hasPeer: !!peer,
            connectionState: peer ? peer.connectionState : 'none',
            iceConnectionState: peer ? peer.iceConnectionState : 'none',
            signalingState: peer ? peer.signalingState : 'none',
            hasLocalStream: !!localStream
        };
    }

    return {
        isSupported,
        requestMic,
        stopLocal,
        setMuted,
        createPeer,
        closePeer,
        createOffer,
        createAnswer,
        setRemoteAnswer,
        addIceCandidate,
        getConnectionState,
        debug
    };
})();