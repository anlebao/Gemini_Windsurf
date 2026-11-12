// VOICE SERVICE P2 — node unit tests for the reusable voice SDK (window.vananVoice).
// Run: node --test 6_Testing/unit-tests/voice-sdk.test.mjs  (repo convention: node --test)
// Strategy: inject a FAKE SignalR connection + FAKE media — no real network, no real mic
// (browser WebRTC is covered by the Playwright E2E spec voice-p2p.spec.ts).

import { test } from 'node:test';
import assert from 'node:assert/strict';

globalThis.window = globalThis;
await import(new URL('../../10_Voice/vanan-voice/sdk/js/voice-sdk.js', import.meta.url));
const vananVoice = window.vananVoice;

const UUIDV7 = /^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;

function fakeConnection() {
    const handlers = {};
    const invokes = [];
    let responses = {};
    return {
        handlers,
        invokes,
        started: false,
        setResponses(r) { responses = r; },
        async start() { this.started = true; },
        async stop() { this.started = false; },
        on(method, cb) { handlers[method] = cb; },
        async invoke(method, ...args) {
            invokes.push({ method, args });
            if (responses[method]) return responses[method];
            return { callId: undefined, status: 'RINGING', reason: null };
        },
        emit(method, payload) { return handlers[method] ? handlers[method](payload) : undefined; },
        invoked(method) { return invokes.find(i => i.method === method); }
    };
}

function fakeMedia() {
    return {
        micRequests: 0,
        muted: false,
        offerCount: 0,
        peerHandlers: null,
        isSupported: () => true,
        async requestMic() { this.micRequests++; return { id: 'local-stream' }; },
        stopLocal() {},
        setMuted(m) { this.muted = m; },
        async createPeer(_ice, handlers) { this.peerHandlers = handlers; this.lastIceServers = _ice; },
        async createOffer(restart) { this.offerCount++; return { sdp: restart ? 'offer-restart' : 'offer-init' }; },
        async createAnswer() { return { sdp: 'answer' }; },
        async setRemoteAnswer() {},
        async addIceCandidate() {},
        getConnectionState() { return 'connected'; },
        closePeer() {}
    };
}

const TIMEOUT = { ringTimeoutSeconds: 30, negotiationTimeoutSeconds: 20, iceTimeoutSeconds: 15, maxDurationMinutes: 120 };
const microtask = () => new Promise(resolve => setTimeout(resolve, 0));

async function setup() {
    const connection = fakeConnection();
    const media = fakeMedia();
    const states = [];
    const localStreams = [];
    await vananVoice.initialize({
        connection,
        media,
        hubUrl: 'http://x/hubs/voice',
        onStateChange: (s) => states.push(s),
        onLocalStream: (s) => localStreams.push(s)
    });
    return { connection, media, states, localStreams };
}

test('initialize connects and reports idle', async () => {
    const { connection, states } = await setup();
    assert.equal(connection.started, true);
    assert.equal(states[0], 'idle');
});

test('startCall generates UUIDv7 callId and sends StartCall with context', async () => {
    const { connection, states } = await setup();
    const target = '11111111-1111-4111-8111-111111111111';

    await vananVoice.startCall(target, { contextType: 'chat', contextReferenceId: 'conv-1' });

    const start = connection.invoked('StartCall');
    assert.ok(start, 'StartCall must be invoked');
    assert.equal(start.args[0].targetUserId, target);
    assert.equal(start.args[0].contextType, 'chat');
    assert.equal(start.args[0].contextReferenceId, 'conv-1');
    assert.match(start.args[0].callId, UUIDV7);
    assert.equal(states[states.length - 1], 'calling');
});

test('caller full flow: StartCall → CallCreated → CallAccepted → offer → answer → connected', async () => {
    const { connection, media, states, localStreams } = await setup();
    const callId = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';

    await vananVoice.startCall('11111111-1111-4111-8111-111111111111');
    connection.emit('CallCreated', { callId, timeoutConfig: TIMEOUT });
    assert.equal(states[states.length - 1], 'calling');

    connection.emit('CallAccepted', { callId });
    await microtask();

    assert.ok(connection.invoked('SendOffer'), 'caller must send the offer');
    assert.equal(connection.invoked('SendOffer').args[0].sdp, 'offer-init');
    assert.equal(localStreams.length, 1, 'caller mic requested once');
    assert.equal(states[states.length - 1], 'negotiating');

    connection.emit('ReceiveAnswer', { callId, sdp: 'answer' });
    await microtask();
    // Media connected → SDK notifies the server (server confirms with CallConnected).
    media.peerHandlers.onConnectionStateChange('connected');
    await microtask();
    assert.ok(connection.invoked('NotifyConnected'), 'SDK must notify the server on media connect');
    connection.emit('CallConnected', { callId });
    assert.equal(states[states.length - 1], 'connected');
});

