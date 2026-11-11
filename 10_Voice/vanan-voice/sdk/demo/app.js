// Vạn An Voice P2P demo wiring (sdk/demo). Two tabs:
//   tab 1: index.html?role=caller&token=<jwt>&target=<calleeUserId>
//   tab 2: index.html?role=callee&token=<jwt>
// Hub URL defaults to same origin /hubs/voice; override with &hub=<url>.

(function () {
    const params = new URLSearchParams(location.search);
    const role = params.get('role') || 'caller';
    const token = params.get('token') || '';
    const target = params.get('target') || '';
    const hub = params.get('hub') || location.origin + '/hubs/voice';

    const stateEl = document.getElementById('state');
    const infoEl = document.getElementById('info');
    const localAudio = document.getElementById('local-audio');
    const remoteAudio = document.getElementById('remote-audio');
    const btnCall = document.getElementById('btn-call');
    const btnAccept = document.getElementById('btn-accept');
    const btnReject = document.getElementById('btn-reject');
    const btnMute = document.getElementById('btn-mute');
    const btnEnd = document.getElementById('btn-end');

    function setBadge(text, cls) {
        stateEl.textContent = text;
        stateEl.className = 'badge' + (cls ? ' ' + cls : '');
    }

    const voice = window.vananVoice;

    voice.initialize({
        hubUrl: hub,
        token,
        iceServers: [],
        onStateChange: (state, info) => {
            setBadge(state, state === 'connected' ? 'active' : (state === 'ringing' ? 'ringing' : ''));
            infoEl.textContent = JSON.stringify({ state, callId: info.callId, reason: info.reason });
            btnAccept.disabled = state !== 'ringing';
            btnReject.disabled = state !== 'ringing';
            btnEnd.disabled = !['accepted', 'negotiating', 'connected', 'reconnecting'].includes(state);
            btnCall.disabled = state !== 'idle';
        },
        onIncoming: (call) => infoEl.textContent = 'Cuộc gọi từ ' + call.callerUserId,
        onLocalStream: (stream) => { localAudio.srcObject = stream; },
        onRemoteStream: (stream) => { remoteAudio.srcObject = stream; },
        onError: (message) => { infoEl.textContent = 'Lỗi: ' + message; }
    }).then(() => {
        setBadge('idle');
        if (role === 'caller' && target) {
            infoEl.textContent = 'Sẵn sàng gọi ' + target;
        }
    }).catch(err => {
        setBadge('lỗi: ' + err.message);
    });

    btnCall.onclick = () => voice.startCall(target).catch(err => infoEl.textContent = 'Lỗi: ' + err.message);
    btnAccept.onclick = () => voice.acceptCall();
    btnReject.onclick = () => voice.rejectCall();
    btnMute.onclick = () => { voice.getCallState().muted ? voice.unmute() : voice.mute(); };
    btnEnd.onclick = () => voice.endCall();
})();