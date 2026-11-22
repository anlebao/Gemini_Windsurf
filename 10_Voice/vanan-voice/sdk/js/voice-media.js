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
    // VIDEO MVP (2026-11-22): modest defaults — 1:1 chat call, bandwidth-friendly.
    const VIDEO_CONSTRAINTS = {
        width: { ideal: 640 }, height: { ideal: 480 }, frameRate: { ideal: 24, max: 30 }
    };
    const OPUS_MAX_BITRATE = 128 * 1000; // bits/s — SRS §9.5 hard cap

    let localStream = null;
    let peer = null;
    let handlers = null;
    // VIDEO MVP: sender của video track — giữ reference qua mọi toggle. replaceTrack
    // (không removeTrack) tránh end remote receiver track → camera bật lại khỏi cần
    // renegotiation và không rơi vào case Chrome không re-fire ontrack (RV V10 bug).
    let videoSender = null;
    // Remote candidates that arrive before setRemoteDescription completes are
    // queued — addIceCandidate throws in that window and would silently drop them.
    let remoteDescSet = false;
    let pendingIce = [];

    function isSupported() {
        return !!(window.RTCPeerConnection
            && navigator.mediaDevices && navigator.mediaDevices.getUserMedia);
    }

    // Request microphone access (explicit user action only — SDK guarantees the call site).
    async function requestMic() { return requestMedia(false); }

    // VIDEO MVP: video=true requests camera alongside the mic (video calls start
    // camera-on by default); audio-only calls keep the old mic-only prompt.
    async function requestMedia(video) {
        if (localStream) return localStream;
        const stream = await navigator.mediaDevices.getUserMedia(
            video ? { audio: MIC_CONSTRAINTS.audio, video: VIDEO_CONSTRAINTS } : MIC_CONSTRAINTS);
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

    function getLocalStream() { return localStream; }

    function hasVideoTrack() {
        return !!(localStream && localStream.getVideoTracks().some(t => t.readyState === 'live'));
    }

    // VIDEO MVP: camera toggle — replaceTrack(null) tắt video mà KHÔNG end remote
    // track (muted thay vì ended) → bật lại chỉ cần replaceTrack(track), không cần
    // re-offer và không đụng glare. First-time camera trên audio call → addTrack
    // (negotiation-needed, SDK vẫn gửi re-offer — harmless khi không cần).
    async function setCameraEnabled(on) {
        if (on) {
            if (hasVideoTrack()) return;
            const vs = await navigator.mediaDevices.getUserMedia({ video: VIDEO_CONSTRAINTS });
            const track = vs.getVideoTracks()[0];
            if (!localStream) localStream = vs;
            else localStream.addTrack(track);
            if (peer) {
                if (videoSender) { try { await videoSender.replaceTrack(track); } catch (_) { /* sender dead */ } }
                else videoSender = peer.addTrack(track, localStream);
            }
        } else {
            if (!localStream) return;
            localStream.getVideoTracks().forEach(t => {
                try { t.stop(); } catch (_) { /* already stopped */ }
                localStream.removeTrack(t);
            });
            if (videoSender) { try { await videoSender.replaceTrack(null); } catch (_) { /* sender dead */ } }
        }
    }

    // Real selected candidate-pair type for server metrics — "TURN" when either end
    // relays, else "P2P". Called once when the connection reaches 'connected'.
    async function getConnectionType() {
        try {
            if (!peer) return null;
            const stats = await peer.getStats();
            let pair = null;
            stats.forEach(r => { if (r.type === 'candidate-pair' && (r.selected || r.state === 'succeeded')) pair = r; });
            if (!pair) return null;
            let local = null, remote = null;
            stats.forEach(r => {
                if (r.id === pair.localCandidateId) local = r;
                if (r.id === pair.remoteCandidateId) remote = r;
            });
            return (local && local.candidateType === 'relay') || (remote && remote.candidateType === 'relay')
                ? 'TURN' : 'P2P';
        } catch (_) { return null; }
    }

    function getSignalingState() {
        return peer ? peer.signalingState : 'closed';
    }

    // Create an RTCPeerConnection; local tracks are attached immediately.
    async function createPeer(iceServers, eventHandlers) {
        closePeer();
        handlers = eventHandlers || {};
        remoteDescSet = false;
        pendingIce = [];
        peer = new RTCPeerConnection({ iceServers: iceServers || [] });
        if (localStream) localStream.getTracks().forEach(track => {
            const sender = peer.addTrack(track, localStream);
            if (track.kind === 'video') videoSender = sender;
        });
        peer.onicecandidate = e => {
            // e.candidate === null marks end-of-candidates — relay it so the remote
            // side can finish trickle ICE promptly.
            if (handlers.onIceCandidate) handlers.onIceCandidate(e.candidate);
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
        videoSender = null;
        if (peer) { peer.close(); peer = null; }
    }

    async function createOffer(iceRestart) {
        const offer = await peer.createOffer(iceRestart ? { iceRestart: true } : undefined);
        await peer.setLocalDescription(offer);
        return offer;
    }

    async function flushPendingIce() {
        const queued = pendingIce;
        pendingIce = [];
        for (const c of queued) {
            try { await peer.addIceCandidate(c); } catch (_) { /* stale */ }
        }
    }

    async function createAnswer(offerSdp) {
        // VIDEO MVP glare: polite side rolls back its own outstanding offer before
        // applying the remote one (camera-toggle re-offer racing on both ends).
        if (peer.signalingState !== 'stable') {
            try { await peer.setLocalDescription({ type: 'rollback' }); } catch (_) { /* older browsers */ }
        }
        await peer.setRemoteDescription({ type: 'offer', sdp: offerSdp });
        remoteDescSet = true;
        const answer = await peer.createAnswer();
        await peer.setLocalDescription(answer);
        await flushPendingIce();
        return answer;
    }

    async function setRemoteAnswer(answerSdp) {
        await peer.setRemoteDescription({ type: 'answer', sdp: answerSdp });
        remoteDescSet = true;
        await flushPendingIce();
    }

    async function addIceCandidate(candidate) {
        if (!peer) return;
        // End-of-candidates marker (empty candidate) — pass through as {}.
        const init = candidate && candidate.candidate
            ? { candidate: candidate.candidate, sdpMid: candidate.sdpMid ?? null, sdpMLineIndex: candidate.sdpMLineIndex ?? null }
            : {};
        if (!remoteDescSet) { pendingIce.push(init); return; }
        try { await peer.addIceCandidate(init); } catch (_) { /* stale candidate */ }
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
        requestMedia,
        stopLocal,
        setMuted,
        getLocalStream,
        hasVideoTrack,
        setCameraEnabled,
        getConnectionType,
        getSignalingState,
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