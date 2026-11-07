// SF-W3 (2026-11-07): voice-input.js unit tests — transcript dedup + callbacks.
// Bug reproduced: old stop() folded latestDisplay into `accumulated`, then the
// browser flushed the interim tail as a final result → "câu hỏi câu hỏi".
// Fix: rebuild transcript from event.results[0..n] each event — no folding.
//
// Run: npm run test:unit (node --test unit-tests/) from 6_Testing/.

import { test, beforeEach } from 'node:test';
import assert from 'node:assert/strict';

global.window = {};
await import('../../UI.Platform/wwwroot/js/voice-input.js');
const vi = window.vananVoiceInput;

// Minimal SpeechRecognition fake. Chrome semantics: stop() flushes pending
// interim segments as final results (pendingFinal) before firing onend.
class FakeSR {
    constructor() { FakeSR.instance = this; this.pendingFinal = null; }
    start() { this.started = true; }
    stop() {
        if (this.pendingFinal) this.emit(this.pendingFinal);
        this.onend?.();
    }
    abort() {}
    emit(results) { this.onresult?.({ resultIndex: 0, results }); }
    emitError(error) { this.onerror?.({ error }); }
}
window.SpeechRecognition = FakeSR;

const makeRef = () => {
    const calls = [];
    return {
        calls,
        ref: { invokeMethodAsync: (name, text) => { calls.push([name, text]); return Promise.resolve(); } },
    };
};
const finalsOf = (calls) => calls.filter(([n]) => n === 'OnVoiceFinal').map(([, t]) => t);
const interimsOf = (calls) => calls.filter(([n]) => n === 'OnVoiceInterim').map(([, t]) => t);

test('stop() flush — final delivered once, no duplication', () => {
    const { calls, ref } = makeRef();
    assert.equal(vi.start(ref), true);
    const sr = FakeSR.instance;

    sr.emit([{ isFinal: false, 0: { transcript: 'phở chay' } }]); // interim only
    sr.pendingFinal = [{ isFinal: true, 0: { transcript: 'phở chay' } }]; // flush on stop
    vi.stop();

    const finals = finalsOf(calls);
    assert.deepEqual(finals, ['phở chay']);
    assert.ok(!finals[0].includes('phở chay phở chay'), 'tail must not double-append');
});

test('multi-segment — finals + interim rebuild, no segment counted twice', () => {
    const { calls, ref } = makeRef();
    vi.start(ref);
    const sr = FakeSR.instance;

    // Utterance 1 final; utterance 2 interim then final — result list grows.
    sr.emit([{ isFinal: true, 0: { transcript: 'phở' } }]);
    sr.emit([
        { isFinal: true, 0: { transcript: 'phở' } },
        { isFinal: false, 0: { transcript: 'chay' } },
    ]);
    sr.emit([
        { isFinal: true, 0: { transcript: 'phở' } },
        { isFinal: true, 0: { transcript: 'chay' } },
    ]);
    vi.stop(); // onend → finalize

    const finals = finalsOf(calls);
    assert.deepEqual(finals, ['phở chay']);
    const lastInterim = interimsOf(calls).at(-1);
    assert.equal(lastInterim, 'phở chay');
});

test('silence timer — auto-submits then ends once', async () => {
    const { calls, ref } = makeRef();
    vi.start(ref);
    const sr = FakeSR.instance;
    sr.emit([{ isFinal: true, 0: { transcript: 'xin chào' } }]);

    await new Promise((r) => setTimeout(r, 2800)); // SILENCE_DELAY_MS = 2500

    assert.deepEqual(finalsOf(calls), ['xin chào']);
    assert.ok(calls.some(([n]) => n === 'OnVoiceEnd'));
});

test('mic hand-off — second start() steals the session from the first ref', () => {
    const a = makeRef();
    const b = makeRef();
    vi.start(a.ref);
    const srA = FakeSR.instance;
    vi.start(b.ref); // aborts session A

    srA.emit([{ isFinal: true, 0: { transcript: 'cũ' } }]); // dead session emits
    assert.equal(a.calls.length, 0, 'handed-off ref must get no callbacks');

    FakeSR.instance.emit([{ isFinal: true, 0: { transcript: 'mới' } }]);
    vi.stop();
    assert.deepEqual(finalsOf(b.calls), ['mới']);
});

test('error — no auto-submit of partial text, localized message sent', () => {
    const { calls, ref } = makeRef();
    vi.start(ref);
    const sr = FakeSR.instance;
    sr.emit([{ isFinal: false, 0: { transcript: 'phở' } }]);
    sr.emitError('not-allowed');
    sr.onend?.();

    assert.equal(finalsOf(calls).length, 0, 'error clears partial transcript');
    const err = calls.find(([n]) => n === 'OnVoiceError');
    assert.ok(err && err[1].includes('micro'), `expected localized error, got ${err?.[1]}`);
});
