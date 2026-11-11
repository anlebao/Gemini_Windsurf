// VOICE SERVICE P4 — node unit tests for the Blazor↔SDK bridge (voice-chat.js).
// Run: node --test 6_Testing/unit-tests/voice-chat.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';

// Load voice-chat.js with a fake window carrying a fake vananVoice SDK.
const invokes = [];
globalThis.window = {
    vananVoice: {
        async initialize(opts) { invokes.push(['initialize', opts]); },
        async startCall(target, ctx) { invokes.push(['startCall', target, ctx]); },
        async acceptCall() { invokes.push(['acceptCall']); return true; },
        async rejectCall() { invokes.push(['rejectCall']); return true; },
        async endCall() { invokes.push(['endCall']); return true; },
        mute() { invokes.push(['mute']); return true; },
        unmute() { invokes.push(['unmute']); return true; },
        getCallState() { return { state: 'connected' }; },
        async dispose() { invokes.push(['dispose']); }
    }
};
await import(new URL('../../UI.Platform/wwwroot/js/voice-chat.js', import.meta.url));
const chat = window.vananVoiceChat;

function dotNetRef() {
    const calls = [];
    return {
        calls,
        invokeMethodAsync(method, ...args) { calls.push([method, ...args]); return Promise.resolve(); }
    };
}

test('initialize wires hub + TURN + .NET callbacks', async () => {
    invokes.length = 0;
    const ref = dotNetRef();
    await chat.initialize(ref, { hubUrl: 'http://v/hubs/voice', serviceUrl: 'http://v/', token: 'tok' });

    const init = invokes.find(i => i[0] === 'initialize');
    assert.ok(init);
    const opts = init[1];
    assert.equal(opts.hubUrl, 'http://v/hubs/voice');
    assert.equal(opts.turnCredentialsUrl, 'http://v/v1/turn/credentials');
    assert.equal(opts.token, 'tok');
    // callbacks bridge back to .NET
    await opts.onStateChange('connected', '{}');
    await opts.onIncoming({ callerUserId: 'x' });
    await opts.onError('lỗi');
    assert.deepEqual(ref.calls.map(c => c[0]), ['OnVoiceStateChange', 'OnVoiceIncoming', 'OnVoiceError']);
});

test('startCall sends chat context "{SubjectType}:{SubjectId}" and empty target', async () => {
    invokes.length = 0;
    await chat.startCall('', 'Shop', 'guid-1');
    const call = invokes.find(i => i[0] === 'startCall');
    assert.deepEqual(call[1], null); // target resolved server-side (P4)
    assert.deepEqual(call[2], { contextType: 'chat', contextReferenceId: 'Shop:guid-1' });
});

test('accept/reject/end/mute/getState/dispose bridge to the SDK', async () => {
    invokes.length = 0;
    await chat.acceptCall();
    await chat.rejectCall();
    await chat.endCall();
    chat.mute();
    chat.unmute();
    const state = chat.getState();
    await chat.dispose();
    assert.deepEqual(invokes.map(i => i[0]),
        ['acceptCall', 'rejectCall', 'endCall', 'mute', 'unmute', 'dispose']);
    assert.equal(state.state, 'connected');
});

test('fetchMissedCalls returns [] on failure and parses on success', async () => {
    const originalFetch = globalThis.fetch;
    try {
        globalThis.fetch = async () => ({ ok: false });
        assert.deepEqual(await chat.fetchMissedCalls('http://v/', 'user-1', 'tok'), []);

        globalThis.fetch = async (url, opts) => {
            assert.equal(url, 'http://v/v1/missed-calls/user-1');
            assert.equal(opts.headers.Authorization, 'Bearer tok');
            return { ok: true, json: async () => [{ callId: 'c1' }] };
        };
        assert.deepEqual(await chat.fetchMissedCalls('http://v/', 'user-1', 'tok'), [{ callId: 'c1' }]);
    } finally {
        globalThis.fetch = originalFetch;
    }
});