test('callee flow: IncomingCall → accept → answer → connected (mic only on accept)', async () => {
    const { connection, media, states, localStreams } = await setup();
    const callId = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';

    connection.emit('IncomingCall', { callId, callerUserId: '11111111-1111-4111-8111-111111111111', timeoutConfig: TIMEOUT });
    assert.equal(states[states.length - 1], 'ringing');
    assert.equal(media.micRequests, 0, 'NO mic before user action (SRS §9.5)');

    await vananVoice.acceptCall();
    await microtask();
    assert.ok(connection.invoked('AcceptCall'));
    assert.equal(media.micRequests, 1, 'mic requested on accept');

    connection.emit('CallAccepted', { callId }); // acceptedLocally path
    await microtask();
    assert.equal(states[states.length - 1], 'accepted');

    connection.emit('ReceiveOffer', { callId, sdp: 'offer' });
    await microtask();
    assert.ok(connection.invoked('SendAnswer'));
    assert.equal(states[states.length - 1], 'negotiating');

    connection.emit('CallConnected', { callId });
    assert.equal(states[states.length - 1], 'connected');
});

test('multi-device: CallAccepted elsewhere cleans up a ringing device (first-accept-wins)', async () => {
    const { connection, states } = await setup();
    connection.emit('IncomingCall', { callId: 'c', callerUserId: 'x', timeoutConfig: TIMEOUT });
    assert.equal(states[states.length - 1], 'ringing');

    connection.emit('CallAccepted', { callId: 'c' }); // accepted on another device
    await microtask();

    assert.equal(states[states.length - 1], 'idle');
});

test('ring timeout ends a ringing call as missed', async () => {
    const { connection, states } = await setup();
    connection.emit('IncomingCall', {
        callId: 'd', callerUserId: 'x',
        timeoutConfig: { ...TIMEOUT, ringTimeoutSeconds: 0.05 }
    });
    assert.equal(states[states.length - 1], 'ringing');

    await new Promise(r => setTimeout(r, 120));
    assert.equal(states[states.length - 1], 'missed');
});

test('offline outcome from StartCall response transitions to offline', async () => {
    const { connection, states } = await setup();
    connection.setResponses({ StartCall: { callId: 'e', status: 'OFFLINE', reason: 'OFFLINE' } });

    await vananVoice.startCall('11111111-1111-4111-8111-111111111111');
    assert.equal(states[states.length - 1], 'offline');
});

test('terminal state auto-returns to idle and allows immediate retry (S6)', async () => {
    const { connection, states } = await setup();
    connection.setResponses({ StartCall: { callId: 'e2', status: 'OFFLINE', reason: 'OFFLINE' } });

    await vananVoice.startCall('11111111-1111-4111-8111-111111111111');
    assert.equal(states[states.length - 1], 'offline');

    // Retry must NOT throw "Đang có cuộc gọi khác." — terminal states are retryable.
    connection.setResponses({ StartCall: { callId: 'e3', status: 'RINGING' } });
    await vananVoice.startCall('22222222-2222-4222-8222-222222222222');
    assert.equal(vananVoice.getCallState().state, 'calling');
    assert.equal(states.filter(s => s === 'idle').length >= 2, true, 'idle re-emitted for panel sync');

    // If the call ends terminally again, the SDK parks in idle ~1.5s later.
    connection.emit('CallOffline', { callId: 'e3', reason: 'OFFLINE' });
    assert.equal(vananVoice.getCallState().state, 'offline');
    await new Promise(r => setTimeout(r, 1700));
    assert.equal(vananVoice.getCallState().state, 'idle');
});

test('mute/unmute toggles media and state', async () => {
    const { connection, media } = await setup();
    await vananVoice.startCall('11111111-1111-4111-8111-111111111111');
    connection.emit('CallCreated', { callId: 'f', timeoutConfig: TIMEOUT });
    connection.emit('CallAccepted', { callId: 'f' });
    await microtask();
    connection.emit('CallConnected', { callId: 'f' });

    vananVoice.mute();
    assert.equal(media.muted, true);
    assert.equal(vananVoice.getCallState().muted, true);
    vananVoice.unmute();
    assert.equal(media.muted, false);
});

test('ICE restart is bounded to 2 attempts then stops notifying', async () => {
    const { connection, media, states } = await setup();
    await vananVoice.startCall('11111111-1111-4111-8111-111111111111');
    connection.emit('CallCreated', { callId: 'g', timeoutConfig: TIMEOUT });
    connection.emit('CallAccepted', { callId: 'g' });
    await microtask();
    connection.emit('CallConnected', { callId: 'g' });

    // Attempt 1
    media.peerHandlers.onConnectionStateChange('disconnected');
    await microtask();
    assert.equal(states[states.length - 1], 'reconnecting');
    assert.ok(connection.invoked('NotifyReconnecting'));
    assert.equal(media.offerCount, 2, 'restart offer sent');

    // Back to connected
    media.peerHandlers.onConnectionStateChange('connected');
    await microtask();
    assert.ok(connection.invoked('NotifyReconnected'));
    assert.equal(states[states.length - 1], 'connected');

    // Attempt 2 then 3 — the 3rd must NOT notify (max 2 restarts, SRS FR-008)
    media.peerHandlers.onConnectionStateChange('disconnected');
    await microtask();
    media.peerHandlers.onConnectionStateChange('disconnected');
    await microtask();

    const reconnectingCount = connection.invokes.filter(i => i.method === 'NotifyReconnecting').length;
    assert.equal(reconnectingCount, 2);
    assert.equal(vananVoice.getCallState().iceRestartCount, 2);
});

test('endCall invokes EndCall', async () => {
    const { connection } = await setup();
    await vananVoice.startCall('11111111-1111-4111-8111-111111111111');
    connection.emit('CallCreated', { callId: 'h', timeoutConfig: TIMEOUT });
    connection.emit('CallAccepted', { callId: 'h' });
    await microtask();
    connection.emit('CallConnected', { callId: 'h' });

    await vananVoice.endCall();
    assert.ok(connection.invoked('EndCall'));
    connection.emit('CallEnded', { callId: 'h', reason: 'USER_ENDED' });
    assert.equal(vananVoice.getCallState().state, 'ended');
});

test('startCall while active is rejected', async () => {
    const { connection } = await setup();
    await vananVoice.startCall('11111111-1111-4111-8111-111111111111');
    await assert.rejects(() => vananVoice.startCall('22222222-2222-4222-8222-222222222222'));
});

test('dispose cleans up and returns to idle', async () => {
    const { connection } = await setup();
    await vananVoice.startCall('11111111-1111-4111-8111-111111111111');
    await vananVoice.dispose();
    assert.equal(connection.started, false);
    assert.equal(vananVoice.getCallState().state, 'idle');
});

test('TURN credentials are fetched per call and passed to the media layer', async () => {
    const originalFetch = globalThis.fetch;
    let fetched = 0;
    globalThis.fetch = async (url, opts) => {
        fetched++;
        assert.equal(url, 'http://voice.test/v1/turn/credentials');
        assert.equal(opts.headers.Authorization, 'Bearer turn-test-token');
        return {
            ok: true,
            json: async () => ({ urls: ['turn:turn.example:3478?transport=udp'], username: 'u1', credential: 'c1', ttlSeconds: 1800 })
        };
    };
    try {
        const connection = fakeConnection();
        const media = fakeMedia();
        await vananVoice.initialize({
            connection, media,
            hubUrl: 'http://x/hubs/voice',
            turnCredentialsUrl: 'http://voice.test/v1/turn/credentials',
            accessTokenProvider: () => 'turn-test-token'
        });
        await vananVoice.startCall('11111111-1111-4111-8111-111111111111');
        connection.emit('CallCreated', { callId: 't1', timeoutConfig: TIMEOUT });
        connection.emit('CallAccepted', { callId: 't1' });
        await microtask();

        assert.equal(fetched, 1, 'credentials fetched once per call');
        // createPeer received the TURN server entry.
        assert.ok(media.lastIceServers, 'iceServers passed to createPeer');
        assert.equal(media.lastIceServers[0].username, 'u1');
        assert.equal(media.lastIceServers[0].credential, 'c1');
        assert.deepEqual(media.lastIceServers[0].urls, ['turn:turn.example:3478?transport=udp']);
    } finally {
        globalThis.fetch = originalFetch;
    }
});

test('TURN fetch failure falls back to STUN-only ICE (fail-open)', async () => {
    const originalFetch = globalThis.fetch;
    globalThis.fetch = async () => ({ ok: false });
    try {
        const connection = fakeConnection();
        const media = fakeMedia();
        await vananVoice.initialize({
            connection, media,
            hubUrl: 'http://x/hubs/voice',
            turnCredentialsUrl: 'http://voice.test/v1/turn/credentials',
            token: 't'
        });
        await vananVoice.startCall('11111111-1111-4111-8111-111111111111');
        connection.emit('CallCreated', { callId: 't2', timeoutConfig: TIMEOUT });
        connection.emit('CallAccepted', { callId: 't2' });
        await microtask();

        assert.ok(connection.invoked('SendOffer'), 'call proceeds without TURN');
        assert.deepEqual(media.lastIceServers, [], 'no TURN servers on failure');
    } finally {
        globalThis.fetch = originalFetch;
    }
});